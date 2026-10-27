# Foodula1 模块化回归入口

更新：2026-10-17，Unity 2022.3.62f3c1 / Test Framework 1.1.33。这是现有测试的运行清单，不是GDD覆盖率审计，也不删除、禁用或隔离任何旧测试。

## 日常怎么运行

Unity 菜单 **Tools → Foodula1 → 模块化回归**。

1. 选择受影响模块，可同时勾选“比赛基础与结算”；窗口先显示实际发现、将运行、未执行及长流程数量。
2. 点击“运行当前选择”。同模块中的原生UI/协程测试照常执行，不使用会排除协程的同步模式。
3. 查看原生 RunFinished 结果；每次独立保存 `Library/Foodula1/RegressionRuns/<UTC+随机ID>/plan.json` 和 `result.json`。文件包括原生通过/失败/跳过/不确定数、耗时、每项结果，以及计划与实际选择是否一致。Library 默认不提交Git；需要共享时只复制本轮相关证据。

三个入口：**选择模块 / 全部短集 / 全量（含长比赛）**。短集不等于全量通过，未执行的长比赛不记为跳过或通过。窗口选择保存在本编辑器SessionState，原生测试跨域重载后重新注册结果回调，不改游戏存档、PlayerPrefs或快速播放设置。

自动化/编辑器代码也可使用同一原生入口：

```csharp
ModularRegressionRunner.Run("tech,drivers");       // 科技＋技能
ModularRegressionRunner.Run("tech,drivers", true); // 同时验证基础与结算
ModularRegressionRunner.Run("ui");                 // UI（仍包含原UI原生测试）
ModularRegressionRunner.Run("audio");
ModularRegressionRunner.Run("fast-all");           // 全部短集
ModularRegressionRunner.Run("long-races");         // 完整比赛/生涯长流程
ModularRegressionRunner.Run("full");               // 所有当前发现的EditMode测试
```

这是显式模块选择，不自动推断代码改动的全部依赖。可组合任意模块；核心规则的跨系统影响不能仅凭文件名判断。

## 2026-10-17 模块清单基线

权威映射为 [RegressionModules.json](../Assets/Tests/Editor/RegressionModules.json)，按测试fixture精确归属；同fixture未来新增参数化用例自动纳入。当前新程序集发现3002项，模块间无重复。

| ID | 范围 | 当前用例数 |
|---|---|---:|
| core | 比赛基础、状态、移动与结算共享边界 | 476 |
| cards-heat | 卡牌、热量、冷却、特技 | 301 |
| gears | 挡位与仪表交互 | 101 |
| tech | 科技规则/档案及科技接线、BBQ、母亲之路 | 472 |
| drivers | 车手规则、主动/被动、呈现与结算 | 103 |
| ai | AI策略与随机源 | 30 |
| tracks-weather-pit | 赛道、天气、维修区及相关执行 | 611 |
| career | 生涯进度、结算、配置与菜单 | 230 |
| tutorial | 教程规则、门控与反馈 | 235 |
| ui | 主题、布局、公共弹窗及实时仪表 | 180 |
| audio | 引擎、换挡、超车与声音生命周期 | 54 |
| diagnostics | 日志、回归证据及测试隔离 | 181 |
| long-races | 原RaceGameLoopCoroutineTests的真实完整比赛/生涯长流程 | 16 |
| tools | 本模块清单/选择工具的护栏测试 | 12 |

全部短集 **2986** ＋ 长流程 **16** ＝ 全量 **3002**；实测二者无交集，集合并集逐项等于全量。`core`有跨系统共享fixture，因此比单一模块宽；这有意保留已有测试，不拆改原用例。

## 何时扩大验证范围

- 改单个UI/音频表现：先对应模块，涉及卡牌、挡位、设置或生命周期时再组合相关模块。
- 改科技或车手技能：先 `tech,drivers`；涉及支付、出牌或换挡，再加 `cards-heat,gears`，需要共享结算保护时勾 `core`。
- 改状态机、移动/热量核心结算、存档或跨系统流程：受影响模块通过后，按风险补跑长流程/全量。
- 发布前、阶段收口需要完整模式验证、或用户明确要求：运行全量；没有新改动或失败时不反复重跑已通过集合。

新增fixture必须在JSON中分组。定向运行会检查未分组用例、过期fixture和重复归属，发现漂移立即拒绝执行，避免悄悄少跑；空选择/未知模块不会退化成全量运行。即使有未分组测试，`full`仍包含所有发现的测试，原Unity Test Runner全量入口也保持可用。

运行前拒绝播放中、编译/导入中、其他原生测试正在运行及任一已加载场景未保存的状态；不会自动停止播放或保存场景。原生繁忙查询依赖当前已核验的1.1.33内部 `IsRunActive`；包升级后若接口变化会拒绝启动并提示使用原生入口，不猜测空闲。

## 本轮实证与边界

