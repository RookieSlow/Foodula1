# Project Overview

Foodula1 is a Unity 2022.3.62f3c1 2D card-driven food truck racing game using
the CCGS project framework.

## Technology

- **Engine**: Unity 2022.3.62f3c1
- **Language**: C# 9.0
- **Rendering**: Built-in Render Pipeline, 2D
- **UI**: uGUI and TextMesh Pro
- **Input**: Legacy Input Manager, mouse interaction plus keyboard race shortcuts
- **Code**: `Assets/Scripts/`

## Current Runtime Structure

- Twenty-three real outer-movement guard/fallback regressions now cover absent participants/tracks, zero nodes, unavailable car slots (including destroyed Unity objects), explicit/clamped movement and six-team human/AI participant isolation. Focused Unity EditMode `109/109`, full `1271/1271` passed on 2026-09-29 (zero failures/skips). The initial fixture field-name compilation error was corrected; an old-assembly 86-test run is not evidence for this increment. Runtime code is unchanged. Existing missing-car fallback wraps position only, skipping lap/finish/lane settlement; it is not a complete headless-race implementation. Animated outer completion/camera and Play Mode remain unverified. Next bounded package: real-manager ordinary reward mode-isolation regressions; TD-001/TD-002 remain open.
- Fourteen additional traversal regressions cover all six teams through repeated non-zero start/finish crossings, two resolved lane changes and the final lap, plus all four fixture crossing indices, rejected edge input and empty traversal gates. Focused Unity EditMode `86/86`, full `1248/1248` passed on 2026-09-28 (zero failures/skips). This increment changes test infrastructure only; manually advanced real iterators do not establish live scheduling, interpolation, camera or Play Mode acceptance. Next bounded work: outer movement guards and position ownership; TD-001/TD-002 remain open.
- `MVPGameManager.TraverseMovementNodes` now owns the small ordered node-visit boundary, while the outer movement adapter retains camera buffers, final position settlement and FX. It refreshes the Indianapolis lane after the crossing choice returns; inward/outward remaining targets previously used the stale initial lane (reproduced by failing target assertions). Ten added real-traversal regressions with a recording animator passed focused Unity EditMode `78/78`, full `1234/1234` on 2026-09-28 (zero failures/skips). No live scheduler, interpolation, camera or Play Mode acceptance is implied; this resolves the cached-target follow-up below, not TD-001/TD-002.
- `MVPGameManager.RegisterStartFinishCrossing` settles lap/finish state before returning whether movement should await the human Indianapolis lane choice, preserving the existing order and guards. Fourteen new six-team/exclusion/final-lap/repeated-crossing regressions passed focused Unity EditMode `75/75`, full `1224/1224` on 2026-09-28 (zero failures/skips). Tests manually compose the real registration and wait adapters; they do not execute the movement animation or full GameLoop. Remaining-node lane caching after a choice needs separate reproduction and regression coverage.
- `UI/LaneChoicePresentationRules` supplies button/confirmation direction labels and actual lane-delta completion logs. The inward/outward log reversal noted below was corrected on 2026-09-28 without changing lane selection rules. Twenty-two new copy/callback/synchronous-panel regressions passed focused Unity EditMode `53/53`, full `1210/1210` (zero failures/skips). Boundary buttons, keep selection, one-time logs and panel cleanup are covered on inactive fixtures; live click/visual and full GameLoop acceptance remain separate.
- `RaceLaneRules.GetAdjacentLane/TryChooseLane` shares the existing one-lane direction and acceptance contract between `TrackManager` and the Indianapolis human callback. Edge rejection retains the waiting gate; keeping a lane accepts and closes it. Twenty-six new regressions passed focused Unity EditMode `31/31`, full `1188/1188` on 2026-09-28 (zero failures/skips). Gameplay position/gear and other input gates remain unchanged; real car/UI/Play Mode acceptance is separate. The existing callback's inward/outward log labels are reversed and remain a separately scoped follow-up.
- `TutorialPitFeedbackIntegrationTests` adds twelve real-manager/Director regressions for pit reservation and delayed-stop feedback. Only expected, presented human events latch completion; AI, decline, pending presentation, wrong lesson and failed entry do not. Explicit Next separates reservation from exit, and the ordinary no-Director path still settles heat/position. Focused Unity EditMode `47/47`, full `1162/1162` passed on 2026-09-28 (zero failures/skips); no GameLoop, UI, persistence or Play Mode acceptance is implied.
- `PitLaneRules.TryGetApproachChoiceDistance` owns the read-only pre-entry offer gate; `RecordApproachChoice` centralizes the two reservation fields used by both human and AI adapters. The manager retains input, UI, logs, tutorial signals and the AI's 60% hand-heat threshold. Thirty new rules/real-adapter regressions passed focused Unity EditMode `72/72`, full `1150/1150` on 2026-09-28 (zero failures/skips); synchronous coroutine fallback/AI paths do not imply GameLoop or Play Mode acceptance.
- `RacePitTurnBoundaryTests` adds 21 synchronous integration regressions for the real entry/exit adapters, session resets and movement/slipstream exclusion. Six teams and human/AI participants retain delayed reservation, permanent heat conservation and next-turn gear selection; entry guards preserve reservations. Inactive temporary objects deliberately do not execute GameLoop, UI or persistence. Focused Unity EditMode `41/41`, full `1120/1120` passed on 2026-09-28 (zero failures/skips); ordinary-race Play Mode acceptance remains pending.
- `Core/RacePitStopExecution` owns ordered scheduled-stop state settlement with injected exit/heat/gear adapters. The manager retains configuration/technology resolution, movement, logs and tutorial signals; A1 skipped-turn ownership stays unchanged. Thirteen state and seven real-manager regressions cover six teams, failed entry, adapter exceptions, wrapped exit, actual CN Fast Charge and existing missing-config behavior. Imported-source Unity EditMode passed focused `47/47`, full `1099/1099` on 2026-09-28; no Play Mode acceptance is implied.
- `CardDeck.RecoverAllHeatWithSources` reports actual full-recovery removals through `HeatCoolingResult`; the legacy void API delegates, and the spin/pit presentation adapter no longer pre-scans zones. Temporary heat is destroyed without refund, permanent heat is conserved, receipts survive until turn reset, and missing-pool exception behavior is preserved. Eight deck and six real-adapter regressions passed focused Unity EditMode `24/24` including existing spin coverage, full `1079/1079` on 2026-09-28. Scheduled-pit success/failure behavior is covered without scene startup; Play Mode effects remain unverified.
- `Tutorial/TutorialHeatFeedbackRules` translates successful human payment/cooling results into ordered signals with unchanged detail strings. The manager retains operation guards, audio and live Director/pending-presentation gates. CN recovery eligibility is re-read after the generic callback; rejected signals do not suppress later signals. Thirteen signal and nine real-manager/Director regressions passed focused Unity EditMode `22/22`, full `1065/1065` on 2026-09-28; completion remains latched until explicit navigation. Play Mode UI/animation acceptance remains separate.
- `Core/RaceSpinRules` computes weather-adjusted unclamped spin counters, effective-cap retirement and CN Recover/standard minimum gear. The coordinator retains heat recovery, rewind, skip, movement/FX and tutorial feedback in their existing order; Final Sprint remains separate. Thirteen pure and ten real-adapter regressions passed focused Unity EditMode `23/23`, full `1043/1043` on 2026-09-28. Partial-payment heat conservation/receipt reset, actual IT tech cap and already-retired no-op are covered; Play Mode presentation acceptance remains pending.
- `Core/HeatPaymentCostRules` owns one-shot cost resolution in multiplier → passive discount → Schwarzbrot order, consuming discounts rather than acting as a read-only query. The coordinator retains actual payment, receipts, shortage rescue/spin and presentation; result metadata preserves DE tutorial gating. Eleven cost and four real-payment regressions passed focused Unity EditMode `38/38`, full `1020/1020` on 2026-09-28. Play Mode shortage animation and tutorial feedback acceptance remain pending.
- `Core/HeatCoolingResult` carries actual card-zone removals from both generic and recorded-payment cooling in `CardDeck`. Legacy int APIs still return totals; the coordinator consumes actual source counts for presentation instead of pre-scanning piles or receipts. Eleven regressions passed focused Unity EditMode `39/39`, full `1005/1005` on 2026-09-28; cooling priority, temporary destruction and permanent-heat conservation are unchanged. Play Mode animation acceptance remains pending.
- `Core/RaceTurnTechnologyCleanup` owns sequential technology settlement with injected movement/cooling/log adapters. Each effect reads current state after the previous callback; exceptions stop later work. Grill eligibility/cooldown conflict was fixed on 2026-09-28: per-turn actual payment receipts select only remaining permanent paid instances, which pass through discard back to the engine. Old heat and already-cooled receipts are untouched; the per-race once flag survives turn resets. Nine added regressions and three updated assertions passed focused EditMode `123/123`, full `994/994`. Play Mode acceptance remains pending.
- `Drivers/DriverSkillRules` owns Final Sprint turn-end eligibility and pure spin-cap/tier cost calculation; the coordinator retains effective-cap injection, mutations and logging without clearing existing terminal/recovery flags. Nine rule and six real-cleanup regressions were added; focused Unity EditMode `26/26`, full `975/975` passed on 2026-09-28. Play Mode acceptance remains pending.
- `RaceTurnBoundaryIntegrationTests` exercises the real coordinator cleanup adapter on an inactive temporary object, followed by session turn reset and deck refill/recycling. Eleven new cross-module regressions passed within Unity EditMode `960/960` on 2026-09-28; scene startup, persistence and Play Mode are deliberately outside this fixture.
- `Core/RaceTurnCleanup` owns ordered turn-end card cleanup with injected skill, temporary-card reporting and technology adapters. The manager retains Unity presentation and effect execution. Terminal status is read after skill cleanup; played references clear last. Nine new regressions passed within full Unity EditMode `949/949` on 2026-09-28; Play Mode acceptance is still pending.
- `Core/RaceTurnRules.GetStartAction` selects the A1 participant route without consuming flags: terminal exclusion, scheduled pit stop, turn skip, then gear choice. The coordinator retains all execution and tutorial waits. Added 35 regressions; Unity EditMode `940/940` passed on 2026-09-28, without Play Mode acceptance.
- `Core/RaceStateLogFormatter.cs` owns read-only `[STATE]` and `[CARDS]` trace formatting; `MVPGameManager` retains recording timing, guards and file output. Seven exact-output/non-mutation regressions passed within Unity EditMode `905/905` on 2026-09-28; this is not Play Mode acceptance evidence.
- Career persistence has explicit source-file boundaries: CareerPersistence.cs owns DTO/schema conversion, CareerRepository.cs owns the career-key storage adapter, and CareerModeService.cs coordinates validated state changes with persistence. Storage exceptions during load produce an invalid-load result; existence checks fail closed, while save/abandon failures do not advance or clear live career state.
- Technology effect-to-modifier mapping, including upgrade-max and additive aggregation policies, is owned by `TechTreeRules`; `RaceSession` composes UK Sun Never Sets target effects through that shared rule entry point.
- `Core/MVPGameManager.cs` coordinates the current race loop.
- Thunderstorm pre-turn display-grid eligibility and slot calculation are owned by `TrackPresentationRules.TryGetThunderstormGridSlot`; the manager supplies roster/track facts and positions scene objects. Gameplay positions remain untouched. Twelve new regressions passed within full Unity EditMode `898/898` on 2026-09-28; visual Play Mode acceptance remains open.
- Current-turn mandatory/optional speed-card counts are read by `CardPlayRules.GetSpeedCardRequirement`; `GearRequirementFeedbackRules` owns the existing gear/extra-slot label. Manager compatibility methods delegate without changing UI flow or missing-card penalties. Added 13 regressions; full Unity EditMode `886/886` passed on 2026-09-28.
- `Drivers/DriverSkillRaceContextRules.cs` reads lap, engine heat and same-lap nearby-behind facts without scene references; `UI/DriverSkillPresentationRules.cs` formats the skill button through existing activation rules. The manager retains input/scene coordination and skill side effects. Added 14 regressions; Unity EditMode `873/873` passed on 2026-09-28; normal-race Play Mode acceptance remains open.
- `Core/RacePhaseState.cs` owns the pure race-phase/input-acceptance contract; the manager still owns coroutine side effects and UI orchestration. Its regression matrix covers each phase against closed/gear/card input gates, null input, stale gates, and the gear-to-cards-to-animation-to-game-over sequence.
- `Core/RaceInputState.cs` represents the mutually exclusive gear/card/discard/lane/pit input gates with one active state while retaining the existing public gate queries. Stale end callbacks leave a newer gate open; phase and input tests passed `30/30` within full EditMode `856/856`.
- `Core/CardDeck.cs`, `CardData.cs`, and `PlayerState.cs` implement race state
  and card lifecycle.
