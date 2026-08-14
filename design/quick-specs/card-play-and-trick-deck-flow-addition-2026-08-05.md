# Quick Design Spec: Card Play and Trick Deck Flow

**Type**: Addition  
**System**: Core card loop and hand interaction  
**GDD Reference**: `design/gdd/foodula-1-core-mechanics.md` — sections 1.1 and 1.3  
**Date**: 2026-08-05

## Change Summary

Trick cards use the same draw-pile, hand, discard-pile, and reshuffle lifecycle as speed cards instead of starting directly in hand. Human card play changes from batch selection to a one-card-at-a-time select, confirm, and resolve flow.

## Motivation

Starting with all four team trick cards removes draw variance and overfills the opening hand. Batch-confirming an entire speed selection also conflicts with instant trick effects and makes the order-sensitive Dim Sum Combo difficult to read. A per-card commit flow makes each play explicit while preserving the simultaneous movement-resolution phase.

## Design Delta

Current GDD says (`foodula-1-core-mechanics.md`, sections 1.1.3 and 1.3):

> 回合开始自动抽牌，补至手牌上限。Step 3 — 出牌：按当前档位打出对应数量的速度牌。

This spec changes that to:

At race setup, the four team trick cards are shuffled into the player's normal draw pile before the opening hand is drawn. During the card-play phase, the player may select only one playable card at a time. Pressing the action button confirms that card immediately: a speed card leaves the hand and is committed to this turn's movement; a trick card leaves the hand, enters the discard pile, and resolves its effect immediately. With no pending card, the same action button ends the card-play phase.

## New Rules / Values

1. Each team's four trick cards are added to the draw pile, then the full pile is shuffled before the opening draw.
2. Trick cards count toward the normal hand limit because they are ordinary drawn cards.
3. A confirmed trick card is removed from the hand, put into the discard pile, and its effect resolves immediately. It can be drawn again after the discard pile is reshuffled.
4. A confirmed speed card is removed from the hand and appended to the current turn's played-speed area. It enters the discard pile during normal end-of-turn cleanup.
5. Only one card can be pending confirmation. Selecting another card replaces the pending selection.
6. The action button reads `确认出牌` when a card is pending and `结束出牌` when none is pending.
7. Ending the phase with fewer confirmed speed cards than required retains the existing engine-failure heat cost.
8. Heat cards remain unplayable and cannot become pending.
9. The optional discard step remains a separate mode and may discard any non-heat card without resolving it.
10. The existing limit of one trick card per turn remains unchanged.

## Affected Systems

| System | Impact | Action Required |
|--------|--------|-----------------|
| `CardDeck` | Trick cards join normal draw/discard cycling | Add draw-pile insertion and generic non-heat discard APIs |
| `MVPGameManager` | Batch submission becomes per-card commit plus explicit phase end | Split card confirmation from phase finalization |
| `CardHandUI` / `CardUI` | Single pending card instead of multi-select play | Centralize selection and update action-button label |
| AI | Keeps automatic selection; trick cards are now drawn normally | No interaction change; use the same deck lifecycle |
| Tech tree | Dim Sum Combo observes real trick-then-speed commit order | Record speed play when each speed card is confirmed |
| GDD / integration guide | Existing opening-hand and batch-play descriptions become stale | Update the relevant card-loop sections |

## Acceptance Criteria

- [x] Opening hands contain only cards actually drawn from the shuffled combined deck; all four trick cards are not injected directly into hand.
- [x] A confirmed trick card disappears from hand, resolves once, and is present in the discard cycle.
- [x] A discarded trick card can return after the discard pile is reshuffled.
- [x] A human player confirms speed and trick cards one at a time; only one card is pending at once.
- [x] Confirmed speed cards accumulate movement and obey the current gear/extra-slot maximum.
- [x] Pressing the action button with no pending card ends card play and applies the existing missing-speed engine-failure rule.
- [x] Heat cards remain unplayable.
- [x] AI card selection and all existing card/heat tests continue to pass.

## GDD Update Required?

Yes. Update `design/gdd/foodula-1-core-mechanics.md` sections 1.1.2, 1.1.3, and 1.3 so the documented lifecycle and Step 3 interaction match this specification.