- 工具护栏12/12，通过12、失败/跳过/不确定0，0.409484秒。
- 科技＋车手＋core：1051/1051，通过1051、失败/跳过/不确定0，13.8328204秒。
- 最终全部短集：2986/2986，通过2986、失败/跳过/不确定0，33.4249368秒；实际用例与预览精确一致，包含原生UI/音频生命周期用例。此前窗口缓存收尾前/后两次短集为30.7405407/34.0922539秒，保留独立证据，不相加作覆盖数。
- 原完整2990项回归710.715秒为此前同生产代码的历史基线，不能计为本轮执行。本轮未再执行16项长比赛；全量3002只验证选择清单完整，不冒称全量通过。耗时是原生执行时间，另有发现/调度/编译开销。

证据：[本轮记录](../production/qa/evidence/modular-regression-2026-10-16/README.md)。本轮仅新增Editor工具、清单及护栏测试，未修改游戏规则或已有测试；尚未专门强制触发运行中脚本重编译/取消等异常生命周期验收，不冒称这些场景已实跑。

自动化等待时须检查 ModularRegressionRunner.IsBusy（含异步发现准备态），并要求Error为空、LastResultPath非空且结果selectionMatches为true。新请求在发现前就清空旧结果，失败或准备态不能借用上一轮Passed。

## 2026-10-22 I00a 增量

英国本人输入时钟新增 43 个案例，沿用既有 `DriverSkillRuntimeTests` 和 `RaceTurnBoundaryIntegrationTests`，分别自动归入 `drivers` 与 `core`，没有新增 fixture 或改变分组。当前真实发现 3045 项：全部短集 3029，加长流程 16；本轮未运行全部短集或长流程。

本轮原生定向 `tech,drivers,core,tutorial` 选择并实际执行 1329 项，通过 1329、失败/跳过/不确定 0，17.031282 秒，`selectionMatches=true`。较上表历史基线，drivers 为 127、core 为 495，其余模块数量不变。首个改后组合 1325 项有一条旧的全局轮被动就绪断言失败；保留原移动/冷却及一次性断言，改为全局后为零、本人输入后兑现，再补 4 例复位/跳过边界后得到上述最终结果。详细原生记录见 [I00a 证据](../production/qa/evidence/tech-driver-I00a-2026-10-22/README.md)。这不是全量、Play Mode 或完整比赛通过。

## 2026-10-22 I00b 哈特费用、目标与 HUD

新增98案例：HunterSkillActivationPlanTests 53归drivers，HunterSkillCardChoiceUITests 31与HunterSkillCardChoiceNativeLifecycleTests 1归ui，既有RaceDriverSkillActivationAdapterTests增加13归drivers。原三项Hart热倍率断言保留黑面包顺序/耗次断言，按新规则改为普通热不放大，非新增案例。

最终原生 drivers/ui/cards-heat/gears/core/tutorial/tech 共2009/2009，失败/跳过/不确定0，22.5163197秒，selectionMatches=true；各模块为193/212/301/101/495/235/472。当前发现3143=短3127+长16，未选1134不是跳过；没有运行长比赛或全量。修复中的UI专项212/212、9.189481秒只作其独立证据，不与最终组合相加为独立覆盖。

三次失败组合1987(1978过9败)、2008(1999过9败)、2009(2007过2败)均保留。六条最初EditMode生命周期假设改为明确调用hook，另增真正EnterPlayMode验证；实际抓到重入SetActive并修复，焦点EditMode显式注册EventSystem而自然注册另由原生案例验证。完整比赛/模式、物理拖动/hoverpressed与平衡尚未验收。[证据](../production/qa/evidence/tech-driver-I00b-2026-10-22/README.md)。


## 2026-10-23 I01a 英国预备餐：生产接入与有限回归

- [x] UK L1 稳定节点 ID 下改为每车每圈弃1普通速度，旧永久热 H优先/D其次最多1、UK/GB+L3最多2；退役与净回分开，取消/过期不扣，旧不足免单移除。A2早餐后冻结，A4首次正式出牌前使用；保留原A1→A2顺序。
- [x] 独立科技按钮与车手按钮在A4/A1互斥使用动作槽；真实按钮→选择→确认/取消通过4项受控pointer测试，mask0/All一致。新规则21例，合计25新增。
- [x] 本轮原生2034/2034，失败0/跳过0，21.3845409s；首轮2030例有5失败（旧预期4、夹具缺config1），已保留失败证据并修正。非全量、非完整比赛。
- [ ] 成功退役净回、多UK与Hart费用组合、完整A2/A4实跑、focus/drag及独立复审待补；I01整体未完成，后续Hart被动/早餐矩阵仍未提前标完成。
- 证据 production/qa/evidence/tech-driver-I01a-2026-10-22/README.md。Unity最终MainMenu干净/idle，Console0error、既有cardPrefab测试警告1；Word原件和草案未改，未提交推送。

I01a 后续独立代码复审已 APPROVED、无必须修复项；视觉/QA与上列残余场景仍未验收，见 evidence/tech-driver-I01a-2026-10-22/review.json。


### 2026-10-23 I01a 补验收；哈特上限待回答

