using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    /// <summary>
    /// 阶段成长报告：打完阶段 Boss 后弹出，把「这一阶段结束时我是什么样」摊开给玩家看。
    ///
    /// 只读不写：不改任何属性，也不发奖励（奖励在战斗结算里已经发过了）。
    /// 目的单一——让成长被看见。之前属性只在 HUD 上默默变化，玩家感受不到「变强」。
    /// </summary>
    public sealed class StageReportView : MonoBehaviour
    {
        private const float PanelWidth = 900f;
        private const float PanelHeight = 640f;
        private const float RowHeight = 44f;

        public UIThemeConfig theme;
        public GameObject panelRoot;
        public GameObject overlayRoot;
        public PixelPanel panelStyle;
        public CanvasGroup canvasGroup;
        public Text titleText;
        public Text stageText;
        public Text statsText;
        public Text highlightsText;
        public Button closeButton;

        private Action onClosed;

        public void BuildDefaultLayout(RectTransform parent)
        {
            if (parent == null || panelRoot != null)
            {
                return;
            }

            // 遮罩：挡住地图点击，避免报告开着还能走路。
            GameObject overlayObject = new GameObject("StageReportOverlay", typeof(RectTransform), typeof(Image));
            RectTransform overlayRect = overlayObject.transform as RectTransform;
            overlayRect.SetParent(parent, false);
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            Image overlayImage = overlayObject.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.62f);
            overlayImage.raycastTarget = true;
            overlayRoot = overlayObject;

            GameObject panelObject = new GameObject("StageReportPanel", typeof(RectTransform));
            RectTransform panelRect = panelObject.transform as RectTransform;
            panelRect.SetParent(overlayRect, false);
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            panelRoot = panelObject;

            Image background = panelObject.AddComponent<Image>();
            background.raycastTarget = true;
            panelStyle = panelObject.AddComponent<PixelPanel>();
            panelStyle.background = background;
            panelStyle.variant = GameUiPanelVariant.Standard;
            canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panelObject);

            titleText = CreateText("Title", panelRect, "阶段成长报告", 34, FontStyle.Bold, TextAnchor.MiddleCenter);
            SetRect(titleText.rectTransform, new Vector2(0f, -32f), new Vector2(PanelWidth, 48f), new Vector2(0.5f, 1f));

            stageText = CreateText("Stage", panelRect, string.Empty, 22, FontStyle.Normal, TextAnchor.MiddleCenter);
            SetRect(stageText.rectTransform, new Vector2(0f, -84f), new Vector2(PanelWidth, 32f), new Vector2(0.5f, 1f));

            statsText = CreateText("Stats", panelRect, string.Empty, 24, FontStyle.Normal, TextAnchor.UpperLeft);
            statsText.lineSpacing = 1.45f;
            SetRect(statsText.rectTransform, new Vector2(70f, -136f), new Vector2(PanelWidth - 140f, 280f), new Vector2(0f, 1f));

            highlightsText = CreateText("Highlights", panelRect, string.Empty, 23, FontStyle.Bold, TextAnchor.UpperLeft);
            highlightsText.lineSpacing = 1.35f;
            SetRect(highlightsText.rectTransform, new Vector2(70f, -436f), new Vector2(PanelWidth - 140f, 130f), new Vector2(0f, 1f));

            closeButton = CreateButton("CloseButton", panelRect, "继续", new Vector2(0f, 40f), new Vector2(220f, 56f));
            RectTransform closeRect = closeButton.transform as RectTransform;
            closeRect.anchorMin = new Vector2(0.5f, 0f);
            closeRect.anchorMax = new Vector2(0.5f, 0f);
            closeRect.pivot = new Vector2(0.5f, 0f);
            closeRect.anchoredPosition = new Vector2(0f, 36f);
            closeRect.sizeDelta = new Vector2(220f, 56f);
            closeButton.onClick.AddListener(Close);

            ApplyStyle();
            SetVisible(false);
        }

        public void Show(StageData stage, GameSessionState session, Action closed = null)
        {
            ResolveReferences();
            onClosed = closed;

            if (stageText != null)
            {
                stageText.text = stage == null || string.IsNullOrEmpty(stage.displayName)
                    ? "本阶段已结束"
                    : stage.displayName;
            }

            PlayerState player = session == null ? null : session.playerState;
            if (statsText != null)
            {
                statsText.text = BuildStatsText(player);
            }

            if (highlightsText != null)
            {
                highlightsText.text = BuildHighlightsText(player);
            }

            SetVisible(true);
            Sfx.Ui.Open();
        }

        public void Close()
        {
            SetVisible(false);
            Sfx.Ui.Close();
            Action callback = onClosed;
            onClosed = null;
            callback?.Invoke();
        }

        private static string BuildStatsText(PlayerState player)
        {
            if (player == null)
            {
                return "属性数据不可用。";
            }

            // 两列排版：左列学业与精力，右列状态与关系。
            string left =
                Row("语文", player.GetStat(PlayerStatKey.Chinese)) +
                Row("数学", player.GetStat(PlayerStatKey.Math)) +
                Row("英语", player.GetStat(PlayerStatKey.English)) +
                Row("精力", player.GetStat(PlayerStatKey.Energy)) +
                Row("精力上限", player.GetStat(PlayerStatKey.MaxEnergy));

            string right =
                Row("压力", player.GetStat(PlayerStatKey.Pressure)) +
                Row("心情", player.GetStat(PlayerStatKey.Mood)) +
                Row("亲子", player.GetStat(PlayerStatKey.ParentRelation)) +
                Row("社交", player.GetStat(PlayerStatKey.Social)) +
                Row("自信", player.GetStat(PlayerStatKey.Confidence));

            return TwoColumns(left, right);
        }

        private static string BuildHighlightsText(PlayerState player)
        {
            if (player == null)
            {
                return string.Empty;
            }

            List<string> lines = new List<string>();

            int chinese = player.GetStat(PlayerStatKey.Chinese);
            int math = player.GetStat(PlayerStatKey.Math);
            int english = player.GetStat(PlayerStatKey.English);
            int average = Mathf.RoundToInt((chinese + math + english) / 3f);
            if (average >= 60)
            {
                lines.Add("学业扎实：三科均值 " + average);
            }
            else if (average >= 40)
            {
                lines.Add("学业稳步推进：三科均值 " + average);
            }
            else
            {
                lines.Add("学业吃紧：三科均值 " + average);
            }

            int pressure = player.GetStat(PlayerStatKey.Pressure);
            if (pressure >= 70)
            {
                lines.Add("压力偏高，记得安排休息");
            }
            else if (pressure <= 30)
            {
                lines.Add("状态放松，压力控制得很好");
            }

            int mood = player.GetStat(PlayerStatKey.Mood);
            if (mood >= 70)
            {
                lines.Add("心情不错，继续保持");
            }
            else if (mood <= 30)
            {
                lines.Add("心情低落，需要调剂一下");
            }

            if (player.GetStat(PlayerStatKey.Energy) <= 20)
            {
                lines.Add("精力见底，别硬撑");
            }

            if (lines.Count == 0)
            {
                lines.Add("各项状态平稳，没有明显短板");
            }

            return string.Join("\n", lines.ToArray());
        }

        private static string Row(string label, int value)
        {
            return "<color=#8C7A63>" + label + "</color>   <color=#3B2A1A><b>" + value + "</b></color>";
        }

        private static string TwoColumns(string left, string right)
        {
            string[] leftLines = left.Split('\n');
            string[] rightLines = right.Split('\n');
            int count = Mathf.Max(leftLines.Length, rightLines.Length);

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                string l = i < leftLines.Length ? leftLines[i] : string.Empty;
                string r = i < rightLines.Length ? rightLines[i] : string.Empty;
                builder.Append(l).Append("        ").Append(r);
                if (i < count - 1)
                {
                    builder.Append('\n');
                }
            }

            return builder.ToString();
        }

        public void ApplyStyle()
        {
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            if (panelStyle != null)
            {
                panelStyle.theme = resolved;
                panelStyle.ApplyStyle();
            }

            GameUiStyle.ApplyPixelText(titleText, resolved, GameUiTextStyle.Title);
            GameUiStyle.ApplyPixelText(stageText, resolved, GameUiTextStyle.Subtitle);
            GameUiStyle.ApplyPixelText(statsText, resolved, GameUiTextStyle.Body);
            GameUiStyle.ApplyPixelText(highlightsText, resolved, GameUiTextStyle.Body);

            if (closeButton != null)
            {
                GameUiStyle.ApplyButton(closeButton, closeButton.targetGraphic as Image, resolved);
                GameUiStyle.ApplyPixelText(closeButton.GetComponentInChildren<Text>(true), resolved, GameUiTextStyle.ButtonText, true);
            }
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }

            if (overlayRoot != null)
            {
                overlayRoot.SetActive(visible);
            }
            else if (panelRoot != null)
            {
                panelRoot.SetActive(visible);
            }
        }

        private void ResolveReferences()
        {
            if (panelRoot == null)
            {
                panelRoot = gameObject;
            }

            if (panelStyle == null && panelRoot != null)
            {
                panelStyle = panelRoot.GetComponent<PixelPanel>();
            }

            if (canvasGroup == null && panelRoot != null)
            {
                canvasGroup = UiAnimationHelper.EnsureCanvasGroup(panelRoot);
            }
        }

        private static Text CreateText(string name, RectTransform parent, string value, int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            GameObject textObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = textObject.transform as RectTransform;
            rect.SetParent(parent, false);

            Text text = textObject.AddComponent<Text>();
            text.text = value;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = true;
            GameUiStyle.ApplyTextFromFontSpec(text, null, fontSize, fontStyle);
            return text;
        }

        private Button CreateButton(string name, RectTransform parent, string label, Vector2 position, Vector2 size)
        {
            GameObject buttonObject = new GameObject(name, typeof(RectTransform));
            RectTransform rect = buttonObject.transform as RectTransform;
            rect.SetParent(parent, false);
            SetRect(rect, position, size, new Vector2(0.5f, 0f));

            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();
            button.targetGraphic = image;

            Text text = CreateText("Text", rect, label, 24, FontStyle.Bold, TextAnchor.MiddleCenter);
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = Vector2.zero;
            text.rectTransform.offsetMax = Vector2.zero;

            return button;
        }

        private static void SetRect(RectTransform rect, Vector2 anchoredPosition, Vector2 sizeDelta, Vector2 anchor)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y > 0.5f ? 1f : 0f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;
        }
    }
}
