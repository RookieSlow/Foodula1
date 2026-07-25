# Foodular1 — 美术资源清单

> **用途**: 美术资源制作和查找的目录索引  
> **关联**: `design/demo-framework.md` §4 资产需求清单  
> **状态**: 全部待制作（🚧 = 占位符可用，⬜ = 待制作）

---

## 资源目录总览

```
Assets/
├── Sprites/           ← 2D精灵图（卡牌、UI、赛车、赛道、特效）
├── Prefabs/           ← Unity预制体（按类型分子目录）
├── Audio/             ← 音频资源（Music / SFX）
├── Fonts/             ← 字体文件
└── Resources/Configs/ ← 运行时数据（JSON赛道）✅已完成
```

---

## 一、卡牌精灵 `Assets/Sprites/Cards/`

| # | 文件名 | 规格 | 描述 | 状态 |
|---|--------|------|------|------|
| 1 | `card_speed_bg.png` | 256×384 | 速度牌底图，科技蓝边框，圆角 | ⬜ |
| 2 | `card_heat_bg.png` | 256×384 | 热量牌底图，暗橙/红棕色调 | ⬜ |
| 3 | `card_speed_selected.png` | 256×384 | 选中态叠加，绿色半透明覆盖 | ⬜ |
| 4 | `card_back.png` | 256×384 | 牌背，赛车主题 | ⬜ |
| 5 | `card_num_1.png` | 128×128 | 速度数字 1 | ⬜ |
| 6 | `card_num_2.png` | 128×128 | 速度数字 2 | ⬜ |
| 7 | `card_num_3.png` | 128×128 | 速度数字 3 | ⬜ |
| 8 | `card_num_4.png` | 128×128 | 速度数字 4 | ⬜ |
| 9 | `card_heat_icon.png` | 128×128 | 火焰简化图标 | ⬜ |

**设计参考**: `design/gdd/foodula-1-visual-style.md` §5 卡牌视觉设计  
**色值参考**: `design/gdd/foodula-1-visual-style.md` §2.2

---

## 二、UI精灵 `Assets/Sprites/UI/`

| # | 文件名 | 规格 | 描述 | 状态 |
|---|--------|------|------|------|
| 10 | `gear_knob_bg.png` | 200×200 | 档位旋钮底，圆形 | ⬜ |
| 11 | `gear_knob_pointer.png` | 200×200 | 档位旋钮指针层 | ⬜ |
| 12 | `heat_meter_bg.png` | 48×256 | 热量温度计底，蓝→红渐变 | ⬜ |
| 13 | `heat_meter_fill.png` | 44×252, 9-slice | 热量填充条 | ⬜ |
| 14 | `panel_bg.png` | 9-slice | 面板底图，暗灰蓝 `#161B22`，细线边框 `#30363D` | ⬜ |
| 15 | `btn_normal.png` | 9-slice | 按钮常态，白色半透明，细边框 | ⬜ |
| 16 | `btn_hover.png` | 9-slice | 按钮悬浮，边框变亮 | ⬜ |
| 17 | `btn_pressed.png` | 9-slice | 按钮按下，填充 | ⬜ |
| 18 | `flag_uk.png` | 64×64 | 英国国旗，圆形裁切 | ⬜ |
| 19 | `flag_de.png` | 64×64 | 德国国旗 | ⬜ |
| 20 | `flag_it.png` | 64×64 | 意大利国旗 | ⬜ |
| 21 | `flag_us.png` | 64×64 | 美国国旗 | ⬜ |
| 22 | `flag_cn.png` | 64×64 | 中国国旗 | ⬜ |
| 23 | `flag_jp.png` | 64×64 | 日本国旗 | ⬜ |

**9-slice 说明**: `panel_bg`, `btn_normal`, `btn_hover`, `btn_pressed` 需要在 Unity Sprite Editor 中设置 9-slice border。建议边框宽度 8px。

---

