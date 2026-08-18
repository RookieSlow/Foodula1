# 模块接入指南 — 面向其他 AI 的操作手册

> 更新: 2026-08-05
> 关联: ADR-002（分层纯函数架构）、`design/planning/roadmap.md`
> 用途: 其他 Claude Code 子代理（或人类开发者）开发新模块 / 接入现有模块时的唯一入口文档。

---

## 1. 一句话架构

```
┌─────────────────────────────────────────────────────────────┐
│ MonoBehaviour 层（编排）: MVPGameManager / AIController /     │
│   TrackManager / HUDUI / CardHandUI / CardUI / TechTreeUI    │
│   —— 只做：等待输入、驱动协程、调用纯函数、刷新 UI            │
│   RaceUIFactory —— 只负责旧场景的程序化 HUD/手牌构建          │
├─────────────────────────────────────────────────────────────┤
│ RaceSession（纯 C# 聚合层，2026-08-03 新增）                 │
│   —— 单场比赛状态：玩家列表、天气、科技/特技数据库、排名       │
├─────────────────────────────────────────────────────────────┤
│ 纯函数规则层（无 Unity 依赖，可 EditMode 测试）:             │
│   RaceRules / TrackRules / WeatherRules / PitLaneRules /     │
│   TrickCardRules / TechTreeRules / RaceRanking / AIPlanner   │
├─────────────────────────────────────────────────────────────┤
│ 数据层: GameConfigSO / TrackConfig(JSON) / CardData /        │
│   PlayerState / 各类 *Data.cs                                 │
└─────────────────────────────────────────────────────────────┘
```

**铁律（ADR-002）**：任何游戏规则计算不得写在 MonoBehaviour 里。先在纯函数层
实现 + 写 EditMode 测试，再在 `MVPGameManager` 里接线。

---

## 2. 5 个核心系统现状（2026-08-03 全部已接入比赛循环）

| 系统 | 文件 | 接入状态 | 说明 |
|------|------|---------|------|
| 多车 | `RaceRanking.cs` | ✅ 完整 | N 车排名/回合顺序/完赛判定 |
| 天气 | `WeatherData.cs` `WeatherRules.cs` | ✅ 完整 | 开局抽天气 + 每圈 30% 换天，雨天弯道限速 -1 |
| 维修区 | `PitLaneRules.cs` | ✅ 完整 | 经过 `pit_entry` 选择进站，冷却全部热量、停 1 回合 |
| 特技牌 | `TrickCardData.cs` `TrickCardRules.cs` | ✅ 完整 | 4 张（2攻2守）洗入普通牌组，每回合限 1，单张确认后即时结算并弃置 |
| 科技树 | `TechTreeData.cs` `TechTreeRules.cs` `TechTreeDatabase.cs` `TechTreeProfileStore.cs` | ✅ UI + 持久化 + 数值接入 | 主菜单入口、按车队保存 RP/解锁/激活状态，比赛读取有效修正；AI 保留 demo 配置 |
| 中国双档 | `ChinaGearShiftRules.cs` `TeamGearRules.cs` | ✅ 比赛循环接入 | Go/Recover 独立出牌数、连续档位热量/冷却链，玩家与 AI 共用同一纯规则模块 |

未接入（文档化 TODO，见 §8）：尾流系统（slipstream）、地标完整机制（US L3
MotherRoad）、SchwarzbierFuel 主动激活、FullEnglish、SunNeverSets 目标选择、
BrothSelection 开局选择 UI、SmokedBBQ 热量当速度用。

---

## 3. 新增文件速查（2026-08-03）

