# HUD 外框与排名区：第二包证据

2026-10-14。本包只接入三块现有 HUD 外层面板及排名/圈数/状态显示；不是全部 UI 完成。

## 改动和接入

- 新增 `RacingTechHudShellUI`，在 `RaceUILayoutController` authored/fallback 两条路径各加一条 Attach。相对本包 `layout-source-before.cs.txt` 只有这两条接入语句。
- OperationPanel、ScoreboardPanel、DeckTablePanel 使用既有 RacingTechTheme 石墨底、白标题及细分隔线；内部牌堆、按钮、六队挡位和 TrackFrame 保持。
- 六个原统计 TMP 引用与文本/字体保持；排名原 TMP 实例放入局部遮罩及 ScrollRect。文本事件、绑定和尺寸变化驱动内容高度，无 Update/LateUpdate 扫描。长名单换行并可滚动，不截断原数据。
- 原状态文字扩大到操作栏 y=.20–.29，不缩小字号。布局外锚点和赛道区域保持。
- 无玩法、存档、音频、资源字体或快速播放设置改动；无新第三方下载。许可和参考在 `docs/ui/racing-tech-resource-licenses.md`，规格在 `design/ux/racing-tech-ui.md` 第 13 节。

## 测试证据

| 阶段 | 总数 | 通过 | 失败 | 跳过 | 说明 |
|---|---:|---:|---:|---:|---|
| 改前定向 | 65 | 65 | 0 | 0 | 布局、文字、六队挡位 |
| 首次接入 | 71 | 69 | 2 | 0 | EditMode 中普通 MonoBehaviour 未自然订阅 TMP 事件；失败记录保留 |
| 修复后定向 | 85 | 85 | 0 | 0 | `focused-85-native.json`，原生 RunFinished，不采用进度计数 |
| 加强后的新测试 | 7 | 7 | 0 | 0 | `focused-7-final-native.json`；6 EditMode + 1 原生 Play Mode 协程 |
| 本次全量 | 2920 | 2920 | 0 | 0 | `full-2920-native.json`，原生 RunFinished，结束于 2026-10-14T08:22:05Z |

原生用例验证文本变长、禁用/重新启用、保持源字体无脏写、滚动条比例、滑至最后一行及内容底部对齐；EditMode 用例实际执行射线、滚轮、Scrollbar 拖动、原按钮点击、重复绑定和 TrackFrame 保持。新测试共 7 项。全部上述最终运行的 inconclusive 均为 0。全量实际耗时约 568 秒，包含真实比赛/教程/生涯流程协程；不是历史通过数。

## 真实受控截图与审查边界

截图来自现有生产 `Assets/Prefabs/UI/RaceCanvas.prefab` 的隔离预览场景，使用生产 HUD/布局和原字体的静态副本，不是设计图，也不是完整比赛截图。原主场景不打开/保存，预览自身对象和事件订阅在 finally 中清理。

- 六队 × 1920×1080、2560×1440、1280×720 × 正常 2/4/6 车/压力 12 车。压力文案含超长中文、圈 999/1000、回合 99999，实际 6 / 预计 14、引擎 7/12、热量 5/12（42%）保持一致。
- `before/`：36 张、36 个溢出记录（长状态及排名各 18），保留原缺陷。
- `after-attempt-1/`：首次失败的 36 张，18 个排名内容高度不足记录；对应 `attempt-1-tests.json` 和 `shell-source-attempt-1.cs.txt`，不删除失败证据。
- `after/`：最终 90 张，包含 18 张压力名单滚动到底部图；manifest 的 8 个标签逐图记录几何与溢出，0 溢出。
- 初版独立审查实际看过 27 张。最终重拍后独立审查重新查看 34 张：六队三尺寸全部 18 张压力底部、六队 720p 的 2/6 车共 12 张，以及 CN1080 两车、US1080 六车、DE1440 两车、JP1440 六车共 4 张。根任务另外查看最终 UK1080 压力底部和 CN720 两车。全部第 12 名可见，过渡行受 viewport 正常裁剪，未逃出排名面板或遮挡牌堆；2/6 车自身名次为 2/2 与 3/6。
- 最终夹具显式执行原生 ScrollRect.LateUpdate 模拟缺少的自然帧阶段，无手填滚动条或内容高度。18 张底部图的 thumb.size 均约 .5172，bar.value 与 normalizedPosition 约 0（浮点误差），图中缩略柄正确缩短；原生 Play Mode 测试独立确认同样行为。
- 720p 原字号仍偏小，舒适可读性不通过；完整自由赛/教程/生涯视觉、真实比赛中的整套卡牌拖动/弹窗状态不在本包通过范围。最终截图生命周期说明见 manifest。

## 工作区保护

`baseline.json` 保存本包开始时已有修改/未跟踪文件的 SHA256；`baseline-diff.txt` 保存起点差异。起点及终点 HEAD 均为 `7fe271b939d2bef8a61553b0d1bf9313198cc9b2`。`final-baseline-comparison.json` 逐文件核对原有文件，只有本包两条布局接入、任务 UX/清单/许可及两份连续性日志发生改变，其他原有修改和未跟踪文件保留；既有音频、教程、生涯、车队等改动不归入本包。`artifact-sha256.csv` 记录截图及关键证据/源文件哈希；未提交或推送。

最终 Unity 2022.3.62f3c1：MainMenu 场景 isDirty=false，未播放/编译/导入、idle、ready_for_tools=true。全量结束时 Console 只有 `TrackDataLoaderTest.test_load_config_missing_track_returns_null` 明确期望的缺失赛道错误；最后仅调整截图夹具并重新编译/拍摄后，Console error/warning 为 0（未人为清空）。见 `final-unity-state.json`。无新增生产脚本变化发生在全量通过之后。
