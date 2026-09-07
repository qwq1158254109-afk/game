# StudentGrowth 项目策划书

生成日期：2026-05-20

## 1. 项目定位

项目名称暂定：`StudentGrowth`

游戏类型：

- 学生成长模拟经营。
- 阶段制养成。
- 2D RPG 地图探索。
- 卡牌回合制挑战。
- 事件选择和 NPC 对话。
- 可选 API 驱动的动态对话和随机事件。

一句话概念：

玩家扮演一名普通学生，在 2D RPG 地图中移动并与地点、NPC、事件点互动，通过有限时间内的学习、休息、社交、家庭沟通、小游戏和卡牌挑战积累属性、卡牌与道具，最终面对阶段 Boss 并走向不同成长结局。

核心体验不是“刷分变强”，而是“在成长压力下做选择”。玩家需要平衡成绩、精力、压力、亲子关系、社交、兴趣、自信、自主性和金钱。API 功能开启后，NPC 和部分事件可以生成更丰富的对话内容；关闭后，游戏使用默认文案和固定事件，保证离线也能完整游玩。

## 2. 已有项目基础

当前项目已经具备一套阶段制学生成长 MVP 架构。

主要运行时系统：

- `GameBootstrapper`：初始化会话、初始牌组、初始道具、阶段、地点和结局；优先读取 `Resources/StudentGrowth/MvpBootstrapperData`，缺失时回退到代码样例数据。
- `GameBootstrapperData`：用于配置正式入口数据，包括初始牌组、初始道具、阶段、地点、结局和首个阶段。
- `MvpSampleDataFactory`：运行时创建三阶段、默认地点、默认结局、初始牌组和初始道具。
- `StageController`：管理阶段开始、活动结算、Boss 解锁、Boss 结果、下一阶段和结局触发。
- `ActivityController`：根据活动类型解析训练、休息、事件、小游戏、普通挑战等活动结果。
- `SceneTransitionController`：管理地点进入、返回地图、地点解锁、地点访问记录和当前地点 ID。
- `LocationData`、`LocationActionData`、`LocationState`：已经为后续 2D RPG 地图和地点交互提供数据基础。
- `ChallengeController`：管理卡牌挑战开始、出牌、回合结束、Boss 奖励和胜负。
- `DeckController`：管理主卡组、抽牌堆、手牌、弃牌堆和消耗堆。
- `RewardResolver`：统一发放奖励。
- `CardEffectResolver`：解析卡牌和道具共用效果。
- `ItemEffectResolver`：处理主动道具、被动道具和阶段限定道具。
- `MiniGameController`：按成功率公式结算小游戏。
- `EndingController`：按结局条件和优先级选择最终结局。
- `StudentGrowthMvpRuntimeUi`：运行时自动创建 MVP UI，已包含阶段界面、地点/地图界面、活动交互、Boss 界面、卡牌战斗和结局界面。
- `BattleCardView`、`HandLayoutController`、`BattleTooltipView`、`BattleTooltipTarget`、`BattleUIConfig`：支撑战斗手牌、拖拽、悬停、Tooltip 和 UI 配置。

主要编辑器工具：

- `MvpSampleAssetGenerator`：生成 MVP 示例 ScriptableObject 资产。
- `GameArtBindingEditorUtility`：为已知数据资产绑定美术 Sprite 引用。

已有测试覆盖：

- 玩家属性边界。
- 奖励发放。
- 卡牌效果。
- 牌堆操作。
- 挑战胜负。
- 阶段推进。
- 小游戏成功率。
- 道具使用和被动触发。
- 美术路径和绑定。
- 启动器回退。
- 地点切换。
- 结局选择。

## 3. 新增策划方向

根据后续策划要求，项目下一阶段增加三个核心方向：

