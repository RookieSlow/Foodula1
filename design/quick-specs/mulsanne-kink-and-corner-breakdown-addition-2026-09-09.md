# Quick Design Spec: 慕尚高速偏弯与弯道限速构成

**Type**: Addition
**System**: Track readability / corner rules explanation
**GDD Reference**: `design/gdd/foodula-1-tracks.md` §2.7；`design/gdd/foodula-1-core-mechanics.md` §1.5；`design/ux/interaction-patterns.md`
**Date**: 2026-09-09
**Status**: Implemented; runtime click/readability acceptance remains open

## Change Summary

在勒芒旧慕尚第 60 格加入单格 `mulsanne_kink` 高速弯心（Lv1、原始限速 6），保留旧慕尚
长直道冲刺感，同时给赛道视觉一个明确方向锚点。弯心旁的数字改为显示当前玩家有效限速；
单击数字可打开只读的构成明细。

## Formula

`有效限速 = max(1, 原始限速 + 天气影响 + 车手技能影响 + 车队操控影响 + 科技树影响)`

显示层复用 `RaceSession.GetCornerLimitBreakdown`，不复制或改变结算规则；天气免疫显示天气
负修正与车手正向补偿。无弯道的 99 不建立可点击弯道数字。

## Acceptance Criteria

- [x] JSON 保持 142 格、2 圈；cell 60 是 `mulsanne_kink` apex、Lv1、limit 6。
- [x] 当前权威直道分布为 `44/37/21/16`，最长 44 格仍满足冲刺门槛。
- [x] 运行时数字从同一规则入口获得有效限速，点击后显示五项带符号修正。
- [x] 限速构成纯层测试覆盖原始/天气/车手/车队/科技树项，赛道回归覆盖高速偏弯元数据。
- [ ] 16:9 Play Mode 在普通车手、雨天和有科技状态下分别点按数字，确认位置、层级、换行和关闭行为。
