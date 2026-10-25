using System.Collections.Generic;
using System.IO;
using System.Linq;
using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StudentGrowthEditor
{
    /// <summary>
    /// 扭蛋机构建器（设计文档：Docs/CapsuleGachaDesign-2026-10-24.md）。
    /// 一次生成全链路：占位贴图（16×24 点阵，美术后补）、卡包配置 CapsulePackData、
    /// 扭蛋活动 ActivityData、商店场景里的实体交互点 CapsuleMachineObject。
    ///
    /// 商店场景对象由 NewArtShopMapBuilder 只验证不重建，本构建器产出的对象可长期存活；
    /// 重跑幂等：贴图/资产覆盖写，场景对象先删同名再建。
    /// </summary>
    public static class CapsuleMachineBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity";
        private const string SpritePath = "Assets/Art/New2_0/Props/Shop/capsule_machine_placeholder.png";
        private const string PackPath = "Assets/Resources/StudentGrowth/NPC/capsule_pack_default.asset";
        private const string ActivityPath = "Assets/Data/StudentGrowth/New2_0/ShopMap/ShopCapsuleGacha.asset";
        private const string MachineName = "CapsuleMachineObject";
        // 交互点在柜台上（美术扭蛋机所在处，2026-10-24 用户确认）。柜台自带阻挡碰撞，
        // 本对象的碰撞体保持关闭，只做隐形交互点。
        private const float MachineX = -2.05f;
        private const float MachineY = 4.9f;

        [MenuItem("Tools/Student Growth/Shop/Build Capsule Machine")]
        public static void Build()
        {
            Sprite sprite = GenerateSprite();
            CapsulePackData pack = GeneratePack();
            ActivityData activity = GenerateActivity(pack);
            PlaceInScene(sprite, activity);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("[CapsuleMachine] 扭蛋机已就位 " + ScenePath
                + "（贴图/卡包/活动资产一并生成，位置 " + MachineX + "," + MachineY + "）");
        }

        // ------------------------------------------------------------------
        // 资产
        // ------------------------------------------------------------------

        private static Sprite GenerateSprite()
        {
            // '.'=透明 '#'=机身红 'o'=玻璃 'a'/'b'/'c'=扭蛋三色 '-'=投币口 '='=底座
            string[] pattern =
            {
                "....########....",
                "..##oooooooo##..",
                ".#oooooooooooo#.",
                ".#o#aa##bb#ooo#.",
                ".#o#aa##bb#ooo#.",
                ".#ooo##cc##ooo#.",
                ".#oooooooooooo#.",
                "..##oooooooo##..",
                "################",
                "################",
                "################",
                "################",
                "####--------####",
                "####--------####",
                "################",
                "################",
                "###==========###",
                "###==========###",
                "################",
                "================",
                "================",
                "================"
            };
            Dictionary<char, Color> colors = new Dictionary<char, Color>
            {
                { '#', new Color(0.72f, 0.30f, 0.28f, 1f) },
                { 'o', new Color(0.82f, 0.86f, 0.88f, 0.85f) },
                { 'a', new Color(0.95f, 0.62f, 0.35f, 1f) },
                { 'b', new Color(0.38f, 0.70f, 0.72f, 1f) },
                { 'c', new Color(0.92f, 0.84f, 0.35f, 1f) },
                { '-', new Color(0.16f, 0.15f, 0.18f, 1f) },
                { '=', new Color(0.35f, 0.30f, 0.30f, 1f) }
            };

            int width = pattern[0].Length;
            int height = pattern.Length;
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color color;
                    texture.SetPixel(x, height - 1 - y, colors.TryGetValue(pattern[y][x], out color) ? color : Color.clear);
                }
            }

            texture.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(SpritePath));
            File.WriteAllBytes(SpritePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(SpritePath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = 16;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        }

        private static CapsulePackData GeneratePack()
        {
            CapsulePackData pack = GetOrCreateAsset<CapsulePackData>(PackPath);
            pack.packId = "capsule_default";
            pack.price = 40;
            pack.commonWeight = 65;
            pack.uncommonWeight = 27;
            pack.rareWeight = 8;
            pack.pityMinRarity = CardRarity.Uncommon;
            pack.duplicateRefund = 15;
            EditorUtility.SetDirty(pack);
            return pack;
        }

        private static CapsuleGachaActivityData GenerateActivity(CapsulePackData pack)
        {
            // 必须用独立子类 CapsuleGachaActivityData：共文件的 ActivityData 落资产
            // m_Script 必坏（2026-10-24 实测首版即如此，游戏里报「活动数据不存在」）。
            CapsuleGachaActivityData activity = GetOrCreateAsset<CapsuleGachaActivityData>(ActivityPath);
            activity.activityId = "activity_shop_capsule_gacha";
            activity.displayName = "扭蛋抽卡";
            activity.description = "投 " + pack.price + " 金抽一张卡：普通 65% / 非凡 27% / 稀有 8%，每局首抽必得非凡以上。";
            activity.activityType = ActivityType.Capsule;
            activity.timeCost = 0;
            // 不限次数：金钱是唯一闸门（次数上限默认 1 会把抽奖卡成一次性的）。
            activity.maxUsesPerStage = -1;
            GameArtBindingUtility.ApplyKnownArtPath(activity);
            EditorUtility.SetDirty(activity);
            return activity;
        }

        private static T GetOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
            {
                return asset;
            }

            // 加载为 null 但文件还在 = 脚本引用坏死的残留资产（m_Script {fileID:0}），
            // 直接删掉重建，否则 CreateAsset 会撞在坏文件上。
            if (File.Exists(path))
            {
                AssetDatabase.DeleteAsset(path);
            }

            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        // ------------------------------------------------------------------
        // 场景
        // ------------------------------------------------------------------

        private static void PlaceInScene(Sprite sprite, ActivityData activity)
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject owner = GameObject.Find("ShopOwnerObject");
            if (owner == null)
            {
                throw new System.InvalidOperationException("ShopOwnerObject is missing — 商店场景结构变了，先确认再跑。");
            }

            Transform interactionRoot = owner.transform.parent;

            GameObject existing = GameObject.Find(MachineName);
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject machine = new GameObject(MachineName);
            machine.transform.SetParent(interactionRoot, false);
            machine.transform.localPosition = new Vector3(MachineX, MachineY, 0f);

            SpriteRenderer renderer = machine.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            // 地图美术里已经有扭蛋机（2026-10-24 用户确认）：本对象只做隐形交互点，
            // 占位贴图不显示，避免叠在美术机台上。碰撞保留，防止穿模。
            renderer.enabled = false;
            renderer.sortingOrder = MapObjectYSorter.CalculateSortingOrder(MachineY, 1000, 100f);

            BoxCollider2D body = machine.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.85f, 1.3f);
            body.offset = new Vector2(0f, -0.1f);
            // 柜台已有阻挡碰撞，这里不重复挡路（2026-10-24）。
            body.enabled = false;

            MapInteractionPoint point = machine.AddComponent<MapInteractionPoint>();
            point.locationId = "location_shop";
            point.actionId = activity.activityId;
            point.interactionType = MapInteractionType.LocationAction;
            point.displayName = "扭蛋机";
            point.description = activity.description;
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = machine.AddComponent<MapInteractionTarget>();
            target.interactionId = "shop_capsule_gacha";
            target.displayName = "扭蛋机";
            target.promptText = "按 E：抽一次扭蛋（40 金）";
            target.fallbackActivity = activity;
            target.interactionPoint = point;
            target.consumeTimeOnInteraction = false;
            target.requireFacing = false;
            target.interactionRange = 2.0f;
            target.priority = 20;

            MapObjectYSorter sorter = machine.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };
            sorter.useRendererCenterAsSortY = true;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new System.InvalidOperationException("Failed to save ShopMapScene.");
            }
        }
    }
}
