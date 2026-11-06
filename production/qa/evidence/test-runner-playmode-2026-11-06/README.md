# PlayMode Test Runner 发现修复复核

日期：2026-11-06（Asia/Shanghai）  
范围：复核用户截图中 Test Runner PlayMode 显示“没有测试”的问题，不改变游戏规则或用户场景。

## 修复边界

- `Assets/Tests/PlayMode/Foodula1.PlayModeTests.asmdef` 作为非 Editor 测试程序集保留 `Assembly-CSharp`、`UnityEngine.TestRunner` 与 `nunit.framework.dll` 引用，并限定 `UNITY_INCLUDE_TESTS`。
- 不额外加入 `optionalUnityReferences: TestAssemblies`：它会与显式 `UnityEngine.TestRunner` 产生重复引用并阻止测试初始化；当前采用 Unity 2022.3 的标准测试程序集配置。
- `ProjectSettings/ProjectSettings.asset` 的 `playModeTestRunnerEnabled` 为 `1`。
- `Assets/Scripts/Editor/EditorPlayBootstrapper.cs` 在 Test Framework 生成的 `Assets/InitTestScene*.unity` 进入 PlayMode 时暂时不强制切 MainMenu，退出测试后恢复普通 MainMenu 启动入口；未改动任何 `InitTestScene*.unity` 场景文件。

## 原生复核

Unity 2022.3.62f3c1，实例 `Foodula1@86b0192a4890cd9b`。

- 刷新/请求脚本编译后，Console 错误 0、警告 0。
- 原生 PlayMode `Foodula1.Tests.PlayMode.PlayModeAssemblySmokeTests.RuntimeTestAssemblyIsDiscoverable`：**1/1 Passed**，失败 0、跳过 0，耗时 **0.0480064 s**；作业 `d95d1e3d85714637a2d2ae96f58d4dbb`，RunFinished 状态 succeeded。
- 本次先复现到 `Assembly has duplicate references: UnityEngine.TestRunner`，撤掉重复的 `optionalUnityReferences` 后强制导入 asmdef，测试恢复可初始化并通过。
- 同一测试在刷新前也已通过 1/1（0.0362198 s）；本次以刷新后的新程序集结果为准，不把旧程序集结果冒充新通过。
- 当前编辑器场景为 `Assets/Scenes/MainMenu.unity`，未播放、未暂停、未编译、未导入；没有保存或修改任何场景。

## 手动操作

1. 在 Unity 选择 `Window > General > Test Runner`（若旧浮窗仍是空白，先关闭该 Test Runner 窗口后重新打开）。
2. 选择 `PlayMode` 标签，等待程序集导入完成。
3. 点击 `Run All` 或先点选 `Foodula1.Tests.PlayMode` 下的测试再运行。

本次不新增截图；Test Runner 的可运行性以刷新后的原生 RunFinished 结果为主。若用户侧窗口仍显示旧的“No tests to show”，先关闭该 Test Runner 浮窗再重新打开，切到 PlayMode，等待导入完成后点击 `Run All`；不要点击 `Create PlayMode Test Assembly Folder` 生成第二个程序集。完整 PlayMode 比赛、三尺寸 UI 和 I13 长流程仍不因这项程序集修复而提前宣称通过。

## 相关文件 SHA256

```text
Assets/Tests/PlayMode/Foodula1.PlayModeTests.asmdef=420DD613C7F58E84E9BA7C7CE97364A4DEFD1A1B93240D300FBED367C9145325
Assets/Tests/PlayMode/PlayModeAssemblySmokeTests.cs=285E2465B8CFAE2B4BC2C15654EB9C499E2F2B118BDE4050A2EA0AEA5B3FDD23
Assets/Scripts/Editor/EditorPlayBootstrapper.cs=39D548B70E98FD326AC644B06845DAAF7A457FBB34676006315843A0C7D1C0E3
ProjectSettings/ProjectSettings.asset=FE12D27C5307AE3A6CB6DB9001AE7AEC53C8ABBAC57700DE55FF9C6FBAC91B10
```
