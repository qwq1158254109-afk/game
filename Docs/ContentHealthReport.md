# 内容体检报告

> 由 `ContentHealthAudit` 生成（工具位于 `Assets/Scripts/StudentGrowth/Editor/ContentHealthAudit.cs`）
> 生成时间：2026-10-22 14:19:59

> **口径分两套，并排给出，不静默替换。**
> 「纸面上界」= 把扫描到的所有来源都算上（含未接线资产、锁死地点内的内容），首版口径，系统性偏乐观。
> 「实机上界」= 只算「已接线 + 所在地点可达」的来源。**做决策请看这一列。**

## 0. 结论速览

| 检查项 | 结果 |
| --- | --- |
| 不可达结局（实机口径） | **0 / 6** |
| 不可达地点 | **0 / 6** |
| 零来源的属性（实机口径） | **0 / 13** |
| 瓶颈属性（实机来源仅 1 处） | **0 / 13** |
| 无来源或无出口的资源类型 | **7 / 13** |
| **未接线的活动资产**（写完没挂进场景） | **3 / 14** |
| **实机不可达的活动**（未接线 + 地点锁死） | **3 / 14** |
| 实机可达活动（按 activityId 去重） | **11 / 14** |
| 阶段地图上无任何活动的地点 | **0**（—） |
| **阻塞级问题合计** | **3** |

> 「阻塞级」= 不可达结局 + 不可达地点 + 零来源属性 + **未接线活动**。
> 资源类型为空通常是设计选择（如本作尚未实现删牌），单列不计数。
> 「实机不可达的活动」不重复计入阻塞级——其中因地点锁死而不可达的部分，已经由「不可达地点」计过一次。

## 1. 数据源清点

- GameBootstrapperData 资产：**未找到**（StudentGrowth/MvpBootstrapperData）→ 实际运行走 MvpSampleDataFactory 代码兜底
- 运行时内容：阶段 3 / 地点 6 / 结局 6
- 场景接线索引：扫描地图场景 11 个（另跳过 Backups/_Recovery 下的 30 个），找到 14 个活动资产被 MapInteractionTarget 引用（合计 22 处引用）
- 资产内容：Assets/Data 与 Assets/Resources 下扫描到 ScriptableObject 67 个，其中活动 14 个
- 地点可达性不动点：迭代 1 轮后收敛，可达地点 6 / 6

## 2. 结局可达性

判定方法：把每个门槛属性的**实机可达来源增量求和**得到上界；上界够不到门槛即判不可达。
有 `⚠ 纸面口径会误判` 标记的结局，是指用「把所有扫描到的来源都算上」的口径会判成可达——差异来自未接线资产或锁死地点内的内容。

| 结局 | 优先级 | 实机判定 | 原因 |
| --- | ---: | --- | --- |
| 压力调整<br><code>ending_pressure_recovery</code> | 60 | 可达（上界） | — |
| 稳定升学<br><code>ending_stable_admission</code> | 50 | 可达（上界） | — |
| 兴趣成长<br><code>ending_interest_growth</code> | 40 | 可达（上界） | — |
| 家庭支持<br><code>ending_family_support</code> | 40 | 可达（上界） | — |
| 自主选择<br><code>ending_self_driven</code> | 40 | 可达（上界） | — |
| 普通但完整的成长<br><code>ending_growth_complete</code> | 0 | 可达（上界） | — |

## 3. 地点解锁可达性

