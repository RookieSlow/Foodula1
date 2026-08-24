# Current Task List

> Updated: 2026-08-24
> Sources: Claude Code project memory, active session state, session history,
> current Git worktree, and current Unity project structure.

## 待推进（2026-08-23 比赛视觉信息强化）

- [x] 比赛中“返回主菜单”按钮移入左侧操作栏动作栈，放在重新开始按钮下方、
  提示日志面板上方，避免覆盖操作栏标题和赛道画面；Play Mode 截图和 Console
  走查通过，并增加运行时锚点回归测试。
- [x] P0：增强挡位出牌要求反馈，显示有效要求值、已出/要求数量和缺牌引擎故障预警；
  保持现有缺牌惩罚规则不变。EditMode 已覆盖正常、待确认和引擎热量不足提示，
  Play Mode 人工走查仍待完成。
- [x] P1：选中卡牌缩放并上移，保持布局槽位不变；选中态使用 1.08 倍缩放、上移
  24px、0.14 秒未缩放时间缓动和蓝色阴影，已覆盖多选、特技单选和布局槽位不变。
  Unity EditMode 当前全量回归 402/402 通过；Play Mode 遮罩边界仍待人工走查。
- [x] P1：为抽牌堆和弃牌堆增加牌背叠放、实际卡牌缩略图、数量徽标和牌堆变化刷新；
  抽牌堆按实际抽取顺序预览，弃牌堆按最近弃入顺序预览，热量牌按真实区域显示。
  Unity EditMode 当前全量回归 402/402 通过；Play Mode 尺寸与可读性仍待人工走查。
- [x] P1：基于赛道 JSON 增加科技蓝玩家光环、1-based `格 X/N` 和玩家前后各 6 格
  的局部刻度；密集节点会自动抽样文字。弯道覆盖层同步重制为 Lv1 绿 / Lv2 黄 /
  Lv3 红的圆角平滑双层曲线带，弯心和限速徽标保持清晰。Unity EditMode 全量回归
  402/402 通过，并覆盖全部 8 条官方 JSON 与 fallback 的弯道曲线采样；Silverstone
  Play Mode 截图与 Console 走查通过，其余官方赛道待抽查。
- [ ] P2：为尾流结算增加独立视觉阶段，显示两车聚焦、蓝色虚线气流和尾流加成；
  不改变规则层移动结果。
- [ ] 验收：相关 EditMode 测试通过，并完成至少一局包含牌堆变化、挡位缺牌和尾流的
  Play Mode 人工走查。

## 本次修复（2026-08-23 手动日志回归）

- [x] 修正 RaceTestLogWriter 的赛道元数据来源：优先记录 TrackManager 实际加载的
  赛道 ID，避免配置默认值与菜单选择不一致。
- [x] 修正比赛终止条件：玩家爆缸只将玩家标记为 DNF，剩余非爆缸赛车继续比赛，
  直到所有活动赛车完成或退赛。
- [x] 修正比赛结果文本中的国旗和状态 Emoji 字形警告，改用稳定的车队代码和中文
  状态标签，避免 TMP 显示方框。
- [x] 增加 RaceRanking 回归测试，并让纯层比赛模拟覆盖玩家 DNF 后其余赛车继续比赛；
  Unity EditMode 全量回归 402/402 通过。
- [ ] 仍需用新的手动 Play Mode 日志确认：玩家 DNF 后 AI 会继续完成并正确记录最终结果。

## 本次完成（2026-08-23 热量牌生命周期、阴阳茶与维修区）

- [x] 修正热量牌生命周期：`initialHeatCards` 不再加入普通牌组；开局与普通补牌只抽
  速度/特技牌。永久热量只能从独立引擎池经明确支付/效果进入手牌或弃牌堆，再由冷却
  或明确回收效果返回引擎。
- [x] 将自动冷却统一到 `CardDeck.CoolHeat()`，严格按手牌→抽牌堆→弃牌堆
  顺序处理热量；永久热量回引擎，限时热量销毁。
- [x] 将中国队阴阳茶改为按 Go/Recover 模式结算：Go 支付引擎热量到弃牌堆并
  前进 1 格，Recover 从手牌冷却 1 张。
- [x] 维修区保持停 1 回合，同时在 `pit_exit` 后按配置前移；新增中国队“快充技术”
  科技修正。
