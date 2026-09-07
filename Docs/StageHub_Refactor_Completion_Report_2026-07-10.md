# StageHub 阶段地图重构完成报告

完成日期：2026-07-10

## 1. 修改文件

- `Assets/Scenes/StudentGrowth/Maps/StageHubMapScene.unity`
  - 新增并启用 `Canvas_StageHubSelection`。
  - 从 StageHub 场景移除旧 `FullscreenMapCanvas`、`PlayerRpg`、旧背景 SpriteRenderer、出生点和 6 个旧 Portal 根对象。
  - 仅保留新画布内的 `Image_WorldBackground` 作为 StageHub 背景显示。
  - 新增一个 `EventSystem_StageHub`，使用 `InputSystemUIInputModule`。
  - 将原有 6 个 `MapLocationPortal` 的地点 ID 与路由职责迁移到 6 个新地点节点。
  - 保留不可见的 `StageHubMapSceneRoot` 运行时控制器，并关闭旧 UI 自动兜底生成。

未修改 HomeMapScene、ShopMapScene、共享 Map UI Prefab 或现有玩法控制器。

## 2. 新增文件

- `Assets/Art/Pixel/UI_New/StageHub/StageHub_LocationSprites.png`
  - StageHub 专用透明地点节点与选中反馈 Sprite Sheet。
- `Assets/Art/Pixel/UI_New/StageHub/StageHub_WorldBackground.png`
  - StageHub 最新世界背景图；使用 Point Filter、Compression None、MipMap Off。
- `Assets/Art/Pixel/UI_New/Shared/ModernGameUi.png`
  - 新黑灰/薄荷绿 UI 图集，洋红底已转换为透明通道。
  - Multiple Sprite，共 19 个切片；Point Filter、Compression None、MipMap Off。
- `Assets/Prefabs/StudentGrowth/UI/StageHub/PF_StageHubLocationNode.prefab`
  - 单个地点节点显示、Hover、选中、锁定和点击区域。
- `Assets/Prefabs/StudentGrowth/UI/StageHub/StageHubSelectionCanvas.prefab`
  - StageHub 独立的单屏地点选择 UI。
- `Assets/Scripts/StudentGrowth/UI/StageHub/StageHubLocationNodeView.cs`
  - 节点显示与鼠标事件，不处理游戏规则。
- `Assets/Scripts/StudentGrowth/UI/StageHub/StageHubLocationInfoView.cs`
  - 右侧地点名称、预览、描述、状态和确认按钮。
- `Assets/Scripts/StudentGrowth/UI/StageHub/StageHubSelectionController.cs`
  - 绑定现有 Portal、管理 UI 选择、刷新状态并调用原有确认入口。
- `Assets/Scripts/StudentGrowth/UI/StageHub/StageHubSelectionInputAdapter.cs`
  - 将 Legacy Input / Input System 的 WASD、方向键、E、Enter、Esc 映射到选择界面。
- `Assets/Tests/EditMode/StageHubSelectionUiTests.cs`
  - 覆盖层级顺序、6 个唯一 ID、四状态按钮和 2×3 键盘导航。
- `Docs/StageHub_Refactor_Completion_Report_2026-07-10.md`
  - 本报告。

Unity 自动生成了对应 `.meta` 文件，未手动改变任何既有 GUID。

## 3. 使用素材

正式 Sprite：

- `Assets/Art/Pixel/UI_New/StageHub/StageHub_LocationSprites.png`

该素材使用内置 `imagegen`，以用户提供的概念图为视觉参考，重新整理为无文字、无层标签、无灰色展示背景的生产 Sprite Sheet。生成阶段使用纯色 Chroma Key 背景，之后通过本地去背工具转换为 Alpha PNG；没有把原始展示图直接放入运行时 UI。

生成约束摘要：

- 六个独立等距像素地点：家、学校、公园、商店、图书馆、社团活动室。
- 选中反馈：黄色菱形描边、定位 Pin、像素星光。
- 禁止文字、层标签、水印、连接背景和相互遮挡。

切片名称：

- `StageHub_Node_Home`
- `StageHub_Node_School`
- `StageHub_Node_Park`
- `StageHub_Node_Shop`
- `StageHub_Node_Library`
- `StageHub_Node_Club`
- `StageHub_Selection_Outline`
- `StageHub_Selection_Pin`
- `StageHub_Selection_Sparkles`

导入设置已通过 Unity MCP 确认：

- Texture Type：Sprite 2D/UI
- Sprite Mode：Multiple，共 9 个切片
- Filter Mode：Point
- Compression：Uncompressed；Default、Standalone、WebGL 均为 0
- MipMap：Off
- Alpha Is Transparency：On
- Wrap Mode：Clamp
- Mesh Type：Full Rect

面板和按钮继续复用 `StudentGrowthPixelTheme.asset`，未修改全局主题引用。

## 4. 场景层级

