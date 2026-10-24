using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 剧情对话 UI：底部横条 + 头像 + 说话人 + 正文 + 选项。
    ///
    /// 与 <see cref="MapDialogueView"/> 的区别：那个是「活动结果面板」，显示奖励清单；
    /// 这个是真正的逐句对白播放器，数据源是 <see cref="DialogueData"/>。
    ///
    /// 用法：
    ///   view.Show(dialogueData, () => { /* 播完回调 */ });
    /// 剧情组只负责填 DialogueData 资产，不需要碰这里。
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        // 参考分辨率 1920×1080，与存档提示栏同一个口径。
        private const float PanelWidth = 1200f;
        private const float PanelHeight = 340f;
        private const float PanelBottom = 60f;
        private const float PortraitSize = 200f;
        // 2026-10-24：3 → 4。原先 BuildOptions 里硬编码 Min(count, 3)，第 4 个选项被静默吞掉。
        private const int MaxVisibleOptions = 4;
        private const float OptionHeight = 92f;
        private const float OptionSpacing = 12f;
        private const float OptionCardWidth = 820f;
        private const float OptionCardPadding = 16f;
        private const float TypewriterCharsPerSecond = 45f;

        public UIThemeConfig theme;
        public GameSessionState sessionState;

        /// <summary>当前对话归属的 NPC（由 DialogueRunner 在 Play 时写入），好感度小心心按它显示。</summary>
        public NpcData currentNpc;

        /// <summary>
        /// 送礼流程中置 true：背包选择器开着，对话面板不能再被点击推进
        /// （否则会把菜单点结束，等玩家挑完道具回来时流程已经散了）。
        /// </summary>
        public bool suppressAdvance;
        public GameObject panelRoot;
        public PixelPanel panelStyle;
        public CanvasGroup canvasGroup;
        public Image portraitImage;
        public Image portraitFrame;
        public Text speakerText;
        public Text bodyText;
        public Text continueHint;
        public RectTransform optionsRoot;
        public Button advanceButton;

        private DialogueData currentDialogue;
        private Action onCompleted;
        private int lineIndex = -1;
        private bool typing;
        private Coroutine typeRoutine;
        private readonly List<Button> optionButtons = new List<Button>();
        private Image[] heartImages;
        private RectTransform heartsRoot;

        private void Awake()
        {
            ResolveReferences();
        }

        public void BuildDefaultLayout(RectTransform parent)
        {
            if (parent == null || panelRoot != null)
            {
                return;
            }

            GameObject rootObject = new GameObject("DialogueView", typeof(RectTransform));
            RectTransform rootRect = rootObject.transform as RectTransform;
            rootRect.SetParent(parent, false);
            rootRect.anchorMin = new Vector2(0.5f, 0f);
            rootRect.anchorMax = new Vector2(0.5f, 0f);
            rootRect.pivot = new Vector2(0.5f, 0f);
            rootRect.anchoredPosition = new Vector2(0f, PanelBottom);
            rootRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRoot = rootObject;

            Image background = rootObject.AddComponent<Image>();
            background.raycastTarget = true;
            panelStyle = rootObject.AddComponent<PixelPanel>();
            panelStyle.background = background;
            panelStyle.variant = GameUiPanelVariant.Standard;
            canvasGroup = UiAnimationHelper.EnsureCanvasGroup(rootObject);

            // 整条可点：点哪里都算「继续」，和传统 RPG 一致。
            advanceButton = rootObject.AddComponent<Button>();
            advanceButton.targetGraphic = background;
            advanceButton.transition = Selectable.Transition.None;
            advanceButton.onClick.AddListener(Advance);

            portraitFrame = CreateImage("PortraitFrame", rootRect, new Vector2(24f, -24f), new Vector2(PortraitSize + 8f, PortraitSize + 8f));
            portraitImage = CreateImage("Portrait", rootRect, new Vector2(28f, -28f), new Vector2(PortraitSize, PortraitSize));

            speakerText = CreateText("Speaker", rootRect, string.Empty, 26, FontStyle.Bold, TextAnchor.MiddleLeft);
            SetRect(speakerText.rectTransform, new Vector2(260f, -24f), new Vector2(560f, 40f), new Vector2(0f, 1f));

            bodyText = CreateText("Body", rootRect, string.Empty, 24, FontStyle.Normal, TextAnchor.UpperLeft);
            bodyText.supportRichText = true;
            bodyText.lineSpacing = 1.15f;
            SetRect(bodyText.rectTransform, new Vector2(260f, -76f), new Vector2(900f, 100f), new Vector2(0f, 1f));

            // 选项不再塞在对话框里，而是屏幕正中一块独立卡片：
            // 对话时三项选择更醒目，也不会把正文挤掉。
            GameObject optionsObject = new GameObject("Options", typeof(RectTransform));
            optionsRoot = optionsObject.transform as RectTransform;
            optionsRoot.SetParent(parent, false);
            optionsRoot.anchorMin = new Vector2(0.5f, 0.5f);
            optionsRoot.anchorMax = new Vector2(0.5f, 0.5f);
            optionsRoot.pivot = new Vector2(0.5f, 0.5f);
            optionsRoot.anchoredPosition = Vector2.zero;
            optionsRoot.sizeDelta = new Vector2(OptionCardWidth, MaxVisibleOptions * (OptionHeight + OptionSpacing) + OptionCardPadding);

            Image optionsBackdrop = optionsObject.AddComponent<Image>();
            optionsBackdrop.color = new Color(0f, 0f, 0f, 0.8f);
            optionsBackdrop.raycastTarget = true;
            optionsObject.SetActive(false);

            continueHint = CreateText("ContinueHint", rootRect, "▼", 22, FontStyle.Bold, TextAnchor.MiddleRight);
            SetRect(continueHint.rectTransform, new Vector2(1000f, -296f), new Vector2(160f, 30f), new Vector2(0f, 1f));

            ApplyStyle();
            EnsureHeartsRow();
            SetVisible(false);
        }

        public void Show(DialogueData dialogue, Action completed = null)
        {
            if (dialogue == null || dialogue.lines == null || dialogue.lines.Count == 0)
            {
                completed?.Invoke();
                return;
            }

            ResolveReferences();
            currentDialogue = dialogue;
            onCompleted = completed;
            lineIndex = -1;
            ShowLine(0);
            SetVisible(true);
            RefreshNpcHearts();
            Sfx.Ui.Open();
        }

        public void Hide()
        {
            StopTypewriter();
            // 选项卡片已经搬到屏幕中央，不在对话框里，收起时必须一起清掉。
            ClearOptions();
            SetVisible(false);
            currentDialogue = null;
            onCompleted = null;
            lineIndex = -1;
        }

        /// <summary>点击/按键推进。正在打字时先补全当前句，不跳句。</summary>
        public void Advance()
        {
            if (currentDialogue == null || suppressAdvance)
            {
                return;
            }

            if (typing)
            {
                CompleteTypewriter();
                return;
            }

            DialogueLine current = CurrentLine();
            if (current != null && current.HasOptions())
            {
                // 有选项时必须先选，点空白不推进，避免误触跳过分支。
                return;
            }

            if (lineIndex + 1 >= currentDialogue.lines.Count)
            {
                Finish();
                return;
            }

            ShowLine(lineIndex + 1);
        }

        private void ShowLine(int index)
        {
            if (currentDialogue == null || index < 0 || index >= currentDialogue.lines.Count)
            {
                Finish();
                return;
            }

            lineIndex = index;
            DialogueLine line = currentDialogue.lines[index];
            Sfx.World.PageFlip();
            RefreshNpcHearts();

            if (speakerText != null)
            {
                speakerText.text = line.ResolveSpeaker(currentDialogue.speakerName);
            }

            ApplyPortrait(line.ResolvePortrait(currentDialogue.portrait));
            ClearOptions();

            bool hasOptions = line.HasOptions();
            if (continueHint != null)
            {
                continueHint.gameObject.SetActive(!hasOptions);
            }

            StartTypewriter(line.text ?? string.Empty);

            if (hasOptions)
            {
                BuildOptions(line);
            }
        }

        private DialogueLine CurrentLine()
        {
            if (currentDialogue == null || lineIndex < 0 || lineIndex >= currentDialogue.lines.Count)
            {
                return null;
            }

            return currentDialogue.lines[lineIndex];
        }

        private void StartTypewriter(string fullText)
        {
            StopTypewriter();
            if (bodyText == null)
            {
                return;
            }

            bodyText.text = fullText;
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            typing = true;
            bodyText.text = string.Empty;
            typeRoutine = StartCoroutine(TypeRoutine(fullText));
        }

        private IEnumerator TypeRoutine(string fullText)
        {
            float accumulated = 0f;
            int shown = 0;
            while (shown < fullText.Length)
            {
                accumulated += Time.unscaledDeltaTime * TypewriterCharsPerSecond;
                int target = Mathf.Min(fullText.Length, Mathf.FloorToInt(accumulated));
                if (target > shown)
                {
                    shown = target;
                    bodyText.text = fullText.Substring(0, shown);
                }

                yield return null;
            }

            bodyText.text = fullText;
            typing = false;
            typeRoutine = null;
        }

        private void CompleteTypewriter()
        {
            DialogueLine line = CurrentLine();
            StopTypewriter();
            if (bodyText != null && line != null)
            {
                bodyText.text = line.text ?? string.Empty;
            }
        }

        private void StopTypewriter()
        {
            if (typeRoutine != null)
            {
                StopCoroutine(typeRoutine);
                typeRoutine = null;
            }

            typing = false;
        }

        private void BuildOptions(DialogueLine line)
        {
            if (optionsRoot == null || line == null || line.options == null)
            {
                return;
            }

            int visible = Mathf.Min(line.options.Count, MaxVisibleOptions);
            if (visible > 0 && optionsRoot != null)
            {
                float height = visible * (OptionHeight + OptionSpacing) + OptionCardPadding;
                optionsRoot.sizeDelta = new Vector2(OptionCardWidth, height);

                // 2026-10-24：卡片默认屏幕居中，选项一多底边会压进底部对话框
                // （4 选项高 432px，盖住说话人并蹭到正文）。约束：底边最多
                // 下探到「对话框顶部 + 24px」，需要时整体上移补足。
                RectTransform canvasRect = optionsRoot.parent as RectTransform;
                float halfHeight = canvasRect != null ? canvasRect.rect.height * 0.5f : 540f;
                float centeredBottom = halfHeight - height * 0.5f;
                float minBottom = PanelBottom + PanelHeight + 24f;
                float lift = Mathf.Max(0f, minBottom - centeredBottom);
                optionsRoot.anchoredPosition = new Vector2(0f, lift);

                optionsRoot.gameObject.SetActive(true);
            }
            if (visible < line.options.Count)
            {
                // 超出上限的选项不会显示，剧情组很难发现——必须在日志里留痕。
                Debug.LogWarning("[DialogueView] 选项数 " + line.options.Count + " 超过上限 "
                    + MaxVisibleOptions + "，多余选项未显示。");
            }
            for (int i = 0; i < visible; i++)
            {
                DialogueOption option = line.options[i];
                if (option == null)
                {
                    continue;
                }

                Button button = CreateOptionButton(option, i);
                optionButtons.Add(button);
            }
        }

        private Button CreateOptionButton(DialogueOption option, int index)
        {
            GameObject buttonObject = new GameObject("Option_" + index, typeof(RectTransform));
            RectTransform rect = buttonObject.transform as RectTransform;
            rect.SetParent(optionsRoot, false);
            rect.SetParent(optionsRoot, false);
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(OptionCardPadding * 0.5f) - index * (OptionHeight + OptionSpacing));
            rect.sizeDelta = new Vector2(OptionCardWidth - OptionCardPadding * 2f, OptionHeight);

            Image background = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;

            Text label = CreateText("Text", rect, string.IsNullOrEmpty(option.text) ? "（未命名选项）" : option.text,
                26, FontStyle.Normal, TextAnchor.MiddleLeft);
            label.rectTransform.anchorMin = Vector2.zero;
            label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.offsetMin = new Vector2(44f, 0f);
            label.rectTransform.offsetMax = new Vector2(-18f, 0f);

            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.background = background;
            pixelButton.labelText = label;
            pixelButton.theme = theme;
            pixelButton.ApplyStyle();

            bool unlocked = EvaluateRequirements(option.requirements);
            button.interactable = unlocked;
            if (!unlocked)
            {
                // 2026-10-24：置灰的同时说明原因，玩家不用猜「为什么选不了」。
                string reason = FormatRequirementText(option.requirements);
                if (!string.IsNullOrEmpty(reason))
                {
                    label.text += "\n<size=16>未满足：" + reason + "</size>";
                }

                label.color = new Color(label.color.r, label.color.g, label.color.b, 0.45f);
            }

            DialogueOption captured = option;
            button.onClick.AddListener(() => Choose(captured));
            return button;
        }

        private void Choose(DialogueOption option)
        {
            if (option == null || currentDialogue == null)
            {
                return;
            }

            if (!EvaluateRequirements(option.requirements))
            {
                return;
            }

            // 奖励发放交给外部（DialogueRunner），这里只负责回调，避免 UI 直接改存档。
            Sfx.Ui.Confirm();
            onOptionChosen?.Invoke(option);

            if (option.chooseItemAsGift)
            {
                // 2026-10-24 修复：不能现在就跳 nextDialogueId——那会变成
                // 「还没送出就先道谢」。先清掉选项等背包，选中后由 Runner 播收礼对话，
                // 取消则回到本菜单。
                ClearOptions();
                return;
            }

            if (!string.IsNullOrEmpty(option.nextDialogueId))
            {
                onJumpRequested?.Invoke(option.nextDialogueId);
                return;
            }

            Finish();
        }

        /// <summary>玩家选了某个选项。由 DialogueRunner 接过去发奖励。</summary>
        public event Action<DialogueOption> onOptionChosen;

        /// <summary>选项要求跳转到另一段对话。</summary>
        public event Action<string> onJumpRequested;

        private bool EvaluateRequirements(List<EventConditionData> requirements)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return true;
            }

            // 好感度条件需要整张会话状态；sessionState 为空时条件自动通过（与旧行为一致）。
            return EventConditionResolver.AreConditionsMet(requirements, sessionState);
        }

        /// <summary>把条件清单转成一行可读文本，如「压力 ≥ 30、与「图书管理员」好感度 ≥ 10」。</summary>
        private static string FormatRequirementText(List<EventConditionData> requirements)
        {
            if (requirements == null || requirements.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            foreach (EventConditionData requirement in requirements)
            {
                if (requirement == null)
                {
                    continue;
                }

                string subject = requirement.subject == ConditionSubject.NpcRelationship
                    ? "与「" + NpcCatalog.DisplayName(requirement.npcId) + "」好感度"
                    : MapInteractionPromptFormatter.FormatStatName(requirement.statKey);
                parts.Add(subject + " " + ComparisonSymbol(requirement.comparison) + " " + requirement.value);
            }

            return string.Join("、", parts.ToArray());
        }

        private static string ComparisonSymbol(ComparisonType comparison)
        {
            switch (comparison)
            {
                case ComparisonType.GreaterOrEqual:
                    return "≥";
                case ComparisonType.LessOrEqual:
                    return "≤";
                case ComparisonType.Equal:
                    return "=";
                case ComparisonType.NotEqual:
                    return "≠";
                default:
                    return "?";
            }
        }

        private void Finish()
        {
            Action callback = onCompleted;
            Hide();
            callback?.Invoke();
        }

        private void ClearOptions()
        {
            for (int i = 0; i < optionButtons.Count; i++)
            {
                if (optionButtons[i] != null)
                {
                    Destroy(optionButtons[i].gameObject);
                }
            }

            optionButtons.Clear();

            if (optionsRoot == null)
            {
                return;
            }

            for (int i = optionsRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = optionsRoot.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            optionsRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------
        // 好感度小心心（2026-10-24）：头像下方一行 5 颗，每 20 点点亮一颗。
        // 心形是程序生成的 7×6 像素图，不依赖字体字形和美术资源；
        // 对话 UI 预制体与代码构建两条路径都靠 EnsureHeartsRow 兜底。
        // ------------------------------------------------------------------
        private static Sprite heartSprite;
        private static readonly string[] HeartPattern =
        {
            ".##.##.",
            "#######",
            "#######",
            ".#####.",
            "..###..",
            "...#..."
        };

        public void RefreshNpcHearts()
        {
            if (heartImages == null || heartImages.Length == 0)
            {
                return;
            }

            bool show = currentNpc != null
                && !string.IsNullOrEmpty(currentNpc.npcId)
                && sessionState != null;
            if (heartsRoot != null)
            {
                heartsRoot.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            EnsureHeartsRow();
            int value = sessionState.npcRelationships.Get(currentNpc.npcId);
            int filled = Mathf.Clamp(value / 20, 0, heartImages.Length);
            for (int i = 0; i < heartImages.Length; i++)
            {
                heartImages[i].color = i < filled
                    ? new Color(0.91f, 0.33f, 0.33f, 1f)
                    : new Color(0f, 0f, 0f, 0.35f);
            }
        }

        private void EnsureHeartsRow()
        {
            if (heartImages != null && heartImages.Length > 0)
            {
                return;
            }

            RectTransform parent = panelRoot != null
                ? panelRoot.transform as RectTransform
                : transform as RectTransform;
            if (parent == null)
            {
                return;
            }

            GameObject rowObject = new GameObject("NpcHearts", typeof(RectTransform));
            heartsRoot = rowObject.transform as RectTransform;
            heartsRoot.SetParent(parent, false);
            heartsRoot.anchorMin = heartsRoot.anchorMax = new Vector2(0f, 1f);
            heartsRoot.pivot = new Vector2(0f, 1f);
            heartsRoot.anchoredPosition = new Vector2(28f, -236f);
            heartsRoot.sizeDelta = new Vector2(125f, 18f);

            Sprite heart = EnsureHeartSprite();
            heartImages = new Image[5];
            for (int i = 0; i < heartImages.Length; i++)
            {
                GameObject heartObject = new GameObject("Heart_" + i, typeof(RectTransform));
                RectTransform rect = heartObject.transform as RectTransform;
                rect.SetParent(heartsRoot, false);
                rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(i * 25f, 0f);
                rect.sizeDelta = new Vector2(21f, 18f);
                Image image = heartObject.AddComponent<Image>();
                image.sprite = heart;
                image.raycastTarget = false;
                heartImages[i] = image;
            }

            heartsRoot.gameObject.SetActive(false);
        }

        private static Sprite EnsureHeartSprite()
        {
            if (heartSprite != null)
            {
                return heartSprite;
            }

            int width = HeartPattern[0].Length;
            int height = HeartPattern.Length;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    texture.SetPixel(x, height - 1 - y, HeartPattern[y][x] == '#' ? Color.white : Color.clear);
                }
            }

            texture.Apply();
            heartSprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
            return heartSprite;
        }

        private void ApplyPortrait(Sprite sprite)
        {
            if (portraitImage != null)
            {
                portraitImage.sprite = sprite;
                portraitImage.enabled = sprite != null;
                portraitImage.preserveAspect = true;
                portraitImage.color = sprite == null ? Color.clear : Color.white;
            }

            if (portraitFrame != null)
            {
                portraitFrame.enabled = sprite != null;
            }
        }

        public void ApplyStyle()
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (panelStyle != null)
            {
                panelStyle.theme = resolved;
                panelStyle.ApplyStyle();
            }

            GameUiStyle.ApplyPixelText(speakerText, resolved, GameUiTextStyle.Subtitle);
            GameUiStyle.ApplyPixelText(bodyText, resolved, GameUiTextStyle.Body);
            GameUiStyle.ApplyPixelText(continueHint, resolved, GameUiTextStyle.SmallText);

            if (portraitFrame != null)
            {
                portraitFrame.color = new Color(0f, 0f, 0f, 0.35f);
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
        }

        private void ResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panelStyle == null && panelRoot != null)
            {
                panelStyle = panelRoot.GetComponent<PixelPanel>();
            }

            if (canvasGroup == null && panelRoot != null)
            {
                canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panelRoot);
            }
        }

        private static Image CreateImage(string name, RectTransform parent, Vector2 position, Vector2 size)
        {
            GameObject imageObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = imageObject.transform as RectTransform;
            rect.SetParent(parent, false);
            SetRect(rect, position, size, new Vector2(0f, 1f));

            Image image = imageObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.enabled = false;
            return image;
        }

        private static Text CreateText(string name, RectTransform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = textObject.transform as RectTransform;
            rect.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = anchor;
            text.raycastTarget = false;
            GameUiStyle.ApplyTextFromFontSpec(text, null, fontSize, fontStyle);
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchor)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0f, anchor.y > 0.5f ? 1f : 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}
