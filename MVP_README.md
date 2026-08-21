# Foodular1 — 当前项目说明

> **当前基线**：Unity `2022.3.62f2c1`，当前实现与文档校准日期：2026-08-21。  
> 本文件替代旧版“42 格 SampleScene 单场原型”说明；旧原型规则只作为历史参考，不作为当前验收口径。

## 项目定位

Foodular1 是一款以 HEAT 桌游为规则参考的卡牌驱动策略竞速游戏。当前 Demo 已从早期单赛道原型扩展为可选择赛道、车队、车手和科技配置的单场比赛：默认 1 名玩家对 1 名 AI，也支持在 `GameConfigSO.aiOpponentCount` 中配置 0–3 名 AI。

核心循环是：选档 → 抽牌/出牌 → 移动 → 弯心超速判定 → 热量管理 → 结算。规则层已拆出为可测试的纯 C# 规则模块，Unity 场景负责输入、表现和编排。

## 当前已接入

| 系统 | 当前状态 |
|---|---|
| 档位、速度牌、热量牌与牌库循环 | 已接入并有 EditMode 覆盖 |
| 弯心判定、起终点/圈数、排名 | 已接入；按 `isApex` 去重判定 |
| 赛道 JSON | 9 条定义；8 条可从菜单选择，另有隐藏 `fallback_42` 兼容资源 |
| 赛道表现 | 8 张布局图、LineRenderer、节点/车道/小地图和天气 HUD 已接入 |
| 维修区 | 当前仅上海 JSON 配置维修区节点；所有符合条件的玩家/AI均可选择标准进站 |
| 车队与赛车 | 6 支车队、车辆属性、车队特技牌和科技修正已接入 |
| 车手 | 12 人目录、XP/等级规则和主菜单选择已接入；被动/签名技能效果尚未全部接线 |
| 科技树 | 约 36 个节点、RP、持久化档案、赛前激活和比赛钩子已接入 |
| AI | 确定性规划器、车队/科技效果和中国 Go/Recover 逻辑已接入；人格/难度仍待扩展 |
| MCP | Unity MCP `10.1.2` 已连接本机 `127.0.0.1:8080` |

## 启动与验证

1. 用 Unity Hub 打开 `D:\unityhub\project\Foodula1`。
2. 使用 Unity `2022.3.62f2c1`。
3. 打开 `Assets/Scenes/MainMenu.unity`，点击 Play。
4. 在主菜单选择车手、赛道和科技配置，再进入 `Assets/Scenes/Race.unity`。

赛道选择由 `TrackSelectionState` 管理；JSON 从 `Assets/Resources/Configs/Tracks/` 加载。`GameConfigSO` 的 `trackId` 和 `totalLaps` 主要服务于编辑器直连/硬编码兼容路径，JSON 赛道优先使用自身的 `laps` 与 `gameCellCount`。

## 当前验证口径

- Unity 编辑器当前可正常打开，当前 C# 编译无错误。
- 维护记录中的最近一次 EditMode 全量结果为 **399/399 通过**；本轮文档同步未重新运行 Test Runner。
- 本机当前未安装 .NET SDK，因此不能把旧日志中的 `dotnet build 0 errors` 当作本机当前可复现结果。

## 尚未完成的工作

- 车手被动技能与主动签名特技的实际比赛结算。
- AI 人格、难度档位、车手技能行为和完整多 AI PlayMode 矩阵。
- 补充赛道（勒芒、纽博格林北环）的动态天气池设计。
- UI 专用美术、完整动画、音效和更多视觉特效。

## 主要文档

| 文档 | 用途 |
|---|---|
| `design/gdd/foodula-1-concept.md` | 当前主概念与范围 |
| `design/gdd/foodula-1-core-mechanics.md` | 当前卡牌、档位、热量与回合规则 |
| `design/gdd/foodula-1-tracks.md` | 国家主赛道与补充赛道设计/JSON 口径 |
| `design/gdd/foodula-1-teams-cars.md` | 车队、车辆与中国车当前规则 |
| `design/gdd/foodula-1-ai.md` | 当前 AI 实现与后续能力边界 |
| `design/planning/asset-manifest.md` | 实际资产库存与待补表现资产 |
| `docs/track-system-readme.md` | 赛道 JSON、加载和运行时集成说明 |
| `production/session-state/active.md` | 当前维护状态与历史验证记录 |
