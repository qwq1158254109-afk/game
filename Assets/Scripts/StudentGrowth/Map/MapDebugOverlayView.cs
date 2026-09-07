using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public sealed class MapDebugOverlayView : MonoBehaviour
    {
        public bool visible;
        public List<GameObject> debugObjects = new List<GameObject>();

        private void Awake()
        {
            ApplyVisibility();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            ApplyVisibility();
        }

        public void Toggle()
        {
            SetVisible(!visible);
        }

        private void ApplyVisibility()
        {
            if (debugObjects == null)
            {
                return;
            }

            for (int i = 0; i < debugObjects.Count; i++)
            {
                if (debugObjects[i] != null)
                {
                    debugObjects[i].SetActive(visible);
                }
            }
        }
    }
}
