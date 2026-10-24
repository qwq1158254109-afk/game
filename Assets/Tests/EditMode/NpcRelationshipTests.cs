using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 对话系统好感度轮（2026-10-24）的核心契约：
    /// 状态累加与夹紧、奖励发放、好感度条件判定、缺 npcId 的防御行为。
    /// </summary>
    public class NpcRelationshipTests
    {
        [Test]
        public void AddRelationshipRewardAccumulatesAndClamps()
        {
            GameSessionState sessionState = new GameSessionState();
            RewardResolver resolver = new RewardResolver();

            resolver.ApplyRewards(new[]
            {
                new RewardData { rewardType = RewardType.AddRelationship, npcId = "npc_librarian", value = 30 },
                new RewardData { rewardType = RewardType.AddRelationship, npcId = "npc_librarian", value = 90 }
            }, sessionState);

            Assert.AreEqual(100, sessionState.npcRelationships.Get("npc_librarian"));
        }

        [Test]
        public void AddRelationshipWithoutNpcIdIsSkipped()
        {
            GameSessionState sessionState = new GameSessionState();
            RewardResolver resolver = new RewardResolver();

            resolver.ApplyReward(new RewardData { rewardType = RewardType.AddRelationship, value = 10 }, sessionState);

            Assert.AreEqual(0, sessionState.npcRelationships.entries.Count);
        }

        [Test]
        public void UnregisteredNpcStartsAtZeroAndNegativeDoesNothing()
        {
            NpcRelationshipState relationships = new NpcRelationshipState();

            Assert.AreEqual(0, relationships.Get("npc_anyone"));
            relationships.Add("npc_anyone", -5);
            Assert.AreEqual(0, relationships.Get("npc_anyone"));
            Assert.AreEqual(0, relationships.entries.Count);
        }

        [Test]
        public void RelationshipConditionResolvesAgainstSession()
        {
            GameSessionState sessionState = new GameSessionState();
            sessionState.npcRelationships.Add("npc_librarian", 30);

            EventConditionData condition = new EventConditionData
            {
                subject = ConditionSubject.NpcRelationship,
                npcId = "npc_librarian",
                comparison = ComparisonType.GreaterOrEqual,
                value = 30
            };

            Assert.IsTrue(EventConditionResolver.IsConditionMet(condition, sessionState));

            condition.value = 31;
            Assert.IsFalse(EventConditionResolver.IsConditionMet(condition, sessionState));
        }

        [Test]
        public void RelationshipConditionWithoutNpcIdAlwaysPasses()
        {
            GameSessionState sessionState = new GameSessionState();
            EventConditionData condition = new EventConditionData
            {
                subject = ConditionSubject.NpcRelationship,
                comparison = ComparisonType.GreaterOrEqual,
                value = 999
            };

            Assert.IsTrue(EventConditionResolver.IsConditionMet(condition, sessionState));
        }

        [Test]
        public void StatConditionStillWorksThroughSessionOverload()
        {
            GameSessionState sessionState = new GameSessionState();
            EventConditionData condition = new EventConditionData
            {
                subject = ConditionSubject.PlayerStat,
                statKey = PlayerStatKey.Pressure,
                comparison = ComparisonType.LessOrEqual,
                value = 20
            };

            // 默认压力 20，未序列化 subject 的存量资产走 PlayerStat 分支，行为不变。
            Assert.IsTrue(EventConditionResolver.IsConditionMet(condition, sessionState));
        }
    }
}
