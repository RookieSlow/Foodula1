# Current Task List

> Updated: 2026-08-15
> Sources: Claude Code project memory, active session state, session history,
> current Git worktree, and current Unity project structure.

## P0 - Resume Approved Scheme A Refactor

- [x] Review all current C# files and reconcile the earlier whole-project
  review with the latest project state.
- [x] Integrate `Assets/Scripts/Core/RaceRules.cs` into
  `MVPGameManager.cs` so shared gear, cooling, movement, and selection rules
  have one source of truth.
- [x] Integrate `Assets/Scripts/AI/AIPlanner.cs` into `AIController.cs`.
- [x] Inject `IRandomSource` where deterministic gameplay or AI behavior is
  required.
- [x] Add EditMode tests for `RaceRules` (9 cases passing in Unity).
- [x] Add EditMode tests for `AIPlanner`, deterministic random behavior,
  and the AI spin-out card-conservation regression (7 cases passing in Unity).
- [x] Validate every changed script, wait for Unity compilation, and confirm
  that the Unity console has no errors (16 EditMode tests, 0 warnings/errors).
- [x] Review the final diff for the approved refactor changes.
- [ ] Commit only with explicit user instruction; scheduled-task authorization
  does not include Git commits.

The four Scheme A source files and their `.meta` files were committed in
`58d1bba`. `RaceRules` and `AIPlanner` are now integrated into the runtime.
`AIController` and `CardDeck` accept injectable random sources, and the
refactor currently has 16 passing EditMode tests. The final review also fixed an
AI spin-out path that could remove selected speed cards without discarding them,
and standardized `IRandomSource.NextDouble` to the [0, 1) contract.
Manager-level seeded replay and broader integration coverage remain useful
follow-ups, but are not blocking the current Demo path.

The first data-driven track completion slice is also verified. `TrackNode` now
preserves JSON apex metadata, pure `TrackRules` owns wrapping traversal and
start/finish lookup, and `MVPGameManager` initializes and counts laps from the
runtime track rather than the legacy config index. Silverstone loads in the
Race scene with 60 nodes and 3 laps. Unity currently passes 20 EditMode tests
with 0 failures, warnings, or errors.

## P0 - Protect and Reconcile the Current Worktree

- [ ] Inspect the existing modifications to `MainMenu.unity` and
  `Race.unity`; preserve legitimate user scene edits.
- [ ] Verify the untracked `Assets/Data/Tracks/NewTrackData.asset` before
  deciding whether it belongs to the track-system work.
- [ ] Keep the Unity MCP package changes in `Packages/manifest.json` and
  `Packages/packages-lock.json` logically separate from gameplay refactoring.
- [ ] Avoid bundling unrelated scene, track-data, MCP installation, and
  refactor changes into one commit.

## P1 - Track System Decision and Completion

- [ ] Choose a reliable authoring workflow: GameObject child nodes, a simpler
  Editor script, or another explicitly approved approach.
- [ ] Decide whether AI-generated `track_layout_*.png` images are authoring
  references, runtime backgrounds, or both.
- [x] Configure `GameConfigSO.trackId` and verify JSON track loading in the
  Race scene (Silverstone, 60 nodes, 3 laps).
- [x] Validate arbitrary node counts throughout movement and UI; remove
  remaining hard-coded `42` display assumptions.
- [x] Preserve JSON `isApex` metadata and validate apex-only, deduplicated
  corner crossing across the lap boundary.
- [ ] Playtest speed-limit heat penalties through the full Race interaction.
- [x] Drive start/finish lookup and crossing from runtime track-node data;
  validate wrapping and a non-zero start/finish index in EditMode tests.
- [ ] Complete a multi-lap manual playthrough to validate finish timing.
- [ ] Implement or verify pit entry and pit exit behavior.
- [x] Drive LineRenderer positions from loaded track coordinates and verify
  the Silverstone path in Play Mode.
- [x] Replace the inaccurate Nürburgring 24H combined bonus layout with a
  219-node standalone Nordschleife sampled from the referenced real layout;
  verify zero self-intersections and regenerate its guide/background.
