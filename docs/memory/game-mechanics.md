# Game Mechanics

This document records the mechanics represented by the current code. Values
may be overridden by the active `GameConfigSO` asset or by loaded track JSON.

> **Implementation snapshot (2026-09-03)**: Unity `2022.3.62f3c1`; the latest
> successful editor EditMode run passed `603/603`; tutorial guidance/checkpoint tests previously passed `31/31` and
> the encyclopedia catalog checks pass `6/6`.
> The required
> `production/session-state/active.md` file is currently absent, so this
> document is based on source, configuration, and the live editor state.
> The 2026-08-27 tailwind time-scale regression passed focused `5/5` and full
> EditMode `471/471`; the Play Mode attempt did not initialize because this
> project has no standalone PlayMode test assembly, so it is not counted as a
> passing runtime test.
> On 2026-08-26, the tailwind/HUD-focused regression passed 56/56 and the full
> EditMode suite passed 451/451; a 5-second MainMenu Play Mode smoke produced
> no project errors or warnings.
> A controlled four-car Silverstone Play Mode smoke on 2026-08-25 produced
> `[SLIPSTREAM]` log entries and found `RaceEventFX` present; this is runtime
> event-chain evidence, not a substitute for a manual two-link visual check.
> The AI tailwind rerun covered 15 focused tests and the full 451-test suite,
> including the same-lap and cross-lap boundary cases; all passed with no skips.
> The team-badge rerun covered 18 focused tests and the full 469-test suite;
> all passed with no skips. Console retained only the existing
> LogAssert-expected missing-track error from its regression test.

## Career Season

- A career season uses the eight entries in `TrackSelectionState.AvailableTracks` in their explicit
  order; Resources enumeration, `fallback_42` and tutorial-only track rules are not part of the calendar.
- The current career rules model a four-car field, matching the configured player-plus-up-to-three-AI
  race boundary. Any of the six teams may be selected, but confirmation locks that team in the rules
  state until the career is completed or later abandoned through the persistence/UI layer.
- Classified finishes score `10/6/4/2`; DNF and invalid positions score zero. Duplicate team entries,
  wrong-track results and repeated result IDs are rejected before state advancement.
- Championship ties compare points, wins, podiums, best finish, most recent finish and finally the
  stable season competitor order.
- Technology configuration is locked during races 1–4. Resolving race four enters `SummerBreak` and
  blocks race five until the sole technology adjustment is confirmed. Races 5–8 are locked again, and
  completing race eight does not create another adjustment window.
- Career progress is stored only under `Foodula1.Career.V1`. The versioned DTO carries the locked team,
  stable competitors, exact schedule/version, results, reconstructed standings, phase and initial/mid-season
  technology snapshots. Loading replays every result through the same rules and rejects drift or tampering.
- Missing or malformed career data returns a safe `NotStarted` state without touching normal tech profiles,
  RP, drivers, tutorial completion or settings. Invalid raw data is retained until explicit replacement or
  abandonment. Candidate progress replaces live state only after storage succeeds.
- The main menu presents the existing single-race route as `自由赛事` without changing its track-selection
  callback. A separate runtime career overlay creates a season from any team, shows the locked team, eight-race
  calendar and computed standings, and requires blocking confirmation before replacement or abandonment.
  Race launch copies the authoritative scheduled track, locked team, stable competitors and career-owned tech
  snapshot into a session request; it does not mutate Quick Race selection or the normal tech profile.
- Track resolution has one precedence rule: Tutorial, then Career, then Quick Race. Settlement requires the
  actually loaded track and exact four-car roster, maps blown cars to DNF, reloads the authoritative career save,
  and advances atomically once. Career races skip normal RP and driver-XP settlement.
- At the race-four summer break, the menu creates a mutable draft from the career-owned active technology snapshot
  for the locked team only. Unlocking spends only the captured career RP, active-node changes remain in the draft,
  cancel discards every change, and explicit confirmation atomically stores the sole summer snapshot before race five.
  The normal `TechTreeProfileStore` is never read or written by this editor.
- Race eight preserves the final standings, identifies the championship leader, and reports the player's score/rank.
  The completed overview offers a new-season path but still requires the existing explicit replacement confirmation.
  Every settlement writes one structured `[CAREER_RESULT]` saved or rejected line before `RACE_END`.
