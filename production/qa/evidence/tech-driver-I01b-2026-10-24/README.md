# I01b 哈特狩猎本能（2026-10-24）

实际接线：RaceSession 的本人驾驶输入带赛道长度/起终点索引，锁定最近前车原实例+ID；范围按被动等级3/4/5格。仅人类且Enabled的哈特，不启用普通AI或教程。RaceSettledSlipstreamResolution 先统一算全体原生链，再冻结对锁定车的正距离追近事实；查询和预测不消费。MVPGameManager 在两段原生移动完成后使用 TurnMovement.ActualCells 的真实遍历差值，另走1格且不再次找车；额外1格实际完成才消费输入开始圈的额度。原生Steps及2次上限不改，追平/超过算追近、同格尾流不算。实际计划/位移账本与中文日志单独标“车手：狩猎本能”。

生命周期：每回合/中断/初始化/结束/退出/重赛清锁；新运行态复制独立。过期receipt不取消新输入；同ID新实例不能接管目标。相对非0起终点的距离用于锁定，原生收据不混地标额外移动；本次驾驶中新登记的下一回合进站不当作已停站，真正停站输入不会打开驾驶窗口。原ID/XP/主动次数和支付规则不改。

独立验证：Unity自带Roslyn编译生产和Editor测试程序集exit0，1.3886653s/1.7039298s；响应文件副本显式加入2个新源码路径。新HunterSlipstreamTests36例与旧HunterSkillActivationPlanTests53例，在新编译生产程序集上由Unity捆绑Mono执行89/89，失败0/跳过0，0.1381324s。见final-pure-result.json和可复现runner源文件IndependentPureRunner.cs。这是生产C#执行，**不是Unity原生Test Runner、原生RunFinished或设计模型**。位置推进及实际格数由夹具注入，不能替代Manager真实移动协程。

保留失败：首次选择3夹具123例，122过/1失败/0跳过，0.1554573s。DriverSkillRuntimeTests.AuthoredRaceCanvasExposesDriverSkillButtonAndLabel需要Unity AssetDatabase，独立Mono无法调用；不是C#规则失败或Unity安装损坏，不修改其断言，不把它当跳过/通过。最终只选择适合独立环境的2夹具89例。独立runner首次编译缺少System引用，已补Unity捆绑Mono库引用；正式两程序集编译原本通过。

静态复审：unity-specialist APPROVED、无必须修复；qa-tester APPROVED WITH SUGGESTIONS，无必须修复，不代表原生或视觉。已修三处初审问题：非0SF的progress；地标额外移动污染nativeEnd；旧receipt的finally关闭新输入。三份当前memory全文已由独立审查分块读完，相关ADR/GDD/Word UK与Hart/P4/P5复核，Word哈希保持。active.md及5治理文件缺失只记录。

边界：Unity MCP工具不可用、旧8080服务拒绝连接；没有安全实时scene/isDirty/play/compile/import/ready/Console查询，未激活前台Unity或打断用户应用。原生0项，耗时无；未确认Unity加载新程序集。没有新运行UI截图，当前文案及反馈三尺寸仍未验收。I01c两项UI回归、费用模态初始焦点、实际A2/A4、实际尾流协程、教程/生涯/12车完整赛、存档兼容仍需相应原生包。旧driver benchmark单独模拟链尚未接本被动，不拿其结果宣称平衡。I01仍进行中，I02-I13不提前标完成。

下一有界包I01d：安全恢复编辑器验证通道，确认新程序集后运行drivers/tech/core/tracks-weather-pit及I01c ui/cards-heat；费用模态焦点隔离修复与三尺寸证据。所有既有修改与未跟踪文件保留，未提交推送、未删移/安装/修改系统设置。
