using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
namespace StudentGrowth
{
    public sealed class MenuFocusIndicator : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        private Outline outline;
        private Text indicator;
        private void Awake()
        {
            outline=gameObject.AddComponent<Outline>(); outline.effectColor=new Color(1,.82f,.4f); outline.effectDistance=new Vector2(3,3); outline.enabled=false;
            indicator=MenuUi.Label((RectTransform)transform,GetComponentInChildren<Text>().font,14,0,38,76,28); indicator.text="›"; indicator.gameObject.SetActive(false);
        }
        public void OnSelect(BaseEventData e) { if(outline!=null) outline.enabled=true; if(indicator!=null) indicator.gameObject.SetActive(true); }
        public void OnDeselect(BaseEventData e) { if(outline!=null) outline.enabled=false; if(indicator!=null) indicator.gameObject.SetActive(false); }
    }
}
