# AI 美术生成 Prompt 手册

> **用途**: 复制 Prompt → 粘贴到 AI 绘图工具，生成游戏美术资源  
> **当前工具**: GPT Image (ChatGPT 内置)  
> **输出格式**: PNG, RGBA (需手动去底)  
> **关联文档**: `design/gdd/foodula-1-visual-style.md`（配色与风格）、`design/planning/asset-manifest.md`（完整清单）
> **实现审计**：2026-08-27。本文保留已生成资源的历史 Prompt 以便重制，
> 但不再是制作任务清单；唯一资源事实表是 `design/planning/asset-manifest.md`。
> 卡牌、赛车、赛道和档位旋钮已经存在，不应按本文旧 P0/P1 标记重复生成。

### 当前待制作 Prompt 包

新一轮 AI 美术只应围绕以下缺口展开：

1. 主菜单 4K 背景与透明 Foodula1 Logo。
2. 12 位车手统一构图头像（文件 ID 见资源清单）。
3. 六队原创徽章；国旗只作辅助，发布前核对许可。
4. 科技树 4K 背景、节点三态和 L1/L2/L3 层级徽章。
5. 天气、结果、车手/车队卡框和 12 张特技牌插画（P1）。

八张赛道缩略图应由现有 `track_layout_*.png` 派生，不重新生成。
运行时尾流、冷却、失控和弯道提示已达到 Demo 基线，FX Prompt 仅用于可选替换。

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

### 车队标识（旧国旗方案）

当前建议优先绘制六枚原创队徽，国旗只作为次级信息。若使用第三方国旗图标，
必须保存来源与许可证，并统一导出到资源清单规定的 256×256 透明安全区。

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

## 🟢 P2.5 — 赛道布局背景图（8 项）

> 🎯 **用途不同于 `track_env_*.png`**：这些是带完整赛道线路的布局图，
> 直接铺在游戏场景中作为赛道本体。赛道线路清晰可见，节点沿线路布设。
> **尺寸**: 4096×4096px — 需要足够大的分辨率来容纳当前 42-219 个节点的细节。
> **视图**: 完全俯视 (top-down)，纯 2D 平面。

### 通用规则（8 条官方赛道全部遵守）

```
ALL tracks must follow these rules:
- PURE 2D top-down flat view — NO 3D perspective, NO isometric tilt,
  NO shadows, NO depth of field, NO gradients
- The RACING CIRCUIT must be clearly visible as a wide road/path
  (dark gray asphalt #3A3D42, ~80-120px wide for the track surface)
- White dashed center line running along the entire track
- Red-white curb stripes (#E5533B + #FFFFFF) on both edges of corners
- The track shape should be recognizable based on the real circuit layout
- Surrounding environment: flat colored terrain with simple geometric
  shapes for buildings, trees, landmarks (board game map style)
- Start/Finish line: a checkered band across the track at the start position
- The track should fill ~70% of the canvas, leaving margins for environment
- Style: like a printed board game map — clean, flat, readable at game scale
- NO text labels, NO numbers on the track itself
```

---

### 🇬🇧 英国 — 银石下午茶赛道 `track_layout_uk.png`

```
[UK Silverstone] A 2D flat top-down board game race track map, 4096x4096px.
Pure 2D — NO 3D, NO perspective, NO shadows, NO gradients.

TRACK LAYOUT (Silverstone Circuit shape):
- The circuit is a roughly triangular/oval hybrid shape, clockwise direction.
- Start/Finish on a long straight at the bottom-left area (Hamilton Straight).
- After Start: a quick right-left flick (Abbey T1 + Farm T2, fast sweeping).
- Then a tight right-hand hairpin complex (Village + The Loop — two tight
  right turns forming a hook shape).
- A medium straight (Wellington Straight) leading to a left-hand sweep
  (Brooklands) followed by another left (Luffield).
- Then the track sweeps right through Woodcote and into the signature
  HIGH-SPEED S-COMPLEX: Maggotts-Becketts-Chapel — a sequence of
  quick direction changes (left-right-left-right-left) through fast curves.
- A long straight (Hangar Straight) follows, then a right-hand corner (Stowe).
- A tight left-right chicane (Vale) before the final right-hander (Club)
  bringing cars back to the Start/Finish straight.
- Total track character: FAST and FLOWING, with the iconic S-complex as
  the visual centerpiece.

ENVIRONMENT (British afternoon tea + Silverstone):
- Surrounding terrain: muted gray-green #7B8C7B flat fill.
- Scattered white geometric triangles (tea tents) near the track edges.
- In the distance (top-left): a simplified Windsor Castle silhouette —
  flat gray rectangle with crenellations.
- A few dark green rounded tree clusters.
- Sparse rain streak hints (very thin, subtle diagonal white lines,
  low opacity) — typical British weather.
- The overall feel: elegant, classic, slightly moody.

TRACK MARKINGS:
- The Maggotts-Becketts S-complex should be visually emphasized with
  slightly wider curb markings.
- Start/Finish line: prominent checkered band across the track.
- The tight hairpin (Village/Loop) should clearly narrow.
```

