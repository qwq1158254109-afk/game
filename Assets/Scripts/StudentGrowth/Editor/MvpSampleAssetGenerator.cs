using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.Editor
{
    public static class MvpSampleAssetGenerator
    {
        private const string ResourceRoot = "Assets/Resources";
        private const string StudentGrowthRoot = ResourceRoot + "/StudentGrowth";
        private const string MvpRoot = StudentGrowthRoot + "/MVP";
        private const string CardsRoot = MvpRoot + "/Cards";
        private const string ItemsRoot = MvpRoot + "/Items";
        private const string MiniGamesRoot = MvpRoot + "/MiniGames";
        private const string EventsRoot = MvpRoot + "/Events";
        private const string ChallengesRoot = MvpRoot + "/Challenges";
        private const string ActivitiesRoot = MvpRoot + "/Activities";
        private const string StagesRoot = MvpRoot + "/Stages";
        private const string EndingsRoot = MvpRoot + "/Endings";
        private const string BootstrapperPath = StudentGrowthRoot + "/MvpBootstrapperData.asset";

        [MenuItem("Tools/Student Growth/Data/Generate MVP Sample Assets")]
        public static void GenerateMvpSampleAssets()
        {
            EnsureFolders();

            Dictionary<string, CardData> cards = CreateCards();
            Dictionary<string, ItemData> items = CreateItems();
            Dictionary<string, MiniGameData> miniGames = CreateMiniGames(cards, items);
            Dictionary<string, GameEventData> events = CreateEvents(cards, items);
            Dictionary<string, ChallengeData> challenges = CreateChallenges();
            Dictionary<string, ActivityData> activities = CreateActivities(miniGames, events);
            List<StageData> stages = CreateStages(challenges, activities);
            List<EndingData> endings = CreateEndings();
            GameBootstrapperData bootstrapperData = GetOrCreateAsset<GameBootstrapperData>(BootstrapperPath);

            bootstrapperData.starterDeck = new List<CardData>
            {
                cards["card_study_class_focus"],
                cards["card_study_math_practice"],
                cards["card_study_wrong_book"],
                cards["card_study_english_words"],
                cards["card_study_mock_exam"],
                cards["card_rest_good_sleep"]
            };
            bootstrapperData.starterItems = new List<ItemData>
            {
                items["item_energy_drink"],
                items["item_lucky_pen"]
            };
            bootstrapperData.stages = stages;
            bootstrapperData.endings = endings;
            bootstrapperData.firstStage = stages.Count > 0 ? stages[0] : null;
            EditorUtility.SetDirty(bootstrapperData);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Generated Student Growth MVP sample assets at " + MvpRoot);
        }

        private static Dictionary<string, CardData> CreateCards()
        {
            Dictionary<string, CardData> cards = new Dictionary<string, CardData>();
            AddCard(cards, "card_study_class_focus", "课堂专注", CardType.Study, 1, CardRarity.Common, Effect(CardEffectType.AddChallengeProgress, 8));
            AddCard(cards, "card_study_math_practice", "数学练习", CardType.Study, 1, CardRarity.Common, Effect(CardEffectType.AddChallengeProgress, 10));
            AddCard(cards, "card_study_english_words", "背单词", CardType.Study, 1, CardRarity.Common, Effect(CardEffectType.AddChallengeProgress, 7));
            AddCard(cards, "card_study_wrong_book", "整理错题", CardType.Study, 1, CardRarity.Common, Effect(CardEffectType.AddChallengeProgress, 11));
            AddCard(cards, "card_study_mock_exam", "模拟考试", CardType.Study, 2, CardRarity.Uncommon, Effect(CardEffectType.AddChallengeProgress, 18), Effect(CardEffectType.AddPressure, 2));
            AddCard(cards, "card_study_after_class_review", "课后复盘", CardType.Study, 1, CardRarity.Common, Effect(CardEffectType.AddChallengeProgress, 9));

            AddCard(cards, "card_rest_good_sleep", "好好睡觉", CardType.Rest, 1, CardRarity.Common, Effect(CardEffectType.GainEnergy, 8), Effect(CardEffectType.ReducePressure, 3));
            AddCard(cards, "card_rest_music_relax", "听音乐放松", CardType.Rest, 1, CardRarity.Common, Effect(CardEffectType.ReducePressure, 6));
            AddCard(cards, "card_rest_walk_outside", "出去走走", CardType.Rest, 1, CardRarity.Common, Effect(CardEffectType.GainEnergy, 5), Effect(CardEffectType.ReducePressure, 2));
            AddCard(cards, "card_rest_daydream", "发呆片刻", CardType.Rest, 0, CardRarity.Common, Effect(CardEffectType.GainEnergy, 3));

            AddCard(cards, "card_social_club_activity", "社团活动", CardType.Social, 1, CardRarity.Common, Effect(CardEffectType.AddPlayerStat, PlayerStatKey.Social, 4));
            AddCard(cards, "card_social_group_discussion", "小组讨论", CardType.Social, 1, CardRarity.Common, Effect(CardEffectType.AddChallengeProgress, 6), Effect(CardEffectType.AddPlayerStat, PlayerStatKey.Social, 2));
            AddCard(cards, "card_social_help_classmate", "帮助同学", CardType.Social, 1, CardRarity.Common, Effect(CardEffectType.AddPlayerStat, PlayerStatKey.Confidence, 2));
            AddCard(cards, "card_social_borrow_notes", "借阅笔记", CardType.Social, 1, CardRarity.Common, Effect(CardEffectType.DrawCards, 1), Effect(CardEffectType.AddChallengeProgress, 5));

            AddCard(cards, "card_family_talk_parent", "和家长谈谈", CardType.Family, 1, CardRarity.Common, Effect(CardEffectType.AddPlayerStat, PlayerStatKey.ParentRelation, 4));
            AddCard(cards, "card_family_parent_support", "家长支持", CardType.Family, 1, CardRarity.Uncommon, Effect(CardEffectType.ReducePressure, 5), Effect(CardEffectType.AddPlayerStat, PlayerStatKey.ParentRelation, 2));
            AddCard(cards, "card_family_do_housework", "做点家务", CardType.Family, 1, CardRarity.Common, Effect(CardEffectType.AddPlayerStat, PlayerStatKey.Autonomy, 3));
            AddCard(cards, "card_family_dinner_talk", "晚饭聊天", CardType.Family, 1, CardRarity.Common, Effect(CardEffectType.ReducePressure, 3), Effect(CardEffectType.AddPlayerStat, PlayerStatKey.Mood, 2));

            AddCard(cards, "card_item_lucky_pen", "幸运笔", CardType.Trait, 0, CardRarity.Uncommon, Effect(CardEffectType.AddChallengeProgress, 5));
            AddCard(cards, "card_item_warm_tea", "热茶", CardType.Rest, 0, CardRarity.Common, Effect(CardEffectType.GainEnergy, 4));
            AddCard(cards, "card_item_wrong_note", "错题便签", CardType.Study, 0, CardRarity.Common, Effect(CardEffectType.DrawCards, 1));
            AddCard(cards, "card_status_distraction", "走神", CardType.Status, 1, CardRarity.Common, Effect(CardEffectType.LoseEnergy, 2));
            AddCard(cards, "card_status_pressure", "压力", CardType.Status, 1, CardRarity.Common, Effect(CardEffectType.AddPressure, 3));
            AddCard(cards, "card_status_fatigue", "疲惫", CardType.Status, 1, CardRarity.Common, Effect(CardEffectType.LoseEnergy, 3));
            return cards;
        }

        private static Dictionary<string, ItemData> CreateItems()
        {
            Dictionary<string, ItemData> items = new Dictionary<string, ItemData>();
            AddItem(items, "item_energy_drink", "功能饮料", ItemType.Consumable, ItemTriggerTiming.OnUse, true, true, true, Effect(CardEffectType.GainEnergy, 15));
            AddItem(items, "item_lucky_pen", "幸运笔", ItemType.Passive, ItemTriggerTiming.OnBossStart, false, false, false, Effect(CardEffectType.AddChallengeProgress, 10));
            AddItem(items, "item_wrong_book", "错题本", ItemType.Passive, ItemTriggerTiming.OnBattleStart, false, true, false, Effect(CardEffectType.DrawCards, 1));
            AddItem(items, "item_parent_note", "家长便签", ItemType.Consumable, ItemTriggerTiming.OnUse, true, false, true, Effect(CardEffectType.ReducePressure, 8));
            AddItem(items, "item_warm_tea", "热茶", ItemType.Consumable, ItemTriggerTiming.OnUse, true, true, true, Effect(CardEffectType.GainEnergy, 8));
            AddItem(items, "item_alarm_clock", "旧闹钟", ItemType.StageOnly, ItemTriggerTiming.OnStageStart, false, false, false, Effect(CardEffectType.AddPlayerStat, PlayerStatKey.Autonomy, 2));
            AddItem(items, "item_headphones", "耳机", ItemType.Passive, ItemTriggerTiming.OnStageStart, false, false, false, Effect(CardEffectType.ReducePressure, 3));
            AddItem(items, "item_pencil_case", "铅笔盒", ItemType.Passive, ItemTriggerTiming.OnBattleStart, false, true, false, Effect(CardEffectType.AddChallengeProgress, 3));
            return items;
        }

        private static Dictionary<string, MiniGameData> CreateMiniGames(Dictionary<string, CardData> cards, Dictionary<string, ItemData> items)
        {
            Dictionary<string, MiniGameData> miniGames = new Dictionary<string, MiniGameData>();
            AddMiniGame(miniGames, "mini_word_memory", "单词记忆", MiniGameType.Memory, PlayerStatKey.English, 2, Reward(PlayerStatKey.English, 4), CardReward(cards["card_study_after_class_review"]));
            AddMiniGame(miniGames, "mini_formula_match", "公式配对", MiniGameType.SimplePuzzle, PlayerStatKey.Math, 3, Reward(PlayerStatKey.Math, 4), ItemReward(items["item_wrong_book"]));
            AddMiniGame(miniGames, "mini_reading_quiz", "阅读问答", MiniGameType.ChoiceQuiz, PlayerStatKey.Chinese, 3, Reward(PlayerStatKey.Chinese, 4));
            AddMiniGame(miniGames, "mini_timing_focus", "专注计时", MiniGameType.Timing, PlayerStatKey.Autonomy, 3, Reward(PlayerStatKey.Autonomy, 3), CardReward(cards["card_social_borrow_notes"]));
            AddMiniGame(miniGames, "mini_reaction_notes", "速记反应", MiniGameType.Reaction, PlayerStatKey.Confidence, 4, Reward(PlayerStatKey.Confidence, 3));
            AddMiniGame(miniGames, "mini_bag_sort", "书包整理", MiniGameType.SimplePuzzle, PlayerStatKey.Energy, 2, Reward(PlayerStatKey.Energy, 6), ItemReward(items["item_pencil_case"]));
            return miniGames;
        }

        private static Dictionary<string, GameEventData> CreateEvents(Dictionary<string, CardData> cards, Dictionary<string, ItemData> items)
        {
            Dictionary<string, GameEventData> events = new Dictionary<string, GameEventData>();
            AddEvent(events, "event_parent_tutor", "补课讨论", "接受建议", Reward(PlayerStatKey.ParentRelation, 2), Reward(PlayerStatKey.Pressure, 2));
            AddEvent(events, "event_club_invite", "社团邀请", "参加活动", Reward(PlayerStatKey.Social, 4), Reward(PlayerStatKey.Interest, 2), CardReward(cards["card_social_club_activity"]));
            AddEvent(events, "event_wrong_book_advice", "错题本建议", "认真记录", Reward(PlayerStatKey.Math, 3), Reward(PlayerStatKey.Confidence, 1), CardReward(cards["card_study_wrong_book"]));
            AddEvent(events, "event_friend_study", "同学约自习", "一起复习", Reward(PlayerStatKey.Social, 2), Reward(PlayerStatKey.English, 2));
            AddEvent(events, "event_family_dinner", "家庭晚饭", "聊聊近况", Reward(PlayerStatKey.ParentRelation, 3), Reward(PlayerStatKey.Mood, 2), ItemReward(items["item_warm_tea"]));
            AddEvent(events, "event_club_showcase", "社团展示", "上台帮忙", Reward(PlayerStatKey.Confidence, 3), Reward(PlayerStatKey.Interest, 2));
            AddEvent(events, "event_quiz_notice", "小测通知", "提前准备", Reward(PlayerStatKey.Confidence, 2), Reward(PlayerStatKey.Pressure, 1), CardReward(cards["card_study_mock_exam"]));
            AddEvent(events, "event_weekend_plan", "周末安排", "自主规划", Reward(PlayerStatKey.Autonomy, 4), Reward(PlayerStatKey.Energy, 4));
            return events;
        }

        private static Dictionary<string, ChallengeData> CreateChallenges()
        {
            Dictionary<string, ChallengeData> challenges = new Dictionary<string, ChallengeData>();
            AddChallenge(challenges, "challenge_class_practice", "课堂练习", false, "stage_01_adaptation", 30, 3);
            AddChallenge(challenges, "challenge_group_quiz", "小组问答", false, "stage_02_month_exam", 55, 4);
            AddChallenge(challenges, "challenge_mock_drill", "模拟演练", false, "stage_03_final_sprint", 80, 5);
            AddChallenge(challenges, "boss_class_quiz", "课堂小测", true, "stage_01_adaptation", 45, 3);
            AddChallenge(challenges, "boss_month_exam", "月考", true, "stage_02_month_exam", 90, 5);
            AddChallenge(challenges, "boss_final_exam", "期末综合考试", true, "stage_03_final_sprint", 150, 7);
            return challenges;
        }

        private static Dictionary<string, ActivityData> CreateActivities(Dictionary<string, MiniGameData> miniGames, Dictionary<string, GameEventData> events)
        {
            Dictionary<string, ActivityData> activities = new Dictionary<string, ActivityData>();
            AddActivity(activities, "activity_focus_class", "认真听课", ActivityType.Training, 1, new[] { Reward(PlayerStatKey.Chinese, 3) });
            AddActivity(activities, "activity_math_drill", "数学刷题", ActivityType.Training, 1, new[] { Reward(PlayerStatKey.Math, 4) });
            AddActivity(activities, "activity_final_review", "综合复习", ActivityType.Training, 1, new[] { Reward(PlayerStatKey.Confidence, 4) });
            AddActivity(activities, "activity_rest_early_sleep", "早点休息", ActivityType.Rest, 1, new[] { Reward(PlayerStatKey.Energy, 8), ReducePressure(4) });
            AddActivity(activities, "activity_music_break", "听音乐放松", ActivityType.Rest, 1, new[] { Reward(PlayerStatKey.Energy, 4), ReducePressure(5) });
            AddActivity(activities, "activity_word_memory", "单词记忆", ActivityType.MiniGame, 1, null, null, miniGames["mini_word_memory"]);
            AddActivity(activities, "activity_formula_match", "公式配对", ActivityType.MiniGame, 1, null, null, miniGames["mini_formula_match"]);
            AddActivity(activities, "activity_focus_timing", "专注计时", ActivityType.MiniGame, 1, null, null, miniGames["mini_timing_focus"]);
            AddActivity(activities, "activity_family_dinner", "家庭晚饭聊天", ActivityType.Event, 1, null, events["event_family_dinner"]);
            AddActivity(activities, "activity_club_invite", "社团邀请", ActivityType.Event, 1, null, events["event_club_invite"]);
            AddActivity(activities, "activity_wrong_book_advice", "错题本建议", ActivityType.Event, 1, null, events["event_wrong_book_advice"]);
            AddActivity(activities, "activity_weekend_plan", "周末安排", ActivityType.Event, 1, null, events["event_weekend_plan"]);
            return activities;
        }

        private static List<StageData> CreateStages(Dictionary<string, ChallengeData> challenges, Dictionary<string, ActivityData> activities)
        {
            StageData stageOne = AddStage("stage_01_adaptation", "适应新学期", 6, false, challenges["boss_class_quiz"]);
            StageData stageTwo = AddStage("stage_02_month_exam", "月考压力", 6, false, challenges["boss_month_exam"]);
            StageData stageThree = AddStage("stage_03_final_sprint", "期末冲刺", 8, true, challenges["boss_final_exam"]);

            stageOne.nextStageId = stageTwo.stageId;
            stageTwo.nextStageId = stageThree.stageId;
            stageOne.availableActivities = new List<ActivityData> { activities["activity_focus_class"], activities["activity_rest_early_sleep"], activities["activity_family_dinner"], activities["activity_club_invite"] };
            stageTwo.availableActivities = new List<ActivityData> { activities["activity_math_drill"], activities["activity_word_memory"], activities["activity_music_break"], activities["activity_wrong_book_advice"] };
            stageThree.availableActivities = new List<ActivityData> { activities["activity_final_review"], activities["activity_formula_match"], activities["activity_focus_timing"], activities["activity_weekend_plan"] };
            MarkDirty(stageOne, stageTwo, stageThree);
            return new List<StageData> { stageOne, stageTwo, stageThree };
        }

        private static List<EndingData> CreateEndings()
        {
            return new List<EndingData>
            {
                AddEnding("ending_pressure_recovery", "压力调整", "高压没有让你停下，但它提醒你需要重新安排节奏。", 60, 0, 0, 0, 100, Condition(PlayerStatKey.Pressure, ComparisonType.GreaterOrEqual, 85)),
                AddEnding("ending_stable_admission", "稳定升学", "你完成所有阶段，并用稳定成绩通过了最终考验。", 50, 3, 3, 55, 100),
                AddEnding("ending_interest_growth", "兴趣成长", "你在学习之外保留了探索兴趣的空间。", 40, 2, 0, 0, 100, Condition(PlayerStatKey.Interest, ComparisonType.GreaterOrEqual, 70)),
                AddEnding("ending_family_support", "家庭支持", "沟通让家庭成为了你的支持资源。", 40, 0, 0, 0, 70, Condition(PlayerStatKey.ParentRelation, ComparisonType.GreaterOrEqual, 70)),
                AddEnding("ending_self_driven", "自主选择", "你逐渐建立了自己的节奏，也更愿意承担选择的结果。", 40, 0, 0, 0, 100, Condition(PlayerStatKey.Autonomy, ComparisonType.GreaterOrEqual, 70), Condition(PlayerStatKey.Confidence, ComparisonType.GreaterOrEqual, 55)),
                AddEnding("ending_growth_complete", "普通但完整的成长", "这是一段不完美但完整的成长。", 0, 0, 0, 0, 100)
            };
        }

        private static void AddCard(Dictionary<string, CardData> cards, string id, string name, CardType type, int cost, CardRarity rarity, params CardEffectData[] effects)
        {
            CardData card = GetOrCreateAsset<CardData>(CardsRoot + "/" + id + ".asset");
            card.cardId = id;
            card.displayName = name;
            card.cardType = type;
            card.cost = cost;
            card.rarity = rarity;
            card.targetType = GetDefaultTargetType(type, effects);
            card.damage = GetEffectValue(effects, CardEffectType.AddChallengeProgress);
            card.block = type == CardType.Rest ? 5 : 0;
            card.drawCount = GetEffectValue(effects, CardEffectType.DrawCards);
            card.effects = new List<CardEffectData>(effects);
            GameArtBindingUtility.ApplyKnownArtPath(card);
            card.cardSprite = LoadSprite(card.imagePath);
            card.icon = card.cardSprite;
            cards[id] = card;
            EditorUtility.SetDirty(card);
        }

        private static CardTargetType GetDefaultTargetType(CardType type, IEnumerable<CardEffectData> effects)
        {
            if (GetEffectValue(effects, CardEffectType.AddChallengeProgress) > 0 || GetEffectValue(effects, CardEffectType.AddSubjectProgress) > 0)
            {
                return CardTargetType.SingleEnemy;
            }

            if (type == CardType.Rest
                || GetEffectValue(effects, CardEffectType.GainEnergy) > 0
                || GetEffectValue(effects, CardEffectType.ReducePressure) > 0)
            {
                return CardTargetType.Self;
            }

            return CardTargetType.None;
        }

        private static int GetEffectValue(IEnumerable<CardEffectData> effects, CardEffectType effectType)
        {
            int value = 0;
            if (effects == null)
            {
                return value;
            }

            foreach (CardEffectData effect in effects)
            {
                if (effect != null && effect.effectType == effectType)
                {
                    value += effect.value;
                }
            }

            return value;
        }

        private static void AddItem(Dictionary<string, ItemData> items, string id, string name, ItemType type, ItemTriggerTiming timing, bool consumedOnUse, bool canUseInBattle, bool canUseInStageScreen, params CardEffectData[] effects)
        {
            ItemData item = GetOrCreateAsset<ItemData>(ItemsRoot + "/" + id + ".asset");
            item.itemId = id;
            item.displayName = name;
            item.itemType = type;
            item.triggerTiming = timing;
            item.consumedOnUse = consumedOnUse;
            item.canUseInBattle = canUseInBattle;
            item.canUseInStageScreen = canUseInStageScreen;
            item.effects = new List<CardEffectData>(effects);
            GameArtBindingUtility.ApplyKnownArtPath(item);
            item.icon = LoadSprite(item.imagePath);
            items[id] = item;
            EditorUtility.SetDirty(item);
        }

        private static void AddMiniGame(Dictionary<string, MiniGameData> miniGames, string id, string name, MiniGameType type, PlayerStatKey stat, int difficulty, params RewardData[] successRewards)
        {
            MiniGameData miniGame = GetOrCreateAsset<MiniGameData>(MiniGamesRoot + "/" + id + ".asset");
            miniGame.miniGameId = id;
            miniGame.displayName = name;
            miniGame.miniGameType = type;
            miniGame.primaryStat = stat;
            miniGame.difficulty = difficulty;
            miniGame.successRewards = new List<RewardData>(successRewards);
            miniGame.failureRewards = new List<RewardData> { AddPressure(1) };
            GameArtBindingUtility.ApplyKnownArtPath(miniGame);
            miniGame.backgroundSprite = LoadSprite(miniGame.imagePath);
            miniGame.icon = miniGame.backgroundSprite;
            miniGames[id] = miniGame;
            EditorUtility.SetDirty(miniGame);
        }

        private static void AddEvent(Dictionary<string, GameEventData> events, string id, string title, string choiceText, params RewardData[] rewards)
        {
            GameEventData eventData = GetOrCreateAsset<GameEventData>(EventsRoot + "/" + id + ".asset");
            eventData.eventId = id;
            eventData.title = title;
            eventData.choices = new List<EventChoiceData>
            {
                new EventChoiceData
                {
                    choiceText = choiceText,
                    rewards = new List<RewardData>(rewards)
                }
            };
            GameArtBindingUtility.ApplyKnownArtPath(eventData);
            eventData.backgroundSprite = LoadSprite(eventData.imagePath);
            eventData.icon = eventData.backgroundSprite;
            events[id] = eventData;
            EditorUtility.SetDirty(eventData);
        }

        private static void AddChallenge(Dictionary<string, ChallengeData> challenges, string id, string name, bool isBoss, string stageId, int targetProgress, int maxTurns)
        {
            ChallengeData challenge = GetOrCreateAsset<ChallengeData>(ChallengesRoot + "/" + id + ".asset");
            challenge.challengeId = id;
            challenge.displayName = name;
            challenge.isBoss = isBoss;
            challenge.requiredStageId = stageId;
            challenge.targetProgress = targetProgress;
            challenge.maxTurns = maxTurns;
            challenge.startingEnergy = 3;
            challenge.startingHandSize = 5;
            challenge.successRewards = new List<RewardData> { Reward(PlayerStatKey.Confidence, 2) };
            challenge.failureRewards = new List<RewardData> { AddPressure(5) };
            challenge.bossClearRewards = isBoss ? new List<RewardData> { Reward(PlayerStatKey.Confidence, 3) } : new List<RewardData>();
            challenge.bossFailureRewards = isBoss ? new List<RewardData> { AddPressure(6) } : new List<RewardData>();
            GameArtBindingUtility.ApplyKnownArtPath(challenge);
            challenge.backgroundSprite = LoadSprite(challenge.imagePath);
            challenge.icon = LoadChallengeIcon(id, challenge.backgroundSprite);
            challenges[id] = challenge;
            EditorUtility.SetDirty(challenge);
        }

        private static void AddActivity(Dictionary<string, ActivityData> activities, string id, string name, ActivityType type, int timeCost, IEnumerable<RewardData> fixedRewards, GameEventData linkedEvent = null, MiniGameData linkedMiniGame = null)
        {
            ActivityData activity = GetOrCreateAsset<ActivityData>(ActivitiesRoot + "/" + id + ".asset");
            activity.activityId = id;
            activity.displayName = name;
            activity.activityType = type;
            activity.timeCost = timeCost;
            activity.fixedRewards = fixedRewards == null ? new List<RewardData>() : new List<RewardData>(fixedRewards);
            activity.linkedEvent = linkedEvent;
            activity.linkedMiniGame = linkedMiniGame;
            GameArtBindingUtility.ApplyKnownArtPath(activity);
            activity.backgroundSprite = LoadSprite(activity.imagePath);
            activity.icon = activity.backgroundSprite;
            activities[id] = activity;
            EditorUtility.SetDirty(activity);
        }

        private static StageData AddStage(string id, string name, int timeLimit, bool isFinalStage, ChallengeData boss)
        {
            StageData stage = GetOrCreateAsset<StageData>(StagesRoot + "/" + id + ".asset");
            stage.stageId = id;
            stage.displayName = name;
            stage.timeLimit = timeLimit;
            stage.isFinalStage = isFinalStage;
            stage.bossChallenge = boss;
            GameArtBindingUtility.ApplyKnownArtPath(stage);
            stage.backgroundSprite = LoadSprite(stage.imagePath);
            stage.icon = stage.backgroundSprite;
            EditorUtility.SetDirty(stage);
            return stage;
        }

        private static EndingData AddEnding(
            string id,
            string name,
            string description,
            int priority,
            int minimumClearedStages,
            int minimumDefeatedBosses,
            int minimumSubjectAverage,
            int maximumPressure,
            params EventConditionData[] conditions)
        {
            EndingData ending = GetOrCreateAsset<EndingData>(EndingsRoot + "/" + id + ".asset");
            ending.endingId = id;
            ending.displayName = name;
            ending.description = description;
            ending.priority = priority;
            ending.minimumClearedStages = minimumClearedStages;
            ending.minimumDefeatedBosses = minimumDefeatedBosses;
            ending.minimumSubjectAverage = minimumSubjectAverage;
            ending.maximumPressure = maximumPressure;
            ending.requireEndingReached = true;
            ending.conditions = new List<EventConditionData>(conditions);
            ending.imagePath = GameArtAssetCatalog.BackgroundPaths["stage_dashboard"];
            ending.illustrationSprite = LoadSprite(ending.imagePath);
            EditorUtility.SetDirty(ending);
            return ending;
        }

        private static CardEffectData Effect(CardEffectType type, int value)
        {
            return new CardEffectData { effectType = type, value = value };
        }

        private static CardEffectData Effect(CardEffectType type, PlayerStatKey stat, int value)
        {
            return new CardEffectData { effectType = type, targetStat = stat, value = value };
        }

        private static RewardData Reward(PlayerStatKey stat, int value)
        {
            return new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = stat,
                value = value
            };
        }

        private static RewardData ReducePressure(int value)
        {
            return new RewardData
            {
                rewardType = RewardType.ReducePressure,
                value = value
            };
        }

        private static RewardData AddPressure(int value)
        {
            return new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.Pressure,
                value = value
            };
        }

        private static RewardData CardReward(CardData card)
        {
            return new RewardData
            {
                rewardType = RewardType.AddCard,
                card = card
            };
        }

        private static RewardData ItemReward(ItemData item)
        {
            return new RewardData
            {
                rewardType = RewardType.AddItem,
                item = item
            };
        }

        private static EventConditionData Condition(PlayerStatKey statKey, ComparisonType comparison, int value)
        {
            return new EventConditionData
            {
                statKey = statKey,
                comparison = comparison,
                value = value
            };
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static Sprite LoadSprite(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Sprite LoadChallengeIcon(string challengeId, Sprite fallback)
        {
            return GameArtAssetCatalog.TryGetChallengeIconPath(challengeId, out string iconPath)
                ? LoadSprite(iconPath)
                : fallback;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Resources");
            EnsureFolder(ResourceRoot, "StudentGrowth");
            EnsureFolder(StudentGrowthRoot, "MVP");
            EnsureFolder(MvpRoot, "Cards");
            EnsureFolder(MvpRoot, "Items");
            EnsureFolder(MvpRoot, "MiniGames");
            EnsureFolder(MvpRoot, "Events");
            EnsureFolder(MvpRoot, "Challenges");
            EnsureFolder(MvpRoot, "Activities");
            EnsureFolder(MvpRoot, "Stages");
            EnsureFolder(MvpRoot, "Endings");
        }

        private static void EnsureFolder(string parent, string folder)
        {
            string path = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }

        private static void MarkDirty(params Object[] assets)
        {
            foreach (Object asset in assets)
            {
                EditorUtility.SetDirty(asset);
            }
        }
    }
}
