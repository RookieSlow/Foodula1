# I03e 德国有限爆发：声明/取消、实际收据收尾与GameLoop

本包生产接入：A1免费声明与车手技能并排，A4未付可取消、首笔实付锁场次；三次黑啤合计预算；取消后无旧自动L3返还；AI挡位前声明，保留一次燃料策略。原实例收尾区分净回/容量退役，只有净回播放回引擎动画。没有改变ID/XP/主动次数或启用普通AI车手技能。

|证据|结果|
|---|---|
|最终原生10模块|3403总/3403过/0失败/0跳过/0不确定；43.9000283秒；selectionMatches=true|
|新增用例|48 UI参数 +8 Manager +2真实GameLoop；修改既有cleanup边界前置|
|实际GameLoop|2/2，0失败/跳过，2.9225509秒；原生RunFinished Passed|
|runtime MVID|04d6d765-0f64-4a05-b432-1a7ab98409f2|
|editor MVID|4a2d3c31-aa91-4d25-915e-b439e65de2e0|
|原生完整结果|D:/UnityProject/Foodula1/Library/Foodula1/RegressionRuns/20261027T094200890Z-add03d0e/result.json|
|原始编译/状态/审计/失败|D:/UnityProject/Foodula1/Library/AutomationReview/tech-driver-I03e-20261027|
|改前截图|D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I03e-2026-10-27/before/run-20261027-043617806|
|改后截图|D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I03e-2026-10-27/after/run-20261027-094346842|

真实两回合GameLoop覆盖：手动声明→G1→三次实付黑啤→正式速度牌→移动→反应/弃牌→下一轮，旧H热保留、窗口原收据清掉、次数已消耗；AI先声明；另比较声明与取消后真实缺牌罚热和下一轮引擎差异。仅有界两回合，不当完整长赛/全模式或全量。十模块包含tech/drivers/cards-heat/core/gears/ui/tutorial/tracks-weather-pit/career/ai，未选long-races全部。

首次原生3403中3378过、25失败，42.5208608秒。生产修复为在button.interactable=false前保存选中焦点；夹具修复为真实声明前置，及ReactStep普通冷却后只计算尚存收据。修复后定向56中40过16失败，均透视RT检测：WorldToScreenPoint中心和ScreenPointToLocalPoint回到(0,0)，native RectangleContainsScreenPoint却false，ForceUpdateRectTransforms/保持activeRT均未解决；仅本轮拥有的摄影机改正交，不改共享CareerUiPointerTestScope或生产摄影机/半槽几何，真实GraphicRaycaster/顶层接收者断言保留。最后新程序集十模块全部通过；不拿中间/旧程序集冒充最终。

1920×1080、2560×1440、1280×720前24/后24均已实际查看动作或模态/状态区域；前为capture代理，后父任务另查看4张全帧。覆盖两德国现有driver、长中文数字、六队周边身份、2/4/6/12 roster、Move/Submit/实际pointer、未付A4取消与焦点回退、待确认遮罩和直接回调拒绝、首付锁定、三次耗尽。0正文overflow。截图是生产HUD/MVP置于自有Canvas，非完整RaceCanvas/物理键盘；周边六队不等于六队技能实现。

**图片边界**：前A1白色自有背景配旧白字，旧driver可读性不接受；后自有背景仅应用既有生产样式。前透视、后正交，不能称同摄影机像素比较。terminal/exit截图在清理前（All为付款确认框），manifest资源字段在清理后；不得当作终态/退出后UI截图。before的engineAfter=0是未填字段。净回/退役日志有生产CleanupTurn实际功能断言，但这些截图无logText，不声称视觉验收。既有拖动回归通过不代表新控件硬件拖动验收。720p可读但细小，主观舒适度仍I13。

独立engine只读有界批准；QA发现截图时点问题并记录边界。最终MainMenu/all loaded scenes干净、停止播放/编译/导入/测试；许可已解析，Console0错误/0警告未主动清，ready=true。模块阶段有预期no_such_track及夹具cardPrefab日志，原件保留。8份Word/32场景SHA未变，git diff --check通过，全部既有脏改保留；缺active.md/治理继续记录，无占位。无提交/推送/清理/安装/系统或快速播放设置变化。

I03规则、接线、受控玩家操作及两回合自动验证有界通过，指针进入I04舒马赫/维特尔。完整RaceCanvas、硬件输入、完整比赛/进站终态/模式/存档组合、返热日志动画视觉及720p舒适度仍待I13，未宣称整项已完成。
