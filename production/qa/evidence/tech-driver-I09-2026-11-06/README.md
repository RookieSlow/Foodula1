# I09 生产接入证据：CN 温杯与周冠宇旧热整备

日期：2026-11-06  
Unity：2022.3.62f3c1  
实例：`Foodula1@86b0192a4890cd9b`

## 范围

- CN L2 继续使用稳定节点 ID `cn-l2-dim-sum-combo`，呈现与效果锁定为“温杯”。声明只在本人 Go 输入、引擎仍有热且本回合尚未使用时成立；按普通移动、费用、冷却后、原 L1 与三连前的顺序一次性支付 1 个永久引擎热到手牌并额外前进 1 格。
- 温杯运行态在 `TechTreeState.warmCup` 中独立保存，复制、回合重置、竞赛重置与取消不改共享 `CardData.value`，预览/取消/非 Go/终态不会扣费。
- 周冠宇保留原稳定 ID、XP 键、解锁等级与 1/1/2 次数。被动“沉稳之心”仅将真实 Go 或 L2 普通弯道实付热标记为旧永久手牌热，随后普通冷却回收至引擎，L3 上限 2；主动“稳守节奏”原子提交一张普通速度牌与最多 1/2 个旧永久手牌热，并在本人输入窗口内为冰果冻或土屋烟幕阻断的尾流提供最多 1 格护航。
- 未扩大为天气免疫、免费跳罚热、常规 Recover 改写或 AI 车手技能；护航不会绕过连续 Go、普通弯道、缺牌、维修/跳过/终态清理。

## 生产接线

- `TechTreeData.cs`：追加 `WarmCup` 枚举值并保留旧枚举/字段兼容；新增 `WarmCupRuntime` 与 `ChinaWarmCupResult`。
- `TechTreeRules.cs` / `TechTreeDatabase.cs`：接入效果映射、目录呈现、Go/引擎热/回合门控；旧节点 ID 不变。
- `RaceTurnTechnologyCleanup.cs` / `RaceSession.cs`：温杯置于普通冷却之后、原 L1/三连之前；周冠宇被动恢复在冷却后执行；本人输入时钟、终态与回合生命周期清理保持原顺序。
- `CardDeck.cs`：周冠宇主动牌费与旧永久热恢复原子提交；`RaceSettledSlipstreamResolution.cs`：护航结算后仅提交实际成功的护航运行态。
- `MVPGameManager.cs` / `HUDUI.cs`：沿用现有合并确认入口，温杯与周冠宇动作在已有车手按钮前置检查；按钮、剩余次数、取消和禁用原因的完整 RaceCanvas 三尺寸视觉验收仍留 I13。
- `DriverData.cs` / GDD：同步“沉稳之心”“稳守节奏”正式文案；`RegressionModules.json` 将 `ChinaWarmCupTests` 归属 `tech`，不重复归属 `drivers`。

## 原生验证

所有结果均来自当前 Unity Test Runner 程序集，均收到原生 `RunFinished`，不是历史结果或模型推演：

| 模式 | 选择 | Job | 总数 | 通过 | 失败 | 跳过 | 耗时 |
|---|---|---|---:|---:|---:|---:|---:|
| EditMode | CN/科技/车手/生命周期聚焦（包含 `ChinaWarmCupTests`、`RaceSessionTest`、`RaceTurnTechnologyCleanupTests`、相关 UI/牌组类） | `57af94ecdecf4375af6ef5e6f524c8c4` | 494 | 494 | 0 | 0 | 2.7495901 s |
| EditMode | `ModularRegressionCatalogTests` | `057d2920658248ae841823f7ac2903af` | 12 | 12 | 0 | 0 | 0.0834334 s |
| PlayMode | `Foodula1.Tests.PlayMode.PlayModeAssemblySmokeTests.RuntimeTestAssemblyIsDiscoverable` | `3d8b3e4a7ec74d6589f30de47874730b` | 1 | 1 | 0 | 0 | 0.0158642 s |

本包 native 总执行 507 项，507 通过，0 失败，0 跳过；三次原生运行合计耗时 2.8488877 s。之前一次包含旧断言的 389 项聚焦尝试不计入本包通过；断言已按锁定规则修正后，以上当前程序集结果为准。

## Unity 状态与边界