| 地点 | 默认解锁 | 判定 | 备注 |
| --- | --- | --- | --- |
| 家里<br><code>location_home</code> | 是 | 可达 | 阶段地图状态：stage_01_adaptation = Unlocked｜stage_02_month_exam = Unlocked｜stage_03_final_sprint = Unlocked<br>阶段地图上可直接进入 |
| 学校<br><code>location_school</code> | 是 | 可达 | 阶段地图状态：stage_01_adaptation = Unlocked｜stage_02_month_exam = Unlocked｜stage_03_final_sprint = Unlocked<br>阶段地图上可直接进入 |
| 商店<br><code>location_shop</code> | 是 | 可达 | 阶段地图状态：stage_01_adaptation = Unlocked｜stage_02_month_exam = Unlocked｜stage_03_final_sprint = Unlocked<br>阶段地图上可直接进入 |
| 公园<br><code>location_park</code> | 是 | 可达 | 阶段地图状态：stage_01_adaptation = Unlocked｜stage_02_month_exam = Unlocked｜stage_03_final_sprint = Unlocked<br>阶段地图上可直接进入 |
| 图书馆<br><code>location_library</code> | 是 | 可达 | 阶段地图状态：stage_01_adaptation = Unlocked｜stage_02_month_exam = Unlocked｜stage_03_final_sprint = Unlocked<br>阶段地图上可直接进入 |
| 社团活动室<br><code>location_club</code> | 是 | 可达 | 阶段地图状态：stage_01_adaptation = Locked｜stage_02_month_exam = Locked｜stage_03_final_sprint = Unlocked<br>阶段地图上可直接进入 |

## 4. 资源来源与出口（RewardType 统计）

| RewardType | 出现次数 | 判定 |
| --- | ---: | --- |
| AddCard | 1 | 有 |
| UpgradeCard | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| RemoveCard | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| AddPlayerStat | 42 | 有 |
| ReducePlayerStat | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| AddMoney | 3 | 有 |
| ReducePressure | 7 | 有 |
| IncreaseMaxEnergy | 2 | 有 |
| AddItem | 2 | 有 |
| RemoveItem | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| UnlockActivity | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| UnlockStage | 0 | ⚠ 零次出现 —— 无来源或无出口 |
| AddStageTime | 0 | ⚠ 零次出现 —— 无来源或无出口 |

## 5. 属性来源

「实机来源数」只统计已接线且所在地点可达的来源；「纸面来源数」把未接线资产也算上。两列差距大的属性，说明它的增量大多挂在玩家碰不到的内容上。

| 属性 | 初始值 | 实机来源数 | 纸面来源数 | 实机上界 | 纸面上界 | 判定 | 来源示例（实机） |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| 语文 | 35 | 3 | 3 | 41 | 41 | 有 | 阶段可用活动固定奖励 ｜ activity_focus_class（认真听课）<br>活动资产固定奖励 ｜ activity_school_class_study（课堂学习）<br>卡牌效果 ｜ card_study_note_taking（课堂笔记） |
| 数学 | 35 | 5 | 6 | 49 | 55 | 有（纸面多 1 处不可达） | 阶段可用活动固定奖励 ｜ activity_math_drill（数学刷题）<br>活动资产固定奖励 ｜ activity_school_class_study（课堂学习）<br>活动资产固定奖励 ｜ activity_school_teacher_help（老师答疑） |
| 英语 | 35 | 2 | 2 | 40 | 40 | 有 | 阶段可用活动关联小游戏成功 ｜ activity_word_memory_mini（单词记忆）<br>卡牌效果 ｜ card_study_word_dictation（默写单词） |
| 精力 | 60 | 5 | 6 | 60 | 60 | 有（纸面多 1 处不可达） | 阶段可用活动固定奖励 ｜ activity_rest_early_sleep（早点休息）<br>阶段可用活动固定奖励 ｜ activity_music_break（听音乐放松）<br>活动资产固定奖励 ｜ activity_park_bench_rest（长椅休息） |
| 精力上限 | 60 | 2 | 2 | 64 | 64 | 有 | 阶段可用活动固定奖励 ｜ activity_rest_early_sleep（早点休息）<br>阶段可用活动固定奖励 ｜ activity_music_break（听音乐放松） |
| 压力 | 20 | 4 | 5 | 38 | 41 | 有（纸面多 1 处不可达） | 阶段Boss失败奖励 ｜ boss_class_quiz（课堂小测）<br>阶段Boss失败奖励 ｜ boss_month_exam（月考）<br>阶段Boss失败奖励 ｜ boss_final_exam（期末综合考试） |
| 心情 | 60 | 7 | 8 | 83 | 85 | 有（纸面多 1 处不可达） | 阶段可用活动关联事件选项1(稳妥处理) ｜ activity_family_dinner_event（家庭晚饭聊天）<br>阶段可用活动关联事件选项1(稳妥处理) ｜ activity_weekend_plan_event（周末安排）<br>活动资产固定奖励 ｜ activity_club_relax（社团休息） |
| 亲子关系 | 50 | 6 | 6 | 67 | 67 | 有 | 阶段可用活动关联事件选项1(稳妥处理) ｜ activity_family_dinner_event（家庭晚饭聊天）<br>阶段可用活动关联事件选项1(稳妥处理) ｜ activity_weekend_plan_event（周末安排）<br>活动资产固定奖励 ｜ activity_home_family_dinner（家庭晚饭聊天） |
| 社交 | 40 | 3 | 3 | 50 | 50 | 有 | 活动资产固定奖励 ｜ activity_club_meeting（社团讨论）<br>卡牌效果 ｜ card_social_club_activity（社团活动）<br>卡牌效果 ｜ card_social_group_discussion（小组讨论） |
| 兴趣 | 40 | 5 | 5 | 53 | 53 | 有 | 活动资产固定奖励 ｜ activity_club_planning（策划社团活动）<br>活动资产固定奖励 ｜ activity_park_flower_viewing（赏花观察）<br>卡牌效果 ｜ card_interest_competition（参加竞赛） |
| 自信 | 45 | 9 | 9 | 70 | 70 | 有 | 阶段Boss通关奖励 ｜ boss_class_quiz（课堂小测）<br>阶段Boss通关奖励 ｜ boss_month_exam（月考）<br>阶段可用活动固定奖励 ｜ activity_final_review（综合复习） |
| 自主性 | 35 | 3 | 5 | 43 | 50 | 有（纸面多 2 处不可达） | 阶段可用活动关联小游戏成功 ｜ activity_focus_timing_mini（专注计时）<br>活动资产固定奖励 ｜ activity_club_planning（策划社团活动）<br>卡牌效果 ｜ card_family_do_housework（做点家务） |
| 金钱 | 100 | 3 | 3 | 250 | 250 | 有 | 阶段Boss通关奖励 ｜ boss_class_quiz（课堂小测）<br>阶段Boss通关奖励 ｜ boss_month_exam（月考）<br>阶段Boss通关奖励 ｜ boss_final_exam（期末综合考试） |

