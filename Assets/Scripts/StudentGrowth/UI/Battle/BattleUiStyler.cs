using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public static class BattleUiStyler
    {
        public static UIThemeConfig ResolveTheme(BattleUIConfig config)
        {
            return GameUiStyle.ResolveTheme(config == null ? null : config.theme);
        }

        public static void ApplyPanel(RectTransform panel, BattleUIConfig config, GameUiPanelVariant variant = GameUiPanelVariant.Standard)
        {
            if (panel == null)
            {
                return;
            }

            UIThemeConfig theme = ResolveTheme(config);
            Image image = panel.GetComponent<Image>();
            GameUiStyle.ApplyPanel(image, theme, variant);
            RecolorLegacyBorder(panel, GetPanelBorderColor(theme, variant));
        }

        public static void ApplyImagePanel(Image image, BattleUIConfig config, GameUiPanelVariant variant = GameUiPanelVariant.Standard)
        {
            if (image == null)
            {
                return;
            }

            UIThemeConfig theme = ResolveTheme(config);
            GameUiStyle.ApplyPanel(image, theme, variant);
        }

        public static void ApplyText(Text text, BattleUIConfig config, GameUiTextStyle style, bool inverse = false)
        {
            GameUiStyle.ApplyPixelText(text, ResolveTheme(config), style, inverse);
        }

        public static void ApplyActionButton(Button button, BattleUIConfig config)
        {
            if (button == null)
            {
                return;
            }

            UIThemeConfig theme = ResolveTheme(config);
            Image background = button.targetGraphic as Image;
            GameUiStyle.ApplyButton(button, background, theme);
            ApplyText(button.GetComponentInChildren<Text>(true), config, GameUiTextStyle.ButtonText, true);
            RecolorLegacyBorder(button.transform as RectTransform, theme.gold);

            PixelButton pixelButton = button.GetComponent<PixelButton>();
            if (pixelButton == null)
            {
                pixelButton = button.gameObject.AddComponent<PixelButton>();
            }

            pixelButton.theme = theme;
            pixelButton.background = background;
            pixelButton.labelText = button.GetComponentInChildren<Text>(true);
            pixelButton.animateScale = true;
            pixelButton.ApplyStyle();
        }

        public static void ApplyTabButton(Button button, BattleUIConfig config, bool selected)
        {
            ApplyActionButton(button, config);

            PixelButton pixelButton = button == null ? null : button.GetComponent<PixelButton>();
            if (pixelButton != null)
            {
                pixelButton.SetSelected(selected);
            }
        }

        public static void ApplyCardButton(Button button, Image background, BattleUIConfig config)
        {
            if (button == null)
            {
                return;
            }

            UIThemeConfig theme = ResolveTheme(config);
            button.transition = Selectable.Transition.None;
            PixelButton pixelButton = button.GetComponent<PixelButton>();
            if (pixelButton == null)
            {
                pixelButton = button.gameObject.AddComponent<PixelButton>();
            }

            pixelButton.applyOnAwake = false;
            pixelButton.theme = theme;
            pixelButton.background = background;
            pixelButton.labelText = button.GetComponentInChildren<Text>(true);
            pixelButton.animateScale = true;

            if (background != null)
            {
                background.sprite = theme.panelSprite != null ? theme.panelSprite : GameUiStyle.GetSolidSprite();
                background.type = theme.panelSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                background.color = GetCardBaseColor(config);
                button.targetGraphic = background;
            }

            RecolorLegacyBorder(button.transform as RectTransform, theme.panelBorder);
        }

        public static void ApplyCardText(Text text, BattleUIConfig config, GameUiTextStyle style, bool inverse = false)
        {
            GameUiStyle.ApplyPixelText(text, ResolveTheme(config), style, inverse);
        }

        public static void ApplyProgressBar(RectTransform track, Image fill, BattleUIConfig config, Color fillColor)
        {
            if (track == null)
            {
                return;
            }

            UIThemeConfig theme = ResolveTheme(config);
            Image trackImage = track.GetComponent<Image>();
            if (trackImage != null)
            {
                trackImage.sprite = GameUiStyle.GetSolidSprite();
                trackImage.color = Color.Lerp(theme.panelDarkBackground, Color.black, 0.20f);
            }

            if (fill != null)
            {
                fill.sprite = GameUiStyle.GetSolidSprite();
                fill.color = fillColor;
            }

            RecolorLegacyBorder(track, theme.panelBorder);
        }

        public static void ApplyBattlefieldWash(Image image, BattleUIConfig config)
        {
            if (image == null)
            {
                return;
            }

            UIThemeConfig theme = ResolveTheme(config);
            image.sprite = GameUiStyle.GetSolidSprite();
            image.color = new Color(
                theme.panelDarkBackground.r,
                theme.panelDarkBackground.g,
                theme.panelDarkBackground.b,
                0.72f);
        }

        public static Color GetCardBaseColor(BattleUIConfig config)
        {
            UIThemeConfig theme = ResolveTheme(config);
            return Color.Lerp(theme.panelBackground, theme.panelDarkBackground, 0.18f);
        }

        public static Color GetPlayableCardColor(BattleUIConfig config)
        {
            UIThemeConfig theme = ResolveTheme(config);
            float glow = config == null ? 0.35f : config.playableCardGlowAlpha;
            return Color.Lerp(GetCardBaseColor(config), theme.success, glow);
        }

        public static Color GetUnplayableCardColor(BattleUIConfig config)
        {
            UIThemeConfig theme = ResolveTheme(config);
            return Color.Lerp(GetCardBaseColor(config), theme.buttonDisabled, 0.55f);
        }

        public static Color GetWarningColor(BattleUIConfig config)
        {
            return ResolveTheme(config).danger;
        }

        public static Color GetGoldColor(BattleUIConfig config)
        {
            return ResolveTheme(config).gold;
        }

        public static Color GetSuccessColor(BattleUIConfig config)
        {
            return ResolveTheme(config).success;
        }

        private static Color GetPanelBorderColor(UIThemeConfig theme, GameUiPanelVariant variant)
        {
            switch (variant)
            {
                case GameUiPanelVariant.Success:
                    return theme.success;
                case GameUiPanelVariant.Warning:
                    return theme.danger;
                default:
                    return theme.panelBorder;
            }
        }

        private static void RecolorLegacyBorder(RectTransform rect, Color color)
        {
            if (rect == null)
            {
                return;
            }

            SetBorderColor(rect, "TopBorder", color);
            SetBorderColor(rect, "BottomBorder", color);
            SetBorderColor(rect, "LeftBorder", color);
            SetBorderColor(rect, "RightBorder", color);
        }

        private static void SetBorderColor(RectTransform rect, string childName, Color color)
        {
            Transform child = rect.Find(childName);
            Image image = child == null ? null : child.GetComponent<Image>();
            if (image != null)
            {
                image.color = color;
            }
        }
    }
}
