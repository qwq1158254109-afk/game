using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public enum LocationType
    {
        Home,
        School,
        Shop,
        Park,
        Library,
        Club,
        ExamHall,
        SpecialEvent
    }

    public enum LocationActionType
    {
        StartActivity,
        OpenShop,
        StartDialogue,
        ManageDeck,
        UseItem,
        Rest,
        Training,
        OpenSubMenu,
        TriggerEvent,
        StartMiniGame,
        StartCardBattle
    }

    public enum SceneTransitionMode
    {
        PanelSwitch,
        UnitySceneLoad,
        AdditiveSceneLoad
    }

    [CreateAssetMenu(menuName = "Game Data/Location Data")]
    public class LocationData : ScriptableObject
    {
        public string locationId;
        public string displayName;
        public string description;
        public LocationType locationType;
        public Sprite icon;
        public Sprite background;
        public string imagePath;

        public bool unlockedByDefault;
        public List<EventConditionData> unlockConditions = new List<EventConditionData>();
        public List<EventConditionData> appearConditions = new List<EventConditionData>();
        public List<LocationActionData> actions = new List<LocationActionData>();

        public int enterTimeCost;
        public int enterEnergyCost;
        public int enterMoneyCost;

        public string recommendedStageId;
        public bool hideWhenNoAvailableActions;
    }

    [CreateAssetMenu(menuName = "Game Data/Location Action Data")]
    public class LocationActionData : ScriptableObject
    {
        public string actionId;
        public string displayName;
        public string description;
        public LocationActionType actionType;

        public int timeCost;
        public int energyCost;
        public int moneyCost;

        public ActivityData linkedActivity;
        public ChallengeData linkedChallenge;
        public MiniGameData linkedMiniGame;
        public GameEventData linkedEvent;
        public string linkedShopId;

        public List<RewardData> fixedRewards = new List<RewardData>();
        public List<RewardData> possibleRewards = new List<RewardData>();
        public List<EventConditionData> appearConditions = new List<EventConditionData>();
        public List<EventConditionData> executeConditions = new List<EventConditionData>();

        public bool showRewardPreview = true;
        public bool requireConfirm;
        public string confirmMessage;
    }

    [Serializable]
    public class LocationState
    {
        public string currentLocationId;
        public List<string> unlockedLocationIds = new List<string>();
        public List<string> visitedLocationIds = new List<string>();
        public Dictionary<string, int> actionUseCount = new Dictionary<string, int>();
        public Dictionary<string, bool> locationFlags = new Dictionary<string, bool>();
    }

    [Serializable]
    public class LocationActionResult
    {
        public bool success;
        public int timeCost;
        public int energyCost;
        public int moneyCost;
        public List<RewardData> rewards = new List<RewardData>();
        public ActivityResult linkedActivityResult;
        public string resultTitle;
        public string resultDescription;
    }

    [Serializable]
    public class MapHintData
    {
        public string hintId;
        public string title;
        public string description;
        public LocationData targetLocation;
        public List<EventConditionData> appearConditions = new List<EventConditionData>();
        public int priority;
    }
}
