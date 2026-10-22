# 项目设计改进体检报告

- 时间：2026-10-22 14:00（Asia/Shanghai）
- 依据：实机代码与资产，不采信规格书声明
- 上一份体检：`Docs/ContentHealthReport.md`（2026-10-17，由 `ContentHealthAudit` 生成）
- 本轮新增核查点：阶段预算 vs 内容供给、生成器未执行、四房间场景接线

---

## 0. 结论速览

| 严重度 | 问题 | 影响 |
| --- | --- | --- |
| **P0 阻断** | 每阶段 3 个活动、每个只能用 1 次，但预算要 6/6/8 次 → **阶段无法推进** | 游戏核心循环死锁 |
| **P0 阻断** | 6 个结局里 **5 个不可达**（属性上界不够） | 多结局承诺落空 |
| **P1** | 生成器已写好能产 25 卡 / 9 道具 / 7 事件 / 13 活动 / 7 结局，但**从未执行** | 内容量不足的根因 |
| **P1** | 金钱、精力上限 **零来源** | 商店系统形同虚设 |
| **P2** | 学校场景 0 个活动（唯一交互点是「教室出口」） | 默认解锁地点无内容 |
| **P2** | 5 个活动资产未接线（写完没挂进场景） | 资产浪费 |
| **P2** | 3 组活动 ID 重复（新旧两套资产并存） | 数据源冲突 |

---

## 1. P0：阶段推进死锁（本轮新发现，最严重）

### 证据

**阶段配置**（`MvpSampleDataFactory.cs:44-46`）：

| 阶段 | timeLimit | 可用活动数 |
| --- | ---: | ---: |
| 适应新学期 | 6 | 3 |
| 月考压力 | 6 | 3 |
| 期末冲刺 | 8 | 3 |

**活动消耗**（`MvpSampleDataFactory.cs:305/317/344/372`）：所有活动 `timeCost = 1`。

**次数上限**（`DataDefinitions.cs:50`）：`maxUsesPerStage = 1`（默认值），
且 `MvpSampleDataFactory.cs` 与 `MvpSampleAssetGenerator.cs` **都没有覆盖它**
（grep 结果为空）。

**门禁**（`MapInteractionFlowController.cs:591/598` 读取上限，`IsCompleted()` 判定）：

```csharp
// 次数用完后
if (IsCompleted(pendingTarget)) {
    ShowPrompt("该活动本阶段次数已用完，请选择其他行动。");
    return;
}
```

**阶段结束条件**（`StageController.cs:106-107`）：

```csharp
sessionState.stageState.remainingTime = Mathf.Max(0, remainingTime - timeCost);
if (remainingTime <= 0) bossUnlocked = true;
```

### 推演

```
适应新学期：timeLimit=6，可用活动 3 个，每个上限 1 次
  → 最多消耗 3 时间
  → remainingTime 停在 3，永远到不了 0
  → bossUnlocked 永远为 false
  → 卡在第一阶段
```

`ShouldStartBoss()` 的条件是 `bossUnlocked || remainingTime <= 0`，两者都不满足。

### 修复方向（三选一，成本递增）

| 方案 | 做法 | 代价 |
| --- | --- | --- |
| **A. 放开次数上限** | 训练类活动 `maxUsesPerStage = -1`（不限），休息类同理 | 1 行改动，立刻可玩 |
| **B. 补足活动数量** | 每阶段活动数 ≥ timeLimit（3 阶段共需 20 个） | 需要内容，但生成器能产 13 个 |
| **C. 缩短阶段时长** | timeLimit 降到 3/3/3 | 阶段过短，失去成长感 |

**建议 A + B 组合**：先用 A 解除死锁（立即可玩），再用 B 补内容（体验完整）。

**验收标准**：从主菜单开始新游戏 → 连续执行活动 → `remainingTime` 归零 → Boss 弹窗出现 → 打完进下一阶段。

---

## 2. P0：5/6 结局不可达

来源：`Docs/ContentHealthReport.md` 第 2 节（2026-10-17），本轮复核代码确认阈值未变。

| 结局 | 门槛 | 实机上界 | 差距 |
| --- | --- | ---: | --- |
| 压力调整 | 压力 ≥ 85 | 41 | −44 |
| 稳定升学 | 三科均值 ≥ 55 | 42 | −13 |
| 兴趣成长 | 兴趣 ≥ 70 | 53 | −17 |
| 家庭支持 | 亲子关系 ≥ 70 | 67 | −3 |
| 自主选择 | 自主性 ≥ 70 | 50 | −20 |
| 普通但完整的成长 | 无门槛 | — | 可达 |

`MvpSampleDataFactory.cs:70` 起是结局定义，阈值与报告一致。

### 关键判断

「家庭支持」只差 3 点，**最容易救**；「压力调整」差 44 点，需要重新设计（压力上界 41 是设计上不可能到 85 的）。

### 修复方向

