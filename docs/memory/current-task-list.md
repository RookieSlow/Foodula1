# Current Task List

## 2026-09-01 正式赛道现实对照校准

- [x] 以 `4+3+3+2 = 12` 格为完整大冲刺门槛，并按现实主直道、中距离直道和短连接段重新审计八图。
- [x] 银石、蒙扎、纽博格林 GP 的格数分别调整为 `77/63/59`；印第按两条长直道与两条短槽重分配，
  铃鹿按官方约 1km 西直道与约 800m 主直道修正为 `12/10` 格。
- [x] 上赛出阴阳高速假弯、银石 Abbey/Copse、蒙扎 Curva Grande、铃鹿 200R/130R 等全油弯升至
  限速 6；近全油弯按 5、常规快弯按 4，保留发卡与重刹弯 2–3。
- [x] 保持实际里程、圈数、线路形状、弯道数量和弯心不变，并新增现实比例、假弯限速和疏密回归。

## 2026-08-31 生涯模式

- [x] 建立 `CareerModeRules` 纯规则基础：复用 `TrackSelectionState` 的唯一八站正式赛历，排除
  `fallback_42` 与教程专用规则，并在目录数量、空 ID 或重复 ID 时立即拒绝初始化。
- [x] 支持六支车队任选其一开档；四车阵容必须包含玩家且车队不重复，确认后规则层锁定玩家车队，
  直接调用也不能在本轮生涯中换队。
- [x] 集中实现四车 `10/6/4/2` 积分、DNF 零分及总排名同分规则：积分、胜场、领奖台、最佳名次、
  最近名次、稳定阵容顺序。
- [x] 赛果使用唯一 `ResultId` 幂等推进；错误赛道、重复回调、非法名次或阵容不改变生涯状态。
- [x] 第 4 站结算后进入唯一 `SummerBreak` 并阻止第 5 站，科技确认后消费机会；第 8 站进入
  `Completed`，不能开始第 9 站或再次调整科技。
- [x] 新增生涯规则 NUnit 回归并真实执行 `16/16`；MSBuild 编译运行时与 Editor 测试程序集成功，
  0 编译错误，保留已有 Unity/MCP 程序集版本冲突警告。完整 Unity Test Runner 尚未在本轮执行。
- [x] 新增 `design/gdd/foodula-1-career-mode.md` 并同步 systems index、roadmap 与连续性文档。
- [x] 新增 `CareerSaveData`/`CareerSaveCodec`：独立 schema 与赛历版本、完整八站 ID、车队/阵容、
  逐站结果、积分榜校验、阶段，以及初始/夏休后科技快照均可重建；不保存单场一次性科技状态。
- [x] 加载通过正常规则重放赛果并核对积分/阶段，拒绝版本漂移、赛历变化、积分篡改和非法数据；
  缺失/损坏返回安全空状态，不读写自由赛事科技、RP、车手、教程或设置键。
- [x] 新增独立 `Foodula1.Career.V1` 仓储与 PlayerPrefs/JsonUtility 适配器；损坏原值在玩家明确
  覆盖或放弃前保留，避免静默删除进度；运行时以当前科技数据库拒绝未知或错队节点，并可在
  生涯快照与新的 `TechTreeState` 间转换，不复用自由赛事的可变实例。
- [x] 新增 `CareerModeService`：已有有效或损坏存档都要求确认后才能新建，放弃同样要求确认；
  赛果与夏休调整在规则重建副本上执行，仅保存成功后替换运行态，失败不推进。
- [x] 新增持久化/服务回归 10 项；生涯规则与持久化定向合计 `26/26` 通过，MSBuild 0 编译错误，
  仅保留已有 Unity/MCP 程序集版本冲突警告。完整 Unity Test Runner 仍未在本轮执行。
- [x] 主菜单原比赛入口显示为“自由赛事”且原选图回调不变；新增独立生涯入口、六队确认、继续总览、
  新建/覆盖/放弃二次确认、八站赛历和规则计算积分榜，所有写入只通过 `CareerModeService`。
- [x] 生涯 UI 未复用自由赛事的赛道选择状态或可变科技实例；创建时把选定车队当前科技配置复制为
  生涯自有快照。尚未接通的比赛启动与夏休科技编辑在界面中明确禁用/说明，不伪造完成状态。
- [x] 新增展示层回归 5 项；生涯规则、持久化与展示定向合计 `31/31` 通过；包含全部新增文件的
  MSBuild 运行时与 Editor 程序集 0 编译错误，仅保留既有 Unity/MCP 程序集版本警告。Unity
  编辑器因窗口权限级别无法由自动化触发刷新，Play Mode 视觉验收仍待后续安全执行。
- [x] 新增只读 `CareerRaceLaunchRequest` 与统一模式解析优先级：教程 > 生涯 > 自由赛事；Race
  按权威存档注入指定赛道、锁定玩家车队、稳定 AI 阵容和克隆科技快照，且赛道背景使用同一解析入口。
- [x] 生涯赛果从实际加载赛道和四车运行态映射连续完赛名次及 DNF；结算前重载权威存档，陈旧回调、
  回退赛道、阵容变化或保存失败均不推进。生涯结算跳过自由赛事 RP 与车手 XP 写入。
- [x] 新增 Race 集成回归 7 项；生涯定向合计 `38/38` 通过。包含新集成源文件与测试的完整
  `Assembly-CSharp-Editor` 重建为 0 编译错误，仅保留既有 Unity/MCP 程序集版本警告。
- [x] 接入第 4 站后的夏休科技树：只展示锁定车队，从 `ActiveTechSnapshot` 创建独立草稿；研发、
  RP 扣除和激活切换不触碰普通 `TechTreeProfileStore`，取消直接丢弃，确认后经服务原子保存并开放第 5 站。
- [x] 新增夏休草稿/门控回归 6 项，覆盖快照隔离、RP 解锁、跨队节点拒绝、UK 复制目标保留、
  一次性确认及保存失败不消费；生涯定向合计 `44/44` 通过，完整程序集重建 0 编译错误。
- [x] 第 8 站完成态显示积分榜第一名总冠军、玩家总分与最终排名；主操作可进入新一轮车队选择，
  最终覆盖仍必须经过既有明确确认。RaceTestLog 新增 `[CAREER_RESULT]` 成功/拒绝结构化行。
- [x] 新增完成日志回归 2 项并扩展完成页断言；生涯定向合计 `46/46` 通过，Unity 生成工程已包含
  新源文件，完整程序集重建 0 编译错误，仅保留既有 Unity/MCP 程序集版本警告。
- [ ] 后续：完成自由赛事、教程、生涯创建→八站→夏休→完成→新一轮的安全 Play Mode 端到端及
  分辨率/交互验收。本轮未把 Play Mode 计为通过。

## 2026-08-27 新手教程、设置与游戏百科

- [x] 建立 `tutorial_le_mans_uk_v1`：固定 UK、勒芒、零科技/车手增益、零正常奖励与进度写入。
- [x] 为 `CardDeck` 增加显式精确顺序模式；固定起手、后续抽牌及弃牌洗回不依赖随机种子，正常比赛随机路径不变。
- [x] 建立 16 步纯教程状态机，覆盖错误门控、跳过、重播、练习转换/重启/完成/退出及 `[TUTORIAL]` 日志。
- [x] 建模脚本天气与 2 格尾流教学领航车 cue；`TutorialRuntimeDirector` 将 cue 作为一次性
  检查点交给 Race 适配层，天气直接写入现有 `RaceSession`，领航车位置在正常回合末尾流
  判定前才应用，未修改普通比赛尾流规则。