- The combined career rules, persistence, presentation, Race integration, summer-break editor and completion flow
  passed `46/46` focused source-level NUnit cases on 2026-08-31. Play Mode visual/runtime acceptance remains open.

## Turn and Card Loop

- Each player has a draw pile, hand, discard pile, and independent engine
  heat pool.
- The default hand limit is 7.
- The default speed deck contains twelve cards:
  `[1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4]`.
- Authored tutorial scenarios may initialize `CardDeck` with an explicit
  top-first sequence. That path never shuffles or consumes a random seed, and
  recycles playable discards in discard chronology. Normal races still use
  the randomized `InitializeDeck` path.
- `TutorialLaunchState` is session-only: it resolves Le Mans for a requested tutorial
  without changing `TrackSelectionState`. Returning to the main menu or starting a
  normal Quick Race clears the tutorial override.
- The tutorial runtime fixes the player to UK with six engine heat, exact opening/future
  draws, no tech state and no intrinsic team-vehicle handling/cooling/pace/slipstream
  bonuses. UK special cards remain because they are explicit teaching content.
- Each guided step owns a semantic focus target and a short standalone mechanism introduction.
- Tutorial copy always presents the next action. Current-state, success and recovery sections are optional;
  blank optional fields render no label, spacer or generic previous-step feedback.
- Tutorial presentation is data-bound through `TutorialOverlayAuthoring` on
  `Assets/Resources/Prefabs/UI/TutorialOverlay.prefab`. The Prefab exposes the sixteen lesson copies and
  practice/completion text without changing step IDs or action gates. Its guide panel and spotlight keep
  manually authored layout values; runtime first reuses a copy placed under `RaceCanvas`, then falls back
  to loading the Resources Prefab when no scene instance exists.
  `TutorialFocusHighlightUI` resolves live HUD, track, pit-choice and specific UK-card rectangles,
  dims only the surrounding area without intercepting clicks, and disables border pulsing under
  reduced-motion settings. During live input it prioritizes gear, card selection/confirmation,
  discard selection/confirmation, pit and lane gates over the lesson subject. Clicking removes the
  callout text and mesh while retaining the target border; highlighting never advances tutorial state.
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
- Tailwind close-ups and the immediately following bonus movement use a scoped
  `0.28` time scale. The close-up keeps realtime presentation timing, while the
  actual reward movement uses the scaled Unity clock so the slowdown is visible.
  Cleanup, disable, and destroy paths restore normal gameplay time (`1`), so
  base movement and later turns are not slowed.
- The draw-pile and discard-pile panels show stacked backs, up to three live
  card thumbnails, and a quantity badge. Draw previews follow the actual draw
  order; discard previews start from the most recently discarded card. Visible
  stack thickness grows by one layer per three cards up to seven layers, while
  the badge remains exact.
- Card ownership changes use a non-blocking table overlay: played/discarded
  cards fly from hand to discard, heat payments fly from engine to their rule
  destination, and cooling flies from the actual source zone back to engine.
  Optional discard resolves the exact card instances that actually moved before
  starting the animation, removes only those card views, and keeps unselected
  cards visible in the hand presentation.
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
- For China Go, the base requirement remains 3 speed cards on the first
  consecutive Go. When Hotpot's optional ATTACK slot is active, the effective
  turn limit is base 3 + 1 extra card, so the HUD/log may correctly show a
  4-card limit even when the consecutive-Go counter has just reset.

## Gear and Cooling Rules

- The selected gear controls how many speed cards may be played.
- A normal shift changes one gear.
- A two-gear shift costs heat.
- Gear 1 removes up to three heat cards through the cooling priority:
  hand, then draw pile, then discard pile.
- Gear 2 removes up to one heat card through the same priority.
- Higher gears provide no automatic cooling.
- The race HUD derives its heat percentage from permanent heat in hand, draw
  pile, and discard pile plus temporary heat. Permanent heat outside the engine
  and the engine remainder define capacity; temporary heat raises the displayed
  load without increasing that capacity. The thermometer changes from cool to
  elevated at 50% and critical/pulsing at 70%.
- Standard G1-G4 controls are arranged as a stove-dial arc; China reuses the
  same circular controls as a two-position Recover/Go selector. This is
  presentation-only and does not change gear legality or shift costs.
