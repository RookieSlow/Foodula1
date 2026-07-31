# Quick Design Spec: Nordschleife Layout Replacement

> Date: 2026-07-31
> Type: Tweak
> System: Track System
> Related GDD: `design/gdd/foodula-1-tracks.md`

## Change Summary

Replace the inaccurate manually digitized Nürburgring 24H combined layout with
the standalone Nürburgring Nordschleife. Keep the existing internal track ID
`nurburgring_24h_endurance` so saved selections and runtime references remain
compatible, while changing all player-facing names and track data to identify
the Nordschleife.

## Motivation

The previous 53-point control path mixed the Grand Prix circuit and
Nordschleife, contained three self-intersections, and did not reproduce the
real route reliably. It made the in-game layout visually confusing and failed
the project's goal of preserving recognizable real-world circuit geometry.

## Rules and Data

- Use the referenced 2013 current-layout Nordschleife SVG as the geometry
  source.
- Sample the source's closed circuit path into 219 evenly spaced gameplay
  nodes.
- Represent one 20,832-metre clockwise lap with 73 real-world corners.
- Preserve 18 gameplay corner groups, each with exactly one apex node.
- Require exactly one start/finish node and zero path self-intersections.
- Do not include the Grand Prix circuit or Mercedes Arena in this track.
- Keep the separate `nurburgring_bier` Grand Prix-based track unchanged.
- Regenerate the track guide and forest-background composite from the same
  normalized node coordinates.

## Affected Content

- Nürburgring endurance JSON configuration
- Main-menu track name and subtitle
- Nürburgring guide and runtime background image
- Real-layout generation and compositing scripts
- Track documentation, source attribution, and EditMode tests

## Acceptance Criteria

- [x] The runtime configuration loads as `纽博格林北环`.
- [x] The track contains 219 sequential nodes totaling exactly 20,832 metres.
- [x] The generated closed path has zero self-intersections.
- [x] No Grand Prix-only segment metadata remains in the configuration.
- [x] The guide and runtime background show the standalone Nordschleife.
- [x] Existing internal references continue to use the stable track ID.
- [x] Focused structural validation and the C# solution build pass.

## Documentation Impact

No core GDD rewrite is required because this bonus circuit is an implementation
extension rather than one of the six national-team tracks defined by the
current draft GDD. This quick spec and `docs/track-system-readme.md` record the
implemented behavior.