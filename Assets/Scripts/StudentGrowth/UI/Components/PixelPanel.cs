using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [RequireComponent(typeof(Image))]
    public sealed class PixelPanel : MonoBehaviour
    {
        private const string BorderRootName = "PixelPanelBorder";

        public UIThemeConfig theme;
        public GameUiPanelVariant variant;
        public Image background;
        public Text titleText;
        public bool createBorder = true;
        public bool applyOnAwake = true;

        private RectTransform rectTransform;
        private RectTransform borderRoot;

        private void Reset()
        {
            ResolveReferences();
        }

        private void Awake()
        {
            ResolveReferences();
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
            GameUiStyle.ApplyPanel(background, resolved, variant);
            GameUiStyle.ApplyPixelText(titleText, resolved, GameUiTextStyle.Subtitle, variant == GameUiPanelVariant.Dark);

            if (createBorder)
            {
                EnsureBorder(resolved);
            }
        }

        public void SetTitle(string value)
        {
            ResolveReferences();
            if (titleText != null)
            {
                titleText.text = value;
            }
        }

        private void EnsureBorder(UIThemeConfig resolved)
        {
            if (rectTransform == null)
            {
                return;
            }

            if (borderRoot == null)
            {
                Transform existing = transform.Find(BorderRootName);
                if (existing != null)
                {
                    borderRoot = existing as RectTransform;
                }
            }

            if (borderRoot == null)
            {
                GameObject root = new GameObject(BorderRootName, typeof(RectTransform));
                borderRoot = root.transform as RectTransform;
                borderRoot.SetParent(rectTransform, false);
                Stretch(borderRoot);
            }

            borderRoot.SetAsLastSibling();
            int thickness = Mathf.Max(0, resolved.panelBorderThickness);
            SetBorderImage("Top", new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 1f), new Vector2(0f, thickness), resolved.panelBorder);
            SetBorderImage("Bottom", Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, thickness), resolved.panelBorder);
            SetBorderImage("Left", Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(thickness, 0f), resolved.panelBorder);
            SetBorderImage("Right", new Vector2(1f, 0f), Vector2.one, new Vector2(1f, 0f), new Vector2(thickness, 0f), resolved.panelBorder);
        }

        private void SetBorderImage(string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 sizeDelta, Color color)
        {
            RectTransform rect = FindOrCreateBorderChild(name);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = sizeDelta;

            Image image = rect.GetComponent<Image>();
            image.sprite = GameUiStyle.GetSolidSprite();
            image.color = color;
            image.raycastTarget = false;
        }

        private RectTransform FindOrCreateBorderChild(string name)
        {
            Transform existing = borderRoot.Find(name);
            if (existing != null)
            {
                return existing as RectTransform;
            }

            GameObject child = new GameObject(name, typeof(RectTransform));
            RectTransform rect = child.transform as RectTransform;
            rect.SetParent(borderRoot, false);
            child.AddComponent<Image>();
            return rect;
        }

        private void ResolveReferences()
        {
            if (rectTransform == null)
            {
                rectTransform = transform as RectTransform;
            }

            if (background == null)
            {
                background = GetComponent<Image>();
            }

            if (titleText == null)
            {
                titleText = GetComponentInChildren<Text>(true);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
        }
    }
}
