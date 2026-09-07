using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class DeckControllerTests
    {
        private GameObject gameObject;
        private DeckController deckController;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("DeckControllerTests");
            deckController = gameObject.AddComponent<DeckController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void InitializeBattleDeckKeepsMasterDeck()
        {
            CardData first = CreateCard();
            CardData second = CreateCard();

            deckController.InitializeBattleDeck(new System.Collections.Generic.List<CardData>
            {
                first,
                second
            });

            Assert.AreEqual(2, deckController.deckState.masterDeck.Count);
            Assert.Contains(first, deckController.deckState.masterDeck);
            Assert.Contains(second, deckController.deckState.masterDeck);
        }

        [Test]
        public void DrawCardsMovesCardsFromDrawPileToHand()
        {
            CardData card = CreateCard();
            deckController.deckState.drawPile.Add(card);

            deckController.DrawCards(1);

            Assert.IsEmpty(deckController.deckState.drawPile);
            Assert.Contains(card, deckController.deckState.hand);
        }

        [Test]
        public void ReshuffleDiscardIntoDrawPileMovesDiscardedCards()
        {
            CardData card = CreateCard();
            deckController.deckState.discardPile.Add(card);

            deckController.ReshuffleDiscardIntoDrawPile();

            Assert.IsEmpty(deckController.deckState.discardPile);
            Assert.Contains(card, deckController.deckState.drawPile);
        }

        [Test]
        public void PlayExhaustCardMovesItToExhaustPile()
        {
            CardData card = CreateCard();
            card.exhaustAfterUse = true;
            deckController.deckState.hand.Add(card);

            deckController.PlayCard(card);

            Assert.IsEmpty(deckController.deckState.hand);
            Assert.Contains(card, deckController.deckState.exhaustPile);
            Assert.IsEmpty(deckController.deckState.discardPile);
        }

        [Test]
        public void PlayNonExhaustCardMovesItToDiscardPile()
        {
            CardData card = CreateCard();
            deckController.deckState.hand.Add(card);

            deckController.PlayCard(card);

            Assert.IsEmpty(deckController.deckState.hand);
            Assert.Contains(card, deckController.deckState.discardPile);
        }

        private static CardData CreateCard()
        {
            return ScriptableObject.CreateInstance<CardData>();
        }
    }
}