- [ ] 仍需在 Play Mode 完成多天气、多圈和完整进站流程的人工走查；本次未修改场景。

## 本次推进（2026-08-23 热量支付与维修区时序）

- [x] 标准热量支付改为默认直接进入手牌，热量会占用手牌；显式弃牌堆路径保留给
  阴阳茶 Go 等明确效果；普通抽牌仍不会抽取热量。
- [x] 维修区选择窗口改为 `pit_entry` 前 1–10 格；选择进站只登记预定状态，
  越过入口的本回合继续移动，下一回合开始才执行停站、全热量冷却和出口前移。
- [x] 增加维修区入口前窗口、跨圈入口检测和“预定后延后一回合执行”的 EditMode 覆盖。
- [x] 本轮 Unity EditMode 全量回归 402/402 通过；Play Mode 进站人工走查仍待验证。

## 本次完成（2026-08-19 赛道天气规则边界）

- [x] 将赛道 JSON 的 `sunny/cloudy/light_rain/heavy_rain/hot` 映射为独立
  `WeatherType`，并保留旧 `WeatherType.Rainy` 作为小雨兼容别名。
- [x] 将弯道限速、尾流范围/禁用、热天冷却惩罚、湿地失控计数器增量和 HUD
  文案统一收敛到 `WeatherModifiers` / `WeatherRules`，管理器仅负责调用。
- [x] 增加 `RaceLapWeatherRules`，让运行时比赛与纯模拟共用起终点过线、每圈天气门控
  和完赛判定顺序；新增跨圈转场回归测试。Unity EditMode：377/377 通过，
  `dotnet build Foodula1.sln --no-restore`：0 错误。
- [x] Play Mode 启动冒烟通过：5 秒运行期间 Console 0 条错误/警告/日志；未修改场景。
- [ ] 仍需在 Play Mode 完成多天气、多圈和完整进站流程的人工走查；本轮未修改场景。

## 本次完成（2026-08-18 出牌与赛事表现）

- [x] 速度牌支持多选后一次确认，也支持选中一张后单张确认；选择数量由本回合
  出牌上限实时限制，提交在 `CardPlayRules` 中原子完成。
- [x] 特技牌仍严格保持单张选择、即时结算，并禁止与速度牌混选。
- [x] 赛车图标显示缩放改为 `GameConfigSO.carSpriteScale`，默认由 0.2 调整为 0.28。
- [x] 新增运行时 `RaceEventFX`：超车慢放特写、失控旋转提示与爆缸退赛提示，均不改变
  规则层状态。
- [x] Unity 编辑器回归测试已完成：EditMode 365/365 通过，Play Mode 启动冒烟无项目
  错误或警告（仅 Unity MCP 自身 WebSocket 重连警告）。

## 本次完成（2026-08-18 模块化推进）

- [x] 将 `MVPGameManager.AutoCreateUI()` 的程序化 HUD、档位按钮、动作按钮、手牌容器
  和卡牌预制体回退逻辑抽取到 `Assets/Scripts/UI/RaceUIFactory.cs`。
- [x] 保留 Prefab 优先、旧场景回退、按钮回调修复和 TMP 字体复用行为；Manager 只负责
  传入回调与接收 UI 引用，不再持有主要 UI 构建细节。
- [x] Unity EditMode：321/321 通过；`dotnet build Foodula1.sln --no-restore`：0 错误。
- [x] Unity Play Mode 启动冒烟无项目错误/警告；MCP 仅记录自身 WebSocket 重连警告。
- [x] 将出生朝向、传送朝向和逐帧旋转的 Unity 适配逻辑抽取到
  `Assets/Scripts/Gameplay/CarOrientationController.cs`；角度规则仍由
  `CarOrientationRules` 纯函数负责。
- [x] 新增朝向偏移与旋转速度回归测试；Unity EditMode：323/323 通过，Dotnet 编译 0 错误。
- [x] 将车队赛车精灵槽位与缺失精灵时的备用颜色映射抽取到
  `Assets/Scripts/Gameplay/TeamCarPresentationRules.cs`；新增 4 项边界回归测试，避免
  `MVPGameManager` 直接维护车队外观身份映射。
