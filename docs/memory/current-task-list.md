# Current Task List

> Updated: 2026-08-21
> Sources: Claude Code project memory, active session state, session history,
> current Git worktree, and current Unity project structure.

> **当前口径说明**：本文件下方的历史条目保留当时的测试数字和机器状态，
> 仅本文件顶部最新条目代表当前状态。当前机器已能打开 Unity；本次审计确认
> Unity MCP 为 10.1.2 并已连接。重装系统后本机未安装 .NET SDK，因此旧条目中
> `dotnet build 0 errors` 不能当作本机当前可复现结果。

## 本次完成（2026-08-21 手牌选中视觉反馈）

- [x] `CardUI` 选中牌增加平滑上移、轻微放大、金色染色、叠加高亮和描边；取消选择时
  自动恢复原位与原色。
- [x] 反馈在运行时动态添加，不修改 `CardPrefab` 资产，兼容当前 `CardHandUI` 的布局组。
- [ ] Unity MCP 工具端点本轮未在 Codex 工具列表暴露；Unity 编辑器保持打开，已完成静态
  检查，待编辑器内实际选牌或 MCP 恢复后做截图确认。

## 本次完成（2026-08-21 冷却、维修区与阴阳茶规则校准）

- [x] 普通冷却统一使用 `CardDeck.CoolHeat`，严格按手牌 → 牌库 → 弃牌堆处理；
  特技牌的“冷却指定手牌”效果仍保留为显式的手牌目标效果。
- [x] 维修区从 `pit_entry` 模拟前进 5 格，清空全部热量并跳过 1 回合；`pit_exit`
  保留为 JSON 校验/表现标记，并新增相邻出口标记的回归测试。
- [x] CN L1 阴阳茶改为人类玩家在回合结束选择阴/阳，AI 保留自动策略；补充输入门控、
  显式选择和额外移动跨起终点的测试与运行时 UI。
- [ ] Unity 已自动重载脚本且 `Editor.log` 未出现 C# 编译错误；EditMode 全量测试本轮
  未能在已打开的 Unity 实例上再次启动，待编辑器内 Test Runner 或关闭编辑器后复跑。

## 本次完成（2026-08-21 维修区解析/应用边界）

- [x] `PitLaneRules.ResolvePitStop` 现在只解析资格、出口位置、冷却量和跳过回合数，
  `ApplyPitStop` 单独应用位置/跳过状态；旧 `EnterPit` 保留为兼容性一站式门面。
- [x] `MVPGameManager` 与纯层比赛模拟显式执行 resolve → apply，规则结果和可变
  `PlayerState` 副作用可以分别验证；空轨道/空玩家/缺失节点输入也安全返回。
- [x] 新增进站解析不变更玩家、应用后才变更以及缺失节点防护回归测试；维护记录中最近一次
  Unity EditMode 全量 **399/399 通过**、0 失败/跳过。本次文档同步未重跑 Test Runner。
- [ ] 当前机器的 `dotnet build Foodula1.sln --no-restore` 尚不可复现：未安装 .NET SDK；
  旧记录中的 0 错误与 MCP 程序集警告属于历史验证结果。
- [ ] 剩余风险：多天气、多圈、多车过线与完整进站选择/出站流程仍需人工组合走查；
  PlayMode Test Runner 当前没有非编辑器测试程序集。

## 本次完成（2026-08-21 多圈跨线终止边界）

- [x] 新增 `RaceLapWeatherRules.AdvanceCrossings`，把同一移动中多个起终点经过的
  圈数递增、每圈天气门控和完赛边界组合为一个纯规则批次；到达完赛圈后立即停止处理
  后续跨线，避免超额移动在纯模拟中重复分配完赛顺位。
- [x] `race_simulation_test` 改用批量跨线结果，同时保留每次跨线的科技重置、天气掷骰和
  完赛副作用接线；新增多圈终止与天气门控回归覆盖。
