# 《Foodula 1》AI 系统设计文档

> **文档状态**：Demo 版本设计 · 可指导开发  
> **关联文档**：`foodula-1-concept.md`（主框架）、`foodula-1-core-mechanics.md`（核心机制）  
> **创建日期**：2026-07-13  
> **适用范围**：Demo 快速比赛模式，中等难度 AI
> **实现快照**：2026-08-23；当前运行时为 `AIController` + `AIPlanner`，
> 共享 `ChinaGearShiftRules`，默认一名 AI；`aiOpponentCount` 可配置更多对手。
> P3 尾流策略、个性化难度和完整 Play Mode 调参仍属于未完成项。

---

## 〇、设计原则

Demo 阶段 AI 只需要满足三点：
1. **不出 bug**——不会做非法操作（升 2 档、不付热量等）
2. **不会总是失控**——能基本管理热量
3. **有变化**——不会每回合做完全一样的事，给玩家不同的比赛体验

不需要"最优策略"，不需要"个性 AI"，不需要"学习"——这些都是后续阶段的事。

---

## 一、行为树结构

```
ROOT（每回合执行一次）
│
├─ PRIORITY 1：生存检查
│   ├─ 热量 ≥ 70%？
│   │   ├─ YES → 降档冷却优先
│   │   └─ NO → 继续
│   └─ 手中速度牌不足当前档位所需？
│       └─ YES → 降档至可出牌档位
│
├─ PRIORITY 2：弯道策略
│   ├─ 前方 N 格内有弯道？
│   │   ├─ YES → 检查弯道限速
│   │   │   ├─ 本回合可能超速 → 适当降档 / 不出大牌
│   │   │   └─ 安全通过 → 继续
│   │   └─ NO → 继续
│
├─ PRIORITY 3：尾流利用
│   ├─ 移动后能触发尾流？
│   │   ├─ YES → 优先出足够的牌来接近前车
│   │   └─ NO → 继续
│
└─ PRIORITY 4：常规推进
    ├─ 选择适当档位
    └─ 选择适当的牌打出
```

---

## 二、决策节点详解

### 2.1 生存检查（P1）

这是最高优先级的检查——如果热量太高或者手牌不够，必须先处理。

#### 热量检查

```
IF heatPercentage >= 70%：
    目标：尽快冷却
    → 降档至最低可行档位（优先 1 档）
    → 如果已在 1 档，本回合出最小的可用速度牌（减少行进距离 = 减少弯道风险）
    
ELSE IF heatPercentage >= 50%：
    倾向：适度保守
    → 不升档（保持当前档位或降 1 档）
    → 出牌时优先出小值速度牌
    
ELSE：
    正常决策
```

#### 手牌不足检查

```
所需出牌数 = 当前档位
可用速度牌数 = 手中速度牌数量（不含热量牌）

IF 可用速度牌数 < 所需出牌数：
    → 降档至"可用速度牌数"对应的档位
    → 如果降档后仍不足（极端情况），降至 1 档并尽可能出牌
```

### 2.2 弯道策略（P2）

AI 检查本回合移动路径上会经过的弯道，预测是否会超速。

```
lookAheadDistance = 预估本回合可移动的格数（基于手中可用的速度牌平均值的 × 档位）

对于移动路径上可能经过的每个弯道：
    cornerId = 弯道标识
    cornerLimit = 弯道限速 + 车队操控加成
    estimatedSpeed = 手中速度牌中位数 × 档位出牌数
    
    IF estimatedSpeed > cornerLimit：
        超速风险
        
        IF heatPercentage > 50%：
            策略：降 1 档（减少出牌数 = 降低总移动力）
        ELSE：
            策略：保持当前档位但打法保守——优先出小值速度牌
```

**弯道预判伪代码：**

```python
def evaluate_corner_risk(current_pos, gear, hand, track):
    max_possible_move = sum_top_n_cards(hand, gear)  # 取手中最大的 N 张速度牌之和
    min_possible_move = sum_bottom_n_cards(hand, gear)  # 取手中最小的 N 张速度牌之和
    
    corners_ahead = get_corners_in_range(current_pos, min_possible_move, max_possible_move, track)
    
    for corner in corners_ahead:
        if max_possible_move > corner.effective_limit:
            corner.risk = "high"
        elif min_possible_move > corner.effective_limit:
            corner.risk = "certain"
        else:
            corner.risk = "low"
    
    return corners_ahead
```