- [x] 将节点间车辆插值与到达阈值抽取到
  `Assets/Scripts/Gameplay/CarMovementAnimator.cs` / `CarMovementRules.cs`；通过注入
  deltaTime 的回归测试保持原有移动速度、终点吸附与朝向更新行为。
- [x] 将环形赛道超车判定抽取到 `Assets/Scripts/Core/RaceMovementRules.cs`；通过注入
  跳过回合谓词覆盖普通超车、失控/维修区跳过和无效赛道长度边界。
- [x] 将普通赛道并排时的后车外线判定抽取到
  `Assets/Scripts/Core/RaceLaneRules.cs`；覆盖同节点、不同圈/节点、完赛退赛和无效索引边界。
- [x] 将档位、卡牌和弃牌阶段的玩家输入等待状态抽取到
  `Assets/Scripts/Core/RaceInputState.cs`；回合协程与 UI 回调共享同一门控状态，并覆盖
  确认一次、阶段互斥和重置边界。
- [x] 将起终点过线后的圈数递增与完赛边界抽取到
  `Assets/Scripts/Core/RaceLapRules.cs`；天气掷骰、科技重置和 UI 日志仍由管理器编排。
- [x] 将印地换道与维修区选择的等待门控并入
  `Assets/Scripts/Core/RaceInputState.cs`；场景面板和选择后的车辆/维修效果仍由管理器编排。
- [x] 将回合跳过、爆缸和完赛后的参与资格抽取到
  `Assets/Scripts/Core/RaceTurnRules.cs`；A1 的跳过消费和各阶段副作用仍由管理器编排。
- [x] 将比赛阶段枚举、阶段切换和输入可接受性抽取到
  `Assets/Scripts/Core/RacePhaseState.cs`；协程副作用和 UI 仍由管理器编排。
- [x] 将每圈天气只掷一次的门控抽取到
  `Assets/Scripts/Core/RaceWeatherState.cs`；天气池选择和 UI 日志仍由会话/管理器编排。
- [x] 增加 `RaceTestLogWriter` 手动测试日志：自动保存 HUD 事件、回合状态、档位、玩家/AI
  出牌、特技牌和移动计划；启动/结束时在 Console 输出日志绝对路径，文件写入或关闭失败
  不影响比赛。日志适配器通过 `GetDefaultDirectory()` 暴露默认目录，并覆盖重开比赛时的
  文件轮换。

## 本次修复（2026-08-18 档位确认卡死）

- [x] 修复 `RaceEventFX` 复用失效 `CanvasGroup` 导致 `MVPGameManager.Start()` 中断的问题。
- [x] 赛事特效初始化每次创建带必需组件的新根节点，并设置为可选表现；即使特效初始化
  失败也会继续启动比赛回合协程。
- [x] Unity 编辑器退出 Play Mode 并重载脚本后复测档位确认按钮和首回合推进；本轮
  EditMode 365/365 通过，启动冒烟未再出现 `CanvasGroup` 异常。

## P0 - Resume Approved Scheme A Refactor

- [x] Review all current C# files and reconcile the earlier whole-project
  review with the latest project state.
- [x] Integrate `Assets/Scripts/Core/RaceRules.cs` into
  `MVPGameManager.cs` so shared gear, cooling, movement, and selection rules
  have one source of truth.
- [x] Integrate `Assets/Scripts/AI/AIPlanner.cs` into `AIController.cs`.
- [x] Inject `IRandomSource` where deterministic gameplay or AI behavior is
  required.
- [x] Add EditMode tests for `RaceRules` (9 cases passing in Unity).
- [x] Add EditMode tests for `AIPlanner`, deterministic random behavior,
  and the AI spin-out card-conservation regression (7 cases passing in Unity).
- [x] Historical baseline: validated changed scripts and Unity console with no errors
  (16 EditMode tests at that stage; current full suite is 402/402).
- [x] Review the final diff for the approved refactor changes.
- [ ] Commit only with explicit user instruction; scheduled-task authorization
  does not include Git commits.

The four Scheme A source files and their `.meta` files were committed in
`58d1bba`. `RaceRules` and `AIPlanner` are now integrated into the runtime.
`AIController` and `CardDeck` accept injectable random sources, and the
refactor currently has 16 passing EditMode tests. The final review also fixed an
AI spin-out path that could remove selected speed cards without discarding them,
and standardized `IRandomSource.NextDouble` to the [0, 1) contract.
Manager-level seeded replay and broader integration coverage remain useful
follow-ups, but are not blocking the current Demo path.