1. 地图改为 2D RPG 地图，玩家通过角色移动和交互点进行操作。
2. 设置界面加入 API 板块，玩家可自行选择模型和 API 配置。
3. API 开启后，NPC 对话和部分事件可以生成随机内容；API 关闭时，使用默认固定对话和事件文本。

这三个方向必须在现有 MVP 架构上扩展，不应推翻当前阶段、卡牌、奖励、结局和测试体系。

## 4. 目标玩家和体验关键词

目标玩家：

- 喜欢学生成长、养成模拟、轻策略卡牌和事件选择的玩家。
- 喜欢有地图探索感、能和 NPC 互动、能看到角色成长反馈的玩家。
- 希望游戏既能离线稳定游玩，又能在开启 API 后获得更多文本变化的玩家。

体验关键词：

- 有限时间。
- 2D RPG 探索。
- 地点互动。
- NPC 对话。
- 成长取舍。
- 压力管理。
- 卡组塑造。
- 事件变化。
- 阶段 Boss。
- 多结局反馈。
- 可选 AI 内容。

## 5. 核心设计支柱

### 5.1 地图是主要操作入口

后续版本中，玩家不应只在菜单里点活动按钮，而是在 2D RPG 地图上移动角色，通过交互点进入活动。

地图承载：

- 地点入口。
- NPC 对话。
- 活动触发点。
- 商店。
- 休息点。
- 小游戏点。
- 普通挑战点。
- Boss 入口。
- 阶段提示和解锁反馈。

当前 `LocationData` 和 `SceneTransitionController` 可以作为地图系统的数据层和状态层；后续需要补充实际 2D 地图控制、碰撞、交互和 UI 提示。

### 5.2 API 是可选增强，不是核心依赖

API 功能必须是可选项：

- 默认关闭。
- 关闭时使用本地固定对话和固定事件。
- 开启时允许 NPC 和部分事件生成变化内容。
- API 调用失败时必须回退到默认文本，不阻塞核心流程。
- 不允许把关键数值、奖励和结局完全交给远程模型决定。

API 适合生成：

- NPC 问候语。
- 同一事件的不同描述。
- 事件选项的润色文本。
- 地点氛围描述。
- 阶段提示语。
- 结局补充旁白。

API 不应直接决定：

- 玩家属性变化。
- 卡牌数值。
- 奖励发放。
- Boss 胜负。
- 存档结构。
- 付费、账号、隐私和安全相关逻辑。

### 5.3 成长不是单一成绩

成绩仍是重要目标，但不是唯一成功标准。项目需要持续强化：

- 休息不是浪费时间。
- 社交不是纯收益或纯损耗。
- 家庭沟通不是单向惩罚。
- 兴趣和自主性应影响后续地点、事件和结局。
- 高压力路线应给玩家反馈和调整空间。

### 5.4 Boss 是阶段总结

Boss 挑战代表阶段末检验，不只是战斗关卡。它检验：

- 学科属性是否足够。
- 卡组是否合理。
- 道具是否保留。
- 压力是否失控。
- 玩家是否做过长期规划。

Boss 失败不应让游戏直接结束，而应造成代价，并继续推动成长或进入不同结局。

## 6. 核心循环

新版目标循环：

```text
开始新游戏
↓
初始化玩家状态、阶段、地点、卡组、道具、结局和 API 设置
↓
进入当前阶段的 2D RPG 地图
↓
玩家移动角色，靠近 NPC / 地点 / 活动点
↓
按交互键触发对话、活动、小游戏、商店或挑战
↓
如果 API 开启：生成或润色部分对话与事件描述
如果 API 关闭：使用本地默认文本
↓
活动结算，发放奖励，扣除时间 / 精力 / 金钱
↓
剩余时间为 0 或 Boss 条件满足
↓
地图上出现 Boss 入口或阶段 Boss 提示
↓
进入 Boss 卡牌挑战
↓
根据胜负发放奖励或代价
↓
进入下一阶段地图
↓
最终阶段完成
↓
EndingController 选择结局，显示结局文本
```