---

### 🇩🇪 德国 — 纽博格林啤酒赛道 `track_layout_de.png`

```
[Germany Nürburgring] A 2D flat top-down board game race track map, 4096x4096px.
Pure 2D — NO 3D, NO perspective, NO shadows, NO gradients.

TRACK LAYOUT (Nürburgring GP-Strecke shape):
- A compact, technical circuit, clockwise direction.
- Start/Finish on a medium straight at the bottom.
- After Start: a tight RIGHT hairpin (Castrol-S, almost 180°).
- Immediately into the MERCEDES ARENA complex: a tight right-left-right
  sequence of connected corners, forming a stadium-like bowl shape.
- A medium left (Valvoline) then right (Ford Kurve).
- A downhill-feeling straight (suggested by terrain getting slightly darker
  green below the track).
- A tight hairpin left (Dunlop Kehre, ~180°).
- Then the SCHUMACHER S: a fast right-left flick — the fastest section
  of this circuit.
- A medium straight, then a series of medium-speed corners (Kumho, Bit).
- A technical chicane (Veedol) followed by NGK Chicane (tight left-right).
- Final medium right (Coca-Cola) back to Start/Finish.
- Total track character: COMPACT and TECHNICAL, rhythm of
  straight → heavy braking → complex corner → repeat.

ENVIRONMENT (German beer garden + Black Forest):
- Surrounding terrain: dark forest green #3A5C3A flat fill.
- Dense clusters of dark green triangle shapes (pine trees — Black Forest)
  surrounding the circuit closely.
- A few amber #B8860B rectangular patches (beer garden clearings with
  long wooden tables — simple brown rectangles with dots for seats).
- In one corner: a simple flat gray geometric castle ruin silhouette
  (Nürburg castle — just a few gray rectangle blocks).
- Sparse fog/mist suggestion: very subtle white translucent streaks
  across low areas.
- The overall feel: dense, enclosed, atmospheric, precision-focused.
```

---

### 🇮🇹 意大利 — 蒙扎意面赛道 `track_layout_it.png`

```
[Italy Monza] A 2D flat top-down board game race track map, 4096x4096px.
Pure 2D — NO 3D, NO perspective, NO shadows, NO gradients.

TRACK LAYOUT (Monza Circuit shape):
- The TEMPLE OF SPEED — long straights, few corners, clockwise.
- Start/Finish on a VERY LONG straight at the bottom (~25% of track length).
- After the long start straight: a tight RIGHT-LEFT chicane
  (Variante del Rettifilo — sharp, narrow, the hardest braking zone).
- A medium straight, then the sweeping CURVA GRANDE: a long,
  gradual right-hand curve spanning a large arc — almost a quarter-circle.
- Another straight, then a tight RIGHT-LEFT chicane (Variante della Roggia).
- More straight into two medium left-handers in sequence:
  Lesmo 1 and Lesmo 2 (gentle curves, close together).
- Another straight, then the VARIANTE ASCARI: a quick left-right-left
  flick — the most technical section of Monza.
- A LONG curved straight leading to the iconic PARABOLICA:
  a huge sweeping right-hand curve (~quarter-circle) that brings
  cars back onto the Start/Finish straight.
- Total track character: LONG STRAIGHTS + SWEEPING CURVES.
  The straights dominate. Curva Grande and Parabolica are the
  signature visual elements — two massive arcs.

ENVIRONMENT (Tuscan countryside + Italian pasta culture):
- Surrounding terrain: warm olive green #8B9A6B flat fill.
- Gentle rolling hill contour lines (slightly darker green curved lines).
- Tall thin dark green cypress tree shapes scattered around.
- A few warm terracotta #C4956A rounded patches (Tuscan rooftops).
- In the top-right distance: a simplified Colosseum silhouette —
  flat beige oval with arched cutouts.
- A few wooden pasta-drying rack structures near the track
  (simple geometric: two vertical brown lines with horizontal cross-bars).
- The overall feel: SUNNY, open, glamorous, speed-focused.
```

