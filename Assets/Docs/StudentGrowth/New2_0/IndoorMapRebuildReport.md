# Indoor Map Rebuild Report

- Rebuilt scene: `Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity`
- Backup scene: `Assets/Scenes/StudentGrowth/Maps/Backups/HomeMapScene_Backup_20260522_190333.unity`
- Replacement strategy: direct replacement per user request; old `HomeMapScene.unity` content was rebuilt in place.
- Room size: `30 x 20` tiles, Grid Cell Size `1 x 1`, PPU `16`.
- Tilemap hierarchy: `Ground_Tilemap`, `Wall_Tilemap`, `FloorTrim_Tilemap`, `WallTop_Tilemap`, `Decoration_Tilemap`, `FurnitureBase_Tilemap`, `Props_Tilemap`, `Collision_Tilemap`, `AbovePlayer_Tilemap`, `Interaction_Tilemap`, `TriggerGuide_Tilemap`.
- Furniture rule: each Prefab `Visual` child uses a complete furniture sprite; room proportion is controlled by child transform scale, not by cropping the furniture body.
- Decorative layout: `DecorativeObjects` contains visual-only bedroom, living room, kitchen, dining, bathroom, plant, wall/window/table details from new B/C atlas sprites; broad pathing remains on Tilemap or Prefab colliders.
- Player spawn: `DefaultSpawnPoint`, spawn id `home_entry`, position `(0, -7.35, 0)`.
- Camera bounds: `CameraBounds` with `BoxCollider2D`, size `30 x 20`.
- Collision: `Collision_Tilemap` with `TilemapCollider2D`, `CompositeCollider2D`, and static `Rigidbody2D`.
- Tile Palette: `Assets/TilePalettes/New2_0/New20_HomeIndoorPalette.prefab`

## Prefabs

- `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_BedObject.prefab`
- `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_DeskObject.prefab`
- `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_DiningTableObject.prefab`
- `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_WardrobeObject.prefab`
- `Assets/Prefabs/StudentGrowth/NPC/New2_0/New20_FamilyNpcObject.prefab`
- `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_ExitDoorObject.prefab`

## Decorative Objects

- `BedroomWindow_Double`: complete window sprite above the bed.
- `StudyBulletinBoard`, `StudyWallArt_Set`, `StudyMapPainting`: top-wall study decorations.
- `LivingSofa`, `LivingTvStand`, `LivingCoffeeTable`, `LivingFloorLamp`: central living area cluster.
- `KitchenCounter_Run`, `KitchenStove`, `KitchenFridge`: right-side kitchen line.
- `BathBathtub`, `BathToilet`, `BathSinkMirror`: lower-left bathroom fixtures.
- `DiningMeal_Decor`, flower boxes, plants, low bookshelves, and entry props: table and expanded room-detail props.

## Main Object Proportions

| Object | Sprite | Tile Size | Scale | Pivot | Pass | Note |
| --- | --- | ---: | ---: | --- | --- | --- |
| BedObject | new20_home_bed | PNG 6x8.625, Visual 4.08x5.87 | 1,1,1 | Center | Yes | 完整床图，Visual 子物体等比缩放 |
| DeskObject | new20_home_desk | PNG 6x5.8125, Visual 3x2.91 | 1,1,1 | Center | Yes | 完整桌柜图，Visual 子物体等比缩放 |
| DiningTableObject | new20_home_dining_table | PNG 9x6, Visual 5.94x3.96 | 1,1,1 | Center | Yes | 完整餐桌图，Visual 子物体等比缩放 |
| WardrobeObject | new20_home_wardrobe | PNG 5.625x8.625, Visual 3.83x5.87 | 1,1,1 | Center | Yes | 完整衣柜图，Visual 子物体等比缩放 |
| FamilyNpcObject | new20_family_npc_idle | 2x2 | 1,1,1 | Center | Yes | 与玩家比例一致 |
| ExitDoorObject | new20_home_exit_door | 3x3 | 1,1,1 | Center | Yes | 底部中央出口，门前 2x3 空地 |
| PlayerRpg | new20_player_idle_down | 2x2 | 1,1,1 | Center | Yes | 出生点在底部中央主通道，不与阻挡碰撞重叠 |

Still needs manual visual pass in Unity Editor for exact crop aesthetics and final palette authoring workflow.
