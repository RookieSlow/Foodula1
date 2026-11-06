# I08：斯图尔特与布什有限窗口、实际跟车、付费与拒绝入口

日期：2026-11-06  
范围：仅接入锁定方案中的 Tony Stewart（斯图尔特）与 Kyle Busch（布什）；不改变车手稳定 ID、XP 键、解锁等级或既有 1/1/2 主动次数。完整普通/生涯模式、真实存档、硬件输入和全 RaceCanvas 三尺寸验收仍留 I13。

## 本包实际接入

- `TrackFuryRuntime`：斯图尔特的 Track Fury 在本人合格驾驶输入上计时；被其他参赛者实际超越后，仅武装下一次本人输入，每圈最多一次。L1/L2/L3 的普通速度贡献分别为 `+1/+2/+2`；L3 的旧永久热回收只在纯直道安全结算时消费一次，不能用临时热或新抽牌伪造来源。
- `True Self`：真实卡牌选择提交后才一次支付 1 张永久引擎热到手牌；输入窗口持续 2/3 个本人输入（按等级），直道收益 `+2/+2/+3`。支付失败会结束窗口但不造成失控/旋转；预览、取消、过期、进站、跳过、终态和退出不扣费、不推进输入时钟。弯道推力为 0 时仍支付固定费用，后续尾流不重新计算该输入。
- `BuschPressureRuntime`：布什在输入开始锁定最近前车，距离范围为 L1 `1–2`、L2 `1–3`、L3 `1–4`；本人实际超越后只对锁定目标武装下一次压力标记，每圈一次。Taunting Bow 只选择最近有效后车（距离 `1–3`），提交时支付 1 张永久热；目标下一次输入可接受或拒绝跟车。接受再支付 1 张永久热到手牌并允许合法尾流，拒绝/缺热不会旋转，也不会继续搜索更远目标。标记在一次输入、尾流结算、进站/跳过/终态或退出后失效。普通 AI 不触发车手技能。
- 费用与牌区：斯图尔特和布什的固定费用均通过既有 `RaceHeatPaymentExecution`/`Race.Time` 入口校验后一次提交；`CardDeck.RemoveOnePermanentHeatFromHand` 只移除旧永久手牌实例，临时热不冒充永久热。

## 原生验证

Unity 版本：2022.3.62f3c1；实例：`Foodula1@86b0192a4890cd9b`；最终场景 `Assets/Scenes/MainMenu.unity`；未播放、未编译、未导入。`read_console` 最终读取错误 0、警告 0。

| 模式 | 选择 | job_id | 总数 | 通过 | 失败 | 跳过 | 耗时 |
|---|---|---|---:|---:|---:|---:|---:|
| EditMode | drivers/cleanup/session/card-deck 聚焦组 | `318b1c5763264dcda32962c7324f9a73` | 252 | 252 | 0 | 0 | 0.7412058 s |
| PlayMode | `Foodula1.Tests.PlayMode.PlayModeAssemblySmokeTests.RuntimeTestAssemblyIsDiscoverable` | `782473cd2be54c4698535b28939e1df2` | 1 | 1 | 0 | 0 | 0.0116497 s |

另有本包之前已完成的重叠聚焦记录：`DriverSkillRuntimeTests`/`DriverSkillRulesTests` 54/54（0.2399762 s）和 True Self 5/5（0.0747744 s）。这些选择与 252 项有覆盖重叠，不作为累计唯一案例再次相加。

## 验收边界

- 纯规则、生产接线、当前程序集 EditMode 聚焦回归和 PlayMode 测试程序集烟测：通过。
- 玩家可操作的完整 RaceCanvas、真实鼠标/键盘焦点与拖动、三尺寸（1920×1080、2560×1440、1280×720）前后截图：本包未生成新截图，未宣称通过。
- 六队/十二车手完整组合、普通与生涯模式、真实存档兼容、教程隔离、硬件长流程：留 I13。

## 相关源文件 SHA256

```text
BE5629A348E9F56C586FCAAD52725A99D77F4C998239A6C0A1C587C7BB887224  Assets/Scripts/Drivers/TrackFuryRuntime.cs
7D4D7ADC2A5E0C58069B240B9EDB8EF39AF401C43B65E0E7DD864C54FF0E2E81  Assets/Scripts/Drivers/BuschPressureRuntime.cs
9F555D071F7EE74A513F5A2FA665FA8A33F8863B9FBE0A13D2DD88A42E4CA36B  Assets/Scripts/Drivers/DriverSkillRules.cs
30265C8A70F822DAC8CE2DA882452AAA06739E87DD9FF9F446C6BEB6C44951E9  Assets/Scripts/Drivers/DriverSkillRuntime.cs
DA2422695CD1DA97BB89E7C5713670E556110C02070FF6A0899290EE2AA04ED9  Assets/Scripts/Core/CardDeck.cs
922CD2257B7E274094AD33D3007ACDF7C00298CF6F1FEC7F76BE198251C40266  Assets/Scripts/Core/RaceSession.cs
AE6B4E21D5566CCB4A2E997FFA36C535E9E2E004944583F9E6DF9FE1C515F5AA  Assets/Scripts/Core/RaceTurnSkillCleanup.cs
16FE4C68C5C853A3875B5EB5335459727842889FB2F662CBAAEA3A1864B223B0  Assets/Scripts/Core/RaceSettledSlipstreamResolution.cs
2A5CFAECB8611AB8FD90DCAC6B22EFD0E55F85EC731B29DA1938BA681587C14E  Assets/Scripts/Core/MVPGameManager.cs
64165A78C1C223DA690DC51F434D3D3A1057AE2B154F10563C62203BE532322A  Assets/Tests/Editor/driver_skill_runtime_test.cs
F8122DA9ABA9E0AA20D6D41342AE2F5E8403E3069A627C5AF89AD960BDCF8AA2  Assets/Tests/Editor/race_driver_skill_activation_adapter_test.cs
E5431414F941F8C2A8E872F69BE2B167DE2771C71291B410FA591A0160EAA98D  Assets/Tests/Editor/final_sprint_turn_end_test.cs
766A1E0F373EC12198BE38028CA224C3864C912C9C5849A5C2023D9B6D8D9CCF  Assets/Tests/Editor/RegressionModules.json
281E077813344AB1E3627BB075DCC28D361A0626C8C39B3492F016A9F62D7BB3  Foodula1_科技树调整草案.docx
94EB7763825F777D477B3FCB8FEE92BAD27E6353E42D8B2CDEB98F3889914F46  Foodula1_车手技能调整草案.docx
```

## Git/工程边界

本包仅写入上述生产代码、测试和记录；没有 commit、push、reset、checkout、rebase、删除/移动用户文件，也没有保存脏场景。其它既有 dirty/untracked 文件（含 `InitTestScene*.unity`、原始 Word 与排除项）保留。
