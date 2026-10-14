# 车队实录引擎与换挡音效：来源、许可和生成证据

更新：2026-10-12（Asia/Shanghai）。本批以真实录音为主体，六队各有低、中、高声层和升/降挡短音；车队声音是创作方向，未认证为特定品牌或真实赛车的发动机录音。

## 原始来源与授权

- 五个燃油队：发布者 [muted.io — Performance Cars](https://muted.io/performance-cars/) 提供的原始 WAV 包，68 段性能车现场录音，96 kHz / 24-bit / stereo。源页明确采用 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)，可商业改编；发布者为 [Sébastien / Seb](https://muted.io/about/)。编号文件未标注品牌。原包通过源页公开下载链接取得，构建工具用公开 ZIP 成员读取并验证 ZIP CRC。
- 中国队：[Joseph Sardin — Car antenna, exit，s1263](https://bigsoundbank.com/car-antenna-exit-s1263.html)。原页说明是 Kia Carnival 伸缩天线的小型电机，48 kHz / 24-bit / mono，使用 MixPre-3 + Sennheiser ME66 实录；明确 CC0、无需账号、可商用及修改。使用原始 BWF WAV 中稳态电机片段，排除开始和末端机构触点；用于电驱声音设计，没有燃油层。它不是汽车驱动电机或赛车实录。
- 换挡机械接触：复用项目既有 `Assets/Resources/Audio/SFX/gear_shift.wav`，实际来源为 344 Audio / Car Foley 的车内风速旋钮与 Epic Stock Media / Lock And Mechanism 的卡扣录音。原始相对路径、Sonniss GDC 2026 包许可及构建记录见 [已有 GDC 来源记录](gdc-2026-sfx-provenance.md) 和 `Assets/Resources/Audio/SFX/build-manifest.json`。此层表示拨档触感，不声称是各品牌变速箱采样。换挡成品含 Sonniss 素材，沿用其游戏使用许可，不作为独立音效库再发行。

CC0 原始引擎/电机素材可用于商业游戏并改编，无强制署名；建议保留上述发布者署名。六首曾研究的 Freesound 品牌候选仍需登录取得原文件，未用于本批成品；公开 MP3 试听未替代正式录音。

原始下载地址：

- [Performance Cars 原始 ZIP](https://mutedio.s3.us-east-2.amazonaws.com/sounds/sample-packs/performance-cars-free-sample-pack-mutedio.zip)
- [s1263 原始 BWF WAV](https://bigsoundbank.com/UPLOAD/bwf-en/1263.wav)

原始缓存位于系统 `%TEMP%/foodula1_real_vehicle_audio`；Unity 资源目录只保留游戏所需的短成品。构建工具的 `--fetch-sources` 只获取五个指定 ZIP 成员和原始电机 WAV，不访问 Freesound 或预览地址。

## 原始文件与 SHA256

| 车队 | 原始文件 | SHA256 |
|---|---|---|
| UK | `015-performance-cars-mutedio.wav` | `37e74bcf4ed1b4995cbabd30b687bed87c8fe21737061797a53d7afcf5d07676` |
| DE | `038-performance-cars-mutedio.wav` | `0b96440e1ed2ccc75d5d7d65723e86e08160762b11d9d8ed785edfc8f695c76b` |
| IT | `042-performance-cars-mutedio.wav` | `0fbdbce27d541f98544d62a157df4d0df3474b7cf5ddd1ab67565ac52dd7e4ba` |
| US | `007-performance-cars-mutedio.wav` | `b0e36bd584640dc84309297ab9875673c0f4b412d8d0cace9013b6601cfdc0a6` |
| CN | `bigsoundbank_1263.wav` | `8b7daf4209c70d21fade84f5cefa759139238110c2d005bc915da25aeb4cf792` |
| JP | `033-performance-cars-mutedio.wav` | `010d3d0df20eb0da606c5faf3c3c9febf9e2267475f7b7b1264405a840411729` |

机械接触输入 `gear_shift.wav` SHA256：`10cbbe175d9ae05b39da11403ea33c5d859f9a99a202cbf4918a22ab7f84c27b`。

## 处理方式与稳定片段

- 五个燃油队分别取不同编号录音，中国队单独取实际电机。真实片段经轻度高/低通、去直流、峰值限制和响度归一；没有振荡器、合成发动机或生成噪声。
- 择段拒绝峰值/RMS 比超过 6.25、12 个子段 RMS 变化系数超过 0.4 的候选，减少孤立触点与突变。中国队额外排除原录音前 1.4 秒和末 1.1 秒。
- 每层先截取 1.18 秒，再用 55 ms 原录音首尾重叠生成 1.125 秒循环，并匹配首末 PCM 样本。运行时专用三源等功率交叉淡入，使用绝对已走格数及车队饱和阈值；2026-10-12 听感反馈后扩大连续升转范围，时钟仍不随慢动作变化。原始与 30 份成品 PCM 未改。
- 升挡使用实录高转到中转的短下降；降挡使用实录声层构成短补油。UK 机械触点有两段拨动，DE/JP 较短促，US 更低沉；CN Go 接合上行、Recover 回落，复用真实机械触点。

`source_tonal_peak_hz` 是择段的频谱主峰指标，不是实车测量 RPM；这里的低/中/高层是游戏声音的编排顺序。

| 车队 | 低/中/高原录音起点（秒） | 低/中/高频谱指标（Hz） | 低/中/高成品 RMS（dBFS） |
|---|---|---|---|
| UK | 4.08 / 9.48 / 1.92 | 69.49 / 94.92 / 155.93 | -21.0000 / -21.0000 / -23.1667 |
| DE | 13.92 / 15.24 / 10.32 | 79.66 / 83.05 / 433.90 | -22.3636 / -22.4465 / -21.4395 |
| IT | 3.24 / 6.96 / 2.64 | 291.53 / 355.93 / 415.25 | -23.0065 / -22.9140 / -22.2431 |
| US | 1.32 / 5.88 / 11.88 | 44.92 / 55.08 / 77.12 | -25.3652 / -23.0301 / -21.2243 |
| CN | 4.44 / 3.60 / 2.88 | 434.75 / 1390.68 / 1440.68 | -25.2095 / -22.1449 / -21.1878 |
| JP | 1.80 / 17.16 / 15.36 | 102.54 / 408.47 / 531.36 | -21.8556 / -21.0000 / -21.0000 |

## 重生成与 PCM 检查

2026-10-12 首次录音修订的本地 Python / NumPy 实测：

- 对同一原始缓存连续重生成两次；30 个成品、profile JSON 和 comparison WAV 共 32 文件，SHA256 全部与重生成前相同。
- 30 个游戏成品：18 循环、12 短音；统一 48,000 Hz / 16-bit / mono PCM，总计 2,290,920 bytes。
- 数字满幅样本数 0；循环峰值不高于 -9.9999 dBFS、短音峰值不高于 -7.9999 dBFS（16-bit 量化舍入）。循环 RMS 范围 -25.3652 至 -21.0000 dBFS。
- 18 循环首末样本差全部为 0；接缝两侧斜率变化最大 0.02331543（相对满幅）。端点检查证明没有幅值跳变，不替代实际听验。
- 已补三声层资源加载、源隔离、等功率交叉淡入、pitch 上限、静音、淡出、所有者失活、真实场景卸载与迟到 fade 保护的 Editor/native 播放测试。Unity 运行及完整赛事听验由主线程统一执行并在 QA 记录结果，本清单不提前声明通过。

重建命令（Python 需要 NumPy）：

```powershell
python tools/Audio/build_team_vehicle_audio.py --source-dir "<原始 WAV 缓存目录>"
# 需要从发布者重新取得原始文件时：
python tools/Audio/build_team_vehicle_audio.py --fetch-sources --source-dir "<原始 WAV 缓存目录>"
```

若发布者网络不可达，保留已有成品并报告下载失败；不用预览文件替代原始录音。每个成品的来源起点、峰值、RMS、长度及 SHA256 同时写在 `Assets/Resources/Audio/team_vehicle_profiles.json` 的 `generation.outputs`。

## 对比试听索引

[六队对比 WAV](../../tools/Audio/team_vehicle_comparison.wav)：23.10 秒。每段依次为 2.50 秒低到高声层过渡、短停顿、升挡、短停顿、降挡，再留间隔。该文件用于音色对比，不包含游戏音乐或设置音量，不是完整赛事听验。

| 车队 | 起点（秒） | 下一队起点（秒） |
|---|---|---|
| UK | 0.00 | 3.85 |
| DE | 3.85 | 7.70 |
| IT | 7.70 | 11.55 |
| US | 11.55 | 15.40 |
| CN | 15.40 | 19.25 |
| JP | 19.25 | 23.10 |

当前对比 WAV SHA256：`4993a0fd1933888a6ed71725d4634d54bbd061b953bdc5ddb42374140a33aa44`。更新于 2026-10-12 增益／升转调音；首版 `85b297f15ac7d3acb108fbf113f2dded90044dae0594bfbb3271372f8fe333e7` 是历史试听版本。

## 2026-10-12 增益与连续升转调整

用户反馈引擎偏小、拉转不明显，更新运行时配置与重建工具，保留录音层／来源／30 份成品字节。引擎增益提升 6.444–7.246 dB，10–11 格达到高转；前进 6 格的连续 pitch 上升约 4.410–5.453 个半音，另有录音层变化。主音量／音效音量、静音和慢动作时钟规则保持。

| 车队 | 旧／新增益 | 新 pitch 范围 | 高转格数 |
|---|---|---|---|
| UK | 0.36 → 0.78 | 0.92 → 1.44 | 10 |
| DE | 0.34 → 0.74 | 0.94 → 1.46 | 11 |
| IT | 0.32 → 0.72 | 0.96 → 1.52 | 10 |
| US | 0.40 → 0.84 | 0.88 → 1.36 | 10 |
| CN | 0.31 → 0.70 | 0.94 → 1.44 | 11 |
| JP | 0.33 → 0.76 | 0.94 → 1.52 | 10 |

独立临时目录再次重建 30 成品＋配置＋23.1 秒试听，32 文件 SHA256 与工作区当前版本完全一致；成品与调音前的 30 个 WAV 哈希也全部一致。Engine 三层相关信号的保守峰值上界最高 -8.504 dBFS；这是仅引擎的计算边界，不称为全混音限幅验收。

[33.6 秒旧／新对比](../../tools/Audio/team_vehicle_tuning_comparison.wav) 各队先旧 2.5 秒、间隔 0.15 秒、再新 2.5 秒、队间 0.45 秒，均按前进 0–12 格计算，整段统一归一保留相对音量差；顺序 UK → DE → IT → US → CN → JP。SHA256 `9f94dc1043b874d6434f8bf25b917223f9baadafc3a612b51c36081e99417975`。定向 41/41、实际播放器 3/3 的结果及启动耗时证据见 [验收](../../production/qa/evidence/play-entry-and-engine-tuning-2026-10-12.md)。

## 成品 SHA256 与峰值

路径前缀为 `Assets/Resources/Audio/`；循环层按文件后缀标明中/高层，无后缀为低层。

| 成品 | 峰值 dBFS | SHA256 |
|---|---|---|
| `team_engine_uk.wav` | -12.8330 | `41363041e2bb4ebcbc2f7a6e8346477248ece69e1d05112f2792ba2bfedd0bf7` |
| `team_engine_uk_mid.wav` | -10.6733 | `a53f7325ca6e61a0e913a9d33a2600eaf260520036519d50543bac79ac7e27e0` |
| `team_engine_uk_high.wav` | -9.9999 | `01564faa03adcda74721c3c179e84ada6ff4f8595e0e5e359b7e74ca5b8a94de` |
| `team_shift_uk_up.wav` | -7.9999 | `ee528c57586fdb43461cac44323129268a04704423c91f58fded3b7918838c3d` |
| `team_shift_uk_down.wav` | -7.9999 | `cad1dc8ca916550e9d54ee638ce54b2c95a379ecac0ba81596b3bdc0dc9e924d` |
| `team_engine_de.wav` | -9.9999 | `b03ab7004ba60f19c756850b87c898331eb34190a5511a72bdb5d975e4fee0a3` |
| `team_engine_de_mid.wav` | -9.9999 | `0a48f3364e64e7fe50d8c3bc9cb6731beed875cad46562509dcf5c7d023e70b9` |
| `team_engine_de_high.wav` | -9.9999 | `5cab9d586f679bc5e1bb95dc535203b8d4029f4d195725be884c5cccec8051f4` |
| `team_shift_de_up.wav` | -7.9999 | `65db7d98805fe2fd98ab650eb165d39fee68e9f08094f4c97da1286c8362aecc` |
| `team_shift_de_down.wav` | -7.9999 | `0f66f01aeed37367697e9dae3053d78d8df90e8872ffb252411137ccd20bfab2` |
| `team_engine_it.wav` | -9.9999 | `94f0aa1a0fd113da2785c95702793c9a0775e8cad9d70aab3e925e99a458e72d` |
| `team_engine_it_mid.wav` | -9.9999 | `6530f8ece9ba8212ab25b771c49fa60b08c6ec5adbd6b6be8e73ff9eda8e2944` |
| `team_engine_it_high.wav` | -9.9999 | `9b1568bbb379ab0c596555a5d9b2e9d66b9d6f0716183a2e02d986c95942aac3` |
| `team_shift_it_up.wav` | -7.9999 | `a57a7ccb4027d0b2933d3685c5bf1d96f6b853e1aa50151cc30f4d69a2b2255b` |
| `team_shift_it_down.wav` | -7.9999 | `c6160bac8898cbfc83ff4bbc65d63f03abdacdca2fbc01d2cccb8089f262c0a1` |
| `team_engine_us.wav` | -9.9999 | `2f5220642fc4f2259d8aa9902c4000fe19e683498d9157b6a19850593d8c3812` |
| `team_engine_us_mid.wav` | -9.9999 | `bd5ab9bc127bfd7f9cb1f73cea523c7bbb5bc203264977b8c6d537c518708dd7` |
| `team_engine_us_high.wav` | -9.9999 | `159094543a5a5734bb6ec624a0053d589f015fdc5a649769d59e7091fa40e78e` |
| `team_shift_us_up.wav` | -7.9999 | `96a86665bd8de86547adc63dd965d0534528e0d22bafb0542372e2756f18af99` |
| `team_shift_us_down.wav` | -7.9999 | `01960bca2dac910db96605dcd1e356732a6006c98609d46e8d34c0111ccbbd8a` |
| `team_engine_cn.wav` | -9.9999 | `11c09ce295faf4477fca1c759205db0f68df17ffaa589d6be1c3fd82578c9f63` |
| `team_engine_cn_mid.wav` | -9.9999 | `a8e25f8fd1e0d86f63387138816cdaa189c7d76b354802298b1bf5282c066e51` |
| `team_engine_cn_high.wav` | -9.9999 | `b6284b9edfb247302e5e13df511e9c6f5a8cbc0d1d1b590664ca333722f4d409` |
| `team_shift_cn_up.wav` | -7.9999 | `d662327e2d9ebcf890afd2aaaaa00ae3c91100e6e29ee36d73ad4d175cc7faa6` |
| `team_shift_cn_down.wav` | -7.9999 | `7f964f4a9b11fee8a80bb39e895078904d6f46a5f867ff7ccea9675bbee5fa86` |
| `team_engine_jp.wav` | -9.9999 | `56018c6cf89771456759a158bfbad8c22106e5fed79c5a80bf65222266148f9a` |
| `team_engine_jp_mid.wav` | -11.7357 | `35b7d95dde79f22a64215eff284b5ef3dbae91b16bc3692de01fe8630c81100c` |
| `team_engine_jp_high.wav` | -16.4518 | `06a23cd4b8f106e2372204f58501759da5334d627e4a4f56e343cf30728807b7` |
| `team_shift_jp_up.wav` | -7.9999 | `e21f10c98297dd2b84d7e87479d0569e315726e54a615451ff839703b933f42a` |
| `team_shift_jp_down.wav` | -7.9999 | `193d3a359a45e3b7457a4ee191c90112f842eb738791c7e7df6ab63086792e4c` |

## 2026-10-15 换挡声音迁移：新 CC0 记录优先

用户已明确选择公开提交前用 CC0 替换换挡短音。本文上方十二份 `team_shift_{uk/de/it/us/cn/jp}_{up/down}.wav` 及机械触点处理仅为历史版本，旧 WAV 和其十二份 meta 保留本地，不纳入本次公开提交；旧试听/比较音亦排除。原十八份 CC0 引擎/电机声层保持。

已接入的新十二份 `team_shift_cc0_*` 不读取 Sonniss 机械层，其来源、构建、配置与成品哈希由 [team-shift-cc0-provenance.md](team-shift-cc0-provenance.md) 统一记录，本文旧换挡表不作为新版本权威。2026-10-15 两个原发布页 CC0 与公开再分发权限已独立核验，证据和旧/新许可边界见 [许可归档 README](licenses/README.md#新-cc0-来源独立核验2026-10-15)。
