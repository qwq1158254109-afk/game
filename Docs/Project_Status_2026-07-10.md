# 项目状态检查报告

检查时间：2026-07-10 13:06（UTC+08:00）

检查范围：Unity Editor 实时状态、Console、Build Settings、主要地图 Scene、地图 UI Prefab、像素 UI 资源、运行时代码、数据资产、EditMode 测试目录和 Git 工作区。

## 1. 执行摘要

项目目前仍处于 **V0.2 RPG 地图交互重构阶段**，但已经不再是纯菜单原型。家地图、商店地图和阶段地图均已进入 Build Settings，家地图和商店地图具备玩家、碰撞、动画、面向交互、交互目标、交互流程和地图 HUD 等主要结构。

当前状态可以概括为：

- **运行与编译：可用。** Unity 当前未在编译或 Play Mode 中；Unity MCP 动态检查脚本编译并执行成功。
- **Console：无 Error。** 检查到 2 条 Warning，均来自运行环境或 MCP 安全识别，不是游戏逻辑错误。
- **V0.2 功能：主体结构已落地，完整闭环尚缺 Play Mode 验收。** 文件与场景组件证明移动、交互、活动执行、状态刷新和场景切换代码已经存在，但本次没有实际游玩验证。
- **地图 UI 换肤：完成度较高。** 地图 UI Prefab 已引用 6 张规格图切片，按钮四状态齐全，可拉伸面板已大量使用 Sliced Image，地图 UI 中未发现 `UI_Old` Sprite 引用。
- **文本技术栈：与既定 TMP 目标不一致。** 当前地图 UI 仍全部使用 `UnityEngine.UI.Text`，没有 TextMeshPro 组件。
- **数据驱动：已有基础但不完整。** 已有 8 个 `ActivityData` 和 1 个 `LocationActionData` 资产，其他阶段、卡牌、事件、结局和地点数据仍主要依赖代码中的回退样例数据。
- **最大工程风险：Git 尚无首个提交。** `main` 分支没有提交，约八千项文件处于暂存、修改、删除或未跟踪状态，当前无法可靠比较版本、回滚或定位回归。

综合判断：项目已达到“可继续做 V0.2 闭环验收”的阶段，不建议此时扩展更多地图或复杂剧情。优先事项应是建立 Git 基线、跑完整测试、完成家地图 Play Mode 验收，然后再处理 TMP 与数据资产化。

## 2. Unity 与工程基础状态

| 项目 | 当前状态 |
| --- | --- |
| Unity 版本 | `6000.4.6f1 (0b051c2e5d54)` |
| 当前打开场景 | `Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity` |
| Editor 状态 | 非 Play Mode、非编译中 |
| Build Settings | 4 个启用场景 |
| `Assets` 文件总数 | 7,555 |
| C# 脚本 | 117 个，其中 `Assets/Scripts/StudentGrowth` 下 90 个 |
| PNG | 2,968 个 |
| Prefab | 16 个，其中 `Assets/Prefabs/StudentGrowth` 下 14 个 |
| `.asset` | 126 个 |
| Assembly Definition | 3 个 |

Build Settings 当前启用：

1. `Assets/Scenes/SampleScene.unity`
2. `Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity`
3. `Assets/Scenes/StudentGrowth/Maps/ShopMapScene.unity`
4. `Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity`

主要包版本：

- Input System `1.19.0`
- URP `17.4.0`
- UGUI `2.0.0`
- Test Framework `1.6.0`
- 2D Animation `14.0.3`
- 2D SpriteShape `14.0.1`
- 2D Tilemap Extras `7.0.1`
- Unity AI Assistant `2.11.0-pre.2`
- 本地包 `com.seanba.super-tiled2unity`

## 3. Unity Console 状态

Unity MCP 实时读取结果：

- Error：`0`
- Warning：`2`

两条 Warning 分别为：

1. Unity 正以管理员权限运行。Unity 官方不建议这种运行方式，因为项目脚本和原生库会获得过高系统权限。
2. Unity AI MCP 无法为 Codex 可执行文件采集有效的 Windows 签名。堆栈位于 `com.unity.ai.assistant` 包的安全识别模块中。

这两条 Warning 当前不指向游戏代码错误，但建议以后使用普通权限启动 Unity，并在 MCP 包更新后复查签名警告。

## 4. 场景与 V0.2 地图状态

Unity MCP 对三个主要地图 Scene 进行了只读组件检查，未发现 Missing Script。

