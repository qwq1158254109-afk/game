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
        /// <summary>
        /// 不限次数、不计完成：门 / 传送点这类「走过去」的交互。
        /// 置 true 后不写入 actionUseCount，也就不会被 maxUsesPerStage 卡住，
        /// 提示栏不会出现「本阶段已完成」。（2026-10-22：房间门原本每阶段只能用一次）
        /// </summary>
        public bool unlimitedUses;
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