The first data-driven track completion slice is also verified. `TrackNode` now
preserves JSON apex metadata, pure `TrackRules` owns wrapping traversal and
start/finish lookup, and `MVPGameManager` initializes and counts laps from the
runtime track rather than the legacy config index. Silverstone loads in the
Race scene with 60 nodes and 3 laps. Unity currently passes 20 EditMode tests
with 0 failures, warnings, or errors.

## P0 - Protect and Reconcile the Current Worktree

- [ ] Inspect the existing modifications to `MainMenu.unity` and
  `Race.unity`; preserve legitimate user scene edits.
- [ ] Verify the untracked `Assets/Data/Tracks/NewTrackData.asset` before
  deciding whether it belongs to the track-system work.
- [ ] Keep the Unity MCP package changes in `Packages/manifest.json` and
  `Packages/packages-lock.json` logically separate from gameplay refactoring.
- [ ] Avoid bundling unrelated scene, track-data, MCP installation, and
  refactor changes into one commit.

## P1 - Track System Decision and Completion

- [ ] Choose a reliable authoring workflow: GameObject child nodes, a simpler
  Editor script, or another explicitly approved approach.
- [ ] Decide whether AI-generated `track_layout_*.png` images are authoring
  references, runtime backgrounds, or both.
- [x] Configure `GameConfigSO.trackId` and verify JSON track loading in the
  Race scene (Silverstone, 60 nodes, 3 laps).
- [x] Validate arbitrary node counts throughout movement and UI; remove
  remaining hard-coded `42` display assumptions.
- [x] Preserve JSON `isApex` metadata and validate apex-only, deduplicated
  corner crossing across the lap boundary.
- [ ] Playtest speed-limit heat penalties through the full Race interaction.
- [x] Drive start/finish lookup and crossing from runtime track-node data;
  validate wrapping and a non-zero start/finish index in EditMode tests.
- [ ] Complete a multi-lap manual playthrough to validate finish timing.
- [x] Implement and unit-test pit entry and pit exit behavior; Play Mode manual validation remains open above.
- [x] Drive LineRenderer positions from loaded track coordinates and verify
  the Silverstone path in Play Mode.
- [x] Replace the inaccurate Nürburgring 24H combined bonus layout with a
  219-node standalone Nordschleife sampled from the referenced real layout;
  verify zero self-intersections and regenerate its guide/background.
- [x] Standardize node colors across all tracks: apex red, other corner
  nodes orange, straights white, and start/finish green; enforce exactly one
  apex per corner group across every track config.
- [x] Add presentation-only lane slots to every track: two lanes for standard
  tracks and four lanes for Indianapolis; keep gameplay, camera, and minimap
  positions centerline-based, and regenerate backgrounds with matching lanes.
- [x] Add Indianapolis lane-specific corner limits (inner-to-outer 4/5/6/7,
  with outer-lane limit 7)
  and a player one-lane inward/outward choice at each start/finish crossing.
- [x] Replace runtime grid-like track visuals with selected layout backgrounds,
  yellow corner masks, red apex masks, visible speed-limit labels, and
  editor-only node metadata.
- [x] Make ordinary tracks use the inside lane by default, move only the
  trailing car outside when cars share a node, and keep Indianapolis vehicle
  placement tied to the player's explicit lane choice.
- [x] Preserve authored corners, start/finish, and pit landmarks while
  re-sampling only the straight runs at equal arc-length intervals; add a
  numbered F8 runtime node overlay and a regression test covering all tracks.
- [x] Keep the background generator on the same 16:9 world-space sampling
  metric as `TrackDataLoader`; regenerate all eight official layouts and
  verify every sampled node remains on the painted road centerline.
- [x] Reconcile Monza corner metadata with the visible turning sections;
  relocate the seven corner groups off the former straight-only cells and
  regenerate its background.
- [x] Reconcile Shanghai, Indianapolis, and Nürburgring GP corner metadata
  with their painted turning sections; move the stale straight-road masks,
  regenerate the three backgrounds, and add regression coverage for the
  corrected node ranges.