- [x] Unity 资源刷新后 EditMode 全量 **396/396 通过**、0 失败；`dotnet build
  Foodular1.sln --no-restore` 0 错误，仅保留既存 MCP 程序集版本冲突警告。
- [ ] 剩余风险：多天气、多圈、多车过线与完整进站选择/出站流程仍需人工组合走查；
  PlayMode Test Runner 当前没有非编辑器测试程序集。

## 本次完成（2026-08-21 纯层事件快照一致性）

- [x] `PitLaneRules.CrossedPitEntry` 保留旧纯规则 API，但内部改为消费
  `TrackRules.GetTraversalEvents`；运行时、维修区门面和测试模拟不再各自重走跨圈节点路径。
- [x] `race_simulation_test` 的起终点/天气/完赛与维修区检查改用同一 `TrackTraversalEvents`
  快照，纯层模拟与 `MVPGameManager` 的移动事件顺序保持一致。
- [x] 保留既有跨圈事件与维修区回归覆盖；Unity 资源刷新后 EditMode 全量 **394/394 通过**、
  0 失败/跳过，并完成 PlayMode 上海赛道一回合 Go 冒烟。本轮不修改场景、不 commit/push。
- [ ] 剩余风险：多天气、多圈、多车过线与完整进站选择/出站流程仍需人工组合走查；PlayMode
  Test Runner 当前没有非编辑器测试程序集。

## 本次完成（2026-08-20 移动事件快照边界）

- [x] 新增 `TrackTraversalEvents`，由 `TrackRuntimeContext.GetTraversalEvents` 从未取模的
  原始移动目标一次采样有序节点、起终点经过次数、去重弯心和维修区入口；车辆动画、过线、
  弯道、阴阳茶与维修区编排消费同一份事件快照，减少 `MVPGameManager` 内重复路径判断。
- [x] `Mother Road` 地标判定改用最终未取模移动目标，支持非零地标跨圈与额外移动；
  `TechTreeRules.CrossedPositionForward` 保留既有一圈语义并覆盖多圈原始目标。
- [x] 新增 3 项事件/地标跨圈回归测试；Unity Test Runner EditMode 全量 **394/394 通过**、
  0 失败/跳过。测试中的 `no_such_track` 预期错误日志已清理，最终 Console 为 0 条错误/警告。
- [x] MainMenu → 上海国际赛车场 → Race 启动并执行一回合 Go；赛道、车辆、HUD 和移动后
  状态正常，PlayMode 已停止；`dotnet build Foodular1.sln --no-restore` 0 错误，仅保留既存
  MCP 程序集版本冲突警告。未修改场景，未 commit/push。
- [ ] 剩余风险：多天气、多圈、多车过线与完整进站选择/出站流程仍需人工组合走查；PlayMode
  Test Runner 当前没有非编辑器测试程序集。

## 本次完成（2026-08-20 赛道前进路径边界）

- [x] `TrackRules.GetCrossedNodeIndices` 统一生成原始前进目标对应的有序、归一化节点序列；
  `TrackRuntimeContext` 暴露该查询，车辆逐节点动画、弯心/起终点查询和维修区入口检测共用
  同一环形路径采样，跨一圈或多圈时不再各自维护取模逻辑。
- [x] 新增跨圈有序路径、空/反向路径和快照多圈事件查询回归测试；Unity EditMode 全量
  391/391 通过。清理 `no_such_track` 的 `LogAssert.Expect` 预期日志后，Console 为 0 条
  错误/警告。
- [x] MainMenu → 上海国际赛车场 → Race 重新加载并按 F8 检查编号节点、弯道/弯心与起终点
  标记；运行时背景和覆盖层对齐，PlayMode 已停止，未修改场景，未 commit/push。
- [ ] 剩余风险：完整多天气、多圈以及进站选择/出站流程仍需人工组合走查；PlayMode Test
  Runner 当前没有非编辑器测试程序集。

## 本次完成（2026-08-20 调试覆盖层读取边界）

