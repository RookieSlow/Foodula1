# Current Task List

> Updated: 2026-07-29
> Sources: Claude Code project memory, active session state, session history,
> current Git worktree, and current Unity project structure.

## P0 - Resume Approved Scheme A Refactor

- [ ] Review all current C# files and reconcile the earlier whole-project
  review with the latest project state.
- [ ] Integrate `Assets/Scripts/Core/RaceRules.cs` into
  `MVPGameManager.cs` so shared gear, cooling, movement, and selection rules
  have one source of truth.
- [ ] Integrate `Assets/Scripts/AI/AIPlanner.cs` into `AIController.cs`.
- [ ] Inject `IRandomSource` where deterministic gameplay or AI behavior is
  required.
- [ ] Add EditMode tests for `RaceRules`, `AIPlanner`, and deterministic
  random behavior.
- [ ] Validate every changed script, wait for Unity compilation, and confirm
  that the Unity console has no errors.
- [ ] Review the final diff and commit only the approved refactor changes.

The four Scheme A source files and their `.meta` files currently exist as
untracked work. They are not referenced by the existing runtime and must not
be treated as a completed refactor.

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
- [ ] Configure `GameConfigSO.trackId` and verify JSON track loading in the
  Race scene.
- [ ] Validate arbitrary node counts throughout movement and UI; remove
  remaining hard-coded `42` display assumptions.
- [ ] Validate unique-corner crossing and speed-limit penalties.
- [ ] Validate start/finish crossing and lap counting from track data.
- [ ] Implement or verify pit entry and pit exit behavior.
- [ ] Drive LineRenderer positions from loaded track coordinates.
- [ ] Rotate vehicles to follow the tangent between track nodes.
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
- [ ] Implement the driver-selection flow.
- [ ] Add sound effects.
- [ ] Add card-play, vehicle movement, bounce, and spin-out animations.
- [ ] Add weather gameplay after track data and race rules are stable.

## Open Decisions

- [ ] Select the replacement for the reverted Track Node Editor.
- [ ] Confirm how the Le Mans test track relates to the six national teams.
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
