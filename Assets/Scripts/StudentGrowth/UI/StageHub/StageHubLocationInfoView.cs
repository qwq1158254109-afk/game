using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class StageHubLocationInfoView : MonoBehaviour
    {
        public Text locationNameText;
        public Image previewImage;
        public Text descriptionText;
        [FormerlySerializedAs("stateText")]
        public Text availabilityReasonText;
        public Text resultMessageText;
        public Button confirmButton;
        public Text statusBadgeText;
        public Text confirmButtonText;

        public void Refresh(
            MapLocationPortal portal,
            StageMapLocationEntry entry,
            StageMapLocationStatus status,
            Sprite previewSprite,
            string reason,
            bool isCurrentLocation,
            bool allowRepeatEntry)
        {
            string displayName = entry != null && !string.IsNullOrEmpty(entry.displayName)
                ? entry.displayName
                : portal != null ? portal.displayName : "未选择地点";
            string description = entry != null && !string.IsNullOrEmpty(entry.description)
                ? entry.description
                : "该地点暂无额外说明。";
            bool canEnter = CanEnter(status);
            bool showCurrentLocation = canEnter && isCurrentLocation;
            bool canConfirm = canEnter && (!showCurrentLocation || allowRepeatEntry);

            SetText(locationNameText, displayName);
            SetText(descriptionText, description);
            SetAvailabilityReason(canEnter ? string.Empty : CompactReason(reason));
            SetText(statusBadgeText, BuildBadgeText(status, showCurrentLocation));
            SetText(confirmButtonText, BuildConfirmButtonText(canEnter, showCurrentLocation, allowRepeatEntry));

            if (statusBadgeText != null)
            {
                statusBadgeText.color = ResolveBadgeColor(status, showCurrentLocation);
            }

            if (previewImage != null)
            {
                previewImage.sprite = previewSprite;
                previewImage.enabled = previewSprite != null;
                previewImage.preserveAspect = true;
                previewImage.color = status == StageMapLocationStatus.Unlocked || status == StageMapLocationStatus.Completed
                    ? Color.white
                    : new Color(0.68f, 0.68f, 0.66f, 0.72f);
            }

            if (confirmButton != null)
            {
                confirmButton.interactable = canConfirm;
            }

        }

        public void SetResultMessage(string message)
        {
            SetText(resultMessageText, message);
        }

        private void SetAvailabilityReason(string reason)
        {
            if (availabilityReasonText == null)
            {
                return;
            }

            bool visible = !string.IsNullOrEmpty(reason);
            availabilityReasonText.text = reason;
            availabilityReasonText.gameObject.SetActive(visible);
        }

        private static string BuildBadgeText(StageMapLocationStatus status, bool isCurrentLocation)
        {
            if (isCurrentLocation)
            {
                return "当前所在地点";
            }

            switch (status)
            {
                case StageMapLocationStatus.Unlocked:
                    return "已开放";
                case StageMapLocationStatus.Completed:
                    return "已完成";
                case StageMapLocationStatus.Locked:
                    return "已锁定";
                case StageMapLocationStatus.Disabled:
                    return "不可进入";
                default:
                    return "尚未开放";
            }
        }

        private static string BuildConfirmButtonText(bool canEnter, bool isCurrentLocation, bool allowRepeatEntry)
        {
            if (!canEnter)
            {
                return "尚未开放";
            }

            if (!isCurrentLocation)
            {
                return "确认进入";
            }

            return allowRepeatEntry ? "进入" : "已在此处";
        }

        private static Color ResolveBadgeColor(StageMapLocationStatus status, bool isCurrentLocation)
        {
            if (isCurrentLocation)
            {
                return new Color(0.35f, 0.72f, 1f, 1f);
            }

            if (status == StageMapLocationStatus.Completed)
            {
                return new Color(1f, 0.78f, 0.24f, 1f);
            }

            return CanEnter(status)
                ? new Color(0.35f, 0.95f, 0.55f, 1f)
                : new Color(0.66f, 0.68f, 0.7f, 1f);
        }

        private static bool CanEnter(StageMapLocationStatus status)
        {
            return status == StageMapLocationStatus.Unlocked
                || status == StageMapLocationStatus.Completed;
        }

        private static string CompactReason(string reason)
        {
            return string.IsNullOrEmpty(reason) ? "当前不可进入。" : reason;
        }

        private static void SetText(Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }
    }
}
