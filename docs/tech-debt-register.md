# 技术债台账

最后更新：2026-09-28
条目：4 | 估算总工作量：S + M + L + XL（T 恤尺码，不作数值相加）

优先级按 `(未修复影响 × 遇到频率) / 工作量` 计算。影响与频率取 1–5；工作量折算为 S=1、M=2、L=3、XL=4。分数用于排序，不代表精确工期。

| ID | 类别 | 描述 | 证据文件 | 工作量 | 未修复影响 | 优先级 | 登记日期 | 计划迭代 |
|---|---|---|---|---|---|---:|---|---|
| TD-002 | 测试债务 | 对近期变更的常规赛事启动、参赛者装配、结算及赛道可读性覆盖一次安全的完整 Play Mode 验收；车队教程已由用户验收，本项不重复计算该范围。 | `Assets/Scripts/Core/MVPGameManager.cs`；`Assets/Scripts/Gameplay/TrackReadabilityOverlay.cs`；`Assets/Tests/Editor/normal_race_reward_settlement_test.cs`；`Assets/Tests/Editor/race_participant_plan_builder_test.cs` | S | 高 | 12 | 2026-09-27 | Backlog |
| TD-001 | 架构债务 | `MVPGameManager` 仍有约 4,570 行并承担多类运行时职责；后续应以小步、可验证的边界继续拆分，而非整体搬迁协程和 Inspector 绑定。 | `Assets/Scripts/Core/MVPGameManager.cs`；`Assets/Scripts/Core/NormalRaceRewardSettlement.cs`；`Assets/Scripts/Core/RaceParticipantPlan.cs` | XL | 高 | 4 | 2026-09-27 | Backlog |
| TD-003 | 文档债务 | 仓库根 `AGENTS.md` 引用的五份 `.Codex/docs` 规范文档和 `production/session-state/active.md` 当前缺失，导致协作、编码与当前会话约定无法从其指定位置核对。 | `AGENTS.md`；`.Codex/docs/directory-structure.md`；`.Codex/docs/technical-preferences.md`；`.Codex/docs/coordination-rules.md`；`.Codex/docs/coding-standards.md`；`.Codex/docs/context-management.md`；`production/session-state/active.md` | M | 中 | 3 | 2026-09-27 | Backlog |
| TD-004 | 依赖/资源债务 | 字体与编辑器辅助资源分布在 `Assets/TmpFont`、`Assets/ttf`、`Assets/TmpTool` 等目录；应盘点用途、来源、许可和责任归属，再决定是否整理。当前已确认部分字体被菜单、比赛 UI、教程预制件或 Editor 测试引用。 | `Assets/Scripts/Editor/BuildHighQualityChineseFont.cs`；`Assets/Scripts/Editor/MainMenuBuilder.cs`；`Assets/Scripts/Editor/RaceCanvasBuilder.cs`；`Assets/Tests/Editor/menu_overlay_navigation_test.cs`；`Assets/Resources/Prefabs/UI/TutorialOverlay.prefab`；`Assets/Prefabs/UI/RaceCanvas.prefab`；`Assets/Scenes/MainMenu.unity` | L | 中 | 2 | 2026-09-27 | Backlog |

## 接受原因与重新评估条件

- **TD-002：** 先完成了 EditMode 可覆盖的拆分和规则测试；用户已经验收车队教程 Play Mode。本轮没有把该验收外推为常规赛事或所有场景均已验收。只在 Unity 编辑器空闲、场景状态安全时安排一次有明确检查清单的人工/受控运行；若编辑器不可用，保留为 QA 门槛，不重复尝试曾卡住的自动化场景切换。
- **TD-001：** 已先隔离常规赛事奖励结算与参赛者计划等边界。继续拆分大型管理器会触及序列化引用、协程时序和模式分支，接受此债务以避免无测试保护的广泛重构；开始下一阶段前先给出职责边界和行为回归测试。
- **TD-003：** 缺失文档的权威内容尚未确认；不以空白占位文件冒充恢复，也不创建 `active.md`。待维护者恢复原文或确认新的规范来源后关闭。
- **TD-004：** 当前目录名称不足以证明资源可删除或迁移；已知字体 GUID 被实际场景/预制件引用。先保留原路径和用户资源，待来源/许可/用途清单及引用迁移测试齐备后再整理。

## 本轮偿还记录（不代表关闭现有条目）