```text
Canvas_StageHubSelection
├── Layer_00_Background
│   ├── Image_WorldBackground
│   └── Image_BackgroundDecoration
├── Layer_01_MapRoutes
│   ├── Route_01
│   ├── Route_02
│   ├── Route_03
│   ├── Route_04
│   ├── Route_05
│   ├── Route_06
│   └── Route_07
├── Layer_02_LocationNodes
│   ├── Node_01_家里
│   ├── Node_02_学校
│   ├── Node_03_公园
│   ├── Node_04_商店
│   ├── Node_05_图书馆
│   └── Node_06_社团活动室
├── Layer_03_SelectionFX
│   ├── Selection_Outline
│   ├── Selection_Pin
│   ├── Selection_Sparkles
│   └── Selection_Label
├── Layer_04_TopLeftTimeInfo
│   └── Panel_TimeInfo
│       ├── Text_TimePeriod
│       ├── Text_Time
│       └── Text_CurrentLocation
├── Layer_05_BottomLeftPlayerStatus
│   └── Panel_Status
│       ├── Status_Row_01
│       ├── Status_Row_02
│       ├── Status_Row_03
│       └── Status_Row_04
├── Layer_06_RightLocationInfo
│   └── Panel_LocationInfo
│       ├── Text_LocationName
│       ├── Panel_Preview
│       ├── Text_LocationDescription
│       ├── Text_TargetScene
│       ├── Text_LocationState
│       ├── Text_ResultMessage
│       └── Button_Confirm
├── Layer_07_RightResidentsEvents
│   └── Panel_Residents
│       ├── Residents_Grid
│       ├── Panel_Events
│       └── Events_Grid
├── Layer_08_BottomControlHint
│   └── Panel_ControlHint
│       ├── Hint_Move
│       ├── Hint_Confirm
│       └── Hint_Back
└── Layer_09_OverlayFrame
    ├── Overlay_Top
    ├── Overlay_Bottom
    ├── Frame_Screen
    └── Frame_Corners
```

`Layer_09_OverlayFrame` 检测到的 Raycast Target 数量为 0，不遮挡节点点击。

## 5. 地点绑定

以下状态为默认第一阶段状态；运行时仍由现有 `StageMapLocationAvailabilityService` 重新计算。

| 节点 | 现有 ID | 目标场景 | 默认状态 | 确认行为 |
| --- | --- | --- | --- | --- |
| 家里 | `location_home` | `HomeMapScene` | Unlocked | 调用原有路由进入 |
| 学校 | `location_school` | `SchoolMapScene` | ComingSoon | 显示尚未接入，禁止确认 |
| 公园 | `location_park` | `ParkMapScene` | ComingSoon | 显示尚未接入，禁止确认 |
| 商店 | `location_shop` | `ShopMapScene` | Unlocked | 调用原有路由进入 |
| 图书馆 | `location_library` | `LibraryMapScene` | Locked | 第二阶段后按原规则解锁 |
| 社团活动室 | `location_club` | `ClubRoomMapScene` | Locked | 兴趣或社交条件按原规则解锁 |

节点选择只改变 UI 和右侧预览，不扣时间、不消耗资源、不执行活动。

## 6. 复用接口

- 场景确认入口：`MapInteractionController.TryInteract(MapInteractionPoint)`
- 场景路由：`LocationSceneRouter.EnterLocationScene`
- 运行时服务绑定：`FullscreenMapSceneController`
- 地点状态刷新：`StageHubMapSceneController.RefreshPortals`
- 解锁判断：`StageMapLocationAvailabilityService`
- 状态读取：`GameSessionState`、`StageState`、`PlayerState`
- 输入：现有 Legacy Input 与 Input System 键值；未新增全局 Input Action
- UI 事件：StageHub 独立 `EventSystem` + `InputSystemUIInputModule`

## 7. 未修改系统

- 战斗与 Boss
- 卡牌与卡组
- 任务系统
- 时间推进规则
- 活动条件、资源消耗与奖励结算
- `LocationActionExecutor`
- `GameSessionState`、`PlayerState`、`StageController`、`ActivityController`
- `RpgPlayerController`
- HomeMapScene
- ShopMapScene
- `Assets/Prefabs/StudentGrowth/UI/Map` 下的共享 Prefab
- 全局主题和全局字体
- Tilemap、Sorting Layer、碰撞和角色动画

HomeMapScene、ShopMapScene、共享 FullscreenMapCanvas 与 FullscreenMapHud 的文件修改时间均保持在 2026-06-13，本任务没有写入这些文件。

## 8. 测试结果

### 编译与 Console

- 新增 C# 脚本编译成功。
- 最终 Unity Console：Error 0。
- 当前唯一 Warning 为 Unity MCP 相机捕获工具释放 RenderTexture 的提示，不来自游戏逻辑。

### Unity MCP 静态审计

