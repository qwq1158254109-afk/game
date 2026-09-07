# Unity 学生成长模拟卡牌游戏：Codex 阶段制开发规格书

> 建议文件名：`CODEX_GAME_SPEC.md`  
> 用途：把本文件放到 Unity 项目仓库根目录，或直接复制给 Codex，作为第一版原型开发说明。  
> 工作方式：Codex 应优先实现**可运行、可测试、可扩展**的 MVP，不要一次性开发完整商业版本。

---

## 0. 项目核心设定

本项目是一款基于 Unity 的学生成长模拟经营游戏，整体体验参考“学生成长、家庭教育、阶段选择、属性养成”的模拟经营结构，主要战斗 / 挑战玩法参考卡牌回合制构筑。

核心不是“打怪”，而是让玩家在有限阶段时间内，通过学习、休息、社交、家庭沟通、兴趣发展、小游戏和卡牌挑战来获得属性、卡牌和道具，并在阶段末用已积累的卡组、属性和道具挑战 Boss。

### 0.1 一句话玩法

```text
玩家在每个成长阶段中选择活动，消耗阶段时间，获得属性、卡牌和道具；阶段末进入 Boss 挑战，利用阶段内积累的资源击败 Boss 并进入下一阶段。
```

### 0.2 第一版必须体现的四个点

1. Unity 项目。
2. 学生成长模拟经营框架。
3. 卡牌回合制挑战作为主要玩法。
4. 每个阶段有时间限制，阶段末有 Boss，阶段内通过小游戏或卡牌对战获取新卡牌和道具。

---

## 1. 给 Codex 的执行指令

你是本 Unity 项目的代码代理。请根据本文档实现一个**阶段制学生成长模拟经营 + 卡牌回合制挑战**的 MVP 原型。

### 1.1 最高优先级目标

1. 先做一个能完整跑通的核心循环，而不是完整商业游戏。
2. 必须实现阶段制流程：`Stage -> Activity -> Reward -> Boss -> Next Stage`。
3. 所有核心玩法尽量数据驱动，不要把阶段、卡牌、事件、Boss、道具写死在 UI 脚本里。
4. 使用英文类名、方法名和字段名；UI 文案和示例数据可以使用中文。
5. 代码要便于扩展，不要为了 MVP 写成无法维护的一次性脚本。
6. 不要复制任何现有游戏的美术、UI、文案、角色名、卡牌名或专有表达；只借鉴玩法结构。

### 1.2 MVP 必须实现

第一版至少实现：

- 3 个阶段。
- 每个阶段有时间限制。
- 每个阶段有多个可选活动。
- 活动类型至少包括：普通卡牌挑战、简化小游戏、事件、休息。
- 至少 1 个活动可获得新卡牌。
- 至少 1 个活动可获得道具。
- 每个阶段末有 1 个 Boss 挑战。
- Boss 胜利后进入下一阶段。
- 完成最后阶段后进入结局界面。
- 至少 24 张示例卡牌。
- 至少 8 个示例事件。
- 至少 8 个示例道具。
- 至少 6 个示例小游戏配置。
- 至少 3 个 Boss 配置。
- EditMode 测试覆盖核心数值逻辑。

### 1.3 MVP 暂不实现

第一版不要实现以下内容：

- 完整小学、初中、高中、大学人生线。
- 多代传承系统。
- 商业级 UI 动效。
- 大型剧情演出。
- 完整存档系统。
- 复杂角色立绘。
- 大型地图或开放世界。
- 真实复杂小游戏操作。
- 联机、排行榜、内购。

---

## 2. MVP 阶段制核心循环

### 2.1 总流程

```text
开始新游戏
↓
初始化 PlayerState、牌库、道具栏、StageState
↓
进入 Stage 1
↓
显示阶段主界面：当前阶段、剩余时间、学生状态、可选活动、道具、卡组摘要
↓
玩家选择 1 个活动
↓
根据活动类型进入：
- 卡牌挑战
- 简化小游戏
- 事件选择
- 休息恢复
- 商店 / 训练，MVP 可选
↓
活动结算，发放奖励：属性、卡牌、道具、金钱、压力变化等
↓
扣除阶段时间
↓
如果阶段时间未结束：返回活动选择
如果阶段时间结束：进入阶段 Boss
↓
Boss 胜利：发放 Boss 奖励，进入下一阶段
Boss 失败：触发失败处理，仍可进入下一阶段或进入特殊结局，视配置而定
↓
完成全部阶段
↓
根据最终属性、Boss 胜利次数、压力、亲子关系、兴趣、社交、卡组倾向和道具显示结局
```

### 2.2 MVP 阶段数量

第一版固定为 3 个阶段。

| 阶段 ID | 阶段名 | 时间限制 | 阶段 Boss | 主要目标 |
|---|---|---:|---|---|
| `stage_01_adaptation` | 适应新学期 | 6 | 课堂小测 | 教会玩家活动选择、卡牌挑战和奖励选择 |
| `stage_02_month_exam` | 月考压力 | 6 | 月考 | 引入道具、压力管理、卡组构筑 |
| `stage_03_final_sprint` | 期末冲刺 | 8 | 期末综合考试 | 综合检验属性、卡组、道具和压力控制 |

### 2.3 阶段时间规则

- 每个阶段有 `timeLimit`。
- 每次选择活动会消耗 `timeCost`。
- MVP 中大部分活动消耗 1 点时间。
- 阶段剩余时间降到 0 时，自动进入 Boss。
- 可选扩展：允许玩家提前挑战 Boss，但 MVP 可以先不做。

### 2.4 Boss 失败规则

不要让一次失败直接结束游戏。MVP 建议：

| Boss | 失败处理 |
|---|---|
| Stage 1 Boss | 仍进入 Stage 2，但少拿一张奖励卡，压力 +5 |
| Stage 2 Boss | 进入 Stage 3，但自信 -5，获得一个补救事件 |
| Stage 3 Boss | 进入结局，根据失败原因显示普通结局或调整结局 |

---

## 3. 核心资源系统

### 3.1 PlayerState

`PlayerState` 是全局玩家成长状态。不要让 UI 直接修改数值，统一通过 Resolver 或 Controller 修改。

```csharp
[System.Serializable]
public class PlayerState
{
    public int chinese;
    public int math;
    public int english;

    public int energy;
    public int maxEnergy;
    public int pressure;
    public int mood;

    public int parentRelation;
    public int social;
    public int interest;
    public int confidence;
    public int autonomy;

    public int money;
}
```

### 3.2 数值范围

MVP 统一使用以下范围：

