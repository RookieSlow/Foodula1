# Track Camera and Minimap Addition

**Date:** 2026-07-31  
**Classification:** Addition  
**Status:** Approved for implementation

## Context

Long layouts such as Le Mans and the Nürburgring Nordschleife contain far more
cells than the standard tracks. Showing the complete circuit through the main
camera makes cars and individual track nodes too small to read.

## Player-facing behavior

- The main race camera follows the player's current section of track.
- The visible section is calculated from the fifteen cells behind the player,
  the player's nearest cell, and the fifteen cells ahead.
- Camera position and zoom change smoothly as the player moves.
- A minimap in the upper-right corner always shows the complete circuit.
- Player and AI minimap markers retain a fixed screen size on every track.
- Node and corner-label visuals shrink on dense layouts to avoid overlap.
- The same presentation applies to all selectable tracks.

## Rules

1. Find the track node nearest to the player's rendered car position.
2. Wrap indices at the start/finish line.
3. Calculate a world-space bounding box for the configured cells behind and
   ahead.
4. Fit that box to the main camera aspect ratio and add configurable padding.
5. Calculate a second fixed bounding box containing every track node for the
   minimap camera.
6. Smooth only the main camera; the minimap remains fixed.

## Tuning

All values live in `GameConfigSO`:

- cells behind and ahead;
- main camera padding and minimum orthographic size;
- position and zoom smoothing times;
- minimap size, margin, world padding, marker size, and render resolution.

## Edge cases

- Track indices wrap around the start/finish line.
- Empty tracks or missing cameras fail safely without creating the minimap.
- One-node tracks return a valid minimum camera size.
- Resetting a race replaces car instances without rebuilding the camera system.
- A missing UI canvas keeps the main follow camera functional.

## Acceptance criteria

- All tracks use a 31-cell main view with the default configuration.
- Le Mans and Nürburgring cars remain readable in the main view.
- The complete selected circuit is visible in the minimap.
- Player and AI markers remain readable on long layouts.
- The minimap does not overlap the right-side race status column.
- Camera rule unit tests and the existing EditMode suite pass.

