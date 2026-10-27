# 简约赛车科技 UI 屏幕验收清单

2026-10-14 建立。`[x]` 仅表示该项有实际证据；视觉/接入/测试互不替代。每轮只推进一个中等完整包；完成项无新问题不反复打磨。

## 首包：主题 token + 比赛数据条

- [x] 盘点当前入口与显示脚本；读取当前 UX/玩法；记录缺失规范。
- [x] 实际打开官方排名页并看浏览器截图；计时页受登录推广限制未看到计时塔。
- [x] 核验既有 Kenney 许可与开放字体候选，新增资产选原创 uGUI。
- [x] 有界主题/UX 规范完成并审查允许实现。
- [x] 改前真实 UI 截图及基线哈希存档。
- [x] 独立主题 token 接入 RaceTelemetryStripUI；不改预测数据与结算。
- [x] 新程序集编译完成，定向测试实际运行，记录总/通过/失败/跳过。
- [x] 改后 1920×1080 / 2560×1440 真实 UI 截图实际查看。
- [x] 较小窗口检查：1280×720 无新增裁切；原有等效约 8.7 像素文字偏小，未视作舒适可读性通过。
- [ ] 六队、长中文、大数字、正常/试算/预计/费用不足/输入关闭检查。
- [ ] 点击明细、关闭、射线穿透、卡牌/挡位原拖动、禁用与确认检查。
- [x] 最终 Console、场景 dirty/播放/编译状态与工作区差异记录。MainMenu 干净/idle/ready，仅保留缺失赛道测试预期日志。

2026-10-14 首包已接入数据条。初次定向 94/94；全量发现三项 AudioSource 生命周期异常，经过独立复现、两轮精确失败回归，补充停止已销毁声源、非当前单例解绑、失效场景回调自行解绑保护。最终定向原生 RunFinished 102/102（98 EditMode + 4 原生 Play Mode UI/音频），失败/跳过/不确定均 0；本轮新增 4 个主题和 5 个声音清理边界案例。三次全量失败和原生异常均保留，最终原生全量 2913/2913，失败/跳过/不确定 0。六队 × 三尺寸 × 实际/预计共 36 张改前和 36 张改后受控生产 RaceCanvas 预览，数据条各 72 条文字检查均无溢出。原生指针/拖杆/明细及选牌确认回归通过；完整物理卡牌拖动、额外长中文/极端数字截图及完整模式视觉仍未勾选。证据位于 `production/qa/evidence/racing-tech-ui-2026-10-14/`，周边默认记分牌数据不代表全 HUD 数据一致性验收。首包不是全屏视觉迁移完成。

## 第二包：HUD 外框与排名/状态

- [x] 完整读取本轮连续性文档及当前 UX；缺失 active.md/治理规范仅记录，不造占位。
- [x] 保存已有修改/未跟踪文件哈希、HEAD、改前源码和 36 张生产预制体受控截图。
- [x] 三块外层面板主题接入 authored/fallback 两条路径；赛道外观/外锚点、原按钮/字体和六队挡位保持。
- [x] 原排名 TMP 绑定接入局部滚动遮罩；原状态文字扩大，原圈数/名次/引擎/天气文字保持。
- [x] 六队 × 三尺寸 × 二/四/六/十二车，长中文和极端数字；90 张改后图、0 溢出，最终重拍实际审查 34 张，全部 18 张底部图第 12 名可见，滚动柄比例正确。
- [x] 真实射线/滚轮/滚动条拖动/原按钮点击、重复绑定、禁用/重启、原生 Play Mode 文本更新及滚动条比例/底部对齐通过。
- [x] 定向原生 85/85；加强后的 7 个新用例复跑 7/7，失败/跳过/不确定均 0。首次 69/71 的失败证据保留。
- [x] 本包最终全量 RunFinished 2920/2920，失败/跳过/不确定 0；MainMenu 干净、idle/ready，最终夹具重拍后 Console error/warning 0，基线哈希核对归档。
- [ ] 720p 舒适可读性、完整自由赛/教程/生涯视觉与全部卡牌/弹窗状态（本包未声称完成）。

本包证据：`production/qa/evidence/racing-tech-hud-shell-2026-10-14/README.md`。排名/状态属于部分迁移；侧边温度计、六队挡位内饰、卡牌/操作按钮及弹窗继续按顺序推进，不据本包勾选整屏完成。

## 第三包：温度计与六队挡位

