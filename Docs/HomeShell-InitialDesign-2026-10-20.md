# 家用房间壳「按初版设计」复原记录（2026-10-20 晚）

## 一、起因与依据

用户要求：**去 git 上找初版的家庭环境，按它的房间壳去设计**（后续补充说明"包括地板和墙壁"）。

### 初版是哪个提交

```text
git log --oneline
  20b8503 2026-09-23 feat: 体检工具加「可达性」维度…      <- HEAD
  0babae4 2026-09-23 feat: 新增内容体检工具（B1）
  a77203e 2026-09-23 chore: 新增 .gitattributes
  091f0f6 2026-09-23 chore: 建立 V0.2 可跑基线（B0）
  900848a 2026-09-07 Initial commit: student growth Unity RPG   <- 初版
```

仓库只有 **5 个提交**，初版 = `900848a`。

### 关键事实：初版素材至今未改，所以"初版"就是这两个文件本身

```text
Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png     初版:1a3b84a12c  HEAD:1a3b84a12c  未变
Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_reference.png 初版:4243b61722  HEAD:4243b61722  未变
Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_item.png      初版:454480d26a  HEAD:454480d26a  未变
```

- `home_new_space.png`（1448×1086）= **初版空房间壳**，是权威壳
- `home_new_reference.png`（1448×1086）= **初版完整室内设计图**（家具版），用于交叉验证墙色
- `Assets/Scenes/StudentGrowth/Maps/HomeMapScene.unity` 在所有 5 个提交里是**同一个 blob
  （`639debdd`）**，即家地图场景本身从未变过
- 四间房场景（`HomeLivingRoomScene` 等）**完全未被 git 跟踪**，是本地新建的

远端：`origin https://github.com/qwq1158254109-afk/game.git`

---

## 二、初版房间壳的真实结构（实测）

工具：`Tools/probe_initial_shell.py`、`Tools/probe_initial_wall_layers.py`

初版壳北墙逐行精量（x200..500 净面，避开窗与立柱）：

| y | 内容 | 亮度 L |
| --- | --- | --- |
| 28–35 | 图外暗边 | 8.7 |
| 36 | 极暗铰链线 | 3.8 |
| 38–63 | **深木上框** | 46 → 58 |
| 64 | 暗分线 | 43 |
| 65–67 | 过渡（木→奶油） | 106 → 142 |
| **68–265** | **奶油墙面** | 148 → 124（自带纵深变暗） |
| 266+ | 直接切进地板 | 62 |

**结论：初版墙只有两层——深木框 + 奶油墙面，没有独立踢脚线。**

---

## 三、旧口径错在哪（三处）

| # | 旧做法 | 问题 |
| --- | --- | --- |
| 1 | `BASE` 取 y264..296 当"踢脚" | **那是地板**。把地板当墙用，在墙内侧压出一条深棕带，读作"墙里又嵌了一圈框" |
| 2 | `FACE` 取 y68..263 整段 195px **压到 87px** | 缩放把墙面固有纵深（148→124）压扁，墙"发飘" |
| 3 | `TRIM` 取 y40..65 深木框 13px、`EDGE` 取 y36..38 | 分段边界落在铰链线上，比例被带偏 |

而"参照初版"还有一层意义：初版是**整张壳图**（`home_new_space.png` 直接当底），
学校 `SchoolShell.png` 也是同一思路。当前管线是"材质砖拼四间房"，属于后来的拆解。

---

## 四、本轮改了什么

只改**取材口径与分段**，**几何一个像素都没动**。

### `Tools/make_shell_tex.py`

| 项 | 旧 | 新 |
| --- | --- | --- |
| 墙带分段 | 外沿4 + 压边13 + 墙面87 + 踢脚24 | **外沿4 + 深木上框16 + 奶油墙面108** |
| 墙面取源 | y68..263 压到 87px | **y110..218 逐像素 1:1（108px，不缩放）** |
| 深木框取源 | y40..65 → 13px | y38..62 → 16px（LANCZOS） |
| 踢脚 | y264..296（地板） | **取消**（初版没有） |
| 地板取源 | y620..740 | **y440..560**（实测该段垂直缝 4.30、行内 std 5.03 最干净） |

地板取源另有一处修正：先曾试 y262..382 / y400..520，两者都压在**墙脚交界带**上，
取出来当砖顶会自带一道暗边，叠成整面地板后出现规律性横向条纹
（实测垂直缝 40.85 / 14.90）；挪到 y440..560 后降到 **2.03**。

