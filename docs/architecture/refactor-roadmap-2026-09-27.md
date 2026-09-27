# Foodula1 代码整理与渐进式重构清单

日期：2026-09-27。依据当前工作区、`Assets/Scripts/`、`Assets/Tests/` 和已接受的 ADR-002 整理；这是实施顺序，不替代现行 GDD 或玩法规则。

## 当前结构

- 当前有 137 个 `Assets/Scripts/**/*.cs` 文件、63 个 Editor 测试源码；两张正式场景为 `MainMenu` 和 `Race`。
- ADR-002 的“场景协调器 → 可测试规则 → 数据”分层仍适用。当前行数：`MVPGameManager` 4,570；教程定义主文件 1,035，三个课程 partial 合计 566；`CardHandUI` 1,031；`HUDUI` 961；`RaceUILayoutController` 599。大文件仍是变更风险指标，不以机械拆文件作为验收。
- 当前工作区开始整理时已有 13 个未提交修改，涉及 US 地标和车队教程。它们属于在途工作，应在各自功能验收后再安排涉及同一文件的重构。
- `Assets/ttf`、`Assets/TmpFont`、`Assets/TmpTool`、`Assets/TmpScenes` 等目录需要先做资源引用和来源核对。Unity 资源路径与 `.meta` GUID 可能被场景、预制体或运行时加载使用，现阶段不移动或删除。
- `AGENTS.md` 所指的 `production/session-state/active.md` 与部分 `.Codex/docs/` 文档在当前工作区缺失。保留为文档链路问题，不建立空占位文件。

## 重构顺序与验收门槛

| 顺序 | 范围 | 要解决的问题 | 第一小步 | 验收 |
| --- | --- | --- | --- | --- |
| 1（已做） | `CardDeck` 固定牌序初始化 | 旧代码在验证完新牌序前清空现有手牌、牌库和弃牌堆；非法配置会破坏当前对局 | 完整验证并暂存新牌序后再替换状态，抽牌/回收路径共用可抽牌判断 | 旧牌区与热量池在非法配置后保持原样；`CardDeckTest` 与全量 EditMode 通过 |
| 2（代码切片完成） | `MVPGameManager` 对局协调 | 启动名单与普通赛奖励和场景生命周期纠缠 | 抽出 `NormalRaceRewardSettlement` 与纯 `RaceParticipantPlanBuilder`；GameObject、协程、AI 组件和 Inspector 引用仍留在管理器 | 新模块定向 4/4 + 4/4、全量 EditMode 782/782；真实普通赛 Play Mode 尚未验收。后续跨阶段服务化不属于此低风险切片 |
| 3（已做） | `TutorialScenarioDefinition` | 六队课程工厂集中在一个文件 | 按基础勒芒、CN/US、其他车队提取 partial 源文件，保留定义和工厂调用面 | 六队脚本/目录/门控定向组 142/142，全部 782/782 |
| 4（代码切片完成） | `CardHandUI` / `HUDUI` | 输入和结果文案规则贴在 MonoBehaviour 中，布局应保持独立 | 抽出 `CardHandInteractionRules`、`RaceResultPresentationRules`；继续由现有 `RaceUILayoutController` 管布局 | 手牌/预览 22/22，赛果格式/面板 2/2，全量 782/782；16:9 Play Mode 视觉与点击验收未完成 |
| 5（引用审计完成；未迁移） | 资源和文档目录 | 多套字体和临时目录的所有权/依赖不清，缺失的治理文档不能用空文件伪造 | 对 tracked 场景、预制体、资源、脚本中的 GUID 与资源路径做有界核对；逐个保留明确运行时/工具引用，未证明安全的资产不动 | HQ TMP 字体的 GUID、字体构建脚本路径、测试 SDF 路径已识别；四个目录均保留。失效文档路径和完整运行时视觉仍需后续处理 |

## 本次实施记录

生涯持久化边界切片：CareerPersistence.cs 保留存档 DTO 与 schema codec；键值接口、加载结果和 CareerRepository 移至 CareerRepository.cs；事务式状态更新及存档协调的 CareerModeService 移至独立文件。调用面与运行规则不变；序列化失败时必须保留既有存档键值，新增定向回归。生涯持久化组 12/12、全量 Unity EditMode 808/808，失败 0、跳过 0。

