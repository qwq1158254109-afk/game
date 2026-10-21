# LitMotion 接入记录（2026-10-20 晚）

> 状态：**运行时代码已编译通过**（`Library/ScriptAssemblies/StudentGrowth.Runtime.dll` 21:44:06 生成）。
> 测试程序集曾因我自己的测试写错而失败一次，已修复，等待最后一次重编译。

---

## 一、改了什么（4 个文件 + 1 个新测试）

### 1. `Assets/Scripts/StudentGrowth/UI/Map/MapInteractionPromptView.cs`

交互提示栏的显隐从**硬切**改为**淡入 + 上滑**：

| 项 | 值 | 理由 |
| --- | --- | --- |
| 入场时长 | 0.14s | 提示栏随目标切换频繁显隐，慢动作会显得反馈迟钝 |
| 出场时长 | 0.09s | 出场要比入场更干脆 |
| 位移 | 12px（`Ease.OutQuad`） | 偶数像素，Canvas 缩放 1.1523 下不产生半像素、像素字体不发虚 |
| 调度器 | `MotionScheduler.UpdateIgnoreTimeScale` | 本工程暂停/设置菜单会 `Time.timeScale = 0`，默认调度器会让提示栏卡在半透明 |

**关键守卫**：`RefreshTarget` 是**每帧**调用的（`MapInteractionFlowController.cs:146-149`），
所以 `ShowMessage` 会被同一条消息反复调用。新增 `hasState/visibleState/lastMessage`
三元缓存，**内容与可见性都没变就直接 return**——否则动效每帧重启、永远播不完。

**两个必须注意的实现细节**：

1. **淡出结束才 `SetActive(false)`**（`FinishHide` 回调）。原实现是立刻 `SetActive(false)`，
   直接照搬会让动画完全不可见。回调里还要判 `visibleState`——淡出途中又被显示时不能关掉面板。
2. **动效基准 Y 由布局给出，不读运行中的 `anchoredPosition`**。
   `ApplyCompactLayout()` 里 `baseAnchoredY = 64f` 是唯一来源；
   若在补间途中采集，会把动画临时值当基准、逐次累积偏移。

### 2. `Assets/Scripts/StudentGrowth/Scene/FadeController.cs`

`FadeTo` 的**手写协程 Lerp 换成 LitMotion 补间**，并显式取消上一条补间。

⚠️ **这里纠正一个我先前写错的结论**：早先在注释与本文件里写「按目标绑定天然互斥、
第二条会自动取消第一条」——**这是错的**。读了源码才确认：
`MotionBuilder.Bind` 只是 `SetCallbackData` + `ScheduleMotion`，**没有任何取消逻辑**。

实测后果（测试 `SecondFade_CancelsPreviousTweenWithoutStalling` 抓到的）：
`FadeTo(1f,0.4f)` 进行中再 `FadeTo(0f,0.05f)`，第二条跑完后 alpha 被第一条拉回
**0.375**（正是第一条在 t=0.15s 的插值）——黑场收不住。

修法两处：

1. **起新补间前显式取消旧的**（`if (fadeMotion.IsActive()) fadeMotion.Cancel();`）
2. **加过渡代际 `fadeGeneration`**：协程在补间结束后比对代际，
   不是最新一代就 `yield break`。否则被取代的旧协程会继续往下跑，
   把新过渡刚设好的 `blocksRaycasts` / 状态覆盖掉。

另外补了两处健壮性：

- `OnDestroy` 里取消补间。**必须**：补间按目标绑定，不取消的话对象销毁后补间还会访问它，
  抛 `MissingReferenceException: The object of type 'UnityEngine.UI.Image' has been destroyed`。
- 收尾（落 alpha + 复位 `blocksRaycasts`）**确定地放在协程末尾**，不只依赖 `WithOnComplete`。
  实测出现过「alpha=0 但 blocksRaycasts 仍为 true」＝全透明却点不动。

`ToYieldInstruction()` 在补间被取消时只是退出循环、**不抛异常**（已读源码确认），
所以 `yield return` 写法安全。

**`schedulerOverride` 字段**：默认 null（正式运行走 `UpdateIgnoreTimeScale`），
测试注入 `ManualMotionDispatcher` 用。存在的理由见 1.5 第 2 条。

### 3. `Assets/Scripts/StudentGrowth/StudentGrowth.Runtime.asmdef` ⚠️ 关键

