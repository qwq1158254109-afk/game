# AGENT.md

## 1. 项目定位

本项目是一个使用 Unity 开发的 2D 像素风学生成长模拟游戏。

核心方向：

> 学生成长模拟 + RPG 地图探索 + 场景交互 + 卡牌战斗 + 多结局成长路线

游戏不应继续停留在纯菜单式模拟器阶段，而应逐步发展为“地图驱动的学生生活 RPG”。玩家通过在不同地点移动、交互、选择活动、触发事件和进行卡牌战斗，影响学生属性、压力、心情、卡组、道具和最终结局。

---

## 2. Agent 角色

你是该 Unity 项目的开发代理，负责协助完成代码实现、系统重构、功能扩展、Bug 修复和项目结构整理。

你的目标不是一次性堆叠大量功能，而是按阶段稳定推进，让项目始终保持：

- 可运行
- 可测试
- 可扩展
- 可维护
- 可展示

每次修改都应优先保证现有功能不被破坏。

---

## 3. 当前优先方向

当前阶段重点是：

> V0.2 RPG 地图交互重构阶段

目标是先把“家地图”做成完整可玩的 RPG 场景，然后再复制扩展到学校、商店、公园等地点。

优先完成：

1. 玩家可以在家地图中自由移动。
2. 家具、墙体、门具有正确碰撞。
3. 床、电脑、书桌、门等对象可以交互。
4. 交互可以触发活动、对话或场景切换。
5. 活动会消耗时间、体力或其他资源。
6. 活动会改变学生属性。
7. 门口可以返回主界面或切换到其他地图。
8. 地图物体支持基础遮挡和层级显示。
9. 玩家行走 / 跑步动画可以正常播放。

不要在 V0.2 阶段优先开发大量新地图、复杂剧情或过多 Boss。先保证一个地图完整闭环。

---

## 4. 游戏主循环

后续所有系统都应服务于以下主循环：

```text
进入游戏
↓
查看学生状态
↓
选择地点或进入当前地图
↓
在地图中移动
↓
与 NPC / 家具 / 门 / 事件点交互
↓
执行活动或触发事件
↓
消耗时间、体力、心情、金钱等资源
↓
获得属性、道具、卡牌、金币或剧情标记
↓
进入下一时间段或下一天
↓
阶段性触发考试 / Boss / 剧情节点
↓
根据成长路线进入不同结局
```

任何新功能如果不能接入这个循环，应暂缓开发。

---

## 5. 版本路线

### V0.2：RPG 地图交互版

目标：让家地图成为完整可玩的 RPG 场景。

包含：

- 玩家移动
- 玩家动画
- Tilemap 碰撞
- 家具交互
- 门口交互
- 活动触发
- 时间消耗
- 属性变化
- 地图返回 / 切换
- 基础交互提示 UI

---

### V0.3：多场景切换版

目标：多个地点都可以进入并交互。

包含：

- 家
- 学校
- 商店
- 公园
- 地图入口统一
- 场景切换淡入淡出
- 每个地图至少 3 个交互点
- 统一 SpawnPoint / TransitionPoint 机制

---

### V0.4：卡牌战斗强化版

目标：让战斗系统具有基础策略性。

包含：

- 抽牌
- 手牌
- 弃牌
- 能量
- 攻击牌
- 防御牌
- 能力牌
- Boss 意图
- 状态效果
- 战斗奖励选牌
- 商店买卡

---

### V0.5：成长路线版

目标：让属性影响长期路线和结局。

包含：

- 多属性成长
- 每日结算
- 压力系统
- 心情系统
- 期中 / 期末 Boss
- 成长报告界面
- 多结局判断

---

### V0.6：剧情 NPC 版

目标：让游戏拥有角色、关系和支线。

包含：

- NPC 对话
- 选项事件
- 好感度
- 支线任务
- 特殊事件
- 隐藏结局

---

### V1.0：完整展示版

目标：可以作为课程项目、比赛作品或作品集展示。

建议包含：

- 完整开场
- 至少 4 个地图
- 至少 3 个 Boss
- 至少 30 张卡牌
- 至少 20 个事件
- 至少 5 个结局
- 存档系统
- 设置界面
- 音效和 BGM
- 可打包发布

---

## 6. 工程设计原则

### 6.1 UI、Controller、Data 分离

必须尽量遵守：

```text
UI 只负责显示和输入
Controller 负责流程和规则
Data 负责配置和静态信息
RuntimeModel 负责运行时状态
```

避免把大量游戏逻辑继续堆进单个 UI 脚本中。

---

### 6.2 推荐结构

建议逐步整理为以下结构：

```text
Assets/
└── _Project/
    ├── Art/
    │   ├── Characters/
    │   ├── Tilesets/
    │   ├── UI/
    │   └── Effects/
    ├── Audio/
    │   ├── BGM/
    │   └── SFX/
    ├── Prefabs/
    │   ├── Player/
    │   ├── Map/
    │   ├── UI/
    │   ├── Battle/
    │   └── NPC/
    ├── Scenes/
    │   ├── MainMenu.unity
    │   ├── Home.unity
    │   ├── School.unity
    │   ├── Shop.unity
    │   ├── Park.unity
    │   └── Battle.unity
    ├── ScriptableObjects/
    │   ├── Activities/
    │   ├── Cards/
    │   ├── Items/
    │   ├── NPCs/
    │   ├── Locations/
    │   └── Endings/
    └── Scripts/
        ├── Core/
        ├── Student/
        ├── TimeSystem/
        ├── Map/
        ├── Interaction/
        ├── Activity/
        ├── Battle/
        ├── Cards/
        ├── Inventory/
        ├── NPC/
        ├── UI/
        ├── Save/
        └── Utils/
```

