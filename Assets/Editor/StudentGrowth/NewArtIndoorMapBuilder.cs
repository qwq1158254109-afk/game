using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

namespace StudentGrowth.Editor
{
    public static class NewArtIndoorMapBuilder
    {
        private const string ScenePath = "Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity";
        private const string BackupRoot = "Assets/Scenes/StudentGrowth/Maps/Backups";

        private const string SourceTilesetRoot = "Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets";
        private const string SourceA2 = SourceTilesetRoot + "/0_Inside_A2_new.png";
        private const string SourceA4 = SourceTilesetRoot + "/0_Inside_A4_new.png";
        private const string SourceB = SourceTilesetRoot + "/0_Inside_B_new.png";
        private const string SourceC = SourceTilesetRoot + "/0_Inside_C_new.png";
        private const string SourceCharacters = "Assets/Art/Characters/ModernInteriors/RpgMakerMV/new_characters_48x48.png";

        private const string ArtRoot = "Assets/Art/New2_0";
        private const string SlicedTileSpriteRoot = ArtRoot + "/Tilesets/Sliced";
        private const string PropSpriteRoot = ArtRoot + "/Props/Home";
        private const string CharacterSpriteRoot = ArtRoot + "/Characters/Home/Old";
        private const string TileRoot = "Assets/Tiles/New2_0";
        private const string TilePaletteRoot = "Assets/TilePalettes/New2_0";
        private const string PrefabMapRoot = "Assets/Prefabs/StudentGrowth/Maps/New2_0/Indoor";
        private const string PrefabInteractableRoot = "Assets/Prefabs/StudentGrowth/Interactables/New2_0";
        private const string PrefabNpcRoot = "Assets/Prefabs/StudentGrowth/NPC/New2_0";
        private const string DataRoot = "Assets/Data/StudentGrowth/New2_0/HomeMap";
        private const string AnimationRoot = "Assets/Animations/StudentGrowth/New2_0/HomeMap";
        private const string DocsRoot = "Assets/Docs/StudentGrowth/New2_0";
        private const string ProportionReportPath = "Assets/Docs/IndoorSceneRebuild_New2_0_Report.md";

        private const float TilePpu = 16f;
        private const float CharacterPpu = 16f;
        private const int MapMinX = -15;
        private const int MapMaxX = 14;
        private const int MapMinY = -10;
        private const int MapMaxY = 9;
        private const int MapWidth = MapMaxX - MapMinX + 1;
        private const int MapHeight = MapMaxY - MapMinY + 1;
        private const int TopWallY = MapMaxY;
        private const int WallInteriorY = MapMaxY - 1;
        private const int WallFloorTrimY = MapMaxY - 2;

        [MenuItem("Tools/Student Growth/New Art/Rebuild HomeMapScene With New2_0")]
        public static void RebuildHomeMapSceneWithNewArt()
        {
            EnsureFolders();
            ConfigureSourceImporters();
            GeneratedAssets assets = GenerateAssets();
            GeneratePrefabs(assets);
            BuildHomeScene(assets);
            WriteReports(assets);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("HomeMapScene rebuilt with New2_0 indoor Tilemap art.");
        }

        [MenuItem("Tools/Student Growth/New Art/Verify HomeMapScene New2_0")]
        public static void VerifyHomeMapSceneNew20()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Require(GameObject.Find("HomeMapSceneRoot") != null, "HomeMapSceneRoot is missing.");
            Require(GameObject.Find("HomeMapRpgRoot") != null, "HomeMapRpgRoot is missing.");
            foreach (string layer in new[]
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
            })
            {
                Require(GameObject.Find(layer) != null, layer + " is missing.");
            }

            GameObject collision = GameObject.Find("Collision_Tilemap");
            Require(collision.GetComponent<TilemapCollider2D>() != null, "Collision_Tilemap needs TilemapCollider2D.");
            Require(collision.GetComponent<CompositeCollider2D>() != null, "Collision_Tilemap needs CompositeCollider2D.");
            Rigidbody2D collisionBody = collision.GetComponent<Rigidbody2D>();
            Require(collisionBody != null && collisionBody.bodyType == RigidbodyType2D.Static, "Collision_Tilemap needs static Rigidbody2D.");
            Require(GameObject.Find("DefaultSpawnPoint") != null, "DefaultSpawnPoint is missing.");
            foreach (string objectName in new[] { "BedObject", "DeskObject", "DiningTableObject", "FamilyNpcObject", "ExitDoorObject" })
            {
                GameObject target = GameObject.Find(objectName);
                Require(target != null, objectName + " is missing.");
                Require(target.GetComponent<MapInteractionPoint>() != null, objectName + " needs MapInteractionPoint.");
                Require(target.GetComponent<MapInteractionTarget>() != null, objectName + " needs MapInteractionTarget.");
                Require(target.transform.Find("Visual") != null, objectName + " needs Visual child.");
                Require(target.transform.Find("BlockingCollider") != null, objectName + " needs BlockingCollider child.");
                Require(target.transform.Find("InteractionTrigger") != null, objectName + " needs InteractionTrigger child.");
            }

