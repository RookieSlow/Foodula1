# Quick Design Spec: Standard Inner Line and Indianapolis Player-Lane Visuals

**Type**: Tweak  
**System**: Track presentation and vehicle positioning  
**GDD Reference**: `design/gdd/foodula-1-tracks.md`  
**Date**: 2026-08-05

## Change Summary

普通赛道的赛车默认使用内线；只有两辆仍在比赛中的赛车处于同一圈、同一赛道节点时，稳定排序靠后的赛车才显示在外线，形成并排效果。印地赛道继续使用显式车道选择，不套用普通赛道的自动换线规则。

## Motivation

此前普通赛道的玩家和 AI 固定分居两条线，导致没有并排时也显得像两条独立线路。该调整让视觉表现符合“默认内线、并排才外移”的赛道阅读方式，同时保留印地内外线限速不同的策略表达。

## New Rules / Values

1. 非 `indianapolis_burger` 赛道：
   - 所有车辆默认使用内线。
   - 两车同圈且同节点时，`session.Players` 中排序靠后的车辆视为后车，使用外线；前车保持内线。
   - 该规则只改变视觉车道，不改变位置、移动格数或弯道限速。
2. `indianapolis_burger`：
   - 车辆继续使用各自记录的显式车道索引。
   - 玩家在起终点选择向内/保持/向外后，车辆立即移动到所选车道。
   - AI 不因普通赛道的并排规则自动换道。

## Acceptance Criteria

- [ ] 普通赛道无并排时，所有赛车均位于内线。
- [ ] 普通赛道两车同圈同节点时，后车位于外线，前车仍位于内线。
- [ ] 普通赛道视觉换线不改变位置、移动或弯道判定。
- [ ] 印地玩家选择车道后，玩家赛车立即显示在对应车道。
- [ ] 印地内外线限速和起终点换道交互不回归。
- [ ] Unity 编译无错误，既有规则测试继续通过。
