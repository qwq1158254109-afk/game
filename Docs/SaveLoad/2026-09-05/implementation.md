# 暂停菜单与存读档实施记录

## 实施范围

按《03_暂停菜单与存读档职责拆分设计》实现独立暂停导航、十槽存读档、缩略图缓存、场景恢复和有序读档后处理。复用既有 SettingsService / SettingsPanelView 和显示事务服务。运行时代码位于 Assets/Scripts/StudentGrowth/Pause 与 Save；编辑器工具位于 Assets/Editor。

当前工程不存在文档所述 SettingPanelUI、旧存读档代码、旧 Record.dat 格式、Steam SDK 或独立标题场景。因此本次是接入新模块，未声称已迁移或兼容未知旧二进制存档。主菜单使用暂停后的标题面板；“开始新游戏”加载已有 StageHubMapScene。未保存或覆盖用户原本处于 dirty 状态的场景。

## 使用方式

1. Play 后按 Esc 打开暂停菜单；选择继续、保存、读取、设置、返回主菜单或退出。
2. 十槽中第一个是自动存档槽，只能读取。跨天后在可保存状态生成自动存档。其余九槽用于手动存档。
3. 点击槽位后确认保存/覆盖/读取。取消不会写盘。读档成功后恢复场景、玩家位置朝向、属性、时间、卡组、物品和剧情标记，并恢复游戏输入。
4. 设置关闭后回到原来的暂停/标题上下文。显示确认和恢复事务期间拒绝普通导航；退出由显示服务取消候选，不保存未确认意图。
5. 新增可存档的卡牌、道具、阶段、活动、结局资源后，运行 Tools > Student Growth > Rebuild Save Content Catalog。保持业务 ID 和 .meta GUID 稳定。

## 模块与数据

- PauseMenuRuntime 只负责组合服务、事件连接与跨场景生命周期；Coordinator 管理导航、确认和忙碌状态；View 仅展示和转发输入。
- SaveLoadService 校验并返回 LoadedGameState；GameFlowService 调用 UnityGameFlowBackend 恢复场景，再执行显式注册的 PostLoadPipeline。
- SaveGameCodec 使用版本 1 的 JSON DTO，不保存 MonoBehaviour、场景引用或类型元数据。保存前验证 ID 可解析，读取时校验摘要哈希、结构、范围和版本。
- 已发现 HomeMap 与 New2_0 中三组活动使用相同业务 ID、不同奖励。资源目录用原有 GUID 生成 asset: 存档键以精确恢复对象，不改动业务 ID 或 .meta。运行时生成的示例数据继续使用稳定业务 ID。
- StageHubMapSceneController 在新游戏/读档后优先绑定共享状态，防止场景序列化默认数据覆盖时间控制器，使跨天自动档与 HUD 使用同一份日期。
- 存储根为 Application.persistentDataPath/Saves。Slot00～Slot09 内使用版本目录，包含 Record.dat、RecordTitle.dat、ScreenShot.png；Current.dat 原子切换可见版本。截图失败时存档仍可成功并提示，面板显示占位。旧版本目录保留，尚未实现自动清理。
- 摘要读取不读完整存档；缩略图为 288×162 PNG，在一次面板会话内缓存，关闭/销毁释放。损坏或缺失图片也缓存占位结果。
- MenuTextService 的语言事件只刷新文本。宿主监听 SettingsService.Pending.LanguageCode；项目没有完整语言设置控件或翻译 SDK，本次没有添加全游戏本地化系统。
- AchievementSyncService 具备离线诊断和幂等补发接口，运行时未配置平台和成就规则。离线结果不阻塞读档，但目前不会向 Steam 发放真实成就。

## 验证与复现

本轮结果：Unity 6000.4.6f1 编辑器选定 NUnit 测试 87 通过 / 0 失败；独立 .NET 设置核心回归 56 通过 / 0 失败。真实 Play 的保存/读取、状态与玩家恢复、设置返回、标题新游戏、跨天自动档、View 销毁后事件均通过；存档面板截图为本目录 save-menu.png。Unity MCP Console 检查 Error 为 0。

- 编辑器测试：在项目 Temp/SettingsVerification.request 写入任意文本；报告位于 Docs/Settings/2026-09-05/Unity-verification.txt 和 Unity-Editor-NUnit-results.xml。包含本次 PauseSaveTests，以及设置、音频、显示既有回归。
- 真实 Play 回归：在 Temp/PauseSaveVerification.request 写入任意文本；报告为本目录 Unity-Play.txt。测试隔离设置与存档于 Temp，在当前场景进入/退出 Play，不保存场景。
- 既有设置 Play 回归：Temp/SettingsPlayVerification.request。两个 Play 请求应分开运行。
- L-01～L-07、L-11：十槽摘要、确认/取消、自动槽保护、损坏读取、截图缓存/释放/占位和连续语言切换测试。
- L-08：真实场景保存、修改数据、读取并恢复状态与玩家位置；检查无额外资源消耗或发奖。
- L-09/L-10：设置 View/Controller 无截图读取或场景加载逻辑。
- L-12/L-13：假平台测试覆盖离线不阻塞、缺失成就补发和重复调用幂等。真实平台与旧版本存档验收仍待 SDK、成就规则和旧档样本，未标记为已通过或已获批准的 N/A。
- L-14：模拟显示平台进入危险确认期间，PauseMenu 拒绝返回、继续、保存、读取、标题导航；事务恢复后可继续。
- L-15：文本事件解除订阅、View 销毁后服务可继续工作；真实 Play 测试销毁存读档 View 后切换语言。

## 手工验收及限制

在目标设备打包后检查 Esc、鼠标/键盘按钮、十槽布局、截图、跨天自动档、跨地图读档、应用重启后读档、退出和显示回滚。检查 UI 刷新、位置、行动点、属性及旧地图交互功能。

战斗中和活动确认/执行中明确拒绝保存，防止产生缺少战斗/交互中间态的存档。地图稳定状态可保存。真实 Steam、旧格式迁移、多平台文件系统和硬件显示行为尚需外部环境验证。场景激活后的恢复失败保留已加载状态并在暂停面板报告错误，不宣称支持完整场景回滚。
