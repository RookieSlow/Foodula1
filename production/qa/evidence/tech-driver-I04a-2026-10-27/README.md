# I04a 舒马赫被动付款调校


### 2026-10-27 I04a：舒马赫真实弯道付款调校（主动/维特尔仍待）

工程师调校已替换旧定时通用减费：一级同本人输入实际普通弯道永久热付满2、二三级满1，准备下一次普通弯道费-1；仅下1/1/2个合格本人输入的首笔正费用有效。每圈最多授予一次、旧标记不叠/不续，同一输入不可兑现。新费用来源枚举仅由ResolveCornerSet传入，按实际Drawn的新永久原收据去重，重复回调/旧前缀/临时热不积累。普通罚热部分支付/失控保留，免到零无付款收据；换挡、黑啤、固定车手费均不触发或消耗调校。普通AI车手技能未启用。

被动有独立本人输入计数，全球轮、其他车输入不扣期限；未顺带迁移PerfectLap旧主动时钟/免角费。真实失控先清、scheduled进站/跳过/终态/退出清理；仅request未scheduled不误禁用。正常cleanup保留未来标记，重开初始化清空；深复制三套集合并给预测显式重绑定，补pitStopScheduled复制，预测不改真实牌值/热池/标记或每圈记录。

最终实际新runtime dec425bf-2fe1-41d6-8ebc-7936e91615a1、editor9da3c2ed-2d23-466f-9492-c89d2dd74ae7。11模块原生 **3484/3484，失败0/跳过0/不确定0，40.2925337秒，selectionMatches=true**；新增42规则+24真实适配器+3车手页参数。另原生真实GameLoop **3/3，失败0/跳过0，4.2840327秒**：新增舒马赫三输入实际过弯付2→下输入付1、旧标记一次兑现/同圈不再授予，以及2条已有德国声明流程；非完整长赛/全量。新增partial沿既有long-races，其他新夹具登记drivers/ui。

首轮3472中3469过/3失败，40.2716347秒，均为新摘要在三尺寸溢出；仅D03被动文字启用12..14字号适配后新程序集全部通过。独立引擎审查指出pit预测copy遗漏后已修并加2例；复审代理随后额度受限，最终父任务核查/原生验证，不冒称独立全审。主菜单车手页的被动摘要已更新，旧主动摘要仍真实反映未迁移行为。

受控生产DriverSelectionUI在1920×1080、2560×1440、1280×720改前/后各3全帧实际查看；新D03被动overflow前后0，每尺寸全页既有14条其他文字/姓名overflow保留，不算全页可读验收；UK拥挤与姓名重叠未扩修。真实Raycaster选12车手/六队、Submit、遮罩、返回均通过；正交自有Canvas不算实际菜单启动/硬件/完整比赛模式，720p舒适度仍I13。前绝对目录 D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04a-2026-10-27/before/run-20261027-100904358；后 D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04a-2026-10-27/after/run-20261027-152526369。

最终MainMenu干净、未播放/编译/导入/测试，Console0错误/0警告未清，许可正常；ready记录见证据。8 Word/32场景及既有排除文件保留，未改快速播放设置。I04仍进行中，下一I04b舒马赫弃牌低挡准备/两段逐付热主动，再接维特尔；完整模式/存档仍I13。用户随后明确授权“将改动提交到github”，按相关范围核对并提交/推送，其他钩子/音频暂存/字体缓存/编辑器设置不纳入。证据 production/qa/evidence/tech-driver-I04a-2026-10-27/README.md。

原生完整结果绝对路径：D:/UnityProject/Foodula1/Library/Foodula1/RegressionRuns/20261027T152340797Z-5e0d4362/result.json。源状态/编译/原始结果与本地提交前审计：D:/UnityProject/Foodula1/Library/AutomationReview/tech-driver-I04a-20261027。

提交前补记：仅清除新增.meta空字段尾空格、uk_prep_meal_test.cs末尾多余空行，并为原始增量diff添加既有保字节策略。生产runtime保持dec425bf-2fe1-41d6-8ebc-7936e91615a1；新editor b60d7ab3-e318-4d64-afcd-6bd9f7073c66已编译。额外UkPrepMealTests复检启动后Unity正常退出，未取得完成结果，计入通过数0；此前3484/3484及GameLoop3/3原生结果有效。最后MainMenu干净、Console0/0，随后编辑器关闭，不自动重启。用户本轮授权将相关改动提交GitHub；本地钩子/音频暂存/字体缓存/EditorSettings和全部场景排除。