---

### 🇺🇸 美国 — 印第安纳波利斯汉堡赛道 `track_layout_us.png`

```
[USA Indianapolis] A 2D flat top-down board game race track map, 4096x4096px.
Pure 2D — NO 3D, NO perspective, NO shadows, NO gradients.

TRACK LAYOUT (Indianapolis Motor Speedway Oval shape):
- A PURE OVAL — the simplest and most iconic shape in racing.
- Counter-clockwise direction (Indy 500 tradition).
- The oval is a rounded rectangle: two long parallel straights
  (front straight and back straight), connected by four identical
  banked turns at the corners.
- Each turn is a wide, sweeping 90° curve — all four are identical
  in radius and width.
- The front straight (bottom) is slightly longer than the back straight.
- Start/Finish line: prominent checkered band across the front straight,
  near the exit of Turn 4.
- The track is WIDER than other circuits (representing the wide oval).
- Total track character: BRUTALLY SIMPLE — 4 identical turns,
  2 long straights, pure speed. NOT a road course.

ENVIRONMENT (American BBQ + Midwest):
- Surrounding terrain: warm beige-tan #C4B896 flat fill (Midwest plains).
- The INFIELD (inside the oval): a large rectangular area filled with
  small BBQ-themed elements —
  - Tiny orange-red dot clusters (charcoal grills)
  - Small red-white checkered tablecloth squares (picnic tables)
  - A simplified Indy Pagoda silhouette at the center:
    a tall rectangular tower with layered tiers, flat gray
- The OUTSIDE: mostly empty beige space with scattered small
  grandstand shapes (simple gray rectangles with tiny colored dots for seats).
- A few subtle heat-wave shimmer lines across the far straight
  (very faint, thin horizontal wavy lines — hot Midwest sun).
- The overall feel: VAST, open, American-scale, BBQ party atmosphere.
```

---

### 🇨🇳 中国 — 上海点心赛道 `track_layout_cn.png`

```
[China Shanghai] A 2D flat top-down board game race track map, 4096x4096px.
Pure 2D — NO 3D, NO perspective, NO shadows, NO gradients.

TRACK LAYOUT (Shanghai International Circuit shape):
- The track is shaped like the Chinese character "上" (shàng) —
  this is the most distinctive circuit silhouette in the game.
- Clockwise direction.
- Start/Finish on a medium straight at the bottom.
- After Start, the track immediately enters the YIN-YANG SPIRAL
  (Turns 1-3): a 270° tightening right-hand spiral that coils inward.
  Visually, this should look like a snail-shell spiral — the track
  curves right and keeps tightening until it faces downward.
- A short left flick (Turn 4) to exit the spiral.
- A short straight, then a medium right kink (Turn 5).
- Another short straight into a tight RIGHT HAIRPIN (Turn 6, ~180°).
- A short connecting section with two fast left-right sweeps (Turns 7-8).
- Two medium left-handers (Turns 9-10).
- A medium straight into the ANTING SPIRAL (Turns 11-13):
  a REVERSE spiral — starts TIGHT and gradually WIDENS.
  Visually: small radius → expanding radius, like a conch shell opening up.
- After the spiral exits: THE LONGEST STRAIGHT IN THE GAME
  (Dragon Beard Straight) — spanning ~30% of the entire track length,
  running horizontally across the canvas.
- At the end of the long straight: a TIGHT RIGHT HAIRPIN (Turn 14) —
  the heaviest braking zone. Visually dramatic contrast:
  long fast arrow → sudden sharp turn.
- A short straight, a medium right kink (Turn 15), and the final
  left curve (Turn 16) back to Start/Finish.
- Total track character: EXTREME CONTRAST — the tightest spirals
  next to the longest straight. The "上" shape should be subtly
  readable from above.

ENVIRONMENT (Shanghai skyline + dim sum culture):
- Surrounding terrain: pale gray-blue #C8CCD0 flat fill
  (urban/overcast Shanghai feel).
- A very faint, large "上" watermark character in slightly darker gray
  across the background — subtle, like a texture, not distracting.
- In the top-right distance: Lujiazui skyline silhouette —
  simplified flat geometric shapes:
  - Oriental Pearl Tower: two spheres on a tall spike
  - Shanghai Tower: a twisting tapered rectangle
  - Other buildings: flat rectangles of varying heights
- The PIT AREA (near the Anting Spiral exit): a cylindrical
  bamboo steamer building shape — large round structure with
  horizontal line texture (bamboo weave suggestion).
- Red #C41E3A neon accent lines following the track edges
  (thin, like LED strips).
- The overall feel: MODERN meets traditional, technical precision,
  dramatic scale contrasts.
```

