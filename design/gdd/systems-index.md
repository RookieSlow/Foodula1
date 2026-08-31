# Systems Index

> Design order: Foundation → Core → Feature → Presentation → Polish
> Status: Not Started | In Progress | In Review | Approved | Needs Revision | Retired
> Snapshot: 2026-08-31

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
| 14 | Tutorial, Settings & Encyclopedia | `foodula-1-tutorial-settings-encyclopedia.md` | In Progress | Isolated tutorial/practice, exact deck, author-refined guidance with optional detail sections, per-mechanic spotlight, safe checkpoints, settings persistence and 17-entry data-driven encyclopedia are wired; final guided Play Mode acceptance remains |
| 15 | Career Mode | `foodula-1-career-mode.md` | In Review | The full eight-race rules, isolated persistence, menu/Race flow, summer-break editor, final champion feedback, restart confirmation and structured settlement logs are implemented; Play Mode acceptance remains |

## Project Planning

| File | Description |
|---|---|
| `design/planning/demo-framework.md` | Current Demo architecture and implementation boundary |
| `design/planning/asset-manifest.md` | Audited art/font/audio fact table and remaining production list |
| `design/planning/ai-art-prompts.md` | Legacy prompt library; current production scope is controlled by the asset manifest |
| `design/planning/roadmap.md` | Demo acceptance, resource/audio completion and post-Demo plan |
| `docs/reference/熱力狂飆_規則書_完整文本.md` | External HEAT rulebook reference |

## 2026-08-28 Implementation Snapshot

- Project stage is `Production`; the latest successful full EditMode run passed `514/514`, with 0 failures and 0 skips.
- The latest manual tailwind log confirms scoped slow motion during the independent tailwind presentation/bonus movement and restoration to `time_scale=1.00` afterward.
- Current runtime scope is 8 selectable official JSON tracks plus `fallback_42`, six teams, twelve selectable drivers and configurable AI opponents.
- Missing presentation assets are now explicitly tracked in `asset-manifest.md`; runtime-generated effects and retired 42-node artwork are no longer reported as missing files.
- `production/session-state/active.md` remains missing and has not been created automatically.
- The tutorial foundation now defines UK + Le Mans isolation, exact non-seeded card order,
  scripted weather/opponent cues and the complete guided-step order with EditMode coverage.
- The menu-to-Race tutorial launch, runtime Director, guide panel and mechanic event gates are wired.
  Guide completion/skip now rebuilds a deterministic one-lap practice session with restart, replay,
  exit and zero-reward completion feedback. Focused tutorial EditMode is `16/16`; full EditMode is
  checkpoints, progressive copy, the dedicated Prefab authoring inspector and 13 semantic spotlight targets now bring focused tutorial coverage to `31/31` and full EditMode to `514/514`.
  The earlier exact-opening/zero-benefit and separate Quick Race smokes remain valid.
- Tutorial presentation is now authored in `Resources/Prefabs/UI/TutorialOverlay.prefab`. Its root exposes all
  sixteen step copies, while the guide panel and spotlight retain manually authored RectTransforms. Runtime reuses
  an instance under `RaceCanvas` before loading the Resources fallback, so manual scene placement does not duplicate it.
- The main menu settings overlay now persists versioned audio placeholders, display mode, resolution,
  animation speed, reduced motion and the separate tutorial-completion preference. Display and supported
  presentation timings apply at runtime; audio values are explicitly data-only until an AudioMixer exists.
  Settings plus tutorial focused EditMode is `22/22`; full EditMode is `493/493`.
- The settings overlay now opens a scrollable encyclopedia backed by a versioned 17-entry JSON catalog.
  Catalog validation and runtime trace checks for 12 trick cards, 12 drivers and five weather profiles pass `6/6`;
  the resulting full EditMode suite passes `499/499`.
- Eight guided player-state checkpoints now rebuild exact card/heat zones at safe turn boundaries. Each of the
  16 lessons now exposes goal/current state/action/success/recovery text plus previous-step success feedback.
  A tutorial-only 132/4 virtual pit view reuses normal pit rules because the official Le Mans JSON has no pit;
  official nodes remain unchanged. The author-refined copy and optional-section rendering bring the latest full EditMode run to `516/516`.
- `design/registry/entities.yaml` exists but is empty, so entity-level automated consistency checking cannot yet be treated as evidence.
- Career mode now has a pure-rule foundation tied to the eight-track catalog: six teams can be selected,
  the selected team is locked for the season, four-car results use a centralized 10/6/4/2 table,
  race four opens the sole summer-break tech window, and race eight completes the season. Focused
  source-level NUnit execution passed `16/16` for that initial pure-rule slice.
- Career persistence now uses `Foodula1.Career.V1`, validates schema and the exact track calendar,
  rebuilds standings from saved race results, preserves initial/summer-break technology snapshots,
  returns a safe empty state for malformed data, and only swaps runtime progress after storage succeeds.
  The runtime menu now exposes Free Race and Career separately, supports six-team creation plus confirmed
  replacement/abandonment, and renders the eight-race calendar and computed standings. The combined focused
  rules/persistence/presentation run passes `31/31`; Race and summer-break technology wiring remain open.

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