| 文件 | 职责 |
|------|------|
| `Assets/Scripts/Core/RaceSession.cs` | **新模块唯一需要知道的类**。比赛状态 + 跨系统规则粘合 |
| `Assets/Scripts/Core/ChinaGearShiftRules.cs` | 中国队 Go/Recover 纯规则：连续计数、4 张超频、内置冷却 |
| `Assets/Scripts/Core/TeamGearRules.cs` | 标准四档与中国双档的统一门面，管理器不直接分支规则细节 |
| `Assets/Scripts/Core/TeamVehicleRules.cs` | 车队基础性能/耐久配置边界，供比赛初始化和后续平衡调整使用 |
| `Assets/Scripts/Core/RaceMovementRules.cs` | 环形赛道超车判定纯规则；跳过回合策略由 `MVPGameManager` 注入 |
| `Assets/Scripts/Core/RaceLaneRules.cs` | 同节点车辆的内/外线占用规则；车队赛道车道数量仍由 `TrackPresentationRules` 提供 |
| `Assets/Scripts/Core/RaceInputState.cs` | 档位、卡牌、弃牌、印地换道和维修区选择的互斥输入门控；不持有 UI/场景引用，由回合协程与回调共同驱动 |
| `Assets/Scripts/Core/RacePhaseState.cs` | 比赛阶段状态机与输入可接受性；只管理 WaitingForGear/WaitingForCards/Animating/GameOver 转换，不执行协程副作用 |
| `Assets/Scripts/Core/RaceTurnRules.cs` | 回合跳过与终止状态的参与资格判定；A1 已消费的跳过集合由管理器传入，规则层不修改玩家状态 |
| `Assets/Scripts/Core/RaceWeatherState.cs` | 每圈天气掷骰的一次性门控；天气池选择和实际天气变化仍由 `RaceSession`/`WeatherRules` 负责 |
| `Assets/Scripts/Core/RaceTestLogWriter.cs` | 手动测试日志持久化适配器；HUD 事件、回合快照、档位、玩家/AI 速度牌、特技牌和移动计划写入 `persistentDataPath/race-logs`，`GetDefaultDirectory()` 供测试工具定位，文件失败不阻断比赛 |
| `Assets/Scripts/Core/RaceLapRules.cs` | 起终点过线后的圈数递增与完赛边界；天气、科技和 UI 仍由管理器编排 |
| `Assets/Scripts/TechTree/TechTreeProfileStore.cs` | PlayerPrefs JSON 适配层；纯科技规则与存档/UI 解耦 |
| `Assets/Scripts/UI/TechTreeUI.cs` | 运行时构建的车队科技树界面，不依赖 Race 场景 |
| `Assets/Scripts/UI/RaceUIFactory.cs` | RaceCanvas 缺失时的程序化 HUD/手牌构建；只接收回调，不持有比赛状态 |
| `Assets/Scripts/Gameplay/CarOrientationController.cs` | 车辆朝向的 Unity Transform 适配；角度规则由 `CarOrientationRules` 纯函数提供 |
| `Assets/Scripts/Gameplay/CarMovementAnimator.cs` / `CarMovementRules.cs` | 节点间车辆插值与到达阈值适配；`MVPGameManager` 保留圈数、弯道和摄像机编排 |
| `Assets/Scripts/Gameplay/TeamCarPresentationRules.cs` | 车队赛车精灵槽位与备用颜色映射；避免外观身份逻辑散落在比赛编排器中 |
| `Assets/Scripts/Core/PlayerState.cs` | 新增 `techState` / `trickState` / `extraCardSlotsThisTurn` / `cornerTotalThisTurn` 等 |
| `Assets/Scripts/Core/CardDeck.cs` | 特技牌与速度牌共用抽牌/弃牌循环（`AddTrickCardsToDrawPile` / `GetTricksInHand` / `DiscardTrickCard` / `DiscardPlayableCardsFromHand`） |
| `Assets/Scripts/Core/CardPlayRules.cs` | 速度牌单张/多选确认的纯规则：校验精确手牌所有权与本回合出牌上限后原子移入已打出区 |
| `Assets/Scripts/Core/CardData.cs` | 新增 `isTemp`（限时热量牌）与 `CreateTempHeat()` |
| `Assets/Scripts/Config/GameConfigSO.cs` | 新增 `aiOpponentCount` / `playerTeam` / `aiTeams` / 4 个系统开关 |
| `Assets/Scripts/Core/MVPGameManager.cs` | 比赛循环重构为 N 玩家 + 5 系统接线 |

