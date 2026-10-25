using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 扭蛋抽卡契约（设计文档 §3 / §7）：
    /// 概率 65/27/8（万次 ±2pp）、首抽保底、重复判定、空池回退。
    /// </summary>
    public class CapsuleDrawTests
    {
        private static CardData MakeCard(string id, CardRarity rarity)
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.cardId = id;
            card.rarity = rarity;
            return card;
        }

        private static List<CardData> MakePool()
        {
            return new List<CardData>
            {
                MakeCard("c1", CardRarity.Common), MakeCard("c2", CardRarity.Common), MakeCard("c3", CardRarity.Common),
                MakeCard("c4", CardRarity.Common), MakeCard("c5", CardRarity.Common), MakeCard("c6", CardRarity.Common),
                MakeCard("u1", CardRarity.Uncommon), MakeCard("u2", CardRarity.Uncommon), MakeCard("u3", CardRarity.Uncommon),
                MakeCard("r1", CardRarity.Rare), MakeCard("r2", CardRarity.Rare)
            };
        }

        private static CapsulePackData MakePack()
        {
            CapsulePackData pack = ScriptableObject.CreateInstance<CapsulePackData>();
            pack.packId = "pack_test";
            pack.price = 40;
            pack.commonWeight = 65;
            pack.uncommonWeight = 27;
            pack.rareWeight = 8;
            pack.pityMinRarity = CardRarity.Uncommon;
            pack.duplicateRefund = 15;
            return pack;
        }

        [Test]
        public void WeightedRollMatchesConfiguredRates()
        {
            CapsulePackData pack = MakePack();
            List<CardData> pool = MakePool();
            System.Random rng = new System.Random(20261024);

            const int rolls = 10000;
            int common = 0, uncommon = 0, rare = 0;
            for (int i = 0; i < rolls; i++)
            {
                CardData card = CapsuleDrawService.Roll(pack, pool, false, null, rng).card;
                Assert.IsNotNull(card, "加权抽卡必须总能出卡");
                if (card.rarity == CardRarity.Common) common++;
                else if (card.rarity == CardRarity.Uncommon) uncommon++;
                else rare++;
            }

            Assert.AreEqual(rolls, common + uncommon + rare);
            Assert.LessOrEqual(Mathf.Abs(common - 6500), 200, "普通 65%±2pp");
            Assert.LessOrEqual(Mathf.Abs(uncommon - 2700), 200, "非凡 27%±2pp");
            Assert.LessOrEqual(Mathf.Abs(rare - 800), 200, "稀有 8%±2pp");
        }

        [Test]
        public void PityGuaranteesUncommonOrBetterAndConsumesOnce()
        {
            CapsulePackData pack = MakePack();
            List<CardData> pool = MakePool();
            System.Random rng = new System.Random(7);

            CapsuleDrawService.DrawOutcome outcome = CapsuleDrawService.Roll(pack, pool, true, null, rng);
            Assert.IsTrue(outcome.usedPity, "保底可用时必须消耗");
            Assert.GreaterOrEqual(outcome.card.rarity, CardRarity.Uncommon, "首抽必为非凡以上");

            outcome = CapsuleDrawService.Roll(pack, pool, false, null, rng);
            Assert.IsFalse(outcome.usedPity, "保底用掉后不再触发");
        }

        [Test]
        public void DuplicateFlagMatchesMasterDeck()
        {
            CapsulePackData pack = MakePack();
            List<CardData> pool = MakePool();
            System.Random rng = new System.Random(3);
            List<CardData> deck = new List<CardData> { pool[6] }; // u1

            CapsuleDrawService.DrawOutcome outcome = CapsuleDrawService.Roll(pack, pool, false, deck, rng);
            bool sawDuplicate = false;
            for (int i = 0; i < 300; i++)
            {
                outcome = CapsuleDrawService.Roll(pack, pool, false, deck, rng);
                if (outcome.card == pool[6])
                {
                    sawDuplicate = true;
                    Assert.IsTrue(outcome.duplicate, "卡组里已有的卡要标记重复");
                    break;
                }
                Assert.IsFalse(outcome.duplicate, "不在卡组里的卡不标记重复");
            }

            Assert.IsTrue(sawDuplicate, "300 次内应至少抽到一次卡组里那张（非凡占比 27%）");
        }

        [Test]
        public void MissingRaritySubPoolFallsBackToWholePool()
        {
            CapsulePackData pack = MakePack();
            // 池里一张稀有都没有，但权重仍可能掷到稀有位。
            List<CardData> pool = new List<CardData> { MakeCard("only1", CardRarity.Common), MakeCard("only2", CardRarity.Common) };
            System.Random rng = new System.Random(11);

            for (int i = 0; i < 500; i++)
            {
                CapsuleDrawService.DrawOutcome outcome = CapsuleDrawService.Roll(pack, pool, false, null, rng);
                Assert.IsNotNull(outcome.card, "目标品质池为空时回退全池，不能空手而归");
            }
        }
    }
}
