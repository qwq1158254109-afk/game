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
    public static class ClubRoomSceneFinalizer
    {
        public const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/ClubRoomMapScene.unity";
        public const string FinalSpriteRoot = "Assets/Art/New2_0/Club/Final/Facilities";
        private const float PixelsPerUnit = 64f;
        private const float GridStep = 0.25f;
        private const string MeetingActionId = "club_meeting";

        [MenuItem("Tools/Student Growth/Club/Apply V2 Finalization")]
        private static void ApplyFromMenu()
        {
            bool confirmed = EditorUtility.DisplayDialog(
                "Apply Club Room V2 finalization?",
                "This edits the formal scene, which is the final layout source. A backup should exist before continuing.",
                "Apply",
                "Cancel");
            if (confirmed)
            {
                ApplyToFormalScene();
            }
        }

        [MenuItem("Tools/Student Growth/Club/Validate V2 Finalization")]
        public static void ValidateFromMenu()
        {
            ValidateFinalScene();
        }

        public static void ApplyToFormalScene()
        {
            EnsureFolder(FinalSpriteRoot);
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid(), "ClubRoomMapScene could not be opened.");

            GameObject facilityRoot = RequireObject("EditableClubFacilities");
            GameObject wallDecorRoot = RequireObject("EditableClubWallDecor");
            NormalizeRenderers(facilityRoot.GetComponentsInChildren<SpriteRenderer>(true));
            NormalizeRenderers(wallDecorRoot.GetComponentsInChildren<SpriteRenderer>(true));
            SnapFinalLayout(facilityRoot.transform, wallDecorRoot.transform);
            ApplyLayoutAdjustments(facilityRoot.transform);
            ConfigureDoorAndSpawn();
            ConfigureMeetingEdgeInteractions();
            RefreshSorting(facilityRoot.transform);
            RefreshSceneControllerTargets();

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene, ScenePath), "Failed to save finalized ClubRoomMapScene.");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateFinalScene();
            Debug.Log("ClubRoomMapScene V2 finalization applied. The formal scene is now the layout source of truth.");
        }

        public static void ValidateFinalScene()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(scene.IsValid(), "ClubRoomMapScene could not be opened for V2 validation.");

            GameObject facilityRoot = RequireObject("EditableClubFacilities");
            GameObject wallDecorRoot = RequireObject("EditableClubWallDecor");
            GameObject spawn = RequireObject("ClubEntrySpawn");
            GameObject player = RequireObject("PlayerRpg");
            GameObject exit = RequireObject("ClubExitDoor");
            GameObject collision = RequireObject("Collision");

            MapInteractionTarget exitTarget = exit.GetComponent<MapInteractionTarget>();
            Require(exitTarget != null && exitTarget.isExitPortal, "Club exit target is missing.");
            Require(exitTarget.requireFacing, "Club exit must require the player to face the door.");
            Require(exitTarget.interactionRange <= 0.8f, "Club exit interaction range is too large.");
            Require(Vector2.Distance(spawn.transform.position, exit.transform.position)
                    > exitTarget.interactionRange + 0.1f,
                "Club spawn is still inside the exit interaction range.");
            Require(collision.transform.Find("DoorCollision")?.GetComponent<BoxCollider2D>() != null,
                "Closed club door needs a blocking collider.");

            Collider2D playerCollider = player.GetComponent<Collider2D>();
            Require(playerCollider != null, "Player collider is missing.");
            Vector3 originalPlayerPosition = player.transform.position;
            player.transform.position = spawn.transform.position;
            Physics2D.SyncTransforms();
            Collider2D[] staticColliders = collision.GetComponentsInChildren<Collider2D>(true);
            Require(staticColliders.All(item => !item.bounds.Intersects(playerCollider.bounds)),
                "Player bounds overlap a static collider at the club spawn.");
            player.transform.position = originalPlayerPosition;
            Physics2D.SyncTransforms();

            SpriteRenderer[] editableRenderers = facilityRoot.GetComponentsInChildren<SpriteRenderer>(true)
                .Concat(wallDecorRoot.GetComponentsInChildren<SpriteRenderer>(true))
                .ToArray();
            Require(editableRenderers.Length == 31,
                $"Expected 31 editable club renderers, found {editableRenderers.Length}.");
            foreach (SpriteRenderer renderer in editableRenderers)
            {
                Require(renderer.sprite != null, renderer.name + " has no sprite.");
                Require(IsScaleOne(renderer.transform.lossyScale), renderer.name + " must use Scale 1.");
                string spritePath = AssetDatabase.GetAssetPath(renderer.sprite);
                Require(spritePath.StartsWith(FinalSpriteRoot, StringComparison.Ordinal),
                    renderer.name + " is not using a normalized final sprite.");
                ValidateImporter(spritePath);
            }

            ValidateContactGrid(facilityRoot.transform);
            MapInteractionTarget[] meetingTargets = Object.FindObjectsByType<MapInteractionTarget>(FindObjectsInactive.Include)
                .Where(target => target.interactionPoint != null
                    && target.interactionPoint.actionId == MeetingActionId)
                .ToArray();
            Require(meetingTargets.Length == 4,
                $"Expected four meeting-table edge interactions, found {meetingTargets.Length}.");
            Require(meetingTargets.All(target => target.requireFacing && target.interactionRange <= 0.9f),
                "Meeting-table edge interactions must use facing and a short range.");

            Require(facilityRoot.GetComponentsInChildren<Collider2D>(true).Length >= 15,
                "Finalization removed club facility colliders.");
            Require(facilityRoot.GetComponentsInChildren<MapObjectYSorter>(true).Length >= 15,
                "Finalization removed club Y sorters.");
            FullscreenMapSceneController controller = Object.FindAnyObjectByType<FullscreenMapSceneController>();
            Require(controller != null && controller.mapId == "location_club",
                "Club scene controller is invalid after finalization.");
            Require(controller.interactionTargets != null && controller.interactionTargets.Length >= 7,
                "Club scene controller did not collect the finalized interaction targets.");

            Debug.Log($"Club V2 validation passed: {editableRenderers.Length} normalized renderers, "
                + $"{meetingTargets.Length} meeting edges, {staticColliders.Length} static colliders, "
                + $"{controller.interactionTargets.Length} interactions.");
        }

        private static void NormalizeRenderers(IEnumerable<SpriteRenderer> renderers)
        {
            foreach (SpriteRenderer renderer in renderers.OrderBy(item => item.gameObject.name, StringComparer.Ordinal))
            {
                NormalizeRenderer(renderer);
            }
        }

        private static void NormalizeRenderer(SpriteRenderer renderer)
        {
            Require(renderer != null && renderer.sprite != null, "Cannot normalize a missing club renderer.");
            Sprite sourceSprite = renderer.sprite;
            string sourcePath = AssetDatabase.GetAssetPath(sourceSprite);
            Require(!string.IsNullOrEmpty(sourcePath) && File.Exists(Path.GetFullPath(sourcePath)),
                "Club sprite source is missing: " + renderer.name);

            Bounds oldBounds = renderer.bounds;
            Vector3 oldScale = renderer.transform.localScale;
            BoxCollider2D boxCollider = renderer.GetComponent<BoxCollider2D>();
            Vector2 preservedColliderSize = boxCollider == null
                ? Vector2.zero
                : Vector2.Scale(boxCollider.size, new Vector2(Mathf.Abs(oldScale.x), Mathf.Abs(oldScale.y)));
            Vector2 preservedColliderOffset = boxCollider == null
                ? Vector2.zero
                : Vector2.Scale(boxCollider.offset, new Vector2(oldScale.x, oldScale.y));

            Texture2D source = LoadTexture(sourcePath);
            int targetWidth = Mathf.Max(1, Mathf.RoundToInt(source.width * Mathf.Abs(oldScale.x)));
            int targetHeight = Mathf.Max(1, Mathf.RoundToInt(source.height * Mathf.Abs(oldScale.y)));
            Texture2D normalized = ResizeNearest(source, targetWidth, targetHeight);
            string outputPath = FinalSpriteRoot + "/ClubFinal_" + SanitizeFileName(renderer.gameObject.name) + ".png";
            File.WriteAllBytes(Path.GetFullPath(outputPath), normalized.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(normalized);

            AssetDatabase.ImportAsset(outputPath, ImportAssetOptions.ForceSynchronousImport);
            ConfigureFinalSprite(outputPath);
            Sprite finalSprite = AssetDatabase.LoadAssetAtPath<Sprite>(outputPath);
            Require(finalSprite != null, "Normalized club sprite failed to import: " + outputPath);

            renderer.sprite = finalSprite;
            renderer.transform.localScale = Vector3.one;
            Vector3 position = renderer.transform.position;
            float bottomDelta = oldBounds.min.y - renderer.bounds.min.y;
            float centerDelta = oldBounds.center.x - renderer.bounds.center.x;
            renderer.transform.position = new Vector3(
                position.x + centerDelta,
                position.y + bottomDelta,
                position.z);

            if (boxCollider != null)
            {
                boxCollider.size = preservedColliderSize;
                boxCollider.offset = preservedColliderOffset;
            }

            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(renderer.transform);
            if (boxCollider != null)
            {
                EditorUtility.SetDirty(boxCollider);
            }
        }

        private static void SnapFinalLayout(Transform facilityRoot, Transform wallDecorRoot)
        {
            Transform[] meetingGroup = facilityRoot.Cast<Transform>()
                .Where(item => item.name == "MeetingTable"
                    || item.name.StartsWith("MeetingChair_", StringComparison.Ordinal))
                .ToArray();
            Transform meetingTable = meetingGroup.First(item => item.name == "MeetingTable");
            SpriteRenderer tableRenderer = meetingTable.GetComponent<SpriteRenderer>();
            Vector2 groupDelta = new Vector2(
                -1f - tableRenderer.bounds.center.x,
                -2.75f - tableRenderer.bounds.min.y);
            foreach (Transform item in meetingGroup)
            {
                item.position += (Vector3)groupDelta;
            }

            foreach (Transform item in facilityRoot)
            {
                if (meetingGroup.Contains(item))
                {
                    continue;
                }

                SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
                if (renderer != null)
                {
                    SnapBottomCenter(renderer);
                }
            }

            foreach (Transform item in wallDecorRoot)
            {
                SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                Vector2 delta = new Vector2(
                    Snap(renderer.bounds.center.x) - renderer.bounds.center.x,
                    Snap(renderer.bounds.center.y) - renderer.bounds.center.y);
                item.position += (Vector3)delta;
            }
        }

        private static void ApplyLayoutAdjustments(Transform facilityRoot)
        {
            MoveBottomCenter(RequireChild(facilityRoot, "TrashCan").GetComponent<SpriteRenderer>(),
                new Vector2(-3.5f, 2f));
            MoveBottomCenter(RequireChild(facilityRoot, "CoatRack").GetComponent<SpriteRenderer>(),
                new Vector2(-7.5f, -6.25f));
        }

        private static void ConfigureDoorAndSpawn()
        {
            GameObject spawn = RequireObject("ClubEntrySpawn");
            GameObject player = RequireObject("PlayerRpg");
            GameObject exit = RequireObject("ClubExitDoor");
            GameObject collisionRoot = RequireObject("Collision");

            Vector3 safeSpawn = new Vector3(-5.5f, -3f, 0f);
            spawn.transform.position = safeSpawn;
            player.transform.position = new Vector3(safeSpawn.x, safeSpawn.y, player.transform.position.z);
            exit.transform.position = new Vector3(-5.5f, -4.1f, 0f);
            MapInteractionTarget exitTarget = exit.GetComponent<MapInteractionTarget>();
            Require(exitTarget != null, "Club exit target is missing.");
            exitTarget.interactionRange = 0.75f;
            exitTarget.requireFacing = true;

            Transform existingDoorCollision = collisionRoot.transform.Find("DoorCollision");
            GameObject doorCollision = existingDoorCollision == null
                ? new GameObject("DoorCollision")
                : existingDoorCollision.gameObject;
            doorCollision.transform.SetParent(collisionRoot.transform, true);
            doorCollision.transform.position = new Vector3(-5.5f, -4.65f, 0f);
            BoxCollider2D collider = doorCollision.GetComponent<BoxCollider2D>();
            if (collider == null)
            {
                collider = doorCollision.AddComponent<BoxCollider2D>();
            }

            collider.isTrigger = false;
            collider.size = new Vector2(2f, 0.35f);
            collider.offset = Vector2.zero;
            EditorUtility.SetDirty(spawn.transform);
            EditorUtility.SetDirty(player.transform);
            EditorUtility.SetDirty(exit.transform);
            EditorUtility.SetDirty(exitTarget);
            EditorUtility.SetDirty(collider);
        }

        private static void ConfigureMeetingEdgeInteractions()
        {
            MapInteractionTarget[] existingTargets = Object.FindObjectsByType<MapInteractionTarget>(FindObjectsInactive.Include)
                .Where(target => (target.interactionPoint != null
                        && target.interactionPoint.actionId == MeetingActionId)
                    || target.name.StartsWith("MeetingTableInteraction", StringComparison.Ordinal))
                .ToArray();
            ActivityData activity = existingTargets.Select(target => target.fallbackActivity)
                .FirstOrDefault(item => item != null);
            Require(activity != null, "Club meeting activity reference is missing.");
            Transform interactionRoot = RequireObject("InteractableObjects").transform;
            foreach (MapInteractionTarget target in existingTargets)
            {
                Object.DestroyImmediate(target.gameObject);
            }

            SpriteRenderer table = RequireObject("MeetingTable").GetComponent<SpriteRenderer>();
            Require(table != null, "Meeting table renderer is missing.");
            Bounds bounds = table.bounds;
            const float edgeOffset = 0.45f;
            CreateMeetingAnchor("MeetingTableInteraction_Top",
                new Vector2(bounds.center.x, bounds.max.y + edgeOffset), interactionRoot, activity);
            CreateMeetingAnchor("MeetingTableInteraction_Bottom",
                new Vector2(bounds.center.x, bounds.min.y - edgeOffset), interactionRoot, activity);
            CreateMeetingAnchor("MeetingTableInteraction_Left",
                new Vector2(bounds.min.x - edgeOffset, bounds.center.y), interactionRoot, activity);
            CreateMeetingAnchor("MeetingTableInteraction_Right",
                new Vector2(bounds.max.x + edgeOffset, bounds.center.y), interactionRoot, activity);
        }

        private static void CreateMeetingAnchor(
            string name,
            Vector2 position,
            Transform parent,
            ActivityData activity)
        {
            GameObject anchor = new GameObject(name);
            anchor.transform.SetParent(parent, false);
            anchor.transform.position = position;
            MapInteractionPoint point = anchor.AddComponent<MapInteractionPoint>();
            point.locationId = "location_club";
            point.actionId = MeetingActionId;
            point.interactionType = MapInteractionType.LocationAction;
            point.displayName = "社团讨论";
            point.description = "按 E 参加社团讨论";
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = anchor.AddComponent<MapInteractionTarget>();
            target.interactionId = MeetingActionId + "_" + name.Substring(name.LastIndexOf('_') + 1).ToLowerInvariant();
            target.displayName = "社团讨论";
            target.promptText = "按 E 参加社团讨论";
            target.fallbackActivity = activity;
            target.interactionPoint = point;
            target.consumeTimeOnInteraction = true;
            target.requireFacing = true;
            target.interactionRange = 0.85f;
            target.priority = 30;
        }

        private static void RefreshSorting(Transform facilityRoot)
        {
            foreach (MapObjectYSorter sorter in facilityRoot.GetComponentsInChildren<MapObjectYSorter>(true))
            {
                SpriteRenderer renderer = sorter.GetComponent<SpriteRenderer>();
                if (renderer == null)
                {
                    continue;
                }

                float sortOffset = -renderer.bounds.extents.y + 0.05f;
                sorter.renderers = new[] { renderer };
                sorter.sortYOffset = sortOffset;
                renderer.sortingOrder = MapObjectYSorter.CalculateSortingOrder(
                    sorter.transform.position.y + sortOffset,
                    sorter.baseOrder,
                    sorter.orderScale);
                EditorUtility.SetDirty(sorter);
                EditorUtility.SetDirty(renderer);
            }
        }

        private static void RefreshSceneControllerTargets()
        {
            FullscreenMapSceneController controller = Object.FindAnyObjectByType<FullscreenMapSceneController>();
            Require(controller != null, "Club scene controller is missing.");
            controller.interactionTargets = Object.FindObjectsByType<MapInteractionTarget>(FindObjectsInactive.Include)
                .OrderByDescending(target => target.priority)
                .ThenBy(target => target.name, StringComparer.Ordinal)
                .ToArray();
            controller.autoCollectTargets = true;
            EditorUtility.SetDirty(controller);
        }

        private static void ValidateContactGrid(Transform facilityRoot)
        {
            foreach (Transform item in facilityRoot)
            {
                SpriteRenderer renderer = item.GetComponent<SpriteRenderer>();
                if (renderer == null || item.name.StartsWith("MeetingChair_", StringComparison.Ordinal))
                {
                    continue;
                }

                Require(IsOnGrid(renderer.bounds.center.x), item.name + " center X is off the 16px grid.");
                Require(IsOnGrid(renderer.bounds.min.y), item.name + " contact Y is off the 16px grid.");
            }

            SpriteRenderer table = RequireChild(facilityRoot, "MeetingTable").GetComponent<SpriteRenderer>();
            Require(IsOnGrid(table.bounds.center.x) && IsOnGrid(table.bounds.min.y),
                "Meeting group reference point is off the 16px grid.");
        }

        private static void SnapBottomCenter(SpriteRenderer renderer)
        {
            Vector2 delta = new Vector2(
                Snap(renderer.bounds.center.x) - renderer.bounds.center.x,
                Snap(renderer.bounds.min.y) - renderer.bounds.min.y);
            renderer.transform.position += (Vector3)delta;
        }

        private static void MoveBottomCenter(SpriteRenderer renderer, Vector2 targetBottomCenter)
        {
            Vector2 delta = new Vector2(
                targetBottomCenter.x - renderer.bounds.center.x,
                targetBottomCenter.y - renderer.bounds.min.y);
            renderer.transform.position += (Vector3)delta;
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Require(texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath))),
                "Failed to read texture: " + assetPath);
            return texture;
        }

        private static Texture2D ResizeNearest(Texture2D source, int width, int height)
        {
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32[] sourcePixels = source.GetPixels32();
            Color32[] outputPixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Min(source.height - 1,
                    Mathf.FloorToInt((y + 0.5f) * source.height / height));
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Min(source.width - 1,
                        Mathf.FloorToInt((x + 0.5f) * source.width / width));
                    outputPixels[y * width + x] = sourcePixels[sourceY * source.width + sourceX];
                }
            }

            output.SetPixels32(outputPixels);
            output.Apply();
            RemoveLightExteriorFringe(output);
            return output;
        }

        private static void RemoveLightExteriorFringe(Texture2D texture)
        {
            Color32[] source = texture.GetPixels32();
            Color32[] cleaned = (Color32[])source.Clone();
            int width = texture.width;
            int height = texture.height;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int index = y * width + x;
                    Color32 pixel = source[index];
                    if (pixel.a == 0 || !IsNearTransparency(source, width, height, x, y, 3))
                        continue;

                    int maximum = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b));
                    int minimum = Mathf.Min(pixel.r, Mathf.Min(pixel.g, pixel.b));
                    int luminance = GetLuminance(pixel);
                    if (maximum - minimum > 36 || luminance < 140)
                        continue;

                    Color32 replacement = pixel;
                    int darkestLuminance = luminance;
                    for (int offsetY = -4; offsetY <= 4; offsetY++)
                    {
                        for (int offsetX = -4; offsetX <= 4; offsetX++)
                        {
                            int neighbourX = x + offsetX;
                            int neighbourY = y + offsetY;
                            if (neighbourX < 0 || neighbourY < 0
                                || neighbourX >= width || neighbourY >= height)
                                continue;

                            Color32 neighbour = source[neighbourY * width + neighbourX];
                            if (neighbour.a == 0)
                                continue;

                            int neighbourLuminance = GetLuminance(neighbour);
                            if (neighbourLuminance >= darkestLuminance)
                                continue;

                            replacement = neighbour;
                            darkestLuminance = neighbourLuminance;
                        }
                    }

                    if (darkestLuminance >= 140)
                    {
                        replacement.r = (byte)(pixel.r * 0.45f);
                        replacement.g = (byte)(pixel.g * 0.45f);
                        replacement.b = (byte)(pixel.b * 0.45f);
                    }

                    replacement.a = pixel.a;
                    cleaned[index] = replacement;
                }
            }

            texture.SetPixels32(cleaned);
            texture.Apply();
        }

        private static bool IsNearTransparency(
            Color32[] pixels,
            int width,
            int height,
            int x,
            int y,
            int radius)
        {
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0)
                        continue;

                    int neighbourX = x + offsetX;
                    int neighbourY = y + offsetY;
                    if (neighbourX < 0 || neighbourY < 0
                        || neighbourX >= width || neighbourY >= height
                        || pixels[neighbourY * width + neighbourX].a == 0)
                        return true;
                }
            }

            return false;
        }

        private static int GetLuminance(Color32 pixel)
        {
            return (pixel.r * 54 + pixel.g * 183 + pixel.b * 19) >> 8;
        }

        private static void ConfigureFinalSprite(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Final club sprite importer is missing: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.spritePivot = new Vector2(0.5f, 0.5f);
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void ValidateImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Require(importer != null, "Final club sprite importer is missing: " + path);
            Require(Mathf.Approximately(importer.spritePixelsPerUnit, PixelsPerUnit),
                path + " must use 64 PPU.");
            Require(importer.filterMode == FilterMode.Point, path + " must use Point filtering.");
            Require(importer.wrapMode == TextureWrapMode.Clamp,
                path + " must use Clamp wrapping to prevent edge bleeding.");
            Require(!importer.mipmapEnabled, path + " must disable mipmaps.");
            Require(importer.textureCompression == TextureImporterCompression.Uncompressed,
                path + " must be uncompressed.");
        }

        private static float Snap(float value)
        {
            return Mathf.Round(value / GridStep) * GridStep;
        }

        private static bool IsOnGrid(float value)
        {
            return Mathf.Abs(value / GridStep - Mathf.Round(value / GridStep)) < 0.002f;
        }

        private static bool IsScaleOne(Vector3 scale)
        {
            return Mathf.Abs(scale.x - 1f) < 0.001f
                && Mathf.Abs(scale.y - 1f) < 0.001f
                && Mathf.Abs(scale.z - 1f) < 0.001f;
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
            {
                value = value.Replace(invalid, '_');
            }

            return value;
        }

        private static GameObject RequireObject(string name)
        {
            GameObject target = GameObject.Find(name);
            Require(target != null, name + " is missing from ClubRoomMapScene.");
            return target;
        }

        private static Transform RequireChild(Transform parent, string name)
        {
            Transform child = parent.Find(name);
            Require(child != null, name + " is missing under " + parent.name + ".");
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