后续小步拆分：将八张正式赛道的名称、ID 与稳定顺序收敛为只读 `OfficialTrackCatalog`；
`TrackSelectionState` 现在只负责会话选中值，仍保留 `AvailableTracks` 兼容入口；生涯赛历
直接读取正式目录，避免规则层借用有状态的 UI 选择服务。新增目录完整性/不可变回归，并验证
自由赛事和生涯已有轨道顺序测试。Unity Editor 2022.3.62f3c1 全量 EditMode `807/807`，
失败 0、跳过 0。

`CardDeck.InitializeExactOrder` 先验证并复制完整的非热量牌序，再改动牌堆和热量池引用。抽牌、可抽牌计数、弃牌洗回与直接弃置共用同一个可抽牌判定。新增回归覆盖非法新牌序不破坏当前手牌、剩余抽牌、已支付热量和弃牌堆；`CardDeckTest` 31/31。当前最终全量 Unity EditMode 为 782/782，0 失败、0 跳过。

普通自由赛 RP、IT L3 主场加成、车手 XP、报告格式与存储调用由 `NormalRaceRewardSettlement` 承担；场景协调器仍按原结算分支调用，模块入口对教程/生涯再次短路，存储回调注入便于回归。`RaceParticipantPlanBuilder` 负责按教程→生涯→自由赛优先级解析参赛车手、车队、名称、初始 XP 和生涯科技快照，GameObject/AI 创建仍在管理器。两个模块定向组各 4/4；全量 782/782。

教程课程定义按 `TeamCourses`、`CnUsCourses`、`LeMansCourse` 分离；六队脚本、固定牌序和菜单步骤仍通过原 `TutorialScenarioDefinition` 静态 API 提供。CardHand 的键盘选牌/动作快捷键/效果文案已放入 `CardHandInteractionRules`；HUD 赛果富文本放入 `RaceResultPresentationRules`；布局仍由 `RaceUILayoutController` 负责。相应定向 UI 测试 22/22、2/2；全量 782/782。

后续小型维护切片已将 `TechTreeRules.ComputeModifiers` 与 UK「日不落」目标科技合并的布尔效果映射收敛为同一纯规则入口；保留现有数值效果聚合策略，回归覆盖全部 17 种标志、数值效果旁路及真实 US 目标科技合并。全量 Unity EditMode `804/804`，0 失败、0 跳过。

赛道摄像机小切片：鼠标拖拽的 armed/held/release 差值状态现在由独立的 `RaceCameraPointerDragState` 维护；`RaceCameraController` 仍采样 Unity 输入、判断摄像机视口、接收纯状态输出并执行平移，因此场景引用和操作流程不变。摄像机相关 EditMode 13/13 纳入 Unity 2022.3.62f3c1 全量 EditMode `811/811`（失败 0、跳过 0）；覆盖未 armed、按住阈值、松开态不改起点和显式结束。迷你地图的渲染资源和动态标记目前仍由摄像机适配器创建，后续只有在发现实际可验证边界时再拆，不为了减少行数机械搬动 Unity 对象创建。

科技树存档边界小切片：`TechTreeProfileCodec` 独立负责稳定 JSON DTO 的编码/解码与节点过滤；`TechTreeProfileStore` 仍掌管 PlayerPrefs 键、缓存、旧拼写迁移和默认演示配置。新增回归覆盖持久资料往返、未知/未解锁节点过滤、由存储键确定车队，以及比赛内一次性标记不进入持久数据。Unity 2022.3.62f3c1 全量 EditMode `816/816`，失败 0、跳过 0；不关闭 TD-001。

