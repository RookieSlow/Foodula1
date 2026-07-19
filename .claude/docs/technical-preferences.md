# Technical Preferences

<!-- Populated by /setup-engine. Updated as the user makes decisions throughout development. -->
<!-- All agents reference this file for project-specific standards and conventions. -->

## Engine & Language

- **Engine**: Unity 2022.3.62f2
- **Language**: C# 9.0
- **Rendering**: Built-in Render Pipeline (Forward, 2D project)
- **Physics**: Built-in 2D Physics (Box2D)

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC Standalone (Windows)
- **Input Methods**: Keyboard/Mouse
- **Primary Input**: Mouse (click-based card selection and button interaction)
- **Gamepad Support**: None
- **Touch Support**: None
- **Platform Notes**: UI designed for mouse interaction. No hover-only interactions needed.

## Naming Conventions

- **Classes**: PascalCase (e.g., `GameManager`)
- **Variables**: camelCase (e.g., `moveSpeed`) — existing codebase convention
- **Signals/Events**: PascalCase (e.g., `OnCardClicked`)
- **Files**: PascalCase matching class (e.g., `GameManager.cs`)
- **Scenes/Prefabs**: PascalCase (e.g., `SampleScene.unity`, `CardPrefab.prefab`)
- **Constants**: PascalCase or UPPER_SNAKE_CASE

## Performance Budgets

- **Target Framerate**: 60fps
- **Frame Budget**: 16.6ms
- **Draw Calls**: Minimal (simple 2D scene with LineRenderer track)
- **Memory Ceiling**: 512MB

## Testing

- **Framework**: Unity Test Framework (com.unity.test-framework@1.1.33)
- **Minimum Coverage**: To be determined
- **Required Tests**: Card selection/deselection logic, heat penalty calculation, track node speed limit enforcement, movement range clamping

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- No `Find()`, `FindObjectOfType()`, or `SendMessage()` — use Inspector references or dependency injection
- No `Resources.Load()` — use direct Inspector references for this project's scope
- No allocations in `Update()` hot paths (use coroutines for movement)
- No hardcoded gameplay values in methods — use serialized fields configurable in Inspector

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- TextMesh Pro 3.0.7 (already integrated — UI text rendering)
- Unity 2D Feature Set 2.0.1 (already integrated — sprite/animation/tilemap)

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- [No ADRs yet — use /architecture-decision to create one]

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: unity-specialist
- **Language/Code Specialist**: unity-specialist (C# review — primary covers it)
- **Shader Specialist**: unity-shader-specialist (Shader Graph, HLSL, URP/HDRP materials — if needed)
- **UI Specialist**: unity-ui-specialist (UI Toolkit UXML/USS, UGUI Canvas, runtime UI)
- **Additional Specialists**: unity-dots-specialist (ECS, Jobs system, Burst compiler), unity-addressables-specialist (asset loading, memory management, content catalogs)
- **Routing Notes**: Invoke primary for architecture and general C# code review. Invoke UI specialist for all interface implementation (uGUI + TextMesh Pro). Shader and DOTS specialists available if needed for future features.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (.cs files) | unity-specialist |
| Shader / material files (.shader, .shadergraph, .mat) | unity-shader-specialist |
| UI / screen files (.uxml, .uss, Canvas prefabs) | unity-ui-specialist |
| Scene / prefab / level files (.unity, .prefab) | unity-specialist |
| Native extension / plugin files (.dll, native plugins) | unity-specialist |
| General architecture review | unity-specialist |
