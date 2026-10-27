# I03b 德国有限爆发运行时与生产适配

本包完成可独立验证的声明、实付锁定、三次预算和原收据返还，未完成玩家手动按钮。I03保持进行中，I04待办。

生产改动：新增GermanBurstRuntime按session/赛车/牌组/热池/回合绑定；RaceSession接声明/取消/预算/实付记录/净回退役；TechTreeState预测深复制；MVP挡位确认封口、按全局轮开始窗口、移动聚合实际已付燃料、独立窗口收尾和退出清理；Mansell固定付款登记真实收据；RaceTurnCleanup及TechnologyCleanup全程finally关闭窗口。普通费用计算器不变。

关键边界：声明免费；首笔实际付款才锁一次场次机会；零支付不锁；取消须未锁且版本一致；最多3次包括声明前当轮常规用量；旧圈记录不会被跨线/冷却刷新。返还仅原窗口收据，早期旧热/已冷却不补替。更换热池/牌组失效；预测不改真实资源/集合；回调抛错也关窗。实际位移重复计算保持已付收益且不再扣费。

## 原生证据

| 验证 | 结果 |
|---|---|
| 模块 | tech/drivers/cards-heat/core/tracks-weather-pit/gears/ui/tutorial |
| 最终RunFinished |2990总/2990过/0失败/0跳过/0不确定，19.5448375秒，selectionMatches=true |
| 首轮 |2990总/2988过/2失败/0跳过，18.717076秒 |
| 新运行时案例 |29，GermanBurstRuntimeTests登记tech |
| 新MVP适配参数 |5，既有RacePitTurnBoundaryTests归属tracks-weather-pit |
| 实际GameLoop |3总/3过/0失败/0跳过，3.3656232秒，native root Passed |
| runtime MVID |1355eddf-9d2a-4dae-8a1d-19ecc171d10e |
| final editor MVID |a0cab66d-67cb-488c-aee8-cb227b684d94 |
| 原生结果 |D:/UnityProject/Foodula1/Library/Foodula1/RegressionRuns/20261026T232604698Z-6cec6093/result.json |
| 新截图绝对路径 |无。本包无HUD布局/文案改动，未宣称视觉验收。 |

首轮两个旧断言要求重复ComputeMovements后已付燃料位移变0，修为保留+2且弃区仍仅一张付款热；生产代码未改。修订后新editor加载再跑。GameLoop为3条既有Mansell真实协程（本人输入、费用、A2冷却），在测试预期修订前跑于同一最终runtime；不算德国手动GameLoop或全量/完整模式通过。完整原件、first/final result及native GameLoop root在D:/UnityProject/Foodula1/Library/AutomationReview/tech-driver-I03b-20261027；本目录native-summary.json是摘要。

独立引擎和QA最终静态复审均有界通过，修过池身份未冻结及技能回调异常未关窗两项，父任务执行原生验证。一次直接诊断查询调用不存在的IsTestRunActive API导致CodeDOM查询失败，改查询后成功；未改项目程序集或用旧程序集测试。

## 安全与剩余

最终MainMenu及全部loaded scene干净，未播放/编译/导入/测试；许可日志Successfully resolved entitlement details。Console error1为预期no_such_track负面夹具，warning1为UI回归后CardHandUI未设cardPrefab，未清Console；无项目编译错误。editor/state为stale_status，保留直接查询与native RunFinished，不称新鲜ready。

手动L1/L3 HUD、固定费用请求计划/版本复核、费用/次数/禁用/取消/焦点提示、实际手动完整GameLoop、三尺寸改前后截图、六队借用和完整模式/存档仍待后续。当前声明API并非玩家已可点击；ManualPathThisTurn以外仍有旧自动L1/L3兼容，下一I03c在手动入口可用时迁移，不能提前标德国实装完成。完整终态/进站/跳过比赛仍待验。

未动Word原件、场景、排除文件和既有脏改；未commit/push/reset/checkout/rebase/删除移动/安装改系统或快速播放设置。缺失active.md/治理文件沿记录未建占位。Git状态/diff及文件SHA256前后保存在本包Library目录。
