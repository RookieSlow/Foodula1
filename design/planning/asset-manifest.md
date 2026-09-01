# Foodula1 — Demo 资源清单

> **用途**：当前版本的美术、字体、音乐与音效资源事实表。
> **审计日期**：2026-09-01。
> **状态含义**：✅ 已有并接入；🟨 运行时生成/占位可用；⬜ 尚缺；◻ 可选升级。
> **事实来源**：`Assets/` 文件、当前 UI/Gameplay 代码和运行时配置；旧版 42 格赛道与未来架构草案不再作为资源缺口。

---

## 一、当前结论

- 核心比赛视觉已经达到 Demo 可玩基线：9 张卡牌图、6 辆车、8 张 4K 赛道布局、档位旋钮、中文 TMP 字体均已存在并接入。
- 主菜单正式背景图与透明 Logo 已通过统一 `Resources/Brand` 加载边界接入，资源缺失时保留旧标题回退。
- 六队原创抽象徽章已接入车手选择、生涯选队、科技树标签和比赛头顶名次徽标；12 位车手仍缺正式头像。
- 科技树规则、存档和菜单界面已接入，但树状背景、节点图标和节点状态框仍由运行时代码绘制。
- `Assets/Audio/Music/` 与 `Assets/Audio/SFX/` 已建目录但没有实际音频文件；
  代码中也没有 `AudioSource`、`AudioClip`、`AudioMixer` 或音频管理器，音乐与音效尚未接入。
- 尾流、失控、卡牌流转、冷却提示、弯道遮罩等已有运行时表现。独立 FX 贴图不是 Demo 阻塞项，除非后续美术替换能保持“极简克制”的视觉规范。

---

## 二、Demo 封版前建议补齐的美术资源（P0）

| 资源 | 建议文件 | 规格 | 用途与验收 | 状态 |
|---|---|---|---|---|
| 主菜单背景 | `Assets/Resources/Brand/main_menu_background.png` | 1672×941 PNG，16:9 | 赛道俯视剪影、餐车元素与深空黑留白；运行时全屏铺设 | ✅ |
| 游戏 Logo | `Assets/Resources/Brand/foodula1_logo.png` | 2048×768 透明 PNG | 统一使用 **Foodula1**；主菜单等比显示 | ✅ |
| 科技树背景 | `Assets/Sprites/TechTree/tech_tree_background.png` | 3840×2160 PNG，16:9 | 深灰蓝电路/路线图底纹，不抢节点文字 | ⬜ |
| 科技节点框 | `Assets/Sprites/TechTree/tech_node_{locked,available,active}.png` | 360×160，9-slice | 明确区分锁定、可解锁、已激活三态 | ⬜ |
| 科技层级徽章 | `Assets/Sprites/TechTree/tech_tier_{l1,l2,l3}.png` | 128×128 透明 PNG | L1/L2/L3 视觉层级，不依赖颜色作为唯一信息 | ⬜ |
| 六队徽章/国旗 | `Assets/Resources/Brand/team_{uk,de,it,us,cn,jp}.png` | 1254×1254 透明 PNG | 原创抽象队徽；车手卡、生涯选队、科技树与赛车头顶标识共用 | ✅ |
| 12 位车手头像 | `Assets/Sprites/Drivers/driver_<driver_id>.png` | 768×768 透明 PNG | 两位/队；肩部以上、统一视角与光源、圆形裁切安全区 80% | ⬜ |
| 8 条赛道选择缩略图 | `Assets/Sprites/Track/Thumbnails/track_<track_id>.png` | 640×360 PNG | 可从现有 4K 赛道图派生，叠加赛道名/天气不应烘焙进图 | 🟨 可派生 |

### 车手头像文件 ID

| 车队 | 文件 ID |
|---|---|
| 英国 | `uk_hunter_hart`、`uk_nigel_mansell` |
| 德国 | `de_michael_schumacher`、`de_sebastian_vettel` |
| 意大利 | `it_alberto_ascari`、`it_tazio_nuvolari` |
| 美国 | `us_tony_stewart`、`us_kyle_busch` |
| 中国 | `cn_zhou_guanyu`、`cn_ma_qinghua` |
| 日本 | `jp_keiichi_tsuchiya`、`jp_takumi_fujiwara` |

> 头像是对历史/现实车手原型的风格化致敬，不应直接描摹受版权保护的照片；发布前还需统一确认肖像、姓名和商业使用边界。

---

## 三、后续视觉增强资源（P1/P2）

