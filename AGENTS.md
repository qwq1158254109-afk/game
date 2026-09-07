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
