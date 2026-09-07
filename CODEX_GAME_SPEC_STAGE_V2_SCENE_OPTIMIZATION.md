# Unity 学生成长模拟卡牌游戏：V2 多场景运营与体验优化规格书

> 建议文件名：`CODEX_GAME_SPEC_STAGE_V2_SCENE_OPTIMIZATION.md`  
> 适用前提：MVP 阶段制核心循环、卡牌挑战、小游戏、事件、道具、奖励、Boss、结局、基础 UI 已经实现。  
> V2 目标：在不推翻现有核心结构的前提下，把“阶段主界面 + 活动列表”扩展为“地图场景 + 场景操作 + 活动入口”的完整体验，重点增加商店、学校、公园、家庭、图书馆等场景切换与交互操作。

---

## 0. V2 版本定位

V2 不是重写游戏，而是在已完成 MVP 的基础上做体验、结构和内容的扩展。

MVP 的核心循环保持不变：

```text
Stage -> Location / Scene -> Action / Activity -> Reward -> Boss -> Next Stage -> Ending
```

V2 要解决的问题：

1. 活动列表过于抽象，缺少“玩家在城市 / 校园中移动”的空间感。
2. 商店、学校、公园等地点目前只是活动类型，缺少专属界面、操作和规则。
3. 道具、金钱、卡牌构筑和压力管理之间的联系还可以更强。
4. 阶段内选择应从“点一个活动”升级为“选择地点，再选择地点内操作”。
5. UI 需要更清晰地表达：当前位置、可执行操作、时间消耗、奖励预览和风险提示。

V2 完成后，玩家应该能在一个阶段内进行如下体验：

```text
查看当前阶段目标
↓
打开地图
↓
选择去学校 / 商店 / 公园 / 家里 / 图书馆 / 社团
↓
进入对应场景界面
↓
执行该场景下的操作，例如上课、买道具、散步、和父母沟通、整理卡组
↓
获得奖励或触发事件
↓
消耗时间、精力或金钱
↓
返回地图或继续在当前场景操作
↓
阶段时间耗尽后进入 Boss
```

---

## 1. V2 设计原则

### 1.1 不破坏 MVP 数据驱动结构

现有的以下系统继续保留：

- `StageData`
- `ActivityData`
- `ChallengeData`
- `MiniGameData`
- `GameEventData`
- `ItemData`
- `RewardData`
- `EndingData`
- `GameSessionState`

V2 新增地点和场景系统，但不要把现有活动系统废掉。推荐做法：

```text
LocationData 包含多个 LocationActionData
LocationActionData 可以引用原有 ActivityData
ActivityData 继续负责具体玩法入口
```

这样既能保留 MVP，又能让场景系统扩展。

### 1.2 场景切换优先做 UI 面板切换

V2 阶段建议不要立刻创建多个大型 Unity Scene。

推荐实现方式：

```text
MainScene.unity
  ├── MapScreenPanel
  ├── HomeScenePanel
  ├── SchoolScenePanel
  ├── ShopScenePanel
  ├── ParkScenePanel
  ├── LibraryScenePanel
  ├── ClubScenePanel
  ├── BattlePanel
  ├── EventPopupPanel
  ├── RewardPopupPanel
  └── EndingPanel
```

优点：

- 不容易丢失 `GameSessionState`。
- 切换速度快。
- 更适合原型迭代。
- UI、状态、控制器更容易调试。

后续商业化版本再考虑使用 `SceneManager.LoadSceneAsync` 或 additive scene。

### 1.3 地点选择不应变成跑路负担

地点切换应提供策略感，但不要让玩家频繁浪费时间。

推荐规则：

| 切换类型 | 是否消耗阶段时间 | 说明 |
|---|---:|---|
| 地图打开 / 关闭 | 否 | 纯 UI 操作 |
| 进入地点 | 默认不消耗 | 避免无意义消耗 |
| 执行地点操作 | 消耗 | 由 `LocationActionData.timeCost` 决定 |
| 特殊远距离地点 | 可消耗 | 例如郊外活动、竞赛会场 |
| 快速返回地图 | 否 | 保证操作流畅 |

阶段时间主要由“操作”消耗，而不是由“切换界面”消耗。

---

## 2. V2 总流程

### 2.1 阶段内流程

```text
进入阶段
↓
显示 Stage HUD：阶段名、剩余时间、压力、精力、金钱、Boss 目标
↓
显示地图 Map Screen
↓
玩家选择 Location
↓
SceneTransitionController 切换到对应 Location View
↓
Location View 显示：地点说明、NPC、可执行操作、消耗、奖励预览、风险提示
↓
玩家选择 LocationAction
↓
根据 Action 类型进入：
- CardBattle
- MiniGame
- Event
- Rest
- Shop
- Training
- DeckManagement
- ItemUse
- Dialogue
↓
结算 ActionResult
↓
RewardResolver 发放奖励
↓
StageController 扣除时间
↓
刷新当前位置 UI
↓
如果剩余时间为 0，进入 Boss
```

### 2.2 阶段结束流程

```text
阶段时间耗尽
↓
弹出 Boss Preview
↓
玩家确认进入 Boss
↓
Boss 挑战
↓
胜利 / 失败结算
↓
清理 StageOnly 道具和临时地点状态
↓
进入下一阶段或结局
```

### 2.3 操作结果分类

每个场景操作都应返回统一结果：

```csharp
public class LocationActionResult
{
    public bool success;
    public int timeCost;
    public int energyCost;
    public int moneyCost;
    public List<RewardData> rewards;
    public ActivityResult linkedActivityResult;
    public string resultTitle;
    public string resultDescription;
}
```

---

## 3. 新增地点系统 Location System

### 3.1 LocationType

```csharp
public enum LocationType
{
    Home,
    School,
    Shop,
    Park,
    Library,
    Club,
    ExamHall,
    SpecialEvent
}
```

### 3.2 LocationData

新增 `LocationData : ScriptableObject`。

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Location Data")]
public class LocationData : ScriptableObject
{
    public string locationId;
    public string displayName;
    public string description;
    public LocationType locationType;

    public Sprite icon;
    public Sprite background;

    public bool unlockedByDefault;
    public List<EventConditionData> unlockConditions;
    public List<EventConditionData> appearConditions;

    public List<LocationActionData> actions;

    public int enterTimeCost;
    public int enterEnergyCost;
    public int enterMoneyCost;