如果当前项目已有不同目录结构，应优先兼容现有结构，不要一次性大规模搬迁。

---

## 7. 核心系统规范

## 7.1 学生状态系统

学生状态是全局成长核心。

建议保留或逐步整理以下属性：

```text
智力 Intelligence
体力 Stamina
心情 Mood
压力 Stress
社交 Social
金钱 Money
自律 Discipline
成绩 Grade
时间 Time
日期 Day
```

规则：

- 所有活动都必须通过统一接口修改学生状态。
- 不要在 UI 按钮中直接散落修改属性的代码。
- 属性变化应可以被 UI 刷新、每日结算、结局判断和存档系统读取。
- 压力、心情、体力应成为后续玩法的重要约束。

---

## 7.2 时间系统

时间系统用于限制每日行动次数。

建议设计：

```text
Morning
Noon
Afternoon
Evening
Night
```

或使用简单数值：

```text
CurrentDay
CurrentTimeSlot
MaxTimeSlotPerDay
```

规则：

- 大多数活动需要消耗时间。
- 战斗、考试、特殊剧情可以消耗大量时间或直接进入下一天。
- 时间耗尽后进入每日结算。
- 不允许某些入口绕过时间消耗。

---

## 7.3 地图系统

地图是后续游戏体验的核心。

每个地点应包含：

- Tilemap 地板层
- Tilemap 墙体层
- 家具层
- 前景遮挡层
- 碰撞层
- 交互点
- 玩家出生点
- 地图出口

推荐地图层级：

```text
Floor
Wall
Furniture_Back
Furniture_Front
Decoration
Collision
Interaction
Lighting
Foreground
```

规则：

- 碰撞不要散落在复杂对象上，优先使用 Tilemap Collider 2D 或统一的 Collider 物体。
- 所有可交互对象应使用统一交互系统。
- 不要为每个家具写一套单独逻辑。
- 场景切换应通过统一 TransitionPoint 触发。

---

## 7.4 交互系统

推荐抽象：

```csharp
public interface IInteractable
{
    string GetPromptText();
    bool CanInteract();
    void Interact();
}
```

推荐组件：

```text
InteractionController
InteractionDetector
InteractionPoint
InteractionPromptUI
DoorInteraction
ActivityInteraction
NpcInteraction
```

规则：

- 玩家靠近交互点时显示提示。
- 玩家按交互键时执行交互。
- 交互键优先使用当前项目已有输入方案。
- 不要在 PlayerController 中硬编码所有交互逻辑。
- 交互失败时应给出简单反馈，例如“体力不足”“时间不够”。

---

## 7.5 活动系统

活动是成长模拟的主要载体。

建议使用数据驱动：

```text
ActivityData
- ActivityId
- DisplayName
- Description
- TimeCost
- StaminaCost
- MoneyCost
- RequiredStats
- Rewards
- PossibleCards
- PossibleItems
- StressChange
- MoodChange
```

推荐流程：

```text
玩家交互活动点
↓
检查活动条件
↓
显示确认 UI
↓
执行活动
↓
扣除资源
↓
发放奖励
↓
刷新状态 UI
↓
推进时间
```

规则：

- 活动逻辑不要写死在 UI Button 中。
- 活动奖励应统一从 ActivityController 或 ActivityService 发放。
- 活动失败不能改变状态。
- 活动执行后应触发统一事件，方便 UI、音效、日志和任务系统监听。

---

## 7.6 卡牌战斗系统

战斗系统方向参考轻量化 DBG 卡牌战斗，不做复杂动作游戏。

卡牌类型：

```text
Attack
Defence
Skill
Power
Status
Item
```

学生主题卡牌示例：

```text
刷题
专注冲刺
整理笔记
深呼吸
学习计划
错题本
咖啡
运动饮料
```

敌人 / Boss 主题示例：

```text
作业山
拖延症
月考焦虑
手机诱惑
熬夜疲劳
期末考试
```

规则：

- 卡牌数据优先使用 ScriptableObject 或可序列化配置。
- 卡牌效果不要硬编码在 UI 中。
- 战斗 UI 只负责显示手牌、能量、血量、意图和按钮。
- 战斗流程由 BattleController 管理。
- 后续应支持战斗胜利后选牌、金币、道具或属性奖励。

---

## 7.7 NPC 与剧情系统

V0.6 之前只做轻量版本。

推荐结构：

```text
NpcData
DialogueData
DialogueChoice
EventFlag
RelationshipValue
```

规则：

- NPC 初期只需要支持简单对话和选项。
- 不要过早制作复杂剧情编辑器。
- 选项结果可以修改属性、发放奖励、设置剧情标记或触发活动。
- 好感度系统应在基础对话稳定后再加入。

---

## 7.8 存档系统

V0.5 之后应加入存档。

至少保存：

```text
当前日期
当前时间段
当前位置
学生属性
金币
背包
卡组
已触发事件
NPC 好感度
结局标记
```

规则：

