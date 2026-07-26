# AI 美术生成 Prompt 手册

> **用途**: 复制 Prompt → 粘贴到 AI 绘图工具，生成游戏美术资源  
> **推荐工具**: Midjourney / DALL·E 3 / Stable Diffusion (SDXL) / 通义万相  
> **输出格式**: PNG, RGBA (需手动去底)  
> **关联文档**: `design/gdd/foodula-1-visual-style.md` (配色与风格), `design/asset-manifest.md` (完整清单)

---

## 使用说明

1. 每条 Prompt 已经过尺寸、配色、风格调优，**直接复制使用**
2. 生成后需要：**去底 (remove background)** → **裁切到精确尺寸** → 放入 `Assets/Sprites/[类别]/`
3. 九宫格 UI 元素（按钮、面板）建议用 Unity Sprite Editor 手动设置 border
4. 标注 `[MJ]` = Midjourney 优化 / `[DL]` = DALL·E 3 优化 / `[通用]` = 两者通用

---

## 🔴 P0 — 卡牌资源（9 项）

### 1. 速度牌底图 `card_speed_bg.png`

```
[通用] A single rectangular trading card with rounded corners (8px radius),
256x384px, viewed from top-down. Clean minimal style.
Background: very light gray #F0F3F6 with faint vertical pinstripes.
Border: thin 1px stroke in tech blue #58A6FF.
The card should be EMPTY — no text, no icons, no numbers.
Just the card frame and textured background.
Style: clean UI card, like Slay the Spire cards but simpler.
Flat design, no 3D shadows, no gradients.
```

### 2. 热量牌底图 `card_heat_bg.png`

```
[通用] A single rectangular trading card with rounded corners (8px radius),
256x384px, viewed from top-down.
Background: dark warm gradient from burnt orange #C2553A at bottom
to dark amber #8B5E3C at top. The texture should feel "heavy" and oppressive,
subtle heat-wave distortion lines across the surface.
Border: thin 1px stroke in warn orange #F78166.
The card should be EMPTY — no text, no icons, no numbers.
Style: dark, warm, slightly ominous card back. Flat design with subtle texture.
```

### 3. 卡牌选中高亮 `card_selected_overlay.png`

```
[通用] A semi-transparent overlay, 256x384px.
30% opacity green #3FB950 fill covering the entire rectangle.
Thicker 3px border in the same green.
Rounded corners (8px radius).
Everything else is fully transparent (alpha=0).
This is meant to be layered ON TOP of another card image.
```

### 4-7. 速度数字 1~4 `card_num_1.png` ~ `card_num_4.png`

```
[通用] A single large bold number [1/2/3/4] centered in a 128x128px canvas.
The number should be white with a thick dark outline (2-3px stroke in #1B1F2B).
Style: geometric sans-serif, racing-sport aesthetic. Bold and chunky,
like a race car number. Not a system font — hand-drawn or custom lettering feel.
Slight italic lean (~8°) for dynamic racing feel.
The number fills ~70% of the canvas height.
Background: fully transparent.
Generate ONE image with the number [X].
```

### 8. 热量图标 `card_heat_icon.png`

```
[通用] A simplified flame icon, 80x80px canvas, centered.
Three flame petals/ tongues in warm colors:
- Outer petal: red #E5533B
- Middle petal: orange #F78166  
- Inner petal: yellow #F0C040
Clean, minimal, icon-style — not photorealistic fire.
Should read clearly at small sizes (like a mobile app icon).
Background: fully transparent.
Style: flat design icon, geometric flame shapes, no glow effects.
```

### 9. 牌背 `card_back.png`

```
[通用] A single rectangular card back, 256x384px, rounded corners (8px radius).
Dark background #161B22 with a subtle racing-themed geometric pattern:
- Fine diagonal grid lines in #30363D
- A centered subtle gear/cog icon outline in #58A6FF (very faint, ~20% opacity)
Border: thin 1px stroke in #30363D.
The card back should feel like a premium board game card.
Style: elegant, understated, board-game aesthetic. No text.
```