- [x] 保存完整已有改动基线与相关源码；恢复对照 593 文件无缺失。
- [x] 有界 UX/视觉 gate；只迁移温度计与挡位内饰，徽章留后包。
- [x] 六队三尺寸待选/已确认/禁用改前 54 张，manifest 无文字溢出；受控生产预制体夹具明确标注。
- [x] 改前定向 83/83、失败/跳过 0（MCP completed，未 arm 原生 RunFinished）。
- [x] 改后54张/0溢出；实际查看34张（18待选、12张六队720p已锁/禁用、4张大尺寸代表），六队机构/文字状态/实际与预计分隔无新增裁切。720p小字不视为舒适可读性通过。
- [x] 原生定向102/102，失败/跳过/不确定0；首次95/102失败记录保留，未拿旧结果替代最终复跑。
- [x] 新程序集原生定向102/102、UI阶段全量2931/2931，失败/跳过/不确定0；之后CC0音频整改须独立复验。
- [x] 改后54张、最终哈希与两份连续性同步；MainMenu干净/idle/ready，Console仅缺失赛道测试预期error，无warning。
- [x] 另行授权CC0换挡整改原生41/41；最终全量2932/2932，失败/跳过/不确定0，旧音频与用户设置保留且提交排除清单已归档。

证据：`production/qa/evidence/racing-tech-instruments-2026-10-14/README.md`。本包不声称完整模式或720p舒适可读性完成，剩余页面继续按有限顺序推进。

## 用户优先包：地图选择页

- [x] 保存HEAD58656899、41项原有改动和真实页面源码基线；用户原字体/快进设置/旧音频保留。
- [x] 确认真实目录8条，点击即开始与返回关闭语义；未构造第9赛道或额外确认。
- [x] 三尺寸改前真实生产入口预览3张已全部查看，8卡可见、0文字溢出；定向MCP completed20/20，非native。
- [x] 首次30张全部查看；最终39张/0溢出，重审24张（9原生悬停/按下/禁用+三尺寸默认/GP/勒芒/北环/关闭），无新增裁切；closed active卡/标签0，不冒充主菜单恢复。
- [x] 最终原生定向33/33，失败/跳过/不确定0，含原生点击/关闭/重开/禁用及有效文字对比检查。
- [x] 原生交互/生命周期及组合最终全量2949/2949，失败/跳过/不确定0；中断轮次1094不是通过，未冒称地图2945已完成。
- [x] MainMenu干净/idle/ready/Free License可用；Console仅缺失赛道测试预期error，无warning或新增运行错误。
- [x] 最终哈希、许可与两份连续性同步；41原文件SHA不变，剩余页面/完整模式边界保留。

证据：`production/qa/evidence/racing-tech-track-selection-2026-10-15/README.md`。关闭夹具为空独立宿主，不冒称完整主菜单画面。

## 第五包：比赛共享操作确认弹窗

- [x] 本轮完整分页读取game-mechanics1–789，首尾SHA256一致；当前UX/清单/交互与确认源码对照完成。
- [x] 有界UX/视觉规范gate通过，仅共享确认层；八动作/原双按钮标签、mask与Space保护保持，无新交互/资源。
- [x] 改前基线及三尺寸明确受控生产夹具截图保存；UX实际查看隔离改前3张，初次before另外保留。
- [x] 独立主题适配器接入构造/复用路径；原layout/font/material/listeners/navigation及其他弹窗保持；仅两Button允许ColorTint。
- [x] 八动作三尺寸/压力及状态共36张改后、manifest溢出0；UX实际查看16张覆盖八动作/三尺寸/压力/hover/pressed/disabled，无新增裁切/遮挡，不冒称全部目视审查。
- [x] normal/highlighted/pressed/selected/disabled有效文字对比≥4.5:1实际回归，原语义标签与动作位置保持。
- [x] 模态阻挡、取消无回调、确认一次、mask关闭直达、Space普通动作/Reset/Menu保护、禁用/重开及重复Ensure实际验证；不替代完整模式/OS输入验收。
- [x] 新程序集实际定向原生77/77，失败/跳过/不确定0，UTC11:39:30.8703966Z；2961仅发现数，历史2949不计本轮。纯视觉包本轮不强制全量，阶段收口/核心风险时再安排。
- [x] 最终root核验MainMenu干净、未播放/编译/导入、ready/资源空闲与Free License可用；定向后Console实际0error/warning（不称已修复旧Error/未知Assert）。77/77原生定向与两份continuity同步，151基线0missing、仅4份授权UX/continuity增量；HUD独立源快照确认4行插入。git diff --check退出0、HEAD58656899未变，final-hash-manifest.json归档；本包未全量。

此包只共享确认弹窗；操作主按钮、卡牌及其他明细/决策/赛果未因此完成。完整模式走查、720p舒适可读性与主观最终验收独立保留。

