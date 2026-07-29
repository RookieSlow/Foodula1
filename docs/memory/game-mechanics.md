# Game Mechanics

This document records the mechanics represented by the current code. Values
may be overridden by the active `GameConfigSO` asset or by loaded track JSON.

## Turn and Card Loop

- Each player has a draw pile, hand, discard pile, and independent engine
  heat pool.
- The default hand limit is 7.
- The default speed deck contains twelve cards:
  `[1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4]`.
- Three heat cards start in the deck by default.
- A turn selects a gear from 1 through 4 and plays speed cards.
- The selected speed-card values determine movement.
- Played speed cards enter the discard pile; the discard pile is reshuffled
  when the draw pile is empty.
- Heat cards cannot be played as speed cards and can clog the hand.

## Gear and Cooling Rules

- The selected gear controls how many speed cards may be played.
- A normal shift changes one gear.
- A two-gear shift costs heat.
- Gear 1 removes up to three heat cards from the hand.
- Gear 2 removes up to one heat card from the hand.
- Higher gears provide no automatic cooling.

The gear-shift and cooling constants are still embedded in
`MVPGameManager.cs`; moving them into configuration or `RaceRules` is part of
the pending Scheme A refactor.

## Heat

- Each player starts with an independent engine heat pool of 6 by default.
- Overspeeding through corners, sudden braking, and engine failures can move
  heat cards from the engine pool into the deck/discard lifecycle.
- Cooling returns heat cards from the hand to the engine pool.
- Running out of payable engine heat affects the race flow according to the
  current manager rules.

The earlier "Cold Storage below zero" description belongs to the original
prototype and is no longer the authoritative model.

## Track and Race

- The current fallback track contains 42 nodes and runs for 3 laps by
  default.
- When `GameConfigSO.trackId` is populated, `TrackManager` attempts to load
  `Resources/Configs/Tracks/<trackId>.json`.
- JSON tracks may define their own node count, lap count, start/finish node,
  corners, speed limits, pit entry/exit, weather pool, and layout cells.
- `TrackManager` already contains JSON conversion, corner lookup,
  start/finish crossing, arbitrary-node positioning, and LineRenderer support.
- Full scene configuration, data validation, vehicle orientation, pit
  behavior, and weather integration remain incomplete.

## Opponents and Win Condition

- The current demo includes the player and one AI-controlled opponent.
- A race ends when a participant reaches the configured lap count.
- Final ranking compares completed laps and track position.

See [project-overview.md](project-overview.md) for architecture context and
[current-task-list.md](current-task-list.md) for remaining work.
