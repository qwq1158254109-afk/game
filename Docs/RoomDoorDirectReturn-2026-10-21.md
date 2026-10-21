# 房间门「落地即可原路返回」修复

- 时间：2026-10-21 13:15（Asia/Shanghai）
- 上一份：`Docs/handoff-20261021-123718.md`
- 用户原话：**「现在进入下一个场景之后还得先离开交互范围才能回去，我想要直接能回去」**

---

## 1. 根因（两条叠加，缺一不成）

### 1.1 落地点落在门触发区**里面**

四个房间的出生点全部压在各自那扇门的触发区里：

| 房间 | 出生点 | 门触发区（AABB） | 是否重叠 |
| --- | --- | --- | --- |
| 客厅 | `spawn_from_bedroom (11.95, 0)` | 东门 x∈[11.255, 12.155] | **重叠** |
| 客厅 | `spawn_from_study (-11.95, 0)` | 西门 x∈[-12.155, -11.255] | **重叠** |
| 客厅 | `spawn_from_kitchen (-6, 5.85)` | 北门 y∈[4.95, 5.85] | **重叠** |
| 卧室 | `spawn_from_living (-11.95, 0)` | 西门 x∈[-12.155, -11.255] | **重叠** |
| 书房 | `spawn_from_living (11.95, 0)` | 东门 x∈[11.255, 12.155] | **重叠** |
| 厨房 | `spawn_from_living (0, -4.9)` | 南门 y∈[-5.85, -4.95] | **重叠** |

### 1.2 `OnTriggerEnter2D` 对「一开始就重叠」不触发

玩家一落地，碰撞体**已经**和触发区重叠。物理引擎不会补发 `OnTriggerEnter2D`——
没有"进入"这个事件，只有"一直待着"。所以 `RoomDoor` 完全收不到信号。

### 1.3 `armDelay` 把落地那一帧也挡掉了

`RoomDoor.armDelay = 0.7f` 原本是为了防"刚进新房间就被门口触发区弹回去"。
它配合 1.2 的效果是：落地瞬间那唯一一次可能的进入判定也被吞了。

**最终表现**：进房间 → 站在门口按方向键朝门走 → 门毫无反应 → 必须先走到房间中间
（离开触发区）再走回来，`OnTriggerEnter2D` 才会触发。这正是用户报的现象。

---

## 2. 修法（三层，互相兜底）

### 2.1 出生点挪到触发区外（治本）

`HomeRoomsBuilder.cs` 的 `SpawnSpec`：

| 房间 | 出生点 id | 旧 | 新 |
| --- | --- | --- | --- |
| 客厅 | `spawn_from_bedroom` | 11.95, 0 | **10.7, 0** |
| 客厅 | `spawn_from_study` | -11.95, 0 | **-10.7, 0** |
| 客厅 | `spawn_from_kitchen` | -6, 5.85 | **-6, 4.5** |
| 卧室 | `spawn_from_living` | -11.95, 0 | **-10.7, 0** |
| 书房 | `spawn_from_living` | 11.95, 0 | **10.7, 0** |
| 厨房 | `spawn_from_living` | 0, -4.9 | **0, -3.9** |

计算口径（与 `PlayerRpg` 的 `CapsuleCollider2D` size (0.68,1.05) / offset (0,-0.32) 一致）：
- 玩家半宽 0.34、胶囊底 -0.845、胶囊顶 +0.205
- 东西门：触发区内沿 11.255 → 中心 ≤ 11.255 - 0.34 = **10.915**，取 10.7（留 0.2 余量）
- 北门：触发区下沿 4.95 → 中心 ≤ 4.95 - 0.205 = **4.745**，取 4.5
- 南门：触发区上沿 -4.95 → 中心 ≥ -4.95 + 0.845 = **-4.105**，取 -3.9

**已用脚本核对：6 个落地点均不与任何实体碰撞体（家具/墙）重叠。**

### 2.2 `RoomDoor` 支持「人已经在区里」（兜底）

不再只靠 `OnTriggerEnter2D`：

- `OnTriggerEnter2D` → 记录 `playerInside` 并立即尝试进入（原行为保留）
- `OnTriggerStay2D` → 持续刷新 `playerInside`（覆盖"一开始就在里面"的情况）
- `OnTriggerExit2D` → 清空
- **`Update`** → 若 `playerInside != null` 且玩家**正在移动**且**朝向与"指向门中心"点积 ≥ 0.5**（±60°），才触发

加方向判定是为了避免两个新问题：站在门口不动被吸走、贴着门横向走被带走。

