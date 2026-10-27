# I01e 哈特新增位移弯心与真实协程（UTC 2026-10-24）

Word 车手草案共用边界（段52，43–53相邻上下文）要求新增格数仍执行新经过弯心的原费用、失控及终态门控。现生产曾让主动新增尾端/被动1格沿旧额外移动入口绕过角判；本包修复这条接线，不改锁定参数。两草案SHA未变，旧P4/P5研究未批准字样不阻断生产授权。

实际生产：RaceMovementRules计算Hart主动反事实尾端max(cardSpeed,total-HartBonus)..total；不提高cornerTotalThisTurn、不接管其他奖励原先已能到达的角。TrackRules临时物理段key去重，跨圈同编号是新一次经过；这不是存档/全input集合。原card集合及新增角同一次RaceCornerResolver，以原速度、EffectiveCornerLimit、ResolveOverspeedHeatCost、TryPayHeat普通部分支付/失控/回退处理。预测传实际nodes，同算法只支付副本，未知原生尾流不凭空预测被动+1。

最终冲线：仅有Hart新增弯心的base协程，在本次局部closure暂记最终越线，角费失败不写finishOrder；成功后才沿原RegisterStartFinishCrossing登记。普通/非最终跨线照旧。停止协程无持久待提交状态，不在finally自动完赛。原AnimateMovement2参、AnimateMovementByAmount4参、TraverseMovementNodes4参反射入口保留，带gate实现用不同名，未改原用例断言。新主动角费失败/terminal仅阻止本笔后续地标与进站登记，保留其他冷却/教程等待/弃牌与全局生命周期。

被动：真实两原生尾流仍最多两段，单独1格不再找车。实际1格先消费原圈额度，再对这新格的apex按原角速收费；不足仍部分付热→spin，回退点为这1格起点，不退款额度，失败后不发地标/pit。原生尾流自身与其他角色不额外收费。

新增16个原生Runner案例：movement范围4、track去重/终点6、预测2、native协程4（其中主动final/nonfinal×热6/1四子场景，被动热6/1两子场景，中断重赛gate一个，A2/A4一个）。所有归属沿用既有fixtures：core的RaceMovementRulesTests/RaceTurnPreviewTests、tracks-weather-pit的TrackRulesTests、long-races的RaceGameLoopCoroutineTests；没有新fixture，无需新增/重分配RegressionModules.json条目。发现与选中检查无漂移。

第一原生运行first-native-result.json：2168/2168，失败/跳过/不确定0，285.8493526s；包含19个long-races案例，全部自然完成，未强停。此后审查发现失控仍按原计划端点预约进站，增加范围限定门控及扩展真实pit/中断用例。第一结果不能冒充最后版本19长流程通过。

最后原生运行final-native-result.json：2154/2154、失败/跳过/不确定0，10.3944555s，selectionMatches=true、missing/unexpected空。选中tech/drivers/cards-heat/core/tracks-weather-pit所有fixtures，加long-races中4新用例及既有六队两回合精确5项，没再重跑其余无变化长流程。精确选择脚本run-final-selection.cs保留：只在Library运行，复用原ModularRegressionRunner原生RunFinished回调/SessionState跨重载证据；未改项目Runner或Catalog算法。plan.modules含long-races是归属标签，longSelected=5，不能据此称最后跑了整个long-races模块。

编译/证据：最初Unity编译3条CS0246(Action缺using)已改全限定System.Action，记录initial-compile-errors.json；未带错误运行旧程序集。随后隔离Roslyn生产/Editor exit0（1.0736901/1.3203751s，初稿非最后编译）。新文件只请求script编译时尚未导入，反射newTest=false即拒绝启动测试；scope=all导入后true才运行。最后runtime MVID fd9e07a1-6993-4142-b257-2dabd92808df、Editor eb04cd14-4479-4b92-8ce2-43c55f6a344f，真实新程序集已加载。旧会话404后重新MCP initialize，没有重启服务/改配置/装包；直接实时查idle/所有loaded scene无dirty，无用户播放。

最后MainMenu干净、未播放/编译/导入/native/module占用、Personal许可证。Console只有TrackDataLoader no_such_track已由LogAssert.Expect声明的负面测试error；未清Console。editor/state资源仍stale_status，不能说ready=true；实时直接查询/原生EnsureSafe/RunFinished单独保留，见unity-postflight.json。

验收边界：主动用例是真实GameLoop→A1/A4→移动→反应→新角费→finish/pit；被动只在真实原生调度下执行生产尾流结算阶段，不是完整回合/真实美术车。中断只验证局部finish-gate非完整Hart退出流程。A2/A4真实循环证明早餐后冻结永久热、主场回2不撤早餐临时热、正式提交关窗。没有新UI几何或截图；新角费风险提示尚未写入实际确认窗，真实改前后1920×1080/2560×1440/1280×720鼠标/键盘/拖动仍待I01f，不把旧图当新验收。全角色/全模式/新存档组合仍I13，不据已有长流程宣称新技能全模式完成。

下一有限I01f：把新增弯心热风险接入真实Hart确认窗，实际改前后截图和操作；通过UK有界操作后进入I02曼森，不重复已过来源审计或无变化长流程。I01仍in_progress。两草案/原反馈不覆盖，InitTestScene、字体、排除音频及项目settings保持基线；缺active.md和治理文件仅记录。无commit/push/reset/checkout、删移文件或系统改动。源哈希及preservation.json记录本包实际差异。