- 顶层 Layer：10/10，顺序正确。
- 地点节点：6/6，ID 唯一，Sprite 引用完整。
- Button：7 个，7/7 使用 Sprite Swap 且 hover / pressed / disabled 完整。
- Sliced Image：19 个。
- Missing Script：0。
- EventSystem：1。
- `UI_Old` Sprite 引用：0。
- Overlay Raycast Target：0。
- StageHub Collider2D：0。
- StageHub Animator：0。
- StageHub `MapInteractionFlowController`：0。

### EditMode

- 已新增 `StageHubSelectionUiTests.cs`，脚本编译成功。
- 当前 Editor 内通过 Unity MCP 执行了等价断言：6 个唯一 ID、节点 Sprite、按钮四状态、左右/上下网格导航、锁定节点确认保护，全部通过。
- 尝试启动全量 Unity EditMode Test Runner，但当前项目已被打开的 Editor 锁定，批处理实例退出且未生成 XML。未关闭用户当前 Editor。

### Play Mode

- 已进入并退出 Play Mode。
- Play Mode 期间 Console Error / Warning 均为 0。
- MCP 的运行时审计连接在返回前被系统中止，因此没有自动完成鼠标点击和真实场景跳转断言。

### 分辨率

- Canvas Scaler：Scale With Screen Size，参考分辨率 1920×1080，Match 0.5。
- 1920×1080、1600×900、1366×768、1280×720 均为同一 16:9 构图范围。
- 已按参考坐标检查左右面板、6 个节点、底部操作栏和 32 px 安全边距；左下节点曾与状态面板轻微相交，已移动并同步虚线路径。
- Unity Camera Capture 不包含 Screen Space Overlay Canvas，未能生成有效 Game View 截图，仍建议在 Editor 中切换上述 4 个分辨率做最终目视确认。

### 场景跳转

- 路由绑定静态确认通过：家里与商店继续使用现有 MapInteractionController / LocationSceneRouter。
- 本次未自动点击进入 HomeMapScene / ShopMapScene，需按下方步骤人工回归。

## 9. 已知问题

- 当前没有可读取的居民或事件数据，右下区域统一显示“暂无可用信息 / 暂无额外事件”，没有创建新 NPC 或事件系统。
- 学校、公园仍无有效最新地图；图书馆、社团仍按现有规则锁定。这是项目原状态，不属于本次 UI 重构缺陷。
- 全量 EditMode Test Runner 因当前 Editor 项目锁未执行。
- Game View 分辨率截图和真实鼠标点击 / 场景跳转仍需人工复核。

建议人工验证：

1. 打开 `StageHubMapScene` 并进入 Play Mode。
2. 验证默认选中家里，WASD / 方向键按 2×3 邻接关系切换。
3. 验证 Hover 仅提亮，第一次点击选择，第二次点击或 E / Enter 确认。
4. 验证学校、公园、图书馆、社团不能错误进入。
5. 验证 StageHub → HomeMapScene、StageHub → ShopMapScene。
6. 验证返回后玩家属性、剩余时间和原有地图 HUD 不变。
7. 在 1920×1080、1600×900、1366×768、1280×720 下检查边距和文字。

## 10. 旧阶段地图清理

- 场景顶层对象现为：`Main Camera`、`StageHubMapSceneRoot`、`Canvas_StageHubSelection`、`EventSystem_StageHub`。
- 旧 `Background_StageHub`、`PlayerRpg`、`Spawn_stage_hub_start`、6 个 `Portal_*` 和 `FullscreenMapCanvas` 均已从 StageHub 场景移除。
- 新画布中保留 6 个地点节点、6 个 `MapLocationPortal`、6 个 `MapInteractionTarget`，原有解锁判断和场景切换逻辑不变。
- `FullscreenMapSceneController.createFallbackUi` 已关闭，运行时不会重新生成旧 HUD。
- Unity MCP 静态审计通过：旧根对象 0、旧 SpriteRenderer 0、新背景 1、新节点入口 6。
- `Image_WorldBackground` 已解除旧监控室背景引用，改为 `StageHub_WorldBackground.png`，并使用 `AspectRatioFitter/EnvelopeParent` 等比覆盖画布。

## 11. 新 UI 主题替换

- StageHub 的时间面板、玩家状态面板、地点信息窗、地点预览框、居民面板、底部提示条、地点标签和锁定标记已改用 `ModernGameUi.png`。
- 7 个按钮均使用新图集的 default / hover / pressed / disabled 四态 Sprite Swap，原按钮事件和导航绑定保持不变。
- 路径虚线、状态条、选中框和文本颜色已从木质黄棕配色调整为黑灰、白、薄荷绿、蓝与状态红。
- StageHub 场景和两个 StageHub Prefab 中的旧 `UI_SpecSheets` 引用均为 0；旧图已归档到 `Assets/Art/Pixel/UI_Old/OldSpecSheets`。
- 最终透明图可见洋红像素为 0；带洋红底的中间源文件未保留在项目中。
