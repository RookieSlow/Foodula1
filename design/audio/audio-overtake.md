# 超车实录音效

2026-10-15。状态：方向及制作规格 APPROVED（用户预授权及根任务采用）；真实资源和事件调用已接入，本轮自动验收通过：加强后相关六组原生定向40/40、最终组合全量2949/2949，失败/跳过/不确定均0。完整赛事混音及主观听验仍待试听。应用 `.agents/skills/team-audio/SKILL.md` 的 lean 音频方向→声音/可访问性→技术→接入审查流程；本文件不代表全部音频系统完成。

## 方向与现状

采用单次短汽车掠过实录，突出真实引擎及路过空气瞬态，符合 `foodula-1-audio-style.md` 的克制、短促和规则事件区分。不添加爆炸、警报、合成白噪声、旁白或配乐；不把普通道路汽车录音称为真实 F1/品牌赛车。已有六队 CC0 引擎可作后备，但其循环稳态片段不如自然路过瞬态贴合此次需求，因此采用另行核验的同一发布者 CC0 路过原件。

当前 `Core/MVPGameManager.cs` 在移动完成、位置提交及实际超车计数后，按 `playOvertake && overtakeCount > 0` 调用 `RaceEventFX.PlayOvertake`。该表现持续约 0.78 秒，已经合并 count 和提供跳过回调；音效应在取得表现槽且确认未跳过之后触发。现有服务名为 `AudioService`，不存在 AudioManager 类；其 SFX 使用主音量×音效音量、unscaledTime 限频、pitch=1、Resources 缓存。不得另建平行音频管理器。

## 事件与混音

| 事件 | 要求 |
|---|---|
| `AudioEventNames.OvertakePassby`（`overtake_passby`）实际超车表现开始 | 仅一次，合并 count 无论1或多车均不叠层、不按格重播 |
| 预测、同格无超车、count≤0、缺 car/未初始化、开始前已跳过 | 无声，不为声音新增实际超车判定 |
| 表现中跳过/禁用/销毁/场景退出 | 专用声源停止；不可停止其他 SFX 或 UI；迟到结束不能终止新所有者声音 |
| 连续超车 | 一条专用非循环声源，实际 unscaledTime 限频0.12秒用于抑制同帧/短突发；声源所有者存在时拒绝新播放，0.760秒成品结束后释放，0.78秒观察上限兜底；不叠声、不积压队列，不以0.78秒全局冷却吞掉紧接的下一场演出 |

资源0.760秒，早于0.78秒表现收尾，避免正常 finally 截尾。播放 pitch=1、spatialBlend=0、panStereo=0，mono 兼容；不虚构固定左右声场，不受 Time.timeScale 慢动作拉低。实际乘量0.85，最终为 master×SFX×0.85；主/SFX为0时有效音量必须为0，进行中设置变化即时作用，不恢复至旧值。不新增 Mixer 包或设置项，不为本音效改音乐曲目、音量或连续六队引擎参数。

采样离线峰值≤−9dBFS，RMS约−22.33dBFS，短淡入35ms、淡出100ms。听验重点是不要盖住卡牌、热量、失控和完赛提示；如混音争抢，应降低本 cue，而非提高所有音效或堆叠多个 passby。

## 可访问性

- 超车不携带音频独占规则信息：保留既有 `OVERTAKE!`、中文“超车成功”/倍数、排名及位置更新；静音仍能理解结果。
- 不增加新输入或声音强制确认，不改现有跳过、动画设置、教程/生涯规则。无对白，无新增字幕系统需求。
- 不用突然高频提示或左右声像作为唯一反馈；音效短而有限幅，不改变现有静音行为。
- 需要检查耳机与普通扬声器实际听感；PCM数值合格不等于舒适度已验收。

## 资产及制作

原件：Joseph SARDIN 的 [Cars: Passages s0556](https://bigsoundbank.com/cars-passages-s0556.html)，6辆普通汽车70–90km/h自然路过，70.5122708秒，48kHz/16bit/stereo。源页实际打开并明示CC0、允许商业修改/再分发、无需署名。下载与完整哈希见[来源记录](../../docs/audio/overtake-recordings-provenance.md)。

输出：`tools/Audio/overtake-staging/overtake_cc0.wav`，截取原件5.000–5.760秒，不变速、不升降调、不加入任何旧 Sonniss 或合成层。左右声道均值后温和一阶70Hz高通/6500Hz低通，余弦淡入淡出与有限增益归一。构建[脚本](../../tools/Audio/build_overtake_audio.py)只用标准库，核验原件SHA256，拒绝输入漂移；不下载、不执行外部内容、不写Assets。

`ui_impl` 已把暂存成品复制到 `Assets/Resources/Audio/SFX/overtake_passby.wav`，运行事件常量为 `AudioEventNames.OvertakePassby`、值 `overtake_passby`，并接入专用 `OvertakeAudioPresentation` 声源及 `RaceEventFX` 表现入口。未覆盖既有音效。暂存名 `overtake_cc0.wav` 与运行名不同，实测二者SHA256相同：`456af869f1b432c69530197f4aa0df57e74c6d315108e417133914618b976165`；对应原始录音SHA256为 `6afee85df5e2f3c417a7e743fc14006ea3a0a1fdfb0b74ba01ee3ce8db8b5703`。原件/暂存/运行资源有不同职责，不能把失败下载的HTML当作WAV导入。上述为磁盘接线/哈希核验，不替代Unity编译或测试通过；本轮[验收记录](../../production/qa/evidence/overtake-audio-2026-10-15/README.md)待补原生运行结果。

## 验收门槛

- 离线制作已实跑：有效PCM/非静音/格式/时长/无削波/首尾0；二次构建须逐字节一致。
- 运行测试须覆盖 count≤0/1/多车、跳过前后、重复调用、所有者失效、场景清理、缺资源、master/SFX为0及进行中设置、慢动作pitch不变。
- 实际超车入口播放一次并自然结束；无超车/纯预测不响，合并多车不叠声；退出不残留。
- 完整赛事、六队及耳机/扬声器主观听验未在样本制作阶段完成，不把独立AudioSource播放等同完整赛事通过。测试数量、Unity/Console状态由运行证据记录，本文不提前勾选。

本轮加强后定向原生RunFinished为UTC `2026-10-15T06:24:29.0899253Z`，40/40通过，失败/跳过/不确定0；自然结束及 master=1/SFX=0后恢复均已通过对应运行用例。最终组合全量原生RunFinished为UTC `2026-10-15T06:33:14.3194297Z`，2949/2949，失败/跳过/不确定0。证据见本轮README及 `focused-40-native.json`、`full-2949-native.json`，旧13项初版结果保留不重复累计。根任务核验MainMenu干净、idle/ready、无播放/编译/更新、许可正常；Console仅缺失赛道负向测试预期error，无warning或新增runtime error。上述自动验收不代替完整赛事混音与主观听验，本包结束不再扩张。