- [x] Re-audit all eight selectable tracks in Play Mode with the numbered debug
  overlay; confirm runtime nodes, road centerlines, corner/apex masks and speed
  limit labels remain aligned with the selected background art.
- [x] Add turn-scoped manual camera control to the race map: left/middle drag,
  mouse-wheel zoom, moving-vehicle focus, player focus outside movement, and
  automatic-focus suspension after manual input until the next turn.
- [x] Detach the minimap camera from the moving main camera so the complete-track
  view remains fixed while the player pans, zooms, or follows another vehicle.
- [x] Tune race presentation pacing with 0.14s movement focus lead-in, 0.06s
  per-node pause and 0.10s focus trail-out; keep the authored ±15-cell window.
- [x] Add a configurable test assist that guarantees the China player's
  `cn-hotpot-base` ATTACK card is present in the opening hand while preserving
  hand size and card conservation.
- [x] Rotate vehicles to follow the tangent between track nodes (default sprite offset corrected to 0° for right-facing car art).
- [x] Integrate track weather-pool selection after the core track path is stable;
  profile effects now resolve through `WeatherRules`, with Play Mode multi-weather
 走查 remaining.

## P2 - Demo Asset Replacement

- [x] Reconcile the Phase 2 planning document with assets already completed; remaining gaps are listed in `design/planning/asset-manifest.md`.
- [ ] Finish remaining UI panel artwork.
- [ ] Replace gear-button placeholders with the approved gear controls.
- [ ] Replace the heat text placeholder with the approved thermometer UI.
- [ ] Replace remaining flag/UI-node placeholders; track layouts and runtime corner masks are already integrated.
- [ ] Verify card and vehicle sprites in both scenes at target resolution.

## P3 - Feature Completion

- [ ] Tune and Play Mode-verify configured multi-AI races.
- [x] Implement slipstream rules and team range modifiers; broader balance remains open.
- [x] Implement team vehicle attributes and tech modifiers; balance review remains open.
- [x] Shuffle team trick cards into the normal deck lifecycle and replace batch
  hand submission with one-card select/confirm play, immediate trick resolution,
  and explicit end-of-card-phase behavior.
- [x] Implement the driver-selection flow as a catalog, session state, and
  runtime-built main-menu panel; connect the selected driver to race setup.
- [ ] Add sound effects.
- [ ] Add card-play, vehicle movement, bounce, and spin-out animations.
- [x] Add weather gameplay after track data and race rules are stable; the five
  design profiles are now wired into limits, slipstream, cooling, spin-out and HUD.

## Open Decisions

- [ ] Select the replacement for the reverted Track Node Editor.
- [x] Treat Le Mans as a France expansion track with no home team; it is not one of the six national-team home circuits.
- [ ] Confirm whether Kanto Oden carry-over slots are mandatory (the current
  runtime behavior) or optional; Hotpot's additional slot is already optional.
- [ ] Decide the commit boundaries for current scene, data, MCP, and
  refactor changes.

## Completed Context

- [x] Main-menu and Race scene flow.
- [x] Chinese UI and Chinese font integration.
- [x] Card number and heat icon display.
- [x] Six national-team vehicle sprites.
- [x] Eight track-layout image prompts.
- [x] Unity MCP 10.1.0 package installed and connection verified.

## Maintenance Rule

Update this file whenever a task is completed, superseded, or blocked.
Historical Claude Code files under `.claude/agent-memory/` and
`production/session-logs/` remain provenance only; this task list is the
maintained source for current work.

## 2026-08-15 Tech Tree and Team Gear Audit

- [x] Audited the existing tech-tree rules/database against the design docs;
  common, unique and China EV node pools are now selected through one database API.
- [x] Added the main-menu tech-tree entry and runtime-built configuration UI;
  RP, permanent unlocks and active race selections persist per team.
- [x] Replaced the race's demo-only human tech state with the saved profile;
  AI opponents retain transient demo profiles so a race cannot mutate campaign RP.
- [x] Added the China Go/Recover pure gear module and a team-aware facade;
  player controls, AI selection, card limits, overclock heat and Recover cooling
  all use the same rules.
- [x] Added `TeamVehicleRules` as the boundary for team profile values and base
  durability/heat-pool setup; full movement/handling balancing remains a follow-up.
