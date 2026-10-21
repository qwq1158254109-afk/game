# 旧输入刷屏修复（方案 A）2026-10-21

## 一、问题

Play 时 Console 刷满（实测 999+，日志累计 **335593 条**）：

```text
InvalidOperationException: You are trying to read Input using the UnityEngine.Input class,
but you have switched active Input handling to Input System package in Player Settings.
  at UnityEngine.Input.GetKeyDown (UnityEngine.KeyCode key)
```

设置侧：`ProjectSettings/ProjectSettings.asset` 的 `activeInputHandler: 1`
= **只启用新 Input System**。在这种设置下调用旧 `UnityEngine.Input` 必然抛异常。

## 二、根因（栈顶定位）

`Editor.log` 里该报错的栈顶**几乎全是同一处**：

```text
at StudentGrowth.InventoryController.Update () [0x00009]
   in Assets/Scripts/StudentGrowth/Inventory/InventoryController.cs:67
```

`InventoryController.Update()` 每帧**无任何保护**地读旧输入 3 次（第 67 / 86 / 97 行）：

```csharp
if (Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.B))   // 67 行 ← 刷屏主源
if (Input.GetKeyDown(KeyCode.Escape))                                  // 86 行
if (Input.GetKeyDown(KeyCode.Alpha1 + i))                              // 97 行
```

该组件由 `RuntimeInitializeOnLoadMethod` → 场景加载后在运行时 `AddComponent` 自建，
所以只要进地图场景就逐帧抛 2~3 条。

**这是既有问题，不是本轮改动引入的**（`AGENTS.md` 20.4 早有记录）。

## 三、工程原有机制（只缺一个标志位）

6 个文件已有这套写法：第一次抛异常 → 置 `legacyInputBlocked` → 之后只走新输入通道。

```csharp
if (!legacyInputBlocked)
{
    try { if (Input.GetKeyDown(legacyKey)) return true; }
    catch (InvalidOperationException) { legacyInputBlocked = true; }
}
return ReadInputSystemKey(inputSystemKey, "wasPressedThisFrame");
```

但有 3 个文件**缺这个标志**，于是逐帧/逐次重抛：

| 文件 | 行 | 修前状态 |
| --- | --- | --- |
| `Inventory/InventoryController.cs` | 67 / 86 / 97 | **完全无 try/catch**（刷屏主源） |
| `UI/Battle/CardChoiceView.cs` | 204 | 有 catch、无标志（`Pressed` 是**静态**方法） |
| `UI/Map/MapInteractionConfirmView.cs` | 137 | 有 catch、无标志（`Pressed` 是**静态**方法） |

## 四、改法（方案 A：最小止血）

只补齐"抛过就不再重试"的标志，**不动输入语义**。

### `InventoryController.cs`（+57 行）

- 加 `using System;`（反射需要）
- 加实例字段 `private bool legacyInputBlocked;`
- 三处裸调用改为 `IsKeyPressed(legacyKey, inputSystemKey)`：

| 旧 | 新 |
| --- | --- |
| `Input.GetKeyDown(KeyCode.Alpha6) \|\| Input.GetKeyDown(KeyCode.B)` | `IsKeyPressed(KeyCode.Alpha6, "digit6Key") \|\| IsKeyPressed(KeyCode.B, "bKey")` |
| `Input.GetKeyDown(KeyCode.Escape)` | `IsKeyPressed(KeyCode.Escape, "escapeKey")` |
| `Input.GetKeyDown(KeyCode.Alpha1 + i)` | `IsKeyPressed(KeyCode.Alpha1 + i, "digit" + (i + 1) + "Key")` |

- 新增 `IsKeyPressed(...)`（旧输入优先、被禁回退新输入）与
  `ReadInputSystemKey(...)`（**反射**读 `Keyboard.current`，不直接引用 Unity.InputSystem 程序集）

`KeyCode.Alpha6` 这个运算我已核对：Unity 的 `Alpha1 = 0x31 = 49`，
所以 `KeyCode.Alpha1 + i`（i=0..4）= Alpha1..Alpha5，与原意一致。

### `CardChoiceView.cs` / `MapInteractionConfirmView.cs`（各 +12 行）

- 各加一个 **`private static bool legacyInputBlocked;`**
  （必须是静态的：`Pressed` 是静态方法且每次按键都会走）
- `Pressed` 里把 `try` 包进 `if (!legacyInputBlocked)`，catch 里置标志

## 五、验证

### 静态核对（已做）

```text
全工程 9 个含旧输入的文件，守卫覆盖率 9/9：
  InventoryController.cs           调用1 有守卫1  标志:实例
  MapInteractionFlowController.cs  调用1 有守卫1  标志:实例
  RpgPlayerController.cs           调用2 有守卫2  标志:实例
  SceneDoor.cs                     调用1 有守卫1  标志:实例
  SettingsMenuController.cs        调用1 有守卫1  标志:实例
  StudentGrowthMvpRuntimeUi.cs     调用2 有守卫2  标志:实例
  CardChoiceView.cs                调用1 有守卫1  标志:static
  MapInteractionConfirmView.cs     调用1 有守卫1  标志:static
  StageHubSelectionInputAdapter.cs 调用1 有守卫1  标志:实例

大括号平衡：InventoryController 35/35、CardChoiceView 45/45、MapInteractionConfirmView 65/65
```

**新输入键名合法性**（这条比报错更隐蔽：键名不存在时旧输入一被禁，功能就静默失效）：