---

## 🟡 P1 — 赛车精灵（6 项）

> **通用要求**: 每张 256×128px, 俯视图 (top-down view), 手绘质感 (hand-painted),
> 透明背景, 赛车朝右 →

### 🇬🇧 英国 `car_uk.png`

```
[通用] A top-down view of a cartoon race car shaped like FISH AND CHIPS,
256x128px. The car faces RIGHT. Hand-painted game sprite style.
- Body: long golden fish fillet shape, breadcrumb texture (regular dots),
  navy blue #1B3A5C racing stripe down the center
- Rear wing: 4-5 thick french fries arranged horizontally, golden yellow
- Exhaust pipes: small malt vinegar bottle shapes on each side
- Wheels: 4 lemon slice circles (white with yellow center, radial lines)
- Headlights: two round white circles at the front (fish "eyes")
Colors: golden brown + navy blue + lemon yellow.
Background: transparent. Style: Pixar Cars meets board game token.
```

### 🇩🇪 德国 `car_de.png`

```
[通用] A top-down view of a cartoon race car shaped like DARK RYE BREAD
with pretzel, 256x128px. The car faces RIGHT. Hand-painted game sprite style.
- Body: rectangular dark rye bread shape #6B3A2A, visible grain dots on surface,
  silver-gray #B0B0B0 racing stripe
- Roll cage: pretzel-shaped loop on top, golden brown with salt crystals
- Exhaust: two beer tap handles on sides, wooden brown
- Wheels: 4 beer coaster circles (red/white checkered pattern)
- Front: squared-off bread loaf end with headlights
Colors: dark brown + silver-gray + beer gold.
Background: transparent. Style: Pixar Cars meets board game token.
```

### 🇮🇹 意大利 `car_it.png`

```
[通用] A top-down view of a cartoon race car shaped like LASAGNA and PIZZA,
256x128px. The car faces RIGHT. Hand-painted game sprite style.
- Body: layered lasagna visible from side — beige pasta sheets with
  red tomato sauce #C41E3A and white béchamel lines between layers
- Top shell: pizza crust dome in cream white #FFF5E6, slightly curved
- Front wing: two green basil leaves
- Engine cover: melted mozzarella cheese patch, slightly bubbly
- Wheels: 4 tomato slice circles (red with seed pattern)
- Ferrari red #C41E3A as the dominant accent color
Colors: Ferrari red + cream white + basil green.
Background: transparent. Style: Pixar Cars meets board game token.
```

### 🇺🇸 美国 `car_us.png`

```
[通用] A top-down view of a cartoon race car shaped like a DOUBLE CHEESEBURGER,
256x128px. The car faces RIGHT. Hand-painted game sprite style.
- Body: two thick beef patties #8B4513 stacked vertically, oversized (1.5x)
- Roof: sesame seed bun top, golden brown with visible white sesame dots
- Side skirts: wavy green lettuce leaf edges
- Exhaust: two large red Coca-Cola cups #C41E3A with straws
- Wheels: 4 onion ring circles (golden brown rings)
- Front: cheese slice #FFD700 dripping slightly over the front patty
Colors: cola red + cheese yellow + sesame bun gold.
Background: transparent. Style: Pixar Cars meets board game token.
```

### 🇨🇳 中国 `car_cn.png`

```
[通用] A top-down view of a cartoon race car shaped like BAMBOO STEAMER
BASKETS, 256x128px. The car faces RIGHT. Hand-painted game sprite style.
- Body: 3-4 stacked circular bamboo steamer baskets, pale bamboo yellow #E8D5B7,
  visible horizontal bamboo weave lines (simple cross-hatch)
- Chassis: low, sleek, emits faint blue glow #58A6FF from underbody
- Front wing: two chopsticks extending forward, dark wood brown
- Rear: steam wisps (2-3 subtle white translucent curls) from basket gaps
- Wheels: 4 shiitake mushroom slice circles (dark brown #5C3D2E with gill lines)
- Accent: jade green #50C878 trim lines along the basket edges
Colors: porcelain white + bamboo yellow + jade green + blue underglow.
Background: transparent. Style: Pixar Cars meets board game token.
```

