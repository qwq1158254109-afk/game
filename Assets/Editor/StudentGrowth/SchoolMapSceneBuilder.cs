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
    public static class SchoolMapSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/SchoolMapScene.unity";
        private const string TemplateScenePath = "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity";
        private const string ShellPath = "Assets/Art/New2_0/School/Source/SchoolShell.png";
        private const string PropsSheetPath = "Assets/Art/New2_0/School/Source/SchoolPropsSheet.png";
        private const string WallPropsSheetPath = "Assets/Art/New2_0/School/Source/SchoolWallPropsSheet.png";
        private const string ReferencePath = "Assets/Art/New2_0/School/Reference/SchoolTarget.png";
        private const string EditableRoot = "Assets/Art/New2_0/School/Editable";
        private const string PropRoot = EditableRoot + "/Props";
        private const string DataRoot = "Assets/Data/StudentGrowth/New2_0/SchoolMap";
        private const string SchoolHudThemePath = "Assets/Resources/StudentGrowth/UI/SchoolMapHudTheme.asset";
        private const float PixelsPerUnit = 64f;
        private const int CanvasWidth = 1536;
        private const int CanvasHeight = 1024;

        private static readonly SpriteSpec[] SpriteSpecs =
        {
            new SpriteSpec("TallCabinet", 32, 35, 246, 264),
            new SpriteSpec("LowCabinet", 556, 130, 151, 128),
            new SpriteSpec("SideCabinet", 348, 115, 151, 121),
            new SpriteSpec("TeacherPodium", 386, 316, 140, 146),
            new SpriteSpec("TeacherDesk", 558, 292, 183, 181),
            new SpriteSpec("StudentDesk", 94, 499, 144, 130),
            new SpriteSpec("StudentChair", 118, 648, 86, 88),
            new SpriteSpec("OpenShelf", 1266, 606, 184, 188),
            new SpriteSpec("PlantMedium", 752, 150, 92, 111),
            new SpriteSpec("PlantSmall", 858, 326, 63, 70),
            new SpriteSpec("PlantLarge", 1044, 101, 109, 128),
            new SpriteSpec("PlantTall", 1148, 83, 117, 157),
            new SpriteSpec("Cactus", 1239, 139, 91, 111),
            new SpriteSpec("WallPlant", 1204, 250, 99, 128),
            new SpriteSpec("Broom", 1323, 71, 86, 159),
            new SpriteSpec("Bucket", 1425, 145, 77, 91),
            new SpriteSpec("Books", 783, 325, 60, 71),
            new SpriteSpec("PencilBox", 936, 355, 56, 49),
            new SpriteSpec("StorageBox", 1323, 813, 78, 70),
            new SpriteSpec("ChalkWhite", 1077, 365, 43, 24),
            new SpriteSpec("ChalkPink", 1122, 365, 47, 25),
            new SpriteSpec("BoardPanel", 563, 343, 176, 52, new Vector4(13f, 10f, 13f, 10f)),
            new SpriteSpec("WoodPanel", 350, 115, 147, 56, new Vector4(13f, 10f, 13f, 10f)),
            new SpriteSpec("WallBlackboard", WallPropsSheetPath, 199, 150, 1138, 334),
            new SpriteSpec("WallSpeaker", WallPropsSheetPath, 712, 42, 111, 89),
            new SpriteSpec("WallClock", WallPropsSheetPath, 234, 605, 130, 129),
            new SpriteSpec("WallNoticeBoard", WallPropsSheetPath, 476, 552, 265, 214),
            new SpriteSpec("NoticePaperWhite", WallPropsSheetPath, 792, 554, 76, 93),
            new SpriteSpec("NoticePaperGreen", WallPropsSheetPath, 924, 553, 76, 96),
            new SpriteSpec("NoticePaperPink", WallPropsSheetPath, 1052, 553, 78, 110),
            new SpriteSpec("NoticePaperBlue", WallPropsSheetPath, 792, 678, 77, 93),
            new SpriteSpec("WallArt", WallPropsSheetPath, 1210, 638, 116, 207),
            new SpriteSpec("WallSwitch", WallPropsSheetPath, 994, 818, 64, 88)
        };

        [MenuItem("Tools/Student Growth/School/Rebuild Editable School Map")]
        public static void Rebuild()
        {
            EnsureFolder(EditableRoot);
            EnsureFolder(PropRoot);
            EnsureFolder(DataRoot);
            ConfigureShellTexture();
            ConfigureReferenceTexture();
            GenerateEditablePropSprites();
            Dictionary<string, Sprite> sprites = LoadGeneratedSprites();
            ActivityData classStudy = CreateActivity(
                "SchoolClassStudy.asset",
                "activity_school_class_study",
                "课堂学习",
                "认真完成一节课堂学习。",
                Reward(PlayerStatKey.Math, 4, "数学 +4"),
                Reward(PlayerStatKey.Chinese, 2, "语文 +2"));
            ActivityData teacherHelp = CreateActivity(
                "SchoolTeacherHelp.asset",
                "activity_school_teacher_help",
                "老师答疑",
                "在讲台前整理问题并向老师请教。",
                Reward(PlayerStatKey.Confidence, 3, "自信 +3"),
                Reward(PlayerStatKey.Math, 3, "数学 +3"));
            BuildScene(sprites, classStudy, teacherHelp);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("SchoolMapScene rebuilt from the supplied shell and prop sheet as an editable classroom map.");
        }

        [MenuItem("Tools/Student Growth/School/Validate Editable School Map")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(GameObject.Find("SchoolMapSceneRoot") != null, "SchoolMapSceneRoot is missing.");
            Require(GameObject.Find("SchoolMapRpgRoot") != null, "SchoolMapRpgRoot is missing.");
            Require(GameObject.Find("SchoolShell")?.GetComponent<SpriteRenderer>()?.sprite != null,
                "The supplied school shell is not bound to the scene.");
            Require(GameObject.Find("SchoolTargetReference") == null,
                "The target reference must not be used as a flattened runtime image.");
            Require(GameObject.Find("EditableSchoolFurniture")?.transform.childCount >= 35,
                "Editable school furniture objects are missing.");
            Require(FindByPrefix("StudentDesk_").Count == 12, "Expected 12 independently editable student desks.");
            Require(FindByPrefix("StudentChair_").Count == 12, "Expected 12 independently editable student chairs.");
            Require(GameObject.Find("Blackboard")?.GetComponent<SpriteRenderer>()?.sprite?.name.Contains("WallBlackboard") == true,
                "The supplied editable blackboard is missing.");
            foreach (string wallObjectName in new[] { "WallSpeaker", "WallClock", "NoticeBoard", "WallArt", "WallSwitch" })
            {
                Require(GameObject.Find(wallObjectName)?.GetComponent<SpriteRenderer>()?.sprite != null,
                    wallObjectName + " is missing its supplied wall sprite.");
            }
            Require(GameObject.Find("Collision")?.GetComponentsInChildren<BoxCollider2D>(true).Length >= 4,
                "School boundary collision is incomplete.");

            MapInteractionTarget[] targets = Object.FindObjectsByType<MapInteractionTarget>(FindObjectsInactive.Include);
            Require(targets.Length >= 3, "Expected at least three school interaction targets.");
            MapInteractionTarget exit = targets.FirstOrDefault(target => target.isExitPortal);
            Require(exit != null && !exit.consumeTimeOnInteraction, "School exit must not consume time.");

            FullscreenMapSceneController controller = Object.FindAnyObjectByType<FullscreenMapSceneController>();
            Require(controller != null && controller.mapId == "location_school", "School map controller is not configured.");
            Require(controller.player != null && controller.cameraController != null,
                "School player or camera references are missing.");
            Require(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == ScenePath),
                "SchoolMapScene is missing from Build Settings.");

            TextureImporter shellImporter = AssetImporter.GetAtPath(ShellPath) as TextureImporter;
            Require(shellImporter != null && Mathf.Approximately(shellImporter.spritePixelsPerUnit, PixelsPerUnit)
                && shellImporter.filterMode == FilterMode.Point && !shellImporter.mipmapEnabled,
                "School shell pixel-art import settings are invalid.");
            Debug.Log($"Editable School validation passed: {FindByPrefix("StudentDesk_").Count} desks, "
                + $"{FindByPrefix("StudentChair_").Count} chairs, {targets.Length} interactions and build settings are valid.");
        }

        private static void ConfigureShellTexture()
        {
            ConfigureSingleSprite(ShellPath, Vector4.zero);
        }

        private static void ConfigureReferenceTexture()
        {
            AssetDatabase.ImportAsset(ReferencePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(ReferencePath) as TextureImporter;
            Require(importer != null, "School target reference importer is missing.");
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void GenerateEditablePropSprites()
        {
            foreach (IGrouping<string, SpriteSpec> sourceGroup in SpriteSpecs.GroupBy(spec => spec.sourcePath))
            {
                string sourcePath = sourceGroup.Key;
                Require(File.Exists(Path.GetFullPath(sourcePath)), "School prop sheet is missing: " + sourcePath);
                Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(Path.GetFullPath(sourcePath)));
                Require(source.width == CanvasWidth && source.height == CanvasHeight,
                    $"Expected a {CanvasWidth}x{CanvasHeight} school prop sheet, got {source.width}x{source.height}: {sourcePath}");

                foreach (SpriteSpec spec in sourceGroup)
                {
                    Texture2D crop = new Texture2D(spec.width, spec.height, TextureFormat.RGBA32, false);
                    int sourceBottom = source.height - spec.yFromTop - spec.height;
                    Color[] pixels = source.GetPixels(spec.x, sourceBottom, spec.width, spec.height);
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        Color pixel = pixels[i];
                        float magentaDominance = Mathf.Min(pixel.r, pixel.b) - pixel.g;
                        bool magenta = pixel.r > 0.45f
                            && pixel.b > 0.45f
                            && Mathf.Abs(pixel.r - pixel.b) < 0.48f
                            && magentaDominance > 0.11f;
                        pixels[i] = magenta ? Color.clear : pixel;
                    }

                    crop.SetPixels(pixels);
                    crop.Apply();
                    File.WriteAllBytes(Path.GetFullPath(GetSpritePath(spec.name)), crop.EncodeToPNG());
                    Object.DestroyImmediate(crop);
                }

                Object.DestroyImmediate(source);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (SpriteSpec spec in SpriteSpecs)
            {
                ConfigureSingleSprite(GetSpritePath(spec.name), spec.border);
            }
        }

        private static void ConfigureSingleSprite(string path, Vector4 border)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Sprite importer is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            importer.spriteBorder = border;
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

        private static Dictionary<string, Sprite> LoadGeneratedSprites()
        {
            Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
            foreach (SpriteSpec spec in SpriteSpecs)
            {
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetSpritePath(spec.name));
                Require(sprite != null, "Generated school sprite is missing: " + spec.name);
                sprites[spec.name] = sprite;
            }

            return sprites;
        }

        private static ActivityData CreateActivity(
            string fileName,
            string id,
            string displayName,
            string description,
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

                activity = ScriptableObject.CreateInstance<SchoolMapActivityData>();
                AssetDatabase.CreateAsset(activity, path);
            }

            activity.activityId = id;
            activity.displayName = displayName;
            activity.description = description;
            activity.activityType = ActivityType.Training;
            activity.timeCost = 1;
            activity.fixedRewards.Clear();
            activity.fixedRewards.AddRange(rewards);
            EditorUtility.SetDirty(activity);
            return activity;
        }

        private static RewardData Reward(PlayerStatKey stat, int value, string description)
        {
            return new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = stat,
                value = value,
                description = description
            };
        }

        private static void BuildScene(
            IReadOnlyDictionary<string, Sprite> sprites,
            ActivityData classStudy,
            ActivityData teacherHelp)
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
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

            sceneRoot.name = "SchoolMapSceneRoot";
            MapInteractionFlowController flow = sceneRoot.GetComponentInChildren<MapInteractionFlowController>(true);
            Require(flow != null, "The map template is missing MapInteractionFlowController.");
            GameObject mapRoot = flow.gameObject;
            mapRoot.name = "SchoolMapRpgRoot";
            for (int i = mapRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(mapRoot.transform.GetChild(i).gameObject);
            }

            GameObject visualRoot = CreateChild("Visual", mapRoot.transform);
            GameObject shell = CreateSpriteObject(
                "SchoolShell",
                visualRoot.transform,
                AssetDatabase.LoadAssetAtPath<Sprite>(ShellPath),
                Vector2.zero,
                Vector3.one,
                -1000,
                false);
            shell.transform.localPosition = Vector3.zero;

            GameObject wallDecor = CreateChild("EditableWallDecor", visualRoot.transform);
            CreateSpriteObject("Blackboard", wallDecor.transform, sprites["WallBlackboard"],
                PixelToWorld(738f, 190f), new Vector3(0.535f, 0.535f, 1f), -300, true);
            CreateSpriteObject("WallSpeaker", wallDecor.transform, sprites["WallSpeaker"],
                PixelToWorld(772f, 86f), new Vector3(0.52f, 0.52f, 1f), -290, true);
            CreateSpriteObject("WallClock", wallDecor.transform, sprites["WallClock"],
                PixelToWorld(1128f, 96f), new Vector3(0.44f, 0.44f, 1f), -289, true);
            CreateSpriteObject("NoticeBoard", wallDecor.transform, sprites["WallNoticeBoard"],
                PixelToWorld(1128f, 191f), new Vector3(0.52f, 0.52f, 1f), -288, true);
            CreateSpriteObject("NoticePaper_White", wallDecor.transform, sprites["NoticePaperWhite"],
                PixelToWorld(1087f, 169f), new Vector3(0.28f, 0.28f, 1f), -280, true);
            CreateSpriteObject("NoticePaper_Green", wallDecor.transform, sprites["NoticePaperGreen"],
                PixelToWorld(1126f, 169f), new Vector3(0.28f, 0.28f, 1f), -279, true);
            CreateSpriteObject("NoticePaper_Pink", wallDecor.transform, sprites["NoticePaperPink"],
                PixelToWorld(1166f, 170f), new Vector3(0.28f, 0.28f, 1f), -278, true);
            CreateSpriteObject("NoticePaper_Blue", wallDecor.transform, sprites["NoticePaperBlue"],
                PixelToWorld(1105f, 217f), new Vector3(0.28f, 0.28f, 1f), -277, true);
            CreateSpriteObject("WallArt", wallDecor.transform, sprites["WallArt"],
                PixelToWorld(1447f, 296f), new Vector3(0.35f, 0.35f, 1f), -276, true);
            CreateSpriteObject("WallSwitch", wallDecor.transform, sprites["WallSwitch"],
                PixelToWorld(1247f, 195f), new Vector3(0.3f, 0.3f, 1f), -275, true);

            GameObject furnitureRoot = CreateChild("EditableSchoolFurniture", mapRoot.transform);
            CreateFurniture("TallCabinet", furnitureRoot.transform, sprites["TallCabinet"], 252f, 216f, 0.91f, true);
            CreateFurniture("TeacherPodium", furnitureRoot.transform, sprites["TeacherPodium"], 758f, 366f, 0.87f, true);
            GameObject teacherDesk = CreateFurniture("TeacherDesk", furnitureRoot.transform, sprites["TeacherDesk"],
                947f, 364f, 0.91f, true);
            CreateFurniture("RightSideCabinet", furnitureRoot.transform, sprites["LowCabinet"], 1190f, 306f, 0.91f, true);
            CreateFurniture("LeftSideCabinet", furnitureRoot.transform, sprites["SideCabinet"], 95f, 658f, 0.9f, true);
            CreateFurniture("OpenShelf", furnitureRoot.transform, sprites["OpenShelf"], 1405f, 802f, 0.9f, true);

            float[] columns = { 464f, 755f, 1020f };
            float[] deskRows = { 501f, 620f, 746f, 872f };
            float[] chairRows = { 539f, 662f, 787f, 921f };
            for (int row = 0; row < deskRows.Length; row++)
            {
                for (int column = 0; column < columns.Length; column++)
                {
                    string suffix = $"R{row + 1:00}_C{column + 1:00}";
                    CreateFurniture("StudentDesk_" + suffix, furnitureRoot.transform, sprites["StudentDesk"],
                        columns[column], deskRows[row], 0.88f, true);
                    CreateFurniture("StudentChair_" + suffix, furnitureRoot.transform, sprites["StudentChair"],
                        columns[column], chairRows[row], 0.84f, true);
                }
            }

            CreateOverlayProp("CabinetPlant", furnitureRoot.transform, sprites["PlantMedium"], 186f, 91f, 0.88f, 710);
            CreateOverlayProp("TeacherDeskPlant", furnitureRoot.transform, sprites["PlantSmall"], 1002f, 353f, 0.63f, 900);
            CreateOverlayProp("RightCabinetPlant", furnitureRoot.transform, sprites["PlantSmall"], 1175f, 260f, 0.7f, 780);
            CreateFurniture("BottomLeftPlant", furnitureRoot.transform, sprites["PlantLarge"], 87f, 788f, 0.72f, true);
            CreateFurniture("BottomLeftTallPlant", furnitureRoot.transform, sprites["PlantTall"], 91f, 826f, 0.62f, true);
            CreateFurniture("BottomLeftCactus", furnitureRoot.transform, sprites["Cactus"], 63f, 878f, 0.62f, true);
            CreateOverlayProp("WallShelfPlant", furnitureRoot.transform, sprites["WallPlant"], 1410f, 492f, 0.7f, 1100);
            CreateFurniture("CleaningBroom", furnitureRoot.transform, sprites["Broom"], 1360f, 586f, 0.82f, true);
            CreateFurniture("CleaningBucket", furnitureRoot.transform, sprites["Bucket"], 1417f, 609f, 0.8f, true);
            CreateOverlayProp("ShelfStorageBox", furnitureRoot.transform, sprites["StorageBox"], 1362f, 848f, 0.85f, 1600);
            CreateOverlayProp("TeacherBooks", furnitureRoot.transform, sprites["Books"], 890f, 354f, 0.78f, 900);
            CreateOverlayProp("TeacherPencilBox", furnitureRoot.transform, sprites["PencilBox"], 1226f, 279f, 0.75f, 790);

            GameObject rugs = CreateChild("EditableSchoolRugs", mapRoot.transform);
            CreateSlicedSprite("ReadingRug_Green", rugs.transform, sprites["BoardPanel"],
                PixelToWorld(1309f, 874f), new Vector2(2.55f, 2.25f), -850, new Color(0.72f, 0.78f, 0.42f));
            CreateSlicedSprite("ReadingRug_Tan", rugs.transform, sprites["WoodPanel"],
                PixelToWorld(1421f, 952f), new Vector2(1.85f, 0.63f), -849, new Color(0.92f, 0.82f, 0.62f));

            GameObject collisionRoot = CreateChild("Collision", mapRoot.transform);
            AddStaticCollider(collisionRoot.transform, "Wall_Left", new Vector2(-11.65f, -0.25f), new Vector2(0.55f, 14.4f));
            AddStaticCollider(collisionRoot.transform, "Wall_Right", new Vector2(11.65f, -0.25f), new Vector2(0.55f, 14.4f));
            AddStaticCollider(collisionRoot.transform, "Wall_Bottom", new Vector2(0f, -7.47f), new Vector2(23.2f, 0.45f));
            AddStaticCollider(collisionRoot.transform, "Wall_Top", new Vector2(0f, 3.15f), new Vector2(23.2f, 0.38f));

            GameObject spawnRoot = CreateChild("PlayerSpawnPoints", mapRoot.transform);
            GameObject spawn = CreateChild("SchoolEntrySpawn", spawnRoot.transform);
            spawn.transform.position = new Vector3(-2.35f, -6.4f, 0f);
            spawn.AddComponent<PlayerSpawnPoint>().spawnPointId = "school_entry";

            GameObject cameraBoundsObject = CreateChild("CameraBounds", mapRoot.transform);
            BoxCollider2D cameraBounds = cameraBoundsObject.AddComponent<BoxCollider2D>();
            cameraBounds.size = new Vector2(24f, 16f);
            cameraBounds.isTrigger = true;

            GameObject interactionRoot = CreateChild("InteractableObjects", mapRoot.transform);
            MapInteractionTarget boardTarget = CreateActivityInteraction(
                "BlackboardInteraction",
                interactionRoot.transform,
                new Vector2(-0.5f, 3f),
                new Vector2(6.8f, 1.05f),
                "school_blackboard_study",
                "课堂学习",
                "按 E 开始课堂学习",
                classStudy,
                20);
            MapInteractionTarget teacherTarget = CreateActivityInteraction(
                "TeacherDeskInteraction",
                interactionRoot.transform,
                teacherDesk.transform.position,
                new Vector2(2.5f, 1.65f),
                "school_teacher_help",
                "老师答疑",
                "按 E 向老师请教",
                teacherHelp,
                30);
            MapInteractionTarget exitTarget = CreateExitInteraction(interactionRoot.transform);

            ConfigurePlayer(player, flow);
            ConfigureCamera(cameraObject, player.transform, cameraBounds);
            ConfigureSceneController(sceneRoot, player, cameraObject, cameraBounds,
                new[] { boardTarget, teacherTarget, exitTarget });
            AddSceneToBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Failed to save SchoolMapScene.");
        }

        private static GameObject CreateFurniture(
            string name,
            Transform parent,
            Sprite sprite,
            float pixelX,
            float pixelY,
            float scale,
            bool addCollider)
        {
            GameObject target = CreateSpriteObject(name, parent, sprite, PixelToWorld(pixelX, pixelY),
                new Vector3(scale, scale, 1f), 1000, true);
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            float sortOffset = -renderer.bounds.extents.y + 0.05f;
            renderer.sortingOrder = MapObjectYSorter.CalculateSortingOrder(
                target.transform.position.y + sortOffset, 1000, 100f);
            MapObjectYSorter sorter = target.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };
            sorter.baseOrder = 1000;
            sorter.orderScale = 100f;
            sorter.sortYOffset = sortOffset;

            if (addCollider)
            {
                BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
                Vector2 size = renderer.sprite.bounds.size;
                collider.size = new Vector2(size.x * 0.8f, Mathf.Max(0.34f, size.y * 0.43f));
                collider.offset = new Vector2(0f, -size.y * 0.25f);
            }

            return target;
        }

        private static GameObject CreateOverlayProp(
            string name,
            Transform parent,
            Sprite sprite,
            float pixelX,
            float pixelY,
            float scale,
            int sortingOrder)
        {
            return CreateSpriteObject(name, parent, sprite, PixelToWorld(pixelX, pixelY),
                new Vector3(scale, scale, 1f), sortingOrder, true);
        }

        private static GameObject CreateSpriteObject(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector3 scale,
            int sortingOrder,
            bool editable)
        {
            GameObject target = CreateChild(name, parent);
            target.transform.position = new Vector3(position.x, position.y, 0f);
            target.transform.localScale = scale;
            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            if (editable)
            {
                target.tag = "Untagged";
            }

            return target;
        }

        private static GameObject CreateSlicedSprite(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector2 size,
            int sortingOrder,
            Color color)
        {
            GameObject target = CreateSpriteObject(name, parent, sprite, position, Vector3.one, sortingOrder, true);
            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = size;
            renderer.color = color;
            return target;
        }

        private static void AddStaticCollider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject target = CreateChild(name, parent);
            target.transform.localPosition = position;
            target.AddComponent<BoxCollider2D>().size = size;
        }

        private static MapInteractionTarget CreateActivityInteraction(
            string name,
            Transform parent,
            Vector2 position,
            Vector2 triggerSize,
            string actionId,
            string displayName,
            string prompt,
            ActivityData activity,
            int priority)
        {
            GameObject targetObject = CreateChild(name, parent);
            targetObject.transform.position = position;
            BoxCollider2D trigger = targetObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = triggerSize;

            MapInteractionPoint point = targetObject.AddComponent<MapInteractionPoint>();
            point.locationId = "location_school";
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
            GameObject exit = CreateChild("SchoolExitDoor", parent);
            exit.transform.position = new Vector3(8.65f, 2.7f, 0f);
            BoxCollider2D trigger = exit.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = new Vector2(1.65f, 0.75f);

            MapInteractionPoint point = exit.AddComponent<MapInteractionPoint>();
            point.locationId = "location_school";
            point.actionId = "school_exit";
            point.interactionType = MapInteractionType.ReturnToMainUi;
            point.displayName = "返回阶段地图";
            point.description = "离开教室并返回阶段地图。";
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = exit.AddComponent<MapInteractionTarget>();
            target.interactionId = "school_exit";
            target.displayName = "教室出口";
            target.promptText = "按 E 返回阶段地图";
            target.interactionPoint = point;
            target.isExitPortal = true;
            target.consumeTimeOnInteraction = false;
            target.requireFacing = false;
            target.interactionRange = 1.25f;
            target.priority = 100;

            MapExitPortal portal = exit.AddComponent<MapExitPortal>();
            portal.destinationId = "StageHubMapScene";
            portal.promptText = target.promptText;
            return target;
        }

        private static void ConfigurePlayer(GameObject player, MapInteractionFlowController flow)
        {
            player.transform.position = new Vector3(-2.35f, -6.4f, -0.1f);
            RpgPlayerController controller = player.GetComponent<RpgPlayerController>();
            Require(controller != null, "The map template player is missing RpgPlayerController.");
            controller.canMove = true;
            controller.useMovementBounds = true;
            controller.movementMin = new Vector2(-11.05f, -7.05f);
            controller.movementMax = new Vector2(11.05f, 2.85f);
            controller.interactionFlowController = flow;
            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sortingOrder = 1000;
            }
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
            sceneCamera.backgroundColor = Color.black;
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
            controller.mapId = "location_school";
            controller.displayName = "学校";
            controller.defaultSpawnPointId = "school_entry";
            controller.player = player.transform;
            controller.playerController = player.GetComponent<RpgPlayerController>();
            controller.facingInteractor = player.GetComponent<PlayerFacingInteractor>();
            controller.cameraController = cameraObject.GetComponent<MapCameraController>();
            controller.cameraBounds = cameraBounds;
            controller.interactionTargets = targets;
            controller.hudTheme = AssetDatabase.LoadAssetAtPath<UIThemeConfig>(SchoolHudThemePath);
            controller.createFallbackRuntimeServices = true;
            controller.autoCollectTargets = true;
        }

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(entry => entry.path != ScenePath))
            {
                int insertIndex = Mathf.Min(4, scenes.Count);
                scenes.Insert(insertIndex, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        private static Vector2 PixelToWorld(float pixelX, float pixelY)
        {
            return new Vector2(
                (pixelX - CanvasWidth * 0.5f) / PixelsPerUnit,
                (CanvasHeight * 0.5f - pixelY) / PixelsPerUnit);
        }

        private static List<GameObject> FindByPrefix(string prefix)
        {
            return Object.FindObjectsByType<Transform>(FindObjectsInactive.Include)
                .Where(item => item.name.StartsWith(prefix, StringComparison.Ordinal))
                .Select(item => item.gameObject)
                .ToList();
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child;
        }

        private static string GetSpritePath(string name)
        {
            return PropRoot + "/School_" + name + ".png";
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

        private readonly struct SpriteSpec
        {
            public readonly string name;
            public readonly string sourcePath;
            public readonly int x;
            public readonly int yFromTop;
            public readonly int width;
            public readonly int height;
            public readonly Vector4 border;

            public SpriteSpec(string name, int x, int yFromTop, int width, int height)
                : this(name, PropsSheetPath, x, yFromTop, width, height, Vector4.zero)
            {
            }

            public SpriteSpec(string name, int x, int yFromTop, int width, int height, Vector4 border)
                : this(name, PropsSheetPath, x, yFromTop, width, height, border)
            {
            }

            public SpriteSpec(string name, string sourcePath, int x, int yFromTop, int width, int height)
                : this(name, sourcePath, x, yFromTop, width, height, Vector4.zero)
            {
            }

            public SpriteSpec(string name, string sourcePath, int x, int yFromTop, int width, int height, Vector4 border)
            {
                this.name = name;
                this.sourcePath = sourcePath;
                this.x = x;
                this.yFromTop = yFromTop;
                this.width = width;
                this.height = height;
                this.border = border;
            }
        }
    }
}