2026-09-29 外层移动保护/缺车位置边界：新增 23 项真实适配回归，本轮仅扩展已有测试文件与同步文档，无运行时代码/场景/资源/元数据修改。Unity 定向 `109/109`、全量 EditMode `1271/1271`，0 失败、0 跳过；首次夹具 CS1061 已修正重编译，旧程序集 86 项不计新增验证。最终 Console 仅 1 条预期 `no_such_track.json` 负向错误、0 警告、无编译错误，MainMenu 干净、idle/ready，许可证 entitlement 已解析。缺车降级路径只环形更新位置，不做圈数/完赛/换道结算；保持既有行为并记录限制，不外推为完整 headless/Play Mode 通过。有车路径最终位置/镜头与完整比赛仍待验收，TD-001/TD-002 不关闭。全部既有改动/未跟踪文件保留，未创建缺失 active.md/治理规范。下一工作包限定真实管理器普通赛奖励的模式隔离回归。

2026-09-28 连续跨线/非零起点测试基础设施：仅扩展既有测试文件，新增 14 项六队/重复选择/最终圈/四种起终点索引/边界拒绝/空遍历组合回归。Unity 定向 `86/86`、全量 EditMode `1248/1248`，0 失败、0 跳过。最终 Console 1 条 MCP WebSocket 警告、1 条预期 `no_such_track.json` 负向日志，无编译错误；MainMenu 干净、编辑器 idle/ready，启动核对的许可证 entitlement 已解析。未运行 Play Mode，手动推进真实协程不代表 Unity 帧调度/插值/镜头或完整比赛已验收；TD-001/TD-002 仍开放。下一工作包为外层移动保护与最终位置归属回归。保留全部基线修改与未跟踪文件，未改运行时代码、场景、资源或元数据，未创建缺失规范及 active.md。

2026-09-28 节点遍历与换道恢复：管理器内收敛小段节点遍历，选择返回后刷新车道，修复符合 GDD 的剩余节点目标；摄像机、最终位置及特效边界不搬迁。新增 10 项真实遍历/记录动画器回归，首次夹具漏按钮 3 项异常已修正，之后内/外两项坐标失败复现旧缓存缺陷。最终 Unity 定向 `78/78`、全量 EditMode `1234/1234`，0 失败、0 跳过；Console 1 条 MCP WebSocket 警告、1 条预期 `no_such_track.json` 负向日志，无编译错误，MainMenu 干净且 idle/ready，许可证日志显示 entitlement 已解析。未运行 Play Mode，手动推进遍历不代表真实帧调度/完整比赛；TD-001/TD-002 仍开放。下一工作包限定多次跨线/非零起点组合回归。全部既有修改/未跟踪文件保留，缺失 active.md/治理规范未创建，未改场景、资源或元数据。

2026-09-28 跨起点结算/换道等待边界：管理器新增同步登记适配入口，移动协程保持先结算圈数/完赛再等待换道的顺序。新增 14 项回归，Unity 定向 `75/75`、全量 EditMode `1224/1224`，0 失败、0 跳过。首次测试 CS0029 已修正并重新编译；最终 Console 仅 1 条预期 `no_such_track.json` 负向日志、0 警告、无编译错误，MainMenu 干净。测试手动组合登记/等待/回调，不验证实际移动动画或完整 GameLoop/Play Mode；TD-001/TD-002 不关闭。下一切片需复现换道后剩余节点仍持有旧车道缓存的源码风险。保留本轮全部基线修改和未跟踪文件，未改资源/元数据、未创建缺失规范或 active.md。

2026-09-28 换道反馈边界：`LaneChoicePresentationRules` 收敛按钮/确认文案及实际车道差值日志，修复已登记的内/外完成文案反写，不改换道规则。新增 22 项回归，Unity 定向 `53/53`、全量 EditMode `1210/1210`，0 失败、0 跳过。真实管理器→HUD 日志回调、边界拒绝、重复输入和手动推进的面板启闭/按钮资格已覆盖，不外推为完整 GameLoop 或 Play Mode 视觉/点击验收；TD-001/TD-002 不关闭。Console 为 1 条 MCP WebSocket 警告及预期缺失赛道日志，无编译错误，MainMenu 干净。保留所有本轮基线修改和未跟踪文件，active.md/五份规范仍缺失且未创建。