### 🇯🇵 日本 `car_jp.png`

```
[通用] A top-down view of a cartoon race car shaped like a FUTOMAKI SUSHI ROLL,
256x128px. The car faces RIGHT. Hand-painted game sprite style.
- Body: cylindrical sushi roll wrapped in dark nori seaweed #2C2C2C (matte black-green),
  cross-section visible on top showing:
  - Orange salmon block #FA8072 with white fat lines
  - Yellow tamago (egg) block #FFD700
  - Green cucumber strip #90EE90
  - White rice #FFFFF0 with subtle grain dots
- Rear wing: tempura shrimp — golden brown crispy batter texture
- Exhaust: small ramen bowl shape at rear
- Wheels: 4 wasabi green balls #7FFF00
- Accent: vermillion red #E60012 trim line along sides
Colors: nori black + vermillion red + sushi fill colors.
Background: transparent. Style: Pixar Cars meets board game token.
```

---

## 🟡 P1 — UI 资源（非 AI，制作说明）

> ⚠️ **不建议用 AI 生成 UI 元素**。以下是更快的制作方法。

### 国旗图标 (#18-23, 64×64px)

不需要 AI。直接下载：
- https://flagicons.lipis.dev/ — 免费国旗 SVG/PNG，选 64px 圆形裁切
- 6 面国旗放到 `Assets/Sprites/UI/flag_*.png`

### 面板底图 `panel_bg.png`

在 Unity 中手动制作（或用 Figma）：
1. 新建 64×64 画布
2. 填充 `#161B22`
3. 1px 内边框 `#30363D`
4. 导出 PNG → Unity Sprite Editor 设置 9-slice border: 16,16,16,16

### 按钮 `btn_normal/hover/pressed.png`

同上，在 Unity/Figma 中做：
- normal: `rgba(255,255,255,0.15)` 填充 + `#30363D` 边框
- hover: 边框变 `#58A6FF`
- pressed: 填充变 `#58A6FF` 40% opacity
- 32×32px，9-slice border: 12,12,12,12

### 档位旋钮 `gear_knob_bg.png`

```
[通用] A flat-design stove knob viewed from top-down, 200x200px canvas.
Circular shape centered.
Dark background #161B22 with an arc of 4 tick marks:
- Bottom-left: "G1" label
- Bottom-right: "G4" label  
- Arc path from G1 through top to G4
Tick marks in tech blue #58A6FF, thin lines.
One tick is highlighted in green #3FB950 (the current gear position marker).
Style: minimalist F1 steering wheel rotary dial meets kitchen stove knob.
No 3D effects, flat design. Background: transparent.
```

---

## 🟢 P2 — 赛道资源（8 项）

### 赛道贴图

#### 沥青路面 `track_surface_tile.png`

```
[通用] A tileable top-down asphalt road texture, 256x256px.
Dark gray asphalt #3A3D42 with subtle noise/grain.
A white dashed lane marking line down the center (#FFFFFF, 2px wide,
dashes: 12px dash, 8px gap).
The texture should tile seamlessly in all 4 directions.
Style: clean, minimal racing surface. Not photorealistic — stylized
to match a board game aesthetic.
```

#### 弯道路缘 `track_curb_tile.png`

```
[通用] A tileable top-down racetrack curb/kerb texture, 256x256px.
Alternating red #E5533B and white #FFFFFF stripes running vertically.
Each stripe ~16px wide. The curb has a slight inner shadow on one side.
Seamless tiling.
Style: F1 circuit curb, stylized for top-down view.
```

### 赛道标记

#### 弯心标记 `track_apex_marker.png`

```
[通用] A circular track marker for corner apex, 64x64px canvas.
Red circle #F78166 with 2px white outline.
A large bold number "[X]" centered inside, white with dark shadow.
The marker has a subtle glow effect (outer blur in red, ~8px radius).
Style: clean racing telemetry marker, reads clearly at small sizes.
Background: transparent. Generate three variants with speed limits 1, 2, 3.
```