- `Core/RaceLapWeatherRules` owns lap-transition and once-per-lap weather decisions;
  `Core/RaceWeatherState` commits the runtime lap gate without synthesizing a lap transition.
- `Core/RaceParticipantPlanBuilder` resolves the ordered tutorial/career/free-race
  roster before scene objects are created; `Core/NormalRaceRewardSettlement`
  isolates ordinary-race RP/XP settlement behind injected persistence callbacks.
- `Gameplay/TrackManager.cs` supports the eight selectable JSON tracks and the
  `fallback_42` 42-node fallback track.
- `Gameplay/TrackDataLoader.cs` converts track JSON into runtime nodes and
  world positions.
- `AI/AIController.cs` controls the current opponent and uses `AIPlanner` for
  heat/corner-aware card selection plus low-risk active slipstream planning.
- `AI/AIController.cs` delegates team-specific China gear decisions to the
  pure `ChinaGearShiftRules` module.
- `TechTree/` contains the pure tech database/rules plus the
  `TechTreeProfileStore` persistence adapter.
- `UI/` contains the card hand, HUD, card, main-menu, driver-selection, and
  runtime tech-tree views.
- `UI/CardHandInteractionRules` and `UI/RaceResultPresentationRules` hold pure
  card-input/result-copy rules outside their MonoBehaviour view adapters;
  `RaceUILayoutController` remains the race HUD layout boundary.