---

## 4. 比赛循环时序（接入点地图）

`MVPGameManager.GameLoop()` 每回合按以下相位执行。**新模块若要在比赛中生效，
必须明确挂到其中一个相位**：

```
回合开始
  ├─ ClearTurnState + trickState.ResetPerTurn + techState.ResetPerTurn
  │   （extraCardSlotsThisTurn 在此从 ConsumeKantoOden 恢复）
  ├─ TickBankuruwaseForAll()          ← JP L3 科技钩子
  ├─ turnOrder = RaceRanking.GetTurnOrder()   ← 末位先行
  │
  ├─ PHASE A1 档位决策   （人类等 UI；AI 用 AIController.DecideGear）
  ├─ PHASE A2 抽牌       （手牌上限 = EffectiveHandSize + extraSlots）
  ├─ PHASE A3 AI 特技牌  （DecideAITrick 启发式；成功后立即弃置并结算）
  ├─ PHASE A4 速度牌出牌 （人类可选 1 张或多张 → 确认；特技牌始终单张即时结算）
  │                       （无选择时点击按钮结束出牌阶段）
  ├─ ComputeMovements    ← 科技直道加成 / 酸菜 / 寿司 / 鱼雷 / 火锅底料在此汇总
  │
  ├─ PHASE B 执行（按 turnOrder 逐个）:
  │   ├─ AnimateMovement（过线 → OnPlayerCrossedStartFinish：圈数/完赛/换天）
  │   ├─ ReactStep       （档位冷却 + 汤底/万骨涌冷却）
  │   ├─ ResolveCorners  ← 弯道判定：EffectiveCornerLimit（科技+天气）→ 热量支付
  │   └─ 维修区检测       （CrossedPitEntry → 玩家弹窗 / AI 启发式）
  │
  ├─ 弃牌（仅人类）
  ├─ CleanupTurn         ← 阴阳茶 / 点心连击 / 烤肉拼盘 / 限时牌销毁
  ├─ CheckGameEnd        （人类完赛或全员完赛/爆缸）
  └─ HUD 刷新
```

---

## 5. 接入一个新模块的步骤（7 步法）

1. **找对层**：规则写进纯函数静态类（无 UnityEngine using），状态写进
   `PlayerState` 或 `RaceSession`，UI 写进 `HUDUI` / `CardHandUI`。
2. **加配置**：`GameConfigSO` 加开关字段（默认值尽量不影响现有玩法），
   管理器里 `if (config.enableXxx)` 包裹。
3. **写测试**：在 `Assets/Tests/Editor/` 加 `xxx_test.cs`，命名
   `test_系统_场景_期望结果`，纯逻辑测试不依赖场景（用 `SystemRandomSource(seed)`
   保证确定性）。
4. **接线**：在 `MVPGameManager` 找对应相位（§4），调用纯函数、改 `PlayerState`。程序化 UI
   只通过 `RaceUIFactory` 构建，禁止把新的 GameObject 创建逻辑直接塞回 Manager。
   注意：
   - **跳过回合**：永远用 `ShouldSkipTurn(p)` / 回合级 `turnSkipped` 集合，
     不要在多个相位重复判断同一标志的"是否已清除"状态。
   - **弯道判定用 `p.cornerTotalThisTurn`，实际移动用 `p.totalMovementThisTurn`**
     （火锅底料的 +1 不计入弯道判定）。
   - **热量支付必须走 `TryPayHeat()`**（黑面包垫底/炸鱼薯条/烤肉拼盘都在这里挂钩）。
