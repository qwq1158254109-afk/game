# 家用房间壳「照最新版（学校壳）设计」记录（2026-10-20 深夜）

> 前一份 `Docs/HomeShell-InitialDesign-2026-10-20.md` 记的是"照初版"那一轮。
> **用户随后更正：不是初版，是最新版**，并要求「地板也要和 git 上的保持一致」「加厚」。
> 本文件覆盖那一轮的结论。

---

## 一、先纠正：git 里没有"最新版的家"

```text
git log  —— 全仓只有 5 个提交
  20b8503 2026-09-23  …可达性…（HEAD）
  0babae4 2026-09-23  …内容体检工具…
  a77203e 2026-09-23  …gitattributes…
  091f0f6 2026-09-23  …V0.2 可跑基线…
  900848a 2026-09-07  Initial commit: student growth Unity RPG
```

各提交改动的**家相关文件数**：

```text
20b8503: 0    0babae4: 0    a77203e: 0    091f0f6: 0    900848a: 327
```

即：家资源**只在初版那一次进过 git**，之后所有改动都在工作区、从未提交。
所以"最新版的家"不在 git 里 —— 它指的是**最新版地点套件**（学校/图书馆/社团/公园）。

## 二、用像素比对把"地板出自哪"钉死

`Tools/probe_floor_provenance.py`：从用户截图取一块纯地板（48×48，无家具），
在各壳图的地板区按原尺度滑动搜索，取平均绝对差（MAD）：

| 壳 | MAD | 结论 |
| --- | --- | --- |
| **School/`SchoolShell.png`** | **4.66** | **最小 → 地板来源** |
| Club/`ClubShell.png` | 7.07 | |
| `home_new_reference.png` | 8.74 | |
| `home_new_space.png` | 9.44 | |

色差也吻合：

| | 截图地板 | SchoolShell 地板 | 家源图地板 |
| --- | --- | --- | --- |
| RGB | (150.6, 96.9, 56.0) | **(150.4, 96.6, 57.4)** | (168.6, 97.7, 46.9) |
| R-B | 94.6 | **93.0** | 121.6（明显偏橙） |

**结论：地板/墙按 `Assets/Art/New2_0/School/Source/SchoolShell.png`（git HEAD 内）来做。**

> 一处自我纠错：先前 `probe_shell_style.py` 把 x620..1130 / y235..285 当成"护墙板"取样，
> 实际取到了**窗帘**（R-B 1.6、偏绿）。已改用多点取样并加"偏绿率"剔除，
> 见 `Tools/probe_school_shell_colors.py`。

## 三、最新版地点套件的统一口径（关键）

| 壳 | 尺寸 | 宽高比 | 相机 ortho |
| --- | --- | --- | --- |
| School / Library / Club / Park `*Shell.png` | **1536×1024** | **1.500（3:2）** | 全部 **8** |
| 家现状 `room_*.png` | 1888×1120 | 1.686 | 8.75 |

学校壳 PPU=64 → 24×16 世界单位；ortho 8 + 3:2 → 可见 `2×8×1.5 = 24`，
**正好等于画布宽，零裁剪**。家的画布 29.5 宽 > 可见 31.11（16:9）之外的量，
**左右各被裁 0.81 单位**，侧墙只剩 1.19/2.0 可见 —— 这是"墙显得薄"的结构性原因。

## 四、本轮改了什么（几何未动）

`Tools/make_shell_tex.py` 的**取源全部换成 SchoolShell**，并按它的四段节奏组装墙带：

| 段落 | 高 | 取源 | 生成值 | 学校实测 |
| --- | --- | --- | --- | --- |
| 外沿(暗) | 4 | y28..31 | — | — |
| 木框/上沿 | 12 | y31..45 | — | — |
| **奶油墙面** | 62 | y80..200 | **L=162.7 R-B=75.4** | L=163.8 R-B=74.9 |
| **深木护墙板** | 40 | y240..300 | **L=57.4 R-B=47.0** | L=57.8 R-B=46.9 |
| 墙脚收边 | 10 | y300..312 | — | — |
| **地板砖** | — | y500..620 | **L=101.1 R-B=93.4** | L=101.3 R-B=93.1 |

**护墙板是本轮新增的段落**——此前家的墙带根本没有护墙板，这是"照学校做"最明显的缺失。
墙面/护墙板/地板三段色值与学校几乎逐项相同。

### 修掉的一个自制缺陷

护墙板源区带**竖向木板纹理**，而墙带是**横向平铺**的；加上原先对每段都用
`mirror_seamless`，镜像中心在 460px 周期处产生**突变**，成品最上沿出现规律横向条纹。

改法：护墙板改用 `crossfade_seamless()`（首尾交叉淡化，平铺天然连续），
其余段仍用镜像。修复后护墙板段水平缝 2.14、整带水平缝 0.67。

## 五、验证

```text
python Tools/verify_shell.py   ->  总计: PASS
  四间房门洞锚点偏差全部 0.0000；尺寸仍 1888x1120；可走区仍 25.50 x 13.50
  北墙条色数 595 -> 4435（×7.5）
```

