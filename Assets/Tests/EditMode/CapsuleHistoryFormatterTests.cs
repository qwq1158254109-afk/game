using System.Collections.Generic;
using NUnit.Framework;
using StudentGrowth;

namespace StudentGrowth.Tests
{
    /// <summary>
    /// 抽卡记录面板的文本逻辑（2026-10-28，T4）。
    ///
    /// 锁的契约：
    /// 1. 「最近 20 条」恰好 20 条，不多不少（这里曾被怀疑 off-by-one）；
    /// 2. 最新的一条在最上面，序号用原始 1-based 位置（不是面板内的行号）；
    /// 3. 汇总的稀有度 / 重复 / 花费统计与条目一致；
    /// 4. 空列表与 null 不炸，返回空文本 / 提示语。
    /// </summary>
    public class CapsuleHistoryFormatterTests
    {
        [Test]
        public void BuildLines_WithExactly20_Returns20Rows()
        {
            List<CapsulePullEntry> pulls = MakePulls(20);

            List<string> lines = CapsuleHistoryFormatter.BuildLines(pulls);

            Assert.AreEqual(20, lines.Count, "恰好 20 条时不能多一条也不能少一条");
            StringAssert.StartsWith("20. ", lines[0], "最新一条在最上面，序号是原始的 20");
            StringAssert.StartsWith("1. ", lines[19], "最老一条在最后，序号是 1");
        }

        [Test]
        public void BuildLines_WithMoreThan20_KeepsLatest20()
        {
            List<CapsulePullEntry> pulls = MakePulls(25);

            List<string> lines = CapsuleHistoryFormatter.BuildLines(pulls);

            Assert.AreEqual(20, lines.Count);
            StringAssert.StartsWith("25. ", lines[0]);
            StringAssert.StartsWith("6. ", lines[19], "第 20 行应该是第 6 次抽卡（25 往前数 20 条）");
        }

        [Test]
        public void BuildLines_WithFewerThan20_ReturnsAll()
        {
            List<CapsulePullEntry> pulls = MakePulls(5);

            List<string> lines = CapsuleHistoryFormatter.BuildLines(pulls);

            Assert.AreEqual(5, lines.Count);
            StringAssert.StartsWith("5. ", lines[0]);
            StringAssert.StartsWith("1. ", lines[4]);
        }

        [Test]
        public void BuildLines_EmptyOrNull_ReturnsEmpty()
        {
            Assert.AreEqual(0, CapsuleHistoryFormatter.BuildLines(null).Count);
            Assert.AreEqual(0, CapsuleHistoryFormatter.BuildLines(new List<CapsulePullEntry>()).Count);
        }

        [Test]
        public void BuildLines_MarksDuplicateAndRarity()
        {
            List<CapsulePullEntry> pulls = new List<CapsulePullEntry>
            {
                new CapsulePullEntry { cardId = "c1", cardName = "刷题", rarity = 2, duplicate = false },
                new CapsulePullEntry { cardId = "c2", cardName = "深呼吸", rarity = 1, duplicate = true },
                new CapsulePullEntry { cardId = "c3", cardName = "错题本", rarity = 0, duplicate = false }
            };

            List<string> lines = CapsuleHistoryFormatter.BuildLines(pulls);

            // 最新一条（列表末尾）排最上面，所以顺序是 c3 → c2 → c1
            StringAssert.Contains("普通", lines[0]);
            StringAssert.Contains("错题本", lines[0]);
            StringAssert.Contains("非凡", lines[1]);
            StringAssert.Contains("（重复）", lines[1]);
            StringAssert.Contains("稀有", lines[2]);
            StringAssert.Contains("刷题", lines[2]);
        }

        [Test]
        public void BuildEntriesText_AppendsTruncationHint()
        {
            string text = CapsuleHistoryFormatter.BuildEntriesText(MakePulls(25));

            StringAssert.Contains("只显示最近 20 条", text);
            StringAssert.Contains("共 25 条", text);
            Assert.AreEqual(21, text.Split('\n').Length, "20 行正文 + 1 行提示");
        }

        [Test]
        public void Summarize_CountsRarityDuplicateAndSpend()
        {
            List<CapsulePullEntry> pulls = MakePulls(10);
            // MakePulls 的稀有度按 i % 3 轮换：i = 0..9 →
            // 普通 4 个（0/3/6/9）、非凡 3 个（1/4/7）、稀有 3 个（2/5/8）

            CapsuleHistoryFormatter.Summary summary = CapsuleHistoryFormatter.Summarize(pulls, 30);

            Assert.AreEqual(10, summary.total);
            Assert.AreEqual(4, summary.common);
            Assert.AreEqual(3, summary.uncommon);
            Assert.AreEqual(3, summary.rare);
            Assert.AreEqual(0, summary.duplicate, "MakePulls 默认不标重复");
            Assert.AreEqual(300, summary.spent, "10 次 × 30 金");
        }

        [Test]
        public void BuildSummaryText_EmptyList_ShowsPlaceholder()
        {
            Assert.AreEqual("还没有抽过卡",
                CapsuleHistoryFormatter.BuildSummaryText(new List<CapsulePullEntry>(), 30));
        }

        // ── 构造数据 ────────────────────────────────────────────────

        private static List<CapsulePullEntry> MakePulls(int count)
        {
            List<CapsulePullEntry> pulls = new List<CapsulePullEntry>();
            for (int i = 0; i < count; i++)
            {
                pulls.Add(new CapsulePullEntry
                {
                    cardId = "card_" + i,
                    cardName = "卡" + (i + 1),
                    rarity = i % 3,
                    duplicate = false
                });
            }

            return pulls;
        }
    }
}
