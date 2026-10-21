# 4 个插件安装记录（2026-10-20）

- 工程：`E:\大创项目`，Unity **6000.4.6f1** + URP 17.4.0 + 新 Input System 1.19.0
- 操作：在 `Packages/manifest.json` 增加 4 条 git 依赖
- 备份：`_backup_install4plugins_2026-10-20/Packages/{manifest.json,packages-lock.json}`
  - 原 `manifest.json` md5 `f83a5226030a4ce0f6912d24556f7188`（2583 B）
  - 原 `packages-lock.json` md5 `f56ccef39bfca0de8c628ae96dc702fd`（16737 B）
- **回滚方法**：把那两个文件拷回 `Packages/`，再删 `Library/PackageCache/` 下对应 4 个目录，重开 Unity

---

## 一、装了什么（已核验）

| 包名 | 版本 | GitHub 来源 | 许可 | lock 中 commit hash |
| --- | --- | --- | --- | --- |
| `com.annulusgames.lit-motion` | 2.0.2 | [annulusgames/LitMotion](https://github.com/annulusgames/LitMotion) `?path=src/LitMotion/Assets/LitMotion#v2.0.2` | MIT | `0b4c588ee75a07198841d92aab653e6b39445089` |
| `com.coffee.ui-effect` | 5.11.7 | [mob-sakai/UIEffect](https://github.com/mob-sakai/UIEffect) `?path=Packages/src#main` | MIT | `ff824c234121cd6dde7038c8e8ca4904d7f4d7f3` |
| `com.h8man.2d.navmeshplus` | 0.2.23 | [h8man/NavMeshPlus](https://github.com/h8man/NavMeshPlus) `#master` | MIT | `019e15cfe2fe7985ff1ac04cc8db6666ccc0df8d` |
| `dev.yarnspinner.unity` | 3.2.8 | [YarnSpinnerTool/YarnSpinner-Unity](https://github.com/YarnSpinnerTool/YarnSpinner-Unity) `#v3.2.8` | MIT | `bfc5b6a2e8bc8978e777caeea3a827a0d408cc7d` |

四个包都被 Unity 解析进 `packages-lock.json`（`source = git`，`depth = 0`），
并各自编译出了运行时段程序集，位于 `Library/ScriptAssemblies/`：

```text
LitMotion.dll                 17:59:47
LitMotion.Extensions.dll      18:00:09
Coffee.UIEffect.dll           17:59:46
NavMeshPlus.dll               17:59:46
YarnSpinner.Unity.dll         17:59:46
（编辑器侧另有 LitMotion.Editor / Coffee.UIEffect.Editor /
  NavMeshPlusEditor / YarnSpinner.Unity.Editor）
```

### 两个"名字和直觉不一样"的点（都已在 manifest 里处理好）

1. **Yarn Spinner 的 Unity 包不在 `YarnSpinnerTool/YarnSpinner`。**
   那个仓库只有编译器与引擎无关部分，根目录**没有** `package.json`，直接当 UPM 装会失败。
   官方 Unity 集成在独立仓库 `YarnSpinnerTool/YarnSpinner-Unity`（841★），根目录即包。
   本工程装的是后者 `v3.2.8`。两仓库版本号并不总是同步（核心仓最新 tag 是 v3.2.2）。

2. **UIEffect 不能用 `#v4.0.0-preview.10`。**
   该 tag 指向的提交里 `package.json` 位于子目录 `Packages/UIEffect/`，Unity 按包根找不到清单，
   会报 "cannot be found"。而 `main`/`develop` 分支（同一提交 `ff824c23`）的包在 `Packages/src/`，
   版本 **5.11.7**，含 *TextMeshPro Support (Unity 6)* 与 *ShaderGraph Support (Unity 6 URP)* 样例，
   `unity` 字段 2020.3，本工程可用。故采用 `?path=Packages/src#main`。

---

## 二、验证证据（可复现）

| 检查项 | 证据 |
| --- | --- |
| 解析成功 | `Library/PackageManager/projectResolution.json` 的 `outputs` 含 4 个包，`source = git`，版本 2.0.2 / 5.11.7 / 0.2.23 / 3.2.8 |
| 写入 lock | `Packages/packages-lock.json` 含 4 条记录（改后 17906 B，原 16737 B） |
| 包已落地 | `Library/PackageCache/` 下 `com.annulusgames.lit-motion@e1db5183ffc0`、`com.coffee.ui-effect@5913675f7f05`、`com.h8man.2d.navmeshplus@019e15cfe2fe`、`dev.yarnspinner.unity@bfc5b6a2e8bc` |
| 编译通过 | 5 个运行时段 DLL 全部生成；`Assembly-CSharp.dll` 于 18:00:18 重新编译（编译未被错误中断） |
| 域重载完成 | `Logs/relay.txt`：`10:00:34Z Domain reload signal received` → `10:00:36Z Client disconnected` → `10:00:51Z Unity client connected`（域重载能走完即证明脚本编译通过） |

**逐个复核命令**（在工程根目录执行）：

```bash
python -c "import json;d=json.load(open('Packages/packages-lock.json',encoding='utf-8'))['dependencies'];[print(k,v['hash']) for k,v in d.items() if v.get('source')=='git']"
ls Library/ScriptAssemblies | grep -E "LitMotion|UIEffect|NavMeshPlus|YarnSpinner"
```

---

## 三、踩坑记录（重要，下次别再花 40 分钟）

### 3.1 网络：github 间歇性被重置 → UPM 整个 resolve 作废

实测同一分钟内：

```text
git ls-remote 5 次 → 4 次 OK，1 次 `fatal: ... Recv failure: Connection was reset`
curl https://github.com/          → 有时 20s 超时，几分钟后 HTTP 200
curl https://codeload.github.com/ → 稳定 HTTP 200
```

`upm.log` 里的真实失败（17:28–17:31 那次）：

```text
[ERROR] git ls-remote https://github.com/h8man/NavMeshPlus.git master master^{}
        fatal: unable to access ... Recv failure: Connection was reset
[ERROR] git fetch -q --depth=1 origin master
        fatal: ... Failed to connect to github.com port 443 after 21050 ms
```

**关键教训**：UPM 克隆 git 包没有重试。一旦失败，`upm.log` 里仍会写
`project:resolve-packages --> 200`，但
`Library/PackageManager/projectResolution.json` 里**一个新包都没有**，
`Packages/packages-lock.json` 也**不更新**（mtime 还是旧的）——
表面上像"Unity 没反应"，实际是整次 resolve 作废了。第一次 resolve 耗时
`1283592 ms`（21.4 分钟）后正是这个结局。

**诊断入口**（比看 Editor.log 有效，Editor.log 可能长时间不 flush）：
1. `%LOCALAPPDATA%\Unity\Editor\upm.log` —— 最直接，写 `git ls-remote`/`git fetch` 失败原因
2. `Library/PackageManager/projectResolution.json` 的 `outputs` —— 判断"这次到底成了没"
3. `Logs/relay.txt` —— 判断域重载是否走完

**缓解措施**：`Tools/install_git_mirrors.sh` 在 `_gitcache/` 建镜像并写全局
`url.<镜像>.insteadOf`。修复后第二次 resolve 只用了 **11401 ms**（11.4 秒）。

### 3.2 `insteadOf` 是前缀匹配，`*` 不是通配符

实测（用不存在的本地路径当靶子）：

```text
insteadOf = https://github.com/a/B.git   + URL "https://github.com/a/B.git?path=sub"
  → fatal: '/nonexistent-mirror.git?path=sub' does not appear to be a git repository
    （改写命中了，但查询参数 ?path=sub 被原样拼到镜像路径后面）

insteadOf = https://github.com/a/B.git*  + 同一 URL
  → 未命中，仍走真实 github（把 * 当字面量了）
```

所以镜像只能兜住**不带查询参数**的 URL。LitMotion 和 UIEffect 用的都是
`?path=` 形态，改不到——好在它们已经落在 `Library/PackageCache`，
正常情况下不会再克隆。

### 3.3 LitMotion 的依赖本来就在，别被 package.json 吓到

LitMotion 声明依赖 `com.unity.burst` 1.6.0 / `com.unity.collections` 1.5.1 /
`com.unity.mathematics` 1.0.1。本工程原本就有这三个（URP 带进来的更高版本），
`upm.log` 显示被就地"抬到最低版本"：

```text
Requested version [1.0.0] for package [com.unity.burst] overridden by minimum version [1.8.29]
```

即解析结果用 `com.unity.burst@1.8.29` / `com.unity.collections@6.4.0` / `com.unity.mathematics@1.3.3`，
**没有发生降级**，无需额外操作。

### 3.4 装完之后 MCP 进 Console 拿不到日志（既有问题）

`Logs/mcp-task.log` 记录了本次域重载后的老毛病：

```text
[MCP Unity] Failed to start WebSocket server: Port 8096 is already in use.
  at McpUnity.Unity.McpUnityServer:StartServerInternal (McpUnityServer.cs:403)
```

`get_console_logs` 两次调用均 `Request timed out`。按 `AGENTS.md` 20.4，这是已知现象，
处理办法是重启 Unity，**不要反复重试 MCP**。本次未重启（Unity 里有用户未保存的编辑），
所以 **Console 的最终人工确认留着没做**——见下方"待人工确认"。

---

## 四、各插件怎么用（贴合本项目）

### 4.1 LitMotion —— 交互提示 / 卡牌动画（V0.2–V0.4）

零 GC 的补间库。命名空间 `LitMotion`，核心是 `LMotion.Create(from, to, duration)`。

```csharp
using LitMotion;
using UnityEngine;

// 交互提示淡入：靠近床/书桌时提示条从下方浮起
LMotion.Create(0f, 1f, 0.18f)
      .WithEase(Ease.OutQuad)
      .Bind(alpha => _promptGroup.alpha = alpha);

// 让它跟随一个 Transform（无需每帧写 Update）
LMotion.Create(_promptRoot.localPosition, _targetLocalPos, 0.2f)
      .BindToLocalPosition(_promptRoot);

// 卡牌飞入：延迟 + 缓动
LMotion.Create(new Vector3(0f, -400f, 0f), Vector3.zero, 0.25f)
      .WithDelay(0.05f * index)
      .WithEase(Ease.OutBack)
      .BindToLocalPosition(cardRect.transform);
```

**本工程注意**：项目 `AGENTS.md` 禁止在 `Update()` 里制造 GC。LitMotion 正好契合，
但**不要**在交互检测里每帧 `LMotion.Create`——一次交互只发一次补间，并在
`OnDisable` 时 `.Cancel()` 掉，否则对象池复用时会有残留动画。
详见 `Library/PackageCache/com.annulusgames.lit-motion@*/README.md`。

### 4.2 UIEffect —— 像素风 UI 反馈 / 战斗特效（V0.4）

在 uGUI 物体上直接加组件即可，无需额外相机：

- `UIEffect` —— 灰度 / 褐色 / 模糊 / 溶解 / 色调
- `UIDissolve` —— 溶解（做"卡牌被打碎"）
- `UIShiny` —— 扫光（做"稀有卡牌高亮"）
- `UITransitionEffect` —— 转场（做场景切换遮罩）

像素风要点：**加组件后要确认没破坏 Point 过滤与像素对齐**。若出现糊边，
说明该 effect 走了 `Canvas` 的额外采样——优先用 `UIDissolve`/`UIShiny`
这类只改 UI 顶点流的组件，别用带模糊的 `UIEffect` 模式。

本项目 UI 主题资产在 `Assets/Resources/StudentGrowth/UI/StudentGrowthPixelTheme.asset`，
改颜色/贴图走那里，不要在每个 Prefab 上散着改。

### 4.3 NavMeshPlus —— NPC 寻路（V0.6 用，现在别接）

给 2D Collider / Tilemap 烘焙 NavMesh，然后 `NavMeshAgent` 就能用。
组件在 `NavMeshComponents/Scripts`（`NavMeshSurface2d` 等），
编辑器菜单在 `NavMeshComponents/Editor`。

**现阶段不要接**：V0.2 只有玩家移动，没有 NPC 追人需求。等 V0.6 做 NPC 巡逻时再用。
另外把烘焙出来的 NavMesh 资产放进 `Assets/Data/StudentGrowth/` 下的独立目录，
别和现有 HomeMap 数据混放。

### 4.4 Yarn Spinner —— NPC 对话 / 多结局分支（V0.6 用）

写 `.yarn` 文本即得对话树，支持变量、条件、选项、`<<jump>>`。

包内已有现成 Prefab 可直接摆进场景：

```text
Library/PackageCache/dev.yarnspinner.unity@bfc5b6a2e8bc/Prefabs/
```

导出 Prefab 到 `Assets/Prefabs/StudentGrowth/NPC/` 再改（别直接改包内文件，升级会丢）。
`.yarn` 脚本建议放 `Assets/Data/StudentGrowth/Dialogue/`。

**与本工程规则的衔接**：选项结果不要直接改属性，应调用
`StudentStatusController` 暴露的统一接口；对话触发活动走 `ActivityController`。
这样才符合 `AGENTS.md` 7.7「选项结果可以修改属性…」但「不要在 UI 里散落改属性」的要求。

---

## 五、待人工确认（我做不了的部分）

MCP 进 Console 通道当前不可用（见 3.4），所以**下面两条需要你在 Unity 里点一下**：

1. **Console 是否 0 error**：
   菜单 `Window → General → Console`，点 `Clear`，然后看是否有红字。
   若想强制重来一次：删掉 `Library/PackageCache/` 下那 4 个目录 → 关闭并重开 Unity。
2. **MCP 是否恢复**：若你要继续用 MCP 改工程，按 `AGENTS.md` 20.4
   「关 Unity → 等 `netstat -ano | findstr :8096` 完全为空 → 重开 Unity」。

另外**打包/发布前**记得：Yarn Spinner 的 `Editor` 与 `SourceGenerator` 程序集只应在编辑器下编译，
若后续要出 IL2CPP 包，先跑一次 Windows 构建确认没有 AOT 报错（这是它的常见坑，不是本工程的）。

---

## 六、相关文件

| 文件 | 作用 |
| --- | --- |
| `Tools/install_git_mirrors.sh` | 建/更新 4 个仓库的本地镜像并写 insteadOf 规则；`--uninstall` 可撤销 |
| `Tools/gh_plugin_scan.py` | 多关键词批量检索 Unity 插件（可读 `GITHUB_TOKEN` 提配额） |
| `Tools/gh_repo_check.py` / `gh_repo_check2.py` | 按仓库精确核验星标/许可/推送时间 |
| `Docs/Unity-Plugin-Candidates-2026-10-20.md` | 候选清单与取舍理由（含劝退项） |
| `_gitcache/` | 本地镜像（174MB，已 gitignore） |
| `_backup_install4plugins_2026-10-20/` | 改动前备份（已 gitignore） |