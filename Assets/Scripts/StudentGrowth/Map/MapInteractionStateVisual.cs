using UnityEngine;

namespace StudentGrowth
{
    public sealed class MapInteractionStateVisual : MonoBehaviour
    {
        public MapInteractionTarget target;
        public GameObject nearbyIndicator;
        public GameObject debugRange;
        public SpriteRenderer[] tintRenderers = new SpriteRenderer[0];

        public Color idleColor = Color.white;
        public Color nearbyColor = new Color(1f, 0.92f, 0.35f, 1f);
        public Color completedColor = new Color(0.65f, 0.9f, 0.7f, 1f);
        public Color lockedColor = new Color(0.55f, 0.55f, 0.55f, 1f);

        public void Apply(bool isNearby, bool isDebugVisible, bool isCompleted, bool isLocked)
        {
            if (nearbyIndicator != null)
            {
                nearbyIndicator.SetActive(isNearby && !isDebugVisible && !isCompleted && !isLocked);
            }

            if (debugRange != null)
            {
                debugRange.SetActive(isDebugVisible);
            }

            Color color = idleColor;
            if (isLocked)
            {
                color = lockedColor;
            }
            else if (isCompleted)
            {
                color = completedColor;
            }
            else if (isNearby)
            {
                color = nearbyColor;
            }

            if (tintRenderers == null)
            {
                return;
            }

            for (int i = 0; i < tintRenderers.Length; i++)
            {
                if (tintRenderers[i] != null)
                {
                    tintRenderers[i].color = color;
                }
            }
        }
    }
}
