# Foodular1 — Demo 游戏框架

> **文档类型**: 框架设计 + 资源需求  
> **创建日期**: 2026-07-22  
> **状态**: 核心运行时已完成 ✅ | 表现资产补齐进行中 🚧  
> **当前口径**: 本文最初是 2026-07-22 的框架草案；当前实现以
> `RaceSession`、`TrackRuntimeContext`、`RaceUIFactory` 和实际 Assets 目录为准。
> 本文中的“计划新增”脚本/预制体不代表已经存在。  

---

## 一、架构总览

```
Assets/
├── Scripts/                    # C# 游戏逻辑
│   ├── Core/                   # 比赛编排、状态与纯规则聚合
│   ├── Gameplay/               # 玩法系统
│   ├── AI/                     # AI 系统
│   ├── UI/                     # UI 系统
│   └── Config/                 # ScriptableObject 配置
│
├── Prefabs/                    # Unity 预制体
│   ├── UI/                     # UI 预制体（Canvas 面板、按钮、卡牌）
│   ├── Cars/                   # 赛车预制体（6 辆）
│   ├── Track/                  # 赛道预制体（节点、弯心标记）
│   └── Effects/                # 特效预制体
│
├── Sprites/                    # 精灵图资源
│   ├── UI/                     # UI 精灵（面板、按钮、图标）
│   ├── Cars/                   # 赛车精灵（各车队）
│   ├── Track/                  # 赛道精灵
│   ├── Cards/                  # 卡牌精灵
│   └── Effects/                # 特效精灵
│
├── Scenes/                     # Unity 场景
│   ├── MainMenu.unity          # 主菜单
│   ├── Race.unity              # 比赛场景（核心）
│
├── Audio/                      # 音频资源
│   ├── Music/
│   └── SFX/
│
├── Fonts/                      # 字体资源
│
└── Resources/                  # 动态加载资源
    └── Configs/                # 运行时配置
```

---

## 二、脚本架构

### 2.1 现有脚本 → 新位置映射

| 现有文件 | → 新位置 | 重构说明 |
|---------|---------|---------|
| `MVPGameManager.cs` | `Scripts/Core/MVPGameManager.cs` | 当前仍是 Unity 比赛编排器；逐步抽取职责，不改名为 `GameManager` |
| `CardDeck.cs` | `Scripts/Core/CardDeck.cs` | 保持，已纯 C# |
| `CardData.cs` | `Scripts/Core/CardData.cs` | 保持 |
| `PlayerState.cs` | `Scripts/Core/PlayerState.cs` | 保持 |
| `HeatPool.cs` | `Scripts/Core/HeatPool.cs` | 从 CardDeck.cs 中独立出来 |
| `TrackManager.cs` | `Scripts/Gameplay/TrackManager.cs` | 重构，数据驱动赛道 |
| `AIController.cs` | `Scripts/AI/AIController.cs` | 保持 |
| `GameConfigSO.cs` | `Scripts/Config/GameConfigSO.cs` | 当前保留为比赛配置；赛道 JSON 派生数据不再回写此对象 |
| `CardUI.cs` | `Scripts/UI/CardUI.cs` | 重构，支持 sprite |
| `CardHandUI.cs` | `Scripts/UI/CardHandUI.cs` | 重构，Canvas 预制体 |
| `HUDUI.cs` | `Scripts/UI/HUDUI.cs` | 重构，Canvas 预制体 |
| `TrackNode.cs` | `Scripts/Gameplay/TrackNode.cs` | 保持 |

### 2.2 需要新增的脚本

| 文件 | 位置 | 职责 |
|------|------|------|
| `RaceManager.cs` | `Scripts/Core/` | 比赛流程管理（从 GameManager 分离） |
| `InputManager.cs` | `Scripts/Core/` | 输入管理（键盘/鼠标） |
| `CarEntity.cs` | `Scripts/Gameplay/` | 赛车实体（MonoBehaviour，挂载到赛车 Prefab） |
| `CarStats.cs` | `Scripts/Gameplay/` | 赛车属性配置（纯数据） |
| `TrackDataSO.cs` | `Scripts/Config/` | 赛道数据 ScriptableObject |
| `CarConfigSO.cs` | `Scripts/Config/` | 赛车配置 ScriptableObject |
| `DriverConfigSO.cs` | `Scripts/Config/` | 车手配置 ScriptableObject |
| `TechTreeConfigSO.cs` | `Scripts/Config/` | 旧计划项；当前科技树由 `TechTreeDatabase`/`TechTreeProfileStore` 提供 |
| `UIPanel.cs` | `Scripts/UI/` | UI 面板基类 |
| `MainMenuUI.cs` | `Scripts/UI/` | 主菜单 |
| `GarageUI.cs` | `Scripts/UI/` | 旧计划项；当前由车手选择面板间接确定车队 |
| `ResultsUI.cs` | `Scripts/UI/` | 比赛结果面板 |
| `AIDriverProfile.cs` | `Scripts/AI/` | AI 车手个性配置 |
| `AudioManager.cs` | `Scripts/Core/` | 音效/音乐管理 |
| `SceneLoader.cs` | `Scripts/Core/` | 场景切换 |

