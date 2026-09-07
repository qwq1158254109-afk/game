using UnityEngine;

namespace StudentGrowth
{
    [ExecuteAlways]
    public sealed class MapObjectYSorter : MonoBehaviour
    {
        public SpriteRenderer[] renderers = new SpriteRenderer[0];
        public int baseOrder = 1000;
        public float orderScale = 100f;
        public float sortYOffset;
        public bool useRendererCenterAsSortY;

        private void LateUpdate()
        {
            int order = CalculateSortingOrder(GetSortY(), baseOrder, orderScale);
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null)
                {
                    if (renderers[i].sortingOrder != order)
                    {
                        renderers[i].sortingOrder = order;
                    }
                }
            }
        }

        public static int CalculateSortingOrder(float y, int baseOrder, float orderScale)
        {
            return baseOrder - Mathf.RoundToInt(y * orderScale);
        }

        private float GetSortY()
        {
            if (!useRendererCenterAsSortY)
            {
                return transform.position.y + sortYOffset;
            }

            if (renderers == null || renderers.Length == 0)
            {
                return transform.position.y;
            }

            Bounds bounds = default;
            bool hasBounds = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = renderer.bounds;
                    hasBounds = true;
                    continue;
                }

                bounds.Encapsulate(renderer.bounds);
            }

            return hasBounds ? bounds.center.y : transform.position.y;
        }
    }
}