| 优先级 | 资源 | 建议规格 | 当前替代方案 | 状态 |
|---|---|---|---|---|
| P1 | 天气图标 5 枚 | 128×128：晴、多云、小雨、大雨、炎热 | HUD 文字 | ⬜ |
| P1 | 比赛结果/领奖台底图 | 1920×1080 或 9-slice 面板 | 运行时纯色面板 + TMP | ⬜ |
| P1 | 12 张车队特技牌专属插画 | 1024×1536，沿用卡牌安全区 | 通用牌面 + 文字 | ⬜ |
| P1 | 车队/车手选择卡框 | 600×360，9-slice | 运行时纯色按钮 | ⬜ |
| P1 | 设置页音量图标 | 96×96：音乐、音效、静音 | 尚无音频设置 | ⬜ |
| P2 | 主菜单轻量动画层 | 透明前景层或 Unity 运行时位移 | 静态背景 | ◻ |
| P2 | 尾流箭头、冷却光点美术替换 | 64–256px 透明 PNG | `RaceEventFX` 运行时绘制 | ◻ |
| P2 | 失控/过线附加图形 | 128–256px 透明 PNG | 运行时旋转、文字与颜色脉冲 | ◻ |

禁止把轮胎火花、尾焰、粒子爆炸、速度线或屏幕震动列为正式需求；它们与 `foodula-1-visual-style.md` §6.2 冲突。

---

## 四、已存在并接入的资源

### 卡牌 `Assets/Sprites/Cards/`

| 文件 | 源尺寸 | 状态 |
|---|---:|---|
| `card_speed_bg.png` | 1024×1536 | ✅ |
| `card_heat_bg.png` | 1024×1536 | ✅ |
| `card_selected_overlay.png` | 1024×1536 | ✅ |
| `card_back.png` | 1696×2528 | ✅ |
| `card_num_1.png` … `card_num_4.png` | 1024×1536 | ✅ |
| `card_heat_icon.png` | 1024×1536 | ✅ |

### 赛车 `Assets/Sprites/Cars/`

`car_uk.png`、`car_de.png`、`car_it.png`、`car_us.png`、`car_cn.png`、`car_jp.png` 均已存在并接入；源图宽度为 256–288px、高度为 128–144px。

### 赛道 `Assets/Sprites/Track/`

- 8 张 `track_layout_*.png` 均为 3840×2160，并与 8 条官方 JSON 赛道接入。
- `track_surface_tile.png`、`track_curb_tile.png` 均为 2048×2048。
- 节点编号、弯道/弯心遮罩、限速和玩家位置由运行时绘制。
- 旧清单中的 `track_straight.png`、`track_apex.png`、`track_start_finish.png`、`track_bg_demo.png` 已退役，不再制作。

### UI 与字体

- `Assets/Sprites/UI/gear_knob_bg.png`：2048×2048，已接入。
- 热量温度计、面板、按钮状态和牌堆层数均由 uGUI 运行时绘制；独立 9-slice 图可作为后续美术替换，但不是功能缺失。
- 中文 TMP 已有思源黑体/思源宋体/Noto Sans 等资源；`Assets/ttf/` 也已有多种等宽与标题字体。旧清单中的 Inter、JetBrains Mono、NotoSansSC 下载项不是 Demo 阻塞项，新增字体前必须先核对许可证与构建体积。

---

## 五、音乐资源需求

详细风格、循环与混音规则见 `design/gdd/foodula-1-audio-style.md`。

| 优先级 | 文件 | 时长/格式 | 用途 | 状态 |
|---|---|---|---|---|
| P0 | `bgm_menu.ogg` | 90–150 秒，无缝循环 | 主菜单、车队/车手选择、科技树 | ⬜ |
| P0 | `bgm_race.ogg` | 120–180 秒，无缝循环 | 比赛常态，避免压住规则提示 | ⬜ |
| P1 | `bgm_final_lap.ogg` | 45–90 秒循环或节奏层 | 最后一圈；也可由比赛 BGM 增强层替代 | ⬜ |
| P1 | `stinger_finish_win.wav` | 3–6 秒 | 玩家完赛/胜利 | ⬜ |
| P1 | `stinger_finish_other.wav` | 2–4 秒 | 非冠军完赛 | ⬜ |

---

## 六、音效资源需求

### P0：交互与核心规则反馈