## 7. 阶段系统策划

当前 MVP 三阶段保持不变：

| 阶段 ID | 名称 | 时间 | Boss | 主题 |
|---|---|---:|---|---|
| `stage_01_adaptation` | 适应新学期 | 6 | 课堂小测 | 熟悉地图、活动、卡牌和基础成长 |
| `stage_02_month_exam` | 月考压力 | 6 | 月考 | 引入压力管理、资源积累和更多地点 |
| `stage_03_final_sprint` | 期末冲刺 | 8 | 期末综合考试 | 综合检验属性、卡组、道具和压力 |

阶段内建议结构：

- 开始阶段时进入主地图。
- 地图中显示当前阶段可用地点和活动。
- 活动消耗阶段时间。
- 时间耗尽后 Boss 入口变为可交互。
- Boss 胜利进入下一阶段。
- 最终阶段完成后进入结局。

阶段地图变化：

- Stage 1 地图较小，只开放家里、学校、公园等基础地点。
- Stage 2 增加商店、图书馆或补习相关地点。
- Stage 3 增加考场、社团、最终复习点等重点区域。

## 8. 2D RPG 地图系统策划

### 8.1 地图目标

地图系统要替代纯按钮式活动列表，让玩家通过移动和交互理解“我在哪里、我能做什么、这次选择会消耗什么”。

基础要求：

- 2D 俯视或斜俯视地图。
- 玩家角色可移动。
- 地图存在碰撞边界。
- 地点或活动点有交互提示。
- 按键或点击触发交互。
- 交互后进入活动、对话、小游戏、商店或挑战。

### 8.2 地图与现有系统关系

建议关系：

```text
2D RPG Map Controller
↓
SceneTransitionController / LocationData
↓
LocationActionData
↓
ActivityController / MiniGameController / ChallengeController / RewardResolver
↓
GameSessionState
```

说明：

- 地图控制玩家移动和交互。
- `SceneTransitionController` 仍负责地点状态。
- `LocationActionData` 定义交互点行为。
- 活动结算仍由现有 Controller 完成。
- 所有状态变化仍写入 `GameSessionState`。

### 8.3 地图交互点

交互点类型：

| 类型 | 对应行为 |
|---|---|
| NPC | 打开对话，可能进入事件 |
| 书桌 / 黑板 / 课堂点 | 训练或普通挑战 |
| 床 / 沙发 / 公园长椅 | 休息 |
| 商店柜台 | 购买道具或卡牌 |
| 书架 / 图书馆桌 | 卡牌升级、学习活动 |
| 社团海报 / 活动室 | 兴趣和社交事件 |
| Boss 标记 | 阶段 Boss 挑战 |

交互提示：

- 靠近时显示活动名。
- 显示时间、精力、金钱成本。
- 显示主要奖励预览。
- 不满足条件时显示原因。

### 8.4 地图状态

地图状态建议继续使用 `LocationState` 扩展：

- `currentLocationId`：当前地点。
- `unlockedLocationIds`：已解锁地点。
- `visitedLocationIds`：访问过的地点。
- `actionUseCount`：动作使用次数。
- `locationFlags`：地点事件标记。

后续可新增但需要谨慎：

- 当前地图坐标。
- 阶段地图 ID。
- NPC 互动次数。
- 当日或本阶段已触发事件。

## 9. API 系统策划

### 9.1 API 设置入口

API 功能放在设置界面中，命名为“API 设置”或“AI 对话设置”。

设置项建议：

| 设置项 | 说明 |
|---|---|
| 启用 API | 总开关，默认关闭 |
| API 地址 | 用户填写 endpoint |
| API Key | 用户填写密钥，本地保存时必须谨慎 |
| 模型名称 | 用户自行选择或输入模型 |
| 最大生成长度 | 限制对话长度 |
| 温度 / 随机性 | 控制文本变化 |
| 超时时间 | 避免卡住游戏 |
| 使用范围 | NPC 对话、事件描述、结局补充等 |

