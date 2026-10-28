# 编辑器内存审计与优化记录（2026-10-28）

> 数据来源：本机扫描（`Logs/art_mem_audit.json`、`Logs/atlas_ref_scan.json`）与
> Unity `Editor.log`。所有体积均为「解压为 RGBA32 后」的估算值 = 宽 × 高 × 4 字节，
> 不含 mipmap（本项目 `enableMipMap: 1` 为 0 张）。

## 一、现象

用户截图显示 Unity Editor 一组 5 个进程合计 6,612 MB，系统内存占用 95%。

实测（15:53 与 15:56 两次采样）：

| 时间 | 进程数 | 合计 | 主进程 | 系统可用 |
|---|---:|---:|---:|---:|
| 15:53 | 5 | 7,509 MB | 3,528 MB | 0.46 GB / 15.31 GB |
| 15:56 | 3 | 5,859 MB | 3,535 MB | 1.58 GB |

主进程内存 12 秒内零变化、CPU 降到 2% 单核 → 导入已结束，非泄漏。
4 个 `AssetImportWorker` 子进程是导入结束后陆续自行退出的。

## 二、根因

1. **3208 / 3513 张贴图是 `textureCompression: 0`（不压缩）**，占 91%。
2. 3209 张 PNG 总像素 353,668,289，全部解码约 **1,349 MB**。
3. 启动触发全量同步导入：`Asset Pipeline Refresh ... Total: 30.182 seconds - Initiated by InitialRefreshV2(ForceSynchronousImport)`。
4. 导入期间堆从 363 MB 涨到 870 MB（`Request Asset Garbage Collect because used heap size increased from 363.5 MB to 0.87 GB`），结束后回落。
5. `Library` 目录：`Artifacts` 1,592 MB、`PackageCache` 2,028 MB、`Search` 444 MB、`Bee` 223 MB、`BurstCache` 147 MB。

按目录解压体积排名（前 12）：

| 目录 | 张数 | 解压 MB | GUID 引用 | 代码路径引用 |
|---|---:|---:|---:|---:|
| Art/Modern_Interiors | 745 | 424 | 0 | 2 |
| Art/Tiles | 133 | 225 | 2 | 4 |
| Art/Pixel | 219 | 206 | 85 | 13 |
| Art/New2_0 | 336 | 136 | 310 | 31 |
| Art/CardReferences | 55 | 84 | 37 | 7 |
| Art/Tilemaps | 52 | 82 | **0** | **0** |
| Art/Characters | 262 | 67 | 6 | 5 |
| Art/Props | 320 | 58 | 0 | 2 |
| Art/StudentGrowth | 5 | 30 | 5 | 1 |
| Art/CharacterReferences | 35 | 20 | 1 | 4 |
| Art/CroppedObjects | 1,027 | 11 | 0 | 1 |
| Art/CardPixel | 1 | 6 | **0** | **0** |
| Art/UI | 4 | ~0 | **0** | **0** |

## 三、本轮已做（零引用、零风险）

移出 `Assets` 到 `_parked_art_20261028/`（git 跟踪，可一键移回）：

| 目录 | 张数 | 解压 MB |
|---|---:|---:|
| Art/Tilemaps | 52 | 82 |
| Art/CardPixel | 1 | 6 |
| Art/UI | 4 | ~0 |
| **合计** | **57** | **约 88** |

移回命令：`git mv _parked_art_20261028/<目录> Assets/Art/<目录>`（`.meta` 一起移，GUID 不变）。

验证：

1. 全库 GUID 扫描（639 个文本资源）三个目录命中 0；
2. 代码路径扫描（319 个 `.cs` 里的 208 处 `"Assets/..."`）命中 0；
3. 重启编辑器后 Console 无 missing / error，`ShopMapScene` 渲染截图与移出前一致。

**未做**：移出前后同一场景的主进程内存对比，88 MB 是理论值。

## 四、待决策的三档方案

### 档 1：移出「有代码路径引用但只在 builder 用」的目录

| 目录 | 可移出 | 保留 | 收益 |
|---|---:|---|---:|
| Art/Modern_Interiors | 741 张 | `4_RPG_MAKER_MV/Tilesets` 下 4 个文件（`0_Inside_A2/A4/B/C_new.png`） | 约 350 MB |
| Art/Tiles | 127 张 | `48x48/ThemeSorter/4_bedroom_48x48.png`、`12_kitchen_48x48.png`、`48x48/floors_only_48x48.png` | 约 200 MB |
| Art/Props | 约 318 张 | `HomeMap/` 子目录 + `ModernInteriors/AnimatedObjects/48x48/Spritesheets/animated_door_1_48x48.png` | 约 50 MB |

