# 主菜单 v1.1 实施记录

实施范围：独立入口、会话生命周期、十槽元数据、自动档保护、未保存导航与恢复界面。测试结果以同目录 logic.txt / play.txt / build.txt 为准；截图不是逻辑验收证明。

## 实际接入点

- `Assets/Scripts/StudentGrowth/Pause/GameApplicationRuntime.cs`：唯一应用级组合入口，使用已有 SettingsMenuController 常驻根。创建原仓库、SaveLoadService、GameFlowService、共用存取面板和恢复 View。
- `PauseMenuRuntime.cs`：保留旧输入和导航调用接口，转发到应用宿主，不再创建一套服务。
- `MainMenuController.cs` / `MainMenuView.cs`：独立主菜单。正式构建第一个场景为 `Assets/Scenes/StudentGrowth/MainMenuScene.unity`，新局进入 `HomeMapScene`，StageHub 保留。
- `UnityGameFlowBackend.cs`：场景预检、结束旧会话、加载和实际绑定检查。30 秒加载、10 秒绑定，均用实时钟。底层加载超时后等其收束，不能重入加载。
- `ApplicationRecoveryView.cs`：直接用应用宿主创建，不依赖主菜单资源。显示错误、等待、重试返回主菜单和受提交保护的退出。
- `SaveSlotRepository.cs`：保留 GUID 版本目录、Record.dat、RecordTitle.dat、Current.dat 原子发布；新元数据写入游戏存档 V2，设置版本不变。

## 标识和权限

RuntimeSessionId 每次进入会话重建。RunId 新局生成，读档保留。SaveId 就是现有版本目录 GUID。V1 在内存中取得 `legacy-v1:<提交 GUID>`，读取不改写正文。

Slot00 空或同局可自动保存。不同局只有存档面板专用确认后才即时替换；授权校验当前运行会话及确认时的 GUID。手动另存不转移自动权限。损坏槽不作为空槽。存档同步事务在主线程串行执行，不在协程中分步让出提交中间状态；已开始写入不会被取消回调当成撤销。发布异常后复核 Current.dat，无法确认时禁止下一次覆盖或导航。

## 实际状态映射

| 运行字段 | 保存 DTO | 比较 |
| --- | --- | --- |
| playerState 全部字段（学科、体力、压力、心情、关系、社交、兴趣、自信、自主、金钱） | player | 全部纳入 |
| timeActionState 全部字段 | time | 全部纳入；游戏内日期/时段/行动值影响 dirty |
| locationState 地点、已解锁/到访列表、次数/标记字典 | location | 字典按键序，列表保留顺序 |
| stageState 当前阶段、剩余时间、Boss 状态和计数 | currentStageId 等字段 | 纳入 |
| deckState.masterDeck、itemInventory.ownedItems | cardIds / itemIds | 稳定资源 ID，保留重复和顺序 |
| unlockedActivities / unlockedStages | activityIds / stageIds | 纳入 |
| clearedStageIds / defeatedBossIds / unlockedActivityIds / unlockedLocationIds | 同名列表 | 纳入 |
| currentEnding / endingReached / tutorialSeen | endingId / endingReached / tutorialSeen | 纳入 |
| 玩家世界坐标、朝向 | playerX/Y、facingX/Y | 位置按玩家 Sprite 的 PPU 换算一像素，AwayFromZero 舍入；无 Sprite 时 1/16 世界单位；朝向直接比较 |
| 现实游戏时长 | playedSeconds | 保存但排除 dirty |
| 提交版本、序号、UTC 时间 | version / saveSequence / savedAtUtc | 排除 dirty |
| 运行会话 ID、UI 焦点、相机、动画 | 不进入 DTO | 临时状态 |

规范化从实际保存 DTO 的完整 JSON 树派生，递归排序对象键；数组不排序。新 DTO 字段默认参与比较。直接公开字段赋值和集合变动不依赖通知。基线按提交快照及运行会话校验更新。

## 清理归属

应用服务和设置保留。结束时停止跨天订阅、清空本会话待保存快照、解绑共享状态、禁用并销毁玩家、清空 PlayerManager 引用；后端 Single 加载卸载源地图。输入锁按拥有者释放。普通地图切换继续使用项目原路由。

## 测试与恢复

所有存档破坏性用例位于 Temp 下的唯一目录。旧设置验收工具已增加互斥，避免共享隔离标记启动两套 Play 验收。

编辑器曾卡死，重启前保存了 `EditorRecovery/ClubRoom-PlayBackup.unity` 和磁盘版本，校验备份 SHA256 一致；这些在 Docs 下，不会作为游戏资源导入。真实存档未用于篡改测试。

静态背景由家地图的编辑器预览场景相机烘焙，排除角色和 Canvas，不运行地图玩法；仅新增 PNG 使用 Point / 无压缩，不改变原美术导入设置。
