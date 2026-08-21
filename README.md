# Foodular1

Foodular1 是一款基于 HEAT 桌游核心体验的 Unity 2D 卡牌竞速游戏：玩家通过选档、出牌、移动和热量管理，在不同主题赛道上与 AI 竞速。

## 当前状态

- Unity `2022.3.62f2c1`
- 主菜单 → 赛道/车手/科技配置 → Race 比赛流程已接通
- 9 条 JSON 赛道，其中 8 条可从菜单选择；`fallback_42` 为隐藏兼容资源
- 6 支车队、12 位车手、约 36 个科技树节点
- 默认 1 名 AI，可配置 0–3 名 AI
- Unity MCP `10.1.2` 已安装并连接本机 Unity 编辑器

当前剩余工作主要是车手技能效果、AI 人格/难度、补充赛道天气池，以及 UI 美术、动画、音效和特效打磨。

## 运行项目

1. 使用 Unity `2022.3.62f2c1` 打开项目目录。
2. 打开 `Assets/Scenes/MainMenu.unity`。
3. 点击 Play，在主菜单选择车手、赛道和科技配置。

## 主要目录

| 目录 | 内容 |
|---|---|
| `Assets/Scripts/` | Unity 游戏代码、纯规则模块和编辑器工具 |
| `Assets/Scenes/` | `MainMenu.unity`、`Race.unity` |
| `Assets/Resources/Configs/Tracks/` | 赛道 JSON |
| `Assets/Sprites/` | 卡牌、赛车和赛道布局资源 |
| `Assets/Tests/Editor/` | Unity EditMode 测试 |
| `design/gdd/` | 当前设计文档 |
| `design/planning/` | 路线图、资产清单和开发计划 |
| `docs/` | 技术说明、架构决策和维护记录 |
| `production/` | 阶段、里程碑和任务状态 |
| `tools/` | 平衡模拟和辅助工具 |

## 文档入口

- [当前项目说明](MVP_README.md)
- [主概念文档](design/gdd/foodula-1-concept.md)
- [核心机制](design/gdd/foodula-1-core-mechanics.md)
- [赛道设计](design/gdd/foodula-1-tracks.md)
- [车队与赛车](design/gdd/foodula-1-teams-cars.md)
- [开发路线图](design/planning/roadmap.md)
- [实际资产清单](design/planning/asset-manifest.md)
- [赛道系统说明](docs/track-system-readme.md)

## 验证记录

维护记录中的最近一次 Unity EditMode 全量结果为 `399/399` 通过；本次文档和仓库清理未重新运行 Test Runner。当前机器未安装 .NET SDK，旧的 `dotnet build` 成功记录仅作历史参考。

## 仓库规则

Unity 的 `Library/`、`Temp/`、`Logs/`、`UserSettings/`、IDE 配置、MCP 安装包和本地 PDF 资料已由 `.gitignore` 排除。提交 Unity 项目时应保留 `Assets/`、`Packages/`、`ProjectSettings/`、当前设计文档和必要的生产记录。
