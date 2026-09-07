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
    public static class ClubRoomMapSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/ClubRoomMapScene.unity";
        private const string TemplateScenePath = "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity";
        private const string ShellPath = "Assets/Art/New2_0/Club/Source/ClubShell.png";
        private const string FacilitySheetPath = "Assets/Art/New2_0/Club/Source/ClubFacilitiesSheet.png";
        private const string ReferencePath = "Assets/Art/New2_0/Club/Reference/ClubLayoutReference.png";
        private const string EditableRoot = "Assets/Art/New2_0/Club/Editable/Facilities";
        private const string DataRoot = "Assets/Data/StudentGrowth/New2_0/ClubMap";
        private const string HudThemePath = "Assets/Resources/StudentGrowth/UI/SchoolMapHudTheme.asset";
        private const float PixelsPerUnit = 64f;
        private const int CanvasWidth = 1536;
        private const int CanvasHeight = 1024;

        private static readonly SpriteSpec[] SpriteSpecs =
        {
            new SpriteSpec("MeetingTable", 60, 132, 558, 274),
            new SpriteSpec("MetalShelf", 650, 80, 210, 332),
            new SpriteSpec("LowCabinet", 866, 180, 272, 188),
            new SpriteSpec("NoticeBoard", 1168, 80, 304, 212),
            new SpriteSpec("Whiteboard", 1158, 298, 318, 228),
            new SpriteSpec("Chair", 68, 414, 106, 170),
            new SpriteSpec("Sofa", 850, 405, 294, 204),
            new SpriteSpec("TallPlant", 56, 584, 122, 242),
            new SpriteSpec("StandingFan", 220, 596, 136, 214),
            new SpriteSpec("TrashCan", 400, 650, 116, 154),
            new SpriteSpec("CoatRack", 544, 604, 98, 248),
            new SpriteSpec("GreenRug", 690, 626, 420, 210),
            new SpriteSpec("CoffeeTable", 1152, 654, 196, 164),
            new SpriteSpec("OrangeBox", 88, 860, 96, 108),
            new SpriteSpec("BlueBox", 214, 860, 100, 108),
            new SpriteSpec("Books", 350, 858, 142, 108),
            new SpriteSpec("SmallBlueBox", 514, 868, 96, 100),
            new SpriteSpec("BrownBox", 736, 868, 100, 100),
            new SpriteSpec("Tissues", 866, 866, 102, 102),
            new SpriteSpec("OrangeBooks", 1014, 860, 102, 108),
            new SpriteSpec("SmallPlant", 1128, 842, 104, 128),
            new SpriteSpec("PurpleBox", 1242, 860, 106, 108),
            new SpriteSpec("SmallJar", 1354, 864, 86, 104)
        };

        [MenuItem("Tools/Student Growth/Club/Rebuild Editable Club Room")]
        public static void Rebuild()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            {
                EditorUtility.DisplayDialog(
                    "ClubRoomMapScene is the final layout source",
                    "V2 finalization makes the scene the source of truth. The generator is now first-build only "
                    + "and will not overwrite the formal scene. Duplicate the scene before testing generator changes.",
                    "OK");
                return;
            }

            EnsureFolder(EditableRoot);
            EnsureFolder(DataRoot);
            ConfigureSingleSprite(ShellPath);
            ConfigureReferenceTexture();
            GenerateFacilitySprites();
            Dictionary<string, Sprite> sprites = LoadFacilitySprites();
            ActivityData meeting = CreateActivity(
                "ClubMeeting.asset",
                "activity_club_meeting",
                "社团讨论",
                "和社团成员一起讨论下一次活动。",
                ActivityType.Event,
                Reward(PlayerStatKey.Social, 5, "社交 +5"),
                Reward(PlayerStatKey.Confidence, 2, "自信 +2"));
            ActivityData planning = CreateActivity(
                "ClubPlanning.asset",
                "activity_club_planning",
                "策划社团活动",
                "在白板前整理活动计划与分工。",
                ActivityType.Training,
                Reward(PlayerStatKey.Interest, 5, "兴趣 +5"),
                Reward(PlayerStatKey.Autonomy, 2, "自主 +2"));
            ActivityData relax = CreateActivity(
                "ClubRelax.asset",
                "activity_club_relax",
                "社团休息",
                "在休息区放松并和同伴闲聊。",
                ActivityType.Rest,
                Reward(PlayerStatKey.Mood, 6, "心情 +6"),
                Reward(RewardType.ReducePressure, PlayerStatKey.Pressure, 4, "压力 -4"));

            BuildScene(sprites, meeting, planning, relax);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("ClubRoomMapScene rebuilt from the supplied shell and facilities sheet with editable facility objects.");
        }

        [MenuItem("Tools/Student Growth/Club/Validate Editable Club Room")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid(), "ClubRoomMapScene could not be opened.");
            Require(GameObject.Find("ClubRoomMapSceneRoot") != null, "ClubRoomMapSceneRoot is missing.");
            Require(GameObject.Find("ClubRoomRpgRoot") != null, "ClubRoomRpgRoot is missing.");
            Require(GameObject.Find("ClubShell")?.GetComponent<SpriteRenderer>()?.sprite != null,
                "The supplied club shell is not bound to the scene.");
            Require(GameObject.Find("ClubLayoutReference") == null,
                "The layout reference must not be used as a flattened runtime image.");

            Transform facilities = GameObject.Find("EditableClubFacilities")?.transform;
            Require(facilities != null && facilities.childCount >= 26,
                "Editable club facility objects are missing.");
            Require(FindByPrefix("MeetingChair_").Count == 6,
                "Expected six independently editable meeting chairs.");
            Require(facilities.GetComponentsInChildren<SpriteRenderer>(true).All(renderer => renderer.sprite != null),
                "One or more club facilities have no sprite.");
            Require(facilities.GetComponentsInChildren<BoxCollider2D>(true).Length >= 13,
                "Club facility collision coverage is incomplete.");

            foreach (string wallItem in new[] { "NoticeBoard", "Whiteboard" })
            {
                Require(GameObject.Find(wallItem)?.GetComponent<SpriteRenderer>()?.sprite != null,
                    wallItem + " is missing its editable sprite.");
            }

            Require(GameObject.Find("Collision")?.GetComponentsInChildren<BoxCollider2D>(true).Length >= 5,
                "Club room boundary collision is incomplete.");
            MapInteractionTarget[] targets = Object.FindObjectsByType<MapInteractionTarget>(FindObjectsInactive.Include);
            Require(targets.Length >= 4, "Expected three club activities and an exit interaction.");
            Require(targets.Any(target => target.isExitPortal && !target.consumeTimeOnInteraction),
                "Club exit must not consume time.");

            FullscreenMapSceneController controller = Object.FindAnyObjectByType<FullscreenMapSceneController>();
            Require(controller != null && controller.mapId == "location_club",
                "Club room controller is not configured.");
            Require(controller.player != null && controller.cameraController != null,
                "Club room player or camera references are missing.");
            Require(EditorBuildSettings.scenes.Any(entry => entry.enabled && entry.path == ScenePath),
                "ClubRoomMapScene is missing from Build Settings.");
            Debug.Log($"Editable Club Room validation passed: {facilities.childCount} facilities, "
                + $"{targets.Length} interactions and build settings are valid.");
        }

        private static void GenerateFacilitySprites()
        {
            Require(File.Exists(Path.GetFullPath(FacilitySheetPath)), "Club facilities sheet is missing.");
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(Path.GetFullPath(FacilitySheetPath)));
            Require(source.width == CanvasWidth && source.height == CanvasHeight,
                $"Expected a {CanvasWidth}x{CanvasHeight} facility sheet, got {source.width}x{source.height}.");

            foreach (SpriteSpec spec in SpriteSpecs)
            {
                Texture2D crop = CropFromTop(source, spec);
                ClearBorderConnectedCheckerboard(crop);
                File.WriteAllBytes(Path.GetFullPath(GetSpritePath(spec.name)), crop.EncodeToPNG());
                Object.DestroyImmediate(crop);
            }

            Object.DestroyImmediate(source);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (SpriteSpec spec in SpriteSpecs)
            {
                ConfigureSingleSprite(GetSpritePath(spec.name));
            }
        }

        private static Texture2D CropFromTop(Texture2D source, SpriteSpec spec)
        {
            int sourceBottom = source.height - spec.yFromTop - spec.height;
            Texture2D crop = new Texture2D(spec.width, spec.height, TextureFormat.RGBA32, false);
            crop.SetPixels(source.GetPixels(spec.x, sourceBottom, spec.width, spec.height));
            crop.Apply();
            return crop;
        }

        private static void ClearBorderConnectedCheckerboard(Texture2D texture)
        {
            Color[] pixels = texture.GetPixels();
            bool[] visited = new bool[pixels.Length];
            Queue<int> queue = new Queue<int>();
            for (int x = 0; x < texture.width; x++)
            {
                EnqueueBackground(x, 0, texture.width, texture.height, pixels, visited, queue);
                EnqueueBackground(x, texture.height - 1, texture.width, texture.height, pixels, visited, queue);
            }

            for (int y = 0; y < texture.height; y++)
            {
                EnqueueBackground(0, y, texture.width, texture.height, pixels, visited, queue);
                EnqueueBackground(texture.width - 1, y, texture.width, texture.height, pixels, visited, queue);
            }

            int[] offsets = { -1, 1, -texture.width, texture.width };
            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % texture.width;
                int y = index / texture.width;
                pixels[index] = Color.clear;
                for (int i = 0; i < offsets.Length; i++)
                {
                    int next = index + offsets[i];
                    if (next < 0 || next >= pixels.Length)
                    {
                        continue;
                    }

                    int nextX = next % texture.width;
                    int nextY = next / texture.width;
                    if (Mathf.Abs(nextX - x) + Mathf.Abs(nextY - y) != 1)
                    {
                        continue;
                    }

                    EnqueueBackground(nextX, nextY, texture.width, texture.height, pixels, visited, queue);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
        }

        private static void EnqueueBackground(
            int x,
            int y,
            int width,
            int height,
            IReadOnlyList<Color> pixels,
            IList<bool> visited,
            Queue<int> queue)
        {
            if (x < 0 || x >= width || y < 0 || y >= height)
            {
                return;
            }

            int index = y * width + x;
            if (visited[index] || !IsCheckerboardPixel(pixels[index]))
            {
                return;
            }

            visited[index] = true;
            queue.Enqueue(index);
        }

        private static bool IsCheckerboardPixel(Color pixel)
        {
            float minimum = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
            float maximum = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
            return minimum > 0.82f && maximum - minimum < 0.055f;
        }

        private static void ConfigureSingleSprite(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Sprite importer is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
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

        private static void ConfigureReferenceTexture()
        {
            AssetDatabase.ImportAsset(ReferencePath, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = AssetImporter.GetAtPath(ReferencePath) as TextureImporter;
            Require(importer != null, "Club layout reference importer is missing.");
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static Dictionary<string, Sprite> LoadFacilitySprites()
        {
            return SpriteSpecs.ToDictionary(
                spec => spec.name,
                spec => AssetDatabase.LoadAssetAtPath<Sprite>(GetSpritePath(spec.name)),
                StringComparer.Ordinal);
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
                activity = ScriptableObject.CreateInstance<ClubMapActivityData>();
                AssetDatabase.CreateAsset(activity, path);
            }

            activity.activityId = id;
            activity.displayName = displayName;
            activity.description = description;
            activity.activityType = activityType;
            activity.timeCost = 1;
            activity.fixedRewards.Clear();
            activity.fixedRewards.AddRange(rewards);
            EditorUtility.SetDirty(activity);
            return activity;
        }

        private static RewardData Reward(PlayerStatKey stat, int value, string description)
        {
            return Reward(RewardType.AddPlayerStat, stat, value, description);
        }

        private static RewardData Reward(RewardType type, PlayerStatKey stat, int value, string description)
        {
            return new RewardData
            {
                rewardType = type,
                targetStat = stat,
                value = value,
                description = description
            };
        }

        private static void BuildScene(
            IReadOnlyDictionary<string, Sprite> sprites,
            ActivityData meeting,
            ActivityData planning,
            ActivityData relax)
        {
            Dictionary<string, TransformSnapshot> preservedLayout = CaptureExistingLayout();
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

            sceneRoot.name = "ClubRoomMapSceneRoot";
            MapInteractionFlowController flow = sceneRoot.GetComponentInChildren<MapInteractionFlowController>(true);
            Require(flow != null, "The map template is missing MapInteractionFlowController.");
            GameObject mapRoot = flow.gameObject;
            mapRoot.name = "ClubRoomRpgRoot";
            for (int i = mapRoot.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(mapRoot.transform.GetChild(i).gameObject);
            }

            GameObject visualRoot = CreateChild("Visual", mapRoot.transform);
            CreateSpriteObject("ClubShell", visualRoot.transform, AssetDatabase.LoadAssetAtPath<Sprite>(ShellPath),
                Vector2.zero, Vector3.one, -1000);

            GameObject wallRoot = CreateChild("EditableClubWallDecor", visualRoot.transform);
            CreateEditable("NoticeBoard", wallRoot.transform, sprites["NoticeBoard"], 658f, 183f, 0.9f, -500,
                false, preservedLayout);
            CreateEditable("Whiteboard", wallRoot.transform, sprites["Whiteboard"], 960f, 194f, 0.9f, -490,
                false, preservedLayout);

            GameObject facilityRoot = CreateChild("EditableClubFacilities", mapRoot.transform);
            CreateEditable("GreenRug", facilityRoot.transform, sprites["GreenRug"], 1180f, 800f, 0.82f, -400,
                false, preservedLayout);
            CreateEditable("MetalShelf", facilityRoot.transform, sprites["MetalShelf"], 205f, 288f, 0.9f, 1000,
                true, preservedLayout, 0.82f, 0.28f);
            CreateEditable("LowCabinet", facilityRoot.transform, sprites["LowCabinet"], 414f, 320f, 0.84f, 1000,
                true, preservedLayout, 0.84f, 0.35f);
            CreateEditable("TallPlant", facilityRoot.transform, sprites["TallPlant"], 1196f, 273f, 0.84f, 1000,
                true, preservedLayout, 0.56f, 0.24f);
            CreateEditable("StandingFan", facilityRoot.transform, sprites["StandingFan"], 1308f, 390f, 0.82f, 1000,
                true, preservedLayout, 0.55f, 0.2f);
            CreateEditable("TrashCan", facilityRoot.transform, sprites["TrashCan"], 1350f, 526f, 0.78f, 1000,
                true, preservedLayout, 0.65f, 0.3f);
            CreateEditable("CoatRack", facilityRoot.transform, sprites["CoatRack"], 1432f, 650f, 0.83f, 1000,
                true, preservedLayout, 0.55f, 0.18f);
            CreateEditable("MeetingTable", facilityRoot.transform, sprites["MeetingTable"], 708f, 582f, 0.88f, 1000,
                true, preservedLayout, 0.86f, 0.34f);

            float[] chairColumns = { 570f, 705f, 840f };
            for (int i = 0; i < chairColumns.Length; i++)
            {
                CreateEditable($"MeetingChair_Top_{i + 1:00}", facilityRoot.transform, sprites["Chair"],
                    chairColumns[i], 468f, 0.88f, 1000, true, preservedLayout, 0.62f, 0.3f);
                CreateEditable($"MeetingChair_Bottom_{i + 1:00}", facilityRoot.transform, sprites["Chair"],
                    chairColumns[i], 716f, 0.88f, 1000, true, preservedLayout, 0.62f, 0.3f);
            }

            CreateEditable("Sofa", facilityRoot.transform, sprites["Sofa"], 1300f, 765f, 0.9f, 1000,
                true, preservedLayout, 0.84f, 0.36f);
            CreateEditable("CoffeeTable", facilityRoot.transform, sprites["CoffeeTable"], 1145f, 797f, 0.82f, 1000,
                true, preservedLayout, 0.8f, 0.34f);

            CreateShelfAndCabinetProps(facilityRoot.transform, sprites, preservedLayout);

            GameObject collisionRoot = CreateChild("Collision", mapRoot.transform);
            AddStaticCollider(collisionRoot.transform, "Wall_Left", PixelToWorld(56f, 615f), new Vector2(0.48f, 9.2f));
            AddStaticCollider(collisionRoot.transform, "Wall_Right", PixelToWorld(1475f, 615f), new Vector2(0.48f, 9.2f));
            AddStaticCollider(collisionRoot.transform, "Wall_Top", PixelToWorld(768f, 328f), new Vector2(22.2f, 0.42f));
            AddStaticCollider(collisionRoot.transform, "Wall_BottomLeft", PixelToWorld(205f, 924f), new Vector2(4.5f, 0.48f));
            AddStaticCollider(collisionRoot.transform, "Wall_BottomRight", PixelToWorld(990f, 924f), new Vector2(14.2f, 0.48f));

            GameObject spawnRoot = CreateChild("PlayerSpawnPoints", mapRoot.transform);
            GameObject spawn = CreateChild("ClubEntrySpawn", spawnRoot.transform);
            spawn.transform.position = PixelToWorld(418f, 835f);
            spawn.AddComponent<PlayerSpawnPoint>().spawnPointId = "club_entry";

            GameObject cameraBoundsObject = CreateChild("CameraBounds", mapRoot.transform);
            BoxCollider2D cameraBounds = cameraBoundsObject.AddComponent<BoxCollider2D>();
            cameraBounds.size = new Vector2(24f, 16f);
            cameraBounds.isTrigger = true;

            GameObject interactionRoot = CreateChild("InteractableObjects", mapRoot.transform);
            MapInteractionTarget meetingTarget = CreateActivityInteraction(
                "MeetingTableInteraction", interactionRoot.transform, PixelToWorld(708f, 620f),
                "club_meeting", "社团讨论", "按 E 参加社团讨论", meeting, 30, 3.8f);
            MapInteractionTarget boardTarget = CreateActivityInteraction(
                "WhiteboardInteraction", interactionRoot.transform, PixelToWorld(960f, 325f),
                "club_planning", "策划社团活动", "按 E 策划社团活动", planning, 25, 2.4f);
            MapInteractionTarget sofaTarget = CreateActivityInteraction(
                "SofaInteraction", interactionRoot.transform, PixelToWorld(1295f, 790f),
                "club_relax", "社团休息", "按 E 在休息区放松", relax, 20, 2.5f);
            MapInteractionTarget exitTarget = CreateExitInteraction(interactionRoot.transform);

            ConfigurePlayer(player, flow);
            ConfigureCamera(cameraObject, player.transform, cameraBounds);
            ConfigureSceneController(sceneRoot, player, cameraObject, cameraBounds,
                new[] { meetingTarget, boardTarget, sofaTarget, exitTarget });
            AddSceneToBuildSettings();

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Failed to save ClubRoomMapScene.");
        }

        private static void CreateShelfAndCabinetProps(
            Transform parent,
            IReadOnlyDictionary<string, Sprite> sprites,
            IReadOnlyDictionary<string, TransformSnapshot> preservedLayout)
        {
            CreateEditable("ShelfOrangeBoxTop", parent, sprites["OrangeBox"], 160f, 142f, 0.58f, 1500, false, preservedLayout);
            CreateEditable("ShelfBlueBoxTop", parent, sprites["BlueBox"], 239f, 143f, 0.55f, 1501, false, preservedLayout);
            CreateEditable("ShelfBooks", parent, sprites["Books"], 169f, 232f, 0.6f, 1502, false, preservedLayout);
            CreateEditable("ShelfBlueBoxMiddle", parent, sprites["SmallBlueBox"], 255f, 238f, 0.56f, 1503, false, preservedLayout);
            CreateEditable("ShelfBrownBox", parent, sprites["BrownBox"], 158f, 318f, 0.55f, 1504, false, preservedLayout);
            CreateEditable("ShelfBlueBoxBottomA", parent, sprites["SmallBlueBox"], 218f, 318f, 0.52f, 1505, false, preservedLayout);
            CreateEditable("ShelfBlueBoxBottomB", parent, sprites["SmallBlueBox"], 261f, 318f, 0.52f, 1506, false, preservedLayout);
            CreateEditable("ShelfOrangeBoxBottom", parent, sprites["OrangeBox"], 158f, 399f, 0.55f, 1507, false, preservedLayout);
            CreateEditable("ShelfTissues", parent, sprites["Tissues"], 241f, 394f, 0.55f, 1508, false, preservedLayout);

            CreateEditable("CabinetOrangeBooks", parent, sprites["OrangeBooks"], 338f, 248f, 0.58f, 1510, false, preservedLayout);
            CreateEditable("CabinetSmallPlant", parent, sprites["SmallPlant"], 390f, 233f, 0.5f, 1511, false, preservedLayout);
            CreateEditable("CabinetPurpleBox", parent, sprites["PurpleBox"], 445f, 251f, 0.5f, 1512, false, preservedLayout);
            CreateEditable("CabinetSmallJar", parent, sprites["SmallJar"], 493f, 252f, 0.48f, 1513, false, preservedLayout);
        }

        private static GameObject CreateEditable(
            string name,
            Transform parent,
            Sprite sprite,
            float pixelX,
            float pixelY,
            float scale,
            int sortingOrder,
            bool addCollider,
            IReadOnlyDictionary<string, TransformSnapshot> preservedLayout,
            float colliderWidthFactor = 0.8f,
            float colliderHeightFactor = 0.34f)
        {
            Require(sprite != null, "Club facility sprite failed to load: " + name);
            GameObject target = CreateSpriteObject(name, parent, sprite, PixelToWorld(pixelX, pixelY),
                new Vector3(scale, scale, 1f), sortingOrder);
            if (preservedLayout.TryGetValue(name, out TransformSnapshot snapshot))
            {
                target.transform.position = snapshot.position;
                target.transform.rotation = snapshot.rotation;
                target.transform.localScale = snapshot.scale;
            }

            SpriteRenderer renderer = target.GetComponent<SpriteRenderer>();
            if (sortingOrder >= 1000 && sortingOrder < 1500)
            {
                float sortOffset = -renderer.bounds.extents.y + 0.05f;
                renderer.sortingOrder = MapObjectYSorter.CalculateSortingOrder(
                    target.transform.position.y + sortOffset, 1000, 100f);
                MapObjectYSorter sorter = target.AddComponent<MapObjectYSorter>();
                sorter.renderers = new[] { renderer };
                sorter.baseOrder = 1000;
                sorter.orderScale = 100f;
                sorter.sortYOffset = sortOffset;
            }

            if (addCollider)
            {
                Vector2 spriteSize = renderer.sprite.bounds.size;
                BoxCollider2D collider = target.AddComponent<BoxCollider2D>();
                collider.size = new Vector2(spriteSize.x * colliderWidthFactor,
                    Mathf.Max(0.28f, spriteSize.y * colliderHeightFactor));
                collider.offset = new Vector2(0f, -spriteSize.y * (0.5f - colliderHeightFactor * 0.5f));
            }

            return target;
        }

        private static GameObject CreateSpriteObject(
            string name,
            Transform parent,
            Sprite sprite,
            Vector2 position,
            Vector3 scale,
            int sortingOrder)
        {
            GameObject target = CreateChild(name, parent);
            target.transform.position = new Vector3(position.x, position.y, 0f);
            target.transform.localScale = scale;
            SpriteRenderer renderer = target.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return target;
        }

        private static void AddStaticCollider(Transform parent, string name, Vector2 position, Vector2 size)
        {
            GameObject target = CreateChild(name, parent);
            target.transform.position = position;
            target.AddComponent<BoxCollider2D>().size = size;
        }

        private static MapInteractionTarget CreateActivityInteraction(
            string name,
            Transform parent,
            Vector2 position,
            string actionId,
            string displayName,
            string prompt,
            ActivityData activity,
            int priority,
            float interactionRange)
        {
            GameObject targetObject = CreateChild(name, parent);
            targetObject.transform.position = position;

            MapInteractionPoint point = targetObject.AddComponent<MapInteractionPoint>();
            point.locationId = "location_club";
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
            target.interactionRange = interactionRange;
            target.priority = priority;
            return target;
        }

        private static MapInteractionTarget CreateExitInteraction(Transform parent)
        {
            GameObject exit = CreateChild("ClubExitDoor", parent);
            exit.transform.position = PixelToWorld(418f, 884f);

            MapInteractionPoint point = exit.AddComponent<MapInteractionPoint>();
            point.locationId = "location_club";
            point.actionId = "club_exit";
            point.interactionType = MapInteractionType.ReturnToMainUi;
            point.displayName = "返回阶段地图";
            point.description = "离开社团活动室并返回阶段地图。";
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = exit.AddComponent<MapInteractionTarget>();
            target.interactionId = "club_exit";
            target.displayName = "社团活动室出口";
            target.promptText = "按 E 返回阶段地图";
            target.interactionPoint = point;
            target.isExitPortal = true;
            target.consumeTimeOnInteraction = false;
            target.requireFacing = false;
            target.interactionRange = 1.6f;
            target.priority = 100;

            MapExitPortal portal = exit.AddComponent<MapExitPortal>();
            portal.destinationId = "StageHubMapScene";
            portal.promptText = target.promptText;
            return target;
        }

        private static void ConfigurePlayer(GameObject player, MapInteractionFlowController flow)
        {
            player.transform.position = PixelToWorld(418f, 835f);
            RpgPlayerController controller = player.GetComponent<RpgPlayerController>();
            Require(controller != null, "The map template player is missing RpgPlayerController.");
            controller.canMove = true;
            controller.useMovementBounds = true;
            controller.movementMin = new Vector2(-10.9f, -6.65f);
            controller.movementMax = new Vector2(10.9f, 2.7f);
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
            controller.mapId = "location_club";
            controller.displayName = "社团活动室";
            controller.defaultSpawnPointId = "club_entry";
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

        private static Dictionary<string, TransformSnapshot> CaptureExistingLayout()
        {
            Dictionary<string, TransformSnapshot> layout = new Dictionary<string, TransformSnapshot>(StringComparer.Ordinal);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                return layout;
            }

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (string rootName in new[] { "EditableClubFacilities", "EditableClubWallDecor" })
            {
                Transform root = scene.GetRootGameObjects()
                    .SelectMany(item => item.GetComponentsInChildren<Transform>(true))
                    .FirstOrDefault(item => item.name == rootName);
                if (root == null)
                {
                    continue;
                }

                foreach (Transform child in root)
                {
                    layout[child.name] = new TransformSnapshot(
                        child.position,
                        child.rotation,
                        child.localScale);
                }
            }

            return layout;
        }

        private static void AddSceneToBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(entry => entry.path != ScenePath))
            {
                int insertIndex = Mathf.Min(6, scenes.Count);
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
            return EditableRoot + "/Club_" + name + ".png";
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
            public readonly int x;
            public readonly int yFromTop;
            public readonly int width;
            public readonly int height;

            public SpriteSpec(string name, int x, int yFromTop, int width, int height)
            {
                this.name = name;
                this.x = x;
                this.yFromTop = yFromTop;
                this.width = width;
                this.height = height;
            }
        }

        private readonly struct TransformSnapshot
        {
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public readonly Vector3 scale;

            public TransformSnapshot(Vector3 position, Quaternion rotation, Vector3 scale)
            {
                this.position = position;
                this.rotation = rotation;
                this.scale = scale;
            }
        }
    }
}
