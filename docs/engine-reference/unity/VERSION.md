# Unity Engine — Version Reference

| Field | Value |
|-------|-------|
| **Engine Version** | Unity 2022.3.62f2 |
| **Release Date** | 2022 LTS stream, f2 patch |
| **Project Pinned** | 2026-07-19 |
| **Last Docs Verified** | 2026-07-19 |
| **LLM Knowledge Cutoff** | May 2025 |
| **Risk Level** | LOW — version is within LLM training data |

## Note

This engine version (Unity 2022 LTS) is within the LLM's training data.
Engine reference docs are lightweight — agents can rely on their training
for most Unity 2022.x APIs.

## Project-Specific Configuration

- **Template**: 2D (com.unity.template.2d@7.0.4)
- **Scripting Backend**: Mono
- **API Compatibility**: .NET Standard 2.1
- **Color Space**: Gamma
- **Target Platform**: Standalone Windows 64-bit
- **Input Handler**: Legacy Input Manager
- **Renderer**: Forward Rendering (Built-in)

## Key Packages

| Package | Version | Purpose |
|---------|---------|---------|
| TextMesh Pro | 3.0.7 | UI text rendering |
| 2D Feature Set | 2.0.1 | 2D sprite/animation/tilemap |
| UGUI | 1.0.0 | Unity UI system |

## Verified Sources

- Official docs: https://docs.unity3d.com/2022.3/Documentation/Manual/index.html
- C# API reference: https://docs.unity3d.com/2022.3/Documentation/ScriptReference/index.html

Run `/setup-engine refresh` to populate full reference docs if needed for newer Unity versions.