本轮新增8项预备餐来源/成功退役净回/双UK独立/早餐四矩阵/Hart两张费用生产规则测试。原生995/995、失败0跳过0，14.5479979s；非全量/非完整比赛，无生产规则/UI/音频变更。Console1个既有无效科技快照负面测试error、1个CN开局测试warning，原生通过。证据 production/qa/evidence/tech-driver-I01a-followup-2026-10-23/README.md。

哈特Word“原总上限”与代码只有两次原生尾流、无格数总上限不一致，异步询问两段原生尾流后是否仍+1，等待用户明确后才接新规则；不提前标I01b完成。已通过的8项残余测试不重复审计。完整流程/焦点拖动/模式仍未验。


### 2026-10-24 I01c 待执行回归

既有 ui 模块 UkPrepMealManagerTests 新增 FirstFormalSpeedCommitImmediatelyDisablesPrepMeal(false/true) 两例，验证非法提交保留窗口、成功提交立即禁用及旧动作不扣费。登记沿用既有 RegressionModules.json，无新夹具。独立编译通过；原生未执行，不计通过或跳过。


### 2026-10-24 I01b Hart真实尾流来源

HunterSlipstreamTests新增36例已登记drivers：三级3/4/5格、两段原生后仍+1无第三段、第二段锁定源、同ID替换不继承、无/部分真实原生遍历、每圈额度、非0终点/数组回绕、旧receipt跨输入、地标/预约及终态/预测/模式清理。与HunterSkillActivationPlanTests 53例独立Mono合计89/89，0失败/跳过，0.1381324s；生产程序集真实C#执行，未宿主Unity。首次123/122/1中AssetDatabase UI用例不适合独立环境，记录保留；不把最终缩小选择当全量通过。Unity原生/实际Manager协程仍待执行。


### 2026-10-24 I01d：费用模态焦点隔离与原生补验

费用选择打开后移焦取消，四按钮使用窗内Explicit导航；翻页禁用当前箭头前保存焦点，处理Unity自动清空选择，关闭恢复原Navigation及合法原焦点。新增8项真实EventSystem Move/Submit回归；沿用既有ui/core夹具登记，无新夹具。首轮2363总/2360过/3失败/0跳过、12.8776254s：焦点1项、缺TrackManager的旧GameLoop时钟夹具2项。生产修复焦点，夹具补20节点，未弱化玩法断言。

刷新并核对新runtime/editor MVID后，ui/tech/drivers/cards-heat/core/tracks-weather-pit原生RunFinished 2363/2363、0失败/跳过/不确定、12.1640629s，selectionMatches=true；包含I01b36项与I01c2项。本轮只定向，不是全量、完整赛事或模式验收。生产受控RaceCanvas三尺寸15张新after图/0溢出，root实际查看6张；没有本轮改前新图，不把历史I00b图冒充新before，也不据静态图验收真实拖动、六队/参赛车数/模式。

Unity最后MainMenu/全部loaded scene干净、未播放/编译/导入/测试占用；许可证为Personal。Console一个no_such_track预期负面测试error（LogAssert.Expect），无编译错误；未清Console。editor/state资源仍stale_status，记录直接实时查询与原生防护，不伪称ready缓存为true。I01仍进行中。证据 production/qa/evidence/tech-driver-I01d-2026-10-24/README.md；下一包I01e为真实Manager尾流协程与A2/A4环路及剩余输入/视觉证据，不重复已过模块。


### 2026-10-24 I01e：哈特新增位移的真实弯心费用

按Word共用路径边界补生产接线：主动只纳入去掉Hart后到不了的尾端弯心，角速仍为原速度牌总值；与原card角同一次普通结算，物理弯段去重、跨圈再经过仍收费。新增弯心在最终线前时，以本次协程局部状态延后最终完赛登记，费用不足仍部分支付/失控、不退技能次数，也不能凭失控前计划路径预约进站/领地标。被动保留两原生段后独立实际1格，真实格数先消耗原圈额度，再按原角速付新增弯心费用；原生尾流及其他车手既有路径不扩大角判。主动预测使用同范围/副本支付，不花真实牌/热/次数。

新增16项：12规则/预测参数案例，4真实原生协程案例。首轮2168/2168、0失败/跳过、285.8493526s，含19长流程，但在最后失控路线门控前，保留版本边界。最后修订MVID已确认，五个受影响模块加5个精确原生案例（4新+既有六队两回合）RunFinished2154/2154、0失败/跳过/不确定、10.3944555s，selectionMatches=true。不是全量，也不是最后版本重新跑19长流程。新增partial沿用RaceGameLoopCoroutineTests既有long-races归属，无新增fixture、无清单漂移。

真实GameLoop验证A2早餐后冻旧永久热、A4主场回2且保留早餐临时热、正式速度提交关窗；主动完整A1/A4→移动→角费→finish/pit；被动是生产尾流结算适配器协程，非完整回合；中断项是局部finish-gate生命周期，不冒充完整Hart GameLoop退出。Unity最终MainMenu/loaded scenes无dirty、未播放/编译/导入/测试占用、Personal；Console仅no_such_track预期负面测试error。ready资源仍stale_status，以直接实时查询/原生安全门和RunFinished为据。初次编译3个Action缺using错误已改全限定名，未在旧程序集上跑通过；MCP会话404重新初始化，无配置/服务安装修改。

