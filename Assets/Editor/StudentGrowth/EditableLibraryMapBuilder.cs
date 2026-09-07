using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StudentGrowth;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace StudentGrowthEditor
{
    public static class EditableLibraryMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/LibraryMapScene.unity";
        private const string ShellPath = "Assets/Art/New2_0/Library/Source/LibraryShell.png";
        private const string PropsSourcePath = "Assets/Art/New2_0/Library/Source/LibraryPropsSheet.png";
        private const string RugSourcePath = "Assets/Art/New2_0/Library/Source/LibraryRugSheet.png";
        private const string TableChairSourcePath = "Assets/Art/New2_0/Library/Source/LibraryTableChairSheet.png";
        private const string EditableArtRoot = "Assets/Art/New2_0/Library/Editable";
        private const string TransparentPropsPath = EditableArtRoot + "/LibraryPropsTransparent.png";
        private const string PropSpriteRoot = EditableArtRoot + "/Props";
        private const string RugSpriteRoot = EditableArtRoot + "/Rugs";
        private const string TableChairSpriteRoot = EditableArtRoot + "/TableChair";
        private const string TileRoot = "Assets/Tiles/New2_0/Library";
        private const string TileSetPath = TileRoot + "/LibraryEditableTileSet.asset";
        private const string PaletteRoot = "Assets/TilePalettes/StudentGrowth/New2_0";
        private const string PalettePath = PaletteRoot + "/LibraryEditablePalette.prefab";
        private const string CollisionTilePath = "Assets/Tiles/New2_0/Collision/New20_Tile_Collision_Block_00.asset";
        private const string InteractionTilePath = "Assets/Tiles/New2_0/Decoration/New20_Tile_Interaction_Marker_00.asset";
        private const string CarpetTilePath = "Assets/Tiles/New2_0/Decoration/New20_Tile_Decoration_Carpet_00.asset";
        private const int PixelsPerUnit = 64;
        private const int TilePixels = 64;
        private const int Columns = 24;
        private const int Rows = 16;

        private static readonly string[] PropNames =
        {
            "ReceptionCounter", "ReadingTable_A", "LoungeChair_A", "LoungeChair_B",
            "Chair_A", "Chair_B", "Chair_C", "Chair_D", "RoundTable", "ReadingTable_C",
            "ReadingTable_B", "ReadingTable_D", "Chair_E", "Chair_F", "Chair_G",
            "Chair_H", "Chair_I", "Chair_J", "Chair_K", "Bookshelf_A", "Bookshelf_B",
            "Bookshelf_C", "Bookshelf_D", "Bookshelf_E", "Bookshelf_F", "MobileBookshelf",
            "Plant_A", "Plant_B", "Plant_C", "Plant_D", "Lamp_A", "Lamp_B", "Lamp_C", "Lamp_D"
        };

        private static readonly string[] RugNames =
        {
            "Teal_Wide", "Red_Wide", "Teal_Large", "Red_Large", "Red_Small"
        };

        private static readonly string[] TableChairNames =
        {
            "Table", "Chair_Left", "Chair_Right"
        };

        [MenuItem("Tools/Student Growth/Library/Rebuild As Editable Map")]
        public static void Rebuild()
        {
            EnsureFolder(EditableArtRoot);
            EnsureFolder(PropSpriteRoot);
            EnsureFolder(RugSpriteRoot);
            EnsureFolder(TableChairSpriteRoot);
            EnsureFolder(TileRoot);
            EnsureFolder(PaletteRoot);
            ConfigureShellTexture();
            GenerateTransparentPropsAtlas();
            GenerateIndividualRugSprites();
            GenerateIndividualTableChairSprites();
            LibraryEditableTileSet assets = LoadOrCreateTileSet();
            BuildScene(assets);
            assets = ReloadTileSetReferences();
            CreatePalette(assets);
            LibraryLibrarianBuilder.BuildAndPlace();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Validate();
            Debug.Log("LibraryMapScene rebuilt as editable Tilemap plus independent prop objects.");
        }

        [MenuItem("Tools/Student Growth/Library/Validate Editable Map")]
        public static void Validate()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            string[] requiredLayers =
            {
                "Ground_Tilemap", "Wall_Tilemap", "FloorTrim_Tilemap", "WallTop_Tilemap",
                "Decoration_Tilemap", "FurnitureBase_Tilemap", "Props_Tilemap",
                "Collision_Tilemap", "AbovePlayer_Tilemap", "Interaction_Tilemap", "TriggerGuide_Tilemap"
            };

            foreach (string layer in requiredLayers)
            {
                Require(GameObject.Find(layer)?.GetComponent<Tilemap>() != null, layer + " is missing.");
            }

            int usedTileCount = Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include)
                .Sum(tilemap => tilemap.GetUsedTilesCount());
            Require(usedTileCount >= Columns * Rows,
                $"Editable library Tilemaps are empty or incomplete. Used tiles: {usedTileCount}.");

            GameObject collision = GameObject.Find("Collision_Tilemap");
            Require(collision.GetComponent<TilemapCollider2D>() != null, "Collision_Tilemap needs TilemapCollider2D.");
            Require(collision.GetComponent<CompositeCollider2D>() != null, "Collision_Tilemap needs CompositeCollider2D.");
            Rigidbody2D body = collision.GetComponent<Rigidbody2D>();
            Require(body != null && body.bodyType == RigidbodyType2D.Static, "Collision_Tilemap needs a static Rigidbody2D.");
            Require(GameObject.Find("LibraryCompositeBackground") == null, "Whole-image library background is still present.");
            Require(GameObject.Find("Collision") == null, "Legacy collision-box root is still present.");

            GameObject props = GameObject.Find("EditableLibraryProps");
            Require(props != null && props.transform.childCount >= 16, "Editable library props are missing.");
            GameObject rugs = GameObject.Find("EditableLibraryRugs");
            Require(rugs != null && rugs.transform.childCount == RugNames.Length,
                $"Expected {RugNames.Length} independent rug objects.");
            Require(rugs.GetComponentsInChildren<SpriteRenderer>(true).All(renderer => renderer.sprite != null),
                "One or more library rug objects have no sprite.");
            string[] separatedFurnitureNames =
            {
                "ReadingTable_BottomRight", "Chair_BottomRight_Left", "Chair_BottomRight_Right"
            };
            foreach (string objectName in separatedFurnitureNames)
            {
                GameObject furniture = GameObject.Find(objectName);
                Require(furniture != null, objectName + " is missing.");
                Require(furniture.GetComponent<SpriteRenderer>()?.sprite != null,
                    objectName + " needs its own SpriteRenderer and sprite.");
                Require(furniture.GetComponent<BoxCollider2D>() != null,
                    objectName + " needs its own BoxCollider2D.");
            }
            Require(AssetDatabase.LoadAssetAtPath<LibraryEditableTileSet>(TileSetPath) != null, "Library tile set asset is missing.");
            Require(AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath) != null, "Library tile palette is missing.");

            FullscreenMapSceneController controller = Object.FindAnyObjectByType<FullscreenMapSceneController>();
            Require(controller != null && controller.player != null && controller.cameraController != null,
                "Library gameplay references were not preserved.");
            Debug.Log($"Editable Library validation passed: {usedTileCount} painted tiles, prop objects, collision layer, palette and gameplay references are valid.");
        }

        private static void ConfigureShellTexture()
        {
            TextureImporter importer = AssetImporter.GetAtPath(ShellPath) as TextureImporter;
            Require(importer != null, "Library shell importer is missing.");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
            GenerateIndividualPropSprites();
        }

        private static void GenerateIndividualPropSprites()
        {
            List<RectInt> propRects = FindPropRects(LoadTexture(PropsSourcePath));
            Require(propRects.Count == PropNames.Length,
                $"Expected {PropNames.Length} props but detected {propRects.Count}.");
            Texture2D atlas = LoadTexture(TransparentPropsPath);
            for (int i = 0; i < propRects.Count; i++)
            {
                RectInt topRect = propRects[i];
                Texture2D crop = new Texture2D(topRect.width, topRect.height, TextureFormat.RGBA32, false);
                int sourceBottom = atlas.height - topRect.y - topRect.height;
                for (int y = 0; y < topRect.height; y++)
                {
                    for (int x = 0; x < topRect.width; x++)
                    {
                        crop.SetPixel(x, y, atlas.GetPixel(topRect.x + x, sourceBottom + y));
                    }
                }

                crop.Apply();
                File.WriteAllBytes(Path.GetFullPath(GetPropSpritePath(i)), crop.EncodeToPNG());
                Object.DestroyImmediate(crop);
            }

            Object.DestroyImmediate(atlas);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int i = 0; i < PropNames.Length; i++)
            {
                string path = GetPropSpritePath(i);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null, "Generated prop importer is missing: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static void GenerateTransparentPropsAtlas()
        {
            Texture2D source = LoadTexture(PropsSourcePath);
            Color key = source.GetPixel(0, source.height - 1);
            Texture2D output = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    float distance = ColorDistance(pixel, key);
                    float magentaDominance = Mathf.Min(pixel.r, pixel.b) - pixel.g;
                    bool magentaFringe = magentaDominance > 0.08f
                        && Mathf.Abs(pixel.r - pixel.b) < 0.45f;
                    pixel.a = distance < 0.22f || magentaFringe ? 0f : 1f;
                    output.SetPixel(x, y, pixel);
                }
            }

            output.Apply();
            File.WriteAllBytes(Path.GetFullPath(TransparentPropsPath), output.EncodeToPNG());
            Object.DestroyImmediate(source);
            Object.DestroyImmediate(output);
            AssetDatabase.ImportAsset(TransparentPropsPath, ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(TransparentPropsPath) as TextureImporter;
            Require(importer != null, "Generated props atlas importer is missing.");
            importer.textureType = TextureImporterType.Default;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static void GenerateIndividualRugSprites()
        {
            Texture2D source = LoadTexture(RugSourcePath);
            List<RectInt> rugRects = FindRugRects(source);
            Require(rugRects.Count == RugNames.Length,
                $"Expected {RugNames.Length} rugs but detected {rugRects.Count} in {RugSourcePath}.");
            Color key = source.GetPixel(0, 0);

            for (int i = 0; i < rugRects.Count; i++)
            {
                RectInt rect = rugRects[i];
                Texture2D crop = new Texture2D(rect.width, rect.height, TextureFormat.RGBA32, false);
                for (int y = 0; y < rect.height; y++)
                {
                    for (int x = 0; x < rect.width; x++)
                    {
                        Color pixel = source.GetPixel(rect.x + x, rect.y + y);
                        float greenDominance = pixel.g - Mathf.Max(pixel.r, pixel.b);
                        bool greenScreen = ColorDistance(pixel, key) < 0.35f
                            || (pixel.g > 0.15f && greenDominance > 0.12f);
                        pixel = greenScreen ? Color.clear : new Color(pixel.r, pixel.g, pixel.b, 1f);
                        crop.SetPixel(x, y, pixel);
                    }
                }

                crop.Apply();
                string path = GetRugSpritePath(i);
                File.WriteAllBytes(Path.GetFullPath(path), crop.EncodeToPNG());
                Object.DestroyImmediate(crop);
            }

            Object.DestroyImmediate(source);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int i = 0; i < RugNames.Length; i++)
            {
                string path = GetRugSpritePath(i);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null, "Generated rug importer is missing: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static void GenerateIndividualTableChairSprites()
        {
            List<RectInt> objectRects = FindPropRects(LoadTexture(TableChairSourcePath));
            Require(objectRects.Count == TableChairNames.Length,
                $"Expected {TableChairNames.Length} table/chair objects but detected {objectRects.Count} in {TableChairSourcePath}.");
            Texture2D source = LoadTexture(TableChairSourcePath);
            Color key = source.GetPixel(0, source.height - 1);

            for (int i = 0; i < objectRects.Count; i++)
            {
                RectInt topRect = objectRects[i];
                Texture2D crop = new Texture2D(topRect.width, topRect.height, TextureFormat.RGBA32, false);
                int sourceBottom = source.height - topRect.y - topRect.height;
                for (int y = 0; y < topRect.height; y++)
                {
                    for (int x = 0; x < topRect.width; x++)
                    {
                        Color pixel = source.GetPixel(topRect.x + x, sourceBottom + y);
                        float magentaDominance = Mathf.Min(pixel.r, pixel.b) - pixel.g;
                        bool magentaScreen = ColorDistance(pixel, key) < 0.28f
                            || (Mathf.Min(pixel.r, pixel.b) > 0.18f
                                && magentaDominance > 0.12f
                                && Mathf.Abs(pixel.r - pixel.b) < 0.45f);
                        pixel = magentaScreen ? Color.clear : new Color(pixel.r, pixel.g, pixel.b, 1f);
                        crop.SetPixel(x, y, pixel);
                    }
                }

                crop.Apply();
                string path = GetTableChairSpritePath(i);
                File.WriteAllBytes(Path.GetFullPath(path), crop.EncodeToPNG());
                Object.DestroyImmediate(crop);
            }

            Object.DestroyImmediate(source);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int i = 0; i < TableChairNames.Length; i++)
            {
                string path = GetTableChairSpritePath(i);
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Require(importer != null, "Generated table/chair importer is missing: " + path);
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = PixelsPerUnit;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
        }

        private static List<RectInt> FindRugRects(Texture2D source)
        {
            Color key = source.GetPixel(0, 0);
            bool[] solid = new bool[source.width * source.height];
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    Color pixel = source.GetPixel(x, y);
                    float greenDominance = pixel.g - Mathf.Max(pixel.r, pixel.b);
                    solid[y * source.width + x] = ColorDistance(pixel, key) >= 0.35f
                        && !(pixel.g > 0.15f && greenDominance > 0.12f);
                }
            }

            bool[] visited = new bool[solid.Length];
            int[] offsetX = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] offsetY = { -1, -1, -1, 0, 0, 1, 1, 1 };
            Queue<int> queue = new Queue<int>();
            List<RectInt> rects = new List<RectInt>();
            for (int start = 0; start < solid.Length; start++)
            {
                if (!solid[start] || visited[start])
                {
                    continue;
                }

                visited[start] = true;
                queue.Enqueue(start);
                int minX = source.width;
                int minY = source.height;
                int maxX = -1;
                int maxY = -1;
                int pixelCount = 0;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int x = index % source.width;
                    int y = index / source.width;
                    pixelCount++;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                    for (int direction = 0; direction < offsetX.Length; direction++)
                    {
                        int nextX = x + offsetX[direction];
                        int nextY = y + offsetY[direction];
                        if (nextX < 0 || nextX >= source.width || nextY < 0 || nextY >= source.height)
                        {
                            continue;
                        }

                        int nextIndex = nextY * source.width + nextX;
                        if (solid[nextIndex] && !visited[nextIndex])
                        {
                            visited[nextIndex] = true;
                            queue.Enqueue(nextIndex);
                        }
                    }
                }

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                if (pixelCount < 1000 || width < 32 || height < 32)
                {
                    continue;
                }

                const int padding = 2;
                int paddedMinX = Mathf.Max(0, minX - padding);
                int paddedMinY = Mathf.Max(0, minY - padding);
                int paddedMaxX = Mathf.Min(source.width - 1, maxX + padding);
                int paddedMaxY = Mathf.Min(source.height - 1, maxY + padding);
                rects.Add(new RectInt(
                    paddedMinX,
                    paddedMinY,
                    paddedMaxX - paddedMinX + 1,
                    paddedMaxY - paddedMinY + 1));
            }

            float topRowThreshold = source.height * 0.6f;
            IEnumerable<RectInt> topRow = rects
                .Where(rect => rect.center.y > topRowThreshold)
                .OrderBy(rect => rect.x);
            IEnumerable<RectInt> lowerRows = rects
                .Where(rect => rect.center.y <= topRowThreshold)
                .OrderBy(rect => rect.x);
            return topRow.Concat(lowerRows).ToList();
        }

        private static string GetRugSpritePath(int index)
        {
            return RugSpriteRoot + $"/Library_Rug_{index:00}_{RugNames[index]}.png";
        }

        private static string GetTableChairSpritePath(int index)
        {
            return TableChairSpriteRoot + $"/Library_TableChair_{index:00}_{TableChairNames[index]}.png";
        }

        private static LibraryEditableTileSet LoadOrCreateTileSet()
        {
            LibraryEditableTileSet existing = AssetDatabase.LoadAssetAtPath<LibraryEditableTileSet>(TileSetPath);
            if (existing != null
                && existing.ShellTiles != null && existing.ShellTiles.Length == Columns * Rows
                && existing.PropSprites != null && existing.PropSprites.Length == PropNames.Length)
            {
                LibraryEditableTileSet reloadedExisting = ReloadTileSetReferences();
                reloadedExisting.Configure(
                    reloadedExisting.ShellTexture,
                    AssetDatabase.LoadAssetAtPath<Texture2D>(TransparentPropsPath),
                    reloadedExisting.ShellSprites,
                    reloadedExisting.ShellTiles,
                    LoadExternalPropSprites(),
                    reloadedExisting.CollisionTile,
                    reloadedExisting.InteractionTile,
                    reloadedExisting.RedRugTile,
                    reloadedExisting.TealRugTile);
                EditorUtility.SetDirty(reloadedExisting);
                AssetDatabase.SaveAssets();
                return reloadedExisting;
            }

            Require(existing == null, "LibraryEditableTileSet exists but is incomplete. Delete it before rebuilding.");
            Texture2D shell = AssetDatabase.LoadAssetAtPath<Texture2D>(ShellPath);
            Texture2D props = AssetDatabase.LoadAssetAtPath<Texture2D>(TransparentPropsPath);
            Require(shell != null && shell.width == Columns * TilePixels && shell.height == Rows * TilePixels,
                "Library shell must be 1536x1024.");
            Require(props != null, "Transparent props atlas is missing.");

            LibraryEditableTileSet tileSet = ScriptableObject.CreateInstance<LibraryEditableTileSet>();
            AssetDatabase.CreateAsset(tileSet, TileSetPath);

            Sprite[] shellSprites = new Sprite[Columns * Rows];
            TileBase[] shellTiles = new TileBase[Columns * Rows];
            for (int topRow = 0; topRow < Rows; topRow++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    int index = topRow * Columns + column;
                    Rect rect = new Rect(
                        column * TilePixels,
                        shell.height - (topRow + 1) * TilePixels,
                        TilePixels,
                        TilePixels);
                    Sprite sprite = Sprite.Create(shell, rect, new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
                    sprite.name = $"LibraryShell_{column:00}_{topRow:00}";
                    AssetDatabase.AddObjectToAsset(sprite, tileSet);
                    shellSprites[index] = sprite;

                    Tile tile = ScriptableObject.CreateInstance<Tile>();
                    tile.name = $"LibraryShellTile_{column:00}_{topRow:00}";
                    tile.sprite = sprite;
                    tile.colliderType = Tile.ColliderType.None;
                    AssetDatabase.AddObjectToAsset(tile, tileSet);
                    shellTiles[index] = tile;
                }
            }

            Sprite[] propSprites = LoadExternalPropSprites();

            TileBase collisionTile = AssetDatabase.LoadAssetAtPath<TileBase>(CollisionTilePath);
            TileBase interactionTile = AssetDatabase.LoadAssetAtPath<TileBase>(InteractionTilePath);
            TileBase carpetTile = AssetDatabase.LoadAssetAtPath<TileBase>(CarpetTilePath);
            Require(collisionTile != null && interactionTile != null && carpetTile != null,
                "Shared collision, interaction or carpet tiles are missing.");

            Tile redRug = CreateColoredTileSubAsset(tileSet, carpetTile, "Library_Rug_Red", new Color(0.72f, 0.35f, 0.28f, 0.92f));
            Tile tealRug = CreateColoredTileSubAsset(tileSet, carpetTile, "Library_Rug_Teal", new Color(0.28f, 0.58f, 0.58f, 0.92f));
            tileSet.Configure(shell, props, shellSprites, shellTiles, propSprites, collisionTile, interactionTile, redRug, tealRug);
            EditorUtility.SetDirty(tileSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TileSetPath, ImportAssetOptions.ForceSynchronousImport);
            LibraryEditableTileSet reloaded = AssetDatabase.LoadAssetAtPath<LibraryEditableTileSet>(TileSetPath);
            Require(reloaded != null, "Failed to reload the generated library tile set.");
            return reloaded;
        }

        private static Sprite[] LoadExternalPropSprites()
        {
            Sprite[] sprites = new Sprite[PropNames.Length];
            for (int i = 0; i < sprites.Length; i++)
            {
                string path = GetPropSpritePath(i);
                sprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Require(sprites[i] != null, "Generated prop sprite is missing: " + path);
            }

            return sprites;
        }

        private static string GetPropSpritePath(int index)
        {
            return PropSpriteRoot + $"/Library_{index:00}_{PropNames[index]}.png";
        }

        private static LibraryEditableTileSet ReloadTileSetReferences()
        {
            AssetDatabase.ImportAsset(TileSetPath, ImportAssetOptions.ForceSynchronousImport);
            LibraryEditableTileSet reloaded = AssetDatabase.LoadAssetAtPath<LibraryEditableTileSet>(TileSetPath);
            Require(
                reloaded != null
                && reloaded.ShellTiles.All(tile => tile != null)
                && reloaded.PropSprites.All(sprite => sprite != null),
                "Library tile set references did not reload after an editor asset refresh.");
            return reloaded;
        }

        private static Tile CreateColoredTileSubAsset(
            LibraryEditableTileSet parent,
            TileBase sourceTile,
            string name,
            Color color)
        {
            Tile source = sourceTile as Tile;
            Require(source != null && source.sprite != null, "Source carpet tile is invalid.");
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.name = name;
            tile.sprite = source.sprite;
            tile.color = color;
            tile.colliderType = Tile.ColliderType.None;
            AssetDatabase.AddObjectToAsset(tile, parent);
            return tile;
        }

        private static List<RectInt> FindPropRects(Texture2D source)
        {
            Color key = source.GetPixel(0, source.height - 1);
            bool[] solid = new bool[source.width * source.height];
            for (int y = 0; y < source.height; y++)
            {
                for (int x = 0; x < source.width; x++)
                {
                    solid[y * source.width + x] = ColorDistance(source.GetPixel(x, y), key) > 0.16f;
                }
            }

            bool[] visited = new bool[solid.Length];
            int[] offsetX = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] offsetY = { -1, -1, -1, 0, 0, 1, 1, 1 };
            Queue<int> queue = new Queue<int>();
            List<RectInt> rects = new List<RectInt>();
            for (int start = 0; start < solid.Length; start++)
            {
                if (!solid[start] || visited[start])
                {
                    continue;
                }

                visited[start] = true;
                queue.Enqueue(start);
                int minX = source.width;
                int minY = source.height;
                int maxX = -1;
                int maxY = -1;
                int pixelCount = 0;
                while (queue.Count > 0)
                {
                    int index = queue.Dequeue();
                    int x = index % source.width;
                    int y = index / source.width;
                    pixelCount++;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                    for (int direction = 0; direction < offsetX.Length; direction++)
                    {
                        int nextX = x + offsetX[direction];
                        int nextY = y + offsetY[direction];
                        if (nextX < 0 || nextX >= source.width || nextY < 0 || nextY >= source.height)
                        {
                            continue;
                        }

                        int nextIndex = nextY * source.width + nextX;
                        if (solid[nextIndex] && !visited[nextIndex])
                        {
                            visited[nextIndex] = true;
                            queue.Enqueue(nextIndex);
                        }
                    }
                }

                int width = maxX - minX + 1;
                int height = maxY - minY + 1;
                if (pixelCount < 80 || width < 8 || height < 8)
                {
                    continue;
                }

                int padding = 3;
                int paddedMinX = Mathf.Max(0, minX - padding);
                int paddedMinY = Mathf.Max(0, minY - padding);
                int paddedMaxX = Mathf.Min(source.width - 1, maxX + padding);
                int paddedMaxY = Mathf.Min(source.height - 1, maxY + padding);
                rects.Add(new RectInt(
                    paddedMinX,
                    source.height - 1 - paddedMaxY,
                    paddedMaxX - paddedMinX + 1,
                    paddedMaxY - paddedMinY + 1));
            }

            Object.DestroyImmediate(source);
            return rects
                .OrderBy(rect => rect.y)
                .ThenBy(rect => rect.x)
                .ToList();
        }

        private static void BuildScene(LibraryEditableTileSet assets)
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            assets = ReloadTileSetReferences();
            GameObject sceneRoot = scene.GetRootGameObjects().FirstOrDefault(root => root.name == "LibraryMapSceneRoot");
            Require(sceneRoot != null, "LibraryMapSceneRoot is missing.");
            Transform mapRoot = sceneRoot.transform.Find("LibraryMapRpgRoot");
            Require(mapRoot != null, "LibraryMapRpgRoot is missing.");

            DestroyChild(mapRoot, "Visual");
            DestroyChild(mapRoot, "Collision");
            DestroyChild(mapRoot, "Grid");
            DestroyChild(mapRoot, "EditableLibraryProps");
            DestroyChild(mapRoot, "EditableLibraryRugs");

            Grid grid = CreateGrid(mapRoot);
            Tilemap ground = CreateTilemap(grid.transform, "Ground_Tilemap", -100, true, false);
            Tilemap wall = CreateTilemap(grid.transform, "Wall_Tilemap", -95, true, false);
            Tilemap floorTrim = CreateTilemap(grid.transform, "FloorTrim_Tilemap", -90, true, false);
            Tilemap wallTop = CreateTilemap(grid.transform, "WallTop_Tilemap", -85, true, false);
            CreateTilemap(grid.transform, "Decoration_Tilemap", -70, true, false);
            CreateTilemap(grid.transform, "FurnitureBase_Tilemap", -60, true, false);
            CreateTilemap(grid.transform, "Props_Tilemap", 0, true, false);
            Tilemap collision = CreateTilemap(grid.transform, "Collision_Tilemap", 0, false, true);
            CreateTilemap(grid.transform, "AbovePlayer_Tilemap", 2000, true, false);
            Tilemap interaction = CreateTilemap(grid.transform, "Interaction_Tilemap", 2100, false, false);
            CreateTilemap(grid.transform, "TriggerGuide_Tilemap", 2100, false, false);

            PaintShell(assets, ground, wall, floorTrim, wallTop);
            Require(
                ground.GetUsedTilesCount() + wall.GetUsedTilesCount() + floorTrim.GetUsedTilesCount() + wallTop.GetUsedTilesCount() >= Columns * Rows,
                "Shell Tilemaps were not painted before saving the scene.");
            PaintStructureCollision(assets, collision);
            interaction.SetTile(new Vector3Int(0, -6, 0), assets.InteractionTile);

            GameObject propsRoot = new GameObject("EditableLibraryProps");
            propsRoot.transform.SetParent(mapRoot, false);
            BuildEditableProps(assets, propsRoot.transform);

            GameObject rugsRoot = new GameObject("EditableLibraryRugs");
            rugsRoot.transform.SetParent(mapRoot, false);
            BuildEditableRugs(rugsRoot.transform);

            foreach (Tilemap tilemap in grid.GetComponentsInChildren<Tilemap>(true))
            {
                tilemap.RefreshAllTiles();
                tilemap.CompressBounds();
                EditorUtility.SetDirty(tilemap);
            }

            FullscreenMapSceneController controller = sceneRoot.GetComponent<FullscreenMapSceneController>();
            Require(controller != null, "FullscreenMapSceneController is missing.");
            controller.displayName = "图书馆";

            EditorSceneManager.MarkSceneDirty(scene);
            Require(EditorSceneManager.SaveScene(scene), "Failed to save LibraryMapScene.");
        }

        private static Grid CreateGrid(Transform parent)
        {
            GameObject gridObject = new GameObject("Grid");
            gridObject.transform.SetParent(parent, false);
            Grid grid = gridObject.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            return grid;
        }

        private static Tilemap CreateTilemap(
            Transform parent,
            string name,
            int sortingOrder,
            bool render,
            bool collisionLayer)
        {
            GameObject tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(parent, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer renderer = tilemapObject.AddComponent<TilemapRenderer>();
            renderer.enabled = render;
            renderer.sortingOrder = sortingOrder;
            renderer.mode = TilemapRenderer.Mode.Chunk;
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

        private static void PaintShell(
            LibraryEditableTileSet assets,
            Tilemap ground,
            Tilemap wall,
            Tilemap floorTrim,
            Tilemap wallTop)
        {
            for (int topRow = 0; topRow < Rows; topRow++)
            {
                for (int column = 0; column < Columns; column++)
                {
                    Vector3Int cell = new Vector3Int(column - Columns / 2, Rows / 2 - 1 - topRow, 0);
                    TileBase tile = assets.ShellTiles[topRow * Columns + column];
                    Require(tile != null, $"Shell tile {column},{topRow} is null.");
                    Tilemap target;
                    if (topRow == 0)
                    {
                        target = wallTop;
                    }
                    else if (topRow <= 3 || column == 0 || column == Columns - 1)
                    {
                        target = wall;
                    }
                    else if (topRow >= 13)
                    {
                        target = floorTrim;
                    }
                    else
                    {
                        target = ground;
                    }

                    target.SetTile(cell, tile);
                    Require(target.HasTile(cell), $"Failed to paint shell tile {column},{topRow}.");
                }
            }
        }

        private static void PaintStructureCollision(LibraryEditableTileSet assets, Tilemap collision)
        {
            for (int x = -12; x <= 11; x++)
            {
                collision.SetTile(new Vector3Int(x, 7, 0), assets.CollisionTile);
                collision.SetTile(new Vector3Int(x, 6, 0), assets.CollisionTile);
                if (x < -2 || x > 1)
                {
                    collision.SetTile(new Vector3Int(x, -7, 0), assets.CollisionTile);
                }
            }

            for (int y = -7; y <= 7; y++)
            {
                collision.SetTile(new Vector3Int(-12, y, 0), assets.CollisionTile);
                collision.SetTile(new Vector3Int(11, y, 0), assets.CollisionTile);
            }

            collision.SetTile(new Vector3Int(-2, -6, 0), assets.CollisionTile);
            collision.SetTile(new Vector3Int(1, -6, 0), assets.CollisionTile);
        }

        private static void BuildEditableProps(LibraryEditableTileSet assets, Transform parent)
        {
            CreateProp(parent, "ReceptionCounter", assets.PropSprites[0], new Vector2(0f, 4.5f), new Vector2(4.6f, 2f));
            CreateProp(parent, "LoungeChair_Left", assets.PropSprites[2], new Vector2(-8.25f, 4.15f), new Vector2(1.3f, 1.65f));
            CreateProp(parent, "LoungeChair_Right", assets.PropSprites[3], new Vector2(-6f, 4.15f), new Vector2(1.3f, 1.65f));
            CreateProp(parent, "LoungeRoundTable", assets.PropSprites[8], new Vector2(-7.1f, 3.25f), new Vector2(1.05f, 0.85f));
            CreateProp(parent, "ReadingTable_TopRight", assets.PropSprites[1], new Vector2(6.55f, 3.9f), new Vector2(3f, 1.45f));
            CreateProp(parent, "Bookshelf_Row1_B", assets.PropSprites[20], new Vector2(-3f, 1.25f), new Vector2(2.55f, 1.7f));
            CreateProp(parent, "Bookshelf_Row1_C", assets.PropSprites[21], new Vector2(2.7f, 1.24f), new Vector2(2.55f, 1.7f));
            CreateProp(parent, "ReadingTable_BottomLeft", assets.PropSprites[10], new Vector2(-7.45f, -2.05f), new Vector2(2.8f, 1.55f));
            CreateProp(parent, "Bookshelf_Row2", assets.PropSprites[23], new Vector2(-3f, -2.4f), new Vector2(2.55f, 1.7f));
            CreateProp(parent, "MobileBookshelf", assets.PropSprites[25], new Vector2(3f, -2.15f), new Vector2(1.55f, 2.2f));
            BuildBottomRightTableChair(parent);
            CreateProp(parent, "Plant_TopLeft", assets.PropSprites[26], new Vector2(-10.25f, 4.95f), new Vector2(0.65f, 0.85f), new Vector2(0f, -0.35f));
            CreateProp(parent, "Plant_TopCenter", assets.PropSprites[27], new Vector2(-3.25f, 4.95f), new Vector2(0.65f, 0.85f), new Vector2(0f, -0.35f));
            CreateProp(parent, "Plant_TopRight", assets.PropSprites[28], new Vector2(10.25f, 4.95f), new Vector2(0.65f, 0.85f), new Vector2(0f, -0.35f));
            CreateProp(parent, "Plant_EntranceLeft", assets.PropSprites[29], new Vector2(-3.2f, -6.41f), new Vector2(0.8f, 1.3f), new Vector2(0f, -0.25f));
            CreateProp(parent, "Plant_EntranceRight", assets.PropSprites[26], new Vector2(2.41f, -6.2f), new Vector2(0.8f, 1.3f), new Vector2(0f, -0.25f));
        }

        private static void BuildBottomRightTableChair(Transform parent)
        {
            List<RectInt> objectRects = FindPropRects(LoadTexture(TableChairSourcePath));
            Require(objectRects.Count == TableChairNames.Length,
                $"Expected {TableChairNames.Length} separated table/chair objects.");

            int minX = objectRects.Min(rect => rect.xMin);
            int maxX = objectRects.Max(rect => rect.xMax);
            const float targetCombinedWidth = 3.265625f;
            float pixelToWorld = targetCombinedWidth / (maxX - minX);
            float uniformScale = pixelToWorld * PixelsPerUnit;

            string[] objectNames =
            {
                "ReadingTable_BottomRight", "Chair_BottomRight_Left", "Chair_BottomRight_Right"
            };
            Vector2[] positions =
            {
                new Vector2(6.8f, -1.55710089f),
                new Vector2(6.06f, -2.64f),
                new Vector2(7.34f, -2.61f)
            };
            Vector2[] colliderSizes =
            {
                new Vector2(3.05f, 0.75f), new Vector2(0.55f, 0.65f), new Vector2(0.55f, 0.65f)
            };
            Vector2[] colliderOffsets =
            {
                new Vector2(0f, -0.55f), new Vector2(0f, -0.05f), new Vector2(0f, -0.05f)
            };

            for (int i = 0; i < objectRects.Count; i++)
            {
                CreateSeparatedFurniturePiece(
                    parent,
                    objectNames[i],
                    i,
                    positions[i],
                    uniformScale,
                    colliderSizes[i],
                    colliderOffsets[i]);
            }
        }

        private static GameObject CreateSeparatedFurniturePiece(
            Transform parent,
            string name,
            int spriteIndex,
            Vector2 position,
            float uniformScale,
            Vector2 worldColliderSize,
            Vector2 worldColliderOffset)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetTableChairSpritePath(spriteIndex));
            Require(sprite != null, "Generated table/chair sprite is missing: " + GetTableChairSpritePath(spriteIndex));
            GameObject piece = new GameObject(name);
            piece.transform.SetParent(parent, false);
            piece.transform.localPosition = new Vector3(position.x, position.y, -0.05f);
            piece.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            SpriteRenderer renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = MapObjectYSorter.CalculateSortingOrder(position.y, 1000, 100f);
            BoxCollider2D collider = piece.AddComponent<BoxCollider2D>();
            collider.size = worldColliderSize / uniformScale;
            collider.offset = worldColliderOffset / uniformScale;
            MapObjectYSorter sorter = piece.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };
            sorter.useRendererCenterAsSortY = true;
            return piece;
        }

        private static void BuildEditableRugs(Transform parent)
        {
            CreateRug(parent, "Rug_Teal_TopLeft", 0, new Vector2(-7.5f, 3.5f), 5f);
            CreateRug(parent, "Rug_Red_TopRight", 1, new Vector2(6.64f, 3.43f), 4f);
            CreateRug(parent, "Rug_Teal_BottomLeft", 2, new Vector2(-7.5f, -2f), 5f);
            CreateRug(parent, "Rug_Red_BottomRight", 3, new Vector2(6.84f, -2.06f), 4f);
            CreateRug(parent, "Rug_Red_Entrance", 4, new Vector2(-0.32f, -5.98f), 2f);
        }

        private static GameObject CreateRug(
            Transform parent,
            string name,
            int spriteIndex,
            Vector2 position,
            float targetWidth)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GetRugSpritePath(spriteIndex));
            Require(sprite != null, "Generated rug sprite is missing: " + GetRugSpritePath(spriteIndex));
            GameObject rug = new GameObject(name);
            rug.transform.SetParent(parent, false);
            rug.transform.localPosition = new Vector3(position.x, position.y, 0f);
            float uniformScale = targetWidth / sprite.bounds.size.x;
            rug.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
            SpriteRenderer renderer = rug.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = -70;
            return rug;
        }

        private static GameObject CreateProp(
            Transform parent,
            string name,
            Sprite sprite,
            Vector2 position,
            Vector2 colliderSize,
            Vector2 colliderOffset = default)
        {
            GameObject prop = new GameObject(name);
            prop.transform.SetParent(parent, false);
            prop.transform.localPosition = new Vector3(position.x, position.y, -0.05f);
            SpriteRenderer renderer = prop.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = MapObjectYSorter.CalculateSortingOrder(position.y, 1000, 100f);
            BoxCollider2D collider = prop.AddComponent<BoxCollider2D>();
            collider.size = colliderSize;
            collider.offset = colliderOffset;
            MapObjectYSorter sorter = prop.AddComponent<MapObjectYSorter>();
            sorter.renderers = new[] { renderer };
            sorter.useRendererCenterAsSortY = true;
            return prop;
        }

        private static void CreatePalette(LibraryEditableTileSet assets)
        {
            GameObject root = new GameObject("LibraryEditablePalette");
            Grid grid = root.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            GameObject tilemapObject = new GameObject("Palette_Tilemap");
            tilemapObject.transform.SetParent(root.transform, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();

            for (int column = 0; column < Columns; column++)
            {
                tilemap.SetTile(new Vector3Int(column, 0, 0), assets.ShellTiles[column]);
                tilemap.SetTile(new Vector3Int(column, -1, 0), assets.ShellTiles[6 * Columns + column]);
                tilemap.SetTile(new Vector3Int(column, -2, 0), assets.ShellTiles[12 * Columns + column]);
            }

            tilemap.SetTile(new Vector3Int(0, -4, 0), assets.RedRugTile);
            tilemap.SetTile(new Vector3Int(1, -4, 0), assets.TealRugTile);
            tilemap.SetTile(new Vector3Int(2, -4, 0), assets.InteractionTile);
            tilemap.SetTile(new Vector3Int(3, -4, 0), assets.CollisionTile);
            PrefabUtility.SaveAsPrefabAsset(root, PalettePath);
            Object.DestroyImmediate(root);
        }

        private static Texture2D LoadTexture(string assetPath)
        {
            Require(File.Exists(Path.GetFullPath(assetPath)), "Missing texture: " + assetPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Require(texture.LoadImage(File.ReadAllBytes(Path.GetFullPath(assetPath))), "Failed to load texture: " + assetPath);
            return texture;
        }

        private static float ColorDistance(Color a, Color b)
        {
            float red = a.r - b.r;
            float green = a.g - b.g;
            float blue = a.b - b.b;
            return Mathf.Sqrt(red * red + green * green + blue * blue);
        }

        private static void DestroyChild(Transform parent, string childName)
        {
            Transform child = parent.Find(childName);
            if (child != null)
            {
                Object.DestroyImmediate(child.gameObject);
            }
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
    }
}