- Italy has no permanent straight movement bonus. Completing a corner arms a
  one-shot `+1` for the first speed card played on a later turn; an empty turn
  preserves it, and a failed/spun corner does not arm it.
- China AI projects every unique apex crossed by the lowest legal Go card set.
  It may accept at most one corner heat when the engine can also pay overclock
  and missing-card costs; larger or unaffordable risks force Recover. The
  tolerance is `GameConfigSO.aiChinaAffordableCornerHeat` (currently `1`).
- Standard AI, when heat is below the cautious threshold and no corner risk is
  predicted, considers an opponent within `GameConfigSO.aiSlipstreamPlanningRange`
  (default `2`) and searches the current gear's hand for an exact movement
  combination that ends one cell behind the opponent's estimated endpoint;
  otherwise it falls back to the normal high/low-card policy.

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

## Tutorial Mode Foundation

- `tutorial_le_mans_uk_v1` fixes the player to UK on
  `le_mans_old_mulsanne`, with tech-tree modifiers, driver skills, normal
  rewards and normal progression writes disabled by definition.
- Its pure state machine orders 16 guided topics from objectives/UI through
  UK special cards and review, rejects out-of-order actions without advancing,
  then enters a restartable one-lap practice phase.
- `TutorialRuntimeDirector` drains ordered state-machine events exactly once and
  exposes one-shot weather/opponent checkpoint cues. The Race adapter applies rain
  or cloudy weather through the existing session weather state.
- Every guided step carries a section label plus explicit goal, current scripted state, ordered
  player action, observable success signal and safe recovery hint. Real actions latch lesson completion
  without changing the visible step; Next becomes available after completion. Previous reviews reached
  lessons without rewinding race state or replaying checkpoint cues. Skip and Exit remain available.
- The guide panel uses a pure safe-area rule for common 16:9 resolutions. Its expanded form scales
  down from 540x440 and switches to compact typography at small sizes; collapse leaves only the
  title, section progress and expand control. Toggling presentation never advances tutorial state.
- The tutorial leader checkpoint is queued on entering the slipstream step and only
  positions the leader at cell 42/player at cell 40 after base movement, immediately
  before the unchanged end-of-turn slipstream resolver. This preserves the rule that
  only the rear car benefits.
- A runtime-built guide panel displays authored title/body/progress. Reading steps may advance immediately;
  operation steps require matching real race events for turn completion, exact card requirement, movement,
  heat, cooling, missing cards, spin, slipstream, pit timing and UK trick cards, then wait for explicit Next.
- Completing or skipping the guide rebuilds the Race session directly into `Practice`:
  lap and positions return to zero/start, the exact deck and six-heat pool are recreated,
  the teaching opponent is restored and scripted cloudy weather is applied. Practice uses
  a one-lap override without changing `GameConfigSO` or track JSON; completion ends the
  tutorial immediately and never enters RP/XP/progression settlement. Restart, guide replay
  and exit are available from the guide panel and emit tutorial log events.
- Eight risky guided steps now enter authored safe states at a gear-input gate or the next turn
  boundary. Exact normal/heat zones guarantee heat payment/cooling, a one-card G2 shortage,
  a zero-engine Dunlop spin after G2 cooling, and valid Scone/Tea targets; reset also clears spin,
  skip-turn and pit flags so the following lesson cannot inherit a soft lock.
- Official Le Mans has no pit nodes. Tutorial mode creates a separate 142-cell rule-only view with
  entry 132 and exit 4, then passes that view to unchanged `PitLaneRules`; official JSON/runtime
  track nodes remain unmodified. The full guided-to-practice Play Mode walkthrough remains open.

## Player Settings

- `Foodula1.Settings.V1` stores master, music and SFX volume values; fullscreen/window mode;
  resolution; animation speed; reduced motion; and a separate tutorial-completed preference.
- A persistent runtime `AudioService` loads menu/race music and named gameplay clips from
  `Resources/Audio`, crossfades on `MainMenu`/`Race` scene changes, and applies master, music and
  SFX settings immediately. Music, SFX and UI use separate AudioSources; UI currently follows SFX.