2026-09-28 印地换道规则边界：相邻车道计算与选择接受条件收敛至 `RaceLaneRules`，赛道适配器和玩家回调复用，保留输入/定位/日志边界。新增 26 项规则与真实回调回归；Unity 定向 `31/31`、全量 EditMode `1188/1188`，0 失败、0 跳过。Console 为 1 条 MCP WebSocket 警告及预期缺失赛道负向日志，无 C# 编译错误，MainMenu 干净。未运行 Play Mode或验证真实车辆视觉重定位；TD-001/TD-002 不关闭。既有换道日志内/外方向反写需下一切片修正，未在本轮改变；缺失治理文件及所有基线改动保留。

2026-09-28 维修区教程事件回归：新增 12 项真实管理器+Director 组合验证，覆盖 AI/继续比赛/待呈现/错误课/失败进站拒绝、实际预约与出站完成锁存及显式导航，无 Director 路径仍正常结算。Unity 定向 `47/47`、全量 EditMode `1162/1162`，0 失败、0 跳过；Console 1 条 MCP 警告与预期负向赛道日志，无编译错误，MainMenu 干净。没有运行完整 GameLoop/Play Mode或验证文件日志/存档，因此 TD-001/TD-002 不关闭。本轮仅补测试基础设施，既有代码与未跟踪资源保留，缺失治理文件不创建。

2026-09-28 维修区预约边界：入口窗口资格与玩家/AI 重复预约写入收敛到现有 `PitLaneRules`，不搬迁 UI/协程编排或改变规则。新增 30 项回归，Unity 定向 `72/72`、全量 EditMode `1150/1150`，0 失败、0 跳过；Console 1 条 MCP 警告和预期负向赛道日志，无编译错误，MainMenu 干净。迟到/重复按钮不会反转已提交决定或关闭其他输入门，无面板默认继续，AI 60% 阈值与跨圈重新选择均按真实适配器验证。同步协程测试不代替完整 GameLoop/Play Mode；TD-001/TD-002 仍开放，缺失 active.md/治理规范未创建，所有基线改动保留。

2026-09-28 维修区跨回合同步边界测试：新增 21 项真实入口/出口适配与 Session 组合回归，覆盖六队玩家/AI、预约延迟、热量守恒、停站后已消费标志仍由当回合 skipped 集合排除移动/尾流，以及下一回合恢复选挡。未调用完整 GameLoop，也未验证 UI/持久化；不会外推为普通赛事 Play Mode 完成。首次属性名编译错误已修正并重新导入；最终定向 `41/41`、全量 Unity EditMode `1120/1120`，0 失败、0 跳过。最终 Console 为 1 条预期缺失赛道负向日志，0 警告、无编译错误；MainMenu 干净。仅新增测试基础设施并同步文档，所有既有运行时代码、未跟踪文件和资源保留；TD-001/TD-002 仍开放。

2026-09-28 预约进站结算边界：`RacePitStopExecution` 隔离有序状态写入，管理器保留出口配置/科技与场景/教程适配，不改延迟进站或 A1 跳过集合。新增 20 项回归，强制刷新后定向 Unity EditMode `47/47`、全量 `1099/1099`，0 失败、0 跳过；首次旧程序集 27 项不作为新增证据。Console 仅 1 条 MCP 警告和预期缺失赛道测试日志，无编译错误；没有运行 Play Mode。保留缺配置时标准队成功进站在冷却后抛异常的既有契约，不在本轮偷偷改变运行行为；有效配置路径通过。不关闭 TD-001/TD-002，缺失规范/active.md 和原资源不动。

2026-09-28 全热量回收来源边界：CardDeck 三牌区共用回收实现并返回实际来源结果，旧 void API 兼容；管理器不再预扫描打转/维修区冷却表现。新增 14 项回归，最终定向 `24/24`（含既有打转组）、全量 Unity EditMode `1079/1079`，0 失败、0 跳过。首次 1 项测试因公开 API 拒绝注入永久热量而未构造所需异常状态，已修正测试并完整重跑。Console 为 1 条 MCP 警告及预期负向赛道日志，无编译错误；未运行 Play Mode，TD-001/TD-002 仍开放，不改场景、缺失规范或资源位置。

2026-09-28 热量教学反馈适配：从管理器抽出有序信号/detail 生成，保留仅玩家的实际操作条件、实时 Director 门控与 CN 回调后重读状态；不改变正常赛事、奖励或存储。新增 22 项回归（13 信号边界、9 真实管理器/Director），Unity 定向 `22/22`、全量 EditMode `1065/1065`，0 失败、0 跳过。Console 仅 1 条 MCP 警告及预期负向赛道日志，无编译错误；场景干净，没有运行 Play Mode。因此 TD-001/TD-002 仍开放，缺失规范/active.md 和既有资源不动。