    public string recommendedStageId;
    public bool hideWhenNoAvailableActions;
}
```

### 3.3 LocationActionType

```csharp
public enum LocationActionType
{
    StartActivity,
    OpenShop,
    StartDialogue,
    ManageDeck,
    UseItem,
    Rest,
    Training,
    OpenSubMenu,
    TriggerEvent,
    StartMiniGame,
    StartCardBattle
}
```

### 3.4 LocationActionData

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Location Action Data")]
public class LocationActionData : ScriptableObject
{
    public string actionId;
    public string displayName;
    public string description;
    public LocationActionType actionType;

    public int timeCost;
    public int energyCost;
    public int moneyCost;

    public ActivityData linkedActivity;
    public ChallengeData linkedChallenge;
    public MiniGameData linkedMiniGame;
    public GameEventData linkedEvent;
    public ShopData linkedShop;

    public List<RewardData> fixedRewards;
    public List<RewardData> possibleRewards;
    public List<EventConditionData> appearConditions;
    public List<EventConditionData> executeConditions;

    public bool showRewardPreview;
    public bool requireConfirm;
    public string confirmMessage;
}
```

### 3.5 LocationState

`LocationState` 记录地点运行时状态。

```csharp
[System.Serializable]
public class LocationState
{
    public string currentLocationId;
    public List<string> unlockedLocationIds = new List<string>();
    public List<string> visitedLocationIds = new List<string>();
    public Dictionary<string, int> actionUseCount = new Dictionary<string, int>();
    public Dictionary<string, bool> locationFlags = new Dictionary<string, bool>();
}
```

### 3.6 GameSessionState 扩展

```csharp
[System.Serializable]
public class GameSessionState
{
    public PlayerState playerState;
    public DeckState deckState;
    public ItemInventory itemInventory;
    public StageState stageState;
    public LocationState locationState;
    public ShopRuntimeState shopRuntimeState;

    public List<string> clearedStageIds = new List<string>();
    public List<string> defeatedBossIds = new List<string>();
    public List<string> unlockedActivityIds = new List<string>();
    public List<string> unlockedLocationIds = new List<string>();
}
```

---

## 4. 场景切换系统 Scene / Location Transition

### 4.1 SceneTransitionMode

```csharp
public enum SceneTransitionMode
{
    PanelSwitch,
    UnitySceneLoad,
    AdditiveSceneLoad
}
```

V2 默认使用：

```text
PanelSwitch
```

### 4.2 SceneTransitionController 职责

```csharp
public class SceneTransitionController : MonoBehaviour
{
    public void ShowMap();
    public void GoToLocation(LocationData locationData);
    public void ReturnToPreviousLocation();
    public void RefreshCurrentLocation();
    public bool CanEnterLocation(LocationData locationData);
    public void RegisterLocationView(LocationType type, LocationView view);
}
```

职责说明：

- 控制地图和地点界面的显示 / 隐藏。
- 记录当前位置。
- 检查地点进入条件。
- 通知 `LocationController` 刷新可用操作。
- 不直接发奖励，不直接改属性。

### 4.3 切换表现

V2 基础切换效果：

| 效果 | MVP+ 要求 |
|---|---|
| 淡入淡出 | 可选，0.2 - 0.4 秒 |
| 地点标题条 | 必须 |
| 当前地点图标 | 必须 |
| 返回地图按钮 | 必须 |
| 操作锁定提示 | 必须 |
| 切换中禁止重复点击 | 必须 |

### 4.4 地点锁定提示

如果地点不可进入，UI 不应只隐藏。推荐显示灰色按钮和原因。

示例：

```text
图书馆
暂未开放：需要进入 Stage 2 后开放。
```

```text
社团活动室
暂未开放：兴趣 >= 45 后开放。
```

---

## 5. 地图界面 Map Screen

### 5.1 地图界面必须显示

```text
顶部：当前阶段、剩余时间、阶段目标、Boss 状态
左侧：玩家核心状态，精力 / 压力 / 心情 / 金钱
中间：地点按钮网格或简易地图
右侧：今日建议、任务、关键事件提示
底部：卡组、道具、日志、设置、结束阶段按钮
```

### 5.2 地点按钮信息

每个地点按钮至少显示：

- 地点名。
- 地点图标。
- 是否可进入。
- 推荐 / 高收益 / 有新事件提示。
- 当前地点内可执行操作数量。

示例：

```text
学校
可用操作：4
推荐：今天有课堂挑战
```

```text
商店
可用操作：2
新货：错题本、功能饮料
```

### 5.3 地图提示系统

新增 `MapHintData`。

```csharp
[System.Serializable]
public class MapHintData
{
    public string hintId;
    public string title;
    public string description;
    public LocationData targetLocation;
    public List<EventConditionData> appearConditions;
    public int priority;
}
```

用途：引导玩家知道当前阶段该做什么，但不强迫唯一路线。

示例提示：

| 条件 | 提示 |
|---|---|
| 压力 >= 70 | 公园或家里可以降低压力 |
| 金钱 >= 80 | 商店有新道具可购买 |
| 数学 < 当前 Boss 推荐值 | 学校的刷题训练可以提高数学 |
| 兴趣 >= 50 | 社团有新的展示机会 |

---

## 6. 家里 Home Scene

### 6.1 设计目标

家里是恢复、家庭沟通、卡组整理和阶段计划的主要地点。

不要把家里设计成只有“休息”。家里应承担：

- 恢复精力。
- 降低压力。
- 与父母沟通。
- 调整学习计划。
- 管理卡组。
- 使用部分阶段道具。

### 6.2 Home 可执行操作

| 操作 ID | 名称 | 类型 | 消耗 | 结果 |
|---|---|---|---|---|
| `home_rest_short` | 小睡一会 | Rest | 时间 1 | 精力 +18，压力 -3 |
| `home_family_talk` | 和父母沟通 | Event | 时间 1 | 亲子关系 / 压力 / 自主性变化 |
| `home_plan_study` | 制定学习计划 | Training | 时间 1 | 自主性 +3，下次学习挑战开局进度 +3 |
| `home_deck_manage` | 整理卡组 | ManageDeck | 时间 0 | 打开卡组管理界面 |
| `home_use_item` | 使用道具 | UseItem | 时间 0 | 打开可在阶段界面使用的道具 |
| `home_early_sleep` | 早点睡觉 | Rest | 时间 1 | 精力恢复至上限，心情 +2，压力 -6 |

### 6.3 家里界面 HomeView

```text
背景：房间 / 书桌
顶部：地点名、当前阶段、剩余时间
左侧：玩家状态
中间：操作按钮
右侧：父母关系、当前家庭事件、可用阶段道具
底部：返回地图、卡组、道具、日志
```

### 6.4 家庭沟通事件池

家里事件建议从以下类型中抽取：