## 三、赛车精灵 `Assets/Sprites/Cars/`

| # | 文件名 | 规格 | 描述 | 状态 |
|---|--------|------|------|------|
| 24 | `car_uk.png` | 256×128 | 英国炸鱼薯条赛车，金黄车身+薯条尾翼 | ⬜ |
| 25 | `car_de.png` | 256×128 | 德国啤酒黑面包赛车，银灰+深棕 | ⬜ |
| 26 | `car_it.png` | 256×128 | 意大利意面披萨赛车，法拉利红+芝士白 | ⬜ |
| 27 | `car_us.png` | 256×128 | 美国汉堡可乐赛车，可乐红+双层肉饼 | ⬜ |
| 28 | `car_cn.png` | 256×128 | 中国电动点心赛车，瓷白蒸笼+翡翠绿 | ⬜ |
| 29 | `car_jp.png` | 256×128 | 日本寿司拉面赛车，玄黑海苔+彩色截面 | ⬜ |

**设计要求**: 俯视图（赛道从上方看）。手绘质感（Hand-Painted）。  
**设计参考**: `design/gdd/foodula-1-visual-style.md` §3.2

---

## 四、赛道精灵 `Assets/Sprites/Track/`

| # | 文件名 | 规格 | 描述 | 状态 |
|---|--------|------|------|------|
| 30 | `track_straight.png` | 32×32 | 直道节点，灰色圆点 | 🚧 |
| 31 | `track_apex.png` | 32×32 | 弯心节点，红色圆点+限速数字 | 🚧 |
| 32 | `track_start_finish.png` | 32×32 | 起终点线，绿色+方格旗图案 | 🚧 |
| 33 | `track_bg_demo.png` | 2048×2048 | 示范赛道整体底图 | ⬜ |

**替代方案**: Demo 阶段可用 LineRenderer 画线（现有方案），节点用简单有色精灵标记。

---

## 五、特效精灵 `Assets/Sprites/Effects/`

| # | 文件名 | 规格 | 描述 | 状态 |
|---|--------|------|------|------|
| 34 | `fx_slipstream.png` | 64×32 | 尾流虚线箭头，科技蓝 | ⬜ |
| 35 | `fx_spin_01.png` | 128×128 | 失控旋转帧 1/4 | ⬜ |
| 36 | `fx_spin_02.png` | 128×128 | 失控旋转帧 2/4 | ⬜ |
| 37 | `fx_spin_03.png` | 128×128 | 失控旋转帧 3/4 | ⬜ |
| 38 | `fx_spin_04.png` | 128×128 | 失控旋转帧 4/4 | ⬜ |
| 39 | `fx_corner_flash.png` | 32×32 | 弯道判定脉冲，红色 | ⬜ |
| 40 | `fx_cool.png` | 16×16 | 冷却粒子，蓝色光点 | ⬜ |

---

## 六、字体 `Assets/Fonts/`

| # | 文件名 | 描述 | 许可 | 状态 |
|---|--------|------|------|------|
| 41 | `Inter-Variable.ttf` | 主 UI 字体（Inter），高可读性 | SIL Open Font License, 免费 | ⬜ |
| 42 | `JetBrainsMono-Regular.ttf` | 等宽字体，日志/调试用 | SIL Open Font License, 免费 | ⬜ |

**下载**: Inter → https://fonts.google.com/specimen/Inter | JetBrains Mono → https://www.jetbrains.com/lp/mono/

---

## 七、音频 `Assets/Audio/`

### Music `Audio/Music/`

| # | 文件名 | 格式 | 描述 | 状态 |
|---|--------|------|------|------|
| 43 | `bgm_race.ogg` | OGG | 比赛BGM，节奏感强，非干扰性 | ⬜ |
| 44 | `bgm_menu.ogg` | OGG | 菜单BGM，轻松 | ⬜ |

### SFX `Audio/SFX/`

