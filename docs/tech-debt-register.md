# 技术债台账

最后更新：2026-09-27
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

## 维护规则

每个条目关闭时记录验证证据、日期与变更；超过三个迭代仍未处理时，重新确认其接受原因和优先级。登记不授权删除或迁移资产，也不替代项目的 GDD、测试记录或会话状态文档。
