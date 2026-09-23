using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using StudentGrowth;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.Editor
{
    /// <summary>
    /// 内容体检工具（施工单 B1）。
    ///
    /// 回答四个问题：
    ///   ① 每条结局是否存在可达路径
    ///   ② 每个地点是否存在满足解锁条件的路径（含"标记位是否有写入点"）
    ///   ③ 每种资源是否存在来源与出口
    ///   ④ 每个属性是否存在至少一个来源
    ///
    /// 方法：**不跑完整游戏**，做「上界推算」——把每个属性的所有可及来源增量求和，
    /// 与门槛比较。若上界都够不到门槛，则该结局/地点在数学上不可达。
    /// 这是充分条件而非必要条件：报"不可达"必然为真；报"可达"只代表上界够，
    /// 实战还受时间预算与供给量约束（本工具不负责那一层）。
    ///
    /// 自检：首次运行应报出已知的 4 项（见报告"已知答案对照"节）。
    /// 若报不出，说明是工具错了，不是内容没问题。
    /// </summary>
    public static class ContentHealthAudit
    {
        private const string ReportPath = "Docs/ContentHealthReport.md";
        private const int SuccessExitCode = 0;
        private const int FailureExitCode = 1;

        /// <summary>工具自身文件名。源码扫描标记位时必须排除，否则会自我污染判据。</summary>
        private const string SelfFileName = "/ContentHealthAudit.cs";

        private static readonly string[] ScanRoots = { "Assets/Data", "Assets/Resources" };

        // ------------------------------------------------------------------
        // 入口
        // ------------------------------------------------------------------

        [MenuItem("Tools/Student Growth/Content/运行内容体检", priority = 100)]
        public static void RunFromMenu()
        {
            AuditSummary summary = RunAudit();

            string summaryLine = summary.BlockingCount == 0
                ? "[内容体检] 通过，未发现不可达项。报告：" + ReportPath
                : "[内容体检] 发现 " + summary.BlockingCount + " 项阻塞级问题（不可达结局 "
                  + summary.UnreachableEndingCount + "｜不可达地点 " + summary.UnreachableLocationCount
                  + "｜零来源属性 " + summary.ZeroSourceStatCount + "）。报告：" + ReportPath;

            // ⚠ 刻意不使用 EditorUtility.DisplayDialog。
            // 模态对话框会阻塞编辑器主线程，后果有二：
            //   ① 挡住后续的自动化调用（MCP / 批量 -executeMethod 会一直排队直到超时）；
            //   ② 人工每次运行都得点一次"确定"，而这个工具是要被反复运行的仪表盘。
            // 结果同时写到 Console 与报告文件，两者都不阻塞且足够。
            if (summary.BlockingCount == 0)
            {
                Debug.Log(summaryLine);
            }
            else
            {
                Debug.LogError(summaryLine);
            }

            ExitBatchMode(summary.BlockingCount == 0 ? SuccessExitCode : FailureExitCode);
        }

        /// <summary>批量入口：Unity -batchmode -quit -projectPath &lt;路径&gt; -executeMethod StudentGrowth.Editor.ContentHealthAudit.RunAuditBatch</summary>
        public static void RunAuditBatch()
        {
            RunFromMenu();
        }

        // ------------------------------------------------------------------
        // 主流程
        // ------------------------------------------------------------------

        public static AuditSummary RunAudit()
        {
            var ctx = new AuditContext();
            ctx.CollectAll();

            var summary = new AuditSummary();
            summary.DataSources = ctx.DataSourceNotes;
            summary.Endings = CheckEndings(ctx, summary);
            summary.Locations = CheckLocations(ctx, summary);
            summary.RewardTypes = CheckRewardTypes(ctx, summary);
            summary.Stats = CheckStatSources(ctx, summary);
            summary.ExtraFindings = CheckExtraFindings(ctx);
            summary.GeneratedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            WriteReport(summary);
            return summary;
        }

        // ------------------------------------------------------------------
        // 数据采集
        // ------------------------------------------------------------------

        public sealed class AuditContext
        {
            // 去重规则：按 (ownerId, 属性) 归并，同一组只取绝对值最大的一次。
            //   · 不同 ownerId 之间正常求和（不同活动是不同来源，应累加）
            //   · 同一 ownerId 的多次出现只计一次（同一 ID 存在多份资产时不应双重计数）
            // 取"绝对值最大"而非"第一份"，是为了让上界保持为上界（不会因去重而低估）。
            private readonly Dictionary<string, StatDelta> _deltaByOwnerStat = new Dictionary<string, StatDelta>();
            public readonly Dictionary<RewardType, int> RewardTypeCounts = new Dictionary<RewardType, int>();
            public readonly List<string> DataSourceNotes = new List<string>();
            // 记录每个 ID 的**全部出现位置**（含具体资产路径）。
            // 用途：发现"同一 ID 存在多份资产"——它们会被视为同一份内容，
            // 上界计算取最大、不叠加，但重复本身需要在报告里暴露出来供人工合并。
            public readonly Dictionary<string, List<string>> IdOccurrences = new Dictionary<string, List<string>>();

            public List<StatDelta> BuildDeltas()
            {
                return new List<StatDelta>(_deltaByOwnerStat.Values);
            }

            /// <summary>记入一条属性增量。同一 (ownerId, 属性) 重复出现时取绝对值较大者。</summary>
            public void Accumulate(string ownerId, PlayerStatKey stat, int value, string origin)
            {
                if (value == 0) return;
                string key = (ownerId ?? string.Empty) + "|" + (int)stat;
                StatDelta existing;
                if (_deltaByOwnerStat.TryGetValue(key, out existing))
                {
                    if (Mathf.Abs(value) > Mathf.Abs(existing.Value))
                    {
                        _deltaByOwnerStat[key] = new StatDelta(stat, value, origin);
                    }
                    return;
                }
                _deltaByOwnerStat[key] = new StatDelta(stat, value, origin);
            }

            public List<StageData> Stages = new List<StageData>();
            public List<LocationData> Locations = new List<LocationData>();
            public List<EndingData> Endings = new List<EndingData>();

            public int BossCount;
            public int StageCount;

            public void CollectAll()
            {
                CollectRuntimeContent();
                CollectAssetContent();
                CollectCardAndItemEffects();
                CountBosses();
            }

            // ---- 1) 运行时内容：GameBootstrapper 的实际取数逻辑（含兜底）----
            private void CollectRuntimeContent()
            {
                GameBootstrapperData boot = Resources.Load<GameBootstrapperData>(GameBootstrapper.DefaultResourcePath);
                bool hasBoot = boot != null;

                DataSourceNotes.Add(hasBoot
                    ? "GameBootstrapperData 资产：**已找到** → 实际运行使用资产数据"
                    : "GameBootstrapperData 资产：**未找到**（" + GameBootstrapper.DefaultResourcePath + "）→ 实际运行走 MvpSampleDataFactory 代码兜底");

                Stages = (hasBoot && boot.stages != null && boot.stages.Count > 0)
                    ? boot.stages.Where(s => s != null).ToList()
                    : MvpSampleDataFactory.CreateThreeStageSequence();

                Locations = (hasBoot && boot.locations != null && boot.locations.Count > 0)
                    ? boot.locations.Where(l => l != null).ToList()
                    : MvpSampleDataFactory.CreateDefaultLocations();

                Endings = (hasBoot && boot.endings != null && boot.endings.Count > 0)
                    ? boot.endings.Where(e => e != null).ToList()
                    : MvpSampleDataFactory.CreateDefaultEndings();

                DataSourceNotes.Add("运行时内容：阶段 " + Stages.Count + " / 地点 " + Locations.Count + " / 结局 " + Endings.Count);

                foreach (StageData stage in Stages)
                {
                    if (stage == null) continue;
                    RegisterId(stage.stageId, "StageData(运行时)", "MvpSampleDataFactory 阶段序列");
                    AddRewards(stage.enterStageRewards, "阶段进入奖励", stage.stageId, stage.displayName);
                    AddRewards(stage.clearStageRewards, "阶段通关奖励", stage.stageId, stage.displayName);

                    if (stage.availableActivities != null)
                    {
                        foreach (ActivityData act in stage.availableActivities)
                        {
                            CollectActivity(act, "阶段可用活动", stage.stageId);
                        }
                    }

                    CollectChallenge(stage.bossChallenge, "阶段Boss", stage.stageId);
                }

                foreach (LocationData loc in Locations)
                {
                    if (loc == null) continue;
                    RegisterId(loc.locationId, "LocationData(运行时)", "MvpSampleDataFactory 默认地点");
                    if (loc.actions == null) continue;
                    foreach (LocationActionData action in loc.actions)
                    {
                        if (action == null) continue;
                        AddRewards(action.fixedRewards, "地点行动固定奖励", action.actionId, action.displayName);
                        AddRewards(action.possibleRewards, "地点行动可能奖励", action.actionId, action.displayName);
                    }
                }
            }

            // ---- 2) 资产内容：Assets/Data 与 Assets/Resources 下全部 SO ----
            private void CollectAssetContent()
            {
                List<UnityEngine.Object> assets = LoadScriptableObjects();
                int counts = 0;

                foreach (UnityEngine.Object obj in assets)
                {
                    if (obj == null) continue;

                    ActivityData activity = obj as ActivityData;
                    if (activity != null)
                    {
                        counts++;
                        RegisterId(activity.activityId, "ActivityData(资产)", AssetDatabase.GetAssetPath(activity));
                        CollectActivity(activity, "活动资产", AssetDatabase.GetAssetPath(activity));
                        continue;
                    }

                    GameEventData evt = obj as GameEventData;
                    if (evt != null)
                    {
                        RegisterId(evt.eventId, "GameEventData(资产)", AssetDatabase.GetAssetPath(evt));
                        CollectEvent(evt, "事件资产");
                        continue;
                    }

                    MiniGameData mini = obj as MiniGameData;
                    if (mini != null)
                    {
                        RegisterId(mini.miniGameId, "MiniGameData(资产) " + AssetDatabase.GetAssetPath(mini));
                        CollectMiniGame(mini, "小游戏资产");
                        continue;
                    }

                    ChallengeData challenge = obj as ChallengeData;
                    if (challenge != null)
                    {
                        RegisterId(challenge.challengeId, "ChallengeData(资产)", AssetDatabase.GetAssetPath(challenge));
                        CollectChallenge(challenge, "挑战资产", null);
                        continue;
                    }

                    StageData stage = obj as StageData;
                    if (stage != null)
                    {
                        RegisterId(stage.stageId, "StageData(资产)", AssetDatabase.GetAssetPath(stage));
                        AddRewards(stage.enterStageRewards, "阶段进入奖励(资产)", stage.stageId, stage.displayName);
                        AddRewards(stage.clearStageRewards, "阶段通关奖励(资产)", stage.stageId, stage.displayName);
                        continue;
                    }

                    EndingData ending = obj as EndingData;
                    if (ending != null)
                    {
                        RegisterId(ending.endingId, "EndingData(资产)", AssetDatabase.GetAssetPath(ending));
                        continue;
                    }

                    LocationData location = obj as LocationData;
                    if (location != null)
                    {
                        RegisterId(location.locationId, "LocationData(资产)", AssetDatabase.GetAssetPath(location));
                    }
                }

                DataSourceNotes.Add("资产内容：Assets/Data 与 Assets/Resources 下扫描到 ScriptableObject " + assets.Count
                    + " 个，其中活动 " + counts + " 个");
            }

            // ---- 3) 卡牌与道具的效果（含重要：卡片效果是"投资牌"的属性来源）----
            private void CollectCardAndItemEffects()
            {
                List<UnityEngine.Object> assets = LoadScriptableObjects();

                foreach (UnityEngine.Object obj in assets)
                {
                    CardData card = obj as CardData;
                    if (card != null)
                    {
                        RegisterId(card.cardId, "CardData(资产)", AssetDatabase.GetAssetPath(card));
                        AddCardEffects(card.effects, "卡牌效果", card.cardId, card.displayName);
                        continue;
                    }

                    ItemData item = obj as ItemData;
                    if (item != null)
                    {
                        RegisterId(item.itemId, "ItemData(资产)", AssetDatabase.GetAssetPath(item));
                        AddCardEffects(item.effects, "道具效果", item.itemId, item.displayName);
                    }
                }
            }

            private void CountBosses()
            {
                int bosses = 0;
                foreach (StageData stage in Stages)
                {
                    if (stage != null && stage.bossChallenge != null && stage.bossChallenge.isBoss) bosses++;
                }
                BossCount = bosses;
                StageCount = Stages.Count;
            }

            // ---- 采集助手 ----

            public void CollectActivity(ActivityData activity, string where, string fallbackOwner)
            {
                if (activity == null) return;
                string ownerId = string.IsNullOrEmpty(activity.activityId) ? fallbackOwner : activity.activityId;
                AddRewards(activity.fixedRewards, where + "固定奖励", ownerId, activity.displayName);
                AddRewards(activity.possibleRewards, where + "可能奖励", ownerId, activity.displayName);

                // 跟随关联内容。运行时有很多事件/小游戏/挑战是用代码构造后挂到活动上的
                // （例：MvpSampleDataFactory.CreateEventActivity 内部 new 一个 GameEventData），
                // 它们不在 Assets 扫描范围内 —— 不跟随就会漏掉这些奖励。
                // 不会与资产扫描重复计数：Accumulate 按 (来源 ID, 属性) 归并，同一事件只计一次。
                if (activity.linkedEvent != null)
                {
                    CollectEvent(activity.linkedEvent, where + "关联事件");
                }
                if (activity.linkedMiniGame != null)
                {
                    CollectMiniGame(activity.linkedMiniGame, where + "关联小游戏");
                }
                if (activity.linkedChallenge != null)
                {
                    CollectChallenge(activity.linkedChallenge, where + "关联挑战", ownerId);
                }
            }

            public void CollectMiniGame(MiniGameData mini, string where)
            {
                if (mini == null) return;
                AddRewards(mini.successRewards, where + "成功", mini.miniGameId, mini.displayName);
                AddRewards(mini.failureRewards, where + "失败", mini.miniGameId, mini.displayName);
            }

            private void CollectEvent(GameEventData evt, string where)
            {
                if (evt == null || evt.choices == null) return;
                for (int i = 0; i < evt.choices.Count; i++)
                {
                    EventChoiceData choice = evt.choices[i];
                    if (choice == null) continue;
                    string label = where + "选项" + (i + 1) + "(" + (string.IsNullOrEmpty(choice.choiceText) ? "无文本" : choice.choiceText) + ")";
                    AddRewards(choice.rewards, label, evt.eventId, evt.title);
                }
            }

            private void CollectChallenge(ChallengeData challenge, string where, string fallbackOwner)
            {
                if (challenge == null) return;
                string ownerId = string.IsNullOrEmpty(challenge.challengeId) ? fallbackOwner : challenge.challengeId;
                AddRewards(challenge.successRewards, where + "成功奖励", ownerId, challenge.displayName);
                AddRewards(challenge.failureRewards, where + "失败奖励", ownerId, challenge.displayName);
                AddRewards(challenge.bossClearRewards, where + "通关奖励", ownerId, challenge.displayName);
                AddRewards(challenge.bossFailureRewards, where + "失败奖励", ownerId, challenge.displayName);
            }

            public void AddRewards(List<RewardData> rewards, string where, string ownerId, string ownerName)
            {
                if (rewards == null) return;
                foreach (RewardData reward in rewards)
                {
                    if (reward == null) continue;

                    if (!RewardTypeCounts.ContainsKey(reward.rewardType)) RewardTypeCounts[reward.rewardType] = 0;
                    RewardTypeCounts[reward.rewardType]++;

                    string origin = where + " ｜ " + ownerId + "（" + ownerName + "）";
                    switch (reward.rewardType)
                    {
                        case RewardType.AddPlayerStat:
                            Accumulate(ownerId, reward.targetStat, reward.value, origin);
                            break;
                        case RewardType.ReducePlayerStat:
                            Accumulate(ownerId, reward.targetStat, -Mathf.Abs(reward.value), origin);
                            break;
                        case RewardType.ReducePressure:
                            Accumulate(ownerId, PlayerStatKey.Pressure, -Mathf.Abs(reward.value), origin);
                            break;
                        case RewardType.IncreaseMaxEnergy:
                            Accumulate(ownerId, PlayerStatKey.MaxEnergy, reward.value, origin);
                            break;
                    }
                }
            }

            public void AddCardEffects(List<CardEffectData> effects, string where, string ownerId, string ownerName)
            {
                if (effects == null) return;
                foreach (CardEffectData effect in effects)
                {
                    if (effect == null) continue;
                    string origin = where + " ｜ " + ownerId + "（" + ownerName + "）";
                    switch (effect.effectType)
                    {
                        case CardEffectType.AddPlayerStat:
                        case CardEffectType.AddSubjectProgress:
                            Accumulate(ownerId, effect.targetStat, effect.value, origin);
                            break;
                        case CardEffectType.ReducePlayerStat:
                            Accumulate(ownerId, effect.targetStat, -Mathf.Abs(effect.value), origin);
                            break;
                        case CardEffectType.ReducePressure:
                            Accumulate(ownerId, PlayerStatKey.Pressure, -Mathf.Abs(effect.value), origin);
                            break;
                        case CardEffectType.AddPressure:
                            Accumulate(ownerId, PlayerStatKey.Pressure, effect.value, origin);
                            break;
                        case CardEffectType.GainEnergy:
                            Accumulate(ownerId, PlayerStatKey.Energy, effect.value, origin);
                            break;
                    }
                }
            }

            public void RegisterId(string id, string kind, string detail = null)
            {
                if (string.IsNullOrEmpty(id)) return;
                if (!IdOccurrences.ContainsKey(id)) IdOccurrences[id] = new List<string>();

                string occurrence = string.IsNullOrEmpty(detail) ? kind : kind + "  " + detail;
                if (!IdOccurrences[id].Contains(occurrence))
                {
                    IdOccurrences[id].Add(occurrence);
                }
            }

            private List<UnityEngine.Object> LoadScriptableObjects()
            {
                var result = new List<UnityEngine.Object>();
                string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", ScanRoots);
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path)) continue;
                    UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                    if (obj != null) result.Add(obj);
                }
                return result;
            }
        }

        // ------------------------------------------------------------------
        // 检查 1：结局可达性
        // ------------------------------------------------------------------

        private static List<EndingCheck> CheckEndings(AuditContext ctx, AuditSummary summary)
        {
            var results = new List<EndingCheck>();
            StatBounds bounds = StatBounds.Compute(ctx);

            foreach (EndingData ending in ctx.Endings)
            {
                if (ending == null) continue;
                var check = new EndingCheck
                {
                    Id = ending.endingId,
                    Name = ending.displayName,
                    Priority = ending.priority,
                    Notes = new List<string>()
                };

                if (ending.minimumClearedStages > ctx.StageCount)
                {
                    check.Unreachable = true;
                    check.Notes.Add("要求通关阶段数 " + ending.minimumClearedStages + " > 实际阶段数 " + ctx.StageCount);
                }

                if (ending.minimumDefeatedBosses > ctx.BossCount)
                {
                    check.Unreachable = true;
                    check.Notes.Add("要求击败 Boss 数 " + ending.minimumDefeatedBosses + " > 实际 Boss 数 " + ctx.BossCount);
                }

                int maxAvg = (bounds.Max(PlayerStatKey.Chinese) + bounds.Max(PlayerStatKey.Math) + bounds.Max(PlayerStatKey.English)) / 3;
                if (ending.minimumSubjectAverage > maxAvg)
                {
                    check.Unreachable = true;
                    check.Notes.Add("要求三科均值 ≥ " + ending.minimumSubjectAverage + "，上界仅 " + maxAvg);
                }

                if (ending.conditions != null)
                {
                    foreach (EventConditionData condition in ending.conditions)
                    {
                        if (condition == null) continue;
                        string reason = EvaluateCondition(condition, bounds);
                        if (reason != null)
                        {
                            check.Unreachable = true;
                            check.Notes.Add(reason);
                        }
                    }
                }

                results.Add(check);
            }

            summary.UnreachableEndingCount = results.Count(r => r.Unreachable);
            return results;
        }

        /// <summary>返回 null 表示可满足；否则返回不可满足的原因。</summary>
        private static string EvaluateCondition(EventConditionData condition, StatBounds bounds)
        {
            int max = bounds.Max(condition.statKey);
            int min = bounds.Min(condition.statKey);
            string statName = StatLabel(condition.statKey);

            switch (condition.comparison)
            {
                case ComparisonType.GreaterOrEqual:
                    if (max < condition.value)
                        return statName + " 需 ≥ " + condition.value + "，但全部来源的上界只有 " + max;
                    break;
                case ComparisonType.LessOrEqual:
                    if (min > condition.value)
                        return statName + " 需 ≤ " + condition.value + "，但下界已是 " + min;
                    break;
                case ComparisonType.Equal:
                    return statName + " 要求精确等于 " + condition.value + "（建议改用区间条件，精确相等在实机里几乎不可控）";
                case ComparisonType.NotEqual:
                    break;
            }
            return null;
        }

        // ------------------------------------------------------------------
        // 检查 2：地点解锁可达性
        // ------------------------------------------------------------------

        private static List<LocationCheck> CheckLocations(AuditContext ctx, AuditSummary summary)
        {
            var results = new List<LocationCheck>();
            StatBounds bounds = StatBounds.Compute(ctx);

            // 已知的特殊解锁键（StageMapLocationAvailabilityService 中的地点标记）
            const string ClubFlag = "club_intro";

            foreach (LocationData location in ctx.Locations)
            {
                if (location == null) continue;
                var check = new LocationCheck
                {
                    Id = location.locationId,
                    Name = location.displayName,
                    UnlockedByDefault = location.unlockedByDefault,
                    Notes = new List<string>()
                };

                if (location.unlockedByDefault)
                {
                    check.Reachable = true;
                    check.Notes.Add("默认解锁");
                    results.Add(check);
                    continue;
                }

                bool anyConditionSatisfiable = false;
                bool hasCondition = false;

                if (location.unlockConditions != null && location.unlockConditions.Count > 0)
                {
                    hasCondition = true;
                    foreach (EventConditionData condition in location.unlockConditions)
                    {
                        if (condition == null) continue;
                        string reason = EvaluateCondition(condition, bounds);
                        if (reason == null)
                        {
                            anyConditionSatisfiable = true;
                        }
                        else
                        {
                            check.Notes.Add("条件不满足：" + reason);
                        }
                    }
                }

                if (!hasCondition && !location.unlockedByDefault)
                {
                    check.Notes.Add("非默认解锁，但未配置 unlockConditions —— 只能靠运行时标记位解锁，需人工确认写入点");
                }

                check.Reachable = anyConditionSatisfiable;

                // 特殊：社团活动室的标记位检查
                if (location.locationId != null && location.locationId.Contains("club"))
                {
                    List<string> writers = FindSourceReferences(ClubFlag);
                    if (writers.Count == 0)
                    {
                        check.Notes.Add("⚠ 该地点依赖地点标记 <code>" + ClubFlag + "</code>，但**全工程未找到该字符串的任何出现**（含读取点）");
                        check.Reachable = false;
                    }
                    else if (writers.Count == 1)
                    {
                        check.Notes.Add("⚠ 该地点依赖地点标记 <code>" + ClubFlag + "</code>，但该字符串**只在 1 个文件中出现**（"
                            + writers[0] + "）——即只有读取点、没有写入点，条件永远不成立");
                        check.Reachable = false;
                    }
                    else
                    {
                        check.Notes.Add("地点标记 <code>" + ClubFlag + "</code> 在 " + writers.Count + " 个文件中出现："
                            + string.Join("、", writers));
                    }
                }

                results.Add(check);
            }

            summary.UnreachableLocationCount = results.Count(r => !r.Reachable);
            return results;
        }

        /// <summary>在 Assets 下的所有 .cs 中查找某个字符串字面量的出现文件（用于识别"只读不写的标记位"）。</summary>
        private static List<string> FindSourceReferences(string token)
        {
            var hits = new List<string>();
            if (string.IsNullOrEmpty(token) || !Directory.Exists("Assets")) return hits;

            try
            {
                string[] files = Directory.GetFiles("Assets", "*.cs", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    // 跳过工具自身：本文件里含有被检查的字面量（如 club_intro），
                    // 若计入会把"只出现于 1 个文件 = 只读不写"的判据污染成 2 个文件
                    // （本工具首版实测踩过：导致社团活动室该报未报）。
                    if (file.Replace('\\', '/').EndsWith(SelfFileName, StringComparison.Ordinal)) continue;

                    string text;
                    try { text = File.ReadAllText(file); }
                    catch { continue; }
                    if (text.IndexOf(token, StringComparison.Ordinal) >= 0)
                    {
                        hits.Add(file.Replace('\\', '/'));
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[内容体检] 扫描源码引用失败：" + ex.Message);
            }

            return hits;
        }

        // ------------------------------------------------------------------
        // 检查 3：资源 source / sink
        // ------------------------------------------------------------------

        private static List<RewardTypeCheck> CheckRewardTypes(AuditContext ctx, AuditSummary summary)
        {
            var results = new List<RewardTypeCheck>();
            foreach (RewardType type in Enum.GetValues(typeof(RewardType)))
            {
                int count;
                ctx.RewardTypeCounts.TryGetValue(type, out count);
                results.Add(new RewardTypeCheck
                {
                    Type = type,
                    Count = count,
                    NoSourceOrSink = count == 0
                });
            }

            summary.EmptyRewardTypeCount = results.Count(r => r.NoSourceOrSink);
            return results;
        }

        // ------------------------------------------------------------------
        // 检查 4：属性来源
        // ------------------------------------------------------------------

        private static List<StatSourceCheck> CheckStatSources(AuditContext ctx, AuditSummary summary)
        {
            var results = new List<StatSourceCheck>();
            StatBounds bounds = StatBounds.Compute(ctx);
            List<StatDelta> deltas = ctx.BuildDeltas();

            foreach (PlayerStatKey key in Enum.GetValues(typeof(PlayerStatKey)))
            {
                List<StatDelta> sources = deltas.Where(d => d.Stat == key && d.Value > 0).ToList();
                results.Add(new StatSourceCheck
                {
                    Stat = key,
                    Name = StatLabel(key),
                    Start = bounds.Start(key),
                    SourceCount = sources.Count,
                    UpperBound = bounds.Max(key),
                    NoSource = sources.Count == 0,
                    IsBottleneck = sources.Count == 1,
                    ExampleOrigins = sources.Take(3).Select(s => s.Origin).ToList()
                });
            }

            summary.ZeroSourceStatCount = results.Count(r => r.NoSource);
            summary.BottleneckStatCount = results.Count(r => r.IsBottleneck);
            return results;
        }

        // ------------------------------------------------------------------
        // 附带发现
        // ------------------------------------------------------------------

        private static List<string> CheckExtraFindings(AuditContext ctx)
        {
            var findings = new List<string>();

            foreach (KeyValuePair<string, List<string>> pair in ctx.IdOccurrences)
            {
                if (pair.Value.Count > 1)
                {
                    findings.Add("重复 ID <code>" + pair.Key + "</code> 出现在 " + pair.Value.Count + " 处：<br>"
                        + string.Join("<br>", pair.Value)
                        + "<br>（同一 ID 会被视为同一份内容，上界取最大值不叠加；但数据源冲突需人工确认以哪份为准）");
                }
            }

            if (findings.Count == 0)
            {
                findings.Add("未发现重复 ID。");
            }

            return findings;
        }

        // ------------------------------------------------------------------
        // 统计边界（上界 / 下界）
        // ------------------------------------------------------------------

        public sealed class StatBounds
        {
            private readonly Dictionary<PlayerStatKey, int> _start = new Dictionary<PlayerStatKey, int>();
            private readonly Dictionary<PlayerStatKey, int> _max = new Dictionary<PlayerStatKey, int>();
            private readonly Dictionary<PlayerStatKey, int> _min = new Dictionary<PlayerStatKey, int>();

            public int Start(PlayerStatKey key) { int v; return _start.TryGetValue(key, out v) ? v : 0; }
            public int Max(PlayerStatKey key) { int v; return _max.TryGetValue(key, out v) ? v : 0; }
            public int Min(PlayerStatKey key) { int v; return _min.TryGetValue(key, out v) ? v : 0; }

            public static StatBounds Compute(AuditContext ctx)
            {
                var bounds = new StatBounds();
                var player = new PlayerState();
                player.ResetToDefaults();

                List<StatDelta> deltas = ctx.BuildDeltas();

                foreach (PlayerStatKey key in Enum.GetValues(typeof(PlayerStatKey)))
                {
                    int start = player.GetStat(key);
                    int cap = CapOf(key, player);
                    bounds._start[key] = start;

                    // 上界只累加"增益"来源，下界只累加"损耗"来源。
                    // ⚠ 不能把正负增量混在一起求和再取 max —— 那会让"既有增益又有损耗"的属性
                    //   上界退化成初始值（本工具首版实测踩过：压力有 4 个增益源却报上界 20）。
                    int gainSum = 0;
                    int lossSum = 0;
                    foreach (StatDelta delta in deltas)
                    {
                        if (delta.Stat != key) continue;
                        if (delta.Value > 0) gainSum += delta.Value;
                        else lossSum += delta.Value;
                    }

                    int rawMax = start + gainSum;
                    int rawMin = start + lossSum;

                    bounds._max[key] = Mathf.Clamp(rawMax, ValueClampUtility.StatMin, cap);
                    bounds._min[key] = Mathf.Clamp(rawMin, ValueClampUtility.StatMin, cap);
                }

                return bounds;
            }

            private static int CapOf(PlayerStatKey key, PlayerState player)
            {
                switch (key)
                {
                    case PlayerStatKey.Money: return ValueClampUtility.MoneyMax;
                    case PlayerStatKey.Energy: return player.maxEnergy;
                    case PlayerStatKey.MaxEnergy: return ValueClampUtility.StatMax;
                    default: return ValueClampUtility.StatMax;
                }
            }
        }

        public struct StatDelta
        {
            public readonly PlayerStatKey Stat;
            public readonly int Value;
            public readonly string Origin;

            public StatDelta(PlayerStatKey stat, int value, string origin)
            {
                Stat = stat;
                Value = value;
                Origin = origin;
            }
        }

        // ------------------------------------------------------------------
        // 展示助手
        // ------------------------------------------------------------------

        private static string StatLabel(PlayerStatKey key)
        {
            switch (key)
            {
                case PlayerStatKey.Chinese: return "语文";
                case PlayerStatKey.Math: return "数学";
                case PlayerStatKey.English: return "英语";
                case PlayerStatKey.Energy: return "精力";
                case PlayerStatKey.MaxEnergy: return "精力上限";
                case PlayerStatKey.Pressure: return "压力";
                case PlayerStatKey.Mood: return "心情";
                case PlayerStatKey.ParentRelation: return "亲子关系";
                case PlayerStatKey.Social: return "社交";
                case PlayerStatKey.Interest: return "兴趣";
                case PlayerStatKey.Confidence: return "自信";
                case PlayerStatKey.Autonomy: return "自主性";
                case PlayerStatKey.Money: return "金钱";
                default: return key.ToString();
            }
        }

        private static string ComparisonLabel(ComparisonType comparison)
        {
            switch (comparison)
            {
                case ComparisonType.GreaterOrEqual: return "≥";
                case ComparisonType.LessOrEqual: return "≤";
                case ComparisonType.Equal: return "=";
                case ComparisonType.NotEqual: return "≠";
                default: return "?";
            }
        }

        private static void ExitBatchMode(int exitCode)
        {
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(exitCode);
            }
        }

        // ------------------------------------------------------------------
        // 报告输出
        // ------------------------------------------------------------------

        private static void WriteReport(AuditSummary summary)
        {
            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(ReportPath, summary.BuildMarkdown());
            Debug.Log("[内容体检] 报告已导出：" + ReportPath);
        }

        // ------------------------------------------------------------------
        // 数据结构
        // ------------------------------------------------------------------

        public sealed class AuditSummary
        {
            public string GeneratedAt;
            public List<string> DataSources = new List<string>();
            public List<EndingCheck> Endings = new List<EndingCheck>();
            public List<LocationCheck> Locations = new List<LocationCheck>();
            public List<RewardTypeCheck> RewardTypes = new List<RewardTypeCheck>();
            public List<StatSourceCheck> Stats = new List<StatSourceCheck>();
            public List<string> ExtraFindings = new List<string>();

            public int UnreachableEndingCount;
            public int UnreachableLocationCount;
            public int EmptyRewardTypeCount;
            public int ZeroSourceStatCount;
            public int BottleneckStatCount;

            public int BlockingCount
            {
                get { return UnreachableEndingCount + UnreachableLocationCount + ZeroSourceStatCount; }
            }

            public string BuildMarkdown()
            {
                var sb = new StringBuilder();
                sb.AppendLine("# 内容体检报告");
                sb.AppendLine();
                sb.AppendLine("> 由 `ContentHealthAudit` 生成（工具位于 `Assets/Scripts/StudentGrowth/Editor/ContentHealthAudit.cs`）");
                sb.AppendLine("> 生成时间：" + GeneratedAt);
                sb.AppendLine();

                // 0. 结论
                sb.AppendLine("## 0. 结论速览");
                sb.AppendLine();
                sb.AppendLine("| 检查项 | 结果 |");
                sb.AppendLine("| --- | --- |");
                sb.AppendLine("| 不可达结局 | **" + UnreachableEndingCount + " / " + Endings.Count + "** |");
                sb.AppendLine("| 不可达地点 | **" + UnreachableLocationCount + " / " + Locations.Count + "** |");
                sb.AppendLine("| 零来源的属性 | **" + ZeroSourceStatCount + " / " + Stats.Count + "** |");
                sb.AppendLine("| 瓶颈属性（来源仅 1 处） | **" + BottleneckStatCount + " / " + Stats.Count + "** |");
                sb.AppendLine("| 无来源或无出口的资源类型 | **" + EmptyRewardTypeCount + " / " + RewardTypes.Count + "** |");
                sb.AppendLine("| **阻塞级问题合计** | **" + BlockingCount + "** |");
                sb.AppendLine();
                sb.AppendLine("> 「阻塞级」= 不可达结局 + 不可达地点 + 零来源属性。资源类型为空通常是设计选择（如本作尚未实现删牌），单列不计数。");
                sb.AppendLine();

                // 数据源
                sb.AppendLine("## 1. 数据源清点");
                sb.AppendLine();
                foreach (string note in DataSources) sb.AppendLine("- " + note);
                sb.AppendLine();

                // 2. 结局
                sb.AppendLine("## 2. 结局可达性");
                sb.AppendLine();
                sb.AppendLine("判定方法：把每个门槛属性的**全部可及来源增量求和**得到上界；上界够不到门槛即判不可达。");
                sb.AppendLine();
                sb.AppendLine("| 结局 | 优先级 | 判定 | 原因 |");
                sb.AppendLine("| --- | ---: | --- | --- |");
                foreach (EndingCheck ending in Endings)
                {
                    sb.AppendLine("| " + ending.Name + "<br><code>" + ending.Id + "</code> | " + ending.Priority + " | "
                        + (ending.Unreachable ? "**不可达**" : "可达（上界）") + " | "
                        + (ending.Notes.Count == 0 ? "—" : string.Join("<br>", ending.Notes)) + " |");
                }
                sb.AppendLine();

                // 3. 地点
                sb.AppendLine("## 3. 地点解锁可达性");
                sb.AppendLine();
                sb.AppendLine("| 地点 | 默认解锁 | 判定 | 备注 |");
                sb.AppendLine("| --- | --- | --- | --- |");
                foreach (LocationCheck location in Locations)
                {
                    sb.AppendLine("| " + location.Name + "<br><code>" + location.Id + "</code> | "
                        + (location.UnlockedByDefault ? "是" : "否") + " | "
                        + (location.Reachable ? "可达" : "**不可达**") + " | "
                        + (location.Notes.Count == 0 ? "—" : string.Join("<br>", location.Notes)) + " |");
                }
                sb.AppendLine();

                // 4. 资源
                sb.AppendLine("## 4. 资源来源与出口（RewardType 统计）");
                sb.AppendLine();
                sb.AppendLine("| RewardType | 出现次数 | 判定 |");
                sb.AppendLine("| --- | ---: | --- |");
                foreach (RewardTypeCheck check in RewardTypes)
                {
                    sb.AppendLine("| " + check.Type + " | " + check.Count + " | "
                        + (check.NoSourceOrSink ? "⚠ 零次出现 —— 无来源或无出口" : "有") + " |");
                }
                sb.AppendLine();

                // 5. 属性
                sb.AppendLine("## 5. 属性来源");
                sb.AppendLine();
                sb.AppendLine("| 属性 | 初始值 | 来源数 | 上界 | 判定 | 来源示例 |");
                sb.AppendLine("| --- | ---: | ---: | ---: | --- | --- |");
                foreach (StatSourceCheck check in Stats)
                {
                    sb.AppendLine("| " + check.Name + " | " + check.Start + " | " + check.SourceCount + " | "
                        + check.UpperBound + " | "
                        + (check.NoSource ? "**零来源**" : (check.IsBottleneck ? "**仅 1 处来源（瓶颈）**" : "有")) + " | "
                        + (check.ExampleOrigins.Count == 0 ? "—" : string.Join("<br>", check.ExampleOrigins)) + " |");
                }
                sb.AppendLine();

                // 6. 附带发现
                sb.AppendLine("## 6. 附带发现");
                sb.AppendLine();
                foreach (string finding in ExtraFindings) sb.AppendLine("- " + finding);
                sb.AppendLine();

                // 附录 A
                sb.AppendLine("## 附录 A：已知答案对照（工具自检）");
                sb.AppendLine();
                sb.AppendLine("工具首次运行时，必须能报出以下已知问题。**报不出即说明工具写错，而不是内容没问题。**");
                sb.AppendLine();
                sb.AppendLine("| 已知问题 | 是否报出 |");
                sb.AppendLine("| --- | --- |");
                sb.AppendLine("| 结局「稳定升学」不可达（三科均值上界不足） | " + Yes(FindEnding("stable_admission")) + " |");
                sb.AppendLine("| 结局「兴趣成长」不可达 | " + Yes(FindEnding("interest_growth")) + " |");
                sb.AppendLine("| 结局「自主选择」不可达 | " + Yes(FindEnding("self_driven")) + " |");
                sb.AppendLine("| 结局「压力调整」不可达 | " + Yes(FindEnding("pressure_recovery")) + " |");
                sb.AppendLine("| 地点「社团活动室」不可达 | " + Yes(FindLocation("club")) + " |");
                sb.AppendLine("| 属性「英语」来源不足（≤1 处，瓶颈） | " + Yes(FindStatWeak(PlayerStatKey.English)) + " |");
                sb.AppendLine("| 资源「AddMoney」零次出现（金钱无来源） | " + Yes(FindRewardType(RewardType.AddMoney)) + " |");
                sb.AppendLine();

                // 附录 B
                sb.AppendLine("## 附录 B：口径说明");
                sb.AppendLine();
                sb.AppendLine("1. **这是上界推算，不是完整模拟。** 报「不可达」必然为真（连把全部来源都吃到都不够）；报「可达」只代表上界够，实战还受阶段时间预算与内容供给量约束——那一层由别的检查负责。");
                sb.AppendLine("2. **同一 ID 只计一次。** 属性增量按 (来源 ID, 属性) 归并：**不同 ID 之间求和，同一 ID 的多次出现只取绝对值最大的一次**（取最大而非取第一份，是为了让上界不因去重而被低估）。重复 ID 本身记在「附带发现」里，建议人工合并。");
                sb.AppendLine("3. **「瓶颈属性」= 全工程只有 1 处来源。** 这类属性是单点依赖：那处来源一旦被删或被解锁条件卡住，该属性就再也不涨。零来源是\"完全不可达\"，仅 1 处来源是\"极脆弱\"，两者都值得处理。");
                sb.AppendLine("4. **属性上限取自 `ValueClampUtility`**：常规属性 0–100，精力上限按 `maxEnergy`，金钱 0–9999。");
                sb.AppendLine("5. **标记位检查**是启发式：扫描 `Assets` 下所有 `.cs` 中该字符串的出现文件数。只出现于 1 个文件（即 `StageMapLocationAvailabilityService`）时判定为「只读不写」。");
                sb.AppendLine("6. **本工具不修改任何内容。** 只读资产与源码，唯一的写操作是导出本报告。");
                sb.AppendLine();

                return sb.ToString();
            }

            private bool FindEnding(string idFragment)
            {
                foreach (EndingCheck e in Endings)
                {
                    if (e.Id != null && e.Id.Contains(idFragment) && e.Unreachable) return true;
                }
                return false;
            }

            private bool FindLocation(string idFragment)
            {
                foreach (LocationCheck l in Locations)
                {
                    if (l.Id != null && l.Id.Contains(idFragment) && !l.Reachable) return true;
                }
                return false;
            }

            /// <summary>该属性的来源数 ≤ 1（含零来源）即视为"来源不足"。</summary>
            private bool FindStatWeak(PlayerStatKey key)
            {
                foreach (StatSourceCheck s in Stats)
                {
                    if (s.Stat == key && s.SourceCount <= 1) return true;
                }
                return false;
            }

            private bool FindRewardType(RewardType type)
            {
                foreach (RewardTypeCheck r in RewardTypes)
                {
                    if (r.Type == type && r.NoSourceOrSink) return true;
                }
                return false;
            }

            private static string Yes(bool value)
            {
                return value ? "✅ 已报出" : "❌ **未报出**";
            }
        }

        public sealed class EndingCheck
        {
            public string Id;
            public string Name;
            public int Priority;
            public bool Unreachable;
            public List<string> Notes = new List<string>();
        }

        public sealed class LocationCheck
        {
            public string Id;
            public string Name;
            public bool UnlockedByDefault;
            public bool Reachable;
            public List<string> Notes = new List<string>();
        }

        public sealed class RewardTypeCheck
        {
            public RewardType Type;
            public int Count;
            public bool NoSourceOrSink;
        }

        public sealed class StatSourceCheck
        {
            public PlayerStatKey Stat;
            public string Name;
            public int Start;
            public int SourceCount;
            public int UpperBound;
            public bool NoSource;
            /// <summary>来源仅 1 处。单点依赖：这处来源一旦被删/被卡，该属性就彻底不涨。</summary>
            public bool IsBottleneck;
            public List<string> ExampleOrigins = new List<string>();
        }
    }
}