| 事件类型 | 作用 |
|---|---|
| 成绩沟通 | 学科、压力、亲子关系取舍 |
| 休息沟通 | 压力、精力、亲子关系取舍 |
| 兴趣沟通 | 兴趣、自主性、亲子关系取舍 |
| 消费沟通 | 金钱、道具、亲子关系取舍 |
| 计划沟通 | 自主性、自信、学习收益取舍 |

---

## 7. 学校 School Scene

### 7.1 设计目标

学校是学习、普通挑战、老师事件、同学关系和考试准备的核心地点。

学校不应只有“刷题”。应同时提供：

- 学科提升。
- 卡牌挑战。
- 老师请教。
- 同学互动。
- 社团入口。
- 临时事件。

### 7.2 School 可执行操作

| 操作 ID | 名称 | 类型 | 消耗 | 结果 |
|---|---|---|---|---|
| `school_class_listen` | 课堂听讲 | CardBattle | 时间 1，精力 5 | 挑战成功后学科属性 / 卡牌奖励 |
| `school_teacher_help` | 请教老师 | Event | 时间 1 | 学科 + 自信，可能压力 +1 |
| `school_quiz_practice` | 课间小测 | MiniGame | 时间 1 | 成功获得学科属性或普通卡 |
| `school_group_study` | 小组学习 | CardBattle / Event | 时间 1 | 社交 + 学习收益 |
| `school_notice_board` | 查看公告栏 | OpenSubMenu | 时间 0 | 显示特殊活动或竞赛入口 |
| `school_club_gate` | 去社团活动室 | OpenSubMenu | 时间 0 | 切换到 Club Scene |

### 7.3 学校子区域

V2 可以先用同一个 `SchoolView` 表现多个子区域，不需要创建真实 3D 场景。

| 子区域 | 功能 |
|---|---|
| 教室 | 上课、课堂挑战、同学互动 |
| 办公室 | 请教老师、获取学习建议 |
| 公告栏 | 查看特殊事件、阶段目标提示 |
| 操场入口 | 跳转公园或运动事件 |
| 社团入口 | 跳转社团活动室 |

### 7.4 老师建议系统

新增 `TeacherAdviceData`。

```csharp
[System.Serializable]
public class TeacherAdviceData
{
    public string adviceId;
    public string title;
    public string description;
    public PlayerStatKey relatedStat;
    public int recommendedValue;
    public List<EventConditionData> appearConditions;
}
```

示例：

```text
数学老师建议：
你最近数学基础还可以，但考试时容易紧张。
建议：去家里制定学习计划，或在商店购买错题本。
```

### 7.5 学校 UI 特殊提示

学校界面应显示当前 Boss 推荐准备度：

```text
课堂小测准备度：一般
推荐：语文 / 数学 / 英语任意一科达到 45，压力低于 60。
```

准备度只做提示，不要强制拦截玩家。

---

## 8. 商店 Shop Scene

### 8.1 设计目标

商店负责把金钱、道具、卡牌构筑和阶段策略连接起来。

商店不做内购，不做真实付费。所有购买只使用游戏内 `money`。

商店应提供：

- 购买消耗道具。
- 购买被动道具。
- 购买少量卡牌。
- 购买阶段限定道具。
- 刷新商品。
- 出售或回收部分道具，V2 可选。

### 8.2 ShopType

```csharp
public enum ShopType
{
    ConvenienceStore,
    BookStore,
    SchoolStore,
    SpecialVendor
}
```

### 8.3 ShopData

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Shop Data")]
public class ShopData : ScriptableObject
{
    public string shopId;
    public string displayName;
    public string description;
    public ShopType shopType;

    public List<ShopItemEntryData> defaultStock;
    public List<ShopItemEntryData> randomStockPool;

    public int refreshCost;
    public int maxRandomStockCount;
    public bool refreshEachStage;
    public bool refreshAfterPurchase;

    public List<EventConditionData> appearConditions;
}
```

### 8.4 ShopItemEntryData

```csharp
[System.Serializable]
public class ShopItemEntryData
{
    public string entryId;
    public ItemData item;
    public CardData card;

    public int basePrice;
    public int discountPercent;
    public int stockCount;

    public bool soldOutAfterPurchase;
    public List<EventConditionData> appearConditions;
}
```

### 8.5 ShopRuntimeState

```csharp
[System.Serializable]
public class ShopRuntimeState
{
    public Dictionary<string, List<ShopStockItemState>> shopStocks = new Dictionary<string, List<ShopStockItemState>>();
    public Dictionary<string, int> refreshCounts = new Dictionary<string, int>();
}

