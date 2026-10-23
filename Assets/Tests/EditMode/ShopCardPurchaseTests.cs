using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 商店买卡。重点是三条契约：
    /// 1. 失败时不得改动金钱与卡组（买不起不能半途扣钱）；
    /// 2. 成功后卡进 masterDeck，且金钱按售价扣除；
    /// 3. 售价缺失时按稀有度兜底，不能出现 0 元卡。
    /// </summary>
    public class ShopCardPurchaseTests
    {
        private readonly ShopPurchaseService service = new ShopPurchaseService();

        private static GameSessionState CreateSession(int money)
        {
            GameSessionState sessionState = new GameSessionState();
            sessionState.playerState.money = money;
            return sessionState;
        }

        private static CardData CreateCard(string cardId, CardRarity rarity, int price = 0)
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.cardId = cardId;
            card.displayName = cardId;
            card.rarity = rarity;
            card.price = price;
            return card;
        }

        [Test]
        public void PurchaseCardSucceedsAndAddsToMasterDeck()
        {
            GameSessionState sessionState = CreateSession(200);
            CardData card = CreateCard("card_test_common", CardRarity.Common);
            int deckBefore = sessionState.deckState.masterDeck.Count;
            int moneyBefore = sessionState.playerState.money;

            ShopCardPurchaseResult result = service.TryPurchaseCard(card, sessionState);

            Assert.IsTrue(result.success, result.message);
            Assert.AreEqual(moneyBefore - ShopPurchaseService.CommonCardPrice, sessionState.playerState.money);
            Assert.AreEqual(deckBefore + 1, sessionState.deckState.masterDeck.Count);
            Assert.AreSame(card, sessionState.deckState.masterDeck[deckBefore]);
            Assert.AreEqual(sessionState.deckState.masterDeck.Count, result.deckSizeAfter);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void PurchaseCardFailsWithoutChargingWhenMoneyShort()
        {
            GameSessionState sessionState = CreateSession(10);
            CardData card = CreateCard("card_test_expensive", CardRarity.Rare);
            int deckBefore = sessionState.deckState.masterDeck.Count;
            int moneyBefore = sessionState.playerState.money;

            ShopCardPurchaseResult result = service.TryPurchaseCard(card, sessionState);

            Assert.IsFalse(result.success);
            Assert.AreEqual(moneyBefore, sessionState.playerState.money, "买不起时不能扣钱");
            Assert.AreEqual(deckBefore, sessionState.deckState.masterDeck.Count, "买不起时不能进卡组");
            Object.DestroyImmediate(card);
        }

        [Test]
        public void PurchaseCardFailsOnNullCard()
        {
            GameSessionState sessionState = CreateSession(500);
            ShopCardPurchaseResult result = service.TryPurchaseCard(null, sessionState);
            Assert.IsFalse(result.success);
        }

        [Test]
        public void PurchaseCardFailsOnUninitializedSession()
        {
            CardData card = CreateCard("card_test_null_state", CardRarity.Common);
            ShopCardPurchaseResult result = service.TryPurchaseCard(card, null);
            Assert.IsFalse(result.success);
            Object.DestroyImmediate(card);
        }

        [Test]
        public void ResolveCardPriceFallsBackToRarity()
        {
            CardData common = CreateCard("c_common", CardRarity.Common);
            CardData uncommon = CreateCard("c_uncommon", CardRarity.Uncommon);
            CardData rare = CreateCard("c_rare", CardRarity.Rare);

            Assert.AreEqual(ShopPurchaseService.CommonCardPrice, ShopPurchaseService.ResolveCardPrice(common));
            Assert.AreEqual(ShopPurchaseService.UncommonCardPrice, ShopPurchaseService.ResolveCardPrice(uncommon));
            Assert.AreEqual(ShopPurchaseService.RareCardPrice, ShopPurchaseService.ResolveCardPrice(rare));

            Object.DestroyImmediate(common);
            Object.DestroyImmediate(uncommon);
            Object.DestroyImmediate(rare);
        }

        [Test]
        public void ResolveCardPricePrefersExplicitPrice()
        {
            CardData card = CreateCard("c_explicit", CardRarity.Rare, 7);
            Assert.AreEqual(7, ShopPurchaseService.ResolveCardPrice(card));
            Object.DestroyImmediate(card);
        }

        [Test]
        public void ResolveCardPriceNeverReturnsZeroForDefinedCard()
        {
            CardData card = CreateCard("c_zero", CardRarity.Common, 0);
            Assert.Greater(ShopPurchaseService.ResolveCardPrice(card), 0, "兜底价不能是 0，否则卡牌白送");
            Object.DestroyImmediate(card);
        }

        [Test]
        public void RepeatedPurchaseAddsDuplicateCopies()
        {
            GameSessionState sessionState = CreateSession(500);
            CardData card = CreateCard("card_test_dup", CardRarity.Common);
            int deckBefore = sessionState.deckState.masterDeck.Count;

            Assert.IsTrue(service.TryPurchaseCard(card, sessionState).success);
            Assert.IsTrue(service.TryPurchaseCard(card, sessionState).success);

            Assert.AreEqual(deckBefore + 2, sessionState.deckState.masterDeck.Count, "DBG 结构下同名卡可重复购买");
            Object.DestroyImmediate(card);
        }

        [Test]
        public void DefaultShopCardsAreNonEmpty()
        {
            List<CardData> cards = MvpSampleDataFactory.CreateDefaultShopCards();
            Assert.IsNotNull(cards);
            Assert.Greater(cards.Count, 0, "商店必须有卡可卖，卡池缺失时应走兜底");

            for (int i = 0; i < cards.Count; i++)
            {
                Assert.IsNotNull(cards[i], "货架不能出现空引用");
            }
        }
    }
}
