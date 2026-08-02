# Quick Design Spec: Indianapolis Lane Speed and Start/Finish Lane Change

**Type**: Addition
**System**: Track Rules and Track Presentation
**GDD Reference**: `design/gdd/foodula-1-tracks.md` — Indianapolis section
**Date**: 2026-08-03

## Change Summary

Indianapolis keeps four visual lanes, but its oval corner limit now depends on
the lane: the inner lane is slowest and the outer lane is fastest. Each time
the player crosses the start/finish node, the player may move one lane inward,
stay in the current lane, or move one lane outward.

## Motivation

The four-lane oval currently has no mechanical distinction between racing
lines. Progressive limits create a meaningful inner-line risk/reward tradeoff,
while the start/finish choice gives the player a predictable place to change
lines without adding lane movement to every turn.

## Design Delta

Indianapolis corner limits are ordered from inner to outer as:

`[4, 5, 6, 7]`

The inner lane keeps the original base limit 4, and the outer lane reaches 7;
each move outward adds one point of corner-speed allowance.

The runtime renderer's lane indices are outer-to-inner for the
counter-clockwise Indianapolis path, so the mapping is explicit rather than
assuming lane index 0 is the inner lane. At start/finish, the player receives
three choices: inward one lane, keep the lane, or outward one lane. Invalid
boundary choices are disabled. The AI keeps its existing lane.

All other tracks retain their existing corner limits and do not expose the
lane-change prompt. Movement position, apex traversal, lap counting, camera,
and minimap remain centerline/cell based.

## Affected Systems

| System | Impact | Action Required |
|---|---|---|
| Track JSON/config | Adds lane limits and start/finish permission to Indianapolis | Update data model and JSON |
| TrackManager | Resolves lane-specific corner limits and lane direction helpers | Update runtime API |
| MVPGameManager | Waits for the player's start/finish lane choice and applies it to movement | Update race loop/UI |
| Track presentation rules | Maps renderer lane indices to inner-to-outer limits | Add pure helper methods |
| GDD / continuity docs | Records the new track rule | Update design and memory docs |

## Acceptance Criteria

- [ ] Indianapolis resolves every corner as inner-to-outer limits 4/5/6/7,
  with outer-lane limit 7.
- [ ] Non-Indianapolis tracks continue using their existing corner limits.
- [ ] At Indianapolis start/finish, player can choose inward, keep, or outward
  by at most one lane; invalid boundary actions are unavailable.
- [ ] The selected player lane is used for subsequent movement and corner
  checks; AI lane behavior is unchanged.
- [ ] No regression in apex-only corner detection, lap counting, or minimap
  centerline behavior.

## GDD Update Required?

Applied to the Indianapolis section of `design/gdd/foodula-1-tracks.md`.
