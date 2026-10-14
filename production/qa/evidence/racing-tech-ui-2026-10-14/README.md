# 赛车科技 UI 首包验收（2026-10-14）

本轮已将独立 `RacingTechTheme` 接入现有 `RaceTelemetryStripUI`：石墨底、白色左对齐摘要、顶部细线、低装饰轨道和实际/预计热量色。保留全部原文案、数据分支、原点击明细与六队挡位。未改玩法、输入方式、字体、快速播放设置、场景或预制体。

## 本轮实际证据

| 检查 | 结果 |
|---|---|
| 修改前相关 EditMode | 86/86，失败/跳过 0；作业 `694d84f342b94f50a74c6675e2365a0d` |
| 修改后定向原生 RunFinished | 94/94，失败/跳过/不确定 0；作业 `c9b686ffdc9b43a78c94fa0faec210c6` |
| 原生 Play Mode UI | 上述 94 项中包含 1 项，真实 RaceCanvas/CardPrefab 选挡、选牌与预测撤销；不是额外相加 |
| 新增主题测试 | 4 项：文字/数据色对比度、字体/文字/射线/回调/禁用归属、重复接入与控件复用 |
| 全量首轮 | 2908 项：通过 2905、失败 3、跳过 0；作业 `d712981957904c1492e9a57d67bb5c20` |
| 音频失败独立复跑 | 3 项均复现；作业 `c9d51ae014e54186aca919c450e10663` |
| 一行判空修复后原音频案例 | 3/3，失败/跳过 0；作业 `b533d02f0ff948b6bba9420fd76f32e1`，此轮未包含随后导入的两个新案例 |
| 仅声源判空后定向 | 99/99（95 EditMode + 4 原生 Play Mode UI/音频），失败/跳过/不确定 0；作业 `e4a1bef8c36f41308b91bbb49bab8909` |
| 仅声源判空后全量 | 2910 项：通过 2907、失败 3、跳过 0；异常移到旧服务音乐播放回调；作业 `4c39cf8708ef4093a59c23000c589d42` |
| 旧回调解绑回归修复前 | 1 项实际失败，捕获非当前单例留下回调；作业 `43caed98d55a46ae965e6952f95de150` |
| 解绑顺序调整后定向 | 100/100（96 EditMode + 4 原生 Play Mode UI/音频），失败/跳过/不确定 0；作业 `502fae05ef524a5f97c8b821e4133c4c` |
| 解绑顺序调整后全量 | 2911 项：通过 2908、失败 3、跳过 0；作业 `a78e3cd83d71470ebb4113a0e86ca2b9` |
| 销毁目标回调回归修复前 | 2 项实际失败，捕获不会自行解绑的已销毁监听目标；作业 `76436350498c46578091019255899e47` |
| 最终定向原生 RunFinished | 102/102（98 EditMode + 4 原生 Play Mode UI/音频），失败/跳过/不确定 0；作业 `ac2905acec97413597faf6a2143bb732` |
| 最终全量原生 RunFinished | **2913/2913，通过；失败/跳过/不确定 0**，结束 2026-10-14 11:45:49 北京时间；作业 `1c8338ef71474e6db4fad90add4c6429` |
| 改前/改后截图 | 各 36 PNG；六队 × 1920×1080 / 2560×1440 / 1280×720 × 实际/预计两状态 |
| 文字几何 | 每阶段 72 条数据条文字记录，溢出 0；不外推全屏其他文字 |
| 实际查看 | 根任务查看 UK1080、CN1440、DE720；审查任务查看六队720预计、US/DE720实际、IT1440及CN1080改前 |

`focused-final-native-run.json` / `focused-final-native-ui-case.json` 是最后 102 项的原生回调原文；更早的 focused 文件也保留。`full-native-run.json`、`full-after-source-guard-native-run.json`、`full-after-unsubscribe-native-run.json` 保存三次未通过的全量结果，不被后续覆盖。`before/manifest.json` 和 `after/manifest.json` 标注了真实受控夹具边界，`screenshot-integrity.json` 保存 72 PNG 的字节数与 SHA256。`source-hashes.json` 记录代码哈希。

## 全量发现的生命周期修复

三项 `TeamVehicleRuntimeAudioTests` 在完整回归及独立复跑均出现 `AudioService.StopVehicleEngine` 对已销毁 `AudioSource.Stop()` 的异常，经 OnSceneLoaded/OnSceneUnloaded 进入。音频源码在故障定位时与起始 SHA256 `DD62AA55C0CFE929FB1DF78CF18C0D6BBEAE2E8C86119B67BF1B6591F9BC37C9` 一致；不由此推断所有失败历史。完整异常见 `full-console-errors.json`。

先为循环新增 `if (source == null) continue;`，跳过已销毁/空源，存活源仍按原逻辑停止、清 clip 和复位音高/音量。独立音频及合并定向通过，但完整执行顺序下仍发现旧服务场景加载回调访问已销毁音乐源；第二次完整堆栈见 `full-after-source-guard-console-errors.json`。这证明单独通过不能外推全量。

