using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 扭蛋抽奖界面（设计文档：Docs/CapsuleGachaDesign-2026-10-24.md）。
    /// 常驻面板、可连抽：顶部金钱、中央演出舞台、底部「抽一次/再抽一次 + 离开」。
    /// 演出 V1 全程序生成（胶囊/卡背/粒子都是点阵贴图），按品质停顿预告稀有度，
    /// 任意点击舞台可跳过。打开期间持有 GameplayInputGate，关闭释放。
    /// </summary>
    public sealed class CapsuleView : MonoBehaviour
    {
        private const float PanelWidth = 960f;
        private const float PanelHeight = 760f;
        private const float StageWidth = 640f;
        private const float StageHeight = 420f;
        private const float CardWidth = 220f;
        private const float CardHeight = 300f;

        public UIThemeConfig theme;
        public GameSessionState sessionState;

        private RectTransform rootRect;
        private CanvasGroup canvasGroup;
        private Image backdrop;
        private Text titleText;
        private Text moneyText;
        private RectTransform stageRect;
        private RectTransform capsuleRect;
        private Image capsuleImage;
        private RectTransform cardRect;
        private Image cardImage;
        private Text cardNameText;
        private Text cardCostText;
        private Text resultText;
        private Button pullButton;
        private Text pullButtonText;
        private Button closeButton;
        private readonly List<Image> particles = new List<Image>();

        private CapsulePackData pack;
        private List<CardData> pool;
        private bool animating;
        private bool skipRequested;

        private static Sprite capsuleSprite;
        private static Sprite cardBackSprite;
        private static Sprite particleSprite;

        /// <summary>场景里没有就现建一个（挂在运行时画布下），有则复用。</summary>
        public static CapsuleView Ensure(RectTransform parent)
        {
            CapsuleView existing = FindFirstObjectByType<CapsuleView>(FindObjectsInactive.Include);
            if (existing != null)
            {
                return existing;
            }

            GameObject viewObject = new GameObject("CapsuleView", typeof(RectTransform));
            RectTransform rect = viewObject.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            CapsuleView view = viewObject.AddComponent<CapsuleView>();
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
            ShowResult(null, string.Empty);
            SetVisible(true);
            RefreshHeader();
            Sfx.Ui.Open();
            GameplayInputGate.Acquire(this);
        }

        public void Close()
        {
            if (!gameObject.activeSelf)
            {
                return;
            }

            StopAllCoroutines();
            animating = false;
            SetVisible(false);
            GameplayInputGate.Release(this, true);
        }

        public bool IsOpen => gameObject.activeSelf;

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
            string suffix = outcome.duplicate
                ? "\n（重复卡·返还 " + pack.duplicateRefund + " 金）"
                : "\n（已加入卡组）";
            if (outcome.duplicate && pack.duplicateRefund > 0)
            {
                sessionState.playerState.AddStat(PlayerStatKey.Money, pack.duplicateRefund);
            }

            RefreshHeader();
            StartCoroutine(PlayReveal(outcome.card, suffix));
        }

        /// <summary>演出主时间轴。skipRequested 为 true 时各阶段立刻跳到终点。</summary>
        private IEnumerator PlayReveal(CardData card, string resultSuffix)
        {
            animating = true;
            skipRequested = false;
            SetPullInteractable(false);
            ShowResult(null, string.Empty);
            ShowCard(false);
            ShowCapsule(true);

            // 投币 + 摇晃
            Sfx.Ui.Click();
            float shakeEnd = Time.unscaledTime + 0.7f;
            Vector3 basePosition = capsuleRect.localPosition;
            while (Time.unscaledTime < shakeEnd && !skipRequested)
            {
                float t = (shakeEnd - Time.unscaledTime) / 0.7f;
                capsuleRect.localPosition = basePosition + new Vector3(Mathf.Sin(Time.unscaledTime * 40f) * 14f * t, 0f, 0f);
                capsuleRect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.unscaledTime * 40f) * 10f * t);
                yield return null;
            }

            capsuleRect.localPosition = basePosition;
            capsuleRect.localRotation = Quaternion.identity;

            // 爆开：胶囊隐藏，卡背弹出
            Sfx.World.PageFlip();
            ShowCapsule(false);
            ShowCard(true);
            cardImage.sprite = EnsureCardBackSprite();
            cardNameText.text = string.Empty;
            cardCostText.text = string.Empty;
            cardRect.localScale = Vector3.one * 0.4f;
            SpawnParticles(card.rarity);
            float popEnd = Time.unscaledTime + 0.25f;
            while (Time.unscaledTime < popEnd && !skipRequested)
            {
                cardRect.localScale = Vector3.Lerp(Vector3.one * 0.4f, Vector3.one, (0.25f - (popEnd - Time.unscaledTime)) / 0.25f);
                yield return null;
            }

            cardRect.localScale = Vector3.one;

            // 品质停顿：普通 0.3s / 非凡 0.8s / 稀有 1.4s（稀有加描边脉冲）
            float suspense = card.rarity == CardRarity.Rare ? 1.4f : card.rarity == CardRarity.Uncommon ? 0.8f : 0.3f;
            float suspenseEnd = Time.unscaledTime + suspense;
            while (Time.unscaledTime < suspenseEnd && !skipRequested)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
                cardImage.color = Color.Lerp(Color.white, RarityEdgeColor(card.rarity), card.rarity == CardRarity.Common ? 0f : pulse * 0.6f);
                yield return null;
            }

            // 翻面：水平缩放 1→0→1，中点换成卡面
            float flipEnd = Time.unscaledTime + 0.25f;
            bool swapped = false;
            while (Time.unscaledTime < flipEnd && !skipRequested)
            {
                float p = (0.25f - (flipEnd - Time.unscaledTime)) / 0.25f;
                cardRect.localScale = new Vector3(Mathf.Abs(Mathf.Cos(p * Mathf.PI)), 1f, 1f);
                if (!swapped && p >= 0.5f)
                {
                    swapped = true;
                    ApplyCardFace(card);
                    cardImage.color = Color.white;
                    if (card.rarity == CardRarity.Rare)
                    {
                        StartCoroutine(FlashBackdrop());
                        Sfx.Ui.Confirm();
                    }
                }

                yield return null;
            }

            ApplyCardFace(card);
            cardImage.color = Color.white;
            cardRect.localScale = Vector3.one;
            ShowResult(card, resultSuffix);
            animating = false;
            skipRequested = false;
            RefreshHeader();
            SetPullInteractable(true);
        }

        private IEnumerator FlashBackdrop()
        {
            float end = Time.unscaledTime + 0.35f;
            while (Time.unscaledTime < end)
            {
                float p = (end - Time.unscaledTime) / 0.35f;
                backdrop.color = new Color(1f, 0.85f, 0.35f, 0.35f * p);
                yield return null;
            }

            backdrop.color = new Color(0f, 0f, 0f, 0.78f);
        }

        // ------------------------------------------------------------------
        // UI 状态
        // ------------------------------------------------------------------

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
                pullButtonText.text = "抽一次（" + pack.price + " 金）";
            }

            SetPullInteractable(!animating && pack != null && money >= pack.price);
            if (pullButtonText != null && pack != null && !animating && money < pack.price)
            {
                pullButtonText.text = "金钱不足（需 " + pack.price + "）";
            }
        }

        private void SetPullInteractable(bool interactable)
        {
            if (pullButton != null)
            {
                pullButton.interactable = interactable;
            }
        }

        private void ShowResult(CardData card, string suffix)
        {
            if (resultText == null)
            {
                return;
            }

            resultText.text = card == null
                ? string.Empty
                : card.displayName + suffix;
        }

        private void ShowCapsule(bool visible)
        {
            if (capsuleRect != null)
            {
                capsuleRect.gameObject.SetActive(visible);
            }
        }

        private void ShowCard(bool visible)
        {
            if (cardRect != null)
            {
                cardRect.gameObject.SetActive(visible);
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

        private void SpawnParticles(CardRarity rarity)
        {
            ClearParticles();
            int count = rarity == CardRarity.Rare ? 10 : rarity == CardRarity.Uncommon ? 6 : 4;
            for (int i = 0; i < count; i++)
            {
                GameObject particle = new GameObject("Particle_" + i, typeof(RectTransform));
                RectTransform rect = particle.transform as RectTransform;
                rect.SetParent(stageRect, false);
                rect.sizeDelta = new Vector2(10f, 10f);
                Image image = particle.AddComponent<Image>();
                image.sprite = EnsureParticleSprite();
                image.raycastTarget = false;
                image.color = RarityEdgeColor(rarity);
                particles.Add(image);
                StartCoroutine(FlyParticle(rect, i));
            }
        }

        private IEnumerator FlyParticle(RectTransform rect, int index)
        {
            float angle = index * Mathf.PI * 2f / 12f;
            Vector3 from = Vector3.zero;
            Vector3 to = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (140f + index * 9f);
            float end = Time.unscaledTime + 0.6f;
            while (Time.unscaledTime < end && !skipRequested && rect != null)
            {
                float p = (0.6f - (end - Time.unscaledTime)) / 0.6f;
                rect.localPosition = Vector3.Lerp(from, to, p);
                rect.localScale = Vector3.one * (1f - p * 0.7f);
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

        private static Color RarityEdgeColor(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Rare: return new Color(1f, 0.8f, 0.3f, 1f);
                case CardRarity.Uncommon: return new Color(0.4f, 0.65f, 1f, 1f);
                default: return Color.white;
            }
        }

        // ------------------------------------------------------------------
        // 布局
        // ------------------------------------------------------------------

        private void BuildDefaultLayout()
        {
            GameObject root = new GameObject("Root", typeof(RectTransform));
            rootRect = root.transform as RectTransform;
            rootRect.SetParent(transform, false);
            rootRect.anchorMin = rootRect.anchorMax = Vector2.zero;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;

            backdrop = root.AddComponent<Image>();
            backdrop.color = new Color(0f, 0f, 0f, 0.78f);
            backdrop.raycastTarget = true;

            GameObject panelObject = new GameObject("Panel", typeof(RectTransform));
            RectTransform panelRect = panelObject.transform as RectTransform;
            panelRect.SetParent(rootRect, false);
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            Image panelBackground = panelObject.AddComponent<Image>();
            panelBackground.color = new Color(0.09f, 0.09f, 0.13f, 0.98f);
            PixelPanel panelStyle = panelObject.AddComponent<PixelPanel>();
            panelStyle.background = panelBackground;
            panelStyle.variant = GameUiPanelVariant.Standard;
            theme = GameUiStyle.ResolveTheme(theme);
            panelStyle.theme = theme;
            panelStyle.ApplyStyle();
            canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panelObject);

            titleText = CreateText("Title", panelRect, "扭蛋机", 40, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(PanelWidth - 40f, 64f), new Vector2(0f, -20f));

            moneyText = CreateText("Money", panelRect, string.Empty, 26, FontStyle.Bold, TextAnchor.MiddleRight);
            Place(moneyText.rectTransform, new Vector2(1f, 1f), new Vector2(280f, 52f), new Vector2(320f, -66f));

            GameObject stageObject = new GameObject("Stage", typeof(RectTransform));
            stageRect = stageObject.transform as RectTransform;
            stageRect.SetParent(panelRect, false);
            stageRect.anchorMin = stageRect.anchorMax = new Vector2(0.5f, 0.5f);
            stageRect.sizeDelta = new Vector2(StageWidth, StageHeight);
            stageRect.anchoredPosition = new Vector2(0f, 36f);
            Image stageBackground = stageObject.AddComponent<Image>();
            stageBackground.color = new Color(0f, 0f, 0f, 0.35f);

            // 点击舞台 = 跳过演出
            Button stageButton = stageObject.AddComponent<Button>();
            stageButton.transition = Selectable.Transition.None;
            stageButton.targetGraphic = stageBackground;
            stageButton.onClick.AddListener(() => skipRequested = true);

            GameObject capsuleObject = new GameObject("Capsule", typeof(RectTransform));
            capsuleRect = capsuleObject.transform as RectTransform;
            capsuleRect.SetParent(stageRect, false);
            capsuleRect.sizeDelta = new Vector2(120f, 108f);
            capsuleImage = capsuleObject.AddComponent<Image>();
            capsuleImage.sprite = EnsureCapsuleSprite();
            capsuleImage.raycastTarget = false;

            GameObject cardObject = new GameObject("Card", typeof(RectTransform));
            cardRect = cardObject.transform as RectTransform;
            cardRect.SetParent(stageRect, false);
            cardRect.sizeDelta = new Vector2(CardWidth, CardHeight);
            cardImage = cardObject.AddComponent<Image>();
            cardImage.sprite = EnsureCardBackSprite();
            cardImage.raycastTarget = false;
            cardObject.SetActive(false);

            cardNameText = CreateText("CardName", stageRect, string.Empty, 26, FontStyle.Bold, TextAnchor.LowerCenter);
            Place(cardNameText.rectTransform, new Vector2(0.5f, 0f), new Vector2(StageWidth - 60f, 40f), new Vector2(0f, 14f));
            cardCostText = CreateText("CardCost", stageRect, string.Empty, 20, FontStyle.Normal, TextAnchor.LowerCenter);
            Place(cardCostText.rectTransform, new Vector2(0.5f, 0f), new Vector2(StageWidth - 60f, 30f), new Vector2(0f, -6f));

            resultText = CreateText("Result", panelRect, string.Empty, 28, FontStyle.Bold, TextAnchor.MiddleCenter);
            Place(resultText.rectTransform, new Vector2(0.5f, 0f), new Vector2(PanelWidth - 80f, 48f), new Vector2(0f, 176f));

            pullButton = CreateButton(panelRect, "Pull", "抽一次", new Vector2(-170f, 60f), new Vector2(320f, 76f), TryPull);
            pullButtonText = pullButton.GetComponentInChildren<Text>();

            closeButton = CreateButton(panelRect, "Close", "离开", new Vector2(170f, 60f), new Vector2(240f, 76f), Close);

            SetVisible(false);
        }

        private Button CreateButton(RectTransform parent, string name, string label, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction action)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = buttonObject.transform as RectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            Image background = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = background;
            button.onClick.AddListener(action);

            Text text = CreateText("Text", rect, label, 26, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.background = background;
            pixelButton.labelText = text;
            pixelButton.theme = theme;
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
            rect.pivot = new Vector2(0.5f, anchor.y > 0.5f ? 1f : 0f);
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
        // 程序生成贴图（点阵），不依赖美术资源
        // ------------------------------------------------------------------

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
                ".##oo##oo##.",
                ".#o##..##o#.",
                "##o##oo##o##",
                "##o######o##",
                "##o#oooo#o##",
                ".#o#oooo#o#.",
                ".##o####o##.",
                "..##oooo##..",
                "...######...",
                "....####...."
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

        /// <summary>'#' 主色、'o' 次色，其余字符透明的点阵转 Sprite（Point 采样）。</summary>
        private static Sprite BuildSprite(string[] pattern, Color primary, Color secondary)
        {
            int height = pattern.Length;
            int width = pattern[0].Length;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    char c = pattern[y][x];
                    Color color = c == '#' ? primary : c == 'o' ? secondary : Color.clear;
                    texture.SetPixel(x, height - 1 - y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        }
    }
}