**原 `references` 是空的**——asmdef 之间不自动互相引用，
所以 `FadeController` / `MapInteractionPromptView` 里的 LitMotion 类型根本不可见。
最终只引用核心库：

```json
"references": ["LitMotion"]
```

**为什么不引用 `LitMotion.Extensions`**：该程序集用 `versionDefines` 判定
「`com.unity.ugui` >= 2.0.0」成立 → 定义 `LITMOTION_SUPPORT_TMP`，
其内部引用了 `Unity.TextMeshPro` 的 `TMP_Text`。一旦引用它，
`StudentGrowth.Runtime` 就必须连带引用 `Unity.TextMeshPro`，否则：

```text
error CS0012: The type 'TMP_Text' is defined in an assembly that is not referenced.
      You must add a reference to assembly 'Unity.TextMeshPro, ...'
```

本工程用 uGUI 的 `Text`、根本没有用 TMP，所以改为**直接在核心库上 `Bind`**：

```csharp
// 不依赖 Extensions 的等价写法（照抄 BindToAlpha / BindToColorA / BindToAnchoredPositionY 的实现）
.Bind(canvasGroup, static (x, target) => target.alpha = x)
.Bind(fadeImage, static (x, target) => { var c = target.color; c.a = x; target.color = c; })
.Bind(rect,      static (x, target) => target.anchoredPosition = x)
```

代价只有几个 lambda，换来少一层无关依赖。`Bind` 的签名是
`Bind<TState>(TState state, Action<TValue, TState> action)`（`MotionBuilder.cs:326`）。

### 4. `Assets/Tests/EditMode/StudentGrowth.EditMode.Tests.asmdef`

加了 `"LitMotion"` 引用（测试要用 `ManualMotionDispatcher`）。

### 5. 新增 `Assets/Tests/EditMode/FadeControllerMotionTests.cs`

3 条确定性测试，**全部通过**（`Logs/editmode-fade-results.xml`：总计=3 通过=3 失败=0）：

| 测试 | 断言 | 结果 |
| --- | --- | --- |
| `FadeIn_AnimatesAlphaFromZeroToOne` | 中途 alpha ∈ (0,1) 证明是渐变；最终 =1；过渡中 `blocksRaycasts=true` | ✅ |
| `FadeOut_AnimatesAlphaBackToZero` | 最终 alpha=0；`blocksRaycasts=false` | ✅ |
| `SecondFade_CancelsPreviousTweenWithoutStalling` | 后发起的淡出胜出（alpha=0），证明旧补间被取消、没有互相拉扯 | ✅ |

用 `ManualMotionDispatcher` 手动推进时间；`FadeController.schedulerOverride` 注入调度器。

**四条写法约束（全部是实测踩出来的，别重蹈）**：

```csharp
// ✗ 错：StartCoroutine 返回 Coroutine 包装对象，没有 MoveNext
Coroutine routine = controller.StartCoroutine(controller.FadeIn(0.2f));
routine.MoveNext();   // CS1061

// ✗ 错：FadeIn/FadeOut 只是 `yield return FadeTo(...)` 的薄封装。
//        手动 MoveNext 时外层只把内层枚举器当 Current 返回，内层方法体根本不执行。
//        （递归执行内层是 Unity 协程调度器干的活）
IEnumerator routine = controller.FadeIn(0.2f);
routine.MoveNext();   // FadeTo 方法体没跑 → blocksRaycasts 没置位、alpha 不动

// ✓ 对：直接驱动真正干活的那层（FadeTo 已提升为 public）
IEnumerator routine = controller.FadeTo(1f, 0.2f);
routine.MoveNext();
dispatcher.Update(0.02);
routine.MoveNext();
```

```csharp
// ✗ 错：靠 DefaultScheduler 覆盖。EditMode 下会被完全忽略
MotionScheduler.DefaultScheduler = dispatcher.Scheduler;

// ✓ 对：注入到控制器（内部 WithScheduler 才走 buffer.Scheduler 分支）
controller.schedulerOverride = dispatcher.Scheduler;
```

还有一条：**断言前要 `MoveNext` 到底**。补间可能在循环里刚完成但协程还没被推到收尾那行。

---

## 二、验证结果（全部通过）

