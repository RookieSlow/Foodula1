# Foodular1 — 美术与表现资产清单

> **用途**：记录当前仓库中真实存在、已接入或明确延期的表现资产。  
> **最后核对**：2026-08-21  
> **权威范围**：本清单反映当前 Unity 项目；原始 58 项制作计划保留在 Git 历史中，
> 不再作为完成度判断依据。

## 当前结论

核心 Demo 表现已经不再是“全部待制作”：卡牌、六支车队赛车、八条可选赛道背景、
中文字体和 RaceCanvas 已在项目中。UI 面板、档位/热量专用图形、音频和粒子帧仍未齐全，
当前由程序化 UI、TMP 文本、LineRenderer 和 `RaceEventFX` 的运行时表现兜底。

## 已存在并已接入

### 卡牌：`Assets/Sprites/Cards/`

| 资产 | 状态 | 说明 |
|---|---|---|
| `card_speed_bg.png` | ✅ | 速度牌底图 |
| `card_heat_bg.png` | ✅ | 热量牌底图 |
| `card_selected_overlay.png` | ✅ | 当前选中态；替代旧计划名 `card_speed_selected.png` |
| `card_back.png` | ✅ | 牌背 |
| `card_num_1.png` – `card_num_4.png` | ✅ | 速度数字图 |
| `card_heat_icon.png` | ✅ | 热量图标 |

### 赛车：`Assets/Sprites/Cars/`

| 资产 | 状态 |
|---|---|
| `car_uk.png`, `car_de.png`, `car_it.png` | ✅ |
| `car_us.png`, `car_cn.png`, `car_jp.png` | ✅ |

### 赛道表现：`Assets/Sprites/Track/`

| 资产 | 状态 | 说明 |
|---|---|---|
| `track_layout_uk.png` | ✅ | 银石 |
| `track_layout_de.png` | ✅ | 纽博格林 GP |
| `track_layout_de_endurance.png` | ✅ | 纽博格林 24h 补充赛道 |
| `track_layout_it.png` | ✅ | 蒙扎 |
| `track_layout_us.png` | ✅ | 印第安纳波利斯 |
| `track_layout_cn.png` | ✅ | 上海 |
| `track_layout_jp.png` | ✅ | 铃鹿 |
| `track_layout_fr_lemans.png` | ✅ | 勒芒补充赛道 |
| `track_surface_tile.png`, `track_curb_tile.png` | ✅ | 运行时赛道表面/路肩 |

### 字体与 UI 预制体

| 资产 | 状态 | 说明 |
|---|---|---|
| `Assets/ttf/futurab.ttf` | ✅ | 卡牌/标题字体来源 |
| `Assets/ttf/unispace bd.ttf` | ✅ | 科技感标题字体来源 |
| `Assets/TmpFont/` 下的 TMP 字体资源 | ✅ | 中文字体与 TMP 材质 |
| `Assets/Sprites/UI/gear_knob_bg.png` | ✅ | 档位旋钮底图；指针仍由 UI 绘制 |
| `Assets/Prefabs/UI/RaceCanvas.prefab` | ✅ | 当前唯一独立 Race UI 预制体 |

## 运行时生成或暂以占位方案实现

| 类别 | 当前实现 | 后续资产 |
|---|---|---|
| UI 面板/按钮 | 程序化 uGUI/TMP、默认颜色和文本 | `panel_bg`、按钮九宫格 |
| 档位控制 | 程序化按钮 + `gear_knob_bg` | 指针层和最终旋钮视觉 |
| 热量显示 | TMP 文本/程序化 HUD | 温度计底图与填充条 |
| 车队标识 | Emoji/文字 | 六支车队旗帜图标 |
| 赛道节点 | LineRenderer、运行时节点、弯道/弯心蒙版 | 独立节点精灵与标签预制体 |
| 赛车实体 | 运行时创建 GameObject + 车队 Sprite | `CarEntity` 独立预制体 |
| 卡牌槽位/档位按钮 | `RaceUIFactory` 或 RaceCanvas 层级 | 独立 CardSlot/GearButton 预制体 |
| 比赛事件表现 | `RaceEventFX` 运行时文本、旋转/慢放等表现 | 失控、超车、冷却等特效图层 |

## 尚未提供

### 特效：`Assets/Sprites/Effects/`

当前没有实际特效帧资产。尾流、失控、弯道提示和冷却表现暂由代码/UI 兜底。

### 音频：`Assets/Audio/Music/`、`Assets/Audio/SFX/`

当前没有 BGM 或 SFX 文件。音效系统属于 P3 打磨范围。

### 独立预制体

当前没有独立的赛车、赛道节点、卡牌槽位、档位按钮和 SpinEffect 预制体；
这些对象由场景、`RaceUIFactory` 或运行时构建逻辑生成。不要把原计划中的空预制体
条目误报为“已存在”。

## 运行时内容索引

| 内容 | 当前数量 | 来源 |
|---|---:|---|
| 可选赛道 | 8 | `TrackSelectionState.cs` |
| 赛道 JSON | 9 | `Assets/Resources/Configs/Tracks/`，含 `fallback_42` |
| 车队 | 6 | `TeamId` / `TeamVehicleRules` |
| 车手 | 12 | `DriverData.cs` |
| 特技牌 | 12 | `TrickCardRules.cs` |
| 科技节点 | 36 | `TechTreeDatabase.cs` |
| Unity 场景 | 2 | `MainMenu.unity`、`Race.unity` |

## 后续制作优先级

1. 完成 UI 面板、档位和热量视觉，替换程序化占位表现。
2. 为卡牌/车辆/赛道表现做目标分辨率复核。
3. 增加比赛事件特效和天气表现。
4. 最后接入 BGM、SFX 和更完整的动画反馈。

## 关联文档

- `design/planning/demo-framework.md`：运行时框架和表现层边界
- `design/gdd/foodula-1-visual-style.md`：视觉规范
- `design/planning/ai-art-prompts.md`：主赛道和资产生成提示词
- `docs/memory/project-overview.md`：项目当前状态
