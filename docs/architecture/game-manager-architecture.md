---
status: reverse-documented
source: Assets/Scripts/
date: 2026-07-19
verified-by: User
---

# ADR-001: Monolithic GameManager Architecture for Prototype Phase

## Status

Superseded by [ADR-002](adr-002-layered-pure-function-architecture.md) (2026-07-30)

## Context

Foodular1 was built as a rapid prototype. The entire game logic — track
generation, card dealing, movement, heat penalty, UI updates, and game state
— lives in a single `GameManager` MonoBehaviour. This architecture was chosen
for speed of iteration during the concept exploration phase.

## Decision

Use a monolithic `GameManager` pattern for the prototype, with:
- **GameManager**: Central controller owning all game state and logic
- **CardUI**: Thin MonoBehaviour handling card selection display and click events
- **TrackNode**: Pure data class (System.Serializable) for track node configuration

Communication pattern:
- **Child → Parent**: CardUI calls `gameManager.CalculateSelectedSteps()` directly
  (serialized field reference passed via `SetupCard()`)
- **UI → Logic**: Unity Button onClick events call `GameManager.PlayTurn()` and `GameManager.ResetGame()`
- **Logic → UI**: GameManager directly writes to TMP_Text components (Inspector references)

## Consequences

### Positive
- **Fast iteration**: All logic in one place — easy to understand and modify
- **No indirection**: Direct references, no event system or dependency injection overhead
- **Unity-friendly**: Leverages Inspector references and serialized fields for configuration
- **Appropriate for scope**: 3 scripts, ~300 lines of code — architecture matches complexity

### Negative
- **Not scalable**: Adding more systems (card draw, AI opponents, multiplayer) would
  make GameManager unwieldy (currently ~295 lines)
- **Hard to test**: GameManager's methods are tightly coupled to Unity's
  Instantiate, Destroy, and coroutine system — difficult to unit test
- **Data is hardcoded**: Track coordinates (85 Vector2), speed limits, starting
  hand values are all in code rather than data assets
- **No separation of concerns**: GameManager handles track, UI, cards, heat,
  and animation in one class

## ADR Dependencies

None — this is the foundational architecture decision.

## Engine Compatibility

- **Unity 2022.3.62f2**: Confirmed working
- **Built-in Render Pipeline**: Confirmed (2D project, Sprites/Default shader)
- **Mono scripting backend**: Confirmed

## GDD Requirements Addressed

| TR-ID | Requirement | Status |
|-------|-------------|--------|
| TR-CARD-001 | Card selection and play | Implemented |
| TR-HEAT-001 | Heat penalty calculation | Implemented |
| TR-TRACK-001 | Track generation and visualization | Implemented |
