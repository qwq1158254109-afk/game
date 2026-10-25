# 生成器 vs 代码工厂 差异表（跑 MvpSampleAssetGenerator 前必读）

- 时间：2026-10-24（Asia/Shanghai）
- 基线：分支 `素材` @ `bfbadc3`（= main `36dc6f9` + 8 个素材提交）
- 依据：逐行比对 `Assets/Scripts/StudentGrowth/Editor/MvpSampleAssetGenerator.cs`（下称"生成器"）与
  `Assets/Scripts/StudentGrowth/MvpSampleDataFactory.cs`（下称"工厂"），并实读
  `Assets/Resources/StudentGrowth/Battle/` 下卡池与卡牌资产数值。
- 上一份相关文档：`Docs/DesignAudit-2026-10-22.md` 第 3 节（"先做差异表再执行"的要求即来自那里）。

---

## 0. 结论速览

| 项 | 结论 |
| --- | --- |
| **直接跑生成器** | **会回退 5 个已修复的问题**（结局阈值 / 阶段死锁 / Boss 缺钱 / 精力上限零来源 / 卡牌旧数值），并制造 7 个卡牌 ID 双资产冲突 |
| 生成器产出规模（代码实测） | 24 卡 / 8 道具 / 6 小游戏 / 8 事件 / 6 挑战 / 12 活动 / 3 阶段 / 6 结局 + 1 个 `MvpBootstrapperData.asset`，共 74 个资产，全部落在 `Assets/Resources/StudentGrowth/MVP/`（新目录） |
| 切换机制 | `MvpBootstrapperData.asset` 一旦存在，运行时的阶段 / Boss / 结局 / 初始道具立刻从工厂切到生成器数值（`GameBootstrapper.cs:136-145` Resources.Load） |
| 不受影响的部分 | 初始牌组（卡池优先，`GameBootstrapper.cs:151-160`）；地点（生成器不产 LocationData）；商店货架（工厂方法，不走 bootstrapper） |
| 落地决策（2026-10-24 已定） | 生成器改造为**快照工具**（从工厂取当前权威数值），本文件第 5 节方案 A 先行；方案 B 留作后续 |

---

## 1. 三套数据源，谁是实机权威

当前实机数值实际上由**三套来源**拼成，生成器是其中最旧的一套：

```text
实机数据来源（现状）
├─ 卡牌（初始牌组）    → Battle/P2 资产（经 pool_starter 卡池）    ← 权威
├─ 卡牌（商店/奖励池） → Battle/Cards + P2 + P4 资产（pool_reward） ← 权威
├─ 阶段/Boss/结局/活动 → MvpSampleDataFactory 代码工厂              ← 权威（无资产）
├─ 地图活动（场景接线）→ Assets/Data/StudentGrowth/New2_0/*.asset  ← 权威
└─ 生成器硬编码数值表   → 从未执行，停留在 MVP 时代                 ← 最旧
```

**关键链路**（为什么"一跑就换玩法"）：

1. `GameBootstrapper.GetData()`（`GameBootstrapper.cs:136-145`）：序列化字段为空时
   `Resources.Load("StudentGrowth/MvpBootstrapperData")`——该资产当前不存在 → 所有 getter 走工厂兜底。
2. 生成器第 35-54 行会**创建**这个资产并塞入自己的阶段 / 结局 / 初始卡组 / 初始道具 →
   下一次运行起，`GetStageSequence()` / `GetEndingOptions()` / `CreateSession()` 全部改读生成器资产。
3. 地图流程真实消费这些数据：`FullscreenMapSceneController.cs:516/532`（阶段序列、地点），
   且 `FullscreenMapSceneController.cs:671-694` 的 `BindAvailableActivities()` 会把场景里
   `fallbackActivity` 为空的交互点按 名字/ID 匹配到 **阶段活动列表** ——生成器的活动数值漂移会直接漏进地图交互。

---

## 2. 逐类差异明细

### 2.1 结局（6 个，ID 全部相同，**5 个阈值是 10-22 修复前的旧值**）——最严重

工厂在 2026-10-22 按实机上界重新标定了阈值（`MvpSampleDataFactory.cs:134-146` 注释），
生成器仍是旧值（`MvpSampleAssetGenerator.cs:180-191`）：