> 不可达的来源明细（举例）：

- **数学**：活动资产固定奖励 ｜ activity_home_computer_study（电脑学习）
- **精力**：活动资产固定奖励 ｜ activity_home_rest_early_sleep（早点休息）
- **压力**：活动资产固定奖励 ｜ activity_home_computer_study（电脑学习）
- **心情**：活动资产固定奖励 ｜ activity_home_desk_plan（整理计划）
- **自主性**：活动资产固定奖励 ｜ activity_home_computer_study（电脑学习）；活动资产固定奖励 ｜ activity_home_desk_plan（整理计划）

## 6. 活动可达性（资产写完 ≠ 内容可用）

判定方法：活动资产必须被某个地图场景的 `MapInteractionTarget` 引用（= 接线），且它所在的地点在实机里可达，玩家才可能看到这个活动。
交互的真实机制走的是场景里的 `MapInteractionTarget.fallbackActivity` ——`LocationActionData`（`locationAction` 字段）在本项目里全是空的，实际没参与任何交互。

| 活动 | activityId | 所在地点 | 场景接线 | 实机可达 | 备注 |
| --- | --- | --- | --- | --- | --- |
| 社团讨论 | <code>activity_club_meeting</code> | location_club | ✅ | ✅ | — |
| 策划社团活动 | <code>activity_club_planning</code> | location_club | ✅ | ✅ | — |
| 社团休息 | <code>activity_club_relax</code> | location_club | ✅ | ✅ | — |
| 电脑学习 | <code>activity_home_computer_study</code> | — | ❌ 未接线 | **❌** | 没有被任何地图场景的 MapInteractionTarget 引用 → 玩家永远看不到这个活动（资产写完没接线） |
| 整理计划 | <code>activity_home_desk_plan</code> | — | ❌ 未接线 | **❌** | 没有被任何地图场景的 MapInteractionTarget 引用 → 玩家永远看不到这个活动（资产写完没接线） |
| 家庭晚饭聊天 | <code>activity_home_family_dinner</code> | location_home | ✅ | ✅ | — |
| 早点休息 | <code>activity_home_rest_early_sleep</code> | — | ❌ 未接线 | **❌** | 没有被任何地图场景的 MapInteractionTarget 引用 → 玩家永远看不到这个活动（资产写完没接线） |
| 询问图书管理员 | <code>activity_library_librarian_talk</code> | location_library | ✅ | ✅ | — |
| 长椅休息 | <code>activity_park_bench_rest</code> | location_park | ✅ | ✅ | — |
| 赏花观察 | <code>activity_park_flower_viewing</code> | location_park | ✅ | ✅ | — |
| 池边散步 | <code>activity_park_pond_walk</code> | location_park | ✅ | ✅ | — |
| 课堂学习 | <code>activity_school_class_study</code> | location_school | ✅ | ✅ | — |
| 老师答疑 | <code>activity_school_teacher_help</code> | location_school | ✅ | ✅ | — |
| 浏览商店 | <code>activity_shop_browse_goods</code> | location_shop | ✅ | ✅ | — |

