---
status: reverse-documented
source: Assets/Scripts/GameManager.cs (lines 47-91), Assets/Scripts/TrackNode.cs
date: 2026-07-19
verified-by: User
---

# Track System Design

> **Note**: This document was reverse-engineered from the existing implementation.
> It captures current behavior. Some sections may be incomplete where
> design intent was not yet formalized.

## 1. Overview

The track system defines the racing circuit as a series of 85 nodes (indices
0–84) connected by a LineRenderer. Each node has an optional speed limit that
triggers heat penalties. The track shape is defined by 85 coordinate points
forming a closed loop.

## 2. Player Fantasy

The player navigates a visible, winding track with distinct corners — each
named corner presents a strategic decision: push through fast and take heat,
or play conservatively.

## 3. Detailed Rules

- **Node Count**: 85 nodes (0–84)
- **Default Speed Limit**: 99 (effectively unlimited) for straight sections
- **Corner Nodes**: 5 nodes have reduced speed limits:
  | Node | Limit | Name |
  |------|-------|------|
  | 13 | 2 | T1 中速直角弯 (Medium Right-Angle) |
  | 48 | 3 | T6 缓直角弯 (Gentle Right-Angle) |
  | 53 | 1 | Chicane (入) (Entry) |
  | 55 | 1 | Chicane (出) (Exit) |
  | 66 | 1 | T7 发卡弯 (Hairpin) |
- **Track Shape**: 85 Vector2 coordinates forming a winding circuit
- **Start Position**: Node 51 (configurable; scene uses 43 for testing)
- **Visualization**: LineRenderer draws white track line; speed-limited nodes
  have yellow sprite color

## 4. Formulas

| Formula | Definition |
|---------|------------|
| Node Initialization | `new TrackNode(index, 99)` — default unlimited |
| Limit Override | `trackNodes[i].speedLimit = {1,2,3}` for specific corners |
| Node Position | `new Vector3(pathCoords[i].x, pathCoords[i].y, 0)` |

## 5. Edge Cases

- **Movement Capped**: Cannot exceed node 84 (track wraps? or ends?)
- **Multiple corners in one move**: Each corner's penalty calculated independently
- **Track Loop**: The LineRenderer closes the loop (`SetPosition(85, pathCoords[0])`),
  but movement is capped at node 84 — does not wrap around

## 6. Dependencies

- TrackNode data class (node index, speed limit, name)
- LineRenderer (visual track line)
- Node prefab with SpriteRenderer (node visualization)
- Heat System (reads speed limits for penalty calculation)

## 7. Tuning Knobs

| Knob | Default | Location |
|------|---------|----------|
| Node Count | 85 | InitializeTrack() loop bounds |
| Track Coordinates | 85 Vector2 values | GetTrackShape() |
| Speed Limit Values | 1, 2, 3 | InitializeTrack() hardcoded |
| Corner Count | 5 | InitializeTrack() |
| Node Sprite | UISprite | NodePrefab |
| Track Line Width | 0.5f | LineRenderer startWidth/endWidth |
| Track Line Color | White | LineRenderer |
| Limited Node Color | Yellow | InitializeTrack() |

## 8. Acceptance Criteria

- [x] 85 nodes generated and positioned correctly
- [x] Track line connects all nodes in order
- [x] 5 corner nodes have speed limits displayed
- [x] Speed-limited nodes are visually distinct (yellow)
- [x] Track is a closed loop (line goes back to origin)
- [x] Truck starts at correct node
- [ ] Track data is data-driven (ScriptableObject or external config)
- [ ] Track lap counter / finish line
- [ ] Visual polish for track and nodes
