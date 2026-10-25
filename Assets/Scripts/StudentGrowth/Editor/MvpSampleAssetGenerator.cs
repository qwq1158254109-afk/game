using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace StudentGrowth.Editor
{
    /// <summary>
    /// 把 MvpSampleDataFactory 的当前权威数值快照成 ScriptableObject 资产。
    ///
    /// 2026-10-24 重写：旧版本内嵌一份 MVP 时代的硬编码数值表（24 卡 / 8 道具 / 6 小游戏 /
    /// 8 事件 / 6 挑战 / 12 活动 / 3 阶段 / 6 结局），与工厂在 2026-10-22/23 的修复
    /// （结局阈值重标定、活动不限次、Boss 通关加钱、精力上限来源）严重漂移，一旦执行会用旧值
    /// 覆盖运行时数据，并写出 7 个与 Battle/P2 撞 ID 的卡牌资产。完整差异表：
    /// Docs/GeneratorVsFactoryDiff-2026-10-24.md。旧实现备份在
    /// _backup_generator_snapshot_20261024/。
    ///
    /// 本工具不再自带任何数值，一律取自 MvpSampleDataFactory（单一事实来源），快照资产与
    /// 实机兜底数值天然一致。当前只快照结局（差异表方案 A）；阶段 / 活动 / 道具的资产化
    /// 前置条件见差异表第 5 节方案 B。
    /// </summary>
    public static class MvpSampleAssetGenerator
    {
        private const string EndingsRoot = "Assets/Data/StudentGrowth/Endings";
        private const string BootstrapperPath = "Assets/Resources/StudentGrowth/MvpBootstrapperData.asset";

        [MenuItem("Tools/Student Growth/Data/Generate MVP Sample Assets")]
        public static void GenerateMvpSampleAssets()
        {
            // 批处理模式（-executeMethod）下 DisplayDialog 无法交互，直接执行。
            if (!Application.isBatchMode
                && !EditorUtility.DisplayDialog(
                    "从工厂快照数据资产",
                    "把 MvpSampleDataFactory 当前的结局数值快照为 .asset，并创建/覆盖 MvpBootstrapperData.asset"
                    + "（只填 endings，其余字段留空走工厂逐字段兜底）。\n\n"
                    + "手改过 Endings 资产或 BootstrapperData 的内容会被覆盖，是否继续？",
                    "快照",
                    "取消"))
            {
                return;
            }

            EnsureFolders();

            List<EndingData> endings = SnapshotEndings();
            GameBootstrapperData bootstrapperData = GetOrCreateAsset<GameBootstrapperData>(BootstrapperPath);

            // 只填 endings：stages / locations / starterDeck / starterItems / firstStage 留空。
            // GameBootstrapper 与 ContentHealthAudit 对这些字段都是"有资产用资产、没有回工厂"，
            // 留空即保持现有工厂行为不变。
            bootstrapperData.endings = endings;
            bootstrapperData.starterDeck = new List<CardData>();
            bootstrapperData.starterItems = new List<ItemData>();
            bootstrapperData.stages = new List<StageData>();
            bootstrapperData.locations = new List<LocationData>();
            bootstrapperData.firstStage = null;
            EditorUtility.SetDirty(bootstrapperData);

            AssetDatabase.SaveAssets();
            VerifySnapshotAssets();
            Debug.Log("[Snapshot] 结局资产 " + endings.Count + " 个 → " + EndingsRoot
                      + "；BootstrapperData → " + BootstrapperPath + "（仅 endings）");
        }

        /// <summary>生成后自校验：任何结局资产加载失败就报 error。EndingData 已拆到独立文件，
        /// 资产的 m_Script 由 Unity 正常解析；此校验用于在批处理日志里留下明确证据。</summary>
        private static void VerifySnapshotAssets()
        {
            List<EndingData> endings = MvpSampleDataFactory.CreateDefaultEndings();
            int failures = 0;
            foreach (EndingData source in endings)
            {
                EndingData loaded = AssetDatabase.LoadAssetAtPath<EndingData>(EndingsRoot + "/" + source.endingId + ".asset");
                if (loaded == null || loaded.endingId != source.endingId)
                {
                    Debug.LogError("[Snapshot] 校验失败：" + source.endingId + " 无法作为 EndingData 加载");
                    failures++;
                }
            }

            GameBootstrapperData bootstrapper = AssetDatabase.LoadAssetAtPath<GameBootstrapperData>(BootstrapperPath);
            if (bootstrapper == null || bootstrapper.endings == null || bootstrapper.endings.Count != endings.Count)
            {
                Debug.LogError("[Snapshot] 校验失败：BootstrapperData 缺失或 endings 数量不对");
                failures++;
            }

            Debug.Log(failures == 0
                ? "[Snapshot] 自校验通过：" + endings.Count + " 个结局资产 + BootstrapperData"
                : "[Snapshot] 自校验失败 " + failures + " 项");
        }

        private static List<EndingData> SnapshotEndings()
        {
            List<EndingData> result = new List<EndingData>();
            foreach (EndingData source in MvpSampleDataFactory.CreateDefaultEndings())
            {
                if (source == null)
                {
                    continue;
                }

                EndingData ending = GetOrCreateAsset<EndingData>(EndingsRoot + "/" + source.endingId + ".asset");
                ending.endingId = source.endingId;
                ending.displayName = source.displayName;
                ending.description = source.description;
                ending.illustrationSprite = source.illustrationSprite;
                ending.imagePath = source.imagePath;
                ending.conditions = source.conditions != null
                    ? new List<EventConditionData>(source.conditions)
                    : new List<EventConditionData>();
                ending.minimumClearedStages = source.minimumClearedStages;
                ending.minimumDefeatedBosses = source.minimumDefeatedBosses;
                ending.minimumSubjectAverage = source.minimumSubjectAverage;
                ending.maximumPressure = source.maximumPressure;
                ending.requireEndingReached = source.requireEndingReached;
                ending.priority = source.priority;
                EditorUtility.SetDirty(ending);
                result.Add(ending);
            }
            return result;
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

        private static void EnsureFolders()
        {
            EnsureFolder("Assets", "Data");
            EnsureFolder("Assets/Data", "StudentGrowth");
            EnsureFolder("Assets/Data/StudentGrowth", "Endings");
        }

        private static void EnsureFolder(string parent, string folder)
        {
            string path = parent + "/" + folder;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
