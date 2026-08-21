# Project Overview

> Updated: 2026-08-21. This file describes the current Unity project, not the
> archived prototype snapshots.

Foodular1 is a Unity 2022.3.62f2 2D card-driven food truck racing game using
the CCGS project framework.

## Technology

- **Engine**: Unity 2022.3.62f2
- **Language**: C# 9.0
- **Rendering**: Built-in Render Pipeline, 2D
- **UI**: uGUI and TextMesh Pro
- **Input**: Legacy Input Manager, mouse interaction
- **Code**: `Assets/Scripts/`

## Current Runtime Structure

- `Core/MVPGameManager.cs` remains the Unity-side race coordinator. Pure rules
  and presentation adapters are extracted around it; the coordinator is still
  intentionally large and is not yet a fully split `RaceManager`.
- `Core/CardDeck.cs`, `CardData.cs`, and `PlayerState.cs` implement race state
  and card lifecycle.
- `Gameplay/TrackManager.cs` loads data-driven JSON tracks and exposes the
  loaded `TrackRuntimeContext` snapshot. `fallback_42.json` remains a hidden
  42-cell compatibility resource; the normal menu uses the eight selectable
  JSON tracks, while the code-level fallback is the final safety path.
- `Gameplay/TrackDataLoader.cs` converts track JSON into runtime nodes and
  world positions.
- `AI/AIController.cs` controls the current opponent.
- `AI/AIController.cs` delegates team-specific China gear decisions to the
  pure `ChinaGearShiftRules` module.
- `TechTree/` contains the pure tech database/rules plus the
  `TechTreeProfileStore` persistence adapter.
- `UI/` contains the card hand, HUD, card, main-menu, driver-selection, and
  runtime tech-tree views.
- `Core/TeamGearRules.cs` is the team-aware gear facade; `Core/TeamVehicleRules.cs`
  owns tunable team vehicle profiles without leaking them into UI code.
- `Drivers/` contains the immutable driver catalog and progression rules;
  `Core/DriverSelectionState.cs` stores the current menu choice.
- `Config/GameConfigSO.cs` contains tunable race, deck, gear, animation, and
  AI parameters.

## Scenes

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/Race.unity`

The verified scene flow is:

`MainMenu -> Race -> race result -> MainMenu`

## Current Development State

- Demo framework Phase 1 and the core runtime integration are complete.
- The project is in Production stage. The remaining presentation work is
  selective UI polish, audio, and effects; card, vehicle, track-background,
  font, and Chinese UI assets are already integrated.
- The maintained active-session record is the current task source. The latest
  recorded Unity EditMode run is 399/399 passed; this documentation sync does
  not rerun the Test Runner.
- The main-menu tech-tree entry now persists per-team RP, unlocks, and active
  nodes; `RaceSession` centralizes numeric tech modifiers while the manager
  invokes explicit `TechTreeRules` event hooks at documented race phases.
- China uses an independent Go/Recover drivetrain (3-card Go, 1-card Recover,
  consecutive overclock heat and built-in Recover cooling) shared by player UI
  and AI through the same pure rules module.
- The driver-selection vertical slice is now implemented: 12 catalog entries,
  XP/tier rules, main-menu selection UI, and race initialization integration.
- Card number and heat icons, Chinese UI, Chinese font support, team car
  sprites, and the main-menu flow have been implemented.
- The custom Track Node Editor experiment was reverted after Scene View
  interaction problems.
- The menu exposes eight selectable JSON tracks. The project currently stores
  nine track JSON files: eight selectable layouts plus `fallback_42`.
  Silverstone remains the default configured track. Runtime apex traversal,
  start/finish lookup, lane presentation, weather, pit entry/exit, and loaded
  LineRenderer coordinates are integrated through `TrackRuntimeContext`.
- Track JSON authoring and schema validation are implemented. New-track
  authoring and the full multi-weather/multi-lap/pit Play Mode matrix still
  need broader validation.
- `RaceRules`, `TrackRules`, `WeatherRules`, `PitLaneRules`, `RaceSession`,
  `RaceLapWeatherRules`, `TrickCardRules`, and `TechTreeRules` form the tested
  pure-rule boundary. Current runtime movement and pure simulation share the
  same traversal-event snapshot.
- The default demo runs one human and one AI opponent. `aiOpponentCount` can
  configure up to three AI participants, while personality tuning and driver
  signature effects remain follow-up work.

## Memory Provenance

The original Claude Code memory under `.claude/agent-memory/` described an
earlier prototype with an 85-node track and three scripts. Those values are
historical and must not override the current implementation. Older entries in
`docs/memory/current-task-list.md` and `production/session-state/active.md`
also preserve historical test counts and machine state; the newest entry in
each file overrides them.

See [game-mechanics.md](game-mechanics.md) for current gameplay rules and
[current-task-list.md](current-task-list.md) for active work.
