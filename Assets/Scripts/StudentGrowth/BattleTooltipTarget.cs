using UnityEngine;
using UnityEngine.EventSystems;

namespace StudentGrowth
{
    public class BattleTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private BattleTooltipView tooltipView;
        private RectTransform source;
        private string title;
        private string body;

        public void Initialize(BattleTooltipView tooltip, string tooltipTitle, string tooltipBody)
        {
            tooltipView = tooltip;
            title = tooltipTitle;
            body = tooltipBody;
            source = transform as RectTransform;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (tooltipView != null)
            {
                tooltipView.Show(title, body, source);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (tooltipView != null)
            {
                tooltipView.Hide();
            }
        }
    }
}
