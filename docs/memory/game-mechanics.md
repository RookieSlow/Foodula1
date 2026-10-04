# Game Mechanics

- 2026-10-04 16:02 UTC 轮：有车和缺车移动现在共享逐节点的起终点→圈数/天气/完赛→适用印地换道等待→后续节点顺序；只跳过不存在的车体动画，不再让纯比赛状态取决于车体是否创建。缺车迭代器直接嵌套返回，既支持原生调度也能在 inactive 夹具中完整 drain；原正常车体/相机路径未改。原生等价测试覆盖六组零/负移动、非零起终点及多圈/中途完赛×四种车体状态（24 比较、1 注册测试），天气事件顺序、一次性名次和终态相同。第二项原生测试由真实 Start 连续跑完银石 77 格/3 圈，六队、默认四车/2/6/12 车、三次真实 HandleSpin 退赛后 AI 完赛共 10 场景；控制速度一牌序、关闭天气/维修/花招以界定时长，未设置终态/圈数或直接调用结果。核验排名、普通 RP/XP、真人 RP→XP 恰一次保存、退赛车手零 XP、结果 HUD/输入关闭及唯一 RACE_END/Completed 日志。最终全量 2489/2489（2026-10-04T16:29:12.1620194Z），失败/跳过 0；缺车旧错误预期的 20 测试及 1 inactive 同步移动夹具更新后 167/167。默认随机牌序、渲染/相机、真实玩家存储及生涯完整比赛仍未验收；正常自由赛/教程/生涯规则不变。

- 2026-10-04 15:32 UTC startup boundary: production display, XP and technology adapters retain their previous call order and defaults; career launch validation uses the same repository dependency as settlement. Normal startup reads only human XP/profile (AI technology remains transient); technology-disabled races never read a profile. Tutorial precedence clears career launch and ignores free roster/progression; valid careers read their own snapshot and retain the existing human XP read, without writing/advancing a career or reading ordinary tech. Twenty-seven new initialization/isolation cases passed within focused 104/104 and full 2487/2487. One native UnityTest now includes fifteen scenarios, five exercising real Start and default/2/4/6/12-car input wiring on the current 77-node Silverstone. Display/player persistence are injected, cars/Canvas are headless, and execution stops before startup races move/finish; this is not visual, full-race or real-storage acceptance. Gameplay rules and source track data were not changed.

- 2026-10-04 14:54 UTC 原生回合调度证据（规则未变）：实际 GameLoop 在六队直道夹具中完成两回合输入→出牌→移动→弃牌确认→收尾→再次抽牌；临时热量销毁、已出牌进入弃牌堆、热容量守恒及 AI 移动均验证。最后冲刺经真实技能输入和收尾提交代价，Tier 1 次回合恢复被消耗且不开放人类输入，Tier 3 正常重新开放输入；达到失控上限则两层级都退赛、下回合排除，但 AI 仍能出牌且比赛未结束。10 场景为 1 个原生 UnityTest；加日志隔离/真实赛果存储接线共新增 14 测试，最终原生全量 2460/2460，失败/跳过 0。夹具使用合成直道、无车体表现与人工会话注入，绕过正式 Start，结算存储被隔离，故不是完整普通赛、教程、生涯、赛道视觉或真实玩家存储验收。日志覆盖仅编辑器 SessionState，测试后恢复；玩家可见默认规则/路径不变。

- 2026-09-30 中国 EV 旧档研发门槛：同层标准与 EV 科技若编号相同只占一个门槛名额，避免旧存档同时持有两版时提前解锁；不同编号可混合累计。其他五队仍只计标准通用科技。Unity 科技组 179/179、全量 EditMode 1925/1925，失败/跳过 0；未验证真实存储/Play Mode。

- 2026-09-30 通用科技研发归属：新中国档案只购 EV 版通用线，其他五队只购标准线；旧中国档案若已持有标准 L1，可继续研发对应标准 L2，以免中断旧升级链。UI 展示与规则购买口径一致，既有存档/激活节点不被迁移或删除。Unity 科技组 175/175、全量 EditMode 1921/1921，失败/跳过 0；Play Mode/真实存储未验收。

- 2026-09-30 新版科技树购买归属：车队专属科技只能由本队研发购买；英国「日不落引擎」的赛中借用仍是临时效果，不是替别队购买或写入档案。六队所有 L1/L2/L3 专属目录新增 6 项参数化回归；Unity 科技组 165/165、全量 EditMode 1911/1911，失败/跳过 0。实际 Play Mode 与旧档案迁移未验收。

- 2026-09-30 US L1 得来速：按本次基础移动经过的每处不同地标给 +1，单次经过起点与中点可合计 +2；奖励位移自身不再触发额外一轮得来速。纯计数规则与管理器实际移动均覆盖未经过/单地标/双地标、科技关闭，共新增 10 项；Unity 定向 EditMode 126/126、全量 1885/1885，失败/跳过 0。该双地标差异按新版 GDD 修正；Play Mode 未验收。

- 2026-09-30 US 母亲之路双地标顺序：单次正向位移若同时经过起点与中点，先结算实际先到的地标；出发位置恰在地标时该格要下一圈重新经过才算。若先到的地标复兴追加位移使赛车完赛，后到地标不再计次。新增 7 项顺序/真实管理器回归，Unity 定向 EditMode 89/89、全量 1875/1875，失败/跳过 0；Indianapolis 车道交互及普通/生涯 Play Mode 未验收。

- 2026-09-30 正向地标越线：`CrossedPositionForward` 同时接受未取模的正向位移终点与历史起点取模形式；原地零位移不算再次经过，同一圈或跨圈经过计入 US 母亲之路及 IT Drive-Thru。US L3 第一地标复兴追加位移若越过终点并完赛，第二地标在该次基础移动中不再结算；未完赛仍继续结算。新增 3 项回归，Unity 定向 14/14、全量 EditMode 1868/1868，失败/跳过 0；Play Mode、Indianapolis 分岔交互和真实存储未验收。

- 2026-09-30 维修预约保持：`RegisterPitEntryCrossing` 在实际越过入口后，如该赛车已有 `pitStopScheduled`，保持下回合进站和本圈选择状态，避免多段位移的第二次入口判定把预约撤销；没有预约的首次穿越语义不变。直接入口及 US L3 复兴整合新增 2 项回归，Unity 定向 54/54、全量 EditMode 1865/1865，失败/跳过 0。Indianapolis 同步即时位移的换道交互仍未验收。

- 2026-09-30 即时科技位移穿过维修入口：US 复兴与 CN 阴阳茶现在以追加位移的独立起止位置调用既有 `RegisterPitEntryCrossing`；有预定则下回合进站，无预定则重开下圈接近入口的选择机会，不改变本回合位置或热量结算。新增 3 个真实管理器回归场景，Unity 定向 11/11、全量 EditMode 1863/1863，失败/跳过 0；Indianapolis 车道交互和 Play Mode 未验收。

- 2026-09-30 US L3 复兴追加位移：立即移动现与 CN 阴阳茶共用终点线跨越结算，按赛道节点取模、更新圈数/完赛并同步区域热量容量；US 原直接 `position += bonus` 不记圈的缺口已修复。新增 US 两个参数化场景及 CN 既有规则回归，Unity 定向 8/8、全量 EditMode 1860/1860，失败/跳过 0。维修入口、Indianapolis 分岔以及普通/生涯 Play Mode 尚未验收。