- [x] 主菜单教程入口、隔离启动请求和 Race 运行时接线：固定 UK/勒芒/6 热量、精确玩家与
  教学对手牌组、零科技/车型被动、零 RP/XP 结算，并保持普通 Quick Race 选择不被覆盖。
- [x] 教程定向 EditMode `16/16`、全量 `487/487` 通过；此前 Play Mode 冒烟验证教程精确起手
  `[1,2,2,3,4,1,uk-scone]`，普通 Monza 比赛仍为随机牌组/科技/车型增益路径，两次 Console 均 0 错误/警告。
- [x] 新增运行时引导面板与实际操作门控：阅读步骤可点继续，回合、满额出牌、移动、支付/冷却热量、
  缺牌、失控、尾流、维修区及两张 UK 特殊牌由真实比赛事件推进；RaceTestLog 记录步骤与 cue。
- [x] 引导结束或跳过后重建完整 `Practice` 会话：圈数/位置/精确牌组/热量/对手归零，应用阴天，
  一圈完成后立即反馈；重新开始、重播引导、退出和现有 Reset 按钮均按相位执行并写教程日志。
- [x] 主菜单设置入口与 `Foodula1.Settings.V1` 已接入：三路音量诚实持久化为音频预留，
  全屏/窗口、分辨率、动画速度/减少动态真实应用；教程完成偏好可重置且与正常存档隔离。
- [x] 设置 6 项、教程 16 项联合定向 `22/22`，全量 EditMode `493/493` 通过，0 失败、0 跳过。
- [x] 新增 17 条版本化 JSON 游戏百科、目录解析/完整性校验与设置内滚动阅读器；必需主题、
  ID 唯一、12 张特技牌、12 名车手和 5 种天气运行时追踪定向 `6/6`，全量 EditMode
  `499/499` 通过，0 失败、0 跳过。
- [x] 新增 8 个逐机制玩家检查点，在挡位输入门/下一回合边界精确重建牌序、手牌/弃牌热量、
  引擎、位置和挡位并清除失控/维修残留；缺牌、打转、司康和红茶均有确定性前置状态。
- [x] 勒芒正式 JSON 无维修区时，教程以配置的 132/4 入口/出口生成独立规则视图并复用正常
  `PitLaneRules`；正式节点未修改。教程定向 `20/20`、全量 EditMode `503/503` 通过。
- [x] 教程体验优化：16 步采用人工修订后的渐进、鼓励式短句；“轮到你了”保持核心操作，
  “现在场上 / 完成后 / 没反应？”改为可选段落，空字段不显示标签、空行或顶部泛化反馈。
- [x] 为每步增加语义化高光目标和独立机制短说明；13 类目标覆盖状态、挡位、手牌、牌区、引擎、
  赛道、天气、维修选择及两张 UK 卡。四块无遮拦遮罩形成聚光窗口，减少动态时边框静止。
- [x] 渐进式指引与高光回归：教程定向 EditMode `26/26`、全量 `509/509` 通过，0 失败、0 跳过；
  编译成功，受控 Play Mode 因持续停在域切换状态而安全停止，未计为视觉通过。
- [x] 教程面板响应式/收起交互：展开按屏幕安全区缩放并在小分辨率使用紧凑字号；收起后只保留
  标题、章节进度与展开按钮，便于查看牌堆、热量和赛道，且不会推进教程状态。
- [x] 将运行时生成的表现层转换为 `Assets/Resources/Prefabs/UI/TutorialOverlay.prefab`：根节点可编辑
  16 步与练习文案，面板/按钮/聚光说明可在 Hierarchy 手工调整；运行时优先复用 `RaceCanvas`
  下的实例，缺失时自动加载 Prefab，展开时恢复人工布局。
- [x] 为 `TutorialOverlayAuthoring` 增加非 Play Mode 步骤/练习/完成态预览入口，使文案换行与
  RectTransform 能在 Prefab Mode 联合校正；预览不创建 Director、不推进步骤也不写进度。
- [x] 增加只读作者校验，报告 16 步缺失/重复 ID、空白必填展示字段和断开的 Guide/Focus 引用；
  可选提示允许留空，绝不自动覆盖手工文本或布局。
- [x] 增加 `TutorialOverlayAuthoringEditor` 专用 Inspector：常驻步骤/练习/完成态预览按钮、Undo
  支持和内联校验结果；人工文案同步与条件渲染回归后全量 EditMode `516/516` 通过，0 失败、0 跳过。
- [x] 指引框按当前 TMP 正文首选高度自动增长，底部进度与按钮同步下移且不超过屏幕安全高度；
  每步高光可由下一次独立单击关闭，同步刷新不会复现。全量 EditMode `519/519` 通过，0 失败、0 跳过。
- [x] 修正教程回合边界：第 3 步与第 7 步在上一回合完整结束、检查点落位后才刷新显示；同回合
  的操作与结果观察保持连续；延迟课程仅在镜头已对准玩家、选挡 HUD 已就绪后出现。显示时序
  回归后全量 EditMode `522/522` 通过。
- [x] 修正首次进入教程的显示同步：MainMenu 可继续保留启用的作者预览实例供手工排版，
  但运行时会在首帧渲染前隐藏；Race 随后先完成比赛状态和摄像机初始化，再对准玩家并显示第一步。
- [x] 修正基础驾驶牌区与检查点显示时机：确认速度牌后的移动课改为高亮赛道；牌区课延迟到
  下一回合并先按固定牌序补齐可见手牌。所有玩家检查点应用后立即同步赛车 Transform、车道、
  摄像机、手牌和 HUD，不再等待下一次挡位确认才显示脚本位置；进入牌区课前自动跳过被旧面板
  遮住的可选弃牌输入，避免第 4 步移动完成后看似卡住。
- [ ] 教程体验优化验收：完整人工走查渐进式面板与高光，确认 16:9 常用分辨率下文字、按钮、
  遮罩边界和关键 HUD 无遮挡，并按实际操作节奏继续精简过长步骤。
- [ ] 下一工作包：执行完整引导到练习的一次安全人工 Play Mode 验收，并单独走查普通
  Quick Race 的随机牌组、科技、正常赛道维修区与存档路径。

> Updated: 2026-08-28
> Sources: Claude Code project memory, active session state, session history,
> current Git worktree, and current Unity project structure.

## 下一阶段（2026-08-27 Demo 验收、资源与音频）

- [x] 审计当前 `Assets/`：核心卡牌、六队赛车、八张 4K 赛道图、档位旋钮和中文
  TMP 已接入；主菜单背景/Logo、六队徽章、12 位车手头像、科技树美术和全部音频缺失。
- [x] 重写 `design/planning/asset-manifest.md`，把旧 42 格节点图、已被运行时替代的
  FX/Prefab 与真正缺失资源分开；增加美术规格、完整音乐/音效事件表和制作顺序。
- [x] 新增 `design/gdd/foodula-1-audio-style.md`，定义音乐方向、事件声纹、Mixer
  分组、限频、慢放边界和 Demo 音频验收标准。
- [x] 同步 `systems-index.md`、`roadmap.md`、`demo-framework.md` 与视觉 GDD：
  当前阶段改为 Demo 候选，最新全量 EditMode 证据为 `471/471`，粒子火花等冲突需求已移除。
- [ ] P0：用户进行完整比赛与高风险机制验收；读取日志并仅修复明确缺陷。
- [ ] P0：验收修复完成后重跑定向 + 全量 EditMode，记录 Console/日志/分辨率证据并冻结 Demo 基线。
- [ ] P1：按资源清单制作并接入主菜单/Logo、车队/车手、科技树和赛道缩略图。
- [ ] P1：建立 AudioMixer/音频服务，接入菜单/比赛 BGM 与核心玩法音效。

## 本轮修复（2026-08-27 尾流慢放作用域）

