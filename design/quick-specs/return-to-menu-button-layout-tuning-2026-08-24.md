# Quick Design Spec: 比赛返回主菜单按钮位置

**Type**: Tuning
**System**: Race HUD Layout
**GDD Reference**: `design/gdd/foodula-1-visual-style.md` §2.2–2.4
**Date**: 2026-08-24
**Status**: Implemented and Play Mode verified

## Change

| Parameter | Old Value | New Value | Rationale |
|---|---|---|---|
| 运行时父级 | RaceCanvas 根节点 | OperationPanel | 与比赛中其他操作归为同一视觉组 |
| 锚点 | 左上绝对位置 `(24, -24)`、尺寸 `160×44` | OperationPanel 内 `x 0.08–0.92`、`y 0.405–0.475` | 位于重新开始按钮下方、提示日志面板上方，不遮挡标题和赛道 |

## Tuning Knob Mapping

该布局由 `RaceUILayoutController` 统一管理，不使用 gameplay 数据文件。新位置位于现有操作栏的预留空隙内，并沿用操作按钮的水平宽度和间距。

## Acceptance Criteria

- [x] 返回按钮运行时成为 `OperationPanel` 的子对象。
- [x] 按钮位于重新开始按钮下方、提示日志面板上方。
- [x] Play Mode 截图中不遮挡“其他操作菜单”、比赛信息或赛道。
- [x] 点击监听与返回主菜单行为保持不变。
