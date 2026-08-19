# Project Overview

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

- `Core/MVPGameManager.cs` coordinates the current race loop.
- `Core/CardDeck.cs`, `CardData.cs`, and `PlayerState.cs` implement race state
  and card lifecycle.
- `Gameplay/TrackManager.cs` supports a data-driven JSON track and a
  hard-coded 42-node fallback track.
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

- Demo framework Phase 1 is complete.
- Phase 2 asset replacement is in progress.
- The project is in Production stage. A full audit on 2026-08-15 verified the
  MainMenu-to-Race flow, corrected race card/icon references, and cleared
  unsupported-glyph runtime warnings.
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
- The Race scene is configured for the 60-node Silverstone JSON track. Runtime
  apex traversal, start/finish lookup, arbitrary-node HUD display, and loaded
  LineRenderer coordinates are integrated and verified; authoring workflow,
  pit behavior, and full-lap playtesting remain. Track weather now preserves
  sunny/cloudy/light-rain/heavy-rain/hot profiles through one pure rules boundary.
- The four Scheme A refactor sources were committed in `58d1bba`.
  `RaceRules.cs` and `AIPlanner.cs` are integrated into the runtime.
  `AIController` and `CardDeck` accept injectable `IRandomSource`
  implementations for deterministic tests. The reviewed refactor currently
  passes its original 16 EditMode tests with no Unity warnings or errors.
- `Gameplay/TrackRules.cs` now provides pure, tested track traversal rules.
  Together with the current feature tests, the project passes 307 EditMode
  tests. A Play Mode smoke test loaded the menu, tech-tree panel and a China
  race (Shanghai) with no project warnings or errors.

## Memory Provenance

The original Claude Code memory under `.claude/agent-memory/` described an
earlier prototype with an 85-node track and three scripts. Those values are
historical and must not override the current implementation. When facts
conflict, verify the current code, Unity project state, and
`docs/memory/current-task-list.md`.

See [game-mechanics.md](game-mechanics.md) for current gameplay rules and
[current-task-list.md](current-task-list.md) for active work.
