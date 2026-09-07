using System;
using System.Collections.Generic;

namespace StudentGrowth
{
    public class ItemEffectResolver
    {
        private readonly CardEffectResolver cardEffectResolver = new CardEffectResolver();

        public ItemUseResult UseItem(
            ItemData item,
            GameSessionState sessionState,
            ChallengeRuntimeState challengeState = null,
            DeckController deckController = null,
            bool inBattle = false)
        {
            ItemUseResult result = ValidateItemUse(item, sessionState, inBattle);
            if (!result.success)
            {
                return result;
            }

            ItemUseResult effectResult = ApplyItemEffects(item, ItemTriggerTiming.OnUse, sessionState, challengeState, deckController);
            if (!effectResult.success)
            {
                return effectResult;
            }

            if (item.consumedOnUse || item.itemType == ItemType.Consumable)
            {
                sessionState.itemInventory.RemoveItem(item);
                effectResult.consumed = true;
            }

            return effectResult;
        }

        public int ApplyPassiveItems(
            ItemTriggerTiming timing,
            GameSessionState sessionState,
            ChallengeRuntimeState challengeState = null,
            DeckController deckController = null)
        {
            if (sessionState == null)
            {
                throw new ArgumentNullException("sessionState");
            }

            int appliedCount = 0;
            List<ItemData> snapshot = new List<ItemData>(sessionState.itemInventory.ownedItems);
            foreach (ItemData item in snapshot)
            {
                if (item == null || item.itemType != ItemType.Passive)
                {
                    continue;
                }

                if (ApplyItemEffects(item, timing, sessionState, challengeState, deckController).success)
                {
                    appliedCount++;
                }
            }

            return appliedCount;
        }

        public ItemUseResult ApplyItemEffects(
            ItemData item,
            ItemTriggerTiming timing,
            GameSessionState sessionState,
            ChallengeRuntimeState challengeState = null,
            DeckController deckController = null)
        {
            if (item == null)
            {
                return ItemUseResult.Failed("Item is null.");
            }

            if (sessionState == null)
            {
                throw new ArgumentNullException("sessionState");
            }

            if (item.triggerTiming != timing)
            {
                return ItemUseResult.Failed("Item timing does not match.");
            }

            foreach (CardEffectData effect in item.effects)
            {
                cardEffectResolver.ApplyEffect(effect, sessionState, challengeState, deckController);
            }

            return ItemUseResult.Success(item.effects.Count, false);
        }

        public int ClearStageOnlyItems(GameSessionState sessionState)
        {
            if (sessionState == null)
            {
                throw new ArgumentNullException("sessionState");
            }

            int beforeCount = sessionState.itemInventory.ownedItems.Count;
            sessionState.itemInventory.ClearStageOnlyItems();
            return beforeCount - sessionState.itemInventory.ownedItems.Count;
        }

        private static ItemUseResult ValidateItemUse(ItemData item, GameSessionState sessionState, bool inBattle)
        {
            if (item == null)
            {
                return ItemUseResult.Failed("Item is null.");
            }

            if (sessionState == null)
            {
                throw new ArgumentNullException("sessionState");
            }

            if (!sessionState.itemInventory.ContainsItem(item))
            {
                return ItemUseResult.Failed("Item is not owned.");
            }

            if (inBattle && !item.canUseInBattle)
            {
                return ItemUseResult.Failed("Item cannot be used in battle.");
            }

            if (!inBattle && !item.canUseInStageScreen)
            {
                return ItemUseResult.Failed("Item cannot be used on the stage screen.");
            }

            return ItemUseResult.Success(0, false);
        }
    }

    [Serializable]
    public class ItemUseResult
    {
        public bool success;
        public bool consumed;
        public int appliedEffectCount;
        public string message;

        public static ItemUseResult Success(int appliedEffectCount, bool consumed)
        {
            return new ItemUseResult
            {
                success = true,
                consumed = consumed,
                appliedEffectCount = appliedEffectCount,
                message = string.Empty
            };
        }

        public static ItemUseResult Failed(string message)
        {
            return new ItemUseResult
            {
                success = false,
                consumed = false,
                appliedEffectCount = 0,
                message = message
            };
        }
    }
}
