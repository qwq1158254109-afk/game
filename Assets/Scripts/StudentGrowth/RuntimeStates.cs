using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    [Serializable]
    public class StageState
    {
        public StageData currentStage;
        public int remainingTime;
        public bool bossUnlocked;
        public bool bossDefeated;
        public int clearedStageCount;
        public int defeatedBossCount;
    }

    [Serializable]
    public class ChallengeRuntimeState
    {
        public ChallengeData data;
        public int currentProgress;
        public int currentTurn;
        public int currentEnergy;
        public bool isComplete;
        public bool isSuccess;
        public CombatPhase phase;
        public bool inputLocked;
        public int playerHp;
        public int playerMaxHp;
        public int playerBlock;
        public int enemyIntentDamage;
    }

    [Serializable]
    public class DeckState
    {
        public List<CardData> masterDeck = new List<CardData>();
        public List<CardData> drawPile = new List<CardData>();
        public List<CardData> hand = new List<CardData>();
        public List<CardData> discardPile = new List<CardData>();
        public List<CardData> exhaustPile = new List<CardData>();

        public void AddCardToMasterDeck(CardData card)
        {
            if (card != null)
            {
                masterDeck.Add(card);
            }
        }

        public bool RemoveCardFromMasterDeck(CardData card)
        {
            return card != null && masterDeck.Remove(card);
        }

        public bool UpgradeCard(CardData source, CardData upgraded)
        {
            if (source == null || upgraded == null)
            {
                return false;
            }

            int index = masterDeck.IndexOf(source);
            if (index < 0)
            {
                return false;
            }

            masterDeck[index] = upgraded;
            return true;
        }

        public void ClearBattlePiles()
        {
            drawPile.Clear();
            hand.Clear();
            discardPile.Clear();
            exhaustPile.Clear();
        }
    }

    [Serializable]
    public class ItemInventory
    {
        public List<ItemData> ownedItems = new List<ItemData>();

        public void AddItem(ItemData item)
        {
            if (item != null)
            {
                ownedItems.Add(item);
            }
        }

        public bool RemoveItem(ItemData item)
        {
            return item != null && ownedItems.Remove(item);
        }

        public bool ContainsItem(ItemData item)
        {
            return item != null && ownedItems.Contains(item);
        }

        public void ClearStageOnlyItems()
        {
            ownedItems.RemoveAll(item => item != null && item.itemType == ItemType.StageOnly);
        }
    }

    /// <summary>好感度条目（进存档）。npcId 对应 NpcData.npcId。</summary>
    [Serializable]
    public class NpcRelationshipEntry
    {
        public string npcId;
        public int value;
    }

    /// <summary>
    /// 全 NPC 好感度运行时状态（2026-10-24，对话系统好感度轮）。
    /// 0-100，起步 0；同一 NPC 重复累加并夹紧。存档按 entries 整表进出。
    /// </summary>
    [Serializable]
    public class NpcRelationshipState
    {
        public const int MinValue = 0;
        public const int MaxValue = 100;

        public List<NpcRelationshipEntry> entries = new List<NpcRelationshipEntry>();

        public int Get(string npcId)
        {
            NpcRelationshipEntry entry = Find(npcId);
            return entry != null ? entry.value : MinValue;
        }

        public void Add(string npcId, int delta)
        {
            if (string.IsNullOrEmpty(npcId) || delta == 0)
            {
                return;
            }

            NpcRelationshipEntry entry = Find(npcId);
            if (entry == null)
            {
                if (delta < 0)
                {
                    // 未登记过的 NPC 从 0 起步，负增量直接落地为 0，不产生负值条目。
                    return;
                }

                entry = new NpcRelationshipEntry { npcId = npcId };
                entries.Add(entry);
            }

            entry.value = Mathf.Clamp(entry.value + delta, MinValue, MaxValue);
        }

        private NpcRelationshipEntry Find(string npcId)
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].npcId == npcId)
                {
                    return entries[i];
                }
            }

            return null;
        }
    }
}
