using UnityEngine;

namespace StudentGrowth
{
    public sealed class MapInteractionTarget : MonoBehaviour
    {
        public string interactionId;
        public string displayName;
        public string promptText;
        public LocationActionData locationAction;
        public ActivityData fallbackActivity;
        public MapInteractionPoint interactionPoint;
        public bool isExitPortal;
        public bool consumeTimeOnInteraction = true;
        public bool requireFacing = true;
        public float interactionRange = 0.75f;
        public int priority;

        public bool CanSelect(Vector2 playerPosition, Vector2 facingDirection)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }

            if (interactionRange < 0f)
            {
                return false;
            }

            Vector2 targetPosition = transform.position;
            if ((targetPosition - playerPosition).sqrMagnitude > interactionRange * interactionRange)
            {
                return false;
            }

            return !requireFacing
                || MapFacingInteractionUtility.IsFacingTarget(playerPosition, facingDirection, targetPosition);
        }
    }
}
