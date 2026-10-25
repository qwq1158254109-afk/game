# 扭蛋抽卡（随机卡包）设计文档

- 日期：2026-10-24
- 状态：**已确认**（入口改为实体扭蛋机 + 独立抽奖界面，2026-10-24 用户拍板）
- 定位：商店的金钱消费口 + 开包爽感时刻；DBG 获卡第三渠道（前两个：商店直购、战后三选一）
- 决策记录：采用**单发扭蛋**形态而非"翻牌三选一"——爽感来自翻开瞬间（运气爽），
  稀释放慢卡组膨胀靠"每次只进一张"解决，而不是靠让玩家选（那是策略爽，另一种情绪）。
  入口不做在售货员菜单，做在商店地图的**实体扭蛋机交互点**上，配可连抽的独立界面。

---

## 1. 现状依据（实测）

| 项 | 事实 | 来源 |
| --- | --- | --- |
| 一局收入 | 3 个 Boss × 50 = 150 金 | MvpSampleDataFactory Boss 奖励 |
| 单卡价格 | 按稀有度兜底 50 / 90 / 150 | ShopPurchaseService.ResolveCardPrice |
| 奖励卡池 | pool_reward 共 36 张：普通 20 / 非凡 10 / 稀有 6 | 2026-10-24 脚本实测 |
| 稀有池 | 熬夜冲刺、深度专注、模拟排名、家长信、完美笔记、成果展示 | 同上 |
| 卡背/扭蛋美术 | **无现成素材** | Assets/Art 检索 |
| 开门机制 | 商店 = ActivityType.Shop → shouldOpenShop → ShowShopIfNeeded | LocationActionExecutor / MapInteractionFlowController |
| 场景保护 | 商店对象由 NewArtShopMapBuilder 只验证不重建，手改可存活 | NewArtShopMapBuilder Require 检查 |

## 2. 玩家流程

```text
商店地图 → 扭蛋机（收银台旁的实体交互点）按 E
  ↓ 走活动管线（ActivityType.Capsule，timeCost 0、不限次数）
扭蛋抽奖界面（独立面板，可连抽不关窗）
  ├─ 「抽一次（40 金）」：金钱不足时置灰并显示原因
  │    ↓ 投币 → 胶囊摇晃 → 爆开 → 卡背停顿（时长暗示品质）→ 翻面揭示
  │    ↓ 结果：卡图+名称+费用+效果 +「已加入卡组」/「重复卡·返还 15 金」
  │    ↓ 「再抽一次」回位，可继续
  └─ 「离开」关闭 → 恢复移动
不消耗时间段、不计交互次数（与商店一致）；演出中点击可跳过
```

## 3. 数值设计

| 参数 | 值 | 理由 |
| --- | --- | --- |
| 单抽价格 | **40 金** | 期望价值 ≈ 0.65×50 + 0.27×90 + 0.08×150 = **68.8 金 > 40**：随机折价，确定性要花钱买；一局最多抽 3 次，与单买卡形成预算竞争 |
| 概率 | 普通 **65%** / 非凡 **27%** / 稀有 **8%** | 稀有一局约 0.24 次期望——出现就是事件 |
| 首抽保底 | 每局**首次**抽卡必得非凡以上 | 让玩家一定尝到甜头；状态进存档防读档刷 |
| 重复卡 | **保留 + 返还 15 金** | DBG 里核心卡多张是合法构筑；重复不白给也不恶心 |
| 入组方式 | 直接进主卡组 | 与商店买卡同口径 |

## 4. 抽奖界面与演出（2026-10-24 改版：街机全屏机台）

**界面**：没有"面板"——整屏压黑，中央一台大扭蛋机（440×590 码绘机台，48×64 点阵放大），
机器本身就是按钮；左上金钱、底部一行提示、右上离开。演出素材全部程序生成
（机台/旋钮/金币/扭蛋/蛋壳/集中线），真实美术到位后换 Sprite 引用即可。

**演出链**（点击机器触发，演出中再点 = 跳到结果）：

| 阶段 | 内容 | 时长 |
| --- | --- | --- |
| 投币 | 金币抛物线落入投币口，自旋 720° | 0.45s |
| 旋钮 | 旋钮转 540°（三声咔哒），玻璃罩内扭蛋翻滚 | 0.7s |
| 出蛋 | 扭蛋从出蛋口掉落，弹跳两次 | 0.55s |
| 摇蛋 | 蛋体震动渐强 | 0.5s |
| **漏光预告** | 蛋缝漏光：白=普通 / 蓝=非凡 / 金=稀有（稀有附机台低鸣震动） | 0.3 / 0.8 / 1.4s |
| 炸壳 | 蛋壳两半飞旋而出 + 集中线放射 + 粒子（稀有加全屏金闪） | 0.6s |
| 卡牌 | 卡背弹出 → 水平翻转显形 → 名称/费用/入组提示 | 0.5s |