无新UI布局或截图；新角费风险提示、真实改前后三尺寸操作仍待I01f，不复用历史图冒充本轮视觉验收。I01仍进行中，I02-I13不提前勾选；下一包有限补提示及操作，通过后推进I02。证据 production/qa/evidence/tech-driver-I01e-2026-10-24/README.md。

### 2026-10-25 I01f 三尺寸真实技能确认

新 HunterSkillProductionUiTests 登记 ui，36案例：三尺寸×六夹具×mask0/All，真实 HUD.Start/driver button/MVP目标费用计划；确认同值第二张原实例只扣一次、取消不扣、键盘/射线遮罩/合法焦点、risk文字及TMP无溢出。CareerUiPointerTestScope新增CreateSized，原Create仍1920×1080，预览/字体/RT及设置/会话/音频静态状态均恢复。

最终 loaded runtime6c8c3586-de04-4d92-b2c8-5603486e8a98/editorcf76e26f-bdad-4fd0-b702-491f73561dc6，ui/drivers/tech/gears/cards-heat 原生RunFinished1394/1394、失败/跳过/不确定0、10.1942977秒、selectionMatches=true；含7既有gear拖动案例。非全量/长流程重跑、硬件输入或完整模式验收。首捕获夹具GearButtons层级错误保留为捕获失败，不混入NUnit通过数。证据 production/qa/evidence/tech-driver-I01f-2026-10-24/README.md；后续I02曼森，同步保留I13模式/UI/存档验收。


### 2026-10-26 I02b：曼森连续追近被动有界接入

接续既有 I02a 未验收源码，现已核验本人 A1 两次观察→A2 抽牌/早餐后冷却→预备餐来源冻结的生产顺序。同最近前车原实例/ID、正差距严格缩小，三级终差距 2/3/4；每圈首次合格回旧手永久热最多1，无热过期。弃区付款、新抽热和临时热不得补源，退役与净回分开；原超车赠位移/回热已撤销。修正“预约进站前车被错误排除”，冻结热池身份；未改主动、ID/XP/等级/UK次数或普通热支付。

新增48项 MansellPursuitTests 登记 drivers，另加1项真实 GameLoop A2 协程（沿原long-races夹具归属）。首轮六模块1824总/1822过/2失败/0跳过、26.4781307秒：重复热夹具使用拒绝永久热的公开API，及遗漏旧位移奖励断言；原件保留，修正夹具/旧预期并补真正负赛程差/双UK独立/初始化重赛案例。最终新程序集 drivers/core/tools 原生RunFinished790/790、0失败/跳过/不确定、14.0069824秒，selectionMatches=true；精确GameLoop案例1/1、1.9035575秒，原生RunFinished交叉确认。此前未变tech/cards-heat/tutorial结果保留，不冒称最后版本重新全量。首次协程断言误用NUnit字符串Contain造成1个编译错误，修复并确认新editor程序集后才运行，未用旧程序集测试冒充通过。

最终MainMenu及全部loaded scene干净，未播放/编译/导入/原生测试占用，ready=true且非stale，Console0错误/0警告；许可日志entitlement解析成功。无新UI、截图或三尺寸验收；旧技能面板名称/说明、两段主动、完整模式/存档组合仍未完成，I02保持进行中。证据 production/qa/evidence/tech-driver-I02b-2026-10-26/README.md。下一有界包I02c接固定费用首段与同目标下一输入第二段。未提交推送，Word原件、InitTestScene和其他既有改动保留。


### 2026-10-26 I02c：曼森两段主动事务与移动适配（玩家按钮未迁移）

生产新增MansellChargeRuntime/ActivationPlan及CardDeck整笔费用：首段锁最近原车1–6格，弃普通速度原实例1并付引擎永久热1到弃区，三级首段+1/2/2；下一本人A1同最近车正差距严格缩小且有热1才开放可选续段+2/3/3，仅付热1、不再扣场次数。最后一次首段仍能续；取消/过期/资源不足/跳过/进站/终态结束，不延期等A2回热。计划绑定会话/本人/目标/ID/牌组/热池/输入/轮/挡位/请求及发行版本/全手牌，重发与外部会话隔离；预测复制独立，普通罚热的部分支付/失控保持。

新路径已接Session A1、实际移动及新增尾端弯心费用，原角速不增加、不叠旧超车奖励；管理器退出统一清曼森激活态，不退款。旧玩家按钮尚未迁移，新付费序列与旧激活互斥；只把规则/API/有界GameLoop标通过，玩家可操作/三尺寸/全模式仍未完成。