进一步定位 `OnDestroy` 在失去单例归属时提前返回，导致自身场景事件仍绑定。新增精确回归先在旧顺序实际失败（`stale-scene-callback-red-test.json`），再将 sceneLoaded/sceneUnloaded 解绑移到归属判断之前。测试验证只移除旧服务自己的回调，其他监听器数量和真实单例保留。

第三次完整顺序仍暴露生命周期边界：只读检查发现五个 `sceneLoaded` 监听目标为 Unity 已销毁对象（托管引用仍非空），`OnDestroy` 本身未完成清理的目标仍会收到事件。新增 loaded/unloaded 两个销毁目标回调案例先实际失败（`dead-target-callback-red-tests.json`），再让已失效服务收到事件时先解除自身两个订阅并退出。正常服务仍执行原停止引擎/场景音乐逻辑。清理复用私有方法，无每帧开销或声音重新创建。

原源码完整备份 `audio-source-before.cs.txt`，没有音色、增益、资源或玩法调整。`RacingTechAudioTeardownTests` 共五例；加四项主题测试，本轮新增九个案例。最后定向 102 项实际通过。三次全量失败保留各自原生结果与 Console 堆栈，不把单独复跑通过当完整验证。

最终完整结果为 `full-final-native-run.json`；它与 MCP 的 2913/2913 一致，不用发现数量或旧进度代替结果。`final-unity-state.json` 记录 MainMenu isDirty=false、无播放/编译/导入、armed=false、免费许可有效；ready 资源也恢复 idle/true。当前 previewSceneCount=1，未追溯其来源，不擅自关闭编辑器既有预览。`final-console.json` 只有 `TrackDataLoaderTest.test_load_config_missing_track_returns_null` 刻意使用 `no_such_track.json` 的预期错误，无声源异常或编译错误；未清空日志掩盖问题。

示例：[改前 UK 1080p](before/UK-1920x1080-forecast-14-heat-5-to-7.png)、[改后 UK 1080p](after/UK-1920x1080-forecast-14-heat-5-to-7.png)、[改后 CN 1440p](after/CN-2560x1440-forecast-14-heat-5-to-7.png)、[改后 DE 720p](after/DE-1280x720-forecast-14-heat-5-to-7.png)。

## 审查与未验收边界

UX、视觉、Unity UI 实现和可访问性相关审查允许本有界样板交付；原生指针测试验证明细窗口、关闭/滚动/模态阻挡，既有美国拖杆、选牌取消/确认及六队原控件回归包含在定向测试中。新增两条分隔线不捕获射线，重复 Attach/Refresh 不增加控件；没有 Update、外部资源加载或新动画。未运行入口性能基准，不能据代码审查声明量化性能改善。

截图是生产预制体的 EditMode 受控渲染：无赛道、手牌或活跃玩法管理器，记分牌保留预制体默认挡位/引擎/温度，不与夹具数据条作全 HUD 一致性验收。720p 沿用原缩放后摘要偏小；无新增溢出不等于舒适可读性通过。长中文/极端数字额外截图、多参赛车数、完整默认自由赛/教程/生涯视觉及物理卡牌拖动仍待后续验收。棕金外围面板、排名、卡牌、菜单尚未迁移。

## 工作区和操作记录

基线 HEAD `7fe271b939d2bef8a61553b0d1bf9313198cc9b2`；`baseline.json` 保存原 294 个已修改/未跟踪文件的哈希及 status，`baseline-diff.txt` 保存原 diff 数量。接入后初次核对 293 个原文件字节不变，唯一变化是本包负责的既有未跟踪 `RaceTelemetryStripUI.cs`；原文另存 `telemetry-source-before.cs.txt`，基线 SHA256 `FA4F83A9F498B8C2985825AD2F952894AA6941380CAF1F7D8181E962EC11597B`。本轮只对它做局部增量，不能把整份旧文件归功于本包。

新增代码/测试/捕获工具及其 meta、主题/清单/许可文档和本证据目录属于本轮；此外本轮负责上述 AudioService 局部生命周期清理增量。未提交、推送、重置、切分支、删除或移动用户文件。开发期捕获工具遗漏必需构造参数导致一次编译失败，修正后新程序集编译成功；其间另有该工具源文件时间戳导入竞态，后续编译和完整截图正常，不冒充初次即成功。

AGENTS 引用的 `.Codex/docs/` 五份治理文档和 `production/session-state/active.md` 缺失，未创建占位；现有 UX/玩法与真实实现作为本切片依据。来源和许可见 [资源许可清单](../../../../docs/ui/racing-tech-resource-licenses.md)，不复制官方字体/logo/受保护图形；未新增第三方下载。

最终差异核对：原 294 个基线文件中 290 个字节一致；仅本轮负责的两份代码，以及追加本包状态的 project-overview/current-task-list 有变化，无原文件缺失。中文字体原脏版本哈希保持，HEAD 不变、暂存区 0、`git diff --check` 通过；72 张 PNG 校验无失败，报告本地链接无失效。

下一包：将公共主题应用到比赛 HUD 外壳与排名/圈数/回合/状态，保留六队原控件和输入链，再按清单推进后续卡牌、弹窗与菜单。
