using UnityEngine;

namespace StudentGrowth
{
    [CreateAssetMenu(menuName = "Battle/UI Config")]
    public class BattleUIConfig : ScriptableObject
    {
        public const string ResourcePath = "StudentGrowth/Battle/BattleUIConfig";
        [System.Serializable] public class NamedSprite { public string key; public Sprite sprite; }
        [System.Serializable] public class CardArtBinding { public string cardId; public Sprite sprite; }
        [Header("Battle artwork (v03)")]
        public NamedSprite[] artwork = new NamedSprite[0];
        public CardArtBinding[] cardArtwork = new CardArtBinding[0];
        public Sprite GetArtwork(string key)
        {
            if (artwork != null) foreach (var entry in artwork) if (entry != null && entry.key == key) return entry.sprite;
            return null;
        }
        public Sprite GetCardArtwork(CardData card)
        {
            if (card != null && cardArtwork != null) foreach (var entry in cardArtwork)
                if (entry != null && entry.cardId == card.cardId) return entry.sprite;
            return card == null ? null : card.cardSprite;
        }
        [Header("Layout")]
        public float topBarHeight = 88f;
        public float handAreaHeight = 230f;
        public float logHeight = 72f;
        public Vector2 safePadding = new Vector2(24f, 24f);

        [Header("Cards")]
        public float cardBaseScale = 0.90f;
        public float cardHoverScale = 1.15f;
        public float cardHoverYOffset = 90f;
        public float cardSpacing = 130f;
        public float maxCardRotation = 10f;
        public float handArcHeight = 28f;
        public float hoverSpread = 36f;
        public float cardAnimSpeed = 14f;

        [Header("Tooltip")]
        public float tooltipMaxWidth = 340f;
        public Vector2 tooltipPadding = new Vector2(18f, 14f);

        [Header("Theme")]
        public UIThemeConfig theme;

        [Header("Visual")]
        public float weakBorderAlpha = 0.45f;
        public bool showHandAreaBorder;
        public float handAreaBackgroundAlpha = 0.35f;
        public float disabledCardAlpha = 0.45f;
        public float playableCardGlowAlpha = 0.35f;
        public float hoverGlowAlpha = 0.85f;

        [Header("Animation")]
        public float hudAnimationDuration = 0.15f;
        public float progressAnimDuration = 0.35f;
        public float buttonHoverScale = 1.04f;
        public float floatingTextDuration = 0.8f;

        public static BattleUIConfig CreateRuntimeDefault()
        {
            BattleUIConfig config = CreateInstance<BattleUIConfig>();
            config.hideFlags = HideFlags.HideAndDontSave;
            return config;
        }
    }
}
