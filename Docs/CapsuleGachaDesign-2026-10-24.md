# 扭蛋抽卡（随机卡包）设计文档

- 日期：2026-10-24
- 状态：**待确认**（确认后按此实现）
- 定位：商店的金钱消费口 + 开包爽感时刻；DBG 获卡第三渠道（前两个：商店直购、战后三选一）
- 决策记录：采用**单发扭蛋**形态而非"翻牌三选一"——爽感来自翻开瞬间（运气爽），
  稀释放慢卡组膨胀靠"每次只进一张"解决，而不是靠让玩家选（那是策略爽，另一种情绪）。

---

## 1. 现状依据（实测）

| 项 | 事实 | 来源 |
| --- | --- | --- |
| 一局收入 | 3 个 Boss × 50 = 150 金 | MvpSampleDataFactory Boss 奖励 |
| 单卡价格 | 按稀有度兜底 50 / 90 / 150 | ShopPurchaseService.ResolveCardPrice |
| 奖励卡池 | pool_reward 共 36 张：普通 20 / 非凡 10 / 稀有 6 | 2026-10-24 脚本实测 |
| 稀有池 | 熬夜冲刺、深度专注、模拟排名、家长信、完美笔记、成果展示 | 同上 |
| 卡背/扭蛋美术 | **无现成素材** | Assets/Art 检索 |
| 动效库 | LitMotion 2.0.2 已装 | Packages/manifest.json |
| 入口机制 | DialogueOption 动作位（openShop 等）本轮刚建好，可平移 | ff940da / 70b868d |

## 2. 玩家流程

```text
商店地图 → 售货员按 E → 菜单第 5 项「扭蛋抽卡（40 金）」
  ↓（金钱 < 40 时置灰，提示「未满足：金钱 ≥ 40」——复用现有条件置灰，零新代码）
确认选项 → 对话收起 → 扭蛋演出界面
  ↓ 投币 → 胶囊摇晃 → 爆开 → 卡背停顿（按时长暗示品质）→ 翻面揭示
  ↓ 结果：卡图 + 名称 + 费用 + 一行效果 + 「已加入卡组」/「重复卡·返还 15 金」
任意点击关闭 → 恢复移动（不消耗时间段、不计交互次数，与商店一致）
```

## 3. 数值设计

| 参数 | 值 | 理由 |
| --- | --- | --- |
| 单抽价格 | **40 金** | 期望价值 ≈ 0.65×50 + 0.27×90 + 0.08×150 = **68.8 金 > 40**：随机折价，确定性要花钱买；一局最多抽 3 次，与单买卡形成预算竞争 |
| 概率 | 普通 **65%** / 非凡 **27%** / 稀有 **8%** | 稀有一局约 0.24 次期望——出现就是事件 |
| 首抽保底 | 每局**首次**抽卡必得非凡以上 | 让玩家一定尝到甜头；状态进存档防读档刷 |
| 重复卡 | **保留 + 返还 15 金** | DBG 里核心卡多张是合法构筑；重复不白给也不恶心 |
| 入组方式 | 直接进主卡组 | 与商店买卡同口径 |

## 4. 演出分镜（V1 全程序生成，无美术依赖）

| 时刻 | 事件 |
| --- | --- |
| 0.0s | 半透明黑幕 + 中央胶囊占位（像素圆块）+ 投币音（现有 Sfx） |
| 0.3–1.0s | 胶囊左右摇晃（LitMotion）+ 咔哒声 |
| 1.0s | 爆开：程序生成像素粒子 + 卡背弹出放大至约 460×640 |
| 停顿 | **品质在这里预告**：普通 0.3s 无特效；非凡 0.8s 蓝色描边脉冲；稀有 1.4s 金色粒子环绕 + 屏幕微震 + 蓄力停顿（"最后一张金卡"效应，爽感核心） |
| 翻面 | 0.25s 水平缩放翻转（1→0→1 换图）；稀有额外全屏金光一闪 |
| 结果 | 卡图 + 名称 + 费用 + 效果摘要 + 入组提示 |
| 收尾 | 任意点击 / ESC 关闭，恢复移动 |
| **跳过** | 演出中任意点击直接跳到结果帧（重复观看不折磨人） |

