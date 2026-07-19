# Systems Index

> Design order: Foundation → Core → Feature → Presentation → Polish
> Status: Not Started | In Progress | In Review | Approved | Needs Revision | Retired

## MVP (buildable first milestone)

| # | System | File | Status | Notes |
|---|--------|------|--------|-------|
| 1 | Game Concept (MVP) | `game-concept.md` | Draft | Created 2026-07-19 — scopes full vision to core HEAT loop. Replaces retired prototype doc |
| 2 | Card System | `card-system.md` | Retired | Reverse-documented from prototype — incompatible with deck/cycle model |
| 3 | Heat System | `heat-system.md` | Retired | Reverse-documented from prototype — incompatible with heat-cards model |
| 4 | Track System | `track-system.md` | Retired | Reverse-documented from prototype — per-node penalty bug, needs per-corner-segment |

## Prototype Archive

| File | Description |
|------|-------------|
| `game-concept-ARCHIVED-2026-07-19.md` | Old prototype behavior audit — retired 2026-07-19 per design review |

## Full Vision Systems (forward-looking design)

| # | System | File | Status | Notes |
|---|--------|------|--------|-------|
| 5 | Foodula 1 Concept | `foodula-1-concept.md` | Not Started | Full vision — 6 teams, 12 drivers, 6 tracks. Excellent document |
| 6 | Core Mechanics | `foodula-1-core-mechanics.md` | Not Started | HEAT board game adaptation — gear system, card types, turn flow |
| 7 | Teams & Cars | `foodula-1-teams-cars.md` | Not Started | 6 national food-themed teams with stats and skill trees |
| 8 | Drivers | `foodula-1-drivers.md` | Not Started | 12 drivers with passive + active skills |
| 9 | Tracks | `foodula-1-tracks.md` | Not Started | 6 themed tracks with weather and corner sequences |
| 10 | AI | `foodula-1-ai.md` | Not Started | AI behavior tree with 4 priority nodes |
| 11 | Visual Style | `foodula-1-visual-style.md` | Not Started | UI / world / VFX art direction |

## Design Dependencies

```
MVP Scope ✅ (game-concept.md)
  ├── Core HEAT Loop (gears, deck cycling, per-corner-segment judging)
  ├── One simplified track (35-45 nodes)
  ├── One AI opponent (simple behavior tree)
  └── 3-lap race with finish order

Full Vision (foodula-1-concept.md)
  ├── Core Mechanics (gears, cards, heat, slipstream, spin-out)
  ├── Teams & Cars (6 teams, differentiated stats)
  ├── Drivers (12 drivers, skills)
  ├── Tracks (6 tracks, weather)
  ├── AI (behavior tree, per-team logic)
  └── Visual Style (UI, world, VFX)
```
