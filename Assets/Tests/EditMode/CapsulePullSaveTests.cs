using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;
using UnityEngine;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 抽卡记录的存档往返（2026-10-28，T4）。
    ///
    /// 锁的契约：
    /// 1. capsulePulls 进存档、出存档后字段（cardId / cardName / rarity / duplicate）原样保留；
    /// 2. 上限 200 条，且超过时保留**最近**的 200 条 ——
    ///    原来 Decode 用的是 Take(200)，取的是最老的 200 条，会把最新抽卡丢掉。
    /// </summary>
    public class CapsulePullSaveTests
    {
        [Test]
        public void CapsulePullsSurviveRoundtrip()
        {
            SaveGameCodec codec = new SaveGameCodec(new SaveContentResolver());
            GameSessionState session = new GameSessionState();
            session.capsulePulls.Add(new CapsulePullEntry
            {
                cardId = "card_a", cardName = "刷题", rarity = 2, duplicate = false
            });
            session.capsulePulls.Add(new CapsulePullEntry
            {
                cardId = "card_b", cardName = "深呼吸", rarity = 1, duplicate = true
            });

            string json = codec.Encode(codec.Capture(session, ScenePath, 0, null, Vector2.down));
            List<CapsulePullEntry> restored = codec.Decode(json).Session.capsulePulls;

            Assert.AreEqual(2, restored.Count);
            Assert.AreEqual("card_a", restored[0].cardId);
            Assert.AreEqual("刷题", restored[0].cardName);
            Assert.AreEqual(2, restored[0].rarity);
            Assert.IsFalse(restored[0].duplicate);
            Assert.AreEqual("card_b", restored[1].cardId);
            Assert.AreEqual(1, restored[1].rarity);
            Assert.IsTrue(restored[1].duplicate);
        }

        [Test]
        public void CapsulePullsKeepLatest200WhenOverLimit()
        {
            SaveGameCodec codec = new SaveGameCodec(new SaveContentResolver());
            GameSessionState session = new GameSessionState();
            for (int i = 0; i < 230; i++)
            {
                session.capsulePulls.Add(new CapsulePullEntry
                {
                    cardId = "card_" + i, cardName = "卡" + i, rarity = i % 3, duplicate = false
                });
            }

            string json = codec.Encode(codec.Capture(session, ScenePath, 0, null, Vector2.down));
            List<CapsulePullEntry> restored = codec.Decode(json).Session.capsulePulls;

            Assert.AreEqual(200, restored.Count, "上限 200 条");
            Assert.AreEqual("card_30", restored[0].cardId, "保留的应是最近 200 条，起点是第 30 条");
            Assert.AreEqual("card_229", restored[199].cardId, "最后一条是最新抽卡");
        }

        [Test]
        public void CapsulePullsWithEmptyCardIdAreDropped()
        {
            SaveGameCodec codec = new SaveGameCodec(new SaveContentResolver());
            GameSessionState session = new GameSessionState();
            session.capsulePulls.Add(new CapsulePullEntry { cardId = "", cardName = "坏数据" });
            session.capsulePulls.Add(new CapsulePullEntry { cardId = "card_ok", cardName = "正常" });

            string json = codec.Encode(codec.Capture(session, ScenePath, 0, null, Vector2.down));
            List<CapsulePullEntry> restored = codec.Decode(json).Session.capsulePulls;

            Assert.AreEqual(1, restored.Count);
            Assert.AreEqual("card_ok", restored[0].cardId);
        }

        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity";
    }
}
