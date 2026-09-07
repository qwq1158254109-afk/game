using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

namespace StudentGrowth.Editor
{
    public static class HomeMapSceneBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity";
        private const string GeneratedSpriteRoot = "Assets/Art/Props/HomeMap";
        private const string GeneratedCharacterRoot = "Assets/Art/Characters/HomeMap";
        private const string GeneratedTileRoot = "Assets/Art/Tiles/HomeMap";
        private const string GeneratedDataRoot = "Assets/Data/StudentGrowth/HomeMap";
        private const string GeneratedAnimationRoot = "Assets/Animations/HomeMap";
        private const string DocsRoot = "Assets/Docs";
        private const string HomeMapActivityScript = "Assets/Scripts/StudentGrowth/Map/HomeMapActivityData.cs";

        private const string BedroomSheet = "Assets/Art/Tiles/ModernInteriors/48x48/ThemeSorter/4_bedroom_48x48.png";
        private const string KitchenSheet = "Assets/Art/Tiles/ModernInteriors/48x48/ThemeSorter/12_kitchen_48x48.png";
        private const string FloorSheet = "Assets/Art/Tiles/ModernInteriors/48x48/floors_only_48x48.png";
        private const string DoorSheet = "Assets/Art/Props/ModernInteriors/AnimatedObjects/48x48/Spritesheets/animated_door_1_48x48.png";
        private const string MollySheet = "Assets/Art/Characters/ModernInteriors/48x48/molly_48x48.png";
        private const string LucySheet = "Assets/Art/Characters/ModernInteriors/48x48/lucy_48x48.png";

