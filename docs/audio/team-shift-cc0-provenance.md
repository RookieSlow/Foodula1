# 六队 CC0 换挡短音

2026-10-15。用户明确要求公开提交前替换含 Sonniss 衍生接触层的旧换挡音。新文件仅使用已核验 CC0 的实录引擎/电机声层，不读取旧 `team_shift_*`、`gear_shift.wav` 或任何 Sonniss 音频。旧文件留在本地，禁止作为本次公开提交内容。

## 来源与许可

- UK/DE/IT/US/JP：Sébastien / muted.io，[Performance Cars](https://muted.io/performance-cars/)，[原始 WAV 包](https://mutedio.s3.us-east-2.amazonaws.com/sounds/sample-packs/performance-cars-free-sample-pack-mutedio.zip)。五队原始编号分别为 015/038/042/007/033。原录音 SHA256 及18个现有声层的 SHA256 写入构建清单。
- CN：Joseph Sardin，[Car antenna, exit s1263](https://bigsoundbank.com/car-antenna-exit-s1263.html)，[原始 BWF](https://bigsoundbank.com/UPLOAD/bwf-en/1263.wav)。实录汽车伸缩天线小电机，用作电驱方向设计，不宣称是赛车动力电机。
- 两个发布来源均按已核验的 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) 使用，允许修改、商业使用及再分发，无强制署名。本文件保留作者和源链接。不存在品牌/车型声音认证。

本轮复用本地已核验录音声层，没有重新下载；原始录音的来源与处理见[原有记录](team-vehicle-recordings-provenance.md)。该旧记录包含历史机械层说明，不代表新短音含该层。

## 可复现处理

工具 [build_cc0_team_shifts.py](../../tools/Audio/build_cc0_team_shifts.py) 仅用 Python 标准库 wave/struct/math/hashlib/json。先把18个输入逐一与原生成清单的预期 SHA256 比对，再从各队自身声层线性重采样、余弦交叉淡入和渐入/渐出；录音为主体，没有合成噪声或触点叠加。普通队升挡高转回落、降挡短补油；CN Go 接合上行、Recover 回落。每队 up/down 独立成品，0.16–0.24秒，48kHz/16bit/mono；18/22ms边缘淡化、端点0、RMS目标−22dBFS且峰值上限−9dBFS。

```powershell
python tools/Audio/build_cc0_team_shifts.py
```

输出暂存于 `tools/Audio/cc0-shift-staging/`：12个 `team_shift_cc0_{team}_{up/down}.wav` 与 `manifest.json`。清单含输入/原始录音/成品哈希、源页、许可、作者、帧数、时长、处理参数、峰值/RMS、端点及削波检查。工具自身禁止直接写 Assets；运行时接入需在 Unity 测试结束后单独完成。重复构建以字节哈希核验确定性。

## 接入边界

已复制到 `Assets/Resources/Audio/SFX/` 下12个新文件名，每个使用唯一新 GUID；`team_vehicle_profiles.json` 的运行时 profiles 仅替换六队共12个 up/down 资源路径。generation 同步为新12份成品证据，并把 shift_contact 明确改为无接触层、仅 CC0 录音，关联新工具/清单；18个引擎输出记录及六个来源记录保持。原引擎声层、全部数值参数、玩法、旧12份 WAV 和旧12份 meta 均保留；接入前后核验旧24文件 SHA256 完全相同。

新增 `TeamShiftCc0RuntimeTests.AllTwelveConfiguredCc0CuesContainPcmAndPlayOnAnOwnedAudioSource`，以独占临时 AudioSource 加载配置指向的新12份资源，检查非静音 PCM、端点与实际播放状态并清理。2026-10-15北京时间，本轮原生定向 **41/41** 通过，失败/跳过/不确定均为0，包含该12份音频实际播放用例；原生结束证据见 [cc0-audio-focused-41-native.json](../../production/qa/evidence/racing-tech-instruments-2026-10-14/cc0-audio-focused-41-native.json)。临时播放器验证不替代完整赛事 AudioService 链路或主观听感验收。

集成哈希与配置路径审计见 [cc0-integration-audit.json](../../production/qa/evidence/racing-tech-instruments-2026-10-14/cc0-integration-audit.json)。旧 meta 在 CC0 接入前曾按用户提交要求清理行尾空格；上文“旧24文件保持”指 CC0 接入这个动作的前后比较，不混同更早 UI 基线。旧录音字节未变，旧 meta 的 GUID/非空内容保持。
