# Content Audit — 2026-08-27

## Summary

- **Total specified**: 169 content or presentation items across 9 systems/categories
- **Total found**: 83
- **Gap**: 86 items (50.9% not implemented as dedicated content/assets)
- **Scope**: Full Demo content and presentation audit

> Counts are approximate and intentionally separate implemented gameplay content
> from dedicated presentation assets. Runtime-generated UI/VFX are counted as
> functional substitutes but not as authored portrait, emblem, music or SFX files.
> The large numerical gap does not mean the core Demo is 50.9% incomplete:
> 24 driver skill effects are post-Demo scope and 62 items are art/audio resources.

## Gap Table

| System | Content Type | Specified | Found | Gap | Status |
|---|---|---:|---:|---:|---|
| Teams & Cars | Team profiles + car sprites | 6 | 6 | 0 | COMPLETE |
| Drivers | Driver catalog entries | 12 | 12 | 0 | COMPLETE |
| Drivers | Portraits | 12 | 0 | 12 | NOT STARTED |
| Drivers | Passive + active race effects | 24 | 0 | 24 | NOT STARTED (P2) |
| Tracks | Official JSON tracks | 8 | 8 | 0 | COMPLETE |
| Tracks | Official layout backgrounds | 8 | 8 | 0 | COMPLETE |
| Tech Tree | Runtime node definitions | 37 | 37 | 0 | COMPLETE |
| Trick Cards | Runtime card definitions | 12 | 12 | 0 | COMPLETE |
| Trick Cards | Dedicated illustrations | 12 | 0 | 12 | NOT STARTED (P1) |
| Team Presentation | Team emblems/flags | 6 | 0 | 6 | NOT STARTED |
| Brand Presentation | Main-menu background + Logo | 2 | 0 | 2 | NOT STARTED |
| Tech Presentation | Background + node states + tier badges | 7 | 0 | 7 | NOT STARTED |
| Audio | Core menu/race music | 2 | 0 | 2 | NOT STARTED |
| Audio | P0 interaction/gameplay SFX | 21 | 0 | 21 | NOT STARTED |

## HIGH PRIORITY Gaps

1. **Demo acceptance evidence** is higher priority than adding assets: complete-race,
   pit/weather/heat/slipstream and multi-car checks must be signed off before the
   baseline is frozen.
2. **Brand and selection readability**: main-menu background/Logo, six team emblems,
   twelve portraits and tech-tree art are the largest visible placeholder group.
3. **Audio**: the directory structure exists but contains no audio clips; code contains
   no AudioSource, AudioClip, AudioMixer or audio service.

Driver signature effects and trick-card illustrations are valuable but are not
required to call the present core loop a qualified Demo.

## Per-System Breakdown

### Core Mechanics

- **GDD**: `design/gdd/foodula-1-core-mechanics.md`
- **Notes**: Functional card/heat/gear/corner/pit/weather/slipstream content is
  implemented and regression-tested. The remaining work is final runtime acceptance,
  not a missing content count.

### Teams, Cars and Trick Cards

- **GDDs**: `design/gdd/foodula-1-teams-cars.md`,
  `design/gdd/foodula-1-tech-tree.md`
- **Notes**: Six car sprites and twelve trick-card definitions are present.
  Team emblems and trick-card illustrations are dedicated art gaps.

### Drivers

- **GDD**: `design/gdd/foodula-1-drivers.md`
- **Notes**: All twelve catalog entries, progression data and selection are present.
  Portraits are missing. Twenty-four described passive/active race effects are
  tracked as post-Demo implementation scope.

### Tracks

- **GDD**: `design/gdd/foodula-1-tracks.md`
- **Notes**: Eight official JSONs and eight 3840×2160 layout images are present.
  The old 42-node art checklist is retired; `fallback_42` remains a legitimate
  data fallback, and Indianapolis legitimately has 42 cells.

### Tech Tree

- **GDD**: `design/gdd/foodula-1-tech-tree.md`
- **Notes**: 37 runtime definitions are present. Background, node-state frames and
  tier badges are presentation gaps, not missing rules.

### Visual and Audio

- **GDDs**: `design/gdd/foodula-1-visual-style.md`,
  `design/gdd/foodula-1-audio-style.md`
- **Notes**: Race presentation has runtime substitutes for movement, piles, card
  transfer, tailwind and spin-out. Brand/portrait/tech art and all audio remain.

## Consistency Caveat

`design/registry/entities.yaml` exists but contains no registered entities,
items, formulas or constants. Therefore the automated entity-level
`consistency-check` could not produce a valid pass/fail result. This audit used
manual GDD/code/asset comparison and does not claim registry consistency.

## Recommendation

1. Complete and sign off the Demo acceptance matrix; fix only demonstrated defects.
2. Freeze the verified code baseline and then create the visual identity package.
3. Implement the AudioMixer/service and P0 music/SFX package.
4. Treat driver signature effects, trick-card illustrations and platform features
   as post-Demo work unless acceptance reveals a concrete need.

## Unspecified Content Counts

- Result-screen decoration variants and weather animation variants are qualitative;
  the asset manifest intentionally requests one baseline set rather than an open count.
- Main-menu animation layers are optional and have no fixed count.
- Future garage/career scenes and additional tracks/teams are expansion ideas, not
  current commitments.