- 存档数据应与场景对象解耦。
- 不要直接序列化 MonoBehaviour。
- 推荐使用 JSON 保存运行时数据。
- 所有 ID 必须稳定，例如 ActivityId、CardId、ItemId、NpcId。

---

## 8. Unity 编码规范

### 8.1 C# 风格

优先使用：

```csharp
[SerializeField] private FieldType fieldName;
```

避免无必要的 public 字段。

推荐：

- 类名使用 PascalCase。
- 方法名使用 PascalCase。
- 私有字段使用 camelCase 或 `_camelCase`，保持项目已有风格。
- 常量使用 PascalCase 或 ALL_CAPS，保持项目已有风格。
- 文件名必须与主类名一致。

---

### 8.2 Unity 性能规则

避免：

- 在 `Update()` 中频繁调用 `FindObjectOfType`。
- 在 `Update()` 中频繁 `GetComponent`。
- 每帧创建大量 GC 对象。
- 每个交互对象都自己检测输入。
- 在 UI 脚本中持有过多游戏规则。

推荐：

- 在 `Awake()` 或 `Start()` 中缓存引用。
- 通过事件刷新 UI。
- 使用统一 Manager / Controller 管理流程。
- 对常用引用使用 `[SerializeField]` 注入。

---

### 8.3 场景与资源规则

- 不要随意重命名已有 Scene、Prefab、ScriptableObject。
- 不要删除用户已有资源，除非任务明确要求。
- 新增 Prefab 应放入合理目录。
- 新增脚本应放入对应系统目录。
- 新增 ScriptableObject 数据应使用稳定 ID。
- 场景切换不要硬编码过多字符串，优先集中管理。

---

## 9. UI 规范

UI 应分为：

```text
StatusPanelView
ActivityConfirmView
InteractionPromptView
LocationView
BattleView
CardHandView
InventoryView
DialogueView
EndingView
```

规则：

- UI 类名优先以 `View` 结尾。
- UI 不直接决定复杂游戏规则。
- UI 接收 Controller 或 Model 的数据并刷新显示。
- 按钮点击后调用 Controller，不直接修改核心数据。
- 状态变化后统一刷新，不要多个地方重复写刷新逻辑。

---

## 10. 美术与地图表现规范

项目采用像素风时，应保持统一：

```text
统一 PPU
统一 Tile 尺寸
统一角色比例
统一 UI 像素风格
统一 Sorting Layer
统一描边和色调
```

推荐优先级：

1. 玩家四方向行走动画
2. 玩家跑步动画
3. 门开关反馈
4. NPC 待机动画
5. 交互提示动画
6. 战斗攻击反馈
7. UI 动效

Sorting Layer 建议：

```text
Background
Ground
Objects
Characters
Foreground
UI
```

---

## 11. 任务执行规则

当接到开发任务时，按以下流程执行：

1. 先理解当前功能属于哪个系统。
2. 检查是否已有类似脚本或组件。
3. 优先复用现有结构。
4. 不要重复创建功能相近的 Manager。
5. 修改前判断是否会影响现有主循环。
6. 每次只实现当前任务必要内容。
7. 保证代码能编译。
8. 修改完成后说明改动文件、使用方式和测试方法。
9. **改动完成后必须创建一个对应的 Git commit**（见 11.1）。

---

### 11.1 分支流程与提交纪律（强制）

**规则 1：main 是可玩基线，所有改动走任务分支，验收后才合并。main 上不直接提交。**

```text
main（可玩基线：只接收合并，不直接提交）
  ↑ merge --no-ff（merge commit = 「此任务验收通过」的标记）
feat/xxx / fix/xxx / chore/xxx / docs/xxx（一次性任务分支，合并后即删）
```

任务流程：

1. 从 main 切任务分支：`git checkout -b feat/xxx`（分支名小写短横线，一个任务一条）。
2. 改动在任务分支上提交（下方规则 2 全部适用）。
3. 合并前过三闸门：**recompile 0 error**（tundra 复核，见 20.2）→ **Play 冒烟**（进主流程走一遍）→ **视觉改动出对照图**（`Capture Room Screenshot` 等）。
4. 三闸门通过后 `git checkout main && git merge --no-ff <任务分支>`，然后删任务分支。
5. 整任务回滚 = `git revert -m 1 <merge-commit>`，一步退回验收前状态。
6. 文档改动同样走分支，但无编译闸门（内容自审即可）。

纪律：

- **任务分支短命、串行**：同一时间只有一条任务分支。Unity 的场景 / Prefab 是 YAML，两条长分支并行改场景必然合并冲突。
- 历史基线：2026-10-25 起 `素材` 分支 31 提交整体验收并入 main（merge commit `7719f50`）并退役，之后所有任务从 main 切出。

**规则 2：每完成一次改动，就立刻在任务分支上建一个 Git commit。不允许把多次改动攒在一起再提交。**

理由：提交是唯一的回滚点和追踪点。改动攒在一起，一旦某个改动引入回归，就无法单独回退，也无法从历史里看出「哪次改了什么」。

具体要求：

