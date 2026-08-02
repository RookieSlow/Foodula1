# Quick Design Spec: Track Node Color Semantics

**Type**: Tweak
**System**: Track Presentation
**GDD Reference**: `design/gdd/game-concept.md` — Sections 3.4 and 5
**Date**: 2026-07-31

## Change Summary

Use node colors consistently across every track so the visual language matches
the apex-only corner-resolution rule.

## Motivation

Coloring an entire corner segment red implies that every cell may trigger a
penalty. Players need to distinguish the broader corner approach from the
single apex cell where speed is actually resolved.

## Design Delta

Current GDD says (`design/gdd/game-concept.md`, Section 5):

> 每个弯道只有一个弯心节点。只有移动路径踩到弯心才触发判定

This spec adds the following presentation rule without changing gameplay:

- Straight nodes are white.
- Non-apex nodes belonging to a corner are orange.
- Apex nodes that trigger corner resolution are red.
- The start/finish landmark remains green.

## Affected Systems

- `TrackPresentationRules`: owns the shared color mapping.
- `TrackManager`: applies the mapping to every rendered track node.
- `Race` scene: stores the default white, orange, red, and green colors.
- Track presentation EditMode tests: verify all four mappings.
- Track configuration data: enforce exactly one apex per corner group.

## Edge Cases

- Start/finish takes visual priority if a malformed track also marks it as a
  corner or apex.
- A null or unclassified node falls back to the straight-node color.
- The hard-coded fallback track uses the same mapping as JSON tracks.

## Acceptance Criteria

- [x] Every straight node renders white.
- [x] Every non-apex corner node renders orange.
- [x] Every apex node renders red.
- [x] Start/finish remains green.
- [x] One shared rule applies to all selectable tracks and the fallback track.
- [x] Every track configuration has exactly one apex per corner group.
- [x] Unity EditMode tests pass after compilation (181 passed, 0 failed).

## GDD Update Required?

No. The existing gameplay rule remains unchanged; this quick spec defines its
presentation mapping.
