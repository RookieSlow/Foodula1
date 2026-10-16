# 共享操作确认弹窗：本轮证据

本包仅统一 `HUDUI.EnsureReturnToMenuConfirmationUI` 所有八类动作共享的确认层。保持620×300卡片、两个190×52按钮、原文案/按钮标签/事件/逐项确认设置与已有Space保护；HUD操作区和其他弹窗留后包。本轮不执行Git提交或推送。

## 基线与隔离

- 开始HEAD：`58656899b0bb426b3df90ef2a564de4dfe7faf85`。`baseline.json`记录完整展开的151项已有dirty/untracked文件SHA256；同时保留status、diffstat、name列表和原始diff。原有文件不得清理。
- `source-baseline.json`和五份source-before文本保存HUD、当前UX/清单及两份continuity原文；这些文件若在本包授权修改，收尾按本轮增量审查。
- 项目概览完整读取112540字符、38个有界页，详见`continuity-read-audit.json`。缺失active/governance只记录，不生成占位。
- Unity基线由root实际读取：MainMenu干净、EditMode空闲、许可证可用。Console已有一条`no_such_track` Error（负向赛道测试）及一条`On demand scheduler protocol error` Assert（空file/stack，来源未知）；未清Console，不将其归因UI或插件。

## 真实UI截图方法与当前状态

`RacingTechConfirmationCapture`在独立PreviewScene克隆真实RaceCanvas，显式调用HUD.Start及RequestInRaceAction，项目behaviour关闭，无比赛manager或场景导航。字体与材质静态克隆，settings current/repository在finally恢复。每个动作沿原HUD取消/确认handler验证回调次数0/1。

初次`before/`保留27张及manifest。夹具复查发现ButtonClickAnimation可能在Start附加声音监听，随后仅将夹具自己两个按钮重绑至原HUD handlers以隔离音频副作用；`before-isolated/`单独保存同27张，绝不覆盖初次证据。此隔离不改变生产UI或原事件实现。最终以隔离改前与after比较。

矩阵为三尺寸1920×1080、2560×1440、1280×720，八类原动作和一个明确合成的长中文/数字排版样本。After另外包含三尺寸的原生Selectable hover/pressed/disabled（instant DoStateTransition；禁用同时interactable=false），实拍36张；不会手填图片颜色伪造状态。

初次检查记录：实际查看初次before的ReturnToMenu三尺寸共3张：弹窗完整、两行退出后果正文清晰，manifest为620×300/190×52，零溢出。背景仅真实prefab静态上下文，未模拟整场运行，验收范围只共享modal；720继承文字缩放边界保留。

## 最终实际结果

- [x] `before-isolated/`27张、`after/`36张均已真实生成，manifest均0文字溢出，取消0/确认1回调断言均通过。初次before27张完整保留。
- [x] 本审查者实际查看改后全部36张：八动作×三尺寸24张、长中文/数字3张及hover/pressed/disabled9张；隔离改前ReturnToMenu+压力共6张，初次改前ReturnToMenu另3张。未用manifest代替看图。静态字体中文字形正常，无新增裁切/遮挡，正文后果与原动作标签保留；禁用实际interactable=false，标签raycast=false、遮罩raycast=true。三尺寸卡片/按钮仍620×300及190×52，布局未变。
- [x] 新程序集6组原生RunFinished：**77总数/77通过/0失败/0跳过/0不确定**，UTC2026-10-15T11:39:30.8703966Z，见`focused-77-native.json`。包含新增12项（其中1项原生生命周期用例）；覆盖八动作标签/取消重开单次提交/mask直达、原布局字体回调、模态射线、禁用、真实pointer/selected状态对比和原Space保护。2961仅发现用例数；本包未跑全量，历史2949不能作为本包全量结果。
- [x] 引擎审查：样式为单次Apply，无Update/全局事件订阅/每帧重建；复用已有divider，绝对ColorBlock配white基色避免暗色相乘，不新增资源加载、字体或第三方资产。禁用继续保留可读标签，动作语义不只靠颜色。
- [x] root最终实际状态：MainMenu干净/isDirty=false，未播放/编译/导入，ready/资源空闲及Free License可用；定向后Console实际0error/warning。基线旧Error及未知Assert仍按原记录保留，不能因此声称本包修复了其根因。root `git diff --check`退出0（仅换行提示），HEAD未变；HUD为4行插入/0删除。

本包已接入并完成上述受控视觉/原生交互验收。720p正文按既有缩放约12.7px，舒适大字、完整自由赛/教程/生涯逐场走查、其他操作按钮/卡牌/弹窗及整体UI主观验收仍未完成。不新增完整键盘/手柄或读屏承诺，不把静态夹具截图称为真实整场比赛运行。未commit/push，未恢复其他自动化。

`baseline-preservation.json`核对151项既有文件无丢失；仅本轮授权UX/清单/两份continuity增量可改变SHA，其余旧地图/音频/用户字体缓存与快进设置保持原样。`source-review.json`保留本轮源前后SHA与HUD增量；`final-hash-manifest.json`列出最终源、文档、截图与测试证据SHA（不哈希manifest自身）。