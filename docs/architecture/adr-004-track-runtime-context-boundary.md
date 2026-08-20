# ADR-004: Track Runtime Context Boundary

## Status

Accepted

## Date

2026-08-19

## Engine Compatibility

| Field | Value |
|-------|-------|
| **Engine** | Unity 2022.3.62f2 |
| **Domain** | Core / Track Runtime |
| **Knowledge Risk** | LOW |
| **References Consulted** | `docs/engine-reference/unity/VERSION.md`, `docs/architecture/adr-002-layered-pure-function-architecture.md` |
| **Post-Cutoff APIs Used** | None |
| **Verification Required** | EditMode regression suite and Race Play Mode smoke test |

## ADR Dependencies

| Field | Value |
|-------|-------|
| **Depends On** | ADR-002 (layered pure-function architecture) |
| **Enables** | Further separation of track loading, gameplay, AI, camera and vehicle presentation |
| **Blocks** | None |
| **Ordering Note** | Keep the compatibility facade until all Unity consumers can read the context directly. |

## Context

### Problem Statement

`TrackManager` had become both the JSON/fallback loader, the mutable owner of
track gameplay state, and the renderer. JSON loading also wrote laps, node
count, and fallback dimensions into the shared `GameConfigSO`, allowing one
scene component to change configuration consumed by the race loop and other
systems.

### Constraints

- Preserve existing Race scene serialization and the public `TrackManager` API.
- Keep track JSON as the authoring source and retain the fallback track.
- Keep gameplay rules testable without entering Play Mode.
- Preserve loaded world positions, lane presentation, weather selection, and
  lap behavior.

## Decision

Build one `TrackRuntimeContext` after the track is loaded. It snapshots nodes,
world coordinates, lane offsets, corner metadata, weather metadata, lap count,
country, and lane-specific limits. It copies arrays, dictionaries, and nodes at
the boundary and exposes read-only collection views or defensive copies.

`TrackManager` remains the Unity adapter responsible for loading and rendering.
Its existing methods delegate to `Runtime` so legacy scene bindings and
compatibility callers keep working. AI look-ahead, camera path caching, vehicle
spawn/movement/lane/teleport presentation, and race orchestration now consume
`Runtime` directly, as does `TrackDebugOverlay` for node count, metadata and
world positions. `TrackRuntimeContext.GetTraversalEvents` supplies one immutable
event snapshot containing the normalized nodes visited by movement, start/finish
crossings, unique apex corners and pit-entry hit, so vehicle animation, start/finish,
apex, Nigiri and pit-entry events share one wrap-aware traversal rule. The race coordinator reads track-owned values such as
`Nodes`, `TotalLaps`, `WeatherPool`, `TrackName`, and `Country` through that boundary.
JSON loading no longer mutates `GameConfigSO`; fallback dimensions are resolved
when the context is built.

```text
Track JSON / fallback data
          |
          v
TrackManager (load + render adapter)
          |
          v
TrackRuntimeContext (one read-only runtime snapshot)
       /       |        \\
 Race loop    AI     camera/vehicle presentation
```

### Key Interfaces

```csharp
public TrackRuntimeContext TrackManager.Runtime { get; }
public int TrackManager.TotalLaps { get; }
public string[] TrackManager.WeatherPool { get; }
public string TrackManager.TrackName { get; }
public string TrackManager.Country { get; }
```

The compatibility methods (`GetNode`, `GetNodePosition`, corner queries,
lane queries, and `Nodes`) remain available and delegate to the same snapshot.

## Alternatives Considered

### Alternative 1: Keep mutable state in TrackManager

- **Description**: Let the manager continue exposing its staging lists and
  write JSON-derived values into `GameConfigSO`.
- **Pros**: Lowest immediate code change.
- **Cons**: Preserves hidden cross-system writes and makes loader/rendering
  lifecycle observable to gameplay.
- **Rejection Reason**: Conflicts with ADR-002's dependency boundary and the
  current coupling-reduction priority.

