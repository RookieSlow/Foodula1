# Foodula1 Demo V0.1.1 Release Record

- **Release date**: 2026-09-18
- **Target**: Windows 64-bit
- **Unity**: 2022.3.62f3c1
- **Product version**: 0.1.1
- **Source build commit**: `2e40cc1bf45a7b49a6ce72649eadd9f92c180eae`
- **Source archive tag**: `demo-v0.1.1`
- **GitHub Release**: `https://github.com/RookieSlow/Foodula1/releases/tag/demo-v0.1.1`
- **Build folder**: `Builds/Foodula1-Demo-v0.1.1-Windows/`
- **Archive**: `Builds/Foodula1-Demo-v0.1.1-Windows.zip`
- **Archive SHA-256**: `59BB7C732A0EE2F70D8AA7522CDB437E0FEB4CF7E1E1984B98EC4338C0A42BD7`
- **Uncompressed size**: 204,128,452 bytes (194.67 MiB), 156 files
- **ZIP size**: 76,759,395 bytes (73.20 MiB)

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
- Windows Standalone build: succeeded from the frozen `demo-v0.1.1` worktree with strict Unity
  build options; both enabled scenes were included.
- Built player GPU smoke: Direct3D 11 / NVIDIA GeForce RTX 4070, 15 seconds, no matched errors,
  exceptions or crashes. The process was intentionally stopped after startup verification.
- Latest inspected manual session: 324 events, 0 warnings, 0 errors; Hotpot ATTACK movement and
  corner exclusion were visible in the race log, and animation time scale returned to 1.00.
- `git diff --check`: passed before release submission; line-ending notices are informational.
- Public download verification: the unauthenticated release asset URL returned HTTP 200 and the
  expected content length of 76,759,395 bytes.

## Known acceptance debt and next patch

- Complete the remaining 16:9 visual check for the fixed scrolling race log, confirmation button
  spacing and China consecutive-Go four-card limit.
- V0.1.2 will reconcile all 17 encyclopedia topics with current controls, confirmation preferences,
  telemetry export, real-time heat display and Hotpot ATTACK wording.
- V0.1.2 will update tutorial guidance for the current one-press Space card action and race layout,
  then run the full 16-step guide plus one-lap practice in a Windows build.
- Build outputs remain reproducible through `DemoBuild.BuildWindowsDemo` and intentionally excluded
  from Git; the verified ZIP is distributed through the public GitHub Release asset.
