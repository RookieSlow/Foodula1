# 牌桌四区域壳证据（2026-10-16，已接入）

本包仅抽牌/引擎/手牌/弃牌四区域底板、标题、统计及动态计数/空堆/查看提示。原CardUI、缩略牌/牌背、检查器、输入及玩法不改。UX/视觉gate已批准；改后27张及六组定向59/59已取得真实证据；独立改后审查因协作额度限制未完成，由根任务完成有限自审。

- 基线：`baseline.json`展开411个已有dirty/untracked文件，HEAD58656899；原始status/diff与源码副本保留。实际CardPrefab为`Assets/Prefab/CardPrefab.prefab`，另有独立snapshot；早期猜测路径的missing记录不冒充资源缺失。完整continuity分工阅读见`continuity-read-audit.json`。
- 改前：`before/manifest.json`实拍27张，1920×1080、2560×1440、1280×720。六队7张真实可见手牌、抽12/弃3；CN另有空堆、零引擎真实失控风险、原卡牌选择与原生Selectable.Selected。显示0/1/3/12计数；未造大数字或最大手牌。`HUD.Start/ApplyLayout → CardHand.ShowHand/UpdateDeckInfo`原路径生成，不打开详情或触发onClick。
- 真实已有缺陷：27图共54处TMPoverflow，均HandTitle/DeckTableTitle高度不足。风险“结束出牌 · 缺3，可能失控”原两行完整、不溢出，无需改主按钮。只允许对这两个标题向下扩展最小高度，四区域几何保持；改前原图不覆盖。
- 本审查者实际查看改前6张：CN720基准/风险、UK1080基准、JP1440基准、CN1080selected、CN1440空堆。七张牌/火焰/叠背、精确数字与空堆提示均真实可见；720统计/查看小字的舒适度未验收。
- 夹具是隔离PreviewScene中的真实生产uGUI：静态中文font/material克隆、独占从未激活manager和纯Session/Player，卡实例显式activate且CardUI禁用使选择使用原即时路径。无真实赛道/比赛GameLoop、物理点击拖动或存档。初次helper字符串替换导致编译失败，修正后才成功拍before，详见`helper-compile-correction.json`。

最终结果见下文；不挪用上包61或历史全量。本矩阵仅真实speed/heat卡，未图审特技卡、大数字或牌堆hover/pressed专门截图；后续pointer测试不能冒充这些图像。完整自由赛/教程/生涯、OS输入、720p舒适度、卡面/详情主题与整体UI终验仍未完成；本包无新增第三方资产、未Git操作或恢复自动化。

## 本包最终证据

- 已接入独立RacingTechCardZoneStyle，Layout authored/fallback各1行、Preview lazy创建各1行hook。四区域平面石墨底、白标题/计数和灰提示；HandTitle/DeckTableTitle仅不足时向下补高，anchors/top/字体字号及卡面保持。
- after27张，三个规定尺寸，manifest溢出0（before27张/54处两标题溢出）。根任务实际查看改后9张：六队基准各1（CN/DE/IT1080、UK/JP1440、US720），CN720空堆/风险及CN1440selected；根任务改前2张，另一审查者改前6张，数量不合并虚报。标题完整，7卡/原选中抬升未被新增区域遮挡，真实0/1/3/12计数、空/暂无卡牌与原两行失控提示保留。
- 六组定向原生RunFinished 59总/59通过/0失败/0跳过/0不确定，结束UTC2026-10-15T17:47:34.3214193Z，MCP实际2.5227451秒；包含新8项，含原pointer/检查器单次打开与空堆禁用、动态计数/顺序/有效对比、双布局及标题幂等。见focused-native.json和focused-summary.json。2977只发现数，未再跑全量；本轮不是整场赛事或OS物理输入验收。
- 最后实查UTC2026-10-15T22:35:09Z MainMenu干净、未播放/编译/导入，idle/ready、许可证有效、测试结束；Console0error/warning。中间helper编译失败已修正，域重载另出现一次MCP WebSocket warning，不声称修复工具根因。
- 协作代理额度限制使改后独立UX/视觉审查尚缺；根任务按已批准设计补有限自审并同步文档。没有因此将team-ui完整独立评审门记为完成。411项保留审计见baseline-preservation.json，源差异见source-review.json；最终哈希见final-hash-manifest.json。未commit/push、未动其他自动化。

本包完成接入与上述自动测试/有限自审，整个Demo和UI升级仍未完成。下一包优先牌堆详情/卡面现有UI及待补独立审查；特技卡、大数字、牌堆hover/pressed专门图、720p舒适度、完整模式和主观声音验收仍待完成。