注意：

- 不应把 API Key 写入仓库。
- 不应默认联网。
- 不应在未开启 API 时发起请求。
- API 请求失败必须有默认文本回退。

### 9.2 API 使用范围

适合使用 API：

- NPC 闲聊。
- 地点氛围描述。
- 事件开场描述。
- 事件选项解释。
- 活动完成后的反馈文本。
- Boss 前的心理描写。
- 结局补充旁白。

不适合使用 API：

- 奖励数值。
- 卡牌效果。
- Boss 胜负。
- 金钱、精力、压力等状态变化。
- 资源路径。
- 构建配置。
- 隐私、账号和付费逻辑。

### 9.3 API 回退策略

每个 API 内容都必须有本地默认文本：

```text
请求成功：显示 API 生成文本
请求失败：显示默认文本
API 未开启：显示默认文本
返回内容为空：显示默认文本
返回超时：显示默认文本
```

建议将 API 结果只作为“显示文本”，不要直接改变游戏状态。

### 9.4 API 内容安全边界

项目主题涉及学生成长和压力管理。API 生成文本需要遵守：

- 不生成极端伤害、自伤、恐怖化表达。
- 不把父母、老师或同学写成单纯恶意角色。
- 不输出真实隐私索取内容。
- 不引导玩家输入个人敏感信息。
- 不替代心理、医疗、法律等专业建议。

## 10. NPC 和对话系统策划

### 10.1 NPC 类型

建议 NPC 方向：

| NPC | 主要作用 |
|---|---|
| 班主任 | 阶段目标、学习建议、Boss 提示 |
| 数学老师 | 数学训练、错题本事件 |
| 英语老师 | 单词记忆、小测提示 |
| 同学 | 社交、互助、自习事件 |
| 社团负责人 | 兴趣、社团活动、展示机会 |
| 家长 | 家庭沟通、压力和支持 |
| 商店老板 | 道具购买、资源说明 |

### 10.2 默认对话和 API 对话

每个 NPC 应有默认对话：

- 初次见面。
- 普通问候。
- 阶段相关提示。
- 条件满足提示。
- 条件不满足提示。
- 活动完成反馈。

API 开启后，可以基于以下上下文生成变化文本：

- 当前阶段。
- 当前地点。
- 玩家主要属性。
- 压力水平。
- 最近完成的活动。
- 当前 NPC 性格标签。
- 当前事件主题。

对话结果应只影响显示文本，是否发奖励仍由 `RewardData` 和 `RewardResolver` 决定。

## 11. 活动和事件系统策划

活动类型保持现有枚举：

- `Event`。
- `CardBattle`。
- `MiniGame`。
- `Rest`。
- `Shop`。
- `Training`。

事件系统后续分两层：

1. 固定事件骨架：条件、选项、奖励、风险。
2. 可选 API 文本：描述、对话、氛围、反馈。

事件示例：

```text
事件：错题本建议
固定逻辑：
- 选项 A：认真整理，数学 +3，获得整理错题卡
- 选项 B：简单看看，压力 -1，数学 +1
- 选项 C：请老师讲解，需要自信 >= 45，数学 +4，自信 +1

API 可生成：
- 老师开场对白
- 当前阶段语境描述
- 选项选择后的反馈文本
```

## 12. 卡牌和挑战系统策划

当前挑战以 `targetProgress` 为目标，继续沿用。

战斗流程：

- 初始化战斗卡组。
- 抽起手牌。
- 每回合获得挑战内精力。
- 打出卡牌消耗精力。
- 卡牌效果改变挑战进度、精力、压力、抽牌等。
- 达成目标进度胜利。
- 回合耗尽失败。

卡牌显示字段：

