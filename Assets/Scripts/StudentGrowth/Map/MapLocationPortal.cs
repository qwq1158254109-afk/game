using UnityEngine;

namespace StudentGrowth
{
    [RequireComponent(typeof(MapInteractionTarget), typeof(MapInteractionPoint))]
    public sealed class MapLocationPortal : MonoBehaviour
    {
        public string locationId = "location_home";
        public string displayName = "家里";
        public string unlockedPrompt;
        public string lockedPrompt;
        public bool hideWhenHidden = true;

        public StageMapLocationStatus CurrentStatus { get; private set; } = StageMapLocationStatus.Unlocked;

        private void Reset()
        {
            ConfigurePoint();
        }

        private void Awake()
        {
            ConfigurePoint();
        }

        public void ApplyStatus(StageMapLocationStatus status, string reason)
        {
            CurrentStatus = status;
            if (status == StageMapLocationStatus.Hidden && hideWhenHidden)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            ConfigurePoint();
            MapInteractionTarget target = GetComponent<MapInteractionTarget>();
            if (target == null)
            {
                return;
            }

            bool canEnter = status == StageMapLocationStatus.Unlocked || status == StageMapLocationStatus.Completed;
            target.promptText = canEnter
                ? BuildUnlockedPrompt()
                : BuildLockedPrompt(status, reason);
        }

        private void ConfigurePoint()
        {
            MapInteractionPoint point = GetComponent<MapInteractionPoint>();
            if (point != null)
            {
                point.locationId = locationId;
                point.interactionType = MapInteractionType.EnterLocationScene;
                point.displayName = string.IsNullOrEmpty(point.displayName) ? displayName : point.displayName;
                point.description = string.IsNullOrEmpty(point.description) ? "进入地点：" + displayName : point.description;
                point.requiresUnlockedLocation = false;
            }

            MapInteractionTarget target = GetComponent<MapInteractionTarget>();
            if (target != null)
            {
                target.interactionId = string.IsNullOrEmpty(target.interactionId) ? locationId : target.interactionId;
                target.displayName = string.IsNullOrEmpty(target.displayName) ? displayName : target.displayName;
                target.consumeTimeOnInteraction = false;
                // 地点入口同理：同一阶段里可以反复进出，不该被「每阶段一次」卡住。
                target.unlimitedUses = true;
                target.interactionPoint = point;
            }
        }

        private string BuildUnlockedPrompt()
        {
            if (!string.IsNullOrEmpty(unlockedPrompt))
            {
                return unlockedPrompt;
            }

            string name = string.IsNullOrEmpty(displayName) ? "地点" : displayName;
            return "按 E：进入" + name + "｜不消耗时间";
        }

        private string BuildLockedPrompt(StageMapLocationStatus status, string reason)
        {
            if (!string.IsNullOrEmpty(lockedPrompt))
            {
                return lockedPrompt;
            }

            string name = string.IsNullOrEmpty(displayName) ? "地点" : displayName;
            string compactReason = string.IsNullOrEmpty(reason) ? "当前不可进入。" : reason;
            if (status == StageMapLocationStatus.ComingSoon)
            {
                return name + "｜" + compactReason;
            }

            return name + "｜" + compactReason;
        }
    }
}
