# New20 Tile Slice Report

| Source | Slice Method | Cell Size | PPU | Generated Tile Assets | Notes |
| --- | --- | --- | --- | ---: | --- |
| `Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets/0_Inside_A2_new.png` | 16x16 crop selections | 16x16 | 16 | 6 | Bath tile, entry checker, carpet, marker, and supporting ground samples |
| `Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets/0_Inside_A4_new.png` | source prototype pattern split into editable 16x16 tiles | 6x6 floor, 6x9 wall | 16 | 90 | Warm wood floor and pure wood wall prototype patterns |
| `Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets/0_Inside_B_new.png` | full-object source bounds with background transparency | Object rectangles | 16 | 0 | Furniture Prefab sprites generated separately; scene proportions use `Visual` child transform scale, not partial furniture crops |
| `Assets/Art/Modern_Interiors/4_RPG_MAKER_MV/Tilesets/0_Inside_C_new.png` | full-object decoration bounds with background transparency | Object rectangles | 16 | 0 | Wall art, table meal, and note decoration sprites |

Generated Tile assets:

- `Assets/Tiles/New2_0/Ground/New20_Tile_Ground_Wood_00.asset`
- `Assets/Tiles/New2_0/Ground/New20_Tile_Ground_Light_00.asset`
- `Assets/Tiles/New2_0/Ground/New20_Tile_Ground_Bath_00.asset`
- `Assets/Tiles/New2_0/Ground/New20_Tile_Ground_Entry_00.asset`
- `Assets/Tiles/New2_0/Wall/New20_Tile_Wall_Interior_00.asset`
- `Assets/Tiles/New2_0/Wall/New20_Tile_Wall_Top_00.asset`
- `Assets/Tiles/New2_0/Decoration/New20_Tile_Decoration_Carpet_00.asset`
- `Assets/Tiles/New2_0/Collision/New20_Tile_Collision_Block_00.asset`

Manual confirmation: source atlas Sprite Editor grid slices are not used as direct scene dependencies; generated 16x16 sprite copies drive the Tile assets to avoid fragile atlas sub-sprite references.
