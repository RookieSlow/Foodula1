---
name: game-mechanics
description: Core gameplay mechanics of Foodular1 — card movement, heat penalty, track design
metadata:
  type: project
---

Foodular1 gameplay: 85-node closed circuit track. Player starts with 5 cards [1,2,3,4,1]. Click cards to select (green), click "Next Round" to play selected cards. Truck moves by sum of selected card values, capped at node 84. Five corners have speed limits (1-3): node 13 (T1, limit 2), node 48 (T6, limit 3), node 53 (Chicane in, limit 1), node 55 (Chicane out, limit 1), node 66 (T7 hairpin, limit 1). If moveSteps > speedLimit at any traversed node, penalty = moveSteps - speedLimit, deducted from Cold Storage (starts at 6). Cold Storage < 0 = game over. No card draw mechanic yet, no win condition yet. See [[project-overview]].