- **阈值对齐现实**：把不可达的阈值按实机上界重新标定（如压力 85 → 38）
- **补属性来源**：给对应属性加活动/事件/卡牌奖励
- **删除死结局**：如果某个结局在叙事上不必要，直接移除比勉强救活更诚实

**建议**：先按实机上界重新标定阈值（改数据，0 代码），再评估哪些结局值得补内容。

**验收标准**：跑 `ContentHealthAudit`，`不可达结局（实机口径）` 从 5/6 降到 ≤ 1/6。

---

## 3. P1：生成器从未执行（内容不足的根因）

### 证据

`Assets/Scripts/StudentGrowth/Editor/MvpSampleAssetGenerator.cs:22`：

```csharp
[MenuItem("Tools/Student Growth/Data/Generate MVP Sample Assets")]
```

产出量统计（grep 计数）：

| 类型 | 生成器产出 | 当前 `Assets/Data` 实际 |
| --- | ---: | ---: |
| 卡牌 | 25 | — |
| 道具 | 9 | — |
| 小游戏 | 7 | — |
| 事件 | 9 | — |
| 挑战/Boss | 7 | — |
| 活动 | 13 | 15（其中含旧路径重复） |
| 阶段 | 4 | — |
| 结局 | 7 | **0 个资产** |

**结局资产为 0**，说明运行走的是 `MvpSampleDataFactory` 代码兜底，不是资产。

### 为什么这很重要

按 skill 的「先找是不是已经有生成器」原则：**内容缺失的真实原因是「没执行」，不是「没开发」**。
这把「补内容」的成本从「数周」降到「跑一次菜单 + 审差异 + 提交」。

### 但必须先做这一步

**生成器里的内容 ≠ 代码兜底里的内容。** 两套数值表可能不同（ID 有交叉也有新增），
直接跑会当场换一套玩法。

正确顺序：
1. 先建 Git 基线（当前已干净，`a0cc820`）
2. 跑生成器到临时目录，导出差异表
3. 人工审：哪些 ID 是新增、哪些数值变了
4. 确认后再覆盖

**验收标准**：生成后 `Assets/Data` 下结局资产 ≥ 6 个，且 `ContentHealthAudit` 报「GameBootstrapperData 资产：已找到」（当前报「未找到」）。

---

## 4. P1：金钱与精力上限零来源

### 金钱

`ContentHealthReport.md` 第 4 节：`AddMoney` 出现 **0 次**。
本轮 grep 复核：`RewardType.AddMoney` 只在**读取侧**出现（`RewardResolver.cs:37`、
`MapInteractionPromptFormatter.cs:124`、`StudentGrowthMvpRuntimeUi.cs:3655`），
**没有任何地方产出它**。

初始金钱 100，上限 9999，但**永远停在 100**。

→ 商店（`location_shop`，2 个交互点）没有可用的支付货币，商店系统形同虚设。

### 精力上限

`RewardType.IncreaseMaxEnergy` 同样 0 次出现。精力上限恒为 60。

→ 精力只能恢复到 60，无法成长。这与「成长模拟」的定位矛盾。

### 修复方向

| 问题 | 最小修法 |
| --- | --- |
| 金钱无来源 | 给「考试通关」「Boss 通关」加 `AddMoney` 奖励（1 行数据） |
| 精力上限无成长 | 给「早点休息」「运动」类活动加 `IncreaseMaxEnergy`，或按阶段自动 +5 |

**验收标准**：`ContentHealthAudit` 的 `AddMoney` 与 `IncreaseMaxEnergy` 出现次数 > 0。

---

## 5. P2：学校场景无任何活动

### 证据

```
SchoolMapScene.unity 里 MapInteractionTarget 数量 = 1
  该唯一目标的 interactionId = "school_exit"
  displayName = "教室出口"
  fallbackActivity = {fileID: 0}   ← 空引用
```

学校是**默认解锁**的地点（`unlockedByDefault = true`），玩家进去只能看到出口。

而 `Assets/Data/StudentGrowth/New2_0/SchoolMap/` 下**有 2 个活动资产**
（`SchoolClassStudy` 课堂学习、`SchoolTeacherHelp` 老师答疑），**都没接线**。

→ 这两个活动的属性奖励（语文、数学、自信）在实机里拿不到。

### 修复方向

把这两个资产挂到 `SchoolMapScene` 的交互点上（`MapInteractionTarget.fallbackActivity`）。
这是**纯配置工作，0 代码**。

**验收标准**：学校场景 `MapInteractionTarget` 数量 ≥ 3，且至少 2 个的 `fallbackActivity` 非空。

---

## 6. P2：5 个活动资产未接线

来源：`ContentHealthReport.md` 第 6 节。