- `Core/TeamGearRules.cs` is the team-aware gear facade; `Core/TeamVehicleRules.cs`
  owns tunable team vehicle profiles without leaking them into UI code.
- `Drivers/` contains the immutable catalog, progression and active-skill rules/runtime plus the
  per-driver XP persistence adapter; `Core/DriverSelectionState.cs` stores the current menu choice.
- `Config/GameConfigSO.cs` contains tunable race, deck, gear, animation, and
  AI parameters.
- `Diagnostics/` contains the persistent, local-only playtest operation recorder,
  privacy sanitization, rolling session summaries and tester-controlled ZIP export.
- `Tutorial/` contains the isolated Le Mans/UK scenario definition, explicit
  non-seeded deck order, pure guided-state machine, runtime Director, runtime-built
  guide panel and session-only launch state. Race events complete the active lesson;
  explicit guide-panel navigation advances or reviews authored steps.
  The tutorial center also launches CN/Shanghai and US/Indianapolis specialty
  scenarios with exact decks, team-specific checkpoints and a one-lap practice.
  DE/Nürburgring has a deterministic straight-card lesson and resolved trick checks;
  IT/Monza adds a corner checkpoint, stationary teaching leader for Parmigiano
  slipstream and a heat-in-hand Chianti checkpoint. UK/Silverstone now teaches
  actual Scone heat-for-movement and Tea hand-to-engine cooling with isolated
  exact-card checkpoints. JP/Suzuka now stages a G2 Kanto Oden skip with a
  two-slot carry into G1, then an authored stationary leader for the real
  Torpedo overtake bonus; its unimplemented reverse bonus is not taught.
  DE and US menu lesson rows now mirror their actual scripted checkpoints:
  no menu-only durability lesson, separate Fries/Cola steps, exact gear/card
  actions and an explicit one-lap practice row.
  CN and IT course rows likewise mirror their authored checkpoints, omit the
  unscripted CN pit lesson, and list each actual mechanic plus practice.
  Each menu lesson now owns explicit `TutorialStepId` references; specialty
  course launchability is derived from exact one-to-one coverage of its scripted
  steps (except the objective/briefing page), so future script/menu drift disables
  launch instead of silently hiding a lesson. The user confirmed Play Mode
  acceptance for all six team-specialty courses on 2026-09-27; detailed per-course
  environment and Console evidence was not supplied. Generic foundation-course
  and 16:9 tutorial-center acceptance remain separate.
