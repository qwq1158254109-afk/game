using System;
using System.Collections.Generic;

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
}
