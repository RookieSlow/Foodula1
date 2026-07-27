# Game Mechanics

Foodular1 core gameplay mechanics — card movement, heat penalty system, track design.

## Card System

- Player starts with 5 cards: [1, 2, 3, 4, 1]
- Click cards to select (green highlight)
- Click "Next Round" to play selected cards
- Truck moves by sum of selected card values, capped at node 84
- No card draw mechanic implemented yet
- No win condition implemented yet

## Track & Speed Limits

85-node closed circuit with 5 speed-limited corners:

| Corner | Node | Speed Limit |
|--------|------|-------------|
| T1     | 13   | 2           |
| T6     | 48   | 3           |
| Chicane In | 53 | 1          |
| Chicane Out | 55 | 1         |
| T7 Hairpin | 66 | 1          |

## Cold Storage (Heat Penalty)

- Cold Storage starts at 6
- If `moveSteps > speedLimit` at any traversed node:
  - `penalty = moveSteps - speedLimit`
  - Penalty deducted from Cold Storage
- Cold Storage < 0 = Game Over

See [[project-overview]] for project context.
