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
- `UI/` contains the card hand, HUD, card, and main-menu views.
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
- The project is in Production stage. A full audit on 2026-08-05 verified the
  MainMenu-to-Race flow, corrected race card/icon references, and cleared
  unsupported-glyph runtime warnings.
- The driver-selection vertical slice is now implemented: 12 catalog entries,
  XP/tier rules, main-menu selection UI, and race initialization integration.
- Card number and heat icons, Chinese UI, Chinese font support, team car
  sprites, and the main-menu flow have been implemented.
- The custom Track Node Editor experiment was reverted after Scene View
  interaction problems.
- The Race scene is configured for the 60-node Silverstone JSON track. Runtime
  apex traversal, start/finish lookup, arbitrary-node HUD display, and loaded
  LineRenderer coordinates are integrated and verified; authoring workflow,
  vehicle orientation, pit behavior, weather, and full-lap playtesting remain.
- The four Scheme A refactor sources were committed in `58d1bba`.
  `RaceRules.cs` and `AIPlanner.cs` are integrated into the runtime.
  `AIController` and `CardDeck` accept injectable `IRandomSource`
  implementations for deterministic tests. The reviewed refactor currently
  passes its original 16 EditMode tests with no Unity warnings or errors.
- `Gameplay/TrackRules.cs` now provides pure, tested track traversal rules.
  Together with four new track tests, the project currently passes 20 EditMode
  tests. A Play Mode smoke test loaded Silverstone (60 nodes, 3 laps) with no
  warnings or errors.

## Memory Provenance

The original Claude Code memory under `.claude/agent-memory/` described an
earlier prototype with an 85-node track and three scripts. Those values are
historical and must not override the current implementation. When facts
conflict, verify the current code, Unity project state, and
`docs/memory/current-task-list.md`.

See [game-mechanics.md](game-mechanics.md) for current gameplay rules and
[current-task-list.md](current-task-list.md) for active work.
