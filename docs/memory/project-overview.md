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
- Card number and heat icons, Chinese UI, Chinese font support, team car
  sprites, and the main-menu flow have been implemented.
- The custom Track Node Editor experiment was reverted after Scene View
  interaction problems.
- The track system still needs a reliable authoring workflow and full
  data-driven integration.
- Four Scheme A refactor files currently exist as untracked work and are not
  integrated into the runtime: `AIPlanner.cs`, `IRandomSource.cs`,
  `RaceRules.cs`, and `RandomSources.cs`.

## Memory Provenance

The original Claude Code memory under `.claude/agent-memory/` described an
earlier prototype with an 85-node track and three scripts. Those values are
historical and must not override the current implementation. When facts
conflict, verify the current code, Unity project state, and
`docs/memory/current-task-list.md`.

See [game-mechanics.md](game-mechanics.md) for current gameplay rules and
[current-task-list.md](current-task-list.md) for active work.