- [x] Standardize node colors across all tracks: apex red, other corner
  nodes orange, straights white, and start/finish green; enforce exactly one
  apex per corner group across every track config.
- [x] Add presentation-only lane slots to every track: two lanes for standard
  tracks and four lanes for Indianapolis; keep gameplay, camera, and minimap
  positions centerline-based, and regenerate backgrounds with matching lanes.
- [x] Add Indianapolis lane-specific corner limits (inner-to-outer 4/5/6/7,
  with outer-lane limit 7)
  and a player one-lane inward/outward choice at each start/finish crossing.
- [x] Replace runtime grid-like track visuals with selected layout backgrounds,
  yellow corner masks, red apex masks, visible speed-limit labels, and
  editor-only node metadata.
- [x] Make ordinary tracks use the inside lane by default, move only the
  trailing car outside when cars share a node, and keep Indianapolis vehicle
  placement tied to the player's explicit lane choice.
- [x] Rotate vehicles to follow the tangent between track nodes (default sprite offset corrected to 0° for right-facing car art).
- [ ] Integrate track weather-pool selection after the core track path is
  stable.

## P2 - Demo Asset Replacement

- [ ] Reconcile the Phase 2 planning document with assets already completed.
- [ ] Finish remaining UI panel artwork.
- [ ] Replace gear-button placeholders with the approved gear controls.
- [ ] Replace the heat text placeholder with the approved thermometer UI.
- [ ] Replace remaining flag and track-node placeholders.
- [ ] Verify card and vehicle sprites in both scenes at target resolution.

## P3 - Feature Completion

- [ ] Add multiple AI opponents.
- [ ] Implement slipstream.
- [ ] Implement team attributes.
- [x] Shuffle team trick cards into the normal deck lifecycle and replace batch
  hand submission with one-card select/confirm play, immediate trick resolution,
  and explicit end-of-card-phase behavior.
- [x] Implement the driver-selection flow as a catalog, session state, and
  runtime-built main-menu panel; connect the selected driver to race setup.
- [ ] Add sound effects.
- [ ] Add card-play, vehicle movement, bounce, and spin-out animations.
- [ ] Add weather gameplay after track data and race rules are stable.

## Open Decisions

- [ ] Select the replacement for the reverted Track Node Editor.
- [ ] Confirm how the Le Mans test track relates to the six national teams.
- [ ] Confirm whether Kanto Oden carry-over slots are mandatory (the current
  runtime behavior) or optional; Hotpot's additional slot is already optional.
- [ ] Decide the commit boundaries for current scene, data, MCP, and
  refactor changes.

## Completed Context

- [x] Main-menu and Race scene flow.
- [x] Chinese UI and Chinese font integration.
- [x] Card number and heat icon display.
- [x] Six national-team vehicle sprites.
- [x] Eight track-layout image prompts.
- [x] Unity MCP 10.1.0 package installed and connection verified.

## Maintenance Rule

Update this file whenever a task is completed, superseded, or blocked.
Historical Claude Code files under `.claude/agent-memory/` and
`production/session-logs/` remain provenance only; this task list is the
maintained source for current work.

## 2026-08-15 Tech Tree and Team Gear Audit

- [x] Audited the existing tech-tree rules/database against the design docs;
  common, unique and China EV node pools are now selected through one database API.
- [x] Added the main-menu tech-tree entry and runtime-built configuration UI;
  RP, permanent unlocks and active race selections persist per team.
- [x] Replaced the race's demo-only human tech state with the saved profile;
  AI opponents retain transient demo profiles so a race cannot mutate campaign RP.
- [x] Added the China Go/Recover pure gear module and a team-aware facade;
  player controls, AI selection, card limits, overclock heat and Recover cooling
  all use the same rules.
- [x] Added `TeamVehicleRules` as the boundary for team profile values and base
  durability/heat-pool setup; full movement/handling balancing remains a follow-up.
- [x] Unity EditMode validation after this slice: 307/307 passed; dotnet builds
  complete with 0 errors.
- [ ] Continue extracting orchestration from `MVPGameManager` into phase services
  once the next feature requires changes across multiple phases.

