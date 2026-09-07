using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [CreateAssetMenu(menuName = "StudentGrowth/UI/UI Theme Config")]
    public sealed class UIThemeConfig : ScriptableObject
    {
        [Header("Sprites")]
        public Sprite panelSprite;
        public Sprite buttonSprite;
        public Sprite buttonHoverSprite;
        public Sprite buttonPressedSprite;
        public Sprite buttonDisabledSprite;
        public Sprite buttonSelectedSprite;
        public Sprite hudPlateSprite;
        public Sprite taskPanelSprite;
        public Sprite promptBarSprite;
        public Sprite quickbarSprite;
        public Sprite quickbarSlotSprite;
        public Sprite menuWindowSprite;
        public Sprite dialoguePanelSprite;
        public Sprite shopPanelSprite;
        public Sprite inventoryPanelSprite;
        public Sprite battlePanelSprite;
        public Sprite resultPanelSprite;

        [Header("Icons")]
        public Sprite moneyIcon;
        public Sprite timeIcon;
        public Sprite energyIcon;
        public Sprite moodIcon;
        public Sprite stressIcon;
        public Sprite relationIcon;
        public Sprite inventoryIcon;
        public Sprite cardsIcon;
        public Sprite settingsIcon;
        public Sprite backIcon;
        public Sprite confirmIcon;
        public Sprite cancelIcon;

        [Header("Time HUD")]
        public Sprite morningPeriodIcon;
        public Sprite afternoonPeriodIcon;
        public Sprite eveningPeriodIcon;
        public Sprite nightPeriodIcon;
        public Sprite actionPointFilledIcon;
        public Sprite actionPointEmptyIcon;
        public Color actionPointFilledColor = new Color(0.92f, 0.70f, 0.25f, 1f);
        public Color actionPointEmptyColor = new Color(0.48f, 0.42f, 0.36f, 0.72f);

        [Header("Font")]
        public Font font;
        public int titleSize = 28;
        public int subtitleSize = 22;
        public int bodySize = 18;
        public int buttonTextSize = 18;
        public int statTextSize = 18;
        public int hintTextSize = 18;
        public int smallTextSize = 14;

        [Header("Panel Colors")]
        public Color panelBackground = new Color(0.78f, 0.68f, 0.50f, 0.94f);
        public Color panelDarkBackground = new Color(0.21f, 0.16f, 0.12f, 0.94f);
        public Color panelBorder = new Color(0.29f, 0.18f, 0.10f, 1f);
        public Color panelShadow = new Color(0.08f, 0.06f, 0.04f, 0.35f);

        [Header("Text Colors")]
        public Color titleText = new Color(0.22f, 0.12f, 0.07f, 1f);
        public Color bodyText = new Color(0.28f, 0.18f, 0.10f, 1f);
        public Color inverseText = new Color(0.93f, 0.88f, 0.73f, 1f);
        public Color hintText = new Color(0.88f, 0.68f, 0.22f, 1f);
        public Color disabledText = new Color(0.48f, 0.42f, 0.36f, 1f);

        [Header("Button Colors")]
        public Color buttonNormal = new Color(0.55f, 0.39f, 0.24f, 1f);
        public Color buttonHover = new Color(0.83f, 0.62f, 0.25f, 1f);
        public Color buttonPressed = new Color(0.36f, 0.23f, 0.14f, 1f);
        public Color buttonSelected = new Color(0.42f, 0.25f, 0.12f, 1f);
        public Color buttonDisabled = new Color(0.38f, 0.35f, 0.31f, 0.72f);

        [Header("Semantic Colors")]
        public Color danger = new Color(0.58f, 0.18f, 0.15f, 1f);
        public Color success = new Color(0.28f, 0.52f, 0.32f, 1f);
        public Color gold = new Color(0.92f, 0.70f, 0.25f, 1f);
        public Color time = new Color(0.86f, 0.47f, 0.18f, 1f);

        [Header("Spacing")]
        public int panelPadding = 16;
        public int panelBorderThickness = 3;
        public int buttonBorderThickness = 2;

        [Header("Animation")]
        public float panelFadeDuration = 0.16f;
        public float buttonHoverScale = 1.04f;
        public float buttonPressedScale = 0.97f;
        public float floatingTextDuration = 0.8f;

        public Font ResolveFont()
        {
            return font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        public int GetFontSize(GameUiTextStyle textStyle)
        {
            switch (textStyle)
            {
                case GameUiTextStyle.Title:
                    return titleSize;
                case GameUiTextStyle.Subtitle:
                    return subtitleSize;
                case GameUiTextStyle.ButtonText:
                    return buttonTextSize;
                case GameUiTextStyle.StatText:
                    return statTextSize;
                case GameUiTextStyle.HintText:
                    return hintTextSize;
                case GameUiTextStyle.SmallText:
                    return smallTextSize;
                default:
                    return bodySize;
            }
        }

        public Color GetTextColor(GameUiTextStyle textStyle, bool inverse = false)
        {
            if (inverse)
            {
                return inverseText;
            }

            switch (textStyle)
            {
                case GameUiTextStyle.Title:
                case GameUiTextStyle.Subtitle:
                    return titleText;
                case GameUiTextStyle.HintText:
                    return hintText;
                default:
                    return bodyText;
            }
        }

        public ColorBlock CreateButtonColorBlock()
        {
            ColorBlock colors = ColorBlock.defaultColorBlock;
            colors.normalColor = buttonNormal;
            colors.highlightedColor = buttonHover;
            colors.pressedColor = buttonPressed;
            colors.selectedColor = buttonSelected;
            colors.disabledColor = buttonDisabled;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.06f;
            return colors;
        }

        public static UIThemeConfig CreateRuntimeDefault()
        {
            UIThemeConfig config = CreateInstance<UIThemeConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }

        private void OnValidate()
        {
            titleSize = Mathf.Max(1, titleSize);
            subtitleSize = Mathf.Max(1, subtitleSize);
            bodySize = Mathf.Max(1, bodySize);
            buttonTextSize = Mathf.Max(1, buttonTextSize);
            statTextSize = Mathf.Max(1, statTextSize);
            hintTextSize = Mathf.Max(1, hintTextSize);
            smallTextSize = Mathf.Max(1, smallTextSize);
            panelPadding = Mathf.Max(0, panelPadding);
            panelBorderThickness = Mathf.Max(0, panelBorderThickness);
            buttonBorderThickness = Mathf.Max(0, buttonBorderThickness);
            panelFadeDuration = Mathf.Max(0f, panelFadeDuration);
            buttonHoverScale = Mathf.Max(0.01f, buttonHoverScale);
            buttonPressedScale = Mathf.Max(0.01f, buttonPressedScale);
            floatingTextDuration = Mathf.Max(0f, floatingTextDuration);
        }
    }
}