新增61项纯规则/生命周期适配器案例登记drivers、2项真实GameLoop协程沿原long-races夹具。首轮1972/1971/1失败、32.9891449秒是换牌组夹具Context空池，已修；中间1984/1984、31.8671046秒在最后外部会话/退出修正前，保留版本边界。独立审查发现并补来源/计划重发/退出清理及fallback/外部会话缺口。最终新runtime b825ebc4-d36e-4855-bace-c69939a2b42b，drivers/core/cards-heat/tools原生RunFinished1152/1152、0失败/跳过/不确定、18.154035秒，selectionMatches=true。随后仅修协程夹具，原runtime不变：两协程最终2/2、0失败/跳过、3.3132155秒，SessionState原生RunFinished交叉确认。三次中间协程1过/1失败分别是无效0限速弯心、名称映射未配、失控分支误等弃牌；未弱化实际费/次数/守恒断言，原件保留。

最终MainMenu全部loaded scene干净，未播放/编译/导入/测试占用，ready=true且非stale，Console0错误/警告；32个场景文件SHA保持，Word采用源SHA未变。无UI/文案布局变更或新截图，旧按钮仍待I02d按新事务迁移，并完成真实改前后三尺寸；完整模式/存档组合留I13。证据 production/qa/evidence/tech-driver-I02c-2026-10-26/README.md。I02保持进行中，I03未开始，无提交推送/清理/自动任务恢复。


### 2026-10-27 I02d：曼森玩家入口源码接线，Unity 验收待续

真实 HUD 与 Manager 两处技能入口已在源码中转入 MansellChargeActivationPlan：首段必须选择普通速度牌原实例，即使关闭额外确认仍不可绕过；续段只付1张引擎永久热，不弃速度牌、不再扣场次，遵既有确认开关。复用单一模态、取消/改挡/退出清理及焦点返回；续段取消关闭窗口。名称/说明改为“蓄势追近”“雄狮追击”，ID、XP键、解锁及UK2/2/3次数不变。末次续段按钮明确“不扣次”，不显示歧义×0。

新增 MansellSkillProductionUiTests 登记 ui，共72个参数组合（3尺寸×12夹具×mask0/All），包含原实例、重复入口、末次场次续段、键盘、遮罩、改挡/退出取消和目标过期；mask0部分续段场景直接提交，不是72种独立行为。静态QA发现禁用按钮不能用强制可交互Click helper，已改为断言禁用后派发事件。本轮真实测试运行0、通过0、失败0、跳过0、测试耗时0秒，72例未执行。

最终使用当前全部runtime/editor源码及新runtime引用离线Roslyn编译成功，分别1.328/1.547秒、合计2.875秒，日志和源码SHA已保存；不是Unity导入/加载/原生RunFinished。Unity进程存在，许可证日志解析成功，但MCP8080拒绝连接，窗口抓取未能可靠返回Unity；不再操控编辑器、不进入播放、不保存场景，当前isDirty/播放/编译导入/Console/ready无法完整实查。无有效改前/改后三尺寸截图；捕获器禁止把当前新源码标成before。四行文案和续段两行按钮仍待实际测量，不能宣称零溢出或玩家可操作通过。

I02继续进行中，I03不开始；下一包仅恢复安全只读连接并完成新程序集、定向模块与三尺寸操作验收。完整RaceCanvas/硬件/模式/存档留I13。证据 production/qa/evidence/tech-driver-I02d-2026-10-26/README.md。未提交推送、未清理，Word及32场景原件保留。


### 2026-10-27 I02e：曼森玩家入口有界原生/三尺寸验收，交接I03

Unity连接恢复后，实查MainMenu及全部loaded scenes干净、未播放/编译/导入/测试占用、许可证日志有效、Console初始0错误/警告、ready初始true且非stale；已确认I02d新API/夹具实际载入。首轮72参数组合运行后报告正文overflow失败，原MCP仅有completed72与封顶失败样例、缺原生根汇总，保留原件而不虚填精确过/失败/耗时。实际捕获修复前36图。生产仅把共享选择窗正文高度92调至120，保留字号19、字形位置、按钮、费用/规则和原布局恢复。

确认新runtime 1dad8c6c-b187-451c-81be-3974948136fe加载后，原生ui/drivers/tech/gears/cards-heat/core RunFinished **2076/2076，失败0/跳过0/不确定0，22.5495703秒，selectionMatches=true**；含曼森72个UI参数组合、9项既有拖动相关案例。验证首段原实例弃牌+热/次数，续段只付热/最后一次仍可续、关闭额外确认、重复入口、同值牌保留、键盘/焦点、遮罩、取消重开、改挡/生命周期/目标过期无费用。72是参数组合，不是72种独立行为。未跑全量或长比赛；另在当前新程序集定向重跑3项真实GameLoop，3/3、0失败/跳过、3.1201921秒，原生RunFinished确认。覆盖真实两段续追与其他车动作、尾端弯心普通部分支付/失控、A2旧手永久热来源；I02b/c旧原件仍是历史，本轮这3项为新执行通过。

