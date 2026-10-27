# I03c 黑啤固定费用请求与Manager生产API

有界接入：GermanFuelActivationPlan按真实输入快照验证后，复用既有一次固定永久热付款；MVP持有pending引用和请求版本、提供准备/确认/取消API，并把旧请求取消接入原改挡/确认/准备动作/结束输入/退出生命周期。未增加HUD按钮或弹窗，不声称玩家可操作。

计划冻结session、赛车、turn/request/runtime、牌组/热池/科技身份、圈位挡/车队/借用目标/节点集合、引擎及待退役、H/draw/D/paid四区原实例和元数据；全部重验后才扣1，产生唯一原收据、+2已付位移和预算记录。空手可以付；不复用曼森弃牌门槛，不替用户保留未来角费资源。取消只关此请求，不撤销L3声明或别的真实付款。空节点集合安全拒绝不修写。

## 真实证据

| 项目 | 结果 |
|---|---|
| 最终原生模块 | tech/drivers/cards-heat/core/gears/tracks-weather-pit/tutorial |
| RunFinished |2712总/2712过/0失败/0跳过/0不确定，8.0673993秒，selectionMatches=true |
| 新夹具 |GermanFuelActivationPlanTests，原生56参数案例，登记tech |
| 实际GameLoop |最终程序集3总/3过/0失败/0跳过，3.3701701秒，native root Passed |
| runtime |c33b40ad-fc92-429a-8fb8-28cfd734fafe |
| editor |ab0c90c2-2b5e-48ad-93bc-300585596ee7 |
| 原生完整结果 |D:/UnityProject/Foodula1/Library/Foodula1/RegressionRuns/20261026T234018900Z-bdd64b9b/result.json |
| 本包所有原件 |D:/UnityProject/Foodula1/Library/AutomationReview/tech-driver-I03c-20261027 |
| 新截图绝对路径 |无；未改HUD几何/文案/弹窗，不作视觉验收 |

首次runner在清单TextAsset导入前拒绝46个未映射案例、completed0；源码清单已写但尚未导入，force refresh并检查catalog包含新fixture后才运行，不计通过/失败用例。首轮2702/2702(8.1302243s)在空集合修复前；第二轮2706/2706(8.3303345s)在6项额外QA测试前；最后新editor加载后2712/2712为最终依据。没有项目编译失败，也未拿旧程序集结果冒充最终版本。

最终56例覆盖整笔一次/原收据、免费取消保留L3、空手付款、foreign session/同队另一车/turn/request/input/禁科技、31种状态失效及空集合、其他真实付款锁L3后旧计划失效、并发计划不双扣、普通1+爆发2=3、另一车独立预算、弃区仅元数据变化、实际MVP重发/旧取消/改挡/确认/OnDisable/关科技/切教程。QA静态人工统计52为旧数，原生56准确。引擎与QA最终只读有界复审无阻断。3个GameLoop是既有曼森两段、尾角费、A2冻旧热实际协程，覆盖本包取消钩子的已有流程；不是新德国完整手动GameLoop、PlayMode全套、长赛或全量。

## 未验收边界与安全

HUD按钮、L3声明/取消显示、固定费弹窗、确认开关/焦点、费用/余次/禁用/净回与退役提示尚未接入，三尺寸无新验收。**API取消/过期回调当下不付款，但旧ComputeMovements仍可能在后续自动付款。** I03d在手动UI上线时必须迁移human自动燃料/预测与旧L3，并补取消→正式出牌→移动不扣的真实链回归。当前不能把Cancel API测试当完整玩家取消。I03继续in_progress，I04 pending，全模式/存档I13。

最终MainMenu全部loaded scene干净，未播放/编译/导入/测试，许可证Successfully resolved entitlement details；Console0错误/0警告，未清除。ready资源仍stale_status，保留直接状态及native RunFinished。两份草案与历史清单5Word SHA未变；本轮扩充3正式Word清单共8份，不是Word内容变化。所有Word/32场景/其他脏改/未跟踪/排除文件保留。缺active.md/technical-preferences/coding-standards/control-manifest只记录不建占位，TR registry及ADR存在。Git状态/diff/SHA前后在本包Library；无提交推送、删除移动、安装或系统/快速播放设置变更。
