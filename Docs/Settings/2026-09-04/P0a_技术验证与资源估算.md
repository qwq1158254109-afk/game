# P0a 技术验证与资源估算

日期：2026-09-04。对象：用户确认的 `E:/unity/ML-project`。

## 1. 结论

P0a 的静态基线和隔离主机探针已完成；完整 P0a 尚未通过。12 项探针通过，但没有运行 Unity Console/Play、目标 Player、显示切换或真实玩家路径测试。

本次交付包括面向当前工程的五项设计修订和有条件资源估算。未修改 Assets、ProjectSettings、Packages、玩家 PlayerPrefs 或原游戏文件；新增内容仅为文档及 Tools 下的隔离验证程序。

## 2. 已验证基线

| 项目 | 证据 | 结论与限制 |
| --- | --- | --- |
| Unity | ProjectSettings/ProjectVersion.txt | 6000.4.6f1，静态确认 |
| C# / API 目标 | StudentGrowth.Runtime.csproj | 生成工程标明 C# 9、netstandard2.1；不是重新编译通过证明 |
| URP | Packages/manifest.json | 17.4.0 |
| Input System | manifest 与 ProjectSettings.asset | 包 1.19.0；activeInputHandler=1，运行时输入仍需验证 |
| Newtonsoft UPM | packages-lock.json 与包 package.json | com.unity.nuget.newtonsoft-json 3.2.2，当前为间接依赖 |
| Newtonsoft DLL | 实际 Runtime DLL 文件版本与探针 | 13.0.2；不能把 UPM 版本和 DLL 版本混用 |
| 音频通道 | AudioSettingsModel、AudioSettingsContracts | Master/Music/SFX，默认均为 1 |
| 有效音量 | AudioSettingsController、AudioChannelSource | Master×通道×音源基础音量，存在事件刷新 |
| 现有保存 | AudioSettingsController | SetVolume 更新 PlayerPrefs；Pause/Quit 保存 |
| 现有菜单 | SettingsMenuController、SettingsPanelView | Escape 暂停菜单；Close 保存；F11 直接切窗口/无边框 |
| 当前测试资产 | AudioSettingsModelTests、AudioChannelSourceTests、SettingsUiFactoryTests | 已读源码，不能标记为本次 Unity 测试通过 |
| 源码复用条件 | Assets/Scripts/StudentGrowth/Settings | 存在现成系统；不应创建重复 Manager 或从原游戏迁移剧情配置 |

当前 productName=ML-project、companyName=DefaultCompany。不能把它们推导出的候选 Player 路径视为已实测路径，也不应为整理路径随意修改公司名/产品名而迁移玩家数据。

源码证据集中于：

- `Assets/Scripts/StudentGrowth/Settings/Audio/AudioSettingsController.cs`
- `Assets/Scripts/StudentGrowth/Settings/Audio/AudioSettingsModel.cs`
- `Assets/Scripts/StudentGrowth/Settings/Audio/AudioChannelSource.cs`
- `Assets/Scripts/StudentGrowth/Settings/UI/SettingsMenuController.cs`
- `Assets/Scripts/StudentGrowth/Settings/UI/SettingsPanelView.cs`
- `Assets/Tests/EditMode/AudioSettingsModelTests.cs`
- `Assets/Tests/EditMode/AudioChannelSourceTests.cs`
- `Assets/Tests/EditMode/SettingsUiFactoryTests.cs`

## 3. 隔离探针结果

工具：`Tools/SettingsP0a/SettingsP0a.csproj`，SDK 10.0.300，运行主机 .NET 10.0.8。

直接引用当前 Unity PackageCache 中 Newtonsoft 13.0.2，不下载 NuGet 包；工具不引用 UnityEngine、不加载原游戏程序集执行其代码。此工具使用 net10.0 是为了隔离验证，绝不表示 Unity 工程要升级到 .NET 10。