`armDelay` 从 0.7 降到 **0.35**（只挡落地那一帧，不再需要长时间静默）。

### 2.3 落地面朝房间内部（防自触发）

`RoomDoorRouter.Transition` 里新增 `FaceRoomInterior(survivor)`：
落地后把玩家朝向设为「指向房间中心」的方向（`-position`）。

不设的话玩家会保留进门时的朝向（通常朝门），配合 2.2 的方向判定，
落地瞬间就可能被判成"朝门走"而立刻弹回上一个房间。

---

## 3. 新增防回归闸门

`HomeRoomsBuilder.ValidateSpawnClearance(spec, doorRoot)`：

每次重建房间场景时，用玩家胶囊 AABB 与所有门触发区做重叠检测，
**命中就 `Debug.LogError`**（不静默通过），报出房间名、出生点 id、门名、重叠量、
以及"至少要挪开多少单位"。

这样以后调家具、调门洞、调墙厚时，不会再把出生点悄悄挪回门里。

---

## 4. 运行时探针实测（已通过，探针已删）

探针 `RoomDoorReturnProbe` 用 `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]` +
`DontDestroyOnLoad`，自己 `LoadScene("HomeLivingRoomScene")` 后驱动玩家走位，
把每一步写进 `_roomdoor_probe.txt`。**已用后即删**（`.cs` + `.meta` + 日志全删，
`Assets/Refresh` 后 recompile 0 error）。

实测输出：

```text
A. 客厅起点 pos=(0.00, -4.90) 面朝=(0.00, -1.00)
   door DoorToBedroom @(11.71, 0.00) size=0.9x1.9 -> HomeBedroomScene | 玩家距门 12.69
   走 走向东门 (0.00, -4.90) -> (11.70, 0.00)
   到达 (11.38, -0.13)
B. 走东门之后 scene=HomeBedroomScene pos=(-10.70, 0.00)
C. 卧室落地点 pos=(-10.70, 0.00) 面朝=(1.00, 0.00)      ← 朝房间内侧，不是朝门
   door DoorToLiving @(-11.71, 0.00) size=0.9x1.9 -> HomeLivingRoomScene | 玩家距门 1.01
D. 从落地点直接朝门走（不先离开交互范围）
   走了 2.5s，停在 (5.14, 0.00)
E. 往回走之后 scene=HomeLivingRoomScene pos=(5.14, 0.00)
RESULT: PASS —— 落地后直接往回走就能回去
```

三条关键证据：
1. 落地点 `-10.70` 在触发区 `[-12.155, -11.255]` **之外**（1.01 距离，胶囊半宽 0.34 → 净空 0.67）
2. 落地面朝 `(1.00, 0.00)` = 指向房间中心（卧室门在西墙，朝东即朝内）
3. **没有先离开交互范围**，直接朝门走就切回了客厅

---

## 5. 改动文件

| 文件 | 改动 |
| --- | --- |
| `Assets/Editor/StudentGrowth/HomeRoomsBuilder.cs` | 6 个 SpawnSpec 位置；新增 `ValidateSpawnClearance()` 及其调用 |
| `Assets/Scripts/StudentGrowth/Map/RoomDoor.cs` | 重写：`OnTriggerStay2D` 记录区内玩家；`Update` 按"移动 + 朝门"补触发；`armDelay` 0.7 → 0.35 |
| `Assets/Scripts/StudentGrowth/Scene/RoomDoorRouter.cs` | 新增 `FaceRoomInterior()`，落地后面朝房间内侧 |
| `Assets/Scenes/StudentGrowth/Maps/HomeLivingRoomScene.unity` | 重建（出生点更新） |
| `Assets/Scenes/StudentGrowth/Maps/HomeBedroomScene.unity` | 重建 |
| `Assets/Scenes/StudentGrowth/Maps/HomeStudyRoomScene.unity` | 重建 |
| `Assets/Scenes/StudentGrowth/Maps/HomeKitchenScene.unity` | 重建 |

备份：`_backup_roomdoorreturn_2026-10-21/`（6 个文件，保留原目录结构）

---

## 6. 待人工确认

1. **键盘实感**：探针是直接推 `transform.position` 驱动移动的，
   不是注入真实按键。需要用户在 Play 里用 WASD 走一遍：
   `客厅 → 东门进卧室 → 立刻按 A（朝西墙门走）→ 应能回客厅`。
2. **站立不误触发**：站在门口不动，不应被吸走。
3. **横向走不误触发**：贴着门沿墙横走，不应被带走。
4. **正常进门仍有开门动画 + 黑场**（`RoomDoor.TryEnter` 路径未改逻辑，只是入口多了两个）。