音效 V1 复用现有 Sfx.Ui / Sfx.World；专属开包音效后补。

## 5. 技术方案

### 5.1 数据结构

```csharp
// NPC/CapsulePackData.cs（独立文件，SO 落资产——共文件 SO 必坏的教训）
[CreateAssetMenu(menuName = "Game Data/Capsule Pack Data")]
public class CapsulePackData : ScriptableObject
{
    public string packId;
    public int price;            // 40
    public int commonWeight;     // 65
    public int uncommonWeight;   // 27
    public int rareWeight;       // 8
    public CardRarity pityMinRarity; // Uncommon（首抽保底线）
    public int duplicateRefund;  // 15
}
```

### 5.2 抽卡服务（纯逻辑，可测）

`CapsuleDrawService.Roll(pack, session, poolCards, rng)`：
1. 保底未用 → 从「非凡∪稀有」子池抽；用掉保底标记；
2. 否则按权重掷品质 → 对应品质子池内均匀抽一张；
3. 卡已在主卡组 → 标记重复（返现由调用方结算）。
子池按 pool_reward 的 36 张现算（20/10/6）。

### 5.3 接线机制（与商店同构）

```
ActivityType 枚举末尾追加 Capsule（安全：只追加不改序）
→ 扭蛋活动资产（activity_shop_capsule_gacha：type=Capsule、timeCost=0、maxUsesPerStage=-1）
→ LocationActionExecutionResult.shouldOpenCapsule
→ MapInteractionFlowController.ShowCapsuleIfNeeded(result)（挂在 ShowShopIfNeeded 旁）
→ CapsuleView.Open(session)
```

### 5.4 场景：扭蛋机对象（编辑器构建器生成）

`Editor/CapsuleMachineBuilder.cs`（菜单 Tools/Student Growth/Shop/Build Capsule Machine，可批处理）：
- 程序生成 24×32 像素扭蛋机贴图（圆顶 + 彩色扭蛋 + 机身投币口）→ PNG 资产（16 PPU、Point、无压缩）；
- 在 ShopMapScene 创建 `CapsuleMachineObject`：SpriteRenderer + BoxCollider2D（阻挡）
  + MapInteractionPoint（location_shop / capsule_gacha）+ MapInteractionTarget
  （fallbackActivity=扭蛋活动、consumeTimeOnInteraction=0、requireFacing=0、range 2.0）；
- 位置取收银台旁空位（构建时以 ShopCounterObject 为参照定位）；
- 商店构建器只验证不重建既有对象，扭蛋机落地后可存活。

### 5.5 改动文件清单

| 文件 | 改动 |
| --- | --- |
| `Enums.cs` | ActivityType 末尾 += `Capsule` |
| `Flow/LocationActionExecutor.cs` | result += `shouldOpenCapsule` |
| `Map/MapInteractionFlowController.cs` | `ShowCapsuleIfNeeded` + 分发点一行 |
| `NPC/CapsulePackData.cs`（新） | 上节 SO |
| `NPC/CapsuleDrawService.cs`（新） | 抽卡纯逻辑 |
| `UI/Map/CapsuleView.cs`（新） | 抽奖界面 + 演出（协程动效，同 DialogueView 模式） |
| `GameSessionState.cs` | `capsulePityUsed` bool |
| `Save/SaveModels.cs` + `SaveGameCodec.cs` | 保底状态进出存档 |
| `Editor/CapsuleMachineBuilder.cs`（新） | 贴图 + 场景对象 + 活动/卡包资产 |
| `Tests/EditMode/CapsuleDrawTests.cs`（新） | 见 §7 |

（相比菜单入口方案：不再动 DialogueData/DialogueRunner/DialogueView/售货员菜单。）

### 5.6 明确不做（本轮）

- 豪华五连（演出素材复用后下轮加）；
- 专属卡背 / 扭蛋壳 / 开包音效（程序占位，美术后补）；
- 删卡、升级与卡池的联动（独立任务）。

## 6. 存档影响

新增 `capsulePityUsed` 一个 bool，随 SaveGameRecord 进出；旧档缺字段 = false = 保底可用，无害。

## 7. 验收标准

1. 商店地图出现扭蛋机（占位贴图），按 E 打开抽奖界面，可连抽、可离开；
2. 抽卡扣 40 金、卡入主卡组；重复卡同时返 15 金；金钱不足按钮置灰并显示原因；
3. 每局首抽必为非凡或稀有，存档读回后保底状态正确；
4. EditMode 模拟 10000 次：三档频率与 65/27/8 偏差 ≤ 2 个百分点；
5. 演出期间锁输入，点击可跳过，结束后可移动；
6. 不消耗时间段、不计交互次数；
7. 编译 0 error；体检 0 阻塞；EditMode 失败集合与基线完全一致（新增测试全绿）。
