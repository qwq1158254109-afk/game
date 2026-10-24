using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    // 2026-10-24 从 DataDefinitions.cs 拆出：EndingData 需要能序列化成 .asset
    // （Assets/Data/StudentGrowth/Endings/，由 MvpSampleAssetGenerator 快照生成）。
    // 与 DataDefinitions.cs 同文件的非主类 SO 写成资产时 m_Script 解析失败，
    // 资产加载得 null（实测：LibraryLibrarianEvent.asset 至今如此，存档目录从未收录）。
    // 项目里能正常落盘的 SO（HomeMapActivityData、GameBootstrapperData 等）全部是独立文件的主类。
    [CreateAssetMenu(menuName = "Game Data/Ending Data")]
    public class EndingData : ScriptableObject
    {
        public string endingId;
        public string displayName;
        public string description;
        public Sprite illustrationSprite;
        public string imagePath;
        public List<EventConditionData> conditions = new List<EventConditionData>();
        public int minimumClearedStages;
        public int minimumDefeatedBosses;
        public int minimumSubjectAverage;
        public int maximumPressure = 100;
        public bool requireEndingReached;
        public int priority;
    }
}
