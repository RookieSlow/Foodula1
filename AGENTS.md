# Codex Game Studios -- Game Studio Agent Architecture

Indie game development managed through 49 coordinated Codex subagents.
Each agent owns a specific domain, enforcing separation of concerns and quality.

## Technology Stack

- **Engine**: Unity 2022.3.62f3c1
- **Language**: C# 9.0
- **Version Control**: Git with trunk-based development
- **Build System**: Unity Build Pipeline
- **Asset Pipeline**: Unity Asset Import Pipeline + TextMesh Pro
- **Rendering**: Built-in Render Pipeline (2D project)
- **UI Framework**: uGUI (Unity UI) + TextMesh Pro
- **Input**: Legacy Input Manager (mouse click)

> **Note**: Engine-specialist agents configured for Unity. Use `unity-specialist`
> and its sub-specialists (unity-shader-specialist, unity-ui-specialist, etc.)
> for engine-specific work.

## Project Structure

@.Codex/docs/directory-structure.md

## Engine Version Reference

@docs/engine-reference/unity/VERSION.md

## Technical Preferences

@.Codex/docs/technical-preferences.md

## Coordination Rules

@.Codex/docs/coordination-rules.md

## Collaboration Protocol

**User-driven collaboration, not autonomous execution.**
Every task follows: **Question -> Options -> Decision -> Draft -> Approval**

- Agents MUST ask "May I write this to [filepath]?" before using Write/Edit tools
- Agents MUST show drafts or summaries before requesting approval
- Multi-file changes require explicit approval for the full changeset
- No commits without user instruction

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for full protocol and examples.

> **First session?** If the project has no engine configured and no game concept,
> run `/start` to begin the guided onboarding flow.

## Coding Standards

@.Codex/docs/coding-standards.md

## Context Management

@.Codex/docs/context-management.md

## Project Continuity

Before planning, reviewing, or implementing project work, read the following
project-local continuity documents:

@docs/memory/project-overview.md

@docs/memory/game-mechanics.md

@docs/memory/current-task-list.md

@production/session-state/active.md

The files under `.claude/agent-memory/` and
`production/session-logs/session-log.md` are historical Claude Code sources.
Use them for provenance, but prefer the maintained documents under
`docs/memory/` when information conflicts.