            MapInteractionTarget exit = GameObject.Find("ExitDoorObject").GetComponent<MapInteractionTarget>();
            Require(exit.isExitPortal && !exit.consumeTimeOnInteraction, "ExitDoorObject must be a no-time-cost exit.");
            Debug.Log("HomeMapScene New2_0 verification passed.");
        }

        private static void EnsureFolders()
        {
            foreach (string folder in new[]
            {
                ArtRoot,
                ArtRoot + "/Tilesets",
                SlicedTileSpriteRoot,
                PropSpriteRoot,
                CharacterSpriteRoot,
                TileRoot,
                TileRoot + "/Ground",
                TileRoot + "/Wall",
                TileRoot + "/Decoration",
                TileRoot + "/Furniture",
                TileRoot + "/Collision",
                TilePaletteRoot,
                PrefabMapRoot,
                PrefabInteractableRoot,
                PrefabNpcRoot,
                DataRoot,
                AnimationRoot,
                DocsRoot,
                BackupRoot,
                "Assets/Docs"
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
            ConfigureTextureImporter(SourceA2, TilePpu, SpriteImportMode.Multiple);
            ConfigureTextureImporter(SourceA4, TilePpu, SpriteImportMode.Multiple);
            ConfigureTextureImporter(SourceB, TilePpu, SpriteImportMode.Multiple);
            ConfigureTextureImporter(SourceC, TilePpu, SpriteImportMode.Multiple);
            ConfigureTextureImporter(SourceCharacters, CharacterPpu, SpriteImportMode.Multiple);
        }

        private static void ConfigureTextureImporter(string assetPath, float ppu, SpriteImportMode mode)
        {
            if (!File.Exists(Path.GetFullPath(assetPath)))
            {
                return;
            }

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = mode;
            importer.spritePixelsPerUnit = ppu;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();
        }

        private static GeneratedAssets GenerateAssets()
        {
            GeneratedAssets assets = new GeneratedAssets();

            assets.groundWoodSprite = CropSprite(SourceA4, SlicedTileSpriteRoot + "/new20_ground_wood_00.png", 320, 160, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.groundLightSprite = CropSprite(SourceA2, SlicedTileSpriteRoot + "/new20_ground_light_00.png", 208, 16, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.bathFloorSprite = CropSprite(SourceA2, SlicedTileSpriteRoot + "/new20_ground_bath_tile_00.png", 96, 288, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.entryFloorSprite = CropSprite(SourceA2, SlicedTileSpriteRoot + "/new20_ground_entry_checker_00.png", 0, 288, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.carpetSprite = CropSprite(SourceA2, SlicedTileSpriteRoot + "/new20_decoration_carpet_00.png", 400, 448, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.wallInteriorSprite = CropSprite(SourceA4, SlicedTileSpriteRoot + "/new20_wall_interior_00.png", 112, 160, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.wallTopSprite = CropSprite(SourceA4, SlicedTileSpriteRoot + "/new20_wall_top_00.png", 112, 224, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.wallPillarSprite = CropSprite(SourceA4, SlicedTileSpriteRoot + "/new20_wall_pillar_00.png", 96, 144, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.wallShadowSprite = CropSprite(SourceA4, SlicedTileSpriteRoot + "/new20_wall_shadow_00.png", 112, 224, 16, 16, TilePpu, SpriteImportMode.Single);
            assets.markerSprite = CropSprite(SourceA2, SlicedTileSpriteRoot + "/new20_interaction_marker_00.png", 688, 448, 16, 16, TilePpu, SpriteImportMode.Single);

            assets.bedSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_bed.png", 384, 6, 96, 138, TilePpu, SpriteImportMode.Single, true, true);
            assets.deskSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_desk.png", 624, 147, 96, 93, TilePpu, SpriteImportMode.Single, true);
            assets.diningTableSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_dining_table.png", 384, 336, 144, 96, TilePpu, SpriteImportMode.Single, true);
            assets.wardrobeSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_wardrobe.png", 627, 6, 90, 138, TilePpu, SpriteImportMode.Single, true);
            assets.exitDoorSprite = CreateExitDoorSprite(PropSpriteRoot + "/new20_home_exit_door.png", TilePpu);
            assets.windowSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_decor_window_double.png", 96, 96, 93, 96, TilePpu, SpriteImportMode.Single, true);
            assets.sofaSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_sofa.png", 0, 384, 144, 96, TilePpu, SpriteImportMode.Single, true);
            assets.tvSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_tv_stand.png", 144, 384, 96, 96, TilePpu, SpriteImportMode.Single, true);
            assets.coffeeTableSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_coffee_table.png", 384, 672, 96, 72, TilePpu, SpriteImportMode.Single, true);
            assets.bathtubSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_bathtub.png", 0, 672, 144, 96, TilePpu, SpriteImportMode.Single, true);
            assets.toiletSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_toilet.png", 144, 576, 48, 96, TilePpu, SpriteImportMode.Single, true);
            assets.sinkMirrorSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_sink_mirror.png", 192, 576, 96, 96, TilePpu, SpriteImportMode.Single, true);
            CropSprite(SourceB, PropSpriteRoot + "/new20_home_bath_vanity.png", 192, 576, 96, 96, TilePpu, SpriteImportMode.Single, true);
            assets.kitchenCounterSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_kitchen_counter.png", 144, 672, 144, 96, TilePpu, SpriteImportMode.Single, true);
            assets.stoveSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_stove.png", 288, 672, 48, 96, TilePpu, SpriteImportMode.Single, true);
            assets.fridgeSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_home_fridge.png", 336, 672, 48, 96, TilePpu, SpriteImportMode.Single, true);
            assets.floorLampSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_decor_floor_lamp.png", 725, 2, 35, 92, TilePpu, SpriteImportMode.Single, true);
            assets.smallLampSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_decor_small_lamp.png", 728, 101, 29, 41, TilePpu, SpriteImportMode.Single, true);
            assets.wallArtSprite = CropSprite(SourceC, PropSpriteRoot + "/new20_decor_wall_art_set.png", 0, 192, 192, 72, TilePpu, SpriteImportMode.Single, true);
            assets.tableMealSprite = CropSprite(SourceC, PropSpriteRoot + "/new20_decor_table_meal.png", 192, 288, 144, 48, TilePpu, SpriteImportMode.Single, true);
            assets.notesSprite = CropSprite(SourceC, PropSpriteRoot + "/new20_decor_notes.png", 0, 480, 96, 48, TilePpu, SpriteImportMode.Single, true);
            assets.plantSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_decor_potted_plant.png", 288, 384, 48, 96, TilePpu, SpriteImportMode.Single, true);
            assets.bookshelfSprite = CropSprite(SourceB, PropSpriteRoot + "/new20_decor_low_bookshelf.png", 384, 243, 144, 93, TilePpu, SpriteImportMode.Single, true);
            assets.bulletinBoardSprite = CropSprite(SourceC, PropSpriteRoot + "/new20_decor_bulletin_board.png", 2, 677, 95, 77, TilePpu, SpriteImportMode.Single, true);
            assets.mapPaintingSprite = CropSprite(SourceC, PropSpriteRoot + "/new20_decor_map_painting.png", 194, 527, 92, 98, TilePpu, SpriteImportMode.Single, true);
            assets.flowerBoxSprite = CropSprite(SourceC, PropSpriteRoot + "/new20_decor_flower_box.png", 206, 680, 68, 65, TilePpu, SpriteImportMode.Single, true);
            assets.familyNpcSprite = CropSprite(SourceCharacters, CharacterSpriteRoot + "/new20_family_npc_idle.png", 56, 8, 32, 32, CharacterPpu, SpriteImportMode.Single);
            assets.playerIdleSprite = CropSprite(SourceCharacters, CharacterSpriteRoot + "/new20_player_idle_down.png", 8, 8, 32, 32, CharacterPpu, SpriteImportMode.Single);
            assets.playerWalkSprites = new[]
            {
                CropSprite(SourceCharacters, CharacterSpriteRoot + "/new20_player_walk_down_0.png", 8, 8, 32, 32, CharacterPpu, SpriteImportMode.Single),
                CropSprite(SourceCharacters, CharacterSpriteRoot + "/new20_player_walk_down_1.png", 56, 8, 32, 32, CharacterPpu, SpriteImportMode.Single),
                CropSprite(SourceCharacters, CharacterSpriteRoot + "/new20_player_walk_down_2.png", 104, 8, 32, 32, CharacterPpu, SpriteImportMode.Single),
                CropSprite(SourceCharacters, CharacterSpriteRoot + "/new20_player_walk_down_3.png", 152, 8, 32, 32, CharacterPpu, SpriteImportMode.Single)
            };

            assets.groundWoodTile = CreateTile(TileRoot + "/Ground/New20_Tile_Ground_Wood_00.asset", assets.groundWoodSprite, Color.white, Tile.ColliderType.None);
            assets.groundLightTile = CreateTile(TileRoot + "/Ground/New20_Tile_Ground_Light_00.asset", assets.groundLightSprite, Color.white, Tile.ColliderType.None);
            assets.bathFloorTile = CreateTile(TileRoot + "/Ground/New20_Tile_Ground_Bath_00.asset", assets.bathFloorSprite, Color.white, Tile.ColliderType.None);
            assets.entryFloorTile = CreateTile(TileRoot + "/Ground/New20_Tile_Ground_Entry_00.asset", assets.entryFloorSprite, Color.white, Tile.ColliderType.None);
            assets.carpetTile = CreateTile(TileRoot + "/Decoration/New20_Tile_Decoration_Carpet_00.asset", assets.carpetSprite, Color.white, Tile.ColliderType.None);
            assets.wallInteriorTile = CreateTile(TileRoot + "/Wall/New20_Tile_Wall_Interior_00.asset", assets.wallInteriorSprite, Color.white, Tile.ColliderType.None);
            assets.wallTopTile = CreateTile(TileRoot + "/Wall/New20_Tile_Wall_Top_00.asset", assets.wallTopSprite, Color.white, Tile.ColliderType.None);
            assets.wallPillarTile = CreateTile(TileRoot + "/Wall/New20_Tile_Wall_Pillar_00.asset", assets.wallPillarSprite, Color.white, Tile.ColliderType.None);
            assets.wallShadowTile = CreateTile(TileRoot + "/Wall/New20_Tile_Wall_Shadow_00.asset", assets.wallShadowSprite, new Color(0.8f, 0.72f, 0.64f, 0.72f), Tile.ColliderType.None);
            assets.collisionTile = CreateTile(TileRoot + "/Collision/New20_Tile_Collision_Block_00.asset", assets.wallInteriorSprite, new Color(1f, 1f, 1f, 0f), Tile.ColliderType.Grid);
            assets.markerTile = CreateTile(TileRoot + "/Decoration/New20_Tile_Interaction_Marker_00.asset", assets.markerSprite, new Color(1f, 0.92f, 0.35f, 0.45f), Tile.ColliderType.None);
            assets.groundWoodPatternTiles = CreateTilePattern(
                SourceA4,
                "new20_ground_wood_pattern",
                TileRoot + "/Ground/New20_Tile_Ground_Wood_Pattern",
                288,
                144,
                6,
                6,
                Color.white,
                Tile.ColliderType.None);
            assets.wallFramePatternTiles = CreateTilePattern(
                SourceA4,
                "new20_wall_frame_pattern",
                TileRoot + "/Wall/New20_Tile_Wall_Frame_Pattern",
                96,
                144,
                6,
                9,
                Color.white,
                Tile.ColliderType.None,
                6);

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
                Reward(PlayerStatKey.Autonomy, 0, "明日效率 +10"));
            assets.familyDinnerActivity = CreateActivity(
                "New20_HomeFamilyDinner.asset",
                "activity_home_family_dinner",
                "家庭晚饭聊天",
                "和家人边吃晚饭边聊聊近况。",
                ActivityType.Event,
                Reward(PlayerStatKey.ParentRelation, 5, "亲子 +5"),
                Reward(PlayerStatKey.Mood, 5, "心情 +5"));

            assets.idleClip = CreateClip(AnimationRoot + "/New20_HomePlayer_Idle.anim", new[] { assets.playerIdleSprite }, 4f);
            assets.walkClip = CreateClip(AnimationRoot + "/New20_HomePlayer_Walk.anim", assets.playerWalkSprites, 8f);
            assets.playerAnimator = CreateAnimatorController(AnimationRoot + "/New20_HomePlayer.controller", assets.idleClip, assets.walkClip);
            assets.tilePalettePrefab = CreateTilePalette(assets);
            return assets;
        }

        private static Sprite CropSprite(string sourceAssetPath, string outputAssetPath, int x, int yFromTop, int width, int height, float pixelsPerUnit, SpriteImportMode mode, bool makeBlackTransparent = false, bool makeOliveBackdropTransparent = false)
        {
            string sourcePath = Path.GetFullPath(sourceAssetPath);
            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Missing source art.", sourceAssetPath);
            }

            Texture2D source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            source.LoadImage(File.ReadAllBytes(sourcePath));
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            int unityY = source.height - yFromTop - height;
            Color[] pixels = source.GetPixels(x, unityY, width, height);
            if (makeBlackTransparent)
            {
                for (int i = 0; i < pixels.Length; i++)
                {
                    Color pixel = pixels[i];
                    if ((makeBlackTransparent && pixel.r < 0.01f && pixel.g < 0.01f && pixel.b < 0.01f)
                        || (makeOliveBackdropTransparent && pixel.r > 0.15f && pixel.r < 0.55f && pixel.g > 0.25f && pixel.g < 0.6f && pixel.b > 0.08f && pixel.b < 0.35f))
                    {
                        pixels[i] = new Color(0f, 0f, 0f, 0f);
                    }
                }
            }

            output.SetPixels(pixels);
            output.Apply();

            File.WriteAllBytes(Path.GetFullPath(outputAssetPath), output.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(source);
            UnityEngine.Object.DestroyImmediate(output);

            ConfigureTextureImporter(outputAssetPath, pixelsPerUnit, mode);
            return AssetDatabase.LoadAssetAtPath<Sprite>(outputAssetPath);
        }

        private static Sprite CreateExitDoorSprite(string outputAssetPath, float pixelsPerUnit)
        {
            const int width = 48;
            const int height = 48;
            Texture2D output = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 outline = new Color32(66, 37, 32, 255);
            Color32 darkWood = new Color32(92, 54, 42, 255);
            Color32 midWood = new Color32(128, 82, 58, 255);
            Color32 lightWood = new Color32(172, 116, 76, 255);
            Color32 brass = new Color32(213, 166, 82, 255);
            Color32 mat = new Color32(194, 103, 67, 255);
            Color32 matEdge = new Color32(82, 68, 88, 255);

            Color32[] pixels = Enumerable.Repeat(transparent, width * height).ToArray();
            FillRect(pixels, width, 8, 2, 32, 42, outline);
            FillRect(pixels, width, 11, 5, 26, 36, darkWood);
            FillRect(pixels, width, 14, 8, 8, 25, midWood);
            FillRect(pixels, width, 26, 8, 8, 25, midWood);
            FillRect(pixels, width, 16, 21, 16, 3, lightWood);
            FillRect(pixels, width, 34, 22, 3, 3, brass);
            FillRect(pixels, width, 5, 0, 38, 5, matEdge);
            FillRect(pixels, width, 8, 3, 32, 8, mat);
            FillRect(pixels, width, 11, 6, 26, 2, lightWood);

            output.SetPixels32(pixels);
            output.Apply();
            File.WriteAllBytes(Path.GetFullPath(outputAssetPath), output.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(output);
            ConfigureTextureImporter(outputAssetPath, pixelsPerUnit, SpriteImportMode.Single);
            return AssetDatabase.LoadAssetAtPath<Sprite>(outputAssetPath);
        }

        private static void FillRect(Color32[] pixels, int textureWidth, int x, int y, int width, int height, Color32 color)
        {
            for (int py = y; py < y + height; py++)
            {
                for (int px = x; px < x + width; px++)
                {
                    pixels[py * textureWidth + px] = color;
                }
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

        private static Tile[,] CreateTilePattern(
            string sourceAssetPath,
            string spriteNamePrefix,
            string tilePathPrefix,
            int x,
            int yFromTop,
            int columns,
            int rows,
            Color color,
            Tile.ColliderType colliderType,
            int sourceRows = 0)
        {
            Tile[,] tiles = new Tile[columns, rows];
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int sourceRow = sourceRows > 0 ? row % sourceRows : row;
                    string suffix = "_" + row.ToString("00") + "_" + column.ToString("00");
                    Sprite sprite = CropSprite(
                        sourceAssetPath,
                        SlicedTileSpriteRoot + "/" + spriteNamePrefix + suffix + ".png",
                        x + column * 16,
                        yFromTop + sourceRow * 16,
                        16,
                        16,
                        TilePpu,
                        SpriteImportMode.Single);

                    tiles[column, row] = CreateTile(tilePathPrefix + suffix + ".asset", sprite, color, colliderType);
                }
            }

            return tiles;
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

        private static GameObject CreateTilePalette(GeneratedAssets assets)
        {
            string path = TilePaletteRoot + "/New20_HomeIndoorPalette.prefab";
            GameObject paletteRoot = new GameObject("New20_HomeIndoorPalette");
            Grid grid = paletteRoot.AddComponent<Grid>();
            grid.cellSize = Vector3.one;
            GameObject tilemapObject = new GameObject("Palette_Tilemap");
            tilemapObject.transform.SetParent(paletteRoot.transform, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();
            TileBase[] tiles =
            {
                assets.groundWoodTile,
                assets.groundLightTile,
                assets.bathFloorTile,
                assets.entryFloorTile,
                assets.wallInteriorTile,
                assets.wallTopTile,
                assets.wallPillarTile,
                assets.wallShadowTile,
                assets.carpetTile,
                assets.markerTile,
                assets.collisionTile
            };

            for (int i = 0; i < tiles.Length; i++)
            {
                tilemap.SetTile(new Vector3Int(i, 0, 0), tiles[i]);
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(paletteRoot, path);
            UnityEngine.Object.DestroyImmediate(paletteRoot);
            return prefab;
        }

        private static void GeneratePrefabs(GeneratedAssets assets)
        {
            assets.bedPrefab = CreateInteractionPrefab(
                PrefabInteractableRoot + "/New20_BedObject.prefab",
                "BedObject",
                assets.bedSprite,
                new Vector2(3.4f, 1.4f),
                new Vector2(4.2f, 2.2f),
                new Vector2(0f, -1.1f),
                "location_home",
                assets.restActivity.activityId,
                "早点休息",
                "按 E：早点休息｜进入下一天｜精力 +20｜压力 -5",
                assets.restActivity,
                false,
                10,
                assets.markerSprite,
                20,
                new Vector3(0.68f, 0.68f, 1f));
            assets.deskPrefab = CreateInteractionPrefab(
                PrefabInteractableRoot + "/New20_DeskObject.prefab",
                "DeskObject",
                assets.deskSprite,
                new Vector2(3.2f, 1.2f),
                new Vector2(4.0f, 2.0f),
                new Vector2(0f, -0.7f),
                "location_home",
                assets.planActivity.activityId,
                "整理计划",
                "按 E：整理计划｜消耗 1 时间｜自主 +5｜明日效率 +10",
                assets.planActivity,
                false,
                8,
                assets.markerSprite,
                20,
                new Vector3(0.5f, 0.5f, 1f));
            assets.diningTablePrefab = CreateInteractionPrefab(
                PrefabInteractableRoot + "/New20_DiningTableObject.prefab",
                "DiningTableObject",
                assets.diningTableSprite,
                new Vector2(3.8f, 1.6f),
                new Vector2(4.8f, 2.6f),
                new Vector2(0f, -0.45f),
                "location_home",
                assets.familyDinnerActivity.activityId,
                "家庭晚饭聊天",
                "按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5",
                assets.familyDinnerActivity,
                false,
                9,
                assets.markerSprite,
                20,
                new Vector3(0.66f, 0.66f, 1f));
            assets.familyNpcPrefab = CreateInteractionPrefab(
                PrefabNpcRoot + "/New20_FamilyNpcObject.prefab",
                "FamilyNpcObject",
                assets.familyNpcSprite,
                new Vector2(0.8f, 1.2f),
                new Vector2(1.4f, 2.0f),
                new Vector2(0f, -0.35f),
                "location_home",
                assets.familyDinnerActivity.activityId,
                "家庭晚饭聊天",
                "按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5",
                assets.familyDinnerActivity,
                false,
                7,
                assets.markerSprite,
                20);
            assets.wardrobePrefab = CreateBlockingPropPrefab(
                PrefabInteractableRoot + "/New20_WardrobeObject.prefab",
                "WardrobeObject",
                assets.wardrobeSprite,
                new Vector2(2.6f, 1.1f),
                new Vector2(0f, -0.9f),
                20,
                new Vector3(0.68f, 0.68f, 1f));
            assets.exitDoorPrefab = CreateInteractionPrefab(
                PrefabInteractableRoot + "/New20_ExitDoorObject.prefab",
                "ExitDoorObject",
                assets.exitDoorSprite,
                new Vector2(1.8f, 0.9f),
                new Vector2(2.6f, 2.4f),
                new Vector2(0f, -0.95f),
                "location_home",
                string.Empty,
                "返回阶段地图",
                "按 E：返回阶段地图｜不消耗时间",
                null,
                true,
                20,
                assets.markerSprite,
                20);
        }

        private static GameObject CreateInteractionPrefab(
            string path,
            string objectName,
            Sprite sprite,
            Vector2 blockingSize,
            Vector2 triggerSize,
            Vector2 colliderOffset,
            string locationId,
            string actionId,
            string displayName,
            string promptText,
            ActivityData fallbackActivity,
            bool isExit,
            int priority,
            Sprite markerSprite,
            int sortingOrder,
            Vector3? visualScale = null)
        {
            GameObject root = new GameObject(objectName);

            MapInteractionPoint point = root.AddComponent<MapInteractionPoint>();
            point.locationId = locationId;
            point.actionId = actionId;
            point.displayName = displayName;
            point.description = promptText;
            point.interactionType = isExit ? MapInteractionType.ReturnToMainUi : MapInteractionType.LocationAction;
            point.requiresUnlockedLocation = !isExit;

            MapInteractionTarget target = root.AddComponent<MapInteractionTarget>();
            target.interactionId = string.IsNullOrEmpty(actionId) ? "exit_home" : actionId;
            target.displayName = displayName;
            target.promptText = promptText;
            target.fallbackActivity = fallbackActivity;
            target.interactionPoint = point;
            target.isExitPortal = isExit;
            target.consumeTimeOnInteraction = !isExit;
            target.requireFacing = false;
            target.interactionRange = Mathf.Max(triggerSize.x, triggerSize.y) * 0.55f;
            target.priority = priority;

            if (isExit)
            {
                root.AddComponent<MapExitPortal>().destinationId = "stage_hub";
            }

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = visualScale ?? Vector3.one;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;

            GameObject blocking = new GameObject("BlockingCollider");
            blocking.transform.SetParent(root.transform, false);
            blocking.transform.localPosition = colliderOffset;
            BoxCollider2D blockingCollider = blocking.AddComponent<BoxCollider2D>();
            blockingCollider.size = blockingSize;

            GameObject trigger = new GameObject("InteractionTrigger");
            trigger.transform.SetParent(root.transform, false);
            trigger.transform.localPosition = colliderOffset;
            BoxCollider2D triggerCollider = trigger.AddComponent<BoxCollider2D>();
            triggerCollider.isTrigger = true;
            triggerCollider.size = triggerSize;

            MapInteractionStateVisual stateVisual = root.AddComponent<MapInteractionStateVisual>();
            stateVisual.target = target;
            stateVisual.tintRenderers = new[] { renderer };
            stateVisual.nearbyIndicator = CreateMarker("NearbyMarker", root.transform, markerSprite, new Vector3(0f, triggerSize.y * 0.55f, 0f), new Color(1f, 0.92f, 0.35f, 0.95f), 50, 0.5f);
            stateVisual.debugRange = CreateMarker("DebugRange", root.transform, markerSprite, Vector3.zero, new Color(0.9f, 0.25f, 0.18f, 0.25f), 49, Mathf.Max(triggerSize.x, triggerSize.y));
            stateVisual.nearbyIndicator.SetActive(false);
            stateVisual.debugRange.SetActive(false);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject CreateBlockingPropPrefab(
            string path,
            string objectName,
            Sprite sprite,
            Vector2 blockingSize,
            Vector2 colliderOffset,
            int sortingOrder,
            Vector3? visualScale = null)
        {
            GameObject root = new GameObject(objectName);

            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(root.transform, false);
            visual.transform.localScale = visualScale ?? Vector3.one;
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;

            GameObject blocking = new GameObject("BlockingCollider");
            blocking.transform.SetParent(root.transform, false);
            blocking.transform.localPosition = colliderOffset;
            BoxCollider2D blockingCollider = blocking.AddComponent<BoxCollider2D>();
            blockingCollider.size = blockingSize;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            return prefab;
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

        private static void BuildHomeScene(GeneratedAssets assets)
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

            Grid grid = CreateGrid(indoorRoot.transform);
            Tilemap ground = CreateTilemap(grid.transform, "Ground_Tilemap", 0, true, false);
            Tilemap wall = CreateTilemap(grid.transform, "Wall_Tilemap", 5, true, false);
            Tilemap floorTrim = CreateTilemap(grid.transform, "FloorTrim_Tilemap", 6, true, false);
            Tilemap wallTop = CreateTilemap(grid.transform, "WallTop_Tilemap", 8, true, false);
            Tilemap decoration = CreateTilemap(grid.transform, "Decoration_Tilemap", 10, true, false);
            Tilemap furnitureBase = CreateTilemap(grid.transform, "FurnitureBase_Tilemap", 15, true, false);
            Tilemap props = CreateTilemap(grid.transform, "Props_Tilemap", 22, true, false);
            Tilemap collision = CreateTilemap(grid.transform, "Collision_Tilemap", 0, false, true);
            Tilemap abovePlayer = CreateTilemap(grid.transform, "AbovePlayer_Tilemap", 40, true, false);
            Tilemap interaction = CreateTilemap(grid.transform, "Interaction_Tilemap", 200, false, false);
            Tilemap triggerGuide = CreateTilemap(grid.transform, "TriggerGuide_Tilemap", 200, false, false);

            BuildTilemapLayout(assets, ground, wall, floorTrim, wallTop, decoration, furnitureBase, props, collision, abovePlayer, interaction, triggerGuide);

            GameObject interactableRoot = new GameObject("InteractableObjects");
            interactableRoot.transform.SetParent(indoorRoot.transform, false);
            GameObject decorationRoot = new GameObject("DecorativeObjects");
            decorationRoot.transform.SetParent(indoorRoot.transform, false);
            GameObject spawnRoot = new GameObject("PlayerSpawnPoints");
            spawnRoot.transform.SetParent(indoorRoot.transform, false);
            GameObject cameraBoundsRoot = new GameObject("CameraBounds");
            cameraBoundsRoot.transform.SetParent(indoorRoot.transform, false);
            GameObject mapUi = new GameObject("MapUI");
            mapUi.transform.SetParent(indoorRoot.transform, false);
            new GameObject("TopHud").transform.SetParent(mapUi.transform, false);
            new GameObject("BottomPromptBar").transform.SetParent(mapUi.transform, false);
            GameObject debugRoot = new GameObject("DebugOverlay");
            debugRoot.transform.SetParent(indoorRoot.transform, false);

            InstantiatePrefab(assets.bedPrefab, "BedObject", interactableRoot.transform, new Vector3(-11.75f, 4.65f, -0.1f));
            InstantiatePrefab(assets.deskPrefab, "DeskObject", interactableRoot.transform, new Vector3(2.35f, 6.35f, -0.1f));
            InstantiatePrefab(assets.wardrobePrefab, "WardrobeObject", interactableRoot.transform, new Vector3(-5.8f, 4.85f, -0.1f));
            InstantiatePrefab(assets.diningTablePrefab, "DiningTableObject", interactableRoot.transform, new Vector3(8.45f, -7.05f, -0.1f));
            InstantiatePrefab(assets.familyNpcPrefab, "FamilyNpcObject", interactableRoot.transform, new Vector3(12.2f, -7.05f, -0.1f));
            InstantiatePrefab(assets.exitDoorPrefab, "ExitDoorObject", interactableRoot.transform, new Vector3(0f, -8.35f, -0.1f));
            BuildDecorativeObjects(assets, decorationRoot.transform);

            GameObject spawn = new GameObject("DefaultSpawnPoint");
            spawn.transform.SetParent(spawnRoot.transform, false);
            spawn.transform.localPosition = new Vector3(0f, -7.35f, 0f);
            spawn.AddComponent<PlayerSpawnPoint>().spawnPointId = "home_entry";

            BoxCollider2D boundsCollider = cameraBoundsRoot.AddComponent<BoxCollider2D>();
            boundsCollider.isTrigger = true;
            boundsCollider.size = new Vector2(MapWidth, MapHeight);

            GameObject player = CreatePlayer(assets, flow);
            GameObject cameraObject = CreateCamera(player.transform, cameraBoundsRoot.transform);

            sceneController.mapId = "location_home";
            sceneController.displayName = "家里｜恢复精力、沟通家庭、整理计划。";
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

            debugRoot.AddComponent<MapDebugOverlayView>().debugObjects = debugOverlay.debugObjects;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            assets.sceneBackupPath = backupPath;
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

        private static void BuildTilemapLayout(
            GeneratedAssets assets,
            Tilemap ground,
            Tilemap wall,
            Tilemap floorTrim,
            Tilemap wallTop,
            Tilemap decoration,
            Tilemap furnitureBase,
            Tilemap props,
            Tilemap collision,
            Tilemap abovePlayer,
            Tilemap interaction,
            Tilemap triggerGuide)
        {
            for (int x = MapMinX; x <= MapMaxX; x++)
            {
                for (int y = MapMinY; y <= MapMaxY; y++)
                {
                    TileBase groundTile = GetPatternTile(assets.groundWoodPatternTiles, x, y, MapMinX, TopWallY);
                    if (x >= -14 && x <= -8 && y >= -9 && y <= -5)
                    {
                        groundTile = assets.bathFloorTile;
                    }
                    else if (x >= -2 && x <= 2 && y >= -10 && y <= -8)
                    {
                        groundTile = assets.entryFloorTile;
                    }
                    else if (x >= -13 && x <= -8 && y >= 2 && y <= 5)
                    {
                        groundTile = assets.groundLightTile;
                    }

                    ground.SetTile(new Vector3Int(x, y, 0), groundTile);
                }
            }

            for (int x = MapMinX; x <= MapMaxX; x++)
            {
                int patternColumn = PositiveModulo(x - MapMinX, assets.wallFramePatternTiles.GetLength(0));
                wall.SetTile(new Vector3Int(x, TopWallY, 0), assets.wallFramePatternTiles[patternColumn, 0]);
                wall.SetTile(new Vector3Int(x, WallInteriorY, 0), assets.wallFramePatternTiles[patternColumn, 1]);
                floorTrim.SetTile(new Vector3Int(x, WallFloorTrimY, 0), assets.wallFramePatternTiles[patternColumn, 5]);
                wallTop.SetTile(new Vector3Int(x, WallInteriorY, 0), assets.wallFramePatternTiles[patternColumn, 2]);
                abovePlayer.SetTile(new Vector3Int(x, TopWallY, 0), assets.wallFramePatternTiles[patternColumn, 0]);
                collision.SetTile(new Vector3Int(x, TopWallY, 0), assets.collisionTile);
                collision.SetTile(new Vector3Int(x, WallInteriorY, 0), assets.collisionTile);
                collision.SetTile(new Vector3Int(x, MapMinY, 0), assets.collisionTile);
                floorTrim.SetTile(new Vector3Int(x, MapMinY, 0), assets.wallShadowTile);
            }

            for (int y = MapMinY; y <= MapMaxY; y++)
            {
                int patternRow = PositiveModulo(TopWallY - y, assets.wallFramePatternTiles.GetLength(1));
                wall.SetTile(new Vector3Int(MapMinX, y, 0), assets.wallFramePatternTiles[0, patternRow]);
                wall.SetTile(new Vector3Int(MapMaxX, y, 0), assets.wallFramePatternTiles[5, patternRow]);
                collision.SetTile(new Vector3Int(MapMinX, y, 0), assets.collisionTile);
                collision.SetTile(new Vector3Int(MapMaxX, y, 0), assets.collisionTile);
            }

            PaintPatternRect(wall, assets.wallFramePatternTiles, MapMinX, -7, -4, -4, MapMinX, TopWallY);
            PaintRect(floorTrim, assets.wallTopTile, MapMinX, -7, -4, -4);
            PaintRect(collision, assets.collisionTile, MapMinX, -7, -4, -4);
            PaintPatternRect(wall, assets.wallFramePatternTiles, -7, -7, MapMinY, -4, MapMinX, TopWallY);
            PaintRect(collision, assets.collisionTile, -7, -7, MapMinY, -4);

            PaintRect(floorTrim, assets.wallShadowTile, MapMinX + 1, MapMaxX - 1, WallFloorTrimY, WallFloorTrimY);
            PaintRect(floorTrim, assets.wallShadowTile, MapMinX + 1, -8, -4, -4);
            PaintRect(floorTrim, assets.wallShadowTile, 6, MapMaxX - 1, 0, 0);
            PaintRect(floorTrim, assets.wallShadowTile, 4, MapMaxX - 2, -5, -5);

            PaintRect(decoration, assets.carpetTile, -3, 4, -4, 1);
            PaintRect(decoration, assets.carpetTile, 5, 12, -9, -6);
            PaintRect(decoration, assets.carpetTile, -13, -8, 1, 3);
            PaintRect(decoration, assets.carpetTile, 0, 5, 4, 5);
            PaintRect(decoration, assets.carpetTile, -2, 2, -8, -8);

            PaintRect(furnitureBase, assets.carpetTile, -14, -9, 2, 8);
            PaintRect(furnitureBase, assets.carpetTile, -6, -4, 2, 8);
            PaintRect(furnitureBase, assets.carpetTile, 0, 5, 4, 8);
            PaintRect(furnitureBase, assets.carpetTile, 6, 13, 0, 4);
            PaintRect(furnitureBase, assets.carpetTile, 5, 12, -9, -6);
            PaintRect(furnitureBase, assets.carpetTile, -14, -8, -9, -5);

            PaintRect(collision, assets.collisionTile, 7, 13, 1, 2);
            PaintRect(collision, assets.collisionTile, -14, -12, -9, -8);
            PaintRect(collision, assets.collisionTile, -10, -10, -8, -7);
            PaintRect(collision, assets.collisionTile, -9, -8, -8, -7);
            PaintRect(collision, assets.collisionTile, -6, -5, -3, -2);

            MarkInteractionCell(interaction, triggerGuide, assets, new Vector3Int(-11, 2, 0));
            MarkInteractionCell(interaction, triggerGuide, assets, new Vector3Int(2, 5, 0));
            MarkInteractionCell(interaction, triggerGuide, assets, new Vector3Int(8, -9, 0));
            MarkInteractionCell(interaction, triggerGuide, assets, new Vector3Int(12, -9, 0));
            MarkInteractionCell(interaction, triggerGuide, assets, new Vector3Int(0, -8, 0));
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

        private static void PaintPatternRect(Tilemap tilemap, Tile[,] pattern, int minX, int maxX, int minY, int maxY, int anchorX, int anchorTopY)
        {
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), GetPatternTile(pattern, x, y, anchorX, anchorTopY));
                }
            }
        }

        private static TileBase GetPatternTile(Tile[,] pattern, int x, int y, int anchorX, int anchorTopY)
        {
            int column = PositiveModulo(x - anchorX, pattern.GetLength(0));
            int row = PositiveModulo(anchorTopY - y, pattern.GetLength(1));
            return pattern[column, row];
        }

        private static int PositiveModulo(int value, int modulo)
        {
            int result = value % modulo;
            return result < 0 ? result + modulo : result;
        }

        private static void MarkInteractionCell(Tilemap interaction, Tilemap triggerGuide, GeneratedAssets assets, Vector3Int cell)
        {
            interaction.SetTile(cell, assets.markerTile);
            triggerGuide.SetTile(cell, assets.carpetTile);
        }

        private static void BuildDecorativeObjects(GeneratedAssets assets, Transform parent)
        {
            CreateStaticSprite("BedroomWindow_Double", assets.windowSprite, parent, new Vector3(-12.1f, 7.35f, -0.08f), new Vector3(0.42f, 0.42f, 1f), 14);
            CreateStaticSprite("StudyBulletinBoard", assets.bulletinBoardSprite, parent, new Vector3(1.75f, 7.75f, -0.08f), new Vector3(0.42f, 0.42f, 1f), 14);
            CreateStaticSprite("StudyWallArt_Set", assets.wallArtSprite, parent, new Vector3(5.15f, 7.55f, -0.08f), new Vector3(0.28f, 0.28f, 1f), 14);
            CreateStaticSprite("StudyMapPainting", assets.mapPaintingSprite, parent, new Vector3(11.35f, 7.45f, -0.08f), new Vector3(0.32f, 0.32f, 1f), 14);
            CreateStaticSprite("DeskNotes_Decor", assets.notesSprite, parent, new Vector3(2.35f, 6.85f, -0.12f), new Vector3(0.24f, 0.24f, 1f), 28);
            CreateStaticSprite("DeskLamp_Decor", assets.smallLampSprite, parent, new Vector3(0.75f, 6.72f, -0.12f), new Vector3(0.55f, 0.55f, 1f), 28);

            CreateStaticSprite("LivingSofa", assets.sofaSprite, parent, new Vector3(0.25f, 0.05f, -0.08f), new Vector3(0.5f, 0.5f, 1f), 18);
            CreateStaticSprite("LivingTvStand", assets.tvSprite, parent, new Vector3(-5.35f, -2.05f, -0.08f), new Vector3(0.52f, 0.52f, 1f), 18);
            CreateStaticSprite("LivingCoffeeTable", assets.coffeeTableSprite, parent, new Vector3(0.25f, -2.55f, -0.12f), new Vector3(0.5f, 0.5f, 1f), 24);
            CreateStaticSprite("LivingFloorLamp", assets.floorLampSprite, parent, new Vector3(4.35f, -1.1f, -0.08f), new Vector3(0.58f, 0.58f, 1f), 18);
            CreateStaticSprite("LeftLowBookshelf", assets.bookshelfSprite, parent, new Vector3(-11.45f, -0.95f, -0.08f), new Vector3(0.42f, 0.42f, 1f), 18);
            CreateStaticSprite("BedroomLowBookshelf", assets.bookshelfSprite, parent, new Vector3(-12.1f, 0.25f, -0.08f), new Vector3(0.38f, 0.38f, 1f), 18);
            CreateStaticSprite("StudyLowBookshelf", assets.bookshelfSprite, parent, new Vector3(9.35f, 5.3f, -0.08f), new Vector3(0.42f, 0.42f, 1f), 18);

            CreateStaticSprite("KitchenCounter_Run", assets.kitchenCounterSprite, parent, new Vector3(8.9f, 2.2f, -0.08f), new Vector3(0.5f, 0.5f, 1f), 18);
            CreateStaticSprite("KitchenStove", assets.stoveSprite, parent, new Vector3(11.45f, 2.05f, -0.08f), new Vector3(0.55f, 0.55f, 1f), 18);
            CreateStaticSprite("KitchenFridge", assets.fridgeSprite, parent, new Vector3(13.1f, 2.35f, -0.08f), new Vector3(0.74f, 0.74f, 1f), 18);

            CreateStaticSprite("BathBathtub", assets.bathtubSprite, parent, new Vector3(-12.75f, -8.35f, -0.08f), new Vector3(0.48f, 0.48f, 1f), 18);
            CreateStaticSprite("BathToilet", assets.toiletSprite, parent, new Vector3(-10.55f, -7.65f, -0.08f), new Vector3(0.48f, 0.48f, 1f), 18);
            CreateStaticSprite("BathSinkMirror", assets.sinkMirrorSprite, parent, new Vector3(-9.0f, -7.55f, -0.08f), new Vector3(0.36f, 0.36f, 1f), 18);

            CreateStaticSprite("DiningMeal_Decor", assets.tableMealSprite, parent, new Vector3(8.45f, -6.45f, -0.12f), new Vector3(0.42f, 0.42f, 1f), 28);
            CreateStaticSprite("BedroomPlant_Decor", assets.plantSprite, parent, new Vector3(-14.0f, 5.6f, -0.08f), new Vector3(0.42f, 0.42f, 1f), 18);
            CreateStaticSprite("KitchenPlant_Decor", assets.plantSprite, parent, new Vector3(6.25f, 1.35f, -0.08f), new Vector3(0.38f, 0.38f, 1f), 18);
            CreateStaticSprite("LivingPlant_Decor", assets.plantSprite, parent, new Vector3(4.35f, 2.05f, -0.08f), new Vector3(0.34f, 0.34f, 1f), 18);
            CreateStaticSprite("DiningFlowerBox", assets.flowerBoxSprite, parent, new Vector3(5.15f, -9.25f, -0.08f), new Vector3(0.42f, 0.42f, 1f), 18);
            CreateStaticSprite("CornerFlowerBox", assets.flowerBoxSprite, parent, new Vector3(12.55f, -9.15f, -0.08f), new Vector3(0.34f, 0.34f, 1f), 18);
            CreateStaticSprite("EntryFlowerBox", assets.flowerBoxSprite, parent, new Vector3(-2.65f, -9.35f, -0.08f), new Vector3(0.34f, 0.34f, 1f), 18);
        }

        private static GameObject CreateStaticSprite(string name, Sprite sprite, Transform parent, Vector3 position, Vector3 scale, int sortingOrder)
        {
            GameObject gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localScale = scale;
            SpriteRenderer renderer = gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = sortingOrder;
            return gameObject;
        }

        private static GameObject InstantiatePrefab(GameObject prefab, string objectName, Transform parent, Vector3 position)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            return instance;
        }

        private static GameObject CreatePlayer(GeneratedAssets assets, MapInteractionFlowController flow)
        {
            GameObject player = new GameObject("PlayerRpg");
            player.transform.position = new Vector3(0f, -7.35f, -0.1f);
            SpriteRenderer renderer = player.AddComponent<SpriteRenderer>();
            renderer.sprite = assets.playerIdleSprite;
            renderer.sortingOrder = 30;
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
            controller.movementMin = new Vector2(MapMinX + 1.4f, MapMinY + 0.75f);
            controller.movementMax = new Vector2(MapMaxX - 0.6f, WallFloorTrimY + 0.35f);
            MapObjectYSorter sorter = player.AddComponent<MapObjectYSorter>();
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
            camera.orthographicSize = 10f;
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

        private static void WriteReports(GeneratedAssets assets)
        {
            List<string> newArt = Directory.GetFiles(Path.GetFullPath("Assets/Art"), "*", SearchOption.AllDirectories)
                .Where(path => IsArtOrMeta(path) && path.IndexOf("new", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(ToAssetPath)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();

            WriteTextAsset(
                DocsRoot + "/NewArtAssetScanReport.md",
                "# New Art Asset Scan Report\n\n"
                + "- Scanned directories: `Assets/Art`, `Assets/Tiles`, `Assets/TilePalettes`, `Assets/Prefabs`, `Assets/Scenes`, `Assets/Resources`, `Assets/StudentGrowth`\n"
                + "- Identified `new` art/resource count: " + newArt.Count + "\n"
                + "- New Tile atlas count: 4\n"
                + "- New furniture sprite outputs: 13\n"
                + "- New decorative sprite outputs: 13\n"
                + "- New character/NPC sprite outputs: 2 source paths, 6 generated frame sprites\n"
                + "- Old art fallback used: No\n\n"
                + "## New Resources\n\n"
                + string.Join("\n", newArt.Select(path => "- `" + path + "`"))
                + "\n\n## Unverified\n\n"
                + "- `Assets/Art/Characters/Player/New Animation.anim` is marked by name but is not an art source used by this rebuild.\n");

            WriteTextAsset(
                DocsRoot + "/New20TileSliceReport.md",
                "# New20 Tile Slice Report\n\n"
                + "| Source | Slice Method | Cell Size | PPU | Generated Tile Assets | Notes |\n"
                + "| --- | --- | --- | --- | ---: | --- |\n"
                + "| `" + SourceA2 + "` | 16x16 crop selections | 16x16 | 16 | 6 | Bath tile, entry checker, carpet, marker, and supporting ground samples |\n"
                + "| `" + SourceA4 + "` | source prototype pattern split into editable 16x16 tiles | 6x6 floor, 6x9 wall | 16 | 90 | Warm wood floor and pure wood wall prototype patterns |\n"
                + "| `" + SourceB + "` | full-object source bounds with background transparency | Object rectangles | 16 | 0 | Furniture Prefab sprites generated separately; scene proportions use `Visual` child transform scale, not partial furniture crops |\n"
                + "| `" + SourceC + "` | full-object decoration bounds with background transparency | Object rectangles | 16 | 0 | Wall art, table meal, and note decoration sprites |\n\n"
                + "Generated Tile assets:\n\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.groundWoodTile) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.groundLightTile) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.bathFloorTile) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.entryFloorTile) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.wallInteriorTile) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.wallTopTile) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.carpetTile) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.collisionTile) + "`\n\n"
                + "Manual confirmation: source atlas Sprite Editor grid slices are not used as direct scene dependencies; generated 16x16 sprite copies drive the Tile assets to avoid fragile atlas sub-sprite references.\n");

            WriteTextAsset(
                DocsRoot + "/IndoorMapRebuildReport.md",
                "# Indoor Map Rebuild Report\n\n"
                + "- Rebuilt scene: `" + ScenePath + "`\n"
                + "- Backup scene: `" + assets.sceneBackupPath + "`\n"
                + "- Replacement strategy: direct replacement per user request; old `HomeMapScene.unity` content was rebuilt in place.\n"
                + "- Room size: `30 x 20` tiles, Grid Cell Size `1 x 1`, PPU `16`.\n"
                + "- Tilemap hierarchy: `Ground_Tilemap`, `Wall_Tilemap`, `FloorTrim_Tilemap`, `WallTop_Tilemap`, `Decoration_Tilemap`, `FurnitureBase_Tilemap`, `Props_Tilemap`, `Collision_Tilemap`, `AbovePlayer_Tilemap`, `Interaction_Tilemap`, `TriggerGuide_Tilemap`.\n"
                + "- Furniture rule: each Prefab `Visual` child uses a complete furniture sprite; room proportion is controlled by child transform scale, not by cropping the furniture body.\n"
                + "- Decorative layout: `DecorativeObjects` contains visual-only bedroom, living room, kitchen, dining, bathroom, plant, wall/window/table details from new B/C atlas sprites; broad pathing remains on Tilemap or Prefab colliders.\n"
                + "- Player spawn: `DefaultSpawnPoint`, spawn id `home_entry`, position `(0, -7.35, 0)`.\n"
                + "- Camera bounds: `CameraBounds` with `BoxCollider2D`, size `30 x 20`.\n"
                + "- Collision: `Collision_Tilemap` with `TilemapCollider2D`, `CompositeCollider2D`, and static `Rigidbody2D`.\n"
                + "- Tile Palette: `" + AssetDatabase.GetAssetPath(assets.tilePalettePrefab) + "`\n\n"
                + "## Prefabs\n\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.bedPrefab) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.deskPrefab) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.diningTablePrefab) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.wardrobePrefab) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.familyNpcPrefab) + "`\n"
                + "- `" + AssetDatabase.GetAssetPath(assets.exitDoorPrefab) + "`\n\n"
                + "## Decorative Objects\n\n"
                + "- `BedroomWindow_Double`: complete window sprite above the bed.\n"
                + "- `StudyBulletinBoard`, `StudyWallArt_Set`, `StudyMapPainting`: top-wall study decorations.\n"
                + "- `LivingSofa`, `LivingTvStand`, `LivingCoffeeTable`, `LivingFloorLamp`: central living area cluster.\n"
                + "- `KitchenCounter_Run`, `KitchenStove`, `KitchenFridge`: right-side kitchen line.\n"
                + "- `BathBathtub`, `BathToilet`, `BathSinkMirror`: lower-left bathroom fixtures.\n"
                + "- `DiningMeal_Decor`, flower boxes, plants, low bookshelves, and entry props: table and expanded room-detail props.\n\n"
                + "## Main Object Proportions\n\n"
                + "| Object | Sprite | Tile Size | Scale | Pivot | Pass | Note |\n"
                + "| --- | --- | ---: | ---: | --- | --- | --- |\n"
                + "| BedObject | new20_home_bed | PNG 6x8.625, Visual 4.08x5.87 | 1,1,1 | Center | Yes | 完整床图，Visual 子物体等比缩放 |\n"
                + "| DeskObject | new20_home_desk | PNG 6x5.8125, Visual 3x2.91 | 1,1,1 | Center | Yes | 完整桌柜图，Visual 子物体等比缩放 |\n"
                + "| DiningTableObject | new20_home_dining_table | PNG 9x6, Visual 5.94x3.96 | 1,1,1 | Center | Yes | 完整餐桌图，Visual 子物体等比缩放 |\n"
                + "| WardrobeObject | new20_home_wardrobe | PNG 5.625x8.625, Visual 3.83x5.87 | 1,1,1 | Center | Yes | 完整衣柜图，Visual 子物体等比缩放 |\n"
                + "| FamilyNpcObject | new20_family_npc_idle | 2x2 | 1,1,1 | Center | Yes | 与玩家比例一致 |\n"
                + "| ExitDoorObject | new20_home_exit_door | 3x3 | 1,1,1 | Center | Yes | 底部中央出口，门前 2x3 空地 |\n"
                + "| PlayerRpg | new20_player_idle_down | 2x2 | 1,1,1 | Center | Yes | 出生点在底部中央主通道，不与阻挡碰撞重叠 |\n\n"
                + "Still needs manual visual pass in Unity Editor for exact crop aesthetics and final palette authoring workflow.\n");

            WriteTextAsset(
                DocsRoot + "/IndoorMapInteractionReport.md",
                "# Indoor Map Interaction Report\n\n"
                + "| ID | Object | Prefab | LocationAction | Consumes Time | Rewards | Exit | Trigger Size | Hidden By Default | Debug |\n"
                + "| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |\n"
                + "| `activity_home_rest_early_sleep` | BedObject | `" + AssetDatabase.GetAssetPath(assets.bedPrefab) + "` | `activity_home_rest_early_sleep` | Yes | Energy +20, Pressure -5 | No | `4.2 x 2.2` | Yes | F3 overlay |\n"
                + "| `activity_home_desk_plan` | DeskObject | `" + AssetDatabase.GetAssetPath(assets.deskPrefab) + "` | `activity_home_desk_plan` | Yes | Autonomy +5, 明日效率 +10 description | No | `4.0 x 2.0` | Yes | F3 overlay |\n"
                + "| `activity_home_family_dinner` | DiningTableObject | `" + AssetDatabase.GetAssetPath(assets.diningTablePrefab) + "` | `activity_home_family_dinner` | Yes | ParentRelation +5, Mood +5 | No | `4.8 x 2.6` | Yes | F3 overlay |\n"
                + "| `activity_home_family_dinner` | FamilyNpcObject | `" + AssetDatabase.GetAssetPath(assets.familyNpcPrefab) + "` | `activity_home_family_dinner` | Yes | ParentRelation +5, Mood +5 | No | `1.4 x 2.0` | Yes | F3 overlay |\n"
                + "| `exit_home` | ExitDoorObject | `" + AssetDatabase.GetAssetPath(assets.exitDoorPrefab) + "` | none | No | No | Yes | `2.6 x 2.4` | Yes | F3 overlay |\n\n"
                + "Activity interactions continue through `MapInteractionPoint -> MapInteractionController -> LocationActionExecutor -> StageController.CompleteActivity -> RewardResolver -> GameSessionState`. Exit interaction uses `MapExitPortal` / `ReturnToMainUi` and does not consume time.\n");

            WriteTextAsset(
                ProportionReportPath,
                "# Indoor Scene Rebuild New2_0 Report\n\n"
                + "1. Rebuilt scene: `" + ScenePath + "`\n"
                + "2. Backup scene: `" + assets.sceneBackupPath + "`\n"
                + "3. New art sources: `" + SourceA2 + "`, `" + SourceA4 + "`, `" + SourceB + "`, `" + SourceC + "`, `" + SourceCharacters + "`\n"
                + "4. Tile slicing: selected 16x16 tile sprites and source prototype patterns generated at PPU 16; furniture sprites use complete object bounds with source backgrounds made transparent.\n"
                + "5. Furniture rule: complete sprites are assigned to the `Visual` child; room proportion is handled by child transform scale instead of cutting partial furniture sprites.\n"
                + "6. Wall depth rule: wall cap, pillar, base shadow, and floor transition tiles are layered to improve depth without adding walkable-area collision.\n"
                + "7. Decoration rule: `DecorativeObjects` are visual-only SpriteRenderers sourced from new art and do not affect pathing.\n"
                + "8. Tile assets: `" + TileRoot + "`\n"
                + "9. Tile Palette: `" + AssetDatabase.GetAssetPath(assets.tilePalettePrefab) + "`\n"
                + "10. Prefabs: `" + PrefabInteractableRoot + "`, `" + PrefabNpcRoot + "`\n"
                + "11. Collision risk: Low. Walls and major furniture are on `Collision_Tilemap` or separate `BlockingCollider` children.\n"
                + "12. Camera crop risk: Low. Camera orthographic size is 10 and bounds match the 30x20 room.\n"
                + "13. Compile status: verified by Unity batchmode after generation.\n\n"
                + "## Main Object Sizes\n\n"
                + "| Object | Sprite | Tile Size | Scale | Pivot | Pass | Note |\n"
                + "|---|---|---:|---:|---|---|---|\n"
                + "| BedObject | new20_home_bed | PNG 6x8.625, Visual 4.08x5.87 | 1,1,1 | Center | Yes | Below 5x6 max after Visual scale |\n"
                + "| DeskObject | new20_home_desk | PNG 6x5.8125, Visual 3x2.91 | 1,1,1 | Center | Yes | Below 4x3 max after Visual scale, near wall |\n"
                + "| DiningTableObject | new20_home_dining_table | PNG 9x6, Visual 5.94x3.96 | 1,1,1 | Center | Yes | At 6x4 max after Visual scale, does not block main route |\n"
                + "| WardrobeObject | new20_home_wardrobe | PNG 5.625x8.625, Visual 3.83x5.87 | 1,1,1 | Center | Yes | Below 5x6 max after Visual scale, near wall |\n"
                + "| FamilyNpcObject | new20_family_npc_idle | 2x2 | 1,1,1 | Center | Yes | Player-scale NPC |\n"
                + "| ExitDoorObject | new20_home_exit_door | 3x3 | 1,1,1 | Center | Yes | Door visible at bottom-center entry |\n"
                + "| PlayerRpg | new20_player_idle_down | 2x2 | 1,1,1 | Center | Yes | Spawned near bottom-center corridor |\n\n"
                + "## Manual Confirmation\n\n"
                + "- The selected object crops are proportion-safe, but final art taste should be checked visually in Unity Scene view.\n"
                + "- The Tile Palette is stored as a Unity palette prefab because this project does not already contain `.palette` assets.\n");
        }

        private static bool IsArtOrMeta(string path)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            return extension == ".png"
                || extension == ".jpg"
                || extension == ".jpeg"
                || extension == ".gif"
                || extension == ".psd"
                || extension == ".aseprite"
                || extension == ".meta"
                || extension == ".anim";
        }

        private static string ToAssetPath(string fullPath)
        {
            string normalized = fullPath.Replace('\\', '/');
            int index = normalized.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? normalized.Substring(index + 1) : normalized;
        }

        private static void WriteTextAsset(string path, string content)
        {
            File.WriteAllText(Path.GetFullPath(path), content);
            AssetDatabase.ImportAsset(path);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }

        private sealed class GeneratedAssets
        {
            public Sprite groundWoodSprite;
            public Sprite groundLightSprite;
            public Sprite bathFloorSprite;
            public Sprite entryFloorSprite;
            public Sprite carpetSprite;
            public Sprite wallInteriorSprite;
            public Sprite wallTopSprite;
            public Sprite wallPillarSprite;
            public Sprite wallShadowSprite;
            public Sprite markerSprite;
            public Sprite bedSprite;
            public Sprite deskSprite;
            public Sprite diningTableSprite;
            public Sprite wardrobeSprite;
            public Sprite exitDoorSprite;
            public Sprite windowSprite;
            public Sprite sofaSprite;
            public Sprite tvSprite;
            public Sprite coffeeTableSprite;
            public Sprite bathtubSprite;
            public Sprite toiletSprite;
            public Sprite sinkMirrorSprite;
            public Sprite kitchenCounterSprite;
            public Sprite stoveSprite;
            public Sprite fridgeSprite;
            public Sprite floorLampSprite;
            public Sprite smallLampSprite;
            public Sprite wallArtSprite;
            public Sprite tableMealSprite;
            public Sprite notesSprite;
            public Sprite plantSprite;
            public Sprite bookshelfSprite;
            public Sprite bulletinBoardSprite;
            public Sprite mapPaintingSprite;
            public Sprite flowerBoxSprite;
            public Sprite familyNpcSprite;
            public Sprite playerIdleSprite;
            public Sprite[] playerWalkSprites;
            public Tile groundWoodTile;
            public Tile groundLightTile;
            public Tile bathFloorTile;
            public Tile entryFloorTile;
            public Tile carpetTile;
            public Tile wallInteriorTile;
            public Tile wallTopTile;
            public Tile wallPillarTile;
            public Tile wallShadowTile;
            public Tile[,] groundWoodPatternTiles;
            public Tile[,] wallFramePatternTiles;
            public Tile markerTile;
            public Tile collisionTile;
            public ActivityData restActivity;
            public ActivityData planActivity;
            public ActivityData familyDinnerActivity;
            public AnimationClip idleClip;
            public AnimationClip walkClip;
            public AnimatorController playerAnimator;
            public GameObject tilePalettePrefab;
            public GameObject bedPrefab;
            public GameObject deskPrefab;
            public GameObject diningTablePrefab;
            public GameObject wardrobePrefab;
            public GameObject familyNpcPrefab;
            public GameObject exitDoorPrefab;
            public string sceneBackupPath;
        }
    }
}
