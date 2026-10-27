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
    public static class NewArtShopMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity";
        private const string BackupRoot = "Assets/Scenes/StudentGrowth/Maps/Backups";

        private const string SourceRoot = "Assets/Art/Pixel/Backgrounds/NewShopSources";
        private const string SourceItems = SourceRoot + "/new_shop_items.png";
        private const string SourceEmptyRoom = SourceRoot + "/new_shop_empty_room.png";
        private const string SourceReference = SourceRoot + "/new_shop_reference.png";
        private const string SourceLightMask = SourceRoot + "/new_shop_floor_light_mask.png";

        private const string ArtRoot = "Assets/Art/New2_0";
        private const string ShopPropRoot = ArtRoot + "/Props/Shop";
        private const string ShopTileSpriteRoot = ArtRoot + "/Tilesets/Shop";
        private const string TileRoot = "Assets/Tiles/New2_0/Shop";
        private const string DataRoot = "Assets/Data/StudentGrowth/New2_0/ShopMap";
        private const string DocsRoot = "Assets/Docs/StudentGrowth/New2_0";

        private const string PlayerSpritePath = "Assets/Art/New2_0/Characters/Home/New/PinkPlayer/Frames/pink_player_down_1.png";
        private const string PlayerAnimatorPath = "Assets/Animations/StudentGrowth/New2_0/HomeMap/New20_HomePlayer.controller";
        private const string ShopOwnerSpritePath = "Assets/Art/New2_0/Characters/ShopOwner/Yellow/Frames/yellow_shop_owner_down_1.png";
        private const string ShopOwnerAnimatorPath = "Assets/Animations/StudentGrowth/New2_0/ShopMap/ShopOwner.controller";

        private const float Ppu = 16f;
        private static readonly Color BackgroundColorFallback = new Color32(149, 166, 196, 255);

        [MenuItem("Tools/Student Growth/New Art/Rebuild ShopMapScene With New2_0")]
        public static void RebuildShopMapSceneWithNewArt()
        {
            EnsureFolders();
            ConfigureSourceImporters();
            ShopAssets assets = GenerateAssets();
            BuildShopScene(assets);
            WriteReport(assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("ShopMapScene rebuilt as editable New2_0 Tilemap scene.");
        }

        [MenuItem("Tools/Student Growth/New Art/Verify ShopMapScene New2_0")]
        public static void VerifyShopMapSceneNew20()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(GameObject.Find("ShopMapSceneRoot") != null, "ShopMapSceneRoot is missing.");
            Require(GameObject.Find("ShopMapRpgRoot") != null, "ShopMapRpgRoot is missing.");

            foreach (string layer in RequiredTilemapLayers())
            {
                Require(GameObject.Find(layer) != null, layer + " is missing.");
            }

            Grid grid = UnityEngine.Object.FindAnyObjectByType<Grid>();
            Require(grid != null && grid.cellSize == Vector3.one, "Grid cell size must be 1 x 1.");

            GameObject collision = GameObject.Find("Collision_Tilemap");
            Require(collision != null, "Collision_Tilemap is missing.");
            Require(collision.GetComponent<TilemapCollider2D>() != null, "Collision_Tilemap needs TilemapCollider2D.");
            Require(collision.GetComponent<CompositeCollider2D>() != null, "Collision_Tilemap needs CompositeCollider2D.");
            Rigidbody2D collisionBody = collision.GetComponent<Rigidbody2D>();
            Require(collisionBody != null && collisionBody.bodyType == RigidbodyType2D.Static, "Collision_Tilemap needs static Rigidbody2D.");

            Require(GameObject.Find("DefaultSpawnPoint") != null, "DefaultSpawnPoint is missing.");
            Require(GameObject.Find("Main Camera") != null, "Main Camera is missing.");
            Require(GameObject.Find("CameraBounds") != null, "CameraBounds is missing.");
            Require(GameObject.Find("PlayerRpg") != null, "PlayerRpg is missing.");

            foreach (string objectName in new[] { "ShopOwnerObject", "ExitDoorObject" })
            {
                GameObject target = GameObject.Find(objectName);
                Require(target != null, objectName + " is missing.");
                Require(target.GetComponent<MapInteractionPoint>() != null, objectName + " needs MapInteractionPoint.");
                Require(target.GetComponent<MapInteractionTarget>() != null, objectName + " needs MapInteractionTarget.");
            }

            GameObject shopCounter = GameObject.Find("ShopCounterObject");
            Require(shopCounter != null, "ShopCounterObject is missing.");
            Require(shopCounter.GetComponent<MapInteractionPoint>() == null, "ShopCounterObject must not have MapInteractionPoint.");
            Require(shopCounter.GetComponent<MapInteractionTarget>() == null, "ShopCounterObject must not have MapInteractionTarget.");
            Require(shopCounter.transform.Find("Visual") != null, "ShopCounterObject needs Visual child.");
            Require(shopCounter.transform.Find("BlockingCollider") != null, "ShopCounterObject needs BlockingCollider child.");

            MapInteractionTarget exit = GameObject.Find("ExitDoorObject").GetComponent<MapInteractionTarget>();
            Require(exit.isExitPortal && !exit.consumeTimeOnInteraction, "ExitDoorObject must be a no-time-cost exit.");

            foreach (string propName in new[] { "AisleShelf_Top", "AisleShelf_Middle", "AisleShelf_Bottom", "LeftTallShelf", "RightFreezerStack", "DrinkFridgeGroup" })
            {
                GameObject prop = GameObject.Find(propName);
                Require(prop != null, propName + " is missing.");
                Require(prop.GetComponentInChildren<SpriteRenderer>() != null, propName + " needs SpriteRenderer.");
                Require(prop.transform.Find("BlockingCollider") != null, propName + " needs BlockingCollider child.");
            }

            Require(!SceneUsesWholeShopBackground(), "ShopMapScene must not use the whole shop PNG as a background SpriteRenderer.");
            Debug.Log("ShopMapScene New2_0 verification passed.");
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
                "Collision_Tilemap",
                "AbovePlayer_Tilemap"
            };
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
            {
                "Assets/Art",
                ArtRoot,
                ArtRoot + "/Props",
                ShopPropRoot,
                ArtRoot + "/Tilesets",
                ShopTileSpriteRoot,
                "Assets/Tiles",
                "Assets/Tiles/New2_0",
                TileRoot,
                "Assets/Data",
                "Assets/Data/StudentGrowth",
                "Assets/Data/StudentGrowth/New2_0",
                DataRoot,
                "Assets/Scenes",
                "Assets/Scenes/StudentGrowth",
                "Assets/Scenes/StudentGrowth/Maps",
                BackupRoot,
                "Assets/Docs",
                "Assets/Docs/StudentGrowth",
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
            foreach (string assetPath in new[] { SourceItems, SourceEmptyRoom, SourceReference, SourceLightMask })
            {
                ConfigureTextureImporter(assetPath, Ppu, SpriteImportMode.Single, true);
            }
        }

        private static void ConfigureTextureImporter(string assetPath, float ppu, SpriteImportMode importMode, bool readable)
        {
            if (!File.Exists(Path.GetFullPath(assetPath)))
            {
                throw new FileNotFoundException("Required shop source asset is missing.", assetPath);
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Texture importer missing for " + assetPath);
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = importMode;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.isReadable = readable;
            importer.SaveAndReimport();
        }

        private static ShopAssets GenerateAssets()
        {
            ShopAssets assets = new ShopAssets();
            Texture2D itemSheet = LoadTexture(SourceItems);
            Texture2D emptyRoom = LoadTexture(SourceEmptyRoom);

            assets.floorSprite = CreateTileSprite(emptyRoom, ShopTileSpriteRoot + "/new20_shop_floor_tile_00.png", 520, 400, 42, 42);
            assets.floorWarmSprite = CreateTileSprite(emptyRoom, ShopTileSpriteRoot + "/new20_shop_floor_warm_tile_00.png", 562, 1050, 42, 42);
            assets.wallSprite = CreateTileSprite(emptyRoom, ShopTileSpriteRoot + "/new20_shop_wall_tile_00.png", 140, 70, 42, 42);
            assets.wallTopSprite = CreateTileSprite(emptyRoom, ShopTileSpriteRoot + "/new20_shop_wall_top_tile_00.png", 140, 28, 42, 28);
            assets.wallTrimSprite = CreateTileSprite(emptyRoom, ShopTileSpriteRoot + "/new20_shop_wall_trim_tile_00.png", 140, 170, 42, 24);
            assets.collisionSprite = CreateSolidSprite(ShopTileSpriteRoot + "/new20_shop_collision_tile_00.png", new Color(1f, 1f, 1f, 0f));
            assets.markerSprite = CreateMarkerSprite(ShopTileSpriteRoot + "/new20_shop_interaction_marker_00.png");

            assets.floorTile = CreateTile(TileRoot + "/New20_Shop_Tile_Floor_00.asset", assets.floorSprite, Color.white, Tile.ColliderType.None);
            assets.floorWarmTile = CreateTile(TileRoot + "/New20_Shop_Tile_Floor_Warm_00.asset", assets.floorWarmSprite, Color.white, Tile.ColliderType.None);
            assets.wallTile = CreateTile(TileRoot + "/New20_Shop_Tile_Wall_00.asset", assets.wallSprite, Color.white, Tile.ColliderType.None);
            assets.wallTopTile = CreateTile(TileRoot + "/New20_Shop_Tile_Wall_Top_00.asset", assets.wallTopSprite, Color.white, Tile.ColliderType.None);
            assets.wallTrimTile = CreateTile(TileRoot + "/New20_Shop_Tile_Wall_Trim_00.asset", assets.wallTrimSprite, Color.white, Tile.ColliderType.None);
            assets.collisionTile = CreateTile(TileRoot + "/New20_Shop_Tile_Collision_00.asset", assets.collisionSprite, new Color(1f, 1f, 1f, 0f), Tile.ColliderType.Grid);

            Color key = itemSheet.height > 1 ? itemSheet.GetPixel(0, itemSheet.height - 1) : BackgroundColorFallback;
            assets.cafeCounterSprite = CropProp(itemSheet, key, "new20_shop_cafe_counter.png", 34, 72, 192, 148);
            assets.checkoutCounterSprite = CropProp(itemSheet, key, "new20_shop_checkout_counter.png", 288, 104, 396, 150);
            assets.wallShelfSprite = CropProp(itemSheet, key, "new20_shop_back_wall_shelf.png", 725, 45, 170, 179);
            assets.counterDisplaySprite = CropProp(itemSheet, key, "new20_shop_counter_display.png", 928, 118, 92, 108);
            assets.recycleBinsSprite = CropProp(itemSheet, key, "new20_shop_recycle_bins.png", 1008, 132, 110, 86);
            assets.drinkFridgeSprite = CropProp(itemSheet, key, "new20_shop_drink_fridges.png", 1138, 72, 350, 180);
            assets.leftTallShelfSprite = CropProp(itemSheet, key, "new20_shop_left_tall_shelf.png", 38, 262, 104, 510);
            assets.aisleShelfTopSprite = CropProp(itemSheet, key, "new20_shop_aisle_shelf_top.png", 228, 294, 458, 162);
            assets.aisleShelfMiddleSprite = CropProp(itemSheet, key, "new20_shop_aisle_shelf_middle.png", 740, 294, 390, 162);
            assets.aisleShelfBottomSprite = CropProp(itemSheet, key, "new20_shop_aisle_shelf_bottom.png", 228, 523, 424, 149);
            assets.rightFreezerSprite = CropProp(itemSheet, key, "new20_shop_right_freezer_stack.png", 1242, 300, 118, 438);
            assets.plantSprite = CropProp(itemSheet, key, "new20_shop_potted_plant.png", 70, 794, 76, 72);
            assets.saleSignSprite = CropProp(itemSheet, key, "new20_shop_sale_sign.png", 192, 784, 70, 77);
            assets.cratesSprite = CropProp(itemSheet, key, "new20_shop_green_crates.png", 312, 746, 69, 72);
            assets.basketStackSprite = CropProp(itemSheet, key, "new20_shop_basket_stack.png", 414, 760, 56, 58);
            assets.promoRackSprite = CropProp(itemSheet, key, "new20_shop_promo_rack.png", 542, 726, 83, 94);
            assets.newProductRackSprite = CropProp(itemSheet, key, "new20_shop_new_product_rack.png", 680, 748, 82, 92);
            assets.posterBlueSprite = CropProp(itemSheet, key, "new20_shop_blue_poster.png", 880, 782, 81, 80);
            assets.posterFruitSprite = CropProp(itemSheet, key, "new20_shop_fruit_poster.png", 1000, 782, 55, 50);
            assets.openSignSprite = CropProp(itemSheet, key, "new20_shop_24h_sign.png", 1103, 782, 55, 54);
            assets.clockSprite = CropProp(itemSheet, key, "new20_shop_clock.png", 1255, 812, 53, 50);
            assets.menuBoardSprite = CropProp(itemSheet, key, "new20_shop_menu_board.png", 1350, 834, 106, 74);
            assets.noticeSprite = CropProp(itemSheet, key, "new20_shop_small_notice.png", 154, 903, 52, 41);
            assets.doorSprite = CropProp(itemSheet, key, "new20_shop_glass_exit_door.png", 630, 879, 204, 81);
            assets.redStandSprite = CropProp(itemSheet, key, "new20_shop_red_stand_sign.png", 1018, 902, 29, 54);

            ConfigureGeneratedTextures();
            assets.shopActivity = CreateShopActivity();
            assets.shopAction = CreateShopAction(assets.shopActivity);
            assets.playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerSpritePath);
            assets.playerAnimator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(PlayerAnimatorPath);
            assets.shopOwnerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(ShopOwnerSpritePath);
            assets.shopOwnerAnimator = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ShopOwnerAnimatorPath);
            Require(assets.playerSprite != null, "Player sprite is missing: " + PlayerSpritePath);
            Require(assets.playerAnimator != null, "Player animator is missing: " + PlayerAnimatorPath);
            Require(assets.shopOwnerSprite != null, "Shop owner sprite is missing: " + ShopOwnerSpritePath);
            Require(assets.shopOwnerAnimator != null, "Shop owner animator is missing: " + ShopOwnerAnimatorPath);
            return assets;
        }

        private static Sprite CreateTileSprite(Texture2D source, string destinationPath, int topX, int topY, int sourceWidth, int sourceHeight)
        {
            Texture2D output = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    int sx = topX + Mathf.Clamp(Mathf.RoundToInt((x + 0.5f) / 16f * sourceWidth), 0, sourceWidth - 1);
                    int syTop = topY + Mathf.Clamp(Mathf.RoundToInt((15 - y + 0.5f) / 16f * sourceHeight), 0, sourceHeight - 1);
                    int sy = source.height - 1 - syTop;
                    output.SetPixel(x, y, source.GetPixel(sx, sy));
                }
            }

            output.Apply();
            SavePng(output, destinationPath);
            return ImportSprite(destinationPath);
        }

        private static Sprite CreateSolidSprite(string destinationPath, Color color)
        {
            Texture2D output = new Texture2D(16, 16, TextureFormat.RGBA32, false);
            Color[] pixels = Enumerable.Repeat(color, 16 * 16).ToArray();
            output.SetPixels(pixels);
            output.Apply();
            SavePng(output, destinationPath);
            return ImportSprite(destinationPath);
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
            return ImportSprite(destinationPath);
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
                    Color pixel = source.GetPixel(sx, sy);
                    float distance = ColorDistance(pixel, key);
                    float alpha = Mathf.Clamp01((distance - 0.07f) / 0.18f);
                    pixel.a = alpha;
                    raw.SetPixel(x, y, pixel);
                }
            }

            raw.Apply();
            RectInt trim = FindOpaqueBounds(raw, 0.05f);
            Texture2D trimmed = new Texture2D(trim.width, trim.height, TextureFormat.RGBA32, false);
            for (int y = 0; y < trim.height; y++)
            {
                for (int x = 0; x < trim.width; x++)
                {
                    trimmed.SetPixel(x, y, raw.GetPixel(trim.x + x, trim.y + y));
                }
            }

            trimmed.Apply();
            string destinationPath = ShopPropRoot + "/" + fileName;
            SavePng(trimmed, destinationPath);
            return ImportSprite(destinationPath);
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
                return new RectInt(0, 0, texture.width, texture.height);
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

        private static Texture2D LoadTexture(string assetPath)
        {
            byte[] bytes = File.ReadAllBytes(Path.GetFullPath(assetPath));
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!texture.LoadImage(bytes))
            {
                throw new InvalidOperationException("Failed to load texture " + assetPath);
            }

            return texture;
        }

        private static void SavePng(Texture2D texture, string assetPath)
        {
            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
        }

        private static Sprite ImportSprite(string assetPath)
        {
            ConfigureTextureImporter(assetPath, Ppu, SpriteImportMode.Single, false);
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            Require(sprite != null, "Failed to import sprite " + assetPath);
            return sprite;
        }

        private static void ConfigureGeneratedTextures()
        {
            foreach (string fullPath in Directory.GetFiles(Path.GetFullPath(ShopPropRoot), "*.png", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(Path.GetFullPath(ShopTileSpriteRoot), "*.png", SearchOption.TopDirectoryOnly)))
            {
                ConfigureTextureImporter(ToAssetPath(fullPath), Ppu, SpriteImportMode.Single, false);
            }
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

        private static ActivityData CreateShopActivity()
        {
            string path = DataRoot + "/New20_ShopBrowseGoods.asset";
            ShopMapActivityData activity = AssetDatabase.LoadAssetAtPath<ShopMapActivityData>(path);
            if (activity == null)
            {
                activity = ScriptableObject.CreateInstance<ShopMapActivityData>();
                AssetDatabase.CreateAsset(activity, path);
            }

            activity.activityId = "activity_shop_browse_goods";
            activity.displayName = "浏览商店";
            activity.description = "查看货架与补给。购买系统后续接入。";
            activity.activityType = ActivityType.Shop;
            activity.timeCost = 0;
            EditorUtility.SetDirty(activity);
            return activity;
        }

        private static LocationActionData CreateShopAction(ActivityData activity)
        {
            string path = DataRoot + "/New20_ShopBrowseGoodsAction.asset";
            ShopMapLocationActionData action = AssetDatabase.LoadAssetAtPath<ShopMapLocationActionData>(path);
            if (action == null)
            {
                action = ScriptableObject.CreateInstance<ShopMapLocationActionData>();
                AssetDatabase.CreateAsset(action, path);
            }

            action.actionId = "action_shop_browse_goods";
            action.displayName = "浏览商店";
            action.description = "查看商品与补给。";
            action.actionType = LocationActionType.OpenShop;
            action.timeCost = 0;
            action.energyCost = 0;
            action.moneyCost = 0;
            action.linkedActivity = activity;
            action.linkedShopId = "school_shop";
            action.showRewardPreview = false;
            EditorUtility.SetDirty(action);
            return action;
        }

        private static void BuildShopScene(ShopAssets assets)
        {
            string backupPath = BackupSceneIfPresent();
            Scene scene = File.Exists(Path.GetFullPath(ScenePath))
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            ClearScene(scene);

            GameObject sceneRoot = new GameObject("ShopMapSceneRoot");
            SceneManager.MoveGameObjectToScene(sceneRoot, scene);

            MapInteractionController mapController = sceneRoot.AddComponent<MapInteractionController>();
            FullscreenMapSceneController sceneController = sceneRoot.AddComponent<FullscreenMapSceneController>();
            MapDebugOverlayView debugOverlay = sceneRoot.AddComponent<MapDebugOverlayView>();

            GameObject mapRoot = new GameObject("ShopMapRpgRoot");
            mapRoot.transform.SetParent(sceneRoot.transform, false);
            MapInteractionFlowController flow = mapRoot.AddComponent<MapInteractionFlowController>();

            Grid grid = CreateGrid(mapRoot.transform);
            Tilemap ground = CreateTilemap(grid.transform, "Ground_Tilemap", 0, true, false);
            Tilemap wall = CreateTilemap(grid.transform, "Wall_Tilemap", 5, true, false);
            Tilemap floorTrim = CreateTilemap(grid.transform, "FloorTrim_Tilemap", 6, true, false);
            Tilemap wallTop = CreateTilemap(grid.transform, "WallTop_Tilemap", 8, true, false);
            Tilemap decoration = CreateTilemap(grid.transform, "Decoration_Tilemap", 10, true, false);
            Tilemap furnitureBase = CreateTilemap(grid.transform, "FurnitureBase_Tilemap", 15, true, false);
            Tilemap collision = CreateTilemap(grid.transform, "Collision_Tilemap", 0, false, true);
            Tilemap abovePlayer = CreateTilemap(grid.transform, "AbovePlayer_Tilemap", 40, true, false);

            BuildTilemapLayout(assets, ground, wall, floorTrim, wallTop, decoration, furnitureBase, collision, abovePlayer);

            GameObject interactableRoot = new GameObject("InteractableObjects");
            interactableRoot.transform.SetParent(mapRoot.transform, false);
            GameObject propRoot = new GameObject("EditableShopProps");
            propRoot.transform.SetParent(mapRoot.transform, false);
            GameObject wallDecorRoot = new GameObject("EditableWallDecor");
            wallDecorRoot.transform.SetParent(mapRoot.transform, false);
            GameObject spawnRoot = new GameObject("PlayerSpawnPoints");
            spawnRoot.transform.SetParent(mapRoot.transform, false);
            GameObject cameraBoundsRoot = new GameObject("CameraBounds");
            cameraBoundsRoot.transform.SetParent(mapRoot.transform, false);
            GameObject mapUi = new GameObject("MapUI");
            mapUi.transform.SetParent(mapRoot.transform, false);
            new GameObject("TopHud").transform.SetParent(mapUi.transform, false);
            new GameObject("BottomPromptBar").transform.SetParent(mapUi.transform, false);
            GameObject debugRoot = new GameObject("DebugOverlay");
            debugRoot.transform.SetParent(mapRoot.transform, false);

            BuildWallDecor(assets, wallDecorRoot.transform);
            BuildEditableProps(assets, propRoot.transform);
            BuildInteractables(assets, interactableRoot.transform);

            GameObject spawn = new GameObject("DefaultSpawnPoint");
            spawn.transform.SetParent(spawnRoot.transform, false);
            spawn.transform.localPosition = new Vector3(0f, -6.65f, 0f);
            spawn.AddComponent<PlayerSpawnPoint>().spawnPointId = "shop_entry";

            BoxCollider2D boundsCollider = cameraBoundsRoot.AddComponent<BoxCollider2D>();
            boundsCollider.isTrigger = true;
            boundsCollider.size = new Vector2(24f, 16f);

            GameObject player = CreatePlayer(assets, flow);
            GameObject cameraObject = CreateCamera(player.transform, cameraBoundsRoot.transform);

            sceneController.mapId = "location_shop";
            sceneController.displayName = "商店｜补给、卡组资源与道具购买。";
            sceneController.defaultSpawnPointId = "shop_entry";
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
            debugRoot.AddComponent<MapDebugOverlayView>().debugObjects = debugOverlay.debugObjects;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            assets.sceneBackupPath = backupPath;
        }

        private static void BuildTilemapLayout(
            ShopAssets assets,
            Tilemap ground,
            Tilemap wall,
            Tilemap floorTrim,
            Tilemap wallTop,
            Tilemap decoration,
            Tilemap furnitureBase,
            Tilemap collision,
            Tilemap abovePlayer)
        {
            for (int x = -12; x <= 11; x++)
            {
                for (int y = -8; y <= 7; y++)
                {
                    TileBase tile = y <= -5 || y >= 5 ? assets.floorWarmTile : assets.floorTile;
                    ground.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }

            for (int x = -12; x <= 11; x++)
            {
                wallTop.SetTile(new Vector3Int(x, 7, 0), assets.wallTopTile);
                wall.SetTile(new Vector3Int(x, 6, 0), assets.wallTile);
                floorTrim.SetTile(new Vector3Int(x, 5, 0), assets.wallTrimTile);
                abovePlayer.SetTile(new Vector3Int(x, 7, 0), assets.wallTopTile);

                if (x < -2 || x > 1)
                {
                    floorTrim.SetTile(new Vector3Int(x, -8, 0), assets.wallTopTile);
                    collision.SetTile(new Vector3Int(x, -8, 0), assets.collisionTile);
                }

                collision.SetTile(new Vector3Int(x, 7, 0), assets.collisionTile);
                collision.SetTile(new Vector3Int(x, 6, 0), assets.collisionTile);
            }

            for (int y = -8; y <= 7; y++)
            {
                wall.SetTile(new Vector3Int(-12, y, 0), assets.wallTile);
                wall.SetTile(new Vector3Int(11, y, 0), assets.wallTile);
                wallTop.SetTile(new Vector3Int(-12, y, 0), assets.wallTopTile);
                wallTop.SetTile(new Vector3Int(11, y, 0), assets.wallTopTile);
                collision.SetTile(new Vector3Int(-12, y, 0), assets.collisionTile);
                collision.SetTile(new Vector3Int(11, y, 0), assets.collisionTile);
            }

            foreach (int x in new[] { -9, -2, 5 })
            {
                for (int y = 5; y <= 7; y++)
                {
                    wall.SetTile(new Vector3Int(x, y, 0), assets.wallTrimTile);
                    abovePlayer.SetTile(new Vector3Int(x, y, 0), assets.wallTrimTile);
                }
            }

            for (int x = -4; x <= 3; x++)
            {
                furnitureBase.SetTile(new Vector3Int(x, 4, 0), assets.wallTrimTile);
            }

            foreach (Vector3Int cell in new[]
            {
                new Vector3Int(-10, -6, 0),
                new Vector3Int(-9, -6, 0),
                new Vector3Int(7, -6, 0),
                new Vector3Int(8, -6, 0),
                new Vector3Int(-5, -7, 0),
                new Vector3Int(4, -7, 0)
            })
            {
                decoration.SetTile(cell, assets.floorWarmTile);
            }
        }

        private static void BuildWallDecor(ShopAssets assets, Transform parent)
        {
            CreateStaticSprite("CafeMenuBoard", assets.menuBoardSprite, parent, new Vector3(-8.2f, 6.15f, -0.1f), new Vector3(0.31f, 0.31f, 1f), 1100);
            CreateStaticSprite("WallClock", assets.clockSprite, parent, new Vector3(-7.5f, 5.72f, -0.1f), new Vector3(0.34f, 0.34f, 1f), 1100);
            CreateStaticSprite("BackWallShelf", assets.wallShelfSprite, parent, new Vector3(-1.15f, 5.45f, -0.1f), new Vector3(0.28f, 0.28f, 1f), 1110);
            CreateStaticSprite("MenuBoard", assets.menuBoardSprite, parent, new Vector3(1.15f, 6.05f, -0.1f), new Vector3(0.28f, 0.28f, 1f), 1110);
            CreateStaticSprite("BluePromoPoster_Wall", assets.posterBlueSprite, parent, new Vector3(5.05f, 6.08f, -0.1f), new Vector3(0.31f, 0.31f, 1f), 1110);
            CreateStaticSprite("FruitPoster_Wall", assets.posterFruitSprite, parent, new Vector3(6.55f, 6.03f, -0.1f), new Vector3(0.34f, 0.34f, 1f), 1110);
            CreateStaticSprite("Open24hSign_Wall", assets.openSignSprite, parent, new Vector3(7.65f, 6.02f, -0.1f), new Vector3(0.38f, 0.38f, 1f), 1110);
            CreateStaticSprite("SmallNotice_Wall", assets.noticeSprite, parent, new Vector3(9.65f, 2.9f, -0.1f), new Vector3(0.36f, 0.36f, 1f), 1110);
        }

        private static void BuildEditableProps(ShopAssets assets, Transform parent)
        {
            CreateBlockingSprite("CafeCounterObject", assets.cafeCounterSprite, parent, new Vector3(-8.7f, 5.0f, -0.1f), new Vector3(0.31f, 0.31f, 1f), new Vector2(2.9f, 1.1f), new Vector2(0f, -0.55f));
            CreateBlockingSprite("DrinkFridgeGroup", assets.drinkFridgeSprite, parent, new Vector3(8.55f, 5.0f, -0.1f), new Vector3(0.31f, 0.31f, 1f), new Vector2(6.2f, 1.15f), new Vector2(0f, -0.9f));
            CreateBlockingSprite("LeftTallShelf", assets.leftTallShelfSprite, parent, new Vector3(-9.7f, 0.0f, -0.1f), new Vector3(0.31f, 0.31f, 1f), new Vector2(1.35f, 7.7f), new Vector2(0f, -0.25f));
            CreateBlockingSprite("AisleShelf_Top", assets.aisleShelfTopSprite, parent, new Vector3(-0.5f, 2.45f, -0.1f), new Vector3(0.34f, 0.34f, 1f), new Vector2(8.5f, 1.1f), new Vector2(0f, -0.78f));
            CreateBlockingSprite("AisleShelf_Middle", assets.aisleShelfMiddleSprite, parent, new Vector3(0.0f, -1.15f, -0.1f), new Vector3(0.39f, 0.39f, 1f), new Vector2(8.6f, 1.1f), new Vector2(0f, -0.78f));
            CreateBlockingSprite("AisleShelf_Bottom", assets.aisleShelfBottomSprite, parent, new Vector3(0.0f, -4.65f, -0.1f), new Vector3(0.38f, 0.38f, 1f), new Vector2(8.8f, 1.1f), new Vector2(0f, -0.78f));
            CreateBlockingSprite("RightFreezerStack", assets.rightFreezerSprite, parent, new Vector3(10.1f, -0.8f, -0.1f), new Vector3(0.32f, 0.32f, 1f), new Vector2(1.55f, 5.8f), new Vector2(0f, -0.45f));
            CreateBlockingSprite("RecycleBins", assets.recycleBinsSprite, parent, new Vector3(3.5f, 5.15f, -0.1f), new Vector3(0.32f, 0.32f, 1f), new Vector2(1.7f, 0.7f), new Vector2(0f, -0.35f));
            CreateBlockingSprite("PromoRack_LeftBottom", assets.promoRackSprite, parent, new Vector3(-4.7f, -6.65f, -0.1f), new Vector3(0.38f, 0.38f, 1f), new Vector2(1.4f, 0.75f), new Vector2(0f, -0.35f));
            CreateBlockingSprite("NewProductRack_RightBottom", assets.newProductRackSprite, parent, new Vector3(4.9f, -6.65f, -0.1f), new Vector3(0.38f, 0.38f, 1f), new Vector2(1.4f, 0.75f), new Vector2(0f, -0.35f));
            CreateStaticSprite("SaleSign_Left", assets.saleSignSprite, parent, new Vector3(-10.85f, -4.9f, -0.1f), new Vector3(0.36f, 0.36f, 1f), 1550);
            CreateStaticSprite("GreenCrates_Left", assets.cratesSprite, parent, new Vector3(-9.4f, -6.4f, -0.1f), new Vector3(0.36f, 0.36f, 1f), 1640);
            CreateStaticSprite("BasketStack_Left", assets.basketStackSprite, parent, new Vector3(-7.9f, -6.55f, -0.1f), new Vector3(0.36f, 0.36f, 1f), 1650);
            CreateStaticSprite("Plant_Left", assets.plantSprite, parent, new Vector3(-10.65f, -3.1f, -0.1f), new Vector3(0.42f, 0.42f, 1f), 1450);
            CreateStaticSprite("Plant_Right", assets.plantSprite, parent, new Vector3(8.0f, -6.55f, -0.1f), new Vector3(0.42f, 0.42f, 1f), 1680);
            CreateStaticSprite("BluePoster_Floor", assets.posterBlueSprite, parent, new Vector3(7.5f, -4.95f, -0.1f), new Vector3(0.34f, 0.34f, 1f), 1550);
            CreateStaticSprite("RedStand_Right", assets.redStandSprite, parent, new Vector3(10.9f, -4.05f, -0.1f), new Vector3(0.35f, 0.35f, 1f), 1510);
        }

        private static void BuildInteractables(ShopAssets assets, Transform parent)
        {
            GameObject counter = CreateBlockingSprite(
                "ShopCounterObject",
                assets.checkoutCounterSprite,
                parent,
                new Vector3(-2.05f, 4.8f, -0.1f),
                new Vector3(0.32f, 0.32f, 1f),
                new Vector2(6.3f, 1.0f),
                new Vector2(0f, -0.65f));
            CreateStaticSprite("CounterDisplay_Decor", assets.counterDisplaySprite, counter.transform, new Vector3(2.6f, 0.38f, 0f), new Vector3(0.86f, 0.86f, 1f), 1280);
            MapObjectYSorter counterSorter = counter.GetComponent<MapObjectYSorter>();
            counterSorter.renderers = counter.GetComponentsInChildren<SpriteRenderer>(true)
                .Where(renderer => renderer.gameObject.name == "Visual")
                .ToArray();
            CreateShopOwner(assets, parent);

            CreateInteractionObject(
                "ExitDoorObject",
                assets.doorSprite,
                parent,
                new Vector3(0f, -7.15f, -0.1f),
                new Vector3(0.36f, 0.36f, 1f),
                new Vector2(2.6f, 0.5f),
                new Vector2(0f, -0.28f),
                new Vector2(3.2f, 1.4f),
                "location_shop",
                string.Empty,
                "返回阶段地图",
                "按 E：返回阶段地图｜不消耗时间",
                null,
                null,
                true,
                20,
                1750,
                assets.markerSprite);
        }

        private static GameObject CreateInteractionObject(
            string objectName,
            Sprite sprite,
            Transform parent,
            Vector3 position,
            Vector3 visualScale,
            Vector2 blockingSize,
            Vector2 colliderOffset,
            Vector2 triggerSize,
            string locationId,
            string actionId,
            string displayName,
            string promptText,
            LocationActionData action,
            ActivityData activity,
            bool isExit,
            int priority,
            int sortingOrder,
            Sprite markerSprite)
        {
            GameObject root = new GameObject(objectName);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;

            MapInteractionPoint point = root.AddComponent<MapInteractionPoint>();
            point.locationId = locationId;
            point.actionId = actionId;
            point.displayName = displayName;
            point.description = promptText;
            point.interactionType = isExit ? MapInteractionType.ReturnToMainUi : MapInteractionType.LocationAction;
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = root.AddComponent<MapInteractionTarget>();
            target.interactionId = string.IsNullOrEmpty(actionId) ? "exit_shop" : actionId;
            target.displayName = displayName;
            target.promptText = promptText;
            target.locationAction = action;
            target.fallbackActivity = activity;
            target.interactionPoint = point;
            target.isExitPortal = isExit;
            target.consumeTimeOnInteraction = false;
            target.requireFacing = false;
            target.interactionRange = Mathf.Max(triggerSize.x, triggerSize.y) * 0.55f;
            target.priority = priority;

            if (isExit)
            {
                root.AddComponent<MapExitPortal>().destinationId = "stage_hub";
            }

            SpriteRenderer renderer = AddVisual(root.transform, sprite, visualScale, sortingOrder);
            BoxCollider2D blockingCollider = AddBoxChild(root.transform, "BlockingCollider", colliderOffset, blockingSize, false);
            if (isExit)
            {
                blockingCollider.enabled = false;
            }
            AddBoxChild(root.transform, "InteractionTrigger", colliderOffset, triggerSize, true);

            MapInteractionStateVisual stateVisual = root.AddComponent<MapInteractionStateVisual>();
            stateVisual.target = target;
            stateVisual.tintRenderers = new[] { renderer };
            stateVisual.nearbyIndicator = CreateMarker("NearbyMarker", root.transform, markerSprite, new Vector3(0f, triggerSize.y * 0.55f, 0f), new Color(1f, 0.92f, 0.35f, 0.95f), 2500, 0.5f);
            stateVisual.debugRange = CreateMarker("DebugRange", root.transform, markerSprite, Vector3.zero, new Color(0.9f, 0.25f, 0.18f, 0.25f), 2490, Mathf.Max(triggerSize.x, triggerSize.y));
            stateVisual.nearbyIndicator.SetActive(false);
            stateVisual.debugRange.SetActive(false);
            return root;
        }

        private static GameObject CreateBlockingSprite(
            string name,
            Sprite sprite,
            Transform parent,
            Vector3 position,
            Vector3 visualScale,
            Vector2 blockingSize,
            Vector2 colliderOffset)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            SpriteRenderer renderer = AddVisual(root.transform, sprite, visualScale, 1200);
            AddBoxChild(root.transform, "BlockingCollider", colliderOffset, blockingSize, false);
            MapObjectYSorter sorter = root.AddComponent<MapObjectYSorter>();
            sorter.useRendererCenterAsSortY = true;
            sorter.renderers = new[] { renderer };
            if (name == "AisleShelf_Middle")
            {
                AddOcclusionFader(root, sorter.renderers);
            }
            return root;
        }

        private static GameObject CreateShopOwner(ShopAssets assets, Transform parent)
        {
            GameObject root = new GameObject("ShopOwnerObject");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(-2.05f, 5.05f, -0.1f);

            SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
            renderer.sprite = assets.shopOwnerSprite;
            renderer.sortingOrder = 1150;

            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = assets.shopOwnerAnimator;

            MapInteractionPoint point = root.AddComponent<MapInteractionPoint>();
            point.locationId = "location_shop";
            point.actionId = "action_shop_browse_goods";
            point.interactionType = MapInteractionType.LocationAction;
            point.displayName = "商店老板";
            point.description = "按 E：和店长购买商品｜不消耗时间";
            point.requiresUnlockedLocation = false;

            MapInteractionTarget target = root.AddComponent<MapInteractionTarget>();
            target.interactionId = "action_shop_browse_goods";
            target.displayName = "商店老板";
            target.promptText = "按 E：和店长购买商品｜不消耗时间";
            target.locationAction = assets.shopAction;
            target.fallbackActivity = assets.shopActivity;
            target.interactionPoint = point;
            target.consumeTimeOnInteraction = false;
            target.requireFacing = false;
            target.interactionRange = 2.2f;
            target.priority = 16;

            MapObjectYSorter sorter = root.AddComponent<MapObjectYSorter>();
            sorter.useRendererCenterAsSortY = true;
            sorter.renderers = new[] { renderer };

            MapInteractionStateVisual stateVisual = root.AddComponent<MapInteractionStateVisual>();
            stateVisual.target = target;
            stateVisual.tintRenderers = new[] { renderer };
            stateVisual.nearbyIndicator = CreateMarker("NearbyMarker", root.transform, assets.markerSprite, new Vector3(0f, 1.15f, 0f), new Color(1f, 0.92f, 0.35f, 0.95f), 2500, 0.5f);
            stateVisual.debugRange = CreateMarker("DebugRange", root.transform, assets.markerSprite, Vector3.zero, new Color(0.9f, 0.25f, 0.18f, 0.25f), 2490, target.interactionRange);
            stateVisual.nearbyIndicator.SetActive(false);
            stateVisual.debugRange.SetActive(false);
            return root;
        }

        private static GameObject CreateStaticSprite(string name, Sprite sprite, Transform parent, Vector3 position, Vector3 scale, int sortingOrder)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            AddVisual(root.transform, sprite, scale, sortingOrder);
            return root;
        }

        private static SpriteRenderer AddVisual(Transform parent, Sprite sprite, Vector3 scale, int sortingOrder)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
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

        private static void AddOcclusionFader(GameObject target, SpriteRenderer[] renderers)
        {
            MapSpriteOcclusionFader fader = target.AddComponent<MapSpriteOcclusionFader>();
            fader.targetRenderers = renderers;
            fader.fadedAlpha = 0.45f;
            fader.fadeSpeed = 8f;
            fader.boundsPadding = new Vector2(0.15f, 0.1f);
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

        private static GameObject CreatePlayer(ShopAssets assets, MapInteractionFlowController flow)
        {
            GameObject player = new GameObject("PlayerRpg");
            player.transform.position = new Vector3(0f, -6.65f, -0.1f);
            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = assets.playerSprite;
            renderer.sortingOrder = 1800;
            Rigidbody2D body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            CapsuleCollider2D capsule = player.AddComponent<CapsuleCollider2D>();
            capsule.size = new Vector2(0.75f, 1.2f);
            capsule.offset = new Vector2(0f, -0.35f);
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
            controller.useMovementBounds = true;
            controller.movementMin = new Vector2(-10.6f, -7.2f);
            controller.movementMax = new Vector2(10.4f, 5.2f);
            MapObjectYSorter sorter = player.AddComponent<MapObjectYSorter>();
            sorter.useRendererCenterAsSortY = true;
            sorter.renderers = new[] { renderer };
            return player;
        }

        private static GameObject CreateCamera(Transform player, Transform bounds)
        {
            GameObject cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.backgroundColor = Color.black;
            cameraObject.AddComponent<AudioListener>();
            MapCameraController controller = cameraObject.AddComponent<MapCameraController>();
            controller.targetCamera = camera;
            controller.followTarget = player;
            controller.cameraBounds = bounds.GetComponent<Collider2D>();
            controller.useExplicitBounds = true;
            controller.minPosition = new Vector2(-12f, -8f);
            controller.maxPosition = new Vector2(11f, 7f);
            return cameraObject;
        }

        private static string BackupSceneIfPresent()
        {
            string fullScenePath = Path.GetFullPath(ScenePath);
            if (!File.Exists(fullScenePath))
            {
                return string.Empty;
            }

            EnsureFolder(BackupRoot);
            string backupPath = BackupRoot + "/ShopMapScene_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".unity";
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

        private static bool SceneUsesWholeShopBackground()
        {
            foreach (SpriteRenderer renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>())
            {
                if (renderer == null || renderer.sprite == null)
                {
                    continue;
                }

                string spritePath = AssetDatabase.GetAssetPath(renderer.sprite);
                if (spritePath == SourceReference || spritePath == "Assets/Art/Pixel/Backgrounds/new_bg_school_shop_scene.png")
                {
                    return true;
                }
            }

            return false;
        }

        private static void WriteReport(ShopAssets assets)
        {
            string report =
                "# ShopMapScene New2_0 Build Report\n\n"
                + "- Scene: `" + ScenePath + "`\n"
                + "- Backup scene: `" + (string.IsNullOrEmpty(assets.sceneBackupPath) ? "none; scene did not exist before this build" : assets.sceneBackupPath) + "`\n"
                + "- Reference image: `" + SourceReference + "`\n"
                + "- Scene strategy: editable Tilemap plus independent Sprite objects. The whole reference image is not used as a scene background.\n"
                + "- Grid: `1 x 1`, room plan: `24 x 16` tiles.\n"
                + "- Tilemaps: `" + string.Join("`, `", RequiredTilemapLayers()) + "`.\n"
                + "- Main editable props are under `ShopMapRpgRoot/EditableShopProps` and `ShopMapRpgRoot/EditableWallDecor`.\n"
                + "- Interactions: `ShopOwnerObject` opens placeholder shop interaction, `ExitDoorObject` returns to stage map without time cost. `ShopCounterObject` is visual/blocking only.\n\n"
                + "## Generated Art\n\n"
                + "- Prop sprites: `" + ShopPropRoot + "`\n"
                + "- Tile sprites: `" + ShopTileSpriteRoot + "`\n"
                + "- Tile assets: `" + TileRoot + "`\n"
                + "- Data assets: `" + DataRoot + "`\n\n"
                + "## Notes\n\n"
                + "- Large shelves and cabinets are complete cropped sprites from the provided shop item sheet, not partial furniture fragments.\n"
                + "- Floor/wall tiles are derived from the provided empty shop room source so they remain editable as Tilemap cells.\n";
            WriteTextAsset(DocsRoot + "/ShopMapScene_New2_0_BuildReport.md", report);
        }

        private static void WriteTextAsset(string path, string content)
        {
            File.WriteAllText(Path.GetFullPath(path), content);
            AssetDatabase.ImportAsset(path);
        }

        private static string ToAssetPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            string root = Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/').TrimEnd('/');
            return normalized.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(root.Length + 1)
                : normalized;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class ShopAssets
        {
            public Sprite floorSprite;
            public Sprite floorWarmSprite;
            public Sprite wallSprite;
            public Sprite wallTopSprite;
            public Sprite wallTrimSprite;
            public Sprite collisionSprite;
            public Sprite markerSprite;

            public Sprite cafeCounterSprite;
            public Sprite checkoutCounterSprite;
            public Sprite wallShelfSprite;
            public Sprite counterDisplaySprite;
            public Sprite recycleBinsSprite;
            public Sprite drinkFridgeSprite;
            public Sprite leftTallShelfSprite;
            public Sprite aisleShelfTopSprite;
            public Sprite aisleShelfMiddleSprite;
            public Sprite aisleShelfBottomSprite;
            public Sprite rightFreezerSprite;
            public Sprite plantSprite;
            public Sprite saleSignSprite;
            public Sprite cratesSprite;
            public Sprite basketStackSprite;
            public Sprite promoRackSprite;
            public Sprite newProductRackSprite;
            public Sprite posterBlueSprite;
            public Sprite posterFruitSprite;
            public Sprite openSignSprite;
            public Sprite clockSprite;
            public Sprite menuBoardSprite;
            public Sprite noticeSprite;
            public Sprite doorSprite;
            public Sprite redStandSprite;
            public Sprite playerSprite;
            public Sprite shopOwnerSprite;

            public Tile floorTile;
            public Tile floorWarmTile;
            public Tile wallTile;
            public Tile wallTopTile;
            public Tile wallTrimTile;
            public Tile collisionTile;

            public RuntimeAnimatorController playerAnimator;
            public RuntimeAnimatorController shopOwnerAnimator;
            public ActivityData shopActivity;
            public LocationActionData shopAction;
            public string sceneBackupPath;
        }
    }
}
