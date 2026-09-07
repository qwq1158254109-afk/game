# Indoor Scene Rebuild New2_0 Report

1. Rebuilt scene: `Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity`
2. Backup scene: `Assets/Scenes/StudentGrowth/Maps/Backups/HomeMapScene_Backup_20260522_190333.unity`
3. New art sources: `Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets/0_Inside_A2_new.png`, `Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets/0_Inside_A4_new.png`, `Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets/0_Inside_B_new.png`, `Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets/0_Inside_C_new.png`, `Assets/Art/Characters/ModernInteriors/RpgMakerMV/new_characters_48x48.png`
4. Tile slicing: selected 16x16 tile sprites and source prototype patterns generated at PPU 16; furniture sprites use complete object bounds with source backgrounds made transparent.
5. Furniture rule: complete sprites are assigned to the `Visual` child; room proportion is handled by child transform scale instead of cutting partial furniture sprites.
6. Wall depth rule: wall cap, pillar, base shadow, and floor transition tiles are layered to improve depth without adding walkable-area collision.
7. Decoration rule: `DecorativeObjects` are visual-only SpriteRenderers sourced from new art and do not affect pathing.
8. Tile assets: `Assets/Tiles/New2_0`
9. Tile Palette: `Assets/TilePalettes/New2_0/New20_HomeIndoorPalette.prefab`
10. Prefabs: `Assets/Prefabs/StudentGrowth/Interactables/New2_0`, `Assets/Prefabs/StudentGrowth/NPC/New2_0`
11. Collision risk: Low. Walls and major furniture are on `Collision_Tilemap` or separate `BlockingCollider` children.
12. Camera crop risk: Low. Camera orthographic size is 10 and bounds match the 30x20 room.
13. Compile status: verified by Unity batchmode after generation.

## Main Object Sizes

| Object | Sprite | Tile Size | Scale | Pivot | Pass | Note |
|---|---|---:|---:|---|---|---|
| BedObject | new20_home_bed | PNG 6x8.625, Visual 4.08x5.87 | 1,1,1 | Center | Yes | Below 5x6 max after Visual scale |
| DeskObject | new20_home_desk | PNG 6x5.8125, Visual 3x2.91 | 1,1,1 | Center | Yes | Below 4x3 max after Visual scale, near wall |
| DiningTableObject | new20_home_dining_table | PNG 9x6, Visual 5.94x3.96 | 1,1,1 | Center | Yes | At 6x4 max after Visual scale, does not block main route |
| WardrobeObject | new20_home_wardrobe | PNG 5.625x8.625, Visual 3.83x5.87 | 1,1,1 | Center | Yes | Below 5x6 max after Visual scale, near wall |
| FamilyNpcObject | new20_family_npc_idle | 2x2 | 1,1,1 | Center | Yes | Player-scale NPC |
| ExitDoorObject | new20_home_exit_door | 3x3 | 1,1,1 | Center | Yes | Door visible at bottom-center entry |
| PlayerRpg | new20_player_idle_down | 2x2 | 1,1,1 | Center | Yes | Spawned near bottom-center corridor |

## Manual Confirmation

- The selected object crops are proportion-safe, but final art taste should be checked visually in Unity Scene view.
- The Tile Palette is stored as a Unity palette prefab because this project does not already contain `.palette` assets.