#### 起终点线 `track_start_finish.png`

```
[通用] A start/finish line marker for a top-down racing game, 128x64px.
Checkered pattern: 8×4 grid of alternating black #1B1F2B and white #FFFFFF
squares (each ~16×16px). Below the checkered band: a thin green #3FB950 bar
(height 8px).
The marker has pole-position style: the checkered section leans slightly
forward (~10° tilt) for a dynamic racing feel.
Background: the bottom portion is semi-transparent so it can overlay
on the track surface.
```

### 赛道环境背景

> 每条赛道一张背景图，铺在赛道下方做视觉区分。

#### 英国银石 `track_env_uk.png`

```
[通用] A subtle top-down environment backdrop for a UK racetrack, 2048x2048px.
Muted gray-green grass texture base #7B8C7B, with scattered:
- Small white geometric tent shapes (afternoon tea tents)
- Occasional dark green tree clusters (English countryside hedgerows)
- Faint gray cloud shadows
The overall look should be UNDERSTATED — this is a background that sits
behind the racetrack, should not distract from gameplay elements.
Style: soft watercolor meets board game map. Low contrast, muted palette.
Opacity at 40% strength — more of a texture suggestion than a detailed illustration.
```

#### 德国纽博格林 `track_env_de.png`

```
[通用] A subtle top-down environment backdrop for a German racetrack, 2048x2048px.
Dark green pine forest base #3A5C3A, with scattered:
- Clusters of conical evergreen trees (Black Forest style)
- Occasional small amber/gold clearing patches
- Faint castle ruin silhouette in one corner (extremely subtle)
Low contrast, muted forest tones. Board game map aesthetic.
Opacity at 40% — background texture only, not a detailed scene.
```

#### 意大利蒙扎 `track_env_it.png`

```
[通用] A subtle top-down environment backdrop for an Italian racetrack, 2048x2048px.
Warm golden-olive green base #8B9A6B (Tuscan hills), with scattered:
- Gentle rolling hill contour lines in slightly darker green
- Occasional small cypress tree dots (tall thin dark green)
- Warm terracotta #C4956A subtle patches
Low contrast, warm Mediterranean palette. Board game map aesthetic.
Opacity at 40% — background texture only.
```

#### 美国印第安纳波利斯 `track_env_us.png`

```
[通用] A subtle top-down environment backdrop for a US racetrack, 2048x2048px.
Flat plain base in warm beige-tan #C4B896 (Midwest prairie), with:
- A subtle red-white checkerboard pattern in one corner (very faint)
- Geometric grid pattern suggesting the famous oval layout (barely visible)
- Sparse small brown dots
Low contrast, open flat feel. Board game map aesthetic.
Opacity at 40% — very minimal, the oval track itself is the star.
```

#### 中国上海 `track_env_cn.png`

```
[通用] A subtle top-down environment backdrop for a Chinese racetrack, 2048x2048px.
Pale gray-blue base #C8CCD0 (Shanghai overcast sky reflected), with:
- Subtle "上" character shape embedded in the texture (very faint, like a watermark)
- Occasional small red #C41E3A accent dots
- Faint geometric grid suggesting modern urban layout
- Bamboo green #50C878 subtle streaks
Low contrast, modern sleek feel. Board game map aesthetic.
Opacity at 40% — background texture only.
```

#### 日本铃鹿 `track_env_jp.png`

```
[通用] A subtle top-down environment backdrop for a Japanese racetrack, 2048x2048px.
Deep blue-green base #4A6B5C, with scattered:
- Small pink cherry blossom dots #FFB7C5 (very subtle, scattered)
- Faint figure-8 pattern embedded in texture (Suzuka crossover reference)
- Occasional dark green tree clusters
- A subtle Mount Fuji silhouette in one corner (extremely faint)
Low contrast, serene Japanese garden aesthetic. Board game map.
Opacity at 40% — background texture only.
```