- `Settings/` contains versioned player settings, an injectable PlayerPrefs adapter and
  runtime display/presentation application; `UI/GameSettingsUI.cs` builds the menu overlay,
  including schema-v2 per-action in-race confirmation preferences.
- `Encyclopedia/` contains the versioned catalog loader/validator; the 19-entry Chinese JSON source under
  `Resources/Configs/` drives `UI/GameEncyclopediaUI.cs` without embedding rule prose in UI code.
- `Career/` contains the pure eight-race season calendar, four-car points and stable standings,
  locked-team state, idempotent result advancement, the race-four summer-break technology gate,
  versioned save DTO/validation, isolated PlayerPrefs adapters, an atomic application service and
  the session-only Race launch/result settlement boundary.

## Scenes

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/Race.unity`

The verified scene flow is:

`MainMenu -> Race -> race result -> MainMenu`

## Current Development State

- Career mode foundation is in progress. `CareerModeRules` reuses the explicit eight-track menu
  catalog as its only calendar source, accepts any of the six teams and locks the confirmed team,
  applies centralized 10/6/4/2 points with DNF zero, opens one technology adjustment after race four,
  and completes after race eight. `CareerSaveCodec` now rebuilds saves through the rules layer,
  validates schema/calendar/standings, preserves initial and summer-break tech snapshots, and returns
  a safe empty state for malformed data. `CareerModeService` requires confirmation before replacement
  or abandonment and does not advance runtime state when persistence fails. `CareerModeUI` separates the
  Free Race path from six-team career creation, confirmed replacement/abandonment, calendar and standings.
  Race now consumes an immutable request for the scheduled track, locked team, stable field and cloned tech
  snapshot, then atomically records mapped finishes/DNF against the reloaded authoritative save without normal
  RP/driver-XP writes. The race-four summer break now opens an isolated locked-team technology draft: cancel
  discards it, while explicit confirmation atomically saves the sole mid-season snapshot and enables race five
  without touching `TechTreeProfileStore`. Race eight now reports the champion plus the player's final score/rank,
  the completed overview starts a replacement-confirmed new-season flow, and structured `[CAREER_RESULT]` lines
  capture saved/rejected settlement evidence. Focused career execution passed `46/46`; Play Mode acceptance remains open.
- Demo framework Phase 1 is complete.
- Core race presentation is Demo-grade. The formal main-menu background, Foodula1
  logo and six team emblems are integrated; driver portraits and formal tech-tree
  artwork remain in the next visual asset package.
- The project is in Production stage. The V0.1.1 source baseline full Unity EditMode run passed
  `688/688` on 2026-09-18 after adding external-playtest operation logging, the China
  long-move corner look-ahead regression, Hotpot ATTACK correction, keyboard controls and
  the fixed scrolling race log window; the Thunderstorm
  12-driver mode is now wired and
  awaits runtime visual acceptance.
- The main-menu tech-tree entry now persists per-team RP, unlocks, and active
  nodes; `TechTreeRules` centralizes numeric modifier aggregation while the
  manager invokes explicit rule hooks at documented race phases.
- China uses an independent Go/Recover drivetrain (3-card Go, 1-card Recover,
  consecutive overclock heat and built-in Recover cooling) shared by player UI
  and AI through the same pure rules module.
- The driver-selection and skill slice is implemented: 12 catalog entries, XP/tier rules,
  per-driver normal-race XP persistence, main-menu selection, a race HUD action, all 12 active
  effect hooks and five practical passive effects. Passive Play Mode sign-off remains, and the
  remaining seven passive descriptions are still configuration-only.
- `tutorial_le_mans_uk_v1` now has a playable menu-to-Race launch: UK, Le Mans,
  no tech/driver/team-vehicle/reward/progression benefits, a 16-card exact player
  draw order and a deterministic teaching opponent. Its runtime Director maps real
  turn/card/heat/corner/slipstream/pit/trick events to 16 ordered steps, applies
  scripted weather, and stages the 42/40 opponent checkpoint at a fresh turn boundary;
  the teaching leader stays still for that one turn before normal slipstream resolution.
  Completing or skipping the guide now rebuilds a fresh one-lap
  practice session with the exact deck, six heat, teaching opponent, start positions and
  cloudy weather. Nine safe-boundary checkpoints now rebuild exact card/heat zones for the
  risky guided mechanics. Because official Le Mans has no pit, a tutorial-only 132/4 rule view
  reuses normal pit rules without mutating official nodes. Each lesson now presents an explicit
  mechanism purpose, current scripted state, one next action, success signal and recovery hint in a
  progressive, encouraging voice. Current-state, success and recovery sections are optional and disappear
  completely when blank; the following lesson only retains an explicitly authored success result. Every
  lesson also authors one of 13 semantic focus targets and a standalone mechanism introduction.
  A non-interactive spotlight dims outside the live HUD/card/track region, uses a static border when
  reduced motion is enabled, and follows rebuilt pit/card objects. The panel resolves a safe-area
  layout for common 16:9 sizes and can collapse without changing tutorial state. Presentation is now
  authored in `Assets/Resources/Prefabs/UI/TutorialOverlay.prefab`: its root exposes all sixteen lesson
  copies, while the panel and spotlight retain manually edited RectTransforms. Runtime reuses an instance
  under `RaceCanvas` before loading the Resources fallback. Final guided Play Mode remains open.
  The guide now keeps a completed lesson visible until explicit Next, supports non-destructive Previous review,
  and preserves Skip/Exit. Its spotlight follows every live player input gate; clicking clears the callout mesh
  completely while leaving the operation border visible. Main-menu overlays are mutually exclusive so the career
  entry and summer-break surface cannot leak over driver selection.
- The latest full project EditMode run passes `680/680`; the tutorial and menu overlay slice passes
  `64/64` and retains its
  focused regression coverage.
  Play Mode verified the exact seven-card opening and zero-benefit session; a separate
  Monza Quick Race retained the randomized deck, tech state and vehicle bonuses. The
  earlier guide-panel smoke confirmed its first authored step. A 2026-08-28 visual smoke stayed in
  Unity's play-mode transition and was stopped without retrying; the later spotlight smoke did the same,
  so the revised progressive panel, spotlight boundaries and full guided flow still require manual Play Mode acceptance.
- Main-menu settings now persist master/music/SFX volumes, fullscreen/window mode,
  resolution, animation speed, reduced motion, the separate tutorial-completion flag and
  schema-v2 per-action in-race confirmation gates. The race HUD reuses the live corner formula
  for clickable limit numbers and a read-only signed modifier breakdown.
  Display and supported presentation timings are applied at runtime. A persistent `AudioService`
  now loads menu/race music and the core GDC 2026-derived SFX pack from Resources, crossfades
  scene music, separates Music/SFX/UI sources and throttles repeated board events. An AudioMixer
  asset and independent UI/mute controls remain open. Audio rules passed `9/9`; the full EditMode
  suite passed `603/603` on 2026-09-03.
- Card number and heat icons, Chinese UI, Chinese font support, team car
  sprites, the main-menu flow, and the selected-card scale/lift/shadow feedback
  have been implemented. Draw/discard pile previews now show stacked backs,
  live card thumbnails, and count badges.
- The final four-zone race HUD is baked into `RaceCanvas.prefab` for WYSIWYG
  authoring. Runtime preserves authored RectTransforms and only rebuilds the
  default layout for legacy canvases missing the required panels. The authored
  HUD now includes arc-arranged stove-dial gear controls and a ten-segment
  vertical heat thermometer with 50%/70% warning thresholds.
- `Gameplay/RaceEventFX.cs` now scopes tailwind slow motion to the active
  close-up and its immediately following bonus movement, while overtake keeps
  its own close-up scope. Both restore normal gameplay time when the effect
  finishes or is interrupted; the regression suite passed `471/471` EditMode
  tests after this fix.
- The custom Track Node Editor experiment was reverted after Scene View
  interaction problems.
- The Race scene defaults to the 60-node Silverstone JSON track. Runtime apex
  traversal, start/finish lookup, arbitrary-node HUD display, loaded
  LineRenderer coordinates, pit exit movement, and track weather are
  integrated. The current selectable catalog contains eight official JSON
  tracks; `fallback_42` is retained only as a fallback. Full multi-weather,
  multi-lap, and pit-flow Play Mode walkthroughs remain open.
- Runtime track readability now adds a technology-blue player halo, 1-based
  local cell badges and level-colored smoothed corner ribbons without changing
  authored JSON nodes or gameplay positions. All eight selectable official
  tracks passed a clean Play Mode screenshot/Console spot check on 2026-08-25;
  only the full mechanics-focused manual race walkthrough remains open.
  Le Mans old Mulsanne now includes the JSON-authored cell-60 `mulsanne_kink` high-speed apex
  (limit 6); current circular straight runs are 44/37/21/16.
- When the active roster contains a US racer, `TrackReadabilityOverlay` adds
  US-emblem landmark signs beside the runtime start/finish and midpoint nodes.
  Placement is derived from the selected track's node positions, normals and
  road width, using the shared gameplay landmark rule; without a US racer the
  signs remain hidden. EditMode covers all eight selectable maps plus fallback;
  the new signs still need a visual Play Mode check.
- Vehicle movement presentation now treats each node as a discrete 0.15-second
  linear interpolation along the track plane, without a vertical hop; race
  positions, lap crossings and corner calculations remain unchanged.
- Runtime cars now receive a scene-independent world-space team badge showing a
  stable team code and current rank. It stays upright while the car follows
  track tangents and uses a high-contrast team-color fill. This is a temporary
  readability fallback until authored flag and driver-avatar art is available;
  the car prefab and Race scene remain untouched.
- Race-event presentation keeps spin-out behavior outside the rules layer; its
  default cue is now a configurable 1-second, 360-degree rotation with pure
  timing/easing coverage, while the blow-up and normal spin state transitions
  remain owned by `MVPGameManager`.
- `Core/RaceLogAnalyzer.cs` provides pure checks for turn phase ordering,
  active-discard count integrity, and complete-vs-partial manual logs so new
  Play Mode evidence can be reviewed repeatably. Malformed or overflowing
  discard counters return validation errors without throwing during analysis.
- `Core/RaceLogFileAnalyzer.cs` and the `Foodula1 > Tools > Analyze Latest Race Log`
  editor entry load saved manual logs without changing race state, making the
  phase/order checks repeatable against real playtest files.
- The four Scheme A refactor sources were committed in `58d1bba`.
  `RaceRules.cs` and `AIPlanner.cs` are integrated into the runtime.
  `AIController` and `CardDeck` accept injectable `IRandomSource`
  implementations for deterministic tests. The reviewed refactor currently
  passes its historical original 16 EditMode tests with no Unity warnings or errors;
  the current full suite is tracked separately below.
- `Gameplay/TrackRules.cs` now provides pure, tested track traversal rules.
  Together with the feature tests available at that stage, this track-rules slice
  historically passed 471 EditMode tests. `RaceTestLogWriter` can capture a manual race into a timestamped log
  for later review; a controlled four-car Silverstone Play Mode smoke on
  2026-08-25 produced two `[SLIPSTREAM]` entries and confirmed `RaceEventFX`
  was present, while full manual chain/visual acceptance remains open. The
  2026-08-26 tailwind/HUD regression passed 56 focused tests plus the full
  446-test suite, followed by a 5-second MainMenu Play Mode smoke with no project
  errors or warnings. The subsequent AI tailwind rerun passed 15/15 focused
  tests and 451/451 full EditMode tests, including the new boundary cases.
  The 2026-08-27 team-badge change passed 18/18 focused and 469/469 full
  EditMode tests; the only Console error was an existing LogAssert-expected
  missing-track test message, with no unhandled compilation error.
- The deterministic full-race test and `TrackTeamBalanceBenchmark` now freeze
  every racer's non-slipstream plan before resolving the same two-step chain as
  runtime. The corrected 2026-08-25 benchmark covers nine track configurations,
  uses real heat-card payments, records individual finish turns, and rotates team
  insertion order. US straight-profile stacking is fixed and China's former 92%
  Le Mans DNF was confirmed as a benchmark artifact. Italy's corner-exit bonus
  now resolves on the following turn and its undocumented permanent straight
  `+1` is removed. China corner safety now judges the lowest legal Go hand,
  sums unique projected apex costs, and accepts one heat only when every
  committed cost is payable. The remaining China/UK/Japan spread is tracked in
  the balance-check report.

## Memory Provenance

The original Claude Code memory under `.claude/agent-memory/` described an
earlier prototype with an 85-node track and three scripts. Those values are
historical and must not override the current implementation. When facts
conflict, verify the current code, Unity project state, and
`docs/memory/current-task-list.md`.

See [game-mechanics.md](game-mechanics.md) for current gameplay rules and
[current-task-list.md](current-task-list.md) for active work.
