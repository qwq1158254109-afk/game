using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 扭蛋抽奖界面——街机全屏机台版（Docs/CapsuleGachaDesign-2026-10-24.md §4）。
    /// 不是「面板 + 按钮」：整屏就是一台大扭蛋机，点击机器本身完成抽奖。
    /// 演出链：投币抛物线 → 旋钮转动 + 罩内扭蛋翻滚 → 出蛋弹跳 → 摇蛋 →
    /// 蛋缝漏光预告品质（白/蓝/金）→ 炸壳 + 集中线 + 粒子 → 卡牌翻面浮现。
    /// 演出中点击机器 = 跳过。素材全部程序生成（48×64 机台、蛋壳、放射线），
    /// 真实美术到位后换 Sprite 引用即可。
    /// </summary>
    public sealed class CapsuleView : MonoBehaviour
    {
        // 参考分辨率 1920×1080 下的布局常量。机台上移腾出下方演出区：
        // 机台占 +355..-235，出蛋口对齐贴图下沿（-115），扭蛋停在机台脚下 -270。
        private const float MachineWidth = 440f;
        private const float MachineHeight = 590f;
        private const float MachineCenterY = 60f;
        private const float ChuteX = 0f;
        private const float ChuteY = -115f;
        private const float CapsuleRestY = -270f;
        private const float CardWidth = 250f;
        private const float CardHeight = 340f;

        public UIThemeConfig theme;
        public GameSessionState sessionState;

        private RectTransform rootRect;
        private CanvasGroup canvasGroup;
        private Image backdrop;
        private RectTransform machineRect;
        private Image machineImage;
        private RectTransform capsuleRect;
        private Image capsuleImage;
        private Image capsuleGlow;
        private RectTransform cardRect;
        private Image cardImage;
        private Text cardNameText;
        private Text cardCostText;
        private Text resultText;
        private Text moneyText;
        private Text hintText;
        private Text leaveButtonText;
        private PixelButton leavePixelButton;
        private Button pullButton;
        private Text pullButtonText;
        private PixelButton pullPixelButton;
        private RectTransform textStripRect;
        private RectTransform historyRoot;
        private Text historySummaryText;
        private Text historyEntriesText;
        private RectTransform burstRect;
        private Image burstImage;
        private RectTransform shellLeftRect;
        private RectTransform shellRightRect;
        private readonly List<Image> particles = new List<Image>();

        private CapsulePackData pack;
        private List<CardData> pool;
        private bool animating;
        private bool skipRequested;
        /// <summary>动画看门狗：演出协程意外死掉时强制复位，避免 animating 卡 true。</summary>
        private float animatingDeadline = -1f;
        private static bool legacyInputBlocked;

        private static Sprite machineSprite;
        private static Sprite capsuleSprite;
        private static Sprite cardBackSprite;
        private static Sprite shellSprite;
        private static Sprite burstSprite;
        private static Sprite particleSprite;

        /// <summary>
        /// 场景里没有就现建。2026-10-25 关键修复：不再复用 MapUiFactory.EnsureRuntimeCanvas——
        /// 它会抓场景里「任意一块」画布，商店场景有多块，挂到没有 GraphicRaycaster 的画布上
        /// 就表现为「界面看得见但按钮全点不动」。改为自建专属画布（同对话系统的做法）：
        /// 自带射线组件、sortingOrder 230 压过对话(215)与 HUD、参考分辨率与布局常数一致。
        /// </summary>
        public static CapsuleView Ensure()
        {
            // 先只找激活的：FindAnyObjectByType 不保证优先返回激活实例，
            // 拿到未激活的实例会让 Show() 之后界面仍然看不见。
            CapsuleView existing = FindAnyObjectByType<CapsuleView>();
            if (existing == null)
            {
                existing = FindAnyObjectByType<CapsuleView>(FindObjectsInactive.Include);
            }

            if (existing != null)
            {
                return existing;
            }

            GameObject canvasObject = new GameObject("[UI] CapsuleCanvas", typeof(RectTransform));
            Canvas canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 230;
            CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            RectTransform rect = canvasObject.transform as RectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            CapsuleView view = canvasObject.AddComponent<CapsuleView>();
            view.BuildDefaultLayout();
            return view;
        }

        public void Open(GameSessionState state, CapsulePackData packData, List<CardData> cardPool)
        {
            sessionState = state;
            pack = packData;
            pool = cardPool;
            skipRequested = false;
            animating = false;
            ResetStage();
            SetVisible(true);
            ApplyStyles();
            RefreshHeader();
            Sfx.Ui.Open();
            GameplayInputGate.Acquire(this);
        }

        /// <summary>
        /// 用「已解析主题」给全部文字过一遍字体与配色。创建时传的 theme 可能为 null，
        /// 不补这一步文字会没有字体（按钮上的字看不见的根源，2026-10-24）。
        /// DialogueView 的 ApplyStyle 做的就是这件事。
        /// </summary>
        private void ApplyStyles()
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            // 全部文字显式白色：本界面背景是深色（压黑幕/深灰按钮），主题的
            // 按钮反向色在此不可见（2026-10-25 按钮字/提示看不见的修复）。
            ApplyTextWhite(moneyText, resolved, GameUiTextStyle.Subtitle);
            ApplyTextWhite(hintText, resolved, GameUiTextStyle.Body);
            ApplyTextWhite(cardNameText, resolved, GameUiTextStyle.Title);
            ApplyTextWhite(cardCostText, resolved, GameUiTextStyle.Body);
            ApplyTextWhite(resultText, resolved, GameUiTextStyle.Subtitle);
            ApplyTextWhite(pullButtonText, resolved, GameUiTextStyle.ButtonText);
            ApplyTextWhite(leaveButtonText, resolved, GameUiTextStyle.ButtonText);
        }

        private static void ApplyTextWhite(Text text, UIThemeConfig resolved, GameUiTextStyle style)
        {
            if (text == null)
            {
                return;
            }

            GameUiStyle.ApplyPixelText(text, resolved, style);
            text.color = Color.white;
            // 像素字体在主题的 Truncate 设置下高/宽不足时会被裁切挤压
            // （「费用 2」被挤扁，2026-10-25）；放开启用溢出显示。
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            StopAllCoroutines();
            animating = false;
            // 释放 EventSystem 选中态：Close 时指针不会正常抬起到按钮上，
            // PixelButton 会停在按压/选中的视觉状态（2026-10-25 用户反馈）。
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            SetVisible(false);
            GameplayInputGate.Release(this, true);
        }

        public bool IsOpen => gameObject.activeSelf;

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            // 逃生口一：ESC 关闭。记录面板开着时先关记录，再退整个界面。
            if (EscapePressed())
            {
                if (HistoryIsOpen)
                {
                    CloseHistory();
                }
                else
                {
                    Close();
                }

                return;
            }

            // 逃生口二：看门狗。演出协程被外部 StopAllCoroutines 之类打断后
            // animating 会永远停在 true（按钮全灭）。超过期限强制复位。
            if (animating && animatingDeadline > 0f && Time.unscaledTime > animatingDeadline)
            {
                Debug.LogWarning("[CapsuleView] 演出超时，看门狗强制复位。");
                animating = false;
                skipRequested = false;
                animatingDeadline = -1f;
                ResetStage();
            }

            // 点击调试探测（2026-10-25「按钮点不动」排查用）：打印每次点击命中的
            // UI 对象链。按钮失效时看这条日志即可定位是哪层挡了射线。确认修复后移除。
            if (MousePressedThisFrame())
            {
                LogPointerHits();
            }
        }

        private static bool MousePressedThisFrame()
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetMouseButtonDown(0))
                    {
                        return true;
                    }
                }
                catch (System.InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            return false;
        }

        private static void LogPointerHits()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                Debug.LogWarning("[CapsuleView] 场景里没有 EventSystem，UI 点击全部失效。");
                return;
            }

            Vector2 position = Input.mousePosition;
            PointerEventData eventData = new PointerEventData(eventSystem) { position = position };
            List<RaycastResult> results = new List<RaycastResult>();
            eventSystem.RaycastAll(eventData, results);
            if (results.Count == 0)
            {
                Debug.LogWarning("[CapsuleView] 点击(" + position + ") 未命中任何 UI——射线被挡或坐标在界面外。");
                return;
            }

            List<string> names = new List<string>();
            for (int i = 0; i < results.Count && i < 5; i++)
            {
                names.Add(results[i].gameObject.name);
            }

            Debug.Log("[CapsuleView] 点击(" + position + ") 命中：" + string.Join(" <- ", names.ToArray()));
        }

        private static bool EscapePressed()
        {
            if (!legacyInputBlocked)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.Escape))
                    {
                        return true;
                    }
                }
                catch (System.InvalidOperationException)
                {
                    legacyInputBlocked = true;
                }
            }

            return ReadInputSystemKey("escapeKey", "wasPressedThisFrame");
        }

        /// <summary>反射读新 Input System，与 InventoryController 的既有模式一致。</summary>
        private static bool ReadInputSystemKey(string keyPropertyName, string statePropertyName)
        {
            System.Type keyboardType = System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
            if (keyboardType == null)
            {
                return false;
            }

            object keyboard = keyboardType.GetProperty("current")?.GetValue(null, null);
            if (keyboard == null)
            {
                return false;
            }

            object key = keyboardType.GetProperty(keyPropertyName)?.GetValue(keyboard, null);
            if (key == null)
            {
                return false;
            }

            return (bool)(keyboardType.Assembly.GetType("UnityEngine.InputSystem.Controls.ButtonControl")
                ?.GetProperty(statePropertyName)?.GetValue(key, null) ?? false);
        }

        // ------------------------------------------------------------------
        // 抽卡
        // ------------------------------------------------------------------

        private void TryPull()
        {
            if (animating || pack == null || sessionState == null || sessionState.playerState == null)
            {
                return;
            }

            int money = sessionState.playerState.GetStat(PlayerStatKey.Money);
            if (money < pack.price)
            {
                if (resultText != null)
                {
                    resultText.text = "金钱不足（需要 " + pack.price + " 金）";
                }

                Sfx.Ui.Click();
                return;
            }

            System.Random rng = new System.Random();
            CapsuleDrawService.DrawOutcome outcome = CapsuleDrawService.Roll(
                pack, pool, !sessionState.capsulePityUsed, sessionState.deckState.masterDeck, rng);

            if (outcome.card == null)
            {
                Debug.LogWarning("[CapsuleView] 卡池为空，本次未扣费。");
                return;
            }

            sessionState.playerState.AddStat(PlayerStatKey.Money, -pack.price);
            if (outcome.usedPity)
            {
                sessionState.capsulePityUsed = true;
            }

            sessionState.deckState.AddCardToMasterDeck(outcome.card);
            sessionState.capsulePulls.Add(new CapsulePullEntry
            {
                cardId = outcome.card.cardId,
                cardName = outcome.card.displayName,
                rarity = (int)outcome.card.rarity,
                duplicate = outcome.duplicate
            });
            if (sessionState.capsulePulls.Count > 200)
            {
                sessionState.capsulePulls.RemoveAt(0);
            }
            string suffix;
            if (outcome.duplicate)
            {
                if (pack.duplicateRefund > 0)
                {
                    sessionState.playerState.AddStat(PlayerStatKey.Money, pack.duplicateRefund);
                }

                suffix = outcome.duplicate && pack.duplicateRefund > 0
                    ? "（重复卡·返还 " + pack.duplicateRefund + " 金）"
                    : "（重复卡）";
            }
            else
            {
                suffix = "（已加入卡组）";
            }

            RefreshHeader();
            StartCoroutine(PlayReveal(outcome.card, suffix));
        }

        /// <summary>演出主时间轴；skipRequested 置位时各阶段直接跳到终点。</summary>
        private IEnumerator PlayReveal(CardData card, string resultSuffix)
        {
            animating = true;
            skipRequested = false;
            animatingDeadline = Time.unscaledTime + 12f;
            RefreshHeader();
            ResetStage();
            if (resultText != null)
            {
                resultText.text = string.Empty;
            }

            // ── 1. 机器晃动（简化版演出：去掉投币金币/旋钮/罩内扭蛋这类
            //    依赖像素级对齐的可动件，它们只要偏一点就很怪）
            Sfx.Ui.Click();
            Vector3 machineBase = machineRect.localPosition;
            yield return Tween(0.6f, p =>
            {
                machineRect.localPosition = machineBase
                    + new Vector3(Mathf.Sin(Time.unscaledTime * 42f) * 6f * (1f - p), 0f, 0f);
            });
            machineRect.localPosition = machineBase;

            // ── 3. 出蛋：从机台下沿掉出，一次弹跳后停在机台脚下
            Sfx.World.PageFlip();
            capsuleRect.gameObject.SetActive(true);
            capsuleRect.localPosition = new Vector3(ChuteX, ChuteY, 0f);
            capsuleRect.localScale = Vector3.one;
            capsuleImage.color = Color.white;
            capsuleGlow.gameObject.SetActive(false);
            yield return Tween(0.6f, p =>
            {
                float y;
                if (p < 0.5f)
                {
                    // 掉落
                    y = Mathf.Lerp(ChuteY, CapsuleRestY, p / 0.5f);
                }
                else
                {
                    // 落地后弹一次（高度 35px 递减到 0）
                    float bounce = (p - 0.5f) / 0.5f;
                    y = CapsuleRestY + Mathf.Abs(Mathf.Sin(bounce * Mathf.PI)) * 35f * (1f - bounce * 0.4f);
                }

                capsuleRect.localPosition = new Vector3(ChuteX, y, 0f);
            });
            capsuleRect.localPosition = new Vector3(ChuteX, CapsuleRestY, 0f);

            // ── 4. 摇蛋：越来越剧烈
            yield return Tween(0.5f, p =>
            {
                float amp = 4f + p * 10f;
                capsuleRect.localPosition = new Vector3(
                    ChuteX + Mathf.Sin(Time.unscaledTime * 55f) * amp, CapsuleRestY, 0f);
                capsuleRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 55f) * amp * 0.7f);
            });
            capsuleRect.localRotation = Quaternion.identity;

            // ── 5. 漏光预告：白=普通 蓝=非凡 金=稀有（爽感核心，稀有加屏幕低鸣震动）
            Color glow = RarityGlowColor(card.rarity);
            float suspense = card.rarity == CardRarity.Rare ? 1.4f : card.rarity == CardRarity.Uncommon ? 0.8f : 0.3f;
            capsuleGlow.gameObject.SetActive(true);
            capsuleGlow.color = new Color(glow.r, glow.g, glow.b, 0f);
            yield return Tween(suspense, p =>
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
                capsuleGlow.color = new Color(glow.r, glow.g, glow.b, Mathf.Lerp(0.25f, 0.95f, p) * (0.6f + 0.4f * pulse));
                capsuleGlow.rectTransform.localScale = Vector3.one * (1.15f + pulse * 0.25f);
                if (card.rarity == CardRarity.Rare)
                {
                    machineRect.localPosition = machineBase + new Vector3(Mathf.Sin(Time.unscaledTime * 70f) * 3f, 0f, 0f);
                }
            });
            machineRect.localPosition = machineBase;

            // ── 6. 炸壳：蛋壳两半飞出 + 集中线 + 粒子（稀有全屏金闪）
            capsuleRect.gameObject.SetActive(false);
            capsuleGlow.gameObject.SetActive(false);
            SpawnShellHalves();
            SpawnParticles(card.rarity);
            Sfx.Ui.Confirm();
            if (card.rarity == CardRarity.Rare)
            {
                StartCoroutine(FlashBackdrop());
            }

            StartCoroutine(PlayBurst(card.rarity));

            // ── 7. 卡牌从光里浮现：卡背弹出 → 翻面（衬底条随卡出现）
            if (textStripRect != null)
            {
                textStripRect.gameObject.SetActive(true);
            }

            cardRect.gameObject.SetActive(true);
            cardImage.sprite = EnsureCardBackSprite();
            cardImage.color = Color.white;
            cardNameText.text = string.Empty;
            cardCostText.text = string.Empty;
            yield return Tween(0.25f, p =>
            {
                cardRect.localScale = Vector3.Lerp(Vector3.one * 0.3f, Vector3.one, p);
            });

            bool swapped = false;
            yield return Tween(0.25f, p =>
            {
                cardRect.localScale = new Vector3(Mathf.Abs(Mathf.Cos(p * Mathf.PI)), 1f, 1f);
                if (!swapped && p >= 0.5f)
                {
                    swapped = true;
                    ApplyCardFace(card);
                    if (card.rarity == CardRarity.Rare)
                    {
                        Sfx.Ui.Confirm();
                    }
                }
            });

            ApplyCardFace(card);
            cardRect.localScale = Vector3.one;
            if (resultText != null)
            {
                resultText.text = card.displayName + " " + resultSuffix;
            }

            animating = false;
            skipRequested = false;
            RefreshHeader();

            // 结果展示 3 秒后自动收起，屏幕回到干净的机台画面（2026-10-25
            // 用户反馈卡牌停留太占视野）。开新一抽会让代数失配，自动作废。
            revealGeneration++;
            StartCoroutine(HideCardAfterDelay(revealGeneration));
        }

        private int revealGeneration;

        private IEnumerator HideCardAfterDelay(int generation)
        {
            yield return new WaitForSecondsRealtime(3f);
            if (generation != revealGeneration || !IsOpen)
            {
                yield break;
            }

            cardRect.gameObject.SetActive(false);
            if (textStripRect != null)
            {
                textStripRect.gameObject.SetActive(false);
            }

            if (resultText != null)
            {
                resultText.text = string.Empty;
            }

            // 卡名/费用是独立文字节点，必须一并清掉（2026-10-25 收起后残留）。
            if (cardNameText != null)
            {
                cardNameText.text = string.Empty;
            }

            if (cardCostText != null)
            {
                cardCostText.text = string.Empty;
            }
        }

        /// <summary>通用补间：p 从 0 到 1；skipRequested 时直接跳到 1。</summary>
        private IEnumerator Tween(float duration, System.Action<float> onUpdate)
        {
            float start = Time.unscaledTime;
            while (!skipRequested)
            {
                float p = Mathf.Clamp01((Time.unscaledTime - start) / duration);
                onUpdate(p);
                if (p >= 1f)
                {
                    yield break;
                }

                yield return null;
            }

            onUpdate(1f);
        }

        private IEnumerator FlashBackdrop()
        {
            float end = Time.unscaledTime + 0.4f;
            while (Time.unscaledTime < end)
            {
                float p = (end - Time.unscaledTime) / 0.4f;
                backdrop.color = new Color(1f, 0.85f, 0.35f, 0.4f * p);
                yield return null;
            }

            backdrop.color = BackdropColor;
        }

        private IEnumerator PlayBurst(CardRarity rarity)
        {
            burstRect.gameObject.SetActive(true);
            float end = Time.unscaledTime + 0.6f;
            Color color = RarityGlowColor(rarity);
            while (Time.unscaledTime < end && !skipRequested)
            {
                float p = (end - Time.unscaledTime) / 0.6f;
                burstImage.color = new Color(color.r, color.g, color.b, p * 0.85f);
                burstRect.localScale = Vector3.one * Mathf.Lerp(0.5f, 1.25f, 1f - p);
                burstRect.localRotation = Quaternion.Euler(0f, 0f, (1f - p) * 25f);
                yield return null;
            }

            burstRect.gameObject.SetActive(false);
        }

        private void SpawnShellHalves()
        {
            shellLeftRect.gameObject.SetActive(true);
            shellRightRect.gameObject.SetActive(true);
            shellLeftRect.localPosition = new Vector3(ChuteX, CapsuleRestY, 0f);
            shellRightRect.localPosition = new Vector3(ChuteX, CapsuleRestY, 0f);
            StartCoroutine(FlyShell(shellLeftRect, new Vector3(-260f, 130f, 0f), 340f));
            StartCoroutine(FlyShell(shellRightRect, new Vector3(260f, 130f, 0f), -340f));
        }

        private IEnumerator FlyShell(RectTransform rect, Vector3 velocity, float spin)
        {
            Vector3 position = rect.localPosition;
            float end = Time.unscaledTime + 0.7f;
            while (Time.unscaledTime < end && !skipRequested && rect != null)
            {
                float p = (end - Time.unscaledTime) / 0.7f;
                rect.localPosition = position + velocity * (1f - p * p);
                rect.localRotation = Quaternion.Euler(0f, 0f, spin * (1f - p));
                yield return null;
            }

            if (rect != null)
            {
                rect.gameObject.SetActive(false);
            }
        }

        private void SpawnParticles(CardRarity rarity)
        {
            ClearParticles();
            int count = rarity == CardRarity.Rare ? 14 : rarity == CardRarity.Uncommon ? 8 : 5;
            for (int i = 0; i < count; i++)
            {
                GameObject particle = new GameObject("Particle_" + i, typeof(RectTransform));
                RectTransform rect = particle.transform as RectTransform;
                rect.SetParent(machineRect.parent, false);
                rect.localPosition = new Vector3(ChuteX, CapsuleRestY, 0f);
                rect.sizeDelta = new Vector2(12f, 12f);
                Image image = particle.AddComponent<Image>();
                image.sprite = EnsureParticleSprite();
                image.raycastTarget = false;
                image.color = RarityGlowColor(rarity);
                particles.Add(image);
                StartCoroutine(FlyParticle(rect, i, count));
            }
        }

        private IEnumerator FlyParticle(RectTransform rect, int index, int total)
        {
            float angle = index * Mathf.PI * 2f / total;
            Vector3 to = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (180f + index * 12f);
            Vector3 from = new Vector3(ChuteX, CapsuleRestY, 0f);
            float end = Time.unscaledTime + 0.65f;
            while (Time.unscaledTime < end && !skipRequested && rect != null)
            {
                float p = (end - Time.unscaledTime) / 0.65f;
                rect.localPosition = Vector3.Lerp(from, from + to, 1f - p * p);
                rect.localScale = Vector3.one * (1f - (1f - p) * 0.8f);
                yield return null;
            }

            if (rect != null)
            {
                rect.gameObject.SetActive(false);
            }
        }

        private void ClearParticles()
        {
            for (int i = 0; i < particles.Count; i++)
            {
                if (particles[i] != null)
                {
                    Destroy(particles[i].gameObject);
                }
            }

            particles.Clear();
        }

        // ------------------------------------------------------------------
        // 界面状态
        // ------------------------------------------------------------------

        private static readonly Color BackdropColor = new Color(0.02f, 0.02f, 0.05f, 0.92f);

        private void ResetStage()
        {
            capsuleRect.gameObject.SetActive(false);
            capsuleGlow.gameObject.SetActive(false);
            cardRect.gameObject.SetActive(false);
            burstRect.gameObject.SetActive(false);
            if (textStripRect != null)
            {
                textStripRect.gameObject.SetActive(false);
            }
            shellLeftRect.gameObject.SetActive(false);
            shellRightRect.gameObject.SetActive(false);
            ClearParticles();
            if (resultText != null)
            {
                resultText.text = string.Empty;
            }

            if (cardNameText != null)
            {
                cardNameText.text = string.Empty;
            }

            if (cardCostText != null)
            {
                cardCostText.text = string.Empty;
            }
        }

        private void RefreshHeader()
        {
            int money = sessionState == null || sessionState.playerState == null
                ? 0
                : sessionState.playerState.GetStat(PlayerStatKey.Money);
            if (moneyText != null)
            {
                moneyText.text = "金钱 " + money;
            }

            if (pullButtonText != null && pack != null)
            {
                pullButtonText.text = animating
                    ? "跳过演出…"
                    : money >= pack.price
                        ? "抽一次（" + pack.price + " 金）"
                        : "金钱不足（需 " + pack.price + "）";
            }

            if (pullButton != null)
            {
                // 演出中保持可点 = 跳过；空闲时按金钱门槛。
                pullButton.interactable = animating || (pack != null && money >= pack.price);
            }

            if (hintText != null && pack != null)
            {
                hintText.text = "点击机器或按钮抽奖 · 每局首抽必得非凡以上 · ESC 离开";
            }
        }

        private void ApplyCardFace(CardData card)
        {
            if (cardImage != null && card.cardSprite != null)
            {
                cardImage.sprite = card.cardSprite;
            }

            if (cardNameText != null)
            {
                cardNameText.text = card.displayName;
            }

            if (cardCostText != null)
            {
                cardCostText.text = "费用 " + card.cost;
            }
        }

        private static Color RarityGlowColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Rare: return new Color(1f, 0.82f, 0.32f, 1f);
                case CardRarity.Uncommon: return new Color(0.38f, 0.66f, 1f, 1f);
                default: return new Color(0.92f, 0.92f, 0.92f, 1f);
            }
        }

        // ------------------------------------------------------------------
        // 抽卡记录面板
        // ------------------------------------------------------------------

        private void BuildHistoryPanel()
        {
            GameObject overlayObject = new GameObject("HistoryPanel", typeof(RectTransform));
            historyRoot = overlayObject.transform as RectTransform;
            historyRoot.SetParent(rootRect, false);
            historyRoot.anchorMin = Vector2.zero;
            historyRoot.anchorMax = Vector2.one;
            historyRoot.offsetMin = historyRoot.offsetMax = Vector2.zero;
            Image overlay = overlayObject.AddComponent<Image>();
            overlay.color = new Color(0f, 0f, 0f, 0.88f);

            Text title = CreateText("Title", historyRoot, "抽卡记录", 42, FontStyle.Bold, TextAnchor.MiddleCenter);
            title.rectTransform.anchorMin = title.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            title.rectTransform.sizeDelta = new Vector2(600f, 70f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -50f);
            ApplyTextWhite(title, GameUiStyle.ResolveTheme(theme), GameUiTextStyle.Title);

            historySummaryText = CreateText("Summary", historyRoot, string.Empty, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            historySummaryText.rectTransform.anchorMin = historySummaryText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            historySummaryText.rectTransform.sizeDelta = new Vector2(1100f, 46f);
            historySummaryText.rectTransform.anchoredPosition = new Vector2(0f, -130f);

            historyEntriesText = CreateText("Entries", historyRoot, string.Empty, 24, FontStyle.Normal, TextAnchor.UpperLeft);
            historyEntriesText.supportRichText = true;
            historyEntriesText.rectTransform.anchorMin = historyEntriesText.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            historyEntriesText.rectTransform.sizeDelta = new Vector2(1000f, 640f);
            historyEntriesText.rectTransform.anchoredPosition = new Vector2(0f, -180f);

            Button backButton = CreateButton("Back", "返 回", new Vector2(0.5f, 0f), new Vector2(0f, 56f), new Vector2(240f, 80f), CloseHistory);
            Text backLabel = backButton.GetComponentInChildren<Text>();
            ApplyTextWhite(backLabel, GameUiStyle.ResolveTheme(theme), GameUiTextStyle.ButtonText);
            // 必须挂进面板里：CreateButton 默认挂主界面根，会让「返 回」在
            // 面板关闭时也浮在抽奖界面上（2026-10-25 用户撞见）。
            backButton.transform.SetParent(historyRoot, true);

            overlayObject.SetActive(false);
        }

        private void ShowHistory()
        {
            if (historyRoot == null)
            {
                return;
            }

            List<CapsulePullEntry> pulls = sessionState == null
                ? null
                : sessionState.capsulePulls;
            if (pulls == null || pulls.Count == 0)
            {
                historySummaryText.text = "还没有抽过卡";
                historyEntriesText.text = string.Empty;
                historyRoot.gameObject.SetActive(true);
                return;
            }

            int common = 0, uncommon = 0, rare = 0, duplicate = 0;
            for (int i = 0; i < pulls.Count; i++)
            {
                if (pulls[i] == null)
                {
                    continue;
                }

                if (pulls[i].rarity == 2) rare++;
                else if (pulls[i].rarity == 1) uncommon++;
                else common++;
                if (pulls[i].duplicate) duplicate++;
            }

            int spent = pack != null ? pulls.Count * pack.price : 0;
            historySummaryText.text = "共 " + pulls.Count + " 次｜"
                + "普通 " + common + " · 非凡 " + uncommon + " · 稀有 " + rare
                + "｜重复 " + duplicate
                + (pack != null ? "｜累计花费 " + spent + " 金" : string.Empty);

            List<string> lines = new List<string>();
            int from = pulls.Count - 1;
            int to = Mathf.Max(0, pulls.Count - 20);
            for (int i = from; i >= to; i--)
            {
                CapsulePullEntry entry = pulls[i];
                if (entry == null)
                {
                    continue;
                }

                lines.Add((i + 1) + ". " + RarityTag(entry.rarity) + " " + entry.cardName
                    + (entry.duplicate ? "（重复）" : string.Empty));
            }

            historyEntriesText.text = string.Join("\n", lines.ToArray())
                + (pulls.Count > 20 ? "\n…（只显示最近 20 条，共 " + pulls.Count + " 条）" : string.Empty);
            historyRoot.gameObject.SetActive(true);
            Sfx.Ui.Open();
        }

        private void CloseHistory()
        {
            if (historyRoot != null)
            {
                historyRoot.gameObject.SetActive(false);
            }
        }

        private bool HistoryIsOpen => historyRoot != null && historyRoot.gameObject.activeSelf;

        private static string RarityTag(int rarity)
        {
            switch (rarity)
            {
                case 2: return "<color=#FFD24A>稀有</color>";
                case 1: return "<color=#7DB8FF>非凡</color>";
                default: return "<color=#DDDDDD>普通</color>";
            }
        }

        // ------------------------------------------------------------------
        // 布局：全屏机台
        // ------------------------------------------------------------------

        private void BuildDefaultLayout()
        {
            GameObject root = new GameObject("Root", typeof(RectTransform));
            rootRect = root.transform as RectTransform;
            rootRect.SetParent(transform, false);
            // 全屏拉伸（同上：不是缩成一个点）。
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            backdrop = root.AddComponent<Image>();
            backdrop.color = BackdropColor;
            backdrop.raycastTarget = true;
            // 注意：背景上不放关闭按钮——演出中随手点击容易落在背景上，
            // 会把抽奖直接打断退出（2026-10-25 实测）。退出只走 ESC 和「离开」按钮。
            canvasGroup = UiAnimationHelper.EnsureCanvasGroup(root);

            // 机台（可点击 = 抽奖 / 演出中 = 跳过）
            GameObject machineObject = new GameObject("Machine", typeof(RectTransform));
            machineRect = machineObject.transform as RectTransform;
            machineRect.SetParent(rootRect, false);
            machineRect.anchorMin = machineRect.anchorMax = new Vector2(0.5f, 0.5f);
            machineRect.sizeDelta = new Vector2(MachineWidth, MachineHeight);
            machineRect.localPosition = new Vector3(0f, MachineCenterY, 0f);
            machineImage = machineObject.AddComponent<Image>();
            machineImage.sprite = EnsureMachineSprite();
            // 纯装饰：抽奖入口只有「抽一次」按钮（2026-10-25 用户要求），
            // 机器不响应点击，也不参与射线。
            machineImage.raycastTarget = false;

            // 演出可动件只保留扭蛋本体（金币/旋钮/罩内扭蛋已按简化版移除）。
            // 扭蛋 + 漏光
            GameObject glowObject = new GameObject("CapsuleGlow", typeof(RectTransform));
            RectTransform glowRect = glowObject.transform as RectTransform;
            glowRect.SetParent(rootRect, false);
            glowRect.anchorMin = glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(210f, 190f);
            glowRect.localPosition = new Vector3(ChuteX, CapsuleRestY, 0f);
            capsuleGlow = glowObject.AddComponent<Image>();
            capsuleGlow.sprite = EnsureCapsuleSprite();
            capsuleGlow.raycastTarget = false;
            glowObject.SetActive(false);

            GameObject capsuleObject = new GameObject("Capsule", typeof(RectTransform));
            capsuleRect = capsuleObject.transform as RectTransform;
            capsuleRect.SetParent(rootRect, false);
            capsuleRect.anchorMin = capsuleRect.anchorMax = new Vector2(0.5f, 0.5f);
            capsuleRect.sizeDelta = new Vector2(120f, 108f);
            capsuleImage = capsuleObject.AddComponent<Image>();
            capsuleImage.sprite = EnsureCapsuleSprite();
            capsuleImage.raycastTarget = false;
            capsuleObject.SetActive(false);

            // 蛋壳两半
            shellLeftRect = CreateShell("ShellLeft", new Vector3(ChuteX, CapsuleRestY, 0f), true);
            shellRightRect = CreateShell("ShellRight", new Vector3(ChuteX, CapsuleRestY, 0f), false);

            // 集中线（放射线）爆发层
            GameObject burstObject = new GameObject("Burst", typeof(RectTransform));
            burstRect = burstObject.transform as RectTransform;
            burstRect.SetParent(rootRect, false);
            burstRect.anchorMin = burstRect.anchorMax = new Vector2(0.5f, 0.5f);
            burstRect.sizeDelta = new Vector2(720f, 720f);
            burstRect.localPosition = new Vector3(0f, -60f, 0f);
            burstImage = burstObject.AddComponent<Image>();
            burstImage.sprite = EnsureBurstSprite();
            burstImage.raycastTarget = false;
            burstObject.SetActive(false);

            // 卡牌（机台正前方）
            GameObject cardObject = new GameObject("Card", typeof(RectTransform));
            cardRect = cardObject.transform as RectTransform;
            cardRect.SetParent(rootRect, false);
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            cardRect.localPosition = new Vector3(0f, 40f, 0f);
            cardImage = cardObject.AddComponent<Image>();
            cardImage.raycastTarget = false;
            cardObject.SetActive(false);

            // 卡名/费用/结果的衬底条：贴机台下方（中心 -285，高 130 → -220..-350）。
            // 只在展示抽到的卡时出现，空闲时隐藏（2026-10-25 用户要求）。
            GameObject stripObject = new GameObject("TextStrip", typeof(RectTransform));
            textStripRect = stripObject.transform as RectTransform;
            textStripRect.SetParent(rootRect, false);
            textStripRect.anchorMin = textStripRect.anchorMax = new Vector2(0.5f, 0.5f);
            textStripRect.sizeDelta = new Vector2(760f, 130f);
            textStripRect.localPosition = new Vector3(0f, -285f, 0f);
            Image strip = stripObject.AddComponent<Image>();
            strip.color = new Color(0f, 0f, 0f, 0.72f);
            strip.raycastTarget = false;
            stripObject.SetActive(false);

            cardNameText = CreateText("CardName", rootRect, string.Empty, 38, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(cardNameText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720f, 52f), new Vector2(0f, -250f));
            cardCostText = CreateText("CardCost", rootRect, string.Empty, 26, FontStyle.Normal, TextAnchor.MiddleCenter);
            Place(cardCostText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(720f, 36f), new Vector2(0f, -288f));

            resultText = CreateText("Result", rootRect, string.Empty, 34, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(resultText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(900f, 44f), new Vector2(0f, -330f));

            moneyText = CreateText("Money", rootRect, string.Empty, 34, FontStyle.Bold, TextAnchor.MiddleLeft);
            Place(moneyText.rectTransform, new Vector2(0f, 1f), new Vector2(420f, 52f), new Vector2(40f, -64f));

            hintText = CreateText("Hint", rootRect, string.Empty, 26, FontStyle.Normal, TextAnchor.MiddleCenter);
            Place(hintText.rectTransform, new Vector2(0.5f, 0f), new Vector2(1300f, 42f), new Vector2(0f, 160f));

            // 底部操作条：抽一次 / 记录 / 离开。
            pullButton = CreateButton("Pull", "抽一次", new Vector2(0.5f, 0f), new Vector2(-230f, 64f), new Vector2(280f, 84f), () =>
            {
                if (animating)
                {
                    skipRequested = true;
                }
                else
                {
                    TryPull();
                }
            });
            pullButtonText = pullButton.GetComponentInChildren<Text>();
            pullPixelButton = pullButton.GetComponent<PixelButton>();

            Button historyButton = CreateButton("History", "记录", new Vector2(0.5f, 0f), new Vector2(0f, 64f), new Vector2(160f, 84f), ShowHistory);

            Button leaveButton = CreateButton("Leave", "离 开", new Vector2(0.5f, 0f), new Vector2(200f, 64f), new Vector2(200f, 84f), Close);
            leaveButtonText = leaveButton.GetComponentInChildren<Text>();
            leavePixelButton = leaveButton.GetComponent<PixelButton>();

            BuildHistoryPanel();

            SetVisible(false);
        }

        private RectTransform CreateShell(string name, Vector3 position, bool mirror)
        {
            GameObject shellObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = shellObject.transform as RectTransform;
            rect.SetParent(rootRect, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(110f, 64f);
            rect.localPosition = position;
            rect.localRotation = Quaternion.identity;
            rect.localScale = mirror ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            Image image = shellObject.AddComponent<Image>();
            image.sprite = EnsureShellSprite();
            image.raycastTarget = false;
            shellObject.SetActive(false);
            return rect;
        }

        /// <summary>
        /// 按钮走对话菜单同款配方（PixelButton + 主题）——那套在本项目里被证明
        /// 一定能显示字；自己拼 Image+Text 的做法两次都翻车（字看不见）。
        /// </summary>
        private Button CreateButton(string name, string label, Vector2 anchor, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = buttonObject.transform as RectTransform;
            rect.SetParent(rootRect, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(action);

            Text text = CreateText("Text", rect, label, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = new Vector2(12f, 0f);
            text.rectTransform.offsetMax = new Vector2(-12f, 0f);

            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.background = background;
            pixelButton.labelText = text;
            pixelButton.theme = GameUiStyle.ResolveTheme(theme);
            pixelButton.ApplyStyle();
            return button;
        }

        private static Text CreateText(string name, RectTransform parent, string value, int fontSize, FontStyle style, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = anchor;
            text.raycastTarget = false;
            GameUiStyle.ApplyTextFromFontSpec(text, null, fontSize, style);
            text.transform.SetParent(parent, false);
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, anchor.y > 0.5f ? 1f : 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        private void SetVisible(bool visible)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            rootRect.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------
        // 程序生成贴图（点阵 / 码绘），真实美术到位后替换 Sprite 引用即可
        // ------------------------------------------------------------------

        private static Sprite EnsureMachineSprite()
        {
            if (machineSprite != null)
            {
                return machineSprite;
            }

            // 48×64 机台：玻璃罩 + 罩内扭蛋 + 红机身 + 旋钮底座 + 投币口 + 出蛋口 + 支脚
            PixelCanvas canvas = new PixelCanvas(48, 64);
            Color glass = new Color(0.80f, 0.85f, 0.88f, 0.55f);
            Color glassEdge = new Color(0.62f, 0.68f, 0.72f, 0.95f);
            Color body = new Color(0.72f, 0.30f, 0.28f, 1f);
            Color bodyDark = new Color(0.52f, 0.20f, 0.19f, 1f);
            Color plate = new Color(0.90f, 0.86f, 0.76f, 1f);
            Color dark = new Color(0.13f, 0.12f, 0.15f, 1f);
            Color baseColor = new Color(0.35f, 0.30f, 0.30f, 1f);

            canvas.FillCircle(24, 50, 17, glass);
            canvas.Ring(24, 50, 17, glassEdge);
            canvas.FillRect(9, 46, 30, 2, glassEdge);
            canvas.Dot(17, 53, 3, new Color(0.95f, 0.62f, 0.35f, 1f));
            canvas.Dot(28, 51, 3, new Color(0.38f, 0.70f, 0.72f, 1f));
            canvas.Dot(22, 46, 2, new Color(0.92f, 0.84f, 0.35f, 1f));
            canvas.Dot(31, 55, 2, new Color(0.95f, 0.62f, 0.35f, 1f));
            canvas.Dot(15, 46, 2, new Color(0.38f, 0.70f, 0.72f, 1f));

            canvas.FillRect(8, 24, 32, 22, body);
            canvas.Rect(8, 24, 32, 22, bodyDark);
            canvas.FillRect(8, 24, 32, 2, bodyDark);
            canvas.FillRect(8, 44, 32, 2, bodyDark);

            canvas.FillRect(13, 33, 16, 10, plate);
            canvas.Rect(13, 33, 16, 10, bodyDark);
            canvas.FillRect(33, 32, 4, 9, dark);      // 投币口
            canvas.FillCircle(35, 34, 4, plate);      // 旋钮底座

            canvas.FillRect(16, 16, 16, 7, dark);     // 出蛋口
            canvas.FillRect(17, 22, 14, 2, bodyDark);
            canvas.FillRect(10, 9, 9, 7, baseColor);  // 支脚
            canvas.FillRect(29, 9, 9, 7, baseColor);
            canvas.FillRect(8, 7, 32, 2, bodyDark);

            machineSprite = canvas.ToSprite();
            return machineSprite;
        }

        private static Sprite EnsureCapsuleSprite()
        {
            if (capsuleSprite != null)
            {
                return capsuleSprite;
            }

            string[] pattern =
            {
                "....####....",
                "..##oooo##..",
                ".#oooooooo#.",
                ".#oooooooo#.",
                "##oooooooo##",
                "##o##oo##o##",
                "#o##oooo##o#",
                "#o#oooooo#o#",
                ".##oooooo##.",
                "..########..",
                "...##..##...",
                "....#..#...."
            };
            capsuleSprite = BuildSprite(pattern, new Color(0.95f, 0.42f, 0.38f), new Color(0.98f, 0.92f, 0.78f));
            return capsuleSprite;
        }

        private static Sprite EnsureCardBackSprite()
        {
            if (cardBackSprite != null)
            {
                return cardBackSprite;
            }

            string[] pattern =
            {
                "############",
                "#oooooooooo#",
                "#ooo####ooo#",
                "#oo#oooo#oo#",
                "#oo#o##o#oo#",
                "#oo#o##o#oo#",
                "#oo#oooo#oo#",
                "#ooo####ooo#",
                "#oooooooooo#",
                "#oo######oo#",
                "#oooooooooo#",
                "############"
            };
            cardBackSprite = BuildSprite(pattern, new Color(0.16f, 0.15f, 0.22f), new Color(0.55f, 0.52f, 0.62f));
            return cardBackSprite;
        }

        private static Sprite EnsureShellSprite()
        {
            if (shellSprite != null)
            {
                return shellSprite;
            }

            // 半个蛋壳（右半；左半用镜像缩放）
            PixelCanvas canvas = new PixelCanvas(14, 12);
            Color shell = new Color(0.95f, 0.42f, 0.38f, 1f);
            Color shellInner = new Color(0.98f, 0.92f, 0.78f, 1f);
            canvas.FillRect(0, 0, 7, 12, shell);
            canvas.FillRect(0, 0, 2, 12, shellInner);
            canvas.FillRect(7, 4, 7, 4, shellInner);
            canvas.FillRect(7, 0, 2, 3, shell);
            canvas.FillRect(7, 9, 2, 3, shell);
            shellSprite = canvas.ToSprite();
            return shellSprite;
        }

        private static Sprite EnsureBurstSprite()
        {
            if (burstSprite != null)
            {
                return burstSprite;
            }

            // 集中线：中心向外的放射短线（漫画式速度线）
            PixelCanvas canvas = new PixelCanvas(48, 48);
            Color line = new Color(1f, 1f, 1f, 1f);
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI * 2f / 16f + 0.12f;
                for (int r = 8; r < 23; r += 1)
                {
                    int x = 24 + Mathf.RoundToInt(Mathf.Cos(angle) * r);
                    int y = 24 + Mathf.RoundToInt(Mathf.Sin(angle) * r);
                    canvas.SetPixel(x, y, line);
                    if (i % 2 == 0 && r > 14)
                    {
                        canvas.SetPixel(x + 1, y, line);
                    }
                }
            }

            burstSprite = canvas.ToSprite();
            return burstSprite;
        }

        private static Sprite EnsureParticleSprite()
        {
            if (particleSprite != null)
            {
                return particleSprite;
            }

            string[] pattern = { "###", "###", "###" };
            particleSprite = BuildSprite(pattern, Color.white, Color.white);
            return particleSprite;
        }

        /// <summary>'#' 主色、'o' 次色，其余透明的点阵转 Sprite。</summary>
        private static Sprite BuildSprite(string[] pattern, Color primary, Color secondary)
        {
            int height = pattern.Length;
            int width = pattern[0].Length;
            PixelCanvas canvas = new PixelCanvas(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char c = pattern[y][x];
                    canvas.SetPixel(x, height - 1 - y, c == '#' ? primary : c == 'o' ? secondary : Color.clear);
                }
            }

            return canvas.ToSprite();
        }

        /// <summary>码绘小画布：点/矩形/圆环/实心圆，最终转 Point 采样 Sprite。</summary>
        private sealed class PixelCanvas
        {
            private readonly int width;
            private readonly int height;
            private readonly Color[] pixels;

            public PixelCanvas(int width, int height)
            {
                this.width = width;
                this.height = height;
                pixels = new Color[width * height];
            }

            public void SetPixel(int x, int y, Color color)
            {
                if (x < 0 || y < 0 || x >= width || y >= height)
                {
                    return;
                }

                pixels[y * width + x] = color;
            }

            public void FillRect(int x0, int y0, int w, int h, Color color)
            {
                for (int y = y0; y < y0 + h; y++)
                {
                    for (int x = x0; x < x0 + w; x++)
                    {
                        SetPixel(x, y, color);
                    }
                }
            }

            public void Rect(int x0, int y0, int w, int h, Color color)
            {
                FillRect(x0, y0, w, 1, color);
                FillRect(x0, y0 + h - 1, w, 1, color);
                FillRect(x0, y0, 1, h, color);
                FillRect(x0 + w - 1, y0, 1, h, color);
            }

            public void FillCircle(int cx, int cy, int r, Color color)
            {
                for (int y = -r; y <= r; y++)
                {
                    for (int x = -r; x <= r; x++)
                    {
                        if (x * x + y * y <= r * r)
                        {
                            SetPixel(cx + x, cy + y, color);
                        }
                    }
                }
            }

            public void Ring(int cx, int cy, int r, Color color)
            {
                for (int y = -r; y <= r; y++)
                {
                    for (int x = -r; x <= r; x++)
                    {
                        int d = x * x + y * y;
                        if (d <= r * r && d >= (r - 1) * (r - 1))
                        {
                            SetPixel(cx + x, cy + y, color);
                        }
                    }
                }
            }

            public void Dot(int cx, int cy, int r, Color color)
            {
                FillCircle(cx, cy, r, color);
                Ring(cx, cy, r, new Color(color.r * 0.75f, color.g * 0.75f, color.b * 0.75f, color.a));
            }

            public Sprite ToSprite()
            {
                Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                texture.SetPixels(pixels);
                texture.Apply();
                return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
            }
        }
    }
}
