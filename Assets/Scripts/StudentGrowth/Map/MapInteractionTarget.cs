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
        /// <summary>
        /// 剧情对话。非空时优先于活动：按 E 直接开对话框，不消耗时间、不计次数。
        /// 剧情组只填 DialogueData 资产，不需要改场景逻辑。
        /// </summary>
        public DialogueData dialogue;
        /// <summary>
        /// 对话归属的 NPC 实体（2026-10-24）。影响：送礼的好感度记到 npcId 头上；
        /// 后续好感度门槛、NPC 菜单模板都以它为准。可为空（纯一次性剧情点）。
        /// </summary>
        public NpcData npc;
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
