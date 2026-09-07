# ClubRoomMapScene V2 editing guide

## Layout ownership

`Assets/Scenes/StudentGrowth/Maps/ClubRoomMapScene.unity` is the final layout source.

- Edit facility transforms, colliders, sprites and interactions in the formal scene.
- `ClubRoomMapSceneBuilder` is first-build only and refuses to overwrite the formal scene.
- Before applying broad automated changes, duplicate the scene and validate the copy.
- Normalized V2 sprites live under `Assets/Art/New2_0/Club/Final/Facilities`.

## Editable roots

- `ClubRoomRpgRoot/EditableClubFacilities`: floor furniture and small props.
- `ClubRoomRpgRoot/Visual/EditableClubWallDecor`: notice board and whiteboard.
- `ClubRoomRpgRoot/Collision`: wall and closed-door collision.
- `ClubRoomRpgRoot/InteractableObjects`: exit, board, sofa and meeting-table edge interactions.

## V2 invariants

- Final facility renderers use 64 PPU, Point filtering, no mipmaps, no compression and Transform Scale 1.
- Ordinary facility bottom-center contact points use the 16px / 0.25-world-unit grid.
- The meeting table and six chairs move as one group; the table is the group grid reference.
- The entrance spawn is outside the door collider and exit interaction range.
- The closed visual door blocks movement; exiting requires facing the door and pressing the interaction key.
- The meeting table uses four short-range edge anchors rather than one large center radius.

## Validation

Use `Tools > Student Growth > Club > Validate V2 Finalization` or run the ClubRoom edit-mode tests.
