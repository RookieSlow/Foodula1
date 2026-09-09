# Quick Design Spec: 局内操作二次确认偏好

**Type**: Addition
**System**: Race HUD interaction safety / player settings
**GDD Reference**: `design/gdd/foodula-1-core-mechanics.md` §1.11；`design/gdd/foodula-1-tutorial-settings-encyclopedia.md` 设置与百科；`design/ux/interaction-patterns.md`
**Date**: 2026-09-09
**Status**: Implemented; 16:9 Play Mode visual acceptance remains open

## Change Summary

为比赛中会改变状态或离开当前比赛的操作增加统一二次确认，并在设置中用 bit mask 逐项开关。
默认开启确认换挡、出牌/弃牌、车手技能、重新开始、返回主菜单、维修区决定和印地换道；
换挡选择默认关闭，因为它只是待提交选择，确认换挡仍受保护。

## Scope

| Action | Gate | Default |
|---|---|---|
| 换挡选择 | `GearSelection` | 关闭 |
| 确认换挡 | `GearCommit` | 开启 |
| 出牌 / 弃牌 | `CardAction` | 开启 |
| 车手技能 | `DriverSkill` | 开启 |
| 重新开始 | `ResetRace` | 开启 |
| 返回主菜单 | `ReturnToMenu` | 开启 |
| 维修区决定 | `PitDecision` | 开启 |
| 印地换道 | `LaneChange` | 开启 |

教程翻页、教程跳过/退出、卡牌选中、百科和限速明细关闭按钮不属于比赛状态提交，不弹确认。

## Acceptance Criteria

- [x] 所有八类比赛操作共用一个模态确认入口，关闭某项后原回调立即执行。
- [x] v1 设置读取后迁移到安全默认掩码，未知 bit 在归一化时丢弃。
- [x] 取消确认不改变比赛状态，确认才执行原操作。
- [x] 设置 UI 可以逐项显示并修改开启/关闭状态。
- [ ] 16:9 Play Mode 确认弹窗层级与设置保存后的跨场景持久化走查。
