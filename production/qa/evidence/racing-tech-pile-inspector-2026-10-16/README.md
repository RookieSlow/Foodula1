# 牌堆详情弹窗（第8包，2026-10-16）

原`CardPilePreviewUI.ShowInspector`已接入独立平面主题：石墨面板/列表、白标题与灰正文、细边、原关闭按钮。原黑色遮罩RGBA(0,0,0,0.72)、940×650几何、字体字号、完整说明、抽牌隐藏顺序汇总、弃牌新到旧、MiniCard/Glyph与原关闭/滚动均保留。改前真实路由表明正文点击被祖先Overlay接收；经批准只给Panel增加无回调/无动画的局部Button，内容现在由Panel接收，关闭及遮罩外仍走原销毁回调。

- 真实生产UI受控截图：`before/`21张、`after/`24张，1920×1080、2560×1440、1280×720，两个manifest均0文字溢出。抽/弃顶部和底部、Close原生hover/pressed/disabled；改后额外3张将原最长Go ATTACK效果行居中，原全文完整。抽20/弃22，真实catalog速度/热量/12特技只作诊断库存，不冒称普通赛事牌组。
- 本审查者实际看改前6、改后18张，范围逐图记`visual-review.json`；UX独立10/3、root4/2分别记录，不相加冒称不重复总数。所看无新增裁切遮挡，底部最老速度1可达；三尺寸最长全文与MiniCard类型保持。1440图工具缩放预览；720正文约10px舒适度未通过。
- 本轮四组原生RunFinished **39/39通过，失败/跳过/不确定0**，UTC2026-10-15T23:13:44.9013542Z，2.1340991秒，新增7项含1原生。`focused-native.json`为根任务返回原文；真实轮滚到末行、内容不误关/不穿透、原关闭/遮罩/重开及空堆、顺序、字体/几何、对比和幂等覆盖。2984仅发现数，本纯视觉包未全量，不挪用上包59/历史全量。
- root最终实查UTC23:14:07：MainMenu干净、未播放/编译/导入、许可有效、Console0error/warning；23:14:37资源idle/ready、testsfalse。中间一次MCP WebSocket未初始化warning保留事实，不称productionerror或工具已修复。
- `baseline.json`514项expanded原文件与status/rawdiff/源副本保留；本轮已有运行时仅CardPilePreviewUI增加1行主题hook，前包2lazyhook不改。完整三文档分页分工读审计见`continuity-read-audit.json`。最终保留/SHA清单在`preservation-audit.json`与`final-hash-manifest.json`；HEAD58656899，未commit/push或恢复自动化，无新增第三方资源。

夹具为独立owned Canvas经原Configure/ConfigureInspection/Refresh/ShowInspector生成，静态中文font/material克隆；原Layout/ScrollRect.LateUpdate受控推进，不手填内容尺寸、不调用关闭Destroy伪造EditMode图。关闭生命周期由原生测试证明。完整模式/OS物理输入、Close selected专门截图/极端数字、逐一六队效果、MiniCard既有对比、720舒适度与整体UI终验仍未完成；后续沿有限清单推进其他现有卡牌/操作弹窗，不重复打磨此包。