- [x] `TrackDebugOverlay.SetVisible` 的节点数量、节点元数据和世界坐标读取改为直接消费
  `TrackRuntimeContext`；`TrackManager` 仍只提供调试开关、Prefab 与颜色等表现配置，兼容
  序列化行为保持不变。
- [x] 新增 `test_context_debug_overlay_queries_preserve_node_metadata_and_positions`，覆盖
  调试覆盖层所需的节点编号、弯心标记、环形查询和非零坐标；Unity EditMode 全量 388/388
  通过。清理预期的 `no_such_track` 日志后，Console 为 0 条错误/警告。
- [x] MainMenu → 上海国际赛车场 → Race 的真实 Play Mode 走查中按 F8 显示整条赛道的编号
  节点、弯道/弯心和起终点标记，覆盖层与赛道背景对齐；未修改场景，未 commit/push。
- [ ] 剩余风险：PlayMode Test Runner 当前没有非编辑器测试程序集，多天气、多圈与完整进站
  选择/出站流程仍需后续人工组合走查。

## 本次完成（2026-08-20 比赛编排读取边界）

- [x] `MVPGameManager` 的比赛初始化、天气元数据、地标/MotherRoad、维修区入口与进站、
  阴阳茶位移、圈数天气和奖励国家读取，均改为直接消费 `TrackRuntimeContext`；不再从
  `TrackManager` 兼容门面读取赛道节点、圈数或元数据，既有规则与 UI 行为保持不变。
- [x] 新增带 `pit_entry/pit_exit` 的快照回归测试，验证源节点修改不会污染运行时副本，且
  `PitLaneRules` 可直接消费上下文节点；Unity EditMode 全量 387/387 通过。
- [x] MainMenu → 上海国际赛车场 → Race 初始化并执行一回合 Go 走查，赛道背景、车辆、HUD
  和维修区赛道数据正常；清理 Console 后运行期间 0 条项目错误/警告。`dotnet build
  Foodular1.sln --no-restore` 0 错误，仅保留既存 MCP 程序集版本冲突警告。
- [ ] 历史记录：调试覆盖层读取已在后续切片迁移到 `TrackRuntimeContext`；PlayMode Test Runner
  当前没有非编辑器测试程序集，多天气、多圈与完整进站选择/出站流程仍需后续人工组合走查。
  本轮未修改场景，未 commit/push。

## 本次完成（2026-08-20 移动计划与弯道结算读取边界）

- [x] `MVPGameManager.ComputeMovements`、`GetNigiriBonus`、`GetTorpedoBonus` 和
  `ResolveCorners` 现在直接读取 `TrackRuntimeContext` 的节点数、弯道集合、弯道限速和
  名称；同一切片内的起终点换道 UI 也改为消费快照，未改变移动量、尾流、地标或弯道判定规则。
- [x] 新增快照查询回归测试，覆盖移动计划/弯道结算所需的跨节点弯道去重、车道限速、弯道名称
  和环形节点查询；Unity 资源刷新后 EditMode 全量 386/386 通过。
- [x] MainMenu → Indianapolis → Race 启动并执行一回合 Go 走查，车辆、赛道、HUD 和车道视觉
  正常；清理 Console 后运行期间 0 条项目错误/警告。`dotnet build Foodular1.sln --no-restore`
  0 错误，仅保留既存 MCP 程序集版本冲突警告。
- [ ] 历史剩余风险：进站/MotherRoad、地标/阴阳茶的比赛编排读取已迁移；`TrackDebugOverlay`
  仍有兼容门面读取。PlayMode Test Runner 当前没有非编辑器测试程序集，多天气、多圈与完整
  进站选择/出站流程仍需后续人工组合走查。本轮未修改场景，未 commit/push。

## 本次完成（2026-08-20 车辆路径读取边界）

- [x] `MVPGameManager` 的车辆出生、逐格移动、起终点换道、车道刷新和传送朝向读取，
  直接消费 `TrackRuntimeContext`；不再从 `TrackManager` 兼容门面读取节点坐标、起终点、
  车道边界和赛道标识，保留既有动画、圈数、换道与精灵表现行为。
