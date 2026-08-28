# Foodula1 — Demo 游戏框架

> **文档类型**：当前框架、场景边界与资源入口
> **创建日期**：2026-07-22
> **实现快照**：2026-08-27
> **状态**：核心 Demo 已实现，进入人工验收与封版准备。历史目标重构不再作为当前缺失项。

---

## 一、当前项目结构

```
Assets/
├── Scripts/
│   ├── Core/          # 比赛编排、状态、规则门面、日志分析
│   ├── Gameplay/      # 赛道、车辆表现、维修区、赛事特效
│   ├── AI/            # AIController 与纯规划规则
│   ├── UI/            # 主菜单、比赛 HUD、卡牌、牌堆、科技树
│   ├── Config/        # GameConfigSO 与运行时配置
│   ├── Settings/      # 版本化玩家设置、PlayerPrefs 适配与运行时应用
│   ├── Drivers/       # 12 位车手目录与成长规则
│   ├── TechTree/      # 科技数据库、规则与持久化
│   ├── Tutorial/      # 隔离教程配置、精确脚本与纯状态机
│   └── TrickCards/    # 车队特技牌数据与规则
├── Prefab/            # 既有卡牌、赛车和节点 Prefab
├── Prefabs/UI/        # 当前权威 RaceCanvas.prefab
├── Sprites/
│   ├── Cards/         # 已接入卡牌资源
│   ├── Cars/          # 已接入六队赛车资源
│   ├── Track/         # 已接入八张 4K 赛道图
│   └── UI/            # 当前档位旋钮；其余多为运行时绘制
├── Scenes/
│   ├── MainMenu.unity
│   └── Race.unity
├── Resources/Configs/Tracks/  # 8 条官方 JSON + fallback_42
├── ttf/                       # 字体源文件
└── TmpFont/                   # TMP 字体资产
```

`Assets/Audio/Music/` 与 `Assets/Audio/SFX/` 当前只有目录和 `.meta`，
没有实际音频文件；应在音频工作包开始时按
`design/gdd/foodula-1-audio-style.md` 接入。

---

## 二、当前脚本架构

| 模块 | 当前入口 | 职责 |
|---|---|---|
| 比赛编排 | `Core/MVPGameManager.cs` | 协调回合阶段、UI、表现与规则服务；不是未来重命名任务 |
| 会话聚合 | `Core/RaceSession.cs` | 聚合天气、维修区、特技、科技和尾流等纯规则调用 |
| 卡牌/热量 | `Core/CardDeck.cs`、`CardData.cs`、`PlayerState.cs` | 牌区所有权、引擎热量与比赛状态 |
| 赛道 | `Gameplay/TrackManager.cs`、`TrackDataLoader.cs` | JSON 加载、运行时节点、背景与遮罩 |
| AI | `AI/AIController.cs`、`AIPlanner.cs` | 可重复的热量/弯道/尾流规划 |
| UI | `Prefabs/UI/RaceCanvas.prefab`、`UI/RaceUIFactory.cs` | Prefab 优先，旧场景回退路径 |
| 科技树 | `TechTree/`、`UI/TechTreeUI.cs` | RP、解锁/激活、持久化和比赛钩子 |
| 车手 | `Drivers/DriverData.cs`、`UI/DriverSelectionUI.cs` | 12 位车手目录、XP/等级与选择 |
| 表现 | `Gameplay/RaceEventFX.cs`、`CarMovementAnimator.cs` | 卡牌/车辆/尾流/失控等视觉反馈，不改变规则 |
| 日志 | `Core/RaceTestLogWriter.cs`、`RaceLogAnalyzer.cs` | 人工对局证据采集与结构分析 |
| 教程运行时 | `Tutorial/TutorialScenarioDefinition.cs`、`TutorialCheckpointRules.cs`、`TutorialStateMachine.cs`、`TutorialRuntimeDirector.cs`、`TutorialPracticeRules.cs`、`TutorialGuideUI.cs`、`TutorialFocusHighlightUI.cs`、`TutorialOverlayAuthoring.cs`、`Editor/TutorialOverlayAuthoringEditor.cs`、`Resources/Prefabs/UI/TutorialOverlay.prefab` | 勒芒/UK 隔离 Race、精确牌序、16 步可视化编辑/专用 Inspector 非 Play Mode 预览/只读校验与门控、可人工布局的指引 Prefab、13 类机制聚光、上一项成功反馈、8 个安全边界检查点、天气/尾流 cue、虚拟维修规则视图及一圈练习日志 |
| 玩家设置 | `Settings/GameSettingsData.cs`、`GameSettingsStore.cs`、`GameSettingsRuntime.cs`、`UI/GameSettingsUI.cs` | 版本化持久化显示/分辨率/动画/教程偏好；音量为诚实预留数据，待 AudioMixer 接入 |
| 游戏百科 | `Encyclopedia/EncyclopediaCatalog.cs`、`Resources/Configs/encyclopedia_zh.json`、`UI/GameEncyclopediaUI.cs` | 版本化规则条目、必需主题/重复 ID 校验、运行时目录追踪及设置内滚动阅读 |

### 尚未实现但明确需要的模块

