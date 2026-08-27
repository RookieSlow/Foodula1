# Systems Index

> Design order: Foundation → Core → Feature → Presentation → Polish
> Status: Not Started | In Progress | In Review | Approved | Needs Revision | Retired
> Snapshot: 2026-08-27

## MVP

| # | System | File | Status | Notes |
|---|---|---|---|---|
| 1 | Game Concept (MVP) | `game-concept.md` | In Review | Core runtime is implemented and editor-tested; final Demo acceptance matrix remains |

## Full Vision Systems

| # | System | File | Status | Current implementation truth |
|---|---|---|---|---|
| 5 | Foodula 1 Concept | `foodula-1-concept.md` | In Progress | 6 teams, 12 drivers and 8 selectable official tracks are represented; open decisions remain |
| 6 | Core Mechanics | `foodula-1-core-mechanics.md` | In Review | Card/heat/gear/corner/pit/weather/slipstream loop is implemented; acceptance remains |
| 7 | Teams & Cars | `foodula-1-teams-cars.md` | In Review | Six car sprites, profiles and tech hooks are wired; formal team emblems and final balance sign-off remain |
| 8 | Drivers | `foodula-1-drivers.md` | In Progress | 12-profile catalog, XP/tier rules and selection are wired; portraits and signature race effects remain |
| 9 | Tracks | `foodula-1-tracks.md` | In Review | 8 JSON tracks, 8 layout backgrounds, lanes, corners, pits and weather are wired; final full-race acceptance remains |
| 10 | AI | `foodula-1-ai.md` | In Progress | Deterministic heat/corner/slipstream planning is wired; configured multi-opponent tuning and difficulty remain |
| 11 | Visual Style | `foodula-1-visual-style.md` | In Progress | Race visuals are Demo-grade; menu/driver/team/tech-tree identity assets remain |
| 12 | Tech Tree | `foodula-1-tech-tree.md` | In Review | Rules, persistent profiles, menu UI and race hooks are wired; presentation and playtest sign-off remain |
| 13 | Audio Style | `foodula-1-audio-style.md` | Not Started | Design/event map exists; no project audio files, AudioMixer or runtime audio service yet |

## Project Planning

| File | Description |
|---|---|
| `design/planning/demo-framework.md` | Current Demo architecture and implementation boundary |
| `design/planning/asset-manifest.md` | Audited art/font/audio fact table and remaining production list |
| `design/planning/ai-art-prompts.md` | Legacy prompt library; current production scope is controlled by the asset manifest |
| `design/planning/roadmap.md` | Demo acceptance, resource/audio completion and post-Demo plan |
| `docs/reference/熱力狂飆_規則書_完整文本.md` | External HEAT rulebook reference |

## 2026-08-27 Implementation Snapshot

- Project stage is `Production`; the latest successful full EditMode run passed `471/471`, with 0 failures and 0 skips.
- The latest manual tailwind log confirms scoped slow motion during the independent tailwind presentation/bonus movement and restoration to `time_scale=1.00` afterward.
- Current runtime scope is 8 selectable official JSON tracks plus `fallback_42`, six teams, twelve selectable drivers and configurable AI opponents.
- Missing presentation assets are now explicitly tracked in `asset-manifest.md`; runtime-generated effects and retired 42-node artwork are no longer reported as missing files.
- `production/session-state/active.md` remains missing and has not been created automatically.
- `design/registry/entities.yaml` exists but is empty, so entity-level automated consistency checking cannot yet be treated as evidence.

## Design Dependencies

```
Demo Candidate
  ├── Core HEAT loop and card/heat ownership
  ├── Six team vehicles + Go/Recover + tech-tree hooks
  ├── Twelve selectable drivers (signature race effects are post-Demo)
  ├── Eight official JSON tracks + one fallback
  ├── Configurable AI opponents + deterministic planner
  ├── Weather, pit lane, slipstream and complete race result flow
  ├── Race HUD, card/pile/movement/event presentation
  └── Final acceptance + brand/portrait/tech-tree art + audio
```

## Archived Systems

| # | System | Archived To | Reason |
|---|---|---|---|
| 2 | Card System | `design/archive/2026-07-21/` | Replaced by `foodula-1-core-mechanics.md` |
| 3 | Heat System | `design/archive/2026-07-21/` | Replaced by `foodula-1-core-mechanics.md` |
| 4 | Track System | `design/archive/2026-07-21/` | Replaced by `foodula-1-core-mechanics.md` and current track GDD |

## Document Archive

| Path | Description |
|---|---|
| `design/archive/2026-07-19/` | Old prototype behavior audit |
| `design/archive/2026-07-21/` | Retired card/heat/track GDDs |
| `design/archive/2026-07-24/` | GDD snapshot before July 24 updates |