5. **UI 接线**：HUD 文本字段为 nullable（Prefab 模式未赋值则跳过显示）；
   新 UI 元素在 `AutoCreateUI()` 里创建，Prefab 用户自己拖。
6. **手动冒烟**：Unity 里跑一场比赛（`Race.unity`），验证日志输出。
7. **更新文档**：本文件 §2/§8 + `active.md` + 相应 GDD 的 Acceptance Criteria。

---

## 6. 关键 API 参考

### 6.1 RaceSession（新模块的主入口）

```csharp
session.Players                    // List<PlayerState>，[0] 恒为人类
session.Weather / WeatherPool      // 当前天气 / 赛道天气池
session.TrickDb / TechDb           // 特技牌 / 科技树数据库
session.GetRankings() / GetTurnOrder() / IsRaceOver()
session.GetRank(p) / AssignFinish(p)
session.InitializeWeather(pool, default) / RollWeatherForLap() / WeatherLabel
session.CreateDemoTechState(teamId) // demo 预算 + 解锁 L1 + 全部激活
session.GetModifiers(p)            // → TechModifiers（数值修正的唯一入口）
session.EffectiveHandSize(p, base) / EffectiveHeatPoolSize(p, base) / EffectiveSpinMax(p)
session.EffectiveCornerLimit(p, baseLimit)  // 科技 + 天气
session.ConsumeHeatReduction(p)    // 每圈 1 次弯道超速减免
session.PlayTrick(p, card)         // → TrickPlayResult（校验 + 置回合标志）
session.CreateInitialTrickCards(teamId)
session.ComputeMovementBonus(p, crossedCorner)
session.ResolveEndOfTurn(p)        // → YinYangResult（CN L1）
session.GetGrillSpezialCooldown(p) / ActivateGrillSpezial(p) / TrackHeatPaid(p, n)
```

### 6.2 PlayerState 新增字段

```csharp
p.techState                 // TechTreeState（enableTechTree=false 时为 null）
p.trickState                // TrickCardState（每回合 ResetPerTurn）
p.trickMoveBonusThisTurn    // 特技牌即时移动（司康 +2）
p.cornerTotalThisTurn       // 弯道判定用速度（不含火锅底料 +1）
p.extraCardSlotsThisTurn    // 额外出牌槽（关东慢煮累积）
p.kantoOdenSkipThisTurn     // 关东慢煮：本回合跳过（回合开始清除）
p.positionAtTurnStart       // 失控回退 / 阴阳茶结算基准
```

### 6.3 热量支付挂钩（重要）

所有热量扣减必须经过公开的 `MVPGameManager.TryPayHeat()`。人类与 AI 都必须
复用这条规范路径，避免绕过特技、科技与热量追踪挂钩。挂钩点：

| 系统 | 挂钩方式 |
|------|---------|
| 黑面包垫底（DE 特技） | `TrickCardRules.ApplySchwarzbrot(p.trickState, amount)` |
| 炸鱼薯条（UK L1） | `TechTreeRules.CanUseFishAndChips / UseFishAndChips` |
| 烤肉拼盘（DE L3） | `session.TrackHeatPaid(p, drawn)` |
| 点心连击（CN L2） | `TechTreeRules.TrackDimSumCombo(state, false, false, true)` |

### 6.4 特技牌效果应用（TrickPlayResult → 管理器动作）

| 字段 | 管理器动作 |
|------|-----------|
| `heatToPay` | `TryPayHeat`（司康；失败→失控） |
| `heatToCool` | `deck.RemoveHeatFromHand`（红茶/关东慢煮；永久热量回池，限时热量销毁） |
| `extraMovement` | `p.trickMoveBonusThisTurn += n` |
| `cardsToDraw` | `deck.DrawToHand(HandCount + n)`（可乐） |
| `requiresSpeedDiscard` | 弃最小速度牌（基安蒂） |
| `state.tempHeatAvailable` | 加入限时热量牌，回合结束销毁（薯条） |
| `KantoOden` | `kantoOdenSkipThisTurn = true` + `AccumulateKantoOden` |

