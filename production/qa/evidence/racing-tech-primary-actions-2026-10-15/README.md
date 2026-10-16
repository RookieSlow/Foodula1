# 第六包：五个比赛主操作按钮

本包已将确认换挡、车手技能、重开、返回主菜单及出牌五个原Button接入简约赛车科技主题。RaceUILayoutController authored/fallback两路径共用独立RacingTechActionButtonStyle，移除五目标旧彩色Sprite/厚阴影，主提交深蓝、辅助Raised、清晰白字。HUDUI、CardHandUI、已完成共享modal、gear1–4及赛果backToMenu均不改；不改变卡牌/技能/确认偏好、键盘/鼠标或存档规则。

## 实际证据

- [x] 本包基线：HEAD `58656899b0bb426b3df90ef2a564de4dfe7faf85`；276项dirty/untracked展开到文件逐项SHA256，原始status/diff/stat/names及8份涉及源码/UX/连续性原文副本保留。缺active/governance只记录。project-overview完整重读113677字符/38页，首尾哈希一致，见continuity-read-audit.json。
- [x] 实拍 `before/`36张、`after/`60张，两个manifest范围内文字溢出均0。三尺寸为1920×1080、2560×1440、1280×720；六队gear/card基本矩阵36张，after另原生hover/pressed/disabled9张及原ShowHand/选中/技能动态标签15张。状态没有手填图片颜色，使用Selectable原DoStateTransition即时态；selected颜色由原生pointer测试覆盖，无专门selected截图。
- [x] 本证据审查者实际查看 **after40张、before3张**：六队720p gear/card12张；CN三尺寸5个动态标签+3个Selectable态24张；CN1080/1440 gear/card4张。改前查看CN三尺寸card3张。UX及root各自独立查看数量不相加成未去重总数。无新增裁切/遮挡；技能名称/Lv3原因/仅档位前发动/×1/2回合，以及单速/多速/特技/弃牌/缺3→+3热保持真实原文。720p技能长标签原换行为两行，尺寸/字号不变，舒适可读性仍待验。
- [x] 原生RunFinished六组 **61总数/61通过/0失败/0跳过/0不确定**，UTC2026-10-15T17:05:32.1895263Z（本地次日），MCP实际3.2888873秒，见focused-native.json及focused-summary.json。包含新增8例（1原生）；authored/fallback五目标接入、排除其他按钮、原几何字体材质/回调/导航/状态、重复应用、原生pointer有效对比≥4.5:1及禁用拒绝、原确认取消/单次提交均实际运行。防双击用例实测**自定义0.05秒**恢复；生产默认**0.25秒源码保持未改**，未将其写成实测默认耗时。
- [x] 未重跑全量；2969只是本轮发现数，上包77和历史2949不是本轮全量通过证据。完整GameLoop、整场自由赛/教程/生涯及OS物理输入未由本夹具代替。

## 夹具与真实数据边界

新RacingTechPrimaryActionsCapture在独立PreviewScene克隆真实RaceCanvas，经原HUD.Start及ApplyLayout生成界面。独占never-active MVPGameManager没有运行Awake/Start/GameLoop；纯Session/Player通过既有phase/input接口、HUD.RefreshDriverSkill和CardHand.SetGearSelectionMode/ShowHand/UpdateActionButtonLabel生成按钮文案。技能在合法档位阶段TryActivate后转入出牌，未把不可用技能伪造为同时可发动。静态字体/材质与CardPrefab均是独占副本，finally清理预览对象、显式解除owned TMP监听并恢复RenderTexture.active，不保存场景/玩家设置、不执行生产onClick或声音。

动态CardPrefab克隆保持inactive；卡牌实例只供原CardHand选择/标签读取，**未渲染手牌卡面**，不是卡牌视觉、鼠标选牌或完整牌区数据一致性验收。背景没有比赛赛道/车体/真实排行榜，其他牌桌统计仅prefab上下文。manifest判据限buttons/labels/overflows及primary active/interactable/sibling，不将通用预览结构未填充的仪表字段当赛况结果。

生产原card阶段两主按钮都active，confirmGear禁用、PlayBtn在同锚点上层覆盖；manifest真实记录，主题没有强制新互斥active规则。截图的缺牌为+3热，无“可能失控”边界的专门截图；该更长风险标签及其他队技能长原因、selected专门截图仍按UX清单留未验，不缩字/删风险掩盖问题。

## 引擎审查与最终状态

Apply无Update、全局订阅、每帧重建或资源加载；ButtonClickAnimation沿原ModernUIStyle行为只Attach一次；阴影限目标Graphic，白基色配绝对ColorBlock避免暗色相乘，辅助Label不拦射线。没有新增第三方资源或许可依赖；原中文字体与授权台账沿用，不引入字体替换。

root最终真实状态见final-unity-state.json：UTC2026-10-15T17:06:12.4408184Z，MainMenu干净、未播放/编译/导入、许可证可用；resource idle/ready、测试结束；Console0error/warning。freshcompile期间出现一次MCP WebSocket未初始化warning，作为中间工具状态记录，不宣称本包修复其根因。既有用户改动与全部旧confirmation/map/audio证据保留，HEAD不变，未commit/push/恢复自动化。

baseline-preservation.json记录276项收尾存在性及SHA漂移分类，source-review.json记录8份源前后SHA。最终所有本包源/截图/测试/文档哈希见final-hash-manifest.json（不包含自身）。整个UI升级尚未完成；下包优先原风险长标签/卡牌等有界切片，不重做本包已验收按钮主题。
