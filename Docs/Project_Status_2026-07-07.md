# 项目状态检查报告

检查时间：2026-07-07 19:16（UTC+08:00）

## 1. 基本结论

项目当前处于 V0.2 RPG 地图交互与 UI 整理阶段，主要可见工作集中在家地图、商店地图、地图交互、地图 HUD、像素 UI 资源替换和旧 UI 资源归档。

当前工程不是干净工作区，Git 状态显示存在大量新增、修改和已删除索引项，建议在继续大范围开发前先确认这些变更是否都应保留，并建立一次稳定基线。

本次检查以本地文件、资源目录、脚本结构、Prefab/Scene 文件和 Git 状态为主。当前工具环境未暴露 Unity MCP Console 读取接口，因此本报告未能直接读取 Unity Console 实时日志；需要后续在 Unity Editor 中补一次 Console 检查和 Play Mode 冒烟测试。

## 2. 工程与包状态

- Unity 版本：`6000.4.6f1 (0b051c2e5d54)`
- `Assets` 下当前可检索文件数：`7555`
- C# 脚本约 `117` 个
- PNG 图片资源约 `2968` 个
- Prefab 资源约 `16` 个
- `.asset` 资源约 `126` 个

主要 Unity 包：

- `com.unity.inputsystem` `1.19.0`
- `com.unity.render-pipelines.universal` `17.4.0`
- `com.unity.ugui` `2.0.0`
- `com.unity.test-framework` `1.6.0`
- 2D 相关包：2D Animation、PSD Importer、Sprite、SpriteShape、Tilemap Extras、2D Tilemap Editor
- 本地包：`com.seanba.super-tiled2unity`

## 3. Git 工作区状态

当前 `git status --short` 分类统计：

- `A `：`7953`
- `AM`：`61`
- `AD`：`93`
- `??`：`14`

风险判断：

- 工作区变更量很大，像是一次资源导入或项目基线重建后的状态。
- `AD` 表示有文件在索引中被新增后又从工作区删除，`Docs` 下也存在这类历史报告索引项，需要确认是否清理暂存区或重新生成。
- 建议先不要继续做大范围文件迁移，优先确认当前 Git 状态是否符合预期。

## 4. 场景与地图状态

当前主要地图场景位于 `Assets/Scenes/StudentGrowth/Maps`：

- `HomeMapScene.unity`，约 `942.3 KB`
- `ShopMapScene.unity`，约 `358 KB`
- `StageHubMapScene.unity`，约 `44 KB`

该目录下还有约 `29` 个 `.unity` 备份场景，主要是 `HomeMapScene` 和 `ShopMapScene` 的历史备份。备份对回滚有价值，但长期放在 `Assets` 内会增加项目体积、导入时间和 Git 噪音，建议后续统一整理到文档化的备份策略中。

与 V0.2 目标相关的地图脚本已经比较完整，包括：

- `RpgPlayerController`
- `PlayerFacingInteractor`
- `MapInteractionController`
- `MapInteractionPoint`
- `MapInteractionFlowController`
- `MapExitPortal`
- `MapLocationPortal`
- `MapObjectYSorter`
- `PlayerSpawnPoint`
- `StageMapLocationAvailabilityService`

这说明项目已从纯菜单式模拟器向地图驱动交互推进，当前重点应继续围绕家地图闭环、交互提示、活动消耗和状态刷新做稳定化。

## 5. UI 与美术资源状态

地图 UI Prefab 位于 `Assets/Prefabs/StudentGrowth/UI/Map`，当前包含：

- `FullscreenMapCanvas.prefab`
- `FullscreenMapHud.prefab`
- `MapActivityResultView.prefab`
- `MapDialogueView.prefab`
- `MapFunctionMenuView.prefab`
- `MapInteractionConfirmView.prefab`
- `MapInteractionPrompt.prefab`
- `MapShopView.prefab`

像素 UI 资源当前分组：

- `Assets/Art/Pixel/UI_SpecSheets`：6 张 UI 规格图
- `Assets/Art/Pixel/UI_Old`：约 69 张旧 UI PNG，已单独归档为 old
- `Assets/Art/Pixel/UI_New`：约 27 张新 UI PNG
- `Assets/Resources/StudentGrowth/UI/StudentGrowthPixelTheme.asset`：当前 UI 主题资源

6 张 UI 规格图：

- `ui_spec_01_hud_panels.png`
- `ui_spec_02_button_four_states.png`
- `ui_spec_03_task_panels.png`
- `ui_spec_04_prompt_bars.png`
- `ui_spec_05_quickbar.png`
- `ui_spec_06_menu_windows.png`

最近 HUD 重构后的静态状态：

- HUD 面板尺寸约 `432 x 156`
- 底部资源条已从 HUD 中拆出，资源面板约 `264 x 42`
- 标题区与右侧图标区已分离，间距约 `34 px`
- 图标间距约 `8 px`
- 精力、压力、心情三行已统一行高，进度条起点和长度一致

注意：当前 `Assets/Scripts/StudentGrowth/UI` 与 `Assets/Prefabs/StudentGrowth/UI/Map` 仍主要使用 `UnityEngine.UI.Text`，没有发现 TextMeshPro 组件引用。若后续要求严格保留或迁移 TMP，需要单独排期，避免在 UI 外观调整中顺手改动文本技术栈。