- 2026-09-30 US L3 母亲之路复兴：整场一次机会的可用热量按 `CardDeck.CountHandHeatRestorableToEngine` 预检；`ReturnHeatCardsToPool` 返回净增引擎热量，而非移出手牌总数。永久牌先清偿 BBQ 区域借用、其后才进入引擎；限时牌销毁但不贡献位移。没有净回引擎的牌不发动终极。Unity 定向 38/38、全量 EditMode 1857/1857，失败/跳过 0；尚未验证追加位移跨终点线/维修入口、衰退期玩家可选修复与实际 Play Mode。

- 2026-09-30 JP L3 番狂わせ：连续 3 回合临时取得 L2 四种汤底的并集，被选中的汤底不重复计数。豚骨直道 +1 与味噌尾流 +1 已进入会话实际移动计算；酱油弯心 +1 沿用独立来源的弯心叠加公式；盐味每回合冷却 1 与转子自身每回合冷却 1 分别结算，转子结束仅保留赛前选中的汤底。Unity 科技组 139/139、会话组 67/67、全量 EditMode 1852/1852，失败/跳过 0；未以此替代普通/生涯 Play Mode 验收。

- 2026-09-30 BBQ 区域容量后续快照：新版 GDD 的 +2 引擎热量现仅在地标 ±5 格内借用（US 与 UK 有效借用），离区先移除引擎可用份额；已支付份额在后续冷却时退役，重入不叠加。热量表将清理中已提交且已入弃牌堆的同一实例只计一次。Unity 定向 EditMode 143/143、全量 1830/1830，最终失败/跳过 0；Play Mode、每图实操和真实存储未验收。下面较早的“全局容量占位”描述仅是当时快照。

- 2026-09-30 BBQ 实际接线：合法区域热牌保持 Heat/value 0/原实例，作为速度 2 占普通速度槽；人类手牌和键盘焦点、AI 候选/精确组合、两者提交共用实时资格，离区前已提交牌仍为速度 2。移动、弯道、车型每牌加成、UI 与 `[CARDS]` 日志使用有效值；旧固定 +2 位移删除。打转/维修全量冷却前移出已打热牌，后续收尾不得重复回收。新增 12 项，Unity 定向 95/95、全量 EditMode 1823/1823，失败/跳过 0；两次重载初始化超时不计通过。区域引擎容量 +2 仍为全局占位，尚非 GDD 对齐；没有新 Play Mode/真实存储证据。

- 2026-09-30 BBQ 热牌底层：显式带 Session/赛道长度的提交入口只按提交时当前位置和实时有效科技准许热量；原入口仍禁止热量。热牌以速度 2 占普通牌槽，但保留 Heat 类型/value=0/原实例及限时标记，失败整组不移牌、不消耗 ATTACK。已提交数值查询不因移动出区失效；回合收尾永久热量进入弃牌堆等待冷却，限时热量销毁并合并临时牌反馈，技能→临时反馈→科技顺序不变。新增 40 项，Unity 定向 91/91、全量 1811/1811，失败/跳过 0。尚未接入 UI/AI、管理器速度统计与日志；固定 +2 位移/全局容量占位仍在，不能当作玩家可用 BBQ 或 Play Mode 完成证据。

- 2026-09-29 BBQ 资格边界：`RaceSession.HasSmokedBBQAtPosition` 查询即时有效科技（含 UK 借用 US）及调用者明确传入的位置，不隐式读取玩家当前位置、不缓存、不发放收益。移动占位效果和回合末烟幕共用此入口，保持各自原投影位置与时机；八张真实地图逐节点、重叠区域不叠加、动态科技变更和尾流只给后车新增 34 项回归。定向 100/100、全量 EditMode 1771/1771，失败/跳过 0。当前固定 +2 位移及全局 +2 容量仍为待修正占位；不能据此认定热牌速度 2、区域临时容量或完整 Play Mode 已完成。

- 2026-09-29 IT 红色跃马 RP：冠军基础 ×1.5 保留；第二次 ×1.5 只在本场地图 country=IT 且实际 DriverProfile.Team=IT 时授予。车型/techState 的 IT 标签不替代车手国籍；UK 借用 IT L3 同样遵守该门控。未知/空 driverId 延续 PlayerState 的本队默认车手回退，不写回选择。human/AI 共享规则，但仍只有 human 调用存储回调，XP 算法及教程/生涯短路不变。新增 49 项；定向 Unity 213/213、全量 EditMode 1737/1737，0 失败/跳过；未运行真实存储/Play Mode。

- 2026-09-29 生涯赛果显示边界：`CareerRaceResultPresentation` 只读取 CareerModeRules 积分榜，保留总分/排名、第四站夏休、第八站冠军、重复保存及失败提示的原文与换行；不发放奖励、不推进赛季、不写存储、不修改科技配置。六队八站及全员 DNF 平分排序/重复显示序列化不变等新增 13 项回归；Unity 定向 69/69、全量 EditMode 1688/1688，0 失败/跳过。实际生涯存储与 Play Mode 不在此次证据范围。

- 2026-09-29 赛前日志格式化边界：教程按 runtime→精确牌序→天气→检查点→可选维修区→首个可选对手 cue 的原顺序记录，不应用或重放脚本；首 cue 为 null 时不拿后续 cue 替代。生涯使用不可变启动快照，自由赛按原阵容顺序使用真实车手短名、未知 ID 原样回退。管理器仍保持教程优先、其次生涯、再自由赛，并记录实际加载赛道；抽至已有 `RaceStateLogFormatter` 不改变玩家规则/持久化。新增 23 项；Unity 定向 55/55、全量 EditMode 1675/1675，0 失败/跳过。未执行 Play Mode 或真实进度存储验收。

- 2026-09-29 日志关闭接线回归：六队教程均以 Director 的 Completed 判定练习完成，车辆已完赛但教程仍在 Practice/Guided 或无 Director 时记录 TutorialIncomplete；普通结果与已保存生涯结果为 Completed。重开在新日志/重建前关闭旧日志为 Restarted，退出先记录 Exited 再导航，未关闭日志的销毁为 SceneDestroyed，关闭后销毁不覆写原因。新增 25 项真实管理器 inactive 回归，定向 65/65、全量 Unity EditMode 1652/1652，0 失败/跳过。仅私有回调边界分离场景/重建 I/O，规则与持久化不变；普通测试仅 AI、生涯仅 already_saved，不代表真实人类存储、场景销毁调度或 Play Mode 验收。

- 2026-09-29 日志验收语义：`RACE_END` 只表示日志关闭；新增 `RACE_TERMINATION outcome=...` 区分正常对局结束、重开、退出、场景销毁和教程练习未完成。旧日志无明确原因保持 Unknown，不靠赛果文案猜完赛；结构有效且完整、明确 Completed 才提供完赛日志证据，仍不证明画面、实际存储或 Play Mode 全项验收。拒绝多份拼接/重复关闭/关闭后事件，元数据与普通文案中嵌入的标记不作为阶段。新增 29 项，定向 40/40、最终全量 EditMode 1627/1627，失败/跳过 0；没有修改比赛规则、奖励或存档。

- 2026-09-29 重开边界维护：教程 Guided 仍重启引导，Practice/Completed 重启练习且优先于生涯；非教程已结算生涯返回菜单，其余重建比赛。清理顺序保持停止协程→请求延迟销毁旧 AI→清空绑定→隐藏赛果→初始化比赛/指引→启动循环，异常仍中断后续操作，不新增回滚。新增 38 项适配回归，定向 79/79、全量 EditMode 1598/1598，失败/跳过 0。夹具只使用临时对象、记录初始化/循环回调；未证明真实延迟销毁、协程调度、存储或完整 Play Mode 重开。

