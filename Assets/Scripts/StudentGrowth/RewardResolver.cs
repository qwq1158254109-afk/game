using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public class RewardResolver
    {
        public void ApplyReward(RewardData reward, GameSessionState sessionState)
        {
            if (reward == null)
            {
                return;
            }

            if (sessionState == null)
            {
                throw new ArgumentNullException("sessionState");
            }

            switch (reward.rewardType)
            {
                case RewardType.AddCard:
                    sessionState.deckState.AddCardToMasterDeck(reward.card);
                    break;
                case RewardType.UpgradeCard:
                    UpgradeRewardCard(reward, sessionState.deckState);
                    break;
                case RewardType.RemoveCard:
                    sessionState.deckState.RemoveCardFromMasterDeck(reward.card);
                    break;
                case RewardType.AddPlayerStat:
                    sessionState.playerState.AddStat(reward.targetStat, reward.value);
                    break;
                case RewardType.ReducePlayerStat:
                    sessionState.playerState.AddStat(reward.targetStat, -reward.value);
                    break;
                case RewardType.AddMoney:
                    sessionState.playerState.AddStat(PlayerStatKey.Money, reward.value);
                    break;
                case RewardType.ReducePressure:
                    sessionState.playerState.AddStat(PlayerStatKey.Pressure, -reward.value);
                    break;
                case RewardType.IncreaseMaxEnergy:
                    sessionState.playerState.AddStat(PlayerStatKey.MaxEnergy, reward.value);
                    break;
                case RewardType.AddItem:
                    sessionState.itemInventory.AddItem(reward.item);
                    break;
                case RewardType.RemoveItem:
                    sessionState.itemInventory.RemoveItem(reward.item);
                    break;
                case RewardType.UnlockActivity:
                    AddUnique(sessionState.unlockedActivities, reward.activity);
                    break;
                case RewardType.UnlockStage:
                    AddUnique(sessionState.unlockedStages, reward.stage);
                    break;
                case RewardType.AddStageTime:
                    sessionState.stageState.remainingTime += reward.value;
                    if (sessionState.stageState.remainingTime < 0)
                    {
                        sessionState.stageState.remainingTime = 0;
                    }
                    break;
                case RewardType.AddRelationship:
                    // npcId 为空的 AddRelationship 是配置错误：静默跳过会让剧情组以为好感加上了。
                    if (string.IsNullOrEmpty(reward.npcId))
                    {
                        Debug.LogWarning("[RewardResolver] AddRelationship 缺少 npcId，奖励被跳过。");
                        break;
                    }

                    sessionState.npcRelationships.Add(reward.npcId, reward.value);
                    break;
                default:
                    throw new ArgumentOutOfRangeException("reward.rewardType", reward.rewardType, "Unsupported reward type.");
            }

            sessionState.ClampAll();
        }

        public void ApplyRewards(IEnumerable<RewardData> rewards, GameSessionState sessionState)
        {
            if (rewards == null)
            {
                return;
            }

            foreach (RewardData reward in rewards)
            {
                ApplyReward(reward, sessionState);
            }
        }

        private static void UpgradeRewardCard(RewardData reward, DeckState deckState)
        {
            if (reward.card == null || reward.card.upgradedVersion == null)
            {
                return;
            }

            deckState.UpgradeCard(reward.card, reward.card.upgradedVersion);
        }

        private static void AddUnique<T>(List<T> list, T value) where T : class
        {
            if (value != null && !list.Contains(value))
            {
                list.Add(value);
            }
        }
    }
}
