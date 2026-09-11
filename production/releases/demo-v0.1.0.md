# Foodula1 Demo V0.1.0 Release Record

- **Release date**: 2026-09-11
- **Target**: Windows 64-bit
- **Unity**: 2022.3.62f3c1
- **Product version**: 0.1.0
- **Source build commit**: `680324d4d7df2d21d4db7581cd101af8ced5f9f0`
- **Source archive tag**: `demo-v0.1.0`
- **Build folder**: `Builds/Foodula1-Demo-v0.1.0-Windows/`
- **Archive**: `Builds/Foodula1-Demo-v0.1.0-Windows.zip`
- **Archive SHA-256**: `CBBB033D3BDC0B16805EA39110D7F4988E1A4D2DB35A6B835E2920183C3FE8A1`
- **Uncompressed size**: 204,096,081 bytes (194.64 MiB), 156 files
- **ZIP size**: 77,870,771 bytes (74.26 MiB)

## Release verdict

**GO — suitable for the V0.1.0 Demo archive.** The core card-racing loop, six teams,
twelve drivers, eight official tracks, tutorial, Free Race, Thunderstorm field, current
career flow, settings, encyclopedia, music and core SFX form a coherent playable Demo.
No remaining item requires a large core-gameplay change before this archive.

## Verification evidence

- Unity EditMode: 676 total, 676 passed, 0 failed, 0 skipped (6.65 seconds).
- Windows Standalone build: succeeded, 0 build errors; both enabled scenes included.
- Built player GPU smoke: Direct3D 11 / NVIDIA GeForce RTX 4070, 12 seconds, 0 matched
  errors or exceptions. The process was intentionally stopped after the startup window.
- A separate null-graphics probe emitted expected unsupported-shader messages and is not
  used as visual acceptance evidence.
- Source scan: 0 TODO, 0 FIXME and 0 HACK markers in `Assets/Scripts` and `Assets/Tests`.

## Known non-blocking acceptance debt

- Final 1920x1080 and 2560x1440 visual walkthroughs remain for tutorial overlays,
  confirmation panels, card/vehicle presentation and the twelve-car field.
- The most recent China fix still benefits from a focused manual check for instant
  finish-line traversal and optional Hotpot slots.
- The complete career create-to-eight-race flow and audio mix still need longer manual
  acceptance passes.
- Formal driver portraits, technology-tree artwork, AudioMixer routing, peripheral SFX,
  the remaining seven driver passives and controller/difficulty work are not V0.1.0 blockers.

## Version policy

- **V0.1.x**: confirmed bug fixes, compatibility, copy, audio levels, resource references
  and small presentation corrections only. Preserve core rules, calendar and save contracts.
- **V0.2**: new mechanics, broad balance work, save/schema changes, content expansion,
  formal visual packages, remaining skills, controller support and architecture/tooling work.

Build outputs and ZIP files are intentionally ignored by Git. The tag archives source,
configuration, release metadata and the reproducible `DemoBuild.BuildWindowsDemo` entry point.
