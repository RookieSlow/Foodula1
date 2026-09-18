# Quick Design Spec: Playtest Operation Logging System

**Type**: New Small System
**Scope**: Record privacy-safe, local playtest operations and runtime evidence across the distributed Demo, then export the evidence as one ZIP for voluntary tester feedback. The system does not upload data, record typed text, or collect a player name, device identifier, account, or absolute user path.
**Date**: 2026-09-17
**Estimated Implementation**: 4-6 hours

## Overview

The Demo starts one anonymous log session per application launch. It records application and scene lifecycle, pointer operations, a limited set of control keys, selected UI/runtime state, warnings and exceptions, and a mirror of the existing structured race trace. Events are UTF-8 JSON Lines and are flushed immediately so a crash still leaves useful evidence. A settings button exports playtest sessions and race logs into one ZIP that the tester can send manually.

## Core Rules

1. Logging starts before the first scene and survives scene changes.
2. Each event contains a monotonic sequence, UTC time, unscaled runtime, app version, scene, category, action, target, details, normalized pointer position, and current time scale.
3. Pointer events include down/up, hit object hierarchy, interactable state and visible game-authored label. Input-field contents and clipboard contents are never recorded.
4. Console warnings, errors and exceptions are recorded. Known local roots are replaced with neutral tokens before writing.
5. `RaceTestLogWriter` mirrors its structured gameplay messages into the playtest session while retaining the existing human-readable race log.
6. Files are written below `Application.persistentDataPath/playtest-logs/`. At most 30 completed session directories are retained.
7. Export creates `Application.persistentDataPath/playtest-exports/Foodula1-playtest-*.zip` containing all retained playtest sessions, current race logs and a privacy/readme file.
8. Logging and export failures are best effort and must never block menu or race flow.
9. No automatic network transfer is performed. Sending the ZIP remains an explicit tester action.
10. Pressing F8 creates an issue marker plus game-window screenshot and shows a short confirmation; screenshots remain local until export.
11. A 15-second performance sample records average FPS, worst frame time, slow-frame count and allocated memory. Metadata includes broad compatibility information but no unique hardware/device identifier.
12. Every ZIP includes a machine-readable manifest with file counts, version and export time, and uses millisecond filenames to avoid collisions.

## Tuning Knobs

| Knob | Default | Range | Category | Rationale |
|---|---:|---:|---|---|
| Retained sessions | 30 | 5-100 | storage | Enough for repeated tests without unbounded growth |
| Target path depth | 8 | 2-16 | diagnostics | Identifies controls without oversized events |
| Label length | 120 | 40-240 | privacy/size | Captures authored UI meaning while limiting payload |
| Console message length | 2000 | 256-8000 | diagnostics | Preserves actionable failures without runaway logs |
| Performance interval | 15 s | 5-60 s | diagnostics | Correlates reported problems with stalls without per-frame log volume |
| Slow-frame threshold | 50 ms | 33-100 ms | diagnostics | Highlights player-visible stalls in a lightweight way |

## Acceptance Criteria

- [ ] A fresh application launch creates a session directory, metadata, JSONL events and rolling summary.
- [ ] Scene transitions, pointer actions, supported keys, focus/pause/quit and warning/error/exception events are recorded.
- [ ] Existing structured race events are mirrored without changing race behavior or the legacy race log format.
- [ ] Settings exposes an export action that produces a readable ZIP containing sessions, race logs and privacy guidance.
- [ ] Logs contain no typed text, player name, device identifier, account identifier or unsanitized user-profile path.
- [ ] F8 creates a visible issue marker and screenshot that appears in the exported bundle.
- [ ] Performance samples and anonymous compatibility metadata are present without unique identifiers.
- [ ] Export includes `manifest.json` and reports the resulting session count and bundle size.
- [ ] Writer/export failures do not throw into gameplay.
- [ ] Automated tests cover event serialization, sanitization, summaries, retention and ZIP contents.
- [ ] No regression in the existing race-log analyzer or normal menu/race flow.

## Systems Index

This is presentation/QA infrastructure below systems-index tracking threshold. This quick spec is sufficient; the feature is referenced from the Demo framework and current task list.
