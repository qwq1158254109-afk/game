using System;
using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    [CreateAssetMenu(menuName = "Game Data/Stage Data")]
    public class StageData : ScriptableObject
    {
        public string stageId;
        public string displayName;
        public string description;
        public Sprite backgroundSprite;
        public Sprite icon;
        public string imagePath;

        public int timeLimit;

        public List<ActivityData> availableActivities = new List<ActivityData>();
        public ChallengeData bossChallenge;

        public List<RewardData> enterStageRewards = new List<RewardData>();
        public List<RewardData> clearStageRewards = new List<RewardData>();

        public string nextStageId;
        public bool isFinalStage;
    }

    [CreateAssetMenu(menuName = "Game Data/Activity Data")]
    public class ActivityData : ScriptableObject
    {
        public string activityId;
        public string displayName;
        public string description;
        public ActivityType activityType;
        public Sprite backgroundSprite;
        public Sprite icon;
        public string imagePath;

        public int timeCost = 1;
        public BattleRewardTiming battleRewardTiming = BattleRewardTiming.Participation;

        public ChallengeData linkedChallenge;
        public GameEventData linkedEvent;
        public MiniGameData linkedMiniGame;

        public List<RewardData> fixedRewards = new List<RewardData>();
        public List<RewardData> possibleRewards = new List<RewardData>();
        public List<EventConditionData> appearConditions = new List<EventConditionData>();
    }

    [CreateAssetMenu(menuName = "Game Data/Card Data")]
    public class CardData : ScriptableObject
    {
        public CardEffectSchemaVersion effectSchemaVersion = CardEffectSchemaVersion.Legacy;
        [Tooltip("Also require a stable ID and nonnegative cost. Unsupported targets, effects and keywords are always blocked.")]
        public bool requireSupportedBattleContent;
        public string cardId;
        public string displayName;
        public CardType cardType;
        public CardRarity rarity;
        public int cost;
        public string description;
        public Sprite cardSprite;
        public Sprite icon;
        public string imagePath;
        public CardTargetType targetType;
        public int damage;
        public int block;
        public int drawCount;
        public int discardCount;
        public bool exhaust;
        public bool ethereal;
        public bool retain;
        public List<CardEffectData> effects = new List<CardEffectData>();
        public List<string> tags = new List<string>();
        public bool exhaustAfterUse;
        public bool isUpgradeable;
        public CardData upgradedVersion;
    }

    [Serializable]
    public class CardEffectData
    {
        public CardEffectType effectType;
        public PlayerStatKey targetStat;
        public int value;
        public string tag;
        [Tooltip("For timed battle statuses: number of player action turns covered.")]
        public int durationTurns = 1;
    }

    [CreateAssetMenu(menuName = "Game Data/Challenge Data")]
    public class ChallengeData : ScriptableObject
    {
        public BattleRulesVersion battleRulesVersion = BattleRulesVersion.Legacy;
        public int unifiedHandLimit = 10;
        public int focusIntensityLimit = 10;
        [Tooltip("Optional loan deck for a learning challenge. Empty uses the permanent deck; never saved as owned cards.")]
        public List<CardData> loanDeck = new List<CardData>();
        public BattleIntentMode intentMode = BattleIntentMode.ByTurn;
        public ChallengeIntentData fallbackIntent;
        public bool requireSupportedBattleContent;
        public string challengeId;
        public string displayName;
        public string description;
        public Sprite backgroundSprite;
        public Sprite icon;
        public string imagePath;

        public bool isBoss;
        public string requiredStageId;

        public int targetProgress;
        public int maxTurns;
        public int startingEnergy;
        public int startingHandSize;

        public List<ChallengeIntentData> intents = new List<ChallengeIntentData>();
        public List<RewardData> successRewards = new List<RewardData>();
        public List<RewardData> failureRewards = new List<RewardData>();
        public List<RewardData> bossClearRewards = new List<RewardData>();
        public List<RewardData> bossFailureRewards = new List<RewardData>();
    }

    [Serializable]
    public class ChallengeIntentData
    {
        public string intentId;
        public string displayName;
        public string description;
        public int triggerTurn;
        public List<CardEffectData> effects = new List<CardEffectData>();
    }

    [CreateAssetMenu(menuName = "Game Data/Mini Game Data")]
    public class MiniGameData : ScriptableObject
    {
        public string miniGameId;
        public string displayName;
        public string description;
        public MiniGameType miniGameType;
        public Sprite backgroundSprite;
        public Sprite icon;
        public string imagePath;

        public int difficulty;
        public PlayerStatKey primaryStat;

        public List<RewardData> successRewards = new List<RewardData>();
        public List<RewardData> failureRewards = new List<RewardData>();
    }

    [CreateAssetMenu(menuName = "Game Data/Item Data")]
    public class ItemData : ScriptableObject
    {
        public string itemId;
        public string displayName;
        public ItemType itemType;
        public string description;
        public int price;
        public Sprite icon;
        public string imagePath;

        public List<CardEffectData> effects = new List<CardEffectData>();
        public ItemTriggerTiming triggerTiming = ItemTriggerTiming.OnUse;
        public bool consumedOnUse;
        public bool canUseInBattle;
        public bool canUseInStageScreen;
    }

    [Serializable]
    public class RewardData
    {
        public BattleRewardTiming battleTiming = BattleRewardTiming.InheritActivity;
        public RewardType rewardType;
        public PlayerStatKey targetStat;
        public int value;

        public CardData card;
        public ItemData item;
        public ActivityData activity;
        public StageData stage;

        public string description;
    }

    [CreateAssetMenu(menuName = "Game Data/Event Data")]
    public class GameEventData : ScriptableObject
    {
        public string eventId;
        public string title;
        public string description;
        public Sprite backgroundSprite;
        public Sprite icon;
        public string imagePath;
        public List<EventChoiceData> choices = new List<EventChoiceData>();
        public List<EventConditionData> appearConditions = new List<EventConditionData>();
    }

    [Serializable]
    public class EventChoiceData
    {
        public string choiceText;
        public List<EventConditionData> requirements = new List<EventConditionData>();
        public List<RewardData> rewards = new List<RewardData>();
    }

    [Serializable]
    public class EventConditionData
    {
        public PlayerStatKey statKey;
        public ComparisonType comparison;
        public int value;
    }

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
