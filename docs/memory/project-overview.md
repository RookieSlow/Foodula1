# Project Overview

Foodular1 is a Unity 2022.3.62f2 2D card-driven food truck racing game using the CCGS framework.

## Core Info

- **Engine**: Unity 2022.3.62f2
- **Language**: C# 9.0
- **UI**: uGUI + TextMesh Pro
- **Code Location**: Assets/Scripts/

## Key Scripts

- `GameManager.cs` — core game controller
- `CardUI.cs` — card interaction and UI
- `TrackNode.cs` — track node data class

## Track

- 85-node closed circuit
- 5 speed-limited corners

## Heat System

- "Cold Storage" mechanic — driving too fast through corners generates heat penalties
- Cold Storage starts at 6
- If Cold Storage < 0 = Game Over

See [[game-mechanics]] for detailed rules.
