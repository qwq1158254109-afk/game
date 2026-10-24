using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 按 npcId 查找 NpcData 的小目录。资产约定放 Resources/StudentGrowth/NPC/，
    /// 首次访问时 LoadAll 一次并缓存（进程内不再重复加载）。
    /// </summary>
    public static class NpcCatalog
    {
        private const string ResourceRoot = "StudentGrowth/NPC";
        private static Dictionary<string, NpcData> cache;

        public static NpcData Find(string npcId)
        {
            if (string.IsNullOrEmpty(npcId))
            {
                return null;
            }

            EnsureCache();
            NpcData data;
            return cache.TryGetValue(npcId, out data) ? data : null;
        }

        /// <summary>显示名兜底：找不到 NpcData 时原样返回 npcId，界面不出现空串。</summary>
        public static string DisplayName(string npcId)
        {
            NpcData data = Find(npcId);
            return data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : npcId;
        }

        private static void EnsureCache()
        {
            if (cache != null)
            {
                return;
            }

            cache = new Dictionary<string, NpcData>();
            NpcData[] all = Resources.LoadAll<NpcData>(ResourceRoot);
            if (all == null)
            {
                return;
            }

            foreach (NpcData data in all)
            {
                if (data == null || string.IsNullOrEmpty(data.npcId))
                {
                    continue;
                }

                cache[data.npcId] = data;
            }
        }

        /// <summary>测试与编辑器工具用：清空缓存，下次访问重新加载。</summary>
        public static void InvalidateCache()
        {
            cache = null;
        }
    }
}
