# Quick Design Spec: 比赛移动与尾流演出跳过

**Type**: Addition
**System**: Race presentation / movement and slipstream feedback
**GDD Reference**: design/gdd/foodula-1-core-mechanics.md — 1.3 完整回合流程、1.7 尾流机制、1.11 数字版特有的自动化处理
**Date**: 2026-09-09
**Status**: Implemented; 16:9 Play Mode visual acceptance remains open

## Change Summary

在基础移动和回合末尾流演出期间，允许玩家点击屏幕任意位置立即收束剩余表现并进入既定结算。该入口解决六队十二车大混战中逐车动画和尾流特写累计等待时间过长的问题。

## Motivation

移动和尾流规则已经在表现开始前确定；等待每辆车逐格插值、相机缓冲、超车/尾流特写和尾流奖励移动，会让大混战的节奏被演出时间拖慢。玩家需要在想看演出时保留它，也需要在熟悉规则或重复测试时快速到达下一输入阶段。

## Design Delta

当前 GDD 要求基础移动按位置顺序播放，并在所有基础移动完成后进入独立尾流表现，再应用额外移动。本次补充：

> 在移动与尾流的表现窗口内，主鼠标点击可跳过表现，但不能跳过已经确定的规则结算或必需的玩家决策。

## New Rules / Values

1. 移动表现窗口从基础移动阶段开始，覆盖逐格移动、移动前后缓冲和超车特写。
2. 尾流表现窗口覆盖尾流特写、尾流后的额外移动和残留的卡牌飞行动画。
3. 窗口内的主鼠标点击不区分点击位置；只记录一次跳过请求，后续所有表现协程读取同一请求。
4. 跳过只改变表现：
   - 剩余车辆直接定位到本回合已计算的目标格；
   - 省略等待、插值、相机缓冲、超车/尾流特写；
   - 尾流链、基础移动、弯道/圈数/地标/维修区穿越和日志仍按原顺序结算；
   - 尾流奖励移动仍全部执行，不会因跳过而减少。
5. 印第安纳波利斯换道、维修区预选、教程翻页等必需的玩家决策仍需玩家完成；跳过请求不能替代这些决策。
6. 结束尾流奖励移动后关闭跳过窗口；选档、出牌、弃牌、完赛和主菜单界面不响应该入口。
7. 状态栏在两个窗口显示“点击任意位置跳过动画”，让入口可发现但不增加额外遮罩或按钮。

## Affected Systems

| System | Impact | Action Required |
|---|---|---|
| MVPGameManager | 管理窗口生命周期，轮询主鼠标点击，向所有演出传递同一跳过状态 | Applied |
| CarMovementAnimator | 逐格移动收到请求后直接定位到目标节点 | Applied |
| RaceEventFX | 超车/尾流特写收到请求后立即执行清理并恢复时间倍率 | Applied |
| CardHandUI / CardZoneTransitionUI | 尾流阶段开始前可收束残留卡牌飞行 | Applied |
| 纯层 EditMode tests | 覆盖状态生命周期、车辆直接定位和尾流演出清理 | Applied |

## Acceptance Criteria

- [x] 移动窗口内点击后，十二车剩余移动动画不再逐格等待，最终位置和移动日志保持正确。
- [x] 尾流窗口内点击后，尾流特写和尾流奖励移动立即收束；所有已计算的尾流加成仍然应用。
- [x] 点击不会改变弯道、圈数、地标、维修区穿越或尾流规则结果。
- [x] 跳过入口不会在选档、出牌、弃牌和主菜单阶段误触发。
- [x] 定向 EditMode 测试覆盖跳过状态、车辆移动和尾流演出清理。
- [ ] 16:9 Play Mode 人工确认提示文本、相机层级和大混战最终状态。

## GDD Update Required?

Yes. design/gdd/foodula-1-core-mechanics.md 的 1.11 表格补充移动/尾流长演出的跳过规则；运行时说明同步到 docs/memory/game-mechanics.md。
