using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    public class ChallengeControllerTests
    {
        private GameObject gameObject;
        private ChallengeController challengeController;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("ChallengeControllerTests");
            challengeController = gameObject.AddComponent<ChallengeController>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void PlayingProgressCardAddsChallengeProgress()
        {
            CardData card = CreateProgressCard(5);
            ChallengeData challenge = CreateChallenge(10, 3, false);
            GameSessionState sessionState = new GameSessionState();
            sessionState.deckState.masterDeck.Add(card);
            challengeController.SetSessionState(sessionState);
            challengeController.StartChallenge(challenge);

            challengeController.PlayCard(card);

            Assert.AreEqual(5, challengeController.runtimeState.currentProgress);
        }

        [Test]
        public void ReachingTargetProgressCompletesChallengeSuccessfully()
        {
            CardData card = CreateProgressCard(10);
            ChallengeData challenge = CreateChallenge(10, 3, false);
            GameSessionState sessionState = new GameSessionState();
            sessionState.deckState.masterDeck.Add(card);
            challengeController.SetSessionState(sessionState);
            challengeController.StartChallenge(challenge);

            challengeController.PlayCard(card);

            Assert.IsTrue(challengeController.runtimeState.isComplete);
            Assert.IsTrue(challengeController.runtimeState.isSuccess);
        }

        [Test]
        public void MaxTurnsEndsChallengeAsFailureWhenTargetNotReached()
        {
            ChallengeData challenge = CreateChallenge(10, 1, false);
            challengeController.StartChallenge(challenge);

            challengeController.EndTurn();

            Assert.IsTrue(challengeController.runtimeState.isComplete);
            Assert.IsFalse(challengeController.runtimeState.isSuccess);
        }

        [Test]
        public void BossChallengeAppliesClearRewardsOnSuccess()
        {
            ChallengeData challenge = CreateChallenge(1, 1, true);
            challenge.bossClearRewards.Add(new RewardData
            {
                rewardType = RewardType.AddMoney,
                value = 25
            });
            challengeController.StartChallenge(challenge);

            challengeController.CompleteChallenge(true);

            Assert.AreEqual(125, challengeController.sessionState.playerState.money);
        }

        [Test]
        public void BossChallengeAppliesFailureRewardsOnFailure()
        {
            ChallengeData challenge = CreateChallenge(1, 1, true);
            challenge.bossFailureRewards.Add(new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.Pressure,
                value = 5
            });
            challengeController.StartChallenge(challenge);

            challengeController.CompleteChallenge(false);

            Assert.AreEqual(25, challengeController.sessionState.playerState.pressure);
        }

        private static CardData CreateProgressCard(int progress)
        {
            CardData card = ScriptableObject.CreateInstance<CardData>();
            card.cost = 1;
            card.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.AddChallengeProgress,
                value = progress
            });
            return card;
        }

        private static ChallengeData CreateChallenge(int targetProgress, int maxTurns, bool isBoss)
        {
            ChallengeData challenge = ScriptableObject.CreateInstance<ChallengeData>();
            challenge.targetProgress = targetProgress;
            challenge.maxTurns = maxTurns;
            challenge.startingEnergy = 3;
            challenge.startingHandSize = 5;
            challenge.isBoss = isBoss;
            return challenge;
        }
    }
}
