# Foodula1 — 美术资源清单

> **用途**: 美术资源制作和查找的目录索引  
> **关联**: `design/planning/demo-framework.md` §4 资产需求清单
> **当前审计**：2026-08-23；本清单同时记录已存在资源、运行时生成内容和后续缺口。
> **状态**：已有资源与待制作项混合；以本文件的逐项状态和实际文件为准。
> “⬜”不再表示整个项目没有视觉实现，而是表示该项文件尚未提供或仍需美术替换。

---

## 资源目录总览

```
Assets/
├── Sprites/           ← 2D精灵图（卡牌、UI、赛车、赛道、特效）
├── Prefabs/           ← Unity预制体（按类型分子目录）
├── Audio/             ← 音频资源（Music / SFX）
├── ttf/               ← 已有字体文件
├── TmpFont/           ← TMP 字体资源
└── Resources/Configs/ ← 运行时数据（JSON赛道）✅已完成
```

---

## 一、卡牌精灵 `Assets/Sprites/Cards/`

| # | 文件名 | 规格 | 描述 | 状态 |
|---|--------|------|------|------|
| 1 | `card_speed_bg.png` | 256×384 | 速度牌底图，科技蓝边框，圆角 | ✅ 已有 |
| 2 | `card_heat_bg.png` | 256×384 | 热量牌底图，暗橙/红棕色调 | ✅ 已有 |
| 3 | `card_selected_overlay.png` | 256×384 | 选中态叠加，绿色半透明覆盖 | ✅ 已有 |
| 4 | `card_back.png` | 256×384 | 牌背，赛车主题 | ✅ 已有 |
| 5 | `card_num_1.png` | 128×128 | 速度数字 1 | ✅ 已有 |
| 6 | `card_num_2.png` | 128×128 | 速度数字 2 | ✅ 已有 |
| 7 | `card_num_3.png` | 128×128 | 速度数字 3 | ✅ 已有 |
| 8 | `card_num_4.png` | 128×128 | 速度数字 4 | ✅ 已有 |
| 9 | `card_heat_icon.png` | 128×128 | 火焰简化图标 | ✅ 已有 |

**设计参考**: `design/gdd/foodula-1-visual-style.md` §5 卡牌视觉设计  
**色值参考**: `design/gdd/foodula-1-visual-style.md` §2.2

---

## 二、UI精灵 `Assets/Sprites/UI/`

| # | 文件名 | 规格 | 描述 | 状态 |
|---|--------|------|------|------|
| 10 | `gear_knob_bg.png` | 200×200 | 档位旋钮底，圆形 | ✅ 已有 |
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
| 24 | `car_uk.png` | 256×128 | 英国炸鱼薯条赛车，金黄车身+薯条尾翼 | ✅ 已有 |
| 25 | `car_de.png` | 256×128 | 德国啤酒黑面包赛车，银灰+深棕 | ✅ 已有 |
| 26 | `car_it.png` | 256×128 | 意大利意面披萨赛车，法拉利红+芝士白 | ✅ 已有 |
| 27 | `car_us.png` | 256×128 | 美国汉堡可乐赛车，可乐红+双层肉饼 | ✅ 已有 |
| 28 | `car_cn.png` | 256×128 | 中国电动点心赛车，瓷白蒸笼+翡翠绿 | ✅ 已有 |
| 29 | `car_jp.png` | 256×128 | 日本寿司拉面赛车，玄黑海苔+彩色截面 | ✅ 已有 |

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

> 当前运行时已存在 8 个布局背景：`track_layout_uk/de/it/us/cn/jp/`
> `fr_lemans/de_endurance.png`。普通节点和弯心遮罩由运行时绘制，
> 不对应上表的旧节点精灵文件。

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

## 六、字体（`Assets/ttf/` 与 `Assets/TmpFont/`）

