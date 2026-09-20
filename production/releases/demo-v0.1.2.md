# Foodula1 Demo V0.1.2 Release Record

- **Release date**: 2026-09-20
- **Target**: Windows 64-bit
- **Unity**: 2022.3.62f3c1
- **Product version**: 0.1.2
- **Source build commit**: `5b9ca20389363c2edcacbbe72f92f3958a8f47d3`
- **Source archive tag**: `demo-v0.1.2`
- **GitHub Release**: `https://github.com/RookieSlow/Foodula1/releases/tag/demo-v0.1.2`
- **Build folder**: `Builds/Foodula1-Demo-v0.1.2-Windows/`
- **Archive**: `Builds/Foodula1-Demo-v0.1.2-Windows.zip`
- **Archive SHA-256**: `E4CD16E9E76119FCBD58007EECB02FD89CC76EA280F87462E62DFB0CDFE8E9C1`
- **Uncompressed size**: 204,144,148 bytes (194.69 MiB), 156 files
- **ZIP size**: 78,004,120 bytes (74.39 MiB)

## Release scope

- Added an effect strip above the hand so the currently highlighted or selected card explains its
  concrete gameplay effect without requiring a separate pile-inspection panel.
- Rebuilt the race result presentation as a masked, scrollable ranking panel that remains usable in
  12-driver races while keeping the title and return action fixed on screen.
- Added gold, silver and bronze podium treatment for the top three finishers, including explicit
  champion, runner-up and third-place labels.
- Preserved and packaged the tutorial highlighter lifecycle and slipstream staging corrections from
  the preceding V0.1.2 development changes.
- Updated the GDD, UX interaction patterns, roadmap and systems index to match the shipped behavior.

## Verification evidence

- Unity EditMode: 706 total, 706 passed, 0 failed, 0 skipped.
- Result-presentation targeted regression: 2 total, 2 passed; Unity Console was clean after the run.
- Windows Standalone build: succeeded from frozen source commit
  `5b9ca20389363c2edcacbbe72f92f3958a8f47d3` through `DemoBuild.BuildWindowsDemo`; Unity returned 0
  and logged `[DEMO_BUILD] Success version=0.1.2`.
- Built player GPU smoke: Direct3D 11 / NVIDIA GeForce RTX 4070, 15 seconds, responsive process and
  no matched error, exception, fatal or crash lines. The process was intentionally stopped afterward.
- `git diff --check`: passed for the release source commit before submission.
- Public download verification: the unauthenticated release asset URL returned HTTP 200 with the
  expected content length of 78,004,120 bytes; GitHub reports the asset state as `uploaded`.

## Known acceptance debt and next patch

- Run a complete 16-step tutorial plus one-lap practice session in the packaged Windows player.
- Visually verify the result panel with all 12 drivers at 16:9 and confirm scrollbar, podium colors
  and the fixed return action remain readable at supported resolutions.
- Continue the minimum-spec, controller, ultrawide and broader hardware compatibility matrix in a
  later patch; these are not claimed as verified by this release.
- Build outputs remain reproducible through `DemoBuild.BuildWindowsDemo` and intentionally excluded
  from Git; the verified ZIP is distributed through the public GitHub Release asset.
