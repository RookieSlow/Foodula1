# Systems Index

> Design order: Foundation → Core → Feature → Presentation → Polish
> Status: Not Started | In Progress | In Review | Approved | Needs Revision | Retired
> Snapshot: 2026-09-08

## MVP

| # | System | File | Status | Notes |
|---|---|---|---|---|
| 1 | Game Concept (MVP) | `game-concept.md` | In Review | Core runtime is implemented and editor-tested; final Demo acceptance matrix remains |

## Full Vision Systems

| # | System | File | Status | Current implementation truth |
|---|---|---|---|---|
| 5 | Foodula 1 Concept | `foodula-1-concept.md` | In Progress | 6 teams, 12 drivers and 8 selectable official tracks are represented; open decisions remain |
| 6 | Core Mechanics | `foodula-1-core-mechanics.md` | In Review | Card/heat/gear/corner/pit/weather/slipstream loop is implemented; acceptance remains |
| 7 | Teams & Cars | `foodula-1-teams-cars.md` | In Review | Six car sprites, profiles, tech hooks and formal team emblems are wired; final balance and Play Mode sign-off remain |
| 8 | Drivers | `foodula-1-drivers.md` | In Review | 12-profile catalog, XP persistence, active-skill HUD/runtime and 12 active effects are wired; five practical passives are integrated, seven remain, portraits and Play Mode sign-off remain |
| 9 | Tracks | `foodula-1-tracks.md` | In Review | 8 JSON tracks, 8 layout backgrounds, lanes, corners, pits and weather are wired; final full-race acceptance remains |
| 10 | AI | `foodula-1-ai.md` | In Progress | Deterministic heat/corner/slipstream planning is wired; configured multi-opponent tuning and difficulty remain |
| 11 | Visual Style | `foodula-1-visual-style.md` | In Progress | Race visuals plus main-menu background/Logo and six team emblems are integrated; driver portraits and tech-tree identity assets remain |
| 12 | Tech Tree | `foodula-1-tech-tree.md` | In Review | Rules, persistent profiles, menu UI and race hooks are wired; presentation and playtest sign-off remain |
| 13 | Audio Style | `foodula-1-audio-style.md` | In Review | Menu/race music, core SFX, runtime routing, crossfades, limits and settings are wired; AudioMixer, independent UI/mute, peripheral events and listening acceptance remain |
| 14 | Tutorial, Settings & Encyclopedia | `foodula-1-tutorial-settings-encyclopedia.md` | In Progress | Isolated tutorial/practice, exact deck, author-refined guidance with optional detail sections, per-mechanic spotlight, safe checkpoints, settings persistence and 17-entry data-driven encyclopedia are wired; final guided Play Mode acceptance remains |
| 15 | Career Mode | `foodula-1-career-mode.md` | In Review | The full eight-race rules, isolated persistence, menu/Race flow, summer-break editor, final champion feedback, restart confirmation and structured settlement logs are implemented; Play Mode acceptance remains |
| 16 | Free Race Custom Field | foodula-1-free-race.md | In Review | Pre-race 2–6 team/driver selection plus the 6-team/12-driver Thunderstorm field, player-first ordering, dynamic AI creation and FREE_RACE_SETUP logging are wired; Play Mode and balance acceptance remain |

## Project Planning

| File | Description |
|---|---|
| `design/planning/demo-framework.md` | Current Demo architecture and implementation boundary |
| `design/planning/asset-manifest.md` | Audited art/font/audio fact table and remaining production list |
| `design/planning/ai-art-prompts.md` | Legacy prompt library; current production scope is controlled by the asset manifest |
| `design/planning/roadmap.md` | Demo acceptance, resource/audio completion and post-Demo plan |
| `docs/reference/熱力狂飆_規則書_完整文本.md` | External HEAT rulebook reference |

## 2026-09-09 Implementation Snapshot

- Project stage is `Production`; the latest successful full EditMode run passed `663/663`
  on 2026-09-09, with 0 failures and 0 skips. Free-race roster/menu focused coverage passed `11/11`;
  active-skill coverage previously passed `28/28`,
  and the passive/related regression passed `21/21`.
