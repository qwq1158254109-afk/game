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
    /// 回答五个问题：
    ///   ① 每条结局是否存在可达路径
    ///   ② 每个地点是否存在满足解锁条件的路径（含"标记位是否有写入点"）
    ///   ③ 每种资源是否存在来源与出口
    ///   ④ 每个属性是否存在至少一个来源
    ///   ⑤ **内容是否真的接进了游戏**——活动资产有没有被场景接线、所在地点是否解锁
    ///
    /// 方法：**不跑完整游戏**，做「上界推算」——把每个属性的所有可及来源增量求和，
    /// 与门槛比较。若上界都够不到门槛，则该结局/地点在数学上不可达。
    /// 这是充分条件而非必要条件：报"不可达"必然为真；报"可达"只代表上界够，
    /// 实战还受时间预算与供给量约束（本工具不负责那一层）。
    ///
    /// ⚠ 口径分两套，报告里并排给出，不静默替换：
    ///   · **纸面上界**：把扫描到的所有来源都算上（含未接线资产、锁死地点内的内容）。
    ///     这是首版口径，会系统性偏乐观。
    ///   · **实机可达上界**：只算「已接线 + 所在地点可达」的来源。做决策请看这个。
    ///
    /// 地点可达性用**不动点迭代**求解，而不是一次性判断——因为存在循环依赖：
    /// 社团活动室的门槛是"兴趣或社交达到 45"，而这两个属性的增量来源全部在社团内部。
    /// 一次性判断会漏掉这类自锁（首版就是这么漏的）。
    ///
    /// 自检：首次运行应报出"已知答案对照"节里的全部已知问题。
    /// 若报不出，说明是工具错了，不是内容没问题。
    /// </summary>
    public static class ContentHealthAudit
    {
        private const string ReportPath = "Docs/ContentHealthReport.md";
        private const int SuccessExitCode = 0;
        private const int FailureExitCode = 1;

        /// <summary>工具自身文件名。源码扫描标记位时必须排除，否则会自我污染判据。</summary>
        private const string SelfFileName = "/ContentHealthAudit.cs";

        /// <summary>地图场景根目录。判断活动资产是否接线时扫描此目录下的 .unity。</summary>
        private const string MapSceneRoot = "Assets/Scenes/StudentGrowth/Maps";

        /// <summary>地点可用性服务的源码路径。用于发现"只读不写的地点标记"。</summary>
        private const string LocationAvailabilityServicePath =
            "Assets/Scripts/StudentGrowth/Map/StageMapLocationAvailabilityService.cs";

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
                  + "｜零来源属性 " + summary.ZeroSourceStatCount
                  + "｜未接线活动 " + summary.UnwiredActivityCount + "）。报告：" + ReportPath;

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
            summary.Locations = CheckLocations(ctx, summary);
            summary.Endings = CheckEndings(ctx, summary);
            summary.RewardTypes = CheckRewardTypes(ctx, summary);
            summary.Stats = CheckStatSources(ctx, summary);
            summary.Activities = CheckActivityReachability(ctx, summary);
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

            // ------------------------------------------------------------------
            // 来源作用域（决定一个来源在"实机可达口径"下算不算数）
            //
            //   来源从未进入作用域        → 全局可用（阶段活动、卡牌、道具、阶段奖励等）
            //   来源进入过 null 作用域    → 全局可用（同上，但确实走过一遍采集）
            //   来源作用域 = 空集         → **未接线**：资产存在但没有任何场景引用它
            //   来源作用域 = {地点…}      → 只有这些地点可达时才算数
            //
            // 同一 ownerId 可能有多条路径（例：同一 ID 有新旧两份资产，一份接线一份没有），
            // 采取**并集**语义——只要有一条路能走到，这个来源就算可用。
            // ------------------------------------------------------------------
            private HashSet<string> _currentScope;
            private readonly HashSet<string> _scopedOwners = new HashSet<string>();
            private readonly HashSet<string> _globalOwners = new HashSet<string>();
            private readonly Dictionary<string, HashSet<string>> _ownerLocations =
                new Dictionary<string, HashSet<string>>();

            // 活动资产的可达性明细（activityId → 明细），供检查 5 使用
            public readonly List<ActivityReachInfo> ActivityReach = new List<ActivityReachInfo>();
            // 场景路径 → 该场景对应的地点 id
            public readonly Dictionary<string, string> SceneToLocation = new Dictionary<string, string>();
            // 活动资产 guid → 引用它的场景路径集合
            public readonly Dictionary<string, List<string>> ActivityScenes = new Dictionary<string, List<string>>();
            // 不动点解出的可达地点集合
            public readonly HashSet<string> ReachableLocationIds = new HashSet<string>();
            // 只读不写的地点标记（名字），以及"当下是否还有活的标记位"
            public readonly List<string> DeadLocationFlags = new List<string>();
            public bool HasLiveLocationFlag;

            private string _currentScopeDescription;

            public List<StatDelta> BuildDeltas()
            {
                return new List<StatDelta>(_deltaByOwnerStat.Values);
            }

            /// <summary>进入一个来源作用域。scope 为 null 表示全局；空集表示"未接线"。</summary>
            public void PushScope(HashSet<string> scope, string description)
            {
                _currentScope = scope;
                _currentScopeDescription = description;
            }

            public void PopScope()
            {
                _currentScope = null;
                _currentScopeDescription = null;
            }

            private void RecordScope(string ownerId)
            {
                if (string.IsNullOrEmpty(ownerId)) return;

                if (_currentScope == null)
                {
                    _globalOwners.Add(ownerId);
                    return;
                }

                _scopedOwners.Add(ownerId);
                HashSet<string> set;
                if (!_ownerLocations.TryGetValue(ownerId, out set))
                {
                    set = new HashSet<string>();
                    _ownerLocations[ownerId] = set;
                }

                foreach (string locationId in _currentScope) set.Add(locationId);
            }

            /// <summary>
            /// 该来源在给定可达地点集合下算不算数。
            /// reachableLocations 为 null 时表示"纸面口径"——不做任何可达性过滤。
            /// </summary>
            public bool IsOwnerReachable(string ownerId, HashSet<string> reachableLocations)
            {
                if (reachableLocations == null) return true;
                if (string.IsNullOrEmpty(ownerId)) return true;
                if (_globalOwners.Contains(ownerId)) return true;
                if (!_scopedOwners.Contains(ownerId)) return true;

                HashSet<string> locations;
                if (!_ownerLocations.TryGetValue(ownerId, out locations) || locations.Count == 0)
                {
                    return false; // 未接线
                }

                foreach (string locationId in locations)
                {
                    if (reachableLocations.Contains(locationId)) return true;
                }
                return false;
            }

            /// <summary>该来源所处的地点集合。返回的集合可能为空（= 未接线），调用方需自行判空。</summary>
            public HashSet<string> OwnerLocationIds(string ownerId)
            {
                HashSet<string> set;
                if (ownerId != null && _ownerLocations.TryGetValue(ownerId, out set)) return set;
                return null;
            }

            /// <summary>记入一条属性增量。同一 (ownerId, 属性) 重复出现时取绝对值较大者。</summary>
            public void Accumulate(string ownerId, PlayerStatKey stat, int value, string origin)
            {
                if (value == 0) return;
                RecordScope(ownerId);
                string key = (ownerId ?? string.Empty) + "|" + (int)stat;
                StatDelta existing;
                if (_deltaByOwnerStat.TryGetValue(key, out existing))
                {
                    if (Mathf.Abs(value) > Mathf.Abs(existing.Value))
                    {
                        _deltaByOwnerStat[key] = new StatDelta(stat, value, origin, ownerId, existing.ScopeDescription ?? _currentScopeDescription);
                    }
                    return;
                }
                _deltaByOwnerStat[key] = new StatDelta(stat, value, origin, ownerId, _currentScopeDescription);
            }

            public List<StageData> Stages = new List<StageData>();
            public List<LocationData> Locations = new List<LocationData>();
            public List<EndingData> Endings = new List<EndingData>();

            public int BossCount;
            public int StageCount;

            public void CollectAll()
            {
                // ⚠ 顺序有依赖，不能换：
                //   1) 先采运行时内容 —— 拿到 Stages/Locations/Endings
                //   2) 再建接线索引 —— 它要遍历 Stages 去读阶段地图配置
                //   3) 然后才采资产内容 —— 资产活动要用接线索引算作用域
                CollectRuntimeContent();
                BuildSceneWiringIndex();
                CollectAssetContent();
                CollectCardAndItemEffects();
                ResolveLocationReachability();
                CountBosses();
            }

            // ---- 0) 场景接线索引：活动资产到底有没有被挂到场景的交互点上 ----
            //
            // 交互的真实机制是场景里的 MapInteractionTarget 组件，它用 fallbackActivity
            // 直接指向 ActivityData 资产（locationAction 字段在本项目里全是空的，
            // 也就是说 LocationActionData 这个类实际没参与任何交互）。
            // 结论：**资产写完不等于内容可用**——没被任何场景引用的资产，玩家永远看不到。
            private void BuildSceneWiringIndex()
            {
                // 1) 场景 → 地点。来源是阶段地图配置（每个阶段各有一份 StageMapData）。
                foreach (StageData stage in Stages)
                {
                    if (stage == null) continue;
                    StageMapData mapData = MvpSampleDataFactory.CreateDefaultStageMapData(stage);
                    if (mapData == null) continue;

                    foreach (StageMapLocationEntry entry in mapData.locations)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.sceneName)) continue;
                        string path = NormalizeScenePath(entry.sceneName);
                        if (!SceneToLocation.ContainsKey(path)) SceneToLocation[path] = entry.locationId;
                    }

                    UnityEngine.Object.DestroyImmediate(mapData);
                }

                // 2) 活动资产 guid → 引用它的场景
                var guidsByPath = new Dictionary<string, string>();
                foreach (UnityEngine.Object obj in LoadScriptableObjects())
                {
                    ActivityData activity = obj as ActivityData;
                    if (activity == null) continue;
                    string path = AssetDatabase.GetAssetPath(activity);
                    string guid = AssetDatabase.AssetPathToGUID(path);
                    if (string.IsNullOrEmpty(guid)) continue;
                    guidsByPath[path] = guid;
                }

                if (!Directory.Exists(MapSceneRoot))
                {
                    DataSourceNotes.Add("⚠ 未找到地图场景目录 " + MapSceneRoot + "，无法判断活动资产是否接线");
                    return;
                }

                int wired = 0;
                int scannedScenes = 0;
                int skippedScenes = 0;
                string[] sceneFiles = Directory.GetFiles(MapSceneRoot, "*.unity", SearchOption.AllDirectories);
                foreach (string sceneFile in sceneFiles)
                {
                    string normalized = sceneFile.Replace('\\', '/');
                    // 跳过备份与崩溃恢复目录：那里的引用不代表内容已经上线
                    if (normalized.Contains("/Backups/") || normalized.Contains("/_Recovery/"))
                    {
                        skippedScenes++;
                        continue;
                    }
                    scannedScenes++;

                    string text;
                    try { text = File.ReadAllText(sceneFile); }
                    catch { continue; }

                    // 按 YAML 文档块切开，只认含 MapInteractionTarget 的块——
                    // 避免"资产 guid 恰好出现在别处"造成的误判
                    string[] blocks = text.Split(new[] { "--- " }, StringSplitOptions.None);
                    foreach (string block in blocks)
                    {
                        if (block.IndexOf("MapInteractionTarget", StringComparison.Ordinal) < 0) continue;

                        foreach (KeyValuePair<string, string> pair in guidsByPath)
                        {
                            if (block.IndexOf(pair.Value, StringComparison.Ordinal) < 0) continue;

                            List<string> scenes;
                            if (!ActivityScenes.TryGetValue(pair.Key, out scenes))
                            {
                                scenes = new List<string>();
                                ActivityScenes[pair.Key] = scenes;
                            }
                            if (!scenes.Contains(normalized)) scenes.Add(normalized);
                            wired++;
                        }
                    }
                }

                DataSourceNotes.Add("场景接线索引：扫描地图场景 " + scannedScenes + " 个"
                    + (skippedScenes > 0 ? "（另跳过 Backups/_Recovery 下的 " + skippedScenes + " 个）" : "")
                    + "，找到 " + ActivityScenes.Count + " 个活动资产被 MapInteractionTarget 引用（合计 " + wired + " 处引用）");
            }

            /// <summary>把阶段地图里的 sceneName 统一成 Assets 下的完整场景路径。</summary>
            public static string NormalizeScenePath(string sceneName)
            {
                if (string.IsNullOrEmpty(sceneName)) return string.Empty;
                if (sceneName.IndexOf('/') >= 0) return sceneName.Replace('\\', '/');
                return MapSceneRoot + "/" + sceneName + ".unity";
            }

            /// <summary>活动资产路径 → 它所在的地点集合（未接线时返回空集）。</summary>
            public HashSet<string> ScopeOfActivityAsset(string assetPath)
            {
                var result = new HashSet<string>();
                List<string> scenes;
                if (assetPath == null || !ActivityScenes.TryGetValue(assetPath, out scenes)) return result;

                foreach (string scene in scenes)
                {
                    string locationId;
                    if (SceneToLocation.TryGetValue(scene, out locationId) && !string.IsNullOrEmpty(locationId))
                    {
                        result.Add(locationId);
                    }
                }
                return result;
            }

            /// <summary>
            /// 地点可达性的不动点迭代。
            ///
            /// 为什么要迭代：地点门槛可能引用属性，而属性的来源又在别的地点里。
            /// 一次性判断会漏掉"门槛靠锁死地点内的内容才够"这种自锁（社团活动室就是这个病）。
            /// 起点 = 只算全局来源与默认解锁地点内的来源，然后反复"能开就开、重算边界"，直到不再变化。
            /// </summary>
            private void ResolveLocationReachability()
            {
                Dictionary<string, LocationGate> gates = BuildLocationGates();

                const int MaxIterations = 8;
                for (int iteration = 0; iteration < MaxIterations; iteration++)
                {
                    StatBounds bounds = StatBounds.Compute(this, ReachableLocationIds);
                    bool changed = false;

                    foreach (LocationGate gate in gates.Values)
                    {
                        if (gate.Reachable) continue;
                        if (!EvaluateLocationGate(gate, bounds)) continue;
                        gate.Reachable = true;
                        ReachableLocationIds.Add(gate.LocationId);
                        changed = true;
                    }

                    if (!changed)
                    {
                        DataSourceNotes.Add("地点可达性不动点：迭代 " + (iteration + 1) + " 轮后收敛，可达地点 "
                            + ReachableLocationIds.Count + " / " + gates.Count);
                        break;
                    }
                }

                foreach (LocationGate gate in gates.Values) LocationGates[gate.LocationId] = gate;
            }

            /// <summary>地点 id → 门槛明细。检查 2 直接读它，不再自己判一遍。</summary>
            public readonly Dictionary<string, LocationGate> LocationGates = new Dictionary<string, LocationGate>();

            private Dictionary<string, LocationGate> BuildLocationGates()
            {
                var gates = new Dictionary<string, LocationGate>();

                foreach (LocationData location in Locations)
                {
                    if (location == null) continue;
                    gates[location.locationId] = new LocationGate
                    {
                        LocationId = location.locationId,
                        DisplayName = location.displayName,
                        UnlockedByDefault = location.unlockedByDefault,
                        SceneEntryConditions = location.unlockConditions != null
                            ? new List<EventConditionData>(location.unlockConditions)
                            : new List<EventConditionData>()
                    };
                }

                // 阶段地图上的门槛（StageMapLocationAvailabilityService 读的那一套 key）
                foreach (StageData stage in Stages)
                {
                    if (stage == null) continue;
                    StageMapData mapData = MvpSampleDataFactory.CreateDefaultStageMapData(stage);
                    if (mapData == null) continue;

                    foreach (StageMapLocationEntry entry in mapData.locations)
                    {
                        if (entry == null || string.IsNullOrEmpty(entry.locationId)) continue;

                        LocationGate gate;
                        if (!gates.TryGetValue(entry.locationId, out gate))
                        {
                            gate = new LocationGate { LocationId = entry.locationId, DisplayName = entry.displayName };
                            gates[entry.locationId] = gate;
                        }
                        if (string.IsNullOrEmpty(gate.DisplayName)) gate.DisplayName = entry.displayName;

                        gate.StageEntries.Add(stage.stageId + " = " + entry.defaultStatus);
                        if (entry.defaultStatus == StageMapLocationStatus.Unlocked) gate.UnlockedInSomeStage = true;
                        if (!string.IsNullOrEmpty(entry.unlockConditionKey)
                            && !gate.ConditionKeys.Contains(entry.unlockConditionKey))
                        {
                            gate.ConditionKeys.Add(entry.unlockConditionKey);
                        }
                        if (!string.IsNullOrEmpty(entry.lockedReason)
                            && !gate.LockedReasons.Contains(entry.lockedReason))
                        {
                            gate.LockedReasons.Add(entry.lockedReason);
                        }
                    }

                    UnityEngine.Object.DestroyImmediate(mapData);
                }

                // 只读不写的地点标记：标记位分支一旦没有写入点，这条解锁路径就是死的
                FindLocationFlags();

                // 评估默认解锁的地点（不依赖边界，第一轮就会全部点亮）
                foreach (LocationGate gate in gates.Values)
                {
                    if (gate.UnlockedInSomeStage && gate.UnlockedByDefault)
                    {
                        gate.Reachable = true;
                        ReachableLocationIds.Add(gate.LocationId);
                    }
                }

                return gates;
            }

            /// <summary>
            /// 扫描地点可用性服务，找出它读取的地点标记，并判断每个标记有没有写入点。
            /// 读的是 `locationFlags.TryGetValue("xxx"` 里的字面量。
            /// </summary>
            private void FindLocationFlags()
            {
                if (!File.Exists(LocationAvailabilityServicePath))
                {
                    DataSourceNotes.Add("⚠ 未找到 " + LocationAvailabilityServicePath + "，跳过地点标记检查");
                    return;
                }

                string text;
                try { text = File.ReadAllText(LocationAvailabilityServicePath); }
                catch { return; }

                var flags = new List<string>();
                const string marker = "locationFlags.TryGetValue(";
                int index = text.IndexOf(marker, StringComparison.Ordinal);
                while (index >= 0)
                {
                    int quoteStart = text.IndexOf('"', index + marker.Length);
                    if (quoteStart >= 0)
                    {
                        int quoteEnd = text.IndexOf('"', quoteStart + 1);
                        if (quoteEnd > quoteStart)
                        {
                            string flag = text.Substring(quoteStart + 1, quoteEnd - quoteStart - 1);
                            if (!string.IsNullOrEmpty(flag) && !flags.Contains(flag)) flags.Add(flag);
                        }
                    }
                    index = text.IndexOf(marker, index + marker.Length, StringComparison.Ordinal);
                }

                foreach (string flag in flags)
                {
                    List<string> hits = FindSourceReferences(flag);
                    // FindSourceReferences 已排除本工具自身；读取点自身不算写入点
                    int writers = hits.Count(h => h != LocationAvailabilityServicePath);
                    if (writers == 0)
                    {
                        DeadLocationFlags.Add(flag);
                    }
                    else
                    {
                        HasLiveLocationFlag = true;
                    }
                }
            }

            private bool EvaluateLocationGate(LocationGate gate, StatBounds bounds)
            {
                // 不动点会反复评估同一个门，先清空上一轮的结论，否则报告里原因会重复堆叠
                gate.BlockingReasons.Clear();

                // 门一：阶段地图上的 key
                bool stageKeyOpen = false;
                if (gate.UnlockedInSomeStage) stageKeyOpen = true;

                foreach (string key in gate.ConditionKeys)
                {
                    if (stageKeyOpen) break;
                    if (key == StageMapLocationAvailabilityService.Stage2OrLaterCondition)
                    {
                        stageKeyOpen = Stages.Count >= 2;
                    }
                    else if (key == StageMapLocationAvailabilityService.Stage3OrLaterCondition)
                    {
                        stageKeyOpen = Stages.Count >= 3;
                    }
                    else
                    {
                        // 本工具只认得上面两个阶段键 + 场景入口上的属性条件。
                        // 出现别的 key 一律按不可达处理，并把这句话写进报告——
                        // 免得下一个人以为"没报错就是没问题"。
                        gate.BlockingReasons.Add("解锁键 <code>" + key
                            + "</code> 不在本工具已知语义内（只认 stage_02_or_later / stage_03_or_later，"
                            + "以及场景入口上的属性条件）→ 无法判定，按不可达处理。"
                            + "请确认这个键是在哪里被消费的，并把它的语义补进本工具。");
                    }
                }

                if (gate.ConditionKeys.Count == 0 && !gate.UnlockedInSomeStage)
                {
                    gate.BlockingReasons.Add("阶段地图上所有阶段都是锁定态，但没有配置任何解锁键");
                }

                // 门二：进入场景时的条件（SceneTransitionController.IsUnlocked 的口径）
                bool sceneGateOpen = true;
                if (!gate.UnlockedByDefault)
                {
                    if (gate.SceneEntryConditions.Count == 0)
                    {
                        sceneGateOpen = false;
                        gate.BlockingReasons.Add("非默认解锁，且场景入口未配置 unlockConditions");
                    }
                    else
                    {
                        foreach (EventConditionData condition in gate.SceneEntryConditions)
                        {
                            if (condition == null) continue;
                            string reason = EvaluateCondition(condition, bounds);
                            if (reason == null) continue;
                            sceneGateOpen = false;
                            gate.BlockingReasons.Add("场景入口条件未满足：" + reason);
                        }
                    }
                }

                return stageKeyOpen && sceneGateOpen;
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
                            // 阶段活动菜单里的活动不依赖地点，全局可用 → 作用域保持 null
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
                        string assetPath = AssetDatabase.GetAssetPath(activity);
                        RegisterId(activity.activityId, "ActivityData(资产)", assetPath);

                        // ⚠ 关键一步：资产活动要带上"它在哪个地点"的作用域。
                        // 未接线的资产作用域是**空集**，在实机可达口径下整份不计入——
                        // 这正是首版把学校/图书馆那些没接线的资产当成可用来源的原因。
                        HashSet<string> scope = ScopeOfActivityAsset(assetPath);
                        PushScope(scope, assetPath);

                        CollectActivity(activity, "活动资产", assetPath);

                        PopScope();

                        ActivityReach.Add(new ActivityReachInfo
                        {
                            ActivityId = activity.activityId,
                            DisplayName = activity.displayName,
                            AssetPath = assetPath,
                            LocationIds = new List<string>(scope)
                        });
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
            StatBounds liveBounds = StatBounds.Compute(ctx, ctx.ReachableLocationIds);
            StatBounds paperBounds = StatBounds.Compute(ctx, null);

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

                check.Unreachable = EvaluateEnding(ending, liveBounds, ctx, check.Notes);

                // 口径差异：纸面够、实机不够 —— 这正是"资产写完没接线"最容易骗过评审的地方
                if (check.Unreachable)
                {
                    var paperNotes = new List<string>();
                    bool paperUnreachable = EvaluateEnding(ending, paperBounds, ctx, paperNotes);
                    if (!paperUnreachable)
                    {
                        check.PaperOnlyReachable = true;
                        check.Notes.Add("⚠ 纸面口径会误判为「可达」——够到门槛的那些来源里有未接线资产或锁死地点内的内容，实机拿不到");
                    }
                }

                results.Add(check);
            }

            summary.UnreachableEndingCount = results.Count(r => r.Unreachable);
            return results;
        }

        /// <summary>返回 true 表示该结局在给定边界下不可达；不可达原因写入 notes。</summary>
        private static bool EvaluateEnding(EndingData ending, StatBounds bounds, AuditContext ctx, List<string> notes)
        {
            bool unreachable = false;

            if (ending.minimumClearedStages > ctx.StageCount)
            {
                unreachable = true;
                notes.Add("要求通关阶段数 " + ending.minimumClearedStages + " > 实际阶段数 " + ctx.StageCount);
            }

            if (ending.minimumDefeatedBosses > ctx.BossCount)
            {
                unreachable = true;
                notes.Add("要求击败 Boss 数 " + ending.minimumDefeatedBosses + " > 实际 Boss 数 " + ctx.BossCount);
            }

            int maxAvg = (bounds.Max(PlayerStatKey.Chinese) + bounds.Max(PlayerStatKey.Math) + bounds.Max(PlayerStatKey.English)) / 3;
            if (ending.minimumSubjectAverage > maxAvg)
            {
                unreachable = true;
                notes.Add("要求三科均值 ≥ " + ending.minimumSubjectAverage + "，上界仅 " + maxAvg);
            }

            if (ending.conditions != null)
            {
                foreach (EventConditionData condition in ending.conditions)
                {
                    if (condition == null) continue;
                    string reason = EvaluateCondition(condition, bounds);
                    if (reason != null)
                    {
                        unreachable = true;
                        notes.Add(reason);
                    }
                }
            }

            return unreachable;
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

            foreach (LocationData location in ctx.Locations)
            {
                if (location == null) continue;

                LocationGate gate;
                if (!ctx.LocationGates.TryGetValue(location.locationId, out gate))
                {
                    results.Add(new LocationCheck
                    {
                        Id = location.locationId,
                        Name = location.displayName,
                        UnlockedByDefault = location.unlockedByDefault,
                        Reachable = location.unlockedByDefault,
                        Notes = new List<string> { "⚠ 阶段地图上没有这个地点的配置（玩家在阶段地图上根本看不到它）" }
                    });
                    continue;
                }

                var check = new LocationCheck
                {
                    Id = gate.LocationId,
                    Name = string.IsNullOrEmpty(gate.DisplayName) ? location.displayName : gate.DisplayName,
                    UnlockedByDefault = gate.UnlockedByDefault,
                    Reachable = gate.Reachable,
                    Notes = new List<string>()
                };

                if (gate.StageEntries.Count > 0)
                {
                    check.Notes.Add("阶段地图状态：" + string.Join("｜", gate.StageEntries));
                }

                if (gate.Reachable)
                {
                    check.Notes.Add(gate.UnlockedInSomeStage
                        ? "阶段地图上可直接进入"
                        : "门槛在实机可达边界下可满足");
                }
                else
                {
                    foreach (string reason in gate.BlockingReasons) check.Notes.Add(reason);

                    // 标记位分支：服务里读的标记若全无写入点，这条分支在实机里永远为假。
                    // 报告要把它说清，否则下一个人会以为"补个写入点就好了"。
                    if (ctx.DeadLocationFlags.Count > 0 && !ctx.HasLiveLocationFlag)
                    {
                        check.Notes.Add("本工程读取的地点标记 <code>" + string.Join("、", ctx.DeadLocationFlags)
                            + "</code> 全部只有读取点、没有写入点 → 靠标记位解锁这条分支恒为假，"
                            + "这个地点只能靠阶段键或场景入口条件解锁");
                    }

                    int lockedActivities = CountWiredActivities(ctx, gate.LocationId);
                    if (lockedActivities > 0)
                    {
                        check.Notes.Add("⚠ 这个锁死的地点里有 **" + lockedActivities + " 个已接线活动**，玩家永远看不到");
                    }
                }

                results.Add(check);
            }

            // 循环依赖检测：某地点的门槛属性，其来源是否全部锁在位置不可达的地点里
            DetectCircularGate(ctx, results);

            summary.UnreachableLocationCount = results.Count(r => !r.Reachable);
            return results;
        }

        /// <summary>数一数某地点里接了多少个活动资产。</summary>
        private static int CountWiredActivities(AuditContext ctx, string locationId)
        {
            int count = 0;
            foreach (ActivityReachInfo info in ctx.ActivityReach)
            {
                if (info.LocationIds.Contains(locationId)) count++;
            }
            return count;
        }

        /// <summary>
        /// 循环依赖检测：地点 A 的门槛用到属性 X，而属性 X 的增量来源有一部分**就锁在 A 自己里面**
        /// （或锁在其它同样不可达的地点里），门槛就永远开不了。
        /// 首版没做这一步，所以只报出「标记位没写入点」这个表象，没报出真正的自锁。
        ///
        /// 判据是通用的：门槛属性取自该地点场景入口上的 `unlockConditions`，不针对某个具体地点。
        /// </summary>
        private static void DetectCircularGate(AuditContext ctx, List<LocationCheck> results)
        {
            foreach (LocationGate gate in ctx.LocationGates.Values)
            {
                if (gate.Reachable) continue;
                if (gate.SceneEntryConditions.Count == 0) continue;

                var gateStats = new HashSet<PlayerStatKey>();
                foreach (EventConditionData condition in gate.SceneEntryConditions)
                {
                    if (condition != null) gateStats.Add(condition.statKey);
                }
                if (gateStats.Count == 0) continue;

                int total = 0;
                int lockedSelf = 0;
                int lockedElsewhere = 0;

                foreach (StatDelta delta in ctx.BuildDeltas())
                {
                    if (!gateStats.Contains(delta.Stat)) continue;
                    if (delta.Value <= 0) continue;
                    total++;

                    if (ctx.IsOwnerReachable(delta.OwnerId, ctx.ReachableLocationIds)) continue;

                    HashSet<string> ownerLocations = ctx.OwnerLocationIds(delta.OwnerId);
                    if (ownerLocations != null && ownerLocations.Contains(gate.LocationId)) lockedSelf++;
                    else lockedElsewhere++;
                }

                LocationCheck check = FindCheck(results, gate.LocationId);
                if (check == null || lockedSelf + lockedElsewhere == 0) continue;

                if (lockedSelf > 0)
                {
                    check.Notes.Add("⚠⚠ **循环依赖（自锁）**：这道门用到的属性共 " + total + " 处正增量来源，"
                        + "其中 **" + lockedSelf + " 处就锁在「"
                        + (string.IsNullOrEmpty(gate.DisplayName) ? gate.LocationId : gate.DisplayName) + "」自己里面**"
                        + (lockedElsewhere > 0 ? "，另有 " + lockedElsewhere + " 处锁在别处" : "")
                        + "。进不去就涨不了这些属性，涨不了就进不去——"
                        + "这不是「缺一个标记位写入点」的问题，是门槛设计自己闭环了。");
                }
            }
        }

        private static LocationCheck FindCheck(List<LocationCheck> checks, string locationId)
        {
            foreach (LocationCheck check in checks)
            {
                if (check.Id == locationId) return check;
            }
            return null;
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
                    // 跳过工具自身：本文件的源码里也会出现被检查的字面量（诊断信息、注释里引用的键名），
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
            StatBounds liveBounds = StatBounds.Compute(ctx, ctx.ReachableLocationIds);
            StatBounds paperBounds = StatBounds.Compute(ctx, null);
            List<StatDelta> deltas = ctx.BuildDeltas();

            foreach (PlayerStatKey key in Enum.GetValues(typeof(PlayerStatKey)))
            {
                List<StatDelta> sources = deltas.Where(d => d.Stat == key && d.Value > 0).ToList();
                List<StatDelta> liveSources = sources
                    .Where(d => ctx.IsOwnerReachable(d.OwnerId, ctx.ReachableLocationIds))
                    .ToList();

                results.Add(new StatSourceCheck
                {
                    Stat = key,
                    Name = StatLabel(key),
                    Start = liveBounds.Start(key),
                    SourceCount = liveSources.Count,
                    PaperSourceCount = sources.Count,
                    UpperBound = liveBounds.Max(key),
                    PaperUpperBound = paperBounds.Max(key),
                    NoSource = liveSources.Count == 0,
                    IsBottleneck = liveSources.Count == 1,
                    ExampleOrigins = liveSources.Take(3).Select(s => s.Origin).ToList(),
                    PaperOnlyOrigins = sources
                        .Where(s => !ctx.IsOwnerReachable(s.OwnerId, ctx.ReachableLocationIds))
                        .Take(3).Select(s => s.Origin).ToList()
                });
            }

            summary.ZeroSourceStatCount = results.Count(r => r.NoSource);
            summary.BottleneckStatCount = results.Count(r => r.IsBottleneck);
            return results;
        }

        // ------------------------------------------------------------------
        // 检查 5：活动可达性（资产写完 ≠ 内容可用）
        // ------------------------------------------------------------------

        private static List<ActivityCheck> CheckActivityReachability(AuditContext ctx, AuditSummary summary)
        {
            var results = new List<ActivityCheck>();

            foreach (ActivityReachInfo info in ctx.ActivityReach)
            {
                var check = new ActivityCheck
                {
                    ActivityId = info.ActivityId,
                    DisplayName = info.DisplayName,
                    AssetPath = info.AssetPath,
                    LocationText = info.LocationText,
                    WiredToScene = info.WiredToScene,
                    Notes = new List<string>()
                };

                if (!info.WiredToScene)
                {
                    check.Reachable = false;
                    check.Notes.Add("没有被任何地图场景的 MapInteractionTarget 引用 → 玩家永远看不到这个活动（资产写完没接线）");
                }
                else
                {
                    bool anyReachable = false;
                    foreach (string locationId in info.LocationIds)
                    {
                        if (ctx.ReachableLocationIds.Contains(locationId)) anyReachable = true;
                    }
                    check.Reachable = anyReachable;
                    if (!anyReachable)
                    {
                        check.Notes.Add("已接线，但所在地点在实机里不可达 → 内容被锁死");
                    }
                }

                info.Reachable = check.Reachable;
                results.Add(check);
            }

            // 地点空转：阶段地图上显示这个地点，但里面一个已接线活动都没有
            foreach (LocationData location in ctx.Locations)
            {
                if (location == null) continue;
                int wired = CountWiredActivities(ctx, location.locationId);
                summary.WiredCountByLocation[location.locationId] = wired;
                if (wired == 0)
                {
                    summary.EmptyLocations.Add(location.locationId + "（" + location.displayName + "）");
                }
            }

            summary.UnreachableActivityCount = results.Count(r => !r.Reachable);
            summary.UnwiredActivityCount = results.Count(r => !r.WiredToScene);
            summary.WiredActivityCount = results.Count(r => r.WiredToScene);
            summary.ReachableActivityCount = results.Count(r => r.Reachable);
            summary.DistinctTotalActivityCount = ctx.ActivityReach
                .Select(a => a.ActivityId).Distinct().Count();
            summary.DistinctReachableActivityCount = ctx.ActivityReach
                .Where(a => a.Reachable).Select(a => a.ActivityId).Distinct().Count();
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
                return Compute(ctx, null);
            }

            /// <summary>
            /// reachableLocations 为 null 时是「纸面口径」——所有扫描到的来源都算数；
            /// 传入可达地点集合时是「实机可达口径」——未接线资产与锁死地点内的来源会被剔除。
            /// </summary>
            public static StatBounds Compute(AuditContext ctx, HashSet<string> reachableLocations)
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
                        if (!ctx.IsOwnerReachable(delta.OwnerId, reachableLocations)) continue;
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
            /// <summary>来源 ID。用于按可达性过滤——同一个 ID 的所有增量共享一个作用域。</summary>
            public readonly string OwnerId;
            /// <summary>采集时所在的作用域描述（资产路径 / 地点），仅用于报告里说清"这条从哪来"。</summary>
            public readonly string ScopeDescription;

            public StatDelta(PlayerStatKey stat, int value, string origin)
                : this(stat, value, origin, null, null)
            {
            }

            public StatDelta(PlayerStatKey stat, int value, string origin, string ownerId, string scopeDescription)
            {
                Stat = stat;
                Value = value;
                Origin = origin;
                OwnerId = ownerId;
                ScopeDescription = scopeDescription;
            }
        }

        /// <summary>一个地点的解锁门槛明细（阶段地图门槛 + 场景入口条件）。</summary>
        public sealed class LocationGate
        {
            public string LocationId;
            public string DisplayName;
            public bool UnlockedByDefault;
            public bool UnlockedInSomeStage;
            public bool Reachable;
            /// <summary>各阶段地图上的状态，例："stage_01_adaptation = Locked"。</summary>
            public List<string> StageEntries = new List<string>();
            public List<string> ConditionKeys = new List<string>();
            public List<string> LockedReasons = new List<string>();
            public List<string> BlockingReasons = new List<string>();
            public List<EventConditionData> SceneEntryConditions = new List<EventConditionData>();
        }

        /// <summary>一个活动资产的可达性明细。</summary>
        public sealed class ActivityReachInfo
        {
            public string ActivityId;
            public string DisplayName;
            public string AssetPath;
            public List<string> LocationIds = new List<string>();
            /// <summary>由检查 5 回填：该活动在实机里是否可达。</summary>
            public bool Reachable;

            public bool WiredToScene { get { return LocationIds.Count > 0; } }
            public string LocationText
            {
                get { return LocationIds.Count == 0 ? "—" : string.Join("、", LocationIds); }
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
            public List<ActivityCheck> Activities = new List<ActivityCheck>();
            public List<string> ExtraFindings = new List<string>();
            /// <summary>阶段地图上能看到、但里面一个已接线活动都没有的地点。</summary>
            public List<string> EmptyLocations = new List<string>();
            /// <summary>地点 id → 该地点里已接线（= 玩家可能看到）的活动数。</summary>
            public Dictionary<string, int> WiredCountByLocation = new Dictionary<string, int>();

            public int UnreachableEndingCount;
            public int UnreachableLocationCount;
            public int EmptyRewardTypeCount;
            public int ZeroSourceStatCount;
            public int BottleneckStatCount;

            // 活动可达性
            public int UnreachableActivityCount;
            public int UnwiredActivityCount;
            public int WiredActivityCount;
            public int ReachableActivityCount;
            public int DistinctTotalActivityCount;
            public int DistinctReachableActivityCount;

            public int BlockingCount
            {
                get
                {
                    return UnreachableEndingCount + UnreachableLocationCount
                        + ZeroSourceStatCount + UnwiredActivityCount;
                }
            }

            public string BuildMarkdown()
            {
                var sb = new StringBuilder();
                sb.AppendLine("# 内容体检报告");
                sb.AppendLine();
                sb.AppendLine("> 由 `ContentHealthAudit` 生成（工具位于 `Assets/Scripts/StudentGrowth/Editor/ContentHealthAudit.cs`）");
                sb.AppendLine("> 生成时间：" + GeneratedAt);
                sb.AppendLine();
                sb.AppendLine("> **口径分两套，并排给出，不静默替换。**");
                sb.AppendLine("> 「纸面上界」= 把扫描到的所有来源都算上（含未接线资产、锁死地点内的内容），首版口径，系统性偏乐观。");
                sb.AppendLine("> 「实机上界」= 只算「已接线 + 所在地点可达」的来源。**做决策请看这一列。**");
                sb.AppendLine();

                // 0. 结论
                sb.AppendLine("## 0. 结论速览");
                sb.AppendLine();
                sb.AppendLine("| 检查项 | 结果 |");
                sb.AppendLine("| --- | --- |");
                sb.AppendLine("| 不可达结局（实机口径） | **" + UnreachableEndingCount + " / " + Endings.Count + "** |");
                sb.AppendLine("| 不可达地点 | **" + UnreachableLocationCount + " / " + Locations.Count + "** |");
                sb.AppendLine("| 零来源的属性（实机口径） | **" + ZeroSourceStatCount + " / " + Stats.Count + "** |");
                sb.AppendLine("| 瓶颈属性（实机来源仅 1 处） | **" + BottleneckStatCount + " / " + Stats.Count + "** |");
                sb.AppendLine("| 无来源或无出口的资源类型 | **" + EmptyRewardTypeCount + " / " + RewardTypes.Count + "** |");
                sb.AppendLine("| **未接线的活动资产**（写完没挂进场景） | **" + UnwiredActivityCount + " / " + Activities.Count + "** |");
                sb.AppendLine("| **实机不可达的活动**（未接线 + 地点锁死） | **" + UnreachableActivityCount + " / " + Activities.Count + "** |");
                sb.AppendLine("| 实机可达活动（按 activityId 去重） | **" + DistinctReachableActivityCount + " / " + DistinctTotalActivityCount + "** |");
                sb.AppendLine("| 阶段地图上无任何活动的地点 | **" + EmptyLocations.Count + "**（" + (EmptyLocations.Count == 0 ? "—" : string.Join("、", EmptyLocations)) + "） |");
                sb.AppendLine("| **阻塞级问题合计** | **" + BlockingCount + "** |");
                sb.AppendLine();
                sb.AppendLine("> 「阻塞级」= 不可达结局 + 不可达地点 + 零来源属性 + **未接线活动**。");
                sb.AppendLine("> 资源类型为空通常是设计选择（如本作尚未实现删牌），单列不计数。");
                sb.AppendLine("> 「实机不可达的活动」不重复计入阻塞级——其中因地点锁死而不可达的部分，已经由「不可达地点」计过一次。");
                sb.AppendLine();

                // 数据源
                sb.AppendLine("## 1. 数据源清点");
                sb.AppendLine();
                foreach (string note in DataSources) sb.AppendLine("- " + note);
                sb.AppendLine();

                // 2. 结局
                sb.AppendLine("## 2. 结局可达性");
                sb.AppendLine();
                sb.AppendLine("判定方法：把每个门槛属性的**实机可达来源增量求和**得到上界；上界够不到门槛即判不可达。");
                sb.AppendLine("有 `⚠ 纸面口径会误判` 标记的结局，是指用「把所有扫描到的来源都算上」的口径会判成可达——"
                    + "差异来自未接线资产或锁死地点内的内容。");
                sb.AppendLine();
                sb.AppendLine("| 结局 | 优先级 | 实机判定 | 原因 |");
                sb.AppendLine("| --- | ---: | --- | --- |");
                foreach (EndingCheck ending in Endings)
                {
                    sb.AppendLine("| " + ending.Name + "<br><code>" + ending.Id + "</code> | " + ending.Priority + " | "
                        + (ending.Unreachable ? (ending.PaperOnlyReachable ? "**不可达** ⚠ 纸面口径会误判" : "**不可达**") : "可达（上界）") + " | "
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
                sb.AppendLine("「实机来源数」只统计已接线且所在地点可达的来源；「纸面来源数」把未接线资产也算上。"
                    + "两列差距大的属性，说明它的增量大多挂在玩家碰不到的内容上。");
                sb.AppendLine();
                sb.AppendLine("| 属性 | 初始值 | 实机来源数 | 纸面来源数 | 实机上界 | 纸面上界 | 判定 | 来源示例（实机） |");
                sb.AppendLine("| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |");
                foreach (StatSourceCheck check in Stats)
                {
                    string verdict = check.NoSource
                        ? "**零来源**"
                        : (check.IsBottleneck ? "**仅 1 处来源（瓶颈）**" : "有");
                    if (!check.NoSource && check.PaperSourceCount > check.SourceCount)
                    {
                        verdict += "（纸面多 " + (check.PaperSourceCount - check.SourceCount) + " 处不可达）";
                    }
                    if (check.NoSource && check.PaperSourceCount > 0)
                    {
                        verdict = "**实机零来源**（纸面有 " + check.PaperSourceCount + " 处，全不可达）";
                    }

                    sb.AppendLine("| " + check.Name + " | " + check.Start + " | " + check.SourceCount + " | "
                        + check.PaperSourceCount + " | " + check.UpperBound + " | " + check.PaperUpperBound + " | "
                        + verdict + " | "
                        + (check.ExampleOrigins.Count == 0 ? "—" : string.Join("<br>", check.ExampleOrigins)) + " |");
                }
                sb.AppendLine();
                sb.AppendLine("> 不可达的来源明细（举例）：");
                sb.AppendLine();
                bool anyPaperOnly = false;
                foreach (StatSourceCheck check in Stats)
                {
                    if (check.PaperOnlyOrigins.Count == 0) continue;
                    anyPaperOnly = true;
                    sb.AppendLine("- **" + check.Name + "**：" + string.Join("；", check.PaperOnlyOrigins));
                }
                if (!anyPaperOnly) sb.AppendLine("- 无。");
                sb.AppendLine();

                // 6. 活动可达性
                sb.AppendLine("## 6. 活动可达性（资产写完 ≠ 内容可用）");
                sb.AppendLine();
                sb.AppendLine("判定方法：活动资产必须被某个地图场景的 `MapInteractionTarget` 引用（= 接线），"
                    + "且它所在的地点在实机里可达，玩家才可能看到这个活动。");
                sb.AppendLine("交互的真实机制走的是场景里的 `MapInteractionTarget.fallbackActivity` ——"
                    + "`LocationActionData`（`locationAction` 字段）在本项目里全是空的，实际没参与任何交互。");
                sb.AppendLine();
                sb.AppendLine("| 活动 | activityId | 所在地点 | 场景接线 | 实机可达 | 备注 |");
                sb.AppendLine("| --- | --- | --- | --- | --- | --- |");
                foreach (ActivityCheck activity in Activities)
                {
                    sb.AppendLine("| " + (string.IsNullOrEmpty(activity.DisplayName) ? "—" : activity.DisplayName)
                        + " | <code>" + activity.ActivityId + "</code> | " + activity.LocationText + " | "
                        + (activity.WiredToScene ? "✅" : "❌ 未接线") + " | "
                        + (activity.Reachable ? "✅" : "**❌**") + " | "
                        + (activity.Notes.Count == 0 ? "—" : string.Join("<br>", activity.Notes)) + " |");
                }
                sb.AppendLine();
                sb.AppendLine("> 同上：地点栏为空 = 没有任何场景引用它；资产路径见下表。");
                sb.AppendLine();
                sb.AppendLine("| 活动 ID | 资产路径 |");
                sb.AppendLine("| --- | --- |");
                foreach (ActivityCheck activity in Activities)
                {
                    sb.AppendLine("| <code>" + activity.ActivityId + "</code> | " + activity.AssetPath + " |");
                }
                sb.AppendLine();

                // 7. 附带发现
                sb.AppendLine("## 7. 附带发现");
                sb.AppendLine();
                foreach (string finding in ExtraFindings) sb.AppendLine("- " + finding);
                sb.AppendLine();

                // 附录 A
                sb.AppendLine("## 附录 A：已知答案对照（工具自检）");
                sb.AppendLine();
                sb.AppendLine("工具每次运行都必须能报出以下已知问题。**报不出即说明工具写错，而不是内容没问题。**");
                sb.AppendLine("（这些问题是在第一轮人工核查里确定的答案；工具升级或内容修复后，本表要同步更新。）");
                sb.AppendLine();
                sb.AppendLine("| 已知问题 | 是否报出 |");
                sb.AppendLine("| --- | --- |");
                sb.AppendLine("| 结局「稳定升学」不可达（三科均值上界不足） | " + Yes(FindEnding("stable_admission")) + " |");
                sb.AppendLine("| 结局「兴趣成长」不可达 | " + Yes(FindEnding("interest_growth")) + " |");
                sb.AppendLine("| 结局「自主选择」不可达 | " + Yes(FindEnding("self_driven")) + " |");
                sb.AppendLine("| 结局「压力调整」不可达 | " + Yes(FindEnding("pressure_recovery")) + " |");
                sb.AppendLine("| 结局「家庭支持」不可达（亲子上界不足） | " + Yes(FindEnding("family_support")) + " |");
                sb.AppendLine("| 属性「英语」实机来源 ≤1 处（瓶颈） | " + Yes(FindStatWeak(PlayerStatKey.English)) + " |");
                sb.AppendLine("| 属性「金钱」实机零来源 | " + Yes(FindStatZero(PlayerStatKey.Money)) + " |");
                sb.AppendLine("| 资源「AddMoney」零次出现（金钱无来源） | " + Yes(FindRewardType(RewardType.AddMoney)) + " |");
                sb.AppendLine("| 地点「学校」无任何已接线活动 | " + Yes(WiredCountOf("location_school") == 0)
                    + "（实际 " + WiredCountOf("location_school") + " 个） |");
                sb.AppendLine("| 地点「图书馆」有且仅有 1 个已接线活动 | " + Yes(WiredCountOf("location_library") == 1)
                    + "（实际 " + WiredCountOf("location_library") + " 个） |");
                sb.AppendLine("| 存在未接线的活动资产（写完没挂进场景） | " + Yes(UnwiredActivityCount > 0)
                    + "（" + UnwiredActivityCount + " 个） |");
                sb.AppendLine();
                sb.AppendLine("**— 以下为 2026-09-23 修复后应成立的状态（修复前是反例）—**");
                sb.AppendLine();
                sb.AppendLine("| 修复后应成立 | 是否成立 |");
                sb.AppendLine("| --- | --- |");
                sb.AppendLine("| 地点「社团活动室」可达（第三学段开放，不再依赖属性门槛） | " + Yes(!FindLocation("club")) + " |");
                sb.AppendLine("| 属性「社交」实机来源 ≥1 处（社团开放后救回） | " + Yes(!FindStatZero(PlayerStatKey.Social)) + " |");
                sb.AppendLine("| 属性「兴趣」实机来源 ≥2 处 | " + Yes(!FindStatWeak(PlayerStatKey.Interest)) + " |");
                sb.AppendLine("| 无任何已接线活动被地点锁死 | " + Yes(UnreachableActivityCount - UnwiredActivityCount == 0)
                    + "（" + (UnreachableActivityCount - UnwiredActivityCount) + " 个） |");
                sb.AppendLine("| 参考：实机可达活动（按 activityId 去重） | "
                    + DistinctReachableActivityCount + " / " + DistinctTotalActivityCount + " |");
                sb.AppendLine();
                sb.AppendLine("> **修复前（同日基线）**：社团活动室不可达（兴趣/社交门槛自锁——两个属性的增量来源有一半在社团里面）；"
                    + "连带 3 个已接线活动、6 个交互点位锁死；社交实机零来源（40 恒定）、兴趣实机仅 1 处来源（上界 42）。");
                sb.AppendLine("> 修复内容：`StageMapLocationAvailabilityService` 删掉 `club_interest_or_social` 键与 `HasClubAccess`，"
                    + "社团改为 `stage_03_or_later`；`MvpSampleDataFactory` 里社团的 `unlockedByDefault` 改为 true 并移除属性门槛。");
                sb.AppendLine();
                sb.AppendLine("> 上表里的数量断言是**人工核查确认过的答案**。内容被合法修改后这些数字会变，"
                    + "届时请同步更新本表——变了不一定是工具错，但一定要有人解释为什么变。");
                sb.AppendLine();

                // 附录 B
                sb.AppendLine("## 附录 B：口径说明");
                sb.AppendLine();
                sb.AppendLine("1. **这是上界推算，不是完整模拟。** 报「不可达」必然为真（连把全部来源都吃到都不够）；报「可达」只代表上界够，实战还受阶段时间预算与内容供给量约束——那一层由别的检查负责。");
                sb.AppendLine("2. **同一 ID 只计一次。** 属性增量按 (来源 ID, 属性) 归并：**不同 ID 之间求和，同一 ID 的多次出现只取绝对值最大的一次**（取最大而非取第一份，是为了让上界不因去重而被低估）。重复 ID 本身记在「附带发现」里，建议人工合并。");
                sb.AppendLine("3. **「瓶颈属性」= 实机只有 1 处来源。** 这类属性是单点依赖：那处来源一旦被删或被解锁条件卡住，该属性就再也不涨。零来源是\"完全不可达\"，仅 1 处来源是\"极脆弱\"，两者都值得处理。");
                sb.AppendLine("4. **属性上限取自 `ValueClampUtility`**：常规属性 0–100，精力上限按 `maxEnergy`，金钱 0–9999。");
                sb.AppendLine("5. **地点标记检查**是启发式：扫描 `StageMapLocationAvailabilityService` 里 `locationFlags.TryGetValue(\"...\")` 的字面量，再数该字符串在 `Assets` 下 `.cs` 文件中的出现文件数；只有读取点、没有写入点即判「该分支恒为假」。");
                sb.AppendLine("6. **可达性口径（本版新增）**：一个来源算「实机可达」需同时满足 —— ① 它所在的资产被地图场景的 `MapInteractionTarget` 引用过；② 该场景对应的地点在不动点迭代里被点亮。");
                sb.AppendLine("   - 「地点可达」的迭代方式：起点只算全局来源（阶段活动、卡牌、道具、阶段奖励）与默认解锁地点内的来源，然后反复「能开就开、重算边界」，最多 8 轮。这样能捕获**门槛属性只在锁死地点内有来源**的自锁。");
                sb.AppendLine("   - 已知保守处：若活动接到了一个不在阶段地图配置里的场景，会被判为「地点无法解析 → 不可达」。这属于宁可漏报也不虚报；发现这种情况请把该场景补进 `MvpSampleDataFactory.CreateDefaultStageMapData`。");
                sb.AppendLine("7. **场景扫描范围**：`" + MapSceneRoot + "` 下全部 `.unity`，**排除** `Backups/` 与 `_Recovery/`——那里的引用不代表内容已经上线。");
                sb.AppendLine("8. **本工具不修改任何内容。** 只读资产、场景与源码，唯一的写操作是导出本报告。");
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

            /// <summary>该属性在实机口径下完全零来源。</summary>
            private bool FindStatZero(PlayerStatKey key)
            {
                foreach (StatSourceCheck s in Stats)
                {
                    if (s.Stat == key && s.NoSource) return true;
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

            private int WiredCountOf(string locationId)
            {
                int count;
                return WiredCountByLocation.TryGetValue(locationId, out count) ? count : 0;
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
            /// <summary>纸面口径会判成可达，只有实机口径才判出不可达（说明够门槛的来源够不到）。</summary>
            public bool PaperOnlyReachable;
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

        public sealed class ActivityCheck
        {
            public string ActivityId;
            public string DisplayName;
            public string AssetPath;
            public string LocationText;
            public bool WiredToScene;
            public bool Reachable;
            public List<string> Notes = new List<string>();
        }

        public sealed class StatSourceCheck
        {
            public PlayerStatKey Stat;
            public string Name;
            public int Start;
            /// <summary>实机可达来源数。</summary>
            public int SourceCount;
            /// <summary>纸面来源数（含未接线资产与锁死地点内的内容）。</summary>
            public int PaperSourceCount;
            /// <summary>实机可达上界。</summary>
            public int UpperBound;
            /// <summary>纸面上界。</summary>
            public int PaperUpperBound;
            public bool NoSource;
            /// <summary>来源仅 1 处。单点依赖：这处来源一旦被删/被卡，该属性就彻底不涨。</summary>
            public bool IsBottleneck;
            public List<string> ExampleOrigins = new List<string>();
            /// <summary>纸面算得进、实机够不到的来源（写明出处，方便定位到具体资产）。</summary>
            public List<string> PaperOnlyOrigins = new List<string>();
        }
    }
}
