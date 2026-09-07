using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class MiniGameControllerTests
    {
        private GameObject gameObject;
        private MiniGameController miniGameController;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("MiniGameControllerTests");
            miniGameController = gameObject.AddComponent<MiniGameController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void SuccessChanceIsClampedBetweenFifteenAndNinetyFive()
        {
            MiniGameData easyData = CreateMiniGame(PlayerStatKey.Math, -10);
            PlayerState highPlayer = new PlayerState();
            highPlayer.SetStat(PlayerStatKey.Math, 100);
            highPlayer.SetStat(PlayerStatKey.Mood, 100);
            highPlayer.SetStat(PlayerStatKey.Pressure, 0);

            MiniGameData hardData = CreateMiniGame(PlayerStatKey.Math, 50);
            PlayerState lowPlayer = new PlayerState();
            lowPlayer.SetStat(PlayerStatKey.Math, 0);
            lowPlayer.SetStat(PlayerStatKey.Mood, 0);
            lowPlayer.SetStat(PlayerStatKey.Pressure, 100);

            Assert.AreEqual(95, miniGameController.CalculateSuccessChance(easyData, highPlayer));
            Assert.AreEqual(15, miniGameController.CalculateSuccessChance(hardData, lowPlayer));
        }

        [Test]
        public void HigherPrimaryStatIncreasesSuccessChance()
        {
            MiniGameData data = CreateMiniGame(PlayerStatKey.English, 3);
            PlayerState lowPlayer = new PlayerState();
            PlayerState highPlayer = new PlayerState();
            lowPlayer.SetStat(PlayerStatKey.English, 10);
            highPlayer.SetStat(PlayerStatKey.English, 80);

            int lowChance = miniGameController.CalculateSuccessChance(data, lowPlayer);
            int highChance = miniGameController.CalculateSuccessChance(data, highPlayer);

            Assert.Greater(highChance, lowChance);
        }

        [Test]
        public void HigherPressureReducesSuccessChance()
        {
            MiniGameData data = CreateMiniGame(PlayerStatKey.Chinese, 2);
            PlayerState calmPlayer = new PlayerState();
            PlayerState stressedPlayer = new PlayerState();
            calmPlayer.SetStat(PlayerStatKey.Pressure, 0);
            stressedPlayer.SetStat(PlayerStatKey.Pressure, 80);

            int calmChance = miniGameController.CalculateSuccessChance(data, calmPlayer);
            int stressedChance = miniGameController.CalculateSuccessChance(data, stressedPlayer);

            Assert.Less(stressedChance, calmChance);
        }

        [Test]
        public void SuccessReturnsSuccessRewards()
        {
            MiniGameData data = CreateMiniGame(PlayerStatKey.Math, 1);
            RewardData reward = new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.Math,
                value = 4
            };
            data.successRewards.Add(reward);

            MiniGameResult result = miniGameController.ResolveMiniGame(data, new PlayerState(), new FixedRandom(0));

            Assert.IsTrue(result.success);
            Assert.Contains(reward, result.rewards);
        }

        [Test]
        public void FailureReturnsFailureRewards()
        {
            MiniGameData data = CreateMiniGame(PlayerStatKey.Math, 1);
            RewardData reward = new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.Pressure,
                value = 3
            };
            data.failureRewards.Add(reward);

            MiniGameResult result = miniGameController.ResolveMiniGame(data, new PlayerState(), new FixedRandom(99));

            Assert.IsFalse(result.success);
            Assert.Contains(reward, result.rewards);
        }

        private static MiniGameData CreateMiniGame(PlayerStatKey primaryStat, int difficulty)
        {
            MiniGameData data = ScriptableObject.CreateInstance<MiniGameData>();
            data.primaryStat = primaryStat;
            data.difficulty = difficulty;
            return data;
        }

        private class FixedRandom : System.Random
        {
            private readonly int value;

            public FixedRandom(int value)
            {
                this.value = value;
            }

            public override int Next(int minValue, int maxValue)
            {
                return value;
            }
        }
    }
}
