using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 卡池读取入口（批次 0）。资产缺失时全部返回空，由调用方回退到旧的硬编码牌组，
    /// 保证「没建池子」的工程照样能跑。
    /// </summary>
    public static class CardPoolCatalog
    {
        public const string ResourceRoot = "StudentGrowth/Battle/Pools";
        public const string StarterPoolId = "pool_starter";

        private static CardPoolData[] cache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            cache = null;
        }

        public static CardPoolData[] All()
        {
            if (cache == null)
            {
                cache = Resources.LoadAll<CardPoolData>(ResourceRoot);
            }

            return cache;
        }

        public static CardPoolData Find(string poolId)
        {
            if (string.IsNullOrEmpty(poolId))
            {
                return null;
            }

            CardPoolData[] pools = All();
            for (int i = 0; i < pools.Length; i++)
            {
                if (pools[i] != null && pools[i].poolId == poolId)
                {
                    return pools[i];
                }
            }

            return null;
        }

        /// <summary>按 ID 找，找不到再退到「勾了 isStarterPool 的那个」。</summary>
        public static CardPoolData Starter()
        {
            CardPoolData byId = Find(StarterPoolId);
            if (byId != null)
            {
                return byId;
            }

            CardPoolData[] pools = All();
            for (int i = 0; i < pools.Length; i++)
            {
                if (pools[i] != null && pools[i].isStarterPool)
                {
                    return pools[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 取起始牌组。池子不存在 / 池内全是空引用时返回 false，调用方继续走旧回退。
        /// </summary>
        public static bool TryGetStarterCards(List<CardData> target)
        {
            if (target == null)
            {
                return false;
            }

            CardPoolData pool = Starter();
            if (pool == null || pool.cards == null)
            {
                return false;
            }

            int added = 0;
            for (int i = 0; i < pool.cards.Count; i++)
            {
                CardData card = pool.cards[i];
                if (card == null)
                {
                    continue;
                }

                target.Add(card);
                added++;
            }

            return added > 0;
        }

        /// <summary>按稀有度筛池内卡牌，给后续「三选一」按稀有度配权重用。</summary>
        public static List<CardData> CardsWithRarity(CardPoolData pool, CardRarity rarity)
        {
            List<CardData> result = new List<CardData>();
            if (pool == null || pool.cards == null)
            {
                return result;
            }

            for (int i = 0; i < pool.cards.Count; i++)
            {
                CardData card = pool.cards[i];
                if (card != null && card.rarity == rarity)
                {
                    result.Add(card);
                }
            }

            return result;
        }
    }
}