| 结局 | 工厂（当前实机，已标定） | 生成器（旧值） | 后果 |
| --- | --- | --- | --- |
| 压力调整 `ending_pressure_recovery` | 压力 ≥ **30** | ≥ 85 | 不可达（上界 38） |
| 稳定升学 `ending_stable_admission` | 三科均值 ≥ **32** | ≥ 55 | 不可达（上界 ~40） |
| 兴趣成长 `ending_interest_growth` | 兴趣 ≥ **42** | ≥ 70 | 不可达（上界 53） |
| 家庭支持 `ending_family_support` | 亲子 ≥ **53** | ≥ 70 | 不可达（上界 67） |
| 自主选择 `ending_self_driven` | 自主 ≥ **34** 且 自信 ≥ **44** | ≥ 70 且 ≥ 55 | 不可达 |
| 普通但完整的成长 `ending_growth_complete` | 无门槛 | 无门槛 | 不变 |

→ 直接跑生成器 = **把 DesignAudit-2026-10-22 第 2 节的 P0（5/6 结局不可达）原样复活**。
当前体检报告（2026-10-23 18:19）"不可达结局 0/6"将变成 5/6。

### 2.2 阶段（3 个 ID/时长相同，活动列表 3→4）

| 项 | 工厂 | 生成器 |
| --- | --- | --- |
| 阶段 ID / timeLimit | stage_01(6) / stage_02(6) / stage_03(8, 最终) | 相同 |
| 每阶段活动数 | 3 | **4**（一阶段 +社团邀请，二阶段 +错题本建议，三阶段 +公式配对） |

新增的 3 个活动本身无害，但它们与下面 2.4 的次数上限问题叠加。

### 2.3 Boss / 挑战

| 项 | 工厂（`MvpSampleDataFactory.cs:359-381`） | 生成器（`MvpSampleAssetGenerator.cs:303-323`） |
| --- | --- | --- |
| 3 个 Boss 的 ID/目标进度/回合 | boss_class_quiz 45/3、boss_month_exam 90/5、boss_final_exam 150/7 | 相同 |
| Boss 通关奖励 | 自信+3 **+ 金钱 50**（2026-10-23 加，金钱此前零来源） | 只有自信+3，**没有钱** |
| Boss 失败惩罚 | 压力+5 | 压力+**6** |
| 普通挑战 | 无 | 多 3 个（课堂练习 30/3、小组问答 55/4、模拟演练 80/5） |

→ 商店经济链（Boss 给钱 → 商店买卡，10-23 刚接通）会被切断。

### 2.4 活动（12 个，9 个 ID 与工厂重叠）——**次数上限缺失 = 阶段死锁回归**

生成器的 `AddActivity`（`MvpSampleAssetGenerator.cs:325-340`）**不写 `maxUsesPerStage`**，
落资产后是默认值 1。工厂在 2026-10-22 已把全部 9 个活动改为 -1（不限次）
（`MvpSampleDataFactory.cs:390-393` 注释：默认 1 会造成 remainingTime 永远到不了 0 → Boss 永不解锁）。

数值漂移（仅列不同的）：

| 活动 | 工厂（当前实机） | 生成器 |
| --- | --- | --- |
| 早点休息 | 精力+8、减压 4、**精力上限+2** | 精力+8、减压 4（**缺精力上限+2**，上限零来源回归） |
| 听音乐放松 | 精力+8、减压 4、上限+2 | 精力+**4**、减压 **5**、无上限 |
| 家庭晚饭聊天（事件奖励） | 心情+2、亲子+2、+热茶 | 亲子+**3**、心情+2、+热茶 |
| 周末安排（事件奖励） | 心情+2、亲子+2 | **自主+4、精力+4**（完全不同） |
| 专注计时（小游戏奖励） | 自主+4、+错题本道具 | 自主+**3**、+**借阅笔记卡** |
| 认真听课 / 数学刷题 / 综合复习 / 单词记忆 | 各属性+3/+4/+4/英语+4 | 相同 |

### 2.5 卡牌（三方漂移 + **ID 撞车**）

实机初始牌组走 `pool_starter`（引 P2 资产），工厂卡只是卡池缺失时的兜底。
生成器会把 24 张卡写进 `MVP/Cards/`，其中 7 张与 Battle/P2 **同 ID 不同值**：