[System.Serializable]
public class ShopStockItemState
{
    public string entryId;
    public int currentPrice;
    public int remainingStock;
    public bool purchased;
}
```

### 8.6 商店购买流程

```text
进入商店
↓
ShopController 根据 ShopData 和 ShopRuntimeState 生成库存
↓
ShopView 显示商品列表
↓
玩家点击商品
↓
显示商品详情：价格、效果、库存、是否可购买
↓
确认购买
↓
检查 money 是否足够
↓
扣除 money
↓
AddItem 或 AddCard
↓
刷新库存状态
↓
显示购买结果
```

### 8.7 ShopController 职责

```csharp
public class ShopController : MonoBehaviour
{
    public void OpenShop(ShopData shopData);
    public IReadOnlyList<ShopStockItemState> GetCurrentStock(string shopId);
    public bool CanPurchase(ShopData shopData, ShopStockItemState stockItem);
    public PurchaseResult Purchase(ShopData shopData, ShopStockItemState stockItem);
    public bool CanRefresh(ShopData shopData);
    public void RefreshStock(ShopData shopData);
}
```

### 8.8 PurchaseResult

```csharp
public class PurchaseResult
{
    public bool success;
    public string message;
    public int moneySpent;
    public ItemData acquiredItem;
    public CardData acquiredCard;
}
```

### 8.9 商品类型建议

| 商品类型 | 示例 | 设计作用 |
|---|---|---|
| 精力恢复 | 小零食、功能饮料 | 支持高强度学习路线 |
| 压力管理 | 随身听、舒缓茶 | 支持稳健路线 |
| 学习增益 | 错题本、荧光笔 | 支持 Boss 战和卡牌挑战 |
| 兴趣增益 | 社团徽章、画册 | 支持兴趣结局 |
| 家庭道具 | 父母鼓励纸条 | 支持亲子关系路线 |
| 卡牌商品 | 普通学习卡、休息卡 | 补充卡组短板 |

### 8.10 价格建议

| 稀有度 / 类型 | 价格范围 |
|---|---:|
| 普通消耗道具 | 15 - 35 |
| 强力消耗道具 | 40 - 70 |
| 被动道具 | 80 - 140 |
| 阶段限定道具 | 40 - 90 |
| 普通卡牌 | 30 - 60 |
| 非普通卡牌 | 80 - 150 |
| 稀有卡牌 | 160 - 260 |

### 8.11 商店平衡规则

1. 商店不能保证每次都买到最优解。
2. 商店刷新要有成本。
3. 被动道具强度高，但价格也高。
4. 消耗道具应解决短期问题，例如 Boss 前补状态。
5. 卡牌商品不要过多，否则普通挑战奖励会失去价值。

---

## 9. 公园 Park Scene

### 9.1 设计目标

公园是压力管理、心情恢复、兴趣发展和社交事件的主要地点。

公园的核心不是“纯休息”，而是让玩家获得非学习路线的长期收益。

### 9.2 Park 可执行操作

| 操作 ID | 名称 | 类型 | 消耗 | 结果 |
|---|---|---|---|---|
| `park_walk` | 散步放松 | Rest | 时间 1 | 压力 -8，心情 +3 |
| `park_meet_friend` | 偶遇同学 | Event | 时间 1 | 社交 / 心情 / 学习取舍 |
| `park_hobby_practice` | 兴趣练习 | MiniGame | 时间 1，精力 3 | 兴趣 +，可能获得兴趣卡 |
| `park_read_under_tree` | 树下阅读 | Training | 时间 1 | 语文 +3，压力 -2 |
| `park_family_walk` | 和家人散步 | Event | 时间 1 | 亲子关系 +，压力 - |
| `park_observe_life` | 观察生活 | Event | 时间 1 | 自主性 / 兴趣 / 语文路线 |

### 9.3 公园随机事件池

公园可以提供轻量随机事件，但必须可控。

```csharp
[CreateAssetMenu(menuName = "Game Data/Random Event Pool")]
public class RandomEventPoolData : ScriptableObject
{
    public string poolId;
    public List<GameEventData> events;
    public int maxTriggerPerStage;
}
```

建议事件：

| 事件 | 核心选择 |
|---|---|
| 偶遇同学讨论考试 | 学习 / 社交 / 放松取舍 |
| 看到社团表演 | 兴趣 / 时间 / 自信取舍 |
| 家庭散步 | 亲子关系 / 自主性取舍 |
| 安静阅读 | 语文 / 心情 / 压力取舍 |
| 帮路人找东西 | 社交 / 心情 / 时间取舍 |

### 9.4 公园平衡规则

1. 公园应显著降低压力，但不应直接提供最高学习收益。
2. 公园应支持兴趣、社交、家庭类结局。
3. 高压力玩家去公园应有明显收益。
4. 如果玩家完全忽略公园，压力路线应更难控制。

---

## 10. 图书馆 Library Scene

### 10.1 设计目标

图书馆是稳定学习、卡牌升级、删牌和安静复盘的地点。

图书馆区别于学校：

| 地点 | 重点 |
|---|---|
| 学校 | 课堂挑战、老师、同学、考试 |
| 图书馆 | 自主学习、卡牌管理、低压力提升 |

### 10.2 Library 可执行操作

| 操作 ID | 名称 | 类型 | 消耗 | 结果 |
|---|---|---|---|---|
| `library_focus_study` | 安静自习 | CardBattle | 时间 1 | 学习收益，压力较低 |
| `library_upgrade_card` | 精读笔记 | ManageDeck | 时间 1，金钱 20 | 升级一张可升级卡 |
| `library_remove_card` | 整理旧笔记 | ManageDeck | 时间 1，金钱 30 | 删除一张普通卡或状态类卡 |
| `library_find_book` | 查找资料 | MiniGame | 时间 1 | 获得卡牌候选或语文 + |
| `library_reflect` | 复盘计划 | Event | 时间 1 | 自主性、自信、压力调整 |

### 10.3 卡组管理规则

V2 建议正式增加卡组管理界面：

- 查看当前 `masterDeck`。
- 查看卡牌类型比例。
- 升级卡牌。
- 删除卡牌。
- 标记关键卡。
- 查看卡牌来源。

卡组管理不应无条件强力。推荐消耗：

| 操作 | 消耗 |
|---|---:|
| 查看卡组 | 0 |
| 升级卡牌 | 时间 1 + 金钱 20 - 50 |
| 删除卡牌 | 时间 1 + 金钱 30 - 80 |
| 复制卡牌 | 暂不做，后续版本可扩展 |

---

## 11. 社团 Club Scene

### 11.1 设计目标

社团用于强化兴趣路线、社交路线和自信成长。

### 11.2 Club 可执行操作

| 操作 ID | 名称 | 类型 | 消耗 | 结果 |
|---|---|---|---|---|
| `club_join_activity` | 参加社团活动 | MiniGame | 时间 1，精力 4 | 兴趣 +，社交 + |
| `club_prepare_show` | 准备展示 | CardBattle | 时间 1 | 兴趣卡、信心奖励 |
| `club_talk_member` | 和社员交流 | Event | 时间 1 | 社交、自信、压力取舍 |
| `club_finish_project` | 完成社团作品 | MiniGame | 时间 1 | 兴趣 +，可能获得稀有兴趣卡 |

### 11.3 社团开放条件

推荐：

```text
Stage 1 后半段开放，或兴趣 >= 45 后开放。
```

也可以通过学校事件开放：

```text
同学邀请社团 -> 选择“参加” -> 解锁社团活动室
```

---

## 12. 操作系统 Interaction System

### 12.1 统一操作入口

所有可点击操作都应由 `LocationActionButton` 表示。

```csharp
public class LocationActionButton : MonoBehaviour
{
    public void Bind(LocationActionData data, bool canExecute, string lockedReason);
    public void OnClick();
}
```

按钮显示内容：

```text
操作名称
简短说明
消耗：时间 / 精力 / 金钱
预计奖励
锁定原因或风险提示
```

### 12.2 操作确认规则

以下情况必须二次确认：

- 消耗阶段时间。
- 消耗大量金钱。
- 会显著增加压力。
- 会进入 Boss。
- 会删除卡牌。
- 会出售或消耗道具。

确认弹窗格式：

```text
是否执行：刷题训练？
消耗：时间 1，精力 5
可能结果：数学提升，压力可能增加
[确认] [取消]
```

### 12.3 快捷操作

| 快捷键 | 功能 |
|---|---|
| `M` | 打开 / 返回地图 |
| `I` | 打开道具栏 |
| `D` | 打开卡组 |
| `Esc` | 返回上一层 / 关闭弹窗 |
| `Enter` | 确认当前弹窗 |
| `Space` | 战斗中结束回合，需避免误触确认 |

快捷键应可在设置中关闭。

### 12.4 操作日志

新增 `GameLogEntry`。

```csharp
[System.Serializable]
public class GameLogEntry
{
    public string title;
    public string description;
    public string stageId;
    public string locationId;
    public int remainingTimeAfter;
}
```

日志示例：

```text
你在学校完成了课堂听讲，数学 +4，压力 +2，剩余时间 5。
```

日志用于帮助玩家理解每次操作的结果。

---

## 13. UI 结构优化

### 13.1 新增 UI 脚本

```text
UI/
  Map/
    MapScreenView.cs
    LocationButtonView.cs
    MapHintView.cs

  Locations/
    LocationView.cs
    HomeView.cs
    SchoolView.cs
    ShopLocationView.cs
    ParkView.cs
    LibraryView.cs
    ClubView.cs
    LocationActionButton.cs
    LocationActionDetailPanel.cs

  Shop/
    ShopView.cs
    ShopItemView.cs
    ShopItemDetailView.cs
    PurchaseConfirmPopup.cs

  Deck/
    DeckManagementView.cs
    CardCollectionView.cs
    CardUpgradePopup.cs
    CardRemovePopup.cs

  Common/
    ConfirmPopupView.cs
    ToastView.cs
    GameLogView.cs
    NavigationBarView.cs