- `targetType`：用于 UI 说明目标。
- `damage`：用于显示挑战进度类数值。
- `block`：用于未来防御或压力抵抗。
- `drawCount`：用于 UI 显示抽牌。
- `discardCount`：用于未来弃牌效果。
- `exhaust`、`ethereal`、`retain`：用于未来卡牌关键词。

短期规则：

- `effects` 是实际逻辑权威。
- `damage`、`block`、`drawCount` 是 UI 展示辅助。
- 生成器和绑定工具负责保持展示字段与效果一致。

## 13. 道具系统策划

道具用于连接地图活动和 Boss 表现。

道具类型：

| 类型 | 说明 |
|---|---|
| `Consumable` | 主动使用，使用后移除 |
| `Passive` | 持有后在指定时机自动触发 |
| `StageOnly` | 只在当前阶段有效，进入下一阶段清理 |

地图中的道具来源：

- 商店购买。
- NPC 赠送。
- 事件奖励。
- 小游戏成功奖励。
- 普通挑战奖励。

API 不决定道具实际效果，只能生成获得道具时的描述文本。

## 14. 小游戏系统策划

当前小游戏使用成功率公式：

```text
成功率 = 50 + 主属性 / 2 + (心情 - 50) / 4 - 压力 / 5 - 难度 * 5
最终夹取到 15 到 95
```

后续地图化后，小游戏入口应是地图交互点，例如：

- 单词记忆机。
- 公式配对黑板。
- 图书馆阅读题。
- 专注计时桌。
- 课堂速记点。
- 整理书包点。

小游戏仍先做结算式，不急于做复杂实时操作。

## 15. 结局系统策划

当前 `EndingController` 已支持：

- 最低通关阶段数。
- 最低击败 Boss 数。
- 最低三科平均。
- 最高压力。
- 自定义属性条件。
- 结局优先级。

结局方向：

- 稳定升学。
- 兴趣成长。
- 家庭支持。
- 自主选择。
- 压力调整。
- 普通但完整的成长。

API 可选增强：

- 在结局基本文本后生成一段个性化旁白。
- 旁白只能根据最终状态生成描述，不能改变结局类型。
- API 关闭或失败时使用固定结局描述。

## 16. UI 和交互策划

### 16.1 主界面层级

新版 UI 应围绕 2D RPG 地图组织：

```text
顶部状态栏：阶段、时间、精力、压力、金钱
中央区域：2D RPG 地图
地图角色：玩家当前位置
交互提示：当前可互动对象
右侧或底部：活动说明、奖励预览、地点说明
底部消息栏：系统反馈和 NPC 简短对白
设置入口：包含音量、显示、API 设置
```

### 16.2 设置界面 API 板块

设置界面新增 API 板块：

```text
API 设置
[ ] 启用 API 对话
API 地址：____
API Key：____
模型名称：____
生成长度：____
随机性：____
测试连接
保存
```

交互规则：

- API 默认关闭。
- 未开启时隐藏或禁用详细配置。
- 保存后只影响后续对话，不重置当前游戏。
- 测试连接失败时显示失败原因，但不影响游戏继续。

### 16.3 战斗 UI

已有战斗 UI 应继续优化：

- 手牌扇形布局。
- 悬停放大。
- Tooltip 说明。
- 拖拽到 Boss 或目标区域打出。
- 不可用卡降低透明度并说明原因。
- Boss 意图和目标进度清晰显示。

## 17. 美术资源规划

当前美术目录：

```text
Assets/Art
```

主要目录：

- `Assets/Art/CardReferences/FanworkPixel/Generated/PNG`：卡牌 PNG。
- `Assets/Art/CardReferences/SVG`：卡牌 SVG。
- `Assets/Art/Pixel/Backgrounds`：背景图。
- `Assets/Art/Pixel/UI`：UI 图片。
- `Assets/Art/Pixel/Icons/Items`：道具图标。
- `Assets/Art/Pixel/Icons/Stats`：属性图标。
- `Assets/Art/Pixel/FX`：特效图。
- `Assets/Art/CharacterReferences/Portraits`：角色头像。
- `Assets/Art/CharacterReferences/FanworkPixel/Generated`：像素角色。

