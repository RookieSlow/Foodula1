# 超车录音公开许可记录

首次核验/下载：2026-10-15；本轮重新打开核验：2026-10-16（Asia/Shanghai）。

- 作者：Joseph SARDIN；发布者：[BigSoundBank — Cars: Passages s0556](https://bigsoundbank.com/cars-passages-s0556.html)。普通道路六辆汽车70–90km/h室外路过实录，不称为F1或特定车型。
- 源页License明确CC0；源页的许可说明允许修改、处理、混音与再分发（包括游戏/应用），允许商业项目，无强制署名。因此本项目可公开源录音、派生游戏音效及制作脚本，不代表作者背书或商标授权。此依据是本轮实际打开的录音源页，不声称另外的FAQ网页已成功打开。
- [CC0 1.0官方法律原文](https://creativecommons.org/publicdomain/zero/1.0/legalcode.en)；本地保存原件：[CC0-1.0-original.html](../CC0-1.0-original.html)，SHA256 `B43732F4C1463B087CF10D4DAF8D8766514B2BF8F21BC5EF50905F95BE227726`。本轮未改许可证原件。

| 文件 | 项目路径/来源 | SHA256 |
|---|---|---|
| 原始BWF录音 | [官方WAV](https://bigsoundbank.com/UPLOAD/bwf-en/0556.wav)；[项目源WAV](../../../../tools/Audio/overtake-staging/source/bigsoundbank_0556_bwf.wav) | `6AFEE85DF5E2F3C417A7E743FC14006EA3A0A1FDFB0B74BA01EE3CE8DB8B5703` |
| 游戏导入派生WAV | [overtake_passby.wav](../../../../Assets/Resources/Audio/SFX/overtake_passby.wav) | `456AF869F1B432C69530197F4AA0DF57E74C6D315108E417133914618B976165` |
| 可复现制作脚本 | [build_overtake_audio.py](../../../../tools/Audio/build_overtake_audio.py) | `9EF51C00ACD974B9C77BF5B0FEA5543A18BDA77C80E4128FA4BC9681E52A5A20` |
| 处理与测量清单 | [manifest.json](../../../../tools/Audio/overtake-staging/manifest.json) | `3E93A1C51D4DD1C060763764849935D03E1C6CA3889CA655AD71075222D65905` |

原件为48kHz/16bit/stereo，派生取5.000–5.760秒，合为mono并滤波/淡化，不变速/变调，无合成噪声或Sonniss层。处理参数、选段包络与限制见[完整制作来源记录](../../overtake-recordings-provenance.md)及manifest。

`BigSoundBank-s0556-source.html` 是含网站脚本、广告及会话字段的原始网页响应，仅保留本地供历史核查，不提交/不作为可分发素材；不据声音CC0推定整个网页获许可。本公开README只记录来源与授权依据，不复制整站网页。重复工具暂存派生WAV与对比试听WAV不纳入本次提交；保留源BWF、manifest、制作脚本及实际游戏WAV以支持重建。文件排除不删除本地原件。