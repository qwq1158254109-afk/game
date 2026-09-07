using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public enum GameUiTextStyle
    {
        Title,
        Subtitle,
        Body,
        ButtonText,
        StatText,
        HintText,
        SmallText
    }

    public enum GameUiPanelVariant
    {
        Standard,
        Dark,
        Success,
        Warning
    }

    public static class GameUiStyle
    {
        private const string DefaultThemeResourcePath = "StudentGrowth/UI/StudentGrowthPixelTheme";

        private static UIThemeConfig runtimeDefaultTheme;
        private static Sprite solidSprite;

        public static UIThemeConfig ResolveTheme(UIThemeConfig theme)
        {
            if (theme != null)
            {
                return theme;
            }

            if (runtimeDefaultTheme == null)
            {
                runtimeDefaultTheme = Resources.Load<UIThemeConfig>(DefaultThemeResourcePath);
                if (runtimeDefaultTheme == null)
                {
                    runtimeDefaultTheme = UIThemeConfig.CreateRuntimeDefault();
                }
            }

            return runtimeDefaultTheme;
        }

        public static void ApplyText(Text target, UIThemeConfig theme, GameUiTextStyle textStyle, bool inverse = false)
        {
            if (target == null)
            {
                return;
            }

            UIThemeConfig resolved = ResolveTheme(theme);
            target.font = resolved.ResolveFont();
            target.fontSize = resolved.GetFontSize(textStyle);
            target.color = resolved.GetTextColor(textStyle, inverse);
            target.horizontalOverflow = HorizontalWrapMode.Wrap;
            target.verticalOverflow = VerticalWrapMode.Truncate;
        }

        public static void ApplyTextFromFontSpec(Text target, UIThemeConfig theme, int fontSize, FontStyle fontStyle, bool inverse = false)
        {
            if (target == null)
            {
                return;
            }

            ApplyPixelText(target, theme, ResolveTextStyle(fontSize, fontStyle), inverse);
            target.fontStyle = fontStyle;
        }

        public static PixelText ApplyPixelText(Text target, UIThemeConfig theme, GameUiTextStyle textStyle, bool inverse = false)
        {
            if (target == null)
            {
                return null;
            }

            PixelText pixelText = target.GetComponent<PixelText>();
            if (pixelText == null)
            {
                pixelText = target.gameObject.AddComponent<PixelText>();
            }

            pixelText.Configure(theme, textStyle, inverse, false);
            ApplyText(target, theme, textStyle, inverse);
            return pixelText;
        }

        public static GameUiTextStyle ResolveTextStyle(int fontSize, FontStyle fontStyle)
        {
            if (fontSize >= 28)
            {
                return GameUiTextStyle.Title;
            }

            if (fontSize >= 22 || fontStyle == FontStyle.Bold)
            {
                return GameUiTextStyle.Subtitle;
            }

            return fontSize <= 15 ? GameUiTextStyle.SmallText : GameUiTextStyle.Body;
        }

        public static void ApplyPanel(Image target, UIThemeConfig theme, GameUiPanelVariant variant = GameUiPanelVariant.Standard)
        {
            if (target == null)
            {
                return;
            }

            UIThemeConfig resolved = ResolveTheme(theme);
            ApplyPanelSprite(target, resolved.panelSprite, ResolvePanelColor(resolved, variant));
        }

        public static void ApplyPanelSprite(Image target, Sprite sprite, Color fallbackColor)
        {
            if (target == null)
            {
                return;
            }

            target.sprite = sprite != null ? sprite : GetSolidSprite();
            target.type = sprite != null ? Image.Type.Sliced : Image.Type.Simple;
            target.color = sprite != null ? Color.white : fallbackColor;
        }

        public static void ApplyButton(Button button, Image background, UIThemeConfig theme)
        {
            if (button == null)
            {
                return;
            }

            UIThemeConfig resolved = ResolveTheme(theme);
            if (background == null)
            {
                background = button.targetGraphic as Image;
            }

            if (background != null)
            {
                background.sprite = resolved.buttonSprite != null ? resolved.buttonSprite : GetSolidSprite();
                background.type = resolved.buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
                background.color = resolved.buttonSprite != null ? Color.white : button.interactable ? resolved.buttonNormal : resolved.buttonDisabled;
                button.targetGraphic = background;
            }

            if (HasButtonStateSprites(resolved))
            {
                SpriteState state = button.spriteState;
                state.highlightedSprite = resolved.buttonHoverSprite;
                state.pressedSprite = resolved.buttonPressedSprite;
                state.selectedSprite = resolved.buttonSelectedSprite != null ? resolved.buttonSelectedSprite : resolved.buttonHoverSprite;
                state.disabledSprite = resolved.buttonDisabledSprite;
                button.spriteState = state;
                button.transition = Selectable.Transition.SpriteSwap;
            }
            else
            {
                button.transition = Selectable.Transition.ColorTint;
                button.colors = resolved.CreateButtonColorBlock();
            }
        }

        public static void SetSelectedButtonVisual(Button button, Image background, UIThemeConfig theme, bool selected)
        {
            if (button == null || background == null)
            {
                return;
            }

            UIThemeConfig resolved = ResolveTheme(theme);
            if (selected && resolved.buttonSelectedSprite != null)
            {
                background.sprite = resolved.buttonSelectedSprite;
                background.type = Image.Type.Sliced;
            }
            else if (!button.interactable && resolved.buttonDisabledSprite != null)
            {
                background.sprite = resolved.buttonDisabledSprite;
                background.type = Image.Type.Sliced;
            }
            else
            {
                background.sprite = resolved.buttonSprite != null ? resolved.buttonSprite : GetSolidSprite();
                background.type = resolved.buttonSprite != null ? Image.Type.Sliced : Image.Type.Simple;
            }

            background.color = background.sprite != null && background.sprite != GetSolidSprite()
                ? Color.white
                : selected ? resolved.buttonSelected : button.interactable ? resolved.buttonNormal : resolved.buttonDisabled;
        }

        public static Color ResolvePanelColor(UIThemeConfig theme, GameUiPanelVariant variant)
        {
            UIThemeConfig resolved = ResolveTheme(theme);
            switch (variant)
            {
                case GameUiPanelVariant.Dark:
                    return resolved.panelDarkBackground;
                case GameUiPanelVariant.Success:
                    return resolved.success;
                case GameUiPanelVariant.Warning:
                    return resolved.danger;
                default:
                    return resolved.panelBackground;
            }
        }

        public static Sprite GetSolidSprite()
        {
            if (solidSprite != null)
            {
                return solidSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.name = "StudentGrowthUiSolid";
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.filterMode = FilterMode.Point;
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            solidSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.one * 0.5f, 1f);
            solidSprite.hideFlags = HideFlags.HideAndDontSave;
            return solidSprite;
        }

        private static bool HasButtonStateSprites(UIThemeConfig theme)
        {
            return theme != null
                && theme.buttonSprite != null
                && theme.buttonHoverSprite != null
                && theme.buttonPressedSprite != null
                && theme.buttonDisabledSprite != null;
        }
    }
}