- The latest manual tailwind log confirms scoped slow motion during the independent tailwind presentation/bonus movement and restoration to `time_scale=1.00` afterward.
- Current runtime scope is 8 selectable official JSON tracks plus `fallback_42`, six teams, twelve selectable drivers, configurable AI opponents, a free-race field editor for 2–6 teams/drivers, and a full 6-team/12-driver Thunderstorm field.
- Missing presentation assets are tracked in `asset-manifest.md`; formal main-menu/Logo and six-team
  brand assets are integrated, while driver portraits and tech-tree artwork remain. Runtime-generated
  effects and retired 42-node artwork are not missing-file requirements.
- `production/session-state/active.md` remains missing and has not been created automatically.
- The tutorial defines UK + Le Mans isolation, exact non-seeded card order, scripted weather/opponent
  cues, eight checkpoints and 16 guided steps. Real operations latch completion; the guide panel now
  advances explicitly with Previous/Next, and the live spotlight follows every player input gate.
  Guide completion/skip rebuilds a deterministic one-lap practice with restart, replay, exit and zero
  normal rewards. Tutorial/menu focused EditMode passed `64/64`; complete guided Play Mode acceptance remains open.
- Tutorial presentation is now authored in `Resources/Prefabs/UI/TutorialOverlay.prefab`. Its root exposes all
  sixteen step copies, while the guide panel and spotlight retain manually authored RectTransforms. Runtime reuses
  an instance under `RaceCanvas` before loading the Resources fallback, so manual scene placement does not duplicate it.
- The main menu settings overlay persists master/music/SFX volume, display mode, resolution,
  animation speed, reduced motion and the tutorial-completion preference. Display and presentation
  timings apply at runtime; `AudioService` applies the three current volume values without claiming
  an AudioMixer or independent UI group.
- The settings overlay now opens a scrollable encyclopedia backed by a versioned 17-entry JSON catalog.
  Catalog validation and runtime trace checks for 12 trick cards, 12 drivers and five weather profiles pass `6/6`;
  the resulting full EditMode suite passes `499/499`.
- Eight guided player-state checkpoints now rebuild exact card/heat zones at safe turn boundaries. Each of the
  16 lessons now exposes goal/current state/action/success/recovery text plus previous-step success feedback.
  A tutorial-only 132/4 virtual pit view reuses normal pit rules because the official Le Mans JSON has no pit;
  official nodes remain unchanged. The author-refined copy and optional-section rendering historically brought that slice's full EditMode run to `516/516`.
- `design/registry/entities.yaml` exists but is empty, so entity-level automated consistency checking cannot yet be treated as evidence.
- Career mode uses the eight-track catalog, locks the selected team, scores four cars at 10/6/4/2,
  opens one technology adjustment after race four and completes after race eight. `Foodula1.Career.V1`
  validates and rebuilds the isolated season state; the menu, Race launch/result settlement, summer-break
  technology draft and completed-season restart flow are wired. Focused career coverage passed `46/46`;
  full end-to-end Play Mode acceptance remains open.
- A persistent `AudioService` loads menu/race music and 24 core SFX from `Resources/Audio`, crossfades
  scene music, separates Music/SFX/UI AudioSources and throttles repeated events. AudioMixer routing,
  independent UI volume/mute, peripheral events and a full-match listening pass remain open.

## Design Dependencies

- Free Race Custom Field is now a first-class demo flow: the roster editor validates 2–6
  unique teams and drivers before the existing track-selection entry, and exposes a
  six-team/twelve-driver Thunderstorm experiment for full-field strength checks.

```
Demo Candidate
  ├── Core HEAT loop and card/heat ownership
  ├── Six team vehicles + Go/Recover + tech-tree hooks
  ├── Twelve selectable drivers + active effects + five integrated passives
  ├── Eight official JSON tracks + one fallback
  ├── Configurable AI opponents + deterministic planner
  ├── Weather, pit lane, slipstream and complete race result flow
  ├── Free Race 2–6 team/driver custom field + Thunderstorm 6-team/12-driver field
  ├── Race HUD, card/pile/movement/event presentation
  └── Final acceptance + remaining portraits/tech-tree art + AudioMixer/peripheral audio
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
