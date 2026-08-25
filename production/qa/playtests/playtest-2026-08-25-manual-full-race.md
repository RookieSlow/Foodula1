# Playtest Report

## Session Info
- **Date**: 2026-08-25
- **Build**: `412a9e9` (`master`，本地完整对局开始前基线)
- **Duration**: 约 6 分 22 秒（06:46:12Z–06:52:34Z）
- **Tester**: 项目所有者
- **Platform**: Windows / Unity Editor
- **Input Method**: 鼠标
- **Session Type**: Returning / 完整比赛回归
- **Raw log**: `C:\Users\Admin\AppData\LocalLow\DefaultCompany\Foodula1\race-logs\race-20260825-064612-shanghai_dim_sum-24826f34586c4da08ad92fa39d113d9a.log`
- **Review mode**: lean（CD-PLAYTEST skipped — Lean mode）

## Test Focus

上海点心赛道、中国队、1 名 AI 的三圈完整比赛；重点观察热量、阴阳茶、失控、进站、
天气、尾流和完赛闭环。

## First Impressions (First 5 minutes)
- **Understood the goal?**: Yes
- **Understood the controls?**: Yes
- **Emotional response**: 未由日志记录
- **Notes**: 日志连续覆盖每回合状态、档位、出牌、移动计划和主要规则事件，足以还原流程。

## Gameplay Flow

### What worked well
- 三圈、天气从多云到小雨再到大雨、弯道限速变化和圈数推进均有连续证据。
- 玩家在入口前预选进站，越过入口后下一回合执行；第 34 回合完成全热量回收和出口移动。
- 标准冷却与阴阳茶 Go/Recover 都有多次触发记录；热量进出与回合状态总体守恒。
- 第 10 回合记录 1 次 AI 尾流，说明规则路径仍可触发。
- 首次引擎故障在第 9 回合触发一次可恢复失控，后续比赛继续。

### Pain points
- 玩家第 38 回合已经完赛，但收尾仍执行阴阳茶；之后 7 次成功支付热量并移动，第 8 次
  因引擎耗尽触发爆缸。**Severity: Critical**。
- 最终结果同时保留玩家第一完赛顺位、`[爆缸]` 状态、第一名 RP 和 0 XP，属于互相矛盾的
  终局状态。**Severity: High**。
- 卡牌与热量区域变化主要靠日志和瞬时数字刷新理解，缺少流转反馈。**Severity: Low / Polish**。

### Confusion points
- 已完赛赛车仍继续从位置 10 移动到位置 16，玩家无法从规则意图解释这种赛后变化。

### Moments of delight
- 日志能完整追踪进站预选→越线登记→下一回合执行，便于确认延迟进站机制。

## Bugs Encountered

| # | Description | Severity | Reproducible |
|---|---|---|---|
| 1 | 已完赛车辆仍执行回合结束科技效果，最终可由阴阳茶反向变成爆缸 | Critical | Yes；中国 Go 模式先完赛并等待 AI |
| 2 | 上述缺陷令结果状态、RP 与 XP 相互矛盾 | High | Yes；由 #1 派生 |

## Feature-Specific Feedback

### 热量与阴阳茶
- **Understood purpose?**: Yes
- **Found engaging?**: 日志不能判断
- **Suggestions**: 支付与冷却增加区域间卡牌飞行动画；完赛后必须锁定状态。

### 牌堆表现
- **Understood purpose?**: Partially
- **Found engaging?**: 日志不能判断
- **Suggestions**: 在数量徽标之外，让牌背层数表达数量级；顶部实际牌面继续保留。

## Quantitative Data
- **Turns**: 46
- **Finish timing**: 玩家第 38 回合完赛；AI 第 46 回合完赛
- **Spins**: 玩家 2 次，其中第 2 次发生在玩家完赛后且本不应发生
- **Pit stops**: 玩家 1 次（第 34 回合执行）
- **Slipstream events**: 1 次（AI，第 10 回合）
- **Trick plays**: 23 次
- **Yin/Yang Tea**: 阴 20 次、阳 9 次；玩家完赛后另有 8 次阴分支尝试

## Overall Assessment
- **Would play again?**: 未由日志记录
- **Difficulty**: 本局不能单独得出平衡结论
- **Pacing**: 玩家完赛后等待 AI 的 8 个自动回合很短，但终局状态不应继续被修改
- **Session length preference**: 未由日志记录

## Finding Categories

- **Design changes needed**: 无；问题违反现有“完赛锁定结果”意图，不需要改设计。
- **Balance adjustments**: 无；单局不足以支持数值调整。
- **Bug reports**: 完赛终态仍被回合收尾效果修改（本轮已修复并补回归测试）。
- **Polish items**: 卡牌弃置、热量支付/冷却流转动画；数量驱动牌堆厚度（本轮已实现初版）。

修复后证据：终态与视觉相关针对性 EditMode `69/69`、全量 EditMode `430/430` 通过；
Play Mode 运行时注入验证能看到引擎→弃牌堆热量飞行，Console `0` 错误/警告。

## Top 3 Priorities from this session
1. 锁定完赛终态，禁止后续热量、移动和回合结束科技修改结果。
2. 人工重跑一次中国队先完赛场景，确认结果保持完赛、XP 正常且日志不再出现赛后阴阳茶。
3. 人工验收卡牌区域流转和牌堆厚度，调整时长、弧高与层间距。
