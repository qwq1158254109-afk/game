# 内容体检报告

> 由 `ContentHealthAudit` 生成（工具位于 `Assets/Scripts/StudentGrowth/Editor/ContentHealthAudit.cs`）
> 生成时间：2026-09-23 14:25:39

## 0. 结论速览

| 检查项 | 结果 |
| --- | --- |
| 不可达结局 | **5 / 6** |
| 不可达地点 | **1 / 6** |
| 零来源的属性 | **2 / 13** |
| 瓶颈属性（来源仅 1 处） | **2 / 13** |
| 无来源或无出口的资源类型 | **9 / 13** |
| **阻塞级问题合计** | **8** |

> 「阻塞级」= 不可达结局 + 不可达地点 + 零来源属性。资源类型为空通常是设计选择（如本作尚未实现删牌），单列不计数。

## 1. 数据源清点

- GameBootstrapperData 资产：**未找到**（StudentGrowth/MvpBootstrapperData）→ 实际运行走 MvpSampleDataFactory 代码兜底
- 运行时内容：阶段 3 / 地点 6 / 结局 6
- 资产内容：Assets/Data 与 Assets/Resources 下扫描到 ScriptableObject 37 个，其中活动 17 个

## 2. 结局可达性

判定方法：把每个门槛属性的**全部可及来源增量求和**得到上界；上界够不到门槛即判不可达。

| 结局 | 优先级 | 判定 | 原因 |
| --- | ---: | --- | --- |
| 压力调整<br><code>ending_pressure_recovery</code> | 60 | **不可达** | 压力 需 ≥ 85，但全部来源的上界只有 38 |
| 稳定升学<br><code>ending_stable_admission</code> | 50 | **不可达** | 要求三科均值 ≥ 55，上界仅 44 |
| 兴趣成长<br><code>ending_interest_growth</code> | 40 | **不可达** | 兴趣 需 ≥ 70，但全部来源的上界只有 47 |
| 家庭支持<br><code>ending_family_support</code> | 40 | **不可达** | 亲子关系 需 ≥ 70，但全部来源的上界只有 59 |
| 自主选择<br><code>ending_self_driven</code> | 40 | **不可达** | 自主性 需 ≥ 70，但全部来源的上界只有 48 |
| 普通但完整的成长<br><code>ending_growth_complete</code> | 0 | 可达（上界） | — |

## 3. 地点解锁可达性

| 地点 | 默认解锁 | 判定 | 备注 |
| --- | --- | --- | --- |
| 家里<br><code>location_home</code> | 是 | 可达 | 默认解锁 |
| 学校<br><code>location_school</code> | 是 | 可达 | 默认解锁 |
| 商店<br><code>location_shop</code> | 是 | 可达 | 默认解锁 |
| 公园<br><code>location_park</code> | 是 | 可达 | 默认解锁 |
| 图书馆<br><code>location_library</code> | 是 | 可达 | 默认解锁 |
| 社团活动室<br><code>location_club</code> | 否 | **不可达** | ⚠ 该地点依赖地点标记 <code>club_intro</code>，但该字符串**只在 1 个文件中出现**（Assets/Scripts/StudentGrowth/Map/StageMapLocationAvailabilityService.cs）——即只有读取点、没有写入点，条件永远不成立 |

## 4. 资源来源与出口（RewardType 统计）

| RewardType | 出现次数 | 判定 |
| --- | ---: | --- |
| AddCard | 1 | 有 |
| UpgradeCard | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| RemoveCard | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| AddPlayerStat | 47 | 有 |
| ReducePlayerStat | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| AddMoney | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| ReducePressure | 8 | 有 |
| IncreaseMaxEnergy | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| AddItem | 2 | 有 |
| RemoveItem | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| UnlockActivity | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| UnlockStage | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| AddStageTime | 0 | ⚠ 零次出现 —— 无来源或无出口 |

## 5. 属性来源