- 2026-09-29 返回菜单边界维护：默认确认取消保留教程/生涯请求和自由赛阵容，确认仅消费一次回调并先关闭弹窗、清空回调，再清理会话并请求 MainMenu；空格不得确认返回或重新开始。玩家关闭返回确认时仍走相同清理入口。加载失败后会话已清理是原有非事务行为，本轮没有改为恢复比赛或保存比赛。新增 16 项实际 UI/清理适配回归，Unity 定向 20/20、全量 EditMode 1560/1560，失败/跳过 0；测试记录场景请求，不真实切换场景或写 PlayerPrefs，不代表完整 ResetGame/GameLoop/Play Mode 验收。

- 2026-09-29 模式激活边界维护：初始化/重新初始化仍按教程→生涯→自由赛优先级处理；教程激活会清除生涯请求，同一生涯重进保留原请求而不推进赛季。原结算标志、初始化错误和管理器缓存阵容仍在验证前重置，自由赛全局阵容与选图不变。新增 25 项真实 inactive 管理器适配回归，Unity 定向 52/52、全量 EditMode 1544/1544，失败/跳过 0；未加载比赛场景、写实际存档或执行完整 ResetGame/返回菜单/Play Mode，不改变现有规则或关闭新版科技待办。

- 2026-09-29 生涯组合回归仅刻画既有规则，不改变玩法：六队八站的四车积分/DNF、锁队/固定阵容和科技快照在真实 JSON 重载后保持一致；第四站夏休阻止继续，草稿不写入，确认后第五站使用新快照而初始快照不变；赛事或夏休保存失败不推进，同一请求可重试成功一次，陈旧回调无重复写入，重开赛季要求确认。新增 18 项，Unity 定向 27/27、全量 EditMode 1519/1519，失败/跳过 0。测试使用仅允许生涯键的内存仓储和构造终态赛果，不使用真实 PlayerPrefs、场景 GameLoop 或菜单；完整 Play Mode/实际存储与新版科技未完成功能仍未验收。

- 2026-09-29 AI 接线结构维护：已登记对手才创建并绑定 AIController，控制器仍注入同一管理器/配置/赛道和对应 PlayerState；普通赛仍使用 UnityRandomSource，教程仍每对手独立 RuntimeSeed + 索引 + 1，不改选牌/尾流/技能规则。新增 19 项实际 SelectCards/绑定回归，定向 Unity 98/98、全量 EditMode 1501/1501，失败/跳过 0；12 车场上仅 11 个 AI，各自独立且不操作人类手牌。教程六队可重播、不消耗全局随机流；测试赛道未加载，不代表实际场景启动/重开或完整 Play Mode 已验收。

- 2026-09-29 参与者初始化边界收敛：协调器先按已解析计划赋车手 ID/XP，再初始化车队/科技/牌组，最后初始化独立车手技能和登记参与者；保留玩家先登记后技能、AI 先技能后登记的原顺序。教程双角色和全部 AI 仍禁用技能，普通/生涯人类按实际 XP 层级初始化；不增加 AI 技能、不读写额外 XP 或科技存档。新增 26 项真实 inactive 适配回归，Unity 定向 72/72、全量 EditMode 1482/1482，失败/跳过 0。涵盖六队教程/生涯、12 车手、12 车名单和普通随机消耗等价；不代表场景启动/完整重开、Play Mode 或新版科技交互已完成。

- 2026-09-29 结构维护：`RaceSession.InitializeRaceDeck` 负责重建特技状态、独立热量池与牌区并补起手，科技/模式/容量解析和 CN 人类开局辅助仍由协调器负责。普通洗牌保持 CardDeck 原随机来源，教程玩家/对手使用完整固定牌序且不消耗随机数，不改变引擎容量/牌数/短牌组/null 配置异常契约。新增 30 项，Unity 定向 64/64、全量 EditMode 1456/1456，失败/跳过 0；不代表完整场景重开、存储、Play Mode 或新版科技交互已验收。

- 2026-09-29 Demo 科技配置构造统一至 `TechTreeRules.CreateDemoProfile`，AI 会话与新普通档案均委托；保留 25,000 RP、四个固定 L1 通用按顺序购买（CN 用 EV ID）、仅首个本队 L1 专属、默认余款 9,000 和成功解锁全激活。构造每次返回独立可变状态，不读写缓存/存档，`CreateDemoState` 仍只给预算。新增 32 项回归，Unity 定向 66/66、全量 EditMode 1426/1426，失败/跳过 0；不改变已存在档案，不代表新版科技未完成交互或 Play Mode 已验收。

- 2026-09-29 科技赛前准备结构维护：会话负责本场用量重置后再按实际地图授予，管理器保留普通/AI/生涯/教程的档案来源与牌组分流、Demo 汤底时序。真实 inactive 初始化新增 34 项，定向 63/63、全量 Unity EditMode 1394/1394，0 失败/跳过；普通缓存保留 RP/解锁/活动选择，AI 每次独立状态、生涯每次克隆快照、教程/关闭科技不使用原档案。未验证真实场景重开/Play Mode或存储读写，不意味着新版科技全部接入。

- 2026-09-29 结构维护：科技数值/效果查询共享只读有效节点遍历，只对实际日不落虚拟授予去重，目标/选择变更下一次查询即时生效，不改变数值、次数、存档或当前科技规则。新增 26 项，Unity 科技组 131/131、全量 EditMode 1360/1360，失败 0、跳过 0；无 Play Mode 新证据，新版科技交互接入的剩余事项未关闭。

- 2026-09-29：按新版科技 GDD 修正 L2 通用需要三个 L1、英国按实际地图获得去重的虚拟 L2/L3（不写解锁/RP）、日本味噌只加尾流位移、德国黑啤引擎到弃牌堆/香肠不免除超速、美国烟幕按前车行动终点的地标区阻断。新增 34 项测试，全量 Unity EditMode 1334/1334，0 失败/跳过；无 Play Mode 新证据。英国借用与主场早餐、日本选流派/L3全Buff、美国BBQ牌转换/临时容量等仍未完整接入，以科技 GDD 本轮记录为准。

This document records the mechanics represented by the current code. Values
may be overridden by the active `GameConfigSO` asset or by loaded track JSON.

> **Implementation snapshot (2026-09-17)**: Unity `2022.3.62f3c1`; the latest
> full editor EditMode run passed `680/680` on 2026-09-17. The active-skill
> focused run previously passed `28/28`, and the passive/related regression run passed `21/21`; the tutorial/menu focused run previously passed `64/64`
> and the encyclopedia catalog checks previously passed `6/6`. The new free-race
> roster/menu focused coverage passed `8/8`; Play Mode visual acceptance remains open.
> The required
> `production/session-state/active.md` file is currently absent, so this
> document is based on source, configuration, and the live editor state.
> The 2026-08-27 tailwind time-scale regression passed focused `5/5` and full
> EditMode `471/471`; the Play Mode attempt did not initialize because this
> project has no standalone PlayMode test assembly, so it is not counted as a
> passing runtime test.
> On 2026-08-26, the tailwind/HUD-focused regression passed 56/56 and the full
> EditMode suite passed 451/451; a 5-second MainMenu Play Mode smoke produced
> no project errors or warnings.
> A controlled four-car Silverstone Play Mode smoke on 2026-08-25 produced
> `[SLIPSTREAM]` log entries and found `RaceEventFX` present; this is runtime
> event-chain evidence, not a substitute for a manual two-link visual check.
> The AI tailwind rerun covered 15 focused tests and the full 451-test suite,
> including the same-lap and cross-lap boundary cases; all passed with no skips.
> The team-badge rerun covered 18 focused tests and the full 469-test suite;
> all passed with no skips. Console retained only the existing
> LogAssert-expected missing-track error from its regression test.

