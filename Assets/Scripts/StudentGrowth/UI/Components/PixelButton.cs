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
            GameUiStyle.SetSelectedButtonVisual(button, background, theme, selected);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerInside = true;
            AnimateToHoverState();
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

            // 2026-10-26：鼠标点完立刻清掉 EventSystem 的选中态。
            // 否则按钮会一直停在 selected（点别处才解除），表现为
            // 「点过之后鼠标移开颜色不恢复」。
            if (EventSystem.current != null
                && EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
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