- Core button, card, gear, heat, movement, corner, tailwind, spin, lap and finish events are wired.
  Repeated movement/card/heat sounds use unscaled-time cooldowns, so tailwind slow motion does not
  change music pitch or leave later audio slowed. AudioMixer routing, independent UI volume/mute,
  peripheral P1 events and a full-match listening pass remain open.
- Display mode and resolution apply through an isolated runtime target. Animation speed scales
  race node pauses, camera lead/trail delays and button feedback; reduced motion skips those
  optional presentation durations without changing race rules, card values or movement results.
- Completing the tutorial only marks the tutorial preference. Resetting it changes the main-menu
  replay label and does not clear or write RP, tech-tree, driver XP, unlock or race-progress data.

## Game Encyclopedia

- The settings overlay opens a separate scrollable reader backed by
  `Resources/Configs/encyclopedia_zh.json`; UI code contains layout only, not rule prose.
- Catalog version 1 contains 17 stable topics covering the turn loop, all three card types, gears,
  card zones, heat/cooling, missing-card penalties, corners/spins, slipstream, weather, pits, teams,
  all special cards, drivers, the tech tree and HUD terminology.
- Startup validation rejects unsupported versions, duplicate/blank IDs, missing required topics and
  incomplete content. Tests also match the encyclopedia's related IDs against all 12 runtime trick
  definitions, all 12 drivers and all five active weather profiles.
- The driver entry explicitly states that configured signature skills are not yet applied in race
  resolution, while the audio-related settings remain labelled as data-only placeholders.

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
  snap immediately to the next-node direction, while normal movement performs
  a linear interpolation between adjacent nodes (`nodeMoveDuration=0.15s`)
  without a vertical offset and rotates toward the tangent according to
  `carRotateSpeed`; the stored gameplay position still snaps exactly to the
  destination node after each step.
- A spin-out presentation is visual-only: `RaceEventFX` defaults to a 1-second
  360-degree eased rotation, then restores the authored scale and track-facing
  rotation. Spin counters, heat recovery, rewind, skipped turns and DNF remain
  resolved synchronously by the gameplay rules before the cue is started.
- Each runtime car now gets a non-interactive world-space badge above its sprite,
  displaying the stable team code and current rank. The badge remains upright
  while the car rotates along the track and refreshes after position/ranking
  changes. It is a code/color fallback only; the GDD's authored flag icon and
  driver avatar are still pending visual assets.
- Manual race logs can be checked with the pure `RaceLogAnalyzer`: a valid turn
  ends card selection before base movement, ends base movement before the
  optional slipstream phase, and records active discard counts where
  `discarded` never exceeds `selected`. A log without `RACE_END` is reported as
  incomplete rather than being mistaken for a completed race.
- The file adapter and Unity editor menu can run the same checks against the
  latest or a selected saved `.log`; file-read failures are reported as
  analysis errors rather than interrupting gameplay.
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
- Slipstream resolves only after every racer completes base movement, reaction,
  and corner checks. It uses the actual settled positions, including vehicle,
  technology, and trick bonuses already applied during base movement. Only
  racers on the same lap are eligible. A
  same-cell tie compares base movement first and then base-movement arrival
  order, so only the car that arrived later can be the trailing follower;
  a same-cell pair can never generate reciprocal slipstream.
  successful first slipstream advances the simulated endpoint and may follow one
  different car for a second bonus; the same leader cannot be reused and the chain
  is capped at two triggers.
  After `[MOVE_PHASE] end`, every resolved chain segment is merged into one explicit
  0.95-second visual phase with a card-play handoff, blue moving dash trails,
  two-car camera focus pulses, total bonus text, and a 0.28 time-scale close-up.
  The phase waits for pending card flights, uses unscaled timing, and only after
  `[SLIPSTREAM_PHASE] end` applies the bonus movement. Each segment and the
  `[CARD_PHASE]`/`[MOVE_PHASE]`/`[SLIPSTREAM_PHASE]` boundaries is written to the
  manual log.
- The deterministic full-race test and track/team balance benchmark use the same
  boundary as runtime: all racers choose cards, base movement/reaction/corners
  execute in rank order, settled positions resolve tailwind, the visual boundary
  completes, and bonus movement is applied. The benchmark reports trigger count
  and movement gained per team.
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