| 场景 | GameObject | Collider2D | Animator | 交互目标 | 交互流程 | Missing Script |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| HomeMapScene | 383 | 36 | 1 | 6 | 1 | 0 |
| ShopMapScene | 337 | 21 | 2 | 2 | 1 | 0 |
| StageHubMapScene | 272 | 0 | 0 | 6 | 0 | 0 |

### HomeMapScene

已确认存在：

- `RpgPlayerController`
- `PlayerFacingInteractor`
- `MapInteractionFlowController`
- 6 个 `MapInteractionTarget`
- 36 个 2D Collider
- 玩家 Animator
- 地图 Scene Controller
- Canvas / 地图 HUD

这说明家地图的移动、碰撞、面向检测、交互入口和 UI 承载结构已经形成。是否存在边角穿模、交互距离不合适、门口出生点错误或活动后状态未刷新，仍需 Play Mode 验证。

### ShopMapScene

已确认存在玩家、21 个 Collider、2 个 Animator、2 个交互目标和完整交互流程。商店地图已经具备基本可交互场景结构，但其交互内容明显少于家地图，当前适合作为 V0.3 多场景扩展的试验场，不应优先于家地图闭环验收。

### StageHubMapScene

当前阶段地图有玩家对象和 6 个交互目标，但没有 Collider、Animator 和 `MapInteractionFlowController`。从结构看，它更接近地点选择 Hub，而不是可自由探索的 RPG 地图。若设计目标就是菜单式地点选择，这一状态可以接受；若计划让玩家在该场景行走，则仍缺移动环境和统一交互流程。

### 场景备份

`Assets/Scenes/StudentGrowth/Maps/Backups` 中有 29 个 `.unity` 备份场景。长期放在 `Assets` 中会增加导入、索引、版本控制和场景搜索噪音。应先建立可靠 Git 基线，再决定是否将备份移出 `Assets`；本次未删除或移动任何备份。

## 5. 核心系统结构

项目已经形成较明确的模块边界：

- 地图移动与交互：`Assets/Scripts/StudentGrowth/Map`
- 活动统一执行入口：`Assets/Scripts/StudentGrowth/Flow/LocationActionExecutor.cs`
- 场景路由与淡入淡出：`Assets/Scripts/StudentGrowth/Scene`
- 地图 UI：`Assets/Scripts/StudentGrowth/UI/Map`
- 通用 UI 主题组件：`Assets/Scripts/StudentGrowth/UI/Core`、`UI/Components`
- 游戏状态与规则：`GameSessionState`、`PlayerState`、`StageController`、`ActivityController`
- 编辑器生成与审计工具：`Assets/Scripts/StudentGrowth/Editor`

`LocationActionExecutor` 已承担地点解锁检查、行动条件检查、时间检查、活动解析、奖励结算和后续 UI/战斗/对话/商店分支标记，符合“UI 不直接修改核心状态”的方向。

需要关注的结构问题：

- `RpgPlayerController` 仍使用 Public 字段，并在运行时通过 `FindObjectOfType` 补找交互流程；功能可用，但后续应逐步改为序列化注入或场景控制器统一绑定。
- HUD 当前每 `0.2` 秒轮询刷新。规模较小时可接受，后续状态系统稳定后可改为事件驱动，减少无变化刷新。
- 输入兼容逻辑同时探测 Legacy Input 和新 Input System，保证了兼容性，但尚未形成统一 Input Action 资产绑定。

## 6. UI 与像素素材状态

### 6.1 规格图导入设置

6 张 UI 规格图均已确认：

- Texture Type：Sprite 2D/UI
- Sprite Mode：Multiple（`spriteMode: 2`）
- Filter Mode：Point（`filterMode: 0`）
- Compression：None / Uncompressed（`textureCompression: 0`）
- MipMap：Off（`enableMipMap: 0`）

规格图：

- `ui_spec_01_hud_panels.png`
- `ui_spec_02_button_four_states.png`
- `ui_spec_03_task_panels.png`
- `ui_spec_04_prompt_bars.png`
- `ui_spec_05_quickbar.png`
- `ui_spec_06_menu_windows.png`

`StudentGrowthPixelTheme.asset` 已分别绑定 HUD、按钮四状态、任务面板、提示条、快捷栏和菜单窗口切片。

### 6.2 地图 UI Prefab 审计

Unity MCP 对 `Assets/Prefabs/StudentGrowth/UI/Map` 下 8 个 Prefab 的实际组件和 Sprite 引用进行了检查：