### 2.3 尾流利用（P3）

```
IF 前方 ≤ 2 格范围内有对手：
    target_distance = 到前车的距离
    needed_move = target_distance + 1（保证贴上前车，≤ 1 格触发尾流）
    
    IF 手中可以组合出 needed_move：
        → 选择能精确达到 needed_move 的出牌组合
        → 如果触发尾流 +2 后能追上更前的车，更优先
```

### 2.4 常规推进（P4）

如果没有生存威胁、没有弯道风险、也没有尾流机会，正常推进。

#### 选档

```
IF heatPercentage <= 30% AND 手中速度牌 ≥ 3 张：
    → 倾向升档（如果不在 4 档）
    
ELSE IF heatPercentage <= 50%：
    → 保持当前档位
    
ELSE：
    → 如果需要冷却，降 1 档
```

**注意**：由于 Demo AI 不使用中国车队的 2 档系统，上述逻辑适用于 5 支传统车队。中国车队 AI 逻辑见第七节。

#### 出牌

```
候选牌 = 手中所有速度牌
按数值降序排列

IF 前方有对手（且距离 ≤ 5 格）：
    → 倾向出数值较大的牌（追求追赶/超车）
ELSE：
    → 随机选择（引入变化性）
    但保证：所选牌的数值之和 不会导致明确超速（参考 2.2 弯道预判）
```

**变化性注入**：Demo AI 不追求完美，在以下位置加入随机噪音：
- 出牌时，10% 概率随机打乱出的牌的顺序
- 弯道无风险时，20% 概率选择略激进的牌组合（+1~2 移动力）

---

## 三、档位决策总表

| 热量 | 前方弯道风险 | 尾流机会 | 决策 |
|---|---|---|---|
| ≥ 70% | 任意 | 任意 | **降至 1 档**，出最小牌，全力冷却 |
| 50-69% | 有风险 | 有 | 不换档，保守出牌，兼顾尾流 |
| 50-69% | 有风险 | 无 | 降 1 档，出小牌 |
| 50-69% | 安全 | 有 | 不换档，激进出牌追尾流 |
| ≤ 49% | 有风险 | 有 | 不换档，精确打到尾流距离 |
| ≤ 49% | 有风险 | 无 | 降 1 档（或保持 1-2 档），保守 |
| ≤ 49% | 安全 | 任意 | **升档**（如果未到 4 档），大方出牌 |
| ≤ 30% | 安全 | 任意 | 优先升到 4 档，全力冲刺 |

---

## 四、卡牌选择算法

```python
def select_cards(hand, gear, target_move=None):
    """
    hand: 手牌列表
    gear: 当前档位（需打出的牌数）
    target_move: 可选的目标移动力（用于尾流场景）
    """
    speed_cards = [c for c in hand if c.type == "speed"]
    speed_cards.sort(reverse=True)  # 降序排列
    
    if len(speed_cards) < gear:
        # 手牌不足——应由 P1 生存检查处理，理论上不会走到这里
        return speed_cards  # 打出所有可用的
    
    if target_move:
        # 尾流场景：精确匹配目标距离
        return find_closest_combination(speed_cards, gear, target_move)
    else:
        # 常规场景：取最大的 N 张
        return speed_cards[:gear]

def find_closest_combination(cards, count, target):
    """在 cards 中选择 count 张，使它们的和最接近 target（≥ target 优先）"""
    from itertools import combinations
    best = None
    best_diff = float('inf')
    
    for combo in combinations(cards, count):
        total = sum(c.value for c in combo)
        if total >= target:
            diff = total - target
        else:
            diff = (target - total) * 2  # 惩罚小于目标的情况
        
        if diff < best_diff:
            best_diff = diff
            best = combo
    
    return list(best) if best else cards[:count]
```

---

## 五、车队特有 AI 行为

Demo 阶段的 AI 对手使用简化版车队特性：

| 车队 | Demo AI 行为差异 |
|---|---|
| 🇬🇧 英国 | 技能效果暂不启用（Demo 无技能树） |
| 🇩🇪 德国 | 失控后降为 1 档即可（不跳回合）。弯道策略略保守（利用高耐久） |
| 🇮🇹 意大利 | 弯道策略较激进（操控 +2 让大多数弯道安全）。直道上略保守 |
| 🇺🇸 美国 | 弯道策略非常保守（操控 -1 + 弯道惩罚）。直道上最大档位冲刺 |
| 🇨🇳 中国 | 使用专属 2 档 AI 逻辑（见第七节） |
| 🇯🇵 日本 | 秘方牌随机打出（不进行策略评估），出牌风格偏激进 |

