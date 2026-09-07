using UnityEngine;

namespace StudentGrowth
{
    public static class MapFacingInteractionUtility
    {
        public const float DefaultFacingMinDot = 0.55f;

        public static Vector2 ToCardinal(Vector2 direction, Vector2 fallback)
        {
            if (direction.sqrMagnitude <= 0.0001f)
            {
                return fallback.sqrMagnitude <= 0.0001f ? Vector2.down : fallback.normalized;
            }

            return Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? new Vector2(Mathf.Sign(direction.x), 0f)
                : new Vector2(0f, Mathf.Sign(direction.y));
        }

        public static bool IsInsideRangeAndFacing(
            Rect interactionRect,
            Vector2 playerPosition,
            Vector2 facingDirection,
            Vector2 targetPosition,
            float minDot = DefaultFacingMinDot)
        {
            return interactionRect.Contains(playerPosition)
                && IsFacingTarget(playerPosition, facingDirection, targetPosition, minDot);
        }

        public static bool IsFacingTarget(
            Vector2 playerPosition,
            Vector2 facingDirection,
            Vector2 targetPosition,
            float minDot = DefaultFacingMinDot)
        {
            Vector2 safeFacing = ToCardinal(facingDirection, Vector2.down);
            Vector2 toTarget = targetPosition - playerPosition;
            if (toTarget.sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            return Vector2.Dot(safeFacing.normalized, toTarget.normalized) >= minDot;
        }
    }
}