| 编号 | 检查 | 结果 |
| --- | --- | --- |
| JSON-01 | 显式属性往返、枚举字符串 | PASS |
| JSON-02 | 可选成员默认值、未知成员忽略 | PASS |
| JSON-03 | 必需对象缺失被拒绝 | PASS |
| JSON-04 | 必需对象 null 被拒绝 | PASS |
| JSON-05 | 整数枚举被拒绝 | PASS |
| JSON-06 | 未知枚举字符串被拒绝 | PASS |
| JSON-07 | 根 JSON 为 null 时必须另做空值检查 | PASS |
| JSON-08 | NaN/Infinity 需要额外有限值校验 | PASS |
| JSON-09 | 按 Writable 过滤仍保留可写公共字段 | PASS |
| IO-01 | 同目录 move 首次创建 | PASS |
| IO-02 | 工作区文件 replace 并保留上一版 backup | PASS |
| IO-03 | 缺失临时源造成的已知失败保持旧正式文件 | PASS |

结果：`passed=12 failed=0`。这是 9 项序列化行为检查与 3 项基础文件操作检查，不是新 SettingsService/Repository 的测试结果。

JSON-09 仅重建原程序集静态可见的过滤谓词；并未执行原游戏 resolver。IO 测试只写工具自身 artifacts 下的唯一运行目录，不覆盖用户配置。测试数据保留供核查，未执行清理删除。

初次运行被全局 NuGet 配置权限阻挡；随后明确使用禁用包源的本地 NuGet.Config 恢复。第一次探针还暴露了工具自身路径校验的尾分隔符问题，已修正并重新运行，最终 12/12 通过。这不是 Unity 游戏缺陷。

### 复现

在仓库根目录运行，需本机 .NET 10 SDK 和当前 PackageCache。包缓存路径变化后应更新探针引用，不允许偷偷回退到其他版本 DLL。

```powershell
$env:DOTNET_CLI_HOME = "$PWD/Tools/SettingsP0a/.dotnet"
$env:NUGET_PACKAGES = "$PWD/Tools/SettingsP0a/.nuget"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
dotnet restore Tools/SettingsP0a/SettingsP0a.csproj --configfile Tools/SettingsP0a/NuGet.Config
dotnet run --project Tools/SettingsP0a/SettingsP0a.csproj --no-restore --no-launch-profile -- "$PWD/Tools/SettingsP0a/artifacts"
```

## 4. 原参考资料的纠错与适用边界

在用户明确当前工程之前，已对原 TheCensor 指定目录进行了只读元数据/IL 检查。它只用于校正文档，不能作为 ML-project 的运行证据。

- globalgamemanagers 头部版本串为 2022.3.0f1；存在 Managed/Assembly-CSharp.dll 和 MonoBleedingEdge。指定范围未找到完整源码工程，不代表磁盘其他位置一定没有源码。
- 原 get_SaveFileRootPath 的分支是：IsSteamVersion=true 使用 persistentDataPath，false 使用 dataPath/../。原分析写反了分支。实际运行中的 IsSteamVersion 未执行验证。
- 原 Deserialize<T> 在解析前调用 Activator.CreateInstance<T>()；解析异常通常返回该初始实例，并非统一返回 default(T)。但 JSON 字面量 null 会使结果为 null；UserSettingsV1 的空构造也不初始化 AudioRecord，因此风险仍然存在，原因应分别描述。
- WritablePropertiesOnlyResolver 的静态谓词只是 JsonProperty.Writable；不能仅凭名字断言公共字段被排除。此前评审的相关推断已在本稿更正。
- 原 I2 GetVersion 常量为 2.8.22 f4；Steamworks.NET.txt 标称 2025.162.1 / SDK 1.62。均不属于当前工程已接入依赖。

原参考程序集 SHA256：`E2BEBDFBD578616CE6B13ACD15A8166EBDAFE8790067A65BA93F0B9CE2DE1AEE`。

这些差异说明后续应以当前工程及探针为准，不再通过给原文重复加分代替验证。

## 5. 尚未通过的 P0a 门槛