**代价**：`NewArtIndoorMapBuilder`、`HomeMapSceneBuilder` 下次重跑会找不到图，
需要先把对应目录移回来。运行中的游戏不受影响。

### 档 2：改贴图压缩

把 `textureCompression` 从 0 改为 1。像素风在块压缩下会出现色块，
**会改变外观**。建议先在 1-2 张图上试，确认可接受再批量。

### 档 3：不改资源，改编辑器行为

- 启动时不加载上次场景：本次实测差异来源——只开轻场景时主进程 1.2 GB，
  打开 `HomeLivingRoomScene` 后 3.5 GB。这一档收益最大，但需要在
  Unity 偏好设置里手动关（我无法操作 GUI，也没有找到可安全写入的配置文件）。
- 关闭 Search 索引：`Library/Search` 444 MB，主要影响磁盘与首次索引时间。
- `Unity Hub` 单独占 484 MB，关掉不影响编辑器。

## 五、第 3 档实测：不加载上次场景（结论：收益有限）

实现方式：把 `Library/LastSceneManagerSetup.txt` 改成 `sceneSetups: []`。
（Unity 6 的编辑器程序集里搜不到 "Load Previous Scene" 字样，无法确认该 UI 选项存在，故走文件机制。）
窗口标题变为「大创项目 - 无标题」，确认生效。

| 配置 | 主进程 WorkingSet |
|---|---:|
| DX12 + `HomeLivingRoomScene` | 3,528 MB |
| DX12 + `ShopMapScene` | 3,315 MB |
| DX12 + 空场景 | 3,131 MB |
| DX11 + 空场景（`-force-d3d11`） | 2,999 MB |

**结论修正**：本文档第二节曾推测「3.5 GB 里约 2.3 GB 来自场景贴图」，该推测**错误**。
空场景下仍占 3.1 GB；场景只影响 200–400 MB，图形 API 只差约 130 MB。
**这 3 GB 是 Unity 6 编辑器加载本项目的固定开销**，与场景、贴图、图形 API 关系都不大。
另：12:36 那次测到的 1.2 GB 是启动初期时机过早，不能作为基线。

该方案不可持续——`LastSceneManagerSetup.txt` 每次 Unity 正常退出都会被重写，已恢复原内容。

## 六、已执行：移除 com.unity.ai.assistant

包缓存里 `com.unity.ai.assistant` 占 503 MB，带 `Microsoft.CodeAnalysis 3.11`、
`System.Collections.Immutable`、`System.Reflection.Metadata` 一整套 Roslyn 编译器服务。

实测（同场景 `ShopMapScene`、同 API DX12、同机）：

| | 主进程 WorkingSet |
|---|---:|
| 移除前 | 3,159 MB |
| 移除后 | **2,971 MB** |
| 差值 | **188 MB（约 6%）** |

验证：`Assets` 下无 `.cs` / `.asmdef` 引用 `Unity.AI`（grep 0 命中）；编译 0 error；
Console 无 missing / Exception；`PackageCache` 中该包已消失；场景渲染截图一致。
可逆：Package Manager 重装，或 `git revert`。

## 七、事实与推测分界

**可验证事实**：所有张数、解压体积、GUID 引用数、代码路径引用数、`Editor.log` 原文、
进程 `WorkingSet`、`Library` 各目录体积、`textureCompression` 分布。

**推测（无直接证据）**：主进程 3.5 GB 中约 2.9 GB 是原生内存与 DX12 显存映射
（Unity 自报托管堆仅 0.6 GB）；移出资源后的实际内存降幅；各档方案的实际收益量级。

## 六、复现方式

```bash
# 重跑引用扫描（只读）
python -c "..."   # 脚本见 Logs/art_mem_audit.json 的生成过程
```

扫描要点：读每张 PNG 的 `.meta` 取 `guid:`，再扫 639 个文本资源里的
`guid: <32 hex>`；代码路径引用另扫 `.cs` 里的 `"Assets/..."` 字符串。
**注意正则里不要用 `\s`**——bash heredoc 传参时会被吞掉，导致全部误判为零引用。
