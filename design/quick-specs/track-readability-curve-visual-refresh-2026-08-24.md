# Quick Design Spec: 赛道可读性与弯道视觉重制

**Type**: Tweak
**System**: Track Presentation
**GDD Reference**: `design/gdd/foodula-1-visual-style.md` §2.4、§4.1；`design/gdd/foodula-1-tracks.md`
**Date**: 2026-08-24
**Status**: Implemented; Silverstone Play Mode verified, remaining tracks pending spot-check

## Change Summary

在不改变赛道 JSON、节点位置或弯道判定的前提下，重制运行时弯道覆盖层，并增加玩家位置光环、1-based 格数和局部格号刻度。目标是在正常比赛镜头下同时看清“我在哪里、下一段是什么弯、限速多少”。

## Motivation

现有统一黄色直线段蒙版缺少弯道等级差异，节点连接处也容易呈现折线感；玩家位置主要依赖 0-based HUD 文本，无法快速对应赛道画面。新表现继续遵循“信息优先、桌游感、特效克制”的视觉原则。

## Design Delta

当前视觉规范要求弯道格子有等级颜色、当前车辆有科技蓝光环，并在玩家附近显示格号。此次将这些要求落实为以下可执行规则：

1. 弯道覆盖层使用仅供渲染的平滑采样，不修改任何游戏节点坐标。
2. 弯道采用深色外沿加等级色内带：Lv1 安全绿、Lv2 琥珀黄、Lv3 警示珊瑚红；端点和折点使用圆角。
3. 弯心徽标与弯道等级同色，限速数字保持白色粗体和深色描边。
4. 玩家赛车使用科技蓝脉冲圆环，并在上方显示 `格 X/N`；X 为 1-based。
5. 玩家前后各 6 格显示短刻度与 1-based 格号。密集赛道按相邻格距离自动抽样文字，但保留所有短刻度和当前格。
6. F8 调试覆盖层继续使用 0-based 编号，不与玩家信息混用。

## New Rules / Values

| 参数 | 默认值 | 范围/说明 |
|---|---:|---|
| 弯道平滑细分 | 5 | 每段 1–8 个渲染采样 |
| 局部格半径 | 6 | 玩家前后各 5–7 格 |
| 玩家光环 | 科技蓝 `#58A6FF` | 使用未缩放时间轻微脉冲 |
| Lv1 / Lv2 / Lv3 | `#3FB950` / `#D29922` / `#F78166` | 透明覆盖，不遮挡背景细节 |
| 文字最小间距 | 0.72 世界单位 | 更密时自动每 2–3 格显示编号 |

## Affected Systems

| System | Impact | Action Required |
|---|---|---|
| TrackManager | 重制弯道渲染并公开只读展示数据 | 修改代码，不改规则 |
| TrackReadabilityOverlay | 新增玩家光环与局部刻度 | 新增运行时表现组件 |
| HUDUI | 玩家和 AI 的赛道位置统一为 1-based | 修改展示文本 |
| TrackPresentationRules | 平滑采样、格号和密度规则 | 增加纯函数与 EditMode 测试 |

## Acceptance Criteria

- [ ] 所有 JSON 赛道和 fallback 都使用等级分色的平滑弯道带，限速仍来自当前弯道规则。
- [x] 玩家赛车周围显示科技蓝光环，上方显示正确的 `格 X/N`。
- [x] 玩家附近前后各 6 格显示刻度；密集赛道文字不会全部挤在一起。
- [x] HUD 中所有比赛位置文本使用 1-based 格号。
- [x] 渲染采样不改变 `TrackManager.GetNodePosition`、移动、圈数或弯道判定。
- [x] 新增 EditMode 测试并通过全量回归；Silverstone Play Mode 截图确认无遮挡和明显重叠。

## GDD Update Required?

No. 现有视觉 GDD 已明确要求等级色弯道、玩家蓝色光环和局部格号；本规格只细化实现参数。