## Team-Specialty Tutorial Catalog

- Each team-specific menu lesson stores the exact `TutorialStepId` values that it represents. A lesson may own a short consecutive sequence (CN's combined first/second Go lesson); the tutorial objective/briefing page is intentionally not a menu action lesson.
- `TeamTutorialCourseDefinition.IsPlayable` is derived from the authored team scenario: every non-objective step, including the review/practice handoff, must be mapped exactly once. Missing, duplicate, or foreign step IDs make the course unavailable from the selection menu. The general UK/Le Mans foundation course remains outside this team-specialty validation.
- The regression methods for six-team full coverage and incomplete-course rejection passed via a local Mono reflection harness after MSBuild compiled the Editor assembly; this is not a Unity Test Runner or Play Mode result.

## Career Season

- A career season uses the eight entries in `TrackSelectionState.AvailableTracks` in their explicit
  order; Resources enumeration, `fallback_42` and tutorial-only track rules are not part of the calendar.
- The current career rules model a four-car field, matching the configured player-plus-up-to-three-AI
  race boundary. Any of the six teams may be selected, but confirmation locks that team in the rules
  state until the career is completed or later abandoned through the persistence/UI layer.
- Classified finishes score `10/6/4/2`; DNF and invalid positions score zero. Duplicate team entries,
  wrong-track results and repeated result IDs are rejected before state advancement.
- Championship ties compare points, wins, podiums, best finish, most recent finish and finally the
  stable season competitor order.
- Technology configuration is locked during races 1–4. Resolving race four enters `SummerBreak` and
  blocks race five until the sole technology adjustment is confirmed. Races 5–8 are locked again, and
  completing race eight does not create another adjustment window.
- Career progress is stored only under `Foodula1.Career.V1`. The versioned DTO carries the locked team,
  stable competitors, exact schedule/version, results, reconstructed standings, phase and initial/mid-season
  technology snapshots. Loading replays every result through the same rules and rejects drift or tampering.
- Missing or malformed career data returns a safe `NotStarted` state without touching normal tech profiles,
  RP, drivers, tutorial completion or settings. Invalid raw data is retained until explicit replacement or
  abandonment. Candidate progress replaces live state only after storage succeeds.
- The main menu presents the existing single-race route as `自由赛事` without changing its track-selection
  callback. A separate runtime career overlay creates a season from any team, shows the locked team, eight-race
  calendar and computed standings, and requires blocking confirmation before replacement or abandonment.
  Race launch copies the authoritative scheduled track, locked team, stable competitors and career-owned tech
  snapshot into a session request; it does not mutate Quick Race selection or the normal tech profile.
- Track resolution has one precedence rule: Tutorial, then Career, then Quick Race. Settlement requires the
  actually loaded track and exact four-car roster, maps blown cars to DNF, reloads the authoritative career save,
  and advances atomically once. Career races skip normal RP and driver-XP settlement.
- At the race-four summer break, the menu creates a mutable draft from the career-owned active technology snapshot
  for the locked team only. Unlocking spends only the captured career RP, active-node changes remain in the draft,
  cancel discards every change, and explicit confirmation atomically stores the sole summer snapshot before race five.
  The normal `TechTreeProfileStore` is never read or written by this editor.
- Race eight preserves the final standings, identifies the championship leader, and reports the player's score/rank.
  The completed overview offers a new-season path but still requires the existing explicit replacement confirmation.
  Every settlement writes one structured `[CAREER_RESULT]` saved or rejected line before `RACE_END`.
- The combined career rules, persistence, presentation, Race integration, summer-break editor and completion flow
  passed `46/46` focused source-level NUnit cases on 2026-08-31. Play Mode visual/runtime acceptance remains open.

## Free Race Custom Field

- Before selecting a track, the free-race flow opens a 2–6 car roster editor.
  Each selected team receives one unique driver from that team; the first selected
  team is the human car and all remaining cars use the normal AI controller.
- The same panel provides a Thunderstorm mode that enters all six teams and all
  twelve catalogued drivers. The selected garage driver is first for the human
  player and the remaining eleven drivers use the normal AI controller.
- The default roster remains the garage driver team plus UK/DE/IT, preserving the
  previous player-plus-three-AI behavior. This session-only roster never writes
  career persistence or tutorial state.
- Validated entries are copied into RaceSession.Players in player-first order.
  The opening manual log includes a structured FREE_RACE_SETUP line with the full
  team/driver field; Thunderstorm runs record `field=12` so a large-battle strength
  comparison can be reconstructed. The 12 cars use a staggered presentation grid
  before turn one while retaining the shared rules start cell.

## Turn and Card Loop

- The drivetrain base card count is mandatory. Extra card slots granted by tricks or technology
  increase only the maximum selectable count and never create a missing-card engine-failure penalty.
- Instant bonus movement uses the same start/finish traversal rule as animated movement. In particular,
  Yin-Yang Tea Go movement can increment the lap and finish the race when it crosses the line.
- Human heat mutations refresh both the card-zone counters and HUD thermometer immediately. Corner
  overspeed payments, gear/skill payments, normal cooling, hand-only recovery, full spin recovery and
  Fish-and-Chips recovery do not wait for the next phase-level HUD refresh.
- Keyboard assistance mirrors the pointer flow: `1–4` select a gear; after a card is selected, one `Space` press
  immediately plays/discards that selection without opening the pointer confirmation dialog; `Space` also skips
  an active presentation; `A/D` and arrow keys move card
  focus, and `F` toggles the focused card. During normal card play, an empty selection makes `Space` a no-op;
  ending the card phase requires the explicit pointer button so a repeated key press cannot submit and end the
  turn together. Pointer actions retain their configurable confirmation gates. Space cannot confirm
  reset-race or return-to-menu prompts.
- The operation log uses a fixed masked scroll viewport above the primary action button. It keeps up to 80 recent
  lines, places newest events at the top, supports wheel review, and cannot grow across or intercept the confirm area.
- Each player has a draw pile, hand, discard pile, and independent engine
  heat pool.
- The default hand limit is 7.
- The default speed deck contains twelve cards:
  `[1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4]`.
- Authored tutorial scenarios may initialize `CardDeck` with an explicit
  top-first sequence. That path never shuffles or consumes a random seed, and
  recycles playable discards in discard chronology. Normal races still use
  the randomized `InitializeDeck` path.
- `TutorialLaunchState` is session-only: it resolves the authored track for a requested tutorial
  without changing `TrackSelectionState`. Returning to the main menu or starting a
  normal Quick Race clears the tutorial override.
- The foundation tutorial fixes the player to UK with six engine heat, exact opening/future
  draws, no tech state and no intrinsic team-vehicle handling/cooling/pace/slipstream
  bonuses. CN and US specialty tutorials instead use their own base durability (7/8)
  and intrinsic team vehicle bonuses, still with fixed draws and no tech or driver skills.
- Each guided step owns a semantic focus target and a short standalone mechanism introduction.
- Tutorial copy always presents the next action. Current-state, success and recovery sections are optional;
  blank optional fields render no label, spacer or generic previous-step feedback.
- Tutorial presentation is data-bound through `TutorialOverlayAuthoring` on
  `Assets/Resources/Prefabs/UI/TutorialOverlay.prefab`. The Prefab exposes the sixteen lesson copies and
  practice/completion text without changing step IDs or action gates. Its guide panel and spotlight keep
  manually authored layout values; runtime first reuses a copy placed under `RaceCanvas`, then falls back
  to loading the Resources Prefab when no scene instance exists.
  `TutorialFocusHighlightUI` resolves live HUD, track, pit-choice and specific UK-card rectangles,
  dims only the surrounding area without intercepting clicks, and disables border pulsing under
  reduced-motion settings. During live input it prioritizes gear, card selection/confirmation,
  discard selection/confirmation, pit and lane gates over the lesson subject. Clicking removes the
  callout text and mesh while retaining the target border; highlighting never advances tutorial state.
  Disabling the guide panel clears its four lesson text meshes and hides the
  spotlight; replay or preview repopulates them from the selected step.
- Heat cards do not start in the normal deck. Each player's independent engine
  heat pool is the only source of permanent heat cards.
- When trick cards are enabled, four team cards (two attack and two defense)
  are shuffled into the normal draw pile before the opening seven-card draw;
  ordinary drawing never draws heat cards.
- A turn selects a gear from 1 through 4. Speed cards can be selected singly
  or as a group and confirmed together; trick cards remain one-at-a-time
  immediate actions. A speed-card selection may never exceed the turn limit.
  The selected card visual scales to 1.08, lifts 24 px, and uses a 0.14-second
  unscaled-time transition plus a blue shadow; the LayoutGroup slot size and
  sibling positions remain unchanged.
- Confirmed trick cards resolve immediately, leave the hand, and enter the
  discard pile. They can return after the discard pile is reshuffled, and the
  existing one-trick-per-turn limit still applies.
- Confirmed speed cards leave the hand and accumulate in the current turn's
  played area. Pressing the action button with no pending card ends card play;
  any missing required speed cards use the existing engine-failure rule.
- The selected speed-card values determine movement.
- Tailwind close-ups and the immediately following bonus movement use a scoped
  `0.28` time scale. The close-up keeps realtime presentation timing, while the
  actual reward movement uses the scaled Unity clock so the slowdown is visible.
  Cleanup, disable, and destroy paths restore normal gameplay time (`1`), so
  base movement and later turns are not slowed.
- The draw-pile and discard-pile panels show stacked backs, up to three live
  card thumbnails, and a quantity badge. Draw previews follow the actual draw
  order; discard previews start from the most recently discarded card. Visible
  stack thickness grows by one layer per three cards up to seven layers, while
  the badge remains exact.
- Card ownership changes use a non-blocking table overlay: played/discarded
  cards fly from hand to discard, heat payments fly from engine to their rule
  destination, and cooling flies from the actual source zone back to engine.
  Optional discard resolves the exact card instances that actually moved before
  starting the animation, removes only those card views, and keeps unselected
  cards visible in the hand presentation.
- Played speed cards enter the discard pile during end-of-turn cleanup; only
  non-heat cards are reshuffled when the draw pile is empty.
- Heat cards cannot be played as speed cards and can clog the hand after they
  are explicitly paid from the engine or granted by an effect.
- The optional discard step can discard speed or trick cards without resolving
  them, but heat cards cannot be selected.
- Kanto Oden carry-over slots and its skip flag are consumed at the start of
  the next turn independently of whether the tech-tree module is enabled. The
  current runtime counts those carry-over slots as required for missing-card
  engine failure; whether they should instead be optional remains a design
  decision.
- Hotpot in China Go arms the next normally committed speed card instead of
  adding an optional card slot. That ATTACK card receives +1 total movement and
  its full effective speed is excluded from corner-limit speed. If a group is
  confirmed, the first card in that committed group receives the marker.
- China Go therefore keeps its ordinary 3-card first-use requirement/limit (or
  4 cards on the consecutive overclocked Go). Hotpot never raises those limits
  to 4/5 and cannot create a missing-card or hand-pressure downside by itself.

## Gear and Cooling Rules

- The selected gear controls how many speed cards may be played.
- A normal shift changes one gear.
- A two-gear shift costs heat.
- Gear 1 removes up to three heat cards through the cooling priority:
  hand, then draw pile, then discard pile.
- Gear 2 removes up to one heat card through the same priority.
- Higher gears provide no automatic cooling.
- The race HUD derives its heat percentage from permanent heat in hand, draw
  pile, and discard pile plus temporary heat. Permanent heat outside the engine
  and the engine remainder define capacity; temporary heat raises the displayed
  load without increasing that capacity. The thermometer changes from cool to
  elevated at 50% and critical/pulsing at 70%.
- Standard G1-G4 controls are arranged as a stove-dial arc; China reuses the
  same circular controls as a two-position Recover/Go selector. This is
  presentation-only and does not change gear legality or shift costs.
- Italy has no permanent straight movement bonus. Completing a corner arms a
  one-shot `+1` for the first speed card played on a later turn; an empty turn
  preserves it, and a failed/spun corner does not arm it.
- China AI projects every unique apex crossed by the lowest legal Go card set.
  It may accept at most one corner heat when the engine can also pay overclock
  and missing-card costs; larger or unaffordable risks force Recover. The
  tolerance is `GameConfigSO.aiChinaAffordableCornerHeat` (currently `1`).
- Standard AI, when heat is below the cautious threshold and no corner risk is
  predicted, considers an opponent within `GameConfigSO.aiSlipstreamPlanningRange`
  (default `2`) and searches the current gear's hand for an exact movement
  combination that ends one cell behind the opponent's estimated endpoint;
  otherwise it falls back to the normal high/low-card policy.

Gear-shift heat cost and gear-one/gear-two cooling values are configured in
`GameConfigSO`. `MVPGameManager` resolves these rules through `RaceRules` for
both the player and AI race flow.

## Heat

- Each player starts with an independent engine heat pool of 6 by default.
- Overspeeding through corners, sudden braking, and engine failures pay heat
  cards from the engine pool into the hand by default, so heat occupies hand
  capacity; an explicit effect can choose the discard pile (China's Yin/Yang
  Tea Go branch does this). Heat never enters through ordinary drawing.
- Generic cooling returns permanent heat cards from hand/draw/discard to the
  engine pool in that order. The normal draw pile contains no heat cards, but
  the draw-pile step remains a defensive boundary for legacy or explicit test
  states. Temporary heat cards are consumed and destroyed instead; effects that
  explicitly say “from hand” remain hand-only.
- Running out of payable engine heat affects the race flow according to the
  current manager rules.

The earlier "Cold Storage below zero" description belongs to the original
prototype and is no longer the authoritative model.

## Tutorial Mode Foundation

- The new-player tutorial center offers the playable UK foundation course plus
  all six team specialty courses: CN/Shanghai, US/Indianapolis, DE/Nürburgring,
  IT/Monza, UK/Silverstone and JP/Suzuka. They use exact card orders, team-aware
  safe checkpoints, real event gates and a reset one-lap practice without
  RP/XP/progression writes. JP teaches the implemented Kanto carry and Torpedo
  self-overtake bonus, not the unimplemented reverse bonus.
- Menu lesson rows describe the actual scripted checkpoints. CN's Shanghai
  specialty course does not claim a pit lesson; IT's Monza course lists its
  corner, corner-exit, Parmigiano, Chianti and practice steps separately.
- The user confirmed Play Mode acceptance for all six team-specialty courses on
  2026-09-27. Detailed per-course runtime environment, resolution and Console
  evidence were not supplied; the generic foundation-course walkthrough remains
  a separate acceptance item.

- `tutorial_le_mans_uk_v1` fixes the player to UK on
  `le_mans_old_mulsanne`, with tech-tree modifiers, driver skills, normal
  rewards and normal progression writes disabled by definition.
- Its pure state machine orders 16 guided topics from objectives/UI through
  UK special cards and review, rejects out-of-order actions without advancing,
  then enters a restartable one-lap practice phase.
- `TutorialRuntimeDirector` drains ordered state-machine events exactly once and
  exposes one-shot weather/opponent checkpoint cues. The Race adapter applies rain
  or cloudy weather through the existing session weather state.
- Every guided step carries a section label plus explicit goal, current scripted state, ordered
  player action, observable success signal and safe recovery hint. Real actions latch lesson completion
  without changing the visible step; Next becomes available after completion. Previous reviews reached
  lessons without rewinding race state or replaying checkpoint cues. Skip and Exit remain available.
- The guide panel uses a pure safe-area rule for common 16:9 resolutions. Its expanded form scales
  down from 540x440 and switches to compact typography at small sizes; collapse leaves only the
  title, section progress and expand control. Toggling presentation never advances tutorial state.
- The slipstream lesson starts at a fresh turn boundary with the leader at cell 42 and
  the player at cell 40. The teaching leader's base movement is held at zero for that
  single turn; the player uses a real G1 speed-1 card to settle at cell 41 before the
  unchanged end-of-turn resolver grants only the rear car its bonus.
- A runtime-built guide panel displays authored title/body/progress. Reading steps may advance immediately;
  operation steps require matching real race events for turn completion, exact card requirement, movement,
  heat, cooling, missing cards, spin, slipstream, pit timing and UK trick cards, then wait for explicit Next.
- Completing or skipping the guide rebuilds the Race session directly into `Practice`:
  lap and positions return to zero/start, the exact deck and six-heat pool are recreated,
  the teaching opponent is restored and scripted cloudy weather is applied. Practice uses
  a one-lap override without changing `GameConfigSO` or track JSON; completion ends the
  tutorial immediately and never enters RP/XP/progression settlement. Restart, guide replay
  and exit are available from the guide panel and emit tutorial log events.
- Nine risky guided steps now enter authored safe states at a gear-input gate or the next turn
  boundary. Exact normal/heat zones guarantee heat payment/cooling, a one-card G2 shortage,
  a zero-engine Dunlop spin after G2 cooling, and valid Scone/Tea targets; reset also clears spin,
  skip-turn and pit flags so the following lesson cannot inherit a soft lock.
- Official Le Mans has no pit nodes. Tutorial mode creates a separate 142-cell rule-only view with
  entry 132 and exit 4, then passes that view to unchanged `PitLaneRules`; official JSON/runtime
  track nodes remain unmodified. The full guided-to-practice Play Mode walkthrough remains open.

## Player Settings

- `Foodula1.Settings.V1` stores master, music and SFX volume values; fullscreen/window mode;
  resolution; animation speed; reduced motion; a separate tutorial-completed preference; and
  schema-v2 `inRaceConfirmationMask` per-action confirmation gates. The V1 key remains for
  backwards compatibility.
- A persistent runtime `AudioService` loads menu/race music and named gameplay clips from
  `Resources/Audio`, crossfades on `MainMenu`/`Race` scene changes, and applies master, music and
  SFX settings immediately. Music, SFX and UI use separate AudioSources; UI currently follows SFX.
- Core button, card, gear, heat, movement, corner, tailwind, spin, lap and finish events are wired.
  Repeated movement/card/heat sounds use unscaled-time cooldowns, so tailwind slow motion does not
  change music pitch or leave later audio slowed. AudioMixer routing, independent UI volume/mute,
  peripheral P1 events and a full-match listening pass remain open.
- Display mode and resolution apply through an isolated runtime target. Animation speed scales
  race node pauses, camera lead/trail delays and button feedback; reduced motion skips those
  optional presentation durations without changing race rules, card values or movement results.
- Completing the tutorial only marks the tutorial preference. Resetting it changes the main-menu
  replay label and does not clear or write RP, tech-tree, driver XP, unlock or race-progress data.

## Game Encyclopedia

- The settings overlay opens a separate scrollable reader backed by
  `Resources/Configs/encyclopedia_zh.json`; UI code contains layout only, not rule prose.
- Catalog version 1 contains 17 stable topics covering the turn loop, all three card types, gears,
  card zones, heat/cooling, missing-card penalties, corners/spins, slipstream, weather, pits, teams,
  all special cards, drivers, the tech tree and HUD terminology.
- Startup validation rejects unsupported versions, duplicate/blank IDs, missing required topics and
  incomplete content. Tests also match the encyclopedia's related IDs against all 12 runtime trick
  definitions, all 12 drivers and all five active weather profiles.
- The encyclopedia driver entry now records that all twelve active skills plus five redesigned passive
  effects (Mansell, Schumacher, Vettel, Zhou and Ma) are applied in normal race resolution. The
  remaining seven passive descriptions are still configuration-only, and tutorial mode disables all
  driver skills.

## Track and Race

- The main menu currently offers eight JSON-backed tracks. The selected track
  is carried into the Race scene; Silverstone remains the default selection,
  and the legacy 42-node layout is a fallback only.
- When `GameConfigSO.trackId` is populated, `TrackManager` loads
  `Resources/Configs/Tracks/<trackId>.json`.
- JSON tracks may define their own node count, lap count, start/finish node,
  corners, apex cells, speed limits, pit entry/exit, weather pool, and layout.
- A pit decision is offered while the car is 1–10 cells before `pit_entry`.
  Choosing to pit only schedules the stop; after the car crosses the entry,
  the next turn is consumed by the pit stop. The car then exits one cell beyond
  authored `pit_exit` by default. `GameConfigSO.pitExitMoveBonus` controls the
  base value; China's Fast Charge tech adds another cell.
- The read-only offer gate and the two reservation writes are shared by human and AI adapters through `PitLaneRules`; resolving a choice suppresses another offer until the entry is passed without a reservation or a stop is executed. A duplicate/late human click cannot reverse a submitted choice; there is no separate cancel-reservation command. AI retains its 60% hand-heat threshold. Thirty new regressions passed within Unity EditMode `1150/1150` on 2026-09-28; this is not full Play Mode acceptance.
- Tutorial reservation/exit feedback only latches the expected presented human lesson; crossing alone does not complete the exit lesson or navigate. Twelve real-manager/Director regressions passed within EditMode `1162/1162` on 2026-09-28; GameLoop, UI and file-log acceptance remain separate.
- Corner-speed resolution triggers only when movement crosses an `isApex`
  cell. Repeated apex cells for the same corner are deduplicated per move.
- Player initialization and lap crossing use the runtime node marked
  `isStartFinish`, including when that node is not index 0.
- HUD position totals and LineRenderer coordinates use the loaded track data.
- Player-facing positions use 1-based `格 X/N`. The human car has a pulsing
  technology-blue ring and a local scale covering six cells behind and ahead;
  dense layouts retain every tick but sample number badges to avoid overlap.
- Track presentation uses the selected layout background in Play Mode, with
  smoothed, rounded corner ribbons over a dark edge: Lv1 is safety green, Lv2
  amber, and Lv3 warning coral. Apex badges inherit the corner level color and
  keep visible speed-limit labels. Runtime grid nodes, lane lines, and debug corner text are hidden;
  tracks with explicit lane-specific limits show the effective limit above each
  lane's apex. Editor Scene view retains node metadata and limits for authoring.
  Ordinary tracks place all cars on the inside lane by default; when cars share
  the same lap and node, the stable-order trailing car uses the outside lane
  for the side-by-side visual. This is presentation-only: gameplay positions,
  corner checks, lap counts, camera framing, and minimap tracking are unchanged.
  Indianapolis keeps explicit per-car lane choices, and the player's start/finish
  lane selection immediately repositions the player car.
- The shared `RaceLaneRules` adjacent-lane contract keeps positive direction as
  inward and negative as outward, moving at most one lane. An edge rejection
  leaves human lane selection waiting; keeping the lane accepts and closes it.
  Twenty-six added rules/adapter regressions passed within Unity EditMode
  `1188/1188` on 2026-09-28; this does not verify live car/UI presentation.
  `LaneChoicePresentationRules` now shares button/confirmation direction copy
  and logs the actual lane delta: decreasing indices mean inward, increasing
  indices mean outward. The prior reversed completion labels were corrected
  on 2026-09-28. Twenty-two new copy/callback/panel regressions passed within
  Unity EditMode `1210/1210`; synchronous panel lifecycle is not live click or
  visual Play Mode acceptance, and gameplay lane rules are unchanged.
  Start/finish registration settles lap and finish state before offering a
  human lane choice: only the unfinished player on Indianapolis waits; AI,
  other tracks and the final crossing do not. Repeated unfinished crossings
  can reopen the choice. The separate synchronous technology-movement path
  currently settles crossings without offering that choice; its player-facing
  lane interaction remains a known gap, not an accepted GDD behavior.
  The 2026-10-04 refactor separates a readonly calculation plan in
  RaceMovementRules from coordinator effects: position commit, regional heat
  synchronization, capped lap/finish registration, pit-entry reservation using
  the raw endpoint, then car teleport. Signed remainder, authored markers,
  integer overflow and invalid-track failures are preserved; no lane wait or
  new player rule is introduced. Thirty-five new plan/adapter cases passed in
  focused native EditMode 250/250 and full 2420/2420, zero failures/skips.
  Eight authored tracks and lap/pit/teleport observations do not replace live
  animation, input scheduling or normal/career Play Mode. Eighteen earlier
  regressions added on 2026-10-04 guard the handoff to the next ordered traversal,
  actual CN/US technology entry points, other input gates, edge/stale callbacks,
  AI/missing panels and terminal registration using authored Indianapolis nodes.
  Focused Unity EditMode 226/226 and full 2385/2385 passed with no failures/skips;
  manual iterator advancement does not prove live scheduling or UI clicks.
  Fourteen earlier adapter regressions passed focused
  Unity EditMode `75/75`, full `1224/1224` on 2026-09-28; manually advancing the
  wait coroutine does not verify movement animation scheduling or Play Mode.
  The ordered node traversal now re-reads the chosen Indianapolis lane after
  that wait, so remaining node targets follow the new lane instead of the
  pre-choice cache. This corrects presentation coordinates without changing
  movement amount, lap settlement or ordinary-track lane policy. Ten added
  recording-animator traversal regressions passed within full Unity EditMode
  `1234/1234` on 2026-09-28; live interpolation/camera acceptance is separate.
  Fourteen further traversal cases verify repeated crossings at a non-zero
  start/finish node for all six teams, final-lap suppression of lane choice,
  all four fixture crossing indices, rejected edge input followed by keep,
  and empty traversal preserving the existing gear gate. Focused Unity
  EditMode `86/86`, full `1248/1248` passed on 2026-09-28 (zero failures/skips).
  No runtime rules changed; these manually advanced iterators still exclude
  live scheduling, interpolation, camera and full GameLoop/Play Mode.
  The existing outer movement adapter exits without state changes for a null
  participant, absent track or zero nodes. With an unavailable car slot it
  clamps negative movement to zero and wraps only the supplied participant's
  position; it does not settle lap, finish or lane-crossing events. This is
  an existing presentation-unavailable fallback, not full headless gameplay
  or an alternative to normal crossing rules. Twenty-three added real-adapter
  regressions passed focused Unity EditMode `109/109`, full `1271/1271` on
  2026-09-29 (zero failures/skips); runtime behavior is unchanged. The normal
  animated outer completion/camera path and Play Mode remain unverified.
- The race readability overlay adds US landmark signs at the actual start/finish
  and midpoint nodes when any active participant is on team US. The shared
  gameplay landmark-position rule supplies the node indices; the selected
  track's runtime positions/normals and road width determine sign placement.
  Other rosters hide these signs. Rule and track-data coverage includes all
  eight selectable maps plus the fallback; the new sign appearance still needs
  a visual Play Mode check.
- On `indianapolis_burger`, corner limits are lane-specific from inner to outer:
  `4/5/6/7`; in the Unity path data lane 0 is the inside lane and lane 3 is
  the outside lane, so the outer lane accommodates a speed total well above 4
  before overspeed heat. At each start/finish crossing, the player may move one
  lane inward, keep the current lane, or move one lane outward; boundary
  choices are disabled and the AI keeps its lane.
- Le Mans old Mulsanne uses 142 cells and 2 laps without a pit lane. Cell 60 is the authored
  `mulsanne_kink` high-speed apex (Lv1, base limit 6), keeping the long-straight visual rhythm
  while making the bend readable. Its current circular straight distribution is 44/37/21/16.
- A corner limit number is clickable. It opens a read-only breakdown using the same formula as
  `RaceSession.GetCornerLimitBreakdown`: base limit + weather + driver skill + team handling +
  technology, clamped to the real effective limit. The player sees signed contributions, including
  weather penalty plus driver compensation for weather immunity.
- Vehicle sprites follow the track tangent: spawning and teleport-style moves
  snap immediately to the next-node direction, while normal movement performs
  a linear interpolation between adjacent nodes (`nodeMoveDuration=0.15s`)
  without a vertical offset and rotates toward the tangent according to
  `carRotateSpeed`; the stored gameplay position still snaps exactly to the
  destination node after each step.
- A spin-out presentation is visual-only: `RaceEventFX` defaults to a 1-second
  360-degree eased rotation, then restores the authored scale and track-facing
  rotation. Spin counters, heat recovery, rewind, skipped turns and DNF remain
  resolved synchronously by the gameplay rules before the cue is started.
- Each runtime car now gets a non-interactive world-space badge above its sprite,
  displaying the stable team code and current rank. The badge remains upright
  while the car rotates along the track and refreshes after position/ranking
  changes. It is a code/color fallback only; the GDD's authored flag icon and
  driver avatar are still pending visual assets.
- During the base-movement and end-of-turn slipstream presentation windows, a
  primary click anywhere requests a presentation skip. Remaining node movement,
  camera buffers, overtake/tailwind close-ups, tailwind bonus movement, and
  leftover card flights settle immediately; movement, corner/lap/landmark/pit
  crossings, tailwind bonuses, required lane/pit/tutorial decisions, and logs
  remain unchanged. The skip window closes before discard input.
- Manual race logs can be checked with the pure `RaceLogAnalyzer`: a valid turn
  ends card selection before base movement, ends base movement before the
  optional slipstream phase, and records active discard counts where
  `discarded` never exceeds `selected`. A log without `RACE_END` is reported as
  incomplete rather than being mistaken for a completed race.
- The file adapter and Unity editor menu can run the same checks against the
  latest or a selected saved `.log`; file-read failures are reported as
  analysis errors rather than interrupting gameplay.
- Track authoring workflow, pit behavior, and a full multi-lap manual playthrough
  now have full-race evidence; broader multi-track validation remains open.

## Weather

- The selected track supplies a weighted weather pool and optional default.
  Weather selection is deterministic when the session receives an injected
  `IRandomSource`.
- Runtime weather profiles are `Sunny`, `Cloudy`, `LightRain`, `HeavyRain`, and
  `Hot`; the legacy `Rainy` enum name remains an alias for `LightRain`.
- Cloudy keeps the normal slipstream trigger range but reduces the final
  slipstream movement bonus by one. Light rain reduces corner limits
  by one and adds one spin-counter point on a spin-out. Heavy rain reduces corner
  limits by two, adds two spin-counter points, and disables slipstream. Hot
  reduces reaction-step cooling by one. Corner limits, slipstream, cooling,
  spin-out increments, and HUD labels all resolve through `WeatherRules`.
- Slipstream resolves only after every racer completes base movement, reaction,
  and corner checks. It uses the actual settled positions, including vehicle,
  technology, and trick bonuses already applied during base movement. Only
  racers on the same lap are eligible. A
  same-cell tie compares base movement first and then base-movement arrival
  order, so only the car that arrived later can be the trailing follower;
  a same-cell pair can never generate reciprocal slipstream.
  successful first slipstream advances the simulated endpoint and may follow one
  different car for a second bonus; the same leader cannot be reused and the chain
  is capped at two triggers.
  After `[MOVE_PHASE] end`, every resolved chain segment is merged into one explicit
  0.95-second visual phase with a card-play handoff, blue moving dash trails,
  two-car camera focus pulses, total bonus text, and a 0.28 time-scale close-up.
  The phase waits for pending card flights, uses unscaled timing, and only after
  `[SLIPSTREAM_PHASE] end` applies the bonus movement. Each segment and the
  `[CARD_PHASE]`/`[MOVE_PHASE]`/`[SLIPSTREAM_PHASE]` boundaries is written to the
  manual log.
- The deterministic full-race test and track/team balance benchmark use the same
  boundary as runtime: all racers choose cards, base movement/reaction/corners
  execute in rank order, settled positions resolve tailwind, the visual boundary
  completes, and bonus movement is applied. The benchmark reports trigger count
  and movement gained per team.
- Weather rolls once per newly crossed lap; the `RaceWeatherState` gate prevents
  multiple cars crossing the same start/finish node from rerolling the lap.
  The shared gate keeps the highest rolled lap: a lagging car crossing an earlier
  lap cannot reroll weather or move the gate backwards (2026-10-04 regression fix).
  `RaceLapWeatherRules.ShouldRollWeatherForLap` owns the shared enabled/higher-lap
  predicate; `Advance` composes it with lap progression, while the state adapter records
  an already-known lap directly.

## Opponents and Win Condition

- DE Grill Spezial settles automatically at the first eligible turn end with remaining actual engine-payment heat, once per race. The coordinator records paid card instances (including actual partial payments); only owned permanent receipts still outside the engine pass through discard back to the engine. Ordinary payment destinations and generic cooling priority are unchanged. Old heat, temporary heat and already-cooled receipts cannot substitute; a turn reset clears receipts/payment count but not the used flag. Terminal racers remain excluded. This correction passed full Unity EditMode `994/994` on 2026-09-28; Play Mode presentation is unverified.
- The default demo includes the player and one AI-controlled opponent;
  `aiOpponentCount` supports a larger configured opponent count, while the
  broader multi-opponent balance/playtest remains open.
- AI speed-card selection uses configurable normal, heat-warning, and
  corner-risk behavior. Its variation probability and random source are
  injectable so seeded runs can be reproduced in tests.
- A participant reaching the configured lap count locks its finish order; the
  race ends after all non-blown participants have finished, or no active
  participant remains.
- `hasFinished` and `isBlown` are terminal gameplay states. A finisher still
  clears already-played speed cards into discard, but no longer resolves
  Yin/Yang Tea, Grill Spezial, heat payments, movement, or other end-of-turn
  technology while waiting for the remaining racers.
- A blown/DNF participant is removed from future turns and ranks below active
  and finished racers; it does not immediately terminate the race.
- Final ranking compares completed laps and track position.

## Drivers

- Driver turn-end execution now uses `RaceTurnSkillCleanup`: an actually activated Final Sprint commits its existing spin/blown/recovery cost before HUD reporting, then queries live overtakes and the current skill runtime for the next-turn passive. The formula remains in `DriverSkillRules`; the coordinator supplies session technology caps or fallback 3. Tier 3 does not add a recovery skip, but does not clear a previous skip/blown flag. Exceptions stop later cleanup without rolling back committed cost; active-use/duration reset remains at turn start. Final Sprint cost is not merged into normal `HandleSpin` heat recovery/movement. Sixteen execution and ten inactive coordinator/result cases were added on 2026-10-04 (12:23 UTC round), including IT cap 4 and real ordinary retirement/AI finish/RP/zero-XP/HUD wiring. Old-code guards passed 124/124 after one initialization timeout with no usable result; final native focused/full EditMode passed 140/140 and 2446/2446, zero failed/skipped (fd25a98c09f74b33b814e049e99d2cf7 / 8c9fef81f8aa4476b55207e74f27b5ae). Career keeps default-driver selection and tutorial skills remain disabled; full live Play Mode and real storage are not established by these tests.

- Ordinary RP/XP settlement keeps all RP mutations/persistence before driver XP, persists only human participants, and still grants XP without a technology state. Tutorial/career modes return without normal dependencies or writes. The manager's private adapter reads the loaded track country for Cavallino, using an empty fallback; production storage is unchanged. Saving is not transactional: a storage exception stops later work but does not roll back earlier in-memory rewards. Current-source pure-layer Mono/NUnit `16/16` passed on 2026-09-29; 17 manager cases only compile, awaiting Unity. This is not Unity Test Runner, PlayerPrefs integration or Play Mode evidence.
- The main menu exposes a session-only driver selection panel with two drivers
  for each of the six national teams.
- Each `PlayerState` stores a driver catalog ID and XP. The selected driver
  supplies the player's team for race initialization; an unset selection uses
  the configured team's first catalog entry.
- Driver progression uses cumulative XP thresholds `0/100/250/500/1000/2000/4000`
  for levels 1 through 7. Passive tiers unlock at levels 2/4/6 and active
  tiers at levels 3/5/7. Level 7 provides two active uses per race; the UK
  team adds one extra active use.
- Normal races load and save XP independently per driver through `Foodula1.DriverXp.v1.<driverId>`;
  tutorial progression remains disabled and career settlement remains isolated.
- All twelve active skills use `DriverSkillRules` plus per-race `DriverSkillRuntimeState`. The HUD action
  is available only before gear confirmation and reports unlock, condition, duration and remaining-use state.
  Tutorial initializes the runtime disabled. Active-rule/EditMode coverage previously passed `28/28`,
  the passive/related regression passes `21/21`, and the full suite now passes `674/674`; Play Mode sign-off remains.
- Five redesigned passives (Mansell, Schumacher, Vettel, Zhou and Ma) now resolve in normal races;
  the other seven passive descriptions remain data-only and must not be described as active runtime effects.
- `DriverBalanceBenchmark` runs 9 available tracks × 12 drivers × levels 3/7 with paired skill-enabled/
  disabled controls (864 pairs / 1728 total simulations). The 2026-09-08 report shows the largest positive
  rank deltas for Mansell, high-tier Vettel and high-tier Tony Stewart; Schumacher high tier and Ascari are
  near neutral, while Hunter Hart's current fixed-policy result is negative. These are directional signals
  for manual timing/UX verification, not automatic balance decisions.
- `DriverData.cs` provides the immutable catalog and structured descriptions;
  active execution is isolated in the driver-skill rules/runtime layer.

See [project-overview.md](project-overview.md) for architecture context and
[current-task-list.md](current-task-list.md) for remaining work.