        [MenuItem("Tools/Student Growth/Maps/Rebuild Home RPG Map")]
        public static void BuildHomeMapScene()
        {
            EnsureFolders();
            GeneratedAssets assets = GenerateSpritesTilesActivitiesAndAnimations();
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            BuildScene(scene, assets);
            WriteReports(assets);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Tools/Student Growth/Maps/Verify Home RPG Map")]
        public static void VerifyHomeMapScene()
        {
            Require(AssetDatabase.LoadAssetAtPath<ActivityData>(GeneratedDataRoot + "/HomeRestEarlySleep.asset") != null, "Home rest activity asset is missing or invalid.");
            Require(AssetDatabase.LoadAssetAtPath<ActivityData>(GeneratedDataRoot + "/HomeDeskPlan.asset") != null, "Home desk activity asset is missing or invalid.");
            Require(AssetDatabase.LoadAssetAtPath<ActivityData>(GeneratedDataRoot + "/HomeFamilyDinner.asset") != null, "Home family dinner activity asset is missing or invalid.");
            Require(AssetDatabase.LoadAssetAtPath<AnimatorController>(GeneratedAnimationRoot + "/HomePlayer.controller") != null, "Home player animator is missing.");

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(GameObject.Find("HomeMapRpgRoot") != null, "HomeMapRpgRoot is missing.");
            Require(GameObject.Find("Ground_Tilemap") != null, "Ground_Tilemap is missing.");
            Require(GameObject.Find("Wall_Tilemap") != null, "Wall_Tilemap is missing.");
            Require(GameObject.Find("Decoration_Tilemap") != null, "Decoration_Tilemap is missing.");
            Require(GameObject.Find("AbovePlayer_Tilemap") != null, "AbovePlayer_Tilemap is missing.");

            GameObject collision = GameObject.Find("Collision_Tilemap");
            Require(collision != null, "Collision_Tilemap is missing.");
            Rigidbody2D collisionBody = collision.GetComponent<Rigidbody2D>();
            Require(collisionBody != null && collisionBody.bodyType == RigidbodyType2D.Static, "Collision_Tilemap needs a static Rigidbody2D.");
            TilemapCollider2D tilemapCollider = collision.GetComponent<TilemapCollider2D>();
            Require(tilemapCollider != null && tilemapCollider.usedByComposite, "Collision_Tilemap needs a TilemapCollider2D using a CompositeCollider2D.");
            Require(collision.GetComponent<CompositeCollider2D>() != null, "Collision_Tilemap needs a CompositeCollider2D.");

            foreach (string objectName in new[] { "BedObject", "DeskObject", "DiningTableObject", "FamilyNpcObject", "ExitDoorObject" })
            {
                GameObject targetObject = GameObject.Find(objectName);
                Require(targetObject != null, objectName + " is missing.");
                Require(targetObject.GetComponent<SpriteRenderer>() != null, objectName + " needs a SpriteRenderer.");
                Require(targetObject.GetComponent<MapInteractionPoint>() != null, objectName + " needs a MapInteractionPoint.");
                Require(targetObject.GetComponent<MapInteractionTarget>() != null, objectName + " needs a MapInteractionTarget.");
                Require(targetObject.GetComponent<MapInteractionStateVisual>() != null, objectName + " needs a MapInteractionStateVisual.");
            }

            GameObject player = GameObject.Find("PlayerRpg");
            Require(player != null, "PlayerRpg is missing.");
            Rigidbody2D playerBody = player.GetComponent<Rigidbody2D>();
            Require(playerBody != null && Mathf.Approximately(playerBody.gravityScale, 0f) && playerBody.freezeRotation, "PlayerRpg needs Rigidbody2D gravity 0 and frozen rotation.");
            Require(player.GetComponent<Collider2D>() != null, "PlayerRpg needs a Collider2D.");
            Require(player.GetComponent<Animator>() != null && player.GetComponent<Animator>().runtimeAnimatorController != null, "PlayerRpg needs an Animator Controller.");
            Require(player.GetComponent<RpgPlayerController>() != null, "PlayerRpg needs RpgPlayerController.");

            FullscreenMapSceneController sceneController = Object.FindObjectOfType<FullscreenMapSceneController>();
            Require(sceneController != null && sceneController.createFallbackRuntimeServices, "FullscreenMapSceneController needs fallback runtime services enabled.");
            MapInteractionTarget exitTarget = GameObject.Find("ExitDoorObject").GetComponent<MapInteractionTarget>();
            Require(exitTarget.isExitPortal && !exitTarget.consumeTimeOnInteraction, "ExitDoorObject must be an exit portal without time cost.");

            MapInteractionTarget[] targets = Object.FindObjectsOfType<MapInteractionTarget>();
            Require(targets.Length >= 5, "Expected at least five active map interaction targets.");
            VerifyRuntimeInteractionFlow(sceneController);
            Debug.Log("HomeMapScene verification passed with " + targets.Length + " active interaction targets.");
        }

        private static void VerifyRuntimeInteractionFlow(FullscreenMapSceneController sceneController)
        {
            sceneController.InitializeScene();
            MapInteractionFlowController flow = Object.FindObjectOfType<MapInteractionFlowController>();
            StageController stage = Object.FindObjectOfType<StageController>();
            Require(flow != null, "MapInteractionFlowController is missing at runtime.");
            Require(stage != null && stage.sessionState != null, "StageController runtime service is missing.");
            Require(stage.sessionState.stageState.remainingTime > 1, "Runtime fallback stage time was not initialized.");

            flow.requireConfirmForActivities = false;
            MapInteractionTarget bedTarget = GameObject.Find("BedObject").GetComponent<MapInteractionTarget>();
            int beforeRest = stage.sessionState.stageState.remainingTime;
            flow.RefreshTarget(bedTarget);
            Require(flow.TryBeginInteraction(bedTarget), "BedObject interaction did not start.");
            int afterRest = stage.sessionState.stageState.remainingTime;
            Require(afterRest == beforeRest - 1, "BedObject activity should consume exactly one time.");

            flow.TryBeginInteraction(bedTarget);
            Require(stage.sessionState.stageState.remainingTime == afterRest, "Completed BedObject activity should not execute twice through the map flow.");

            MapInteractionTarget exitTarget = GameObject.Find("ExitDoorObject").GetComponent<MapInteractionTarget>();
            int beforeExit = stage.sessionState.stageState.remainingTime;
            Require(flow.TryBeginInteraction(exitTarget), "ExitDoorObject interaction did not start.");
            Require(stage.sessionState.stageState.remainingTime == beforeExit, "ExitDoorObject must not consume time.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new System.InvalidOperationException(message);
            }
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
            {
                GeneratedSpriteRoot,
                GeneratedCharacterRoot,
                GeneratedTileRoot,
                GeneratedDataRoot,
                GeneratedAnimationRoot,
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

        private static GeneratedAssets GenerateSpritesTilesActivitiesAndAnimations()
        {
            GeneratedAssets assets = new GeneratedAssets();
            assets.floorSprite = CropSprite(FloorSheet, GeneratedTileRoot + "/home_floor_wood.png", 96, 192, 48, 48, 48);
            assets.wallSprite = CropSprite(FloorSheet, GeneratedTileRoot + "/home_wall_panel.png", 0, 0, 48, 48, 48);
            assets.markerSprite = CropSprite(FloorSheet, GeneratedTileRoot + "/home_interaction_marker.png", 336, 624, 48, 48, 48);
            assets.bedSprite = CropSprite(BedroomSheet, GeneratedSpriteRoot + "/home_bed_blue.png", 430, 16, 100, 156, 48);
            assets.deskSprite = CropSprite(BedroomSheet, GeneratedSpriteRoot + "/home_desk_wood.png", 574, 18, 190, 126, 48);
            assets.diningTableSprite = CropSprite(KitchenSheet, GeneratedSpriteRoot + "/home_dining_table.png", 160, 610, 210, 150, 48);
            assets.exitDoorSprite = CropSprite(DoorSheet, GeneratedSpriteRoot + "/home_exit_door.png", 0, 0, 48, 96, 48);
            assets.playerIdleSprite = CropSprite(MollySheet, GeneratedCharacterRoot + "/home_player_idle_down.png", 144, 0, 48, 48, 48);
            assets.playerWalkSprites = new[]
            {
                CropSprite(MollySheet, GeneratedCharacterRoot + "/home_player_walk_0.png", 0, 96, 48, 48, 48),
                CropSprite(MollySheet, GeneratedCharacterRoot + "/home_player_walk_1.png", 48, 96, 48, 48, 48),
                CropSprite(MollySheet, GeneratedCharacterRoot + "/home_player_walk_2.png", 96, 96, 48, 48, 48),
                CropSprite(MollySheet, GeneratedCharacterRoot + "/home_player_walk_3.png", 144, 96, 48, 48, 48)
            };
            assets.familyNpcSprite = CropSprite(LucySheet, GeneratedCharacterRoot + "/home_family_npc_lucy.png", 144, 0, 48, 48, 48);

            assets.floorTile = CreateTile(GeneratedTileRoot + "/HomeFloorTile.asset", assets.floorSprite, Color.white);
            assets.wallTile = CreateTile(GeneratedTileRoot + "/HomeWallTile.asset", assets.wallSprite, Color.white);
            assets.collisionTile = CreateTile(GeneratedTileRoot + "/HomeCollisionTile.asset", assets.wallSprite, new Color(1f, 1f, 1f, 0f));

            assets.restActivity = CreateActivity(
                "HomeRestEarlySleep.asset",
                "activity_home_rest_early_sleep",
                "早点休息",
                "在床边早点休息，恢复精力并降低压力。",
                ActivityType.Rest,
                Reward(PlayerStatKey.Energy, 20, "精力 +20"),
                ReducePressure(5, "压力 -5"));
            assets.planActivity = CreateActivity(
                "HomeDeskPlan.asset",
                "activity_home_desk_plan",
                "整理计划",
                "在书桌整理明日计划。",
                ActivityType.Training,
                Reward(PlayerStatKey.Autonomy, 5, "自主 +5"),
                Reward(PlayerStatKey.Autonomy, 0, "明日效率 +10"));
            assets.familyDinnerActivity = CreateActivity(
                "HomeFamilyDinner.asset",
                "activity_home_family_dinner",
                "家庭晚饭聊天",
                "和家人边吃晚饭边聊聊近况。",
                ActivityType.Event,
                Reward(PlayerStatKey.ParentRelation, 5, "亲子 +5"),
                Reward(PlayerStatKey.Mood, 5, "心情 +5"));

            assets.idleClip = CreateClip(GeneratedAnimationRoot + "/HomePlayer_Idle.anim", new[] { assets.playerIdleSprite }, 4f);
            assets.walkClip = CreateClip(GeneratedAnimationRoot + "/HomePlayer_Walk.anim", assets.playerWalkSprites, 8f);
            assets.playerAnimator = CreateAnimatorController(GeneratedAnimationRoot + "/HomePlayer.controller", assets.idleClip, assets.walkClip);
            return assets;
        }

        private static Sprite CropSprite(string sourceAssetPath, string outputAssetPath, int x, int yFromTop, int width, int height, float pixelsPerUnit)
        {
            string sourcePath = Path.GetFullPath(sourceAssetPath);
            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(sourcePath));
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            int unityY = source.height - yFromTop - height;
            Color[] pixels = source.GetPixels(x, unityY, width, height);
            output.SetPixels(pixels);
            output.Apply();

            File.WriteAllBytes(Path.GetFullPath(outputAssetPath), output.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(output);
            AssetDatabase.ImportAsset(outputAssetPath, ImportAssetOptions.ForceUpdate);

            TextureImporter importer = AssetImporter.GetAtPath(outputAssetPath) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Sprite>(outputAssetPath);
        }

        private static Tile CreateTile(string path, Sprite sprite, Color color)
        {
            Tile tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }

            tile.sprite = sprite;
            tile.color = color;
            tile.colliderType = Tile.ColliderType.Sprite;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static ActivityData CreateActivity(string fileName, string id, string displayName, string description, ActivityType type, params RewardData[] rewards)
        {
            string path = GeneratedDataRoot + "/" + fileName;
            ActivityData activity = AssetDatabase.LoadAssetAtPath<ActivityData>(path);
            if (activity == null && File.Exists(Path.GetFullPath(path)))
            {
                RepairHomeMapActivityAssetScript(path);
                activity = AssetDatabase.LoadAssetAtPath<ActivityData>(path);
            }

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

        private static void RepairHomeMapActivityAssetScript(string path)
        {
            string scriptGuid = AssetDatabase.AssetPathToGUID(HomeMapActivityScript);
            if (string.IsNullOrEmpty(scriptGuid))
            {
                return;
            }

            string fullPath = Path.GetFullPath(path);
            string text = File.ReadAllText(fullPath);
            if (!text.Contains("m_Script: {fileID: 0}") && !text.Contains("m_EditorClassIdentifier: StudentGrowth.Runtime:StudentGrowth:ActivityData"))
            {
                return;
            }

            text = text.Replace("m_Script: {fileID: 0}", "m_Script: {fileID: 11500000, guid: " + scriptGuid + ", type: 3}");
            text = text.Replace("m_EditorClassIdentifier: StudentGrowth.Runtime:StudentGrowth:ActivityData", "m_EditorClassIdentifier: ");
            File.WriteAllText(fullPath, text);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
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

        private static AnimationClip CreateClip(string path, Sprite[] sprites, float frameRate)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
            }

            clip.frameRate = frameRate;
            ObjectReferenceKeyframe[] frames = new ObjectReferenceKeyframe[sprites.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                frames[i] = new ObjectReferenceKeyframe
                {
                    time = i / frameRate,
                    value = sprites[i]
                };
            }

            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, frames);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = sprites.Length > 1;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateAnimatorController(string path, AnimationClip idle, AnimationClip walk)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            EnsureParameter(controller, "MoveX", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "MoveY", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            EnsureParameter(controller, "IsMoving", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "Moving", AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState state in stateMachine.states.ToArray())
            {
                stateMachine.RemoveState(state.state);
            }

            AnimatorState idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            AnimatorState walkState = stateMachine.AddState("Walk");
            walkState.motion = walk;
            stateMachine.defaultState = idleState;

            AnimatorStateTransition toWalk = idleState.AddTransition(walkState);
            toWalk.hasExitTime = false;
            toWalk.duration = 0f;
            toWalk.AddCondition(AnimatorConditionMode.If, 0f, "IsMoving");
            AnimatorStateTransition toIdle = walkState.AddTransition(idleState);
            toIdle.hasExitTime = false;
            toIdle.duration = 0f;
            toIdle.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsMoving");

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void EnsureParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(parameter => parameter.name == name))
            {
                return;
            }

            controller.AddParameter(name, type);
        }

        private static void BuildScene(Scene scene, GeneratedAssets assets)
        {
            DisableLegacyObject("Background_HomeDiningRoom");
            DisableLegacyObject("Interact_Bed_Rest");
            DisableLegacyObject("Interact_Desk_Plan");
            DisableLegacyObject("Interact_DiningTable_FamilyDinner");
            DisableLegacyObject("Interact_ParentNpc");
            DisableLegacyObject("Interact_Door_ReturnStageMap");
            DisableLegacyObject("ParentNpcVisual");

            GameObject existing = GameObject.Find("HomeMapRpgRoot");
            if (existing != null)
            {
                Object.DestroyImmediate(existing);
            }

            GameObject root = new GameObject("HomeMapRpgRoot");
            SceneManager.MoveGameObjectToScene(root, scene);

            Grid grid = CreateGrid(root.transform);
            Tilemap ground = CreateTilemap(grid.transform, "Ground_Tilemap", 0, true, false);
            Tilemap wall = CreateTilemap(grid.transform, "Wall_Tilemap", 20, true, false);
            Tilemap decoration = CreateTilemap(grid.transform, "Decoration_Tilemap", 40, true, false);
            Tilemap collision = CreateTilemap(grid.transform, "Collision_Tilemap", 0, false, true);
            Tilemap abovePlayer = CreateTilemap(grid.transform, "AbovePlayer_Tilemap", 2200, true, false);

            FillGround(ground, assets.floorTile);
            BuildWalls(wall, collision, abovePlayer, assets.wallTile, assets.collisionTile);
            BuildFurnitureCollision(collision, assets.collisionTile);

            MapDebugOverlayView debugOverlay = root.AddComponent<MapDebugOverlayView>();
            MapInteractionFlowController flow = root.AddComponent<MapInteractionFlowController>();

            GameObject bed = CreateInteractionObject(
                "BedObject",
                root.transform,
                assets.bedSprite,
                new Vector3(-3.35f, 1.15f, -0.1f),
                new Vector2(1.2f, 1.45f),
                "location_home",
                assets.restActivity.activityId,
                "早点休息",
                "按 E：早点休息｜消耗 1 时间｜精力 +20｜压力 -5",
                assets.restActivity,
                false,
                10,
                assets,
                debugOverlay);

            GameObject desk = CreateInteractionObject(
                "DeskObject",
                root.transform,
                assets.deskSprite,
                new Vector3(3.15f, 1.35f, -0.1f),
                new Vector2(1.65f, 0.9f),
                "location_home",
                assets.planActivity.activityId,
                "整理计划",
                "按 E：整理计划｜消耗 1 时间｜自主 +5｜明日效率 +10",
                assets.planActivity,
                false,
                8,
                assets,
                debugOverlay);

            GameObject dining = CreateInteractionObject(
                "DiningTableObject",
                root.transform,
                assets.diningTableSprite,
                new Vector3(-0.15f, -0.55f, -0.1f),
                new Vector2(1.8f, 1.1f),
                "location_home",
                assets.familyDinnerActivity.activityId,
                "家庭晚饭聊天",
                "按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5",
                assets.familyDinnerActivity,
                false,
                9,
                assets,
                debugOverlay);

            GameObject npc = CreateInteractionObject(
                "FamilyNpcObject",
                root.transform,
                assets.familyNpcSprite,
                new Vector3(1.72f, -0.2f, -0.1f),
                new Vector2(0.72f, 0.82f),
                "location_home",
                assets.familyDinnerActivity.activityId,
                "家庭晚饭聊天",
                "按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5",
                assets.familyDinnerActivity,
                false,
                7,
                assets,
                debugOverlay);

            GameObject exit = CreateInteractionObject(
                "ExitDoorObject",
                root.transform,
                assets.exitDoorSprite,
                new Vector3(4.25f, -2.35f, -0.1f),
                new Vector2(0.85f, 1f),
                "location_home",
                string.Empty,
                "返回阶段地图",
                "按 E：返回阶段地图｜不消耗时间",
                null,
                true,
                20,
                assets,
                debugOverlay);
            exit.AddComponent<MapExitPortal>().destinationId = "stage_hub";

            ConfigurePlayer(assets, flow);
            ConfigureSceneController(flow, debugOverlay);
            ConfigureCamera();

            MapInteractionStateVisual[] visuals = root.GetComponentsInChildren<MapInteractionStateVisual>(true);
            flow.stateVisuals = visuals;
            debugOverlay.debugObjects = visuals
                .Where(visual => visual != null && visual.debugRange != null)
                .Select(visual => visual.debugRange)
                .ToList();
            debugOverlay.SetVisible(false);

            decoration.SetTile(new Vector3Int(-4, 2, 0), assets.wallTile);
            decoration.SetTile(new Vector3Int(4, 2, 0), assets.wallTile);

            Selection.activeGameObject = root;
        }

        private static Grid CreateGrid(Transform parent)
        {
            GameObject gridObject = new GameObject("HomeTilemapGrid");
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
                collider.usedByComposite = true;
                tilemapObject.AddComponent<CompositeCollider2D>();
            }

            return tilemap;
        }

        private static void FillGround(Tilemap ground, Tile floorTile)
        {
            for (int x = -5; x <= 5; x++)
            {
                for (int y = -3; y <= 3; y++)
                {
                    ground.SetTile(new Vector3Int(x, y, 0), floorTile);
                }
            }
        }

        private static void BuildWalls(Tilemap wall, Tilemap collision, Tilemap abovePlayer, Tile wallTile, Tile collisionTile)
        {
            for (int x = -5; x <= 5; x++)
            {
                wall.SetTile(new Vector3Int(x, 3, 0), wallTile);
                abovePlayer.SetTile(new Vector3Int(x, 3, 0), wallTile);
                collision.SetTile(new Vector3Int(x, 3, 0), collisionTile);
                collision.SetTile(new Vector3Int(x, -3, 0), collisionTile);
            }

            for (int y = -3; y <= 3; y++)
            {
                wall.SetTile(new Vector3Int(-5, y, 0), wallTile);
                wall.SetTile(new Vector3Int(5, y, 0), wallTile);
                collision.SetTile(new Vector3Int(-5, y, 0), collisionTile);
                collision.SetTile(new Vector3Int(5, y, 0), collisionTile);
            }
        }

        private static void BuildFurnitureCollision(Tilemap collision, Tile collisionTile)
        {
            foreach (Vector3Int cell in new[]
            {
                new Vector3Int(-4, 1, 0),
                new Vector3Int(-3, 1, 0),
                new Vector3Int(3, 1, 0),
                new Vector3Int(4, 1, 0),
                new Vector3Int(0, -1, 0),
                new Vector3Int(1, -1, 0)
            })
            {
                collision.SetTile(cell, collisionTile);
            }
        }

        private static GameObject CreateInteractionObject(
            string name,
            Transform parent,
            Sprite sprite,
            Vector3 position,
            Vector2 colliderSize,
            string locationId,
            string actionId,
            string displayName,
            string promptText,
            ActivityData activity,
            bool isExit,
            int priority,
            GeneratedAssets assets,
            MapDebugOverlayView debugOverlay)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = position;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = isExit ? 70 : 60;

            BoxCollider2D bodyCollider = gameObject.AddComponent<BoxCollider2D>();
            bodyCollider.size = colliderSize;
            bodyCollider.offset = Vector2.zero;

            BoxCollider2D trigger = gameObject.AddComponent<BoxCollider2D>();
            trigger.isTrigger = true;
            trigger.size = colliderSize + new Vector2(0.7f, 0.7f);

            MapInteractionPoint point = gameObject.AddComponent<MapInteractionPoint>();
            point.locationId = locationId;
            point.actionId = actionId;
            point.displayName = displayName;
            point.description = promptText;
            point.interactionType = isExit ? MapInteractionType.ReturnToMainUi : MapInteractionType.LocationAction;
            point.requiresUnlockedLocation = !isExit;

            MapInteractionTarget target = gameObject.AddComponent<MapInteractionTarget>();
            target.interactionId = string.IsNullOrEmpty(actionId) ? "exit_home" : actionId;
            target.displayName = displayName;
            target.promptText = promptText;
            target.fallbackActivity = activity;
            target.interactionPoint = point;
            target.isExitPortal = isExit;
            target.consumeTimeOnInteraction = !isExit;
            target.requireFacing = false;
            target.interactionRange = 1.05f;
            target.priority = priority;

            MapInteractionStateVisual visual = gameObject.AddComponent<MapInteractionStateVisual>();
            visual.target = target;
            visual.tintRenderers = new[] { renderer };
            visual.nearbyIndicator = CreateMarker(name + "_NearbyMarker", gameObject.transform, assets.markerSprite, new Vector3(0f, colliderSize.y * 0.5f + 0.35f, 0f), new Color(1f, 0.92f, 0.35f, 0.95f), 90, 0.22f);
            visual.debugRange = CreateMarker(name + "_DebugRange", gameObject.transform, assets.markerSprite, Vector3.zero, new Color(0.9f, 0.25f, 0.18f, 0.25f), 89, Mathf.Max(colliderSize.x, colliderSize.y) + 0.7f);
            visual.nearbyIndicator.SetActive(false);
            visual.debugRange.SetActive(false);

            if (debugOverlay != null)
            {
                debugOverlay.debugObjects.Add(visual.debugRange);
            }

            return gameObject;
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

        private static void ConfigurePlayer(GeneratedAssets assets, MapInteractionFlowController flow)
        {
            GameObject player = GameObject.Find("PlayerRpg");
            if (player == null)
            {
                player = new GameObject("PlayerRpg");
            }

            player.transform.position = new Vector3(0f, -2f, -0.1f);
            player.transform.localScale = Vector3.one;
            SpriteRenderer renderer = player.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                renderer = player.AddComponent<SpriteRenderer>();
            }

            renderer.sprite = assets.playerIdleSprite;
            renderer.sortingOrder = 1000;

            Rigidbody2D body = player.GetComponent<Rigidbody2D>();
            if (body == null)
            {
                body = player.AddComponent<Rigidbody2D>();
            }

            body.gravityScale = 0f;
            body.freezeRotation = true;

            Collider2D collider = player.GetComponent<Collider2D>();
            if (collider == null)
            {
                CapsuleCollider2D capsule = player.AddComponent<CapsuleCollider2D>();
                capsule.size = new Vector2(0.45f, 0.72f);
                capsule.offset = new Vector2(0f, -0.08f);
                collider = capsule;
            }

            Animator animator = player.GetComponent<Animator>();
            if (animator == null)
            {
                animator = player.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = assets.playerAnimator;

            PlayerFacingInteractor interactor = player.GetComponent<PlayerFacingInteractor>();
            if (interactor == null)
            {
                interactor = player.AddComponent<PlayerFacingInteractor>();
            }

            RpgPlayerController controller = player.GetComponent<RpgPlayerController>();
            if (controller == null)
            {
                controller = player.AddComponent<RpgPlayerController>();
            }

            controller.body = body;
            controller.bodyCollider = collider;
            controller.spriteRenderer = renderer;
            controller.animator = animator;
            controller.facingInteractor = interactor;
            controller.interactionFlowController = flow;
            controller.moveSpeed = 3f;
            controller.useMovementBounds = true;
            controller.movementMin = new Vector2(-4.35f, -2.45f);
            controller.movementMax = new Vector2(4.35f, 2.35f);

            MapObjectYSorter sorter = player.GetComponent<MapObjectYSorter>();
            if (sorter == null)
            {
                sorter = player.AddComponent<MapObjectYSorter>();
            }

            sorter.renderers = new[] { renderer };
        }

        private static void ConfigureSceneController(MapInteractionFlowController flow, MapDebugOverlayView debugOverlay)
        {
            GameObject sceneRoot = GameObject.Find("HomeMapSceneRoot");
            if (sceneRoot == null)
            {
                sceneRoot = new GameObject("HomeMapSceneRoot");
            }

            MapInteractionController mapController = sceneRoot.GetComponent<MapInteractionController>();
            if (mapController == null)
            {
                mapController = sceneRoot.AddComponent<MapInteractionController>();
            }

            FullscreenMapSceneController sceneController = sceneRoot.GetComponent<FullscreenMapSceneController>();
            if (sceneController == null)
            {
                sceneController = sceneRoot.AddComponent<FullscreenMapSceneController>();
            }

            sceneController.mapId = "location_home";
            sceneController.displayName = "家里｜恢复精力、沟通家庭、整理计划。";
            sceneController.player = GameObject.Find("PlayerRpg").transform;
            sceneController.playerController = sceneController.player.GetComponent<RpgPlayerController>();
            sceneController.facingInteractor = sceneController.player.GetComponent<PlayerFacingInteractor>();
            sceneController.interactionController = mapController;
            sceneController.autoCollectTargets = true;
            sceneController.createFallbackUi = true;
            sceneController.createFallbackRuntimeServices = true;
            sceneController.defaultSpawnPointId = "home_entry";

            flow.interactionController = mapController;
            flow.debugOverlay = debugOverlay;

            PlayerFacingInteractor interactor = sceneController.facingInteractor;
            if (interactor != null)
            {
                interactor.mapInteractionController = mapController;
            }
        }

        private static void ConfigureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.AddComponent<AudioListener>();
            }

