# I10 马青骅真实 Recover 留置与下一 Go 兑现

日期：2026-11-06（Asia/Shanghai）

## 本包边界

- 按 `implementation-plan.json` I10 锁定范围接入马青骅 `ExtremeSwitch`：只在本人真实输入窗口声明，真实原生 Recover 冷却时冻结一张旧手永久热，下一次合格 Go 输入兑现移动加成。
- 留置不伪造赠冷、临时热、免费热量或连续 Go；普通冷却、普通罚热、固定费用、预测副本和未知存档字段保持原生命周期。
- 马青骅 Tier 1/2/3 的下一 Go 移动分别为 +2/+3/+3；每场次数保持 1/1/2。被动 AllRounder 仅在真实 Recover→Go 后首个弯生效，Tier 1/2 每圈一次，Tier 3 每圈两次；Tier 2/3 首弯额外减热 1（最低 0）。

## 生产接线

- `MaAdaptationRuntime` 保存输入窗口、留置旧手牌实例、下一 Go 兑现、每圈被动配额及中断清理状态。
- `DriverSkillRuntime`、`DriverSkillRules` 将旧通用激活路径拒绝为“真实 Recover 声明”，并把 Ma 的移动/弯心上限/首弯减热接到现有输入与弯道结算。
- `CardDeck` 提供排除已冻结牌实例的原生冷却来源遍历；`RaceSession` 提供声明、真实 Recover 提交、下一输入解析及新圈重置 API。
- `MVPGameManager`/`HUDUI` 复用现有确认窗、按钮禁用原因与取消路径；`RaceTurnSkillCleanup` 在进站/跳过等回合中断时释放留置状态，不返还次数或热量。
- `RegressionModules.json` 新增 `MaAdaptationRuntimeTests`；正式车手 GDD 已同步。

## 原生 Unity 证据

Unity 2022.3.62f3c1，实例 `Foodula1@86b0192a4890cd9b`，活动场景 `Assets/Scenes/MainMenu.unity`；测试前后未播放、未保存脏场景，编译完成。最终 Console 错误 0、警告 0；中途曾读到 1 条 MCP WebSocket 初始化 Warning，非项目编译/运行错误，最终读取已清零。

| 原生 RunFinished 作业 | 模式 | 选择 | 总数 | 通过 | 失败 | 跳过 | 耗时 |
|---|---|---|---:|---:|---:|---:|---:|
| `68b650e5560f4c6b8453abdfe346754e` | EditMode | `MaAdaptationRuntimeTests` | 3 | 3 | 0 | 0 | 0.078413s |
| `ed83bdc37fad498090d529dbf0fd1412` | EditMode | `DriverSkillRuntimeTests` + `DriverSkillRulesTests` + `CardDeckTest` + `HeatCoolingSourcesTests` | 100 | 100 | 0 | 0 | 0.1705152s |
| `087061a37a2e4b649236bc85801a319f` | EditMode | `MaAdaptationRuntimeTests` + `DriverSkillRuntimeTests` + `DriverSkillRulesTests` + `RaceTurnSkillCleanupTests` | 71 | 71 | 0 | 0 | 0.1875679s |
| `6371db8c9d0a4818b8aaeba0b2409b62` | PlayMode | `Foodula1.Tests.PlayMode.PlayModeAssemblySmokeTests.RuntimeTestAssemblyIsDiscoverable` | 1 | 1 | 0 | 0 | 0.0193393s |

以上作业存在测试选择重叠，不把各行相加当作独立覆盖数；本包记录为四次原生 RunFinished，逐次总计 175 次执行、175 通过、0 失败、0 跳过。此前一次焦点重跑暴露的是新夹具把 1/1/2 次数误写成了 2 的期望，已修正后再运行；该失败尝试不计入通过。PlayMode 烟测只证明当前运行时程序集可发现，不代表完整比赛。

## 文件哈希（SHA-256）

