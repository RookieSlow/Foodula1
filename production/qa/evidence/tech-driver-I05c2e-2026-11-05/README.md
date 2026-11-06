# I05c2e 单回合/重赛清理前置证据

## 范围与状态

本包只处理意大利 L3B 与阿斯卡里待提交计划在重建/退出边界的清理前置，不新增科技节点、车手 ID、XP 键、解锁等级或主动次数。

- 状态：`production_change_native_editmode_verified`
- 生产接线：`MVPGameManager.OnDisable/OnDestroy -> ClosePrepMealInputs(true)`；重建入口先取消 IT L3B/阿斯卡里计划，再关闭本人输入时钟。
- 生命周期语义：退出不刷新陈旧 HUD；普通重建仍走原有展示刷新。取消不退款、不修改共享 `CardData.value`，正常回合跨回合蓝色幸运节奏保持不变。
- 新回归：`RaceGameLoopCoroutineTests.ItalyAndAscariPendingPlansAreInvalidatedBeforeRuntimeRebuild`

## Unity 与源验证

- Unity：2022.3.62f3c1，实例 `Foodula1@86b0192a4890cd9b`，场景 `Assets/Scenes/MainMenu.unity`。
- 本次重试前后直接字段边界：未播放、未编译；测试失败后已退出由测试启动的 PlayMode 并刷新恢复，未保存场景，最终 ready=true。
- `validate_script`：`MVPGameManager.cs` 0 errors（3 条既有性能提示）；`race_ascari_precision_coroutine_test.cs` 0 errors/0 warnings。
- `read_console` 在编译验证后无编译错误；最终恢复后 Console 为 0 条，未把日志清理结果冒充测试通过。

## 原生测试边界

2026-11-06 核对程序集归属后确认：本项目现有测试全部位于 `Assets/Tests/Editor`，Test Runner 将其归为 EditMode；`[UnityTest]` 只提供协程测试语义，不会把测试自动归入 PlayMode。因此 PlayMode 页签显示 `No tests to show` 是当前程序集结构的预期结果，前三次 PlayMode 运行属于模式选择错误，不是缺少 Unity Test Framework。

以下四次旧尝试均在初始化阶段超时，`total=0`、`started=0`，仅保留为诊断历史，不计入通过/失败/跳过：

| 模式 | 选择 | Job | 结果 |
|---|---|---|---|
| PlayMode | `RaceGameLoopCoroutineTests.ItalyAndAscariPendingPlansAreInvalidatedBeforeRuntimeRebuild` | `eed1c2099fa240b98080e06b1ac7e4e7` | `failed_initialize`, 120s, 0 started |
| PlayMode | 同上（编译修复后重试） | `7270165d75c74930bbe9ad1a78e2dfe1` | `failed_initialize`, 120s, 0 started |
| EditMode | IT/Ascari/driver/cleanup focused modules | `2866d7d02b1243d8946af305c7b36809` | `failed_initialize`, 120s, 0 started |
| PlayMode | 同上（运行器恢复后单次重跑） | `48799c5e97d64dc18b4ff70beadc1d9d` | `failed_initialize`, 120s, 0 started |

改用正确的 EditMode 程序集后，当前源状态的原生结果为：

| 模式 | 选择 | Job | 结果 |
|---|---|---|---|
| EditMode | `RaceGameLoopCoroutineTests.ItalyAndAscariPendingPlansAreInvalidatedBeforeRuntimeRebuild` | `a912567dda204b9681bd0c7ed1d3d07b` | 1/1 passed, 0 failed, 0 skipped, 1.7250048s |
| EditMode | Ascari/IT/driver/Blue Luck/turn cleanup focused modules | `daee4e1fdcb84062a6e39ce1bef9a18d` | 236/236 passed, 0 failed, 0 skipped, 1.2588058s |

两次当前运行合计 237 个互不重叠案例，全部通过。Unity 最终为 MainMenu、未播放、未编译、未导入、ready=true；Console 错误/警告 0。

## 视觉、模式与保留边界

- 新截图：0；玩家布局未改。
- 未验收：完整 RaceCanvas、三尺寸本包后续视觉、物理鼠标/键盘焦点与硬件、完整普通/生涯模式、真实存档兼容、I13 跨系统长流程。
- `Assets/Tests/Editor/RegressionModules.json` 已有 RaceGameLoopCoroutineTests 与 drivers 模块登记，本包未新增夹具类。
- 工作区既有未提交、未跟踪、Word/Markdown、InitTestScene 与其他任务文件均保留；本包不 commit/push/reset/checkout/rebase。

## 最终记录

最终源哈希、Git 状态计数和 JSON 计划进度写入同目录 `summary.json`。I05c2e 已在正确的 EditMode 程序集完成原生验证；I05 有界范围收口，下一工作包为 I06 诺瓦预付容错与有限低引擎联动。完整模式、真实存档、硬件与跨系统长流程仍留 I13。
