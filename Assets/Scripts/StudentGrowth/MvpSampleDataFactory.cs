using System.Collections.Generic;
using UnityEngine;

namespace StudentGrowth
{
    public static class MvpSampleDataFactory
    {
        /// <summary>
        /// 「本阶段不限次数」的 maxUsesPerStage 取值。
        /// 约定见 DataDefinitions.cs 的注释：负数 = 不限次数。
        /// </summary>
        private const int UnlimitedUses = -1;

        /// <summary>商店货架最多摆几张卡。与 MapShopView 的可见行数对齐，避免加了看不到。</summary>
        private const int ShopCardShelfSize = 4;

        public static GameSessionState CreateSessionWithStarterDeck()
        {
            GameSessionState sessionState = new GameSessionState();
            sessionState.deckState.masterDeck.Add(CreateProgressCard("card_study_class_focus", "课堂专注", 6));
            sessionState.deckState.masterDeck.Add(CreateProgressCard("card_study_math_practice", "数学练习", 8));
            sessionState.deckState.masterDeck.Add(CreateProgressCard("card_study_mock_exam", "模拟考试", 18, 2));
            sessionState.deckState.masterDeck.Add(CreateProgressCard("card_study_wrong_book", "整理错题", 10));
            sessionState.deckState.masterDeck.Add(CreateProgressCard("card_study_english_words", "背单词", 7));
            sessionState.deckState.masterDeck.Add(CreateRestCard("card_rest_good_sleep", "好好睡觉"));
            return sessionState;
        }

        public static void AddDefaultStarterItems(GameSessionState state)
        {
            if (state == null)
            {
                return;
            }

            state.itemInventory.AddItem(CreateEnergyDrink());
            state.itemInventory.AddItem(CreateLuckyPen());
        }

        public static List<ItemData> CreateDefaultShopItems()
        {
            return new List<ItemData>
            {
                CreateWarmTea(),
                CreateEnergyDrink(),
                CreateWrongBook(),
                CreateLuckyPen(),
                // 2026-10-26 补齐的 5 件：此前只有图标没有数据
                CreateAlarmClock(),
                CreateHeadphones(),
                CreateParentNote(),
                CreatePencilCase(),
                CreateStickerStar()
            };
        }

        /// <summary>
        /// 商店货架上的卡牌。优先取 Resources 下真实卡池（pool_starter / pool_reward）里的卡，
        /// 卡池缺失时才退回本文件自建的兜底卡，保证商店在任何数据状态下都有卡可卖。
        /// </summary>
        public static List<CardData> CreateDefaultShopCards()
        {
            List<CardData> result = new List<CardData>();
            HashSet<string> seen = new HashSet<string>();

            CardPoolData[] pools = CardPoolCatalog.All();
            for (int p = 0; p < pools.Length; p++)
            {
                CardPoolData pool = pools[p];
                if (pool == null || pool.cards == null)
                {
                    continue;
                }

                for (int i = 0; i < pool.cards.Count; i++)
                {
                    CardData card = pool.cards[i];
                    if (card == null)
                    {
                        continue;
                    }

                    string key = string.IsNullOrEmpty(card.cardId) ? card.name : card.cardId;
                    if (!seen.Add(key))
                    {
                        continue;
                    }

                    result.Add(card);
                }
            }

            if (result.Count == 0)
            {
                CardData review = CreateProgressCard("card_shop_wrong_review", "错题重做", 12);
                review.rarity = CardRarity.Uncommon;
                result.Add(review);

                CardData breath = CreateRestCard("card_shop_deep_breath", "深呼吸");
                breath.rarity = CardRarity.Common;
                result.Add(breath);
            }

            if (result.Count > ShopCardShelfSize)
            {
                result.RemoveRange(ShopCardShelfSize, result.Count - ShopCardShelfSize);
            }

            return result;
        }

