# Game Mechanics

This document records the mechanics represented by the current code. Values
may be overridden by the active `GameConfigSO` asset or by loaded track JSON.

> **Implementation snapshot (2026-08-25)**: Unity `2022.3.62f3c1`; the latest
> latest successful editor EditMode run passed `430/430`. The required
> `production/session-state/active.md` file is currently absent, so this
> document is based on source, configuration, and the live editor state.

## Turn and Card Loop

- Each player has a draw pile, hand, discard pile, and independent engine
  heat pool.
- The default hand limit is 7.
- The default speed deck contains twelve cards:
  `[1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4]`.
- Heat cards do not start in the normal deck. Each player's independent engine
  heat pool is the only source of permanent heat cards.
- When trick cards are enabled, four team cards (two attack and two defense)
  are shuffled into the normal draw pile before the opening seven-card draw;
  ordinary drawing never draws heat cards.
- A turn selects a gear from 1 through 4. Speed cards can be selected singly
  or as a group and confirmed together; trick cards remain one-at-a-time
  immediate actions. A speed-card selection may never exceed the turn limit.
  The selected card visual scales to 1.08, lifts 24 px, and uses a 0.14-second
  unscaled-time transition plus a blue shadow; the LayoutGroup slot size and
  sibling positions remain unchanged.
- Confirmed trick cards resolve immediately, leave the hand, and enter the
  discard pile. They can return after the discard pile is reshuffled, and the
  existing one-trick-per-turn limit still applies.
- Confirmed speed cards leave the hand and accumulate in the current turn's
  played area. Pressing the action button with no pending card ends card play;
  any missing required speed cards use the existing engine-failure rule.
- The selected speed-card values determine movement.
- The draw-pile and discard-pile panels show stacked backs, up to three live
  card thumbnails, and a quantity badge. Draw previews follow the actual draw
  order; discard previews start from the most recently discarded card. Visible
  stack thickness grows by one layer per three cards up to seven layers, while
  the badge remains exact.
- Card ownership changes use a non-blocking table overlay: played/discarded
  cards fly from hand to discard, heat payments fly from engine to their rule
  destination, and cooling flies from the actual source zone back to engine.
- Played speed cards enter the discard pile during end-of-turn cleanup; only
  non-heat cards are reshuffled when the draw pile is empty.
- Heat cards cannot be played as speed cards and can clog the hand after they
  are explicitly paid from the engine or granted by an effect.
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
- Gear 1 removes up to three heat cards through the cooling priority:
  hand, then draw pile, then discard pile.
- Gear 2 removes up to one heat card through the same priority.
- Higher gears provide no automatic cooling.
- Italy has no permanent straight movement bonus. Completing a corner arms a
  one-shot `+1` for the first speed card played on a later turn; an empty turn
  preserves it, and a failed/spun corner does not arm it.
- China AI projects every unique apex crossed by the lowest legal Go card set.
  It may accept at most one corner heat when the engine can also pay overclock
  and missing-card costs; larger or unaffordable risks force Recover. The
  tolerance is `GameConfigSO.aiChinaAffordableCornerHeat` (currently `1`).

Gear-shift heat cost and gear-one/gear-two cooling values are configured in
`GameConfigSO`. `MVPGameManager` resolves these rules through `RaceRules` for
both the player and AI race flow.

## Heat

- Each player starts with an independent engine heat pool of 6 by default.
- Overspeeding through corners, sudden braking, and engine failures pay heat
  cards from the engine pool into the hand by default, so heat occupies hand
  capacity; an explicit effect can choose the discard pile (China's Yin/Yang
  Tea Go branch does this). Heat never enters through ordinary drawing.