---

## 三、UI 系统框架

当前 UI 由 `RaceUIFactory` 负责构建：优先复用场景/Prefab 引用，缺失时回退到程序化 Canvas/UI。两条路径都是现行兼容方案，不能再按“必须移除 AutoCreateUI”的旧计划理解。

### 3.1 UI Canvas 层级

```
RaceCanvas (Canvas, Screen Space - Overlay)
├── TopBar
│   ├── LapText (圈数/总圈数)
│   ├── PositionText (当前名次)
│   └── WeatherIcon (天气图标)
│
├── RightPanel
│   ├── GearIndicator (档位旋钮)
│   ├── HeatMeter (热量温度计)
│   ├── SpinCounter (失控计数器)
│   └── DriverPortrait (车手头像)
│
├── BottomBar (手牌区)
│   ├── CardSlot_0..6 (7 个卡牌槽位)
│   └── DeckInfoText (牌堆信息)
│
├── GearSelectionPanel (档位选择)
│   ├── GearButton_G1..G4
│   └── ConfirmGearButton
│
├── ActionButtons
│   ├── PlayButton
│   └── ResetButton
│
├── LogPanel (左下角日志)
│   └── LogText
│
├── GameOverPanel (比赛结束，默认隐藏)
│   ├── ResultText
│   ├── RankingList
│   └── BackToMenuButton
│
└── StatusText (顶部居中状态提示)
```

### 3.2 UI 组件规范

| 组件 | 类型 | 说明 |
|------|------|------|
| GearIndicator | Image + TMP | 圆形旋钮风格，弧形排列 G1-G4，当前档位高亮 |
| HeatMeter | Image(Filled) | 垂直温度计，引擎/手牌双轨显示 |
| CardSlot | Button + Image + TMP | 卡牌槽位，支持选中/未选中/不可选三态 |
| GearButton | Button + TMP | 方形按钮，选中态绿色，禁止态灰色(+2档需1热提示) |
| DriverPortrait | Image | 圆形头像框 + 国旗图标 + 车手名 |

---

## 四、资产需求清单

### 4.1 卡牌精灵

| 资产 | 文件名 | 规格 | 说明 |
|------|--------|------|------|
| 速度牌底图 | `card_speed_bg.png` | 256×384, PNG | 速度牌通用底图，科技蓝边框，圆角 |
| 热量牌底图 | `card_heat_bg.png` | 256×384, PNG | 热量牌底图，暗橙/红棕色调，"沉重"感 |
| 速度牌高亮 | `card_selected_overlay.png` | 256×384, PNG | 当前实现的选中态叠加；旧规划名为 `card_speed_selected.png` |
| 卡牌背图 | `card_back.png` | 256×384, PNG | 牌组背面，赛车主题 |
| 数字 1-4 | `card_num_1..4.png` | 128×128, PNG | 速度牌中央大号数字，自定义风格字体 |
| 热量图标 | `card_heat_icon.png` | 128×128, PNG | 火焰简化图标 |

**设计要求**: 参考 `foodula-1-visual-style.md` §5 卡牌视觉设计。保留桌游实体卡质感：轻微圆角（8px）、细边框、阴影。具体色值见 `foodula-1-visual-style.md` §2.2。

### 4.2 UI 精灵