| # | 文件名 | 描述 | 许可 | 状态 |
|---|--------|------|------|------|
| 41 | `Inter-Variable.ttf` | 主 UI 英文/数字正文 | Google Fonts (免费) | ⬜ |
| 42 | `JetBrainsMono-Regular.ttf` | 等宽数字（热量/速度/档位数值） | Google Fonts (免费) | ⬜ |
| — | `NotoSansSC-Regular.otf` | 中文 UI 文字（思源黑体） | Adobe (免费) | ⬜ |
| — | `futurab.ttf` | 卡牌大号数字 | 已有 `Assets/ttf/futurab.ttf` | ✅ |
| — | `unispace bd.ttf` | 科技感标题 | 已有 `Assets/ttf/unispace bd.ttf` | ✅ |

**下载**: Inter → https://fonts.google.com/specimen/Inter | JetBrains Mono → https://www.jetbrains.com/lp/mono/ | 思源黑体 → https://github.com/adobe-fonts/source-han-sans

> 需为 TMP 生成 Font Asset (SDF): 在 Unity 中右键 .ttf → Create → TextMeshPro → Font Asset

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
| 50 | `Prefab/CardPrefab.prefab` | 卡牌预制体，需替换精灵图+底图 | ✅ 已有，运行时使用 |
| 51 | `Prefab/CarPrefab.prefab` | 赛车预制体，需替换精灵图 | ✅ 已有，运行时使用 |
| 52 | `Prefab/NodePrefab.prefab` | 赛道节点预制体 | ✅ 已有，运行时使用 |

### 已有

| # | 文件名 | 描述 | 状态 |
|---|--------|------|------|
| 53 | `Prefabs/UI/RaceCanvas.prefab` | 比赛 Canvas 预制体（完整 UI 层级） | ✅ 已生成 |
| 54 | `Prefabs/UI/CardSlot.prefab` | 单张卡牌槽位（内嵌于 RaceCanvas 的 HandContainer） | ⬜ |
| 55 | `Prefabs/UI/GearButton.prefab` | 档位按钮（内嵌于 RaceCanvas 的 GearButtons） | ⬜ |
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

## 资源就绪度

| 类别 | 数量 | MVP 占位方案 | Demo 就绪目标 |
|------|------|------------|-------------|
| 卡牌底图 | 2 | 白色方块 Image | ✅ Phase 2 |
| 赛车精灵 | 6 | 纯色方块 + 国旗 | ✅ Phase 2 |
| UI 面板 | 4 | 无底图，纯 TMP 文字 | ✅ Phase 2 |
| 档位旋钮 | 2 | 方形按钮 | Phase 2 |
| 热量温度计 | 2 | TMP 文字显示 | Phase 2 |
| 国旗图标 | 6 | Emoji 🇬🇧🇩🇪🇮🇹🇺🇸🇨🇳🇯🇵 | Phase 2 |
| 赛道节点 | 3 | SpriteRenderer 默认圆 | Phase 2 |
| 特效 | 7 | 无特效 | Phase 3 |
| 车手头像 | 12 | 纯色圆形 | Phase 3 |
| 字体 | 5 | msyh SDF (微软雅黑) | ✅ Phase 2 |

---
## 快速查找

| 要找什么 | 去哪里 |
|----------|--------|
| 卡牌相关精灵 | `Assets/Sprites/Cards/` |
| UI面板/按钮/图标 | `Assets/Sprites/UI/` |
| 赛车图 | `Assets/Sprites/Cars/` |
| 赛道节点 | `Assets/Sprites/Track/` |
| 粒子/特效 | `Assets/Sprites/Effects/` |
| 字体 | `Assets/ttf/`、`Assets/TmpFont/` |
| 音乐 | `Assets/Audio/Music/` |
| 音效 | `Assets/Audio/SFX/` |
| UI预制体 | `Assets/Prefabs/UI/` |
| 赛车预制体 | `Assets/Prefabs/Cars/` |

---

> 📄 **关联文档**: `design/planning/demo-framework.md`（框架设计）、`design/gdd/foodula-1-visual-style.md`（视觉规范）、`design/planning/ai-art-prompts.md`（AI 生成 Prompt）
