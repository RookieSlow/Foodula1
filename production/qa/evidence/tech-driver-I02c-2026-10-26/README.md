# I02c 雄狮追击：固定费用事务、本人窗口与真实移动

2026-10-26；本包跨06:53与07:23 UTC两次heartbeat连续完成同一主题，未另起研究或I03。使用code-review与regression-suite流程，ADR-002分层。

## 接入与边界

新增MansellChargeRuntime/ActivationPlan；CardDeck验证完整固定费用后一次弃普通速度原实例1及永久热1到弃区；首段锁1–6格最近原车，tier1/2/3收益+1/+2/+2。下一本人A1同最近原车正差距严格缩小、引擎有1热才开放可选第二段，收益+2/+3/+3、仅再付1热，不扣第二场次数。首次场次数耗尽仍可续；取消、过期、热不足、跳过、进站与终态不退款/不延期等A2回热。

Session已接本人A1观察及TryPrepareMansellCharge，实际移动读取已付费段；复用现有UK额外尾端弯心适配，原角速不增加，普通罚热仍可部分支付并失控。新段不叠旧超车奖励。Manager退出清理取消新计划/序列和激活态，保留已付费与已用次数。固定计划绑定session/参赛车/原目标/ID/牌组/热池/输入/轮/几何/拟选挡位/请求和发行版本/整手牌值及实例。副本独立，不修改共享CardData。

**新玩家按钮尚未接入。** 旧按钮在未进入新付费序列时仍保留原行为；一旦进入新序列则互斥，旧入口不得插入免费激活。这里验证生产API、A1时钟与实际GameLoop适配，不宣称正常玩家已能操作新主动。I02d必须迁移按钮与统一确认、文案/禁用/余次后再进行三尺寸验收。

## 真实测试

- 新MansellChargePlanTests 61例登记drivers；新2个真实GameLoop协程沿RaceGameLoopCoroutineTests既有long-races归属。
- 首轮七模块1972总、1971过、1失败、0跳过/不确定，32.9891449秒：换deck夹具Context读到空pool，nullsafe修正。
- 中间1984/1984、31.8671046秒通过，但在最终外部会话/paid退出修正前；仅中间证据。
- 最终drivers/core/cards-heat/tools原生RunFinished **1152/1152、失败/跳过/不确定0、18.154035秒，selectionMatches=true**。runtime b825ebc4-d36e-4855-bace-c69939a2b42b，模块editor0c5fe9d5-d696-4047-8a13-192942072fcc。
- 随后只修协程夹具，生产runtime未变；最终新editor f7783e3a-4051-42a4-89fe-4f22311ac19f上两真实GameLoop协程 **2/2、失败/跳过0、3.3132155秒**，独立SessionState原生RunFinished同为2/2。验证其他车真实行动后的第二次差距、新尾端弯心正常付费及热不足失控。
- 三次中间协程均1过/1失败：0限速被生产视为直道；自建弯心未填名称映射；失控跳过弃牌而正常助手误等弃牌。改用合法限速1/卡速2、正确名称表、观察实际失控或弃牌分支，原费用/次数/守恒断言保持。全部失败证据保留，未计通过。
- MCP库存3386只是发现数；无本轮全量或全部长赛结论。生命周期回归调用真实inactive Manager.OnDisable方法，不冒称实际用户关窗验收。

独立审查发现并修正：退出未清Charge；续段发行前来源/身份变化；旧取消影响重发计划；退出后fallback旧加成；外部session失败请求关闭真窗口。最终静态有界APPROVED，见review.json；静态审查不替代运行。

## Unity与保留

最终MainMenu及所有loaded scenes干净，未播放/编译/导入/测试，ready=true且非stale，Console0error/0warning；未清Console。Personal/entitlement日志沿本会话有效；未改Unity版本、快速播放设置、包或系统。32场景SHA保持；Word采用源科技281e077813344ab1e3627bb075dcc28d361a0626c8c39b3492f016a9f62d7bb3、车手94eb7763825f777d477b3fcb8fee92bad27e6353e42d8b2cdeb98f3889914f46不变。active.md与.Codex/docs治理缺失仅记录，不建占位。

未改UI布局、技能名称/按钮或确认文案，没有截图；真实1920×1080、2560×1440、1280×720改前后及完整模式/存档组合仍未验。未改ID/XP/解锁/UK2/2/3次数、普通AI开启或存储字段，无删除/移动清理、Git提交推送或恢复其他自动化。

## 下一有界包

I02d把真实曼森按钮接到本包已验收API：首段必选原速度牌、第二段可选付热确认，合并现有确认开关；维护取消/返回/焦点/遮罩/点击拖动及禁用原因/余次，改前后三尺寸实际查看。I02继续进行中，I03尚未开始。

原始status/diff/完整基线与源码SHA在 D:/UnityProject/Foodula1/Library/AutomationReview/tech-driver-I02c-20261026/；此目录保存原生结果与关键状态，最终保留审计见preservation-audit.json。