---

### 🇯🇵 日本 — 铃鹿寿司赛道 `track_layout_jp.png`

```
[Japan Suzuka] A 2D flat top-down board game race track map, 4096x4096px.
Pure 2D — NO 3D, NO perspective, NO shadows, NO gradients.

TRACK LAYOUT (Suzuka Circuit shape):
- The ONLY FIGURE-8 CIRCUIT in the game — the track CROSSES ITSELF.
  This must be clearly visible: two sections of track overlapping,
  with the crossover point visually distinct.
- Clockwise direction overall, but the figure-8 means half the lap
  goes one way and half goes the other relative to the crossing.
- Start/Finish on a medium straight at the bottom-right.
- After Start: a fast sweeping RIGHT (First Curve, T1).
- Then a tighter RIGHT (Second Curve, T2) — heavy braking.
- Immediately into THE ESSES (S-Curves, T3-T7): a legendary sequence
  of 5-7 quick left-right direction changes.
  Visually: a snake-like wiggle — left, right, left, right, left —
  flowing up the canvas like a ribbon. This is Suzuka's signature.
- A short straight, then DEGNER CURVES (T8-T9): a medium right
  followed by a tight right — two-step increasing difficulty.
- Immediately after Degner: the CROSSOVER POINT. The track passes
  UNDER the earlier section (the back straight crosses OVER via a bridge).
  Visually: the track dips into a tunnel/darker section, while another
  track segment passes above it.
- After the crossover: a TIGHT LEFT HAIRPIN (T11, Ramen Hairpin).
- A short straight, then 200R: a FAST sweeping right curve.
- A short connecting section into SPOON CURVE (T13-14): a long
  double-apex left-hander — two connected left curves forming
  a spoon-like shape.
- A LONG BACK STRAIGHT — running across the top of the canvas,
  crossing OVER the earlier section at the figure-8 crossover bridge.
- After the back straight: the legendary 130R — a VERY FAST
  sweeping left curve. Wide, flowing, the fastest corner on the track.
- A short braking zone into the CASIO TRIANGLE CHICANE (T16-18):
  a tight right-left-right flick just before the finish.
- Back to Start/Finish.
- Total track character: THE ULTIMATE DRIVER'S CIRCUIT —
  figure-8 uniqueness, high corner density, the Esses + 130R +
  Spoon as three iconic sequences.

ENVIRONMENT (Japanese sushi culture + Suzuka surroundings):
- Surrounding terrain: deep blue-green #4A6B5C flat fill.
- At the CROSSOVER POINT: a GIANT PAIR OF CHOPSTICKS sculpture —
  two long flat brown rectangles crossing in an X or parallel
  arrangement, marking the figure-8 intersection.
- At the Start/Finish area: a simplified vermillion TORII GATE —
  two vertical red-orange #E60012 posts with a horizontal curved top bar.
- Scattered pink #FFB7C5 dot clusters (cherry blossom trees) —
  small pink circles with slightly lighter centers.
- In the top-left distance: MOUNT FUJI silhouette — a simple flat
  white triangle with a flat gray top (snow cap). Clean geometric shape.
- A few subtle curved line patterns in the background suggesting
  Japanese wave motifs (Seigaiha — very faint, just texture).
- The overall feel: SACRED racing ground, precision and tradition,
  beautiful but unforgiving. The most technical and visually
  distinctive circuit.
```