- [x] 修正 `RaceEventFX` 尾流特写遗漏恢复 `Time.timeScale` 的问题；慢放现在覆盖尾流
  特写及紧接的尾流奖励移动，演出/奖励移动结束、异常清理、组件禁用或销毁时都会恢复
  到正常比赛倍率 `1`。基础速度牌移动和后续回合不在慢放作用域内。
- [x] 超车特写复用同一安全的慢放作用域，避免两个事件表现留下全局慢速状态。
- [x] 新增慢放作用域回归测试；定向 EditMode `5/5`、全量 EditMode `471/471` 通过，
  0 失败、0 跳过、Console 无错误/警告。Play Mode 回归作业因当前项目缺少独立 PlayMode
  测试程序集而在初始化阶段未启动，未将其记为通过；新增的无效测试文件已撤回。

## 本轮完成（2026-08-27 车队可读性工作包）

- [x] 为运行时每辆赛车增加不改场景/Prefab 的世界空间车队徽标：显示稳定车队代码与实时名次，
  车体转向时徽标保持正向，并使用车队颜色与高对比文字。
- [x] 新增车队代码、徽标颜色和文字对比度纯规则测试；徽标是当前缺少国旗/车手头像素材时的
  可读性回退，不宣称已经完成设计案要求的正式国旗图标与头像素材。
- [x] Unity MCP 已完成脚本重新导入；车队徽标定向 EditMode `18/18`、全量 EditMode
  `469/469` 通过，0 失败、0 跳过。
- [ ] 待进行一次多车 Play Mode 视觉验收，确认徽标尺寸、遮挡关系和名次刷新；正式国旗/头像
  素材仍属于后续视觉资产工作包。

## 本轮完成（2026-08-27 日志证据自动化）

- [x] 新增纯 C# `RaceLogAnalyzer`：逐回合检查 `[CARD_PHASE]`→`[MOVE_PHASE]`→可选
  `[SLIPSTREAM_PHASE]` 的顺序，拒绝尾流早于基础移动的旧日志，并校验
  `[DISCARD] selected=N discarded=M` 的数量关系。
- [x] 新增 4 个日志分析回归用例，覆盖有效尾流/弃牌、旧版尾流顺序错误、弃牌数量溢出和
  未结束的半局日志；分析器区分结构错误与仅缺少 `RACE_END` 的不完整日志。
- [x] 新增文件级适配器与 Unity 菜单入口：`Foodula1 > Tools > Analyze Latest Race Log`
  会读取最新真实日志，输出回合数、尾流阶段、弃牌事件和结构错误；也可选择指定 `.log` 文件。
- [x] 文件适配器新增 2 个回归用例已包含在本轮全量 EditMode `469/469` 通过结果中。
- [ ] 仍待对最新完整试玩日志执行一次 Unity 菜单检查；这项真人日志入口验收尚未冒充为已完成。

## 本轮完成（2026-08-27 失控动画参数化）

- [x] 将 `RaceEventFX` 的失控旋转从硬编码 0.92 秒改为默认 1 秒、360 度，并保留
  爆缸脉冲和规则层状态不变；旋转仍为克制的单车体表现，不新增粒子、镜头震动或速度线。
- [x] 新增纯层 `RaceEventPresentationRules` 的时间归一化与缓出旋转覆盖，以及默认字段的
  EditMode 测试；测试文件和 `.meta` 已加入，未修改 Race 场景或 HUD 预制体。
- [ ] Unity EditMode/Play Mode 视觉验收待 MCP 实例恢复后执行；本轮只做静态实现断言，
  不将未运行的 Unity 测试标记为通过。

## 本轮完成（2026-08-30 移除车辆移动弹跳）

- [x] 移除逐格移动的垂直跳跃、弧高配置和对应纯函数；车辆保留每格 0.15 秒的
  赛道平面线性插值、切线朝向与节点精确对齐，传送/静止移动仍直接定位。
- [x] 更新车辆移动回归，逐帧验证中间位置不偏离起点到目标节点的赛道平面线段；
  `moveAnimSpeed` 继续作为旧配置的时长回退。
- [x] Unity 全量 EditMode 回归 `521/521` 通过，0 失败、0 跳过；Play Mode 视觉验收
  仍待手动走一段比赛确认最终观感。

## 本轮手动试玩视觉回归（2026-08-26）

- [x] 分析最新试玩日志：`C:\Users\Admin\AppData\LocalLow\DefaultCompany\Foodula1\race-logs\race-20260826-052850-silverstone_afternoon_tea-0f67c27acc98460ca100e7a2e0605e01.log`。
  日志覆盖 7 个回合，规则层已记录多段 `[SLIPSTREAM]`，没有记录视觉层异常；它不能单独证明
  尾流特写是否可见。
- [x] 修正主动弃牌表现：数据层返回实际从手牌移入弃牌堆的卡牌实例，动画逐张捕获对应手牌位置，
  然后只隐藏/移除已弃置的手牌 UI，不再整手销毁重建；未选中的牌保持在原手牌展示中，不会再被
  表现为飞入弃牌堆。新增 `[DISCARD] selected=N discarded=M` 日志，便于下一次手动试玩核对规则与视觉。
- [x] 将尾流明确拆为回合末独立阶段：所有车辆先完成基础移动、反应与弯道判定，日志记录
  `[MOVE_PHASE] end` 后才按实际落位结算尾流；已有卡牌飞行结束后显示“尾流阶段”并聚焦车辆，
  使用 0.95 秒、0.28 倍时间尺度的独立特写，`[SLIPSTREAM_PHASE] end` 后才执行额外移动。
- [x] 新增回合末实际落位的尾流回归覆盖，并保留弃牌实例与温度计绑定回归；本轮定向 Unity
  EditMode `56/56`、全量 `446/446` 通过，0 失败、0 跳过。尚未用新日志完成包含弃牌和尾流
  画面的人工验收。
- [x] 分析最新试玩日志 `C:\Users\Admin\AppData\LocalLow\DefaultCompany\Foodula1\race-logs\race-20260826-081928-silverstone_afternoon_tea-df5ea074251143398bf63204c684030c.log`：两车基础移动后同落第 9 格，旧规则将同格前向距离 0 双向判为前车。已按设计案接入“基础移动力优先、同值按到达顺序”的同格判定，并让运行时、纯模拟和车队基准在统一解析后再应用奖励移动。
- [ ] 下一步重跑一小段包含主动弃牌与尾流的 Play Mode：确认只有选中牌飞行，且尾流特写完整结束后
  才开始移动；同时核对新日志中的 `[DISCARD]`、`[CARD_PHASE]`、`[SLIPSTREAM_PHASE]`、`[MOVE_PHASE]` 顺序。

## 本轮验证（2026-08-26 HUD 表现回归）

- [x] 按更新后的自动化流程调用 `sprint-status` skill；项目当前没有
  `production/sprints/`、`sprint-status.yaml` 或可执行 story，已按回退规则继续从本清单推进，
  未创建流程文件或 `production/session-state/active.md`。
- [x] HUD/牌堆/挡位反馈定向 EditMode `21/21` 通过，全量 EditMode `444/444` 通过，0 失败、
  0 跳过；测试期间唯一的缺失赛道 Console 错误由 `LogAssert` 明确预期，已清理。
- [x] Unity Play Mode 启动冒烟运行 5 秒：进入 `MainMenu` 后无项目错误/警告；退出后编辑器回到
  非播放、非编译、`ready_for_tools=true`，`Race` 场景 `isDirty=false`。
- [x] 按 `consistency-check` skill 定向核对核心 GDD：实体注册表当前为空，未产生实体冲突；已修正
  MVP 尾流旧状态、尾流/移动阶段顺序、雨天失控计数描述和过弯热量去向，并同步当时的 `436/436`
  回归证据。

