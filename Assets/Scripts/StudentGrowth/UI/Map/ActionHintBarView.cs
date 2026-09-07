using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class ActionHintBarView : MonoBehaviour
    {
        private static readonly float[] SlotWidths = { 112f, 116f, 100f };

        public PixelPanel panel;
        public Text[] labels = new Text[0];
        public Image[] icons = new Image[0];
        public UIThemeConfig theme;

        public void Configure(PixelPanel targetPanel, Text[] slotLabels, Image[] slotIcons, UIThemeConfig config)
        {
            panel = targetPanel;
            labels = slotLabels ?? new Text[0];
            icons = slotIcons ?? new Image[0];
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
                    GameUiStyle.ApplyPanelSprite(panel.background,
                        resolved.quickbarSprite != null ? resolved.quickbarSprite : resolved.hudPlateSprite,
                        resolved.panelDarkBackground);
                }

                SetRect(panel.transform as RectTransform, Vector2.zero, Vector2.zero, Vector2.zero,
                    new Vector2(18f, 18f), new Vector2(360f, 44f));
            }

            float x = 10f;
            for (int i = 0; i < labels.Length; i++)
            {
                Text label = labels[i];
                Transform slot = label == null ? null : label.transform.parent;
                bool active = i < SlotWidths.Length;
                if (slot != null)
                {
                    slot.gameObject.SetActive(active);
                }

                if (!active || label == null)
                {
                    continue;
                }

                RectTransform slotRect = slot as RectTransform;
                float width = SlotWidths[i];
                SetRect(slotRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                    new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(width, 30f));
                x += width + 4f;

                GameUiStyle.ApplyPixelText(label, resolved, GameUiTextStyle.SmallText, true);
                label.fontSize = 12;
                label.fontStyle = FontStyle.Normal;
                label.alignment = TextAnchor.MiddleCenter;
                SetRect(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f),
                    Vector2.zero, Vector2.zero);

                PixelPanel slotPanel = slot.GetComponent<PixelPanel>();
                if (slotPanel != null)
                {
                    slotPanel.theme = resolved;
                    slotPanel.variant = GameUiPanelVariant.Dark;
                    slotPanel.ApplyStyle();
                }

                QuickbarTooltipTarget tooltip = slot.GetComponent<QuickbarTooltipTarget>();
                if (tooltip != null)
                {
                    tooltip.tooltipText = label.text;
                }
            }

            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null)
                {
                    icons[i].gameObject.SetActive(false);
                }
            }
        }

        public void RefreshBindings(RpgPlayerController player)
        {
            if (panel != null)
            {
                panel.gameObject.SetActive(player != null);
            }

            if (player == null || labels.Length < 3)
            {
                return;
            }

            labels[0].text = DisplayKey(player.interactKey) + " 互动";
            labels[1].text = DisplayKey(player.runKey) + " 奔跑";
            labels[2].text = "Esc 返回";
            for (int i = 0; i < 3; i++)
            {
                QuickbarTooltipTarget tooltip = labels[i] == null ? null : labels[i].GetComponentInParent<QuickbarTooltipTarget>();
                if (tooltip != null)
                {
                    tooltip.tooltipText = labels[i].text;
                }
            }
        }

        private static string DisplayKey(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.LeftShift:
                case KeyCode.RightShift:
                    return "Shift";
                case KeyCode.Escape:
                    return "Esc";
                case KeyCode.Return:
                    return "Enter";
                case KeyCode.Space:
                    return "Space";
                default:
                    return key.ToString();
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