1920×1080、2560×1440、1280×720修复前36/后36均实际逐张查看；修复前33条正文overflow，后0条。**36对PNG SHA完全相同**：本修复矫正容器边界/溢出标记，保留本已可见的字形，不能称为字体变大或视觉排版改观。原I02d旧UI改前图仍缺；本轮图片只是I02d新UI与I02e修复前/后，不回填伪造。背景入口/挡位是拥有控件，确认面板/Manager为生产，EventSystem事件不冒称硬件或完整RaceCanvas。

最终MainMenu干净、未播放/编译/导入/测试；模块后Console0错误/1条既有cardPrefab夹具警告，三协程后Console0错误/0警告，未主动清Console。ready资源变为stale_status，以直接实时查询与原生RunFinished记录当前状态，未声称最终ready资源新鲜。I02规则/生产/有界玩家操作通过；完整RaceCanvas、硬件、所有模式/存档组合、720p舒适度仍I13。执行指针进入I03德国有限爆发，不在I02重复审计。证据 production/qa/evidence/tech-driver-I02e-2026-10-27/README.md；截图目录 D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I02d-2026-10-26/after/run-20261026-180113728 与 run-20261026-180515665。无提交推送/清理，Word和场景原件保留。


### 2026-10-27 I03a：德国黑啤固定费用生产接线（有限爆发未完成）

黑啤现在经CardDeck.TryPayFixedPermanentHeatToDiscard和Session.TryCommitSchwarzbierFuel，只有实付1张永久引擎热到弃牌区才获得+2；记录同一原实例到本车本回合收据与真实付热计数，成功才记每圈使用。生产ComputeMovements和预测共用提交规则，预测只改深复制deck/tech状态，不动真实资源、牌值、收据或次数；复制终态标记防止终态副本凭空触发。

经源码核对，旧问题确切是舒马赫被动可把黑啤1热折到0、黑啤会误耗黑面包次数；当前主动PerfectLap仅免角费，通用热倍率已为恒等，不能把旧问题说成主动免单/倍率现仍造成免费黑啤。固定费用保留普通折费/黑面包供后续普通费用使用，普通罚热部分支付与失控路径未改。

新增SchwarzbierFixedPaymentTests20例登记tech，既有RacePitTurnBoundaryTests新增8个真实MVP移动适配器参数，沿tracks-weather-pit归属。初始新测试两处Does.Not.Contain误选字符串重载导致2条CS1503，已改CollectionAssert并确认新editor载入后才运行；另在静态审查中纠正免角费前置和终态未入手选牌的假通过风险。最终runtime2f6be9ac-e642-4a6b-a4ae-d9e3429edeea、editor561035bf-5f01-4907-aa0d-0fb7cf3d95c9实际加载；原生tech/drivers/cards-heat/core/tracks-weather-pit **2286/2286，失败0/跳过0/不确定0，27.771329秒，selectionMatches=true**。非全量、非本轮GameLoop/PlayMode或长比赛验收。两独立审查完成初步反馈后遇额度限制，最终复审未完成；父任务修正测试并完成原生验证，不伪称独立全审通过。

最终MainMenu及全部loaded scene干净，未播放/编译/导入/测试占用，许可日志有效；Console仅1条no_such_track预期缺配置负面夹具日志、0警告、无编译错误，未清Console。ready资源为stale_status，保留直接实时状态与原生RunFinished，不当新鲜ready。没有新UI布局/文案/入口或截图，三尺寸手动交互未验。L1仍沿旧自动时机，L3仍是旧自动收尾；手动声明、三次合计预算、首笔实付锁一次机会、开窗后收据冻结返还及全模式都未完成。I03保持进行中，不进入I04。下一I03b接上述有限窗口/实际支付链，随后单独完成手动UI。证据 production/qa/evidence/tech-driver-I03a-2026-10-27/README.md。Word/32场景与其他脏改动保留，无提交推送/清理。


### 2026-10-27 I03b：德国有限爆发运行时与生产适配（手动入口待接）

已接入每赛车/会话/牌组/热池/回合绑定的L3声明API：确认挡位前免费声明，首笔开窗后的真实永久热付款才锁本场机会；取消仅可在未实付且版本匹配时进行。黑啤本回合成功合计最多3次含声明前常规次数，跨线/冷却/重复BeginTurn不刷新，圈标记冻结声明时圈。实际移动聚合已付+2/次，重复计算不再扣费或丢收益。普通罚热部分支付/失控规则保持，Mansell固定付款也登记同一永久热收据。

窗口排除全部旧实例，仅返开窗后仍在本人手/弃区的原收据；先前已冷却不替换，净回引擎与容量退役分别计数。预测深复制收据和旧来源排除集合，隔离真实热池；替换牌组/热池使旧窗口失效。回合重置、退出与收尾finally清引用，技能/科技回调异常也关闭窗口，已锁场次机会不退。

