# Indoor Map Interaction Report

| ID | Object | Prefab | LocationAction | Consumes Time | Rewards | Exit | Trigger Size | Hidden By Default | Debug |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| `activity_home_rest_early_sleep` | BedObject | `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_BedObject.prefab` | `activity_home_rest_early_sleep` | Yes | Energy +20, Pressure -5 | No | `4.2 x 2.2` | Yes | F3 overlay |
| `activity_home_desk_plan` | DeskObject | `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_DeskObject.prefab` | `activity_home_desk_plan` | Yes | Autonomy +5, 明日效率 +10 description | No | `4.0 x 2.0` | Yes | F3 overlay |
| `activity_home_family_dinner` | DiningTableObject | `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_DiningTableObject.prefab` | `activity_home_family_dinner` | Yes | ParentRelation +5, Mood +5 | No | `4.8 x 2.6` | Yes | F3 overlay |
| `activity_home_family_dinner` | FamilyNpcObject | `Assets/Prefabs/StudentGrowth/NPC/New2_0/New20_FamilyNpcObject.prefab` | `activity_home_family_dinner` | Yes | ParentRelation +5, Mood +5 | No | `1.4 x 2.0` | Yes | F3 overlay |
| `exit_home` | ExitDoorObject | `Assets/Prefabs/StudentGrowth/Interactables/New2_0/New20_ExitDoorObject.prefab` | none | No | No | Yes | `2.6 x 2.4` | Yes | F3 overlay |

Activity interactions continue through `MapInteractionPoint -> MapInteractionController -> LocationActionExecutor -> StageController.CompleteActivity -> RewardResolver -> GameSessionState`. Exit interaction uses `MapExitPortal` / `ReturnToMainUi` and does not consume time.