- `FullscreenMapCanvas.prefab`：15/15 个按钮使用 Sprite Swap，15/15 具备 hover / pressed / disabled Sprite。
- `FullscreenMapHud.prefab`：5/5 个按钮四状态完整。
- `MapFunctionMenuView.prefab`：5/5 个按钮四状态完整。
- `MapInteractionConfirmView.prefab`：2/2 个按钮四状态完整。
- `MapActivityResultView.prefab`、`MapDialogueView.prefab`、`MapShopView.prefab`：各自按钮四状态完整。
- 地图 UI Prefab 已大量使用 Sliced Image；`FullscreenMapCanvas` 中检测到 40 个 Sliced Image。
- 所有地图 UI Prefab 均引用规格图切片，未发现来自 `Assets/Art/Pixel/UI_Old` 的 Sprite 引用。

旧 UI 已集中在 `Assets/Art/Pixel/UI_Old`，其中约 69 张 PNG；新 UI 在 `Assets/Art/Pixel/UI_New` 中约 27 张 PNG。

### 6.3 HUD 状态

`FullscreenMapHudView` 当前已实现简化 HUD 布局：

- HUD 约 `432 x 156`
- 地点标题和右侧图标区分离
- 精力、压力、心情三行使用统一起点、行距和进度条长度
- 资源面板约 `264 x 42`，与主 HUD 分离
- 图标间距约 `8 px`
- 主要文字距边框大于 `12 px`

代码仍保留任务、快捷栏和资源字段，以兼容现有数据绑定和 Prefab 引用；显示层通过 `showResourcePanel`、`showActionPanel` 和可见性控制简化 HUD。

### 6.4 仍需处理的 UI 问题

地图 UI Prefab 中检测到的文本全部是 `UnityEngine.UI.Text`：

- `FullscreenMapCanvas.prefab`：52 个 Legacy Text，0 个 TMP
- `FullscreenMapHud.prefab`：26 个 Legacy Text，0 个 TMP
- 其他地图 UI Prefab 同样为 Legacy Text

因此，“保留 TextMeshPro 文本”的目标与当前工程实际不一致。现状不是 TMP 被替换掉，而是这些地图 UI 目前就没有使用 TMP。建议把 TMP 迁移作为单独任务处理，不要和纯外观换肤混在同一次改动中。

本次没有对像素图做逐像素颜色检查，因此“青色底是否在所有切片 Alpha 中完全清除”仍需通过 Sprite Editor 或透明背景预览人工确认。

## 7. 数据资产状态

代码已经定义并支持以下 ScriptableObject 类型：

- Stage、Activity、Card、Challenge、MiniGame
- Item、Event、Ending
- Location、LocationAction
- MapSceneConfig、StageMap、GameBootstrapperData

但实际资产化程度较低：

| 数据类型 | 当前资产数 |
| --- | ---: |
| ActivityData | 8 |
| LocationActionData | 1 |
| StageData | 0 |
| CardData | 0 |
| ChallengeData | 0 |
| MiniGameData | 0 |
| ItemData | 0 |
| EventData | 0 |
| EndingData | 0 |
| LocationData | 0 |
| MapSceneConfigData | 0 |
| StageMapData | 0 |
| GameBootstrapperData | 0 |

现有资产主要集中在：

- `Assets/Data/StudentGrowth/HomeMap`
- `Assets/Data/StudentGrowth/New2_0/HomeMap`
- `Assets/Data/StudentGrowth/New2_0/ShopMap`

这意味着核心规则虽然支持数据驱动，但大量游戏内容仍依赖 `MvpSampleDataFactory` 或场景内直接引用的回退数据。短期不影响 V0.2 家地图闭环，进入 V0.3/V0.4 前应逐步把地点、阶段、卡牌、事件和结局转成稳定 ID 的资产。

## 8. 测试状态

`Assets/Tests/EditMode` 中有 22 个测试脚本，检测到约 100 个 `[Test]` / `[TestCase]` / `[UnityTest]` 标记。覆盖范围包括：

- 活动执行、奖励、资源扣除
- 卡牌效果与卡组
- 阶段与挑战
- 玩家状态边界
- 地图面向交互、提示格式、地图 Scene 组件
- 场景路由与切换
- 游戏启动与样例数据
- UI 主题审计

本次检查没有执行完整 EditMode Test Runner，也没有进入 Play Mode。当前“可编译、Console 无 Error”不能替代功能测试，因此测试结论应视为：**有较好的测试代码基础，但当前通过率未知。**

建议下一轮验证顺序：

