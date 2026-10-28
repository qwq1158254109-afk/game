using System.Collections.Generic;
using System.Text;

namespace StudentGrowth
{
    /// <summary>
    /// 抽卡记录面板的纯文本逻辑（2026-10-28 从 CapsuleView.ShowHistory 抽出）。
    ///
    /// 抽出来的理由：原来「取最近 N 条」的循环和统计都写死在 UI 方法里，
    /// EditMode 单测碰不到 —— 而「记录看不见 / 条数不对」正是用户报的问题。
    /// 这里只做字符串，不碰 Unity 对象，测试可以直接断言。
    /// </summary>
    public static class CapsuleHistoryFormatter
    {
        /// <summary>列表最多显示多少条（最近的在最上面）。</summary>
        public const int MaxEntries = 20;

        public sealed class Summary
        {
            public int total;
            public int common;
            public int uncommon;
            public int rare;
            public int duplicate;
            public int spent;
        }

        /// <summary>统计各稀有度、重复数与累计花费。packPrice 传 0 表示不显示花费。</summary>
        public static Summary Summarize(IReadOnlyList<CapsulePullEntry> pulls, int packPrice)
        {
            Summary summary = new Summary();
            if (pulls == null)
            {
                return summary;
            }

            for (int i = 0; i < pulls.Count; i++)
            {
                CapsulePullEntry entry = pulls[i];
                if (entry == null)
                {
                    continue;
                }

                if (entry.rarity == 2)
                {
                    summary.rare++;
                }
                else if (entry.rarity == 1)
                {
                    summary.uncommon++;
                }
                else
                {
                    summary.common++;
                }

                if (entry.duplicate)
                {
                    summary.duplicate++;
                }
            }

            summary.total = pulls.Count;
            summary.spent = packPrice > 0 ? pulls.Count * packPrice : 0;
            return summary;
        }

        /// <summary>
        /// 取最近 max 条，最新的一行在最前面，序号用原始的 1-based 位置。
        /// 恰好 max 条时不多不少（这里曾经被怀疑 off-by-one，测试锁住）。
        /// </summary>
        public static List<string> BuildLines(IReadOnlyList<CapsulePullEntry> pulls, int max = MaxEntries)
        {
            List<string> lines = new List<string>();
            if (pulls == null || pulls.Count == 0 || max <= 0)
            {
                return lines;
            }

            int from = pulls.Count - 1;
            int to = pulls.Count - max;
            if (to < 0)
            {
                to = 0;
            }

            for (int i = from; i >= to; i--)
            {
                CapsulePullEntry entry = pulls[i];
                if (entry == null)
                {
                    continue;
                }

                lines.Add((i + 1) + ". " + RarityTag(entry.rarity) + " " + entry.cardName
                          + (entry.duplicate ? "（重复）" : string.Empty));
            }

            return lines;
        }

        /// <summary>列表正文（含「只显示最近 N 条」的尾部提示）。</summary>
        public static string BuildEntriesText(IReadOnlyList<CapsulePullEntry> pulls, int max = MaxEntries)
        {
            List<string> lines = BuildLines(pulls, max);
            if (lines.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < lines.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(lines[i]);
            }

            if (pulls.Count > max)
            {
                builder.Append("\n…（只显示最近 " + max + " 条，共 " + pulls.Count + " 条）");
            }

            return builder.ToString();
        }

        /// <summary>顶部汇总行。</summary>
        public static string BuildSummaryText(IReadOnlyList<CapsulePullEntry> pulls, int packPrice)
        {
            if (pulls == null || pulls.Count == 0)
            {
                return "还没有抽过卡";
            }

            Summary summary = Summarize(pulls, packPrice);
            string text = "共 " + summary.total + " 次｜"
                          + "普通 " + summary.common + " · 非凡 " + summary.uncommon + " · 稀有 " + summary.rare
                          + "｜重复 " + summary.duplicate;
            if (packPrice > 0)
            {
                text += "｜累计花费 " + summary.spent + " 金";
            }

            return text;
        }

        public static string RarityTag(int rarity)
        {
            switch (rarity)
            {
                case 2: return "<color=#FFD24A>稀有</color>";
                case 1: return "<color=#7DB8FF>非凡</color>";
                default: return "<color=#DDDDDD>普通</color>";
            }
        }
    }
}
