---
status: reverse-documented
source: Assets/Scripts/
date: 2026-07-19
verified-by: User
---

# Foodula1 — Game Concept

> **Note**: This document was reverse-engineered from the existing implementation.
> It captures current behavior. Some sections may be incomplete where
> design intent was not yet formalized.

## 1. Overview

Foodula1 is a 2D card-driven food truck racing game. The player manages a food
truck racing along an 85-node circuit track. Movement is determined by playing
cards from a hand — each card has a step value (1–4). The core strategic
mechanic is **speed limits** at specific corners: if the total steps of played
cards exceeds a corner's speed limit, the truck's "cold storage" (冰鲜库) takes
a heat penalty. Run out of cold storage and the truck overheats, ending the game.

The name blends "food" + "formula" — a food truck racing concept.

## 2. Player Fantasy

The player feels like a strategic food truck racer who must balance speed
against preservation of their cold storage. Push too hard through corners and
risk overheating; play too conservatively and never reach the finish line.

## 3. Detailed Rules

- **Track**: 85 nodes (indices 0–84) forming a closed circuit
- **Starting Position**: Node 51 (configurable)
- **Starting Hand**: 5 cards with values [1, 2, 3, 4, 1]
- **Card Play**: Click cards to toggle selection (green = selected). Click
  "Next Round" to play all selected cards and move the truck
- **Movement**: Truck advances by the sum of selected card values, capped at
  node 84
- **Speed Limits**: Five corners have speed limits (1–3). If the card total
  exceeds a corner's limit, the difference is subtracted from Cold Storage
- **Cold Storage (冰鲜库)**: Starts at 6. When it drops below 0, the truck
  overheats — game over
- **Win Condition**: Not yet defined in code (likely: reach/finish the track)

## 4. Formulas

| Formula | Definition |
|---------|------------|
| Target Position | `min(currentPosition + sum(selectedCards), 84)` |
| Penalty | `sum(max(0, moveSteps - node.speedLimit))` for each traversed node |
| Overheat Check | `currentHeat < 0` → game over |

## 5. Edge Cases

- **Movement cap**: Player cannot move beyond node 84 (track end)
- **Zero cards selected**: PlayTurn() shows warning, no movement
- **Moving during animation**: isMoving flag blocks concurrent moves
- **Overheat mid-move**: Penalty calculated after movement completes;
  game-over state blocks further turns

## 6. Dependencies

- TextMesh Pro (UI text rendering)
- UGUI (buttons, canvas, layout groups)
- Unity 2D (SpriteRenderer, LineRenderer)

## 7. Tuning Knobs

| Knob | Default | Location |
|------|---------|----------|
| Starting Position | 51 | GameManager.currentCarPosition (serialized) |
| Cold Storage | 6 | GameManager.currentHeat (serialized) |
| Move Speed (animation) | 8f | GameManager.moveSpeed (serialized) |
| Starting Hand | [1,2,3,4,1] | DealStartingHand() (hardcoded — should be data-driven) |
| Speed Limits | See track-system.md | InitializeTrack() (hardcoded — should be data-driven) |

## 8. Acceptance Criteria

- [x] Cards can be selected/deselected by clicking
- [x] Selected card total is displayed in UI
- [x] "Next Round" button moves the truck by selected steps
- [x] Speed limit penalties are calculated correctly
- [x] Cold storage depletes on penalties
- [x] Game over when cold storage < 0
- [x] Reset button restores initial state
- [ ] Win condition implemented
- [ ] Card values and starting hand are data-driven
- [ ] Speed limits and track data are data-driven