```
Assets/Scripts/Drivers/MaAdaptationRuntime.cs A533227079CF5D270532BC781D777BFEAA85D790FD8E954406D58A9B7C518E90
Assets/Scripts/Drivers/MaAdaptationRuntime.cs.meta 69906609F00997255835D8900F49288522A84A424AAF27AD2AE31800EEF242A3
Assets/Scripts/Drivers/DriverSkillRuntime.cs 2C4B4C8820B4737473C71685C15D451B1F23FF2DDC8A847E47051B49EA75BB0D
Assets/Scripts/Drivers/DriverSkillRules.cs 472DD75069F0A4D498498C6ECE7E78E64F7E537C88A85880A5D5AF073E2783B3
Assets/Scripts/Core/CardDeck.cs FCF5FA75A94B3CC76E32608DE16C357D5149C745437727E245C7B84E685676A1
Assets/Scripts/Core/RaceSession.cs 497A1FE938425DB33F909FD6094FCD549E0C76079865817DC01D27589DD22586
Assets/Scripts/Core/MVPGameManager.cs 2174725F8EB45054ADB740B6A573D67991C6135CF30446F5F55DEF9A59699E84
Assets/Scripts/Core/RaceTurnSkillCleanup.cs 1D13D0D3870CB43B2FC6403107BF9B02F963F8B1D228882D304032B0093B55CB
Assets/Scripts/Drivers/DriverData.cs 4000D23EBBF59EA5C664F2BD1F07353BD5F0857EF745727EADAB5B78C55384CC
Assets/Scripts/UI/HUDUI.cs 510665B08AA998BD2E99D6E26EC563FF015AA19A58D968C1BF094F3AFF4E5D67
Assets/Tests/Editor/driver_skill_runtime_test.cs 7B9548FF61818D16C4D86B171491282D11AA8B9077915332DD161F610E2418CE
Assets/Tests/Editor/driver_skill_rules_test.cs D326E23C7F4F352BE784B0540467C4E3ECAD91D4C517C53982D3A25432EDE367
Assets/Tests/Editor/ma_adaptation_runtime_test.cs 285E0B1B9E542BF40D272C656459415E61AFFC21DE54A3BF945BA6D43D9E53A5
Assets/Tests/Editor/RegressionModules.json FC6ACF7A12F3BE4B28863508951C042ED470955360B8575C3239B19126FBB5D5
design/gdd/foodula-1-drivers.md 4A6D39655E7E410FE00B5E1F1D780E131B53B10C659CA7E9E2DA8E058883E838
```

计划、连续性与本证据文件哈希：

```
design/planning/tech-driver-rework-2026-10-22/implementation-plan.json 5B84EF8E1F45CEB3F5B866A7BBECEF00C0621B6DE4E0206D875633C983047057
design/planning/tech-driver-rework-2026-10-22/progress.json 163D24AC311AAD93EB42FFF9CA398DBCBBFA75C59CFDA29B93455262C04C4AE6
docs/memory/project-overview.md E3325F8B5F47C3DE926A990AC52CFD6E2F7FC3DB82B0B856D7DF6A3619B32044
docs/memory/game-mechanics.md 6F4096CBE425D8080F6EAD504D6A72FBA88F752DF1C5E7C5BC0CC0EC3676089A
docs/memory/current-task-list.md ED0CB8E917BB725EFD3ACEF23EC4C156339807BCB83FA37C0DA70588392AB0BA
production/qa/evidence/tech-driver-I10-2026-11-06/README.md A916F8C603E3DB6FC8975976E58087CBA71D35B624D3CB9825D59153D05A30FB
```

## 验收边界

- 纯规则、生产接线、当前 C# 程序集编译及聚焦 EditMode 已通过；旧手牌实例排除、下一 Go 失效、每圈配额和中断清理均有测试。
- 本包没有新增或声称三尺寸截图、真实鼠标/键盘焦点拖动、普通/生涯完整比赛、真实存档兼容、普通 AI 车手技能或 I13 跨系统长流程；这些继续留在后续收口。
- 未提交、未推送、未重置/切换分支；其他工作区修改、未跟踪文件与 `InitTestScene*.unity` 保留。