| 卡 | 工厂（兜底） | **P2 资产（实机权威）** | 生成器 |
| --- | --- | --- | --- |
| 课堂专注 | 6 | 6 | **8** |
| 数学练习 | 8 | 8 | **10** |
| 整理错题 | 10 | 10 | **11** |
| 背单词 | 7 | 7 | 7 |
| 模拟考试 | 18 / 2 费 / 普通 | 18 / 2 费 / 稀有 1 | 18 / 2 费 / 稀有 1 **+ 加压 2** |
| 好好睡觉 | 格挡 5 + **回能 6** + 减压 3 | 格挡 5 + 减压 3（**无回能**） | **回能 8** + 减压 3 + 格挡 5 |
| 课后复盘 | 9（小游戏奖励的运行时实例） | **8**（pool_reward） | 9 |

另外两处**现存问题**（与生成器无关，2026-10-24 决定先记录不处理，待拍板后统一）：

- 课后复盘存在"工厂 9 / P2 资产 8"的实机不一致——同一张卡从小游戏奖励拿到是 9，从商店/奖励池拿到是 8。
- 好好睡觉 P2 版没有回能 6，工厂兜底版有；当前实机生效的是 P2 版。
- 生成器还会写 17 张 Battle 目录已有精修版的卡（card_rest_* / card_social_* / card_family_* 等）
  → 同 ID 双资产，存档解析"first registration wins"（`SaveContentCatalog.cs:25-32`）按加载顺序决定权威，不确定性。

### 2.6 道具（8 个，**全部缺 price 和 description**）

生成器 `AddItem`（`MvpSampleAssetGenerator.cs:249-264`）不写价格与描述：

| 道具 | 工厂 | 生成器 |
| --- | --- | --- |
| 功能饮料 | 价 12，有描述，回能 15 | **价 0，无描述**，回能 15 |
| 幸运笔 | 价 24，有描述 | **价 0，无描述** |
| 错题本 | 价 18，有描述 | **价 0，无描述** |
| 热茶 | 价 8，有描述，回能 8 | **价 0，无描述** |
| 家长便签 / 旧闹钟 / 耳机 / 铅笔盒 | 工厂不存在 | **新增 4 个，同样价 0 无描述** |

商店货架本身不受影响（商店走工厂方法），但初始道具、事件/小游戏奖励道具会变成无价无描述的资产。

### 2.7 小游戏 / 事件（ID 命名体系不同，两套并存）

| 类型 | 工厂 ID | 生成器 ID |
| --- | --- | --- |
| 小游戏 | `activity_word_memory_mini`、`activity_focus_timing_mini` | `mini_word_memory`、`mini_formula_match` 等 6 个 |
| 事件 | `activity_family_dinner_event`、`activity_weekend_plan_event` | `event_family_dinner`、`event_weekend_plan` 等 8 个 |

同一内容两套 ID；存档按 ID 引用内容时互不相认（旧档引用工厂 ID，新资产是生成器 ID）。

### 2.8 地点（无差异风险）

生成器不产 LocationData，`bootstrapperData.locations` 留空 → `GetLocations()` 继续走工厂（`GameBootstrapper.cs:96-114`），无影响。

---

## 3. 对周边系统的影响（落地时必须处理）

### 3.1 体检工具 `ContentHealthAudit`

- **活动资产误报**：`ScopeOfActivityAsset`（`ContentHealthAudit.cs:498-513`）只认场景 `MapInteractionTarget` 引用。
  只被 StageData.availableActivities 引用的活动资产会被记为"未接线"（空作用域）→
  阶段活动快照落地会凭空多 9 条"未接线"误报（与 10-22 修过的 RoomDoor 误报同类）。
  体检的"阶段可用活动"路径本身是全局作用域（`ContentHealthAudit.cs:768-773`），语义正确，只是资产扫描这条路径不认识 Stage 引用。
- **阶段资产重复 ID 误报**：`CheckExtraFindings`（`ContentHealthAudit.cs:1488-1508`）对"同 ID 出现超 1 次"一律报告。
  bootstrapper 资产存在后，阶段资产会被运行时路径（`StageData(运行时)`）和资产扫描（`StageData(资产)`）各记一次 → 3 条误报。
- **体检会正确切换数据源**：`ContentHealthAudit.cs:747-757` 对 boot.stages/locations/endings 逐字段"有资产用资产、没有回工厂"，与运行时行为一致。
- **对方案 A 的影响**：A 只落结局资产——结局 ID 只在资产扫描注册一次（运行时路径不注册结局 ID），
  不触发上述两类误报；体检应从"未找到"变为"已找到"，其余结论不变。

### 3.2 存档目录 `SaveContentCatalog`

