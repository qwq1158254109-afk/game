using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 房间之间的门：玩家靠近后**按 E** 切到目标房间场景，并在目标场景的指定出生点落地。
    /// 与「地点」无关，不消耗时间/行动点；会话由 RoomDoorRouter 负责带过去。
    ///
    /// 门面（DoorVisual + MapDoorAnimator）由 HomeRoomsBuilder 生成，本脚本不碰像素，
    /// 只在切场景前播一次开门动画：按 E → 门开（约 0.33s）→ 黑场 → 换场景。
    ///
    /// 2026-10-21 改造（用户要求「给门改成按 E 键交互」）：
    ///   原先靠 `OnTriggerEnter2D` 自动触发，玩家一落地就在触发区里时不触发，
    ///   必须先走出去再走回来；后来补了「朝门走就触发」的 Update 补路，虽然能用了，
    ///   但和床/书桌/家人那套「靠近 → 提示 → 按 E」的交互手感不一致。
    ///   现在改成标准交互目标：本组件**自己挂** MapInteractionPoint + MapInteractionTarget，
    ///   由 FullscreenMapSceneController 的 CollectInteractionTargets 自动收集，
    ///   提示栏显示「按 E：前往卧室」，按 E 走 MapInteractionFlowController → RoomDoorRouter。
    ///   不再有任何自动触发逻辑。
    /// </summary>
    [RequireComponent(typeof(BoxCollider2D))]
    [RequireComponent(typeof(MapInteractionPoint))]
    [RequireComponent(typeof(MapInteractionTarget))]
    public sealed class RoomDoor : MonoBehaviour
    {
        public string targetSceneName;
        public string targetSpawnPointId;
        /// <summary>提示栏文案里的目的地名称（如「卧室」）。留空则用场景名。</summary>
        public string destinationDisplayName;

        /// <summary>落地后的静默时间，防止刚进新房间就被门口触发区弹回去。</summary>
        [HideInInspector] public float armDelay = 0.35f;

        private bool opening;

        private void Reset()
        {
            ConfigureInteraction();
        }

        private void Awake()
        {
            ConfigureInteraction();
        }

        /// <summary>
        /// 把本组件接进统一交互系统：
        ///   MapInteractionPoint  → 类型标成 LocationAction（不用 ReturnToMainUi，那是回阶段地图）
        ///   MapInteractionTarget → 关掉耗时、关掉朝向要求、给足范围
        /// 重复调用是幂等的（场景里可能已经存了旧值）。
        /// </summary>
        public void ConfigureInteraction()
        {
            MapInteractionPoint point = GetComponent<MapInteractionPoint>();
            if (point != null)
            {
                point.interactionType = MapInteractionType.LocationAction;
                point.displayName = ResolveDisplayName();
                point.description = "按 E：" + ResolvePromptSuffix() + "｜不消耗时间";
                // 房间门不需要地点解锁判定（那是「地点」层面的东西）。
                point.requiresUnlockedLocation = false;
            }

            MapInteractionTarget target = GetComponent<MapInteractionTarget>();
            if (target != null)
            {
                target.interactionId = "room_door_" + name;
                target.displayName = ResolveDisplayName();
                target.promptText = "按 E：" + ResolvePromptSuffix() + "｜不消耗时间";
                target.interactionPoint = point;
                target.consumeTimeOnInteraction = false;
                // 门在墙上，玩家是走到门前按 E —— 不该再要求「面朝门」才给提示，
                // 否则从侧面走过来站在门口会看不到提示。
                target.requireFacing = false;
                target.interactionRange = 1.6f;
                // 高于家具（家具默认 0），保证站在门口时优先提示门。
                target.priority = 10;
            }
        }

        private string ResolveDisplayName()
        {
            if (!string.IsNullOrEmpty(destinationDisplayName))
            {
                return destinationDisplayName;
            }

            if (!string.IsNullOrEmpty(targetSceneName))
            {
                // HomeBedroomScene → 卧室（没有更准的信息时至少给个可读名字）
                return targetSceneName.Replace("Home", string.Empty).Replace("Scene", string.Empty);
            }

            return "下一个房间";
        }

        private string ResolvePromptSuffix()
        {
            return "前往" + ResolveDisplayName();
        }

        /// <summary>
        /// 由 MapInteractionFlowController 在玩家按 E 后调用（经 MapInteractionController 转发）。
        /// 这里只负责播开门动画 + 交给 RoomDoorRouter 切场景。
        /// </summary>
        public void Enter()
        {
            if (opening)
            {
                return;
            }

            if (string.IsNullOrEmpty(targetSceneName))
            {
                Debug.LogWarning("[RoomDoor] " + name + " 没有配置 targetSceneName。");
                return;
            }

            MapDoorAnimator animator = GetComponentInChildren<MapDoorAnimator>(true);
            if (animator != null && animator.HasOpenAnimation && !animator.IsPlaying)
            {
                opening = true;
                Sfx.World.DoorOpen();
                // TryPlayOpenThen 返回 true = 动画接管流程，播完开门帧再切场景
                if (animator.TryPlayOpenThen(() => RoomDoorRouter.Enter(this)))
                {
                    return;
                }

                opening = false;
            }

            RoomDoorRouter.Enter(this);
        }
    }
}