## 本轮完成（2026-08-26 AI 尾流策略接入）

- [x] 修正 AI 行为树中明确标注“P3 尾流 — MVP 跳过”但运行时没有主动规划的问题：低热量、
  无弯道风险且前车当前前向距离在配置窗口内时，AI 会估算前车计划移动并尝试精确组合当前
  档位的速度牌，使计划终点保持 1 格尾流触发距离；没有精确组合时安全回退到原策略。
- [x] 修正 AI 已经落后前车 1 格时被错误排除在主动规划窗口外的问题；现在会按前车预估
  移动量寻找维持 1 格尾流距离的精确牌组，仍将同格车辆交给回合末到达顺序判定。
- [x] 新增 `GameConfigSO.aiSlipstreamPlanningRange`（默认 2）和 `AIPlanner` 纯函数：覆盖
  环形赛道前向距离、前车移动估算后的目标移动力、精确卡牌组合与不可达回退边界。
- [x] 历史 Unity EditMode AI 定向测试 `12/12`、全量 `441/441` 通过，0 失败、0 跳过；
  本轮新增 2 个“一格尾流规划”边界用例。完整多车
  Play Mode 调参仍未完成。
- [x] AI 主动尾流候选与运行时规则对齐：不同圈车辆不再作为规划目标；新增 1 个跨圈
  集成回归用例。
- [x] Unity 编辑器恢复后完成 AI 尾流定向回归 `15/15`、全量 EditMode `451/451`，
  0 失败、0 跳过；覆盖同圈主动规划、一格边界、不可达回退和跨圈候选过滤。
- [x] 读取最新人工日志 `C:\Users\Admin\AppData\LocalLow\DefaultCompany\Foodula1\race-logs\race-20260826-085042-silverstone_afternoon_tea-f5f194d14b67484292b35b8db9c240fc.log`：
  `CARD_PHASE end → MOVE_PHASE end → SLIPSTREAM → SLIPSTREAM_PHASE → SLIPSTREAM_MOVE_PHASE`
  顺序正确，且本局只有玩家获得 1 次 `+2` 尾流移动；本局在 `RACE_END` 前没有进入弃牌阶段，
  因而不能替代弃牌视觉验收。
- [ ] 下一步人工构造相邻 AI/玩家位置，确认 AI 实际选牌后日志出现 `[SLIPSTREAM]`，并观察
  尾流特写与后续回合；这与现有规则层/受控事件链测试互补。

## 本次完成（2026-08-25 链式尾流与车队平衡闭环）

- [x] 按核心 GDD 补齐每回合最多两段的链式尾流：第一段奖励会推进模拟位置，再寻找
  更前方且尚未跟随过的车辆；不会重复吸同一辆车，也不会绕过开启冰糕的最近前车。
- [x] 尾流判定改为所有车辆完成基础移动、反应和弯道判定后的回合末实际落位；科技、特技
  和车队加成先完成基础移动，尾流不再在回合开始或基础移动前触发，避免受到阶段顺序影响。
- [x] 尾流日志现在逐段记录 `chain`、前车、单段奖励和总奖励；表现层会把链中每一段
  都纳入同一个尾流特写阶段。新增 5 项链式、上限、多云、完整移动和冰糕阻断回归。
- [x] Unity EditMode 定向测试 47/47、全量测试 411/411 通过；编译后二次完整刷新
  Console 为 0 错误/警告，`Race` 场景保持未修改。
- [x] 将 `race_simulation_test` 和 `TrackTeamBalanceBenchmark` 与运行时统一为：全员选牌 → 依排名
  执行基础移动/反应/弯道 → 按回合末实际落位解析链式尾流 → 应用额外移动。纯模拟同时补上
  进站成功后的出口位置应用；确定性完整比赛测试 1/1 通过。
- [x] 实际重跑 9 个赛道配置 × 每图 12 场六车平衡基准；报告新增每队平均尾流触发
  次数和尾流移动收益，证明离线基准确实走到链式尾流路径。完整 EditMode 仍为 411/411。
- [x] 完成车队平衡审计并修正三项基准/运行时错误：美国直道旧面板 `+3` 不再与固定
  `+1` 重复叠加；中国 Recover→Go 风险按首轮 3 张牌估算；基准支付热量会生成真实
  手牌热量供冷却。重跑后 648 个车队参赛样本全部完赛，原中国勒芒 92% DNF 被确认
  为基准假象。基准同时改为记录个人完赛回合并轮换车队插入顺序。
- [x] 修正意大利“激情过弯”时序：成功通过弯心后挂起增益，在后续首个实际打出
  速度牌的回合一次性 `+1`；空回合保留，失控不挂起，不再错误加在过弯当回合。
- [x] 用受控基准确认意大利面板加速被误算为常驻直道 `+1`：旧规则 9 图平均排名
  `1.37`、胜率 `74%`；仅移除该未写入 GDD 的加成后回到排名 `2.02`、胜率 `35%`。
  最终运行时只保留操控 `+2` 与下一回合一次性出弯 `+1`；对齐 AI 阈值后的矩阵约为
  排名 `2.31`、胜率 `26%`。
- [x] 平衡基准新增中国 Go/Recover、弯前强制 Recover、原始牌面移动与非尾流移动
  计数。AI 弯前模式判断改用“最低合法 Go 手牌”而非最高牌；中国平均排名由约
  `5.55` 改善到 `4.90`，仍保持 0% DNF。完全移除保护的对照在四条密集弯道出现
  `83–100%` DNF，因此保留“最低牌仍必然超速才 Recover”的安全边界。
- [x] 在上述安全边界上继续加入可负担风险：最低合法 Go 组合会累计本回合跨过的唯一
  弯心成本，只有预计弯道热量 ≤ `1`，且引擎还能支付超频和缺牌成本时才保持 Go。
  同步修正基准的 6 格预判和 AI 热量阈值以匹配运行时 `0.7/0.5/0.3`；最终中国九图
  平均排名 `4.54`、胜率约 `7%`、0% DNF，平均 Recover 从严格零热策略的 `25.8`
  降到 `22.5`。
- [x] 本轮新增意大利时序与中国热量预算回归，Unity EditMode 全量 `417/417` 通过；九赛道基准已按
  最终规则重跑，设计说明、平衡报告和项目记忆已同步。
- [x] 参数接线后的最终 Unity EditMode 全量回归已真实通过 `435/435`；本轮结束时
  Console 清理后为 0 错误/警告。当前环境没有可用的 .NET SDK，因此
  `dotnet build --no-restore` 仍不能作为替代验证。
- [ ] 平衡后续：中国在银石/铃鹿仍偏弱；先做人工手感验证，再考虑最后一圈风险容忍或
  路线策略。英国/日本的主动特技未被纯车体基准充分使用，不应直接按当前排名加数值。
- [x] 受控 Play Mode 尾流冒烟已实际走通：临时将运行时配置设为玩家 + 3 AI，注入相邻
  计划终点后，日志出现 `AI3 follows=AI2 bonus=2` 与 `AI2 follows=AI3 bonus=2`；
  `RaceEventFX` 在场且尾流阶段可进入，证明运行时事件链和表现组件均被调用。日志路径为
  `C:/Users/Admin/AppData/LocalLow/DefaultCompany/Foodula1/race-logs/race-20260825-124729-silverstone_afternoon_tea-5e5ee1a868b04cf9ba61ee3303abde34.log`。
  本次是受控单段命中冒烟，不等同于两段链式人工验收；完整人工对局仍需确认气流连线、
  特写遮罩、后续回合推进，以及修正后的美国直道 `+1` 与中国 Go/Recover 节奏。