音效 V1 全部复用现有 Sfx.Ui / Sfx.World；专属开包音效后补。

## 5. 技术方案

### 5.1 数据结构

```csharp
// NPC/CapsulePackData.cs（独立文件，SO 落资产——共文件 SO 必坏的教训）
[CreateAssetMenu(menuName = "Game Data/Capsule Pack Data")]
public class CapsulePackData : ScriptableObject
{
    public string packId;              // 稳定 ID，进存档用
    public int price;                  // 40
    public int commonWeight;           // 65
    public int uncommonWeight;         // 27
    public int rareWeight;             // 8
    public Rarity pityMinRarity;       // Uncommon（首抽保底线）
    public int duplicateRefund;        // 15
}
```

### 5.2 抽卡服务（纯逻辑，可测）

`CapsuleDrawService.Roll(pack, session, poolCards, seededRandom)`：
1. 保底未用 → 从「非凡∪稀有」子池抽；用掉保底标记；
2. 否则按权重掷品质 → 在对应品质子池内均匀抽一张；
3. 卡已在主卡组 → 标记重复（返现由调用方结算）。
子池按 `pool_reward` 的 36 张现算（20/10/6），稀有池 6 张抽到重复属正常。

### 5.3 改动文件清单

| 文件 | 改动 |
| --- | --- |
| `Dialogue/DialogueData.cs` | DialogueOption += `openCapsule` 动作位 |
| `Dialogue/DialogueRunner.cs` | pendingOpenCapsule + Consume/Clear（照抄 openShop 模式） |
| `Map/MapInteractionFlowController.cs` | EndDialogue 分支 + `OpenCapsuleAfterDialogue()`（镜像 OpenShopAfterDialogue） |
| `Dialogue/DialogueView.cs` | MaxVisibleOptions 4 → **5**（5 项菜单卡高 536px，底部锚定下顶边 960 < 1080，放得下） |
| `NPC/CapsulePackData.cs`（新） | 上节 SO |
| `NPC/CapsuleDrawService.cs`（新） | 抽卡纯逻辑 |
| `UI/Map/CapsuleView.cs`（新） | 演出界面（代码构建，同 DialogueView 模式；LitMotion 动效） |
| `GameSessionState.cs` | `capsulePityUsed` bool |
| `Save/SaveModels.cs` + `SaveGameCodec.cs` | 保底状态进出存档（旧档缺字段 = false = 保底可用，无害） |
| `Dialogue_npc_shop_owner_Menu.asset` | 手改加第 5 项「扭蛋抽卡（40 金）」：openCapsule + requirements 金钱 ≥ 40 |
| `Assets/Resources/StudentGrowth/NPC/capsule_pack_default.asset` | 默认卡包配置资产 |
| `Tests/EditMode/CapsuleDrawTests.cs`（新） | 见 §7 |

### 5.4 明确不做（本轮）

- 豪华五连（一次翻五、低→高揭示、留一张）——演出素材复用后下轮加；
- 商店地图实体扭蛋机摆件（要美术）；
- 专属卡背 / 扭蛋壳 / 开包音效（程序占位，美术后补）；
- 删卡、升级与卡池的联动（`RemoveCard` 零来源问题独立成任务，扭蛋会放大它的必要性但不阻塞本任务）。

## 6. 存档影响

新增 `capsulePityUsed` 一个 bool，随 SaveGameRecord 进出；其余（扣钱、入组、返现）走现有会话状态，无需新字段。

## 7. 验收标准

1. 售货员菜单 5 项全显示；金钱 < 40 时扭蛋项置灰并显示「未满足：金钱 ≥ 40」；
2. 抽卡扣 40 金、卡入主卡组；重复卡同时返 15 金；
3. 每局首抽必为非凡或稀有，存档读回后保底状态正确；
4. EditMode 模拟 10000 次：三档频率与 65/27/8 偏差 ≤ 2 个百分点；
5. 演出期间锁输入，点击可跳过，结束后可移动；
6. 不消耗时间段、不计交互次数；
7. 编译 0 error；体检 0 阻塞；EditMode 失败集合与基线完全一致（新增测试全绿）。