---

## 🟡 P1 — UI 装饰资源（4 项）

### 面板底图 `panel_bg.png`

```
[通用] A dark UI panel background with subtle tech texture, 256x256px.
Base color: deep navy-charcoal #161B22.
Texture: extremely subtle diagonal grid lines in #30363D (~10% opacity),
giving a "carbon fiber" feel without being obvious.
A thin 1px border in #30363D.
The panel should tile cleanly via 9-slice (border 24px).
Style: F1 telemetry screen meets premium board game card table.
Dark, elegant, understated. No bright elements, no gradients.
This is a BACKGROUND for text and UI elements to sit on top of.
```

### 面板标题栏 `panel_header.png`

```
[通用] A UI panel header bar, 256×40px.
Left-aligned accent bar in tech blue #58A6FF (4px wide, full height).
The rest is transparent gradient fading right from #161B22 at 50% to fully transparent.
A thin 1px bottom border line in #30363D full width.
Style: sleek F1 dashboard panel header. Minimal, functional.
Background: the right side is fully transparent so it blends with panel_bg.
```

### HUD 底栏 `hud_bottom_bar.png`

```
[通用] A UI bottom bar / card hand area background, 1920×120px.
Full width dark bar, gradient from #0D1117 at top (90% opacity)
to #0D1117 at bottom (100% opacity).
Top edge: a thin 1px accent line in tech blue #58A6FF (subtle glow).
The bar has extremely subtle horizontal rule lines at 30% and 70% height
in #30363D (barely visible, for visual structure).
Style: premium card game hand area — like a velvet card table edge.
This bar sits at the bottom of the screen behind the player's hand cards.
```

### 分隔线 `ui_divider.png`

```
[通用] A thin horizontal UI divider line, 256×4px.
Center: a 1px line in #30363D.
Fades to fully transparent at both ends (gradient).
That's it — pure minimalism.
Background: fully transparent except the center line.
```

---

## 🔵 P3 — 特效资源（5 项）

### 尾流箭头 `fx_slipstream.png`

```
[通用] A slipstream trail arrow for a racing game, 128×32px.
Three dashed segments followed by an arrowhead → pointing RIGHT.
Color: tech blue #58A6FF at 60% opacity.
Each dash: 16×4px rounded rectangle. Arrow: simple triangle.
The dashes should have a subtle glow (outer blur, 4px).
Background: fully transparent.
Style: clean, F1 telemetry overlay.
```

### 冷却粒子 `fx_cool.png`

```
[通用] A single small cooling particle / spark for a game effect, 16×16px.
A soft blue-white glowing dot, center bright white #FFFFFF, outer glow
in tech blue #58A6FF fading to transparent at edges.
Simple radial gradient. No shapes, just a soft light point.
Background: fully transparent.
Style: subtle UI feedback particle.
```

### 弯道判定脉冲 `fx_corner_flash.png`

```
[通用] A corner judgment flash effect, 64×64px.
A hollow circle ring (4px wide) in warning orange #F78166, with outer glow.
The ring is NOT filled — it's just the outline with a soft pulse glow.
Fades to fully transparent both inward and outward from the ring.
Background: fully transparent.
Style: minimap ping effect, clean and readable.
```

### 过热警告边框 `fx_overheat_border.png`

```
[通用] A screen-edge warning indicator for overheat, 1920×16px.
A thin horizontal bar, gradient from transparent at edges to
warning red #E5533B at center (the center ~40% is visible red).
Soft glow on the red portion.
This is placed at screen edges as a subtle "danger" indicator.
Background: fully transparent.
Style: diegetic F1 warning light, understated not alarmist.
```

### 完赛旗帜 `fx_finish_flag.png`

```
[通用] A finish line celebration flag icon, 128×128px.
A waving checkered flag (black #1B1F2B + white #FFFFFF squares)
on a small flagpole, angled ~30° as if waving.
The flag has 2-3 wave folds with simple shading.
Green #3FB950 accent glow behind the flag.
Style: victory icon, celebratory but not cartoonish.
Background: fully transparent.
```

