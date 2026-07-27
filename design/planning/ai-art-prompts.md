# AI 美术生成 Prompt 手册

> **用途**: 复制 Prompt → 粘贴到 AI 绘图工具，生成游戏美术资源  
> **当前工具**: GPT Image (ChatGPT 内置)  
> **输出格式**: PNG, RGBA (需手动去底)  
> **关联文档**: `design/gdd/foodula-1-visual-style.md` (配色与风格), `design/asset-manifest.md` (完整清单)

---

## ⚠️ GPT Image 关键规则

GPT Image 默认倾向生成 **3D 透视照片**，但本项目所有资源都是 **2D 平面游戏精灵**。
每条 Prompt 必须遵守以下规则，否则 AI 会自作主张加 3D 效果：

| 规则 | 说明 |
|------|------|
| **必须写 "2D flat"** | 每条 prompt 开头声明这是 2D 平面资源 |
| **必须写否定词** | "NO 3D, NO perspective, NO shadows, NO depth, NO gradients" |
| **用实物类比** | "like a printed board game" / "like a flat sticker" / "like wallpaper" |
| **不要用触发 3D 的词** | 避免 "texture"（改用 "pattern"）、"feel"、"looks like"、"curb/kerb"（改用 "stripe marking"） |
| **透明度在 Unity 处理** | 不要要求 AI 输出半透明，生成全不透明图，透明度在 Unity 中设置 |

---

## 使用说明