        public static List<StageData> CreateThreeStageSequence()
        {
            StageData stageOne = CreateStage("stage_01_adaptation", "适应新学期", 6, false);
            StageData stageTwo = CreateStage("stage_02_month_exam", "月考压力", 6, false);
            StageData stageThree = CreateStage("stage_03_final_sprint", "期末冲刺", 8, true);

            stageOne.nextStageId = stageTwo.stageId;
            stageTwo.nextStageId = stageThree.stageId;

            stageOne.bossChallenge = CreateBoss("boss_class_quiz", "课堂小测", stageOne.stageId, 45, 3);
            stageTwo.bossChallenge = CreateBoss("boss_month_exam", "月考", stageTwo.stageId, 90, 5);
            stageThree.bossChallenge = CreateBoss("boss_final_exam", "期末综合考试", stageThree.stageId, 150, 7);

            stageOne.availableActivities.Add(CreateTrainingActivity("activity_focus_class", "认真听课", PlayerStatKey.Chinese, 3));
            stageOne.availableActivities.Add(CreateRestActivity("activity_rest_early_sleep", "早点休息"));
            stageOne.availableActivities.Add(CreateEventActivity("activity_family_dinner", "家庭晚饭聊天", CreateItemReward(CreateWarmTea())));

            stageTwo.availableActivities.Add(CreateTrainingActivity("activity_math_drill", "数学刷题", PlayerStatKey.Math, 4));
            stageTwo.availableActivities.Add(CreateMiniGameActivity("activity_word_memory", "单词记忆", PlayerStatKey.English, 2, CreateCardReward(CreateProgressCard("card_study_after_class_review", "课后复盘", 9))));
            stageTwo.availableActivities.Add(CreateRestActivity("activity_music_break", "听音乐放松"));

            stageThree.availableActivities.Add(CreateTrainingActivity("activity_final_review", "综合复习", PlayerStatKey.Confidence, 4));
            stageThree.availableActivities.Add(CreateMiniGameActivity("activity_focus_timing", "专注计时", PlayerStatKey.Autonomy, 3, CreateItemReward(CreateWrongBook())));
            stageThree.availableActivities.Add(CreateEventActivity("activity_weekend_plan", "周末安排"));

            return new List<StageData> { stageOne, stageTwo, stageThree };
        }

        /// <summary>
        /// 结局阈值按「实机可达上界」标定，不按设计理想值。
        ///
        /// 2026-10-22 修：旧阈值（压力 85 / 三科均值 55 / 兴趣 70 / 亲子 70 / 自主 70）
        /// 全部高于属性上界，导致 5/6 结局永远不可达。新阈值取实测上界的约 80%：
        ///   压力   上界 38 → 30
        ///   三科均值 上界 40 → 32
        ///   兴趣   上界 53 → 42
        ///   亲子关系 上界 67 → 53
        ///   自主性  上界 43 → 34（配套的自信门槛 55 → 44，同比例下调）
        /// 数据来源：Docs/ContentHealthReport.md（2026-10-22 14:11 重跑）。
        /// 取 80% 而非 100%：留出「顾此失彼」的空间，让结局选择仍然有意义。
        /// </summary>
        public static List<EndingData> CreateDefaultEndings()
        {
            return new List<EndingData>
            {
                CreateEnding(
                    "ending_pressure_recovery",
                    "压力调整",
                    "你经历了高压的一轮成长，最终学会先照顾状态，再继续前进。",
                    60,
                    0,
                    0,
                    0,
                    100,
                    Condition(PlayerStatKey.Pressure, ComparisonType.GreaterOrEqual, 30)),
                CreateEnding(
                    "ending_stable_admission",
                    "稳定升学",
                    "你完成了所有阶段，并用稳定的学科能力通过了最终考验。",
                    50,
                    3,
                    3,
                    32,
                    100),
                CreateEnding(
                    "ending_interest_growth",
                    "兴趣成长",
                    "成绩之外，你也保留了探索兴趣的空间。",
                    40,
                    2,
                    0,
                    0,
                    100,
                    Condition(PlayerStatKey.Interest, ComparisonType.GreaterOrEqual, 42)),
                CreateEnding(
                    "ending_family_support",
                    "家庭支持",
                    "沟通让家庭成为了你的支持资源，而不是额外压力。",
                    40,
                    0,
                    0,
                    0,
                    70,
                    Condition(PlayerStatKey.ParentRelation, ComparisonType.GreaterOrEqual, 53)),
                CreateEnding(
                    "ending_self_driven",
                    "自主选择",
                    "你逐渐建立了自己的节奏，也更愿意承担选择的结果。",
                    40,
                    0,
                    0,
                    0,
                    100,
                    Condition(PlayerStatKey.Autonomy, ComparisonType.GreaterOrEqual, 34),
                    Condition(PlayerStatKey.Confidence, ComparisonType.GreaterOrEqual, 44)),
                CreateEnding(
                    "ending_growth_complete",
                    "普通但完整的成长",
                    "这是一段不完美但完整的成长。你看见了压力，也积累了新的方法。",
                    0,
                    0,
                    0,
                    0,
                    100)
            };
        }