- [x] 分析 2026-08-25 11:34 的中国队印第安纳波利斯日志：第 1 回合 Go 的有效上限为
  4 张，是基础 Go 3 张加火锅底料的 1 个 ATTACK 额外槽；第 2 回合连续 Go 的基础上限
  才是 4 张。第 3 回合失控进入 Recover 后计数清零，第 4 回合重新 Go 的基础上限已重置
  为 3 张，但当回合再次打出火锅底料后有效上限仍为 4 张。定向 EditMode 133/133、全量
  EditMode 436/436 通过；当前日志未出现尾流事件。

- [x] 2026-08-25 受控上海/中国队首回合走查：日志记录了 AI 档位与出牌、玩家挡位缺牌后
  `Engine failure! Missing 1 speed card(s). +1 Heat to hand.`，以及中国队 Recover 冷却
  1 张热量回引擎；日志路径为
  `C:/Users/Admin/AppData/LocalLow/DefaultCompany/Foodula1/race-logs/race-20260825-110823-shanghai_dim_sum-dbfbc7c7595a4e3db687d3597f296342.log`。
  本轮定向 EditMode 195/195、全量 EditMode 435/435 通过，Race 场景退出后保持未修改，Console
  清理后为 0 条；该日志只覆盖首回合，未出现 `[SLIPSTREAM]`，不能替代尾流人工验收。
- [x] 复核受控输入中 `Animating` 超过 10 秒的现象：代码确认移动后会进入 `DiscardStep()`，
  由 `inputState.WaitingForDiscard` 等待玩家确认弃牌，而阶段枚举在收尾前仍保持
  `Animating`；本次退出发生在确认弃牌之前，因此不是已证实的移动动画死锁。
- [ ] 继续完整人工走查：下一轮先确认弃牌完成收尾，再用不插入查询的方式单独构造相邻车辆
  尾流，确认气流连线、`[SLIPSTREAM]` 日志、尾流奖励和后续回合是否正常推进。

## 待推进（2026-08-23 比赛视觉信息强化）

- [x] 赛道目录健康检查：Unity `Foodula1/Tools/Validate Track JSONs` 已校验全部
  9 条赛道，节点连续性、起终点、弯心/限速、维修区成对、坐标、圈数、天气与闭环
  布局全部通过；维修区、天气、尾流、纯层完整比赛和全部赛道显示回归定向测试
  `67/67` 通过。
- [x] Play Mode 已逐条抽查全部 8 条官方赛道：银石、纽博格林大奖、蒙扎、印第安纳波利斯、
  上海、铃鹿、纽博格林北环和勒芒旧慕尚。分别确认 JSON 加载、2/4 车道布局、格数/弯道
  标识、玩家光环、四区 HUD 与缩略图均可见，最终 Console 无错误/警告。
- [ ] 完成包含牌堆变化、挡位缺牌、尾流与维修区的完整人工走查；本轮仅验证了各赛道的
  初始 Play Mode 画面与加载状态。

- [x] 根据 2026-08-25 上海完整对局日志修复终局锁定：玩家第 38 回合完赛后不再继续
  触发阴阳茶、支付热量或移动，避免已完赛状态在等待 AI 时反向变成爆缸；新增终态规则和
  RaceSession 回归。原始日志已整理为 `production/qa/playtests/playtest-2026-08-25-manual-full-race.md`。
- [x] 增加卡牌区域流转初版：速度/特技/实际被选中的主动弃牌飞向弃牌堆，热量支付从引擎飞向规则
  目标，冷却按真实来源飞回引擎；牌堆厚度改为每 3 张增加 1 层、最多 7 层，精确徽标保留。
  针对性 EditMode 69/69、全量 EditMode 430/430 通过；Play Mode 运行时注入热量支付动画
  可见，Race 场景退出播放后保持 `isDirty=false`，Console 0 错误/警告。
- [ ] Play Mode 人工走查上述动画的遮罩、弧线方向、批量节奏和牌堆层距，并重跑一次
  “玩家先完赛、AI 后完赛”的中国队对局，确认赛后无阴阳茶/爆缸、结果与 XP 一致。

- [x] 根据 2026-08-24 上海半局日志修正多云尾流语义：多云不再把基础 1 格触发距离
  压成 0，而是按 GDD 的“尾流效率 -1”将最终尾流移动奖励减少 1；大雨仍完全禁用。
  已补天气纯规则与 RaceSession 回归，Unity EditMode 全量 406/406 通过；Play Mode
  仍需实际触发确认表现。

- [x] 将比赛最终四区 HUD 烘焙进 `RaceCanvas.prefab`，运行时优先保留 Prefab 中人工
  调整的 RectTransform；旧 Canvas 缺少主面板时才自动补齐。新增 Race UI Authoring
  编辑器入口和已编排/回退两条布局回归测试；Unity EditMode 全量回归 403/403 通过，
  Play Mode 确认采用 authored layout、画面与基线一致且 Console 0 错误/警告。
- [x] 比赛中“返回主菜单”按钮移入左侧操作栏动作栈，放在重新开始按钮下方、
  提示日志面板上方，避免覆盖操作栏标题和赛道画面；Play Mode 截图和 Console
  走查通过，并增加运行时锚点回归测试。
- [x] P0：增强挡位出牌要求反馈，显示有效要求值、已出/要求数量和缺牌引擎故障预警；
  保持现有缺牌惩罚规则不变。EditMode 已覆盖正常、待确认和引擎热量不足提示，
  Play Mode 人工走查仍待完成。
- [x] P1：选中卡牌缩放并上移，保持布局槽位不变；选中态使用 1.08 倍缩放、上移
  24px、0.14 秒未缩放时间缓动和蓝色阴影，已覆盖多选、特技单选和布局槽位不变。
  Unity EditMode 当前全量回归 403/403 通过；Play Mode 遮罩边界仍待人工走查。
- [x] P1：为抽牌堆和弃牌堆增加牌背叠放、实际卡牌缩略图、数量徽标和牌堆变化刷新；
  抽牌堆按实际抽取顺序预览，弃牌堆按最近弃入顺序预览，热量牌按真实区域显示。
  Unity EditMode 当前全量回归 403/403 通过；Play Mode 尺寸与可读性仍待人工走查。
- [x] P1：基于赛道 JSON 增加科技蓝玩家光环、1-based `格 X/N` 和玩家前后各 6 格
  的局部刻度；密集节点会自动抽样文字。弯道覆盖层同步重制为 Lv1 绿 / Lv2 黄 /
  Lv3 红的圆角平滑双层曲线带，弯心和限速徽标保持清晰。Unity EditMode 全量回归
  403/403 通过，并覆盖全部 8 条官方 JSON 与 fallback 的弯道曲线采样；8 条官方赛道
  均已完成 Play Mode 截图与 Console 走查。
- [x] P2：尾流结算现在返回命中的前车与最终加成，并在基础移动、反应和弯道判定结束后把
  同回合全部事件合并为 0.95 秒独立表现：两车同步聚焦、科技蓝流动虚线和“尾流 +N”提示；
  特写使用 0.28 倍时间尺度且计时使用 unscaled time，结束后才应用额外移动。
  Unity EditMode 当前全量回归 442/442 通过，
  实际画面仍待包含尾流的一局 Play Mode 人工走查。
- [ ] 验收：相关 EditMode 测试通过，并完成至少一局包含牌堆变化、挡位缺牌和尾流的
  Play Mode 人工走查。

## 本次修复（2026-08-23 手动日志回归）

- [x] 修正 RaceTestLogWriter 的赛道元数据来源：优先记录 TrackManager 实际加载的
  赛道 ID，避免配置默认值与菜单选择不一致。