| 数值 | 范围 |
|---|---:|
| 学科能力 | 0 - 100 |
| 精力 | 0 - maxEnergy |
| 最大精力 | 1 - 100 |
| 压力 | 0 - 100 |
| 心情 | 0 - 100 |
| 亲子关系 | 0 - 100 |
| 社交 | 0 - 100 |
| 兴趣 | 0 - 100 |
| 自信 | 0 - 100 |
| 自主性 | 0 - 100 |
| 金钱 | 0 - 9999 |

### 3.3 初始数值

```text
chinese = 35
math = 35
english = 35
energy = 60
maxEnergy = 60
pressure = 20
mood = 60
parentRelation = 50
social = 40
interest = 40
confidence = 45
autonomy = 35
money = 100
```

### 3.4 压力阈值

压力不是简单血量，而是策略资源。压力越高，卡牌挑战越难。

| 压力范围 | 效果 |
|---:|---|
| 0 - 30 | 正常 |
| 31 - 60 | 部分学习牌额外增加压力 |
| 61 - 80 | 每场挑战开始加入 1 张“分心”状态牌 |
| 81 - 100 | 每场挑战开始加入 2 张“疲惫”状态牌，并降低初始手牌 1 张 |

注意：压力系统只做策略表达，不写极端伤害、自伤或恐怖化文本。

---

## 4. 阶段系统 Stage System

阶段系统是本版本的主流程核心。

### 4.1 StageData

创建 `StageData : ScriptableObject`。

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Stage Data")]
public class StageData : ScriptableObject
{
    public string stageId;
    public string displayName;
    public string description;

    public int timeLimit;

    public List<ActivityData> availableActivities;
    public ChallengeData bossChallenge;

    public List<RewardData> enterStageRewards;
    public List<RewardData> clearStageRewards;

    public string nextStageId;
    public bool isFinalStage;
}
```

### 4.2 StageState

```csharp
[System.Serializable]
public class StageState
{
    public StageData currentStage;
    public int remainingTime;
    public bool bossUnlocked;
    public bool bossDefeated;
    public int clearedStageCount;
    public int defeatedBossCount;
}
```

### 4.3 StageController 职责

`StageController` 负责：

- 开始新阶段。
- 初始化剩余时间。
- 提供可选活动列表。
- 活动结束后扣除时间。
- 判断是否进入 Boss。
- 处理 Boss 胜利 / 失败。
- 进入下一阶段。
- 判断是否进入结局。

建议方法：

```csharp
public class StageController : MonoBehaviour
{
    public void StartStage(StageData stageData);
    public IReadOnlyList<ActivityData> GetAvailableActivities();
    public void CompleteActivity(ActivityData activityData, ActivityResult result);
    public bool ShouldStartBoss();
    public void StartBoss();
    public void CompleteBoss(bool success);
    public void MoveToNextStage();
}
```

### 4.4 阶段进入和清除奖励

- `enterStageRewards`：进入阶段时自动发放。
- `clearStageRewards`：Boss 胜利后发放。
- 失败奖励由 Boss 的 `bossFailureRewards` 控制。

---

## 5. 活动系统 Activity System

阶段内不再自动每周触发事件，而是由玩家选择活动。

### 5.1 ActivityType

```csharp
public enum ActivityType
{
    Event,
    CardBattle,
    MiniGame,
    Rest,
    Shop,
    Training
}
```

### 5.2 ActivityData

创建 `ActivityData : ScriptableObject`。

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Activity Data")]
public class ActivityData : ScriptableObject
{
    public string activityId;
    public string displayName;
    public string description;
    public ActivityType activityType;

    public int timeCost;

    public ChallengeData linkedChallenge;
    public GameEventData linkedEvent;
    public MiniGameData linkedMiniGame;

    public List<RewardData> fixedRewards;
    public List<RewardData> possibleRewards;
    public List<EventConditionData> appearConditions;
}
```

### 5.3 活动类型说明

| 活动类型 | MVP 处理方式 | 主要奖励 |
|---|---|---|
| `CardBattle` | 进入普通卡牌挑战 | 卡牌、属性、压力变化 |
| `MiniGame` | 使用简化成功率结算 | 属性、道具、普通卡 |
| `Event` | 显示事件文本和选项 | 关系、心情、属性、自主性 |
| `Rest` | 直接结算 | 精力、心情、压力调整 |
| `Shop` | 可选，MVP 可简化为道具三选一 | 道具、卡牌 |
| `Training` | 可选，MVP 可直接结算 | 单项属性提升 |

### 5.4 活动选择规则

- 每次显示 3 - 5 个活动。
- 活动可根据阶段、剩余时间、属性、压力、金钱决定是否出现。
- MVP 可以直接显示 `currentStage.availableActivities` 中所有满足条件的活动。
- 活动完成后必须调用奖励系统，不能在 UI 脚本中直接改数值。

---

## 6. 小游戏系统 MiniGame System

MVP 不需要实现复杂实时小游戏。先实现“简化小游戏结算”。

### 6.1 MiniGameType

```csharp
public enum MiniGameType
{
    Memory,
    Timing,
    ChoiceQuiz,
    Reaction,
    SimplePuzzle
}
```

### 6.2 MiniGameData

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Mini Game Data")]
public class MiniGameData : ScriptableObject
{
    public string miniGameId;
    public string displayName;
    public string description;
    public MiniGameType miniGameType;

    public int difficulty;
    public PlayerStatKey primaryStat;

    public List<RewardData> successRewards;
    public List<RewardData> failureRewards;
}
```

### 6.3 简化成功率公式

```text
baseChance = 50
statBonus = primaryStat / 2
moodBonus = (mood - 50) / 4
pressurePenalty = pressure / 5
difficultyPenalty = difficulty * 5
successChance = Clamp(baseChance + statBonus + moodBonus - pressurePenalty - difficultyPenalty, 15, 95)
```

示例：

```text
单词记忆小游戏：
primaryStat = english
成功：英语 +4，获得 1 张普通学习卡候选
失败：压力 +3，心情 -2
```

### 6.4 MiniGameController 职责

```csharp
public class MiniGameController : MonoBehaviour
{
    public MiniGameResult ResolveMiniGame(MiniGameData data, PlayerState playerState, System.Random random);
}
```

```csharp
public class MiniGameResult
{
    public bool success;
    public int successChance;
    public List<RewardData> rewards;
}
```

---

## 7. 道具系统 Item System

道具用于让阶段经营和 Boss 挑战产生联系。

### 7.1 ItemType

```csharp
public enum ItemType
{
    Consumable,
    Passive,
    StageOnly
}
```

### 7.2 ItemData

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Item Data")]
public class ItemData : ScriptableObject
{
    public string itemId;
    public string displayName;
    public ItemType itemType;
    public string description;

    public List<CardEffectData> effects;
    public bool consumedOnUse;
    public bool canUseInBattle;
    public bool canUseInStageScreen;
}
```

