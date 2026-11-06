# I05c2b — 阿斯卡里蓝色幸运生产回合结算

## 范围

本包仅接入 D06「蓝色幸运」的生产规则、运行态、真实回合结算和清理边界；保留 I05c2 已完成的目标选择、陈旧计划保护、同牌互斥和异牌合并确认，不推进 I06。

- Lv2：连续 3 个阿斯卡里本人无热合格输入后，下一次本人输入准备一次弯道限速 +1。
- Lv4/Lv6：连续 2 个本人无热合格输入后触发。
- Lv6：触发回合额外冷却 +1。
- 热量来自 RaceSession.TrackHeatPaid 的实际支付，含不足额支付；跳过、进站和其他车手回合不推进阿斯卡里输入时钟。
- 触发效果只消费当前车手运行态的一次待用实例；CreatePresentationCopy 只用于预测，不消费真实实例。

## 生产改动

- Assets/Scripts/Drivers/DriverSkillRules.cs
  - 新增蓝色幸运无热阈值和 Lv6 额外冷却规则。
  - 蓝色幸运触发时复用现有一次性弯道限速修正。
- Assets/Scripts/Drivers/DriverSkillRuntime.cs
  - 记录本人输入段的真实热支付、连续无热样本和待触发状态。
  - 在本人输入边界结算上一段样本；回合效果重置不影响既有其他车手被动时钟。
- Assets/Scripts/Core/RaceSession.cs
  - TrackHeatPaid 同步喂给车手运行态，同时保留德国科技热量记录。
- Assets/Tests/Editor/blue_luck_test.cs
  - 覆盖规则阈值、Lv2/Lv6 触发、热支付重置、RaceSession 实际输入结算和预测隔离。

## 真实验证

| 选择 | 总数 | 通过 | 失败 | 跳过 | 耗时 |
|---|---:|---:|---:|---:|---:|
| BlueLuckTests | 5 | 5 | 0 | 0 | 0.0842922 s |
| DriverSkillRulesTests + DriverSkillRuntimeTests + AscariPrecisionTests + RaceTurnSkillCleanupTests | 101 | 101 | 0 | 0 | 0.1705166 s |

Unity 2022.3.62f3c1；实例 Foodula1@86b0192a4890cd9b；活动场景 Assets/Scenes/MainMenu.unity；未播放；最终 read_console 返回 0 条 error/warning。编辑器状态资源最后一次轮询为 stale，未将 stale 状态夸大为更强 ready 证据。

## 未验收边界

完整 RaceCanvas、物理鼠标/键盘焦点、硬件输入、全部赛事模式、真实存档兼容和 I13 跨系统长流程仍未验收。被动效果不新增玩家按钮，本包没有新截图，也不把静态文案或旧截图当作视觉通过。

## 文件完整性

本轮 SHA256 见同目录 summary.json。工作区的其他 dirty/untracked 文件、InitTestScene 场景和既有证据均保留；未执行 commit、push、reset、checkout、rebase、场景保存或清理。
