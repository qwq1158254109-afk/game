using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudentGrowth
{
    [DisallowMultipleComponent]
    public sealed class StageHubLocationNodeView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public string locationId;
        public string fallbackDisplayName;
        public Image nodeSprite;
        public Image selectedOutline;
        public Image locationPin;
        public Image selectionSparkles;
        public Image lockMarker;
        public Text lockText;
        public Image nameLabelBackground;
        public Text locationNameText;
        public Button hitArea;

        public MapLocationPortal Portal { get; private set; }
        public StageMapLocationEntry Entry { get; private set; }
        public StageMapLocationStatus CurrentStatus { get; private set; }
        public bool CanConfirm => CurrentStatus == StageMapLocationStatus.Unlocked
            || CurrentStatus == StageMapLocationStatus.Completed;

        private StageHubSelectionController controller;
        private RectTransform rootRect;
        private Vector2 basePosition;
        private bool isSelected;
        private bool isHovered;

        private void Awake()
        {
            rootRect = transform as RectTransform;
            if (rootRect != null)
            {
                basePosition = rootRect.anchoredPosition;
            }
        }

        public void Bind(
            StageHubSelectionController owner,
            MapLocationPortal portal,
            StageMapLocationEntry entry,
            StageMapLocationStatus status)
        {
            controller = owner;
            Portal = portal;
            Entry = entry;
            CurrentStatus = status;

            if (portal != null)
            {
                locationId = portal.locationId;
            }

            string displayName = entry != null && !string.IsNullOrEmpty(entry.displayName)
                ? entry.displayName
                : portal != null && !string.IsNullOrEmpty(portal.displayName)
                    ? portal.displayName
                    : fallbackDisplayName;
            if (locationNameText != null)
            {
                locationNameText.text = string.IsNullOrEmpty(displayName) ? "未命名地点" : displayName;
            }

            if (hitArea != null)
            {
                hitArea.onClick.RemoveListener(HandleClick);
                hitArea.onClick.AddListener(HandleClick);
            }

            ApplyVisualState();
        }

        public void SetStatus(StageMapLocationEntry entry, StageMapLocationStatus status)
        {
            Entry = entry;
            CurrentStatus = status;
            ApplyVisualState();
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            ApplyVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            isHovered = true;
            controller?.SetHoveredNode(this);
            ApplyVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            controller?.SetHoveredNode(null);
            ApplyVisualState();
        }

        private void HandleClick()
        {
            controller?.HandleNodeClick(this);
        }

        private void ApplyVisualState()
        {
            bool unavailable = CurrentStatus == StageMapLocationStatus.Hidden
                || CurrentStatus == StageMapLocationStatus.ComingSoon
                || CurrentStatus == StageMapLocationStatus.Disabled;
            bool locked = CurrentStatus == StageMapLocationStatus.Locked;
            bool emphasized = isSelected || isHovered;

            if (nodeSprite != null)
            {
                nodeSprite.color = unavailable
                    ? new Color(0.62f, 0.65f, 0.68f, 0.58f)
                    : locked
                        ? new Color(0.58f, 0.56f, 0.52f, 0.76f)
                        : emphasized
                            ? Color.white
                            : new Color(0.9f, 0.9f, 0.88f, 1f);
            }

            SetActive(selectedOutline, isSelected);
            SetActive(locationPin, isSelected);
            SetActive(selectionSparkles, isSelected);
            SetActive(lockMarker, !CanConfirm);

            if (lockText != null)
            {
                lockText.text = locked ? "锁" : unavailable ? "未开放" : string.Empty;
            }

            if (locationNameText != null)
            {
                locationNameText.color = CanConfirm
                    ? new Color(1f, 0.94f, 0.76f, 1f)
                    : new Color(0.72f, 0.72f, 0.7f, 1f);
            }

            if (rootRect != null)
            {
                rootRect.anchoredPosition = basePosition + (emphasized ? new Vector2(0f, 3f) : Vector2.zero);
            }
        }

        private static void SetActive(Graphic graphic, bool active)
        {
            if (graphic != null)
            {
                graphic.gameObject.SetActive(active);
            }
        }
    }
}
