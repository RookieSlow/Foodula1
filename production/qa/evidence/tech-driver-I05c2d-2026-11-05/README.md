# I05c2d 意大利阶段依赖前置与生命周期退出边界

## 范围

本包对意大利阶段现有蓝色幸运、精密过弯与 IT L3B 入口做依赖前置审计，并补齐生命周期退出边界。I05 继续进行中，不推进 I06；没有新增科技节点、车手 ID、XP 键、解锁等级或主动次数，也没有新增玩家界面布局。

## 生产接线

`MVPGameManager.ClosePrepMealInputs()` 现在在 `OnDisable`/`OnDestroy` 生命周期退出时调用 `player?.driverSkill?.InterruptDrivingInput()`。这样未完成的阿斯卡里蓝色幸运本人输入样本、连续无热计数和待触发标记会在退出时清理，但不会退还已经提交的主动次数；正常回合清理仍保留跨回合蓝色幸运节奏。进站、跳过和终态路径继续使用既有 `RaceSession`/`RaceTurnSkillCleanup` 清理。

## 原生 Unity 回归

Unity Test Runner 使用当前项目程序集实际执行，未使用旧程序集结果：

| 选择 | 总数 | 通过 | 失败 | 跳过 | 耗时 |
|---|---:|---:|---:|---:|---:|
| `BlueLuckTests` + `RaceDriverSkillActivationAdapterTests` + `AscariPrecisionTests` + `ItalyPrecisionTuneTests` | 102 | 102 | 0 | 0 | 0.3437354 s |
| `RaceTurnCleanupTests` + `RaceTurnSkillCleanupTests` + `RaceTurnTechnologyCleanupTests` + `RaceSessionTest` | 134 | 134 | 0 | 0 | 0.1332634 s |
| 本包两组聚焦选择 | 236 | 236 | 0 | 0 | 0.4769988 s |

新增生命周期案例 `LifecycleExitClearsAscariBlueLuckOwnerInputWithoutTouchingRaceUse` 验证退出清除输入样本/待触发状态且不改变已用次数。`RaceTurnCleanup` 未被改为中断输入，确保正常回合仍保留蓝色幸运节奏。

## Unity 与 Console 边界

- Unity `2022.3.62f3c1`，实例 `Foodula1@86b0192a4890cd9b`。
- 活动场景 `Assets/Scenes/MainMenu.unity`；最终未播放、未编译。
- `read_console` 清理后读取为 0 errors / 0 warnings。编辑器状态资源最后一次轮询为 stale，因此不把它外推为更强的 ready 证据；直接场景/播放/编译字段如上记录。
- 本包没有新 UI 布局或截图；玩家可操作验收不因本生命周期切片扩张。

## 未验收边界

完整 RaceCanvas、物理鼠标/键盘焦点、全部模式与真实存档兼容、硬件输入以及 I13 跨系统长流程仍未验收。静态文案或历史截图不作为本包视觉通过。

## 来源与工作区保全

- `Assets/Scripts/Core/MVPGameManager.cs` SHA256：`7e9f9c851df16b84907d90d5e73b876d1ad0ead8931fa354b8a93a91460f3df3`
- `Assets/Tests/Editor/race_driver_skill_activation_adapter_test.cs` SHA256：`d7926ee532c5e22e36434087c29c7cd1311e97c546945d98898e9a1954e6562d`
- `design/planning/tech-driver-rework-2026-10-22/progress.json` SHA256：`27359c86eb01aaad190fdd1e1a225c49acf781c53c11e721908547812459dee1`
- `design/planning/tech-driver-rework-2026-10-22/implementation-plan.json` SHA256：`a82944161f201b7d3bbcf2b047b205f696eafedd7f8fe195a99139e9f9ffe7b4`
- 收尾 `git status --short` 为 79 行（22 个 tracked modified、57 个 untracked）；`git diff --check` 仅有既存 LF/CRLF 提示，`git diff --stat` 为 22 个已跟踪文件、811 insertions/80 deletions。未把无关音频/UI/字体等条目归因于本包。
- 本包未 commit、push、reset、checkout、rebase、删除/移动文件或保存脏场景；既有 tracked/untracked、Word、Markdown、排除文件和 `InitTestScene*.unity` 均保留。

证据仅覆盖本包的依赖审计与生命周期退出边界，不代表意大利整队、十二位车手或完整模式已完成。