2026-09-28 打转计算切片：纯 `RaceSpinRules` 承担天气加罚、未钳制计数、有效上限及恢复挡位，管理器保留原副作用与表现顺序。新增 23 项回归，真实支付不足路径验证热量守恒、限时热量销毁、下回合跳过/终态及收据重置；定向 `23/23`、全量 Unity EditMode `1043/1043`，0 失败、0 跳过。测试源码初次 CS7036 已修复，初次零用例运行未计入通过；最终 Console 仅 MCP 警告及预期负向赛道日志，无残留编译错误。未运行 Play Mode，不关闭 TD-001/TD-002；缺失规范/active.md 与原资源仍保持原状。

2026-09-28 热量费用边界：管理器委托 `HeatPaymentCostRules` 消费技能倍率/被动折扣/黑面包并取得费用与教程元数据，实际支付/救援/打转/表现仍留在原适配器。新增 15 项规则与真实支付回归，Unity 定向 `38/38`、全量 EditMode `1020/1020`，失败 0、跳过 0；Console 为 1 条 MCP WebSocket 警告及预期负向赛道日志，无编译错误。没有运行 Play Mode，也未验证普通支付不足的打转动画或教程面板；TD-001/TD-002 保持开放，不扩大验收结论。

2026-09-27 将 CareerPersistence.cs 的仓库与应用服务拆为独立代码边界，并补充序列化失败不覆盖既有存档的回归（Unity EditMode 12/12 定向、808/808 全量通过）。这项维护不改变 TD-001 至 TD-004 的状态，也不代表大型比赛协调器或 Play Mode 验收债务已关闭。

2026-09-27 从 `RaceCameraController` 提取了纯 `RaceCameraPointerDragState`，保持视口内起始、拖拽按住、逐帧差值、0.25 平方像素阈值及释放语义不变；相机输入边界新增三项状态回归。摄像机相关用例 13/13（包含于全量），Unity 2022.3.62f3c1 全量 EditMode `811/811`，失败 0、跳过 0。此小步不关闭 TD-001。

2026-09-27 将科技树存档 DTO 与节点集合过滤转换抽至 `TechTreeProfileCodec`；PlayerPrefs 缓存、键名、旧键迁移和默认资料仍由 `TechTreeProfileStore` 管理。新增五个编解码用例，Unity 2022.3.62f3c1 全量 EditMode `816/816`，失败 0、跳过 0。此边界改进不关闭 TD-001。

2026-09-27 天气门控小切片：将“指定圈是否已触发”的纯判断收敛到 `RaceLapWeatherRules.ShouldRollWeatherForLap`；`RaceWeatherState` 使用已知圈号，不再构造无意义的上一圈/`int.MaxValue` 圈长 transition。新增重复圈、下一圈和天气禁用三项回归；Unity 2022.3.62f3c1 全量 EditMode `819/819`，失败 0、跳过 0。此小步不关闭 TD-001。

2026-09-27 诊断边界小切片：`RaceLogAnalyzer` 将 `[DISCARD]` 数量转换改为 invariant `TryParse` 并将溢出报告为分析错误；结构完整度仍与内容有效性分开。新增 3 个边界用例，Unity 2022.3.62f3c1 全量 EditMode `822/822`，失败 0、跳过 0。此维护不关闭 TD-001/TD-002。

2026-09-27 生涯存储异常边界小切片：加载查询异常返回 `Invalid`，存在性查询异常采取 fail-closed，保存/放弃接口异常返回失败；新增加载、无确认覆盖保护、删除失败及服务状态保留回归。定向 EditMode `17/17`、全量 `827/827`，失败 0、跳过 0。此维护不关闭 TD-001/TD-002。

2026-09-27 将科技效果到 `TechModifiers` 的布尔/数值映射归并到 `TechTreeRules.TryApplyModifierEffect`，供普通科技聚合与 UK「日不落」目标科技复用；保持升级取最大值和容量类累加语义。新增效果策略边界用例，科技树定向 EditMode `87/87`、全量 `837/837`，失败 0、跳过 0。此维护不关闭 TD-001/TD-002。

2026-09-27 为 `RacePhaseState` 补齐阶段 × 输入门控矩阵、空输入、陈旧门与 gear→cards→animation→game-over 顺序回归；定向 EditMode `20/20`，全量 `854/854`，失败 0、跳过 0。此项为后续阶段服务拆分建立现状契约，不改变 `MVPGameManager` 协程编排，不关闭 TD-001；未运行 Play Mode，因此也不推进/关闭 TD-002。

