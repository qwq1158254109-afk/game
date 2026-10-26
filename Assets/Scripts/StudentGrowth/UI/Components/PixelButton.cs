using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudentGrowth
{
    [RequireComponent(typeof(Button))]
    [RequireComponent(typeof(Image))]
    public sealed class PixelButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler, ISelectHandler, IDeselectHandler
    {
        public UIThemeConfig theme;
        public Image background;
        public Text labelText;
        public bool selected;

        /// <summary>
        /// true = 高亮只跟鼠标悬停（移开即恢复），忽略 selected；
        /// false = 保留 selected 语义（地图菜单、阶段节点这类需要持续高亮的场景）。
        /// 商店的商品横条用 true：需求是「只在悬停时变化」。
        /// </summary>
        public bool highlightOnHoverOnly;

        public bool animateScale = true;
        public bool applyOnAwake = true;

        private Button button;
        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private Coroutine scaleRoutine;
        private bool pointerInside;
        private bool pointerDown;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
            baseScale = rectTransform == null ? Vector3.one : rectTransform.localScale;
            if (applyOnAwake)
            {
                ApplyStyle();
            }
        }

        private void OnEnable()
        {
            if (applyOnAwake)
            {
                ApplyStyle();
            }
        }

        public void ApplyStyle()
        {
            ResolveReferences();
            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            GameUiStyle.ApplyButton(button, background, resolved);
            GameUiStyle.ApplyPixelText(labelText, resolved, GameUiTextStyle.ButtonText, true);
            RefreshSelectionVisual();
        }

        public void SetSelected(bool value)
        {
            selected = value;
            RefreshSelectionVisual();
        }

        public void RefreshSelectionVisual()
        {
            ResolveReferences();

            // 2026-10-26：highlightOnHoverOnly 的按钮只看悬停，不看 selected。
            // 原因：鼠标点击会让 EventSystem 记住 currentSelectedGameObject，
            // 解除要等点别处，于是点过的按钮一直亮着、移开也不恢复。
            // 默认 false 保留原语义，不影响战斗 UI / 地图菜单 / 阶段节点。
            bool highlight = highlightOnHoverOnly ? pointerInside : selected;
            GameUiStyle.SetSelectedButtonVisual(button, background, theme, highlight);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            AnimateToHoverState();
            RefreshSelectionVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerInside = false;
            pointerDown = false;
            AnimateToHoverState();
            RefreshSelectionVisual();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            pointerDown = true;
            AnimateToHoverState();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            pointerDown = false;
            AnimateToHoverState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            pointerInside = true;
            AnimateToHoverState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            pointerInside = false;
            pointerDown = false;
            AnimateToHoverState();
            RefreshSelectionVisual();
        }

        private void AnimateToHoverState()
        {
            if (!animateScale || rectTransform == null)
            {
                return;
            }

            UIThemeConfig resolved = GameUiStyle.ResolveTheme(theme);
            float scale = 1f;
            if (pointerDown)
            {
                scale = resolved.buttonPressedScale;
            }
            else if (pointerInside)
            {
                scale = resolved.buttonHoverScale;
            }

            if (scaleRoutine != null)
            {
                StopCoroutine(scaleRoutine);
            }

            scaleRoutine = UiAnimationHelper.ScaleTo(this, rectTransform, baseScale * scale, 0.08f);
        }

        private void ResolveReferences()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (labelText == null)
            {
                labelText = GetComponentInChildren<Text>(true);
            }
        }
    }
}