### 7.3 ItemInventory

```csharp
[System.Serializable]
public class ItemInventory
{
    public List<ItemData> ownedItems = new List<ItemData>();
}
```

### 7.4 道具规则

- `Consumable`：使用后消失，例如功能饮料。
- `Passive`：持有即生效，例如错题本。
- `StageOnly`：只在当前阶段生效，进入下一阶段时清除。
- MVP 中每场挑战最多主动使用 1 个消耗道具。
- 被动道具自动由 `ItemEffectResolver` 在指定时机处理。

### 7.5 道具触发时机

```csharp
public enum ItemTriggerTiming
{
    OnAcquire,
    OnStageStart,
    OnBattleStart,
    OnCardPlayed,
    OnBossStart,
    OnUse,
    OnStageEnd
}
```

MVP 可以先实现：

- `OnAcquire`
- `OnBattleStart`
- `OnBossStart`
- `OnUse`
- `OnStageEnd`

---

## 8. 卡牌系统 Card System

卡牌系统保留原有构筑思路，但要服务阶段制成长。

### 8.1 CardType

```csharp
public enum CardType
{
    Study,
    Rest,
    Social,
    Family,
    Interest,
    Trait,
    Status
}
```

### 8.2 CardRarity

```csharp
public enum CardRarity
{
    Common,
    Uncommon,
    Rare
}
```

### 8.3 CardData

创建 `CardData : ScriptableObject`。

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Card Data")]
public class CardData : ScriptableObject
{
    public string cardId;
    public string displayName;
    public CardType cardType;
    public CardRarity rarity;
    public int cost;
    public string description;
    public List<CardEffectData> effects;
    public List<string> tags;
    public bool exhaustAfterUse;
    public bool isUpgradeable;
    public CardData upgradedVersion;
}
```

### 8.4 CardEffectData

```csharp
[System.Serializable]
public class CardEffectData
{
    public CardEffectType effectType;
    public PlayerStatKey targetStat;
    public int value;
    public string tag;
}
```

### 8.5 CardEffectType

```csharp
public enum CardEffectType
{
    AddChallengeProgress,
    AddSubjectProgress,
    AddPlayerStat,
    ReducePlayerStat,
    DrawCards,
    GainEnergy,
    LoseEnergy,
    ReducePressure,
    AddPressure,
    AddTemporaryBuff,
    AddStatusCardToDrawPile,
    AddStatusCardToDiscardPile,
    ExhaustCard,
    ModifyNextCardEffect
}
```

### 8.6 PlayerStatKey

```csharp
public enum PlayerStatKey
{
    Chinese,
    Math,
    English,
    Energy,
    MaxEnergy,
    Pressure,
    Mood,
    ParentRelation,
    Social,
    Interest,
    Confidence,
    Autonomy,
    Money
}
```

---

## 9. 牌库与挑战系统 Deck & Challenge

### 9.1 DeckState

```csharp
[System.Serializable]
public class DeckState
{
    public List<CardData> masterDeck = new List<CardData>();
    public List<CardData> drawPile = new List<CardData>();
    public List<CardData> hand = new List<CardData>();
    public List<CardData> discardPile = new List<CardData>();
    public List<CardData> exhaustPile = new List<CardData>();
}
```

### 9.2 DeckController 职责

```csharp
public class DeckController : MonoBehaviour
{
    public void InitializeBattleDeck(List<CardData> masterDeck);
    public void ShuffleDrawPile();
    public void DrawCards(int count);
    public void PlayCard(CardData card);
    public void DiscardHand();
    public void ReshuffleDiscardIntoDrawPile();
    public void AddCardToMasterDeck(CardData card);
    public void RemoveCardFromMasterDeck(CardData card);
    public void UpgradeCard(CardData source, CardData upgraded);
}
```

### 9.3 ChallengeData

创建 `ChallengeData : ScriptableObject`。

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Challenge Data")]
public class ChallengeData : ScriptableObject
{
    public string challengeId;
    public string displayName;
    public string description;

    public bool isBoss;
    public string requiredStageId;

    public int targetProgress;
    public int maxTurns;
    public int startingEnergy;
    public int startingHandSize;

    public List<ChallengeIntentData> intents;
    public List<RewardData> successRewards;
    public List<RewardData> failureRewards;
    public List<RewardData> bossClearRewards;
    public List<RewardData> bossFailureRewards;
}
```

### 9.4 ChallengeRuntimeState

```csharp
[System.Serializable]
public class ChallengeRuntimeState
{
    public ChallengeData data;
    public int currentProgress;
    public int currentTurn;
    public int currentEnergy;
    public bool isComplete;
    public bool isSuccess;
}
```

### 9.5 ChallengeIntentData

Boss 和普通挑战都可以有“意图”，用于模拟外部压力或目标变化。

```csharp
[System.Serializable]
public class ChallengeIntentData
{
    public string intentId;
    public string displayName;
    public string description;
    public int triggerTurn;
    public List<CardEffectData> effects;
}
```

示例意图：

| 意图 | 说明 |
|---|---|
| 临时作业 | 本回合压力 +2 |
| 老师提问 | 若语文低于 40，压力 +4 |
| 同学干扰 | 抽牌 -1 |
| 父母关注 | 亲子关系高时获得自信，低时压力增加 |

### 9.6 ChallengeController 职责

```csharp
public class ChallengeController : MonoBehaviour
{
    public void StartChallenge(ChallengeData challengeData);
    public bool CanPlayCard(CardData card);
    public void PlayCard(CardData card);
    public void EndTurn();
    public void ApplyTurnIntent();
    public void CompleteChallenge(bool success);
}
```

### 9.7 Boss 挑战规则

- Boss 是阶段末强制挑战。
- Boss 难度高于普通挑战。
- Boss 应读取玩家阶段内积累的属性、卡组和道具。
- Boss 胜利后进入下一阶段。
- Boss 失败后根据 `bossFailureRewards` 和阶段配置处理。

---

## 10. 奖励系统 Reward System

奖励系统是活动、小游戏、事件、挑战、Boss 的统一出口。

### 10.1 RewardType

```csharp
public enum RewardType
{
    AddCard,
    UpgradeCard,
    RemoveCard,
    AddPlayerStat,
    ReducePlayerStat,
    AddMoney,
    ReducePressure,
    IncreaseMaxEnergy,
    AddItem,
    RemoveItem,
    UnlockActivity,
    UnlockStage,
    AddStageTime
}
```

### 10.2 RewardData

```csharp
using UnityEngine;

[System.Serializable]
public class RewardData
{
    public RewardType rewardType;
    public PlayerStatKey targetStat;
    public int value;

    public CardData card;
    public ItemData item;
    public ActivityData activity;
    public StageData stage;

    public string description;
}
```

