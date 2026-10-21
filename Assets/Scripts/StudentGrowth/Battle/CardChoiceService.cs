using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 批次 1：战后三选一的抽卡逻辑（纯逻辑，不碰 UI）。
    /// 规则（2026-10-17 用户拍板）：
    /// - 候选从**玩家自己的牌组**里抽；
    /// - 同一个弹窗内按 cardId 去重（三张不重复），跨弹窗允许重复；
    /// - 选中即复制一张进牌组（消耗类卡用掉会进 exhaust，重复有意义）；
    /// - 跳过无补偿。
    /// </summary>
    public static class CardChoiceService
    {
        /// <summary>从牌组里抽 count 张互不重复的卡作为候选；牌组不足时有多少给多少。</summary>
        public static List<CardData> BuildOffer(DeckState deck, int count)
        {
            List<CardData> offer = new List<CardData>();
            if (deck == null || deck.masterDeck == null || count <= 0)
            {
                return offer;
            }

            List<CardData> pool = new List<CardData>();
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < deck.masterDeck.Count; i++)
            {
                CardData card = deck.masterDeck[i];
                if (card == null)
                {
                    continue;
                }

                string key = string.IsNullOrEmpty(card.cardId) ? card.name : card.cardId;
                if (!seen.Add(key))
                {
                    continue;
                }

                pool.Add(card);
            }

            // Fisher-Yates，用 UnityEngine.Random 与本工程的随机源保持一致。
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                CardData tmp = pool[i];
                pool[i] = pool[j];
                pool[j] = tmp;
            }

            for (int i = 0; i < pool.Count && offer.Count < count; i++)
            {
                offer.Add(pool[i]);
            }

            return offer;
        }

        /// <summary>把玩家选中的卡复制一张进牌组。返回是否真的加进去了。</summary>
        public static bool Commit(GameSessionState session, CardData card)
        {
            if (session == null || session.deckState == null || card == null)
            {
                return false;
            }

            session.deckState.AddCardToMasterDeck(card);
            return true;
        }
    }
}