---

### 🇩🇪 德国 — 纽博格林北环耐力赛 `track_layout_de_endurance.png`

```
[Germany Nürburgring Nordschleife] A 2D flat top-down board game race track map,
4096x4096px. Pure 2D — NO 3D, NO perspective, NO shadows, NO gradients.

TRACK LAYOUT (Nürburgring Nordschleife — "Green Hell"):
- THE LONGEST TRACK IN THE GAME. ~20.8km, 73+ corners.
  The track should fill ~85% of the canvas — it's massive and sprawling.
- Clockwise direction.
- The layout is a wild, serpentine ribbon winding through dense forest.
  Unlike modern GP circuits, this is an old-school road circuit:
  NO long straights, NO rhythm — just corner after corner after corner,
  like a roller coaster drawn by a madman.
- Start/Finish at the bottom-left area (near the old pits).
- KEY SECTIONS (from Start):
  - After Start: a sequence of fast sweeping curves climbing upward —
    Flugplatz (the "airport" — a crest where cars nearly lift off),
    then a series of linked fast bends winding through the forest.
  - Adenauer Forst: a tight hairpin-like section in a forest clearing.
  - Fuchsröhre ("Fox Hole"): the track dips into a deep compression —
    visually, the road narrows between steep banks.
  - Bergwerk: a tight right-hander at the lowest point, then the climb begins.
  - THE KARUSSELL: the most famous corner in motorsport —
    a steeply banked concrete bowl corner. Visually distinctive:
    a circular carousel-like curve with banked inner wall (suggested
    by a wider, darker inner edge).
  - After Karussell: a relentless climb through Hohe Acht, Wippermann,
    Brünnchen (a spectator-favorite sweeping right with a viewing area),
    Pflanzgarten (a series of jumps and compressions).
  - Schwalbenschwanz ("Swallow's Tail"): a complex multi-apex section
    near the end.
  - A final sweeping section through Galgenkopf back to the start.
- The Nordschleife overlaps partially with the GP-Strecke layout —
  they share the Start/Finish area and final corners. But the Nordschleife
  immediately diverges into the forest while the GP circuit stays compact.
- Total track character: BRUTAL, RELENTLESS, terrifying. A narrow ribbon
  of asphalt through dense dark forest, constantly turning, climbing,
  and falling. No rest, no respite. "Green Hell."

ENVIRONMENT (German Black Forest + dark fairy tale):
- Surrounding terrain: deep dark forest green #2D4A2D flat fill
  (darker and more ominous than the GP circuit).
- EXTREMELY DENSE tree cover — the track is hemmed in by forest
  on all sides. Use tight clusters of dark green triangles (pine trees)
  crowding right up to the track edges.
- Patches of fog/mist: very subtle translucent white streaks weaving
  through the forest sections (low opacity).
- A few small amber #B8860B glowing dots scattered in the forest
  (campfires / torch lights of spectators camping in the woods —
  a Nordschleife tradition).
- At the KARUSSELL: a small spectator viewing area — simple
  geometric shapes suggesting a wooden platform with tiny colorful dots.
- Castle ruins visible at two points:
  - Nürburg castle (gray geometric ruins) near the top of the circuit
  - A second smaller ruin deeper in the forest
- The overall feel: DARK, MYSTERIOUS, intimidating.
  Like a Grimm fairy tale forest that happens to have a race track
  running through it. The track should look DANGEROUS just from
  its visual density.
```

---

### 🏁 勒芒拉萨尔特赛道 `track_layout_fr_lemans.png`

> ℹ️ **设计说明**：勒芒作为测试/中立赛道，不对应任何国家队。法国美食主题仅用于赛道环境的视觉包装。

