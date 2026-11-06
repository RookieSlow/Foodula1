# Tech/Driver I07 — US BBQ B 与母亲之路复兴上限

日期：2026-11-06  
范围：按 `implementation-plan.json` 锁定的 US L2 B / L3 A 有界生产切片。

## 锁定规则与实际接入

- L2 B：保留热量牌速度 2、占一个普通速度槽；当前区域借额为 +1；不再以 BBQ 烟幕阻断尾流。区域效果仍由双地标连续门控和选牌封账后的实例状态决定，新热不回填。
- L3 A：短/中/长赛程的维修实际付热 1/2/3；母亲之路复兴终极每场共用一次，先清偿区域借额，净回引擎与位移均最多 3，超出的永久热留在手牌，临时热继续按原生命周期销毁。
- 保持原节点 ID、RP、解锁等级、车手身份/XP/主动次数；不重做普通罚热、普通 Recover 或基础挡位。

## 生产文件

- `Assets/Scripts/TechTree/TechTreeRules.cs`
- `Assets/Scripts/TechTree/TechTreeData.cs`（同步 L2 B 注释，避免旧 +2/烟幕描述残留）
- `Assets/Scripts/TechTree/RaceMotherRoadExecution.cs`
- `Assets/Scripts/Core/RaceSession.cs`
- `Assets/Scripts/TechTree/TechTreeDatabase.cs`
- `Assets/Scripts/UI/CardHandUI.cs`
- `Assets/Tests/Editor/race_session_test.cs`
- `Assets/Tests/Editor/race_mother_road_execution_test.cs`
- `Assets/Tests/Editor/tech_tree_rules_test.cs`
- `design/gdd/foodula-1-tech-tree.md`

## 原生验证

Unity 2022.3.62f3c1，实例 `Foodula1@86b0192a4890cd9b`。

- `RaceSessionTest`、`BBQEligibilityBoundaryTests`、`RaceMotherRoadExecutionTests`、`TechTreeRulesTests`：EditMode **335/335 通过**，失败 0、跳过 0，0.4234552 s。
- PlayMode 运行时程序集烟测 `Foodula1.Tests.PlayMode.PlayModeAssemblySmokeTests.RuntimeTestAssemblyIsDiscoverable`：**1/1 通过**，失败 0、跳过 0，0.0180639 s。
- `BBQHeatCardLifecycleTests`、`RaceBBQHumanCommitAdapterTests`、`RaceTechnologySetupTests`、`RaceParticipantTechnologySetupTests`、`TechTreeProfileCodecTests`：EditMode **162/162 通过**，失败 0、跳过 0，0.482971 s。

三次 RunFinished 均为成功；本包原生执行总量为 498（其中聚焦类存在与既有覆盖重叠，不将其直接累加为累计唯一测试数）。

## Unity 状态与边界

- 最后观测活动场景：`Assets/Scenes/MainMenu.unity`。
- 最后观测未播放、未暂停、未编译、未导入；Test Runner 作业已结束。`editor/state` 资源随后显示为 stale，故不把其 `ready_for_tools` 当作更强证据；以已完成的原生 RunFinished、编译后 Console 复查和上述直接状态字段为界。
- 聚焦测试期间产生的两条预期诊断（生涯无活动科技快照、CN ATTACK 牌保证提示）在最终 Console 清理后复查为错误 0、警告 0；无新的编译错误。
- 本包没有新截图；没有把旧图当作改后视觉证据。三尺寸（1920×1080、2560×1440、1280×720）RaceCanvas、真实鼠标/键盘/焦点、拖动/遮罩，以及完整普通/生涯模式、真实存档兼容和 I13 长流程仍未验收。
- 未执行提交、推送、reset、checkout、rebase；其他脏文件、未跟踪文件及 InitTestScene 保留。

## 本包源文件 SHA256

```text
Assets/Scripts/TechTree/TechTreeRules.cs=3203CF6D9C4A5C7AC47CBC1DDE6327E1E3DDC2D7340AACCB37E0B6AF9F8586F9
Assets/Scripts/TechTree/TechTreeData.cs=9AE29785D64E170B8AE237D6DEEBD5C5A988C4FE0589A8177FBD51608D0A641F
Assets/Scripts/TechTree/RaceMotherRoadExecution.cs=9743A99356C8FEB482C95F6D06C21404321413DAE090BA0C933E5EAA09714581
Assets/Scripts/Core/RaceSession.cs=E3E75C9B0EFF9CFC6595911415809D2C29A69F58CC7310C04F7CF99BC7658DF6
Assets/Scripts/TechTree/TechTreeDatabase.cs=150941FE6A416874A3F027E2085431B784A59D9172604934CB0740DA74447CE6
Assets/Scripts/UI/CardHandUI.cs=125B1E8818274CD7D8911F18DFEA8B3A3C0969E725480D3BAE0B4632F9086C53
Assets/Tests/Editor/race_session_test.cs=F78BEB89953BAD0F168A4567717640CE399FC1DBD4040FF40ABC066DB771DCE7
Assets/Tests/Editor/race_mother_road_execution_test.cs=E890150A640B72329AD66E633FCB9A2C00D4061441A70CE7229A4A217BF62F35
Assets/Tests/Editor/tech_tree_rules_test.cs=CA9C3348F73CC6110FF78275AF25A824C5F1013ED76CC1572526AFC65D2DDD54
design/gdd/foodula-1-tech-tree.md=96DD0902CBC540E15FA50D4754B10E00903B794D112CAB3F96030BD81F436FC0
```

## 下一包

I08：斯图尔特与布什的有限窗口、实际跟车、付费与拒绝入口；完整模式、存档、三尺寸 RaceCanvas 与 I13 跨系统收口继续保留边界。
