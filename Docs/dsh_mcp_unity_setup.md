# dsh × mcp-unity 接入说明

记录日期：2026-10-19 ｜ 工程 `E:\大创项目` ｜ Unity 6000.4.6f1 ｜ mcp-unity 1.5.0 ｜ MCP 端口 8096

这份文档解决一件事：**让 dsh（DeepSeek Harness）能直接操作 Unity 编辑器**。装好后的能力见第四节。

---

## 一、结论先行

| 项 | 值 |
|---|---|
| 配置文件 | `C:\Users\cyl\.dsh\profiles\desktop\cordis.patch.yml`（dsh 的 profile 补丁层） |
| 注册方式 | `- insert:` 列表里挂一条 `@deepseek-ai/dsh-mcp-client` |
| 服务器名 | `unity`（工具会显示成 `mcp__unity__*`） |
| 桥接进程 | `node Packages/mcp-unity/Server~/build/index.js`（stdio） |
| 连 Unity 的端口 | 8096，来自 `ProjectSettings/McpUnitySettings.json` |
| 鉴权令牌 | `Library/McpUnity/bridge-token`（由 Unity 生成，勿提交、勿手改） |
| 生效方式 | dsh 会**原地重载**配置项，不需要重启（2026-10-19 实测：改完当轮 `unity` 就出现在可用 MCP 列表里） |

## 二、为什么是这个写法（两个坑，都踩过）

**坑 1：裸条目会被静默忽略。**
`cordis.patch.yml` 顶层的 `- id: xxx` 是「修改一个已存在的插件」，不是「新增插件」。新增必须包在 `insert:` 里，否则不报错、也不生效——看起来像配置没写对。

```yaml
- insert:
    - id: mcp-unity
      name: "@deepseek-ai/dsh-mcp-client"
      config:
        serverName: unity
        transport: stdio
        command: 'node'
        args:
          - 'E:\大创项目\Packages\mcp-unity\Server~\build\index.js'
        env:
          MCP_UNITY_SETTINGS_PATH: 'E:\大创项目\ProjectSettings\McpUnitySettings.json'
          MCP_UNITY_AUTH_TOKEN_PATH: 'E:\大创项目\Library\McpUnity\bridge-token'
        toolCallTimeoutMs: 60000
```

**坑 2：dsh 会写这个文件。**
在界面里改任何设置（例如推理强度），dsh 都会回写 `cordis.patch.yml`。它会保留注释和中文，但**会重排部分内容**，所以：改之前先备份，改完用 YAML 解析确认没被写坏。

排查期间还验证过：PowerShell 用 `Get-Content` 读这个 UTF-8 文件会显示成乱码（`E:\澶у垱椤圭洰`），那是**显示层**的编码问题，文件本身没坏。要判断内容以 `read` 工具为准，别被 `Get-Content` 骗。

## 三、验证方法（三步，都不用开 dsh 界面）

1. **桥本身通不通**（不依赖 Unity）：

```powershell
'{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"probe","version":"1"}}}' |
  node "E:\大创项目\Packages\mcp-unity\Server~\build\index.js"
```

应返回 `serverInfo: { name: "MCP Unity Server", version: "1.5.0" }`。

2. **Unity 有没有在听 8096**：`Get-NetTCPConnection -State Listen | Where-Object LocalPort -eq 8096`
3. **dsh 有没有拿到这个服务器**：让 dsh 调 `list_mcp_resources(server="unity")`，能列出 `unity://menu-items`、`unity://logs/...` 等 13 个资源即成功。

## 四、能用它做什么

- 场景：读层级、加载/卸载/保存/新建场景
- 对象：查/建/删/改 GameObject、组件字段、Transform、材质
- 工程：触发 recompile 并读编译日志、执行任意菜单项、跑 EditMode 测试
- 诊断：读 Unity Console（info/warning/error，可分页）

**禁区（沿用《防降智作业规程 v1》）**：

- PlayMode `run_tests` 会卡死，**禁用**；要验证就写 probe
- Play 模式下 MCP 会「半残」（端口在听、握手超时），此时别反复重试，改走 probe 或按"关 Unity → 等端口释放 → 重启"三步
- 用户在玩时不动 Unity（重启/关窗/注入/切场景）
- 进 Play 前必须 recompile 0 error

## 五、回滚

删掉 `cordis.patch.yml` 里那个 `- insert:` 块（以及它上面的注释），dsh 会自动卸载该服务器。改动前的原始文件备份在 `_backup_dsh_rules_20261019\cordis.patch.yml`。
