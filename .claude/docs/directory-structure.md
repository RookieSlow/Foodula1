# Directory Structure

```text
/
├── CLAUDE.md                    # Master configuration
├── .claude/                     # Agent definitions, skills, hooks, rules, docs
├── Assets/                      # Unity project assets
│   ├── Scripts/                 # Game source code (C# scripts)
│   ├── Prefab/                  # Unity prefabs (Card, Car, Node)
│   ├── Scenes/                  # Unity scenes
│   └── TextMesh Pro/            # TextMesh Pro assets (third-party)
├── ProjectSettings/             # Unity project configuration
├── Packages/                    # Unity package dependencies
├── src/                         # Framework source root (see Assets/Scripts/)
│   ├── core/                    # Engine-level systems
│   ├── gameplay/                # Gameplay features
│   ├── ai/                      # AI behaviors
│   ├── networking/              # Multiplayer (future)
│   ├── ui/                      # User interface
│   └── tools/                   # Development tools
├── design/                      # Game design documents (gdd, narrative, levels, balance)
├── docs/                        # Technical documentation (architecture, api, postmortems)
│   └── engine-reference/        # Curated engine API snapshots (version-pinned)
├── tests/                       # Test suites (unit, integration, performance, playtest)
├── tools/                       # Build and pipeline tools (ci, build, asset-pipeline)
├── prototypes/                  # Throwaway prototypes (isolated from src/)
└── production/                  # Production management (sprints, milestones, releases)
    ├── session-state/           # Ephemeral session state (active.md — gitignored)
    └── session-logs/            # Session audit trail (gitignored)
```

> **Unity Note**: This project uses Unity, so actual game code lives in `Assets/Scripts/`.
> The `src/` directory mirrors framework conventions. Game assets (prefabs, scenes,
> sprites) live in `Assets/`. Engine configuration lives in `ProjectSettings/` and
> `Packages/manifest.json`.
