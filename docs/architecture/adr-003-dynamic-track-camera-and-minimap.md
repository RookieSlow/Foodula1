# ADR-003: Dynamic Track Camera and Minimap

- **Status:** Accepted
- **Date:** 2026-07-31
- **Related design:** `design/quick-specs/track-camera-and-minimap-addition-2026-07-31.md`

## Context

Track layouts range from 42 to 267 cells. A single fixed orthographic camera
cannot keep cars readable while also presenting the full circuit.

## Decision

Use two orthographic views:

1. the main camera smoothly fits a configurable wrapped window of track cells
   around the player's rendered position (15 behind and 15 ahead by default);
2. a runtime-created secondary camera renders the complete track to a
   `RenderTexture` displayed by the race HUD.

Camera geometry is implemented as pure functions in `RaceCameraRules`. Runtime
composition and smoothing live in `RaceCameraController`. Tuning values remain
in `GameConfigSO`, and `MVPGameManager` exposes only the two rendered car
transforms required by the presentation layer.

Dense-track node sizing is derived from median neighboring-cell distance through
TrackPresentationRules. This preserves existing visuals on standard tracks
while shrinking nodes and labels on endurance layouts.

Minimap car indicators are UI markers projected through the minimap camera.
This keeps them a stable pixel size without introducing project layers or
duplicating world-space car objects.

## Consequences

- Camera calculations can be unit tested without entering Play Mode.
- Every track automatically receives the same presentation.
- The minimap incurs one additional low-resolution camera render.
- The controller is created at runtime, so existing scenes and prefabs do not
  require serialized changes.