证据：`production/qa/evidence/racing-tech-actions-2026-10-15/README.md`。UX/视觉/引擎有界审查APPROVED；720p正文仍约12.7屏幕像素，不因零溢出勾选舒适度。

## 第六包：五个比赛主操作按钮

- [x] 本轮完整分页重读game-mechanics1–789、首尾hash一致；动态技能/出牌文案及原禁用/确认偏好核对。
- [x] UX/视觉gate通过，确认换挡/技能/重开/返回/出牌五目标；不扩gear1–4、赛果、卡牌或弹窗。
- [x] 根任务保存276文件基线及原文快照；改前36图/0溢出，UX实际查看三尺寸代表3张。
- [x] 独立主题接入authored/fallback两路径，只有五目标替换旧Modern样式；原布局/字体材质/事件/门控保持，play覆盖禁用confirm的原active/interactable/层级不改，不新增互斥开关。
- [x] 改后60图/0溢出，UX实际查看20张覆盖六队/三尺寸/真实缺3→+3热/五动态组及hover/pressed/disabled；独占inactive管理器/纯状态夹具明确非整个GameLoop，不冒称全状态图审。
- [x] 原生正常/hover/pressed/selected/disabled有效对比≥4.5:1；所看长技能原因/次数/回合与缺牌热量原文保留，不仅靠颜色表达。
- [x] 相关定向验证pointer/原叠放、确认取消/重开、禁用无回调、原标签刷新；原生防双击用custom0.05秒恢复，生产默认0.25秒未改，不称测过默认延时。
- [ ] “可能失控”风险标签及selected专门截图未覆盖；selected有pointer/Selectable测试，原formatter规则测试保持，不冒称完整视觉验收。
- [x] 本轮六组定向原生61/61，失败/跳过/不确定0，UTC17:05:32.1895263Z；2969仅发现总数，未跑全量，不挪上包77或历史全量。
- [x] 最终MainMenu干净/未播放编译导入、idle/ready/许可有效、Console0error/warning；276基线0missing、272SHA一致、仅4授权UX/continuity增量。两continuity与README同步，源/证据SHA清单归档，HEAD58656899未变、diffcheck0；未全量/commit/push。

只五按钮主题；六队输入、卡牌、其他弹窗/菜单/赛果及完整模式/720p舒适度未因此完成。

## 有限迁移顺序与现有屏幕

| 顺序 | 现有界面 / 责任脚本 | 接入 | 实际视觉验收 |
|---|---|---|---|
| 1 | 公共主题、按钮/面板：ModernUIStyle / RaceUIFactory | [ ] | [ ] |
| 1 | 位移/热量条样板：RaceTelemetryStripUI | [x] | [x] 三尺寸受控；720p 舒适可读性待改善 |
| 1 | 侧边温度计：HeatThermometerUI | [x] | [x] 六队三尺寸受控；720p小字边界保留 |
| 2 | 排名/圈数/回合/状态：HUDUI / RaceHudTextRules / RaceCarBadgeUI | 部分：RacingTechHudShellUI 接入排名/统计/状态，徽章待迁移 | 部分：六队三尺寸 4/12 车受控，完整模式待验 |
| 2 | 六队挡位：GearDialPresentationUI / TeamGearPresentationUI / TeamGearGraphic / TeamGearDragHandleUI | [x] 现用Presentation/Graphic样式 | [x] 六队三尺寸待选/锁定/禁用受控；完整模式待验 |
| 3 | 卡牌/牌堆/操作：CardUI / CardHandUI / CardPilePreviewUI / RaceUIFactory | [ ] | [ ] |
| 3 | 明细、确认、维修区、车道弹窗：HUDUI 现有构造入口 | [ ] | [ ] |
| 4 | 主菜单：MainMenuUI | [ ] | [ ] |
| 4 | 车手/名单：DriverSelectionUI / FreeRaceRosterUI | [ ] | [ ] |
| 4 | 赛道：TrackSelectionUI | [x] 用户优先接入 | [x] 三尺寸受控39状态图；完整开赛模式待验 |
| 5 | 设置/百科：GameSettingsUI / GameEncyclopediaUI | [ ] | [ ] |
| 5 | 科技/生涯科技：TechTreeUI / CareerTechTreeUI | [ ] | [ ] |
| 5 | 生涯：CareerModeUI / CareerMenuPresentation | [ ] | [ ] |
| 5 | 教程选择/指引：TutorialSelectionUI / TutorialGuideUI / TutorialFocusHighlightUI | [ ] | [ ] |
| 5 | 赛果：HUDUI / RaceResultPresentationRules | [ ] | [ ] |