- [x] 历史记录：该切片完成时 Unity EditMode 307/307 通过；当前总回归已更新为 402/402。
  该历史切片的 dotnet build 当时为 0 错误。
- [ ] Continue extracting orchestration from `MVPGameManager` into phase services
  once the next feature requires changes across multiple phases.

## 2026-08-05 Module Audit

- [x] Audited the modules introduced by the previous AI integration commit.
- [x] Fixed temporary-heat card injection and AI effective card-slot handling.
- [x] Connected AI corner risk to lane-specific limits and active weather/tech modifiers.
- [x] Accepted the existing fixed-default weather tracks in the JSON validator.
- [x] Run Unity EditMode/Play Mode tests through the open Unity instance or MCP (EditMode 262 passed; no PlayMode tests configured).
- [x] Corrected Indianapolis lane winding so lane 0 is inside and limits rise from 4 (inside) to 7 (outside).
- [x] Completed a full static + runtime audit: corrected stale stage metadata,
  restored Race speed-card/heat icon references, and removed unsupported emoji
  glyphs from runtime UI labels.
- [x] Added the driver data slice from `foodula-1-drivers.md`: 12 profiles,
  XP thresholds, tier unlocks, UK active-use bonus, XP reward calculation,
  selection state, menu panel, and 5 EditMode regression tests.
- [x] Re-ran Unity EditMode tests after the audit and driver slice: 273/273
  passed with no failures or skips; dotnet build has 0 errors.
- [x] Fixed the card-play lifecycle regression: opening tricks are randomly
  drawn, confirmed tricks enter discard immediately, confirmed speed cards stay
  in the played area until cleanup, and optional discard accepts any non-heat
  card. Unity EditMode tests now pass 287/287; runtime smoke verified the
  seven-card opening hand, button states, hand/UI synchronization, and trick
  transfer to discard.
- [x] Completed the follow-up runtime audit and repaired cross-system card/heat
  ownership: exact runtime card instances are consumed atomically, temporary
  heat can no longer inflate the permanent engine pool, and AI heat payments
  use the same canonical path as human payments.
- [x] Fixed turn-start and movement edge cases: Kanto Oden carry-over is
  consumed even when the tech tree is disabled, Hotpot grants movement only
  when its optional ATTACK slot is actually used, and teleports immediately
  restore the car's track-tangent facing.
- [x] Hardened card UI state: reset clears every interaction mode, heat cards
  are non-interactable, resource displays refresh after card/heat changes, and
  a short action-button debounce prevents a physical double-click from both
  confirming a card and ending the phase. Gear controls are now interactable
  only while the human player is actively choosing a gear.
- [x] Replaced the placeholder race simulation assertions with an actual
  draw/pay/commit/move/cleanup/reshuffle loop and added exact-ownership,
  temporary-heat, Kanto, Hotpot, shared AI heat-payment, and orientation tests.
  Final verification: Unity EditMode 301/301 passed; runtime smoke covered
  main menu -> track selection -> Race, gear/card/discard/reset interaction,
  disabled heat-card input, and post-teleport orientation with a clean console.
  `dotnet build Foodula1.sln --no-restore` reports 0 errors (two existing MCP
  assembly-version warnings remain).

## 本次完成（2026-08-15 赛事回归与平衡）

- [x] 比赛 HUD 增加“返回主菜单”按钮；修复 RaceCanvas 预制体重建后的旧引用导致重复 HUD 的问题。
- [x] 所有主要菜单/比赛控制按钮统一接入短按压/释放缩放动画；运行态确认按钮存在且可触发场景切换。
- [x] 新增 `TrackTeamBalanceBenchmark`，覆盖 Resources 中全部赛道与六支车队，每图 12 场确定性比赛，报告写入 `design/balance/track-team-benchmark-2026-08-15.md`。
- [x] 平衡收敛：标准 AI 在预计抵达弯道时优先低值牌，风险窗口按预计移动量计算；中国队恢复设计案 Go 直道输出、操控从 -1 调为 0，并默认采用 Go→Go→Recover；美国直道加成调整为每回合固定 +1。
- [x] 最终验证：Unity EditMode 308/308 通过；`dotnet build Foodula1.sln --no-restore` 0 错误；MainMenu→Race→返回主菜单运行态冒烟通过，单一 HUD、按钮动画组件和控制台均正常。
