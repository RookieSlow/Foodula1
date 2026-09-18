# Foodula1 Demo V0.1.1 Source Release Record

- **Release date**: 2026-09-18
- **Unity**: 2022.3.62f3c1
- **Product version**: 0.1.1
- **Source archive tag**: `demo-v0.1.1`
- **Release type**: source snapshot; no Windows build or ZIP was produced in this release turn

## Release scope

- Added local-only anonymous playtest telemetry, issue marking and tester-controlled ZIP export.
- Refreshed the heat gauge immediately after heat payment, cooling and corner penalties.
- Reorganized the race action area, added keyboard race controls and replaced the expanding status
  text with a fixed masked scrolling log window.
- Corrected China Hotpot ATTACK so it empowers the next normally required speed card without adding
  a card slot, grants +1 movement and excludes that whole card from corner-speed calculation.
- Improved China AI corner look-ahead for long Go movements and expanded regression coverage.

## Verification evidence

- Unity EditMode: 688 total, 688 passed, 0 failed, 0 skipped.
- Runtime and Editor MSBuild: 0 compilation errors; existing Unity/MCP assembly-version warnings remain.
- Latest inspected manual session: 324 events, 0 warnings, 0 errors; Hotpot ATTACK movement and
  corner exclusion were visible in the race log, and animation time scale returned to 1.00.
- `git diff --check`: passed before release submission; line-ending notices are informational.

## Known acceptance debt and next patch

- Complete the remaining 16:9 visual check for the fixed scrolling race log, confirmation button
  spacing and China consecutive-Go four-card limit.
- V0.1.2 will reconcile all 17 encyclopedia topics with current controls, confirmation preferences,
  telemetry export, real-time heat display and Hotpot ATTACK wording.
- V0.1.2 will update tutorial guidance for the current one-press Space card action and race layout,
  then run the full 16-step guide plus one-lap practice in a Windows build.
- No V0.1.1 Windows binary or GitHub Release attachment is claimed by this record. Build outputs remain
  reproducible through `DemoBuild.BuildWindowsDemo` and intentionally excluded from Git.