            camera.orthographic = true;
            camera.orthographicSize = 4.2f;
            camera.transform.position = new Vector3(0f, 0f, -10f);
            MapCameraController cameraController = camera.GetComponent<MapCameraController>();
            if (cameraController != null)
            {
                cameraController.useExplicitBounds = true;
                cameraController.minPosition = new Vector2(-1.2f, -0.8f);
                cameraController.maxPosition = new Vector2(1.2f, 0.8f);
                cameraController.followTarget = GameObject.Find("PlayerRpg").transform;
            }
        }

        private static void DisableLegacyObject(string name)
        {
            GameObject gameObject = GameObject.Find(name);
            if (gameObject != null)
            {
                gameObject.SetActive(false);
            }
        }

        private static void WriteReports(GeneratedAssets assets)
        {
            File.WriteAllText(
                DocsRoot + "/HomeMapAssetUsageReport.md",
                "# HomeMap Asset Usage Report\n\n"
                + "| Scene Object | Asset | Source | Copied/Moved | Risk |\n"
                + "| --- | --- | --- | --- | --- |\n"
                + "| Ground_Tilemap | home_floor_wood.png | " + FloorSheet + " | Cropped copy, original kept | Low |\n"
                + "| Wall_Tilemap / AbovePlayer_Tilemap | home_wall_panel.png | " + FloorSheet + " | Cropped copy, original kept | Low |\n"
                + "| BedObject | home_bed_blue.png | " + BedroomSheet + " | Cropped copy, original kept | Low |\n"
                + "| DeskObject | home_desk_wood.png | " + BedroomSheet + " | Cropped copy, original kept | Low |\n"
                + "| DiningTableObject | home_dining_table.png | " + KitchenSheet + " | Cropped copy, original kept | Low |\n"
                + "| ExitDoorObject | home_exit_door.png | " + DoorSheet + " | Cropped copy, original kept | Low |\n"
                + "| PlayerRpg | home_player_*.png | " + MollySheet + " | Cropped copy, original kept | Existing scene player sprite replaced in HomeMapScene only |\n"
                + "| FamilyNpcObject | home_family_npc_lucy.png | " + LucySheet + " | Cropped copy, original kept | Low |\n\n"
                + "No original art file was deleted or moved. Existing project references outside HomeMapScene were not retargeted.\n"
                + "Manual confirmation needed: `明日效率 +10` has no dedicated stat in `PlayerState`, so it is represented as a zero-value reward description in the Home desk activity.\n");

            File.WriteAllText(
                DocsRoot + "/HomeMapBuildReport.md",
                "# HomeMap Build Report\n\n"
                + "## Added Scene Structure\n\n"
                + "- `HomeMapRpgRoot`\n"
                + "- `HomeTilemapGrid`\n"
                + "- `Ground_Tilemap`\n"
                + "- `Wall_Tilemap`\n"
                + "- `Decoration_Tilemap`\n"
                + "- `Collision_Tilemap` with `TilemapCollider2D`, `CompositeCollider2D`, and static `Rigidbody2D`\n"
                + "- `AbovePlayer_Tilemap`\n\n"
                + "## Added Objects\n\n"
                + "- `BedObject`: `MapInteractionPoint` + `MapInteractionTarget`, prompt `按 E：早点休息｜消耗 1 时间｜精力 +20｜压力 -5`\n"
                + "- `DeskObject`: `MapInteractionPoint` + `MapInteractionTarget`, prompt `按 E：整理计划｜消耗 1 时间｜自主 +5｜明日效率 +10`\n"
                + "- `DiningTableObject`: `MapInteractionPoint` + `MapInteractionTarget`, prompt `按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5`\n"
                + "- `FamilyNpcObject`: linked to the family dinner interaction\n"
                + "- `ExitDoorObject`: `MapExitPortal`, `ReturnToMainUi`, no activity reward or time cost\n\n"
                + "## Runtime Flow\n\n"
                + "Formal activities still execute through `MapInteractionPoint -> MapInteractionController -> LocationActionExecutor -> StageController.CompleteActivity -> RewardResolver -> GameSessionState`.\n"
                + "The exit uses `MapExitPortal -> MapInteractionController -> LocationSceneRouter.ReturnToStageMap()` or `SceneTransitionController.ShowMap()` and does not call activity completion.\n\n"
                + "## UI And Debug\n\n"
                + "- Top HUD uses `FullscreenMapHudView` and includes location, time, energy, mood, parent relation, and money.\n"
                + "- Bottom prompt defaults to `靠近可互动位置后按 E 交互。`.\n"
                + "- `MapInteractionFlowController` handles Idle, Nearby, Confirming, Executing, Completed, Locked, and Debug states.\n"
                + "- F3 toggles debug trigger range visibility.\n\n"
                + "## Runtime Services\n\n"
                + "- `FullscreenMapSceneController` can create fallback runtime services when the scene is opened directly.\n"
                + "- Fallback services configure `StageController`, `ActivityController`, `LocationActionExecutor`, `SceneTransitionController`, and `LocationSceneRouter` without bypassing the normal activity completion path.\n\n"
                + "## Generated Assets\n\n"
                + "- Activity assets: `" + AssetDatabase.GetAssetPath(assets.restActivity) + "`, `" + AssetDatabase.GetAssetPath(assets.planActivity) + "`, `" + AssetDatabase.GetAssetPath(assets.familyDinnerActivity) + "`\n"
                + "- Activity asset type: `HomeMapActivityData : ActivityData`, used only to give Unity a valid script asset for generated home-map activity files.\n"
                + "- Animator: `" + AssetDatabase.GetAssetPath(assets.playerAnimator) + "`\n"
                + "- Animation clips: `" + AssetDatabase.GetAssetPath(assets.idleClip) + "`, `" + AssetDatabase.GetAssetPath(assets.walkClip) + "`\n");
            AssetDatabase.ImportAsset(DocsRoot + "/HomeMapAssetUsageReport.md");
            AssetDatabase.ImportAsset(DocsRoot + "/HomeMapBuildReport.md");
        }

        private sealed class GeneratedAssets
        {
            public Sprite floorSprite;
            public Sprite wallSprite;
            public Sprite markerSprite;
            public Sprite bedSprite;
            public Sprite deskSprite;
            public Sprite diningTableSprite;
            public Sprite exitDoorSprite;
            public Sprite playerIdleSprite;
            public Sprite[] playerWalkSprites;
            public Sprite familyNpcSprite;
            public Tile floorTile;
            public Tile wallTile;
            public Tile collisionTile;
            public ActivityData restActivity;
            public ActivityData planActivity;
            public ActivityData familyDinnerActivity;
            public AnimationClip idleClip;
            public AnimationClip walkClip;
            public AnimatorController playerAnimator;
        }
    }
}