| # | 文件名 | 格式 | 描述 | 状态 |
|---|--------|------|------|------|
| 45 | `sfx_card_select.wav` | WAV | 选牌，轻微咔嗒声 | ⬜ |
| 46 | `sfx_card_play.wav` | WAV | 出牌，刷刷声 | ⬜ |
| 47 | `sfx_corner.wav` | WAV | 过弯判定 | ⬜ |
| 48 | `sfx_spin.wav` | WAV | 失控，轮胎尖叫+撞击 | ⬜ |
| 49 | `sfx_finish.wav` | WAV | 完赛，欢呼/旗帜 | ⬜ |

---

## 八、预制体 `Assets/Prefabs/`

### 已有（需更新精灵引用）

| # | 文件名 | 描述 | 状态 |
|---|--------|------|------|
| 50 | `Prefab/CardPrefab.prefab` | 卡牌预制体，需替换精灵图+底图 | 🚧 占位 |
| 51 | `Prefab/CarPrefab.prefab` | 赛车预制体，需替换精灵图 | 🚧 占位 |
| 52 | `Prefab/NodePrefab.prefab` | 赛道节点预制体 | 🚧 占位 |

### 待创建

| # | 文件名 | 描述 | 状态 |
|---|--------|------|------|
| 53 | `Prefabs/UI/RaceCanvas.prefab` | 比赛 Canvas 预制体（整个 UI 层级） | ⬜ |
| 54 | `Prefabs/UI/CardSlot.prefab` | 单张卡牌槽位 | ⬜ |
| 55 | `Prefabs/UI/GearButton.prefab` | 档位按钮 | ⬜ |
| 56 | `Prefabs/Track/TrackNode.prefab` | 赛道节点（含精灵+标签） | ⬜ |
| 57 | `Prefabs/Cars/CarEntity.prefab` | 赛车实体（含脚本+精灵） | ⬜ |
| 58 | `Prefabs/Effects/SpinEffect.prefab` | 失控旋转特效 | ⬜ |

---

## 优先级速查（按Demo制作顺序）

| 优先级 | 资源 | 原因 |
|--------|------|------|
| 🔴 P0 | 卡牌精灵 (#1-9) | 核心交互，占屏幕最大面积 |
| 🔴 P0 | UI面板+按钮 (#14-17) | 所有面板都需要底图 |
| 🔴 P0 | 字体 (#41) | 替换默认 Liberation Sans |
| 🟡 P1 | 赛车精灵 (#24-29) | 替换红色/蓝色方块 |
| 🟡 P1 | 国旗图标 (#18-23) | 车队标识 |
| 🟡 P1 | 档位旋钮 (#10-11) | 视觉焦点 |
| 🟢 P2 | 赛道精灵 (#30-33) | 可用 LineRenderer 暂代 |
| 🟢 P2 | 热量温度计 (#12-13) | 可用 Filled Image 暂代，纯色也行 |
| 🔵 P3 | 特效精灵 (#34-40) | 动画+特效，Demo后期 |
| 🔵 P3 | 音频 (#43-49) | Demo末期或Post-Demo |

---

## 快速查找

| 要找什么 | 去哪里 |
|----------|--------|
| 卡牌相关精灵 | `Assets/Sprites/Cards/` |
| UI面板/按钮/图标 | `Assets/Sprites/UI/` |
| 赛车图 | `Assets/Sprites/Cars/` |
| 赛道节点 | `Assets/Sprites/Track/` |
| 粒子/特效 | `Assets/Sprites/Effects/` |
| 字体 | `Assets/Fonts/` |
| 音乐 | `Assets/Audio/Music/` |
| 音效 | `Assets/Audio/SFX/` |
| UI预制体 | `Assets/Prefabs/UI/` |
| 赛车预制体 | `Assets/Prefabs/Cars/` |

---

> 📄 **关联文档**: `design/demo-framework.md`（框架设计）、`design/gdd/foodula-1-visual-style.md`（视觉规范）
