# 超车掠过真实录音：CC0 来源与制作

核验/下载日期：2026-10-15（Asia/Shanghai）。本次仅新增有界超车音效，不覆盖既有六队声组或历史Sonniss档案。

## 发布者与许可

- 作者 Joseph SARDIN；发布者 [BigSoundBank — Cars: Passages s0556](https://bigsoundbank.com/cars-passages-s0556.html)。实际 search/open 原页：普通道路六辆不同汽车以70–90km/h路过，室外实录、stereo、48kHz/16bit；不宣称为特定品牌/F1赛车。
- 原页明确 CC0，源页许可说明允许修改、混音、商业游戏/应用及再分发，无强制署名。故可随游戏及公开源码项目分发此录音和本次派生；不代表商标授权或作者背书。
- 已实际打开 [CC0官方法律原文](https://creativecommons.org/publicdomain/zero/1.0/legalcode.en)。沿用已保存且本轮重验哈希的 [CC0-1.0-original.html](licenses/CC0-1.0-original.html)，SHA256 `B43732F4C1463B087CF10D4DAF8D8766514B2BF8F21BC5EF50905F95BE227726`；不修改既有许可原件。
- 公开许可与来源证据见 [超车录音许可README](licenses/overtake/README.md)，含2026-10-16重新打开源页核验、CC0原文与源/导入WAV实际哈希。原始响应BigSoundBank-s0556-source.html（SHA256 `91987D90CDD728690E2FC2FCF776059FC884F6222AB11006B90E91EF6B3F80D4`）仅保留本地且不提交；它包含网站脚本/广告/会话字段，不宣称整页可分发，不执行其内容。

## 原件

原始BWF：[发布者下载地址](https://bigsoundbank.com/UPLOAD/bwf-en/0556.wav)。本地 `tools/Audio/overtake-staging/source/bigsoundbank_0556_bwf.wav`，13,539,544字节，3,384,589帧，70.512270833秒；实际验证RIFF/WAVE、PCM16bit/stereo/48000Hz。

SHA256：`6afee85df5e2f3c417a7e743fc14006ea3a0a1fdfb0b74ba01ee3ce8db8b5703`。

获取过程：普通download.php表单请求超时；`/UPLOAD/wav/0556.wav`返回44,164字节HTML，未当作有效音频。该失败响应仅为本轮新建的临时文件，核验后已移除，未删除任何用户原有文件；禁止将此HTML导入或提交为音频。最后从官方BWF路径成功取得原件，未采用MP3/OGG压缩试听、不登录/付费/绕过验证码。既有 muted.io CC0 Performance Cars 实录本轮重新search/open核验，作为后备没有混入成品；搜索中声明AI生成的SFXMint候选未采用。

## 成品与可复现性

`tools/Audio/build_overtake_audio.py`：Python标准库，输入SHA256强校验，仅截取5.000–5.760秒；L+R均值、温和一阶70Hz高通/6500Hz低通、35ms/100ms余弦淡化，目标RMS−22dBFS且峰值≤−9dBFS。不变速、不升降调，不使用生成噪声或机械/Sonniss层。只写工具暂存，不直接接入Assets。

| 输出 | 数值 |
|---|---|
| `tools/Audio/overtake-staging/overtake_cc0.wav` | 48kHz/16bit/mono PCM，36,480帧，0.760秒 |
| SHA256 | `456af869f1b432c69530197f4aa0df57e74c6d315108e417133914618b976165` |
| 峰值/RMS | −9.000392 / −22.328653 dBFS |
| 首末样本/满幅样本 | 0 / 0；满幅0 |

构建机器可用的输入/输出及处理证据位于 `tools/Audio/overtake-staging/manifest.json`。本轮二次重建成品SHA256完全一致。`ui_impl` 已在安全状态复制到 `Assets/Resources/Audio/SFX/overtake_passby.wav`，与暂存 `overtake_cc0.wav` 只改文件名、不改字节，实测运行资源SHA256同为 `456af869f1b432c69530197f4aa0df57e74c6d315108e417133914618b976165`。编译、运行测试及主观听验结果待另记，不将磁盘接入当作完整赛事通过。

## 选段依据与可听对照

原件4–7秒的100ms RMS包络已实测并写入manifest。首辆车5.0秒为−16.26dBFS，5.2秒−13.85，5.4–5.5秒达到−11.92，5.7秒回落至−15.66，6.0秒−23.15，6.9秒−32.62。选段含接近上升、路过峰和初段离去；0.760秒窗口无法完整保留远处接近/离去，末端100ms渐隐是有意的表现剪辑，不冒充完整自然pass时长。

可听对比：`tools/Audio/overtake-staging/overtake_source_then_cue.wav`（4.26秒），0–3秒为原件4–7秒保留立体声，仅统一衰减至峰值−9dBFS；3–3.5秒静音；3.5–4.26秒为派生单声道同送L/R。对比文件SHA256 `43970e1b0b9ec94ef092cbd1ec17f9d505f417c11464fb37e6bd3edebca19c26`，不含音乐或运行音量。未声称已主观试听通过。

行为/混音/可访问性要求见[音频规格](../../design/audio/audio-overtake.md)。最终复制资源、meta、运行测试和听验由实施工作包记录；保留全部旧资源与未跟踪文件。
