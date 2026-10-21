
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace StudentGrowth.EditorTools
{
    /// <summary>
    /// 家（HomeMapScene）加隔断：方案 2「卧室 + 书房」。
    /// 做法：Tiled SpriteRenderer 画墙（交给 MapObjectYSorter 排序）+ BoxCollider2D 挡路，
    /// 不动烘焙底图。可重复执行（先删旧的 Partitions 再重建）。
    /// 只写 Console 日志，不弹模态框，便于自动化。
    /// </summary>
    public static class HomePartitionBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity";
        private const string TileDir = "Assets/Art/Pixel/Generated/HomeReference/Tiles";
        private const string VerticalPath = TileDir + "/home_ref_partition_v.png";
        private const string HorizontalPath = TileDir + "/home_ref_partition_h.png";
        private const string RootName = "Partitions";

        [MenuItem("Tools/Student Growth/Maps/Add Home Partitions")]
        public static void Build()
        {
            EnsureSpriteImport(VerticalPath);
            EnsureSpriteImport(HorizontalPath);
            AssetDatabase.Refresh();

            Sprite v = AssetDatabase.LoadAssetAtPath<Sprite>(VerticalPath);
            Sprite h = AssetDatabase.LoadAssetAtPath<Sprite>(HorizontalPath);
            if (v == null || h == null)
            {
                Debug.LogError("[Partition] 墙片未导入为 Sprite，检查 " + TileDir);
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != ScenePath)
            {
                Debug.LogError("[Partition] 请先打开 " + ScenePath + "（当前 " + scene.path + "）");
                return;
            }

            GameObject old = GameObject.Find(RootName);
            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            Transform parent = FindGrid();
            GameObject root = new GameObject(RootName);
            root.transform.SetParent(parent, false);

            int count = 0;
            // 卧室竖墙 x=-4：上段 y 7.5→-1.5，门洞 y=-2，下段 -2.5→-4.5
            count += WallV(root.transform, v, "BedroomWall_Upper", -4f, 7.5f, -1.5f);
            count += WallV(root.transform, v, "BedroomWall_Lower", -4f, -2.5f, -4.5f);
            // 书房底墙 y=1：左段 -4.25→0.5，门洞 x=1，右段 1.5→4.25
            count += WallH(root.transform, h, "StudyWall_South_Left", 1f, -4.25f, 0.5f);
            count += WallH(root.transform, h, "StudyWall_South_Right", 1f, 1.5f, 4.25f);
            // 书房右墙 x=4：y 7.5→0.75
            count += WallV(root.transform, v, "StudyWall_East", 4f, 7.5f, 0.75f);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Partition] 已建 " + count + " 段隔断（卧室+书房）并保存场景");
        }

        [MenuItem("Tools/Student Growth/Maps/Remove Home Partitions")]
        public static void Remove()
        {
            GameObject old = GameObject.Find(RootName);
            if (old == null)
            {
                Debug.Log("[Partition] 场景里没有 " + RootName);
                return;
            }

            Object.DestroyImmediate(old);
            Scene scene = SceneManager.GetActiveScene();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Partition] 已移除隔断");
        }

        private static int WallV(Transform parent, Sprite sprite, string name, float x, float yTop, float yBottom)
        {
            float height = Mathf.Abs(yTop - yBottom);
            float centerY = (yTop + yBottom) * .5f;
            GameObject go = MakeWall(parent, sprite, name, new Vector3(x, centerY, 0f));
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(.5f, height);

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(.5f, height);
            AddSorter(go, sr);
            return 1;
        }

        private static int WallH(Transform parent, Sprite sprite, string name, float y, float xLeft, float xRight)
        {
            float width = Mathf.Abs(xRight - xLeft);
            float centerX = (xLeft + xRight) * .5f;
            GameObject go = MakeWall(parent, sprite, name, new Vector3(centerX, y, 0f));
            SpriteRenderer sr = go.GetComponent<SpriteRenderer>();
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(width, .5f);

            BoxCollider2D box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(width, .5f);
            AddSorter(go, sr);
            return 1;
        }

        private static GameObject MakeWall(Transform parent, Sprite sprite, string name, Vector3 position)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            return go;
        }

        private static void AddSorter(GameObject go, SpriteRenderer sr)
        {
            MapObjectYSorter sorter = go.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { sr };
            sorter.baseOrder = 1000;
            sorter.orderScale = 100f;
            // 略微偏向镜头一侧，避免与玩家同 y 时排序打架
            sorter.sortYOffset = -.15f;
        }

        private static void EnsureSpriteImport(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite) { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (!Mathf.Approximately(importer.spritePixelsPerUnit, 64f)) { importer.spritePixelsPerUnit = 64f; dirty = true; }
            if (importer.filterMode != FilterMode.Point) { importer.filterMode = FilterMode.Point; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed) { importer.textureCompression = TextureImporterCompression.Uncompressed; dirty = true; }
            if (importer.mipmapEnabled) { importer.mipmapEnabled = false; dirty = true; }
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect) { settings.spriteMeshType = SpriteMeshType.FullRect; importer.SetTextureSettings(settings); dirty = true; }
            if (dirty) { importer.SaveAndReimport(); }
        }

        private static Transform FindGrid()
        {
            GameObject grid = GameObject.Find("Grid");
            return grid != null ? grid.transform : null;
        }
    }
}