- [x] 修正比赛终止条件：玩家爆缸只将玩家标记为 DNF，剩余非爆缸赛车继续比赛，
  直到所有活动赛车完成或退赛。
- [x] 修正比赛结果文本中的国旗和状态 Emoji 字形警告，改用稳定的车队代码和中文
  状态标签，避免 TMP 显示方框。
- [x] 增加 RaceRanking 回归测试，并让纯层比赛模拟覆盖玩家 DNF 后其余赛车继续比赛；
  Unity EditMode 全量回归 403/403 通过。
- [ ] 仍需用新的手动 Play Mode 日志确认：玩家 DNF 后 AI 会继续完成并正确记录最终结果。

## 本次完成（2026-08-23 热量牌生命周期、阴阳茶与维修区）

- [x] 修正热量牌生命周期：`initialHeatCards` 不再加入普通牌组；开局与普通补牌只抽
  速度/特技牌。永久热量只能从独立引擎池经明确支付/效果进入手牌或弃牌堆，再由冷却
  或明确回收效果返回引擎。
- [x] 将自动冷却统一到 `CardDeck.CoolHeat()`，严格按手牌→抽牌堆→弃牌堆
  顺序处理热量；永久热量回引擎，限时热量销毁。
- [x] 将中国队阴阳茶改为按 Go/Recover 模式结算：Go 支付引擎热量到弃牌堆并
  前进 1 格，Recover 从手牌冷却 1 张。
- [x] 维修区保持停 1 回合，同时在 `pit_exit` 后按配置前移；新增中国队“快充技术”
  科技修正。
- [ ] 仍需在 Play Mode 完成多天气、多圈和完整进站流程的人工走查；本次未修改场景。

## 本次推进（2026-08-23 热量支付与维修区时序）

- [x] 标准热量支付改为默认直接进入手牌，热量会占用手牌；显式弃牌堆路径保留给
  阴阳茶 Go 等明确效果；普通抽牌仍不会抽取热量。
- [x] 维修区选择窗口改为 `pit_entry` 前 1–10 格；选择进站只登记预定状态，
  越过入口的本回合继续移动，下一回合开始才执行停站、全热量冷却和出口前移。
- [x] 增加维修区入口前窗口、跨圈入口检测和“预定后延后一回合执行”的 EditMode 覆盖。
- [x] 本轮 Unity EditMode 全量回归 403/403 通过；Play Mode 进站人工走查仍待验证。

## 本次完成（2026-08-19 赛道天气规则边界）

- [x] 将赛道 JSON 的 `sunny/cloudy/light_rain/heavy_rain/hot` 映射为独立
  `WeatherType`，并保留旧 `WeatherType.Rainy` 作为小雨兼容别名。
- [x] 将弯道限速、尾流范围/禁用、热天冷却惩罚、湿地失控计数器增量和 HUD
  文案统一收敛到 `WeatherModifiers` / `WeatherRules`，管理器仅负责调用。
- [x] 增加 `RaceLapWeatherRules`，让运行时比赛与纯模拟共用起终点过线、每圈天气门控
  和完赛判定顺序；新增跨圈转场回归测试。Unity EditMode：377/377 通过，
  `dotnet build Foodula1.sln --no-restore`：0 错误。
- [x] Play Mode 启动冒烟通过：5 秒运行期间 Console 0 条错误/警告/日志；未修改场景。
- [ ] 仍需在 Play Mode 完成多天气、多圈和完整进站流程的人工走查；本轮未修改场景。

## 本次完成（2026-08-18 出牌与赛事表现）

- [x] 速度牌支持多选后一次确认，也支持选中一张后单张确认；选择数量由本回合
  出牌上限实时限制，提交在 `CardPlayRules` 中原子完成。
- [x] 特技牌仍严格保持单张选择、即时结算，并禁止与速度牌混选。
- [x] 赛车图标显示缩放改为 `GameConfigSO.carSpriteScale`，默认由 0.2 调整为 0.28。
- [x] 新增运行时 `RaceEventFX`：超车慢放特写、失控旋转提示与爆缸退赛提示，均不改变
  规则层状态。
- [x] Unity 编辑器回归测试已完成：EditMode 365/365 通过，Play Mode 启动冒烟无项目
  错误或警告（仅 Unity MCP 自身 WebSocket 重连警告）。

## 本次完成（2026-08-18 模块化推进）

- [x] 将 `MVPGameManager.AutoCreateUI()` 的程序化 HUD、档位按钮、动作按钮、手牌容器
  和卡牌预制体回退逻辑抽取到 `Assets/Scripts/UI/RaceUIFactory.cs`。
- [x] 保留 Prefab 优先、旧场景回退、按钮回调修复和 TMP 字体复用行为；Manager 只负责
  传入回调与接收 UI 引用，不再持有主要 UI 构建细节。
- [x] Unity EditMode：321/321 通过；`dotnet build Foodula1.sln --no-restore`：0 错误。
- [x] Unity Play Mode 启动冒烟无项目错误/警告；MCP 仅记录自身 WebSocket 重连警告。
- [x] 将出生朝向、传送朝向和逐帧旋转的 Unity 适配逻辑抽取到
  `Assets/Scripts/Gameplay/CarOrientationController.cs`；角度规则仍由
  `CarOrientationRules` 纯函数负责。
- [x] 新增朝向偏移与旋转速度回归测试；Unity EditMode：323/323 通过，Dotnet 编译 0 错误。
- [x] 将车队赛车精灵槽位与缺失精灵时的备用颜色映射抽取到
  `Assets/Scripts/Gameplay/TeamCarPresentationRules.cs`；新增 4 项边界回归测试，避免
  `MVPGameManager` 直接维护车队外观身份映射。
- [x] 将节点间车辆插值与到达阈值抽取到
  `Assets/Scripts/Gameplay/CarMovementAnimator.cs` / `CarMovementRules.cs`；通过注入
  deltaTime 的回归测试保持原有移动速度、终点吸附与朝向更新行为。
- [x] 将环形赛道超车判定抽取到 `Assets/Scripts/Core/RaceMovementRules.cs`；通过注入
  跳过回合谓词覆盖普通超车、失控/维修区跳过和无效赛道长度边界。
- [x] 将普通赛道并排时的后车外线判定抽取到
  `Assets/Scripts/Core/RaceLaneRules.cs`；覆盖同节点、不同圈/节点、完赛退赛和无效索引边界。
- [x] 将档位、卡牌和弃牌阶段的玩家输入等待状态抽取到
  `Assets/Scripts/Core/RaceInputState.cs`；回合协程与 UI 回调共享同一门控状态，并覆盖
  确认一次、阶段互斥和重置边界。
- [x] 将起终点过线后的圈数递增与完赛边界抽取到
  `Assets/Scripts/Core/RaceLapRules.cs`；天气掷骰、科技重置和 UI 日志仍由管理器编排。
- [x] 将印地换道与维修区选择的等待门控并入
  `Assets/Scripts/Core/RaceInputState.cs`；场景面板和选择后的车辆/维修效果仍由管理器编排。
- [x] 将回合跳过、爆缸和完赛后的参与资格抽取到
  `Assets/Scripts/Core/RaceTurnRules.cs`；A1 的跳过消费和各阶段副作用仍由管理器编排。
- [x] 将比赛阶段枚举、阶段切换和输入可接受性抽取到
  `Assets/Scripts/Core/RacePhaseState.cs`；协程副作用和 UI 仍由管理器编排。
- [x] 将每圈天气只掷一次的门控抽取到
  `Assets/Scripts/Core/RaceWeatherState.cs`；天气池选择和 UI 日志仍由会话/管理器编排。