## 各阶段统一验收

- [ ] 既有参赛车数边界（0/1/多车/最大名单按实际支持配置）、长中文及数字无裁切遮挡。
- [ ] 自由赛/教程/生涯相关页面与显示状态保持；不修改规则或存档。
- [ ] 常态、悬停、按下、待选、已确认、禁用、错误/费用不足有非颜色表达。
- [ ] 中文字体、原键盘/鼠标/拖动与模态层阻挡保持。
- [ ] 无新增不受减少动态设置约束的动效、无闪烁。
- [x] 首包安全状态下全量回归实际运行：原生 2913/2913；后续迁移阶段仍须重新验证。
- [x] 本包许可清单覆盖来源；无新增第三方下载，候选/仅参考资源明确标注。后续每包仍须逐项核验。
- [ ] 无明显每帧重建/资源加载/入口性能退化，记录检查边界。

全部现有屏幕接入、交互/模式及规定截图审查通过后，仅报告“UI升级已完成，等待用户最终主观验收”，停止扩张。

## 第七包：牌桌四区域壳与计数（已接入，独立补审已完成）

- [x] 本轮完整分页重读game-mechanics1–789并核对首尾SHA；当前UX/清单/interaction与相关core/visual规范、动态牌堆入口对照。
- [x] 根任务保存411项基线及11份相关源码快照；有界UX/视觉/引擎约束gate允许实现，仅四区域底板/标题/统计/计数与空堆提示。
- [x] authored/fallback及动态CountBadge/EmptyLabel/InspectHint主题真实接入，原CardUI/缩略图/叠背/检查器/布局/字体/输入与计数规则保持。
- [x] 三尺寸改前27/54标题溢出、改后27/0；根任务实际查看after9/before2，另审查者before6；六队、可见speed/heat、空/多牌堆、风险及selected已覆盖。特技、大数字、pile hover/pressed专门图未覆盖。
- [x] 原formatter真实“可能失控”长风险及selected截图补证；改前风险两行完整无overflow，按钮布局不改，不删字/缩字/改规则。
- [x] 精确count/空堆/层数/原卡面、检查器点击/禁用/射线及重复配置、最终有效对比实际定向通过，记录本轮总/通过/失败/跳过/不确定数。
- [x] 本轮Unity/Console/基线差异与证据同步；未冒称完整模式、卡面/详情主题、720p舒适度或全部UI完成。

本包无新第三方资源，沿用已核验现有卡面与字体及原创uGUI几何；上一包61/61与历史全量均不计本轮结果。CardUI既有减少动态支持缺口独立记录，本包不扩改。

- [x] 改前27图manifest确认仅HandTitle/DeckTableTitle高度不足共54项；UX实际查看CN1080七牌及CN720失控风险2张，允许两标题向下最小补高，其他文字/几何排除。
- [x] 两标题改后无overflow、顶部/anchors/字体保持、已有足高不改及重复应用幂等通过；三尺寸实际检查不碰牌面/分隔线。

- [x] 原生定向59/59通过，失败/跳过/不确定0，2.5227451秒；2977只发现数，未全量，证据在card-zones本包。
- [x] 独立改后UX/视觉补审已于本轮完成，§19.1实际9图有限APPROVED；旧证据冻结，未覆盖状态/720p舒适度保留。

## 第八包：牌堆详情弹窗（进行中）

- [x] 本轮game-mechanics1–789完整分页重读、首尾SHA一致；当前UX/清单/interaction全文及相关GDD/实际详情构造对照，缺规范仅记录。
- [x] expanded514基线READY后追加§19；最小详情主题UX/视觉gate批准，旧包冻结记录不改。
- [x] 独立补审上包after9张覆盖六队/三尺寸/空/失控/selected，27记录0overflow；结论与未看状态在§19.1记录，不称全部27图目视通过。
- [x] before21图/0文字溢出，独立看三尺寸代表3张；真实GetEventHandler证明正文/Panel解析Overlay dismiss，批准仅Panel空Button挡板（无监听/TransitionNone/NavigationNone），尚未派发或冒称修复通过。
- [x] 仅详情层/原Close与行文字主题真实接入，独立源码审查APPROVED；原MiniCard/Glyph/数据排序/布局/字体及Close/Overlay配置保持。
- [x] before21/after24、文字溢出均0；独立实际看before3/after10，三尺寸抽弃top/bottom/原最长Go全文/Close hoverpresseddisabled，有限视觉gate APPROVED；未拍selected/极端大数字、未逐审全六队效果保留。
- [x] 根任务本轮原生39/39，失败/跳过/不确定0，UTC23:13:44.9013542Z、2.1340991秒，四组含新增7项/1native；2984只发现数未全量，不复用历史结果。
- [x] 最终MainMenu干净/未播放编译导入、idle/ready/许可有效、Console0error/warning；514基线0missing、509SHA一致、仅5份授权源/UX/continuity增量，两continuity及README/原生39/39与SHA清单已归档，无新资源许可缺口或不实全面验收。

