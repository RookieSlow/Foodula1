---
status: reverse-documented
source: Assets/Scripts/CardUI.cs, Assets/Prefab/CardPrefab.prefab
date: 2026-07-19
verified-by: User
---

# Card System Design

> **Note**: This document was reverse-engineered from the existing implementation.
> It captures current behavior. Some sections may be incomplete where
> design intent was not yet formalized.

## 1. Overview

The card system handles the player's hand of speed cards. Each card has a
numeric value (1–4) representing how many track nodes the truck advances.
Cards are selected/deselected by clicking, and played as a group via the
"Next Round" button. Played cards are consumed (destroyed).

## 2. Player Fantasy

The player feels like they're making tactical speed choices — selecting the
right combination of cards to navigate corners safely while making progress.

## 3. Detailed Rules

- **Starting Hand**: 5 cards: [1, 2, 3, 4, 1]
- **Selection**: Click a card to toggle selection. Selected cards turn green.
- **Multi-Select**: Any number of cards can be selected simultaneously.
- **Play**: Click "Next Round" to play all selected cards. The total of
  selected card values determines movement distance.
- **Consumption**: Played cards are destroyed and removed from the hand.
- **Recalculation**: The "Ready to move: X steps" display updates in real-time
  as cards are selected/deselected.
- **No Draw**: There is currently no card draw mechanic — the starting hand is
  all the player gets (Reset redeals the same hand).

## 4. Formulas

| Formula | Definition |
|---------|------------|
| Total Steps | `sum(card.value)` for all cards where `card.isSelected == true` |

## 5. Edge Cases

- **Zero selected**: PlayTurn() warns "Please select at least one card" and
  does not proceed
- **All cards played**: After playing all 5 cards, hand is empty. No draw
  mechanic exists — player must reset
- **Rapid clicks**: Toggle is instant and idempotent — no debounce issues

## 6. Dependencies

- GameManager (hand management, step calculation, play resolution)
- TextMesh Pro (card value display)
- UGUI Button (click detection)
- UGUI Image (selection highlight color)

## 7. Tuning Knobs

| Knob | Default | Location |
|------|---------|----------|
| Starting Hand Values | [1,2,3,4,1] | GameManager.DealStartingHand() |
| Hand Size | 5 | GameManager.DealStartingHand() |
| Card Width × Height | 160 × 30 | CardPrefab RectTransform |
| Selected Color | Green | CardUI.OnCardClicked() |
| Deselected Color | White | CardUI.SetupCard() |

## 8. Acceptance Criteria

- [x] Cards display their value as text
- [x] Clicking a card toggles its selection state
- [x] Selected cards are visually distinct (green background)
- [x] Selecting/deselecting updates the total steps display
- [x] Playing a turn consumes selected cards
- [x] Missing cards do not break CalculateSelectedSteps()
- [ ] Card draw mechanic (future)
- [ ] Card values are data-driven (currently hardcoded)
- [ ] Card art/visual design beyond color highlight