- 最终观察：活动场景 `Assets/Scenes/MainMenu.unity`，未播放、未编译、未导入；Console 读取为 0 错误、0 警告。
- `mcpforunity://editor/state` 的 ready 状态在本包末尾仍报告 `stale_status`；这没有阻止 Test Runner 完成，故只记录为工具状态，不伪称 ready。
- 本包无截图：完整 RaceCanvas 的 1920×1080、2560×1440、1280×720 实机交互（中文长文案、按钮焦点、禁用原因、取消/返回、遮罩/点击穿透与拖动）尚未验收；普通/生涯完整模式、真实存档兼容、硬件和 I13 跨系统长流程未验收。
- 未提交、未推送、未 reset/checkout/rebase，未保存或清理用户场景；既有脏文件和 `InitTestScene*.unity` 保留。

## 相关文件 SHA256（本包末尾）

```text
Assets/Scripts/TechTree/TechTreeData.cs 66765C29483CDEFBD57F53823AA683625FD3AADD12E4412C93F3B4E2504EA679
Assets/Scripts/TechTree/WarmCupRuntime.cs 43C4DAE3806365F7EECE8276D77099C9454C3D718842A14B2B4BACEAD12808F6
Assets/Scripts/TechTree/TechTreeRules.cs 720804EE3A6A83645191020D761AB4BE0447ABF92CEED624731D25E6D967884E
Assets/Scripts/TechTree/TechTreeDatabase.cs FCB4890266A8712E75CD92B39486489C9C392A313D20C6672667158981909C38
Assets/Scripts/Core/CardDeck.cs B01AC10B28F33AFCB2C11D34339DAAB6A81A392EB81673270CF0DBA942AE32B5
Assets/Scripts/Core/RaceSession.cs 755A8FE71BFF86DFC91A331F739D2814BCF54317B5D814FA24258AD4EFBE17DF
Assets/Scripts/Core/RaceTurnTechnologyCleanup.cs C8A4DB423D7219F4F44002AB447489B6E7B8D12463A553D2FCCA0184C7595898
Assets/Scripts/Core/RaceSettledSlipstreamResolution.cs A933034998C25779FAC01AC6F1E44ACEDE43656AB0C215EDBE38C71038676593
Assets/Scripts/Core/MVPGameManager.cs 4E3F2743F3526A92468019727C238ADB1541BB16A9FC7A8C16B4B72C1A507A5F
Assets/Scripts/UI/HUDUI.cs 7388989EB1F1FC8A47A6386F781FE655197C837764F2EF16CA34D9E44CCAE783
Assets/Scripts/Drivers/DriverData.cs A26B0CD2E828448E655A71A2D46F87935F00F9599083E8EEF0EC7F1D76D98127
Assets/Scripts/Drivers/DriverSkillRules.cs 6314A3236EDFB117FC3C1648CEC90A6EA82DF76891CCA2F276B0FC07BF40BC12
Assets/Scripts/Drivers/DriverSkillRuntime.cs 8E78CC1AF70646FA1E73A9AAF46194D3A6B7628F8950D63C52BBB54F0355166D
Assets/Scripts/Drivers/ZhouEscortRuntime.cs 6D4F650DFDDE13D91D0B835160423AA6A1211AC65E53FE57DA0E8F15400EE8D4
Assets/Scripts/Drivers/ZhouSteadyHeartRuntime.cs A99F7C3EA3CE89F986E92356D9985F3E371AA8F7A517CA9984D7D7FC7269E8FF
Assets/Tests/Editor/china_warm_cup_test.cs DA4B04C5B5B3D1E264531276C3D084C17F88AC9C4EB479B945A4D8D371ACA8D0
Assets/Tests/Editor/RegressionModules.json 264505EC4B96AE0BCD44D847F3475E22D1D17F1CF17EC283D7E5587C32FB2F76
design/gdd/foodula-1-tech-tree.md F58D18E83F87D0AC35F3B265BBCA5485E69D42B02931149BCA6C46B2C13403FA
design/gdd/foodula-1-drivers.md BD904B62EFB5FC47CF15A8846E2EFA6508420ED471C3D26504F80CE9FC669467
```

`git diff --check`：通过；Git 仅报告既有 LF/CRLF 工作副本提示，不作为本包失败。