| 模块 | 优先级 | 边界 |
|---|---|---|
| 音频服务 + AudioMixer | P1 | 音乐/音效事件路由、混音、限频、设置持久化 |
| 教程最终验收 | P0T | 引导、渐进式步骤说明、逐机制聚光/检查点、虚拟维修区、一圈练习、设置和百科已实现；完整 Play Mode、聚光边界/面板遮挡与 Quick Race 人工防回归待收口 |
| 车手签名技能执行层 | P2 | 当前仅有目录、成长与选择；属于 Demo 后功能扩展 |
| 难度/手柄/比赛中途存档 | P3 | 不属于当前 Demo 验收阻塞项 |

`RaceManager`、`InputManager`、`TrackDataSO`、`CarConfigSO`、
`DriverConfigSO`、`Garage.unity` 等旧目标只有在后续需求证明现架构不足时再提 ADR；
它们不是“为了完成 Demo 必须创建”的文件。

---

## 三、UI 框架

### 3.1 比赛 HUD

`RaceCanvas.prefab` 是当前可视化编辑权威，运行时保留人工设置的 RectTransform；
只有旧 Canvas 缺少关键面板时才由 `RaceUIFactory` 补齐。

当前 HUD 由四个主要区域组成：

- 顶部/赛道信息：圈数、排名、天气、阶段提示和迷你地图。
- 左侧操作栏：档位/模式、确认/重置、返回主菜单与事件日志。
- 右侧资源栏：引擎热量、牌堆/弃牌堆缩略和精确数量。
- 底部手牌区：卡牌高亮、打出/弃牌与热量流转动画。

比赛中的赛车上方使用运行时车队代码 + 名次徽标。正式六队徽章和车手头像接入后，
应替换图形内容但保留当前名次和正向显示规则。

### 3.2 主菜单

当前 `MainMenu.unity` 提供：

- Foodula1 标题、开始比赛、车库/配置入口、科技树和退出。
- 新手教程/重播入口与设置入口；设置覆盖层支持显示、分辨率、动画/减少动态、教程重置，
  并可打开 17 条数据驱动游戏百科的滚动阅读器。
- 赛道、车队、车手选择与科技树配置的运行时面板。
- 纯色深色背景和 TMP 文字。

正式主菜单背景、Logo、车手头像、车队徽章和科技树背景尚缺；清单见
`design/planning/asset-manifest.md`。

---

## 四、场景与数据

### 4.1 Race.unity

- 主相机 + 固定全图小地图相机。
- `TrackManager` 按选择的 JSON 构建运行时赛道，`TrackEnvironmentController`
  选择对应 4K 背景。
- 赛车按赛道切线转向，并以逐格跳跃表现移动。
- `RaceCanvas.prefab` 负责 HUD；`MVPGameManager` 编排比赛阶段。

### 4.2 MainMenu.unity

- 负责快速比赛入口与赛前配置。
- 不依赖独立 `Garage.unity` 才能完成 Demo 流程。
- 正式背景图和品牌资源尚未接入。

### 4.3 当前数据权威

| 数据 | 当前权威 |
|---|---|
| 赛道格数、弯道、维修区、天气、圈数 | `Assets/Resources/Configs/Tracks/*.json` |
| 比赛参数、AI 数量、动画时间 | `GameConfigSO` 及其资产 |
| 六队车辆与机制 | `TeamVehicleRules`、科技/特技数据库 |
| 十二位车手 | `DriverCatalog` |
| 科技树 | `TechTreeDatabaseFactory` + `TechTreeProfileStore` |

不要依据旧版 42 格示意图或废弃的 ScriptableObject 草案覆盖当前数据。

---

## 五、资源状态

### 已完成

- 9 张核心卡牌图。
- 6 辆车队赛车图。
- 8 张 3840×2160 官方赛道布局图。
- 档位旋钮、中文 TMP 字体、RaceCanvas HUD。
- 运行时卡牌流转、牌堆层数、赛道标识、逐格移动、失控和尾流阶段表现。

### 仍缺

- 主菜单背景与 Foodula1 Logo。
- 六队徽章/国旗、12 位车手头像。
- 科技树背景、节点三态与层级徽章。
- 赛道选择缩略图（可从现有 4K 图派生）。
- 天气/结果/特技牌等 P1 美术。
- 全部音乐、音效、AudioMixer 和音量设置。

完整文件名、规格和优先级只在
`design/planning/asset-manifest.md` 维护，避免与本框架重复漂移。

---

## 六、下一阶段

### Phase A — Demo 验收与冻结

- 完整比赛日志与高风险机制场景验收。
- 多车排名、引擎量表、维修区、天气、尾流和结果返回验收。
- 1920×1080 / 2560×1440 视觉检查。
- 定向 + 全量 EditMode 回归，记录 Console 和日志证据。

### Phase B — 视觉身份包

- 主菜单、Logo、车队、车手、科技树和赛道缩略图。
- 接入后替换运行时占位图形，但不改变规则层。

### Phase C — 核心音频包

- AudioMixer、音频服务与音量设置。
- 菜单/比赛音乐和核心玩法音效。
- 通过实际比赛阶段与日志验证播放时机。

### Phase D — Demo 后扩展

- 车手签名技能、多 AI 难度、独立车库/生涯、平台适配和更多内容。

---

> 关联：`design/gdd/systems-index.md`、`design/planning/roadmap.md`、
> `design/planning/asset-manifest.md`、`design/gdd/foodula-1-visual-style.md`、
> `design/gdd/foodula-1-audio-style.md`。