```

### 13.2 通用导航栏 NavigationBar

所有场景界面顶部建议使用同一导航栏。

显示：

```text
当前位置 | 当前阶段 | 剩余时间 | 精力 | 压力 | 心情 | 金钱
```

按钮：

```text
地图 | 卡组 | 道具 | 日志 | 设置
```

### 13.3 奖励预览

操作执行前显示预览，但不要暴露所有随机结果。

示例：

```text
课堂听讲
消耗：时间 1，精力 5
主要收益：学科属性、学习卡牌
风险：压力可能上升
```

### 13.4 风险提示

当玩家压力、精力或金钱不足时，操作按钮应给出提示。

示例：

```text
当前压力较高，继续高强度学习可能加入状态牌。
```

```text
精力不足，无法执行该操作。
```

---

## 14. 经济与商店平衡

### 14.1 金钱来源

| 来源 | 金额建议 |
|---|---:|
| 阶段开始零花钱 | 50 - 100 |
| 事件奖励 | 10 - 50 |
| Boss 胜利奖励 | 50 - 150 |
| 帮忙 / 社团小奖励 | 10 - 30 |
| 出售道具，V2 可选 | 原价 30% - 50% |

### 14.2 金钱用途

| 用途 | 设计意义 |
|---|---|
| 买消耗道具 | 短期解决精力 / 压力问题 |
| 买被动道具 | 长期构筑 |
| 买卡牌 | 补强卡组 |
| 升级卡牌 | 提升核心卡质量 |
| 删除卡牌 | 精简卡组 |
| 刷新商店 | 寻找更合适道具 |

### 14.3 防止商店过强

1. 商品库存有限。
2. 每阶段只刷新一次，主动刷新需要花钱。
3. 稀有商品有出现条件。
4. 关键卡牌不应只靠购买获得。
5. Boss 胜利奖励仍应有吸引力。

---

## 15. 时间、精力、压力的 V2 联动

### 15.1 时间

时间代表阶段内可行动次数。V2 中：

- 场景切换默认不消耗时间。
- 场景操作消耗时间。
- 某些轻操作消耗 0 时间，例如查看公告、整理卡组界面、打开商店。
- 真正产生收益的操作必须消耗时间、金钱或精力之一。

### 15.2 精力

精力作为执行学习、社团、挑战的限制。

建议规则：

| 精力范围 | 表现 |
|---:|---|
| 40 - max | 正常 |
| 20 - 39 | 部分高强度操作提示风险 |
| 1 - 19 | 学习挑战开局精力 -1 或成功率下降 |
| 0 | 只能休息、使用道具或执行少量轻操作 |

### 15.3 压力

保留 MVP 压力规则，V2 增加地图提示和场景建议。

| 压力范围 | 地图反馈 |
|---:|---|
| 0 - 30 | 正常 |
| 31 - 60 | 学习操作显示轻微风险 |
| 61 - 80 | 地图提示公园 / 家里可缓解压力 |
| 81 - 100 | 高风险提示，部分高压力操作需要确认 |

不要使用极端化、恐怖化或伤害化文本。压力只表示策略负担和状态管理。

---

## 16. 新增保存与读取 Save System

MVP 可以没有完整存档，但 V2 建议加入基础存档。

### 16.1 SaveData

```csharp
[System.Serializable]
public class SaveData
{
    public string saveVersion;
    public string saveTime;
    public GameSessionState sessionState;
}
```

### 16.2 SaveController

```csharp
public class SaveController : MonoBehaviour
{
    public void SaveGame(int slotIndex);
    public bool HasSave(int slotIndex);
    public SaveData LoadGame(int slotIndex);
    public void DeleteSave(int slotIndex);
}
```

### 16.3 存档规则

V2 至少支持：

- 手动保存。
- 进入新阶段自动保存。
- Boss 前自动保存。
- 3 个存档槽。

不要在卡牌挑战中途强制自动保存，避免状态不完整。

---

## 17. 新增任务与引导 Quest / Guide

### 17.1 轻量任务系统

任务系统用于引导玩家尝试新地点，不做复杂成就系统。

```csharp
[CreateAssetMenu(menuName = "Game Data/Quest Data")]
public class QuestData : ScriptableObject
{
    public string questId;
    public string displayName;
    public string description;
    public List<QuestObjectiveData> objectives;
    public List<RewardData> rewards;
    public List<EventConditionData> appearConditions;
}
```

### 17.2 QuestObjectiveType

```csharp
public enum QuestObjectiveType
{
    VisitLocation,
    CompleteAction,
    WinChallenge,
    AcquireItem,
    AcquireCard,
    ReachPlayerStat,
    DefeatBoss
}
```

### 17.3 V2 引导任务示例

| 任务 | 目标 | 奖励 |
|---|---|---|
| 熟悉地图 | 访问学校、家里、商店 | 金钱 +20 |
| 第一次购物 | 在商店购买 1 个道具 | 心情 +2 |
| 学会放松 | 在公园执行 1 次散步 | 压力 -3 |
| 整理卡组 | 打开一次卡组管理界面 | 自主性 +2 |
| 准备 Boss | 在 Boss 前查看 Boss 预览 | 自信 +1 |

---

## 18. 示例地点配置

### 18.1 Location：家里

```text
locationId: location_home
name: 家里
type: Home
unlockedByDefault: true
enterTimeCost: 0
```

Actions:

```text
home_rest_short
home_family_talk
home_plan_study
home_deck_manage
home_use_item
home_early_sleep
```

### 18.2 Location：学校

```text
locationId: location_school
name: 学校
type: School
unlockedByDefault: true
enterTimeCost: 0
```

Actions:

```text
school_class_listen
school_teacher_help
school_quiz_practice
school_group_study
school_notice_board
school_club_gate
```

### 18.3 Location：商店

```text
locationId: location_shop
name: 商店
type: Shop
unlockedByDefault: true
enterTimeCost: 0
```

Actions:

```text
shop_open_convenience
shop_open_bookstore
shop_refresh_stock
```

### 18.4 Location：公园

```text
locationId: location_park
name: 公园
type: Park
unlockedByDefault: true
enterTimeCost: 0
```

Actions:

```text
park_walk
park_meet_friend
park_hobby_practice
park_read_under_tree
park_family_walk
park_observe_life
```

### 18.5 Location：图书馆

```text
locationId: location_library
name: 图书馆
type: Library
unlockedByDefault: false
unlockCondition: stage >= stage_02_month_exam
enterTimeCost: 0
```

Actions:

```text
library_focus_study
library_upgrade_card
library_remove_card
library_find_book
library_reflect
```

### 18.6 Location：社团活动室

```text
locationId: location_club
name: 社团活动室
type: Club
unlockedByDefault: false
unlockCondition: interest >= 45 或完成同学邀请社团事件
enterTimeCost: 0
```

Actions:

```text
club_join_activity
club_prepare_show
club_talk_member
club_finish_project
```

---

## 19. 示例商店配置

### 19.1 便利店

```text
shopId: shop_convenience
name: 便利店
type: ConvenienceStore
refreshEachStage: true
refreshCost: 10
maxRandomStockCount: 4
```

默认商品：

| 商品 | 价格 | 库存 | 说明 |
|---|---:|---:|---|
| 小零食 | 25 | 2 | 心情 +8，精力 +5 |
| 功能饮料 | 45 | 1 | 战斗中精力 +15 |
| 随身听 | 60 | 1 | 阶段界面压力 -10 |

随机商品池：

| 商品 | 价格 | 出现条件 |
|---|---:|---|
| 父母鼓励纸条 | 50 | 亲子关系 >= 50 |
| 幸运笔 | 120 | Stage 2 后 |
| 社团徽章 | 90 | 社团已开放 |

### 19.2 书店

```text
shopId: shop_bookstore
name: 书店
type: BookStore
refreshEachStage: true
refreshCost: 20
maxRandomStockCount: 3
```

商品：

| 商品 | 价格 | 说明 |
|---|---:|---|
| 错题本 | 100 | 学习牌进度 +1 |
| 荧光笔 | 80 | 战斗开始抽 1 张 |
| 阅读课文卡 | 45 | 普通语文卡 |
| 整理错题卡 | 90 | 非普通学习卡 |

---

## 20. 示例场景操作数据

### 20.1 学校：课堂听讲

```text
actionId: school_class_listen
displayName: 课堂听讲
actionType: StartCardBattle
timeCost: 1
energyCost: 5
linkedChallenge: challenge_class_learning
showRewardPreview: true
requireConfirm: true
```

奖励倾向：

```text
成功：学科属性 +3 - 5，可能获得普通学习卡
失败：压力 +2，心情 -1
```

### 20.2 商店：打开便利店

```text
actionId: shop_open_convenience
displayName: 逛便利店
actionType: OpenShop
timeCost: 0
linkedShop: shop_convenience
showRewardPreview: false
requireConfirm: false
```

### 20.3 公园：散步放松

```text
actionId: park_walk
displayName: 散步放松
actionType: Rest
timeCost: 1
energyCost: 0
fixedRewards:
  - ReducePressure: 8
  - AddPlayerStat Mood: 3