---

## 📋 完整 AI 生成清单

| # | 文件名 | 类别 | 尺寸 | 用 AI? |
|---|--------|------|------|--------|
| **卡牌** |
| 1 | `card_speed_bg.png` | 卡牌底图 | 256×384 | ✅ AI |
| 2 | `card_heat_bg.png` | 卡牌底图 | 256×384 | ✅ AI |
| 3 | `card_selected_overlay.png` | 卡牌叠加 | 256×384 | ✅ AI |
| 4-7 | `card_num_1~4.png` | 数字图标 | 128×128 | ✅ AI |
| 8 | `card_heat_icon.png` | 火焰图标 | 80×80 | ✅ AI |
| 9 | `card_back.png` | 牌背 | 256×384 | ✅ AI |
| **赛车** |
| 10-15 | `car_uk/de/it/us/cn/jp.png` | 赛车精灵 | 256×128 | ✅ AI |
| **赛道** |
| 16 | `track_surface_tile.png` | 路面贴图 | 256×256 | ✅ AI |
| 17 | `track_curb_tile.png` | 路缘贴图 | 256×256 | ✅ AI |
| 18 | `track_apex_marker.png` | 弯心标记 | 64×64 | ✅ AI |
| 19 | `track_start_finish.png` | 起终点线 | 128×64 | ✅ AI |
| 20-25 | `track_env_*.png` ×6 | 赛道环境 | 2048×2048 | ✅ AI |
| **UI** |
| 26 | `panel_bg.png` | 面板底图 | 256×256 | ✅ AI |
| 27 | `panel_header.png` | 面板标题 | 256×40 | ✅ AI |
| 28 | `hud_bottom_bar.png` | 底部手牌栏 | 1920×120 | ✅ AI |
| 29 | `ui_divider.png` | 分隔线 | 256×4 | ✅ AI |
| 30 | `gear_knob_bg.png` | 档位旋钮 | 200×200 | ✅ AI |
| **特效** |
| 31 | `fx_slipstream.png` | 尾流箭头 | 128×32 | ✅ AI |
| 32 | `fx_cool.png` | 冷却粒子 | 16×16 | ✅ AI |
| 33 | `fx_corner_flash.png` | 弯道脉冲 | 64×64 | ✅ AI |
| 34 | `fx_overheat_border.png` | 过热边框 | 1920×16 | ✅ AI |
| 35 | `fx_finish_flag.png` | 完赛旗帜 | 128×128 | ✅ AI |
| **不需要 AI** |
| — | 国旗 ×6 | UI | 64×64 | ❌ 下载 flagicons.lipis.dev |
| — | 按钮 3 态 | UI | 32×32 | ❌ Figma 5分钟 |
| — | 热量温度计 | UI | 48×256 | ❌ Unity Image.Filled |
| — | 字体 ×2 | 字体 | — | ❌ Google Fonts |

**AI 生成总计: 35 张**（9 卡牌 + 6 赛车 + 10 赛道 + 5 UI + 5 特效）

---

## 后处理流程

AI 出图后，每张图需要：

1. **去底**: 用 remove.bg 或 Photoshop "选择主体" → 删除背景
2. **裁切**: 裁到精确尺寸（见上表）
3. **格式**: 导出 PNG-24, RGBA
4. **命名**: 严格按文件名（`card_speed_bg.png`, `car_uk.png`...）
5. **Unity 导入**: 拖入 `Assets/Sprites/[类别]/`
   - Texture Type: Sprite (2D and UI)
   - Filter Mode: Point (for pixel art) or Bilinear (for painted)
   - Compression: None (for UI elements)
   - Max Size: 匹配或略大于实际尺寸

---

> 📄 **关联**: `design/gdd/foodula-1-visual-style.md` (完整配色与风格参考),  
> `design/asset-manifest.md` (58 项完整清单),  
> `design/asset-requirements.md` (详细规格)
