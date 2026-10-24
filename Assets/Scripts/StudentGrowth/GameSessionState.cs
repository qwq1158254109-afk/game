using System;
using System.Collections.Generic;

namespace StudentGrowth
{
    [Serializable]
    public class GameSessionState
    {
        [NonSerialized] private BattleResultConsumer battleResults;
        public BattleResultConsumer BattleResults => battleResults ?? (battleResults = new BattleResultConsumer(this));
        [NonSerialized] private BattleSessionFlow battleFlow;
        public BattleSessionFlow BattleFlow => battleFlow ?? (battleFlow = new BattleSessionFlow(this));
        public string runId;
        public bool tutorialSeen;
        public PlayerState playerState = new PlayerState();
        public TimeActionState timeActionState = new TimeActionState();
        public StageState stageState = new StageState();
        public LocationState locationState = new LocationState();
        public DeckState deckState = new DeckState();
        public ItemInventory itemInventory = new ItemInventory();
        public NpcRelationshipState npcRelationships = new NpcRelationshipState();
        public List<ActivityData> unlockedActivities = new List<ActivityData>();
        public List<StageData> unlockedStages = new List<StageData>();
        public List<string> clearedStageIds = new List<string>();
        public List<string> defeatedBossIds = new List<string>();
        public List<string> unlockedActivityIds = new List<string>();
        public List<string> unlockedLocationIds = new List<string>();
        public EndingData currentEnding;
        public bool endingReached;

        public void ClampAll()
        {
            playerState.ClampAll();
            if (timeActionState == null)
            {
                timeActionState = new TimeActionState();
            }

            timeActionState.Normalize();
            if (stageState.remainingTime < 0)
            {
                stageState.remainingTime = 0;
            }
        }
    }
}