- [x] after真实路由正文→Panel/Close→Close；原生内容点击不关/底层0、滚轮末行/Close关闭重开/背景关闭、重复样式保持滚位和几何。原黑.72遮罩/Close配置保持；未据此声称OS物理拖动通过。

本包独立UX/视觉/引擎有限gate APPROVED；720p舒适度、miniCard/Glyph原对比、完整模式/物理输入及未拍状态仍未验。新原生证据与最终状态/基线归capture，最后一项待归档核对后勾选。

## 第九包：选中牌效果面板（已接入，有限验收通过）

- [x] 602文件基线READY后写§20；本轮完整分页mechanics1–789及当前UX/清单/interaction、相关GDD/原EnsureShowHide与formatter核对，缺治理仅记录。
- [x] team-ui lean＋quick-design Tweak有界UX/视觉/引擎约束gate；只效果条及真实截断后的局部阅读修复，不扩卡面/玩法/输入或旧包。
- [x] before45/0overflow、全文/原文/clear/reopen均45/45、truncated0；UX实际看三尺寸3张，未见截断。最终锁定仅主题，不wrap/扩高/改几何，原Ellipsis保留；普通heat为privateShow诊断非新增可选操作。
- [x] 原Ensure构造/已有引用复用两处主题接入；原Show/Hide/formatter富文本/BBQ与选择生命周期不改，几何/15号/nowrap/Ellipsis保持。
- [x] before/after各45图0overflow，全文/原文/clear/reopen均45true、truncated0、rawText前后0差异；独立after12/before3图审覆盖六队/三尺寸/BBQ/速度/普通heat诊断/冰糕discard。未全45图目视、无单独clear截图，边界见§20.5。
- [x] 本轮实际读取focused-56-native.json原生56/56，失败/跳过/不确定0，UTC04:07:37.1394358Z；四组含新6/1native，root记录3.2094577秒。原生输入/生命周期/射线与对比通过，旧结果未复用；本轮新全量2990/2990已完成，见下项。
- [x] 实际读本轮full-2990-native.json，原生2990/2990、失败/跳过/不确定0，UTC04:19:55.0543520Z；root记录710.7150231秒。最终MainMenu干净、未播放编译更新、许可有效、idle/ready/testfalse；Console仅no_such_track负向预期1error，warning0，无新增runtimeerror。
- [x] 最终基线/continuity与Git前范围审计完成；实际推送由root核验，不预称Git已完成。本切片与自动回归通过不等于整套UI完成。

## I00b 哈特费用选择（有限完成）

- [x] 选牌沿原确认层/中文字体，必选费用不被mask0旁路，普通窗620×300几何还原。
- [x] 精确同值实例、目标/收益/次数、“弃置并发动”、取消/重开、Space单次提交与禁用原因。
- [x] 改前3、首after15溢出保留；最小中标签扩72后最终15图/0溢出，根任务全看，独立10图有限APPROVED。
- [x] 原生指针/遮罩底层点击阻断、合法焦点恢复及真正Play隐藏/禁用/销毁取消；新生命周期不重入SetActive，不留下透明射线阻断。
- [x] 新程序集最终七模块2009/2009、22.5163197秒，失败/跳过/不确定0，清单分组及失败记录保留；不是全量/完整比赛通过。
- [ ] 720p舒适度、真实鼠标/拖动、hover/pressed及完整自由赛/教程/生涯组合留后续I13；UK科技/被动和其他车手未由本弹窗验收。

证据 production/qa/evidence/tech-driver-I00b-2026-10-22/README.md；没有覆盖Word用户原件或已有历史证据。


## 2026-10-23 I01a 英国预备餐：生产接入与有限回归

