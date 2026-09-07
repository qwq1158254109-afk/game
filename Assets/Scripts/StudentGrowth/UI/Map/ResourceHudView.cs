using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class ResourceHudView : MonoBehaviour
    {
        public PixelPanel panel;
        public Image moneyIcon;
        public Image stageTimeIcon;
        public Text moneyText;
        public Text stageTimeText;
        public UIThemeConfig theme;

        public void Configure(PixelPanel targetPanel, Image money, Text moneyLabel, Image time,
            Text timeLabel, UIThemeConfig config)
        {
            panel = targetPanel;
            moneyIcon = money;
            moneyText = moneyLabel;
            stageTimeIcon = time;
            stageTimeText = timeLabel;
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
                    new Vector2(0f, 1f), new Vector2(18f, -136f), new Vector2(340f, 40f));
            }

            ApplyIcon(moneyIcon, resolved.moneyIcon, resolved.gold, new Vector2(14f, -11f));
            ApplyText(moneyText, resolved.gold, new Vector2(38f, -10f), new Vector2(104f, 20f));
            ApplyIcon(stageTimeIcon, resolved.timeIcon, resolved.time, new Vector2(154f, -11f));
            ApplyText(stageTimeText, resolved.time, new Vector2(178f, -10f), new Vector2(148f, 20f));
        }

        public void Refresh(PlayerState player, StageState stageState)
        {
            if (moneyText != null)
            {
                moneyText.text = player == null ? "金币 -" : "金币 " + player.money;
            }

            bool hasStage = stageState != null && stageState.currentStage != null;
            if (stageTimeIcon != null)
            {
                stageTimeIcon.gameObject.SetActive(hasStage);
            }

            if (stageTimeText != null)
            {
                stageTimeText.gameObject.SetActive(hasStage);
                stageTimeText.text = hasStage ? "阶段剩余 " + stageState.remainingTime : string.Empty;
            }
        }

        private void ApplyText(Text text, Color color, Vector2 position, Vector2 size)
        {
            if (text == null)
            {
                return;
            }

            GameUiStyle.ApplyPixelText(text, theme, GameUiTextStyle.SmallText, true);
            text.fontSize = 14;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = color;
            SetRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), position, size);
        }

        private static void ApplyIcon(Image image, Sprite sprite, Color color, Vector2 position)
        {
            if (image == null)
            {
                return;
            }

            image.sprite = sprite;
            image.enabled = sprite != null;
            image.color = color;
            image.preserveAspect = true;
            image.raycastTarget = false;
            SetRect(image.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), position, new Vector2(18f, 18f));
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
