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

        /// <summary>
        /// true = 外观只跟鼠标悬停走：点击、键盘选中都不换底图、不做按下缩放。
        /// 商店商品横条用 true（2026-10-27 用户要求「物品栏只要悬停变化，不要点击变化」）。
        /// 与 highlightOnHoverOnly 的区别：后者只管「是否看 selected」，仍会因点击进入
        /// selected 而留在高亮态；本开关连 selected / pointerDown 一起屏蔽。
        /// </summary>
        public bool hoverOnlyVisual;

        /// <summary>hoverOnlyVisual 模式下的常态底图颜色（买不起时是压暗色），由外部写入。</summary>
        public Color restingColor = Color.white;

        /// <summary>
        /// hoverOnlyVisual 模式下的常态 / 悬停底图。两者都给了就换图做「变亮」，
        /// 不给则退回「把 restingColor 往暖白偏」的颜色方案。
        /// 换图比调颜色明显得多——主题里 button_hover 本身就是暖金，一眼能看出悬停。
        /// </summary>
        public Sprite restingSprite;
        public Sprite hoverSprite;

        /// <summary>hoverOnlyVisual 模式下悬停时的暖色偏移强度（0 = 不变）。</summary>
        [Range(0f, 1f)] public float hoverWarmth = 0.5f;

        /// <summary>
        /// true = 用纯色块做底，忽略主题的 buttonSprite。
        /// 商店商品横条这种 570x100 的大条目用九宫格贴图时，四个角保持原尺寸、
        /// 四条边被拉长，看起来就是一圈多余的细框。送礼格子也因为这个原因
        /// 改成了纯色（见 GiftPickerView.ApplySlotStyle 的注释）。
        /// </summary>
        public bool solidBackground;

        public Color solidNormal = new Color(0.33f, 0.28f, 0.23f, 1f);
        public Color solidHover = new Color(0.45f, 0.37f, 0.29f, 1f);
        public Color solidDisabled = new Color(0.25f, 0.22f, 0.18f, 1f);

        public bool animateScale = true;
        public bool applyOnAwake = true;

        private Button button;
        private RectTransform rectTransform;
        private Vector3 baseScale = Vector3.one;
        private Coroutine scaleRoutine;
        private bool pointerInside;
        private bool pointerDown;
        // 真实鼠标是否在按钮上。pointerInside 会被 OnSelect 也置 true（键盘选中），
        // hoverOnlyVisual 要区分这两者，所以单独记一份。
        private bool pointerOver;

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

        public void SetRestingColor(Color color)
        {
            restingColor = color;
            // 只有 hoverOnlyVisual 才用得上；其他按钮保持原语义，
            // 别让这里意外把它们的底图刷一遍。
            if (hoverOnlyVisual)
            {
                RefreshSelectionVisual();
            }
        }

        public void RefreshSelectionVisual()
        {
            ResolveReferences();

            // hoverOnlyVisual：外观只跟真实鼠标走，点击 / 键盘选中一律不参与。
            // 给了 restingSprite + hoverSprite 就换图（主题里 hover 是暖金，最直观的「变亮」）；
            // 没给就退回颜色提亮。
            if (hoverOnlyVisual)
            {
                if (restingSprite != null && hoverSprite != null)
                {
                    Sprite target = pointerOver ? hoverSprite : restingSprite;
                    if (background.sprite != target)
                    {
                        background.sprite = target;
                        background.type = Image.Type.Sliced;
                    }

                    background.color = restingColor;
                    return;
                }

                background.color = pointerOver ? WarmHighlight(restingColor) : restingColor;
                return;
            }

            // 纯色模式：不走 SetSelectedButtonVisual，直接刷 color。
            // 这样不会被 buttonSprite 的九宫格边干扰，也不受主题配色牵制。
            if (solidBackground)
            {
                background.sprite = GameUiStyle.GetSolidSprite();
                background.type = Image.Type.Simple;
                background.color = button != null && !button.interactable
                    ? solidDisabled
                    : pointerInside ? solidHover : solidNormal;
                return;
            }

            // 2026-10-26：highlightOnHoverOnly 的按钮只看悬停，不看 selected。
            // 原因：鼠标点击会让 EventSystem 记住 currentSelectedGameObject，
            // 解除要等点别处，于是点过的按钮一直亮着、移开也不恢复。
            // 默认 false 保留原语义，不影响战斗 UI / 地图菜单 / 阶段节点。
            bool highlight = highlightOnHoverOnly ? pointerInside : selected;
            GameUiStyle.SetSelectedButtonVisual(button, background, theme, highlight);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            pointerOver = true;
            pointerInside = true;
            AnimateToHoverState();
            RefreshSelectionVisual();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            pointerOver = false;
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
            if (hoverOnlyVisual)
            {
                // 点击会让 EventSystem 记住选中态，这里不接，避免点过的横条一直亮着。
                return;
            }

            pointerInside = true;
            AnimateToHoverState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            pointerDown = false;
            if (hoverOnlyVisual)
            {
                // 鼠标可能还在上面，别把悬停态一起清掉。
                AnimateToHoverState();
                RefreshSelectionVisual();
                return;
            }

            pointerInside = false;
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
            if (hoverOnlyVisual)
            {
                // 只要悬停缩放，按下不再额外变化。
                scale = pointerOver ? resolved.buttonHoverScale : 1f;
            }
            else if (pointerDown)
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

        /// <summary>把常态色往暖白偏一点。白底乘系数不会变，所以用插值而不是乘法。</summary>
        private Color WarmHighlight(Color color)
        {
            if (hoverWarmth <= 0f)
            {
                return color;
            }

            Color warm = new Color(1f, 0.94f, 0.82f, color.a);
            return Color.Lerp(color, warm, hoverWarmth);
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
