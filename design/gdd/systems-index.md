# Systems Index

> Design order: Foundation → Core → Feature → Presentation → Polish
> Status: Not Started | In Progress | In Review | Approved | Needs Revision | Retired

## MVP (buildable first milestone)

| # | System | File | Status | Notes |
|---|--------|------|--------|-------|
| 1 | Game Concept (MVP) | `game-concept.md` | In Review | Updated 2026-07-22 — aligned heat lifecycle with HEAT rules. 12 ACs all met in code |

## Archived Systems

| # | System | Archived To | Reason |
|---|--------|-------------|--------|
| 2 | Card System | `design/archive/2026-07-21/` | Replaced by foodula-1-core-mechanics.md |
| 3 | Heat System | `design/archive/2026-07-21/` | Replaced by foodula-1-core-mechanics.md |
| 4 | Track System | `design/archive/2026-07-21/` | Replaced by foodula-1-core-mechanics.md |

## Project Planning

| File | Description |
|------|-------------|
| `design/planning/demo-framework.md` | Demo project framework — architecture, scripts, UI, assets. Phase 1 ✅ |
| `design/planning/asset-manifest.md` | Master asset checklist — 58 items with status, priority, readiness |
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
| 5 | Foodula 1 Concept | `foodula-1-concept.md` | Draft | Full vision — 6 teams, 12 drivers, 6 tracks. Pending: open items in §10 |
| 6 | Core Mechanics | `foodula-1-core-mechanics.md` | Draft | HEAT board game adaptation — **§1.6 heat lifecycle rewritten 2026-07-22** to match HEAT rules |
| 7 | Teams & Cars | `foodula-1-teams-cars.md` | Draft | 6 national food-themed teams with stats and skill trees |
| 8 | Drivers | `foodula-1-drivers.md` | Draft | 12 drivers with passive + active skills |
| 9 | Tracks | `foodula-1-tracks.md` | Draft | 6 themed tracks with weather and corner sequences |
| 10 | AI | `foodula-1-ai.md` | Draft | AI behavior tree with 4 priority nodes |
| 11 | Visual Style | `foodula-1-visual-style.md` | Draft | UI / world / VFX art direction |
| 12 | Tech Tree | `foodula-1-tech-tree.md` | Draft | UK/DE/IT done, US/CN/JP pending |

## Design Dependencies

```
MVP Scope ✅ (game-concept.md)
  ├── Core HEAT Loop (gears, deck cycling, per-corner-segment judging)
  ├── Heat cards CANNOT be played — clog hand, only removed by cooldown (HEAT-correct)
  ├── One simplified track (42 nodes, 5 corners)
  ├── One AI opponent (simple behavior tree)
  └── 3-lap race with finish order

Full Vision (foodula-1-concept.md)
  ├── Core Mechanics (gears, cards, heat, slipstream, spin-out) — §1.6 rewritten 2026-07-22
  ├── Teams & Cars (6 teams, differentiated stats)
  ├── Drivers (12 drivers, skills)
  ├── Tracks (6 tracks, weather)
  ├── AI (behavior tree, per-team logic)
  └── Visual Style (UI, world, VFX)
```