材质/几何尺寸未变：`wall_band_h` 460×128、`floor_tile` 400×360、`room_*` 1888×1120。
`make_rooms.py`、`HomeRoomsBuilder.cs` **一行未动** → 碰撞、门洞、装饰坐标全部不受影响。

目检图（存 `Docs/Screenshots/`）：

- `home_shell_school_match_20261020.png` —— 上=改前 / 下=照学校壳
- `home_wallband_school_20261020.png` —— 左=学校源 1:1 / 中=改前 / 右=改后

## 六、方案B 加厚（**已实施**）

用户选定方案 B。所有派生量用脚本算出（不手算），几何一致性已核对。

### 新几何

```text
墙厚          2.0 -> 2.5 世界单位（128 -> 160 px），四墙等厚
画布          1888x1120 -> 1991x1184（29.50x17.50 -> 31.109x18.50 单位）
相机 ortho    8.75 -> 9.25（= 画布高/2，纵向零裁）
可走区        25.50x13.50 -> 26.11x13.50（只变宽，高度不变）
门洞/门素材   128x128 -> 160x160
16:9 可见宽   32.889 单位 -> 横向每边仅余 0.89 单位（外沿可见，不再被裁）
```

取 **1991** 而非 2106 的原因：1991/64 = 31.109 单位，与 ortho 9.25 在 16:9 下的可见宽
严丝合缝；2106 会左右各留 0.89 单位空隙。

### 改动文件（5 处连锁，全部已改）

| 文件 | 改动 |
| --- | --- |
| `Tools/make_shell_tex.py` | `BAND_H` 128→**160**；五段重分为 外沿4/木框15/墙面76/护墙板51/收边14；各段取源按新高度等比放大（护墙板 y226..300，避开墙脚过渡带） |
| `Tools/make_rooms.py` | `CANVAS_W/H`→1991/1184；`W_SIDE/W_TOP/W_BOTTOM`→160；`DOOR_W/DOOR_HOLE`→160 |
| `Assets/Editor/StudentGrowth/HomeRoomsBuilder.cs` | `WallThick`→2.5；`RoomW/H`→31.11/18.5；`Ortho`/`LivingOrtho`→9.25；`WalkW`→26.11；`InnerHalfW`→13.055；**全部 WallSpec（24 处）+ 全部 DoorSpec（6 处）+ 贴东西墙的家具/装饰（8 处，x 外移 0.305）** |
| `Assets/Editor/StudentGrowth/RoomScreenshotCapture.cs` | `Width/Height`→1991/1184 |
| `Tools/verify_shell.py` | 期望值同步（含此前遗漏的硬编码 `HOLE = 128`→160） |

WallSpec / DoorSpec 的改写由 `Tools/apply_room_thickness.py` 完成：
**逐条校验命中数，不符预期就整批中止不落盘**（预演两次才通过，共替换 **48 处**）。
这个"命中数闸门"当场抓出了三处计数错误（客厅/厨房共用南墙段、
`±13.75f, 0f, 2.0f, RoomH` 的实际分布与我推理的相反）。

### 验证

```text
python Tools/verify_shell.py   ->  总计: PASS
  四间房门洞锚点偏差全部 0.0000
  living  left(80,592) / right(1911,592) / top(611,80) / bottom(995,1104)
  bedroom left(80,592)      study right(1911,592)      kitchen bottom(995,1104)
  尺寸：四间均 1991x1184
  北墙条色数 595 -> 6388（×10.7）

尺寸一致性：
  room_*.png          1991x1184  x4
  wall_band_h/v       460x160 / 160x460
  home_ref_door_*     160x160   x4（含 3 帧开门）
  floor_tile          400x360
```

编译：`Assembly-CSharp-Editor.dll` 于 23:59:55 重建、**0 error**
（`Assets/Editor/` 下的代码编入预定义程序集，不在 `StudentGrowth.Editor.dll` 里）。

### 仍未做的一步

**四间房场景尚未重建** —— `Tools/Student Growth/Maps/Build Home Room Scenes`
需要经 Unity 执行，而当时 MCP 已断（`get_play_mode_status` 超时）。
底图与代码都已就绪，重建后场景几何才会与底图一致。

场景已备份到 `_backup_roomthick_2026-10-20/Assets/Scenes/StudentGrowth/Maps/`，
重建后可用它对比。

## 七、回滚

```bash
cp _backup_homeshell_initial_2026-10-20/Tools/*.py Tools/
cp _backup_homeshell_initial_2026-10-20/Assets/Art/Pixel/Generated/HomeRooms/Materials/*.png \
   Assets/Art/Pixel/Generated/HomeRooms/Materials/
cp _backup_homeshell_initial_2026-10-20/Assets/Art/Pixel/Generated/HomeRooms/room_*.png \
   Assets/Art/Pixel/Generated/HomeRooms/
```

未改任何 `.meta` / GUID；`Assets/Art/New2_0/School/**` 为只读取源，未被修改。