2026-09-28 将 `RaceInputState` 的五个互斥输入等待标志收敛为单一活动门，保留原有公开查询与选择回调；迟到的旧门结束回调不会关闭后来打开的门。输入/阶段定向 EditMode `30/30`、全量 `856/856`，失败 0、跳过 0。此项减少输入状态组合与协程时序耦合风险，不关闭 TD-001/TD-002；常规赛事 Play Mode 仍待验收。

2026-09-28 日志工具边界小切片：将最新 `.log` 文件选择从 Editor 菜单移入 `RaceLogFileAnalyzer`，同时间戳按序数路径确定性选择，保持菜单原有的缺目录、空目录与读取失败反馈。新增 3 项临时目录回归，定向 EditMode `3/3`、全量 `859/859`，失败 0、跳过 0。该切片改进 TD-002 的日志验收工具，但不替代常规赛事 Play Mode，因此 TD-002 保持开放。

2026-09-28 车手技能边界切片：从 `MVPGameManager` 提取只读 `DriverSkillRaceContextRules` 与 `DriverSkillPresentationRules`，分别负责发动条件所需的圈数/热量/后车距离事实和按钮文案；保留同圈、身后 1–3 格、排除完赛/退赛车辆及主动技能持续文案优先级。新增 14 项回归，Unity 2022.3.62f3c1 定向 EditMode `14/14`、全量 `873/873`，失败 0、跳过 0。Console 有 1 条 MCP WebSocket 重载警告和负向测试预期的 `no_such_track.json` 日志，无 C# 编译错误。未运行 Play Mode；TD-001/TD-002 保持开放，缺失的治理文档和资源均未创建/迁移。

2026-09-28 出牌要求边界切片：`CardPlayRules.GetSpeedCardRequirement` 从玩家当前状态生成基础必出张数/额外可选容量，`GearRequirementFeedbackRules.FormatRequirementLabel` 接管原档位说明；`MVPGameManager` 保留对外兼容入口。新增六队、CN 连续 Go、负额外槽钳制、实时状态读取及管理器委托等 13 项回归。定向 Unity EditMode `13/13`、全量 `886/886`，失败 0、跳过 0。未改缺牌惩罚、手牌交互、技能效果或协程，TD-001/TD-002 保持开放；上一轮技能边界改动完整保留。

2026-09-28 雷雨赛前排位边界：将 `MVPGameManager` 的 12 车显示格/车道计算移入已有 `TrackPresentationRules.TryGetThunderstormGridSlot`；管理器继续提供当前回合、名单索引和赛道数据并执行场景定位，不改玩法位置。新增 12 项回归覆盖 2/4 车道、起点环绕、非零起点、首回合关闭、非雷雨阵容与无效输入；Unity 定向 EditMode `12/12`、全量 `898/898`，失败 0、跳过 0。Console 仅 MCP 重载警告和负向赛道测试预期日志；Play Mode 未运行，TD-001/TD-002 保持开放，前两轮未提交改动保留。

2026-09-28 比赛状态日志边界：从管理器提取只读 `RaceStateLogFormatter`，保持 `[STATE]`/`[CARDS]` 字段顺序、布尔值、牌序和 CN/额外槽数据；不改变记录时机或文件写入。新增 7 项回归；修正测试夹具的抽牌接口与明确热量入手牌路径后，Unity 定向 EditMode `7/7`、全量 `905/905`，失败 0、跳过 0。最终 Console 为 1 条 MCP WebSocket 重载警告及 1 条负向赛道测试预期日志，无编译错误。Play Mode 未运行，TD-001/TD-002 保持开放；既有未提交边界切片和资源均保留。

2026-09-28 回合开始分流边界：现有 `RaceTurnRules.GetStartAction` 接管 A1 参与者动作优先级，管理器仍执行预约进站、教程等待、失控跳过和选挡；仅预选进站不会提前执行，终态优先于所有标志，判断不消费任何状态。新增 35 项回归，Unity 定向 EditMode `39/39`、全量 `940/940`，失败 0、跳过 0。最终 Console 1 条 MCP WebSocket 警告与 1 条负向赛道测试预期日志，无编译错误。Play Mode 未运行，TD-001/TD-002 保持开放；未创建缺失治理文档或移动资源，全部既有改动保留。