- Generic cooling returns permanent heat cards from hand/draw/discard to the
  engine pool in that order. The normal draw pile contains no heat cards, but
  the draw-pile step remains a defensive boundary for legacy or explicit test
  states. Temporary heat cards are consumed and destroyed instead; effects that
  explicitly say “from hand” remain hand-only.
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
- A pit decision is offered while the car is 1–10 cells before `pit_entry`.
  Choosing to pit only schedules the stop; after the car crosses the entry,
  the next turn is consumed by the pit stop. The car then exits one cell beyond
  authored `pit_exit` by default. `GameConfigSO.pitExitMoveBonus` controls the
  base value; China's Fast Charge tech adds another cell.
- Corner-speed resolution triggers only when movement crosses an `isApex`
  cell. Repeated apex cells for the same corner are deduplicated per move.
- Player initialization and lap crossing use the runtime node marked
  `isStartFinish`, including when that node is not index 0.
- HUD position totals and LineRenderer coordinates use the loaded track data.
- Player-facing positions use 1-based `格 X/N`. The human car has a pulsing
  technology-blue ring and a local scale covering six cells behind and ahead;
  dense layouts retain every tick but sample number badges to avoid overlap.
- Track presentation uses the selected layout background in Play Mode, with
  smoothed, rounded corner ribbons over a dark edge: Lv1 is safety green, Lv2
  amber, and Lv3 warning coral. Apex badges inherit the corner level color and
  keep visible speed-limit labels. Runtime grid nodes, lane lines, and debug corner text are hidden;
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
- Track authoring workflow, pit behavior, and a full multi-lap manual playthrough
  now have full-race evidence; broader multi-track validation remains open.

## Weather

- The selected track supplies a weighted weather pool and optional default.
  Weather selection is deterministic when the session receives an injected
  `IRandomSource`.
- Runtime weather profiles are `Sunny`, `Cloudy`, `LightRain`, `HeavyRain`, and
  `Hot`; the legacy `Rainy` enum name remains an alias for `LightRain`.
- Cloudy keeps the normal slipstream trigger range but reduces the final
  slipstream movement bonus by one. Light rain reduces corner limits
  by one and adds one spin-counter point on a spin-out. Heavy rain reduces corner
  limits by two, adds two spin-counter points, and disables slipstream. Hot
  reduces reaction-step cooling by one. Corner limits, slipstream, cooling,
  spin-out increments, and HUD labels all resolve through `WeatherRules`.
- Slipstream uses every racer's complete non-slipstream movement plan, including
  vehicle, technology and trick bonuses. A successful first slipstream advances
  the simulated endpoint and may follow one different car for a second bonus;
  the same leader cannot be reused and the chain is capped at two triggers.
  Before movement, every resolved chain segment is merged into one 0.9-second
  unscaled visual phase with blue moving dash trails, two-car focus pulses, and
  total bonus text; each segment is also written to the manual race log.
- The deterministic full-race test and track/team balance benchmark use the
  same four phase boundary as runtime: all racers choose cards, all non-slipstream
  plans are frozen, all slipstream chains resolve, then movement executes in
  rank order. The benchmark reports trigger count and movement gained per team.
- Weather rolls once per newly crossed lap; the `RaceWeatherState` gate prevents
  multiple cars crossing the same start/finish node from rerolling the lap.

## Opponents and Win Condition

- The default demo includes the player and one AI-controlled opponent;
  `aiOpponentCount` supports a larger configured opponent count, while the
  broader multi-opponent balance/playtest remains open.
- AI speed-card selection uses configurable normal, heat-warning, and
  corner-risk behavior. Its variation probability and random source are
  injectable so seeded runs can be reproduced in tests.
- A participant reaching the configured lap count locks its finish order; the
  race ends after all non-blown participants have finished, or no active
  participant remains.
- `hasFinished` and `isBlown` are terminal gameplay states. A finisher still
  clears already-played speed cards into discard, but no longer resolves
  Yin/Yang Tea, Grill Spezial, heat payments, movement, or other end-of-turn
  technology while waiting for the remaining racers.
- A blown/DNF participant is removed from future turns and ranks below active
  and finished racers; it does not immediately terminate the race.
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
