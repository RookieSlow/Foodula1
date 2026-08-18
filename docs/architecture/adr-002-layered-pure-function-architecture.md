# ADR-002: Layered Pure-Function Architecture for Demo Phase

## Status

Accepted

## Date

2026-08-05

## Last Verified

2026-07-30

## Decision Makers

User (RookieSlow) + Claude Code (unity-specialist + gameplay-programmer)

## Summary

The prototype monolith (ADR-001) has been refactored into a layered architecture:
**pure-function rules engines** (`RaceRules`, `TrackRules`, `AIPlanner`) decoupled
from Unity, a **data-driven track pipeline** (JSON → `TrackConfig` → `TrackNode`),
and **dependency injection** for deterministic randomness (`IRandomSource`).
`MVPGameManager` remains the coordinator but delegates all algorithmic decisions
to testable pure functions. This preserves fast iteration while enabling unit
testing and data-driven content authoring.

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 2022.3.62f2 |
| **Domain** | Core — Game Architecture |
| **Knowledge Risk** | LOW — in training data |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | None — standard C# 9.0 patterns, no engine-version-specific APIs |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-001 (superseded — this is the evolution) |
| **Enables** | All future gameplay ADRs (AI, track generation, multiplayer) |
| **Blocks** | None |
| **Ordering Note** | None |

## Context

### Problem Statement

ADR-001 described a ~295-line monolithic `GameManager` where all logic (track
generation, card dealing, movement, heat penalty, UI updates) lived in a single
class. As the prototype evolved, this became unwieldy:

- Adding data-driven tracks required JSON parsing that didn't belong in GameManager
- AI decision-making needed to be testable without spinning up a full game
- Hardcoded values (gear shift costs, cooldown amounts, corner limits) made
  balance iteration slow
- Card shuffling was non-deterministic, making AI behavior impossible to reproduce
- No automated tests existed for any gameplay rules

### Current State

The codebase has organically evolved into a layered structure through iterative
refactoring. This ADR formalizes that structure.

### Constraints