| 事件 | 建议文件 | 听觉目标 | 状态 |
|---|---|---|---|
| 按钮悬停/确认/返回 | `ui_hover.wav`、`ui_confirm.wav`、`ui_back.wav` | 短、轻、无刺耳高频 | ⬜ |
| 无效操作 | `ui_error.wav` | 清楚但不惩罚玩家耳朵 | ⬜ |
| 选牌/取消 | `card_select.wav`、`card_deselect.wav` | 纸牌轻触与机械卡扣 | ⬜ |
| 打出/主动弃牌 | `card_play.wav`、`card_discard.wav` | 方向感不同，能区分“使用”和“放弃” | ⬜ |
| 抽牌/洗牌 | `card_draw.wav`、`deck_shuffle.wav` | 短纸牌声；批量时限频 | ⬜ |
| 换挡/挡位失败 | `gear_shift.wav`、`gear_failure.wav` | 机械拨档；失败带低沉卡滞 | ⬜ |
| 支付热量 | `heat_pay.wav` | 温暖、受控的压力释放声，非爆炸 | ⬜ |
| 冷却热量 | `heat_cool.wav` | 清脆、短促的降温声 | ⬜ |
| 热量警告 | `heat_warning.wav` | 可识别但不持续轰鸣；设置冷却时间 | ⬜ |
| 逐格移动 | `car_hop.wav` | 轻量棋子/悬架落点，连续播放需限频 | ⬜ |
| 尾流阶段 | `slipstream_trigger.wav`、`slipstream_move.wav` | 先确认触发，再用短气流推进；仅在独立尾流阶段播放 | ⬜ |
| 弯道成功/超速/失控 | `corner_safe.wav`、`corner_over.wav`、`spin_out.wav` | 三种结果清晰可分；失控不使用重撞击爆炸声 | ⬜ |

### P1：比赛流程与外围系统

| 事件 | 建议文件 | 状态 |
|---|---|---|
| 圈数更新/最后一圈/完赛 | `lap_cross.wav`、`final_lap.wav`、`finish.wav` | ⬜ |
| 维修区询问/进入/维护/驶出 | `pit_prompt.wav`、`pit_enter.wav`、`pit_service.wav`、`pit_exit.wav` | ⬜ |
| 科技解锁/激活/无足够 RP | `tech_unlock.wav`、`tech_activate.wav`、`tech_denied.wav` | ⬜ |
| 选择车队/车手/赛道 | `team_select.wav`、`driver_select.wav`、`track_select.wav` | ⬜ |
| 排名变化 | `rank_up.wav`、`rank_down.wav` | ⬜ |
| 小雨/大雨环境循环 | `amb_light_rain.ogg`、`amb_heavy_rain.ogg` | ⬜ |

### 音频技术规格

- 音乐：OGG Vorbis、44.1/48kHz、立体声、无缝循环；建议目标综合响度约 -16 LUFS。
- 短音效：WAV、44.1/48kHz；无空间方向需求的 UI 声保持 2D，建议峰值不高于 -3 dBFS。
- 至少分为 `Music`、`SFX`、`UI` 三个 Mixer Group，并在设置中提供独立音量和静音。
- 同类高频事件必须限频/合并：逐格移动、批量抽弃牌和热量批处理不能形成连续噪声墙。
- 所有资源必须记录作者、来源、许可证和是否允许商业发布；AI 生成音频同样需要保存生成平台及授权条款。

---

## 七、Prefab 与运行时替代说明

| 项目 | 当前事实 |
|---|---|
| `Assets/Prefabs/UI/RaceCanvas.prefab` | ✅ 当前比赛 HUD 的可编辑权威 Prefab |
| 独立 CardSlot/GearButton Prefab | 🟨 当前内嵌于 HUD/运行时构建，不是缺失功能 |
| 独立 TrackNode/CarEntity Prefab | 🟨 当前由既有 Prefab 和运行时组件组合完成，不是 Demo 必需重构 |
| Effects Prefab/贴图 | 🟨 当前由 `RaceEventFX` 与卡牌流转动画生成；美术替换可选 |
| `Garage.unity` | ◻ 未来独立车库目标；当前主菜单已承担车队、车手与科技树配置 |

---

## 八、制作顺序与验收

1. **先完成 Demo 验收**：确认完整比赛、尾流、弃牌、维修区、天气和结果返回无规则问题。
2. **视觉身份包**：主菜单背景 + Logo + 六队徽章 + 12 车手头像 + 科技树背景/节点状态。
3. **核心音频包**：菜单/比赛 BGM + P0 交互和规则音效 + 音量设置。
4. **派生资源**：由现有 4K 赛道图导出 8 张选择缩略图，避免重新绘制并产生风格漂移。
5. **P1 打磨**：天气、结果、特技牌插画和外围系统音效。

每批资源接入前应检查：目标分辨率、透明安全区、16:9/超宽屏裁切、TMP 可读性、许可证和构建体积。接入后至少进行一次 1920×1080 与 2560×1440 Play Mode 截图验收。

---

> 关联：`design/gdd/foodula-1-visual-style.md`、`design/gdd/foodula-1-audio-style.md`、`design/planning/demo-framework.md`、`design/planning/roadmap.md`。
