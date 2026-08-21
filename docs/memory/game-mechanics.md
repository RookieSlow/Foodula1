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
- A turn selects a gear from 1 through 4. Speed cards can be selected singly
  or as a group and confirmed together; trick cards remain one-at-a-time
  immediate actions. A speed-card selection may never exceed the turn limit.
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
- Gear 1 removes up to three heat cards through the standard cooling order:
  hand → draw pile → discard pile.
- Gear 2 removes up to one heat card through the same order.
- Higher gears provide no automatic cooling.

All ordinary cooling effects use the same hand → draw pile → discard pile
priority. Permanent heat returns to the engine pool and temporary heat is
destroyed. Full recovery effects such as a spin-out recovery or pit stop still
collect heat from all three zones.

Gear-shift heat cost and gear-one/gear-two cooling values are configured in
`GameConfigSO`. `MVPGameManager` resolves these rules through `RaceRules` for
both the player and AI race flow.

## Heat

- Each player starts with an independent engine heat pool of 6 by default.
- Overspeeding through corners, sudden braking, and engine failures can move
  heat cards from the engine pool into the deck/discard lifecycle.
- Cooling returns permanent heat cards from the hand, draw pile, or discard pile
  to the engine pool according to the standard zone order.
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
- Pit-entry detection consumes the unnormalized forward movement target, so
  cross-lap movement and movement bonuses cannot skip a `pit_entry` node;
  selecting a pit stop simulates five cells of pit-lane transit, cools all heat,
  and skips one turn. The authored `pit_exit` remains a track validation and
  presentation marker. The current runtime allows any eligible player or AI
  participant to use a track-defined pit lane; only Shanghai currently
  contains `pit_entry`/`pit_exit` nodes. The China-specific skip helper is
  retained as a compatibility hook, not as a general team lock.
- Pit-stop resolution is a two-stage boundary: `PitLaneRules.ResolvePitStop`
  returns the transition without mutating `PlayerState`, and `ApplyPitStop`
  applies its position/skip state. The legacy `EnterPit` method composes both
  stages for compatibility, while orchestration and pure simulation should use
  the explicit pair.
- Corner-speed resolution triggers only when movement crosses an `isApex`
  cell. Repeated apex cells for the same corner are deduplicated per move.
- Player initialization and lap crossing use the runtime node marked
  `isStartFinish`, including when that node is not index 0.
- `TrackManager` builds one `TrackRuntimeContext` snapshot after loading the
  selected JSON/fallback track. Runtime gameplay reads node count, lap count,
  weather metadata, lane offsets, corner limits, and world positions from this
  boundary; JSON loading no longer writes derived values into the shared
  `GameConfigSO`.
- HUD position totals and LineRenderer coordinates use the loaded track data.
- Vehicle spawning, per-node movement, Indianapolis lane selection, visual lane
  refresh, and teleport orientation read node positions, lane limits, and
  start/finish metadata from the same `TrackRuntimeContext` snapshot; the
  `MVPGameManager` movement planning and apex-only corner resolution now also
  query the same snapshot for node traversal, unique corners, lane limits and
  corner names. Race orchestration also uses the snapshot for pit entry/exit
  nodes, landmarks, MotherRoad, YinYang movement, lap/weather limits and track
  metadata; `TrackDebugOverlay` now reads node count, metadata, and positions directly from the
  snapshot. `TrackManager` remains only for debug presentation settings and legacy compatibility.
- The raw forward path is sampled once through `TrackRuntimeContext.GetTraversalEvents`;
  `TrackTraversalEvents` exposes the ordered normalized nodes, repeated start/finish crossings,
  unique apex corners, and pit-entry hit for the same movement target. Vehicle animation,
  start/finish crossings, apex checks, Nigiri and pit-entry detection consume that shared event
  snapshot, including when a movement target crosses one or more lap boundaries.
- Mother Road landmark checks consume the final unnormalized movement target, so non-zero
  landmarks remain correct when a bonus movement crosses a lap boundary more than once.
- The pure race simulation and the compatibility `PitLaneRules.CrossedPitEntry` facade consume
  the same `TrackTraversalEvents` snapshot as runtime movement; regression simulations must not
  reimplement start/finish or pit-entry path iteration.
- `RaceLapWeatherRules.AdvanceCrossings` resolves repeated start/finish crossings from one
  movement as an ordered pure batch, updates the local per-lap weather gate between crossings,
  and stops at the first finishing crossing. Runtime side effects still run once per returned
  crossing, so multi-lap movement cannot allocate a second finish order after completion.
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
- Track JSON authoring and schema validation are available. Adding new tracks
  and the full multi-weather/multi-lap/pit manual matrix still need broader
  validation.

## Weather

- The selected track supplies a weighted weather pool and optional default.
  Weather selection is deterministic when the session receives an injected
  `IRandomSource`.
- Runtime weather profiles are `Sunny`, `Cloudy`, `LightRain`, `HeavyRain`, and
  `Hot`; the legacy `Rainy` enum name remains an alias for `LightRain`.
- Cloudy reduces slipstream range by one cell. Light rain reduces corner limits
  by one and adds one spin-counter point on a spin-out. Heavy rain reduces corner
  limits by two, adds two spin-counter points, and disables slipstream. Hot
  reduces reaction-step cooling by one. Corner limits, slipstream, cooling,
  spin-out increments, and HUD labels all resolve through `WeatherRules`.
- Weather rolls once per newly crossed lap; the `RaceWeatherState` gate prevents
  multiple cars crossing the same start/finish node from rerolling the lap.

## Opponents and Win Condition

- The default demo includes the player and one AI-controlled opponent;
  `aiOpponentCount` supports a configurable 0–3 AI participants.
- AI speed-card selection uses configurable normal, heat-warning, and
  corner-risk behavior. Its variation probability and random source are
  injectable so seeded runs can be reproduced in tests.
- A race ends when a participant reaches the configured lap count.
- Final ranking compares completed laps and track position.

When CN L1 Yin/Yang Tea is active, the human player chooses Yin or Yang at
end-of-turn. Yin pays one engine heat for +1 movement; Yang cools one heat using
the standard cooling order. AI participants retain the automatic policy.

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