        public static List<LocationData> CreateDefaultLocations()
        {
            LocationData home = CreateLocation(
                "location_home",
                "家里",
                "恢复精力、沟通家庭、整理计划。",
                LocationType.Home,
                true,
                GameArtAssetCatalog.BackgroundPaths["student_bedroom"]);

            LocationData school = CreateLocation(
                "location_school",
                "学校",
                "学习、请教老师、参加课堂挑战。",
                LocationType.School,
                true,
                GameArtAssetCatalog.BackgroundPaths["school_classroom"]);

            LocationData shop = CreateLocation(
                "location_shop",
                "商店",
                "进入商店地图，购买道具和补充卡组资源。",
                LocationType.Shop,
                true,
                GameArtAssetCatalog.BackgroundPaths["school_shop"]);

            LocationData park = CreateLocation(
                "location_park",
                "公园",
                "降低压力、恢复心情、触发社交或兴趣事件。",
                LocationType.Park,
                true,
                GameArtAssetCatalog.BackgroundPaths["stage_dashboard"]);

            LocationData library = CreateLocation(
                "location_library",
                "图书馆",
                "精读笔记、升级卡牌和整理卡组。",
                LocationType.Library,
                true,
                GameArtAssetCatalog.BackgroundPaths["study_desk"],
                Condition(PlayerStatKey.Confidence, ComparisonType.GreaterOrEqual, 45));

            // 社团活动室：第三学段开放的自由地点。
            //
            // ⚠ 2026-09-23 变更（原设计见下）：这里原本是 `unlockedByDefault: false` +
            //   `Condition(Interest >= 45)`，同时阶段地图上的门槛也是"兴趣或社交 >= 45"。
            //   实测发现这道门是**自锁**的：兴趣与社交的增量来源只有 3 处，其中 2 处
            //   （策划社团活动 +5 兴趣、社团讨论 +5 社交）就在社团活动室里面，
            //   而社团外仅"公园赏花"能提供 +2 兴趣，社交在社团外完全零来源。
            //   于是：进不去 → 涨不了属性 → 达不到 45 → 更进不去。社团连同里面 3 个活动、
            //   6 个非出口交互点位永远不可达（内容体检工具 B1 的"循环依赖"检查项已能自动报出）。
            //   改为按学段开放：门槛是"第三学段"而非属性值。兴趣/社交本来也不该当门——
            //   它们更适合作为事件与结局的分叉条件（见 GDD "交友是钥匙不是分数"）。
            //   原始门槛值 45 保留在此处备查，若要回退请先补足够的外部来源。
            LocationData club = CreateLocation(
                "location_club",
                "社团活动室",
                "发展兴趣、社交和自信。",
                LocationType.Club,
                true,
                GameArtAssetCatalog.BackgroundPaths["school_classroom"]);

            return new List<LocationData> { home, school, shop, park, library, club };
        }