### 未改动

`Tools/make_rooms.py`、`HomeRoomsBuilder.cs`、`RoomScreenshotCapture.cs` 一行未动——
画布 1888×1120、墙厚 128、门 128×128、可走区 25.5×13.5、ortho 8.75 全部保持。

---

## 五、验证证据

### 几何校验（`python Tools/verify_shell.py`）— **PASS**

```text
living    left   (64,560)             OK   0.0000   OK
bedroom   left   (64,560)             OK   0.0000   OK
study     right  (1824,560)           OK   0.0000   OK
kitchen   bottom (944,1056)           OK   0.0000   OK
尺寸/色数: 四间均 1888x1120，可走区 25.50 x 13.50
北墙条色数: 595 -> 1221（×2.1）
总计: PASS
```

门洞锚点偏差全部 `0.0000` → 底图里的门与场景里的门仍严格同位。

### 材质口径对照初版

| 段 | 新生成 | 初版源图 |
| --- | --- | --- |
| edge 外沿 | L=8.6 | 8.7 |
| top 深木上框 | L=65.8 | ~55 |
| face 奶油墙面 | L=165.2（R-B 83.6） | 148→124（R-B 83） |
| floor 地板 | L=104.9，垂直缝 2.03 | 纯地板段 104~112 |

### 尺码未变

```text
wall_band_h  240x128   （脚本断言 bh == W_TOP == 128，不符会直接抛错）
floor_tile   360x360
room_*.png   1888x1120 × 4 张
```

### 改动量（底图）

```text
改动像素占比 80.8%，最大通道差 152，完全未变区域 1.4%
北墙带 y0..128 改动 63.0% ／ 地板中部 87.1% ／ 南墙带 61.9%
```

### 目检图

- `Tools/_home_shell_before_after.png` —— 上=旧底图 / 下=新底图（整间厨房）
- `Tools/_wallband_compare3.png` —— 左=初版源图同段 1:1 / 中=旧墙带 / 右=新墙带
  （这张最能说明问题：旧版在奶油墙下方压了一条**深棕踢脚**，而新版与初版结构一致）

---

## 六、备份与回滚

```bash
# 备份（改前）
_backup_homeshell_initial_2026-10-20/
  Tools/make_shell_tex.py            md5 a24556515d2cfe47e0084facefefffcb
  Tools/make_rooms.py                md5 6ac92e6d4f239fb2c76c7b02934e9e65
  Assets/Art/Pixel/Generated/HomeRooms/Materials/{floor_tile,wall_band_h,wall_band_v}.png
  Assets/Art/Pixel/Generated/HomeRooms/room_{living,bedroom,study,kitchen}.png

# 回滚
cp _backup_homeshell_initial_2026-10-20/Tools/*.py Tools/
cp _backup_homeshell_initial_2026-10-20/Assets/Art/Pixel/Generated/HomeRooms/Materials/*.png \
   Assets/Art/Pixel/Generated/HomeRooms/Materials/
cp _backup_homeshell_initial_2026-10-20/Assets/Art/Pixel/Generated/HomeRooms/room_*.png \
   Assets/Art/Pixel/Generated/HomeRooms/
```

**未改任何 .meta / GUID**，所以场景对底图的引用不会断。

---

## 七、一个尚未解决的取舍（需用户定）

初版是**透视视角**（能看到墙面与地板成角度），我们的房间是**俯视带墙带**，
所以墙面只能以"顶边一条带"呈现。当前墙带厚 128px = 画布高的 11.4%；
初版里墙面约占画面高的 30% 以上。

**要让墙在画面里更有存在感，只能加厚墙带 —— 而墙厚是几何的一部分**
（`make_rooms.py` 的 `W_SIDE/W_TOP/W_BOTTOM`、`HomeRoomsBuilder.cs` 的
`WallThick`/`WalkW`/`WalkH`/所有 `WallSpec`、以及全部 `DoorSpec` 位置），
按 `AGENTS.md` 20.5 属于「连锁改动」，漏一处就不一致。

因此本轮**只做材质口径复原，不动几何**。是否加厚墙带留待确认。

## 八、待办

- 未在 Unity 里跑 `Dev/Capture Room Screenshot` 出实机对照图（需求 Unity 侧执行；本轮是纯图像管线改动）
- 未进 Play 目视
- 四间房底图已重生成，但**场景是脚本重建的**：若之后跑
  `Tools/Student Growth/Maps/Build Home Room Scenes`，会以新底图重建；旧场景不受影响