```

### 20.4 图书馆：精读笔记

```text
actionId: library_upgrade_card
displayName: 精读笔记
actionType: ManageDeck
timeCost: 1
moneyCost: 30
executeCondition: hasUpgradeableCard == true
```

由于 `EventConditionData` 当前只支持属性条件，V2 可以新增条件类型，或在 `LocationActionValidator` 中处理复杂条件。

---

## 21. 条件系统扩展 Condition System

MVP 的 `EventConditionData` 主要检查玩家属性。V2 需要支持更多条件。

### 21.1 ConditionType

```csharp
public enum ConditionType
{
    PlayerStat,
    StageId,
    RemainingTime,
    HasItem,
    HasCard,
    LocationUnlocked,
    ActivityUnlocked,
    BossDefeated,
    QuestCompleted,
    ActionUsedCount,
    CustomFlag
}
```

### 21.2 GameConditionData

```csharp
[System.Serializable]
public class GameConditionData
{
    public ConditionType conditionType;
    public PlayerStatKey statKey;
    public ComparisonType comparison;
    public int value;

    public string stringValue;
    public ItemData item;
    public CardData card;
    public StageData stage;
    public LocationData location;
}
```

### 21.3 ConditionResolver

```csharp
public class ConditionResolver
{
    public bool IsConditionMet(GameConditionData condition, GameSessionState state);
    public bool AreConditionsMet(IEnumerable<GameConditionData> conditions, GameSessionState state);
    public string GetFailReason(GameConditionData condition, GameSessionState state);
}
```

用途：

- 地点解锁。
- 商品出现。
- 操作可执行。
- 事件选项可选。
- 任务完成。
- 结局判断。

---

## 22. Controller 职责边界 V2

### 22.1 LocationController

```csharp
public class LocationController : MonoBehaviour
{
    public IReadOnlyList<LocationData> GetAvailableLocations();
    public IReadOnlyList<LocationActionData> GetAvailableActions(LocationData location);
    public bool CanExecuteAction(LocationActionData action, out string reason);
    public void ExecuteAction(LocationActionData action);
}
```

职责：

- 提供当前可见地点。
- 提供当前地点可见操作。
- 检查操作条件。
- 将操作转发给 `ActivityController`、`ShopController`、`DeckManagementController` 等系统。

不负责：

- 具体奖励计算。
- 卡牌战斗流程。
- 商店购买细节。
- UI 绘制。

### 22.2 ShopController

负责商店库存、购买、刷新。

### 22.3 DeckManagementController

```csharp
public class DeckManagementController : MonoBehaviour
{
    public IReadOnlyList<CardData> GetUpgradeableCards();
    public bool CanUpgrade(CardData card, out string reason);
    public void UpgradeCard(CardData card);

