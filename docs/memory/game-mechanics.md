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
- When trick cards are enabled, four team cards (two attack and two defense)
  are shuffled into the same draw pile before the opening seven-card draw.
- A turn selects a gear from 1 through 4 and confirms playable cards one at a
  time. Only one card can be pending confirmation.
- Confirmed trick cards resolve immediately, leave the hand, and enter the
  discard pile. They can return after the discard pile is reshuffled, and the
  existing one-trick-per-turn limit still applies.
- Confirmed speed cards leave the hand and accumulate in the current turn's
  played area. Pressing the action button with no pending card ends card play;
  any missing required speed cards use the existing engine-failure rule.
- The selected speed-card values determine movement.
- Played speed cards enter the discard pile during end-of-turn cleanup; the
  discard pile is reshuffled when the draw pile is empty.
- Heat cards cannot be played as speed cards and can clog the hand.
- The optional discard step can discard speed or trick cards without resolving
  them, but heat cards cannot be selected.
- Kanto Oden carry-over slots and its skip flag are consumed at the start of
  the next turn independently of whether the tech-tree module is enabled. The
  current runtime counts those carry-over slots as required for missing-card
  engine failure; whether they should instead be optional remains a design
  decision.
- Hotpot grants its +1 movement only when an ATTACK trick actually occupies the
  optional Hotpot slot beyond the gear and Kanto Oden card slots.

## Gear and Cooling Rules

- The selected gear controls how many speed cards may be played.
- A normal shift changes one gear.
- A two-gear shift costs heat.
- Gear 1 removes up to three heat cards from the hand.
- Gear 2 removes up to one heat card from the hand.
- Higher gears provide no automatic cooling.

Gear-shift heat cost and gear-one/gear-two cooling values are configured in
`GameConfigSO`. `MVPGameManager` resolves these rules through `RaceRules` for
both the player and AI race flow.

## Heat

- Each player starts with an independent engine heat pool of 6 by default.
- Overspeeding through corners, sudden braking, and engine failures can move
  heat cards from the engine pool into the deck/discard lifecycle.
- Cooling returns permanent heat cards from the hand to the engine pool.
  Temporary heat cards are consumed and destroyed instead; cooling, recovery,
  and defensive deck removal can never convert them into permanent engine heat.
- Running out of payable engine heat affects the race flow according to the
  current manager rules.

The earlier "Cold Storage below zero" description belongs to the original
prototype and is no longer the authoritative model.

## Track and Race

- The main menu currently offers eight JSON-backed tracks. The selected track
  is carried into the Race scene; Silverstone remains the default selection,
  and the legacy 42-node layout is a fallback only.
- When `GameConfigSO.trackId` is populated, `TrackManager` loads
  `Resources/Configs/Tracks/<trackId>.json`.
- JSON tracks may define their own node count, lap count, start/finish node,
  corners, apex cells, speed limits, pit entry/exit, weather pool, and layout.
- Corner-speed resolution triggers only when movement crosses an `isApex`
  cell. Repeated apex cells for the same corner are deduplicated per move.
- Player initialization and lap crossing use the runtime node marked
  `isStartFinish`, including when that node is not index 0.
- HUD position totals and LineRenderer coordinates use the loaded track data.
- Track presentation uses the selected layout background in Play Mode, with
  translucent yellow corner masks, red apex masks, and visible speed-limit
  labels. Runtime grid nodes, lane lines, and debug corner text are hidden;
  tracks with explicit lane-specific limits show the effective limit above each
  lane's apex. Editor Scene view retains node metadata and limits for authoring.
  Ordinary tracks place all cars on the inside lane by default; when cars share
  the same lap and node, the stable-order trailing car uses the outside lane
  for the side-by-side visual. This is presentation-only: gameplay positions,
  corner checks, lap counts, camera framing, and minimap tracking are unchanged.
  Indianapolis keeps explicit per-car lane choices, and the player's start/finish
  lane selection immediately repositions the player car.
- On `indianapolis_burger`, corner limits are lane-specific from inner to outer:
  `4/5/6/7`; in the Unity path data lane 0 is the inside lane and lane 3 is
  the outside lane, so the outer lane accommodates a speed total well above 4
  before overspeed heat. At each start/finish crossing, the player may move one
  lane inward, keep the current lane, or move one lane outward; boundary
  choices are disabled and the AI keeps its lane.
- Vehicle sprites follow the track tangent: spawning and teleport-style moves
  snap immediately to the next-node direction, while normal movement rotates
  smoothly according to `carRotateSpeed`.
- Track authoring workflow, pit behavior, weather integration, and a full
  multi-lap manual playthrough still need completion or broader validation.

## Opponents and Win Condition

- The current demo includes the player and one AI-controlled opponent.
- AI speed-card selection uses configurable normal, heat-warning, and
  corner-risk behavior. Its variation probability and random source are
  injectable so seeded runs can be reproduced in tests.
- A race ends when a participant reaches the configured lap count.
- Final ranking compares completed laps and track position.

## Drivers

- The main menu exposes a session-only driver selection panel with two drivers
  for each of the six national teams.
- Each `PlayerState` stores a driver catalog ID and XP. The selected driver
  supplies the player's team for race initialization; an unset selection uses
  the configured team's first catalog entry.
- Driver progression uses cumulative XP thresholds `0/100/250/500/1000/2000/4000`
  for levels 1 through 7. Passive tiers unlock at levels 2/4/6 and active
  tiers at levels 3/5/7. Level 7 provides two active uses per race; the UK
  team adds one extra active use.
- `DriverData.cs` currently provides the immutable catalog and structured skill
  descriptions. Signature-skill runtime effects are intentionally not applied
  to movement yet; those effects need a separate rules slice and tests.

See [project-overview.md](project-overview.md) for architecture context and
[current-task-list.md](current-task-list.md) for remaining work.