- [x] UK L1 稳定节点 ID 下改为每车每圈弃1普通速度，旧永久热 H优先/D其次最多1、UK/GB+L3最多2；退役与净回分开，取消/过期不扣，旧不足免单移除。A2早餐后冻结，A4首次正式出牌前使用；保留原A1→A2顺序。
- [x] 独立科技按钮与车手按钮在A4/A1互斥使用动作槽；真实按钮→选择→确认/取消通过4项受控pointer测试，mask0/All一致。新规则21例，合计25新增。
- [x] 本轮原生2034/2034，失败0/跳过0，21.3845409s；首轮2030例有5失败（旧预期4、夹具缺config1），已保留失败证据并修正。非全量、非完整比赛。
- [ ] 成功退役净回、多UK与Hart费用组合、完整A2/A4实跑、focus/drag及独立复审待补；I01整体未完成，后续Hart被动/早餐矩阵仍未提前标完成。
- 证据 production/qa/evidence/tech-driver-I01a-2026-10-22/README.md。Unity最终MainMenu干净/idle，Console0error、既有cardPrefab测试警告1；Word原件和草案未改，未提交推送。

I01a 后续独立代码复审已 APPROVED、无必须修复项；视觉/QA与上列残余场景仍未验收，见 evidence/tech-driver-I01a-2026-10-22/review.json。


- [ ] 2026-10-24 I01c：成功正式速度提交后立即显示预备餐禁用；非法提交仍可用。源码与2项用例独立编译通过，原生与三尺寸截图未执行，不能勾选。


### 2026-10-24 I01d：费用模态焦点隔离与原生补验

费用选择打开后移焦取消，四按钮使用窗内Explicit导航；翻页禁用当前箭头前保存焦点，处理Unity自动清空选择，关闭恢复原Navigation及合法原焦点。新增8项真实EventSystem Move/Submit回归；沿用既有ui/core夹具登记，无新夹具。首轮2363总/2360过/3失败/0跳过、12.8776254s：焦点1项、缺TrackManager的旧GameLoop时钟夹具2项。生产修复焦点，夹具补20节点，未弱化玩法断言。

刷新并核对新runtime/editor MVID后，ui/tech/drivers/cards-heat/core/tracks-weather-pit原生RunFinished 2363/2363、0失败/跳过/不确定、12.1640629s，selectionMatches=true；包含I01b36项与I01c2项。本轮只定向，不是全量、完整赛事或模式验收。生产受控RaceCanvas三尺寸15张新after图/0溢出，root实际查看6张；没有本轮改前新图，不把历史I00b图冒充新before，也不据静态图验收真实拖动、六队/参赛车数/模式。

Unity最后MainMenu/全部loaded scene干净、未播放/编译/导入/测试占用；许可证为Personal。Console一个no_such_track预期负面测试error（LogAssert.Expect），无编译错误；未清Console。editor/state资源仍stale_status，记录直接实时查询与原生防护，不伪称ready缓存为true。I01仍进行中。证据 production/qa/evidence/tech-driver-I01d-2026-10-24/README.md；下一包I01e为真实Manager尾流协程与A2/A4环路及剩余输入/视觉证据，不重复已过模块。


### 2026-10-24 I01e：哈特新增位移的真实弯心费用

按Word共用路径边界补生产接线：主动只纳入去掉Hart后到不了的尾端弯心，角速仍为原速度牌总值；与原card角同一次普通结算，物理弯段去重、跨圈再经过仍收费。新增弯心在最终线前时，以本次协程局部状态延后最终完赛登记，费用不足仍部分支付/失控、不退技能次数，也不能凭失控前计划路径预约进站/领地标。被动保留两原生段后独立实际1格，真实格数先消耗原圈额度，再按原角速付新增弯心费用；原生尾流及其他车手既有路径不扩大角判。主动预测使用同范围/副本支付，不花真实牌/热/次数。

新增16项：12规则/预测参数案例，4真实原生协程案例。首轮2168/2168、0失败/跳过、285.8493526s，含19长流程，但在最后失控路线门控前，保留版本边界。最后修订MVID已确认，五个受影响模块加5个精确原生案例（4新+既有六队两回合）RunFinished2154/2154、0失败/跳过/不确定、10.3944555s，selectionMatches=true。不是全量，也不是最后版本重新跑19长流程。新增partial沿用RaceGameLoopCoroutineTests既有long-races归属，无新增fixture、无清单漂移。

真实GameLoop验证A2早餐后冻旧永久热、A4主场回2且保留早餐临时热、正式速度提交关窗；主动完整A1/A4→移动→角费→finish/pit；被动是生产尾流结算适配器协程，非完整回合；中断项是局部finish-gate生命周期，不冒充完整Hart GameLoop退出。Unity最终MainMenu/loaded scenes无dirty、未播放/编译/导入/测试占用、Personal；Console仅no_such_track预期负面测试error。ready资源仍stale_status，以直接实时查询/原生安全门和RunFinished为据。初次编译3个Action缺using错误已改全限定名，未在旧程序集上跑通过；MCP会话404重新初始化，无配置/服务安装修改。

