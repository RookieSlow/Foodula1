# Systems Index

> Design order: Foundation → Core → Feature → Presentation → Polish
> Status: Not Started | In Progress | In Review | Approved | Needs Revision | Retired

## MVP (buildable first milestone)

| # | System | File | Status | Notes |
|---|--------|------|--------|-------|
| 1 | Game Concept (MVP historical baseline) | `game-concept.md` | Retired | 2026-08-21 标记为早期 42 格原型快照；当前范围以 `foodula-1-concept.md` 与系统 GDD 为准 |

## Archived Systems

| # | System | Archived To | Reason |
|---|--------|-------------|--------|
| 2 | Card System | `design/archive/2026-07-21/` | Replaced by foodula-1-core-mechanics.md |
| 3 | Heat System | `design/archive/2026-07-21/` | Replaced by foodula-1-core-mechanics.md |
| 4 | Track System | `design/archive/2026-07-21/` | Replaced by foodula-1-core-mechanics.md |

## Project Planning

| File | Description |
|------|-------------|
| `design/planning/demo-framework.md` | Demo project framework — current runtime boundaries, scripts, UI, assets |
| `design/planning/asset-manifest.md` | Current asset inventory — integrated assets, runtime fallbacks, and deferred presentation work |
| `design/planning/ai-art-prompts.md` | AI image generation prompts for all art assets |
| `docs/reference/熱力狂飆_規則書_完整文本.md` | HEAT rulebook — Chinese translation (external reference) |

## Document Archive

| Path | Description |
|------|-------------|
| `design/archive/2026-07-19/` | Old prototype behavior audit |
| `design/archive/2026-07-21/` | Retired GDDs (card/heat/track systems) |
| `design/archive/2026-07-24/` | Snapshot backup of all GDDs before July 24 updates |

## Full Vision Systems (forward-looking design)

| # | System | File | Status | Notes |
|---|--------|------|--------|-------|
| 5 | Foodula 1 Concept | `foodula-1-concept.md` | In Progress | Full vision — 6 teams, 12 drivers, 8 selectable tracks; current Demo is one human plus configurable AI; open items in §10 remain |
| 6 | Core Mechanics | `foodula-1-core-mechanics.md` | In Progress | HEAT board game adaptation; runtime card/heat/gear loop is implemented and tested |
| 7 | Teams & Cars | `foodula-1-teams-cars.md` | In Progress | 6 national food-themed teams; car sprites, team vehicle profiles and tech modifiers are wired; driver selection indirectly selects a team, but no standalone team-selection screen exists |
| 8 | Drivers | `foodula-1-drivers.md` | In Progress | 12 driver catalog entries, XP/tier rules and menu selection are implemented; signature effects remain data-only |
| 9 | Tracks | `foodula-1-tracks.md` | In Progress | 9 JSON definitions, 8 selectable tracks (6 national + 2 supplementary), real-layout presentation, lanes, apex masks and limits; full pit/weather Play Mode matrix remains |
| 10 | AI | `foodula-1-ai.md` | In Progress | Deterministic planner with configurable 0–3 AI participants is implemented; personality, difficulty and driver-skill behavior remain |
| 11 | Visual Style | `foodula-1-visual-style.md` | In Progress | Main menu, race backgrounds, masks, minimap and card visuals are integrated; polish remains |
| 12 | Tech Tree | `foodula-1-tech-tree.md` | In Progress | Rules/database, persistent profiles, main-menu configuration UI and race-loop hooks are integrated; balance/playtest remains |

## 2026-08-21 Implementation Snapshot

The index is aligned with the current implementation stage. `production/stage.txt`
is `Production`. The latest maintained Unity validation record is 399/399
EditMode tests passed; this documentation sync does not rerun tests. The
remaining `In Progress` items are deliberate feature gaps, not missing source
files accidentally hidden by this index.

## Design Dependencies

```
MVP Scope ✅ (game-concept.md；历史基线)
  ├── Core HEAT Loop (gears, deck cycling, per-corner-segment judging)
  ├── Heat cards CANNOT be played — clog hand, only removed by cooldown (HEAT-correct)
  ├── One simplified fallback track (42 nodes, 5 corners)
  ├── One default AI opponent (runtime supports 0–3 configured AI)
  └── 3-lap race with finish order

当前 Demo 不再受上述单赛道/单 AI 范围限制；当前实现摘要见本文件的 Full Vision Systems 与 `MVP_README.md`。

Full Vision (foodula-1-concept.md)
  ├── Core Mechanics (gears, cards, heat, slipstream, spin-out) — §1.6 rewritten 2026-07-22
  ├── Teams & Cars (6 teams, differentiated stats)
  ├── Drivers (12 drivers, skills)
  ├── Tracks (6 national tracks + 2 supplementary JSON tracks, weather)
  ├── AI (behavior tree, per-team logic)
  └── Visual Style (UI, world, VFX)
```