- [x] 新增车辆路径查询回归测试，覆盖玩家/AI 默认车道、起终点位置、下一节点路径、
  环形节点归一化和起终点换道能力；未修改场景或序列化引用。
- [x] Unity 资源刷新后 EditMode 385/385 通过；Race Play Mode 通过主菜单→Indianapolis
  选轨→比赛初始化走查，车辆与赛道视觉正常，清理 Console 后运行期间 0 条项目错误/警告；
  `dotnet build Foodular1.sln --no-restore` 0 错误，仅保留既存 MCP 程序集版本冲突警告。
- [ ] 历史剩余风险：进站/MotherRoad、地标/阴阳茶和 `TrackDebugOverlay` 仍使用兼容门面；
  PlayMode Test Runner 当前没有非编辑器测试程序集，多天气、多圈与完整进站选择/出站流程仍需
  后续人工组合走查。本轮未修改场景，未 commit/push。

## 本次完成（2026-08-19 赛道运行时上下文边界）

- [x] 新增 `Assets/Scripts/Gameplay/TrackRuntimeContext.cs`，在赛道加载后复制节点、
  世界坐标、车道偏移、弯道限速、天气池、圈数和元数据，作为比赛/AI/镜头/车辆表现的
  单一只读赛道快照；旧 `TrackManager` 查询 API 保持兼容并委托到快照。
- [x] 移除 JSON 赛道加载对共享 `GameConfigSO` 的圈数、节点数和回退尺寸回写；比赛流程
  改从赛道上下文读取 `TotalLaps`、天气、赛道名称和国家，避免跨组件配置污染。
- [x] 新增 4 项快照边界/适配器查询/印地车道限速/坐标归一化回归测试；同步 ADR-004、架构注册表、
  模块接入指南和路线图 D1 状态。
- [x] `AIController` 的弯道预判、`RaceCameraController` 的路径缓存和 Race HUD 的位置总格
  直接依赖 `TrackRuntimeContext`；保留 `TrackManager` 兼容门面，未改变选牌、镜头焦点或场景绑定行为。
- [x] 验证：Unity EditMode 384/384 通过；Race Play Mode 启动运行 7 秒，Console 0 条
  项目错误/警告；`dotnet build Foodular1.sln --no-restore` 0 错误，仅有既存 MCP 程序集
  版本冲突警告；未修改场景、未 commit/push。
- [ ] 剩余风险：车辆编排和调试层仍通过 `TrackManager` 兼容门面读取赛道；多天气、
  多圈与完整进站 Play Mode 走查仍待完成。

## 本次完成（2026-08-19 维修区入口路径边界）

- [x] `PitLaneRules.CrossedPitEntry` 现在沿未取模的前进路径逐格检查维修区入口，
  正确覆盖跨起终点回绕、实际额外移动和空轨道边界。
- [x] `MVPGameManager` 传入 `oldPos + totalMovementThisTurn`，避免科技/特技移动加成
  将车辆带过维修区入口时漏判；纯层比赛模拟同步使用未取模目标。
- [x] 新增维修区入口跨圈命中/未命中/空轨道回归测试；Unity EditMode：380/380 通过，
  `dotnet build Foodular1.sln --no-restore`：0 错误。
- [x] Race Play Mode 真实 MainMenu → Race 转场与初始化冒烟通过，项目 Console 0 条
  错误/警告；未修改场景。
- [ ] 仍需人工完成多天气、多圈与完整进站选择/出站流程走查。

## 本次完成（2026-08-19 赛道天气规则边界）

- [x] 将赛道 JSON 的 `sunny/cloudy/light_rain/heavy_rain/hot` 映射为独立
  `WeatherType`，并保留旧 `WeatherType.Rainy` 作为小雨兼容别名。