- [x] 增加 `RaceTestLogWriter` 手动测试日志：自动保存 HUD 事件、回合状态、档位、玩家/AI
  出牌、特技牌和移动计划；启动/结束时在 Console 输出日志绝对路径，文件写入或关闭失败
  不影响比赛。日志适配器通过 `GetDefaultDirectory()` 暴露默认目录，并覆盖重开比赛时的
  文件轮换。

## 本次修复（2026-08-18 档位确认卡死）

- [x] 修复 `RaceEventFX` 复用失效 `CanvasGroup` 导致 `MVPGameManager.Start()` 中断的问题。
- [x] 赛事特效初始化每次创建带必需组件的新根节点，并设置为可选表现；即使特效初始化
  失败也会继续启动比赛回合协程。
- [x] Unity 编辑器退出 Play Mode 并重载脚本后复测档位确认按钮和首回合推进；本轮
  EditMode 365/365 通过，启动冒烟未再出现 `CanvasGroup` 异常。

## P0 - Resume Approved Scheme A Refactor

- [x] Review all current C# files and reconcile the earlier whole-project
  review with the latest project state.
- [x] Integrate `Assets/Scripts/Core/RaceRules.cs` into
  `MVPGameManager.cs` so shared gear, cooling, movement, and selection rules
  have one source of truth.
- [x] Integrate `Assets/Scripts/AI/AIPlanner.cs` into `AIController.cs`.
- [x] Inject `IRandomSource` where deterministic gameplay or AI behavior is
  required.
- [x] Add EditMode tests for `RaceRules` (9 cases passing in Unity).
- [x] Add EditMode tests for `AIPlanner`, deterministic random behavior,
  and the AI spin-out card-conservation regression (7 cases passing in Unity).
- [x] Historical baseline: validated changed scripts and Unity console with no errors
  (16 EditMode tests at that stage; current full suite is 403/403).
- [x] Review the final diff for the approved refactor changes.
- [ ] Commit only with explicit user instruction; scheduled-task authorization
  does not include Git commits.

The four Scheme A source files and their `.meta` files were committed in
`58d1bba`. `RaceRules` and `AIPlanner` are now integrated into the runtime.
`AIController` and `CardDeck` accept injectable random sources, and the
refactor currently has 16 passing EditMode tests. The final review also fixed an
AI spin-out path that could remove selected speed cards without discarding them,
and standardized `IRandomSource.NextDouble` to the [0, 1) contract.
Manager-level seeded replay and broader integration coverage remain useful
follow-ups, but are not blocking the current Demo path.

The first data-driven track completion slice is also verified. `TrackNode` now
preserves JSON apex metadata, pure `TrackRules` owns wrapping traversal and
start/finish lookup, and `MVPGameManager` initializes and counts laps from the
runtime track rather than the legacy config index. Silverstone loads in the
Race scene with 60 nodes and 3 laps. Unity currently passes 20 EditMode tests
with 0 failures, warnings, or errors.

## P0 - Protect and Reconcile the Current Worktree

- [ ] Inspect the existing modifications to `MainMenu.unity` and
  `Race.unity`; preserve legitimate user scene edits.
- [ ] Verify the untracked `Assets/Data/Tracks/NewTrackData.asset` before
  deciding whether it belongs to the track-system work.
- [ ] Keep the Unity MCP package changes in `Packages/manifest.json` and
  `Packages/packages-lock.json` logically separate from gameplay refactoring.
- [ ] Avoid bundling unrelated scene, track-data, MCP installation, and
  refactor changes into one commit.

## P1 - Track System Decision and Completion

- [ ] Choose a reliable authoring workflow: GameObject child nodes, a simpler
  Editor script, or another explicitly approved approach.
- [ ] Decide whether AI-generated `track_layout_*.png` images are authoring
  references, runtime backgrounds, or both.
- [x] Configure `GameConfigSO.trackId` and verify JSON track loading in the
  Race scene (Silverstone, 60 nodes, 3 laps).
- [x] Validate arbitrary node counts throughout movement and UI; remove
  remaining hard-coded `42` display assumptions.
- [x] Preserve JSON `isApex` metadata and validate apex-only, deduplicated
  corner crossing across the lap boundary.
- [ ] Playtest speed-limit heat penalties through the full Race interaction.
- [x] Drive start/finish lookup and crossing from runtime track-node data;
  validate wrapping and a non-zero start/finish index in EditMode tests.
- [ ] Complete a multi-lap manual playthrough to validate finish timing.
- [x] Implement and unit-test pit entry and pit exit behavior; Play Mode manual validation remains open above.
- [x] Drive LineRenderer positions from loaded track coordinates and verify
  the Silverstone path in Play Mode.
- [x] Replace the inaccurate Nürburgring 24H combined bonus layout with a
  219-node standalone Nordschleife sampled from the referenced real layout;
  verify zero self-intersections and regenerate its guide/background.
- [x] Standardize node colors across all tracks: apex red, other corner
  nodes orange, straights white, and start/finish green; enforce exactly one
  apex per corner group across every track config.
- [x] Add presentation-only lane slots to every track: two lanes for standard
  tracks and four lanes for Indianapolis; keep gameplay, camera, and minimap
  positions centerline-based, and regenerate backgrounds with matching lanes.
- [x] Add Indianapolis lane-specific corner limits (inner-to-outer 4/5/6/7,
  with outer-lane limit 7)
  and a player one-lane inward/outward choice at each start/finish crossing.
- [x] Replace runtime grid-like track visuals with selected layout backgrounds,
  yellow corner masks, red apex masks, visible speed-limit labels, and
  editor-only node metadata.
- [x] Make ordinary tracks use the inside lane by default, move only the
  trailing car outside when cars share a node, and keep Indianapolis vehicle
  placement tied to the player's explicit lane choice.
- [x] Preserve authored corners, start/finish, and pit landmarks while
  re-sampling only the straight runs at equal arc-length intervals; add a
  numbered F8 runtime node overlay and a regression test covering all tracks.
- [x] Keep the background generator on the same 16:9 world-space sampling
  metric as `TrackDataLoader`; regenerate all eight official layouts and
  verify every sampled node remains on the painted road centerline.
- [x] Reconcile Monza corner metadata with the visible turning sections;
  relocate the seven corner groups off the former straight-only cells and
  regenerate its background.
- [x] Reconcile Shanghai, Indianapolis, and Nürburgring GP corner metadata
  with their painted turning sections; move the stale straight-road masks,
  regenerate the three backgrounds, and add regression coverage for the
  corrected node ranges.
- [x] Re-audit all eight selectable tracks in Play Mode with the numbered debug
  overlay; confirm runtime nodes, road centerlines, corner/apex masks and speed
  limit labels remain aligned with the selected background art.
- [x] Add turn-scoped manual camera control to the race map: left/middle drag,
  mouse-wheel zoom, moving-vehicle focus, player focus outside movement, and
  automatic-focus suspension after manual input until the next turn.
- [x] Detach the minimap camera from the moving main camera so the complete-track
  view remains fixed while the player pans, zooms, or follows another vehicle.
- [x] Tune race presentation pacing with 0.14s movement focus lead-in, 0.06s
  per-node pause and 0.10s focus trail-out; keep the authored ±15-cell window.
- [x] Add a configurable test assist that guarantees the China player's
  `cn-hotpot-base` ATTACK card is present in the opening hand while preserving
  hand size and card conservation.
- [x] Rotate vehicles to follow the tangent between track nodes (default sprite offset corrected to 0° for right-facing car art).
- [x] Integrate track weather-pool selection after the core track path is stable;
  profile effects now resolve through `WeatherRules`, with Play Mode multi-weather
 走查 remaining.

## P2 - Demo Asset Replacement