### 10.3 RewardResolver 职责

```csharp
public class RewardResolver
{
    public void ApplyReward(RewardData reward, GameSessionState sessionState);
    public void ApplyRewards(IEnumerable<RewardData> rewards, GameSessionState sessionState);
}
```

### 10.4 奖励来源建议

| 来源 | 奖励倾向 |
|---|---|
| 小游戏 | 属性、普通卡、少量道具 |
| 普通卡牌挑战 | 卡牌、删牌、升级、属性 |
| 事件 | 关系、心情、自主性、压力变化 |
| 休息 | 精力、心情、压力调整 |
| Boss | 稀有卡、强力道具、进入下一阶段 |
| 商店 | 用金钱换道具或卡牌 |

---

## 11. 事件系统 Event System

事件系统作为 `ActivityType.Event` 的一种执行方式。

### 11.1 GameEventData

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Event Data")]
public class GameEventData : ScriptableObject
{
    public string eventId;
    public string title;
    public string description;
    public List<EventChoiceData> choices;
    public List<EventConditionData> appearConditions;
}
```

### 11.2 EventChoiceData

```csharp
[System.Serializable]
public class EventChoiceData
{
    public string choiceText;
    public List<EventConditionData> requirements;
    public List<RewardData> rewards;
}
```

### 11.3 EventConditionData

```csharp
public enum ComparisonType
{
    GreaterOrEqual,
    LessOrEqual,
    Equal,
    NotEqual
}

[System.Serializable]
public class EventConditionData
{
    public PlayerStatKey statKey;
    public ComparisonType comparison;
    public int value;
}
```

### 11.4 事件设计要求

事件应提供选择，而不是单纯加减数值。

示例：父母希望你报补习班

```text
选项 A：接受
奖励：数学 +6，压力 +4，金钱 -30

选项 B：拒绝
奖励：自主性 +5，亲子关系 -3，压力 -2

选项 C：沟通后折中
需求：亲子关系 >= 50
奖励：数学 +3，亲子关系 +3，压力 +1
```

---

## 12. GameSessionState

`GameSessionState` 是一局游戏的总状态。

```csharp
[System.Serializable]
public class GameSessionState
{
    public PlayerState playerState;
    public DeckState deckState;
    public ItemInventory itemInventory;
    public StageState stageState;

    public List<string> clearedStageIds = new List<string>();
    public List<string> defeatedBossIds = new List<string>();
    public List<string> unlockedActivityIds = new List<string>();
}
```

---

## 13. UI 需求

UI 不负责核心计算，只负责显示状态和转发玩家操作。

### 13.1 MVP 场景

建议只做一个主场景：

```text
Assets/Scenes/MainScene.unity
```

主场景内包含：

- `GameBootstrapper`
- `StageController`
- `ActivityController`
- `ChallengeController`
- `DeckController`
- `MiniGameController`
- `RewardResolver`
- UI Canvas

### 13.2 阶段主界面 Stage Screen

必须显示：

```text
顶部：当前阶段、阶段说明、剩余时间、阶段目标
左侧：学生状态，包括三科、精力、压力、心情、亲子关系、兴趣、社交等
中间：可选活动列表
右侧：持有道具、卡组摘要
底部：Boss 按钮或 Boss 状态提示
```

UI 脚本：

```text
StageHUDView
ActivitySelectionView
PlayerStatusView
ItemInventoryView
DeckSummaryView
BossPreviewView
```

### 13.3 活动界面

不同活动进入不同界面：

| 活动类型 | 界面 |
|---|---|
| CardBattle | ChallengeBattleView |
| MiniGame | MiniGameResultView |
| Event | EventPopupView |
| Rest | RewardPopupView |
| Shop | ShopView，MVP 可选 |
| Training | RewardPopupView |

### 13.4 卡牌挑战界面

必须显示：

```text
当前挑战名称
当前回合 / 最大回合
目标进度 / 当前进度
当前精力
压力、心情
敌方或挑战意图
玩家手牌
抽牌堆、弃牌堆、消耗堆数量
结束回合按钮
可用道具按钮
```

### 13.5 奖励界面

奖励界面至少支持：

- 显示获得属性。
- 显示获得卡牌。
- 显示获得道具。
- 卡牌三选一。
- 道具三选一，MVP 可选。
- 点击确认回到阶段主界面或进入下一阶段。

---

## 14. 推荐目录结构

```text
Assets/
  Scenes/
    MainScene.unity

  Scripts/
    Core/
      GameBootstrapper.cs
      GameSessionState.cs
      PlayerState.cs
      PlayerStatKey.cs
      ValueClampUtility.cs

    Stage/
      StageData.cs
      StageState.cs
      StageController.cs

    Activity/
      ActivityData.cs
      ActivityType.cs
      ActivityController.cs
      ActivityResult.cs

    Cards/
      CardData.cs
      CardType.cs
      CardRarity.cs
      CardEffectData.cs
      CardEffectType.cs
      CardEffectResolver.cs
      DeckState.cs
      DeckController.cs

    Challenge/
      ChallengeData.cs
      ChallengeRuntimeState.cs
      ChallengeIntentData.cs
      ChallengeController.cs

    MiniGames/
      MiniGameData.cs
      MiniGameType.cs
      MiniGameController.cs
      MiniGameResult.cs

    Items/
      ItemData.cs
      ItemType.cs
      ItemInventory.cs
      ItemEffectResolver.cs
      ItemTriggerTiming.cs

    Events/
      GameEventData.cs
      EventChoiceData.cs
      EventConditionData.cs
      ComparisonType.cs
      EventController.cs

    Rewards/
      RewardData.cs
      RewardType.cs
      RewardResolver.cs

    Endings/
      EndingData.cs
      EndingController.cs

    UI/
      StageHUDView.cs
      ActivitySelectionView.cs
      PlayerStatusView.cs
      ItemInventoryView.cs
      DeckSummaryView.cs
      BossPreviewView.cs
      ChallengeBattleView.cs
      CardView.cs
      EventPopupView.cs
      RewardPopupView.cs
      MiniGameResultView.cs
      EndingView.cs

  Data/
    Cards/
    Challenges/
    Stages/
    Activities/
    MiniGames/
    Items/
    Events/
    Endings/
