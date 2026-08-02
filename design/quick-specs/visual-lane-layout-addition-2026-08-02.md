# Quick Design Spec: Visual Multi-Lane Track Layout

**Type**: Addition
**System**: Track Presentation
**GDD Reference**: `design/gdd/game-concept.md` — Sections 3.1 and 5
**Date**: 2026-08-02

## Change Summary

Represent each track as multiple parallel visual lanes so vehicles can occupy
the same centerline cell side by side. All tracks use two lanes except
`indianapolis_burger`, which uses four lanes to match an oval circuit.

## Motivation

The current renderer exposes only one centerline position per cell, so the
player and AI overlap or appear diagonally offset when they share a position.
The requested presentation should read as a road with multiple lanes while
keeping the existing cell-based race rules intact.

## Design Delta

Current implementation uses one world-space track position per cell. This
addition changes the presentation layer to derive centered lateral slots from
the local track tangent:

- Standard tracks: two parallel lanes with offsets `-0.14` and `+0.14` world
  units at the default `0.28` lane spacing.
- Indianapolis: four parallel lanes with offsets `-0.42`, `-0.14`, `+0.14`,
  and `+0.42` world units.
- The player and AI use adjacent middle slots. Future participants can use
  the remaining slots without changing the centerline cell position.
- Track rules, corner detection, speed limits, lap counting, and minimap
  framing continue to use the centerline nodes.
- Lane centerlines are rendered in the race scene and the generated track
  background overlays use the same lane count.

## Edge Cases

- A missing or unknown track ID defaults to two lanes.
- A lane index is clamped to the available slot range.
- Lane offsets follow the local tangent and use a wrapped previous/next node
  at the lap boundary.

## Acceptance Criteria

- [x] Every non-Indianapolis track reports two lanes.
- [x] Indianapolis reports four lanes.
- [x] Player and AI spawn in distinct adjacent slots on the start/finish cell.
- [x] Movement animation and spin-out rewind preserve each vehicle's slot.
- [x] Centerline camera and minimap bounds remain unchanged.
- [x] Generated track backgrounds show the configured number of lanes.
- [x] Unity EditMode validation passes with no new errors (181/181).
- [ ] Play Mode validation passes with no new errors.

Validation note: the Unity MCP EditMode run completed 181/181 with no failures.
The PlayMode job did not initialize within its 120-second timeout, so it was
stopped and remains an open validation item rather than being treated as a
code failure.

## GDD Update Required?

No. This is a presentation-layer addition; the existing cell-based gameplay
rule remains authoritative.
