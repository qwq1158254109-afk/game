using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace StudentGrowth.Editor
{
    public static class HomeReferenceMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity";
        private const string BackupRoot = "Assets/Scenes/StudentGrowth/Maps/Backups";

        private const string SourceRoot = "Assets/Art/Pixel/Backgrounds/NewHomeSources";
        private const string SourceItems = SourceRoot + "/home_new_item.png";
        private const string SourceEmptyRoom = SourceRoot + "/home_new_space.png";
        private const string SourceReference = SourceRoot + "/home_new_reference.png";
        private const string GeneratedRoot = "Assets/Art/Pixel/Generated/HomeReference";
        private const string GeneratedPropRoot = GeneratedRoot + "/Props";
        private const string GeneratedTileRoot = GeneratedRoot + "/Tiles";
        private const string DataRoot = "Assets/Data/StudentGrowth/New2_0/HomeMap";
        private const string DocsRoot = "Assets/Docs/StudentGrowth/New2_0";
        private const string ReportPath = DocsRoot + "/HomeMapScene_Figure3_RebuildReport.md";

        private const string PlayerSpritePath = "Assets/Art/New2_0/Characters/Home/New/PinkPlayer/Frames/pink_player_down_1.png";
        private const string PlayerAnimatorPath = "Assets/Animations/StudentGrowth/New2_0/HomeMap/New20_HomePlayer.controller";
        private const string FamilyNpcSourcePath = "Assets/Art/New2_0/Characters/Home/New/new_anime_character_walk_32x32_with_costumes.png";
        private const string FamilyNpcSpritePath = GeneratedPropRoot + "/home_ref_family_npc_black_hair.png";

        private const float Ppu = 64f;
        private const int SourceWidth = 1448;
        private const int SourceHeight = 1086;
        private const int MapMinX = -15;
        private const int MapMaxX = 14;
        private const int MapMinY = -10;
        private const int MapMaxY = 9;
        private const int MapWidth = MapMaxX - MapMinX + 1;
        private const int MapHeight = MapMaxY - MapMinY + 1;

        [MenuItem("Tools/Student Growth/New Art/Rebuild HomeMapScene From Figure 3")]
        public static void RebuildHomeMapSceneFromFigure3()
        {
            EnsureFolders();
            ConfigureSourceImporters();
            HomeReferenceAssets assets = GenerateAssets();
            BuildScene(assets);
            WriteReport(assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("HomeMapScene rebuilt from figure 3 reference art.");
        }

        [MenuItem("Tools/Student Growth/New Art/Verify HomeMapScene Figure 3")]
        public static void VerifyHomeMapSceneFigure3()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(GameObject.Find("HomeMapSceneRoot") != null, "HomeMapSceneRoot is missing.");
            Require(GameObject.Find("HomeMapRpgRoot") != null, "HomeMapRpgRoot is missing.");
            Require(GameObject.Find("RoomSpace_Background") != null, "RoomSpace_Background is missing.");

            foreach (string layer in RequiredTilemapLayers())
            {
                Require(GameObject.Find(layer) != null, layer + " is missing.");
            }

            GameObject collision = GameObject.Find("Collision_Tilemap");
            Require(collision != null, "Collision_Tilemap is missing.");
            Require(collision.GetComponent<TilemapCollider2D>() != null, "Collision_Tilemap needs TilemapCollider2D.");
            Require(collision.GetComponent<CompositeCollider2D>() != null, "Collision_Tilemap needs CompositeCollider2D.");
            Rigidbody2D collisionBody = collision.GetComponent<Rigidbody2D>();
            Require(collisionBody != null && collisionBody.bodyType == RigidbodyType2D.Static, "Collision_Tilemap needs static Rigidbody2D.");
            GameObject collisionBounds = GameObject.Find("CollisionBounds");
            Require(collisionBounds != null && collisionBounds.GetComponentsInChildren<BoxCollider2D>().Length >= 5, "CollisionBounds needs fallback BoxCollider2D walls.");

            foreach (string objectName in new[] { "BedObject", "DeskObject", "FamilyNpcObject", "ExitDoorObject" })
            {
                GameObject target = GameObject.Find(objectName);
                Require(target != null, objectName + " is missing.");
                Require(target.GetComponent<MapInteractionPoint>() != null, objectName + " needs MapInteractionPoint.");
                Require(target.GetComponent<MapInteractionTarget>() != null, objectName + " needs MapInteractionTarget.");
                Require(target.transform.Find("Visual") != null, objectName + " needs Visual child.");
                Require(target.transform.Find("InteractionTrigger") != null, objectName + " needs InteractionTrigger child.");
            }

            MapInteractionTarget exit = GameObject.Find("ExitDoorObject").GetComponent<MapInteractionTarget>();
            Require(exit.isExitPortal && !exit.consumeTimeOnInteraction, "ExitDoorObject must be a no-time-cost exit.");
            MapDoorAnimator doorAnimator = exit.GetComponent<MapDoorAnimator>();
            Require(doorAnimator != null, "ExitDoorObject needs MapDoorAnimator.");
            Require(doorAnimator.openFrames != null && doorAnimator.openFrames.Length >= 4, "ExitDoorObject needs at least four open animation frames.");
            Require(GameObject.Find("ComputerStudyPoint") != null, "ComputerStudyPoint is missing.");
            Require(GameObject.Find("PlayerRpg") != null, "PlayerRpg is missing.");
            Require(GameObject.Find("DefaultSpawnPoint") != null, "DefaultSpawnPoint is missing.");
            Debug.Log("HomeMapScene figure 3 verification passed.");
        }

        private static IEnumerable<string> RequiredTilemapLayers()
        {
            return new[]
            {
                "Ground_Tilemap",
                "Wall_Tilemap",
                "FloorTrim_Tilemap",
                "WallTop_Tilemap",
                "Decoration_Tilemap",
                "FurnitureBase_Tilemap",
                "Props_Tilemap",
                "Collision_Tilemap",
                "AbovePlayer_Tilemap",
                "Interaction_Tilemap",
                "TriggerGuide_Tilemap"
            };
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
            {
                SourceRoot,
                GeneratedRoot,
                GeneratedPropRoot,
                GeneratedTileRoot,
                DataRoot,
                BackupRoot,
                DocsRoot
            })
            {
                EnsureFolder(folder);
            }
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

        private static void ConfigureSourceImporters()
        {
            foreach (string path in new[] { SourceItems, SourceEmptyRoom, SourceReference })
            {
                ConfigureTextureImporter(path, Ppu, true);
            }
        }

        private static void ConfigureTextureImporter(string assetPath, float ppu, bool readable)
        {
            if (!File.Exists(Path.GetFullPath(assetPath)))
            {
                throw new FileNotFoundException("Required home source art is missing.", assetPath);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Texture importer missing for " + assetPath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        private static HomeReferenceAssets GenerateAssets()
        {
            HomeReferenceAssets assets = new HomeReferenceAssets();
            Texture2D itemSheet = LoadTexture(SourceItems);
            Texture2D roomSheet = LoadTexture(SourceEmptyRoom);
            Color key = itemSheet.GetPixel(0, itemSheet.height - 1);

            assets.roomBackgroundSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SourceEmptyRoom);
            assets.referenceSprite = AssetDatabase.LoadAssetAtPath<Sprite>(SourceReference);
            assets.collisionSprite = CreateSolidSprite(GeneratedTileRoot + "/home_ref_collision_tile.png", new Color(1f, 1f, 1f, 0f));
            assets.markerSprite = CreateMarkerSprite(GeneratedTileRoot + "/home_ref_interaction_marker.png");
            assets.collisionTile = CreateTile(GeneratedTileRoot + "/HomeRefCollisionTile.asset", assets.collisionSprite, new Color(1f, 1f, 1f, 0f), Tile.ColliderType.Grid);
            assets.markerTile = CreateTile(GeneratedTileRoot + "/HomeRefInteractionMarkerTile.asset", assets.markerSprite, new Color(1f, 0.92f, 0.35f, 0.45f), Tile.ColliderType.None);

            assets.bedSprite = CropProp(itemSheet, key, "home_ref_bed.png", 16, 42, 198, 277);
            assets.nightstandSprite = CropProp(itemSheet, key, "home_ref_nightstand.png", 239, 99, 96, 144);
            assets.deskSprite = CropProp(itemSheet, key, "home_ref_computer_desk.png", 375, 22, 221, 285);
            assets.chairSprite = CropProp(itemSheet, key, "home_ref_chair.png", 655, 128, 121, 140);
            assets.tallBookshelfSprite = CropProp(itemSheet, key, "home_ref_tall_bookshelf.png", 807, 40, 121, 245);
            assets.wardrobeSprite = CropProp(itemSheet, key, "home_ref_wardrobe.png", 990, 37, 240, 281);
            assets.tallPlantSprite = CropProp(itemSheet, key, "home_ref_tall_plant.png", 928, 126, 49, 130);
            assets.shoeCabinetSprite = CropProp(itemSheet, key, "home_ref_shoe_cabinet.png", 1250, 187, 86, 132);
            assets.shoeRackSprite = CropProp(itemSheet, key, "home_ref_shoe_rack.png", 1351, 206, 79, 145);
            assets.leftBookshelfSprite = CropProp(itemSheet, key, "home_ref_left_bookshelf.png", 35, 344, 196, 151);
            assets.lowCabinetSprite = CropProp(itemSheet, key, "home_ref_low_cabinet.png", 292, 352, 184, 133);
            assets.sofaSprite = CropProp(itemSheet, key, "home_ref_sofa.png", 607, 350, 234, 153);
            assets.sofaTableSprite = CropProp(itemSheet, key, "home_ref_sofa_side_table.png", 859, 387, 54, 104);
            assets.coffeeTableSprite = CropProp(itemSheet, key, "home_ref_coffee_table.png", 973, 392, 184, 101);
            assets.tableShelfSprite = CropProp(itemSheet, key, "home_ref_table_shelf.png", 1227, 390, 160, 100);
            assets.fridgeSprite = CropProp(itemSheet, key, "home_ref_fridge.png", 63, 521, 91, 195);
            assets.sinkSprite = CropProp(itemSheet, key, "home_ref_sink.png", 247, 541, 191, 169);
            assets.stoveSprite = CropProp(itemSheet, key, "home_ref_stove.png", 486, 517, 119, 194);
            assets.kitchenIslandSprite = CropProp(itemSheet, key, "home_ref_kitchen_island.png", 683, 563, 218, 148);
            assets.kitchenDrawerSprite = CropProp(itemSheet, key, "home_ref_kitchen_drawer.png", 1181, 553, 93, 162);
            assets.smallCabinetSprite = CropProp(itemSheet, key, "home_ref_small_cabinet.png", 979, 566, 82, 136);
            assets.wallShelfSprite = CropProp(itemSheet, key, "home_ref_wall_shelf.png", 505, 748, 202, 107);
            assets.coatRackSprite = CropProp(itemSheet, key, "home_ref_coat_rack.png", 1294, 749, 88, 109);
            assets.largeRugSprite = CropProp(itemSheet, key, "home_ref_large_rug.png", 32, 858, 336, 183);
            assets.greenRugSprite = CropProp(itemSheet, key, "home_ref_green_rug.png", 405, 905, 252, 125);
            assets.roundRugSprite = CropProp(itemSheet, key, "home_ref_round_rug.png", 1014, 942, 137, 91);
            assets.greenCushionSprite = CropProp(itemSheet, key, "home_ref_green_cushion.png", 718, 942, 101, 83);
            assets.yellowCushionSprite = CropProp(itemSheet, key, "home_ref_yellow_cushion.png", 879, 949, 86, 75);
            assets.smallPlantSprite = CropProp(itemSheet, key, "home_ref_small_plant.png", 248, 753, 56, 68);
            assets.floorLampSprite = CropProp(itemSheet, key, "home_ref_floor_lamp.png", 1267, 92, 36, 53);
            assets.exitDoorSprite = CropRoomProp(roomSheet, "home_ref_exit_door.png", 675, 843, 150, 125);
            assets.exitDoorOpenFrames = CreateDoorOpenAnimationFrames(GeneratedPropRoot + "/home_ref_exit_door.png", assets.exitDoorSprite);
            assets.exitDoorCloseFrames = ReverseFrames(assets.exitDoorOpenFrames);

            assets.restActivity = CreateActivity(
                "New20_HomeRestEarlySleep.asset",
                "activity_home_rest_early_sleep",
                "早点休息",
                "在床边早点休息，恢复精力并降低压力。",
                ActivityType.Rest,
                Reward(PlayerStatKey.Energy, 20, "精力 +20"),
                ReducePressure(5, "压力 -5"));
            assets.planActivity = CreateActivity(
                "New20_HomeDeskPlan.asset",
                "activity_home_desk_plan",
                "整理计划",
                "在书桌整理明日计划。",
                ActivityType.Training,
                Reward(PlayerStatKey.Autonomy, 5, "自主 +5"),
                Reward(PlayerStatKey.Mood, 2, "心情 +2"));
            assets.computerActivity = CreateActivity(
                "New20_HomeComputerStudy.asset",
                "activity_home_computer_study",
                "电脑学习",
                "使用电脑完成在线练习，提升数学并略微增加压力。",
                ActivityType.Training,
                Reward(PlayerStatKey.Math, 6, "数学 +6"),
                Reward(PlayerStatKey.Autonomy, 2, "自主 +2"),
                AddPressure(3, "压力 +3"));
            assets.familyDinnerActivity = CreateActivity(
                "New20_HomeFamilyDinner.asset",
                "activity_home_family_dinner",
                "家庭晚饭聊天",
                "和家人边吃晚饭边聊聊近况。",
                ActivityType.Event,
                Reward(PlayerStatKey.ParentRelation, 5, "亲子 +5"),
                Reward(PlayerStatKey.Mood, 5, "心情 +5"));

            assets.playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            assets.playerAnimator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorPath);
            assets.familyNpcSprite = CreateCharacterFrameSprite(FamilyNpcSourcePath, "home_ref_family_npc_black_hair.png", 192, 0, 32, 32, 16f);
            Require(assets.roomBackgroundSprite != null, "Room background sprite is missing.");
            Require(assets.playerSprite != null, "Player sprite is missing: " + PlayerSpritePath);
            Require(assets.playerAnimator != null, "Player animator is missing: " + PlayerAnimatorPath);
            Require(assets.familyNpcSprite != null, "Family NPC sprite is missing: " + FamilyNpcSpritePath);
            UnityEngine.Object.DestroyImmediate(itemSheet);
            UnityEngine.Object.DestroyImmediate(roomSheet);
            return assets;
        }

        private static void BuildScene(HomeReferenceAssets assets)
        {
            string backupPath = BackupSceneIfPresent();
            Scene scene = File.Exists(Path.GetFullPath(ScenePath))
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ClearScene(scene);

            GameObject sceneRoot = new GameObject("HomeMapSceneRoot");
            SceneManager.MoveGameObjectToScene(sceneRoot, scene);

            MapInteractionController mapController = sceneRoot.AddComponent<MapInteractionController>();
            FullscreenMapSceneController sceneController = sceneRoot.AddComponent<FullscreenMapSceneController>();
            MapDebugOverlayView debugOverlay = sceneRoot.AddComponent<MapDebugOverlayView>();

            GameObject indoorRoot = new GameObject("HomeMapRpgRoot");
            indoorRoot.transform.SetParent(sceneRoot.transform, false);
            MapInteractionFlowController flow = indoorRoot.AddComponent<MapInteractionFlowController>();

            GameObject artRoot = new GameObject("Figure3RoomArt");
            artRoot.transform.SetParent(indoorRoot.transform, false);
            CreateStaticSprite("RoomSpace_Background", assets.roomBackgroundSprite, artRoot.transform, Vector3.zero, Vector3.one, -100);
            GameObject referenceGuide = CreateStaticSprite("RoomReference_Guide_Disabled", assets.referenceSprite, artRoot.transform, Vector3.zero, Vector3.one, -90);
            referenceGuide.SetActive(false);

            Grid grid = CreateGrid(indoorRoot.transform);
            CreateTilemap(grid.transform, "Ground_Tilemap", 0, false, false);
            CreateTilemap(grid.transform, "Wall_Tilemap", 5, false, false);
            CreateTilemap(grid.transform, "FloorTrim_Tilemap", 6, false, false);
            CreateTilemap(grid.transform, "WallTop_Tilemap", 8, false, false);
            CreateTilemap(grid.transform, "Decoration_Tilemap", 10, false, false);
            CreateTilemap(grid.transform, "FurnitureBase_Tilemap", 15, false, false);
            CreateTilemap(grid.transform, "Props_Tilemap", 22, false, false);
            Tilemap collision = CreateTilemap(grid.transform, "Collision_Tilemap", 0, false, true);
            CreateTilemap(grid.transform, "AbovePlayer_Tilemap", 40, false, false);
            Tilemap interaction = CreateTilemap(grid.transform, "Interaction_Tilemap", 200, false, false);
            Tilemap triggerGuide = CreateTilemap(grid.transform, "TriggerGuide_Tilemap", 200, false, false);
            BuildCollisionLayout(collision, assets.collisionTile);
            BuildCollisionBounds(indoorRoot.transform);
            MarkInteractionCells(interaction, triggerGuide, assets);

            GameObject interactableRoot = new GameObject("InteractableObjects");
            interactableRoot.transform.SetParent(indoorRoot.transform, false);
            GameObject decorationRoot = new GameObject("DecorativeObjects");
            decorationRoot.transform.SetParent(indoorRoot.transform, false);
            GameObject spawnRoot = new GameObject("PlayerSpawnPoints");
            spawnRoot.transform.SetParent(indoorRoot.transform, false);
            GameObject cameraBoundsRoot = new GameObject("CameraBounds");
            cameraBoundsRoot.transform.SetParent(indoorRoot.transform, false);
            BuildFloorAndWallDecor(assets, decorationRoot.transform);
            BuildFurniture(assets, decorationRoot.transform);
            BuildInteractables(assets, interactableRoot.transform);

            GameObject spawn = new GameObject("DefaultSpawnPoint");
            spawn.transform.SetParent(spawnRoot.transform, false);
            spawn.transform.localPosition = new Vector3(0f, -5.35f, 0f);
            spawn.AddComponent<PlayerSpawnPoint>().spawnPointId = "home_entry";

            BoxCollider2D boundsCollider = cameraBoundsRoot.AddComponent<BoxCollider2D>();
            boundsCollider.isTrigger = true;
            boundsCollider.size = new Vector2(MapWidth, MapHeight);

            GameObject player = CreatePlayer(assets, flow);
            GameObject cameraObject = CreateCamera(player.transform, cameraBoundsRoot.transform);

            sceneController.mapId = "location_home";
            sceneController.displayName = "家里｜休息、电脑学习、整理计划、家庭聊天。";
            sceneController.defaultSpawnPointId = "home_entry";
            sceneController.player = player.transform;
            sceneController.playerController = player.GetComponent<RpgPlayerController>();
            sceneController.facingInteractor = player.GetComponent<PlayerFacingInteractor>();
            sceneController.cameraController = cameraObject.GetComponent<MapCameraController>();
            sceneController.cameraBounds = boundsCollider;
            sceneController.interactionController = mapController;
            sceneController.autoCollectTargets = true;
            sceneController.createFallbackUi = true;
            sceneController.createFallbackRuntimeServices = true;

            flow.interactionController = mapController;
            flow.debugOverlay = debugOverlay;
            PlayerFacingInteractor interactor = player.GetComponent<PlayerFacingInteractor>();
            interactor.mapInteractionController = mapController;

            MapInteractionStateVisual[] visuals = interactableRoot.GetComponentsInChildren<MapInteractionStateVisual>(true);
            flow.stateVisuals = visuals;
            debugOverlay.debugObjects = visuals
                .Where(visual => visual != null && visual.debugRange != null)
                .Select(visual => visual.debugRange)
                .ToList();
            debugOverlay.SetVisible(false);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            assets.sceneBackupPath = backupPath;
        }

        private static void BuildFloorAndWallDecor(HomeReferenceAssets assets, Transform parent)
        {
            CreateFlatSprite("BedroomRunnerRug", assets.greenRugSprite, parent, WorldFromPixelCenter(315, 448), Vector3.one, 4);
            CreateFlatSprite("LivingRoomRug", assets.largeRugSprite, parent, WorldFromPixelCenter(745, 695), new Vector3(1.02f, 0.9f, 1f), 4);
            CreateFlatSprite("EntryMat", assets.greenRugSprite, parent, WorldFromPixelCenter(748, 1015), new Vector3(0.46f, 0.42f, 1f), 4);
            CreateFlatSprite("WardrobeRug", assets.greenRugSprite, parent, WorldFromPixelCenter(1110, 405), new Vector3(0.72f, 0.72f, 1f), 4);
            CreateFlatSprite("DeskRoundRug", assets.roundRugSprite, parent, WorldFromPixelCenter(700, 340), new Vector3(0.72f, 0.72f, 1f), 4);
            CreateFlatSprite("CoffeeGreenCushion", assets.greenCushionSprite, parent, WorldFromPixelCenter(404, 505), new Vector3(0.72f, 0.72f, 1f), 5);
            CreateFlatSprite("CoffeeYellowCushion", assets.yellowCushionSprite, parent, WorldFromPixelCenter(665, 772), new Vector3(0.62f, 0.62f, 1f), 5);
        }

        private static void BuildFurniture(HomeReferenceAssets assets, Transform parent)
        {
            CreateProp("BedroomNightstand", assets.nightstandSprite, parent, WorldFromPixelBase(320, 365), new Vector3(0.9f, 0.9f, 1f), new Vector2(0.9f, 0.75f), new Vector2(0f, 0.35f), true, true);
            CreateProp("DeskChair", assets.chairSprite, parent, WorldFromPixelBase(704, 382), new Vector3(0.88f, 0.88f, 1f), new Vector2(1.0f, 0.65f), new Vector2(0f, 0.35f), true, false);
            CreateProp("StudyBookshelf", assets.tallBookshelfSprite, parent, WorldFromPixelBase(850, 346), new Vector3(0.9f, 0.9f, 1f), new Vector2(1.4f, 0.8f), new Vector2(0f, 0.4f), true, true);
            CreateProp("StudyTallPlant", assets.tallPlantSprite, parent, WorldFromPixelBase(936, 355), new Vector3(0.78f, 0.78f, 1f), new Vector2(0.55f, 0.55f), new Vector2(0f, 0.25f), true, false);
            CreateProp("ShoeCabinet", assets.shoeCabinetSprite, parent, WorldFromPixelBase(1290, 350), new Vector3(0.82f, 0.82f, 1f), new Vector2(1.0f, 0.65f), new Vector2(0f, 0.3f), true, true);
            CreateProp("ShoeRack", assets.shoeRackSprite, parent, WorldFromPixelBase(1364, 365), new Vector3(0.82f, 0.82f, 1f), new Vector2(0.85f, 0.7f), new Vector2(0f, 0.35f), true, true);

            CreateProp("LeftBookcase", assets.leftBookshelfSprite, parent, WorldFromPixelBase(120, 575), new Vector3(0.86f, 0.86f, 1f), new Vector2(2.2f, 0.7f), new Vector2(0f, 0.35f), true, true);
            CreateProp("LowLivingCabinet", assets.lowCabinetSprite, parent, WorldFromPixelBase(340, 713), new Vector3(0.88f, 0.88f, 1f), new Vector2(2.0f, 0.75f), new Vector2(0f, 0.35f), true, true);
            CreateProp("LivingSofa", assets.sofaSprite, parent, WorldFromPixelBase(720, 540), new Vector3(0.95f, 0.95f, 1f), new Vector2(3.0f, 0.9f), new Vector2(0f, 0.45f), true, true);
            CreateProp("SofaSideTable", assets.sofaTableSprite, parent, WorldFromPixelBase(870, 552), new Vector3(0.78f, 0.78f, 1f), new Vector2(0.7f, 0.55f), new Vector2(0f, 0.25f), true, false);
            CreateProp("CoffeeTableObjectVisual", assets.coffeeTableSprite, parent, WorldFromPixelBase(750, 723), new Vector3(0.92f, 0.92f, 1f), new Vector2(2.3f, 0.75f), new Vector2(0f, 0.35f), true, true);
            CreateProp("LeftPlant", assets.smallPlantSprite, parent, WorldFromPixelBase(495, 554), new Vector3(0.9f, 0.9f, 1f), new Vector2(0.55f, 0.45f), new Vector2(0f, 0.25f), true, false);

            CreateProp("KitchenFridge", assets.fridgeSprite, parent, WorldFromPixelBase(1095, 765), new Vector3(0.85f, 0.85f, 1f), new Vector2(0.9f, 0.65f), new Vector2(0f, 0.32f), true, true);
            CreateProp("KitchenSink", assets.sinkSprite, parent, WorldFromPixelBase(1214, 770), new Vector3(0.86f, 0.86f, 1f), new Vector2(2.1f, 0.75f), new Vector2(0f, 0.35f), true, true);
            CreateProp("KitchenStove", assets.stoveSprite, parent, WorldFromPixelBase(1322, 770), new Vector3(0.85f, 0.85f, 1f), new Vector2(1.0f, 0.7f), new Vector2(0f, 0.35f), true, true);
            CreateProp("KitchenDrawer", assets.kitchenDrawerSprite, parent, WorldFromPixelBase(1330, 920), new Vector3(0.76f, 0.76f, 1f), new Vector2(0.9f, 0.7f), new Vector2(0f, 0.35f), true, true);
            CreateProp("KitchenTableShelf", assets.tableShelfSprite, parent, WorldFromPixelBase(1115, 935), new Vector3(0.84f, 0.84f, 1f), new Vector2(1.9f, 0.7f), new Vector2(0f, 0.35f), true, true);
            CreateProp("EntryCoatRack", assets.coatRackSprite, parent, WorldFromPixelBase(1287, 1002), new Vector3(0.86f, 0.86f, 1f), new Vector2(1.0f, 0.55f), new Vector2(0f, 0.27f), true, false);
            CreateProp("EntryWallShelf", assets.wallShelfSprite, parent, WorldFromPixelBase(594, 920), new Vector3(0.82f, 0.82f, 1f), new Vector2(2.0f, 0.55f), new Vector2(0f, 0.25f), true, false);
            CreateProp("EntrySmallCabinet", assets.smallCabinetSprite, parent, WorldFromPixelBase(980, 785), new Vector3(0.78f, 0.78f, 1f), new Vector2(0.8f, 0.55f), new Vector2(0f, 0.27f), true, true);
            CreateProp("EntryShoeShelf", assets.shoeRackSprite, parent, WorldFromPixelBase(1248, 1010), new Vector3(0.75f, 0.75f, 1f), new Vector2(0.9f, 0.45f), new Vector2(0f, 0.22f), true, false);
        }

        private static void BuildInteractables(HomeReferenceAssets assets, Transform parent)
        {
            CreateInteractionObject(
                "BedObject",
                assets.bedSprite,
                parent,
                WorldFromPixelBase(190, 468),
                new Vector3(0.96f, 0.96f, 1f),
                new Vector2(2.65f, 1.2f),
                new Vector2(0f, 0.55f),
                new Vector2(3.6f, 2.0f),
                "location_home",
                assets.restActivity.activityId,
                "早点休息",
                "按 E：早点休息｜进入下一天｜精力 +20｜压力 -5",
                assets.restActivity,
                false,
                12,
                assets.markerSprite);

            CreateInteractionObject(
                "DeskObject",
                assets.deskSprite,
                parent,
                WorldFromPixelBase(596, 356),
                new Vector3(0.86f, 0.86f, 1f),
                new Vector2(2.5f, 0.95f),
                new Vector2(0f, 0.45f),
                new Vector2(3.5f, 2.0f),
                "location_home",
                assets.planActivity.activityId,
                "整理计划",
                "按 E：整理计划｜消耗 1 时间｜自主 +5｜心情 +2",
                assets.planActivity,
                false,
                11,
                assets.markerSprite);

            CreateInteractionObject(
                "ComputerStudyPoint",
                assets.chairSprite,
                parent,
                WorldFromPixelBase(670, 380),
                new Vector3(0.38f, 0.38f, 1f),
                Vector2.zero,
                Vector2.zero,
                new Vector2(2.3f, 1.7f),
                "location_home",
                assets.computerActivity.activityId,
                "电脑学习",
                "按 E：电脑学习｜消耗 1 时间｜数学 +6｜压力 +3",
                assets.computerActivity,
                false,
                13,
                assets.markerSprite,
                true);

            GameObject wardrobeProp = CreateInteractionObject(
                "WardrobeObject",
                assets.wardrobeSprite,
                parent,
                WorldFromPixelBase(1110, 350),
                new Vector3(0.94f, 0.94f, 1f),
                new Vector2(2.6f, 1.1f),
                new Vector2(0f, 0.52f),
                new Vector2(3.2f, 1.8f),
                "location_home",
                string.Empty,
                "整理衣柜",
                "按 E：整理衣柜｜消耗 1 时间｜心情 +3｜精力上限 +1",
                null,
                false,
                5,
                assets.markerSprite);

            // 2026-10-23：按用户要求彻底移除「整理衣柜」交互，衣柜只保留为装饰道具。
            if (wardrobeProp != null)
            {
                UnityEngine.Object.DestroyImmediate(wardrobeProp.GetComponent<MapInteractionTarget>());
                UnityEngine.Object.DestroyImmediate(wardrobeProp.GetComponent<MapInteractionPoint>());
                UnityEngine.Object.DestroyImmediate(wardrobeProp.GetComponent<MapInteractionStateVisual>());
            }

            GameObject exitDoor = CreateInteractionObject(
                "ExitDoorObject",
                assets.exitDoorSprite,
                parent,
                WorldFromPixelBase(748, 970),
                new Vector3(0.92f, 0.92f, 1f),
                Vector2.zero,
                Vector2.zero,
                new Vector2(2.4f, 2.0f),
                "location_home",
                string.Empty,
                "返回阶段地图",
                "按 E：返回阶段地图｜不消耗时间",
                null,
                true,
                20,
                assets.markerSprite);
            ConfigureExitDoorAnimation(exitDoor, assets);

            CreateFamilyNpc(assets, parent);
        }

        private static void ConfigureExitDoorAnimation(GameObject exitDoor, HomeReferenceAssets assets)
        {
            if (exitDoor == null)
            {
                return;
            }

            MapDoorAnimator doorAnimator = exitDoor.AddComponent<MapDoorAnimator>();
            Transform visual = exitDoor.transform.Find("Visual");
            doorAnimator.targetRenderer = visual == null ? exitDoor.GetComponentInChildren<SpriteRenderer>() : visual.GetComponent<SpriteRenderer>();
            doorAnimator.openFrames = assets.exitDoorOpenFrames;
            doorAnimator.closeFrames = assets.exitDoorCloseFrames;
            doorAnimator.framesPerSecond = 12f;
            doorAnimator.resetClosedOnEnable = true;
            doorAnimator.openingPromptText = "开门中……";
        }

        private static void BuildCollisionLayout(Tilemap collision, Tile collisionTile)
        {
            PaintRect(collision, collisionTile, -12, 11, 8, 8);
            PaintRect(collision, collisionTile, -12, 11, -10, -10);
            PaintRect(collision, collisionTile, -12, -12, -9, 8);
            PaintRect(collision, collisionTile, 11, 11, -9, 8);
            PaintRect(collision, collisionTile, -12, -8, -9, -4);
        }

        private static void MarkInteractionCells(Tilemap interaction, Tilemap triggerGuide, HomeReferenceAssets assets)
        {
            foreach (Vector3Int cell in new[]
            {
                new Vector3Int(-11, 2, 0),
                new Vector3Int(2, 5, 0),
                new Vector3Int(8, -9, 0),
                new Vector3Int(12, -9, 0),
                new Vector3Int(0, -8, 0),
                new Vector3Int(-1, 3, 0)
            })
            {
                interaction.SetTile(cell, assets.markerTile);
                triggerGuide.SetTile(cell, assets.markerTile);
            }
        }

        private static void PaintRect(Tilemap tilemap, TileBase tile, int minX, int maxX, int minY, int maxY)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }
        }

        private static void BuildCollisionBounds(Transform parent)
        {
            GameObject boundsRoot = new GameObject("CollisionBounds");
            boundsRoot.transform.SetParent(parent, false);

            AddCollisionRect(boundsRoot.transform, "TopWall", -12, 11, 8, 8);
            AddCollisionRect(boundsRoot.transform, "BottomWall", -12, 11, -10, -10);
            AddCollisionRect(boundsRoot.transform, "LeftWall", -12, -12, -9, 8);
            AddCollisionRect(boundsRoot.transform, "RightWall", 11, 11, -9, 8);
            AddCollisionRect(boundsRoot.transform, "BathroomBlock", -12, -8, -9, -4);
        }

        private static void AddCollisionRect(Transform parent, string name, int minX, int maxX, int minY, int maxY)
        {
            GameObject rect = new GameObject("Collision_" + name);
            rect.transform.SetParent(parent, false);
            rect.transform.localPosition = new Vector3(
                (minX + maxX + 1) * 0.5f,
                (minY + maxY + 1) * 0.5f,
                0f);

            BoxCollider2D collider = rect.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(maxX - minX + 1, maxY - minY + 1);
            collider.isTrigger = false;
        }

        private static GameObject CreateInteractionObject(
            string objectName,
            Sprite sprite,
            Transform parent,
            Vector3 basePosition,
            Vector3 visualScale,
            Vector2 blockingSize,
            Vector2 colliderOffset,
            Vector2 triggerSize,
            string locationId,
            string actionId,
            string displayName,
            string promptText,
            ActivityData fallbackActivity,
            bool isExit,
            int priority,
            Sprite markerSprite,
            bool visualOnly = false)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePosition;

            MapInteractionPoint point = root.AddComponent<MapInteractionPoint>();
            point.locationId = locationId;
            point.actionId = actionId;
            point.displayName = displayName;
            point.description = promptText;
            point.interactionType = isExit ? MapInteractionType.ReturnToMainUi : MapInteractionType.LocationAction;
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = root.AddComponent<MapInteractionTarget>();
            target.interactionId = string.IsNullOrEmpty(actionId) ? (isExit ? "exit_home" : objectName) : actionId;
            target.displayName = displayName;
            target.promptText = promptText;
            target.fallbackActivity = fallbackActivity;
            target.interactionPoint = point;
            target.isExitPortal = isExit;
            target.consumeTimeOnInteraction = !isExit && fallbackActivity != null;
            target.requireFacing = false;
            target.interactionRange = Mathf.Max(triggerSize.x, triggerSize.y) * 0.55f;
            target.priority = priority;

            if (isExit)
            {
                root.AddComponent<MapExitPortal>().destinationId = "stage_hub";
            }

            SpriteRenderer renderer = AddBaseVisual(root.transform, sprite, visualScale, 1000);
            if (!visualOnly && blockingSize.sqrMagnitude > 0f)
            {
                AddBoxChild(root.transform, "BlockingCollider", colliderOffset, blockingSize, false);
            }

            AddBoxChild(root.transform, "InteractionTrigger", colliderOffset, triggerSize, true);

            MapObjectYSorter sorter = root.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };

            MapInteractionStateVisual stateVisual = root.AddComponent<MapInteractionStateVisual>();
            stateVisual.target = target;
            stateVisual.tintRenderers = new[] { renderer };
            stateVisual.nearbyIndicator = CreateMarker("NearbyMarker", root.transform, markerSprite, new Vector3(0f, triggerSize.y * 0.55f, 0f), new Color(1f, 0.92f, 0.35f, 0.95f), 2500, 0.42f);
            stateVisual.debugRange = CreateMarker("DebugRange", root.transform, markerSprite, Vector3.zero, new Color(0.9f, 0.25f, 0.18f, 0.25f), 2490, Mathf.Max(triggerSize.x, triggerSize.y));
            stateVisual.nearbyIndicator.SetActive(false);
            stateVisual.debugRange.SetActive(false);
            return root;
        }

        private static GameObject CreateFamilyNpc(HomeReferenceAssets assets, Transform parent)
        {
            GameObject root = new GameObject("FamilyNpcObject");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-2.34f, -1.38f, -0.1f);
            SpriteRenderer renderer = AddBaseVisual(root.transform, assets.familyNpcSprite, Vector3.one, 1000);
            renderer.transform.localPosition = new Vector3(-0.05f, -0.09f, 0f);

            MapInteractionPoint point = root.AddComponent<MapInteractionPoint>();
            point.locationId = "location_home";
            point.actionId = assets.familyDinnerActivity.activityId;
            point.displayName = "家庭晚饭聊天";
            point.description = "按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5";
            point.interactionType = MapInteractionType.LocationAction;
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = root.AddComponent<MapInteractionTarget>();
            target.interactionId = assets.familyDinnerActivity.activityId;
            target.displayName = "家庭晚饭聊天";
            target.promptText = point.description;
            target.fallbackActivity = assets.familyDinnerActivity;
            target.interactionPoint = point;
            target.consumeTimeOnInteraction = true;
            target.requireFacing = false;
            target.interactionRange = 1.4f;
            target.priority = 9;

            AddBoxChild(root.transform, "BlockingCollider", new Vector2(0f, -0.25f), new Vector2(0.65f, 1f), false);
            AddBoxChild(root.transform, "InteractionTrigger", new Vector2(0f, -0.2f), new Vector2(1.5f, 1.8f), true);

            MapObjectYSorter sorter = root.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };

            MapInteractionStateVisual stateVisual = root.AddComponent<MapInteractionStateVisual>();
            stateVisual.target = target;
            stateVisual.tintRenderers = new[] { renderer };
            stateVisual.nearbyIndicator = CreateMarker("NearbyMarker", root.transform, assets.markerSprite, new Vector3(0f, 1f, 0f), new Color(1f, 0.92f, 0.35f, 0.95f), 2500, 0.42f);
            stateVisual.debugRange = CreateMarker("DebugRange", root.transform, assets.markerSprite, Vector3.zero, new Color(0.9f, 0.25f, 0.18f, 0.25f), 2490, 1.8f);
            stateVisual.nearbyIndicator.SetActive(false);
            stateVisual.debugRange.SetActive(false);
            return root;
        }

        private static GameObject CreateProp(
            string name,
            Sprite sprite,
            Transform parent,
            Vector3 basePosition,
            Vector3 visualScale,
            Vector2 blockingSize,
            Vector2 colliderOffset,
            bool useYSort,
            bool fadeWhenOccluding)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = basePosition;
            SpriteRenderer renderer = AddBaseVisual(root.transform, sprite, visualScale, 1000);
            if (blockingSize.sqrMagnitude > 0f)
            {
                AddBoxChild(root.transform, "BlockingCollider", colliderOffset, blockingSize, false);
            }

            if (useYSort)
            {
                MapObjectYSorter sorter = root.AddComponent<MapObjectYSorter>();
                sorter.renderers = new[] { renderer };
            }

            if (fadeWhenOccluding)
            {
                MapSpriteOcclusionFader fader = root.AddComponent<MapSpriteOcclusionFader>();
                fader.targetRenderers = new[] { renderer };
                fader.fadedAlpha = 0.48f;
                fader.fadeSpeed = 8f;
            }

            return root;
        }

        private static GameObject CreateFlatSprite(string name, Sprite sprite, Transform parent, Vector3 centerPosition, Vector3 scale, int sortingOrder)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = centerPosition;
            root.transform.localScale = scale;
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return root;
        }

        private static GameObject CreateStaticSprite(string name, Sprite sprite, Transform parent, Vector3 position, Vector3 scale, int sortingOrder)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localScale = scale;
            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return root;
        }

        private static SpriteRenderer AddBaseVisual(Transform parent, Sprite sprite, Vector3 scale, int sortingOrder)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            Vector2 spriteSize = sprite != null ? sprite.bounds.size : Vector2.one;
            visual.transform.localPosition = new Vector3(0f, spriteSize.y * scale.y * 0.5f, 0f);
            visual.transform.localScale = scale;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static BoxCollider2D AddBoxChild(Transform parent, string name, Vector2 localPosition, Vector2 size, bool trigger)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.transform.localPosition = localPosition;
            BoxCollider2D collider = child.AddComponent<BoxCollider2D>();
            collider.size = size;
            collider.isTrigger = trigger;
            return collider;
        }

        private static GameObject CreateMarker(string name, Transform parent, Sprite sprite, Vector3 localPosition, Color color, int sortingOrder, float scale)
        {
            GameObject marker = new GameObject(name);
            marker.transform.SetParent(parent, false);
            marker.transform.localPosition = localPosition;
            marker.transform.localScale = new Vector3(scale, scale, 1f);
            SpriteRenderer renderer = marker.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return marker;
        }

        private static Grid CreateGrid(Transform parent)
        {
            GameObject gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(parent, false);
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            return grid;
        }

        private static Tilemap CreateTilemap(Transform parent, string name, int sortingOrder, bool render, bool collisionLayer)
        {
            GameObject tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.enabled = render;
            renderer.sortingOrder = sortingOrder;

            if (collisionLayer)
            {
                Rigidbody2D body = tilemapObject.AddComponent<Rigidbody2D>();
                body.bodyType = RigidbodyType2D.Static;
                TilemapCollider2D collider = tilemapObject.AddComponent<TilemapCollider2D>();
                collider.compositeOperation = Collider2D.CompositeOperation.Merge;
                CompositeCollider2D composite = tilemapObject.AddComponent<CompositeCollider2D>();
                composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
            }

            return tilemap;
        }

        private static GameObject CreatePlayer(HomeReferenceAssets assets, MapInteractionFlowController flow)
        {
            GameObject player = new GameObject("PlayerRpg");
            player.transform.position = new Vector3(0f, -5.35f, -0.1f);
            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = assets.playerSprite;
            renderer.sortingOrder = 1000;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            CapsuleCollider2D capsule = player.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.68f, 1.05f);
            capsule.offset = new Vector2(0f, -0.32f);
            Animator animator = player.AddComponent<Animator>();
            animator.runtimeAnimatorController = assets.playerAnimator;
            PlayerFacingInteractor interactor = player.AddComponent<PlayerFacingInteractor>();
            RpgPlayerController controller = player.AddComponent<RpgPlayerController>();
            controller.body = body;
            controller.bodyCollider = capsule;
            controller.spriteRenderer = renderer;
            controller.animator = animator;
            controller.facingInteractor = interactor;
            controller.interactionFlowController = flow;
            controller.moveSpeed = 3f;
            controller.runSpeedMultiplier = 1.55f;
            controller.useMovementBounds = true;
            controller.movementMin = new Vector2(-10.6f, -8.35f);
            controller.movementMax = new Vector2(10.3f, 4.85f);
            MapObjectYSorter sorter = player.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };
            sorter.sortYOffset = -0.85f;
            return player;
        }

        private static GameObject CreateCamera(Transform player, Transform bounds)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8.6f;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            MapCameraController controller = cameraObject.AddComponent<MapCameraController>();
            controller.targetCamera = camera;
            controller.followTarget = player;
            controller.cameraBounds = bounds.GetComponent<Collider2D>();
            controller.useExplicitBounds = true;
            controller.minPosition = new Vector2(MapMinX, MapMinY);
            controller.maxPosition = new Vector2(MapMaxX, MapMaxY);
            return cameraObject;
        }

        private static ActivityData CreateActivity(string fileName, string id, string displayName, string description, ActivityType type, params RewardData[] rewards)
        {
            string path = DataRoot + "/" + fileName;
            ActivityData activity = AssetDatabase.LoadAssetAtPath<ActivityData>(path);
            if (activity == null)
            {
                activity = ScriptableObject.CreateInstance<HomeMapActivityData>();
                AssetDatabase.CreateAsset(activity, path);
            }

            activity.activityId = id;
            activity.displayName = displayName;
            activity.description = description;
            activity.activityType = type;
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

        private static RewardData ReducePressure(int value, string description)
        {
            return new RewardData
            {
                rewardType = RewardType.ReducePressure,
                value = value,
                description = description
            };
        }

        private static RewardData AddPressure(int value, string description)
        {
            return new RewardData
            {
                rewardType = RewardType.AddPlayerStat,
                targetStat = PlayerStatKey.Pressure,
                value = value,
                description = description
            };
        }

        private static Sprite CropProp(Texture2D source, Color key, string fileName, int topX, int topY, int width, int height)
        {
            Texture2D raw = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sx = topX + x;
                    int sy = source.height - (topY + height) + y;
                    raw.SetPixel(x, y, source.GetPixel(sx, sy));
                }
            }

            raw.Apply();
            RemoveConnectedBackground(raw, key);
            RectInt trim = FindOpaqueBounds(raw, 0.04f);
            Texture2D output = new Texture2D(trim.width, trim.height, TextureFormat.RGBA32, false);
            for (int y = 0; y < trim.height; y++)
            {
                for (int x = 0; x < trim.width; x++)
                {
                    output.SetPixel(x, y, raw.GetPixel(trim.x + x, trim.y + y));
                }
            }

            output.Apply();
            RemoveConnectedBackground(output, key);
            UnityEngine.Object.DestroyImmediate(raw);
            string path = GeneratedPropRoot + "/" + fileName;
            SavePng(output, path);
            UnityEngine.Object.DestroyImmediate(output);
            return ImportSprite(path, Ppu);
        }

        private static void RemoveConnectedBackground(Texture2D texture, Color key)
        {
            int width = texture.width;
            int height = texture.height;
            bool[] removable = new bool[width * height];
            bool[] remove = new bool[width * height];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    removable[index] = IsBackgroundLike(texture.GetPixel(x, y), key);
                    bool isBorder = x == 0 || y == 0 || x == width - 1 || y == height - 1;
                    if (isBorder && removable[index])
                    {
                        remove[index] = true;
                        queue.Enqueue(new Vector2Int(x, y));
                    }
                }
            }

            int[] offsetsX = { 1, -1, 0, 0, 1, 1, -1, -1 };
            int[] offsetsY = { 0, 0, 1, -1, 1, -1, 1, -1 };
            while (queue.Count > 0)
            {
                Vector2Int point = queue.Dequeue();
                for (int i = 0; i < offsetsX.Length; i++)
                {
                    int nextX = point.x + offsetsX[i];
                    int nextY = point.y + offsetsY[i];
                    if (nextX < 0 || nextY < 0 || nextX >= width || nextY >= height)
                    {
                        continue;
                    }

                    int nextIndex = nextY * width + nextX;
                    if (remove[nextIndex] || !removable[nextIndex])
                    {
                        continue;
                    }

                    remove[nextIndex] = true;
                    queue.Enqueue(new Vector2Int(nextX, nextY));
                }
            }

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    if (!remove[index])
                    {
                        continue;
                    }

                    MakeTransparent(texture, x, y);
                }
            }

            texture.Apply();
            RemoveAdjacentBackgroundFringe(texture, key, offsetsX, offsetsY, 6);
        }

        private static void RemoveAdjacentBackgroundFringe(Texture2D texture, Color key, int[] offsetsX, int[] offsetsY, int maxPasses)
        {
            int width = texture.width;
            int height = texture.height;
            bool[] fringeRemove = new bool[width * height];

            for (int pass = 0; pass < maxPasses; pass++)
            {
                Array.Clear(fringeRemove, 0, fringeRemove.Length);
                bool changed = false;

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        int index = y * width + x;
                        Color pixel = texture.GetPixel(x, y);
                        if (!IsBackgroundFringe(pixel, key) || !TouchesTransparent(texture, x, y, offsetsX, offsetsY))
                        {
                            continue;
                        }

                        fringeRemove[index] = true;
                        changed = true;
                    }
                }

                if (!changed)
                {
                    break;
                }

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (fringeRemove[y * width + x])
                        {
                            MakeTransparent(texture, x, y);
                        }
                    }
                }

                texture.Apply();
            }
        }

        private static bool TouchesTransparent(Texture2D texture, int x, int y, int[] offsetsX, int[] offsetsY)
        {
            for (int i = 0; i < offsetsX.Length; i++)
            {
                int nextX = x + offsetsX[i];
                int nextY = y + offsetsY[i];
                if (nextX < 0 || nextY < 0 || nextX >= texture.width || nextY >= texture.height)
                {
                    return true;
                }

                if (texture.GetPixel(nextX, nextY).a <= 0.02f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void MakeTransparent(Texture2D texture, int x, int y)
        {
            texture.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
        }

        private static bool IsBackgroundLike(Color pixel, Color key)
        {
            if (pixel.a <= 0.02f)
            {
                return true;
            }

            if (ColorDistance(pixel, key) <= 0.38f)
            {
                return true;
            }

            if (IsBlueScreenFringe(pixel))
            {
                return true;
            }

            return pixel.r <= 0.37f
                && pixel.g >= 0.53f
                && pixel.b >= 0.55f
                && Mathf.Abs(pixel.g - pixel.b) <= 0.47f;
        }

        private static bool IsBackgroundFringe(Color pixel, Color key)
        {
            if (ColorDistance(pixel, key) <= 0.45f)
            {
                return true;
            }

            if (IsBlueScreenFringe(pixel))
            {
                return true;
            }

            return pixel.r <= 0.45f
                && pixel.g >= 0.48f
                && pixel.b >= 0.5f
                && Mathf.Abs(pixel.g - pixel.b) <= 0.52f;
        }

        private static bool IsBlueScreenFringe(Color pixel)
        {
            if (pixel.a <= 0.02f)
            {
                return true;
            }

            return pixel.r <= 0.18f
                && pixel.g >= 0.18f
                && pixel.b >= 0.18f
                && pixel.b >= pixel.r + 0.12f
                && pixel.g >= pixel.r + 0.12f
                && Mathf.Abs(pixel.g - pixel.b) <= 0.28f;
        }

        private static Sprite CropRoomProp(Texture2D source, string fileName, int topX, int topY, int width, int height)
        {
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sx = topX + x;
                    int sy = source.height - (topY + height) + y;
                    output.SetPixel(x, y, source.GetPixel(sx, sy));
                }
            }

            output.Apply();
            string path = GeneratedPropRoot + "/" + fileName;
            SavePng(output, path);
            UnityEngine.Object.DestroyImmediate(output);
            return ImportSprite(path, Ppu);
        }

        private static Sprite CreateCharacterFrameSprite(string sourcePath, string fileName, int topX, int topY, int width, int height, float ppu)
        {
            Texture2D source = LoadTexture(sourcePath);
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int sx = topX + x;
                    int sy = source.height - (topY + height) + y;
                    output.SetPixel(x, y, source.GetPixel(sx, sy));
                }
            }

            output.Apply();
            string path = GeneratedPropRoot + "/" + fileName;
            SavePng(output, path);
            UnityEngine.Object.DestroyImmediate(output);
            UnityEngine.Object.DestroyImmediate(source);
            return ImportSprite(path, ppu);
        }

        private static Sprite[] CreateDoorOpenAnimationFrames(string closedDoorPath, Sprite closedSprite)
        {
            Texture2D closed = LoadTexture(closedDoorPath);
            List<Sprite> frames = new List<Sprite>
            {
                closedSprite,
                CreateDoorOpenFrame(closed, "home_ref_exit_door_open_01.png", 0.28f),
                CreateDoorOpenFrame(closed, "home_ref_exit_door_open_02.png", 0.62f),
                CreateDoorOpenFrame(closed, "home_ref_exit_door_open_03.png", 1f)
            };

            UnityEngine.Object.DestroyImmediate(closed);
            return frames.ToArray();
        }

        private static Sprite CreateDoorOpenFrame(Texture2D closed, string fileName, float openAmount)
        {
            Texture2D frame = CloneTexture(closed);

            int doorX = Mathf.RoundToInt(closed.width * 0.25f);
            int doorY = Mathf.RoundToInt(closed.height * 0.03f);
            int doorW = Mathf.RoundToInt(closed.width * 0.71f);
            int doorH = Mathf.RoundToInt(closed.height * 0.78f);
            FillDoorInterior(frame, doorX, doorY, doorW, doorH);

            int panelW = Mathf.Max(12, Mathf.RoundToInt(Mathf.Lerp(doorW, doorW * 0.29f, openAmount)));
            int panelX = doorX + doorW - panelW;
            CopyScaledRegion(closed, frame, doorX, doorY, doorW, doorH, panelX, doorY, panelW, doorH);
            DrawDoorLeadingEdge(frame, panelX, doorY, doorH, openAmount);

            frame.Apply();
            string path = GeneratedPropRoot + "/" + fileName;
            SavePng(frame, path);
            UnityEngine.Object.DestroyImmediate(frame);
            return ImportSprite(path, Ppu);
        }

        private static Texture2D CloneTexture(Texture2D source)
        {
            Texture2D clone = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            clone.filterMode = FilterMode.Point;
            clone.wrapMode = TextureWrapMode.Clamp;
            clone.SetPixels(source.GetPixels());
            clone.Apply();
            return clone;
        }

        private static void FillDoorInterior(Texture2D texture, int x, int y, int width, int height)
        {
            Color darkBottom = new Color(0.055f, 0.032f, 0.018f, 1f);
            Color darkTop = new Color(0.13f, 0.07f, 0.038f, 1f);
            for (int py = 0; py < height; py++)
            {
                float vertical = height <= 1 ? 0f : py / (float)(height - 1);
                Color color = Color.Lerp(darkBottom, darkTop, vertical);
                for (int px = 0; px < width; px++)
                {
                    texture.SetPixel(x + px, y + py, color);
                }
            }
        }

        private static void CopyScaledRegion(
            Texture2D source,
            Texture2D destination,
            int sourceX,
            int sourceY,
            int sourceWidth,
            int sourceHeight,
            int destinationX,
            int destinationY,
            int destinationWidth,
            int destinationHeight)
        {
            for (int y = 0; y < destinationHeight; y++)
            {
                float v = destinationHeight <= 1 ? 0f : y / (float)(destinationHeight - 1);
                int sy = sourceY + Mathf.Clamp(Mathf.RoundToInt(v * (sourceHeight - 1)), 0, sourceHeight - 1);
                for (int x = 0; x < destinationWidth; x++)
                {
                    float u = destinationWidth <= 1 ? 0f : x / (float)(destinationWidth - 1);
                    int sx = sourceX + Mathf.Clamp(Mathf.RoundToInt(u * (sourceWidth - 1)), 0, sourceWidth - 1);
                    destination.SetPixel(destinationX + x, destinationY + y, source.GetPixel(sx, sy));
                }
            }
        }

        private static void DrawDoorLeadingEdge(Texture2D texture, int x, int y, int height, float openAmount)
        {
            Color highlight = Color.Lerp(new Color(0.43f, 0.2f, 0.08f, 1f), new Color(0.63f, 0.34f, 0.14f, 1f), openAmount);
            Color shadow = new Color(0.035f, 0.02f, 0.012f, 1f);
            for (int py = 0; py < height; py++)
            {
                if (x > 0)
                {
                    texture.SetPixel(x - 1, y + py, shadow);
                }

                if (x < texture.width)
                {
                    texture.SetPixel(x, y + py, highlight);
                }
            }
        }

        private static Sprite[] ReverseFrames(Sprite[] frames)
        {
            if (frames == null || frames.Length == 0)
            {
                return new Sprite[0];
            }

            Sprite[] reversed = new Sprite[frames.Length];
            for (int i = 0; i < frames.Length; i++)
            {
                reversed[i] = frames[frames.Length - 1 - i];
            }

            return reversed;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath)));
            return texture;
        }

        private static RectInt FindOpaqueBounds(Texture2D texture, float threshold)
        {
            int minX = texture.width;
            int minY = texture.height;
            int maxX = -1;
            int maxY = -1;
            for (int y = 0; y < texture.height; y++)
            {
                for (int x = 0; x < texture.width; x++)
                {
                    if (texture.GetPixel(x, y).a <= threshold)
                    {
                        continue;
                    }

                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return new RectInt(0, 0, 1, 1);
            }

            return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        private static float ColorDistance(Color a, Color b)
        {
            float dr = a.r - b.r;
            float dg = a.g - b.g;
            float db = a.b - b.b;
            return Mathf.Sqrt(dr * dr + dg * dg + db * db);
        }

        private static Sprite CreateSolidSprite(string destinationPath, Color color)
        {
            Texture2D output = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = Enumerable.Repeat(color, 16 * 16).ToArray();
            output.SetPixels(pixels);
            output.Apply();
            SavePng(output, destinationPath);
            UnityEngine.Object.DestroyImmediate(output);
            return ImportSprite(destinationPath, 16f);
        }

        private static Sprite CreateMarkerSprite(string destinationPath)
        {
            Texture2D output = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    float dx = x - 7.5f;
                    float dy = y - 7.5f;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    Color color = distance <= 6.2f
                        ? new Color(1f, 0.88f, 0.25f, 0.82f)
                        : new Color(1f, 1f, 1f, 0f);
                    output.SetPixel(x, y, color);
                }
            }

            output.Apply();
            SavePng(output, destinationPath);
            UnityEngine.Object.DestroyImmediate(output);
            return ImportSprite(destinationPath, 16f);
        }

        private static Tile CreateTile(string path, Sprite sprite, Color color, Tile.ColliderType colliderType)
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.sprite = sprite;
            tile.color = color;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static void SavePng(Texture2D texture, string assetPath)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(assetPath));
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static Sprite ImportSprite(string assetPath, float ppu)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Texture importer missing for " + assetPath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
        }

        private static Vector3 WorldFromPixelCenter(int pixelX, int pixelY)
        {
            return new Vector3((pixelX - SourceWidth * 0.5f) / Ppu, (SourceHeight * 0.5f - pixelY) / Ppu, -0.1f);
        }

        private static Vector3 WorldFromPixelBase(int pixelX, int bottomPixelY)
        {
            return new Vector3((pixelX - SourceWidth * 0.5f) / Ppu, (SourceHeight * 0.5f - bottomPixelY) / Ppu, -0.1f);
        }

        private static string BackupSceneIfPresent()
        {
            string fullScenePath = Path.GetFullPath(ScenePath);
            if (!File.Exists(fullScenePath))
            {
                return string.Empty;
            }

            EnsureFolder(BackupRoot);
            string backupPath = BackupRoot + "/HomeMapScene_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
            File.Copy(fullScenePath, Path.GetFullPath(backupPath), true);
            AssetDatabase.ImportAsset(backupPath);
            return backupPath;
        }

        private static void ClearScene(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void WriteReport(HomeReferenceAssets assets)
        {
            string report =
                "# HomeMapScene Figure 3 Rebuild Report\n\n"
                + "- Scene: `" + ScenePath + "`\n"
                + "- Backup scene: `" + (string.IsNullOrEmpty(assets.sceneBackupPath) ? "none" : assets.sceneBackupPath) + "`\n"
                + "- Space source: `" + SourceEmptyRoom + "`\n"
                + "- Item source: `" + SourceItems + "`\n"
                + "- Visual reference: `" + SourceReference + "`\n"
                + "- Strategy: the room shell uses the empty-room image, furniture uses independent cropped sprites from the item sheet, and collision/interactions remain editable Unity objects.\n"
                + "- Required RPG objects kept: `PlayerRpg`, `BedObject`, `DeskObject`, `ComputerStudyPoint`, `FamilyNpcObject`, `ExitDoorObject`, `Collision_Tilemap`, `Interaction_Tilemap`.\n"
                + "- Activities: rest, desk plan, computer study, and family dinner all execute through `MapInteractionTarget` fallback activities, so time/reward flow stays on the existing controller path.\n"
                + "- Camera bounds remain `30 x 20` for compatibility with the existing validator, while the visual room art is 1448x1086 at PPU 64.\n\n"
                + "## Generated Assets\n\n"
                + "- Props: `" + GeneratedPropRoot + "`\n"
                + "- Exit door animation frames: `home_ref_exit_door.png`, `home_ref_exit_door_open_01.png`, `home_ref_exit_door_open_02.png`, `home_ref_exit_door_open_03.png`\n"
                + "- Marker/collision tiles: `" + GeneratedTileRoot + "`\n"
                + "- Computer activity: `" + DataRoot + "/New20_HomeComputerStudy.asset`\n\n"
                + "## Manual Check Points\n\n"
                + "- Play `HomeMapScene`, walk with WASD/arrow keys, hold Shift to run.\n"
                + "- Check that bed, desk/computer, family NPC, and exit door show prompts when approached.\n"
                + "- Press E on the exit door and confirm the door opens before returning to the stage map.\n"
                + "- Press E on each activity and confirm time/reward HUD updates through the existing flow.\n"
                + "- Confirm the player cannot pass through walls, stairs, the bed, sofa, wardrobe, bookcases, kitchen counters, or major furniture.\n";
            File.WriteAllText(Path.GetFullPath(ReportPath), report);
            AssetDatabase.ImportAsset(ReportPath);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class HomeReferenceAssets
        {
            public Sprite roomBackgroundSprite;
            public Sprite referenceSprite;
            public Sprite markerSprite;
            public Sprite collisionSprite;
            public Tile markerTile;
            public Tile collisionTile;

            public Sprite bedSprite;
            public Sprite nightstandSprite;
            public Sprite deskSprite;
            public Sprite chairSprite;
            public Sprite tallBookshelfSprite;
            public Sprite wardrobeSprite;
            public Sprite tallPlantSprite;
            public Sprite shoeCabinetSprite;
            public Sprite shoeRackSprite;
            public Sprite leftBookshelfSprite;
            public Sprite lowCabinetSprite;
            public Sprite sofaSprite;
            public Sprite sofaTableSprite;
            public Sprite coffeeTableSprite;
            public Sprite tableShelfSprite;
            public Sprite fridgeSprite;
            public Sprite sinkSprite;
            public Sprite stoveSprite;
            public Sprite kitchenIslandSprite;
            public Sprite kitchenDrawerSprite;
            public Sprite smallCabinetSprite;
            public Sprite wallShelfSprite;
            public Sprite coatRackSprite;
            public Sprite largeRugSprite;
            public Sprite greenRugSprite;
            public Sprite roundRugSprite;
            public Sprite greenCushionSprite;
            public Sprite yellowCushionSprite;
            public Sprite smallPlantSprite;
            public Sprite floorLampSprite;
            public Sprite exitDoorSprite;
            public Sprite[] exitDoorOpenFrames = new Sprite[0];
            public Sprite[] exitDoorCloseFrames = new Sprite[0];
            public Sprite playerSprite;
            public Sprite familyNpcSprite;
            public RuntimeAnimatorController playerAnimator;

            public ActivityData restActivity;
            public ActivityData planActivity;
            public ActivityData computerActivity;
            public ActivityData familyDinnerActivity;
            public string sceneBackupPath;
        }
    }
}
