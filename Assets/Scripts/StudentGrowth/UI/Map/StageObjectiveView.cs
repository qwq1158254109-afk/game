using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class StageObjectiveView : MonoBehaviour
    {
        public PixelPanel panel;
        public Text titleText;
        public Text objectiveText;
        public Text remainingText;
        public UIThemeConfig theme;
        public bool showWhenAvailable;

        public void Configure(PixelPanel targetPanel, Text title, Text objective, Text remaining,
            UIThemeConfig config, bool visibleWhenAvailable)
        {
            panel = targetPanel;
            titleText = title;
            objectiveText = objective;
            remainingText = remaining;
            theme = config;
            showWhenAvailable = visibleWhenAvailable;
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
                        resolved.taskPanelSprite != null ? resolved.taskPanelSprite : resolved.hudPlateSprite,
                        resolved.panelDarkBackground);
                }

                SetRect(panel.transform as RectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                    new Vector2(1f, 1f), new Vector2(-18f, -106f), new Vector2(292f, 76f));
            }

            ConfigureText(titleText, GameUiTextStyle.SmallText, FontStyle.Bold,
                new Vector2(14f, -10f), new Vector2(264f, 20f));
            ConfigureText(objectiveText, GameUiTextStyle.SmallText, FontStyle.Normal,
                new Vector2(14f, -32f), new Vector2(264f, 22f));
            ConfigureText(remainingText, GameUiTextStyle.SmallText, FontStyle.Normal,
                new Vector2(14f, -54f), new Vector2(264f, 18f));
        }

        public void Refresh(StageState stageState)
        {
            StageData stage = stageState == null ? null : stageState.currentStage;
            string objective = stage == null ? string.Empty : stage.description;
            if (string.IsNullOrWhiteSpace(objective) && stage != null)
            {
                objective = stage.displayName;
            }

            bool visible = showWhenAvailable && !string.IsNullOrWhiteSpace(objective);
            if (panel != null)
            {
                panel.gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            titleText.text = "当前目标";
            objectiveText.text = objective.Trim();
            remainingText.text = "阶段剩余 " + stageState.remainingTime;
        }

        private void ConfigureText(Text text, GameUiTextStyle style, FontStyle fontStyle,
            Vector2 position, Vector2 size)
        {
            if (text == null)
            {
                return;
            }

            GameUiStyle.ApplyPixelText(text, theme, style, true);
            text.fontSize = style == GameUiTextStyle.SmallText ? 13 : text.fontSize;
            text.fontStyle = fontStyle;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            SetRect(text.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(0f, 1f), position, size);
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
