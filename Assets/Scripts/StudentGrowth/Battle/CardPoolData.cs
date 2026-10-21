using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    /// <summary>
    /// 卡池资产（批次 0）：把「哪张卡从哪个池里出」从代码里搬进资产，
    /// 之后加卡 / 调池子不用再改 C#。目录约定 `Assets/Resources/StudentGrowth/Battle/Pools/`。
    /// </summary>
    [CreateAssetMenu(menuName = "Student Growth/Battle/Card Pool", fileName = "pool_new")]
    public class CardPoolData : ScriptableObject
    {
        [Tooltip("池子的稳定 ID，例如 pool_starter / pool_reward")]
        public string poolId = "pool_common";

        [Tooltip("给策划看的显示名")]
        public string displayName = "卡池";

        [Tooltip("启动选型用这个池作为初始牌组时勾上；同一时间只应有一个起始池")]
        public bool isStarterPool;

        [Tooltip("池内卡牌。空引用会被 CardPoolCatalog 过滤掉")]
        public List<CardData> cards = new List<CardData>();
    }
}