无新UI布局或截图；新角费风险提示、真实改前后三尺寸操作仍待I01f，不复用历史图冒充本轮视觉验收。I01仍进行中，I02-I13不提前勾选；下一包有限补提示及操作，通过后推进I02。证据 production/qa/evidence/tech-driver-I01e-2026-10-24/README.md。

### 2026-10-25 I01f 有界确认窗验收

- [x] 真实HUD按钮→MVP计划固定目标/费用/收益/余次/新增弯心付热和失控风险；mask0/All均不可旁路费用。
- [x] 三尺寸改前18/最终改后18逐张查看；长中文数字、六队目标、2/4/6/12 roster，0溢出；按钮/索引/禁用原因均有文字。
- [x] EventSystem/GraphicRaycaster 实际几何点击、窗内Move/Submit、取消/重开、同值第二原实例、遮罩与合法焦点返回；36新增。既有gear拖动7例随1394/1394通过，非Hart新增拖动机制。
- [ ] 完整RaceCanvas布局、OS硬件输入、完整赛事/生涯/教程与存档组合、720p主观舒适度：I13继续验收，不能据本包勾选。

1394总/1394过/0失败/0跳过/0不确定，10.1942977秒；新程序集与有限独立复审通过。证据 production/qa/evidence/tech-driver-I01f-2026-10-24/README.md。I02为下一实装包。


### 2026-10-27 I02d 曼森操作接线（待验）

- [x] 源码：首段必选原实例、续段可选热确认，统一取消/焦点/禁用原因和余次，不改布局主题。
- [ ] Unity新程序集导入/加载及72个新增参数组合原生运行。离线编译不替代此项。
- [ ] 1920×1080、2560×1440、1280×720实际截图/检查：长中文数字、六队目标/参赛车数、按钮/费用/取消/焦点/遮罩及既有拖动。
- [ ] 本包没有有效改前图；Capture(before)禁止执行，不能用后拍新UI伪造前图。
- [ ] 完整RaceCanvas、硬件输入、完整模式及存档兼容仍归I13。

Unity MCP拒连，未操作Play/保存场景；Console/ready当前未知，未将旧状态当本轮通过。见 production/qa/evidence/tech-driver-I02d-2026-10-26/README.md。


### 2026-10-27 I02e 有界操作验收

- [x] Unity新runtime实际载入；原生六模块2076/2076、0失败/跳过、22.5495703s，含曼森72参数组合和9项既有拖动相关案例。
- [x] 三尺寸各12图，修复前36/后36逐张查看，后0条overflow；目标/费用/收益/余次、禁用文字原因、选择/取消/确认可读无字形碰撞。
- [x] 首/续段费用、mask0/All、最后一次续段、重复入口、同值原实例、键盘/焦点、遮罩、改挡/退出/过期取消以原生事件断言验证。
- [ ] 原I02d旧UI前图仍缺，不能拿当前新UI回填；本轮仅I02e修复前后有效。36对PNG相同，因为改的是容器边界而非字形布局。
- [ ] 完整RaceCanvas/硬件/完整模式/存档和720p主观舒适度保持I13未验。

I02有界交接到I03；证据 production/qa/evidence/tech-driver-I02e-2026-10-27/README.md。

I02e收口另在当前程序集实际重跑3项GameLoop（两段续追、尾端角费部分支付/失控、A2旧热来源），3/3、0失败/跳过、3.1201921秒，原生RunFinished确认。最终Console0错误/0警告，MainMenu干净；不是全量或长比赛。


### 2026-10-27 I03d：真人黑啤L1实际HUD与取消→出牌→移动（L3仍待）

A4新增GermanFuelActionUI，沿阶段独占的原driver action slot，按钮显示本圈余次、费用/收益、缺热/已用/待确认文字。HUD直接路由Manager版本化固定费计划；沿CardAction确认开关，开启时共用一次可取消确认、关闭时直接提交。确认正文包含本人长名、永久引擎热→弃牌、+2与后续普通角费/失控风险，不弃速度牌；专用无牌文案不误写第二段。支付后焦点可回有效出牌按钮，模态/详情阻止底层点击，显式回调/退出取消旧请求。

真人ComputeMovements与预测不再自动购买黑啤，只汇总已确认付款的+2；普通AI科技自动策略保留，不顺带启用AI车手技能。取消/过期后，经真实ConfirmPlayerSpeedCards和ComputeMovements不再扣热。已付黑啤但尚未选速度牌时也显示预测+2，并以复制的skip/kanto/terminal标志防止重新激活跳过或终态；实际缺牌罚热沿旧普通支付，不退款。