## 2026-08-05 Module Audit

- [x] Audited the modules introduced by the previous AI integration commit.
- [x] Fixed temporary-heat card injection and AI effective card-slot handling.
- [x] Connected AI corner risk to lane-specific limits and active weather/tech modifiers.
- [x] Accepted the existing fixed-default weather tracks in the JSON validator.
- [x] Run Unity EditMode/Play Mode tests through the open Unity instance or MCP (EditMode 262 passed; no PlayMode tests configured).
- [x] Corrected Indianapolis lane winding so lane 0 is inside and limits rise from 4 (inside) to 7 (outside).
- [x] Completed a full static + runtime audit: corrected stale stage metadata,
  restored Race speed-card/heat icon references, and removed unsupported emoji
  glyphs from runtime UI labels.
- [x] Added the driver data slice from `foodula-1-drivers.md`: 12 profiles,
  XP thresholds, tier unlocks, UK active-use bonus, XP reward calculation,
  selection state, menu panel, and 5 EditMode regression tests.
- [x] Re-ran Unity EditMode tests after the audit and driver slice: 273/273
  passed with no failures or skips; dotnet build has 0 errors.
- [x] Fixed the card-play lifecycle regression: opening tricks are randomly
  drawn, confirmed tricks enter discard immediately, confirmed speed cards stay
  in the played area until cleanup, and optional discard accepts any non-heat
  card. Unity EditMode tests now pass 287/287; runtime smoke verified the
  seven-card opening hand, button states, hand/UI synchronization, and trick
  transfer to discard.
- [x] Completed the follow-up runtime audit and repaired cross-system card/heat
  ownership: exact runtime card instances are consumed atomically, temporary
  heat can no longer inflate the permanent engine pool, and AI heat payments
  use the same canonical path as human payments.
- [x] Fixed turn-start and movement edge cases: Kanto Oden carry-over is
  consumed even when the tech tree is disabled, Hotpot grants movement only
  when its optional ATTACK slot is actually used, and teleports immediately
  restore the car's track-tangent facing.
- [x] Hardened card UI state: reset clears every interaction mode, heat cards
  are non-interactable, resource displays refresh after card/heat changes, and
  a short action-button debounce prevents a physical double-click from both
  confirming a card and ending the phase. Gear controls are now interactable
  only while the human player is actively choosing a gear.
- [x] Replaced the placeholder race simulation assertions with an actual
  draw/pay/commit/move/cleanup/reshuffle loop and added exact-ownership,
  temporary-heat, Kanto, Hotpot, shared AI heat-payment, and orientation tests.
  Final verification: Unity EditMode 301/301 passed; runtime smoke covered
  main menu -> track selection -> Race, gear/card/discard/reset interaction,
  disabled heat-card input, and post-teleport orientation with a clean console.
  `dotnet build Foodular1.sln --no-restore` reports 0 errors (two existing MCP
  assembly-version warnings remain).

## 本次完成（2026-08-15 赛事回归与平衡）

- [x] 比赛 HUD 增加“返回主菜单”按钮；修复 RaceCanvas 预制体重建后的旧引用导致重复 HUD 的问题。
- [x] 所有主要菜单/比赛控制按钮统一接入短按压/释放缩放动画；运行态确认按钮存在且可触发场景切换。
- [x] 新增 `TrackTeamBalanceBenchmark`，覆盖 Resources 中全部赛道与六支车队，每图 12 场确定性比赛，报告写入 `design/balance/track-team-benchmark-2026-08-15.md`。
- [x] 平衡收敛：标准 AI 在预计抵达弯道时优先低值牌，风险窗口按预计移动量计算；中国队恢复设计案 Go 直道输出、操控从 -1 调为 0，并默认采用 Go→Go→Recover；美国直道加成调整为每回合固定 +1。
- [x] 最终验证：Unity EditMode 308/308 通过；`dotnet build Foodular1.sln --no-restore` 0 错误；MainMenu→Race→返回主菜单运行态冒烟通过，单一 HUD、按钮动画组件和控制台均正常。