- **一个改动 = 一个 commit。** 「改动」指一次任务内一个自洽的修改单元（修一个 bug、加一个功能、调一处素材）。同一任务的多个文件如果服务于同一目的，放在同一个 commit 里。
- **提交前确认能编译。** 不允许提交编译不过的状态（`recompile` 0 error，见 20.2 第一条闸门）。这保证 main 上任意两个 merge commit 之间的历史都可以安全 bisect。
- **提交信息要写清「改了什么、为什么改」。** 只写「update」「fix」视为不合格。参照仓库已有的提交风格：`fix(map): 房间门改为按 E 交互，并修掉交互后底部提示栏不消失`。
- **提交前清理临时产物。** 探针（`.cs` + `.meta`）、`_*_probe.txt`、`_layout_dump.txt`、`_room_shot.png`、`Assets/_Recovery/`、`__pycache__/` 一律不得进 commit。`.gitignore` 已覆盖大部分，但**探针脚本本身不在忽略列表里，必须手动删除**（见 20.4「运行时探针纪律」）。
- **不提交的场景**：用户明确说「先别提交」时除外；此时应在回复里说明「改动已完成但未提交」，让用户知道工作区是脏的。

配套检查（提交前跑一遍）：

```bash
git status --porcelain          # 确认没有意外的临时文件
git diff --cached --stat        # 确认这次提交的范围符合预期
```

如果一次任务里确实包含多个不相关的改动（例如同时修了 UI 和一个美术问题），**拆成多个 commit**，不要合并成一个大的。

---

## 12. 禁止事项

除非任务明确要求，否则不要：

- 一次性重写整个项目。
- 大规模重命名文件、类、Prefab 或 Scene。
- 删除已有功能。
- 把所有逻辑继续塞进单个 UI 脚本。
- 在多个地方重复写同一套属性修改逻辑。
- 写死大量场景名、卡牌名、活动名。
- 跳过时间系统直接发放奖励。
- 为每个交互点写完全独立的脚本。
- 引入与项目规模不匹配的复杂框架。
- 在没有必要时引入第三方插件。

---

## 13. 推荐新增脚本清单

V0.2 阶段优先考虑这些脚本：

```text
PlayerController
PlayerAnimationController
InteractionDetector
InteractionPromptView
InteractionPoint
ActivityInteraction
DoorInteraction
SceneTransitionController
SpawnPoint
ActivityController
ActivityData
StudentStatusModel
StudentStatusController
TimeController
StatusPanelView
```

如果已有同类脚本，应优先扩展已有脚本，而不是重复创建。

---

## 14. 推荐数据对象

### ActivityData

用于定义一个活动。

```text
Id
DisplayName
Description
TimeCost
StaminaCost
MoneyCost
IntelligenceReward
MoodReward
StressChange
DisciplineReward
GradeReward
CardRewardIds
ItemRewardIds
```

### LocationData

用于定义一个地点。

```text
Id
DisplayName
SceneName
Description
DefaultSpawnPointId
AvailableActivities
UnlockConditions
```

### CardData

用于定义一张卡牌。

```text
Id
DisplayName
Description
Cost
CardType
TargetType
Effects
Rarity
```

### ItemData

用于定义一个道具。

```text
Id
DisplayName
Description
Price
ItemType
UseEffect
```

### EndingData

用于定义一个结局。

```text
Id
DisplayName
Description
Priority
RequiredStats
RequiredFlags
```

---

## 15. V0.2 验收标准

V0.2 完成时，应满足：

- 可以进入家地图。
- 玩家能在家地图中移动。
- 玩家不能穿墙或穿过主要家具。
- 靠近床、电脑、书桌、门时显示交互提示。
- 按交互键可以触发对应活动或切换。
- 活动能正确扣除时间 / 体力。
- 活动能正确增加或减少属性。
- 状态 UI 能刷新。
- 时间耗尽后能进入下一天或每日结算。
- 返回主界面后数据不丢失。
- 没有明显 Console Error。

---

## 16. 推荐开发顺序

V0.2 推荐顺序：

```text
1. 整理 StudentStatusModel 和 TimeController
2. 建立 PlayerController
3. 建立家地图 Tilemap 和碰撞
4. 建立 InteractionDetector
5. 建立 InteractionPromptView
6. 建立 InteractionPoint
7. 接入床 / 书桌 / 电脑活动
8. 接入 ActivityController
9. 接入属性和时间消耗
10. 接入 DoorInteraction 和 SceneTransitionController
11. 接入玩家动画
12. 测试完整闭环
```

不要先做复杂剧情、复杂战斗或大量新地点。

---

## 17. 测试要求

每次完成任务后，至少检查：

- Unity Console 是否有 Error。
- 场景是否能正常 Play。
- 新功能是否能从入口触发。
- 学生属性是否按预期变化。
- 时间是否按预期消耗。
- UI 是否刷新。
- 场景切换后玩家位置是否正确。
- 旧功能是否仍然可用。

如无法自动验证，应在回复中明确说明需要在 Unity Editor 中手动检查的步骤。

---

## 18. 回复格式要求

完成开发任务后，回复应包含：

```text
完成内容
修改文件
使用方式
测试步骤
注意事项
```

不要只说“已完成”。必须说明具体改了什么、怎么用、怎么测。

---

## 19. 当前长期目标

最终目标是把项目发展为一个完整的学生成长 RPG：

- 地图上能探索
- NPC 能对话
- 活动能成长
- 卡牌能战斗
- 属性能影响路线
- 压力和心情能制约选择
- 考试和 Boss 能形成阶段挑战
- 多结局能提供重复游玩价值