    public IReadOnlyList<CardData> GetRemovableCards();
    public bool CanRemove(CardData card, out string reason);
    public void RemoveCard(CardData card);
}
```

### 22.4 GameLogController

负责记录玩家关键操作。

### 22.5 GuideController

负责阶段引导、地图提示和新系统教程。

---

## 23. 数据目录结构扩展

```text
Assets/
  Data/
    Locations/
      Location_Home.asset
      Location_School.asset
      Location_Shop.asset
      Location_Park.asset
      Location_Library.asset
      Location_Club.asset

    LocationActions/
      Home/
      School/
      Shop/
      Park/
      Library/
      Club/

    Shops/
      Shop_Convenience.asset
      Shop_BookStore.asset
      Shop_SchoolStore.asset

    Quests/
      Quest_Tutorial_Map.asset
      Quest_FirstShop.asset
      Quest_FirstParkWalk.asset

    MapHints/
      Hint_HighPressure.asset
      Hint_LowEnergy.asset
      Hint_BossPreparation.asset

  Scripts/
    Locations/
      LocationData.cs
      LocationType.cs
      LocationActionData.cs
      LocationActionType.cs
      LocationState.cs
      LocationController.cs
      SceneTransitionController.cs

    Shops/
      ShopData.cs
      ShopType.cs
      ShopItemEntryData.cs
      ShopRuntimeState.cs
      ShopController.cs
      PurchaseResult.cs

    Conditions/
      GameConditionData.cs
      ConditionType.cs
      ConditionResolver.cs

    Quests/
      QuestData.cs
      QuestObjectiveData.cs
      QuestController.cs

    Save/
      SaveData.cs
      SaveController.cs

    Logs/
      GameLogEntry.cs
      GameLogController.cs
