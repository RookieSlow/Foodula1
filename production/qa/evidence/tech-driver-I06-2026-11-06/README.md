# I06a：诺瓦预付容错与 PlayMode 测试程序集接线

日期：2026-11-06（Asia/Shanghai）  
范围：I06「诺瓦预付容错与有限低引擎联动」，以及用户反馈的 Test Runner PlayMode 页无测试问题。

## 本轮生产接入

- 新增 `Assets/Scripts/Drivers/NuvolariRuntime.cs`，绑定 session、参赛车实例、车手 ID、本人合格驾驶输入和牌实例。
- 被动「不屈传奇」仅在 IT 人类车手、本人输入引擎不高于锁定阈值时生效；普通弯道罚热成功支付后，最多回收输入开始时手牌中的旧永久热实例（Lv1/Lv2/Lv3 为 1/1/2）。部分支付、失控、临时热、新抽牌和弃牌不触发回收；普通罚热费用仍照常结算。
- 主动低引擎容错改为 `引擎剩余 ≤ 2`，提交时预付一次失控容错并保留一次既有失控上限；仅忽略首个弯道，移动与 Lv1/Lv2 恢复语义沿现有规则，Lv3 不跳过恢复。未提交、进站、跳过、终态、退出和重建按现有输入中断路径清理，不返还已提交次数/费用。
- `RaceSession`、`RaceHeatPaymentExecution`、`MVPGameManager`、`RaceTurnSkillCleanup`、`RaceTurnCleanup` 与预测复制均已接线；预测副本独立复制旧热集合，不修改共享 `CardData.value` 或真实资源。

## Test Runner 修复

截图中的提示来自程序集边界：原有 `[UnityTest]` 文件都位于 `Assets/Tests/Editor`，属于 EditMode；协程属性本身不会把测试归入 PlayMode。新增真实运行时程序集和烟测：

- `Assets/Tests/PlayMode/Foodula1.PlayModeTests.asmdef`
- `Assets/Tests/PlayMode/PlayModeAssemblySmokeTests.cs`

Unity 已导入并生成 `Library/ScriptAssemblies/Foodula1.PlayModeTests.dll`（2026-11-06 14:03:34，6144 bytes）。烟测会在 Test Runner 的 PlayMode 页执行 `Application.isPlaying` 断言；没有把旧 EditMode 结果冒充 PlayMode 通过。

为避免项目级 `EditorPlayBootstrapper` 把 Unity Test Framework 自动生成的 `InitTestScene*.unity` 替换成 MainMenu，`EditorPlayBootstrapper` 现在在检测到测试启动场景时临时清空 `playModeStartScene`，退出测试后恢复 MainMenu。`ProjectSettings/ProjectSettings.asset` 的 `playModeTestRunnerEnabled` 已开启，保留 `runPlayModeTestAsEditModeTest: 0`。

## 当前验证

- Unity 2022.3.62f3c1 已重新导入新程序集；`git diff --check` 通过（仅保留既有 LF/CRLF 提示）。
- JSON、C# 大括号/路径和程序集输出静态检查通过。
- 本轮 Unity 原生运行：PlayMode 烟测 1/1 通过（0 失败、0 跳过，0.0489397s）；Nuvolari EditMode 聚焦 6/6 通过（0 失败、0 跳过，0.0612661s）。两次均由当前新程序集/当前源执行；不是历史结果。
- PlayMode 初始化曾因全局启动场景覆盖 `InitTestScene` 超时；修复后重新运行通过。编辑器当前保持 MainMenu、未播放；不保存场景、不改 Enter Play Mode 快速播放选项。

## 未验收边界

- Test Runner 浮动窗口在 CUA 截图中仍白屏，但 MCP 原生 Test Runner 已识别并执行新 PlayMode 程序集；若用户侧窗口仍空白，可关闭/重开后选择 `Foodula1.PlayModeTests/PlayModeAssemblySmokeTests/RuntimeTestAssemblyIsDiscoverable`。
- 完整 RaceCanvas 三尺寸交互、真实存档/模式组合、I13 长流程仍未验收。

## 相关源 SHA256

```text
Assets/Scripts/Drivers/NuvolariRuntime.cs 83f488fa94a5c2a4e93f10b2cf0e758a72859dff72dc30a5c6656ea0a0e36392
Assets/Scripts/Drivers/DriverSkillRules.cs efacf52d484fb6e4535f7d648700d243ed14814fbdbba125d0e7bb7a0b0d9eac
Assets/Scripts/Drivers/DriverSkillRuntime.cs dc663be893ce94ac43224674bc472354e037569189635339f429513fc6371718
Assets/Scripts/Core/RaceSession.cs 04a155f7cc5bfe1c00ea667ea5337b7e0e0dfe1e0fa11219b6ba4dab42adf00c
Assets/Scripts/Core/RaceHeatPaymentExecution.cs bc8336c2d9defcf4b0921263acc413835ca4ee9106d98c637cfbc69941de150e
Assets/Scripts/Core/MVPGameManager.cs 05ec70e56dce1868374dc423cd2766ce99107a5a34b9bc173b91bb0b739be5a0
Assets/Scripts/Core/RaceTurnCleanup.cs 4206011cf23ce33942855c022924c3196d2d22be7d23bd1c3497c4affbe4f9c0
Assets/Scripts/Core/RaceTurnSkillCleanup.cs 17850d4b0175ae6d73569949100985f5d39c236141bc644af8a732350ba61c49
Assets/Scripts/Editor/EditorPlayBootstrapper.cs 39d548b70e98fd326ac644b06845daaf7a457fbb34676006315843a0c7d1c0e3
ProjectSettings/ProjectSettings.asset fe12d27c5307ae3a6cb6db9001ae7aec53c8abbac57700de55ff9c6fbac91b10
Assets/Tests/Editor/nuvolari_skill_test.cs 48377b0e8224c9b3c54a0188b32f7bac3f1a2a3c376fa4df86459345f03000ec
Assets/Tests/PlayMode/Foodula1.PlayModeTests.asmdef 420dd613c7f58e84e9ba7c7ce97364a4defd1a1b93240d300fbed367c9145325
Assets/Tests/PlayMode/PlayModeAssemblySmokeTests.cs 285e2465b8cfae2b4bc2c15654eb9c499e2f2b118bde4050a2ea0aea5b3fdd23
```
