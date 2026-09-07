using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StudentGrowthEditor
{
    public static class ParkMapSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/ParkMapScene.unity";
        private const string TemplateScenePath = "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity";
        private const string StageHubScenePath = "Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity";
        private const string BackgroundPath = "Assets/Art/New2_0/Park/Source/ParkBase.png";
        private const string ReferenceCompositePath = "Assets/Art/New2_0/Park/Generated/ParkScene_Complete.png";
        private const string FacilitySpriteRoot = "Assets/Art/New2_0/Park/Sprites";
        private const string DataRoot = "Assets/Data/StudentGrowth/New2_0/ParkMap";
        private const string HudThemePath = "Assets/Resources/StudentGrowth/UI/SchoolMapHudTheme.asset";
        private const float PixelsPerUnit = 64f;
        private const int CanvasWidth = 1536;
        private const int CanvasHeight = 1024;

        private sealed class FacilityPlacement
        {
            public string name;
            public string spriteFile;
            public Vector2 topLeft;
            public Vector2 colliderSize;
            public Vector2 colliderOffset;

            public FacilityPlacement(
                string name,
                string spriteFile,
                float x,
                float y,
                float colliderWidth = 0f,
                float colliderHeight = 0f,
                float colliderOffsetY = 0f)
            {
                this.name = name;
                this.spriteFile = spriteFile;
                topLeft = new Vector2(x, y);
                colliderSize = new Vector2(colliderWidth, colliderHeight);
                colliderOffset = new Vector2(0f, colliderOffsetY);
            }
        }

        private static readonly FacilityPlacement[] FacilityPlacements =
        {
            new FacilityPlacement("WC", "Park_WC.png", 232f, 122f, 172f, 54f, 33f),

            new FacilityPlacement("Tree_TopLeft", "Park_Tree_Large.png", 36f, 76f, 38f, 42f, 21f),
            new FacilityPlacement("Tree_TopCentreLeft", "Park_Tree_Medium.png", 413f, -8f, 36f, 42f, 21f),
            new FacilityPlacement("Tree_TopCentre", "Park_Tree_Small.png", 716f, -4f, 32f, 38f, 19f),
            new FacilityPlacement("Tree_TopRight", "Park_Tree_Medium.png", 1262f, -2f, 36f, 42f, 21f),
            new FacilityPlacement("Tree_LeftMiddle", "Park_Tree_Large.png", 42f, 300f, 38f, 42f, 21f),
            new FacilityPlacement("Tree_LeftBottom", "Park_Tree_Small.png", 69f, 690f, 32f, 38f, 19f),
            new FacilityPlacement("Tree_RightMiddle", "Park_Tree_Medium.png", 1323f, 300f, 36f, 42f, 21f),
            new FacilityPlacement("Tree_RightLower", "Park_Tree_Large.png", 1334f, 551f, 38f, 42f, 21f),
            new FacilityPlacement("Tree_RightBottom", "Park_Tree_Small.png", 1271f, 716f, 32f, 38f, 19f),

            new FacilityPlacement("Flowerbed_Top", "Park_FlowerBed_A.png", 568f, 157f, 202f, 48f, 25f),
            new FacilityPlacement("Flowerbed_BottomLeft", "Park_FlowerBed_B.png", 314f, 735f, 200f, 48f, 25f),
            new FacilityPlacement("Flowerbed_BottomRight", "Park_FlowerBed_A.png", 966f, 733f, 202f, 48f, 25f),

            new FacilityPlacement("Lamp_Left", "Park_StreetLight.png", 392f, 439f, 32f, 28f, 14f),
            new FacilityPlacement("Lamp_Right", "Park_StreetLight.png", 963f, 443f, 32f, 28f, 14f),
            new FacilityPlacement("Bench_Left", "Park_Bench.png", 242f, 606f, 140f, 52f, 34f),
            new FacilityPlacement("Bench_Right", "Park_Bench.png", 1071f, 601f, 140f, 52f, 34f),
            new FacilityPlacement("TrashBin", "Park_TrashBin.png", 1260f, 668f, 58f, 54f, 28f),

            new FacilityPlacement("SteppingStone_01", "Park_SteppingStone.png", 309f, 356f),
            new FacilityPlacement("SteppingStone_02", "Park_SteppingStone.png", 329f, 390f),
            new FacilityPlacement("SteppingStone_03", "Park_SteppingStone.png", 302f, 427f),
            new FacilityPlacement("SteppingStone_04", "Park_SteppingStone.png", 323f, 466f),
            new FacilityPlacement("SteppingStone_05", "Park_SteppingStone.png", 306f, 503f)
        };

        [MenuItem("Tools/Student Growth/Park/Rebuild Park Map Scene")]
        public static void Rebuild()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            EnsureFolder(DataRoot);
            ConfigureParkTextures();

            ActivityData benchRest = CreateActivity(
                "ParkBenchRest.asset",
                "activity_park_bench_rest",
                "长椅休息",
                "在树荫下稍作休息，恢复精力并放松心情。",
                ActivityType.Rest,
                Reward(RewardType.AddPlayerStat, PlayerStatKey.Energy, 3, "精力 +3"),
                Reward(RewardType.AddPlayerStat, PlayerStatKey.Mood, 2, "心情 +2"),
                Reward(RewardType.ReducePressure, PlayerStatKey.Pressure, 3, "压力 -3"));
            ActivityData pondWalk = CreateActivity(
                "ParkPondWalk.asset",
                "activity_park_pond_walk",
                "池边散步",
                "沿池塘慢慢散步，让紧绷的情绪平静下来。",
                ActivityType.Rest,
                Reward(RewardType.ReducePressure, PlayerStatKey.Pressure, 5, "压力 -5"),
                Reward(RewardType.AddPlayerStat, PlayerStatKey.Mood, 3, "心情 +3"));
            ActivityData flowerViewing = CreateActivity(
                "ParkFlowerViewing.asset",
                "activity_park_flower_viewing",
                "赏花观察",
                "观察花坛里的植物，获得一点灵感和好心情。",
                ActivityType.Event,
                Reward(RewardType.AddPlayerStat, PlayerStatKey.Interest, 2, "兴趣 +2"),
                Reward(RewardType.AddPlayerStat, PlayerStatKey.Mood, 3, "心情 +3"),
                Reward(RewardType.ReducePressure, PlayerStatKey.Pressure, 2, "压力 -2"));

            BuildScene(benchRest, pondWalk, flowerViewing);
            EnableParkInStageHub();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Debug.Log("ParkMapScene rebuilt and enabled in the stage hub.");
        }

        [MenuItem("Tools/Student Growth/Park/Validate Park Map Scene")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid(), "ParkMapScene could not be opened.");
            Require(GameObject.Find("ParkMapSceneRoot") != null, "ParkMapSceneRoot is missing.");
            Require(GameObject.Find("ParkMapRpgRoot") != null, "ParkMapRpgRoot is missing.");
            Require(GameObject.Find("ParkBackground")?.GetComponent<SpriteRenderer>()?.sprite != null,
                "Park background sprite is not bound.");
            Require(GameObject.Find("PlayerRpg")?.GetComponent<RpgPlayerController>() != null,
                "Park player controller is missing.");
            Require(GameObject.Find("PlayerSpawnPoints")?.GetComponentInChildren<PlayerSpawnPoint>() != null,
                "Park entry spawn is missing.");
            GameObject facilitiesRoot = GameObject.Find("EditableFacilities");
            Require(facilitiesRoot != null && facilitiesRoot.transform.childCount == FacilityPlacements.Length,
                "Editable park facilities are missing or incomplete.");
            Require(facilitiesRoot.GetComponentsInChildren<SpriteRenderer>(true).Length == FacilityPlacements.Length,
                "Every editable park facility must have its own SpriteRenderer.");
            Require(facilitiesRoot.GetComponentsInChildren<BoxCollider2D>(true).Length >= 18,
                "Editable park facility collision coverage is incomplete.");

            SpriteRenderer backgroundRenderer = GameObject.Find("ParkBackground")?.GetComponent<SpriteRenderer>();
            Require(backgroundRenderer != null
                && AssetDatabase.GetAssetPath(backgroundRenderer.sprite) == BackgroundPath,
                "Park must use the facility-free base image as its runtime background.");

            GameObject sceneRoot = GameObject.Find("ParkMapSceneRoot");
            GameObject fullscreenCanvas = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == MapUiFactory.FullscreenMapCanvasName);
            PlayModeOnlyVisibility visibility = sceneRoot == null
                ? null
                : sceneRoot.GetComponent<PlayModeOnlyVisibility>();
            Require(fullscreenCanvas != null
                && !fullscreenCanvas.activeSelf
                && visibility != null
                && visibility.Contains(fullscreenCanvas),
                "Park UI must be hidden outside Play Mode and registered for runtime display.");

            MapInteractionTarget[] targets = Object.FindObjectsByType<MapInteractionTarget>(FindObjectsInactive.Include);
            Require(targets.Length >= 5, "Park requires four activities and one exit interaction.");
            MapInteractionTarget exit = targets.FirstOrDefault(target => target.isExitPortal);
            Require(exit != null && !exit.consumeTimeOnInteraction, "Park exit must not consume time.");

            FullscreenMapSceneController controller = Object.FindAnyObjectByType<FullscreenMapSceneController>();
            Require(controller != null && controller.mapId == "location_park", "Park map controller is not configured.");
            Require(controller.player != null && controller.cameraController != null,
                "Park player or camera references are missing.");
            Require(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == ScenePath),
                "ParkMapScene is missing from Build Settings.");

            TextureImporter importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
            Require(importer != null
                && Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit)
                && importer.filterMode == FilterMode.Point
                && !importer.mipmapEnabled
                && importer.textureCompression == TextureImporterCompression.Uncompressed,
                "Park background pixel-art import settings are invalid.");

            foreach (string spritePath in GetFacilitySpritePaths())
            {
                TextureImporter facilityImporter = AssetImporter.GetAtPath(spritePath) as TextureImporter;
                Require(facilityImporter != null
                    && Mathf.Approximately(facilityImporter.spritePixelsPerUnit, PixelsPerUnit)
                    && facilityImporter.filterMode == FilterMode.Point
                    && !facilityImporter.mipmapEnabled
                    && facilityImporter.textureCompression == TextureImporterCompression.Uncompressed,
                    "Park facility pixel-art import settings are invalid: " + spritePath);
            }

            Scene hubScene = EditorSceneManager.OpenScene(StageHubScenePath, OpenSceneMode.Single);
            Require(hubScene.IsValid(), "StageHubMapScene could not be opened.");
            StageHubMapSceneController hubController = Object.FindAnyObjectByType<StageHubMapSceneController>();
            Require(hubController != null && IsParkUnlocked(hubController.stageMapData),
                "Stage hub still marks the park as unavailable.");

            Debug.Log($"Park validation passed: {targets.Length} interactions, "
                + $"{EditorBuildSettings.scenes.Count(entry => entry.enabled)} enabled scenes and valid pixel import settings.");
        }

        private static void ConfigureParkTextures()
        {
            ConfigureTexture(BackgroundPath, new Vector2(0.5f, 0.5f));
            if (File.Exists(Path.GetFullPath(ReferenceCompositePath)))
            {
                ConfigureTexture(ReferenceCompositePath, new Vector2(0.5f, 0.5f));
            }

            foreach (string spritePath in GetFacilitySpritePaths())
            {
                ConfigureTexture(spritePath, new Vector2(0.5f, 0f));
            }
        }

        private static void ConfigureTexture(string path, Vector2 pivot)
        {
            Require(File.Exists(Path.GetFullPath(path)), "Park texture is missing: " + path);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Park texture importer is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = pivot;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static IEnumerable<string> GetFacilitySpritePaths()
        {
            return FacilityPlacements
                .Select(placement => FacilitySpriteRoot + "/" + placement.spriteFile)
                .Distinct(StringComparer.Ordinal);
        }

        private static ActivityData CreateActivity(
            string fileName,
            string id,
            string displayName,
            string description,
            ActivityType activityType,
            params RewardData[] rewards)
        {
            string path = DataRoot + "/" + fileName;
            ActivityData activity = AssetDatabase.LoadAssetAtPath<ActivityData>(path);
            if (activity == null)
            {
                if (File.Exists(Path.GetFullPath(path)))
                {
                    AssetDatabase.DeleteAsset(path);
                }

                activity = ScriptableObject.CreateInstance<ParkMapActivityData>();
                AssetDatabase.CreateAsset(activity, path);
            }

            activity.activityId = id;
            activity.displayName = displayName;
            activity.description = description;
            activity.activityType = activityType;
            activity.backgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                File.Exists(Path.GetFullPath(ReferenceCompositePath)) ? ReferenceCompositePath : BackgroundPath);
            activity.timeCost = 1;
            activity.fixedRewards.Clear();
            activity.fixedRewards.AddRange(rewards);
            EditorUtility.SetDirty(activity);
            return activity;
        }

        private static RewardData Reward(
            RewardType rewardType,
            PlayerStatKey stat,
            int value,
            string description)
        {
            return new RewardData
            {
                rewardType = rewardType,
                targetStat = stat,
                value = value,
                description = description
            };
        }

        private static void BuildScene(
            ActivityData benchRest,
            ActivityData pondWalk,
            ActivityData flowerViewing)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Require(AssetDatabase.CopyAsset(TemplateScenePath, ScenePath),
                    "Failed to copy the existing map scene template.");
                AssetDatabase.ImportAsset(ScenePath, ImportAssetOptions.ForceSynchronousImport);
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject sceneRoot = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.GetComponent<FullscreenMapSceneController>() != null);
            GameObject player = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "PlayerRpg");
            GameObject cameraObject = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "Main Camera");
            Require(sceneRoot != null && player != null && cameraObject != null,
                "The map template is missing its root, player or camera.");

            GameObject fullscreenCanvas = scene.GetRootGameObjects()
                .FirstOrDefault(root => root.name == MapUiFactory.FullscreenMapCanvasName);
            Require(fullscreenCanvas != null, "The map template is missing FullscreenMapCanvas.");

            sceneRoot.name = "ParkMapSceneRoot";
            PlayModeOnlyVisibility visibility = sceneRoot.GetComponent<PlayModeOnlyVisibility>();
            if (visibility == null)
            {
                visibility = sceneRoot.AddComponent<PlayModeOnlyVisibility>();
            }

            visibility.Configure(fullscreenCanvas);
            MapInteractionFlowController flow = sceneRoot.GetComponentInChildren<MapInteractionFlowController>(true);
            Require(flow != null, "The map template is missing MapInteractionFlowController.");
            GameObject mapRoot = flow.gameObject;
            mapRoot.name = "ParkMapRpgRoot";
            Dictionary<string, Vector3> preservedFacilityPositions = CaptureFacilityPositions(mapRoot.transform);
            for (int i = mapRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(mapRoot.transform.GetChild(i).gameObject);
            }

            GameObject visualRoot = CreateChild("Visual", mapRoot.transform);
            GameObject background = CreateChild("ParkBackground", visualRoot.transform);
            SpriteRenderer backgroundRenderer = background.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            backgroundRenderer.sortingOrder = -1000;
            Require(backgroundRenderer.sprite != null, "Park background sprite failed to load.");

            GameObject referenceRoot = CreateChild("Reference", mapRoot.transform);
            GameObject reference = CreateChild("OriginalCompositeReference", referenceRoot.transform);
            SpriteRenderer referenceRenderer = reference.AddComponent<SpriteRenderer>();
            referenceRenderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ReferenceCompositePath);
            referenceRenderer.sortingOrder = -900;
            referenceRoot.SetActive(false);

            GameObject facilitiesRoot = CreateChild("EditableFacilities", mapRoot.transform);
            Dictionary<string, GameObject> facilities = BuildEditableFacilities(
                facilitiesRoot.transform,
                preservedFacilityPositions);

            GameObject collisionRoot = CreateChild("Collision", mapRoot.transform);
            BuildBoundaryCollision(collisionRoot.transform);
            BuildPondCollision(collisionRoot.transform);

            GameObject spawnRoot = CreateChild("PlayerSpawnPoints", mapRoot.transform);
            GameObject spawn = CreateChild("ParkEntrySpawn", spawnRoot.transform);
            spawn.transform.localPosition = PixelToWorld(768f, 820f);
            spawn.AddComponent<PlayerSpawnPoint>().spawnPointId = "park_entry";

            GameObject cameraBoundsObject = CreateChild("CameraBounds", mapRoot.transform);
            BoxCollider2D cameraBounds = cameraBoundsObject.AddComponent<BoxCollider2D>();
            cameraBounds.size = new Vector2(24f, 16f);
            cameraBounds.isTrigger = true;

            GameObject interactionRoot = CreateChild("MapInteractions", mapRoot.transform);
            MapInteractionTarget leftBench = CreateActivityInteraction(
                "LeftBenchRest",
                facilities["Bench_Left"].transform,
                new Vector2(0f, -0.35f),
                "park_left_bench_rest",
                "长椅休息",
                "按 E 坐下休息",
                benchRest,
                20);
            MapInteractionTarget rightBench = CreateActivityInteraction(
                "RightBenchRest",
                facilities["Bench_Right"].transform,
                new Vector2(0f, -0.35f),
                "park_right_bench_rest",
                "长椅休息",
                "按 E 坐下休息",
                benchRest,
                20);
            MapInteractionTarget pond = CreateActivityInteraction(
                "PondWalk",
                interactionRoot.transform,
                PixelToWorld(1030f, 492f),
                "park_pond_walk",
                "池边散步",
                "按 E 沿池边散步",
                pondWalk,
                30);
            MapInteractionTarget flowers = CreateActivityInteraction(
                "FlowerViewing",
                facilities["Flowerbed_Top"].transform,
                new Vector2(0f, -0.25f),
                "park_flower_viewing",
                "赏花观察",
                "按 E 观察花坛",
                flowerViewing,
                25);
            MapInteractionTarget exit = CreateExitInteraction(interactionRoot.transform);

            ConfigurePlayer(player, flow, spawn.transform.position);
            ConfigureCamera(cameraObject, player.transform, cameraBounds);
            ConfigureSceneController(sceneRoot, player, cameraObject, cameraBounds,
                new[] { leftBench, rightBench, pond, flowers, exit });
            AddSceneToBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Failed to save ParkMapScene.");
        }

        private static void BuildBoundaryCollision(Transform parent)
        {
            AddPixelBoxCollider(parent, "Boundary_Top", 768f, 55f, 1400f, 95f);
            AddPixelBoxCollider(parent, "Boundary_Left", 48f, 500f, 92f, 860f);
            AddPixelBoxCollider(parent, "Boundary_Right", 1488f, 500f, 92f, 860f);
            AddPixelBoxCollider(parent, "Boundary_BottomLeft", 315f, 894f, 625f, 92f);
            AddPixelBoxCollider(parent, "Boundary_BottomRight", 1186f, 894f, 706f, 92f);
            AddPixelBoxCollider(parent, "Boundary_StreetBottom", 768f, 1013f, 1536f, 22f);
        }

        private static Dictionary<string, Vector3> CaptureFacilityPositions(Transform mapRoot)
        {
            Dictionary<string, Vector3> positions = new Dictionary<string, Vector3>(StringComparer.Ordinal);
            Transform facilitiesRoot = mapRoot.Find("EditableFacilities");
            if (facilitiesRoot == null)
            {
                return positions;
            }

            for (int i = 0; i < facilitiesRoot.childCount; i++)
            {
                Transform facility = facilitiesRoot.GetChild(i);
                positions[facility.name] = facility.localPosition;
            }

            return positions;
        }

        private static Dictionary<string, GameObject> BuildEditableFacilities(
            Transform parent,
            IReadOnlyDictionary<string, Vector3> preservedPositions)
        {
            Dictionary<string, GameObject> facilities = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (FacilityPlacement placement in FacilityPlacements)
            {
                string spritePath = FacilitySpriteRoot + "/" + placement.spriteFile;
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
                Require(sprite != null, "Park facility sprite failed to load: " + spritePath);

                GameObject facility = CreateChild(placement.name, parent);
                float bottomX = placement.topLeft.x + sprite.rect.width * 0.5f;
                float bottomY = placement.topLeft.y + sprite.rect.height;
                facility.transform.localPosition = preservedPositions != null
                    && preservedPositions.TryGetValue(placement.name, out Vector3 preservedPosition)
                        ? preservedPosition
                        : PixelToWorld(bottomX, bottomY);

                SpriteRenderer renderer = facility.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;

                bool isGroundDecoration = placement.name.StartsWith("SteppingStone_", StringComparison.Ordinal);
                if (isGroundDecoration)
                {
                    renderer.sortingOrder = -100;
                }
                else
                {
                    MapObjectYSorter sorter = facility.AddComponent<MapObjectYSorter>();
                    sorter.renderers = new[] { renderer };
                    sorter.baseOrder = 1000;
                    sorter.orderScale = 100f;
                    sorter.sortYOffset = 0f;
                }

                if (placement.colliderSize.x > 0f && placement.colliderSize.y > 0f)
                {
                    BoxCollider2D collider = facility.AddComponent<BoxCollider2D>();
                    collider.size = placement.colliderSize / PixelsPerUnit;
                    collider.offset = placement.colliderOffset / PixelsPerUnit;
                }

                facilities.Add(placement.name, facility);
            }

            return facilities;
        }

        private static void BuildPondCollision(Transform parent)
        {
            GameObject pond = CreateChild("Pond", parent);
            PolygonCollider2D collider = pond.AddComponent<PolygonCollider2D>();
            Vector2[] pixelOutline =
            {
                new Vector2(1000f, 125f),
                new Vector2(1150f, 110f),
                new Vector2(1275f, 140f),
                new Vector2(1350f, 205f),
                new Vector2(1365f, 275f),
                new Vector2(1315f, 325f),
                new Vector2(1245f, 345f),
                new Vector2(1240f, 405f),
                new Vector2(1215f, 460f),
                new Vector2(1140f, 485f),
                new Vector2(1070f, 470f),
                new Vector2(1030f, 425f),
                new Vector2(1020f, 340f),
                new Vector2(960f, 320f),
                new Vector2(900f, 285f),
                new Vector2(875f, 230f),
                new Vector2(900f, 185f),
                new Vector2(950f, 150f)
            };
            collider.SetPath(0, pixelOutline.Select(point => PixelToWorld(point.x, point.y)).ToArray());
        }

        private static MapInteractionTarget CreateActivityInteraction(
            string name,
            Transform parent,
            Vector2 position,
            string actionId,
            string displayName,
            string prompt,
            ActivityData activity,
            int priority)
        {
            GameObject targetObject = CreateChild(name, parent);
            targetObject.transform.localPosition = position;
            CircleCollider2D trigger = targetObject.AddComponent<CircleCollider2D>();
            trigger.isTrigger = true;
            trigger.radius = 0.8f;

            MapInteractionPoint point = targetObject.AddComponent<MapInteractionPoint>();
            point.locationId = "location_park";
            point.actionId = actionId;
            point.interactionType = MapInteractionType.LocationAction;
            point.displayName = displayName;
            point.description = prompt;
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = targetObject.AddComponent<MapInteractionTarget>();
            target.interactionId = actionId;
            target.displayName = displayName;
            target.promptText = prompt;
            target.fallbackActivity = activity;
            target.interactionPoint = point;
            target.consumeTimeOnInteraction = true;
            target.requireFacing = false;
            target.interactionRange = 1.45f;
            target.priority = priority;
            return target;
        }

        private static MapInteractionTarget CreateExitInteraction(Transform parent)
        {
            GameObject exit = CreateChild("ParkExit", parent);
            exit.transform.localPosition = PixelToWorld(768f, 936f);
            BoxCollider2D trigger = exit.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(2.5f, 0.8f);

            MapInteractionPoint point = exit.AddComponent<MapInteractionPoint>();
            point.locationId = "location_park";
            point.actionId = "park_exit";
            point.interactionType = MapInteractionType.ReturnToMainUi;
            point.displayName = "返回阶段地图";
            point.description = "从公园入口返回阶段地图。";
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = exit.AddComponent<MapInteractionTarget>();
            target.interactionId = "park_exit";
            target.displayName = "公园出口";
            target.promptText = "按 E 返回阶段地图";
            target.interactionPoint = point;
            target.isExitPortal = true;
            target.consumeTimeOnInteraction = false;
            target.requireFacing = false;
            target.interactionRange = 1.5f;
            target.priority = 100;

            MapExitPortal portal = exit.AddComponent<MapExitPortal>();
            portal.destinationId = "StageHubMapScene";
            portal.promptText = target.promptText;
            return target;
        }

        private static void ConfigurePlayer(GameObject player, MapInteractionFlowController flow, Vector3 spawnPosition)
        {
            player.transform.position = new Vector3(spawnPosition.x, spawnPosition.y, -0.1f);
            RpgPlayerController controller = player.GetComponent<RpgPlayerController>();
            Require(controller != null, "The map template player is missing RpgPlayerController.");
            controller.canMove = true;
            controller.useMovementBounds = true;
            controller.movementMin = new Vector2(-11.3f, -7.55f);
            controller.movementMax = new Vector2(11.3f, 6.45f);
            controller.interactionFlowController = flow;
            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 1000;
            }

            MapObjectYSorter sorter = player.GetComponent<MapObjectYSorter>();
            if (sorter == null)
            {
                sorter = player.AddComponent<MapObjectYSorter>();
            }

            sorter.renderers = player.GetComponentsInChildren<SpriteRenderer>(true);
            sorter.baseOrder = 1000;
            sorter.orderScale = 100f;
            sorter.sortYOffset = 0f;
        }

        private static void ConfigureCamera(GameObject cameraObject, Transform player, Collider2D cameraBounds)
        {
            Camera sceneCamera = cameraObject.GetComponent<Camera>();
            MapCameraController mapCamera = cameraObject.GetComponent<MapCameraController>();
            Require(sceneCamera != null && mapCamera != null, "The map template camera is incomplete.");
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            sceneCamera.orthographic = true;
            sceneCamera.orthographicSize = 8f;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = new Color32(40, 61, 34, 255);
            mapCamera.targetCamera = sceneCamera;
            mapCamera.followTarget = player;
            mapCamera.cameraBounds = cameraBounds;
            mapCamera.followEnabled = false;
            mapCamera.useExplicitBounds = false;
        }

        private static void ConfigureSceneController(
            GameObject sceneRoot,
            GameObject player,
            GameObject cameraObject,
            Collider2D cameraBounds,
            MapInteractionTarget[] targets)
        {
            FullscreenMapSceneController controller = sceneRoot.GetComponent<FullscreenMapSceneController>();
            Require(controller != null, "The map template is missing FullscreenMapSceneController.");
            controller.mapId = "location_park";
            controller.displayName = "公园";
            controller.defaultSpawnPointId = "park_entry";
            controller.player = player.transform;
            controller.playerController = player.GetComponent<RpgPlayerController>();
            controller.facingInteractor = player.GetComponent<PlayerFacingInteractor>();
            controller.cameraController = cameraObject.GetComponent<MapCameraController>();
            controller.cameraBounds = cameraBounds;
            controller.interactionTargets = targets;
            controller.hudTheme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(HudThemePath);
            controller.createFallbackRuntimeServices = true;
            controller.autoCollectTargets = true;
        }

        private static void EnableParkInStageHub()
        {
            Scene hubScene = EditorSceneManager.OpenScene(StageHubScenePath, OpenSceneMode.Single);
            StageHubMapSceneController hubController = Object.FindAnyObjectByType<StageHubMapSceneController>();
            Require(hubController != null, "Stage hub controller is missing.");
            UpdateParkEntry(hubController.stageMapData);
            if (hubController.locationSceneRouter != null
                && hubController.locationSceneRouter.stageMapData != hubController.stageMapData)
            {
                UpdateParkEntry(hubController.locationSceneRouter.stageMapData);
            }

            EditorSceneManager.MarkSceneDirty(hubScene);
            Require(EditorSceneManager.SaveScene(hubScene, StageHubScenePath),
                "Failed to save the stage hub park availability update.");
        }

        private static void UpdateParkEntry(StageMapData mapData)
        {
            Require(mapData != null, "Stage hub map data is missing.");
            StageMapLocationEntry park = mapData.locations.FirstOrDefault(entry => entry.locationId == "location_park");
            Require(park != null, "Stage hub park entry is missing.");
            park.sceneName = "ParkMapScene";
            park.defaultStatus = StageMapLocationStatus.Unlocked;
            park.lockedReason = string.Empty;
            park.comingSoonText = string.Empty;
            EditorUtility.SetDirty(mapData);
        }

        private static bool IsParkUnlocked(StageMapData mapData)
        {
            StageMapLocationEntry park = mapData?.locations?.FirstOrDefault(entry => entry.locationId == "location_park");
            return park != null
                && park.sceneName == "ParkMapScene"
                && park.defaultStatus == StageMapLocationStatus.Unlocked
                && string.IsNullOrEmpty(park.comingSoonText);
        }

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            int existingIndex = scenes.FindIndex(entry => entry.path == ScenePath);
            EditorBuildSettingsScene parkScene = new EditorBuildSettingsScene(ScenePath, true);
            if (existingIndex >= 0)
            {
                scenes[existingIndex] = parkScene;
            }
            else
            {
                int sampleIndex = scenes.FindIndex(entry => entry.path.EndsWith("SampleScene.unity", StringComparison.Ordinal));
                scenes.Insert(sampleIndex >= 0 ? sampleIndex : scenes.Count, parkScene);
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void AddPixelBoxCollider(
            Transform parent,
            string name,
            float pixelX,
            float pixelY,
            float pixelWidth,
            float pixelHeight)
        {
            GameObject target = CreateChild(name, parent);
            target.transform.localPosition = PixelToWorld(pixelX, pixelY);
            BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(pixelWidth / PixelsPerUnit, pixelHeight / PixelsPerUnit);
        }

        private static Vector2 PixelToWorld(float pixelX, float pixelY)
        {
            return new Vector2(
                (pixelX - CanvasWidth * 0.5f) / PixelsPerUnit,
                (CanvasHeight * 0.5f - pixelY) / PixelsPerUnit);
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static void EnsureFolder(string folder)
        {
            string[] parts = folder.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }

                current = next;
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
