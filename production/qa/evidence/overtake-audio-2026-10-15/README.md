# 超车实录音效验收

2026-10-15。状态：真实CC0音效、专用声源和表现入口已接入；本轮自动验收通过：加强后六组相关原生定向 **40/40**、最终组合全量 **2949/2949**，失败/跳过/不确定均0。完整赛事混音、六队及耳机/扬声器主观听验仍待试听。

## 范围与制作证据

既有实际超车表现入口一次播放合并计数；无玩法/判定/输入变更。保留原用户修改、历史音效和未跟踪文件，不删除场景或改设置。设计与可访问性见 [audio-overtake.md](../../../../design/audio/audio-overtake.md)，来源/许可/截取依据见[来源记录](../../../../docs/audio/overtake-recordings-provenance.md)。

- 原件：Joseph SARDIN / BigSoundBank s0556普通汽车路过实录，CC0；SHA256 `6afee85df5e2f3c417a7e743fc14006ea3a0a1fdfb0b74ba01ee3ce8db8b5703`。
- 成品：`Assets/Resources/Audio/SFX/overtake_passby.wav`与工具暂存 `overtake_cc0.wav`实测相同；SHA256 `456af869f1b432c69530197f4aa0df57e74c6d315108e417133914618b976165`。
- 0.760秒、48000Hz/16bit/mono，峰值−9.000392dBFS、RMS−22.328653dBFS，首末0、满幅样本0；二次标准库重建字节一致。
- 原始5.000–5.760秒含第一车接近上升、路过峰及初段离去；非完整远距离pass。可听对比 `tools/Audio/overtake-staging/overtake_source_then_cue.wav`，0–3秒原件4–7秒、0.5秒静音、0.76秒派生，不冒称主观听验通过。
- 实际gain0.85；unscaled突发限频0.12秒；单voice所有者互斥阻止播放中叠声，自然结束释放，0.78秒观察上限。音高1，不随慢动作变化；主音量×SFX生效、owner+token阻止迟到清理杀掉后继声音。

## 当前源码快照

[integrated-source-hashes.json](integrated-source-hashes.json) 是**接入后、测试前**当前工作树哈希，不伪称为改动前基线。记录HEAD及UTC、相关生产/测试源码与资源；原先地图UI基线由根任务另存，不混为本包结果。

加强后的测试源码另存 [strengthened-test-source-hashes.json](strengthened-test-source-hashes.json)，原接入快照保留。此时 `overtake_audio_presentation_test.cs` SHA256为 `9c7610f7a76dad1f03e6430f23caa9fe51027566376dbd6899857722da08a292`，与旧快照的初版测试不同；不将旧哈希描述为最终测试源码。

## 原生运行证据

| 验收 | 总数 | 通过 | 失败 | 跳过/不确定 | 当前状态 |
|---|---:|---:|---:|---:|---|
| 加强后六组超车/音频相关定向 | 40 | 40 | 0 | 0/0 | RunFinished Passed |
| 阶段组合全量 | 2949 | 2949 | 0 | 0/0 | RunFinished Passed |
| 完整赛事、六队及耳机/扬声器听验 | — | — | — | — | 未验收 |

需要核验实际入口count≤0/1/多车、无超车、开始前/中途跳过、重复调用、单声源不叠声、相邻演出、所有者失效、场景清理、静音/进行中设置、慢动作pitch=1及缺资源退化。独立播放器或手动推进迭代器不代替完整赛事调度与主观听感。

定向证据 [focused-40-native.json](focused-40-native.json)，UTC `2026-10-15T06:24:29.0899253Z`，原生总数40、通过40、失败0、跳过0、不确定0；包含加强的自然播放结束和 master=1/SFX=0 后恢复用例。旧初版 [focused-13-native.json](focused-13-native.json) 保留，不与最终40重复相加。

最终组合全量作业 `c830f543a89b4a6d9ced0bb0c7748195` 已结束，原生 [full-2949-native.json](full-2949-native.json) 为Passed：UTC `2026-10-15T06:33:14.3194297Z`，2949/2949，失败/跳过/不确定0；根任务记录耗时512.8688699秒。该结果是本轮音频接入及地图UI组合后的全量，不使用更早地图2945或旧音频结果替代。

根任务在全量结束后实际核验：MainMenu干净、isDirty=false，无播放/编译/更新，ready=true、idle，HasFreeLicense=true。Console仅1条 `no_such_track` 缺配置负向测试预期error（TrackDataLoaderTest），warnings=0，无新增runtime error。状态来源与快照时间边界见 [final-status-reported-by-root.json](final-status-reported-by-root.json)；本子任务没有伪称另行操作Unity核验。

结束时[final-source-hashes.json](final-source-hashes.json)再次保存相关代码/资源哈希，初版接入快照和加强后测试快照保留不覆盖。Git前后与旧文件保留核对由根任务统一完成。本包不继续扩张音效或重跑测试，主观试听未完成不计入自动验收。