2026-09-28 回合收尾执行边界：新增 `RaceTurnCleanup`，从管理器隔离牌区清理顺序与运行时效果适配，保持速度牌→技能→限时牌→非终态科技→打出引用清空的顺序和异常传播。新增 9 项回归验证实际卡牌实例、永久热量守恒、终态/科技门控和回调顺序；Unity 定向 EditMode `9/9`、全量 `949/949`，失败 0、跳过 0。Console 1 条 MCP WebSocket 警告、1 条预期缺失赛道负向日志，无编译错误。未运行 Play Mode；TD-001/TD-002 保持开放，保留所有既有未提交改动和资源，未创建缺失会话/治理文件。

2026-09-28 跨回合测试保护：新增管理器真实收尾适配、Session 回合重置和实际补牌/回收的 11 项跨模块 EditMode 回归。验证六队牌序/实例和热量守恒、JP 牌槽只消费一次、终态及进站/恢复/跳过证据；临时对象不激活，不执行场景启动、普通奖励或持久化。定向 `11/11`、全量 `960/960`，失败 0、跳过 0；最终 Console 仅 1 条 MCP 警告和 1 条预期负向赛道日志，无编译错误。此轮新增测试基础设施而非运行时拆分；TD-001/TD-002 保持开放，所有既有修改保留。

## 维护规则

2026-09-28 冷却结算/表现边界：CardDeck 返回 `HeatCoolingResult` 实际来源，管理器移除通用冷却数量推算及 Grill 收据扫描；旧 int 接口、通用优先级和支付实例规则保持兼容。新增 11 项回归，Unity 定向 `39/39`、全量 EditMode `1005/1005`，失败 0、跳过 0。Console 1 条 MCP WebSocket 警告及 1 条预期负向赛道日志，无编译错误。MainMenu 干净、未运行 Play Mode；TD-001/TD-002 仍开放，缺失 active.md 未创建，所有基线改动保留。

2026-09-28 Grill 专项修复完成（替代上轮待修复状态）：资格和冷却量统一为“未使用”，CardDeck 支付时记录实际牌实例，专属冷却只回收仍持有的永久支付牌，经弃牌堆返还；不会拿旧热量、限时热量或已冷却收据替代。每场一次和本回合记录分别重置，普通支付目的地/冷却顺序保持原样。新增 9 项并更新 3 项回归，Unity 定向 `123/123`、全量 EditMode `994/994`，失败 0、跳过 0。Console 1 条 MCP WebSocket 警告和 1 条预期负向赛道日志，无编译错误；MainMenu 干净，Play Mode 未运行。TD-001/TD-002 保持开放，既有修改全部保留。

2026-09-28 科技收尾边界：`RaceTurnTechnologyCleanup` 承担顺序和即时状态读取，管理器只保留效果与表现适配；新增 10 项回归，Unity 定向 `27/27`、全量 EditMode `985/985`，失败 0、跳过 0。最终 Console 1 条 MCP WebSocket 警告与 1 条预期缺失赛道负向日志，无编译错误；MainMenu 干净、编辑器空闲。未运行 Play Mode，TD-001/TD-002 保持开放，全部基线改动保留。

此切片暴露既有规则缺陷：Session 的 Grill 资格要求未使用，规则冷却量却仅在已使用时非零，导致收尾无冷却。新增两项测试记录当前行为而非确认设计正确；已列入任务清单专项修复，届时替换现状断言并核对 GDD 的弃牌堆冷却来源和每场一次语义。该问题不是本轮引入，亦不因 `985/985` 而关闭。

2026-09-28 最后冲刺收尾边界：`DriverSkillRules` 接管纯判断与代价转换，管理器仍注入有效打转上限、执行状态修改和日志；保持原等级恢复豁免，不清除既有退赛/跳过标志。新增 15 项回归（规则 9、真实管理器收尾 6），覆盖技能退赛后科技回调被阻止。Unity 定向 EditMode `26/26`、全量 `975/975`，失败 0、跳过 0。Console 仅 1 条 MCP WebSocket 警告及 1 条预期缺失赛道负向日志，无编译错误。未运行 Play Mode，TD-001/TD-002 保持开放；既有未提交修改与资源完整保留，未创建缺失会话/治理文件。

每个条目关闭时记录验证证据、日期与变更；超过三个迭代仍未处理时，重新确认其接受原因和优先级。登记不授权删除或迁移资产，也不替代项目的 GDD、测试记录或会话状态文档。
