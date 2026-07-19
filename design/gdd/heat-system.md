---
status: reverse-documented
source: Assets/Scripts/GameManager.cs (lines 186-253)
date: 2026-07-19
verified-by: User
---

# Heat / Cold Storage System Design

> **Note**: This document was reverse-engineered from the existing implementation.
> It captures current behavior. Some sections may be incomplete where
> design intent was not yet formalized.

## 1. Overview

The heat system represents the food truck's refrigeration capacity ("Cold
Storage" / 冰鲜库). Driving too aggressively through corners generates heat,
depleting cold storage. When cold storage drops below zero, the truck overheats
("blows up" / 爆缸) and the game ends.

## 2. Player Fantasy

The player experiences tension between speed and preservation. Driving fast
feels powerful, but corners demand restraint. Managing the cold storage creates
a push-your-luck dynamic — how much heat can you afford to take?

## 3. Detailed Rules

- **Cold Storage Pool**: Starts at 6 (configurable via Inspector).
- **Heat Generation**: Occurs when the player's movement steps exceed a node's
  speed limit. For each traversed node where `moveSteps > speedLimit`, the
  penalty is `moveSteps - speedLimit`.
- **Cumulative Penalty**: All penalties across the entire movement are summed
  and applied once after the truck stops.
- **Overheat**: When `currentHeat - totalPenalty < 0`, the truck overheats.
  A special message is displayed: "爆缸发生！冰鲜库被榨干！"
- **No Recovery**: There is currently no way to recover cold storage.
- **Post-Overheat**: Further turns are blocked when `currentHeat < 0`.

## 4. Formulas

| Formula | Definition |
|---------|------------|
| Per-Node Penalty | `max(0, moveSteps - node.speedLimit)` |
| Total Penalty | `sum(perNodePenalty)` for all nodes `i` in `[currentPos+1, targetPos]` |
| New Heat | `currentHeat - totalPenalty` |
| Overheat Condition | `currentHeat < 0` |

## 5. Edge Cases

- **Exact match**: If moveSteps == speedLimit, no penalty (not > limit)
- **Below limit**: If moveSteps < speedLimit, no penalty
- **Zero heat remaining**: Game checks before allowing PlayTurn()
- **Multiple corners in one move**: Each corner's penalty is calculated and
  summed independently — moving through both Chicane nodes (53 and 55) with
  a step value of 3 would generate (3-1) + (3-1) = 4 heat

## 6. Dependencies

- TrackNode.speedLimit (defines per-node limits)
- GameManager.currentHeat (persistent state)
- GameManager.PlayTurn() (triggers penalty calculation)
- Track System (provides speed limit values)

## 7. Tuning Knobs

| Knob | Default | Location |
|------|---------|----------|
| Initial Cold Storage | 6 | GameManager.currentHeat (serialized) |
| Speed Limit Values | 1–3 | GameManager.InitializeTrack() (hardcoded) |
| Overheat Threshold | < 0 | GameManager.PlayTurn() (conditional) |

## 8. Acceptance Criteria

- [x] Heat penalty is calculated when moveSteps > speedLimit
- [x] Penalty is per-node: moving through multiple corners sums penalties
- [x] Cold storage depletes correctly on penalty
- [x] Game over when cold storage < 0
- [x] Game over message is displayed
- [x] Further moves are blocked after overheat
- [ ] Heat recovery mechanic (future)
- [ ] Visual feedback for heat level
- [ ] Cold storage is data-driven (currently hardcoded)
