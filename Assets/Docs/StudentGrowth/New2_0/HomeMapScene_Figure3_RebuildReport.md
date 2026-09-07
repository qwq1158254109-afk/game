# HomeMapScene Figure 3 Rebuild Report

- Scene: `Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity`
- Backup scene: `Assets/Scenes/StudentGrowth/Maps/Backups/HomeMapScene_Backup_20260529_160734.unity`
- Space source: `Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png`
- Item source: `Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_item.png`
- Visual reference: `Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_reference.png`
- Strategy: the room shell uses the empty-room image, furniture uses independent cropped sprites from the item sheet, and collision/interactions remain editable Unity objects.
- Required RPG objects kept: `PlayerRpg`, `BedObject`, `DeskObject`, `ComputerStudyPoint`, `DiningTableObject`, `FamilyNpcObject`, `ExitDoorObject`, `Collision_Tilemap`, `Interaction_Tilemap`.
- Activities: rest, desk plan, computer study, and family dinner all execute through `MapInteractionTarget` fallback activities, so time/reward flow stays on the existing controller path.
- Camera bounds remain `30 x 20` for compatibility with the existing validator, while the visual room art is 1448x1086 at PPU 64.

## Generated Assets

- Props: `Assets/Art/Pixel/Generated/HomeReference/Props`
- Exit door animation frames: `home_ref_exit_door.png`, `home_ref_exit_door_open_01.png`, `home_ref_exit_door_open_02.png`, `home_ref_exit_door_open_03.png`
- Marker/collision tiles: `Assets/Art/Pixel/Generated/HomeReference/Tiles`
- Computer activity: `Assets/Data/StudentGrowth/New2_0/HomeMap/New20_HomeComputerStudy.asset`

## Manual Check Points

- Play `HomeMapScene`, walk with WASD/arrow keys, hold Shift to run.
- Check that bed, desk/computer, kitchen dinner point, family NPC, and exit door show prompts when approached.
- Press E on the exit door and confirm the door opens before returning to the stage map.
- Press E on each activity and confirm time/reward HUD updates through the existing flow.
- Confirm the player cannot pass through walls, stairs, the bed, sofa, wardrobe, bookcases, kitchen counters, or major furniture.
