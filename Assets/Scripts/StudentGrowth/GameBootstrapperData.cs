using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    [CreateAssetMenu(menuName = "Game Data/Game Bootstrapper Data")]
    public class GameBootstrapperData : ScriptableObject
    {
        public List<CardData> starterDeck = new List<CardData>();
        public List<ItemData> starterItems = new List<ItemData>();
        public List<StageData> stages = new List<StageData>();
        public List<LocationData> locations = new List<LocationData>();
        public List<EndingData> endings = new List<EndingData>();
        public StageData firstStage;
    }
}
