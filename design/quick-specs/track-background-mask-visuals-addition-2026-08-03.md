# Quick Design Spec: Track Background and Corner Mask Visuals

**Type**: Addition
**System**: Track Presentation
**GDD Reference**: `design/gdd/foodula-1-tracks.md`
**Date**: 2026-08-03

## Change Summary

The race view uses the generated track-layout background as the road surface.
Runtime node dots, lane centerlines, corner labels, and grid-like markers are
hidden as debugging geometry. Corner segments receive a translucent yellow
overlay, each apex receives a translucent red overlay, and each apex displays
the applicable speed-limit number directly on the road.

## New Rules / Values

- Background layout sprites remain visible and are fitted to the configured
  track world rectangle.
- Straight sections receive no runtime overlay.
- Every corner group receives one yellow `LineRenderer` mask spanning its
  corner cells and exit point.
- The unique `isApex` node in each corner group receives a red mask.
- Every apex displays a clear speed-limit number; tracks with explicit
  lane-specific limits show one value per lane using that lane's effective
  limit.
- Runtime debug nodes and lane lines are disabled by default.
- Unity editor Scene view draws node positions, types, corner IDs, and apex
  labels through Gizmos/Handles for authoring and debugging.
- `showDebugTrackNodesInPlay` can be enabled temporarily for diagnostics.

## Affected Systems

| System | Impact | Action Required |
|---|---|---|
| TrackManager | Replaces runtime line/node visuals with corner masks | Update renderer |
| TrackEnvironmentController | Continues to provide the selected track background | No data change |
| Editor visualization | Shows node metadata only in Scene view | Add Gizmos/Handles |

## Acceptance Criteria

- [ ] Play Mode shows the selected track-layout sprite as the road surface.
- [ ] Play Mode shows yellow corner masks, red apex masks, and visible
  speed-limit numbers.
- [ ] Play Mode shows no runtime node dots, lane centerlines, or debug corner
  text.
- [ ] Scene view shows node colors and metadata labels for editing.
- [ ] Straight sections remain unmodified by overlays.
- [ ] No regression in vehicle movement, lane positions, apex detection, or
  camera/minimap centerline tracking.

## GDD Update Required?

No gameplay rule changes; this is a presentation-layer addition.
