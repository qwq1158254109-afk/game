using System;
using System.Collections.Generic;

namespace StudentGrowth
{
    /// <summary>背包里的一条：一个道具 + 数量（按 itemId 聚合，同款叠在一起）。</summary>
    public struct InventoryEntry
    {
        public ItemData item;
        public int count;

        public InventoryEntry(ItemData item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public bool IsValid => item != null && count > 0;
    }

    /// <summary>
    /// 背包模型：把 <see cref="ItemInventory.ownedItems"/>（List&lt;ItemData&gt;）按 itemId 聚合成"道具 × 数量"，
    /// 并提供使用 / 丢弃 / 变更事件。ownedItems 仍然是存档真源（SaveGameCodec 不用改）。
    /// </summary>
    public static class InventoryModel
    {
        /// <summary>背包内容变化时触发（使用、丢弃、获得）。UI 订阅它刷新。</summary>
        public static event Action Changed;

        public static IReadOnlyList<InventoryEntry> Entries(GameSessionState session)
        {
            List<InventoryEntry> result = new List<InventoryEntry>();
            if (session == null || session.itemInventory == null || session.itemInventory.ownedItems == null)
            {
                return result;
            }

            Dictionary<string, int> index = new Dictionary<string, int>();
            foreach (ItemData item in session.itemInventory.ownedItems)
            {
                if (item == null)
                {
                    continue;
                }

                string key = string.IsNullOrEmpty(item.itemId) ? item.name : item.itemId;
                if (index.TryGetValue(key, out int at))
                {
                    InventoryEntry entry = result[at];
                    entry.count++;
                    result[at] = entry;
                }
                else
                {
                    index[key] = result.Count;
                    result.Add(new InventoryEntry(item, 1));
                }
            }

            return result;
        }

        public static int CountOf(GameSessionState session, ItemData item)
        {
            if (session == null || session.itemInventory == null || item == null)
            {
                return 0;
            }

            int count = 0;
            foreach (ItemData owned in session.itemInventory.ownedItems)
            {
                if (owned != null && SameItem(owned, item))
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>在地图上使用一个道具；成功且消耗时移除一件，并广播变更。</summary>
        public static bool TryUse(GameSessionState session, ItemData item, out string message)
        {
            message = string.Empty;
            if (session == null || item == null)
            {
                message = "没有可用的道具。";
                return false;
            }

            ItemUseResult result = new ItemEffectResolver().UseItem(item, session);
            message = string.IsNullOrEmpty(result.message) ? (result.success ? "已使用。" : "用不了。") : result.message;
            if (!result.success)
            {
                return false;
            }

            Changed?.Invoke();
            return true;
        }

        /// <summary>丢弃一件（按 itemId 找第一个匹配的实例移除）。</summary>
        public static bool Drop(GameSessionState session, ItemData item)
        {
            if (session == null || session.itemInventory == null || item == null)
            {
                return false;
            }

            foreach (ItemData owned in session.itemInventory.ownedItems)
            {
                if (owned != null && SameItem(owned, item))
                {
                    bool removed = session.itemInventory.RemoveItem(owned);
                    if (removed)
                    {
                        Changed?.Invoke();
                    }

                    return removed;
                }
            }

            return false;
        }

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        private static bool SameItem(ItemData a, ItemData b)
        {
            if (a == b)
            {
                return true;
            }

            if (a == null || b == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(a.itemId) && a.itemId == b.itemId)
            {
                return true;
            }

            return a.name == b.name;
        }
    }
}
