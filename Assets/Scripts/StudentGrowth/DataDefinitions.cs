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

        [Tooltip("日期标签表。约定：长度 = timeLimit + 1，第 0 项是进入本阶段，最后一项是 Boss 前。" +
            "★ 长度限制：HUD 日期槽只有 80x32px / 20px 粗体，最多放 4 个汉字（如「初三下」）。" +
            "超过会折行被裁切。正文更长的日期请放在阶段进入弹窗里，不要塞这里。" +
            "整表留空是合法状态，HUD 会回退显示「剩 N 次」。")]
        public string[] dateMilestones = new string[0];

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

        [Tooltip("本阶段最多可执行次数（GDD §5.2）。1 = 只做一次（默认）；n > 1 = 上限 n；负数 = 不限次数（睡觉等循环活动）。" +
            "计数在进入新阶段时清空。0 按 1 处理（兼容旧资产）。")]
        public int maxUsesPerStage = 1;

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
        [Tooltip("商店售价。<=0 时按稀有度兜底（Common 50 / Uncommon 90 / Rare 150），见 ShopPurchaseService.ResolveCardPrice。")]
        public int price;
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

        [Tooltip("胜利后是否弹「三选一」：从玩家自己的牌组抽 N 张互不重复的卡，选一张复制进牌组（批次 1）")]
        public bool victoryCardChoice;

        [Tooltip("三选一的候选张数，默认 3；牌组里种类不足时有多少给多少")]
        public int victoryCardChoiceCount = 3;
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
