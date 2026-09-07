using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public static class SettingsUiFactory
    {
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);

        public static SettingsPanelView Create(Transform owner)
        {
            return ReferenceVolumeSettingsUiFactory.Create(owner);
        }

        private static SettingsPanelView CreateLegacy(Transform owner)
        {
            UIThemeConfig theme = GameUiStyle.ResolveTheme(null);

            GameObject canvasObject = new GameObject(
                "SettingsCanvas",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasScaler),
                typeof(GraphicRaycaster));
            RectTransform canvasRect = canvasObject.transform as RectTransform;
            canvasRect.SetParent(owner, false);
            Stretch(canvasRect);

            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 5000;
            canvas.pixelPerfect = true;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            GameObject overlayObject = CreateRectObject("SettingsOverlay", canvasRect);
            RectTransform overlayRect = overlayObject.transform as RectTransform;
            Stretch(overlayRect);
            Image overlayImage = overlayObject.AddComponent<Image>();
            overlayImage.sprite = GameUiStyle.GetSolidSprite();
            overlayImage.color = new Color(0.02f, 0.025f, 0.04f, 0.78f);
            overlayImage.raycastTarget = true;
            CanvasGroup canvasGroup = overlayObject.AddComponent<CanvasGroup>();

            GameObject panelObject = CreateRectObject("SettingsPanel", overlayRect);
            RectTransform panelRect = panelObject.transform as RectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(600f, 400f);

            Image panelImage = panelObject.AddComponent<Image>();
            panelImage.raycastTarget = true;
            PixelPanel panelStyle = panelObject.AddComponent<PixelPanel>();
            panelStyle.theme = theme;
            panelStyle.variant = GameUiPanelVariant.Dark;
            panelStyle.background = panelImage;
            panelStyle.createBorder = true;
            panelStyle.ApplyStyle();
            GameUiStyle.ApplyPanelSprite(
                panelImage,
                theme.menuWindowSprite,
                GameUiStyle.ResolvePanelColor(theme, GameUiPanelVariant.Dark));

            Text title = CreateText(
                "Title",
                panelRect,
                "设置",
                GameUiTextStyle.Title,
                TextAnchor.MiddleLeft,
                true);
            SetTopLeft(title.rectTransform, new Vector2(28f, -18f), new Vector2(360f, 44f));

            Text sectionTitle = CreateText(
                "AudioSectionTitle",
                panelRect,
                "音量调节",
                GameUiTextStyle.Subtitle,
                TextAnchor.MiddleLeft,
                true);
            SetTopLeft(sectionTitle.rectTransform, new Vector2(36f, -82f), new Vector2(200f, 32f));

            Text musicLabel = CreateText(
                "MusicLabel",
                panelRect,
                "音乐",
                GameUiTextStyle.Body,
                TextAnchor.MiddleLeft,
                true);
            SetTopLeft(musicLabel.rectTransform, new Vector2(44f, -138f), new Vector2(108f, 34f));

            Slider musicSlider = CreateSlider(
                "MusicSlider",
                panelRect,
                new Vector2(158f, -138f),
                new Vector2(320f, 34f),
                theme.gold,
                theme);

            Text musicValue = CreateText(
                "MusicValue",
                panelRect,
                "100%",
                GameUiTextStyle.StatText,
                TextAnchor.MiddleRight,
                true);
            SetTopLeft(musicValue.rectTransform, new Vector2(490f, -138f), new Vector2(70f, 34f));

            Text sfxLabel = CreateText(
                "SfxLabel",
                panelRect,
                "音效",
                GameUiTextStyle.Body,
                TextAnchor.MiddleLeft,
                true);
            SetTopLeft(sfxLabel.rectTransform, new Vector2(44f, -210f), new Vector2(108f, 34f));

            Slider sfxSlider = CreateSlider(
                "SfxSlider",
                panelRect,
                new Vector2(158f, -210f),
                new Vector2(320f, 34f),
                theme.success,
                theme);

            Text sfxValue = CreateText(
                "SfxValue",
                panelRect,
                "100%",
                GameUiTextStyle.StatText,
                TextAnchor.MiddleRight,
                true);
            SetTopLeft(sfxValue.rectTransform, new Vector2(490f, -210f), new Vector2(70f, 34f));

            Text hint = CreateText(
                "Hint",
                panelRect,
                "ESC：打开或关闭设置｜音量会自动保存",
                GameUiTextStyle.HintText,
                TextAnchor.MiddleCenter,
                true);
            SetTopLeft(hint.rectTransform, new Vector2(40f, -276f), new Vector2(520f, 30f));

            Button closeButton = CreateButton(
                "CloseButton",
                panelRect,
                "返回游戏",
                new Vector2(0f, -336f),
                new Vector2(190f, 42f),
                theme,
                true);

            Button topCloseButton = CreateButton(
                "TopCloseButton",
                panelRect,
                "X",
                new Vector2(546f, -22f),
                new Vector2(34f, 34f),
                theme,
                false);

            SettingsPanelView view = canvasObject.AddComponent<SettingsPanelView>();
            view.Initialize(
                overlayObject,
                canvasGroup,
                null,
                musicSlider,
                sfxSlider,
                null,
                musicValue,
                sfxValue,
                closeButton);
            topCloseButton.onClick.AddListener(() => closeButton.onClick.Invoke());
            return view;
        }

        private static Slider CreateSlider(
            string name,
            RectTransform parent,
            Vector2 topLeftPosition,
            Vector2 size,
            Color fillColor,
            UIThemeConfig theme)
        {
            GameObject sliderObject = CreateRectObject(name, parent);
            RectTransform sliderRect = sliderObject.transform as RectTransform;
            SetTopLeft(sliderRect, topLeftPosition, size);

            GameObject backgroundObject = CreateRectObject("Background", sliderRect);
            RectTransform backgroundRect = backgroundObject.transform as RectTransform;
            backgroundRect.anchorMin = new Vector2(0f, 0.5f);
            backgroundRect.anchorMax = new Vector2(1f, 0.5f);
            backgroundRect.pivot = new Vector2(0.5f, 0.5f);
            backgroundRect.anchoredPosition = Vector2.zero;
            backgroundRect.sizeDelta = new Vector2(0f, 10f);
            Image background = backgroundObject.AddComponent<Image>();
            background.sprite = GameUiStyle.GetSolidSprite();
            background.color = theme.buttonPressed;
            background.raycastTarget = false;

            GameObject fillAreaObject = CreateRectObject("Fill Area", sliderRect);
            RectTransform fillAreaRect = fillAreaObject.transform as RectTransform;
            Stretch(fillAreaRect);
            fillAreaRect.offsetMin = new Vector2(10f, 11f);
            fillAreaRect.offsetMax = new Vector2(-10f, -11f);

            GameObject fillObject = CreateRectObject("Fill", fillAreaRect);
            RectTransform fillRect = fillObject.transform as RectTransform;
            Stretch(fillRect);
            Image fill = fillObject.AddComponent<Image>();
            fill.sprite = GameUiStyle.GetSolidSprite();
            fill.color = fillColor;
            fill.raycastTarget = false;

            GameObject handleAreaObject = CreateRectObject("Handle Slide Area", sliderRect);
            RectTransform handleAreaRect = handleAreaObject.transform as RectTransform;
            Stretch(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(10f, 0f);
            handleAreaRect.offsetMax = new Vector2(-10f, 0f);

            GameObject handleObject = CreateRectObject("Handle", handleAreaRect);
            RectTransform handleRect = handleObject.transform as RectTransform;
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            handleRect.sizeDelta = new Vector2(20f, 28f);
            Image handle = handleObject.AddComponent<Image>();
            handle.sprite = GameUiStyle.GetSolidSprite();
            handle.color = theme.inverseText;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.fillRect = fillRect;
            slider.handleRect = handleRect;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;
            return slider;
        }

        private static Button CreateButton(
            string name,
            RectTransform parent,
            string label,
            Vector2 position,
            Vector2 size,
            UIThemeConfig theme,
            bool centered)
        {
            GameObject buttonObject = CreateRectObject(name, parent);
            RectTransform rect = buttonObject.transform as RectTransform;
            if (centered)
            {
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = position;
                rect.sizeDelta = size;
            }
            else
            {
                SetTopLeft(rect, position, size);
            }

            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText(
                "Text",
                rect,
                label,
                GameUiTextStyle.ButtonText,
                TextAnchor.MiddleCenter,
                true);
            Stretch(text.rectTransform);

            PixelButton pixelButton = buttonObject.AddComponent<PixelButton>();
            pixelButton.theme = theme;
            pixelButton.background = image;
            pixelButton.labelText = text;
            pixelButton.ApplyStyle();
            return button;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            string value,
            GameUiTextStyle style,
            TextAnchor alignment,
            bool inverse)
        {
            GameObject textObject = CreateRectObject(name, parent);
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            GameUiStyle.ApplyPixelText(text, null, style, inverse);
            return text;
        }

        private static GameObject CreateRectObject(string name, RectTransform parent)
        {
            GameObject gameObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = gameObject.transform as RectTransform;
            rect.SetParent(parent, false);
            return gameObject;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
