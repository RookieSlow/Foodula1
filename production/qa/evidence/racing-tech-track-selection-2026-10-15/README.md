# 地图选择页：简约赛车科技 UI

本包由用户明确优先，范围仅主菜单 `TrackSelectionUI` 地图选择弹层，保留8条正式赛道的 ID、顺序、中文名/国家/美食副题，以及点击即开始自由赛事和返回关闭语义。不新增第9赛道或二次确认。

## 基线

`baseline.json`：HEAD `58656899b0bb426b3df90ef2a564de4dfe7faf85`，41项已有 dirty/untracked 文件逐项SHA256和git状态；字体缓存、快进EditorSettings、旧音频/独立试听/暂存重复全部保留。`baseline-diff.txt` 保存原差异，`source-baseline.json` 与源码副本保存生产页、目录、会话状态、主菜单入口与公共旧样式。连续性完整读取版本的哈希对照和缺失治理文件见 `continuity-read-audit.json`，未制造缺失文件占位。

`baseline-tests.json`：改前定向MCP completed 20/20通过，失败/跳过0，1.0218658秒；根任务未arm该轮原生RunFinished，不能标作最终原生结果。

## 真实截图与隔离边界

`RacingTechTrackSelectionCapture` 调用真实生产 `TrackSelectionUI.Initialize/Show`，选项用原按钮事件、关闭用原返回按钮；生产MainMenuUI.Start不运行，回调只记录选项ID以防启动比赛。克隆原中文字体/材质为静态，独立preview scene/camera/RenderTexture，不保存主场景；finally恢复会话selectedTrackId、RenderTexture.active，清除owned按钮事件/脚本生命周期并销毁资源。

改前 `before/` 为1920×1080、2560×1440、1280×720三张默认状态，实际已全部查看，8条赛道均可见，manifest文字溢出0。改前颜色大块、居中卡片和粗面板仅作对照。1440p图片在读取工具缩为2048×1152显示，原文件尺寸保持。

首次改后30张全部实际查看，详见 `visual-review-initial30.json`。补足原生Selectable状态后最终重拍39张：三尺寸 × 默认/8条各选中/悬停/按下/禁用/关闭，manifest39条、0文字溢出。最终重拍实际重新查看24张：全部9张新增Selectable态，加三尺寸默认/最长GP/勒芒/北环/关闭15张。当前选择文字/左细线/底部摘要逐项对应，8条国家与美食副题及返回可见，无新增裁切遮挡。详见 `visual-review.json`，不冒称重读其余15张最终图。

Selectable状态通过原生 `DoStateTransition(instant:true)` 执行，因同步EditMode没有颜色Tween播放器循环；禁用另设真实interactable=false，manifest记录false。没有手填图片颜色，也没有触发这些夹具的点击开赛。首卡悬停/按下/禁用期间，勒芒的当前选择标记保持；该标记不当作EventSystem焦点。最终closed均overlayActive=false、visibleCards0、active labels0；首次枚举失活后代的统计已改activeInHierarchy。关闭图为空preview宿主的真实结果，不能当作完整主菜单恢复截图。

新装饰均不接收射线，原网格/字号/按钮回调保持；没有新增Update、全局订阅或逐帧资源加载。720p“点击开始”仍约10屏幕像素，保留紧凑文字边界，不称大字或整项目可访问性通过。

## 最终运行验证与边界

- `focused-33-native.json` 最终原生RunFinished定向33/33通过，失败/跳过/不确定0；此前31/31为加强前轮次。中间加强轮次 `focused-attempt-2-native.json` 为32/33、1失败，证据保留，不冒称一次通过。
- `full-interrupted-native.json` 为用户取消的中断轮次：已完成1094项均通过，但resultState为Failed(Child)并有Cancelled子项，不能据此声称全量通过。没有把计划中的地图2945项写成通过。
- 定向含8赛道原点击回调、重复初始化回调替换、返回/重开、遮罩射线阻挡与装饰无射线、原生Pointer路径/禁用门、有效文字对比至少4.5:1。原生夹具不启动完整Race场景，不能因此宣称完整开赛/所有菜单流程验收。
- 用户追加超车录音的音频原生定向40/40及组合最终全量 `full-2949-native.json` 独立归档于 `production/qa/evidence/overtake-audio-2026-10-15/`。最终原生RunFinished2949/2949通过，失败/跳过/不确定0，结束2026-10-15T06:33:14.3194297Z，512.8688699秒。结果覆盖地图与超车录音本轮最终程序集，不用地图定向替代音频结果。
- 根任务最终读取：MainMenu干净/isDirty=false，未播放/编译/导入，resource idle/ready=true，Free License=true。Console仅1条no_such_track缺失赛道测试预期error（TrackDataLoaderTest:192），无warning/新增运行错误；不是无日志。
- `final-hash-manifest.json` 保存相关源/截图/测试/连续性与组合全量证据SHA；41项本包开始前已有文件均存在且SHA一致，HEAD仍58656899。本轮没有commit/push，原字体/快进设置与旧音频保留。

没有把受控预览当作完整自由赛、教程、生涯视觉验收，也未声称整体UI升级完成。