> 同上：地点栏为空 = 没有任何场景引用它；资产路径见下表。

| 活动 ID | 资产路径 |
| --- | --- |
| <code>activity_club_meeting</code> | Assets/Data/StudentGrowth/New2_0/ClubMap/ClubMeeting.asset |
| <code>activity_club_planning</code> | Assets/Data/StudentGrowth/New2_0/ClubMap/ClubPlanning.asset |
| <code>activity_club_relax</code> | Assets/Data/StudentGrowth/New2_0/ClubMap/ClubRelax.asset |
| <code>activity_home_computer_study</code> | Assets/Data/StudentGrowth/New2_0/HomeMap/New20_HomeComputerStudy.asset |
| <code>activity_home_desk_plan</code> | Assets/Data/StudentGrowth/New2_0/HomeMap/New20_HomeDeskPlan.asset |
| <code>activity_home_family_dinner</code> | Assets/Data/StudentGrowth/New2_0/HomeMap/New20_HomeFamilyDinner.asset |
| <code>activity_home_rest_early_sleep</code> | Assets/Data/StudentGrowth/New2_0/HomeMap/New20_HomeRestEarlySleep.asset |
| <code>activity_library_librarian_talk</code> | Assets/Data/StudentGrowth/New2_0/LibraryMap/LibraryLibrarianTalkData.asset |
| <code>activity_park_bench_rest</code> | Assets/Data/StudentGrowth/New2_0/ParkMap/ParkBenchRest.asset |
| <code>activity_park_flower_viewing</code> | Assets/Data/StudentGrowth/New2_0/ParkMap/ParkFlowerViewing.asset |
| <code>activity_park_pond_walk</code> | Assets/Data/StudentGrowth/New2_0/ParkMap/ParkPondWalk.asset |
| <code>activity_school_class_study</code> | Assets/Data/StudentGrowth/New2_0/SchoolMap/SchoolClassStudy.asset |
| <code>activity_school_teacher_help</code> | Assets/Data/StudentGrowth/New2_0/SchoolMap/SchoolTeacherHelp.asset |
| <code>activity_shop_browse_goods</code> | Assets/Data/StudentGrowth/New2_0/ShopMap/New20_ShopBrowseGoods.asset |

## 7. 附带发现

- 未发现重复 ID。

## 附录 A：已知答案对照（工具自检）

工具每次运行都必须能报出以下已知问题。**报不出即说明工具写错，而不是内容没问题。**
（这些问题是在第一轮人工核查里确定的答案；工具升级或内容修复后，本表要同步更新。）

| 已知问题 | 是否报出 |
| --- | --- |
| 结局「稳定升学」不可达（三科均值上界不足） | ❌ **未报出** |
| 结局「兴趣成长」不可达 | ❌ **未报出** |
| 结局「自主选择」不可达 | ❌ **未报出** |
| 结局「压力调整」不可达 | ❌ **未报出** |
| 结局「家庭支持」不可达（亲子上界不足） | ❌ **未报出** |
| 属性「英语」实机来源 ≤1 处（瓶颈） | ❌ **未报出** |
| 属性「金钱」实机零来源 | ❌ **未报出** |
| 资源「AddMoney」零次出现（金钱无来源） | ❌ **未报出** |
| 地点「学校」无任何已接线活动 | ❌ **未报出**（实际 2 个） |
| 地点「图书馆」有且仅有 1 个已接线活动 | ✅ 已报出（实际 1 个） |
| 存在未接线的活动资产（写完没挂进场景） | ✅ 已报出（3 个） |