        public static StageMapData CreateDefaultStageMapData(StageData stageData)
        {
            string stageId = stageData == null ? "stage_01_adaptation" : stageData.stageId;
            StageMapData mapData = ScriptableObject.CreateInstance<StageMapData>();
            mapData.stageId = stageId;

            switch (stageId)
            {
                case "stage_02_month_exam":
                    mapData.title = "月考压力地图";
                    mapData.description = "利用图书馆和公园管理复习效率与压力。进入地点不消耗时间，完成互动才会消耗时间。";
                    AddStageMapLocation(mapData, "location_home", "家里", "恢复精力、沟通家庭", "HomeLivingRoomScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 0);
                    AddStageMapLocation(mapData, "location_school", "学校", "月考训练、老师答疑", "Assets/Scenes/StudentGrowth/Maps/SchoolMapScene.unity", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 10);
                    AddStageMapLocation(mapData, "location_shop", "商店", "进入商店地图", "ShopMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 20);
                    AddStageMapLocation(mapData, "location_park", "公园", "降低压力、社交事件", "ParkMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 30);
                    AddStageMapLocation(mapData, "location_library", "图书馆", "精读笔记、整理卡组", "LibraryMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 40);
                    AddStageMapLocation(mapData, "location_club", "社团活动室", "第三学段起开放", "ClubRoomMapScene", StageMapLocationStatus.Locked, StageMapLocationAvailabilityService.Stage3OrLaterCondition, "第三学段起开放。", string.Empty, 50);
                    break;
                case "stage_03_final_sprint":
                    mapData.title = "期末冲刺地图";
                    mapData.description = "期末前的时间有限。选择地点不会消耗时间，完成互动后才会消耗时间。";
                    AddStageMapLocation(mapData, "location_home", "家里", "休息、家庭支持", "HomeLivingRoomScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 0);
                    AddStageMapLocation(mapData, "location_school", "学校", "最终复习、期末挑战", "Assets/Scenes/StudentGrowth/Maps/SchoolMapScene.unity", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 10);
                    AddStageMapLocation(mapData, "location_shop", "商店", "进入商店地图", "ShopMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 20);
                    AddStageMapLocation(mapData, "location_park", "公园", "压力调整、社交收尾", "ParkMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 30);
                    AddStageMapLocation(mapData, "location_library", "图书馆", "高强度复习、整理卡组", "LibraryMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 40);
                    AddStageMapLocation(mapData, "location_club", "社团活动室", "兴趣与社交的收尾地点", "ClubRoomMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 50);
                    break;
                default:
                    mapData.title = "阶段地图";
                    mapData.description = "选择地点进入地图。移动和进入地点不消耗时间，只有完成互动才会消耗时间。";
                    AddStageMapLocation(mapData, "location_home", "家里", "恢复精力、沟通家庭", "HomeLivingRoomScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 0);
                    AddStageMapLocation(mapData, "location_school", "学校", "学习、老师、课堂挑战", "Assets/Scenes/StudentGrowth/Maps/SchoolMapScene.unity", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 10);
                    AddStageMapLocation(mapData, "location_shop", "商店", "进入商店地图", "ShopMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 20);
                    AddStageMapLocation(mapData, "location_park", "公园", "降低压力、社交事件", "ParkMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 30);
                    AddStageMapLocation(mapData, "location_library", "图书馆", "精读笔记、整理卡组", "LibraryMapScene", StageMapLocationStatus.Unlocked, string.Empty, string.Empty, string.Empty, 40);
                    AddStageMapLocation(mapData, "location_club", "社团活动室", "第三学段起开放", "ClubRoomMapScene", StageMapLocationStatus.Locked, StageMapLocationAvailabilityService.Stage3OrLaterCondition, "第三学段起开放。", string.Empty, 50);
                    break;
            }

            return mapData;
        }

        private static void AddStageMapLocation(
            StageMapData mapData,
            string locationId,
            string displayName,
            string description,
            string sceneName,
            StageMapLocationStatus status,
            string unlockConditionKey,
            string lockedReason,
            string comingSoonText,
            int sortOrder)
        {
            mapData.locations.Add(new StageMapLocationEntry
            {
                locationId = locationId,
                displayName = displayName,
                description = description,
                sceneName = sceneName,
                defaultStatus = status,
                unlockConditionKey = unlockConditionKey,
                lockedReason = lockedReason,
                comingSoonText = comingSoonText,
                sortOrder = sortOrder
            });
        }

        private static StageData CreateStage(string stageId, string displayName, int timeLimit, bool isFinalStage)
        {
            StageData stageData = ScriptableObject.CreateInstance<StageData>();
            stageData.stageId = stageId;
            stageData.displayName = displayName;
            stageData.timeLimit = timeLimit;
            stageData.isFinalStage = isFinalStage;
            GameArtBindingUtility.ApplyKnownArtPath(stageData);
            return stageData;
        }

        private static ChallengeData CreateBoss(string challengeId, string displayName, string stageId, int targetProgress, int maxTurns)
        {
            ChallengeData challengeData = ScriptableObject.CreateInstance<ChallengeData>();
            challengeData.challengeId = challengeId;
            challengeData.displayName = displayName;
            challengeData.requiredStageId = stageId;
            challengeData.isBoss = true;
            challengeData.targetProgress = targetProgress;
            challengeData.maxTurns = maxTurns;
            challengeData.startingEnergy = 3;
            challengeData.startingHandSize = 5;
            challengeData.bossClearRewards.Add(CreateStatReward(PlayerStatKey.Confidence, 3));
            // 金钱此前零来源（RewardType.AddMoney 只被读取、从不产出），导致商店没有可花的钱。
            // Boss 通关是最自然的收入口：打过一个阶段就有钱，且和「阶段奖励」的节奏一致。
            challengeData.bossClearRewards.Add(new RewardData
            {
                rewardType = RewardType.AddMoney,
                value = 50
            });
            challengeData.bossFailureRewards.Add(CreateStatReward(PlayerStatKey.Pressure, 5));
            GameArtBindingUtility.ApplyKnownArtPath(challengeData);
            return challengeData;
        }

        private static ActivityData CreateTrainingActivity(string activityId, string displayName, PlayerStatKey statKey, int value)
        {
            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.activityId = activityId;
            activityData.displayName = displayName;
            activityData.activityType = ActivityType.Training;
            activityData.timeCost = 1;
            // 训练类不限次数（-1）。默认值 1 会造成阶段死锁：
            // 每阶段只有 3 个活动 × 上限 1 = 最多消耗 3 时间，而 timeLimit 是 6/6/8，
            // remainingTime 永远到不了 0，bossUnlocked 恒 false（见 Docs/DesignAudit-2026-10-22.md）。
            activityData.maxUsesPerStage = UnlimitedUses;
            activityData.fixedRewards.Add(CreateStatReward(statKey, value));
            GameArtBindingUtility.ApplyKnownArtPath(activityData);
            return activityData;
        }

        private static ActivityData CreateRestActivity(string activityId, string displayName)
        {
            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.activityId = activityId;
            activityData.displayName = displayName;
            activityData.activityType = ActivityType.Rest;
            activityData.timeCost = 1;
            // 休息类必须不限次数：否则时间槽耗尽后无法回复精力，玩家会被困在低精力状态。
            activityData.maxUsesPerStage = UnlimitedUses;
            activityData.fixedRewards.Add(CreateStatReward(PlayerStatKey.Energy, 8));
            activityData.fixedRewards.Add(new RewardData
            {
                rewardType = RewardType.ReducePressure,
                value = 4
            });
            // 精力上限此前零来源（RewardType.IncreaseMaxEnergy 从不产出），
            // 精力永远卡在 60，与「成长模拟」的定位矛盾。
            // 挂在休息类上：睡觉/休息是唯一在叙事上说得通的「上限提升」来源，
            // 且休息不限次数，所以上限会随休息次数缓慢增长（+2/次）。
            activityData.fixedRewards.Add(new RewardData
            {
                rewardType = RewardType.IncreaseMaxEnergy,
                value = 2
            });
            GameArtBindingUtility.ApplyKnownArtPath(activityData);
            return activityData;
        }

        private static ActivityData CreateMiniGameActivity(string activityId, string displayName, PlayerStatKey statKey, int difficulty, params RewardData[] extraSuccessRewards)
        {
            MiniGameData miniGameData = ScriptableObject.CreateInstance<MiniGameData>();
            miniGameData.miniGameId = activityId + "_mini";
            miniGameData.displayName = displayName;
            miniGameData.miniGameType = MiniGameType.Memory;
            miniGameData.primaryStat = statKey;
            miniGameData.difficulty = difficulty;
            miniGameData.successRewards.Add(CreateStatReward(statKey, 4));
            AddRewards(miniGameData.successRewards, extraSuccessRewards);
            GameArtBindingUtility.ApplyKnownArtPath(miniGameData);

            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.activityId = activityId;
            activityData.displayName = displayName;
            activityData.activityType = ActivityType.MiniGame;
            activityData.timeCost = 1;
            // 同训练类：不限次数，否则阶段无法消耗完时间。
            activityData.maxUsesPerStage = UnlimitedUses;
            activityData.linkedMiniGame = miniGameData;
            GameArtBindingUtility.ApplyKnownArtPath(activityData);
            return activityData;
        }

        private static ActivityData CreateEventActivity(string activityId, string displayName, params RewardData[] extraRewards)
        {
            GameEventData eventData = ScriptableObject.CreateInstance<GameEventData>();
            eventData.eventId = activityId + "_event";
            eventData.title = displayName;
            EventChoiceData choice = new EventChoiceData
            {
                choiceText = "稳妥处理",
                rewards = new List<RewardData>
                {
                    CreateStatReward(PlayerStatKey.Mood, 2),
                    CreateStatReward(PlayerStatKey.ParentRelation, 2)
                }
            };
            AddRewards(choice.rewards, extraRewards);
            eventData.choices.Add(choice);
            GameArtBindingUtility.ApplyKnownArtPath(eventData);

            ActivityData activityData = ScriptableObject.CreateInstance<ActivityData>();
            activityData.activityId = activityId;
            activityData.displayName = displayName;
            activityData.activityType = ActivityType.Event;
            activityData.timeCost = 1;
            // 同训练类：不限次数，否则阶段无法消耗完时间。
            activityData.maxUsesPerStage = UnlimitedUses;
            activityData.linkedEvent = eventData;
            GameArtBindingUtility.ApplyKnownArtPath(activityData);
            return activityData;
        }

        private static ItemData CreateEnergyDrink()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_energy_drink";
            itemData.displayName = "功能饮料";
            itemData.itemType = ItemType.Consumable;
            itemData.description = "立刻恢复较多精力，适合在连续行动前补充状态。";
            itemData.price = 12;
            itemData.triggerTiming = ItemTriggerTiming.OnUse;
            itemData.canUseInStageScreen = true;
            itemData.canUseInBattle = true;
            itemData.consumedOnUse = true;
            itemData.effects.Add(new CardEffectData { effectType = CardEffectType.GainEnergy, value = 15 });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        private static ItemData CreateWarmTea()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_warm_tea";
            itemData.displayName = "热茶";
            itemData.itemType = ItemType.Consumable;
            itemData.description = "温和恢复精力，适合在地图探索中临时调整节奏。";
            itemData.price = 8;
            itemData.triggerTiming = ItemTriggerTiming.OnUse;
            itemData.canUseInStageScreen = true;
            itemData.canUseInBattle = true;
            itemData.consumedOnUse = true;
            itemData.effects.Add(new CardEffectData { effectType = CardEffectType.GainEnergy, value = 8 });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        private static ItemData CreateWrongBook()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_wrong_book";
            itemData.displayName = "错题本";
            itemData.itemType = ItemType.Passive;
            itemData.description = "战斗开始时多抽 1 张牌，帮助复习节奏更稳定。";
            itemData.price = 18;
            itemData.triggerTiming = ItemTriggerTiming.OnBattleStart;
            itemData.canUseInBattle = true;
            itemData.effects.Add(new CardEffectData { effectType = CardEffectType.DrawCards, value = 1 });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        private static ItemData CreateLuckyPen()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_lucky_pen";
            itemData.displayName = "幸运笔";
            itemData.itemType = ItemType.Passive;
            itemData.description = "Boss 开始时获得额外进度，适合考试前准备。";
            itemData.price = 24;
            itemData.triggerTiming = ItemTriggerTiming.OnBossStart;
            itemData.effects.Add(new CardEffectData { effectType = CardEffectType.AddChallengeProgress, value = 10 });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        // ── 2026-10-26 补齐的道具 ─────────────────────────────────────
        // 起因：这些 itemId 早就写进了 GameArtAssetCatalog.ItemIconPaths（图也有），
        // 但一直没有 ItemData，所以商店里永远买不到、图标永远不出现。
        // 效果按「学生生活会怎么用它」来配，不复用已有的精力/进度类效果以免撞车。

        /// <summary>闹钟：提前起床，换一天从容 → 减压力。</summary>
        private static ItemData CreateAlarmClock()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_alarm_clock";
            itemData.displayName = "闹钟";
            itemData.itemType = ItemType.Consumable;
            itemData.description = "早起一小时，把一天的节奏调松一点。压力 -6。";
            itemData.price = 22;
            itemData.triggerTiming = ItemTriggerTiming.OnUse;
            itemData.canUseInStageScreen = true;
            itemData.canUseInBattle = false;
            itemData.consumedOnUse = true;
            itemData.effects.Add(new CardEffectData { effectType = CardEffectType.ReducePressure, value = 6 });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        /// <summary>耳机：听音乐放松 → 大幅减压力，顺带抬心情。</summary>
        private static ItemData CreateHeadphones()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_headphones";
            itemData.displayName = "耳机";
            itemData.itemType = ItemType.Consumable;
            itemData.description = "戴上就不想说话的那半小时。压力 -8，心情 +3。";
            itemData.price = 25;
            itemData.triggerTiming = ItemTriggerTiming.OnUse;
            itemData.canUseInStageScreen = true;
            itemData.canUseInBattle = false;
            itemData.consumedOnUse = true;
            itemData.effects.Add(new CardEffectData { effectType = CardEffectType.ReducePressure, value = 8 });
            itemData.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.AddPlayerStat,
                targetStat = PlayerStatKey.Mood,
                value = 3
            });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        /// <summary>家长便条：家里留的字条 → 抬亲子关系。</summary>
        private static ItemData CreateParentNote()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_parent_note";
            itemData.displayName = "家长便条";
            itemData.itemType = ItemType.Consumable;
            itemData.description = "压在桌上的字条，只写了两行。亲子关系 +6。";
            itemData.price = 30;
            itemData.triggerTiming = ItemTriggerTiming.OnUse;
            itemData.canUseInStageScreen = true;
            itemData.canUseInBattle = false;
            itemData.consumedOnUse = true;
            itemData.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.AddPlayerStat,
                targetStat = PlayerStatKey.ParentRelation,
                value = 6
            });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        /// <summary>文具盒：装备齐全，开局多 1 点战斗能量（被动，不消耗）。</summary>
        private static ItemData CreatePencilCase()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_pencil_case";
            itemData.displayName = "文具盒";
            itemData.itemType = ItemType.Passive;
            itemData.description = "笔、橡皮、尺子都在该在的位置。战斗开始时额外获得 1 点能量。";
            itemData.price = 28;
            itemData.triggerTiming = ItemTriggerTiming.OnBattleStart;
            itemData.canUseInBattle = true;
            itemData.effects.Add(new CardEffectData { effectType = CardEffectType.GainBattleEnergy, value = 1 });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        /// <summary>星星贴纸：给自己的小奖励 → 抬自信。</summary>
        private static ItemData CreateStickerStar()
        {
            ItemData itemData = ScriptableObject.CreateInstance<ItemData>();
            itemData.itemId = "item_sticker_star";
            itemData.displayName = "星星贴纸";
            itemData.itemType = ItemType.Consumable;
            itemData.description = "贴在错题本封面上的那种。自信 +4。";
            itemData.price = 15;
            itemData.triggerTiming = ItemTriggerTiming.OnUse;
            itemData.canUseInStageScreen = true;
            itemData.canUseInBattle = false;
            itemData.consumedOnUse = true;
            itemData.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.AddPlayerStat,
                targetStat = PlayerStatKey.Confidence,
                value = 4
            });
            GameArtBindingUtility.ApplyKnownArtPath(itemData);
            return itemData;
        }

        private static LocationData CreateLocation(
            string locationId,
            string displayName,
            string description,
            LocationType locationType,
            bool unlockedByDefault,
            string imagePath,
            params EventConditionData[] unlockConditions)
        {
            LocationData locationData = ScriptableObject.CreateInstance<LocationData>();
            locationData.locationId = locationId;
            locationData.displayName = displayName;
            locationData.description = description;
            locationData.locationType = locationType;
            locationData.unlockedByDefault = unlockedByDefault;
            locationData.imagePath = imagePath;
            locationData.enterTimeCost = 0;
            locationData.unlockConditions = new List<EventConditionData>(unlockConditions);
            return locationData;
        }

        private static EndingData CreateEnding(
            string endingId,
            string displayName,
            string description,
            int priority,
            int minimumClearedStages,
            int minimumDefeatedBosses,
            int minimumSubjectAverage,
            int maximumPressure,
            params EventConditionData[] conditions)
        {
            EndingData endingData = ScriptableObject.CreateInstance<EndingData>();
            endingData.endingId = endingId;
            endingData.displayName = displayName;
            endingData.description = description;
            endingData.priority = priority;
            endingData.minimumClearedStages = minimumClearedStages;
            endingData.minimumDefeatedBosses = minimumDefeatedBosses;
            endingData.minimumSubjectAverage = minimumSubjectAverage;
            endingData.maximumPressure = maximumPressure;
            endingData.requireEndingReached = true;
            endingData.conditions = new List<EventConditionData>(conditions);
            return endingData;
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

        private static CardData CreateProgressCard(string cardId, string displayName, int progress, int cost = 1)
        {
            CardData cardData = ScriptableObject.CreateInstance<CardData>();
            cardData.cardId = cardId;
            cardData.displayName = displayName;
            cardData.cardType = CardType.Study;
            cardData.rarity = CardRarity.Common;
            cardData.cost = cost;
            cardData.targetType = CardTargetType.SingleEnemy;
            cardData.damage = progress;
            cardData.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.AddChallengeProgress,
                value = progress
            });
            GameArtBindingUtility.ApplyKnownArtPath(cardData);
            return cardData;
        }

        private static CardData CreateRestCard(string cardId, string displayName)
        {
            CardData cardData = ScriptableObject.CreateInstance<CardData>();
            cardData.cardId = cardId;
            cardData.displayName = displayName;
            cardData.cardType = CardType.Rest;
            cardData.rarity = CardRarity.Common;
            cardData.cost = 1;
            cardData.targetType = CardTargetType.Self;
            cardData.block = 5;
            cardData.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.GainEnergy,
                value = 6
            });
            cardData.effects.Add(new CardEffectData
            {
                effectType = CardEffectType.ReducePressure,
                value = 3
            });
            GameArtBindingUtility.ApplyKnownArtPath(cardData);
            return cardData;
        }

        private static RewardData CreateStatReward(PlayerStatKey statKey, int value)
        {
            return new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = statKey,
                value = value
            };
        }

        private static RewardData CreateCardReward(CardData card)
        {
            return new RewardData
            {
                rewardType = RewardType.AddCard,
                card = card
            };
        }

        private static RewardData CreateItemReward(ItemData item)
        {
            return new RewardData
            {
                rewardType = RewardType.AddItem,
                item = item
            };
        }

        private static void AddRewards(List<RewardData> target, IEnumerable<RewardData> rewards)
        {
            if (rewards == null)
            {
                return;
            }

            foreach (RewardData reward in rewards)
            {
                if (reward != null)
                {
                    target.Add(reward);
                }
            }
        }
    }
}