- [x] 将弯道限速、尾流范围/禁用、热天冷却惩罚、湿地失控计数器增量和 HUD
  文案统一收敛到 `WeatherModifiers` / `WeatherRules`，管理器仅负责调用。
- [x] 增加 `RaceLapWeatherRules`，让运行时比赛与纯模拟共用起终点过线、每圈天气门控
  和完赛判定顺序；新增跨圈转场回归测试。Unity EditMode：377/377 通过，
  `dotnet build Foodular1.sln --no-restore`：0 错误。
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
- [x] Unity EditMode：321/321 通过；`dotnet build Foodular1.sln --no-restore`：0 错误。
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
- [x] Validate every changed script, wait for Unity compilation, and confirm
  that the Unity console has no errors (16 EditMode tests, 0 warnings/errors).
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
- [ ] Implement or verify pit entry and pit exit behavior.
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

- [x] Reconcile the Phase 2 planning document with assets already completed; the
  authoritative inventory is now `design/planning/asset-manifest.md`.
- [ ] Finish remaining UI panel artwork.
- [ ] Replace gear-button placeholders with the approved gear controls.
- [ ] Replace the heat text placeholder with the approved thermometer UI.
- [ ] Replace remaining flag, track-node, and runtime-generated UI placeholders.
- [x] Verify the current card, vehicle, and eight selectable track-background
  assets are present; target-resolution visual polish remains.

## P3 - Feature Completion

- [x] Support multiple AI participants through `aiOpponentCount` (0–3); deeper
  personality and difficulty tuning remain.
- [x] Implement slipstream and weather gating.
- [x] Implement team vehicle attributes and team-tech modifiers; balance remains.
- [x] Shuffle team trick cards into the normal deck lifecycle and replace batch
  hand submission with one-card select/confirm play, immediate trick resolution,
  and explicit end-of-card-phase behavior.
- [x] Implement the driver-selection flow as a catalog, session state, and
  runtime-built main-menu panel; connect the selected driver to race setup.
- [ ] Add sound effects.
- [ ] Add card-play, vehicle movement, bounce, and spin-out animations.
- [x] Add weather gameplay after track data and race rules are stable; the five
  design profiles are now wired into limits, slipstream, cooling, spin-out and HUD.
- [ ] Implement driver passive/signature effects; the catalog, XP tiers, and
  selection flow are present but the effects are not yet applied to movement.

## Open Decisions

- [ ] Select the replacement for the reverted Track Node Editor.
- [ ] Confirm how the Le Mans test track relates to the six national teams.
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
- [x] Unity MCP 10.1.2 package installed and connection verified; the local
  server is registered on `http://127.0.0.1:8080/mcp`.

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
- [x] Unity EditMode validation after this slice: 307/307 passed; dotnet builds
  complete with 0 errors.
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
  `dotnet build Foodular1.sln --no-restore` reports 0 errors (two existing MCP
  assembly-version warnings remain).

## 本次完成（2026-08-15 赛事回归与平衡）

- [x] 比赛 HUD 增加“返回主菜单”按钮；修复 RaceCanvas 预制体重建后的旧引用导致重复 HUD 的问题。
- [x] 所有主要菜单/比赛控制按钮统一接入短按压/释放缩放动画；运行态确认按钮存在且可触发场景切换。
- [x] 新增 `TrackTeamBalanceBenchmark`，覆盖 Resources 中全部赛道与六支车队，每图 12 场确定性比赛，报告写入 `design/balance/track-team-benchmark-2026-08-15.md`。
- [x] 平衡收敛：标准 AI 在预计抵达弯道时优先低值牌，风险窗口按预计移动量计算；中国队恢复设计案 Go 直道输出、操控从 -1 调为 0，并默认采用 Go→Go→Recover；美国直道加成调整为每回合固定 +1。
- [x] 最终验证：Unity EditMode 308/308 通过；`dotnet build Foodular1.sln --no-restore` 0 错误；MainMenu→Race→返回主菜单运行态冒烟通过，单一 HUD、按钮动画组件和控制台均正常。
