# I04b1 舒马赫三段战术运行态与管理器API

### 2026-10-29 I04b1：舒马赫三段战术运行态与生产API（真实按钮待I04b2）

已接入准备段弃1张原普通速度牌、限1/2挡；之后两个连续本人输入限3/4挡，逐次实付1张永久引擎热到手牌，移动+2/+3/+3，准备时仅扣一次场次（仍1/1/2）。计划绑定session/参赛车/本人输入/回合/版本与牌区快照；取消、过期不扣费，未付执行或缺牌/低挡/进站/跳过/终态/异常/退出丢弃剩余，已付费用与次数不返还。独立Strategy时钟不推进旧PerfectLap主动时钟；进入ManualPath后旧免弯费/高阶尾流保持禁用。新增奖励尾段仍查新弯心并付普通费用；预测复制重绑定不改真实牌值/热池/收据。DE L3记录/回收实际固定手牌原收据，不回收声明前旧热，也不重置黑啤预算。

新程序集runtime e6d6e80f-4853-4179-b3cb-8501d59619d0；11模块editor79888f06-3448-49ab-87ba-fad14881200e。新增53规则+33管理器案例，原生11模块3570/3570，失败/跳过/不确定0，33.0757789秒，selectionMatches=true。另实际GameLoop最终6/6，失败/跳过0，6.7724629秒；新增1案例内部等级3/5两场，各准备→两次执行→第4输入，另5条既有DE/D03被动/Mansell流程。首轮6条为5过1失败：新夹具遗漏关闭德国底盘直道+2，准备期预计1实为3；只修夹具显式关闭底盘、保留原断言，新editor612a0e6a-1efe-44cb-b3ef-07a656e2affa后6条全过。未变生产/3570案例不重复全跑。独立引擎静态终审无阻断，不冒充执行证据。

实际按钮、选择/逐次付热确认、主动摘要尚未迁移，API可用不等于玩家已可操作；本包未改UI、无新截图，三尺寸真实交互留I04b2，既有I04a图片不算本轮验收。最终MainMenu全部loaded scene干净，未播放/编译/导入/测试，Console0错误/0警告未清，许可/ready正常。8 Word/32场景、4既有排除文件diff及原78个未跟踪条目保留；未提交/推送/清理或改快速播放。I04仍进行中，下一I04b2真实UI，再I04c维特尔；全模式/存档组合仍I13。证据为本目录。

## 本轮文件与结果

- 生产：新增SchumacherStrategyRuntime/SchumacherStrategyActivationPlan，复用既有CardDeck、RaceSession、DriverSkillRules/Runtime、MVPGameManager、Preview、Movement与Cleanup边界；无场景或玩家按钮布局变更。
- 原生模块完整结果：`native-modules-result.json`，原始绝对路径 `D:/UnityProject/Foodula1/Library/Foodula1/RegressionRuns/20261028T172407289Z-b0a8f88f/result.json`。
- 真实GameLoop结果与每条耗时：`gameloop-job-final.json`；权威原生结束：`unity-final.json` 内nativeGameLoop。首轮失败及编译证据原样保留。
- 截图：本包0张；实际按钮、确认与三尺寸前后交互尚未接入/运行，不能借用上包截图验收。
- 缺失的active.md/control-manifest/technical-preferences只记录，不建占位。Word SHA未变，未回写反馈/Word。
- 运行证据为合成赛道+受控生产适配器/原生GameLoop，非真实硬件输入、长赛、全模式与存档兼容总验收。未顺带启用普通AI技能。

独立引擎静态终审检查独立时钟、版本化取消、挡位封存、预测重绑定、尾段弯费与cleanup，未发现阻断；审查者未运行Unity，编译/运行通过采用本轮原生记录。

末尾格式复核仅移除两份新增测试各1个EOF空白行，字节还原校验确认无执行内容变化；runtime保持e6d6e80f-4853-4179-b3cb-8501d59619d0，后续editor3850947d-3485-430b-a498-fb03d3ef8730已实际编译/加载，Console0/0；不重复测试、不新增通过计数，上述3570与6条仍对应各自记录的测试程序集。