| 待验证项 | 需要的环境/操作 | 完成标准 |
| --- | --- | --- |
| Unity Console 与既有测试 | 当前工程 Editor、Unity MCP/Test Runner | 无新增编译错误，现有三通道/UI/输入回归通过 |
| 当前 Player 路径与权限 | 隔离测试配置目录、Windows 测试构建 | 正确识别实际 persistentDataPath；读写/拒绝访问有可恢复结果 |
| PlayerPrefs 导入 | 测试账户/隔离键 | 原始三键不被预览污染，导入失败不删除旧值 |
| 显示能力 | Windows Player、实际显示器 | 窗口/无边框、F11、暂停时间、失焦与恢复稳定 |
| 显示 fallback | 至少一套非 16:9 或替代设备 | 可见且可操作，实际模式与原意图分别记录 |
| 故障与中断 | 进程级故障注入、隔离文件 | tmp 永不晋升、提交点前后状态正确、不破坏有效备份 |
| 运行时兼容 | 实际 Mono/IL2CPP 目标构建 | DTO/反射代码和裁剪行为通过；不能用主机探针代替 |

当前会话没有可调用的 Unity MCP Console 工具；本次未启动 Unity Editor 或修改 Unity 脚本以规避这一限制。Tools 中的独立 C# 探针已由 dotnet 编译，但不等于 Unity 编译通过。

用户只确认了实施工程，尚未确认全部发布平台和显示器范围。因此以下估算以 Windows 首期为条件，不承诺跨平台完整设置系统。

## 6. 资源估算：当前工程，不套用原游戏重构

单位：人日，每人日 8 小时。开发含实现与开发自测；QA 为独立回归/设备验证。以下是从本次交付之后的粗估，不是已经消耗的工时，也不是正式工期承诺。

| 工作包 | 开发人日 | QA 人日 | 内容与前提 |
| --- | ---: | ---: | --- |
| P0a 剩余验证 | 0.5～1 | 0.5～1 | Editor/Player/路径与基本显示能力，环境可用 |
| P0b 当前可靠性小修 | 0.5～1.5 | 0.5～1 | 有限值与失败反馈等最小修补，不照搬 V1 JSON 热修 |
| P1 三通道偏好与事务 | 3～5 | 1～2 | PlayerPrefs 导入、Repository、快照、预览恢复、dirty/degraded、现有 UI 绑定 |
| P3 首期显示事务 | 2～4 | 1～2 | 窗口/无边框、F11 统一、观测与确认回滚 |
| 集成与交付 | 0.5～1 | 0.5～1 | 主循环回归、构建、使用说明 |
| 合计 | 6.5～12.5 | 3.5～7 | 不含等待环境与新增内容 |

建议预留约 25% 风险缓冲：约 8～16 开发人日、4.5～9 QA 人日。1 名熟悉工程的 Unity 开发者配合约半职 QA，环境就绪且回归可穿插时，可按约 2～4 周讨论排期；P0a 未完成前不能承诺截止日期。

建议职责：Unity 开发负责服务、适配、迁移与集成；QA 负责配置故障/设备矩阵；产品负责人确认提交语义、平台、可选显示能力。首期沿用 UI 美术，不计新美术制作。

另行估算：多显示器选择/独占全屏/刷新率能力约增加 2～4 开发人日与 2～3 QA 人日，仍须 P0a 验证可实现；本地化内容、字幕、重绑定和 UI 缩放不在此范围，缺少需求不能给确定报价。

原 P2 存档截图/Steam 职责拆分不属于当前设置模块问题，当前估算为“不适用”，不是遗漏开发工作。不为凑齐参考方案而新增存档管理或成就系统。

## 7. 下一步与交付状态

已完成：静态基线、12 项隔离探针、五项设计修订、当前工程资源估算。

未完成：Unity/Player/真实显示器 P0a 验证；未实施运行时修复；未批准设计冻结或发布。

后续工作应先完成第 5 节 P0a 验证与 Windows 首期范围确认，再决定是否实施 P0b/P1。不能将本次设计修订描述为已经修复游戏中的对应代码。
