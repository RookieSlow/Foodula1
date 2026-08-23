# Foodula1 MVP Prototype

**HEAT boardgame digital adaptation — minimum viable prototype**

Built: 2026-07-21 | Unity 2022.3.62f2 | C#

---

## What This Is

A single-player card-driven racing game that validates the core HEAT loop:
gear selection → card play → movement → corner judgment → heat management.

1 human player vs 1 AI opponent, 3 laps on a 42-node circuit.

## What's Implemented

| System | Status |
|--------|--------|
| Gear system (1-4, +1 up, any down, cooldown) | Done |
| Card deck (12 speed + 3 heat, 7 hand limit, cycle) | Done |
| Heat pool (shared, corner penalty → discard → deck → hand) | Done |
| Per-corner-segment judging (5 corners, speed limit 2-4) | Done |
| AI opponent (4-priority behavior tree) | Done |
| 3-lap race with finish ranking | Done |
| G1 cooling bonus (remove 1 heat per turn at gear 1) | Done |
| Engine failure penalty (missing speed cards = +heat) | Done |
| Card selection limits (max gear cards per type) | Done |
| Deck composition display | Done |
| Gear confirmation system | Done |

## What's NOT in MVP (deferred to full demo)

- Multi-lane track / blocking
- Slipstream (+2 move when behind opponent)
- Spin-out mechanic
- Stress / Upgrade cards
- Weather system
- Teams, drivers, skill trees
- Multiple AI opponents
- Pit lane / repair
- Proper UI layout (hardcoded positions)
- Card animations, car rotation

## Architecture

```
GameConfigSO (ScriptableObject, all tunable params)
    |
MVPGameManager (coroutine-driven turn loop)
    |-- PlayerState (data: gear, position, lap, deck)
    |     |-- CardDeck (pure logic: draw/discard/heat lifecycle)
    |-- TrackManager (42 nodes, 5 corners, per-segment dedup)
    |-- AIController (4-priority behavior tree)
    |-- CardHandUI (hand display, card selection)
    |-- HUDUI (status, gear buttons, log, game over)
```

### Key Files

| File | Purpose |
|------|---------|
| `Assets/Scripts/MVPGameManager.cs` | Main game loop, turn flow, gear/card/corner logic |
| `Assets/Scripts/CardDeck.cs` | Pure logic: deck, hand, discard, heat pool lifecycle |
| `Assets/Scripts/AIController.cs` | AI gear selection + card selection |
| `Assets/Scripts/TrackManager.cs` | 42-node circuit, 5 corners, corner label rendering |
| `Assets/Scripts/GameConfigSO.cs` | All tunable parameters (hand size, deck composition, etc.) |
| `Assets/Scripts/CardHandUI.cs` | Hand display, gear/card selection UI |
| `Assets/Scripts/HUDUI.cs` | Status display, log, game over screen |
| `Assets/Scripts/PlayerState.cs` | Player runtime state (gear, lap, position) |
| `Assets/Scripts/CardData.cs` | Card type/value enum |
| `Assets/Scripts/CardUI.cs` | Per-card MonoBehaviour (selection, visual) |
| `Assets/Scripts/TrackNode.cs` | Track node data (cornerId, speedLimit, isStartFinish) |
| `Assets/Scenes/SampleScene.unity` | Main scene |
| `design/gdd/game-concept.md` | MVP design document |

## How to Run

1. Open project in Unity 2022.3.62f2
2. Open `Assets/Scenes/SampleScene.unity`
3. Hit Play
4. UI auto-creates if not configured (no manual setup needed)

## Gameplay Quick Reference

### Turn Flow
1. **Select gear** — click G1-G4, then CONFIRM (upshift max +1, downshift any)
2. **Draw cards** — auto, to 7 hand limit
3. **Select cards** — click N speed cards (N = gear), optionally up to N heat cards, then PLAY
4. **Move + corners** — car animates, each unique corner judges overspeed
5. **Cleanup** — all played cards → discard pile, heat cycles back through deck

### Heat Lifecycle
```
Heat Pool (shared, 12)
  → corner overspeed penalty → Discard Pile
  → deck reshuffle → Draw Pile
  → draw → Hand
  → downshift cooldown (only way out!) → Heat Pool
  → G1 bonus: remove 1 heat/turn from hand or deck → Heat Pool
```

### Corners
| Name | Limit | Description |
|------|-------|-------------|
| T1 Parabolica | 3 | Medium-speed right |
| T2 Grand Hotel | 2 | Hairpin |
| T3 Copse | 4 | High-speed curve |
| T4 Chicane | 2 | Sharp chicane |
| T5 Lesmo | 3 | Medium-speed left |

### Key Rules
- **Downshift 1-2 gears**: remove that many heat from hand (cooldown)
- **Downshift 3+ gears**: NO cooldown, ADD heat instead (punishment!)
- **Gear 1 bonus**: remove 1 heat each turn at G1 (hand first, then deck)
- **Missing speed cards**: each missing = +1 heat to discard (engine failure)
- **Blown engine**: when deck + discard are both empty at draw time

## Known Limitations (MVP Scope)

- UI positions are hardcoded (may look messy at different resolutions)
- No visual feedback for heat levels on car/track
- AI doesn't learn or adapt strategy
- Track is a single hardcoded 42-node circuit
- No sound effects
