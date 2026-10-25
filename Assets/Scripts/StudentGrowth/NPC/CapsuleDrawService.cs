using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    /// <summary>一次扭蛋抽卡的记录（进存档）。普通可序列化类，不是 SO。</summary>
    [Serializable]
    public class CapsulePullEntry
    {
        public string cardId;
        public string cardName;
        /// <summary>CardRarity 序号：0 普通 / 1 非凡 / 2 稀有。</summary>
        public int rarity;
        public bool duplicate;
    }

    /// <summary>
    /// 扭蛋抽卡的纯逻辑（设计文档：Docs/CapsuleGachaDesign-2026-10-24.md）。
    /// 不碰会话状态——扣钱 / 入组 / 返现 / 保底标记由调用方结算，这里保持无副作用，
    /// EditMode 才能用固定种子做 10000 次概率验证。
    /// </summary>
    public static class CapsuleDrawService
    {
        public struct DrawOutcome
        {
            public CardData card;
            /// <summary>本次是否消耗了每局一次的首抽保底。</summary>
            public bool usedPity;
            /// <summary>抽到的卡是否已在主卡组（调用方据此返现）。</summary>
            public bool duplicate;
        }

        /// <summary>
        /// 抽一张。pool 为候选全集（约定 pool_reward）；保底可用时从「保底品质及以上」子池抽。
        /// 任何子池为空都回退全池，保证一定抽得出卡。
        /// </summary>
        public static DrawOutcome Roll(
            CapsulePackData pack,
            IReadOnlyList<CardData> pool,
            bool pityAvailable,
            IReadOnlyList<CardData> masterDeck,
            System.Random rng)
        {
            CardData card;
            bool usedPity = false;

            if (pack != null && pityAvailable)
            {
                List<CardData> pityPool = new List<CardData>();
                CollectRarityAtLeast(pool, pack.pityMinRarity, pityPool);
                if (pityPool.Count > 0)
                {
                    card = pityPool[rng.Next(pityPool.Count)];
                    usedPity = true;
                }
                else
                {
                    card = RollWeighted(pack, pool, rng);
                }
            }
            else
            {
                card = RollWeighted(pack, pool, rng);
            }

            bool duplicate = card != null && masterDeck != null && ContainsCard(masterDeck, card);
            return new DrawOutcome { card = card, usedPity = usedPity, duplicate = duplicate };
        }

        private static bool ContainsCard(IReadOnlyList<CardData> deck, CardData card)
        {
            for (int i = 0; i < deck.Count; i++)
            {
                if (deck[i] == card)
                {
                    return true;
                }
            }

            return false;
        }

        private static CardData RollWeighted(CapsulePackData pack, IReadOnlyList<CardData> pool, System.Random rng)
        {
            int common = Mathf.Max(0, pack.commonWeight);
            int uncommon = Mathf.Max(0, pack.uncommonWeight);
            int rare = Mathf.Max(0, pack.rareWeight);
            int total = common + uncommon + rare;
            if (total <= 0 || pool == null || pool.Count == 0)
            {
                return null;
            }

            int roll = rng.Next(total);
            CardRarity rarity = roll < common ? CardRarity.Common
                : roll < common + uncommon ? CardRarity.Uncommon
                : CardRarity.Rare;

            List<CardData> subPool = new List<CardData>();
            CollectRarity(pool, rarity, subPool);
            if (subPool.Count == 0)
            {
                CollectNonEmpty(pool, subPool);
            }

            return subPool.Count == 0 ? null : subPool[rng.Next(subPool.Count)];
        }

        private static void CollectRarity(IReadOnlyList<CardData> pool, CardRarity rarity, List<CardData> target)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].rarity == rarity)
                {
                    target.Add(pool[i]);
                }
            }
        }

        private static void CollectRarityAtLeast(IReadOnlyList<CardData> pool, CardRarity min, List<CardData> target)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null && pool[i].rarity >= min)
                {
                    target.Add(pool[i]);
                }
            }
        }

        private static void CollectNonEmpty(IReadOnlyList<CardData> pool, List<CardData> target)
        {
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i] != null)
                {
                    target.Add(pool[i]);
                }
            }
        }
    }
}
