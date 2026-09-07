using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StudentGrowth
{
    public class BattleCardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private CardData data;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private Text costText;
        [SerializeField] private CanvasGroup canvasGroup;

        private Color baseColor = Color.white;
        private Color playableColor = Color.white;
        private Color unplayableColor = Color.white;
        private Color baseCostColor = Color.white;
        private Color unplayableCostColor = Color.white;
        private string tooltipBody;
        private float disabledAlpha = 0.55f;
        private bool dragging;
        private bool playable = true;
        public BattleCardPreview InstancePreview { get; private set; }
        public string InstanceId => InstancePreview?.Card?.InstanceId;

        public event Action<BattleCardView> OnCardHovered;
        public event Action<BattleCardView> OnCardUnhovered;
        public event Action<BattleCardView, PointerEventData> OnCardDragStarted;
        public event Action<BattleCardView, PointerEventData> OnCardDragging;
        public event Action<BattleCardView, PointerEventData> OnCardDragEnded;

        public CardData Data
        {
            get { return data; }
        }

        public RectTransform RectTransform
        {
            get { return transform as RectTransform; }
        }

        public int Cost
        {
            get { return InstancePreview != null ? InstancePreview.Cost : data == null ? 0 : data.cost; }
        }

        public string CardName
        {
            get { return InstancePreview != null ? InstancePreview.Card.Definition.Name : data == null ? string.Empty : data.displayName; }
        }

        public string Description
        {
            get
            {
                if (InstancePreview != null) return tooltipBody;
                if (data != null && !string.IsNullOrEmpty(data.description))
                {
                    return data.description;
                }

                return tooltipBody;
            }
        }

        public bool Playable
        {
            get { return playable; }
        }

        public void Initialize(CardData cardData, Button cardButton, Image cardBackground, Text cardCostText, string tooltip, bool isPlayable, BattleUIConfig config)
        {
            button = cardButton;
            background = cardBackground;
            costText = cardCostText;
            tooltipBody = tooltip;
            SetCard(cardData);
            BattleUiStyler.ApplyCardButton(button, background, config);

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            if (background != null)
            {
                baseColor = BattleUiStyler.GetCardBaseColor(config);
                playableColor = BattleUiStyler.GetPlayableCardColor(config);
                unplayableColor = BattleUiStyler.GetUnplayableCardColor(config);
            }

            disabledAlpha = config == null ? 0.55f : config.disabledCardAlpha;

            if (costText != null)
            {
                BattleUiStyler.ApplyCardText(costText, config, GameUiTextStyle.SmallText, true);
                baseCostColor = costText.color;
                unplayableCostColor = BattleUiStyler.GetWarningColor(config);
            }

            SetPlayable(isPlayable);
        }

        public void SetCard(CardData cardData)
        {
            InstancePreview = null;
            data = cardData;
        }

        public void BindInstance(BattleCardPreview preview)
        {
            InstancePreview = preview ?? throw new ArgumentNullException(nameof(preview));
            tooltipBody = "费用 " + preview.Cost + "\n效果：" + BattlePresentationText.Effects(preview.Card.Definition.Effects)
                + "\n本次：" + preview.Summary
                + (preview.Card.Definition.Exhaust?"\n消耗：使用后本场不再抽到。":"")
                + "\n" + BattlePresentationText.Failure(preview.Failure);
            if (costText != null) costText.text = "费用 " + preview.Cost;
            SetPlayable(preview.Playable);
        }

        public void SetPlayable(bool isPlayable)
        {
            playable = isPlayable;
            if (button != null)
            {
                button.interactable = true;
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = isPlayable ? 1f : disabledAlpha;
            }

            if (background != null)
            {
                background.color = isPlayable ? playableColor : unplayableColor;
            }

            if (costText != null)
            {
                costText.color = isPlayable ? baseCostColor : unplayableCostColor;
            }
        }

        public void SetHovered(bool hovered)
        {
            if (background == null || dragging)
            {
                return;
            }

            background.color = hovered
                ? Color.Lerp(playable ? playableColor : unplayableColor, Color.white, 0.10f)
                : playable ? playableColor : unplayableColor;
        }

        public void SetDragging(bool isDragging)
        {
            dragging = isDragging;
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = !isDragging;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (dragging)
            {
                return;
            }

            OnCardHovered?.Invoke(this);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (dragging)
            {
                return;
            }

            OnCardUnhovered?.Invoke(this);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            SetDragging(true);
            OnCardDragStarted?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            OnCardDragging?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            OnCardDragEnded?.Invoke(this, eventData);
            SetDragging(false);
        }
    }
}