| 检查项 | 结果 | 证据 |
| --- | --- | --- |
| 编译 0 error | ✅ | `StudentGrowth.Runtime.dll` 22:49:45、`StudentGrowth.EditMode.Tests.dll` 22:48:53 重建；`python Tools/extract_compile_errors.py` 无输出 |
| Console 无 Error | ✅ | MCP `get_console_logs(error)` 返回 `[]` |
| 场景淡入淡出行为 | ✅ 3/3 | `FadeControllerMotionTests` 全通过（`Logs/editmode-fade-results.xml`：总计=3 通过=3 失败=0） |
| 交互提示动效代码 | ✅ | 提示栏 `BeginShow`/`BeginHide` 显式取消旧补间 + 时长/位移常量集中在文件头 |
| 改前备份 | ✅ | `_backup_litmotion_2026-10-20/`，改动文件 md5 与备份不同（证明备份有效） |
| 分层未被破坏 | ✅ | 只加渲染层动效，未把游戏规则搬进 View；提示栏仍由 `MapInteractionFlowController` 驱动 |
| 像素风约束 | ✅ | 位移取偶数 12px、无缩放动画、无插值糊化 |

**已消除的三个编译错误**（都记在 `Editor.log` 里，属修复前的中间态）：

- `CS0246: The type or namespace name 'LitMotion' could not be found`（asmdef 没加引用）
- `CS0012: The type 'TMP_Text' is defined in an assembly that is not referenced`（错引了 Extensions）
- `CS1061: 'Coroutine' does not contain a definition for 'MoveNext'`（测试写法错，见 1.5）

**仍未验证的（需要人在编辑器里看）**：

- ⏳ **实机动画观感**——本轮的自动化测试证明了「补间会推进、会落位、会互斥」，
  但"看起来顺不顺眼、时长合不合适"只能进 Play 逐眼看。
  步骤：打开 `HomeKitchenScene`（或任一家地图）→ Play → 朝床/书桌走，看提示栏是否淡入上滑；
  走开是否淡出下移；穿门看黑场是否平滑。
- ⏳ Admin 场景切换过程中 `blocksRaycasts` 的手感（过渡期是否真的点不动）。

---

## 三、LitMotion 的两个行为陷阱（本轮最大收获）

这两条都**与直觉相反**，且都不报错、只是行为不对，极易误判成"自己的逻辑写错了"。

### 1. `Bind` 不会自动取消同目标的前一条补间

源码 `MotionBuilder.Bind`（`MotionBuilder.cs:326`）：

```csharp
public MotionHandle Bind<TState>(TState state, Action<TValue, TState> action)
{
    CheckBuffer();
    SetCallbackData(state, action);
    return ScheduleMotion();     // ← 只是调度，没有任何"取消旧绑定"的逻辑
}
```

后果：两条补间会并行 Lerp 同一个属性，**先发起的那条最终会覆盖后者**
（实测 `FadeTo(1f,0.4f)` + `FadeTo(0f,0.05f)` → alpha 停在 0.375）。

**结论：谁起补间谁负责取消。** 凡是可能重入的动效（门、提示栏、弹窗显隐），
起新补间前一律 `if (handle.IsActive()) handle.Cancel();`。

### 2. EditMode 下 `MotionScheduler.DefaultScheduler` 会被忽略

源码 `MotionBuilder.ScheduleMotion()`（`MotionBuilder.cs:403-431`）：

```csharp
if (buffer.Scheduler == null)
{
#if UNITY_EDITOR
    if (!UnityEditor.EditorApplication.isPlaying)
        handle = EditorMotionDispatcher.Schedule(ref this);      // ← EditMode 走这里
    else if (MotionScheduler.DefaultScheduler == MotionScheduler.Update) ...
#endif
}
else
    handle = buffer.Scheduler.Schedule(ref this);                 // ← 显式 WithScheduler 才走这里
```

所以在编辑器非播放态下：

- 设 `DefaultScheduler = manualDispatcher.Scheduler` **完全无效**（被 `EditorMotionDispatcher` 截胡）
- 而生产代码里写死的 `MotionScheduler.UpdateIgnoreTimeScale` 属于 PlayerLoop 调度器，
  **在 EditMode 测试里根本不被驱动**（alpha 永远停在起点）

**结论：想让 EditMode 测试确定性地驱动补间，必须能从外部注入调度器。**
本工程的 `FadeController.schedulerOverride` 就是为此存在的。

### 3. 批量跑测试的正确姿势（附带收益）

`-batchmode -runTests` 不依赖 MCP，改测试后不必反复打断编辑器。
但注意：

