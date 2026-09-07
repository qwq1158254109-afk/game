using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public class BattleTooltipView : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Canvas canvas;
        [SerializeField] private Vector2 screenPadding = new Vector2(24f, 24f);
        [SerializeField] private float maxWidth = 340f;
        private bool parchment;

        public void Initialize(RectTransform tooltipRoot, Text title, Text body, Canvas rootCanvas, BattleUIConfig config)
        {
            root = tooltipRoot;
            titleText = title;
            bodyText = body;
            canvas = rootCanvas;
            parchment=config!=null&&config.GetArtwork("hud_tooltip_panel")!=null;
            if (config != null)
            {
                screenPadding = config.tooltipPadding;
                maxWidth = config.tooltipMaxWidth;
                if (root != null && config.GetArtwork("hud_tooltip_panel") != null)
                {
                    BattleArtStyler.Plate(root, config.GetArtwork("hud_tooltip_panel"));
                    if (titleText != null) titleText.color = new Color(.16f, .085f, .04f);
                    if (bodyText != null) bodyText.color = new Color(.16f, .085f, .04f);
                }
            }

            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        public void ShowForCard(BattleCardView card)
        {
            if (card == null)
            {
                Hide();
                return;
            }

            string body = string.IsNullOrEmpty(card.Description) ? "暂无描述" : card.Description;
            Show(card.CardName, body, card.RectTransform);
        }

        public void Show(string title, string body, RectTransform source)
        {
            if (root == null || titleText == null || bodyText == null || source == null)
            {
                return;
            }

            titleText.text = title;
            bodyText.text = body;
            root.sizeDelta = new Vector2(maxWidth, root.sizeDelta.y);
            root.gameObject.SetActive(true);
            if(parchment){titleText.color=new Color(.16f,.085f,.04f);bodyText.color=titleText.color;}
            root.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            PlaceNear(source);
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        private void PlaceNear(RectTransform source)
        {
            RectTransform parent = root.parent as RectTransform;
            if (parent == null)
            {
                return;
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector3[] corners = new Vector3[4];
            source.GetWorldCorners(corners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[0]);
            Vector2 max = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[2]);
            Vector2 size = root.rect.size;
            if (size.y <= 1f)
            {
                size.y = 150f;
            }

            Vector2 desired = new Vector2((min.x + max.x) * 0.5f, max.y + size.y * 0.5f + 18f);
            if (desired.y + size.y * 0.5f > Screen.height - screenPadding.y)
            {
                float leftX = min.x - size.x * 0.5f - 18f;
                float rightX = max.x + size.x * 0.5f + 18f;
                desired.x = rightX + size.x * 0.5f < Screen.width ? rightX : leftX;
                desired.y = (min.y + max.y) * 0.5f;
            }

            desired.x = Mathf.Clamp(desired.x, screenPadding.x + size.x * 0.5f, Screen.width - screenPadding.x - size.x * 0.5f);
            desired.y = Mathf.Clamp(desired.y, screenPadding.y + size.y * 0.5f, Screen.height - screenPadding.y - size.y * 0.5f);

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, desired, eventCamera, out localPoint))
            {
                root.anchoredPosition = localPoint;
            }
        }
    }

}
