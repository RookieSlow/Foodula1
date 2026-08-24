# Quick Design Spec: RaceCanvas 所见即所得布局工作流

**Type**: Tweak
**System**: Race HUD Authoring
**GDD Reference**: `design/gdd/foodula-1-visual-style.md` §2.1–2.4
**Date**: 2026-08-24
**Status**: Implemented and verified

## Change Summary

将比赛 HUD 的最终四区布局从“只在 Play Mode 动态生成”改为写入
`RaceCanvas.prefab`。运行时优先使用 Prefab 中已经编排的 RectTransform，仅当旧版
或不完整 Canvas 缺少主面板时才重建默认布局。

## Motivation

原工作流中 Scene/Prefab 视图只显示基础控件，Play Mode 又会重新创建面板、改变父级并
覆盖锚点，导致美术和 UI 调整无法所见即所得。新工作流允许直接在 Prefab Mode 查看并
调整最终界面，同时保留旧场景和程序化 HUD 的兼容能力。

## Design Delta

视觉规范要求比赛 HUD 保持稳定的信息分区和清晰层级。本规格不改变玩家看到的布局，
只改变其创作来源：已编排 Prefab 是位置和尺寸的权威来源，运行时代码不覆盖人工调整。

## New Rules / Values

1. `RaceCanvas.prefab` 必须包含 `OperationPanel`、`ScoreboardPanel`、`TrackFrame` 和
   `DeckTablePanel` 四个主面板。
2. 四个主面板存在时，`RaceUILayoutController.ApplyLayout` 只绑定面板及缺失引用，
   不修改任何 RectTransform 或卡牌尺寸。
3. 主面板缺失时，控制器使用当前默认布局补齐，保持旧 Canvas 的运行兼容性。
4. 编辑器工具提供“打开 Prefab”和“写入默认布局”两个显式操作；写入默认布局会警告
   用户其位置将被重置，不在 Play Mode 自动执行。

## Affected Systems

| System | Impact | Action Required |
|---|---|---|
| Race HUD | 最终布局改为 Prefab 可见、可编辑 | 烘焙当前默认布局 |
| Race Camera | 继续从已绑定的 `TrackFrame` 读取视口 | 保持现有接口 |
| Legacy UI fallback | 缺少主面板时继续自动生成 | 保留回归测试 |

## Acceptance Criteria

- [x] `RaceCanvas.prefab` 在编辑模式包含完整四区布局。
- [x] 已编排布局在运行时不被重写位置或卡牌尺寸。
- [x] 缺少主面板的旧 Canvas 仍能得到默认布局。
- [x] 编辑器提供打开 Prefab 和显式重建默认布局的按钮入口。
- [x] Unity EditMode 全量测试通过（403/403）。
- [x] Play Mode 画面与烘焙前基线一致，Console 无项目错误。

## GDD Update Required?

No。玩家视觉规则不变，本规格只调整 Unity UI 的创作和维护工作流。