### Alternative 2: Full event-driven track service

- **Description**: Replace direct reads with a track event bus and separate
  loader, state, renderer, and presentation services.
- **Pros**: Strong isolation for a larger project.
- **Cons**: Adds lifecycle, subscription cleanup, and tracing overhead to a
  small turn-based game.
- **Rejection Reason**: Over-scoped for the current demo; ADR-002 explicitly
  prefers direct pure-rule calls over full MVC/event ceremony.

## Consequences

### Positive

- The race loop no longer relies on JSON loading mutating shared config.
- AI, camera, vehicle movement, movement planning, corner resolution, pit logic,
  and UI can converge on one track snapshot instead of separate manager internals.
- Movement-side consumers now share `TrackTraversalEvents`, so a single raw target
  cannot produce mismatched animation, lap, apex, landmark or pit-entry results.
- Snapshot copies make boundary mutation tests possible and deterministic.
- Existing scenes and serialized references remain compatible.

### Negative

- The manager still contains rendering code and a compatibility facade.
- Track nodes are copied once, so future live track-editing tools must rebuild
  the context explicitly.
- Debug node queries now use the snapshot directly; the manager still owns
  presentation settings (toggle, prefab and colors) and the compatibility
  facade. Further reduction is deferred until no serialized binding depends on it.

### Risks

- A future feature might read `LoadedTrackConfig` directly and bypass the
  snapshot. Mitigation: use `Runtime` metadata properties for new gameplay
  code and keep the direct config property for authoring compatibility only.
- A scene could hold different `GameConfigSO` references on its components.
  Mitigation: track-owned laps and weather now come from the loaded snapshot;
  visual sizing remains component-local until the environment adapter is
  migrated.

## GDD Requirements Addressed

| GDD System | Requirement | How This ADR Addresses It |
|------------|-------------|--------------------------|
| `foodula-1-tracks.md` | JSON cells, apex traversal, lane-specific Indianapolis limits, and track metadata | One loaded runtime context is the authoritative read boundary for those values. |
| `foodula-1-core-mechanics.md` | Closed-loop positions, lap crossings, and configurable track length | The race loop consumes snapshot node count and lap count without mutating global config. |
| `foodula-1-ai.md` | AI reads current cells and corner limits during look-ahead | `AIController` consumes `TrackManager.Runtime` directly; the context preserves the same node and lane-limit behavior. |

## Performance Implications

- **CPU**: One-time copy/build at track load; no per-frame cost beyond the
  existing lookups.
- **Memory**: One small copy of nodes, coordinates, and metadata per loaded
  track, negligible against scene and sprite memory.
- **Load Time**: Minor allocation during existing track initialization.
- **Network**: Not applicable.

## Migration Plan

1. Build and expose `TrackRuntimeContext` at the existing `TrackManager.Awake`
   load boundary.
2. Delegate current manager methods to the context and stop writing JSON values
   into `GameConfigSO`.
3. Route race-loop lap/weather/metadata reads through context-backed properties.
4. Migrate AI, camera, vehicle and race orchestration adapters to
   `TrackManager.Runtime`; retain the facade for presentation-only legacy consumers.
5. Migrate the remaining debug presentation node reads to `TrackRuntimeContext`;
   keep presentation settings on `TrackManager` for existing scene bindings.
6. Reduce the facade surface when no serialized binding depends on it.

## Validation Criteria

- `TrackRuntimeContext` boundary, lane-limit, traversal, metadata-copy and
  position-normalization tests pass.
- Full Unity EditMode suite passes with no failures or skips.
- Race scene starts and runs through the initial waiting-for-gear state with
  no project Console errors or warnings.
- No scene assets are modified by the refactor.

## Related Decisions

- `docs/architecture/adr-002-layered-pure-function-architecture.md`
- `docs/architecture/adr-003-dynamic-track-camera-and-minimap.md`
- `docs/module-integration-guide.md`