项目应始终围绕“学生成长”这个主题，不要偏离成普通打怪 RPG。

# Unity Project Rules

- Always inspect Unity Console before fixing Unity errors.
- Prefer small, testable changes.
- Do not delete assets unless explicitly requested.
- Do not rename public serialized fields without checking prefab and scene references.
- Do not change GUIDs inside .meta files unless explicitly required.
- After editing C# scripts, re-check Unity Console through Unity MCP.
- Keep runtime scripts under Assets/Scripts.
- Keep editor-only scripts under Assets/Editor.
- For 2D RPG systems, prefer modular controllers over one large runtime UI class.
- For scene interaction systems, avoid hardcoding scene object names unless there is no alternative.
- For pixel art assets, preserve import settings, Pixels Per Unit, Filter Mode, Compression, and Sprite Mode unless the task explicitly requires changes.

---

## 20. dsh（DeepSeek Harness）作业规程

本节在 dsh 接入后生效，与上文并行；环境类事实以本节为准，其余仍按上文执行。

### 20.1 开工

- 先看工作区根目录最新的 `handoff-*.md`（截至 2026-10-19 尚无该文件；出现即必读）。
- 再读本文件第 1–19 节的系统规范。
- 设计依据取实际存在的文档：`CODEX_GAME_SPEC_STAGE_MVP.md`、`CODEX_GAME_SPEC_STAGE_V2_SCENE_OPTIMIZATION.md`、`Docs/`、`ScriptCodeDocs/`。清单里提到的 GDD html 与“卡牌系统设计方案”文件当前不在仓库中，不要假装读过。
- dsh 工作区固定为 `E:\大创项目`，命令一律在此目录执行。

### 20.2 三条硬性闸门

1. 改码后必须 recompile，0 error 才允许进 Play。
2. 用 Edit 补方法前先 grep 同名方法，防 CS0111 重复定义。
3. 删脚本后先执行 `Assets/Refresh` 再 recompile，防 CS2001。

### 20.3 纪律