2D RPG 地图新增美术需求：

- 玩家行走角色。
- NPC 角色。
- 地图地砖。
- 家、学校、商店、公园、图书馆、社团、考场地图背景。
- 可交互物件图标。
- 交互提示 UI。
- 地点门牌和阶段标识。

资源规范：

- 像素图使用 Point Filter。
- 地图瓦片尺寸统一。
- 角色和 NPC 方向帧保持一致。
- 可交互物件需要明确高亮状态。

## 18. 数据驱动策略

当前正式方向应以 ScriptableObject 为主：

```text
Assets/Resources/StudentGrowth/MVP/Cards
Assets/Resources/StudentGrowth/MVP/Items
Assets/Resources/StudentGrowth/MVP/MiniGames
Assets/Resources/StudentGrowth/MVP/Events
Assets/Resources/StudentGrowth/MVP/Challenges
Assets/Resources/StudentGrowth/MVP/Activities
Assets/Resources/StudentGrowth/MVP/Stages
Assets/Resources/StudentGrowth/MVP/Endings
Assets/Resources/StudentGrowth/MvpBootstrapperData.asset
```

需要新增或补齐的数据资产：

- NPC 数据。
- NPC 默认对话。
- API 对话 Prompt 模板。
- 2D RPG 地图配置。
- 地图交互点配置。
- 地点动作资产。
- API 设置本地配置。

## 19. 版本规划

### V0.1 当前 MVP 基础

目标：

- 三阶段流程可跑通。
- 活动、Boss、结局完整闭环。
- 保持已有 EditMode 测试通过。
- 使用运行时 UI 验证玩法。

### V0.2 数据资产化

目标：

- 使用 `MvpSampleAssetGenerator` 生成完整 ScriptableObject 数据。
- `GameBootstrapperData` 成为正式入口。
- 补齐地点和地点动作资产。
- 减少 UI 对代码样例数据的依赖。

### V0.3 2D RPG 地图原型

目标：

- 建立玩家角色移动。
- 建立地图碰撞。
- 建立交互点。
- 将地点活动迁移到地图交互。
- 保留原按钮式界面作为调试或回退入口。

验收：

- 玩家能在地图上移动。
- 玩家靠近活动点能看到交互提示。
- 交互后能触发现有活动结算。
- 阶段时间和奖励仍正确变化。

### V0.4 NPC 和固定对话

目标：

- 增加 NPC 数据和默认对话。
- 地图上放置 NPC。
- 与 NPC 对话可触发事件或提示。
- 所有对话离线可用。

验收：

- 不开启 API 时，NPC 和事件仍能完整运作。
- NPC 对话能根据阶段和条件显示不同默认文本。

### V0.5 API 设置和 AI 对话

目标：

- 设置界面加入 API 板块。
- 支持用户配置模型、API 地址和 API Key。
- API 开启后 NPC 对话可生成变化内容。
- 部分事件描述可随机生成。
- API 失败回退默认文本。

验收：

- API 默认关闭。
- 关闭时无网络请求。
- 开启后能测试连接。
- 请求失败不影响活动、奖励、战斗和结局。

### V0.6 战斗和地图体验强化

目标：

- 优化拖卡目标、高亮、Tooltip。
- 增加 Boss 意图表现。
- 增加地图活动反馈动画。
- 增加地点解锁和阶段变化表现。

## 20. 测试和验收标准

优先运行已有 EditMode 测试：

```powershell
$unity = "C:\Program Files\Unity\Hub\Editor\6000.4.6f1\Editor\Unity.exe"
& $unity -batchmode -quit -projectPath "E:\unity\ML-project" -runTests -testPlatform EditMode -testResults "$env:TEMP\ML-project-EditModeResults.xml" -logFile "$env:TEMP\ML-project-EditModeTests.log"
```