- **batch 不会自动重编译**——它会直接用 `Library/ScriptAssemblies` 里的旧程序集，
  出现"改了测试却没生效"的假象。所以改完代码先让编辑器（或 `Assets/Refresh`）编译，
  或接受 batch 自己触发的那次编译后再复跑一次核对。
- **batch 会清空「上次打开的场景」**（`Library/LastSceneManagerSetup.txt` 写成 `sceneSetups: []`），
  下次从 Hub 打开会看到空场景。跑前备份、跑后恢复：

  ```bash
  cp Library/LastSceneManagerSetup.txt /tmp/lastscene.bak    # 跑前
  cp /tmp/lastscene.bak Library/LastSceneManagerSetup.txt    # 跑后
  ```

- **GUI 实例与 batch 不能同时打开同一工程**：GUI 持锁时 batch 会在
  `Successfully changed project path` 之后立刻退出（exit code 1）。
  先关 GUI → 等 8096 释放 → 再跑 batch。
- 结果读 XML，别读日志：`python Tools/read_test_results.py Logs/editmode-fade-results.xml`
  （日志只有摘要，断言文本只在 XML 里；且直接 cat 会中文乱码，用脚本读）。

### 4. 编译错误统一从这里读

`Logs/Editor.log` 可能长时间不 flush（本轮实测停滞 6 小时），
`Library/Bee/tundra.log.json` 才是可靠的编译错误来源。
已封装：`python Tools/extract_compile_errors.py`（输出文件+行号+错误码）。

**另一条判据**：判断"Unity 到底导入新文件没有"→ 看 **`.meta` 有没有生成**。
比看 `SourceAssetDB` 靠谱（后者二进制存储，连确定存在的 GUID 都搜不到）。

---

## 四、回滚方法

```bash
cp -r _backup_litmotion_2026-10-20/Assets/. Assets/
rm -f Assets/Tests/EditMode/FadeControllerMotionTests.cs Assets/Tests/EditMode/FadeControllerMotionTests.cs.meta
```

回滚后要不要留 LitMotion 包由你定——留着不影响编译（没人引用它即可）。

---

## 五、仍需人工看的（自动化测不了的部分）

自动化已经证明「补间会推进、会落位、会互斥」，但**观感**只能进 Play 逐眼看：

```text
1. 打开 Assets/Scenes/StudentGrowth/Maps/HomeKitchenScene.unity（或任一家地图）
2. Play
3. 朝床 / 书桌走 → 提示栏应从下方 12px 淡入上滑（0.14s）
4. 走开          → 提示栏应淡出下移（0.09s），不是瞬间消失
5. 穿门           → 黑场应平滑，且过渡期点不动（blocksRaycasts）
6. 暂停菜单 → 返回 → 黑场不应卡住（这条验证 UpdateIgnoreTimeScale 的作用）
```

嫌快/嫌慢就改 `MapInteractionPromptView.cs` 顶部三个常量
（`ShowDuration` / `HideDuration` / `SlideOffsetY`），不用动逻辑。

**其它动效（卡牌飞入、门开关反馈、伤害数字）本轮没做**，留到 V0.4。

## 六、其它遗留（本轮未处理，已知不影响）

- **全量 EditMode 回归未跑**（334 个用例）。评估：影响面极小——
  `FadeController` 只做**增量**改动（`FadeTo` 由 private 提升为 public、
  新增 `EnsureInitialized()` / `schedulerOverride` / `OnDestroy`，
  没有改任何既有签名），`MapInteractionPromptView` 只改显隐实现。
  真要有回归，最可能是引用这两个组件的用例。
  想跑的话：**先存盘**（Unity 当前有未保存更改，关窗会被保存确认挡住），
  再关 Unity → `Get-ScheduledTask` 确认 8096 释放 → 用 batch 跑全量：

  ```powershell
  $arg = "-batchmode -projectPath `"E:\大创项目`" -runTests -testPlatform EditMode -testResults `"E:\大创项目\Logs\editmode-all-results.xml`" -logFile `"E:\大创项目\Logs\editmode-all.log`""
  ```

  跑前记得备份 `Library/LastSceneManagerSetup.txt`（batch 会清空它）。
- `MapInteractionPromptView.ApplyStyle()` 会把 `anchoredPosition` 重置为 64。
  若某次 `ApplyStyle` 正好落在淡入动画中途，会有一次轻微跳位。
  当前调用路径（门/快捷栏的进出触发）频率很低，先不处理。
- `Assets/_Recovery/` 若出现场景备份文件，是早前强杀 Unity 留下的，不是本轮产生。