- Must remain compatible with Unity 2022 LTS (C# 9.0, Mono)
- Must NOT require Unity for core rules testing (EditMode tests only)
- Must preserve the existing Unity scene setup (no breaking prefab/scene changes)
- Must support rapid iteration — config changes should not require recompilation

### Requirements

- All gameplay formulas must be unit-testable without Unity dependencies
- Card shuffling and AI variation must be deterministic given a seed
- Track data must be authorable as JSON (designer-friendly)
- Balance values must be tunable from the Unity Inspector

## Decision

### Architecture

```
┌─────────────────────────────────────────────────────────┐
│                   Unity Scene Layer                      │
│  MVPGameManager (coordinator, coroutines, UI binding)    │
│  TrackManager (runtime track state, rendering)           │
│  AIController (MonoBehaviour, wiring)                    │
│  CardHandUI / HUDUI (display, user input)                │
└──────────────────────┬──────────────────────────────────┘
                       │ calls / injects
┌──────────────────────▼──────────────────────────────────┐
│              Pure Function Layer (testable)              │
│  RaceRules    — gear shift, cooldown, card summing      │
│  TrackRules   — apex corner detection, start/finish     │
│  AIPlanner    — card selection strategy                 │
│  TrackDataLoader — JSON→TrackConfig→TrackNode pipeline  │
│  DriverProgression — XP, level and skill-tier rules     │
└──────────────────────┬──────────────────────────────────┘
                       │ reads / produces
┌──────────────────────▼──────────────────────────────────┐
│                   Data Layer                             │
│  GameConfigSO  — ScriptableObject (balance knobs)       │
│  TrackConfig   — JSON-deserialized track model          │
│  TrackNode     — runtime track node (cornerId, isApex)  │
│  CardData      — card value + type                      │
│  CardDeck / PlayerState — runtime mutable state         │
│  DriverProfile / DriverCatalog — driver selection data  │
└─────────────────────────────────────────────────────────┘

Data Flow (per turn):
  1. MVPGameManager reads PlayerState + GameConfigSO
  2. Calls RaceRules.ResolveGearShift() → gear + heat cost
  3. Calls AIPlanner.ChooseSpeedCards() (AI only)
  4. Calls RaceRules.SumCardValues() → total movement
  5. Moves cars via coroutine, calls TrackManager.GetUniqueCornersCrossed()
     → delegates to TrackRules.GetUniqueApexCornersCrossed()
  6. Calls RaceRules.GetCooldown() → heat recovery
  7. Calls RaceRules.GetMissingSpeedCardCount() → engine failure check
```

### Key Interfaces

```csharp
// Pure rules — no Unity dependencies, fully testable
public static class RaceRules {
    static GearShiftResult ResolveGearShift(current, requested, min, max, heatCost);
    static int GetCooldown(gear, gearOneCooldown, gearTwoCooldown);
    static int SumCardValues(IReadOnlyList<CardData> cards);
    static int GetMissingSpeedCardCount(gear, selectedCount);
}

public static class TrackRules {
    static HashSet<int> GetUniqueApexCornersCrossed(nodes, fromPos, toPos);
    static bool CrossesStartFinish(nodes, fromPos, toPos, out int index);
    static int FindStartFinishNodeIndex(nodes);
}

public static class AIPlanner {
    static List<CardData> ChooseSpeedCards(deck, gear, heatRatio,
        hasCornerRisk, thresholds, variationChance, IRandomSource);
}

// Dependency injection for determinism
public interface IRandomSource {
    int NextInt(int minInclusive, int maxExclusive);  // [min, max)
    double NextDouble();                               // [0, 1)
}

// Data pipeline
public static class TrackDataLoader {
    static TrackConfig LoadConfig(string trackId);          // Resources.Load
    static List<TrackNode> ConfigToNodes(TrackConfig cfg);  // JSON→runtime
    static Vector2[] ConfigToWorldPositions(TrackConfig, width, height);
}
```

### Implementation Guidelines

1. **New gameplay logic** → pure static function in a `*Rules` or `*Planner` class
2. **New configurable values** → serialized field in `GameConfigSO`
3. **New track data** → JSON file, deserialized via `TrackConfig`
4. **Randomness** → accept `IRandomSource` parameter, never use `UnityEngine.Random`
5. **UI wiring** → `MVPGameManager` owns all UI binding (keep it thin)
6. **Tests** → write EditMode tests for every pure function before or alongside
   implementation

## Alternatives Considered

### Alternative 1: Keep ADR-001 Monolith

- **Description**: Continue evolving the single `GameManager` class, adding more
  methods inline
- **Pros**: No refactoring cost, simple mental model
- **Cons**: Untestable, hard to extend, violates every SOLID principle beyond ~500
  lines, AI behavior non-deterministic, tracks hardcoded
- **Estimated Effort**: Lowest (but cumulative maintenance cost grows)
- **Rejection Reason**: Already proven insufficient — the codebase organically
  outgrew it

### Alternative 2: Full ECS / DOTS Architecture

- **Description**: Rewrite as Unity DOTS with Entities, Jobs, and Burst compilation
- **Pros**: Maximum performance, ideal for thousands of entities
- **Cons**: Massive rewrite, steep learning curve, overkill for 2-player card game,
  C# Jobs have restrictions that complicate coroutine-based animation
- **Estimated Effort**: 10-20× the current approach
- **Rejection Reason**: Premature optimization — 2 cars on a 60-node track don't
  need ECS

### Alternative 3: Full MVC with Events

- **Description**: Introduce a proper Model-View-Controller with C# events for
  all state changes, separate UI controllers
- **Pros**: Clean separation, scalable to large teams
- **Cons**: Adds ceremony (event declarations, subscriptions, cleanup), more files
  for simple operations, harder to trace control flow
- **Estimated Effort**: 2-3× current approach
- **Rejection Reason**: Over-engineering for current scope. The pure-function
  layer already provides testability without MVC ceremony. Consider this if the
  codebase exceeds ~5000 lines or 3+ developers.

## Consequences

### Positive

- **20 unit tests pass** with zero Unity scene dependencies (EditMode only)
- Deterministic seeded randomness enables reproducible AI behavior and replay
- JSON track authoring — designers can create tracks without touching C# code
- Inspector-tunable config via `GameConfigSO` — no recompilation for balance changes
- Pure functions are trivially testable and easy to reason about
- Fallback hardcoded track preserves backward compatibility

### Negative

- `MVPGameManager` remains a large coordinator (turn phases and race presentation
  orchestration still live there), but procedural HUD/hand construction, vehicle
  orientation/node interpolation, and turn input gating are isolated in dedicated
  adapters.
- Two data paths for tracks (JSON primary + hardcoded fallback) add maintenance
  burden
- No automated integration tests (full game loop still manual)
- `TrackManager` holds rendering logic alongside state — mixed concerns

### Neutral

- Static pure functions require explicit dependency passing (no DI container)
- Requires discipline to keep new code in the pure layer rather than Manager

## Risks

| Risk | Probability | Impact | Mitigation |
|------|------------|--------|-----------|
| MVPGameManager grows past 1500 lines | Medium | Code becomes hard to navigate | Extracted UIFactory, CarOrientationController, CarMovementAnimator, and RaceInputState; next boundary is turn-phase orchestration |
| JSON schema changes break all 8 tracks | Low | All tracks fail to load | Schema version field + migration script |
| Pure functions gain hidden state | Low | Tests become misleading | Code review gate — no static fields in Rules classes |

## Performance Implications

| Metric | Before (ADR-001) | After | Budget |
|--------|-----------------|-------|--------|
| CPU (frame time) | < 1ms | < 1ms | 16.6ms |
| Memory (runtime) | ~5MB | ~8MB (JSON configs) | 512MB |
| Load Time | Instant | + ~50ms (JSON parse) | 1s |

## Migration Plan

Already complete — this ADR documents the current state.

**Rollback plan**: Revert to commit `bb9ed07` (pre-refactor). No data migration
needed — the hardcoded fallback in `TrackManager` preserves the original behavior.

## Validation Criteria

- [x] All 20 EditMode tests pass without scene dependencies
- [x] Play Mode smoke test completes with 0 errors (Silverstone track, 60 nodes, 3 laps)
- [x] AI behavior is deterministic given same seed
- [ ] Integration test for full game loop (non-blocking follow-up)
- [ ] Track JSON schema validation tool (non-blocking follow-up)

## GDD Requirements Addressed

| GDD Document | System | Requirement | How This ADR Satisfies It |
|-------------|--------|-------------|--------------------------|
| `design/gdd/foodula-1-core-mechanics.md` | HEAT System | Heat payment, cooldown, engine failure | `RaceRules` pure functions with unit tests |
| `design/gdd/foodula-1-tracks.md` | Track System | Apex-based corner judging, data-driven layouts | `TrackRules` + `TrackDataLoader` + JSON pipeline |
| `design/gdd/foodula-1-ai.md` | AI Opponent | Heat-aware card selection strategy | `AIPlanner` with configurable thresholds |
| `design/gdd/foodula-1-core-mechanics.md` | Card System | Speed card selection, gear limits | `RaceRules.SumCardValues` + `GetMissingSpeedCardCount` |

## Related

- **ADR-001**: Superseded — this is the evolution
- **Future**: ADR-003 (UI layer separation) when MVPGameManager's animation/game-loop responsibilities are split
- **Code**: `Assets/Scripts/Core/RaceRules.cs`, `Assets/Scripts/Gameplay/TrackRules.cs`,
  `Assets/Scripts/AI/AIPlanner.cs`, `Assets/Scripts/Gameplay/TrackDataLoader.cs`
- **Tests**: `Assets/Tests/Editor/race_rules_test.cs`, `ai_planner_test.cs`,
  `track_rules_test.cs`