- 新增任何 SO 资产后应跑 `Tools/Student Growth/Rebuild Save Content Catalog`（`Assets/Editor/SaveContentCatalogBuilder.cs`）。
- 防御现状：`CreateRuntime`（`SaveContentCatalog.cs:43-74`）会跳过陈旧条目并告警（10-22 修复），
  同 ID 双资产按"先注册先得"处理——所以**不重建也不会崩**，但权威归属按加载顺序，应当避免同 ID 双资产。

### 3.3 EditMode 测试

- `GameBootstrapperTests`（`Assets/Tests/EditMode/GameBootstrapperTests.cs`）不注入数据时走
  `Resources.Load(MvpBootstrapperData)`——EditMode 下 Resources 可用。落地资产后测试会读到真实资产：
  断言"≥6 张初始卡（卡池优先，成立）、≥2 个初始道具、3 个阶段且第一个是 stage_01_adaptation"。
  只要快照资产与工厂数值一致，测试不需改动即通过（阶段/道具仍走工厂逐字段兜底，断言继续成立）。

### 3.4 地图交互绑定

- `FullscreenMapSceneController.BindAvailableActivities`（`:671-694`）给 `fallbackActivity` 为空的交互点
  按名字/ID 匹配阶段活动。当前 11 张地图场景共 13 个空 fallback 目标（多为门/出口/NPC）。
  阶段活动数值任何漂移都可能通过这条链路影响实机（方案 B 落地阶段活动时必须逐值对齐）。

---

## 4. 直接跑生成器 = 回退 5 个已修复的问题

| # | 会回归的问题 | 原修复 | 证据 |
| --- | --- | --- | --- |
| 1 | 5/6 结局不可达（P0） | 2026-10-22 阈值重标定 | §2.1 |
| 2 | 阶段推进死锁（P0） | 2026-10-22 活动不限次 | §2.4 |
| 3 | 金钱零来源、商店断粮 | 2026-10-23 Boss 通关 +50 | §2.3 |
| 4 | 精力上限零来源 | 2026-10-23 休息活动 +2 上限 | §2.4 |
| 5 | 卡牌数值旧版 + 7 个 ID 双资产冲突 | P2 资产已成权威 | §2.5 |

另带来：道具无价无描述（§2.6）、小游戏/事件 ID 体系分裂（§2.7）。

---

## 5. 落地方案

**共同原则**：生成器的硬编码数值表已整体过时，**不再作为数据来源**；落地 = 把工厂当前权威数值快照成资产（值完全一致，实机行为零变化），生成器改造为快照工具。

- **方案 A（最小落地，满足 DesignAudit 第 3 节验收）——2026-10-24 采用**
  快照 6 个 EndingData（新阈值）到 `Assets/Data/StudentGrowth/Endings/` + 创建
  `Resources/StudentGrowth/MvpBootstrapperData.asset`（只填 endings，其余字段留空走工厂逐字段兜底）。
  验收：结局资产 ≥ 6、体检报"GameBootstrapperData 资产：已找到"、阻塞级 0、EditMode 测试通过。
  风险：接近零（无活动/阶段资产，不触发 §3.1 的两类误报）。

- **方案 B（全量快照 + 体检工具两处小改）——留作后续任务**
  A 之上再快照：3 阶段（含 Boss 挑战、9 活动、2 小游戏、3 事件、奖励里引用的道具/卡牌资产）。
  配套：体检工具认识 StageData→活动引用（全局作用域，消除 9 条误报）+ 阶段资产不重复注册（消除 3 条误报）；
  卡牌奖励引用现有 P2 资产（不新建卡，避开 §2.5 撞车）；道具带价格描述。
  **前置条件**：先拍板 §2.5 的两处卡牌漂移（课后复盘 9 vs 8、好好睡觉回能 6），否则小游戏奖励的卡牌引用无法做到"实机数值零变化"。
  验收：同 A + 阶段/Boss/活动全部资产化 + 体检误报为 0。

两案都不动：卡牌资产（Battle 已是权威）、地图活动资产（New2_0 已是权威）、工厂代码（保留为兜底与存档安全网）。

### 顺带决策记录（2026-10-24）

1. **课后复盘 9 vs 8 / 好好睡觉回能 6**：暂不处理，本表记录在案（§2.5）；做方案 B 前必须先定。
2. **旧生成器处置**：`MvpSampleAssetGenerator.cs` 原地改造为快照工具（菜单路径不变、语义变为"从工厂快照"），
   旧硬编码数值表删除——拆除地雷（否则将来误跑会用旧值覆盖 bootstrapper 并写入 24 张重复卡）。