1. 每条 Prompt 已经过尺寸、配色、风格调优，**直接复制使用**
2. 生成后需要：**去底 (remove background)** → **裁切到精确尺寸** → 放入 `Assets/Sprites/[类别]/`
3. 九宫格 UI 元素（按钮、面板）建议用 Unity Sprite Editor 手动设置 border
4. 所有 Prompt 均为 GPT Image 优化（也兼容 DALL·E 3）

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
[GPT Image] A 2D flat circular dial graphic, 200x200px canvas. Pure 2D — NO 3D, NO depth, NO shadows.
A simple flat circle outline (thin stroke #30363D) centered on a dark #161B22 filled circle.
Four small tick marks arranged in a left-to-right arc across the top half of the circle:
- G1 at bottom-left of the arc, G4 at bottom-right of the arc
- Each tick is a short thin line in tech blue #58A6FF
- One tick (middle position) is bright green #3FB950
All elements are flat geometric shapes — like a simple diagram drawn in a vector graphics program.
The circle itself has a fully transparent background outside it.
Style: flat 2D UI icon, like a minimalist settings icon. Pure graphic design.
```

---

## 🟢 P2 — 赛道资源（8 项）

> ⚠️ 所有赛道资源都是 **纯 2D 平面图**，不是 3D 渲染。生成后需要在 Unity 中设置为 Sprite (2D and UI)。

### 赛道贴图

#### 沥青路面 `track_surface_tile.png`

```
[GPT Image] A 2D flat seamless repeating pattern for a board game race track surface, 256x256px.
This is a PURELY 2D FLAT image — NO 3D, NO perspective, NO shadows, NO depth, NO lighting, NO gradients.
The entire image is a solid flat dark gray #3A3D42 fill, with tiny random scattered noise dots in slightly lighter gray for subtle variation (like static on an old TV, but very faint).
A white dashed line runs vertically through the exact center: #FFFFFF, 2px wide per dash, each dash 12px long with 8px gaps between them.
The pattern tiles seamlessly — the left edge must match the right edge, and the top edge must match the bottom edge exactly.
Style: flat printed board game surface, like the paper map in Monopoly or Catan. Pure 2D.
```

#### 弯道路肩条纹 `track_curb_tile.png`

```
[GPT Image] A 2D flat seamless repeating pattern of alternating colored stripes, 256x256px.
This is a PURELY 2D FLAT image — like striped wrapping paper or a flat wallpaper pattern.
NO 3D, NO perspective, NO shadows, NO depth, NO lighting, NO bevel, NO rounded edges.
Just flat vertical stripes: red #E5533B and white #FFFFFF, alternating left to right.
Each stripe is exactly 16px wide, with sharp straight edges between colors.
That's it — a simple flat stripe pattern. Nothing more.
The pattern tiles seamlessly — left edge matches right edge, top matches bottom.
Style: flat 2D board game marking, like painted lines on a game board. Pure graphic design, zero depth.
```

### 赛道标记

#### 弯心标记 `track_apex_marker.png`

```
[GPT Image] A 2D flat circular game token marker, 64x64px canvas.
A simple flat circle filled with orange-red #F78166, with a solid white 2px outline stroke around it.
A bold white number is centered inside the circle, filling about 60% of the circle's height.
The entire graphic is completely flat — like a printed cardboard board game chit or a flat sticker.
NO 3D bevel, NO shadows, NO glow effects, NO gradients, NO depth at all.
Outside the circle: fully transparent background.
Style: flat board game token, pure 2D icon.
```

#### 起终点线 `track_start_finish.png`

```
[GPT Image] A 2D flat start/finish line graphic for a board game track, 128x64px canvas.
A rectangular checkered band: 8 columns × 4 rows of alternating black #1B1F2B and white #FFFFFF squares, each square exactly 16×16px, perfectly aligned in a grid.
Below the checkered band: a solid green #3FB950 horizontal bar, 8px tall, full width.
The entire graphic is completely flat — like a printed board game space marker.
NO 3D, NO tilt, NO perspective, NO shadows, NO depth.
Everything else outside the checkered band and green bar is fully transparent.
Style: flat board game start space, pure 2D, like the "GO" square in Monopoly.
```

### 赛道环境背景

> 每条赛道一张背景图，铺在赛道下方做视觉区分。
> 这些是纯色底 + 少量散落小元素的**平面地图纹理**，不是风景画。
> 透明度在 Unity 中设置（修改 SpriteRenderer Color.a），AI 不需要输出半透明。

#### 英国银石 `track_env_uk.png`

```
[GPT Image] A 2D flat background map for a board game, 2048x2048px. Pure 2D top-down view.
The entire image is a solid flat fill of muted gray-green #7B8C7B.
Scattered sparsely across this flat green field: a few small white triangles (like simple geometric tent shapes), and a few small dark green rounded circle clusters (like top-down tree blobs).
The elements should be minimal — maybe 8-12 small shapes total across the entire 2048px canvas. Mostly empty green space.
NO 3D, NO perspective, NO shadows, NO gradients, NO depth. No painting, no watercolor effect.
Style: flat board game map, like the printed surface of a Catan board. Pure 2D vector-graphic look.
```

#### 德国纽博格林 `track_env_de.png`

```
[GPT Image] A 2D flat background map for a board game, 2048x2048px. Pure 2D top-down view.
The entire image is a solid flat fill of dark forest green #3A5C3A.
Scattered sparsely: a few small dark green triangle clusters (like top-down pine trees), and 2-3 small amber #B8860B patches (clearings).
In one corner, a very simple flat gray geometric shape suggesting a castle ruin — just a few gray rectangles.
NO 3D, NO perspective, NO shadows, NO gradients, NO depth. No painting, no watercolor effect.
Style: flat board game map, pure 2D vector-graphic look.
```

#### 意大利蒙扎 `track_env_it.png`

```
[GPT Image] A 2D flat background map for a board game, 2048x2048px. Pure 2D top-down view.
The entire image is a solid flat fill of warm olive green #8B9A6B.
A few gentle curved lines in slightly darker green across the surface (like contour lines on a simple map).
A few small dark green tall thin oval dots (cypress trees), and 2-3 warm terracotta #C4956A rounded patches.
NO 3D, NO perspective, NO shadows, NO gradients, NO depth. No painting, no watercolor effect.
Style: flat board game map, pure 2D vector-graphic look.
```

#### 美国印第安纳波利斯 `track_env_us.png`

```
[GPT Image] A 2D flat background map for a board game, 2048x2048px. Pure 2D top-down view.
The entire image is a solid flat fill of warm beige-tan #C4B896.
In one corner: a small faint red-white checkerboard pattern (like a tiny 4×4 grid, very subdued).
A few small brown dots scattered sparsely.
NO 3D, NO perspective, NO shadows, NO gradients, NO depth. No painting, no watercolor effect.
Style: flat board game map, pure 2D vector-graphic look. Very minimal — mostly empty beige space.
```

#### 中国上海 `track_env_cn.png`

```
[GPT Image] A 2D flat background map for a board game, 2048x2048px. Pure 2D top-down view.
The entire image is a solid flat fill of pale gray-blue #C8CCD0.
In the center: a very faint, large "上" character shape in slightly darker gray (like a subtle watermark).
A few small red #C41E3A dots scattered sparsely.
A few bamboo green #50C878 subtle horizontal streaks.
NO 3D, NO perspective, NO shadows, NO gradients, NO depth. No painting, no watercolor effect.
Style: flat board game map, pure 2D vector-graphic look.
```

#### 日本铃鹿 `track_env_jp.png`

```
[GPT Image] A 2D flat background map for a board game, 2048x2048px. Pure 2D top-down view.
The entire image is a solid flat fill of deep blue-green #4A6B5C.
A few small pink #FFB7C5 dots scattered very sparsely (cherry blossom suggestion).
A very faint figure-8 loop line embedded in the background (slightly darker green, like a subtle map marking).
In one corner: a simple flat geometric triangle shape in white with a gray top (Mount Fuji silhouette — just a flat shape, no shading).
NO 3D, NO perspective, NO shadows, NO gradients, NO depth. No painting, no watercolor effect.
Style: flat board game map, pure 2D vector-graphic look.
```

---

## 🟡 P1 — UI 装饰资源（4 项）

> ⚠️ UI 元素本质上都是简单几何图形。Prompt 要求极简 —— AI 容易画蛇添足加纹理、渐变、阴影。**效果不理想时建议直接用 Unity Image + 纯色替代。**

### 面板底图 `panel_bg.png`

```
[GPT Image] A 2D flat UI panel background, 256x256px. Pure 2D — NO 3D, NO shadows, NO gradients, NO texture.
The entire image is a solid flat rectangle filled with dark navy-charcoal #161B22.
A thin 1px border line in #30363D on all four edges.
That's it — just a flat colored rectangle with a border. Nothing else.
No grid lines, no carbon fiber pattern, no texture of any kind.
Style: flat UI rectangle, like a simple colored <div> in a webpage. Absolute minimalism.
```

### 面板标题栏 `panel_header.png`

```
[GPT Image] A 2D flat UI header bar, 256×40px. Pure 2D — NO 3D, NO shadows, NO gradients.
On the left edge: a solid vertical bar in tech blue #58A6FF, 4px wide, full 40px height.
A thin 1px solid line in #30363D runs across the bottom edge, full width.
Everything else is fully transparent.
That's it — a flat colored accent bar on the left + a bottom border line. Nothing else.
Style: flat UI element, like a simple CSS border-left + border-bottom. Absolute minimalism.
```

### HUD 底栏 `hud_bottom_bar.png`

```
[GPT Image] A 2D flat horizontal bar for a card game hand area, 1920×120px. Pure 2D — NO 3D, NO gradients, NO texture.
The entire bar is a solid flat fill of dark #0D1117.
A thin 1px solid line in tech blue #58A6FF runs across the top edge, full width.
That's it — a flat dark bar with a blue top line. Nothing else.
No horizontal rule lines, no velvet texture, no glow.
Style: flat UI bar, like a solid colored rectangle in a 2D game HUD. Absolute minimalism.
```

### 分隔线 `ui_divider.png`

```
[GPT Image] A 2D flat horizontal divider line, 256×4px. Pure 2D.
A single solid 1px line in #30363D running horizontally across the center.
The line is shorter than the full width — about 200px centered, with 28px of fully transparent space on each side.
That's it — one flat line. Nothing else. Fully transparent everywhere except the line itself.
Style: flat UI divider, like an <hr> tag in HTML. Absolute minimalism.
```

---

## 🔵 P3 — 特效资源（5 项）

> ⚠️ 特效精灵都是极小尺寸的简单几何图形。AI 容易过度设计。保持极简。

### 尾流箭头 `fx_slipstream.png`

```
[GPT Image] A 2D flat UI arrow icon, 128×32px. Pure 2D — NO 3D, NO shadows, NO glow, NO gradients.
Three short horizontal dash segments (each 16×4px flat rectangle, tech blue #58A6FF) followed by a simple right-pointing triangle arrowhead (same blue, ~12px wide).
The dashes and arrow are arranged in a horizontal line pointing RIGHT.
All shapes are solid flat fill, no outline, no glow.
Fully transparent background everywhere except the blue shapes.
Style: flat 2D UI icon, like a simple arrow glyph from a font icon set. Absolute minimalism.
```

### 冷却粒子 `fx_cool.png`

```
[GPT Image] A 2D flat small glowing dot, 16×16px. Pure 2D — NO 3D.
A simple flat circle in bright white #FFFFFF, 8px diameter, centered.
Surrounding it: a slightly larger circle in tech blue #58A6FF, 16px diameter, with the center cut out (a flat ring around the white dot).
Both shapes are solid flat fill.
Fully transparent everywhere else.
Style: flat 2D particle, like a simple dot from a minimalist UI. Absolute minimalism.
```

### 弯道判定脉冲 `fx_corner_flash.png`

```
[GPT Image] A 2D flat hollow circle ring icon, 64×64px. Pure 2D — NO 3D, NO glow, NO gradients.
A simple flat circle outline ring in warning orange #F78166, 4px thick stroke, about 48px diameter, centered.
The ring is NOT filled — just the outline stroke.
Fully transparent everywhere else (inside the ring and outside it).
Style: flat 2D icon, like a thin circle outline from a UI library. Absolute minimalism.
```

### 过热警告边框 `fx_overheat_border.png`

```
[GPT Image] A 2D flat horizontal warning bar, 1920×16px. Pure 2D — NO 3D, NO glow, NO gradients.
The center 40% of the bar is solid flat fill in warning red #E5533B.
The left 30% and right 30% are fully transparent.
That's it — a flat red rectangle in the middle, transparent on the sides.
No gradient fade, no glow — just a solid colored bar.
Style: flat 2D UI element, like a simple health bar segment. Absolute minimalism.
```

### 完赛旗帜 `fx_finish_flag.png`

```
[GPT Image] A 2D flat checkered flag icon, 128×128px. Pure 2D — NO 3D, NO wave folds, NO shading, NO glow.
A simple straight flagpole: a thin dark gray vertical line on the left.
A flat rectangular flag attached to the pole, filled with a checkered pattern: 4×3 grid of alternating black #1B1F2B and white #FFFFFF squares.
The flag does NOT wave — it's a flat rectangle. No folds, no curves.
Fully transparent background everywhere else.
Style: flat 2D icon, like a checkered flag emoji but simpler. Absolute minimalism.
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
