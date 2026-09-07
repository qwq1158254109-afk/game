using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class PlayerStatusView : MonoBehaviour
    {
        public PixelPanel panel;
        public Text energyText;
        public Text pressureText;
        public Text moodText;
        public Image energyBarBack;
        public Image pressureBarBack;
        public Image moodBarBack;
        public Image energyBarFill;
        public Image pressureBarFill;
        public Image moodBarFill;
        public Graphic[] obsoleteGraphics = new Graphic[0];
        public UIThemeConfig theme;

        public void Configure(
            PixelPanel targetPanel,
            Text energy,
            Text pressure,
            Text mood,
            Image energyBack,
            Image pressureBack,
            Image moodBack,
            Image energyFill,
            Image pressureFill,
            Image moodFill,
            Graphic[] hiddenGraphics,
            UIThemeConfig config)
        {
            panel = targetPanel;
            energyText = energy;
            pressureText = pressure;
            moodText = mood;
            energyBarBack = energyBack;
            pressureBarBack = pressureBack;
            moodBarBack = moodBack;
            energyBarFill = energyFill;
            pressureBarFill = pressureFill;
            moodBarFill = moodFill;
            obsoleteGraphics = hiddenGraphics ?? new Graphic[0];
            theme = config;
            ApplyLayoutAndStyle();
        }

        public void ApplyLayoutAndStyle()
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (panel != null)
            {
                panel.theme = resolved;
                panel.variant = GameUiPanelVariant.Dark;
                panel.ApplyStyle();
                if (panel.background != null)
                {
                    GameUiStyle.ApplyPanelSprite(panel.background, resolved.hudPlateSprite, resolved.panelDarkBackground);
                }

                SetRect(panel.transform as RectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(18f, -18f), new Vector2(340f, 104f));
            }

            for (int i = 0; i < obsoleteGraphics.Length; i++)
            {
                if (obsoleteGraphics[i] != null)
                {
                    obsoleteGraphics[i].gameObject.SetActive(false);
                }
            }

            ConfigureRow(energyText, energyBarBack, energyBarFill, -16f, resolved.success);
            ConfigureRow(pressureText, pressureBarBack, pressureBarFill, -46f, resolved.danger);
            ConfigureRow(moodText, moodBarBack, moodBarFill, -76f, resolved.gold);
        }

        public void Refresh(PlayerState player)
        {
            SetText(energyText, player == null ? "精力 -" : "精力 " + player.energy + "/" + player.maxEnergy);
            SetText(pressureText, player == null ? "压力 -" : "压力 " + player.pressure + "/100");
            SetText(moodText, player == null ? "心情 -" : "心情 " + player.mood + "/100");

            SetFill(energyBarFill, player == null || player.maxEnergy <= 0 ? 0f : (float)player.energy / player.maxEnergy);
            SetFill(pressureBarFill, player == null ? 0f : player.pressure / 100f);
            SetFill(moodBarFill, player == null ? 0f : player.mood / 100f);
        }

        private void ConfigureRow(Text label, Image back, Image fill, float y, Color semanticColor)
        {
            if (label != null)
            {
                GameUiStyle.ApplyPixelText(label, theme, GameUiTextStyle.SmallText, true);
                label.fontSize = 14;
                label.fontStyle = FontStyle.Bold;
                label.alignment = TextAnchor.MiddleLeft;
                label.color = semanticColor;
                SetRect(label.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                    new Vector2(0f, 1f), new Vector2(14f, y), new Vector2(98f, 22f));
            }

            ConfigureBar(back, new Color(0.08f, 0.06f, 0.04f, 0.72f), y - 6f);
            ConfigureBar(fill, semanticColor, y - 6f, true);
        }

        private static void ConfigureBar(Image image, Color color, float y, bool filled = false)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = GameUiStyle.GetSolidSprite();
            image.type = filled ? Image.Type.Filled : Image.Type.Simple;
            if (filled)
            {
                image.fillMethod = Image.FillMethod.Horizontal;
                image.fillOrigin = 0;
            }

            image.color = color;
            image.raycastTarget = false;
            SetRect(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), new Vector2(116f, y), new Vector2(208f, 10f));
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static void SetFill(Image target, float value)
        {
            if (target != null)
            {
                target.fillAmount = Mathf.Clamp01(value);
            }
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}