真实验证：新runtime 1355eddf-9d2a-4dae-8a1d-19ecc171d10e、final editor a0cab66d-67cb-488c-aee8-cb227b684d94。8模块tech/drivers/cards-heat/core/tracks-weather-pit/gears/ui/tutorial原生RunFinished **2990/2990，0失败/跳过/不确定，19.5448375秒，selectionMatches=true**；新增29运行时+5真实MVP参数案例登记模块。首轮2988过/2失败/0跳过，18.717076秒；旧夹具误要求重复计算丢已付位移，改为保留+2且仍只扣1后重跑。另3条实际Mansell GameLoop **3/3，0失败/跳过，3.3656232秒**，native root确认；发生于仅测试预期修订前，生产runtime未再改变，不冒充完整德国手动比赛。引擎与QA最终只读有界复审均通过。

MainMenu全部loaded scene干净，停止播放/编译/导入/测试；许可已解析。Console error1（预期no_such_track负面夹具）、warning1（UI回归后CardHandUI未设cardPrefab），未清除；无项目编译错误。ready资源stale_status，使用新鲜直接状态和原生结果。一次诊断查询误用IsTestRunActive导致CodeDOM查询编译失败，已改查询，不是项目脚本失败。

**I03仍进行中：真实玩家手动L1/L3按钮、固定费版本化确认、取消/禁用/剩余次数/返还与退役提示、手动完整GameLoop尚未接入；未声明的新窗口之外仍保留旧自动兼容路径。** 本包没有UI布局/文案修改或新截图，三尺寸视觉/交互未验；终态/跳过/进站完整比赛、完整模式/存档仍待I13。下一I03c接手动入口及确认后迁移旧自动选择，不进入I04。详见production/qa/evidence/tech-driver-I03b-2026-10-27/README.md。Word原件、场景和全部既有脏改保留，无提交推送、清理或系统设置修改。


### 2026-10-27 I03c：黑啤固定费用请求与Manager生产API（HUD未接）

新增GermanFuelActivationPlan：免费创建/取消；确认前冻结并复核session/本人/回合/请求版本、科技与运行态版本、牌组/热池身份、圈/位置/挡位/车队/借用目标/有效节点集合、引擎/待退役和手牌/抽牌/弃牌/实付收据四区原实例及type/value/temp/trick。全部有效后只调用一次既有固定永久热提交，失败或重复回调无新增扣费；不弃速度牌，空手仍可实付，不自动为后续角费保留资源。空科技集合拒绝但不修写原数据。

Manager新增TryPrepare/TryConfirm/CancelGermanFuelRequest及pending引用/请求版本；重发取消旧请求，旧取消/确认不能触及新请求，改挡/确认挡位/关输入/生命周期沿既有取消钩子释放。仅本人真实A4输入开放且科技开、非教程时有效；实际换配置/切教程在确认前重新检查。取消燃料请求不撤销L3声明或其他真实付款造成的场次锁。

最终新runtime c33b40ad-fc92-429a-8fb8-28cfd734fafe、editor ab0c90c2-2b5e-48ad-93bc-300585596ee7实际加载；tech/drivers/cards-heat/core/gears/tracks-weather-pit/tutorial **2712总/2712过/0失败/0跳过/0不确定，8.0673993秒，selectionMatches=true**，含新增56参数案例（已登记tech）。初次测试清单未导入触发inventory安全拒绝，执行0；刷新后才跑，不算通过。中间2702/2702与2706/2706分别在空集合修复和额外QA案例前，最终以2712为准。三条实际Mansell GameLoop在最终程序集重跑 **3/3，0失败/跳过，3.3701701秒**，native root确认，不算德国手动GameLoop/长赛/全量。引擎与QA只读复审有界通过，QA人工数52已由原生56校正。

最终MainMenu全部loaded scene干净，未播放/编译/导入/测试，许可成功解析，Console0错误/0警告，未清Console；ready资源stale_status，直接实时查询/native RunFinished为据。Word草案与已记录原件SHA未变，本包新扩充3份正式Word到只读清单，共8份，并非用户修改；32场景与其他既有改动保留。

**本包仅生产计划/Manager API接线，尚无HUD按钮或弹窗绑定，不算玩家可操作。** 取消/过期请求当下不扣费，但继续走旧ComputeMovements仍可能独立自动付黑啤；不能据本包宣称“完整取消后移动不扣”。新UI几何/文案未改，截图绝对路径无，三尺寸未验。下一I03d实际接L1/L3操作、确认/禁用/焦点和旧自动路径迁移，并补取消→正式出牌→移动不扣费回归。I03保持进行中，I04待办，完整模式/存档仍I13。证据 production/qa/evidence/tech-driver-I03c-2026-10-27/README.md；无commit/push/reset/checkout/rebase/清理或系统设置修改。


### 2026-10-27 I03d：真人黑啤L1实际HUD与取消→出牌→移动（L3仍待）

A4新增GermanFuelActionUI，沿阶段独占的原driver action slot，按钮显示本圈余次、费用/收益、缺热/已用/待确认文字。HUD直接路由Manager版本化固定费计划；沿CardAction确认开关，开启时共用一次可取消确认、关闭时直接提交。确认正文包含本人长名、永久引擎热→弃牌、+2与后续普通角费/失控风险，不弃速度牌；专用无牌文案不误写第二段。支付后焦点可回有效出牌按钮，模态/详情阻止底层点击，显式回调/退出取消旧请求。

