# GitHub 插件候选清单（Unity 2D 学生成长 RPG）

> **更新（2026-10-20）**：其中 4 个已实际安装并验证通过，
> 见 [Unity-Plugin-Install-2026-10-20.md](<Unity-Plugin-Install-2026-10-20.md>)。
> 本文件保留完整的候选与取舍理由（含劝退项）。

- 盘点日期：2026-10-20
- 目标工程：`E:\大创项目`，Unity 6000.4.6f1 + URP 17.4.0 + 新版 Input System 1.19.0
- 数据来源：GitHub 官方 REST API（`api.github.com`）实时查询，非记忆、非二手榜单
- 可复现脚本：`Tools/gh_plugin_scan.py`、`Tools/gh_repo_check.py`、`Tools/gh_repo_check2.py`
- 星标与推送时间均为查询当刻的真实值；`pushed` = 仓库最后一次推送日期

> 说明：本项目 `AGENTS.md` 明确写着「在没有必要时引入第三方插件」属于禁止事项。
> 因此本清单只列**能直接解决当前版本痛点**的插件，并为每条标注「当前是否需要」。
> 标注 ❌ 的表示现阶段不要装。

---

## 一、与 V0.2 / V0.3 直接相关（建议评估）

| 插件 | 星标 | 许可 | 最近推送 | 能解决什么 | 现阶段 |
| --- | --- | --- | --- | --- | --- |
| [h8man/NavMeshPlus](https://github.com/h8man/NavMeshPlus) | 2359 | MIT | 2026-10-18 | 让 Unity 原生 NavMesh 支持 2D Collider/Tilemap，NPC 自动绕家具走 | ✅ 已装 0.2.23；但**现在别接**，V0.6 做 NPC 巡逻再用 |
| [sturdyspoon/unity-pathfinding](https://github.com/sturdyspoon/unity-pathfinding) | 111 | MIT | 2021-03-01 | 直接在 Tilemap 上跑 A*，代码量极小（单文件级） | 若要「点击地面自动走过去」可选它，比 NavMeshPlus 轻 |
| [pixelfac/2D-Astar-Pathfinding-in-Unity](https://github.com/pixelfac/2D-Astar-Pathfinding-in-Unity) | 50 | MIT | 2022-11-17 | 同上，教学向实现 | ❌ 与上一条重复，不必两个都装 |
| [AnnulusGames/LitMotion](https://github.com/annulusgames/LitMotion) | 2343 | MIT | 2026-09-18 | 零 GC 补间动画，做交互提示浮动、卡牌飞入、场景淡入淡出不用手写协程 | ✅ 已装 2.0.2（依赖的 Burst/Collections/Mathematics 工程原本就有，未降级） |
| [KyryloKuzyk/PrimeTween](https://github.com/KyryloKuzyk/PrimeTween) | 2007 | 自定义（需核对） | 2026-10-15 | 性能更强的补间库 | ⚠️ 许可证非标准 MIT，商用可能收费，学生项目用前先读 LICENSE |
| [mob-sakai/UIEffect](https://github.com/mob-sakai/UIEffect) | 7691 | MIT | 2026-08-27 | UI 灰度/描边/溶解/闪光，做像素风 UI 反馈和卡牌特效 | ✅ 已装 5.11.7（`?path=Packages/src#main`，**勿用 `#v4.0.0-preview.10`**，那版包在子目录，UPM 找不到） |
| [mob-sakai/ParticleEffectForUGUI](https://github.com/mob-sakai/ParticleEffectForUGUI) | 6025 | MIT | 2026-10-16 | 粒子直接渲染在 uGUI 上，不需要额外相机/RT，适合伤害数字、升级光效 | V0.4 推荐 |

## 二、对话 / 剧情 / 分支结局（V0.5–V0.6 用）

| 插件 | 星标 | 许可 | 最近推送 | 定位 | 建议 |
| --- | --- | --- | --- | --- | --- |
| [YarnSpinnerTool/YarnSpinner-Unity](https://github.com/YarnSpinnerTool/YarnSpinner-Unity) | 841 | MIT | 2026-10-14 | **实际要装的是这个**：官方 Unity 集成，根目录即 UPM 包 | ✅ 已装 v3.2.8 |
| [YarnSpinnerTool/YarnSpinner](https://github.com/YarnSpinnerTool/YarnSpinner) | 2857 | MIT | 2026-10-14 | 核心编译器（引擎无关），**根目录无 package.json，不能直接当 UPM 装** | ⚠️ 勿单独安装 |
| [inkle/ink](https://github.com/inkle/ink) | 4981 | MIT | 2026-05-05 | 更强的叙事脚本语言（《80 Days》在用） | ✅ 若剧情分支极复杂则选它 |
| [inkle/ink-unity-integration](https://github.com/inkle/ink-unity-integration) | 726 | 自定义（需核对） | 2026-07-08 | ink 的 Unity 集成包 | 配 ink 使用 |
| [snozbot/fungus](https://github.com/snozbot/fungus) | 1951 | MIT | 2025-01-16 | 可视节点编排的互动小说框架 | ⚠️ 偏视觉小说、与自研 Controller 架构重叠，谨慎 |
| [Siccity/xNode](https://github.com/Siccity/xNode) | 3742 | MIT | 2024-08-26 | 通用节点图编辑器框架，可拿来给「活动流程/事件」做可视化编辑 | V0.5 后若要自研剧情编辑器，用它当地基 |

## 三、存档 / 数据（V0.5 用）

| 插件 | 星标 | 许可 | 最近推送 | 说明 | 建议 |
| --- | --- | --- | --- | --- | --- |
| 项目已内置 `com.unity.nuget.newtonsoft-json` 3.2.2 | — | — | — | 官方包，已在 `Packages/manifest.json` 里 | ✅ **首选**，直接写 JSON 存档，零新增风险 |
| [BayatGames/SaveGameFree](https://github.com/BayatGames/SaveGameFree) | 669 | MIT | 2025-10-06 | 跨平台存档（含 WebGL/移动端路径处理） | 仅当要发布到 WebGL/安卓再考虑 |

## 四、工具库 / 基础能力（按需）

| 插件 | 星标 | 许可 | 最近推送 | 用途 |
| --- | --- | --- | --- | --- |
| [Cysharp/UniTask](https://github.com/Cysharp/UniTask) | 11259 | MIT | 2026-10-06 | async/await 无 GC 版，做加载、延时、淡入淡出比协程干净 |
| [yasirkula/UnityIngameDebugConsole](https://github.com/yasirkula/UnityIngameDebugConsole) | 未核验 | MIT | — | 打包后手机上直接开控制台看日志、改属性，做展示版很有用 |
| [yasirkula/UnityRuntimeInspector](https://github.com/yasirkula/UnityRuntimeInspector) | 未核验 | MIT | — | 运行时 Inspector，调数值不用反复重编译 |
| [Unity-Technologies/InputSystem](https://github.com/Unity-Technologies/InputSystem) | 1528 | Unity 官方 | 2026-10-12 | 已是项目依赖，源码可查 |
| [Unity-Technologies/UnityCsReference](https://github.com/Unity-Technologies/UnityCsReference) | 13037 | Unity 官方 | 2026-10-15 | Unity C# 源码参考，查 API 行为用 |

## 五、明确不建议现在引入

| 对象 | 原因 |
| --- | --- |
| Aron Granberg《A* Pathfinding Project》 | 核心 Pro 功能收费，免费版功能被 NavMeshPlus 覆盖 |
| [neuecc/UniRx](https://github.com/neuecc/UniRx) | 已 archived（2024-02-16 最后推送），替代品是 [Cysharp/R3](https://github.com/Cysharp/R3)（4022★, MIT） |
| [EllanJiang/GameFramework](https://github.com/EllanJiang/GameFramework) | 6910★ 但 2023-09 后停更，整套框架会与现有 Controller/Model 分层冲突 |
| Fungus / 大型 RPG 模板工程 | 与 `AGENTS.md` 的 UI-Controller-Data 分层直接冲突，等于推翻现有实现 |
| 各类「Unity 2D RPG 完整模板」仓库 | 多为个人练手工程（几十星），代码质量不可控，且本项目已有自研主循环 |
| Naninovel / Odin Inspector | 商业付费插件，学生项目预算外 |

---

## 六、接入前必须做的三件事

1. **只通过 UPM 或 `Packages/` 引入，不要往 `Assets/` 里扔**
   直接解压到 `Assets/` 会引入新的 `.meta` GUID、可能触发整库重编译，而本项目 recompile 会让 mcp-unity 打半残（见 `AGENTS.md` 20.4）。
2. **一次只装一个，装完立刻看 Console**
   顺序建议：先装 LitMotion（最独立、影响面最小）→ 确认 0 error → 再考虑下一个。
3. **装前备份**
   按 `AGENTS.md` 20.3，改任何文件前先备份到 `_backup_<事项>_<日期>\`，`Packages/manifest.json` 和 `Packages/packages-lock.json` 要一起备份。

## 七、复现 / 扩展检索

```bash
# 多关键词批量检索（搜索 API 未认证限 10 次/分钟，脚本已内置 1.2s 间隔）
python Tools/gh_plugin_scan.py

# 按仓库精确核验（配额独立、更宽松）
python Tools/gh_repo_check.py
python Tools/gh_repo_check2.py

# 想多查几轮，先设 token 再跑，配额提升到 30 次/分钟
set GITHUB_TOKEN=ghp_xxx
python Tools/gh_plugin_scan.py
```

脚本遇到 `403 rate limit exceeded` 属正常现象，等 1 分钟重跑即可，不是脚本坏了。