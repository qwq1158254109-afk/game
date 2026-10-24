using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StudentGrowth;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace StudentGrowthEditor
{
    public static class LibraryLibrarianBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/LibraryMapScene.unity";
        private const string SourceSheetPath = "Assets/Art/New2_0/Characters/Home/New/new_anime_character_walk_32x32_with_costumes.png";
        private const string CharacterRoot = "Assets/Art/New2_0/Characters/LibraryLibrarian/White";
        private const string FrameRoot = CharacterRoot + "/Frames";
        private const string AnimationRoot = "Assets/Animations/StudentGrowth/New2_0/Library";
        private const string ControllerPath = AnimationRoot + "/LibraryLibrarian.controller";
        private const string DataRoot = "Assets/Data/StudentGrowth/New2_0/LibraryMap";
        private const string ActivityPath = DataRoot + "/LibraryLibrarianTalkData.asset";
        private const string EventPath = DataRoot + "/LibraryLibrarianEvent.asset";
        private const string MarkerSpritePath = "Assets/Art/New2_0/Tilesets/Sliced/new20_interaction_marker_00.png";
        private const int FrameSize = 32;
        private const int PixelsPerUnit = 16;
        private const int WhiteCharacterTopRow = 4;

        private static readonly string[] Directions = { "down", "left", "right", "up" };

        [MenuItem("Tools/Student Growth/Library/Build Librarian")]
        public static void BuildAndPlace()
        {
            LibrarianAssets assets = GenerateAssets();
            PlaceInScene(assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("White-haired library character created as the interactive librarian in LibraryMapScene.");
        }

        [MenuItem("Tools/Student Growth/Library/Validate Librarian")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject librarian = GameObject.Find("LibraryLibrarian");
            Require(librarian != null, "LibraryLibrarian is missing.");

            SpriteRenderer renderer = librarian.GetComponent<SpriteRenderer>();
            Animator animator = librarian.GetComponent<Animator>();
            MapInteractionPoint point = librarian.GetComponent<MapInteractionPoint>();
            MapInteractionTarget target = librarian.GetComponent<MapInteractionTarget>();
            Require(renderer != null && renderer.sprite != null, "LibraryLibrarian SpriteRenderer is invalid.");
            Require(AssetDatabase.GetAssetPath(renderer.sprite).StartsWith(FrameRoot, StringComparison.Ordinal),
                "LibraryLibrarian is not using the extracted white-haired character frames.");
            Require(animator != null && animator.runtimeAnimatorController != null,
                "LibraryLibrarian Animator is missing its controller.");
            Require(point != null && point.interactionType == MapInteractionType.LocationAction,
                "LibraryLibrarian interaction point is invalid.");
            Require(target != null && target.fallbackActivity != null && !target.consumeTimeOnInteraction,
                "LibraryLibrarian no-cost interaction is invalid.");

            PlayerFacingInteractor playerInteractor = Object.FindAnyObjectByType<PlayerFacingInteractor>();
            Require(playerInteractor != null && playerInteractor.targets.Contains(target),
                "PlayerFacingInteractor does not include the librarian.");
            Debug.Log("Library librarian validation passed: white frames, animator, collider and interaction are valid.");
        }

        private static LibrarianAssets GenerateAssets()
        {
            EnsureFolder(CharacterRoot);
            EnsureFolder(FrameRoot);
            EnsureFolder(AnimationRoot);
            EnsureFolder(DataRoot);

            Texture2D sheet = LoadTexture(SourceSheetPath);
            Require(sheet.width == 384 && sheet.height == 256,
                "The character sheet must remain 384x256 with 32x32 cells.");

            Dictionary<string, Sprite[]> frames = new Dictionary<string, Sprite[]>();
            for (int directionIndex = 0; directionIndex < Directions.Length; directionIndex++)
            {
                string direction = Directions[directionIndex];
                Sprite[] directionFrames = new Sprite[3];
                for (int frameIndex = 0; frameIndex < directionFrames.Length; frameIndex++)
                {
                    string path = GetFramePath(direction, frameIndex);
                    WriteFrame(sheet, frameIndex, WhiteCharacterTopRow + directionIndex, path);
                    ConfigureFrame(path);
                    directionFrames[frameIndex] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Require(directionFrames[frameIndex] != null, "Failed to import librarian frame: " + path);
                }

                frames[direction] = directionFrames;
            }

            Object.DestroyImmediate(sheet);
            Dictionary<string, AnimationClip> clips = new Dictionary<string, AnimationClip>();
            foreach (string direction in Directions)
            {
                clips[direction] = CreateOrUpdateClip(direction, frames[direction]);
            }

            AnimatorController controller = CreateOrUpdateController(clips);
            ActivityData activity = CreateOrUpdateActivity();
            Sprite marker = AssetDatabase.LoadAssetAtPath<Sprite>(MarkerSpritePath);
            Require(marker != null, "Interaction marker sprite is missing.");
            return new LibrarianAssets
            {
                idleSprite = frames["down"][1],
                controller = controller,
                activity = activity,
                markerSprite = marker
            };
        }

        private static void WriteFrame(Texture2D source, int column, int topRow, string destinationPath)
        {
            Texture2D output = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false);
            int sourceBottom = source.height - (topRow + 1) * FrameSize;
            int sourceLeft = column * FrameSize;
            for (int y = 0; y < FrameSize; y++)
            {
                for (int x = 0; x < FrameSize; x++)
                {
                    output.SetPixel(x, y, source.GetPixel(sourceLeft + x, sourceBottom + y));
                }
            }

            output.Apply();
            File.WriteAllBytes(Path.GetFullPath(destinationPath), output.EncodeToPNG());
            Object.DestroyImmediate(output);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void ConfigureFrame(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Librarian frame importer is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static AnimationClip CreateOrUpdateClip(string direction, Sprite[] frames)
        {
            string clipPath = AnimationRoot + "/LibraryLibrarian_Idle_" + Capitalize(direction) + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, clipPath);
            }

            clip.frameRate = 4f;
            EditorCurveBinding binding = new EditorCurveBinding
            {
                type = typeof(SpriteRenderer),
                path = string.Empty,
                propertyName = "m_Sprite"
            };
            ObjectReferenceKeyframe[] keyframes =
            {
                new ObjectReferenceKeyframe { time = 0f, value = frames[0] },
                new ObjectReferenceKeyframe { time = 0.25f, value = frames[1] },
                new ObjectReferenceKeyframe { time = 0.5f, value = frames[2] },
                new ObjectReferenceKeyframe { time = 0.75f, value = frames[1] }
            };
            AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes);
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        private static AnimatorController CreateOrUpdateController(Dictionary<string, AnimationClip> clips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState defaultState = null;
            foreach (string direction in Directions)
            {
                string stateName = "Idle" + Capitalize(direction);
                AnimatorState state = stateMachine.states
                    .Select(child => child.state)
                    .FirstOrDefault(candidate => candidate != null && candidate.name == stateName);
                if (state == null)
                {
                    state = stateMachine.AddState(stateName);
                }

                state.motion = clips[direction];
                if (direction == "down")
                {
                    defaultState = state;
                }
            }

            stateMachine.defaultState = defaultState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static ActivityData CreateOrUpdateActivity()
        {
            LibraryMapActivityData activity = AssetDatabase.LoadAssetAtPath<LibraryMapActivityData>(ActivityPath);
            if (activity == null)
            {
                activity = ScriptableObject.CreateInstance<LibraryMapActivityData>();
                AssetDatabase.CreateAsset(activity, ActivityPath);
            }

            activity.activityId = "activity_library_librarian_talk";
            activity.displayName = "询问图书管理员";
            activity.description = "图书管理员提醒你保持安静，并建议先整理复习目标，再去对应书架查找资料。";
            activity.activityType = ActivityType.Event;
            // 2026-10-22：原来 timeCost=0、次数默认 1 且没有挂事件 ——
            // 点一次只弹一行"完成活动"，第二次就被「本阶段已完成」挡住。
            // 现在改成：耗 1 次行动、可反复询问、挂一个对话事件。
            activity.timeCost = 1;
            activity.maxUsesPerStage = -1;
            activity.linkedEvent = AssetDatabase.LoadAssetAtPath<GameEventData>(EventPath);
            if (activity.linkedEvent == null)
            {
                Debug.LogWarning("[LibraryLibrarian] 缺少对话事件资产：" + EventPath);
            }
            activity.fixedRewards.Clear();
            activity.possibleRewards.Clear();
            activity.appearConditions.Clear();
            EditorUtility.SetDirty(activity);
            return activity;
        }

        private static void PlaceInScene(LibrarianAssets assets)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            GameObject sceneRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "LibraryMapSceneRoot");
            Require(sceneRoot != null, "LibraryMapSceneRoot is missing.");
            Transform mapRoot = sceneRoot.transform.Find("LibraryMapRpgRoot");
            Require(mapRoot != null, "LibraryMapRpgRoot is missing.");
            Transform interactionRoot = mapRoot.Find("InteractableObjects");
            Require(interactionRoot != null, "InteractableObjects is missing.");

            Transform existing = interactionRoot.Find("LibraryLibrarian");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            GameObject librarian = new GameObject("LibraryLibrarian");
            librarian.transform.SetParent(interactionRoot, false);
            librarian.transform.localPosition = new Vector3(0f, 5.25f, -0.08f);

            SpriteRenderer renderer = librarian.AddComponent<SpriteRenderer>();
            renderer.sprite = assets.idleSprite;
            renderer.sortingOrder = MapObjectYSorter.CalculateSortingOrder(5.25f, 1000, 100f);
            Animator animator = librarian.AddComponent<Animator>();
            animator.runtimeAnimatorController = assets.controller;
            BoxCollider2D body = librarian.AddComponent<BoxCollider2D>();
            body.size = new Vector2(0.75f, 1f);
            body.offset = new Vector2(0f, -0.35f);

            MapInteractionPoint point = librarian.AddComponent<MapInteractionPoint>();
            point.locationId = "location_library";
            point.actionId = assets.activity.activityId;
            point.interactionType = MapInteractionType.LocationAction;
            point.displayName = "图书管理员";
            point.description = assets.activity.description;
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = librarian.AddComponent<MapInteractionTarget>();
            target.interactionId = "library_librarian";
            target.displayName = "图书管理员";
            target.promptText = "按 E：和图书管理员交谈";
            target.fallbackActivity = assets.activity;
            // 对话优先于活动：交互后直接进入「打招呼 / 赠送礼物 / 购买」菜单。
            target.dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(
                "Assets/Resources/StudentGrowth/Dialogue/Dialogue_LibraryLibrarian.asset");
            // 2026-10-24：NPC 实体（好感度记账 ID）。缺失只告警不阻断——
            // 送礼好感会退回旧行为（只加心情），对话菜单不受影响。
            target.npc = AssetDatabase.LoadAssetAtPath<NpcData>(
                "Assets/Resources/StudentGrowth/NPC/npc_librarian.asset");
            if (target.npc == null)
            {
                Debug.LogWarning("[LibraryLibrarianBuilder] npc_librarian.asset 未找到，好感度接线缺失。");
            }
            target.interactionPoint = point;
            target.consumeTimeOnInteraction = false;
            target.requireFacing = false;
            target.interactionRange = 2.5f;
            target.priority = 30;

            MapObjectYSorter sorter = librarian.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };
            sorter.useRendererCenterAsSortY = true;

            MapInteractionStateVisual stateVisual = librarian.AddComponent<MapInteractionStateVisual>();
            stateVisual.target = target;
            stateVisual.tintRenderers = new[] { renderer };
            stateVisual.nearbyIndicator = CreateMarker(
                "NearbyMarker",
                librarian.transform,
                assets.markerSprite,
                new Vector3(0f, 1.15f, 0f),
                new Color(1f, 0.92f, 0.35f, 0.95f),
                2500,
                0.32f);
            stateVisual.debugRange = CreateMarker(
                "DebugRange",
                librarian.transform,
                assets.markerSprite,
                Vector3.zero,
                new Color(0.9f, 0.25f, 0.18f, 0.2f),
                2490,
                target.interactionRange);
            stateVisual.nearbyIndicator.SetActive(false);
            stateVisual.debugRange.SetActive(false);

            MapInteractionTarget[] targets = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MapInteractionTarget>(true))
                .ToArray();
            FullscreenMapSceneController sceneController = sceneRoot.GetComponent<FullscreenMapSceneController>();
            Require(sceneController != null, "FullscreenMapSceneController is missing.");
            sceneController.interactionTargets = targets;
            PlayerFacingInteractor playerInteractor = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<PlayerFacingInteractor>(true))
                .FirstOrDefault();
            Require(playerInteractor != null, "PlayerFacingInteractor is missing.");
            playerInteractor.targets = targets;

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Failed to save LibraryMapScene with librarian.");
        }

        private static GameObject CreateMarker(
            string name,
            Transform parent,
            Sprite sprite,
            Vector3 localPosition,
            Color color,
            int sortingOrder,
            float scale)
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

        private static Texture2D LoadTexture(string path)
        {
            Require(File.Exists(Path.GetFullPath(path)), "Missing texture: " + path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Require(texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(path))), "Failed to load texture: " + path);
            return texture;
        }

        private static string GetFramePath(string direction, int frameIndex)
        {
            return FrameRoot + $"/library_librarian_{direction}_{frameIndex}.png";
        }

        private static string Capitalize(string value)
        {
            return char.ToUpperInvariant(value[0]) + value.Substring(1);
        }

        private static void EnsureFolder(string assetPath)
        {
            Directory.CreateDirectory(Path.GetFullPath(assetPath));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class LibrarianAssets
        {
            public Sprite idleSprite;
            public RuntimeAnimatorController controller;
            public ActivityData activity;
            public Sprite markerSprite;
        }
    }
}