```

---

## 15. 示例阶段配置

### 15.1 Stage 1：适应新学期

```text
stageId: stage_01_adaptation
displayName: 适应新学期
timeLimit: 6
bossChallenge: boss_class_quiz
nextStageId: stage_02_month_exam
```

可选活动：

| 活动 | 类型 | 消耗时间 | 说明 |
|---|---|---:|---|
| 课堂听讲 | CardBattle | 1 | 基础学习挑战，奖励学科属性 |
| 单词记忆 | MiniGame | 1 | 简化记忆小游戏，奖励英语或学习卡 |
| 和父母聊天 | Event | 1 | 提升亲子关系或自主性 |
| 休息一下 | Rest | 1 | 恢复精力，降低压力 |
| 整理书包 | MiniGame | 1 | 奖励小道具或心情 |

Boss：课堂小测

```text
目标进度：45
最大回合：3
胜利：进入 Stage 2，获得 1 张普通卡
失败：压力 +5，仍进入 Stage 2
```

### 15.2 Stage 2：月考压力

```text
stageId: stage_02_month_exam
displayName: 月考压力
timeLimit: 6
bossChallenge: boss_month_exam
nextStageId: stage_03_final_sprint
```

可选活动：

| 活动 | 类型 | 消耗时间 | 说明 |
|---|---|---:|---|
| 刷题训练 | CardBattle | 1 | 较高收益学习挑战 |
| 错题整理 | MiniGame | 1 | 成功后获得错题本类道具或升级机会 |
| 同学互助 | Event | 1 | 社交、学习和心情的取舍 |
| 道具商店 | Shop | 1 | 用金钱购买道具，MVP 可做三选一 |
| 压力管理 | Rest | 1 | 降低压力，恢复心情 |

Boss：月考

```text
目标进度：90
最大回合：5
胜利：进入 Stage 3，获得 1 张非普通卡或 1 个道具
失败：自信 -5，进入 Stage 3，触发补救事件
```

### 15.3 Stage 3：期末冲刺

```text
stageId: stage_03_final_sprint
displayName: 期末冲刺
timeLimit: 8
bossChallenge: boss_final_exam
isFinalStage: true
```

可选活动：

| 活动 | 类型 | 消耗时间 | 说明 |
|---|---|---:|---|
| 综合复习 | CardBattle | 1 | 高难挑战，奖励强力学习卡 |
| 社团收尾 | Event | 1 | 兴趣路线事件 |
| 家庭沟通 | Event | 1 | 亲子关系和压力管理 |
| 模拟考试 | CardBattle | 1 | 高收益高压力挑战 |
| 好好睡觉 | Rest | 1 | 大幅恢复精力，降低压力 |
| 重点突破 | Training | 1 | 选择单项学科提升 |

Boss：期末综合考试

```text
目标进度：150
最大回合：7
胜利：进入结局，根据属性判定优秀 / 兴趣 / 家庭支持等结局
失败：进入普通结局或调整结局
```

---

## 16. 示例卡牌

至少创建 24 张示例卡牌。

| ID | 名称 | 类型 | 稀有度 | 消耗 | 效果 |
|---|---|---|---|---:|---|
| `card_math_practice` | 刷数学题 | Study | Common | 2 | 挑战进度 +8，压力 +2 |
| `card_english_words` | 背英语单词 | Study | Common | 1 | 挑战进度 +5，英语 +1 |
| `card_chinese_reading` | 阅读课文 | Study | Common | 1 | 挑战进度 +5，抽 1 张 |
| `card_preview_lesson` | 课前预习 | Study | Common | 1 | 挑战进度 +4，下张学习牌 +2 |
| `card_wrong_notes` | 整理错题 | Study | Uncommon | 1 | 挑战进度 +6，本场学习牌效果 +1 |
| `card_teacher_help` | 请教老师 | Study | Uncommon | 2 | 挑战进度 +12，自信 +1 |
| `card_focus_time` | 专注时间 | Study | Rare | 2 | 挑战进度 +14，抽 1 张 |
| `card_late_review` | 睡前复习 | Study | Uncommon | 1 | 挑战进度 +10，精力 -5，压力 +3 |
| `card_good_sleep` | 好好睡觉 | Rest | Common | 1 | 精力 +12，压力 -3 |
| `card_listen_music` | 听音乐 | Rest | Common | 1 | 压力 -6，心情 +3 |
| `card_take_walk` | 出去散步 | Rest | Common | 1 | 压力 -4，社交 +1 |
| `card_short_break` | 短暂休息 | Rest | Common | 0 | 精力 +4 |
| `card_chat_classmate` | 和同学聊天 | Social | Common | 1 | 社交 +3，压力 -2 |
| `card_group_study` | 小组学习 | Social | Uncommon | 2 | 挑战进度 +8，社交 +2 |
| `card_help_friend` | 帮朋友补习 | Social | Uncommon | 1 | 挑战进度 +5，社交 +3 |
| `card_join_club` | 参加社团 | Interest | Common | 1 | 兴趣 +4，心情 +2，精力 -3 |
| `card_practice_hobby` | 练习兴趣 | Interest | Common | 1 | 兴趣 +5，压力 -1 |
| `card_creative_burst` | 灵感爆发 | Interest | Rare | 2 | 挑战进度 +10，兴趣 +3，抽 1 张 |
| `card_talk_parent` | 和父母沟通 | Family | Common | 1 | 亲子关系 +4，压力 -2 |
| `card_report_progress` | 主动汇报 | Family | Common | 1 | 亲子关系 +3，自信 +2 |
| `card_negotiate_plan` | 商量计划 | Family | Uncommon | 2 | 自主性 +4，压力 -3 |
| `card_self_discipline` | 自律习惯 | Trait | Rare | 0 | 本场学习牌进度 +1，消耗 |
| `card_optimistic` | 乐观心态 | Trait | Rare | 0 | 压力 -8，心情 +5，消耗 |
| `card_distracted` | 分心 | Status | Common | 1 | 无正向效果，占用手牌 |
| `card_tired` | 疲惫 | Status | Common | 1 | 精力 -2，占用手牌 |

---

## 17. 示例道具

至少创建 8 个示例道具。

| ID | 名称 | 类型 | 效果 |
|---|---|---|---|
| `item_energy_drink` | 功能饮料 | Consumable | 战斗中使用，精力 +15 |
| `item_lucky_pen` | 幸运笔 | Passive | Boss 战开始时挑战进度 +10 |
| `item_wrong_book` | 错题本 | Passive | 学习牌挑战进度 +1 |
| `item_music_player` | 随身听 | Consumable | 阶段界面使用，压力 -10 |
| `item_parent_note` | 父母鼓励纸条 | StageOnly | 本阶段自信 +5，阶段结束移除 |
| `item_highlighter` | 荧光笔 | Passive | 抽牌阶段第一次学习牌消耗 -1，MVP 可简化为挑战开始抽 1 张 |
| `item_club_badge` | 社团徽章 | Passive | 兴趣事件奖励 +1 |
| `item_snack_pack` | 小零食 | Consumable | 心情 +8，精力 +5 |

---

## 18. 示例小游戏

至少创建 6 个 `MiniGameData`。

| ID | 名称 | 类型 | 主属性 | 难度 | 成功奖励 | 失败奖励 |
|---|---|---|---|---:|---|---|
| `mini_word_memory` | 单词记忆 | Memory | English | 2 | 英语 +4，普通学习卡候选 | 压力 +3 |
| `mini_formula_match` | 公式配对 | SimplePuzzle | Math | 3 | 数学 +5，可能获得错题本 | 压力 +4 |
| `mini_reading_quiz` | 阅读问答 | ChoiceQuiz | Chinese | 2 | 语文 +4，心情 +1 | 心情 -2 |
| `mini_timing_focus` | 专注计时 | Timing | Confidence | 3 | 自信 +4，抽卡奖励 | 精力 -3 |
| `mini_reaction_notes` | 课堂反应 | Reaction | Autonomy | 2 | 自主性 +3，语文 +2 | 压力 +2 |
| `mini_bag_sort` | 整理书包 | SimplePuzzle | Mood | 1 | 心情 +4，可能获得小零食 | 无 |

---

## 19. 示例事件

至少创建 8 个事件。

### 19.1 父母要求补习

```text
标题：要不要报补习班？
说明：父母希望你用周末时间补习数学。