---

## 六、最后一圈行为

当进入最后一圈时，AI 增加"冲刺倾向"：

```
IF lap == total_laps：
    热量阈值提高 15%（原本 50% 警告 → 65% 警告）
    弯道风险容忍度 +1（允许轻微超速过弯，负担得起的范围内）
    → 整体行为比正常圈更激进
```

---

## 七、中国车队专属 AI（2 档系统）

中国车队的 AI 与其他 5 支车队使用不同的决策逻辑，因为它的 2 档系统与传统的 4 档系统完全不兼容。

### 档位决策

```
当前运行时不维护 battery_capacity；电池每圈衰减属于设计目标，未接入比赛状态。
Go/Recover 的连续使用和热量代价由 `ChinaGearShiftRules` 统一计算。

// 未来设计：电池阈值驱动的 AI 进站选择尚未接入
// 当前运行时只由玩家在 authored pit_entry 处选择进站；AI 电池策略待实现。

IF 热量 ≥ 60%：
    → 选 Recover 档（连续 Recover 冷却 3→2→1→0 + 出 1 张牌）
    如果连续 R 惩罚严重，仅选 1 回合 R 后切回 Go

ELSE IF 前方有长直道（≥ 5 格直道）：
    → 选 Go 档（3 张牌冲刺）
    检查连续 Go 惩罚：第 1 次 Go 出 3 张，之后出 4 张并按连续次数产生 1/2/3 热量

ELSE：
    → Go ↔ Recover 交替（最优节奏）
    如果上回合是 Go → 本回合 Recover
    如果上回合是 Recover → 本回合 Go
```

### 出牌策略

```
Go 档（3 张牌）：正常选最大的 3 张速度牌（同标准 AI）
Recover 档（1 张牌）：选中间值速度牌（不太快不过弯，不太慢不浪费）
```

---

## 八、待优化项（非 Demo 范围）

以下功能是 Demo 完成后可逐步加入的：

| 功能 | 阶段 | 说明 |
|---|---|---|
| 技能树 AI | Phase 2 | AI 在赛前选择 build，比赛中使用技能 |
| 个性 AI | Phase 3 | 不同车手有不同的风险偏好和行为模式 |
| 难度分层 | Phase 2 | 简单/中等/困难使用不同阈值 |
| 学习/记忆 | Phase 3 | AI 记住之前的比赛结果，调整策略 |
| 多人填补 | Demo+ | 超时或掉线时 AI 代行 |

---

## 九、Unity 实现提示

### 文件结构建议

```
Assets/Scripts/AI/
├── AIController.cs           // 主控制器，每回合调用
├── AIPlanner.cs              // 纯规则决策与出牌计划
├── ChinaGearShiftRules.cs    // 中国 Go/Recover 连续档位规则
└── AIController.cs           // Unity 回合编排与状态注入
```

### AIController 伪代码

```csharp
public class AIController : MonoBehaviour
{
    public void TakeTurn()
    {
        var state = GetGameState();
        
        // P1: 生存检查
        if (state.heatPercentage >= 0.7f)
            EmergencyCoolDown(state);
        
        // P2: 弯道预判
        var cornerRisk = EvaluateCorners(state);
        
        // P3: 尾流检查
        var slipstreamTarget = EvaluateSlipstream(state);
        
        // P4: 选档 + 出牌
        var gear = SelectGear(state, cornerRisk, slipstreamTarget);
        var cards = SelectCards(state, gear, slipstreamTarget);
        
        // 提交决策
        SubmitDecision(gear, cards);
    }
}
```

### 需要从 GameManager 获取的信息

AI 需要访问以下游戏状态：

| 信息 | 来源 | 用途 |
|---|---|---|
| 当前热量 | `currentHeat` | 生存检查 |
| 当前位置 | `currentPosition` | 弯道预判、尾流 |
| 手牌列表 | `hand` | 出牌选择 |
| 当前档位 | `currentGear` | 选档基础 |
| 赛道数据 | `trackCells[]` | 弯道位置和限速 |
| 所有玩家位置 | `allPlayerPositions[]` | 尾流和追赶目标 |
| 当前圈数 | `currentLap` | 最后一圈判断 |
| 车队类型 | `teamType` | 选中国 AI 还是标准 AI |