真人ComputeMovements与预测不再自动购买黑啤，只汇总已确认付款的+2；普通AI科技自动策略保留，不顺带启用AI车手技能。取消/过期后，经真实ConfirmPlayerSpeedCards和ComputeMovements不再扣热。已付黑啤但尚未选速度牌时也显示预测+2，并以复制的skip/kanto/terminal标志防止重新激活跳过或终态；实际缺牌罚热沿旧普通支付，不退款。

最终新runtime be928c88-1478-48b7-b060-8cf4cb7beeb2、editor b3999214-a847-40c7-b71b-207be910dba6实际加载。tech/drivers/cards-heat/core/gears/ui/tutorial/tracks-weather-pit原生 **3087总/3087过/0失败/0跳过/0不确定，32.6400771秒，selectionMatches=true**。新增36 HUD参数（3尺寸、mask0/All、两德国现有车手、六队周边/2/4/6/12参赛车、长中文数字、pointer/Submit、取消/重开/禁用/失效/显式OnDisable、遮罩/焦点、真实出牌→移动）登记ui；5新增空选择已付燃料预测/缺牌付款/实际移动对照沿原tracks-weather-pit。既有拖动随模块回归通过，非本包新增拖动机制。另最终程序集实际Mansell GameLoop3/3，0失败/跳过，4.1799126秒，native root确认；不是德国完整手动GameLoop、长赛或全量。

原生首轮3082总/3062过/20失败/0跳过，32.5160696秒，暴露A4焦点返回缺失、EditMode未触发原生OnDisable及旧auto预期；修复焦点/明确调用生产回调/校正人机策略断言后3082/3082，随后强化真实正式出牌、修复独立审查发现的已付空选择预测，最终3087为准。初始CS0103已改System.Math，未拿旧程序集通过。引擎最终只读有界批准；并行UI/QA代理早期额度失败未写文件，父任务实作与实际验收，不声称独立UX/QA全审通过。

三尺寸有效改前24图（run-20261027-040534023）与改后24图（run-20261027-041640341），已查看所有动作/模态区域，另查看三张最终全帧，前后均0溢出。真实HUD.Start/生产widget/Manager计划置于自有预览Canvas，操作位沿生产归一化尺寸；周边自有背景，非完整RaceCanvas或硬件键盘。初次错误GearButtons夹具产生216个标签溢出记录的旧图完整保留但不验收；修好夹具后在生产改动前重拍。改后加入自有出牌焦点目标，背景差异不当生产布局变更。详见production/qa/evidence/tech-driver-I03d-2026-10-27/README.md及绝对截图目录。

最终MainMenu全部loaded scene干净、未播放/编译/导入/测试，许可证已解析，Console0错误/0警告未清；ready资源stale_status，以直接实时状态/原生RunFinished为据。8 Word/32场景、所有既有改动/未跟踪/排除文件保留，无提交推送、清理或系统/快速播放设置修改。

**I03仍进行中：L3声明/取消实际按钮、与A1车手技能同屏共存、三次合计操作、真人旧自动L3返热迁移及净回/退役反馈待I03e。** 本包只接完真人L1有界操作，不提前进I04；完整RaceCanvas、硬件输入、完整比赛/模式/存档与720p主观舒适度留I13。


### 2026-10-27 I03e 有界验证

A1德国L3声明/取消与driver分槽、A4燃料与未付取消/锁定禁用、三次合计、现有确认遮罩/焦点已接生产。GermanBurstProductionUiTests48参数登记ui、GermanBurstManagerTests8登记tech；新增2条actual GameLoop沿既有long-races。十模块3403/3403（43.9000283秒），GameLoop2/2（2.9225509秒），均0失败/跳过。前后各24张三尺寸受控Canvas图已查看，0溢出；后摄影机改正交修复EditMode透视RT检测异常，仍走真实Raycaster，不能当完整RaceCanvas/硬件验收。前A1旧自有底色对比不足不通过；terminal/exit图在清理前，资源断言在之后，日志净回/退役未视觉渲染。I03有界交付进入I04，完整模式/存档/硬件留I13。详见production/qa/evidence/tech-driver-I03e-2026-10-27/README.md，含截图绝对路径、首轮25失败与后续16夹具失败修复证据。


### I04a 2026-10-27 舒马赫被动有界验证

真实普通弯道付款调校已接线；42规则、24适配器、3车手页案例登记drivers/ui，1新增实际GameLoop沿long-races。最终11模块3484/3484（40.2925337秒）及实际GameLoop3/3（4.2840327秒），均0失败/跳过。被动摘要更新且仅D03字号12..14适配；三尺寸改前后各3图全部查看，本次文字0overflow；全页每尺寸14个既有其他文字问题保留，不算全页UI/硬件/完整模式验收。首轮3失败即本次新摘要溢出，已修；pitScheduled预测复制遗漏静态修复并加2例。I04主动/维特尔待做，详见production/qa/evidence/tech-driver-I04a-2026-10-27/README.md（绝对截图/程序集/原生结果/边界）。