| 属性 | 初始值 | 来源数 | 上界 | 判定 | 来源示例 |
| --- | ---: | ---: | ---: | --- | --- |
| 语文 | 35 | 2 | 40 | 有 | 阶段可用活动固定奖励 ｜ activity_focus_class（认真听课）<br>活动资产固定奖励 ｜ activity_school_class_study（课堂学习） |
| 数学 | 35 | 5 | 54 | 有 | 阶段可用活动固定奖励 ｜ activity_math_drill（数学刷题）<br>活动资产固定奖励 ｜ activity_home_computer_study（电脑学习）<br>活动资产固定奖励 ｜ activity_school_class_study（课堂学习） |
| 英语 | 35 | 1 | 39 | **仅 1 处来源（瓶颈）** | 阶段可用活动关联小游戏成功 ｜ activity_word_memory_mini（单词记忆） |
| 精力 | 60 | 5 | 60 | 有 | 阶段可用活动固定奖励 ｜ activity_rest_early_sleep（早点休息）<br>阶段可用活动固定奖励 ｜ activity_music_break（听音乐放松）<br>活动资产固定奖励 ｜ activity_home_rest_early_sleep（早点休息） |
| 精力上限 | 60 | 0 | 60 | **零来源** | — |
| 压力 | 20 | 4 | 38 | 有 | 阶段Boss失败奖励 ｜ boss_class_quiz（课堂小测）<br>阶段Boss失败奖励 ｜ boss_month_exam（月考）<br>阶段Boss失败奖励 ｜ boss_final_exam（期末综合考试） |
| 心情 | 60 | 8 | 85 | 有 | 阶段可用活动关联事件选项1(稳妥处理) ｜ activity_family_dinner_event（家庭晚饭聊天）<br>阶段可用活动关联事件选项1(稳妥处理) ｜ activity_weekend_plan_event（周末安排）<br>活动资产固定奖励 ｜ activity_home_family_dinner（家庭晚饭聊天） |
| 亲子关系 | 50 | 3 | 59 | 有 | 阶段可用活动关联事件选项1(稳妥处理) ｜ activity_family_dinner_event（家庭晚饭聊天）<br>阶段可用活动关联事件选项1(稳妥处理) ｜ activity_weekend_plan_event（周末安排）<br>活动资产固定奖励 ｜ activity_home_family_dinner（家庭晚饭聊天） |
| 社交 | 40 | 1 | 45 | **仅 1 处来源（瓶颈）** | 活动资产固定奖励 ｜ activity_club_meeting（社团讨论） |
| 兴趣 | 40 | 2 | 47 | 有 | 活动资产固定奖励 ｜ activity_club_planning（策划社团活动）<br>活动资产固定奖励 ｜ activity_park_flower_viewing（赏花观察） |
| 自信 | 45 | 6 | 63 | 有 | 阶段Boss通关奖励 ｜ boss_class_quiz（课堂小测）<br>阶段Boss通关奖励 ｜ boss_month_exam（月考）<br>阶段可用活动固定奖励 ｜ activity_final_review（综合复习） |
| 自主性 | 35 | 4 | 48 | 有 | 阶段可用活动关联小游戏成功 ｜ activity_focus_timing_mini（专注计时）<br>活动资产固定奖励 ｜ activity_home_desk_plan（整理计划）<br>活动资产固定奖励 ｜ activity_club_planning（策划社团活动） |
| 金钱 | 100 | 0 | 100 | **零来源** | — |

## 6. 附带发现

- 重复 ID <code>activity_home_desk_plan</code> 出现在 2 处：<br>ActivityData(资产)  Assets/Data/StudentGrowth/HomeMap/HomeDeskPlan.asset<br>ActivityData(资产)  Assets/Data/StudentGrowth/New2_0/HomeMap/New20_HomeDeskPlan.asset<br>（同一 ID 会被视为同一份内容，上界取最大值不叠加；但数据源冲突需人工确认以哪份为准）
- 重复 ID <code>activity_home_family_dinner</code> 出现在 2 处：<br>ActivityData(资产)  Assets/Data/StudentGrowth/HomeMap/HomeFamilyDinner.asset<br>ActivityData(资产)  Assets/Data/StudentGrowth/New2_0/HomeMap/New20_HomeFamilyDinner.asset<br>（同一 ID 会被视为同一份内容，上界取最大值不叠加；但数据源冲突需人工确认以哪份为准）
- 重复 ID <code>activity_home_rest_early_sleep</code> 出现在 2 处：<br>ActivityData(资产)  Assets/Data/StudentGrowth/HomeMap/HomeRestEarlySleep.asset<br>ActivityData(资产)  Assets/Data/StudentGrowth/New2_0/HomeMap/New20_HomeRestEarlySleep.asset<br>（同一 ID 会被视为同一份内容，上界取最大值不叠加；但数据源冲突需人工确认以哪份为准）

## 附录 A：已知答案对照（工具自检）

工具首次运行时，必须能报出以下已知问题。**报不出即说明工具写错，而不是内容没问题。**

| 已知问题 | 是否报出 |
| --- | --- |
| 结局「稳定升学」不可达（三科均值上界不足） | ✅ 已报出 |
| 结局「兴趣成长」不可达 | ✅ 已报出 |
| 结局「自主选择」不可达 | ✅ 已报出 |
| 结局「压力调整」不可达 | ✅ 已报出 |
| 地点「社团活动室」不可达 | ✅ 已报出 |
| 属性「英语」来源不足（≤1 处，瓶颈） | ✅ 已报出 |
| 资源「AddMoney」零次出现（金钱无来源） | ✅ 已报出 |

## 附录 B：口径说明

1. **这是上界推算，不是完整模拟。** 报「不可达」必然为真（连把全部来源都吃到都不够）；报「可达」只代表上界够，实战还受阶段时间预算与内容供给量约束——那一层由别的检查负责。
2. **同一 ID 只计一次。** 属性增量按 (来源 ID, 属性) 归并：**不同 ID 之间求和，同一 ID 的多次出现只取绝对值最大的一次**（取最大而非取第一份，是为了让上界不因去重而被低估）。重复 ID 本身记在「附带发现」里，建议人工合并。
3. **「瓶颈属性」= 全工程只有 1 处来源。** 这类属性是单点依赖：那处来源一旦被删或被解锁条件卡住，该属性就再也不涨。零来源是"完全不可达"，仅 1 处来源是"极脆弱"，两者都值得处理。
4. **属性上限取自 `ValueClampUtility`**：常规属性 0–100，精力上限按 `maxEnergy`，金钱 0–9999。
5. **标记位检查**是启发式：扫描 `Assets` 下所有 `.cs` 中该字符串的出现文件数。只出现于 1 个文件（即 `StageMapLocationAvailabilityService`）时判定为「只读不写」。
6. **本工具不修改任何内容。** 只读资产与源码，唯一的写操作是导出本报告。