## 6. 脚本结构状态

当前 `Assets/Scripts/StudentGrowth` 下约 `92` 个非 meta 文件，主要目录：

- `Map`
- `UI`
- `Scene`
- `Flow`
- `Editor`

关键系统分布：

- 地图与交互：`Assets/Scripts/StudentGrowth/Map`
- 地图 UI：`Assets/Scripts/StudentGrowth/UI/Map`
- 场景切换：`Assets/Scripts/StudentGrowth/Scene`
- 活动执行入口：`Assets/Scripts/StudentGrowth/Flow/LocationActionExecutor.cs`
- 编辑器生成与审计工具：`Assets/Scripts/StudentGrowth/Editor`

结构整体符合“地图交互优先”的 V0.2 方向。后续应继续避免把新规则写进 UI 脚本，活动消耗、状态变化和时间推进应保持由 Controller 或 Flow 层统一处理。

## 7. 数据与资源组织状态

当前没有发现 `Assets/ScriptableObjects` 目录。项目规范中建议后续把活动、卡牌、物品、地点、结局等数据逐步放入 ScriptableObject 或稳定 ID 数据结构。

当前 `Assets/Resources` 中主要可见资源是：

- `Assets/Resources/StudentGrowth/UI/StudentGrowthPixelTheme.asset`

风险判断：

- UI 主题集中在 Resources 中是可用的，但长期看应避免所有运行时数据都依赖 Resources。
- 活动、地点、卡牌、物品等数据结构如果继续散落在脚本或场景对象中，会影响后续存档、扩展和测试。

## 8. 测试状态

`Assets/Tests/EditMode` 下已有较完整的 EditMode 测试覆盖，包含：

- 活动与奖励：`ActivityControllerTests`、`LocationActionExecutorTests`、`RewardResolverTests`
- 卡牌与卡组：`CardEffectResolverTests`、`DeckControllerTests`
- 阶段与挑战：`StageControllerTests`、`ChallengeControllerTests`
- 地图与交互：`RpgMapSceneComponentTests`、`MapFacingInteractionUtilityTests`、`MapInteractionPromptFormatterTests`
- 场景切换：`LocationSceneRouterTests`、`SceneTransitionControllerTests`
- UI 主题审计：`UiThemeAuditUtilityTests`、`UiThemeAuditEditorUtilityTests`

本次没有运行完整 EditMode 测试，也没有进入 Play Mode。由于当前请求是状态整理文档，建议下一步单独执行：

- Unity Console 检查
- EditMode 全量测试
- `HomeMapScene` Play Mode 冒烟测试
- HUD、资源条、任务面板、提示条、快捷栏、菜单窗口的分辨率适配检查

## 9. 当前主要风险

1. Git 工作区过大且不干净，继续开发前需要确认新增资源、已删除索引项和修改文件是否都符合预期。
2. Unity Console 未在本次检查中通过 MCP 实时读取，编译状态需要在 Editor 中确认。
3. 地图 UI 当前主要使用 `UnityEngine.UI.Text`，与“保留 TextMeshPro 文本”的目标存在技术栈差异，需要明确是保留现状还是迁移 TMP。
4. `Assets/Scenes/StudentGrowth/Maps/Backups` 内备份场景较多，可能造成项目体积和版本控制噪音。
5. 缺少集中 `ScriptableObjects` 数据目录，后续活动、地点、卡牌、道具和结局扩展可能继续分散。
6. UI 资源已分组，但仍需逐项确认 Sprite 切片、九宫格 Border、Point Filter、Compression None、MipMap Off 等导入设置。

## 10. 建议下一步

1. 先确认 Git 工作区状态，决定哪些新增、修改、删除应纳入当前基线。
2. 在 Unity Editor 中打开项目，检查 Console 是否有 Error。
3. 运行 EditMode 测试，优先确认地图交互、场景切换、活动执行和 UI 主题审计。
4. 对 `HomeMapScene` 做 Play Mode 冒烟测试：移动、交互提示、床/书桌/电脑活动、门口返回或切换、状态刷新。
5. 继续按 V0.2 优先级完善家地图闭环，不急于扩展大量新地图。
6. 明确 UI 文本技术路线：保留现有 `UnityEngine.UI.Text`，或单独排期迁移到 TextMeshPro。
7. 将活动、地点、卡牌、物品等数据逐步整理为稳定 ID 的数据资产。

## 11. 关键路径索引

- 地图场景：`Assets/Scenes/StudentGrowth/Maps`
- 地图 UI Prefab：`Assets/Prefabs/StudentGrowth/UI/Map`
- 地图运行时代码：`Assets/Scripts/StudentGrowth/Map`
- 地图 UI 代码：`Assets/Scripts/StudentGrowth/UI/Map`
- 场景切换代码：`Assets/Scripts/StudentGrowth/Scene`
- UI 新规格图：`Assets/Art/Pixel/UI_SpecSheets`
- 旧 UI 归档：`Assets/Art/Pixel/UI_Old`
- UI 主题资源：`Assets/Resources/StudentGrowth/UI/StudentGrowthPixelTheme.asset`
- EditMode 测试：`Assets/Tests/EditMode`
