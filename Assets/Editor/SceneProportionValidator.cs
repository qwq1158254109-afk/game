using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace StudentGrowth.Editor
{
    public static class SceneProportionValidator
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity";
        private const string ReportPath = "Assets/Docs/SceneProportionValidationReport.md";

        [MenuItem("Tools/Student Growth/New Art/Validate Scene Proportions")]
        public static void ValidateHomeMapSceneProportions()
        {
            List<Result> results = new List<Result>();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            ValidateNewSpriteImportSettings(results);
            ValidateSceneStructure(results);
            ValidateObjectScales(results);
            ValidateObjectSizes(results);
            ValidateSpawn(results);
            ValidateCamera(results);
            ValidateCorridorAndStandPoints(results);
            ValidateTriggerSizes(results);
            WriteReport(results);

            if (results.Any(result => !result.pass))
            {
                throw new InvalidOperationException("Scene proportion validation failed. See " + ReportPath);
            }

            Debug.Log("Scene proportion validation passed.");
        }

        private static void ValidateNewSpriteImportSettings(List<Result> results)
        {
            string[] roots = { "Assets/Art/New2_0", "Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets" };
            foreach (string root in roots)
            {
                if (!Directory.Exists(Path.GetFullPath(root)))
                {
                    continue;
                }

                foreach (string fullPath in Directory.GetFiles(Path.GetFullPath(root), "*.png", SearchOption.AllDirectories))
                {
                    string assetPath = ToAssetPath(fullPath);
                    if (assetPath.IndexOf("new", StringComparison.OrdinalIgnoreCase) < 0
                        && assetPath.IndexOf("New2_0", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                    bool pass = importer != null
                        && Mathf.Approximately(importer.spritePixelsPerUnit, 16f)
                        && importer.filterMode == FilterMode.Point
                        && importer.textureCompression == TextureImporterCompression.Uncompressed
                        && !importer.mipmapEnabled;
                    results.Add(new Result("Sprite Import", assetPath, pass, pass ? "PPU 16, Point, uncompressed, no mipmaps." : "Importer settings do not match New2_0 pixel rules."));
                }
            }
        }

        private static void ValidateSceneStructure(List<Result> results)
        {
            foreach (string name in new[]
            {
                "HomeMapSceneRoot",
                "HomeMapRpgRoot",
                "Grid",
                "Ground_Tilemap",
                "Wall_Tilemap",
                "FloorTrim_Tilemap",
                "Decoration_Tilemap",
                "FurnitureBase_Tilemap",
                "Props_Tilemap",
                "Collision_Tilemap",
                "AbovePlayer_Tilemap",
                "Interaction_Tilemap",
                "TriggerGuide_Tilemap",
                "InteractableObjects",
                "DefaultSpawnPoint",
                "CameraBounds",
                "Main Camera",
                "TopHud",
                "BottomPromptBar",
                "DebugOverlay"
            })
            {
                results.Add(new Result("Scene Structure", name, GameObject.Find(name) != null, "Required scene object."));
            }

            Grid grid = UnityEngine.Object.FindFirstObjectByType<Grid>();
            bool gridPass = grid != null && grid.cellSize == Vector3.one;
            results.Add(new Result("Grid Cell Size", "Grid", gridPass, gridPass ? "Grid cell size is 1x1." : "Grid cell size must be 1x1."));

            GameObject collision = GameObject.Find("Collision_Tilemap");
            bool collisionPass = collision != null
                && collision.GetComponent<TilemapCollider2D>() != null
                && collision.GetComponent<CompositeCollider2D>() != null
                && collision.GetComponent<Rigidbody2D>() != null
                && collision.GetComponent<Rigidbody2D>().bodyType == RigidbodyType2D.Static;
            results.Add(new Result("Collision Tilemap", "Collision_Tilemap", collisionPass, "Requires TilemapCollider2D, CompositeCollider2D, static Rigidbody2D."));
        }

        private static void ValidateObjectScales(List<Result> results)
        {
            foreach (GameObject gameObject in UnityEngine.Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
            {
                if (!IsMapObject(gameObject))
                {
                    continue;
                }

                bool pass = Approximately(gameObject.transform.localScale, Vector3.one);
                results.Add(new Result("Transform Scale", gameObject.name, pass, pass ? "Scale is 1,1,1." : "Map object scale should remain 1,1,1."));
            }
        }

        private static bool IsMapObject(GameObject gameObject)
        {
            return gameObject.name == "BedObject"
                || gameObject.name == "DeskObject"
                || gameObject.name == "DiningTableObject"
                || gameObject.name == "WardrobeObject"
                || gameObject.name == "FamilyNpcObject"
                || gameObject.name == "ExitDoorObject"
                || gameObject.name == "PlayerRpg";
        }

        private static void ValidateObjectSizes(List<Result> results)
        {
            ValidateSize(results, "BedObject", new Vector2(5f, 6f));
            ValidateSize(results, "DeskObject", new Vector2(4f, 3f));
            ValidateSize(results, "DiningTableObject", new Vector2(6f, 4f));
            ValidateSize(results, "WardrobeObject", new Vector2(5f, 6f));
            ValidateSize(results, "FamilyNpcObject", new Vector2(2f, 2f));
            ValidateSize(results, "ExitDoorObject", new Vector2(3f, 3f));
            ValidateSize(results, "PlayerRpg", new Vector2(2f, 2f));
        }

        private static void ValidateSize(List<Result> results, string objectName, Vector2 maxTiles)
        {
            GameObject gameObject = GameObject.Find(objectName);
            SpriteRenderer renderer = gameObject == null ? null : gameObject.GetComponentInChildren<SpriteRenderer>();
            if (renderer == null)
            {
                results.Add(new Result("Object Size", objectName, false, "Missing SpriteRenderer."));
                return;
            }

            Vector2 size = renderer.bounds.size;
            bool pass = size.x <= maxTiles.x + 0.05f && size.y <= maxTiles.y + 0.05f;
            results.Add(new Result("Object Size", objectName, pass, "Actual " + Format(size) + " tiles, max " + Format(maxTiles) + "."));
        }

        private static void ValidateSpawn(List<Result> results)
        {
            GameObject spawn = GameObject.Find("DefaultSpawnPoint");
            if (spawn == null)
            {
                results.Add(new Result("Spawn", "DefaultSpawnPoint", false, "Missing spawn point."));
                return;
            }

            Vector2 point = spawn.transform.position;
            Collider2D[] colliders = UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
            bool blocked = colliders.Any(collider => collider != null
                && !collider.isTrigger
                && collider.gameObject.name != "PlayerRpg"
                && collider.bounds.Contains(point));
            results.Add(new Result("Spawn", "DefaultSpawnPoint", !blocked, blocked ? "Spawn overlaps a blocking collider." : "Spawn does not overlap blocking colliders."));
        }

        private static void ValidateCamera(List<Result> results)
        {
            Camera camera = Camera.main;
            GameObject door = GameObject.Find("ExitDoorObject");
            SpriteRenderer doorRenderer = door == null ? null : door.GetComponentInChildren<SpriteRenderer>();
            bool pass = camera != null
                && camera.orthographic
                && doorRenderer != null
                && CameraContains(camera, doorRenderer.bounds);
            results.Add(new Result("Camera", "ExitDoorObject", pass, pass ? "Door is inside orthographic camera view." : "Door is clipped or camera is missing."));

            GameObject boundsObject = GameObject.Find("CameraBounds");
            BoxCollider2D bounds = boundsObject == null ? null : boundsObject.GetComponent<BoxCollider2D>();
            bool boundsPass = bounds != null && Mathf.Approximately(bounds.size.x, 30f) && Mathf.Approximately(bounds.size.y, 20f);
            results.Add(new Result("Camera Bounds", "CameraBounds", boundsPass, boundsPass ? "Bounds match 30x20 room." : "CameraBounds should match 30x20 room."));
        }

        private static bool CameraContains(Camera camera, Bounds bounds)
        {
            float halfHeight = camera.orthographicSize;
            float halfWidth = halfHeight * camera.aspect;
            Bounds cameraBounds = new Bounds(camera.transform.position, new Vector3(halfWidth * 2f, halfHeight * 2f, 100f));
            return cameraBounds.Contains(bounds.min) && cameraBounds.Contains(bounds.max);
        }

        private static void ValidateCorridorAndStandPoints(List<Result> results)
        {
            Tilemap collision = GameObject.Find("Collision_Tilemap")?.GetComponent<Tilemap>();
            if (collision == null)
            {
                results.Add(new Result("Walkability", "Collision_Tilemap", false, "Missing collision tilemap."));
                return;
            }

            bool corridorClear = true;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -4; y <= 5; y++)
                {
                    corridorClear &= collision.GetTile(new Vector3Int(x, y, 0)) == null;
                }
            }

            results.Add(new Result("Main Corridor", "x -1..1, y -4..5", corridorClear, corridorClear ? "Main corridor is at least 2 tiles wide." : "Collision blocks the main corridor."));

            Dictionary<string, Vector3Int> standCells = new Dictionary<string, Vector3Int>
            {
                { "BedObject", new Vector3Int(-11, 2, 0) },
                { "DeskObject", new Vector3Int(2, 5, 0) },
                { "DiningTableObject", new Vector3Int(8, -9, 0) },
                { "FamilyNpcObject", new Vector3Int(12, -9, 0) },
                { "ExitDoorObject", new Vector3Int(0, -8, 0) }
            };

            foreach (KeyValuePair<string, Vector3Int> entry in standCells)
            {
                bool pass = collision.GetTile(entry.Value) == null;
                results.Add(new Result("Interaction Stand Cell", entry.Key, pass, pass ? "Stand cell is walkable." : "Stand cell is blocked: " + entry.Value));
            }
        }

        private static void ValidateTriggerSizes(List<Result> results)
        {
            foreach (MapInteractionTarget target in UnityEngine.Object.FindObjectsByType<MapInteractionTarget>(FindObjectsSortMode.None))
            {
                Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);
                Collider2D trigger = colliders.FirstOrDefault(collider => collider != null && collider.isTrigger);
                bool pass = trigger != null && trigger.bounds.size.x <= 5.2f && trigger.bounds.size.y <= 3.2f;
                results.Add(new Result("Trigger Size", target.name, pass, pass ? "Trigger size is bounded." : "Trigger is missing or too large."));
            }
        }

        private static void WriteReport(List<Result> results)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(ReportPath));
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            int failed = results.Count(result => !result.pass);
            string body = "# Scene Proportion Validation Report\n\n"
                + "- Scene: `" + ScenePath + "`\n"
                + "- Result: " + (failed == 0 ? "Pass" : "Fail") + "\n"
                + "- Failed checks: " + failed + "\n\n"
                + "| Category | Target | Pass | Note |\n"
                + "| --- | --- | --- | --- |\n"
                + string.Join("\n", results.Select(result => "| " + result.category + " | `" + result.target + "` | " + (result.pass ? "Yes" : "No") + " | " + result.note.Replace("|", "/") + " |"))
                + "\n";
            File.WriteAllText(Path.GetFullPath(ReportPath), body);
            AssetDatabase.ImportAsset(ReportPath);
        }

        private static bool Approximately(Vector3 a, Vector3 b)
        {
            return Mathf.Approximately(a.x, b.x)
                && Mathf.Approximately(a.y, b.y)
                && Mathf.Approximately(a.z, b.z);
        }

        private static string Format(Vector2 value)
        {
            return value.x.ToString("0.##") + "x" + value.y.ToString("0.##");
        }

        private static string ToAssetPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            int index = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }

        private readonly struct Result
        {
            public readonly string category;
            public readonly string target;
            public readonly bool pass;
            public readonly string note;

            public Result(string category, string target, bool pass, string note)
            {
                this.category = category;
                this.target = target;
                this.pass = pass;
                this.note = note;
            }
        }
    }
}
