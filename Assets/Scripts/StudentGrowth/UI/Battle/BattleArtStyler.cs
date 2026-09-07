using UnityEngine;
using UnityEngine.UI;

namespace StudentGrowth
{
    public static class BattleArtStyler
    {
        public static void Icon(Image image, Sprite sprite)
        {
            if (image == null || sprite == null) return;
            image.sprite = sprite; image.color = Color.white; image.preserveAspect = true; image.raycastTarget = false;
        }
        public static void Plate(RectTransform rect, Sprite sprite, bool sliced = true)
        {
            if (rect == null || sprite == null) return;
            var image = rect.GetComponent<Image>(); if (image == null) image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite; image.color = Color.white; image.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            image.preserveAspect = !sliced; image.pixelsPerUnitMultiplier = 3f;
            foreach (string name in new[] { "TopBorder", "BottomBorder", "LeftBorder", "RightBorder" })
            { var child = rect.Find(name); if (child != null) child.gameObject.SetActive(false); }
        }
        public static void Button(Button button, BattleUIConfig config, string prefix = "btn_primary")
        {
            if (button == null || config == null || config.GetArtwork(prefix+"_normal") == null) return;
            var pixel = button.GetComponent<PixelButton>(); if (pixel != null) { pixel.animateScale = false; pixel.enabled = false; }
            Plate(button.transform as RectTransform, config.GetArtwork(prefix+"_normal"));
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState { highlightedSprite = config.GetArtwork(prefix+"_hover"), selectedSprite = config.GetArtwork(prefix+"_hover"),
                pressedSprite = config.GetArtwork(prefix+"_pressed"), disabledSprite = config.GetArtwork(prefix+"_disabled") };
            foreach (var text in button.GetComponentsInChildren<Text>()) { text.color = new Color(1f,.93f,.74f); text.raycastTarget = false; }
        }
        public static void Progress(RectTransform rect, Image fill, BattleUIConfig config, string prefix, float value)
        {
            if (config == null || config.GetArtwork(prefix+"_track") == null || fill == null) return;
            Plate(rect, config.GetArtwork(prefix+"_track"));
            fill.sprite = config.GetArtwork(prefix+"_fill"); fill.color = Color.white; fill.raycastTarget = false;
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillOrigin = 0; fill.fillAmount = Mathf.Clamp01(value);
            var f=fill.rectTransform; f.anchorMin=Vector2.zero; f.anchorMax=Vector2.one; f.offsetMin=new Vector2(12,7); f.offsetMax=new Vector2(-12,-7);
        }
    }
}