---

## 7. 测试要求（BLOCKING）

| 类别 | 位置 | 要求 |
|------|------|------|
| 纯函数规则 | `Assets/Tests/Editor/` | EditMode，种子随机，命名 `test_*_*_*` |
| 新模块接入 | 每个接入点至少 1 个测试 | 覆盖边界（爆缸、牌库耗尽、跳过回合） |
| UI | `production/qa/evidence/` | 截图 + 走查文档（ADVISORY） |

现有测试：`race_session_test.cs`（本接入层）、`player_state_test.cs`、
`card_deck_test.cs`、`track_data_loader_test.cs` 为本次新增；
`tech_tree_rules_test.cs` / `trick_card_rules_test.cs` / `weather_rules_test.cs` /
`pit_lane_rules_test.cs` / `race_ranking_test.cs` 为系统自身测试。

运行方式：Unity Test Runner（EditMode）或
`Unity.exe -batchmode -runTests -projectPath . -testPlatform EditMode`。

---

## 8. 待接入清单（其他 AI 的作业）

> 优先级来自 `roadmap.md` P2。每个条目给出接入点位置。
> 2026-08-03 更新：尾流系统与全部独特科技钩子已接入，清单大幅缩短。

| # | 待做模块 | 接入点 | 现状 |
|---|---------|--------|------|
| 1 | ~~尾流系统（slipstream）~~ | `session.ComputeSlipstreamBonus`（ComputeMovements 第二轮调用） | ✅ 已接入：基础 +2，帕尔玛 +2，筋斗云 +2，冰糕阻断，范围 = 1 + 科技 + 临时加成 |
| 2 | ~~科技树 UI~~ | `MainMenuUI` 的“车队科技树”入口；`TechTreeProfileStore` 保存 RP/解锁/激活配置，比赛读取人类玩家配置 | ✅ 已接入 |
| 3 | ~~US L3 MotherRoad~~ | `ResolveMotherRoadPass`（PHASE B 地标结算） | ✅ 已接入（自动结算：繁荣冷却2 / 衰退自动修复 / 复兴转移动） |
| 4 | ~~SchwarzbierFuel~~ | ComputeMovements 自动激活（引擎 >1 热时付 1 热 +2 移动） | ✅ 已接入（自动模式，非手动选择） |
| 5 | ~~FullEnglish（UK L2）~~ | 抽牌相位（A2）检查手牌热/速/特技 → 尾流+1 + 限时热量牌 | ✅ 已接入 |
| 6 | ~~SunNeverSets（UK L3）~~ | `GetModifiers` flag 合并 + `config.enableUkSunNeverSetsDemo` 选择目标国 | ✅ 已接入 |
| 7 | ~~BrothSelection（JP L2）~~ | `SetupPlayerForRace` 按 `config.jpDemoBroth` 自动选择；冷却已在 ReactStep | ✅ 已接入 |
| 8 | ~~SmokedBBQ（US L2）~~ | ComputeMovements：BBQ 区内 +2 移动（热量当 2 速的近似） | ✅ 已接入（近似） |
| 9 | ~~赛道 JSON schema 校验工具~~ | `Assets/Scripts/Editor/TrackJsonValidator.cs`（Foodular1 > Tools 菜单） | ✅ 已完成 |
| 10 | 集成测试 | `race_simulation_test.cs`（纯层 3 玩家全比赛模拟，EditMode） | ✅ 已完成；Play Mode 版本待许可证可用后补 |

**近似说明**：黑啤酒燃料/美式烧烤/复兴终极采用自动激活近似（设计为主动选择/交互），
接入正式 UI 时可改为手动触发。DriveThru 地标判定已接入（+1 移动）。

---