```

---

## 24. V2 开发任务拆分

### Task V2-1：新增地点数据结构

目标：建立地点系统基础。

需要实现：

```text
LocationType
LocationData
LocationActionType
LocationActionData
LocationState
GameSessionState 扩展
```

验收：Unity 编译通过；可以在 Inspector 创建地点和地点操作。

### Task V2-2：实现地图与地点切换

目标：玩家可以在 UI 中从地图进入家里、学校、商店、公园。

需要实现：

```text
MapScreenView
LocationButtonView
SceneTransitionController
LocationView 基类
HomeView
SchoolView
ShopLocationView
ParkView
```

验收：不进入战斗也能在地点之间来回切换，状态不丢失。

### Task V2-3：实现地点操作系统

目标：地点内操作可以调用已有 Activity 系统。

需要实现：

```text
LocationController
LocationActionButton
LocationActionDetailPanel
LocationActionResult
操作条件检查
操作确认弹窗
操作后扣时间 / 精力 / 金钱
```

验收：在学校执行课堂听讲，在公园执行散步，在家里执行休息，都能正确结算。

### Task V2-4：实现商店系统

目标：商店可购买道具和卡牌。

需要实现：

```text
ShopData
ShopType
ShopItemEntryData
ShopRuntimeState
ShopController
ShopView
ShopItemView
PurchaseConfirmPopup
```

验收：进入商店，查看商品，购买道具，扣钱，库存减少，道具进入背包。

### Task V2-5：实现图书馆和卡组管理

目标：玩家可以升级和删除卡牌。

需要实现：

```text
LibraryView
DeckManagementView
DeckManagementController
CardUpgradePopup
CardRemovePopup
```

验收：升级卡牌后 masterDeck 中卡牌替换；删除卡牌后 masterDeck 数量减少。

### Task V2-6：实现公园、社团和非学习路线

目标：兴趣、社交、家庭路线更有价值。

需要实现：

```text
ParkView
ClubView
RandomEventPoolData
兴趣 / 社交 / 亲子相关事件池
地图提示系统
```

验收：玩家可以通过公园和社团获得兴趣、社交、自信等收益，并影响结局。

### Task V2-7：实现条件系统扩展

目标：支持地点、商品、操作、事件、任务的复杂条件。

需要实现：

```text
ConditionType
GameConditionData
ConditionResolver
锁定原因文本
```

验收：图书馆 Stage 2 后开放，社团满足条件开放，商品按条件出现。

### Task V2-8：实现日志、引导和保存

目标：提高可玩性和可测试性。

需要实现：

```text
GameLogEntry
GameLogController
QuestData
QuestController
GuideController
SaveData
SaveController
```

验收：操作后有日志；首次进入地图有引导；可以保存和读取阶段状态。

### Task V2-9：创建 V2 示例数据

目标：完整接入内容。

需要创建：

```text
6 个 LocationData
至少 30 个 LocationActionData
至少 3 个 ShopData
至少 20 个 ShopItemEntryData
至少 8 个新增事件
至少 6 个新增道具
至少 6 个地图提示
至少 5 个引导任务
```

验收：从 Stage 1 到 Stage 3，玩家可以通过地图场景完成一局。

---

## 25. V2 EditMode 测试要求

### 25.1 SceneTransitionControllerTests

测试：

- 进入地点时 currentLocationId 正确更新。
- 返回地图不会改变玩家状态。
- 未解锁地点不能进入。
- 已解锁地点可以进入。

### 25.2 LocationControllerTests

测试：

- 可见地点符合条件。
- 可见操作符合条件。
- 精力不足时不能执行对应操作。
- 金钱不足时不能执行消费操作。
- 执行操作后正确扣除时间、精力、金钱。

### 25.3 ShopControllerTests

测试：

- 商店初始化库存正确。
- 金钱不足时购买失败。
- 购买道具后金钱减少，道具进入背包。
- 购买卡牌后卡牌进入 masterDeck。
- 库存为 0 后不能继续购买。
- 刷新商店会扣除刷新费用。

### 25.4 DeckManagementControllerTests

测试：

- 可升级卡牌能被识别。
- 升级后替换为 upgradedVersion。
- 不可升级卡牌不能升级。
- 删除卡牌后 masterDeck 减少。
- 不允许删除到低于最小卡组数量。

### 25.5 ConditionResolverTests

测试：

- PlayerStat 条件正确。
- StageId 条件正确。
- HasItem 条件正确。
- HasCard 条件正确。
- BossDefeated 条件正确。
- CustomFlag 条件正确。

### 25.6 SaveControllerTests

测试：

- 存档可以写入。
- 存档可以读取。
- 读取后 PlayerState 一致。
- 读取后 StageState 一致。
- 读取后 LocationState 一致。
- 删除存档后 HasSave 返回 false。

---

## 26. V2 验收清单

### 26.1 场景切换验收

- [ ] 地图界面显示家里、学校、商店、公园等地点。
- [ ] 点击地点可以切换到对应地点界面。
- [ ] 每个地点界面都可以返回地图。
- [ ] 切换地点不会丢失玩家状态。
- [ ] 未解锁地点显示锁定原因。
- [ ] 当前地点在导航栏中正确显示。

### 26.2 商店验收

- [ ] 商店显示商品列表。
- [ ] 商品显示价格、库存、效果。
- [ ] 金钱足够时可以购买。
- [ ] 金钱不足时按钮禁用或提示原因。
- [ ] 购买后扣除金钱。
- [ ] 道具商品进入背包。
- [ ] 卡牌商品进入 masterDeck。
- [ ] 库存减少或售罄。
- [ ] 可以按阶段刷新商品。

### 26.3 学校验收

- [ ] 学校有课堂听讲、请教老师、小组学习等操作。
- [ ] 学校操作可以进入卡牌挑战、事件或小游戏。
- [ ] 学校能显示 Boss 准备度提示。
- [ ] 学校能引导玩家进入社团或特殊活动。

### 26.4 公园验收

- [ ] 公园可以降低压力、恢复心情。
- [ ] 公园有社交和兴趣相关事件。
- [ ] 公园收益能影响结局判断。
- [ ] 高压力时地图会提示公园。

### 26.5 家里验收

- [ ] 家里可以休息。
- [ ] 家里可以和父母沟通。
- [ ] 家里可以打开卡组和道具界面。
- [ ] 家里可以执行学习计划类操作。

### 26.6 图书馆 / 社团验收

- [ ] 图书馆可以升级或删除卡牌。
- [ ] 图书馆按条件解锁。
- [ ] 社团按条件解锁。
- [ ] 社团可以提升兴趣、社交和自信。

### 26.7 操作体验验收

- [ ] 操作按钮显示消耗。
- [ ] 操作按钮显示奖励预览或收益类型。
- [ ] 高风险操作有确认弹窗。
- [ ] 操作完成后有结果弹窗或 Toast。
- [ ] 操作日志能记录关键行为。
- [ ] 快捷键可用且不会误触关键操作。

### 26.8 存档验收

- [ ] 可以手动保存。
- [ ] 进入新阶段自动保存。
- [ ] Boss 前自动保存。
- [ ] 可以读取存档并恢复地点、阶段、玩家状态。

---

## 27. V2 平衡重点

### 27.1 避免“学校刷题”成为唯一最优解

需要让不同地点都有价值：

| 地点 | 长期价值 |
|---|---|
| 学校 | 学科、卡牌、Boss 准备 |
| 家里 | 精力、压力、亲子关系、卡组整理 |
| 商店 | 道具、卡牌、构筑补强 |
| 公园 | 压力管理、心情、兴趣、社交 |
| 图书馆 | 稳定学习、升级、删牌 |
| 社团 | 兴趣、自信、社交、特殊结局 |

### 27.2 让压力管理成为有效策略

压力低的玩家应得到更稳定的挑战体验。压力高的玩家可以通过强行学习获得短期收益，但会带来卡牌挑战难度提升。

### 27.3 金钱不能只是购买数值

金钱应提供选择：

- 买道具保 Boss。
- 买卡补卡组。
- 升级核心卡。
- 删除低效卡。
- 刷新商店寻找更适合路线的商品。

### 27.4 非成绩路线要能进入好结局

结局系统需要让以下路线成立：

- 学业稳定路线。
- 兴趣成长路线。
- 家庭支持路线。
- 自主规划路线。
- 社交互助路线。
- 压力管理路线。

---

## 28. 可直接给 Codex 的 V2 提示词

### 28.1 第一次提示词

```text
请阅读 CODEX_GAME_SPEC_STAGE_MVP.md 和 CODEX_GAME_SPEC_STAGE_V2_SCENE_OPTIMIZATION.md。当前 MVP 核心功能已完成。现在先实现 V2 Task V2-1 和 V2-2：新增 LocationData、LocationActionData、LocationState，并实现地图界面与地点面板切换。默认使用 MainScene 内 UI PanelSwitch，不要使用多 Unity Scene。完成后确保玩家可以从地图进入家里、学校、商店、公园并返回地图。
```

### 28.2 第二次提示词

```text
继续实现 V2 Task V2-3：LocationController 和地点操作系统。地点操作需要能引用现有 ActivityData，并根据 LocationActionData 扣除时间、精力、金钱。实现操作按钮、操作详情、确认弹窗和操作结果显示。至少接入家里休息、学校课堂听讲、公园散步三个操作。
```

### 28.3 第三次提示词

```text
继续实现 V2 Task V2-4：商店系统。创建 ShopData、ShopItemEntryData、ShopRuntimeState、ShopController 和 ShopView。商店需要支持道具和卡牌商品，购买时扣除 money，并通过 RewardResolver 或统一入口把道具加入背包、卡牌加入 masterDeck。添加 ShopControllerTests。
```

### 28.4 第四次提示词

```text
继续实现 V2 Task V2-5：图书馆和卡组管理。实现 DeckManagementView 和 DeckManagementController，支持查看 masterDeck、升级可升级卡牌、删除卡牌，并设置最小卡组数量限制。将图书馆地点接入地图，并添加升级 / 删除操作。
```

### 28.5 第五次提示词

```text
继续实现 V2 Task V2-6 和 V2-7：公园、社团、条件系统扩展。新增 GameConditionData、ConditionType、ConditionResolver，用于地点解锁、商品出现、操作执行条件。实现社团按兴趣或事件解锁，公园提供压力管理、兴趣和社交事件。
```

### 28.6 第六次提示词

```text
继续实现 V2 Task V2-8：日志、引导和保存。实现 GameLogController、GuideController、QuestController 和 SaveController。需要支持操作日志、首次进入地图引导、简单任务和 3 个存档槽。进入新阶段和 Boss 前自动保存。
```

### 28.7 第七次提示词

```text
继续实现 V2 Task V2-9：创建 V2 示例数据。至少创建 6 个 LocationData、30 个 LocationActionData、3 个 ShopData、20 个 ShopItemEntryData、8 个新增事件、6 个新增道具、6 个地图提示、5 个引导任务。确保玩家可以通过地图场景完成 Stage 1 到 Stage 3 并进入结局。
```

---

## 29. V2 最终目标

V2 最终交付应达到：

```text
玩家不再只面对抽象活动列表，而是可以在阶段内打开地图，选择家里、学校、商店、公园、图书馆、社团等地点；每个地点都有专属操作、消耗、奖励和事件。玩家可以通过学习、购物、休息、社交、兴趣、卡组管理和道具使用来准备阶段 Boss，最终根据阶段表现进入不同结局。
```

V2 的核心判断标准：

1. 场景切换清晰。
2. 每个地点有独立价值。
3. 商店和金钱系统真正参与策略。
4. 公园、家庭、社团等非学习地点能影响结局。
5. UI 能让玩家理解每次操作的消耗、收益和风险。
6. 现有 MVP 的阶段、活动、奖励、Boss 和结局系统仍然稳定运行。
