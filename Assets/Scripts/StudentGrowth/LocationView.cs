using UnityEngine;

namespace StudentGrowth
{
    public class LocationView : MonoBehaviour
    {
        public LocationType locationType;
        public LocationData data;
        public RectTransform root;

        public virtual void Bind(LocationData locationData)
        {
            data = locationData;
            if (locationData != null)
            {
                locationType = locationData.locationType;
            }
        }

        public virtual void Show()
        {
            if (root != null)
            {
                root.gameObject.SetActive(true);
            }
        }

        public virtual void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }
    }
}