| 资产 | 文件名 | 规格 | 说明 |
|------|--------|------|------|
| 档位旋钮底 | `gear_knob_bg.png` | 200×200, PNG | 圆形，炉灶旋钮风格的扁平化版本 |
| 档位旋钮指针 | `gear_knob_pointer.png` | 200×200, PNG | 指针层，旋转到当前档位方向 |
| 热量温度计 | `heat_meter_bg.png` | 48×256, PNG | 垂直柱状，蓝→红渐变 |
| 热量温度计填充 | `heat_meter_fill.png` | 44×252, PNG(9-slice) | Filled Image 用 |
| 面板底图 | `panel_bg.png` | 动态尺寸, 9-slice | 暗灰蓝 `#161B22`，细线边框 `#30363D` |
| 按钮常态 | `btn_normal.png` | 动态尺寸, 9-slice | 白色半透明，细边框 |
| 按钮悬浮 | `btn_hover.png` | 同上 | 边框变亮 |
| 按钮按下 | `btn_pressed.png` | 同上 | 填充 |
| 国旗图标 ×6 | `flag_uk/de/it/us/cn/jp.png` | 64×64, PNG | 六国国旗，圆形裁切 |

### 4.3 赛车精灵

| 资产 | 文件名 | 规格 | 说明 |
|------|--------|------|------|
| 英国炸鱼薯条赛车 | `car_uk.png` | 256×128, PNG | 金黄车身+薯条尾翼，参考 visual-style §3.2 |
| 德国啤酒黑面包赛车 | `car_de.png` | 256×128, PNG | 银灰+深棕，碱水面包防滚架 |
| 意大利意面披萨赛车 | `car_it.png` | 256×128, PNG | 法拉利红+芝士白顶盖 |
| 美国汉堡可乐赛车 | `car_us.png` | 256×128, PNG | 可乐红+双层肉饼 |
| 中国电动点心赛车 | `car_cn.png` | 256×128, PNG | 瓷白蒸笼+翡翠绿点缀 |
| 日本寿司拉面赛车 | `car_jp.png` | 256×128, PNG | 玄黑海苔+彩色截面 |

**设计要求**: 俯视图（赛道从上方看）。手绘质感（Hand-Painted），非 PBR 写实。多边形面数 ~2000-4000 tri（如果用 3D）或 256px 宽精灵图（如果用 2D）。当前 Demo 用 2D 精灵。

### 4.4 赛道精灵

| 资产 | 文件名 | 规格 | 说明 |
|------|--------|------|------|
| 赛道节点（直道）| `track_straight.png` | 32×32, PNG | 灰色圆点/方块 |
| 赛道节点（弯心）| `track_apex.png` | 32×32, PNG | 红色圆点+限速数字 |
| 起终点线 | `track_start_finish.png` | 32×32, PNG | 绿色+方格旗图案 |
| 赛道布局底图 | `track_layout_*.png` | 运行时按国家/补充赛道复用 | 当前已有 8 张布局图；旧规划名 `track_bg_demo.png` 不再作为实际文件名 |

**替代方案**: 赛道可用 LineRenderer 画线（现有方案），节点用简单精灵标记。后期切换到完整赛道底图。

### 4.5 特效精灵

| 资产 | 文件名 | 规格 | 说明 |
|------|--------|------|------|
| 尾流虚线箭头 | `fx_slipstream.png` | 64×32, PNG | 科技蓝虚线箭头，参考 visual-style §6.1 |
| 失控旋转帧 | `fx_spin_*.png` | 128×128, PNG, 4 帧 | 赛车旋转动画序列 |
| 弯道判定脉冲 | `fx_corner_flash.png` | 32×32, PNG | 红色脉冲圈 |
| 冷却粒子 | `fx_cool.png` | 16×16, PNG | 蓝色光点，飘出效果 |

### 4.6 音频需求（后续）

| 资产 | 文件名 | 格式 | 说明 |
|------|--------|------|------|
| BGM 比赛 | `bgm_race.ogg` | OGG, loop | 节奏感强，非干扰性 |
| BGM 菜单 | `bgm_menu.ogg` | OGG, loop | 轻松 |
| SFX 选牌 | `sfx_card_select.wav` | WAV | 轻微咔嗒声 |
| SFX 出牌 | `sfx_card_play.wav` | WAV | 刷刷声 |
| SFX 过弯 | `sfx_corner.wav` | WAV | 弯道判定音效 |
| SFX 失控 | `sfx_spin.wav` | WAV | 轮胎尖叫+撞击 |
| SFX 完赛 | `sfx_finish.wav` | WAV | 欢呼/旗帜 |

---

## 五、场景设计

### 5.1 Race.unity — 比赛场景（核心）