---

## 7. 环境事实补充（本轮新踩）

- **`recompile_scripts` 有时不重编**：源文件 13:10 改完，`StudentGrowth.Runtime.dll` 仍停在 13:08。
  必须 `Assets/Refresh` + 再 `recompile` 才生效。
- **判断是否真编进去**：`strings Library/ScriptAssemblies/StudentGrowth.Runtime.dll | grep 类名`。
  运行时脚本在 `StudentGrowth.Runtime.asmdef`，**不是** `Assembly-CSharp.dll`
  （后者是 23040 字节的空壳，一直停在 12:24）。
- **`Build Home Room Scenes` 执行后文件时间戳可能不刷新**（四个场景都"已保存"，
  但 `ls` 只看到 2 个新时间戳）。**不要用时间戳判断成败**，
  直接扫场景内容（本轮的 `Spawn_*` 坐标脚本）或看 `Editor.log` 里的 `[Rooms]` 行。
- 探针里 `P(Transform)` 与 `P(Vector2)` 重载 + `P(FindPlayer())` 会撞 CS1503；
  加个 `PPlayer(RpgPlayerController)` 更省事。

---

## 8. 追加修复：交互后底部提示栏不消失（2026-10-21 13:40）

用户第二次反馈，并明确「**我想清除的是交互之后底部的提示栏**」。

### 8.1 根因

`MapInteractionFlowController.ShowConfirm()` 末尾调了
`ShowPrompt("确认执行：" + ResolveDisplayName(target) + "。")`。

而确认面板自己已经把「确认执行」这件事显示在面板正文里了（`confirmView.Show`），
所以底部提示栏是**重复信息**；更糟的是它没有保持期，会一直挂到玩家确认或取消为止。

**探针实测（`_prompt_probe.txt`，已删）**：

```text
靠近未交互 : active=True  alpha=1.00  text="按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5"
确认面板打开: active=True  alpha=1.00  text="确认执行：家庭晚饭聊天。"     ← 一直在
  t+0.5s .. t+3.0s 全部 active=True alpha=1.00 同一句
```

提示栏 rect = `sizeDelta 640x96`、`anchored (0,64)`、屏幕角 `bl=(369,74) tr=(1106,184)`
—— 位于屏幕底部居中，正是用户截图里面板下方那块。

### 8.2 修法

| 位置 | 改动 |
| --- | --- |
| `ShowConfirm()` | 末尾 `ShowPrompt("确认执行：…")` → **`SuppressPromptFor(target)`**（收起提示栏） |
| `CancelPending()` | 开头补 `ClearPromptSuppression()`，让取消后提示栏能恢复 |

`SuppressPromptFor` 内部走 `promptView.ShowMessage(string.Empty)`，
`ShowMessage` 对空串会 `SetVisible(false)` 并淡出，所以是真正的"收起"而不是只清文案。

### 8.3 探针实测（`_prompt_hide_probe.txt`，已删）

```text
[1] 靠近未交互   : active=True  alpha=1.00 text="按 E：家庭晚饭聊天｜消耗 1 时间｜亲子 +5｜心情 +5"
[2] 确认面板打开 : active=False alpha=0.00 text=""        ← 已收起
    t+0.5s .. t+3.0s 全部 active=False alpha=0.00
[3] 取消后       : active=True  alpha=1.00 text="按 E：家庭晚饭聊天｜…"   ← 恢复
[4] 确认执行后   : active=False alpha=0.00 text=""        ← 收起，结果提示正常走
    t+2.5s 起     : active=True  alpha=0.00               ← 仅面板激活，alpha 为 0，不遮挡
```

### 8.4 改动文件

| 文件 | 改动 |
| --- | --- |
| `Assets/Scripts/StudentGrowth/Map/MapInteractionFlowController.cs` | `ShowConfirm()` 改抑制；`CancelPending()` 补 `ClearPromptSuppression()` |

备份见 `_backup_promptfade_2026-10-21/`（上一轮的同名文件已在其中，本轮改动可回滚到该版本）。

### 8.5 顺带确认：面板里的两条金线不是提示栏

用户截图里面板正文区有两条金色横条（y≈250 / y≈618）。查实来源：
`Assets/Art/Pixel/UI_New/Common/panel_paper.png`（48×48）的第 12 行与第 36 行是
金色装饰线 `RGB(226,178,76)`，九宫格（`spriteBorder 6`）把它拉伸到 594×631 后
成了两条长横条。**这是纸面板贴图自带的设计，不是 UI bug**，本轮未改。

