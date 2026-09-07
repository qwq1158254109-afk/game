using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public enum StageMapLocationStatus
    {
        Hidden,
        Locked,
        ComingSoon,
        Unlocked,
        Completed,
        Disabled
    }

    [CreateAssetMenu(menuName = "StudentGrowth/Map/Stage Map Data")]
    public sealed class StageMapData : ScriptableObject
    {
        public string stageId;
        public string title;
        public string description;
        public List<StageMapLocationEntry> locations = new List<StageMapLocationEntry>();
    }

    [Serializable]
    public sealed class StageMapLocationEntry
    {
        public string locationId;
        public string displayName;
        public string description;
        public string sceneName;
        public StageMapLocationStatus defaultStatus;
        public string unlockConditionKey;
        public string lockedReason;
        public string comingSoonText;
        public int sortOrder;
    }
}