```
[France Le Mans] A 2D flat top-down board game race track map, 4096x4096px.
Pure 2D — NO 3D, NO perspective, NO shadows, NO gradients.

TRACK LAYOUT (Circuit de la Sarthe — Le Mans 24 Hours):
- A SEMI-PERMANENT circuit: part dedicated race track, part public roads.
  This means some sections should look more "road-like" (narrower, with
  subtle road markings instead of full curb stripes).
- Clockwise direction. ~13.6km, 38 corners.
- The defining feature: THE MULSANNE STRAIGHT — one of the longest
  straights in motorsport (~6km in real life). But unlike modern Le Mans
  which has 2 chicanes breaking it up, this should show the classic
  CONFIGURATION: a MASSIVE unbroken straight with two small chicane
  interruptions.
- TRACK SHAPE (roughly a stretched triangle/oval hybrid):
  - Start/Finish on the bottom section, a medium straight with pit buildings.
  - Through the DUNLOP CHICANE: a quick right-left flick just after start,
    then a sweeping right curve (Dunlop Curve) climbing slightly.
  - TERTRE ROUGE: a fast right-hand sweeper marking the transition
    from permanent circuit to public road section.
  - MULSANNE STRAIGHT: THE defining feature. A dead-straight line
    running horizontally across almost HALF the canvas width.
    Two small chicanes (Mulsanne Chicane 1 & 2) interrupt it —
    each is a tight left-right flick, like small speed bumps on a highway.
    The straight should visually dominate the top half of the canvas.
  - MULSANNE CORNER: a tight right-hand turn at the end of the straight.
  - INDIANAPOLIS: a fast left-hand sweeper (named after the banked
    Indianapolis-like curve, but flat).
  - ARNAGE: the slowest corner on the track — a tight right-angle right
    turn in the village of Arnage (a few small geometric building shapes
    clustered here).
  - PORSCHE CURVES: a sequence of high-speed sweeping right-left-right
    curves — the most flowing section of the track, like a ribbon undulating
    through the countryside. Visually dramatic.
  - FORD CHICANES: a tight left-right-left-right flick just before
    the finish line.
  - Back to Start/Finish.
- Visual distinction: the PUBLIC ROAD sections (Mulsanne Straight area)
  should have slightly lighter gray asphalt than the permanent circuit,
  with subtle road markings (dashed lines) rather than full curb stripes.
- Total track character: THE ENDURANCE LEGEND — the longest
  single straight in the game (Mulsanne), dramatic transitions between
  permanent track and public roads, day-into-night-into-day atmosphere.

ENVIRONMENT (French countryside + wine/cheese culture):
- Surrounding terrain: warm golden-green #8A9A5E flat fill
  (French countryside in summer).
- TERRE ROUGE area: a patch of reddish-brown #A0522D soil
  near the corner (the real corner is named after the red earth here).
- Along the Mulsanne Straight: rows of poplar trees — thin tall
  geometric green ovals in neat lines (French roadside tree rows).
- Small geometric village shapes at ARNAGE and MULSANNE CORNER:
  clusters of beige/tan rectangles with red-brown triangle roofs
  (French village houses).
- Scattered vineyard patches: small green grids of tiny dots
  (grape vines in rows) near the Porsche Curves area.
- The PIT COMPLEX (Start/Finish): a long rectangular structure
  with a simple geometric grandstand.
- In one corner of the canvas: a subtle, very faint French tricolor
  watermark (three vertical bands: blue, white, red) — almost like
  a stain in the countryside texture, not prominent.
- A few small cheese-wheel shapes (flat beige circles with slightly
  darker rims — subtle, like decorative elements near the track).
- The overall feel: TIMELESS, elegant, pastoral but grand.
  The track feels woven into the French countryside rather than
  carved through it. The Mulsanne Straight is the visual anchor —
  an impossibly long line through the rural landscape.
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

## 📋 历史 AI 生成清单（非当前待办）

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
| **赛道布局** |
| 36-43 | `track_layout_*.png` ×8 | 赛道布局背景 | 4096×4096 | ✅ AI |
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

**历史规划总计：43 张**。其中大量资源已经接入或被运行时方案替代；
不得用这个数量衡量当前缺口。

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
> `design/planning/asset-manifest.md`（当前资产清单与审计状态）。