```
Main Camera (Orthographic, Size 自适应赛道)
└── Background (深灰底色)

RaceCanvas (Screen Space - Overlay)
└── [见 §3.1 层级]

TrackContainer (World Space)
├── TrackLine (LineRenderer)
├── NodeMarkers (实例化 TrackNode Prefab  × N)
├── CarInstances (实例化 CarPrefab × 玩家数)
└── CornerLabels (限速标签 TMP)

GameManager (MonoBehaviour)
├── TrackManager
├── AIController
└── AudioManager
```

### 5.2 MainMenu.unity — 主菜单

```
- 标题: "Foodular 1" (大号 TMP)
- 快速比赛 按钮 → Race.unity
- 车手选择面板 → 选定车手并间接确定车队；独立 Garage 场景尚未实现
- 退出 按钮
- 背景: 赛道剪影 + 动画赛车
```

---

## 六、配置数据架构

### 6.1 TrackDataSO（赛道配置）

```csharp
[CreateAssetMenu(menuName = "Foodular1/Track Config")]
public class TrackDataSO : ScriptableObject
{
    public string trackName;
    public string trackNameEn;
    public int totalLaps;
    public Vector2[] nodePositions;         // 节点坐标
    public int[] apexNodeIndices;           // 弯心节点索引
    public int[] laneCornerSpeedLimits;     // 各车道弯心限速（当前 TrackConfig 字段）
    public string[] cornerNames;            // 弯心名称
    public int startFinishIndex;            // 起点/终点索引
}
```

### 6.2 CarConfigSO（赛车配置）

```csharp
[CreateAssetMenu(menuName = "Foodular1/Car Config")]
public class CarConfigSO : ScriptableObject
{
    public string carName;
    public string country;
    public Sprite carSprite;
    public int topSpeed;    // 极速加成
    public int accel;       // 加速加成
    public int handling;    // 操控加成（弯道限速 +N）
    public int cooling;     // 冷却效率加成
    public int durability;  // 耐久（影响失控容错？）
    public int slipstream;  // 尾流效率加成
}
```

### 6.3 DriverConfigSO（车手配置 — 后续）

```csharp
[CreateAssetMenu(menuName = "Foodular1/Driver Config")]
public class DriverConfigSO : ScriptableObject
{
    public string driverName;
    public string country;
    public Sprite portrait;
    public DriverStyle style;         // Aggressive/Balanced/Technical
    public PassiveSkill passive;
    public ActiveSkill signature;
}
```

---

## 七、实施路线图

### Phase 1 — 框架搭建 ✅（2026-07-25 完成）
- [x] 建立 `Assets/` 目录结构
- [x] 创建 RaceCanvas Prefab（UI 层级）
- [x] 重命名 + 移动现有脚本到新目录（13 脚本迁移，GUID 保留）
- [x] Scripts/ 拆分为 Core/Gameplay/AI/UI/Config + Editor
- [x] RaceCanvas Prefab 生成（Editor 工具: Foodular1 → Build RaceCanvas Prefab）
- [ ] ~~拆分 GameConfigSO → TrackDataSO + CarConfigSO~~ → 当前 JSON 赛道 + `GameConfigSO` 配置方案已取代该计划
- [ ] ~~GameManager 分割：RaceManager + InputManager~~ → 当前保留 `MVPGameManager` 编排器，继续渐进式抽取

### Phase 2 — 资源替换与表现补齐
- [x] 卡牌精灵与赛车精灵已接入
- [x] 八条可选赛道背景已接入
- [ ] UI 面板、档位和热量专用图形继续替换程序化占位
- [x] 中文 TMP 字体已接入；其他字体变体按表现需求补充

### Phase 3 — 功能补全
- [x] 可配置多 AI 参与者（`aiOpponentCount` 0–3）
- [x] 尾流机制
- [x] 车队属性与科技树运行时接线
- [x] 车手选择界面；独立车队选择场景仍未实现

### Phase 4 — 打磨
- [ ] 音效
- [ ] 更完整动画（卡牌飞出、赛车移动弹跳、失控旋转素材）
- [x] 天气系统规则与 HUD 接入；天气专用视觉表现仍待补齐

---

> 📄 关联文档: `design/gdd/foodula-1-visual-style.md`（视觉规范）、`design/gdd/foodula-1-core-mechanics.md`（规则）、`design/gdd/foodula-1-teams-cars.md`（车队数据）、`design/planning/asset-manifest.md`（资产清单）、`design/planning/ai-art-prompts.md`（AI 生成 Prompt）
