using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 「阶段刚通关，待展示成长报告」的进程内队列。
    ///
    /// 为什么不用 GameSessionState 字段：那要同步改存档格式（SaveGameRecord + Codec），
    /// 而这份报告只在「打完 Boss 回地图」这一瞬间有意义，读档后再弹反而是干扰。
    /// 进程内静态标记足够，也不引入存档兼容风险。
    /// </summary>
    public static class StageReportQueue
    {
        private static StageData pending;

        public static bool HasPending => pending != null;

        public static void Enqueue(StageData stage)
        {
            if (stage != null)
            {
                pending = stage;
            }
        }

        /// <summary>取走并清空。返回 null 表示没有待展示的报告。</summary>
        public static StageData Consume()
        {
            StageData value = pending;
            pending = null;
            return value;
        }

        public static void Clear()
        {
            pending = null;
        }
    }
}
