using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace StudentGrowth
{
    public class HandLayoutController : MonoBehaviour
    {
        [SerializeField] private RectTransform handRoot;
        [SerializeField] private RectTransform dragRoot;
        [SerializeField] private BattleTooltipView tooltipView;
        [SerializeField] private BattleUIConfig config;
        [SerializeField] private float cardSpacing = 150f;
        [SerializeField] private float maxCardRotation = 10f;
        [SerializeField] private float arcHeight = 40f;
        [SerializeField] private float baseScale = 0.82f;
        [SerializeField] private float hoverScale = 1.15f;
        [SerializeField] private float hoverYOffset = 90f;
        [SerializeField] private float hoverSpread = 40f;
        [SerializeField] private float layoutLerpSpeed = 12f;
        [SerializeField] private float maxHandWidthRatio = 0.82f;

        private readonly List<BattleCardView> cards = new List<BattleCardView>();
        private Canvas canvas;
        private BattleCardView hoveredCard;
        private BattleCardView draggingCard;
        private int lastScreenWidth;
        private int lastScreenHeight;

        public event Action<BattleCardView, PointerEventData> CardDragStarted;
        public event Action<BattleCardView, PointerEventData> CardDragging;
        public event Action<BattleCardView, PointerEventData> CardDragEnded;
        public Func<BattleCardView, PointerEventData, bool> TryPlayDraggedCard { get; set; }

        public void Initialize(RectTransform root, RectTransform dragLayer, Canvas rootCanvas, BattleUIConfig battleConfig, BattleTooltipView tooltip)
        {
            handRoot = root;
            dragRoot = dragLayer;
            canvas = rootCanvas;
            config = battleConfig;
            tooltipView = tooltip;
            ApplyConfig();
            lastScreenWidth = Screen.width;
            lastScreenHeight = Screen.height;
        }

        public void RegisterCard(BattleCardView card)
        {
            if (card == null || cards.Contains(card))
            {
                return;
            }

            cards.Add(card);
            RectTransform rect = card.RectTransform;
            rect.SetParent(handRoot, false);
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            card.OnCardHovered += HandleCardHovered;
            card.OnCardUnhovered += HandleCardUnhovered;
            card.OnCardDragStarted += HandleCardDragStarted;
            card.OnCardDragging += HandleCardDragging;
            card.OnCardDragEnded += HandleCardDragEnded;
            ApplyLayout(true);
        }

        public void ClearCards()
        {
            for (int i = 0; i < cards.Count; i++)
            {
                Unsubscribe(cards[i]);
            }

            cards.Clear();
            hoveredCard = null;
            draggingCard = null;
        }

        public void RefreshLayout(bool instant)
        {
            ApplyLayout(instant);
        }

        private void OnDestroy()
        {
            ClearCards();
        }

        private void LateUpdate()
        {
            if (handRoot == null)
            {
                return;
            }

            if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
            {
                lastScreenWidth = Screen.width;
                lastScreenHeight = Screen.height;
                ApplyLayout(true);
                return;
            }

            ApplyLayout(false);
        }

        private void ApplyConfig()
        {
            if (config == null)
            {
                return;
            }

            baseScale = config.cardBaseScale;
            hoverScale = config.cardHoverScale;
            hoverYOffset = config.cardHoverYOffset;
            cardSpacing = config.cardSpacing;
            maxCardRotation = config.maxCardRotation;
            arcHeight = config.handArcHeight;
            hoverSpread = config.hoverSpread;
            layoutLerpSpeed = config.cardAnimSpeed;
        }

        private void HandleCardHovered(BattleCardView card)
        {
            hoveredCard = card;
            card.SetHovered(true);
            card.transform.SetAsLastSibling();
            if (tooltipView != null)
            {
                tooltipView.ShowForCard(card);
            }
        }

        private void HandleCardUnhovered(BattleCardView card)
        {
            if (hoveredCard == card)
            {
                hoveredCard = null;
            }

            card.SetHovered(false);
            if (tooltipView != null)
            {
                tooltipView.Hide();
            }
        }

        private void HandleCardDragStarted(BattleCardView card, PointerEventData eventData)
        {
            draggingCard = card;
            hoveredCard = null;
            if (tooltipView != null)
            {
                tooltipView.Hide();
            }

            if (dragRoot != null)
            {
                card.RectTransform.SetParent(dragRoot, true);
            }

            card.transform.SetAsLastSibling();
            CardDragStarted?.Invoke(card, eventData);
        }

        private void HandleCardDragging(BattleCardView card, PointerEventData eventData)
        {
            RectTransform targetRoot = dragRoot != null ? dragRoot : handRoot;
            if (targetRoot == null)
            {
                return;
            }

            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(targetRoot, eventData.position, eventCamera, out localPoint))
            {
                RectTransform rect = card.RectTransform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = localPoint;
                rect.localRotation = Quaternion.identity;
                rect.localScale = Vector3.one * hoverScale;
            }

            CardDragging?.Invoke(card, eventData);
        }

        private void HandleCardDragEnded(BattleCardView card, PointerEventData eventData)
        {
            CardDragEnded?.Invoke(card, eventData);
            if (TryPlayDraggedCard != null && TryPlayDraggedCard(card, eventData))
            {
                draggingCard = null;
                return;
            }

            if (handRoot != null)
            {
                card.RectTransform.SetParent(handRoot, false);
            }

            draggingCard = null;
            card.SetHovered(false);
            ApplyLayout(true);
        }

        private void ApplyLayout(bool instant)
        {
            int count = cards.Count;
            if (count == 0)
            {
                return;
            }

            float rootWidth = handRoot.rect.width > 1f ? handRoot.rect.width : Screen.width;
            float availableWidth = Mathf.Max(120f, rootWidth * maxHandWidthRatio);
            float spacing = count <= 1 ? 0f : Mathf.Min(cardSpacing, availableWidth / (count - 1));
            int hoveredIndex = hoveredCard == null ? -1 : cards.IndexOf(hoveredCard);
            float lerp = instant ? 1f : Mathf.Clamp01(Time.unscaledDeltaTime * layoutLerpSpeed);

            for (int i = 0; i < count; i++)
            {
                BattleCardView card = cards[i];
                if (card == null || card == draggingCard)
                {
                    continue;
                }

                RectTransform rect = card.RectTransform;
                rect.anchorMin = new Vector2(0.5f, 0f);
                rect.anchorMax = new Vector2(0.5f, 0f);
                rect.pivot = new Vector2(0.5f, 0.5f);

                float centeredIndex = i - (count - 1) * 0.5f;
                float normalized = count <= 1 ? 0f : centeredIndex / ((count - 1) * 0.5f);
                float x = centeredIndex * spacing;
                float y = 98f + arcHeight * Mathf.Abs(normalized);
                float rotation = -normalized * maxCardRotation;
                float targetScale = baseScale;

                if (hoveredIndex >= 0)
                {
                    int distance = Mathf.Abs(i - hoveredIndex);
                    if (card == hoveredCard)
                    {
                        y += hoverYOffset;
                        rotation = 0f;
                        targetScale = hoverScale;
                    }
                    else if (distance <= 2)
                    {
                        float spread = hoverSpread * (3 - distance) / 3f;
                        x += i < hoveredIndex ? -spread : spread;
                    }
                }

                Vector2 targetPosition = new Vector2(x, y);
                Vector3 targetScaleVector = Vector3.one * targetScale;
                Quaternion targetRotation = Quaternion.Euler(0f, 0f, rotation);

                rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, targetPosition, lerp);
                rect.localScale = Vector3.Lerp(rect.localScale, targetScaleVector, lerp);
                rect.localRotation = Quaternion.Slerp(rect.localRotation, targetRotation, lerp);

                if (card != hoveredCard)
                {
                    rect.SetSiblingIndex(i);
                }
            }

            if (hoveredCard != null && hoveredCard != draggingCard)
            {
                hoveredCard.transform.SetAsLastSibling();
            }
        }

        private void Unsubscribe(BattleCardView card)
        {
            if (card == null)
            {
                return;
            }

            card.OnCardHovered -= HandleCardHovered;
            card.OnCardUnhovered -= HandleCardUnhovered;
            card.OnCardDragStarted -= HandleCardDragStarted;
            card.OnCardDragging -= HandleCardDragging;
            card.OnCardDragEnded -= HandleCardDragEnded;
        }
    }
}
