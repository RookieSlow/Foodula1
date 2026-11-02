# I04c3a — 维特尔真实来源按钮源码，Unity验收待续

源码接线：维特尔“人车合拍”真实Manager/HUD按钮始终路由固定来源计划，失败也不回落旧免费发动；选择真实VettelNativeSource枚举与1永久引擎热→手牌费用合并为一个必需模态，关闭额外确认开关仍须选择来源。确认先捕获枚举再清行；取消/过期/生命周期不扣费，原卡牌/续段选择保留。新增当前/下次/本次已用与本人窗口/场次文案，玩家名转义TMP标签；旧跨挡免热和通用免费发动已从源码移除，ID/XP/解锁/次数不变。

## 实际验证

本轮新源码使用项目C#9/Unity引用，在Library隔离编译运行时233源和编辑器214源，两者0编译错误；未写入Library/ScriptAssemblies或让Unity加载。新程序集独立Mono/NUnit：维特尔纯规则64/64，0.0636094s；补充Rules/Runtime51例50过1环境失败，0.0569657s，该失败调用UnityEditor.AssetDatabase读取场景，宿主无Unity原生环境。两组不重叠，共115执行/114过/1环境失败/0跳过，保留原XML，不冒充Unity模块通过。

- 离线runtime MVID：e5145345-a280-4a10-a75a-56d5ad2f2ab3；editor：552f101a-d0d7-40aa-aea0-d19c7d343059。独立NUnit宿主确实加载这两份新DLL，日志/结果XML在verification。
- 独立纯规则64过/0失败/0跳过，0.0636094s；补充51例50过/1环境失败/0跳过，0.0569657s。唯一失败DriverSkillRuntimeTests.AuthoredRaceCanvasExposesDriverSkillButtonAndLabel：UnityEditor.AssetDatabase.LoadAssetAtPath无独立宿主原生internal call。不是项目安装损坏，也没有安装/系统变更；不重试或静默将失败记跳过。
- 独立宿主最初两次编译因Unity附带旧NUnit没有TestMessage/TotalCount API失败，仅修Library宿主，无执行测试；最终宿主编译成功。生产runtime/editor离线编译始终0错误。
- 新51项UI/profile实际执行0。已有GameLoop第一案例仅改真实按钮+确认入口，仍未运行；其它真实流程不冒充本轮通过。

## 实装分层

DriverSkillCardChoiceUI新增typed source列表/nullable选择，不用假CardData，过滤无效枚举与重复值，空列表不能确认；沿原布局、分页、显式导航、取消和清理。HUD先捕获源枚举再Clear，沿单一确认面板派发。Manager在HUD和自身两个入口优先消费D04，确认绑定原plan，陈旧回调不收费。Runtime新增只读RemainingInputs及current/next窗口文案；DriverSkillRuntime通用D04免费激活始终拒绝，旧GearHeatCost免热分支已移除。正式车手页主动名/摘要更新，仅D04主动沿12..14autosize。ID/XP键/解锁等级/主动1/1/2保持。

## 未完成验证

Unity工具已不可用，本轮没有操作编辑器、没有验证当前scene/isDirty/播放/编译导入/许可/Console/ready，新程序集实际Unity加载未确认。新增48HUD+3profile案例已编译并登记ui，实际执行0；已有两输入真实GameLoop改为Manager实际技能按钮+HUD来源确认，未在新源码上执行。三尺寸改后截图0，10月29日C3改前HUD/profile共6帧为历史前置、不能充当改后验收；玩家可操作、完整模式、存档与硬件均不标通过。

C3改前绝对目录（10月29日、修改前真实捕获并查看）：

- D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04c3-2026-10-29/before/hud-20261029-044246962
- D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04c3-2026-10-29/before/profile

上述各3帧；11月2日前HUD/row源SHA与该基线一致，保留其前置意义。没有改后图片，新增UI的文字适配、长中文数字、6队/2/4/6/12名参赛者、实际射线/Submit/取消/失效/焦点/遮罩/模态穿透待原生夹具验收。旧14项全页溢出仍未验收，未扩修。

## 审查与保存

原UI/QA代理达到额度，仅前置夹具完成；根承担余下实现/测试。独立引擎专员最终静态无阻断，建议转义玩家名并区分下次待触发，两项均已采用。独立审查未执行Unity/视觉，不冒称其验收通过。8Word/32场景/4排除文件及原未跟踪SHA保留，status/diff、当前源码与清单见归档。未提交/推送/清理、未安装或改系统与快速播放设置。缺失active.md/治理引用仅记录未创建占位。

下一有界包：I04c3b：Unity工具可用后先核验场景/播放/编译导入/许可/Console/ready，再让最终新程序集实际加载，运行tech/drivers及依赖、51项真实UI检查与已升级的GameLoop；实际查看三尺寸改后HUD/profile及输入/取消/焦点/遮罩。当前不推进I05，完整模式/存档/硬件仍I13。
