# Foodula1 — GDC 2026 核心音效来源与处理记录

> 更新日期：2026-09-03  
> 原始包：Sonniss.com — GDC 2026 Game Audio Bundle（本地 Part 9 / 4 of 5）  
> 许可证：随包 `Readme.txt` 与 `License - GDC Game Audio.pdf`；允许个人及商业项目使用，
> 无强制署名。发布前仍应把随包许可证与构建归档一同保存。

## 接入原则

- 原始下载目录只读，项目不包含未经裁切的长录音。
- `Tools/Audio/build_gdc2026_sfx.py` 以文件名定位素材，生成固定结果。
- 所有成品统一为 48 kHz、立体声、16-bit PCM WAV，峰值归一到不高于 -3 dBFS。
- 长素材仅截取短促事件段；部分事件由两个相近素材轻量分层，避免引入与玩法无关的写实噪声。
- 运行时仍需在真实比赛中听验密度、语义和音乐遮蔽；本记录不替代主观听验。

## 来源映射

| 来源素材 | 供应者/素材包 | 主要派生事件 |
|---|---|---|
| `UIClick_UI Button Analog Vintage Double Click Neutral Dry Press 11_ESM_BG.wav` | Epic Stock Media / Board Game | UI 悬停、确认、返回 |
| `PAPRHndl_Game Play Cards Dry Show Flip Toss Disgard Near 12_ESM_BG.wav` | Epic Stock Media / Board Game | 选牌、取消、打出 |
| `GAMECas_Dealing 3...wav`、`GAMECas_Pick Up Multiple Cards At Once 5...wav`、`GAMECas_Automatic Shuffler...wav` | 344 Audio / Casino Cards | 抽牌、弃牌、洗牌 |
| `VEHInt_CLIMATE CONTROL SYSTEM, FAN SPEED DIAL, FAST...wav` | 344 Audio / Car Foley | 换挡 |
| `MECHLtch_Click Deep Mechanism Latch Button Nearfield Thunk 02...wav` | Epic Stock Media / Lock And Mechanism | 换挡、失败、卡牌与车辆落点分层 |
| `OBJMisc_Spray Bottle, Spray 1...wav` | 344 Audio / Barbershop | 冷却热量 |
| `ELECArc_ArcPowerUpDesign04...wav` | InMotionAudio / Arc | 支付热量 |
| `WINDDsgn_Wind, Rush, Whoosh, Long x5 01...wav` | 344 Audio / Elemental Palette | 尾流推进 |
| `METLMisc_Metal, Slow Whoosh, Rattle, Pass By x4 01...wav` | 344 Audio / Elemental Palette | 尾流触发、弯道超速与失控分层 |
| `WIR006.wav` | TheWorkRoom / Wire Cars | 逐格移动、弯道安全 |
| `CRWDCheer_Small Club, 50 People...wav` | Sonik Sound Library / Spanish Crowds | 完赛欢呼 |

完整相对路径和全部输出文件名由
`Assets/Resources/Audio/SFX/build-manifest.json` 自动记录。成品不得被单独转售或重新发布为音效库。

## 当前成品范围

- UI：悬停、确认、返回、无效操作。
- 卡牌：选择、取消、打出、主动弃牌、抽牌、洗牌。
- 核心规则：换挡、挡位失败、支付/冷却/警告、逐格移动、尾流触发/移动、弯道安全/超速、失控。
- 比赛流程：过圈、最后一圈提示、完赛。
- 菜单与比赛音乐来自用户生成的 `menu.mp3`、`race.mp3`，不属于 GDC 包；生成平台与授权证明仍需用户自行归档。

## 待听验与后续

- 在主菜单与一局完整比赛中检查重复触发、批量事件限频和音乐遮蔽。
- P1 的维修区、最后一圈、完赛、科技树、车手/赛道选择及天气环境声尚未由本批成品覆盖。
- 若某个成品语义不符，应优先修改构建脚本参数或替换来源，再重新生成，避免直接手工改 WAV 后失去可追溯性。