- 改任何文件前先备份到 `_backup_<事项>_<日期>\`（保留原目录结构）。
- 先纸面方案、经用户确认，再动实现。
- 不改用户原文件布局；产物落文件，不只口头描述。
- 全部结论必须有证据（数值 / 截图 / 日志），真实不编造。

### 20.4 环境事实

- Unity 版本 6000.4.6f1（`ProjectSettings/ProjectVersion.txt`）。
- mcp-unity 1.5.0，端口 **8095**（`ProjectSettings/McpUnitySettings.json`，2026-10-23 核对；早期文档与日志里写的 8096 是旧值，以本行为准），桥接令牌 `Library/McpUnity/bridge-token`。WorkBuddy 客户端配置在 `C:\Users\cyl\.workbuddy-ai\mcp.json`，dsh 的配置在 `C:\Users\cyl\.dsh\profiles\desktop\cordis.patch.yml`，两者各起一个 node 桥接进程，同一时间只建议一个客户端连。
- **recompile 必然把 MCP 打半残**（2026-10-19 三次复现）：任何一次 recompile（含保存脚本触发的域重载）都会让 mcp-unity 的 WebSocket 重绑失败，日志写 `Failed to start WebSocket server: Port 8095 is already in use`，表现为端口在听、握手超时。处理：关 Unity → 等端口彻底消失 → 重启 Unity。**不要反复重试 MCP**。
- **僵尸端口**：强杀 Unity 会留下「pid 已不存在、netstat 仍显示 LISTENING」的 8095。此时新启动的 Unity 会静默失去 MCP 服务（NO_LISTENER），且不自动重试。必须等 `netstat -ano | findstr :8095` 完全为空再启动。
- 因此：**一次性把代码改完再编译**，编译后统一「重启 Unity → 跑菜单 → 截图验证」，不要边改边验。
- Game 视图在 1x 下会裁切房间，不能当判据；出对照图用菜单 `Tools/Student Growth/Dev/Capture Room Screenshot`（1600×900 离屏渲染、无 HUD，输出 `_room_shot.png`）。
- 装饰落点约定：`DecorSpec(x, y)` = **底边中心**。2026-10-19 修过一次——Props sprite 的 pivot 是 (0.5,0.5)，旧代码按左下角算，导致每件装饰左移半个自身宽度（左右两把椅子实测中心 ±5.54，不是 ±4.6）。
- Play 模式下 MCP 会“半残”（握手超时假象）；probe 验证脚本不依赖 MCP。
- 删脚本后首次 recompile 报 CS2001 属清单缓存，`Assets/Refresh` 即解。
- PlayMode `run_tests` 会卡死，禁用；EditMode 可用。
- 输入测试用“等鼠标静默（45 帧）再注入”的干净注入法。

### 20.5 UI / 地图修改入口

- 颜色 / 贴图 / 边框：`Assets/Resources/StudentGrowth/UI/StudentGrowthPixelTheme.asset`
- 提示栏位置字号：`Assets/Scripts/StudentGrowth/UI/Map/MapInteractionPromptView.cs` 顶部常量
- 家四房间的摆件 / 墙体 / 门：`Assets/Editor/StudentGrowth/HomeRoomsBuilder.cs`——场景由它整体重建，**手改场景会被覆盖**
- 家四房间的**地板 / 墙材质**（2026-10-20 新增）：改 `Tools/make_shell_tex.py` 生成材质砖 → 跑 `Tools/make_rooms.py` 出四张房间底图。**只动底图，不碰场景、不改 GUID**
  - 材质来源：`Assets/Art/Pixel/Backgrounds/NewHomeSources/home_new_space.png`（1448×1086，权威空房间底图，61998 色）
  - 材质砖落在 `Assets/Art/Pixel/Generated/HomeRooms/Materials/`（`floor_tile` / `wall_band_h` / `wall_band_v`，64 PPU + Point + 无压缩）
  - 验证：`python Tools/verify_shell.py`（门洞锚点同位 + 色数门槛，输出 PASS/FAIL）
  - 实机确认：MCP 跑 `Assets/Refresh` → `Tools/Student Growth/Dev/Capture Room Screenshot`，再与 `room_*.png` 比像素（对齐取景：PNG 裁 `y16..1040, x2..1822`）
  - **三个踩过的坑**（都已写进脚本注释）：
    1. 去光照梯度必须**逐通道**重新锚定。写成标量 `reg / bg * bg.mean()` 会把三通道拉成同一灰度值——实测 R−B 从 125 掉到 0.1，地板变灰。
    2. 无缝化用**镜像半条**（`|左半 左半镜像|`），`np.roll` + 交叉淡化会留 26.7 的硬缝；镜像是 0.00。
    3. 墙面材质要取**整段 195px 再压到 64px**，只截最亮的顶部会让墙发飘（实测顶部均值 166.4 / 整段 159.4）。
  - **门洞与墙带必须同位**（2026-10-20 下午修）：四面墙带一律贴画布外沿、厚度统一 96，门洞也用同一口径（`door_hole_box()`）。旧版南墙带在 `y 928..1024` 而门洞在 `y 960..1056`，差 32px，洞口上方就露出墙面色（用户报的"门上方留空"，实测 y940..972 是奶白块）。**注意这个缺陷旧版就有，不是材质改造引入的**——旧版新旧两版在该区域逐像素相同。
  - **地板光照纵深不能逐行照抄源图**：源图亮度里混着楼梯/隔墙/家具投影，照抄会留尖峰骤降（实测 y900 骤降 −22.7）。用 `floor_light_profile()` 拟合（多项式趋势 + 宽高斯），成品最大相邻跳变 7.9。
  - **墙砖必须按像素挖洞，不能按整块跳过**（2026-10-20 傍晚修，最严重的一个 bug）：旧 `wall_band_h/v` 用「块起点是否落在门洞内」决定整块跳过。墙砖宽 240、南门洞 `(848,128)`，块 x=960 覆盖 960..1200 而起点落在洞里 → **整块 240px 被跳过，976..1199 露出地板**（用户报的"门旁边的空隙"）。改用 `_gap_runs()` 逐像素求补集。北墙当时没暴露纯属块对齐的巧合。定位脚本 `Tools/debug_south_wall.py`（逐步骤打印某点像素值，一跑就能看出是哪一步没写上去）。
  - **四条墙带不能互相压盖**：地板只铺可走区（y 96..960 / x 96..1728）；北南墙带只铺 `x 0..CANVAS_W-W_SIDE`；侧墙带只铺 `y W_TOP..CANVAS_H-W_TOP`；东北/东南角用竖带补 96×96。可走区 = 地板区 = 25.5×13.5，与碰撞体一致。
  - **门素材要连门楣一起取**：`crop(22, 0, 150, 125)` 再缩到洞高 96。只取门扇（旧 `crop(22,29,...)`）会让洞口上下露出墙面段。侧墙门用 `ROTATE_90/270`，不要用 `rotate(±90)`——后者会把踢脚线转到墙外侧。
  - **墙加厚是连锁改动**（2026-10-20 第四轮，墙 1.5→2.0 单位）：俯视布局里**墙带的竖向高度就是墙厚**。改动清单（漏一处就不一致）：① `Tools/make_shell_tex.py` 的 `BAND_H` 与外沿/压边/墙面/踢脚线四段高；② `Tools/make_rooms.py` 的 `CANVAS_W/H`、`W_SIDE/W_TOP/W_BOTTOM`、`DOOR_HOLE`；③ `HomeRoomsBuilder.cs` 的 `RoomW/H`、`Ortho`、`WalkW/H`、`InnerHalfW/Top/Bottom`、`WallThick`、**全部 WallSpec**；④ **全部 DoorSpec 的位置与尺寸**（门原先贴在旧内沿，内沿一动门就会压进地板）；⑤ `RoomScreenshotCapture.cs` 的分辨率。用 `Tools/calc_walls.py` 算 WallSpec，别手算。
  - **加厚必须同时抬 ortho**：ortho 8 只显示 16 单位高，画布加高到 17.5 后不动相机就裁掉 0.75/边，等于白加厚。
  - **当前值（2026-10-21 第五轮，方案B 四墙等厚加厚后）**：画布 **31.109×18.5（1991×1184）**、墙 **2.5（160px，四墙等厚）**、门/门洞 **160×160**、可走区 **26.11×13.5**、ortho **9.25**、内沿 x±13.055 / y −6.75..6.75、墙中心 x±14.305 / y±8.0。取 1991 而非 2106 是因为 1991/64=31.109 与 ortho 9.25 在 16:9 下的可见宽严丝合缝（2106 会左右各留 0.89 单位空隙）。
  - 该轮改动由 `Tools/apply_room_thickness.py` 完成（**逐条校验命中数，不符预期就整批中止不落盘**——这个闸门当场抓出三处计数错误：客厅/厨房共用南墙段、`±13.75f,0f,2.0f,RoomH` 的实际分布与推理相反）。共替换 48 处。
  - **门素材含"奶油墙"像素，加厚门洞时必须抹掉**（2026-10-21 用户报"左右两扇门重叠"）：素材 x0..23 是奶油墙（RGB(183,132,85) 饱和 0.55），`crop(22,0,150,125)` 仍带左墙+底部墙色共 **7.8%**，放大到 160 后成左边缘+底边一圈；侧墙门 `ROTATE_90` 后这圈墙色**横躺在房间地板上**。`_neutralize_wall_pixels()` 用「饱和≥0.42 且 L≥110」判墙并换成门框深木 (91,48,21)，降到 2.54%。**窗玻璃饱和只有 0.40，所以必须用饱和度判、不能只用亮度**。判源脚本 `Tools/probe_door_sprite_bounds.py`。
- **墙脚阴影（AO）必须补**（2026-10-20 晚）：`apply_ao()` / `ao_field()`。源图实测：北墙脚 −25%/70px、南墙脚 −53%/70px、侧墙脚 −21%/90px。**不要逐行采样源图剖面**——源图南端叠了门槛+地垫+台阶三层结构，逐行采样分不清"遮蔽"与"本来材质"，会连续踩三个坑（方向用 minimum.accumulate 压平 / 用区间最大值归一时贴墙亮边锁死 / 用远端归一时窗口末端落在阴影核心上又锁死）。最终用「深度 + 衰减长度」两个实测参数的指数式。南端深度取 0.40 而非实测 0.53（源图那部分含门洞内凹投影，我们没有内凹）。
- 房间壳只占整图色数的一部分：只统计北墙一条带，是 **595 → 2046 色**；整图 5423 → 15535。**判断房间壳改善要看墙带条，不要看整图色数。**
- `ProjectSettings.asset` 的 `activeInputHandler: 1`（只用新 Input System），但全工程 **9 个脚本**曾用旧 `UnityEngine.Input`。**2026-10-21 已修（方案 A）**：这 9 个文件现全部具备「抛一次就把 `legacyInputBlocked` 置上、之后只走新输入通道」的守卫。
  - 修前刷屏主源是 `Inventory/InventoryController.cs:67`（`Update` 里 3 处裸调用、**完全无 try/catch**），`Editor.log` 累计 **335593 条** `InvalidOperationException`。
  - 另两个文件（`UI/Battle/CardChoiceView.cs`、`UI/Map/MapInteractionConfirmView.cs`）原本只有 `try/catch`、**没有标志位**，而它们的 `Pressed` 是**静态方法**，所以标志必须写成 `static`——修前逐次重抛。
  - 补方法时照抄既有模式：`if (!legacyInputBlocked) { try { if (Input.GetKeyDown(key)) return true; } catch (InvalidOperationException) { legacyInputBlocked = true; } }`，再回退 `ReadInputSystemKey(keyPropertyName, "wasPressedThisFrame")`（**反射**读 `Keyboard.current`，不直接引用 Unity.InputSystem 程序集）。
  - 键名合法性可直接查 `Library/Bee/artifacts/*/Unity.InputSystem.dll`（`digit1Key`..`digit6Key`/`bKey`/`escapeKey`/`wasPressedThisFrame` 均在，按 ASCII 查）。**键名写错不报错、只静默失效**，必须查。详见 `Docs/InputLegacySpamFix-2026-10-21.md`。
  - **不要为反射随手加 `using System;`**（2026-10-21 踩过，代价是 Play 直接进不去）：`InventoryController` 原有三处 `Object.FindAnyObjectByType<T>()`，加上 `using System;` 后 `Object` 在 `System.Object` / `UnityEngine.Object` 之间歧义 → **3 条 CS0104**。最终修法是**去掉 `using System;`、改全限定名**（`System.Type.GetType(...)`、`catch (System.InvalidOperationException)`），比逐个加别名更彻底。有 `Object.`/`Random.`/`Type.`/`Math.` 等用法的文件尤其注意。
  - **Unity 只在窗口重获焦点时才扫描外部文件改动**：终端里改完代码，Unity 不会自己重编译（2026-10-21 实测：源文件 11:02 改完，到 11:10 `Assembly-CSharp.dll` 仍停在昨天）。所以「改完 → 等程序集时间戳变化」这个验证动作，**必须先让 Unity 窗口获得焦点或按 Ctrl+R**，否则等多久都没用。
  - **编译错误明细只在 `Library/Bee/tundra.log.json`**：`Editor.log` 里只会写一句 `All compiler errors have to be fixed before you can enter playmode!`，不含文件名与行号。查错务必用 `python Tools/extract_compile_errors.py`（读 tundra），别只看 Editor.log——本次就是只看 Editor.log 才误判成"已 0 error"。
- **进 Play 加载的是 Build Settings 第 0 个场景（`MainMenuScene`），不是编辑器里打开的场景**（2026-10-21 实测）。所以「打开房间场景 → Play」看到的仍是主菜单；要测房间必须**游戏内进入**，或让运行时探针自己 `SceneManager.LoadScene("HomeXxxScene")`。Build 顺序：MainMenu / School / StageHub / HomeMap / Shop / Library / Park / Club / SampleScene / 四间 Home 房（index 9..12）。
- **运行时验证必须走落盘探针，不能依赖 MCP**：进 Play 的域重载**必然**把 MCP 打半残（2026-10-21 一次会话内复现 4 次，8095 出现大量 `CLOSE_WAIT`）。探针形态照 skill「运行时探针纪律」：`[RuntimeInitializeOnLoadMethod]` + `DontDestroyOnLoad` + 每 0.5s 写 `_xxx_probe.txt`；只读、不接管会话；**用后即删** `.cs`+`.meta`（空目录也要删，否则 Unity 报警告）→ `Assets/Refresh` → 确认 0 error。
- **`PlayerFacingInteractor.targets` 在编辑器里显示为空数组是正常的**：由 `FullscreenMapSceneController.WireInteractionSystem()` 在**运行时**填充（`CollectInteractionTargets()`）。别据此判定"交互坏了"。
- **客厅只有 2 个交互目标**（`FamilyNpcObject` + `ExitDoorObject`）：客厅家具（沙发/茶几/鞋柜/壁架等）都是纯装饰 `DecorSpec`，**没有交互**。可交互家具在卧室（`BedObject`/`WardrobeObject`）与书房（`DeskObject`/`ComputerStudyPoint`）。
- 提示栏（`MapInteractionPromptView`）行为（2026-10-21 确定，探针实测）：靠近显示行动提示 → 按 E 进 `Confirming`（显示"确认执行：…"）→ 确认后进 `Completed`（显示结果）→ 约 2.2s 后**淡出**（`alpha=0`）→ 站在原地的 `Nearby` 状态**保持隐藏**，走开再回来才重新显示。抑制逻辑在 `MapInteractionFlowController` 的 `suppressPromptTarget`/`SuppressPromptFor()`/`ClearPromptSuppression()`。
- 摆件核对三件套（都在 `Tools/Student Growth/` 下）：
  - `Maps/Rebuild Living Room Only` — 只重建客厅
  - `Dev/Dump Room Layout` — 导 `_layout_dump.txt`（每件东西的世界坐标 + 包围盒，用来算贴墙 / 避门 / 通道）
  - `Dev/Capture Room Screenshot` — 导 `_room_shot.png`（无 HUD 对照图）
- 会覆盖手改的菜单（执行前先备份）：
  - `Tools/Student Growth/UI/Generate Map UI Prefabs`（对应 `Assets/Scripts/StudentGrowth/Editor/MapUiPrefabGeneratorEditorUtility.cs`）
  - `Tools/Student Growth/Maps/Build Home Room Scenes`（四间一起重建）/ `Rebuild Living Room Only`（只客厅）
  - `Tools/Student Growth/Maps/Rebuild Home RPG Map`（对应 `Assets/Scripts/StudentGrowth/Editor/HomeMapSceneBuilder.cs`）

### 20.6 AI 像素素材入库工序（2026-10-25 新增）

AI 生图/放大会产出"伪像素"（网格漂移、非整数格、发糊、色数上万），进 Unity 前必须过 `Tools/fix_pixel_art.py` 门控（基于 vendored 的 [Retro-Diffusion/pixel-art-fixer](https://github.com/Retro-Diffusion/pixel-art-fixer) v1.1，MIT，库在 `Tools/pixel-art-fixer/pixelfixer/`，上游提交 ef376e5）：

```bash
python Tools/fix_pixel_art.py <图片或目录>              # 检测 + 脏图自动修复
python Tools/fix_pixel_art.py <路径> --downscale-clean  # CLEAN 图额外输出 1x 缩小版
python Tools/fix_pixel_art.py <路径> --out <目录>       # 默认输出到 _tmp_img/pixel_fix_out
```

- **三态结果**（详见 report.json）：
  - `CLEAN`：本身就是真像素的整数放大（块不均匀 ≤0.5%），**不要**送修复器，直接整数倍缩小（`--downscale-clean`）或保持原样；
  - `FIXED`：真伪像素图，已由修复器重建 1x 真像素，特征是颜色数坍缩（实测 4~6 万 → 几千），可直接收货；
  - `REVIEW`：confidence=low 或修复后颜色不减反增，**必须人工比对后再收**（颜色变多=误检信号）。
- **为什么有门槛检测**：修复器对"干净整数放大"的图偶尔会误判非整数步长（实测 warm_tea：干净 4x 被判 3.893，输出 132x192 且颜色 56→64）。门槛检测按 2..16 整数步长分块验均匀性，干净的直接短路，双保险。
- **2026-10-24 全量基线**：`FanworkPixel/Generated/PNG` 54 张 → CLEAN 31 / 修复 21 / low 置信 REVIEW 2；`icon_item_energy_drink` 48x48 → 修复为 23x23（2x 块不均匀 41.67%，真脏图）。
- 修复产物是 1x 原生分辨率，进 Unity 按 10 节像素规范设置（Point 采样、无压缩、PPU 对齐）；依赖 numpy/scipy/opencv-python/Pillow（本机 Python 3.14 已装齐）。
- 该目录被 `.gitignore` 的 `/_tmp_img/` 规则覆盖的只有输出目录；`Tools/pixel-art-fixer/` 与 `Tools/fix_pixel_art.py` 已入库。
