using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public class MiniGameController : MonoBehaviour
    {
        public MiniGameResult ResolveMiniGame(MiniGameData data, PlayerState playerState, System.Random random)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (playerState == null)
            {
                throw new ArgumentNullException("playerState");
            }

            int successChance = CalculateSuccessChance(data, playerState);
            System.Random safeRandom = random ?? new System.Random();
            bool success = safeRandom.Next(0, 100) < successChance;

            return new MiniGameResult
            {
                success = success,
                successChance = successChance,
                rewards = new List<RewardData>(success ? data.successRewards : data.failureRewards)
            };
        }

        public int CalculateSuccessChance(MiniGameData data, PlayerState playerState)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (playerState == null)
            {
                throw new ArgumentNullException("playerState");
            }

            int baseChance = 50;
            int statBonus = playerState.GetStat(data.primaryStat) / 2;
            int moodBonus = (playerState.mood - 50) / 4;
            int pressurePenalty = playerState.pressure / 5;
            int difficultyPenalty = data.difficulty * 5;
            int successChance = baseChance + statBonus + moodBonus - pressurePenalty - difficultyPenalty;
            return Mathf.Clamp(successChance, 15, 95);
        }
    }

    [Serializable]
    public class MiniGameResult
    {
        public bool success;
        public int successChance;
        public List<RewardData> rewards = new List<RewardData>();
    }
}