## 9. 常见坑（其他 AI 必读）

1. **跳过回合三态**：`skipNextTurn`（失控/进站，下一回合开头清除）、
   `kantoOdenSkipThisTurn`（本回合剩余，回合开始清除）、
   回合级 `turnSkipped` 集合（A1 已结算跳过的玩家，后续相位不要再判断
   `skipNextTurn` —— 它已被清除）。
2. **`cornerTotalThisTurn` ≠ `totalMovementThisTurn`**：火锅底料的 ATTACK +1
   只进移动、不进弯道判定。
3. **热量支付别绕路**：直接 `DrawHeatFromPool` 会跳过黑面包/炸鱼薯条/烤肉拼盘。
4. **特技牌每回合限 1**：由 `trickState.trickPlayedThisTurn` 保证，`PlayTrick`
   还会校验传入的同一张运行时卡牌确实在手牌中。成功后必须通过 `CardDeck`
   移入弃牌堆，不要只删除 UI 或自己维护第二套状态。
5. **手牌上限随科技变化**：抽牌用 `session.EffectiveHandSize(p, config.handSize)`
   + `extraCardSlotsThisTurn`，不要硬编码 `config.handSize`。
6. **HUD 字段 nullable**：Prefab 模式没有新字段就静默跳过，别 `Find`。
7. **测试确定性**：`SystemRandomSource(seed)`，禁止 `UnityEngine.Random`、
   `DateTime.Now`、文件 IO。
8. **改 `PlayerState` 时同步改 `ClearTurnState()`**：每回合临时字段必须清空，
   持久字段（gear/position/lap）不动。
9. **回退赛道已是 JSON**：原硬编码 42 节点赛道已导出为 `fallback_42.json`
   （roadmap P1 #9）。新增赛道注意：坐标为归一化 [0,1]，弯段只需 1 个
   `isApex: true` 节点（判定只在 apex 触发，多节点弯段不会重复判罚）。
10. **赛车旋转**：`config.carSpriteFacingAngle`（默认 0=精灵朝右）与
    `carRotateSpeed` 控制随赛道方向旋转；出生和传送会立即对齐下一节点切线，
    正常移动则平滑旋转。换新车精灵时先确认朝向角度。

---

## 10. 2026-08-05 integration audit

- Temporary heat cards use `CardDeck.AddCardsToHand`; the four team trick cards
  use `AddTrickCardsToDrawPile` before the opening draw and therefore share the
  normal draw/discard/reshuffle lifecycle.
- Human play supports a one-card or multi-speed-card selection/confirm flow.
  Selected speed cards remain bounded by the turn limit and move atomically to
  the played area; trick cards remain single-card immediate actions and enter
  the discard pile. With no selection, the action button ends card play.
- AI card selection uses the effective per-turn card-slot limit, including
  temporary slots and Hotpot effects.
- AI corner-risk checks use lane-specific limits plus active tech/weather
  modifiers through `RaceSession.EffectiveCornerLimit`.
- Track weather aliases (`cloudy`, `hot`, `light_rain`, `heavy_rain`) are mapped
  to the current Sunny/Rainy runtime model, and fixed-default tracks are valid
  when their weather pool is intentionally empty.
- Exact runtime-card ownership is enforced for speed, trick, and heat transfer.
  Temporary heat is destroyed rather than credited to the permanent engine
  pool, and Mother Road advances only by the cards it actually consumes.
- AI heat payments now use the same public `TryPayHeat` path as human payments;
  Kanto Oden turn-start consumption no longer depends on tech-tree enablement.
- Card UI reset, resource refresh, disabled heat input, and action-button
  debounce were runtime-smoked through the actual menu-to-race flow. Gear and
  confirm controls are enabled only during the human gear-selection wait.
- The complete EditMode suite passes 301/301, the runtime console is clean, and
  the solution build has 0 errors (two existing MCP assembly-version warnings).