| 活动 | 资产路径 | 状态 |
| --- | --- | --- |
| 整理计划 | `Assets/Data/StudentGrowth/HomeMap/HomeDeskPlan.asset` | 未接线 |
| 家庭晚饭聊天 | `.../HomeMap/HomeFamilyDinner.asset` | 未接线 |
| 早点休息 | `.../HomeMap/HomeRestEarlySleep.asset` | 未接线 |
| 课堂学习 | `.../SchoolMap/SchoolClassStudy.asset` | 未接线 |
| 老师答疑 | `.../SchoolMap/SchoolTeacherHelp.asset` | 未接线 |

前三个是**旧路径**（`HomeMap/`），与 `New2_0/HomeMap/` 下的同名 ID 资产重复。

### 附带发现：3 组重复 ID

```
activity_home_desk_plan       HomeMap/HomeDeskPlan.asset + New2_0/HomeMap/New20_HomeDeskPlan.asset
activity_home_family_dinner   HomeMap/HomeFamilyDinner.asset + New2_0/HomeMap/New20_HomeFamilyDinner.asset
activity_home_rest_early_sleep HomeMap/HomeRestEarlySleep.asset + New2_0/HomeMap/New20_HomeRestEarlySleep.asset
```

同一 ID 两份资产，**必须人工确认以哪份为准**。审计工具按「取绝对值最大」处理，不叠加，
但数据源冲突本身是隐患。

**建议**：确认 `New2_0/` 是新的权威版本后，删掉 `HomeMap/` 下那三个旧资产。

---

## 7. 已确认健康的部分（不用改）

避免把力气花在没问题的东西上：

| 系统 | 状态 | 证据 |
| --- | --- | --- |
| 存档系统 | 完整 | `Assets/Scripts/StudentGrowth/Save/` 下 10 个类（Codec/Service/SlotRepository/Models…） |
| 战斗选牌 | 已接 | `BattleView.cs:110` → `CardChoiceService.BuildOffer` → `CardChoiceView.Open` |
| 卡牌池 | 已建 | `CardPoolData` / `CardPoolCatalog` / `CardChoiceService`，31 张卡资产 |
| 房间门交互 | 已改为按 E | 本轮刚完成，客厅 3 扇门都挂了 `MapInteractionTarget` |
| 地点可达性 | 6/6 可达 | 社团自锁已在 2026-09-23 修复（`stage_03_or_later` 替代属性门槛） |
| 输入系统 | 已修 | 9 个脚本的旧 Input 调用全部加守卫，33 万条刷屏已消除 |

---

## 8. 建议的修复顺序

按「阻断核心玩法 → 对外承诺落差 → 成本」排序：

| 顺序 | 事项 | 类型 | 预计成本 | 验收 |
| ---: | --- | --- | --- | --- |
| 1 | 放开 `maxUsesPerStage`（训练/休息类设 -1） | 改数据 | 10 分钟 | 能打通 3 个阶段 |
| 2 | 学校场景挂 2 个活动资产 | 改配置 | 20 分钟 | 学校交互点 ≥ 3 |
| 3 | 给 Boss 通关加 `AddMoney` | 改数据 | 10 分钟 | 金钱能涨 |
| 4 | 结局阈值按实机上界重新标定 | 改数据 | 30 分钟 | 不可达结局 ≤ 1 |
| 5 | 清理 3 组重复 ID | 删旧资产 | 15 分钟 | 审计无重复报告 |
| 6 | 跑生成器补内容（需先做差异表） | 执行 + 审查 | 2 小时 | 结局资产 ≥ 6 |
| 7 | 补足每阶段活动数到 ≥ timeLimit | 内容设计 | 数小时 | 内容供给 ≥ 预算 |

**前 5 项合计约 1.5 小时，能让项目从「卡死」变成「可完整通关」。**

---

## 9. 口径说明与未验证项

### 本报告的口径

- **只采信代码与资产**。`CODEX_GAME_SPEC_*.md` 与 `Docs/` 下的设计文档只用于对照，不作事实来源。
- **上界推算 ≠ 完整模拟**。报「不可达」必然为真；报「可达」只代表上界够。
- 上一份体检报告（2026-10-17）的结论**部分仍有效**，本轮复核了结局阈值、活动资产数、
  学校接线、金钱/精力上限，均未变化；但四房间场景与卡池是本轮新增，旧报告未覆盖。

### 未验证（需要实机 Play 才能确认）

- 阶段死锁的实际表现（推演基于代码，未实机跑通）
- `maxUsesPerStage = 1` 是否在某个我没找到的地方被覆盖（grep 覆盖了 Factory 与 Generator）
- 四房间场景的 `MapInteractionTarget` 是否都能正常触发（本轮刚改，只验证了组件挂载，未验证交互）

### 建议的失败信号（Playtest 前定义）

- **A**：执行 3 次活动后，提示「该活动本阶段次数已用完」且无法继续 → 确认死锁
- **B**：时间槽耗尽但 Boss 不出现 → `ShouldStartBoss()` 逻辑问题
- **C**：进入学校后无任何可交互活动 → 接线未生效