1. 执行全部 EditMode 测试并保存 XML 结果。
2. 在 `HomeMapScene` 验证移动、跑步、碰撞和四方向动画。
3. 验证床、书桌、电脑、家庭互动和门口交互提示。
4. 验证活动条件失败时不扣资源，成功时正确扣时间并刷新精力、压力、心情。
5. 验证返回 StageHub 或切换 Shop 后状态不丢失、出生点正确。
6. 检查 16:9、16:10 和常见窗口分辨率下 HUD、任务面板、提示条、快捷栏与菜单窗口。

## 9. Git 与版本控制风险

检查开始时 Git 状态统计：

| 状态 | 数量 |
| --- | ---: |
| `A ` | 7,953 |
| `AM` | 61 |
| `AD` | 93 |
| `??` | 15 |

当前分支为 `main`，但仓库 **没有任何提交**。这会造成以下问题：

- 无法区分原始项目、用户改动和生成工具改动。
- 无法可靠回滚场景、Prefab 或 `.meta` 文件。
- `AD` 文件表示文件先进入索引后又从工作区删除，容易在首次提交时误带删除状态。
- 大量美术、包样例、备份场景和生成物可能被一次性提交，仓库体积会快速膨胀。

当前最重要的工程动作不是继续大规模改 UI，而是先审阅 `.gitignore`、暂存区和 `AD` 项，建立一个可说明来源的首个基线提交。不要直接执行清空暂存区或删除文件，先确认这些改动是否属于预期项目内容。

## 10. V0.2 验收矩阵

| 验收项 | 当前判断 | 依据 / 缺口 |
| --- | --- | --- |
| 可以进入家地图 | 基本具备 | Scene 已加入 Build Settings；需 Play 验证入口 |
| 玩家能移动 | 已实现 | `RpgPlayerController` 和玩家对象存在；需手感验证 |
| 不能穿墙或主要家具 | 基本具备 | Home 有 36 个 Collider；需实机走边检查 |
| 床、电脑、书桌、门显示提示 | 基本具备 | Home 有 6 个交互目标和提示流程；需逐个验证 |
| 交互触发活动或切换 | 已实现结构 | `MapInteractionFlowController`、`LocationActionExecutor`、场景路由存在 |
| 活动扣时间 / 体力 | 已实现结构 | Controller 和测试代码存在；本次未运行测试 |
| 活动改变属性 | 已实现结构 | Activity/Reward 流程存在；需 Play 验证显示刷新 |
| 状态 UI 刷新 | 已实现 | HUD 绑定 `GameSessionState` 并定时刷新 |
| 时间耗尽进入结算 | 已实现结构 | StageController 和测试代码存在；本次未执行 |
| 返回后数据不丢失 | 已实现结构 | 共享 SessionState 与路由存在；需跨场景验证 |
| 无明显 Console Error | 已确认 | 当前 Error 为 0 |

结论：V0.2 已进入验收阶段，但尚不能标记为完成。最关键缺口是完整 EditMode 测试结果和家地图 Play Mode 闭环验证。

## 11. 优先级建议

### P0：立即处理

1. 审核 Git 暂存区、`AD` 和未跟踪文件，建立首个可回滚基线。
2. 执行全部 EditMode 测试，修复任何红灯后再继续扩展。
3. 完成 `HomeMapScene` 的 V0.2 Play Mode 验收。

### P1：V0.2 收尾

1. 对 HUD 和资源条进行多分辨率视觉复查。
2. 在 Sprite Editor 中确认切片 Border 与透明背景，重点检查青色底残留。
3. 明确 StageHub 是地点选择界面还是可行走地图，避免两套交互范式长期并存。
4. 确认旧 UI 仅作为归档，不再被 Scene、Prefab 或主题资产引用。

### P2：进入 V0.3 前

1. 单独规划 Legacy Text 到 TextMeshPro 的迁移。
2. 将 Location、Stage、Card、Event、Ending 和 Bootstrapper 数据逐步资产化。
3. 把 HUD 状态刷新从轮询逐步调整为事件驱动。
4. 整理 `Assets` 内 29 个备份场景，减少导入和版本控制噪音。

## 12. 本次检查限制

- 未修改任何 Scene、Prefab、Script 或 Sprite。
- 未运行完整 EditMode Test Runner。
- 未进入 Play Mode，因此没有验证实际输入、碰撞手感、场景切换和数据持久性。
- 未逐像素检查 UI 图集透明通道和青色背景残留。
- Git 数量为新增本报告前的快照；本报告生成后未跟踪文件会增加 1 项。

