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

        /// <summary>
        /// 字号覆盖：0（默认）跟随主题；大于 0 时用这个字号。
        /// 开场对话这类需要比常规 UI 大一档的地方用它，不牵动全局字号。
        /// </summary>
        [SerializeField] private int fontSizeOverride;

        public int FontSizeOverride
        {
            get { return fontSizeOverride; }
            set
            {
                fontSizeOverride = value;
                ApplyStyle();
            }
        }

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
            if (fontSizeOverride > 0 && target != null)
            {
                target.fontSize = fontSizeOverride;
            }
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