最终新runtime be928c88-1478-48b7-b060-8cf4cb7beeb2、editor b3999214-a847-40c7-b71b-207be910dba6实际加载。tech/drivers/cards-heat/core/gears/ui/tutorial/tracks-weather-pit原生 **3087总/3087过/0失败/0跳过/0不确定，32.6400771秒，selectionMatches=true**。新增36 HUD参数（3尺寸、mask0/All、两德国现有车手、六队周边/2/4/6/12参赛车、长中文数字、pointer/Submit、取消/重开/禁用/失效/显式OnDisable、遮罩/焦点、真实出牌→移动）登记ui；5新增空选择已付燃料预测/缺牌付款/实际移动对照沿原tracks-weather-pit。既有拖动随模块回归通过，非本包新增拖动机制。另最终程序集实际Mansell GameLoop3/3，0失败/跳过，4.1799126秒，native root确认；不是德国完整手动GameLoop、长赛或全量。

原生首轮3082总/3062过/20失败/0跳过，32.5160696秒，暴露A4焦点返回缺失、EditMode未触发原生OnDisable及旧auto预期；修复焦点/明确调用生产回调/校正人机策略断言后3082/3082，随后强化真实正式出牌、修复独立审查发现的已付空选择预测，最终3087为准。初始CS0103已改System.Math，未拿旧程序集通过。引擎最终只读有界批准；并行UI/QA代理早期额度失败未写文件，父任务实作与实际验收，不声称独立UX/QA全审通过。

三尺寸有效改前24图（run-20261027-040534023）与改后24图（run-20261027-041640341），已查看所有动作/模态区域，另查看三张最终全帧，前后均0溢出。真实HUD.Start/生产widget/Manager计划置于自有预览Canvas，操作位沿生产归一化尺寸；周边自有背景，非完整RaceCanvas或硬件键盘。初次错误GearButtons夹具产生216个标签溢出记录的旧图完整保留但不验收；修好夹具后在生产改动前重拍。改后加入自有出牌焦点目标，背景差异不当生产布局变更。详见production/qa/evidence/tech-driver-I03d-2026-10-27/README.md及绝对截图目录。

最终MainMenu全部loaded scene干净、未播放/编译/导入/测试，许可证已解析，Console0错误/0警告未清；ready资源stale_status，以直接实时状态/原生RunFinished为据。8 Word/32场景、所有既有改动/未跟踪/排除文件保留，无提交推送、清理或系统/快速播放设置修改。

**I03仍进行中：L3声明/取消实际按钮、与A1车手技能同屏共存、三次合计操作、真人旧自动L3返热迁移及净回/退役反馈待I03e。** 本包只接完真人L1有界操作，不提前进I04；完整RaceCanvas、硬件输入、完整比赛/模式/存档与720p主观舒适度留I13。


### 2026-10-27 I03e 有界验证

A1德国L3声明/取消与driver分槽、A4燃料与未付取消/锁定禁用、三次合计、现有确认遮罩/焦点已接生产。GermanBurstProductionUiTests48参数登记ui、GermanBurstManagerTests8登记tech；新增2条actual GameLoop沿既有long-races。十模块3403/3403（43.9000283秒），GameLoop2/2（2.9225509秒），均0失败/跳过。前后各24张三尺寸受控Canvas图已查看，0溢出；后摄影机改正交修复EditMode透视RT检测异常，仍走真实Raycaster，不能当完整RaceCanvas/硬件验收。前A1旧自有底色对比不足不通过；terminal/exit图在清理前，资源断言在之后，日志净回/退役未视觉渲染。I03有界交付进入I04，完整模式/存档/硬件留I13。详见production/qa/evidence/tech-driver-I03e-2026-10-27/README.md，含截图绝对路径、首轮25失败与后续16夹具失败修复证据。


### I04a 2026-10-27 舒马赫被动有界验证

真实普通弯道付款调校已接线；42规则、24适配器、3车手页案例登记drivers/ui，1新增实际GameLoop沿long-races。最终11模块3484/3484（40.2925337秒）及实际GameLoop3/3（4.2840327秒），均0失败/跳过。被动摘要更新且仅D03字号12..14适配；三尺寸改前后各3图全部查看，本次文字0overflow；全页每尺寸14个既有其他文字问题保留，不算全页UI/硬件/完整模式验收。首轮3失败即本次新摘要溢出，已修；pitScheduled预测复制遗漏静态修复并加2例。I04主动/维特尔待做，详见production/qa/evidence/tech-driver-I04a-2026-10-27/README.md（绝对截图/程序集/原生结果/边界）。