- [x] Reconcile the Phase 2 planning document with assets already completed; remaining gaps are listed in `design/planning/asset-manifest.md`.
- [ ] Finish remaining UI panel artwork.
- [x] Replace gear-button placeholders with arc-arranged circular stove-dial
  controls; China uses a two-position Recover/Go layout and selected gear uses
  the technology-blue state.
- [x] Replace the heat text placeholder with a ten-segment vertical thermometer;
  it counts real heat across all zones, excludes temporary heat from capacity,
  and marks the 50%/70% warning thresholds.
- [ ] Replace remaining flag/UI-node placeholders; track layouts and runtime corner masks are already integrated.
- [ ] Verify card and vehicle sprites in both scenes at target resolution.

## P3 - Feature Completion

- [ ] Tune and Play Mode-verify configured multi-AI races.
- [x] Implement slipstream rules and team range modifiers; broader balance remains open.
- [x] Implement team vehicle attributes and tech modifiers; balance review remains open.
- [x] Shuffle team trick cards into the normal deck lifecycle and replace batch
  hand submission with one-card select/confirm play, immediate trick resolution,
  and explicit end-of-card-phase behavior.
- [x] Implement the driver-selection flow as a catalog, session state, and
  runtime-built main-menu panel; connect the selected driver to race setup.
- [ ] Add sound effects.
- [ ] Complete Play Mode visual acceptance for card-play, linear vehicle movement, and spin-out
  animations; card transitions, per-node interpolation, and the 1-second/360-degree `RaceEventFX` cue
  are implemented, while the final combined visual walkthrough remains open.
- [x] Add weather gameplay after track data and race rules are stable; the five
  design profiles are now wired into limits, slipstream, cooling, spin-out and HUD.

## Open Decisions

- [ ] Select the replacement for the reverted Track Node Editor.
- [x] Treat Le Mans as a France expansion track with no home team; it is not one of the six national-team home circuits.
- [ ] Confirm whether Kanto Oden carry-over slots are mandatory (the current
  runtime behavior) or optional; Hotpot's additional slot is already optional.
- [ ] Decide the commit boundaries for current scene, data, MCP, and
  refactor changes.

## Completed Context

- [x] Main-menu and Race scene flow.
- [x] Chinese UI and Chinese font integration.
- [x] Card number and heat icon display.
- [x] Six national-team vehicle sprites.
- [x] Eight track-layout image prompts.
- [x] Unity MCP 10.1.0 package installed and connection verified.

## Maintenance Rule

Update this file whenever a task is completed, superseded, or blocked.
Historical Claude Code files under `.claude/agent-memory/` and
`production/session-logs/` remain provenance only; this task list is the
maintained source for current work.

## 2026-08-15 Tech Tree and Team Gear Audit

- [x] Audited the existing tech-tree rules/database against the design docs;
  common, unique and China EV node pools are now selected through one database API.
- [x] Added the main-menu tech-tree entry and runtime-built configuration UI;
  RP, permanent unlocks and active race selections persist per team.
- [x] Replaced the race's demo-only human tech state with the saved profile;
  AI opponents retain transient demo profiles so a race cannot mutate campaign RP.
- [x] Added the China Go/Recover pure gear module and a team-aware facade;
  player controls, AI selection, card limits, overclock heat and Recover cooling
  all use the same rules.
- [x] Added `TeamVehicleRules` as the boundary for team profile values and base
  durability/heat-pool setup; full movement/handling balancing remains a follow-up.
- [x] 历史记录：该切片完成时 Unity EditMode 307/307 通过；当前总回归已更新为 403/403。
  该历史切片的 dotnet build 当时为 0 错误。
- [ ] Continue extracting orchestration from `MVPGameManager` into phase services
  once the next feature requires changes across multiple phases.

## 2026-08-05 Module Audit

- [x] Audited the modules introduced by the previous AI integration commit.
- [x] Fixed temporary-heat card injection and AI effective card-slot handling.
- [x] Connected AI corner risk to lane-specific limits and active weather/tech modifiers.
- [x] Accepted the existing fixed-default weather tracks in the JSON validator.
- [x] Run Unity EditMode/Play Mode tests through the open Unity instance or MCP (EditMode 262 passed; no PlayMode tests configured).
- [x] Corrected Indianapolis lane winding so lane 0 is inside and limits rise from 4 (inside) to 7 (outside).
- [x] Completed a full static + runtime audit: corrected stale stage metadata,
  restored Race speed-card/heat icon references, and removed unsupported emoji
  glyphs from runtime UI labels.
- [x] Added the driver data slice from `foodula-1-drivers.md`: 12 profiles,
  XP thresholds, tier unlocks, UK active-use bonus, XP reward calculation,
  selection state, menu panel, and 5 EditMode regression tests.
- [x] Re-ran Unity EditMode tests after the audit and driver slice: 273/273
  passed with no failures or skips; dotnet build has 0 errors.
- [x] Fixed the card-play lifecycle regression: opening tricks are randomly
  drawn, confirmed tricks enter discard immediately, confirmed speed cards stay
  in the played area until cleanup, and optional discard accepts any non-heat
  card. Unity EditMode tests now pass 287/287; runtime smoke verified the
  seven-card opening hand, button states, hand/UI synchronization, and trick
  transfer to discard.
- [x] Completed the follow-up runtime audit and repaired cross-system card/heat
  ownership: exact runtime card instances are consumed atomically, temporary
  heat can no longer inflate the permanent engine pool, and AI heat payments
  use the same canonical path as human payments.
- [x] Fixed turn-start and movement edge cases: Kanto Oden carry-over is
  consumed even when the tech tree is disabled, Hotpot grants movement only
  when its optional ATTACK slot is actually used, and teleports immediately
  restore the car's track-tangent facing.
- [x] Hardened card UI state: reset clears every interaction mode, heat cards
  are non-interactable, resource displays refresh after card/heat changes, and
  a short action-button debounce prevents a physical double-click from both
  confirming a card and ending the phase. Gear controls are now interactable
  only while the human player is actively choosing a gear.
- [x] Replaced the placeholder race simulation assertions with an actual
  draw/pay/commit/move/cleanup/reshuffle loop and added exact-ownership,
  temporary-heat, Kanto, Hotpot, shared AI heat-payment, and orientation tests.
  Final verification: Unity EditMode 301/301 passed; runtime smoke covered
  main menu -> track selection -> Race, gear/card/discard/reset interaction,
  disabled heat-card input, and post-teleport orientation with a clean console.
  `dotnet build Foodula1.sln --no-restore` reports 0 errors (two existing MCP
  assembly-version warnings remain).

## 本次完成（2026-08-15 赛事回归与平衡）

- [x] 比赛 HUD 增加“返回主菜单”按钮；修复 RaceCanvas 预制体重建后的旧引用导致重复 HUD 的问题。
- [x] 所有主要菜单/比赛控制按钮统一接入短按压/释放缩放动画；运行态确认按钮存在且可触发场景切换。
- [x] 新增 `TrackTeamBalanceBenchmark`，覆盖 Resources 中全部赛道与六支车队，每图 12 场确定性比赛，报告写入 `design/balance/track-team-benchmark-2026-08-15.md`。
- [x] 平衡收敛：标准 AI 在预计抵达弯道时优先低值牌，风险窗口按预计移动量计算；中国队恢复设计案 Go 直道输出、操控从 -1 调为 0，并默认采用 Go→Go→Recover；美国直道加成调整为每回合固定 +1。
- [x] 最终验证：Unity EditMode 308/308 通过；`dotnet build Foodula1.sln --no-restore` 0 错误；MainMenu→Race→返回主菜单运行态冒烟通过，单一 HUD、按钮动画组件和控制台均正常。
