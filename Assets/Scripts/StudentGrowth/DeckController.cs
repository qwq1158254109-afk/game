using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public class DeckController : MonoBehaviour
    {
        public DeckState deckState = new DeckState();

        public void SetDeckState(DeckState state)
        {
            deckState = state ?? new DeckState();
        }

        public void InitializeBattleDeck(List<CardData> masterDeck)
        {
            List<CardData> sourceCards = masterDeck == null
                ? new List<CardData>()
                : new List<CardData>(masterDeck);

            deckState.ClearBattlePiles();
            deckState.masterDeck.Clear();

            deckState.masterDeck.AddRange(sourceCards);
            deckState.drawPile.AddRange(sourceCards);

            ShuffleDrawPile();
        }

        public void ShuffleDrawPile()
        {
            Shuffle(deckState.drawPile);
        }

        public void DrawCards(int count)
        {
            if (count <= 0)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (deckState.drawPile.Count == 0)
                {
                    ReshuffleDiscardIntoDrawPile();
                }

                if (deckState.drawPile.Count == 0)
                {
                    return;
                }

                CardData card = deckState.drawPile[0];
                deckState.drawPile.RemoveAt(0);
                deckState.hand.Add(card);
            }
        }

        public void PlayCard(CardData card)
        {
            if (card == null || !deckState.hand.Remove(card))
            {
                return;
            }

            if (card.exhaustAfterUse || card.exhaust)
            {
                deckState.exhaustPile.Add(card);
                return;
            }

            deckState.discardPile.Add(card);
        }

        public void DiscardHand()
        {
            deckState.discardPile.AddRange(deckState.hand);
            deckState.hand.Clear();
        }

        public void ReshuffleDiscardIntoDrawPile()
        {
            if (deckState.discardPile.Count == 0)
            {
                return;
            }

            deckState.drawPile.AddRange(deckState.discardPile);
            deckState.discardPile.Clear();
            ShuffleDrawPile();
        }

        public void AddCardToMasterDeck(CardData card)
        {
            deckState.AddCardToMasterDeck(card);
        }

        public void RemoveCardFromMasterDeck(CardData card)
        {
            deckState.RemoveCardFromMasterDeck(card);
        }

        public void UpgradeCard(CardData source, CardData upgraded)
        {
            deckState.UpgradeCard(source, upgraded);
        }

        private static void Shuffle<T>(IList<T> list)
        {
            if (list == null)
            {
                throw new ArgumentNullException("list");
            }

            for (int i = list.Count - 1; i > 0; i--)
            {
                int randomIndex = UnityEngine.Random.Range(0, i + 1);
                T item = list[i];
                list[i] = list[randomIndex];
                list[randomIndex] = item;
            }
        }
    }
}
