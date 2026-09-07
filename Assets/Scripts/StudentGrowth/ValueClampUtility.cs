using System;
using UnityEngine;

namespace StudentGrowth
{
    public static class ValueClampUtility
    {
        public const int StatMin = 0;
        public const int StatMax = 100;
        public const int MaxEnergyMin = 1;
        public const int MoneyMax = 9999;

        public static void ClampPlayerState(PlayerState playerState)
        {
            if (playerState == null)
            {
                throw new ArgumentNullException("playerState");
            }

            playerState.chinese = ClampPercentStat(playerState.chinese);
            playerState.math = ClampPercentStat(playerState.math);
            playerState.english = ClampPercentStat(playerState.english);
            playerState.maxEnergy = Mathf.Clamp(playerState.maxEnergy, MaxEnergyMin, StatMax);
            playerState.energy = Mathf.Clamp(playerState.energy, StatMin, playerState.maxEnergy);
            playerState.pressure = ClampPercentStat(playerState.pressure);
            playerState.mood = ClampPercentStat(playerState.mood);
            playerState.parentRelation = ClampPercentStat(playerState.parentRelation);
            playerState.social = ClampPercentStat(playerState.social);
            playerState.interest = ClampPercentStat(playerState.interest);
            playerState.confidence = ClampPercentStat(playerState.confidence);
            playerState.autonomy = ClampPercentStat(playerState.autonomy);
            playerState.money = Mathf.Clamp(playerState.money, StatMin, MoneyMax);
        }

        public static int ClampPercentStat(int value)
        {
            return Mathf.Clamp(value, StatMin, StatMax);
        }
    }
}