选项 A：接受
奖励：数学 +6，压力 +4，金钱 -30

选项 B：拒绝
奖励：自主性 +5，亲子关系 -3，压力 -2

选项 C：沟通后折中
需求：亲子关系 >= 50
奖励：数学 +3，亲子关系 +3，压力 +1
```

### 19.2 同学邀请社团

```text
选项 A：参加
奖励：兴趣 +6，社交 +3，精力 -4

选项 B：拒绝
奖励：精力 +3，社交 -2

选项 C：只参加一次
奖励：兴趣 +3，社交 +1
```

### 19.3 老师建议整理错题

```text
选项 A：认真整理
奖励：获得卡牌“整理错题”，压力 +2

选项 B：简单看看
奖励：数学 +2，心情 +1

选项 C：请教老师
需求：自信 >= 40
奖励：数学 +4，自信 +2
```

### 19.4 朋友想一起学习

```text
选项 A：一起学习
奖励：社交 +3，挑战进度类卡牌候选

选项 B：自己复习
奖励：数学 +3，自主性 +1

选项 C：先聊天再学习
奖励：社交 +2，压力 -2，精力 -2
```

### 19.5 家庭晚饭聊天

```text
选项 A：分享学校生活
奖励：亲子关系 +5，心情 +2

选项 B：只聊成绩
奖励：语文 +1，数学 +1，英语 +1，压力 +2

选项 C：早点休息
奖励：精力 +8
```

### 19.6 社团展示机会

```text
选项 A：报名展示
奖励：兴趣 +6，自信 +3，压力 +3

选项 B：旁观学习
奖励：兴趣 +3，压力 -1

选项 C：不参加
奖励：精力 +4
```

### 19.7 临时小测通知

```text
选项 A：马上刷题
奖励：数学 +4，压力 +3

选项 B：整理重点
奖励：获得 1 张学习卡候选

选项 C：保持节奏
奖励：心情 +2，自信 +1
```

### 19.8 周末安排

```text
选项 A：集中复习
奖励：三科各 +2，压力 +4

选项 B：休息调整
奖励：压力 -6，心情 +4