资源核对扫描了 267 个 tracked 场景/预制体/资源/脚本文本中的 32 位 GUID 形标识，并核对四个候选目录的已跟踪文件及直接路径引用。`TmpFont` 下唯一直接出现在场景/预制体 GUID 中的 HQ TMP 字体用于 `MainMenu`、`RaceCanvas` 和教程预制体，字体构建脚本还按路径访问源/输出；`Assets/ttf/msyh SDF.asset` 被 Editor 测试按路径加载；`TmpTool` 是自定义 Inspector 工具，`TmpScenes` 是样例场景。扫描不是 Unity 完整依赖图，许可/来源也未确认，因此本次没有移动或删除任何资产。

验证证据：Unity Editor 2022.3.62f3c1，活动场景 `Assets/Scenes/MainMenu.unity`、非播放、非编译、资源已刷新且 ready；最终 EditMode 782/782、失败 0、跳过 0。最终 Console 查询没有剩余 C# 编译错误；记录到 MCP WebSocket 未初始化的瞬时警告以及负向缺失赛道测试日志。Play Mode 视觉/点击烟测未完成：已知一次 Play Mode 切换曾卡住，不重复尝试。资产的删除/搬迁需要另行确认来源许可和使用范围。

后续纯规则收敛：`RaceLapWeatherRules.ShouldRollWeatherForLap` 独立表达天气开启且指定圈未重复的门控；`RaceWeatherState` 直接提交已知圈号，不再伪造圈数 transition。保留 `Advance` 的圈推进、完赛与天气顺序契约。新增三项门控边界用例；Unity 2022.3.62f3c1 全量 EditMode `819/819`，失败 0、跳过 0。

诊断适配器小切片：`RaceLogAnalyzer` 使用 invariant `TryParse` 读取 `[DISCARD]` 计数；溢出计数被记录为内容校验错误，不再让纯分析入口抛出 `OverflowException`。结构完整但包含无效值的日志仍可区分 `IsValid=false` 与 `IsComplete=true`。新增两方向溢出及最大整数边界测试；Unity 2022.3.62f3c1 全量 EditMode `822/822`，失败 0、跳过 0。

2026-09-27 生涯持久化边界维护：`CareerRepository` 将加载时键查询异常转换为 `Invalid`，存档存在性查询遇错按“可能存在”处理以避免无确认覆盖；保存/放弃异常返回失败，`CareerModeService` 仍只在仓库成功后替换运行态。新增加载、覆盖保护、删除查询/写入与服务状态保留回归；定向 EditMode `17/17`、全量 `827/827`，失败 0、跳过 0。

结论：路线图定义的低风险代码边界均已拆分并有 EditMode 回归；“重构”不等于删除未核实资源或把 `MVPGameManager` 拆成无意义 partial 文件。后续若继续削减管理器耦合，应先为完整比赛阶段服务边界设计 ADR/验收，再逐阶段改动；本轮保留为明确后续架构工作，而非声称管理器已完成服务化。

阶段输入门控回归基线：`RacePhaseStateTests` 现在覆盖四种阶段与三种门状态的组合、所有阶段的空输入、切换阶段时遗留输入门的拒绝，以及档位→出牌→动画→完赛的完整门控序列。定向组 `20/20`、Unity 2022.3.62f3c1 全量 EditMode `854/854`，失败 0、跳过 0。此项只刻画现有纯状态边界；没有改变管理器协程和 UI 编排，也不关闭 TD-001/TD-002。下一次管理器阶段拆分仍需单独限定边界并做正常赛事运行验收。

输入门状态收敛：`RaceInputState` 现在只存储一个活动门，仍通过原有五个 `WaitingFor*` 属性供管理器和 UI 查询；新门打开时旧门立即关闭，迟到的旧门结束回调不影响当前门。新增连续换门与陈旧关闭测试；输入/阶段定向 EditMode `30/30`、全量 `856/856`，失败 0、跳过 0。场景协调和协程边界未改，TD-001/TD-002 保持开放。

2026-09-27 科技效果聚合维护：`TechTreeRules.TryApplyModifierEffect` 统一处理布尔映射、升级型数值取最大值及容量/手牌/阈值数值累加；`ComputeModifiers` 与 `RaceSession` 的 UK「日不落」目标科技合并共用该规则入口，删除重复字段映射。科技树定向 EditMode `87/87`、全量 `837/837`，失败 0、跳过 0；现有聚合策略与玩法未变。
