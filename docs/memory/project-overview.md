# Project Overview

Foodula1 is a Unity 2022.3.62f3c1 2D card-driven food truck racing game using
the CCGS project framework.

## Technology

- **Engine**: Unity 2022.3.62f3c1
- **Language**: C# 9.0
- **Rendering**: Built-in Render Pipeline, 2D
- **UI**: uGUI and TextMesh Pro
- **Input**: Legacy Input Manager, mouse interaction
- **Code**: `Assets/Scripts/`

## Current Runtime Structure

- `Core/MVPGameManager.cs` coordinates the current race loop.
- `Core/CardDeck.cs`, `CardData.cs`, and `PlayerState.cs` implement race state
  and card lifecycle.
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
- `Core/TeamGearRules.cs` is the team-aware gear facade; `Core/TeamVehicleRules.cs`
  owns tunable team vehicle profiles without leaking them into UI code.
- `Drivers/` contains the immutable driver catalog and progression rules;
  `Core/DriverSelectionState.cs` stores the current menu choice.
- `Config/GameConfigSO.cs` contains tunable race, deck, gear, animation, and
  AI parameters.
- `Tutorial/` contains the isolated Le Mans/UK scenario definition, explicit
  non-seeded deck order, pure guided-state machine, runtime Director, runtime-built
  guide panel and session-only launch state. Race events complete the active lesson;
  explicit guide-panel navigation advances or reviews authored steps.
- `Settings/` contains versioned player settings, an injectable PlayerPrefs adapter and
  runtime display/presentation application; `UI/GameSettingsUI.cs` builds the menu overlay.
- `Encyclopedia/` contains the versioned catalog loader/validator; the Chinese JSON source under
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
- The project is in Production stage. The latest recorded full Unity EditMode
  run passed 622/622 on 2026-09-07. This is retained evidence rather than a test
  rerun performed during the 2026-09-08 documentation synchronization.
- The main-menu tech-tree entry now persists per-team RP, unlocks, and active
  nodes; `RaceSession` centralizes numeric tech modifiers while the manager
  invokes explicit `TechTreeRules` event hooks at documented race phases.
- China uses an independent Go/Recover drivetrain (3-card Go, 1-card Recover,
  consecutive overclock heat and built-in Recover cooling) shared by player UI
  and AI through the same pure rules module.
- The driver-selection vertical slice is now implemented: 12 catalog entries,
  XP/tier rules, main-menu selection UI, and race initialization integration.
- `tutorial_le_mans_uk_v1` now has a playable menu-to-Race launch: UK, Le Mans,
  no tech/driver/team-vehicle/reward/progression benefits, a 16-card exact player
  draw order and a deterministic teaching opponent. Its runtime Director maps real
  turn/card/heat/corner/slipstream/pit/trick events to 16 ordered steps, applies
  scripted weather, and defers the 42/40 opponent checkpoint until normal end-of-turn
  slipstream resolution. Completing or skipping the guide now rebuilds a fresh one-lap
  practice session with the exact deck, six heat, teaching opponent, start positions and
  cloudy weather. Eight safe-boundary checkpoints now rebuild exact card/heat zones for the
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
- The latest full project EditMode run passes `622/622`; the tutorial and menu overlay slice passes
  `64/64` and retains its
  focused regression coverage.
  Play Mode verified the exact seven-card opening and zero-benefit session; a separate
  Monza Quick Race retained the randomized deck, tech state and vehicle bonuses. The
  earlier guide-panel smoke confirmed its first authored step. A 2026-08-28 visual smoke stayed in
  Unity's play-mode transition and was stopped without retrying; the later spotlight smoke did the same,
  so the revised progressive panel, spotlight boundaries and full guided flow still require manual Play Mode acceptance.
- Main-menu settings now persist master/music/SFX volumes, fullscreen/window mode,
  resolution, animation speed, reduced motion and the separate tutorial-completion flag.
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
  Play Mode evidence can be reviewed repeatably.
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