**— 以下为 2026-09-23 修复后应成立的状态（修复前是反例）—**

| 修复后应成立 | 是否成立 |
| --- | --- |
| 地点「社团活动室」可达（第三学段开放，不再依赖属性门槛） | ✅ 已报出 |
| 属性「社交」实机来源 ≥1 处（社团开放后救回） | ✅ 已报出 |
| 属性「兴趣」实机来源 ≥2 处 | ✅ 已报出 |
| 无任何已接线活动被地点锁死 | ✅ 已报出（0 个） |
| 参考：实机可达活动（按 activityId 去重） | 11 / 14 |

> **修复前（同日基线）**：社团活动室不可达（兴趣/社交门槛自锁——两个属性的增量来源有一半在社团里面）；连带 3 个已接线活动、6 个交互点位锁死；社交实机零来源（40 恒定）、兴趣实机仅 1 处来源（上界 42）。
> 修复内容：`StageMapLocationAvailabilityService` 删掉 `club_interest_or_social` 键与 `HasClubAccess`，社团改为 `stage_03_or_later`；`MvpSampleDataFactory` 里社团的 `unlockedByDefault` 改为 true 并移除属性门槛。

> 上表里的数量断言是**人工核查确认过的答案**。内容被合法修改后这些数字会变，届时请同步更新本表——变了不一定是工具错，但一定要有人解释为什么变。

## 附录 B：口径说明

1. **这是上界推算，不是完整模拟。** 报「不可达」必然为真（连把全部来源都吃到都不够）；报「可达」只代表上界够，实战还受阶段时间预算与内容供给量约束——那一层由别的检查负责。
2. **同一 ID 只计一次。** 属性增量按 (来源 ID, 属性) 归并：**不同 ID 之间求和，同一 ID 的多次出现只取绝对值最大的一次**（取最大而非取第一份，是为了让上界不因去重而被低估）。重复 ID 本身记在「附带发现」里，建议人工合并。
3. **「瓶颈属性」= 实机只有 1 处来源。** 这类属性是单点依赖：那处来源一旦被删或被解锁条件卡住，该属性就再也不涨。零来源是"完全不可达"，仅 1 处来源是"极脆弱"，两者都值得处理。
4. **属性上限取自 `ValueClampUtility`**：常规属性 0–100，精力上限按 `maxEnergy`，金钱 0–9999。
5. **地点标记检查**是启发式：扫描 `StageMapLocationAvailabilityService` 里 `locationFlags.TryGetValue("...")` 的字面量，再数该字符串在 `Assets` 下 `.cs` 文件中的出现文件数；只有读取点、没有写入点即判「该分支恒为假」。
6. **可达性口径（本版新增）**：一个来源算「实机可达」需同时满足 —— ① 它所在的资产被地图场景的 `MapInteractionTarget` 引用过；② 该场景对应的地点在不动点迭代里被点亮。
   - 「地点可达」的迭代方式：起点只算全局来源（阶段活动、卡牌、道具、阶段奖励）与默认解锁地点内的来源，然后反复「能开就开、重算边界」，最多 8 轮。这样能捕获**门槛属性只在锁死地点内有来源**的自锁。
   - 已知保守处：若活动接到了一个不在阶段地图配置里的场景，会被判为「地点无法解析 → 不可达」。这属于宁可漏报也不虚报；发现这种情况请把该场景补进 `MvpSampleDataFactory.CreateDefaultStageMapData`。
7. **场景扫描范围**：`Assets/Scenes/StudentGrowth/Maps` 下全部 `.unity`，**排除** `Backups/` 与 `_Recovery/`——那里的引用不代表内容已经上线。
8. **本工具不修改任何内容。** 只读资产、场景与源码，唯一的写操作是导出本报告。

