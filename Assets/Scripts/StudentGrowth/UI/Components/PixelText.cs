using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    [RequireComponent(typeof(Text))]
    public sealed class PixelText : MonoBehaviour
    {
        public UIThemeConfig theme;
        public Text target;
        public GameUiTextStyle textStyle = GameUiTextStyle.Body;
        public bool inverse;
        public bool applyOnAwake = true;

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

        public void Configure(UIThemeConfig config, GameUiTextStyle style, bool useInverse, bool applyNow = true)
        {
            theme = config;
            textStyle = style;
            inverse = useInverse;
            ResolveReferences();

            if (applyNow)
            {
                ApplyStyle();
            }
        }

        public void ApplyStyle()
        {
            ResolveReferences();
            GameUiStyle.ApplyText(target, theme, textStyle, inverse);
        }

        private void ResolveReferences()
        {
            if (target == null)
            {
                target = GetComponent<Text>();
            }
        }
    }
}
