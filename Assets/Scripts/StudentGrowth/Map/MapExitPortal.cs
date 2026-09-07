using UnityEngine;

namespace StudentGrowth
{
    [RequireComponent(typeof(MapInteractionTarget), typeof(MapInteractionPoint))]
    public sealed class MapExitPortal : MonoBehaviour
    {
        public string destinationId;
        public string promptText = "按 E：返回阶段地图";

        private void Reset()
        {
            ConfigureTarget();
        }

        private void Awake()
        {
            ConfigureTarget();
        }

        private void ConfigureTarget()
        {
            MapInteractionTarget target = GetComponent<MapInteractionTarget>();
            if (target == null)
            {
                return;
            }

            target.isExitPortal = true;
            target.consumeTimeOnInteraction = false;
            if (string.IsNullOrEmpty(target.promptText))
            {
                target.promptText = promptText;
            }

            MapInteractionPoint point = GetComponent<MapInteractionPoint>();
            if (point != null)
            {
                point.interactionType = MapInteractionType.ReturnToMainUi;
                point.displayName = string.IsNullOrEmpty(point.displayName) ? "返回阶段地图" : point.displayName;
                point.requiresUnlockedLocation = false;
                target.interactionPoint = point;
            }
        }
    }
}
