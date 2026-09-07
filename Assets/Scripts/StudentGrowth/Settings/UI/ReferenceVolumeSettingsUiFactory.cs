using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public static class ReferenceVolumeSettingsUiFactory
    {
        private static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        private static readonly Color PageColor = new Color(0.965f, 0.97f, 0.98f, 0.96f);
        private static readonly Color TitleColor = new Color(0.035f, 0.04f, 0.05f, 1f);
        private static readonly Color TextColor = new Color(0.16f, 0.20f, 0.27f, 1f);
        private static readonly Color HintColor = new Color(0.38f, 0.43f, 0.52f, 1f);
        private static readonly Color TrackColor = new Color(0.64f, 0.69f, 0.77f, 1f);
        private static readonly Color HandleColor = new Color(0.13f, 0.14f, 0.16f, 1f);

        private static Sprite roundedBarSprite;
        private static Sprite roundedSquareSprite;

        public static SettingsPanelView Create(Transform owner)
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

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<SettingsCanvasFitView>();

            GameObject overlayObject = CreateRectObject("SettingsOverlay", canvasRect);
            RectTransform overlayRect = overlayObject.transform as RectTransform;
            Stretch(overlayRect);
            Image overlayImage = overlayObject.AddComponent<Image>();
            overlayImage.sprite = GameUiStyle.GetSolidSprite();
            overlayImage.color = PageColor;
            overlayImage.raycastTarget = true;
            CanvasGroup canvasGroup = overlayObject.AddComponent<CanvasGroup>();

            Text fullscreenHint = CreateText(
                "FullscreenHint",
                overlayRect,
                "您也可以使用 F11 切换全屏",
                theme,
                24,
                FontStyle.Normal,
                HintColor,
                TextAnchor.MiddleLeft);
            SetTopLeft(fullscreenHint.rectTransform, new Vector2(100f, -54f), new Vector2(620f, 34f));

            Text title = CreateText(
                "Title",
                overlayRect,
                "设置",
                theme,
                46,
                FontStyle.Bold,
                TitleColor,
                TextAnchor.MiddleLeft);
            SetTopLeft(title.rectTransform, new Vector2(100f, -112f), new Vector2(300f, 62f));

            Text masterValue = CreateVolumeLabel("MasterValue", overlayRect, "主音量：100%", theme, -200f);
            Slider masterSlider = CreateSlider("MasterSlider", overlayRect, -248f);

            Text musicValue = CreateVolumeLabel("MusicValue", overlayRect, "BGM 音量：100%", theme, -300f);
            Slider musicSlider = CreateSlider("MusicSlider", overlayRect, -348f);

            Text sfxValue = CreateVolumeLabel("SfxValue", overlayRect, "音效音量：100%", theme, -400f);
            Slider sfxSlider = CreateSlider("SfxSlider", overlayRect, -448f);

            Text closeHint = CreateText(
                "CloseHint",
                overlayRect,
                "ESC 返回游戏  ·  点击应用保存  ·  垂直同步开启时，帧率上限作为备用值",
                theme,
                22,
                FontStyle.Normal,
                HintColor,
                TextAnchor.MiddleLeft);
            SetTopLeft(closeHint.rectTransform, new Vector2(100f, -990f), new Vector2(1720f, 36f));

            GameObject toggleObject = DefaultControls.CreateToggle(new DefaultControls.Resources());
            toggleObject.name = "VSyncToggle";
            toggleObject.transform.SetParent(overlayRect, false);
            SetTopLeft((RectTransform)toggleObject.transform, new Vector2(100f, -515f), new Vector2(340f, 44f));
            Toggle vSyncToggle = toggleObject.GetComponent<Toggle>();
            Image check = vSyncToggle.graphic as Image;
            if (check != null) { check.sprite = GameUiStyle.GetSolidSprite(); check.color = HandleColor; }
            Text toggleLabel = toggleObject.GetComponentInChildren<Text>();
            toggleLabel.font = theme.ResolveFont(); toggleLabel.fontSize = 24; toggleLabel.color = TextColor; toggleLabel.text = "垂直同步";

            Text fpsLabel = CreateText("FrameRateLabel", overlayRect, "帧率上限", theme, 24, FontStyle.Normal, TextColor, TextAnchor.MiddleLeft);
            SetTopLeft(fpsLabel.rectTransform, new Vector2(520f, -515f), new Vector2(180f, 44f));
            GameObject dropdownObject = DefaultControls.CreateDropdown(new DefaultControls.Resources());
            dropdownObject.name = "FrameRateDropdown";
            dropdownObject.transform.SetParent(overlayRect, false);
            SetTopLeft((RectTransform)dropdownObject.transform, new Vector2(720f, -515f), new Vector2(300f, 44f));
            Dropdown frameRateDropdown = dropdownObject.GetComponent<Dropdown>();
            frameRateDropdown.ClearOptions();
            frameRateDropdown.AddOptions(new System.Collections.Generic.List<string> { "30 FPS", "60 FPS", "120 FPS", "144 FPS", "240 FPS", "不限帧" });
            foreach (Text text in dropdownObject.GetComponentsInChildren<Text>(true))
            { text.font = theme.ResolveFont(); text.fontSize = 22; text.color = TextColor; }
            Transform arrow = dropdownObject.transform.Find("Arrow");
            if (arrow != null)
            {
                arrow.GetComponent<Image>().enabled = false;
                Text arrowText = CreateText("ArrowText", (RectTransform)arrow, "▼", theme, 18, FontStyle.Normal, TextColor, TextAnchor.MiddleCenter);
                Stretch(arrowText.rectTransform);
            }
            foreach (Toggle option in dropdownObject.GetComponentsInChildren<Toggle>(true))
            {
                Image tick = option.graphic as Image;
                if (tick != null) { tick.sprite = GameUiStyle.GetSolidSprite(); tick.color = HandleColor; }
            }

            Text status = CreateText("TransactionStatus", overlayRect, "设置已就绪。", theme, 24, FontStyle.Normal, TextColor, TextAnchor.MiddleLeft);
            SetTopLeft(status.rectTransform, new Vector2(100f, -740f), new Vector2(1720f, 64f));
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            Button apply = CreateActionButton("ApplySettings", "应用", overlayRect, theme, 100f);
            Button cancel = CreateActionButton("CancelSettings", "取消", overlayRect, theme, 450f);
            Button defaults = CreateActionButton("ResetSettings", "恢复默认", overlayRect, theme, 800f);
            Button retry = CreateActionButton("RetrySettings", "重试保存", overlayRect, theme, 1150f);

            Button closeButton = CreateCloseButton(overlayRect, theme);

            SettingsPanelView view = canvasObject.AddComponent<SettingsPanelView>();
            view.Initialize(
                overlayObject,
                canvasGroup,
                masterSlider,
                musicSlider,
                sfxSlider,
                masterValue,
                musicValue,
                sfxValue,
                closeButton);
            view.InitializeTransactions(apply, cancel, defaults, retry, status, vSyncToggle, frameRateDropdown);
            DisplaySettingsView.Create(overlayRect, theme.ResolveFont());
            return view;
        }

        private static Button CreateActionButton(string name, string value, RectTransform parent, UIThemeConfig theme, float x)
        {
            var root = CreateRectObject(name, parent);
            var rect = (RectTransform)root.transform;
            SetTopLeft(rect, new Vector2(x, -846f), new Vector2(300f, 64f));
            var background = root.AddComponent<Image>(); background.sprite = GetRoundedSquareSprite();
            background.type = Image.Type.Sliced; background.color = HandleColor;
            var button = root.AddComponent<Button>(); button.targetGraphic = background;
            Text label = CreateText("Text", rect, value, theme, 26, FontStyle.Normal, Color.white, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return button;
        }

        private static Text CreateVolumeLabel(
            string name,
            RectTransform parent,
            string value,
            UIThemeConfig theme,
            float y)
        {
            Text label = CreateText(
                name,
                parent,
                value,
                theme,
                32,
                FontStyle.Bold,
                TextColor,
                TextAnchor.MiddleLeft);
            SetTopLeft(label.rectTransform, new Vector2(100f, y), new Vector2(620f, 46f));
            return label;
        }

        private static Slider CreateSlider(string name, RectTransform parent, float y)
        {
            GameObject sliderObject = CreateRectObject(name, parent);
            RectTransform sliderRect = sliderObject.transform as RectTransform;
            SetTopLeft(sliderRect, new Vector2(420f, y), new Vector2(1350f, 44f));

            GameObject outerTrackObject = CreateRectObject("TrackBorder", sliderRect);
            RectTransform outerTrackRect = outerTrackObject.transform as RectTransform;
            SetHorizontalBar(outerTrackRect, 20f);
            Image outerTrack = outerTrackObject.AddComponent<Image>();
            outerTrack.sprite = GetRoundedBarSprite();
            outerTrack.type = Image.Type.Sliced;
            outerTrack.color = Color.black;
            outerTrack.raycastTarget = true;

            GameObject innerTrackObject = CreateRectObject("Track", outerTrackRect);
            RectTransform innerTrackRect = innerTrackObject.transform as RectTransform;
            Stretch(innerTrackRect);
            innerTrackRect.offsetMin = new Vector2(3f, 3f);
            innerTrackRect.offsetMax = new Vector2(-3f, -3f);
            Image innerTrack = innerTrackObject.AddComponent<Image>();
            innerTrack.sprite = GetRoundedBarSprite();
            innerTrack.type = Image.Type.Sliced;
            innerTrack.color = TrackColor;
            innerTrack.raycastTarget = false;

            GameObject handleAreaObject = CreateRectObject("Handle Slide Area", sliderRect);
            RectTransform handleAreaRect = handleAreaObject.transform as RectTransform;
            Stretch(handleAreaRect);
            handleAreaRect.offsetMin = new Vector2(18f, 0f);
            handleAreaRect.offsetMax = new Vector2(-18f, 0f);

            GameObject handleObject = CreateRectObject("Handle", handleAreaRect);
            RectTransform handleRect = handleObject.transform as RectTransform;
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.anchoredPosition = Vector2.zero;
            // Slider stretches the handle on the cross axis, so the negative Y delta
            // keeps the final handle square inside the 44 px slider row.
            handleRect.sizeDelta = new Vector2(34f, -10f);
            Image handleBorder = handleObject.AddComponent<Image>();
            handleBorder.sprite = GetRoundedSquareSprite();
            handleBorder.type = Image.Type.Sliced;
            handleBorder.color = Color.black;

            GameObject handleInnerObject = CreateRectObject("HandleInner", handleRect);
            RectTransform handleInnerRect = handleInnerObject.transform as RectTransform;
            Stretch(handleInnerRect);
            handleInnerRect.offsetMin = new Vector2(4f, 4f);
            handleInnerRect.offsetMax = new Vector2(-4f, -4f);
            Image handleInner = handleInnerObject.AddComponent<Image>();
            handleInner.sprite = GetRoundedSquareSprite();
            handleInner.type = Image.Type.Sliced;
            handleInner.color = HandleColor;
            handleInner.raycastTarget = false;

            Slider slider = sliderObject.AddComponent<Slider>();
            slider.fillRect = null;
            slider.handleRect = handleRect;
            slider.targetGraphic = handleBorder;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.value = 1f;

            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.82f, 0.86f, 0.92f, 1f);
            colors.pressedColor = new Color(0.68f, 0.73f, 0.80f, 1f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(0.35f, 0.37f, 0.40f, 0.65f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            slider.colors = colors;
            return slider;
        }

        private static Button CreateCloseButton(RectTransform parent, UIThemeConfig theme)
        {
            GameObject buttonObject = CreateRectObject("CloseButton", parent);
            RectTransform rect = buttonObject.transform as RectTransform;
            SetTopLeft(rect, new Vector2(1768f, -58f), new Vector2(52f, 52f));

            Image image = buttonObject.AddComponent<Image>();
            image.sprite = GetRoundedSquareSprite();
            image.type = Image.Type.Sliced;
            image.color = HandleColor;
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text label = CreateText(
                "Text",
                rect,
                "×",
                theme,
                32,
                FontStyle.Bold,
                Color.white,
                TextAnchor.MiddleCenter);
            Stretch(label.rectTransform);
            return button;
        }

        private static Text CreateText(
            string name,
            RectTransform parent,
            string value,
            UIThemeConfig theme,
            int fontSize,
            FontStyle fontStyle,
            Color color,
            TextAnchor alignment)
        {
            GameObject textObject = CreateRectObject(name, parent);
            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.font = theme.ResolveFont();
            text.fontSize = fontSize;
            text.fontStyle = fontStyle;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite GetRoundedBarSprite()
        {
            if (roundedBarSprite == null)
            {
                roundedBarSprite = CreateRoundedSprite("SettingsRoundedBar", 32, 16, 8);
            }

            return roundedBarSprite;
        }

        private static Sprite GetRoundedSquareSprite()
        {
            if (roundedSquareSprite == null)
            {
                roundedSquareSprite = CreateRoundedSprite("SettingsRoundedSquare", 32, 32, 6);
            }

            return roundedSquareSprite;
        }

        private static Sprite CreateRoundedSprite(string name, int width, int height, int radius)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.name = name + "Texture";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Bilinear;

            Color clear = new Color(1f, 1f, 1f, 0f);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool inside = IsInsideRoundedRect(x, y, width, height, radius);
                    texture.SetPixel(x, y, inside ? Color.white : clear);
                }
            }

            texture.Apply();
            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius, radius, radius, radius));
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        private static bool IsInsideRoundedRect(int x, int y, int width, int height, int radius)
        {
            if (x >= radius && x < width - radius || y >= radius && y < height - radius)
            {
                return true;
            }

            float centerX = x < radius ? radius - 0.5f : width - radius - 0.5f;
            float centerY = y < radius ? radius - 0.5f : height - radius - 0.5f;
            float deltaX = x - centerX;
            float deltaY = y - centerY;
            return deltaX * deltaX + deltaY * deltaY <= radius * radius;
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

        private static void SetHorizontalBar(RectTransform rect, float height)
        {
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(1f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(0f, height);
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