手动验收：

1. 打开 `Assets/Scenes/SampleScene.unity`。
2. 进入 Play Mode。
3. 确认 `StudentGrowth MVP Runtime UI` 自动创建。
4. 完成一次训练、休息、事件、小游戏活动。
5. 确认奖励、时间、精力、压力、道具和卡组变化。
6. 进入 Boss 挑战。
7. 用点击或拖拽打出卡牌。
8. 完成 Boss 并进入下一阶段。
9. 完成三阶段后进入结局。

后续 2D RPG 地图验收：

- 玩家能移动。
- 碰撞有效。
- 交互提示清楚。
- 交互点能触发现有活动。
- 地图交互不会绕过 `RewardResolver` 和 `StageController`。
- 活动消耗和奖励与按钮式入口一致。

后续 API 验收：

- API 默认关闭。
- 设置界面可配置 API。
- 关闭时 NPC 使用默认对话。
- 开启时 NPC 或事件可生成文本。
- 请求失败时回退默认文本。
- API 不直接修改数值状态。
- 不保存或提交任何真实密钥。

## 21. 风险和边界

### 21.1 API 引入风险

API 涉及联网、密钥、本地配置和内容安全。根据项目限制，新增联网或外部接口逻辑必须由用户明确确认后再实现。策划层面可以设计，但实现时不能默认调用远程接口。

需要注意：

- 不写死 API Key。
- 不把密钥提交到仓库。
- 不默认联网。
- 不让 API 控制核心数值。
- 不让 API 失败影响主流程。

### 21.2 UI 类过大

`StudentGrowthMvpRuntimeUi` 目前承担了大量职责。后续做 2D RPG 地图和 API 对话前，应逐步拆分：

- MapView。
- DialogueView。
- SettingsView。
- ApiSettingsView。
- StageView。
- BattleView。
- EndingView。
- RuntimeSpriteLoader。

### 21.3 地图系统不能破坏现有流程

地图只是新的操作入口，不应绕开：

- `StageController`。
- `ActivityController`。
- `RewardResolver`。
- `GameSessionState`。
- `EndingController`。

### 21.4 AI 内容不能替代策划数据

API 生成内容只能增强表现。核心策划数据仍应是本地可测试、可回归、可平衡的 ScriptableObject。

## 22. 下一步开发建议

优先级建议：

1. 固定现有 EditMode 测试通过。
2. 在不改玩法的前提下，拆分 `StudentGrowthMvpRuntimeUi` 中地图、对话、设置相关代码。
3. 设计 `NpcData` 和默认对话数据结构。
4. 设计 `ApiSettingsData`，但暂不实现联网请求，先完成设置 UI 和本地默认逻辑。
5. 建立 2D RPG 地图移动和交互点原型。
6. 将一个已有活动接入地图交互点。
7. 将一个 NPC 默认对话接入地图。
8. 用户确认后，再实现 API 请求层和回退策略。
9. 为地图交互、API 开关、默认对话回退添加测试。

## 23. 最终愿景

完成后，玩家应能体验到：

```text
我在学校、家里、公园、商店、图书馆等地图中移动
我和老师、同学、家长、商店 NPC 对话
我通过交互选择学习、休息、事件、小游戏和挑战
我能选择是否开启 API，让 NPC 和事件有更多变化
我不开启 API 时，游戏仍然完整稳定
我每个阶段都要管理时间、精力、压力、金钱、卡组和道具
我最终得到一个反映成长路径的结局
```

项目的长期方向是：用 2D RPG 地图提升沉浸感，用卡牌挑战提供策略压力，用阶段和结局组织成长叙事，用可选 API 增强文本变化，但始终保证核心游戏逻辑本地可控、可测试、可离线运行。

