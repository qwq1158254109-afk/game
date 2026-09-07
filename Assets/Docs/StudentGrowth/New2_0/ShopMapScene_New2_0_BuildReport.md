# ShopMapScene New2_0 Build Report

- Scene: `Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity`
- Backup scene: `Assets/Scenes/StudentGrowth/Maps/Backups/ShopMapScene_Backup_20260521_233218.unity`
- Reference image: `Assets/Art/Pixel/Backgrounds/NewShopSources/new_shop_reference.png`
- Scene strategy: editable Tilemap plus independent Sprite objects. The whole reference image is not used as a scene background.
- Grid: `1 x 1`, room plan: `24 x 16` tiles.
- Tilemaps: `Ground_Tilemap`, `Wall_Tilemap`, `FloorTrim_Tilemap`, `WallTop_Tilemap`, `Decoration_Tilemap`, `FurnitureBase_Tilemap`, `Collision_Tilemap`, `AbovePlayer_Tilemap`.
- Main editable props are under `ShopMapRpgRoot/EditableShopProps` and `ShopMapRpgRoot/EditableWallDecor`.
- Interactions: `ShopCounterObject` opens placeholder shop interaction, `ExitDoorObject` returns to stage map without time cost.

## Generated Art

- Prop sprites: `Assets/Art/New2_0/Props/Shop`
- Tile sprites: `Assets/Art/New2_0/Tilesets/Shop`
- Tile assets: `Assets/Tiles/New2_0/Shop`
- Data assets: `Assets/Data/StudentGrowth/New2_0/ShopMap`

## Notes

- Large shelves and cabinets are complete cropped sprites from the provided shop item sheet, not partial furniture fragments.
- Floor/wall tiles are derived from the provided empty shop room source so they remain editable as Tilemap cells.
