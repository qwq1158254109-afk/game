# HomeMap Build Report

## Added Scene Structure

- `HomeMapRpgRoot`
- `HomeTilemapGrid`
- `Ground_Tilemap`
- `Wall_Tilemap`
- `Decoration_Tilemap`
- `Collision_Tilemap` with `TilemapCollider2D`, `CompositeCollider2D`, and static `Rigidbody2D`
- `AbovePlayer_Tilemap`

## Added Objects

- `BedObject`: `MapInteractionPoint` + `MapInteractionTarget`, prompt `按 E：早点休息｜消耗 1 时间｜精力 +20｜压力 -5`
- `DeskObject`: `MapInteractionPoint` + `MapInteractionTarget`, prompt `按 E：整理计划｜消耗 1 时间｜自主 +5｜明日效率 +10`
- `DiningTableObject`: `MapInteractionPoint` + `MapInteractionTarget`, prompt `按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5`
- `FamilyNpcObject`: linked to the family dinner interaction
- `ExitDoorObject`: `MapExitPortal`, `ReturnToMainUi`, no activity reward or time cost

## Runtime Flow

Formal activities still execute through `MapInteractionPoint -> MapInteractionController -> LocationActionExecutor -> StageController.CompleteActivity -> RewardResolver -> GameSessionState`.
The exit uses `MapExitPortal -> MapInteractionController -> LocationSceneRouter.ReturnToStageMap()` or `SceneTransitionController.ShowMap()` and does not call activity completion.

## UI And Debug

- Top HUD uses `FullscreenMapHudView` and includes location, time, energy, mood, parent relation, and money.
- Bottom prompt defaults to `靠近可互动位置后按 E 交互。`.
- `MapInteractionFlowController` handles Idle, Nearby, Confirming, Executing, Completed, Locked, and Debug states.
- F3 toggles debug trigger range visibility.

## Runtime Services

- `FullscreenMapSceneController` can create fallback runtime services when the scene is opened directly.
- Fallback services configure `StageController`, `ActivityController`, `LocationActionExecutor`, `SceneTransitionController`, and `LocationSceneRouter` without bypassing the normal activity completion path.

## Generated Assets

- Activity assets: `Assets/Data/StudentGrowth/HomeMap/HomeRestEarlySleep.asset`, `Assets/Data/StudentGrowth/HomeMap/HomeDeskPlan.asset`, `Assets/Data/StudentGrowth/HomeMap/HomeFamilyDinner.asset`
- Activity asset type: `HomeMapActivityData : ActivityData`, used only to give Unity a valid script asset for generated home-map activity files.
- Animator: `Assets/Animations/HomeMap/HomePlayer.controller`
- Animation clips: `Assets/Animations/HomeMap/HomePlayer_Idle.anim`, `Assets/Animations/HomeMap/HomePlayer_Walk.anim`