```text
在 Library/Bee/artifacts/1900b0aE.dag/Unity.InputSystem.dll 中按 ASCII 查找：
  digit1Key..digit6Key, bKey, escapeKey, wasPressedThisFrame   全部 True
另：bKey / digit6Key / escapeKey 本就在工程既有键名名单里（aKey/bKey/dKey/digit6Key/eKey/
    enterKey/escapeKey/f11Key/f3Key/sKey/spaceKey/wKey）
```

### 编译验证（**待做**）

改完后 Unity 处于 MCP 半残状态（8096 在听但两个 `CLOSE_WAIT`，
`get_play_mode_status` 超时），且 `Editor.log` 最后写入 10:46:59 早于改动时间
10:49–10:50 —— **Unity 尚未感知文件变化、未触发重编译**。
需按下节步骤在 Unity 侧完成 0 error 验证。

## 六、待用户在 Unity 侧执行

```text
1) 切到 Unity 窗口让它获得焦点（Unity 只在重获焦点时才扫描外部文件改动），
   或直接按 Ctrl+R 触发 Refresh + 重编译
2) 看 Console 是否还有 error（应只剩已经存在的那条，无新增）
3) 进 Play，确认：
   - Console 不再刷 InvalidOperationException
   - 按 6 或 B 能开关背包
   - 背包开着时按 Esc 能关闭
   - 按 1~5 能使用道具栏对应格
4) （与本修复无关的另一件事）四间房场景仍待重建：
   Tools/Student Growth/Maps/Build Home Room Scenes
```

## 七、回滚

```bash
cp _backup_inputfix_2026-10-21/Assets/Scripts/StudentGrowth/Inventory/InventoryController.cs \
   Assets/Scripts/StudentGrowth/Inventory/
cp _backup_inputfix_2026-10-21/Assets/Scripts/StudentGrowth/UI/Battle/CardChoiceView.cs \
   Assets/Scripts/StudentGrowth/UI/Battle/
cp _backup_inputfix_2026-10-21/Assets/Scripts/StudentGrowth/UI/Map/MapInteractionConfirmView.cs \
   Assets/Scripts/StudentGrowth/UI/Map/
```

备份 md5 已核对与原文件一致。未改任何 `.meta` / GUID、未改按键映射语义。

## 八、踩到的坑：`using System;` 引入 CS0104（Play 一度无法进入）

第一次改完后 Play 失败，`Editor.log`：

```text
All compiler errors have to be fixed before you can enter playmode!
```

`Library/Bee/tundra.log.json` 里唯一错误（共 3 条，全是同一个）：

```text
InventoryController.cs(36,17):  error CS0104: 'Object' is an ambiguous reference
                               between 'UnityEngine.Object' and 'object'
InventoryController.cs(57,52):  error CS0104: 同上
InventoryController.cs(197,17): error CS0104: 同上
```

**原因**：为了让 `Type.GetType` 可用而加的 `using System;`，使 `Object` 同时匹配
`System.Object` 与 `UnityEngine.Object`。而该文件原有三处 `Object.FindAnyObjectByType<T>()`
（备份第 28 / 49 / 140 行确认本来就是这么写的）。

**修法（最终采用）**：**不加 `using System;`**，改用全限定名 —— 从根上不给歧义留机会：

```csharp
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;   // 注意：没有 using System;

...
catch (System.InvalidOperationException) { legacyInputBlocked = true; }
...
System.Type keyboardType = System.Type.GetType("UnityEngine.InputSystem.Keyboard, Unity.InputSystem");
```

（先试过别名 `using Object = UnityEngine.Object;`，也能过；但去掉 `using System;` 更彻底——
不用再逐个排查 `Object`/`Random`/`Type`/`Math` 等同名类型。）

排查时也确认了另外两个文件**没有**同类问题：`CardChoiceView.cs` 的 `Image.Type.Sliced`
是嵌套类型（不是 `System.Type`），`MapInteractionConfirmView.cs` 无风险名。
这两个文件**本来就有 `using System;`**，且各自只用到 `InvalidOperationException`，
改动只是加标志位，未触碰 using。

### 教训（已并入 AGENTS.md 20.4）

1. **给文件加 `using System;` 前，先 grep 该文件里的 `Object.` / `Random.` / `Type.` / `Math.` 等
   同名类型用法**；有就别加 using，直接写全限定名。CS0104 只在编译时才暴露。
2. **编译错误明细只在 `Library/Bee/tundra.log.json`**：`Editor.log` 里只会写一句
   `All compiler errors have to be fixed before you can enter playmode!`，**不含文件名与行号**。
   本次就是先只看了 `Editor.log` 才误判成"已 0 error"，直接导致用户 Play 失败。
3. **Unity 只在窗口重获焦点时才扫描外部文件改动**。所以改完代码如果一直待在终端里，
   Unity 不会重编译（实测：源文件 11:02 改完，等到 11:10 `Assembly-CSharp.dll` 仍停在昨天）。
   验证编译必须：让 Unity 窗口获得焦点（或按 Ctrl+R），再等程序集时间戳变化。

## 九、未做的（方案 B）

其余 6 个文件仍走"旧输入优先"路径：**每次进入 Play 会先抛一次异常再回退**
（现在不再刷屏，但首帧仍有一条）。彻底清掉需做方案 B——13 处全部改用新输入。
本次按用户选择只做方案 A。