using UnityEngine;
using UnityEngine.EventSystems;

namespace StudentGrowth
{
    public sealed class QuickbarTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string tooltipText;

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!string.IsNullOrEmpty(tooltipText))
            {
                InteractionPromptUI.Show(tooltipText, this);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            InteractionPromptUI.Hide(this);
        }
    }
}