选项 C：兴趣练习
奖励：兴趣 +5，自主性 +2
```

---

## 20. 示例 Boss 数据

### 20.1 Boss：课堂小测

```text
challengeId: boss_class_quiz
displayName: 课堂小测
isBoss: true
requiredStageId: stage_01_adaptation
targetProgress: 45
maxTurns: 3
startingEnergy: 3
startingHandSize: 5
```

意图：

| 回合 | 意图 | 效果 |
|---:|---|---|
| 1 | 题目偏基础 | 无 |
| 2 | 老师提醒时间 | 压力 +2 |
| 3 | 最后检查 | 如果进度低于 35，压力 +3 |

### 20.2 Boss：月考

```text
challengeId: boss_month_exam
displayName: 月考
isBoss: true
requiredStageId: stage_02_month_exam
targetProgress: 90
maxTurns: 5
startingEnergy: 3
startingHandSize: 5
```

意图：

| 回合 | 意图 | 效果 |
|---:|---|---|
| 1 | 试卷发下 | 压力 +1 |
| 2 | 难题出现 | 下一张学习牌效果 -1 |
| 3 | 同学提前交卷 | 压力 +3，自信 -1 |
| 4 | 时间紧张 | 当前精力 -1 |
| 5 | 最后冲刺 | 若自信 >= 50，进度 +5 |

### 20.3 Boss：期末综合考试

```text
challengeId: boss_final_exam
displayName: 期末综合考试
isBoss: true
requiredStageId: stage_03_final_sprint
targetProgress: 150
maxTurns: 7
startingEnergy: 3
startingHandSize: 5
```

意图：

| 回合 | 意图 | 效果 |
|---:|---|---|
| 1 | 语文部分 | 语文 >= 60 时进度 +5 |
| 2 | 数学部分 | 数学 >= 60 时进度 +5，否则压力 +2 |
| 3 | 英语部分 | 英语 >= 60 时进度 +5 |
| 4 | 综合应用 | 兴趣 + 自主性 >= 100 时进度 +5 |
| 5 | 状态波动 | 压力 >= 70 时抽牌 -1 |
| 6 | 家庭支持 | 亲子关系 >= 70 时自信 +3 |
| 7 | 最后检查 | 自信 >= 60 时进度 +10 |

---

## 21. 结局系统 Ending System

完成所有阶段或最终 Boss 失败后进入结局。

### 21.1 EndingData

```csharp
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game Data/Ending Data")]
public class EndingData : ScriptableObject
{
    public string endingId;
    public string displayName;
    public string description;
    public List<EventConditionData> conditions;
    public int priority;
}
```

### 21.2 结局判断字段

结局应综合以下内容：

- 通关阶段数量。
- Boss 胜利次数。
- 三科平均分。
- 压力。
- 亲子关系。
- 兴趣。
- 社交。
- 自信。
- 自主性。
- 持有关键道具。
- 卡组中学习 / 兴趣 / 社交 / 家庭牌的比例。

### 21.3 示例结局

| 结局 | 条件 |
|---|---|
| 稳定升学 | 击败所有 Boss，三科平均 >= 75 |
| 兴趣成长 | 最终兴趣 >= 80，且完成最终阶段 |
| 家庭支持 | 亲子关系 >= 80，压力 < 70 |
| 自主选择 | 自主性 >= 80，自信 >= 60 |
| 压力调整 | 最终压力 >= 85 |
| 普通但完整的成长 | 完成至少 2 个阶段，综合属性中等 |

---

## 22. 数据驱动要求

### 22.1 必须使用 ScriptableObject 的数据

以下内容建议用 ScriptableObject：

- `CardData`
- `ChallengeData`
- `StageData`
- `ActivityData`
- `MiniGameData`
- `ItemData`
- `GameEventData`
- `EndingData`

### 22.2 不要硬编码的内容

不要在 Controller 中硬编码：

- 阶段数量。
- 每个阶段的 Boss。
- 每个阶段的活动。
- 卡牌效果文本。
- 道具效果文本。
- 事件选项。
- 结局条件。

MVP 可以通过 `GameBootstrapper` 引用一组默认 ScriptableObject 数据来启动。

---

## 23. Controller 职责边界

### 23.1 GameBootstrapper

负责：

- 初始化一局游戏。
- 加载默认初始牌库。
- 加载 Stage 列表。
- 创建 `GameSessionState`。
- 启动第一个阶段。

### 23.2 StageController

负责阶段流程，不负责卡牌效果。

### 23.3 ActivityController

负责根据 ActivityType 调用对应系统。

### 23.4 ChallengeController

负责单场卡牌挑战流程，不负责结局判断。

### 23.5 RewardResolver

负责统一发放奖励。

### 23.6 CardEffectResolver

负责解析卡牌效果。

### 23.7 ItemEffectResolver

负责解析道具效果。

### 23.8 EndingController

负责根据当前状态选出优先级最高的结局。

---

## 24. EditMode 测试要求

创建测试目录：

```text
Assets/Tests/EditMode/
```

至少实现以下测试。

### 24.1 PlayerStateClampTests

测试：

- 压力不会低于 0。
- 压力不会超过 100。
- 精力不会超过 maxEnergy。
- 金钱不会低于 0。

### 24.2 RewardResolverTests

测试：

- `AddPlayerStat` 正确增加属性。
- `ReducePressure` 正确降低压力。
- `AddCard` 正确加入 masterDeck。
- `AddItem` 正确加入 itemInventory。
- `AddStageTime` 正确增加剩余时间。

### 24.3 StageControllerTests

测试：

- 开始阶段时 remainingTime = timeLimit。
- 完成活动后 remainingTime 减少。
- remainingTime 为 0 时应进入 Boss。
- Boss 胜利后 defeatedBossCount 增加。
- Final Stage 完成后进入结局。

### 24.4 MiniGameControllerTests

测试：

- 成功率被限制在 15 - 95。
- 高属性比低属性成功率更高。
- 压力越高成功率越低。
- 成功时返回 successRewards。
- 失败时返回 failureRewards。

### 24.5 DeckControllerTests

测试：

- 初始化战斗牌堆时 masterDeck 不丢失。
- 抽牌会从 drawPile 移动到 hand。
- 弃牌堆可洗回抽牌堆。
- 使用 exhaustAfterUse 的卡会进入 exhaustPile。

### 24.6 ChallengeControllerTests

测试：

- 打出进度卡会增加挑战进度。
- 达到 targetProgress 判定成功。
- 超过 maxTurns 且未达成目标判定失败。
- Boss 挑战会使用 bossClearRewards 或 bossFailureRewards。

---

## 25. Codex 开发任务拆分

以下任务应按顺序执行。不要一次性实现全部。

### Task 1：创建核心数据结构

目标：创建所有核心数据类和枚举。

需要实现：

```text
PlayerState
PlayerStatKey
GameSessionState
StageData
StageState
ActivityData
ActivityType
CardData
CardEffectData
CardType
CardRarity
CardEffectType
ChallengeData
ChallengeRuntimeState
ChallengeIntentData
MiniGameData
MiniGameType
ItemData
ItemType
RewardData
RewardType
GameEventData
EventChoiceData
EventConditionData
EndingData
```

验收：Unity 编译通过，无 UI 要求。

### Task 2：实现数值夹取和奖励系统

目标：让所有属性变化通过统一入口处理。

需要实现：

```text
ValueClampUtility
RewardResolver
PlayerState 修改方法
ItemInventory 基础方法
DeckState 基础方法
```

验收：RewardResolverTests 通过。

### Task 3：实现卡牌和牌库系统

目标：可以初始化牌库、抽牌、出牌、弃牌、洗牌。

需要实现：

```text
DeckController
CardEffectResolver 基础效果
学习牌增加挑战进度
休息牌恢复精力 / 降低压力
状态牌占用手牌
```

验收：DeckControllerTests 通过。

### Task 4：实现普通卡牌挑战和 Boss 挑战

目标：可以跑通一场挑战。

需要实现：

```text
ChallengeController
ChallengeRuntimeState
回合开始
抽牌
出牌
结束回合
意图结算
胜负判断
Boss 奖励分支
```

验收：ChallengeControllerTests 通过。

### Task 5：实现阶段系统

目标：可以从 Stage 1 跑到 Stage 3。

需要实现：

```text
StageController
StartStage
CompleteActivity
ShouldStartBoss
StartBoss
CompleteBoss
MoveToNextStage
```

验收：StageControllerTests 通过。

### Task 6：实现活动系统

目标：阶段内可以选择活动，并根据类型跳转。

需要实现：

```text
ActivityController
ActivityResult
CardBattle 活动
MiniGame 活动
Event 活动
Rest 活动
```

验收：选择活动后能正确扣时间并发奖励。

### Task 7：实现简化小游戏系统

目标：小游戏使用成功率公式结算。

需要实现：

```text
MiniGameController
MiniGameResult
成功率计算
成功 / 失败奖励返回
MiniGameResultView
```

验收：MiniGameControllerTests 通过。

### Task 8：实现道具系统

目标：活动和奖励可以获得道具，战斗中可使用消耗道具。

需要实现：

```text
ItemInventory
ItemEffectResolver
Consumable 使用
Passive 在 Boss 开始或战斗开始触发
StageOnly 阶段结束清除
```

验收：获得道具、使用道具、清除阶段道具都能正常工作。

### Task 9：实现基础 UI

目标：玩家能从 UI 完整跑一局。

需要实现：

```text
StageHUDView
ActivitySelectionView
PlayerStatusView
ItemInventoryView
ChallengeBattleView
CardView
EventPopupView
RewardPopupView
MiniGameResultView
EndingView
```

验收：不用控制台操作，可以用鼠标完成一局 MVP。

### Task 10：创建示例数据并接入完整流程

目标：创建 MVP 示例内容。

需要创建：

```text
3 个 StageData
至少 12 个 ActivityData
至少 24 张 CardData
至少 8 个 ItemData
至少 6 个 MiniGameData
至少 8 个 GameEventData
至少 6 个 ChallengeData，其中 3 个 Boss
至少 5 个 EndingData
```

验收：从新游戏开始，完成 3 个阶段，进入结局。

---

## 26. 可直接给 Codex 的分阶段提示词

### 26.1 第一次给 Codex

```text
请阅读仓库根目录的 CODEX_GAME_SPEC.md。先不要做 UI 和美术，只完成 Task 1 和 Task 2：创建核心数据结构、枚举、GameSessionState、PlayerState、RewardData、RewardResolver 和数值夹取工具。完成后添加 EditMode 测试，确保 Unity 编译通过。
```

### 26.2 第二次给 Codex

```text
继续根据 CODEX_GAME_SPEC.md 实现 Task 3：DeckController、CardEffectResolver 和基础卡牌效果。请让学习牌可以增加 ChallengeRuntimeState.currentProgress，让休息牌可以恢复精力或降低压力。添加 DeckControllerTests。
```

### 26.3 第三次给 Codex

```text
继续实现 Task 4：ChallengeController。需要支持 StartChallenge、PlayCard、EndTurn、ApplyTurnIntent、CompleteChallenge。普通挑战和 Boss 挑战都要能结算成功 / 失败奖励。添加 ChallengeControllerTests。
```

### 26.4 第四次给 Codex

```text
继续实现 Task 5 和 Task 6：StageController 与 ActivityController。请实现阶段时间限制、活动选择、活动完成扣时间、时间结束进入 Boss、Boss 胜利进入下一阶段。先用代码创建临时示例数据，保证可以从 Stage 1 运行到 Stage 3。
```

### 26.5 第五次给 Codex

```text
继续实现 Task 7 和 Task 8：MiniGameController 和 ItemEffectResolver。小游戏先使用文档中的成功率公式，不做复杂实时操作。道具需要支持 Consumable、Passive、StageOnly，并能在战斗开始或 Boss 开始时触发。
```

### 26.6 第六次给 Codex

```text
继续实现 Task 9：基础 UI。请创建一个 MainScene，包含阶段主界面、活动选择、学生状态、道具栏、卡牌挑战界面、事件弹窗、奖励弹窗、小游戏结果界面和结局界面。UI 可以简陋，但必须能完整跑通一局。
```

### 26.7 第七次给 Codex

```text
继续实现 Task 10：创建 ScriptableObject 示例数据。请创建 3 个阶段、12 个活动、24 张卡牌、8 个道具、6 个小游戏、8 个事件、6 个挑战和 5 个结局。然后接入 GameBootstrapper，让玩家可以从新游戏开始完整跑完 3 个阶段。
```

---

## 27. 验收清单

最终 MVP 必须满足：

### 27.1 流程验收

- [ ] 点击开始新游戏后进入 Stage 1。
- [ ] Stage 1 显示剩余时间和活动列表。
- [ ] 玩家选择活动后会扣除时间。
- [ ] 活动可以发放属性、卡牌或道具奖励。
- [ ] 时间耗尽后进入 Boss。
- [ ] Boss 胜利后进入下一阶段。
- [ ] Boss 失败后按配置处理，不直接崩溃或卡死。
- [ ] 完成 Stage 3 后进入结局。

### 27.2 卡牌验收

- [ ] 可以抽牌。
- [ ] 可以打牌。
- [ ] 打牌消耗精力。
- [ ] 学习牌增加挑战进度。
- [ ] 休息牌恢复精力或降低压力。
- [ ] 状态牌会占用手牌。
- [ ] 可以获得新卡。
- [ ] 可以升级或删除卡，若 MVP 暂不做 UI，至少 RewardResolver 支持。

### 27.3 阶段和 Boss 验收

- [ ] 每个阶段都有独立时间限制。
- [ ] 每个阶段都有独立 Boss。
- [ ] Boss 读取玩家属性、卡牌和道具产生影响。
- [ ] Boss 胜利奖励和失败奖励不同。
- [ ] StageData 可以配置下一个阶段。

### 27.4 小游戏验收

- [ ] 小游戏活动能显示结果。
- [ ] 成功率受属性、心情、压力、难度影响。
- [ ] 成功和失败奖励不同。
- [ ] 不需要复杂实时操作。

### 27.5 道具验收

- [ ] 可以通过奖励获得道具。
- [ ] 道具能显示在道具栏。
- [ ] 消耗道具使用后移除。
- [ ] 被动道具能在战斗或 Boss 中生效。
- [ ] 阶段限定道具在阶段结束后清除。

### 27.6 UI 验收

- [ ] UI 显示当前阶段。
- [ ] UI 显示剩余时间。
- [ ] UI 显示学生状态。
- [ ] UI 显示活动列表。
- [ ] UI 显示卡牌挑战。
- [ ] UI 显示奖励。
- [ ] UI 显示结局。

### 27.7 测试验收

- [ ] EditMode 测试可以运行。
- [ ] 核心数值夹取测试通过。
- [ ] 奖励系统测试通过。
- [ ] 牌库系统测试通过。
- [ ] 小游戏成功率测试通过。
- [ ] 阶段系统测试通过。
- [ ] 挑战系统测试通过。

---

## 28. 设计边界

### 28.1 不要把学习做成唯一最优解

如果只学习永远最强，游戏会变得单调。休息、社交、兴趣、家庭沟通都应有长期价值。

### 28.2 父母不是单纯反派

父母系统应表现为期待、资源、沟通和支持的混合，不要只做成惩罚源。

### 28.3 压力系统只做策略表达

压力可以影响抽牌、状态牌、事件选项和小游戏成功率，但不要使用极端、恐怖、伤害化表达。

### 28.4 Boss 失败不应立刻毁掉整局

MVP 中 Boss 失败应带来代价，但仍给玩家继续成长或进入普通结局的机会。

### 28.5 结局不要只看成绩

成绩是重要维度，但不应是唯一成功标准。兴趣、亲子关系、自主性、社交和压力管理也应影响结局。

---

## 29. 后续扩展方向，不属于 MVP

后续可以扩展：

- 小学、初中、高中多个大章节。
- 更多父母性格类型。
- 同学 NPC 关系线。
- 真正可操作的小游戏。
- 多周目代际系统。
- 更复杂的卡牌流派。
- 存档和读档。
- 图鉴和成就。
- 随机种子和 Roguelike 模式。
- 节日事件、竞赛事件、社团路线。

---

## 30. MVP 最终目标

最终交付的第一版应该做到：

```text
玩家可以从 Stage 1 开始，在有限时间内选择活动，通过小游戏、事件和卡牌挑战获得属性、卡牌和道具；每个阶段末挑战 Boss，成功后进入下一阶段；完成 3 个阶段后进入结局。
```

这比固定 12 周流程更适合当前设计，因为它把“成长阶段”“时间限制”“阶段内经营”“Boss 检验”和“卡牌构筑”整合成了一个清晰的可开发结构。
