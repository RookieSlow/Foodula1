# 六队引擎与换挡界面验证

日期：2026-10-11。用户批准“是的，写入”；范围为声音、界面及相关接线，标准和中国队挡位玩法公式保持原有规则。未提交或推送。

## 实现

| 车队 | 操作器 | 原创声音性格 |
|---|---|---|
| UK | 经典 H 型档把 | 温暖老式引擎、排气脉动、机械挂挡 |
| DE | 方向盘拨片、大号实际挡位 | 平滑性能引擎、利落拨片 |
| IT | 金属开槽档杆 | 明亮高转谐波、金属入槽 |
| US | 粗杆顺序推拉 | 低沉 V8 脉冲、厚重推拉 |
| CN | Go / Recover 两模式 | 电机啸鸣、逆变谐波、回收切换 |
| JP | 短行程快排与增压表 | 增压引擎、快排切断与气流 |

移动时按真实已走绝对格数和格内进度拉转，12–14 格达到配置的高转，短移动不会归一化成最高转。专用 Engine AudioSource 使用主音量 × SFX；慢放不改变音乐 pitch。零移动、初始化定位、横向换道、回退、缺车体验证路径不播放。尾流复用实际动画入口，合法瞬时科技与进站出口使用短加速声。

仅成功变更实际挡位播放同队升/降挡提示。预选、保持、取消和支付失败不播放成功声。等待印地换道、跳过、失控、完赛、重开、退出或 owner 失活时清理；旧 fade/cue 不得结束新赛车声音。即时短声另存赛车身份，避免中国连击后第二次失控/完赛遗漏停音；换道结束且已无剩余格不再启动引擎。

六队均保留 GearButtons 教学焦点、原挡位及确认按钮。实际/待选/已锁定分开，步进从 pending 累进而合法范围按 actual 计算；确认策略、阶段、弹层及净 ±2 门控受测。CN 出牌、连用热、回收和车手减热预览读取现有规则，显示刷新不消耗技能。转速灯不改变热量/速度/卡牌。

## 自动验证

| 范围 | 最终结果 | 证据 |
|---|---|---|
| 音频、移动、新 UI 与既有挡位/跳过规则 | 76/76，失败/跳过 0 | job `8dc3c29928364ae4a7de161eabae6c44` |
| 六队 UI、真实 GraphicRaycaster 命中与 ExecuteEvents 点击 | 25/25 | job `eafa9f763dff4b189777af2d4e1e0148` |
| 实际播放器循环、拉转、慢放、静音、切队和停声 | 1/1 | job `5f9ae33292c746f0946ed0f2936d5831`；原生 RunFinished `2026-10-11T05:22:42.7545639Z` |
| 完整原生 EditMode（包含 Enter/ExitPlayMode 协程） | 2577/2577，失败/跳过/不确定 0 | job `20f7c0426a8047eb9aedfa7470bf1183`；原生 RunFinished `2026-10-11T05:26:43.0387549Z` |
| 资源格式、来源清单与哈希 | 18/18，6 循环 + 12 提示 | 48 kHz、mono、PCM16；18 个不同 SHA256，最高峰值 -7.99985 dBFS，循环首尾 PCM 相同 |

新增 45 项：TeamVehicleAudioTests 13、TeamVehicleMovementAudioTests 6、TeamGearPresentationTests 25、TeamVehicleRuntimeAudioTests 1。资源检查明细见 [JSON](team-audio-assets-2026-10-11.json)；测试结果与最终编辑器状态见 [JSON](team-vehicle-test-results-2026-10-11.json)。

首轮 UI 23 项因测试 Build 对 public config/hudUI 使用 NonPublic 反射失败；改为直接设置公开字段后，最终 25 项通过。全量请求在编译/重载期间被 MCP 报初始化超时，原生运行随后实际完成；播放用例也跨域丢失 MCP 回调。本记录仅采用原生 RunFinished 的完整结果，未将超时、旧进度或空结果当作通过。

测试后 MainMenu 干净、场景数 1、未播放/编译/导入，evidence armed=false、RenderTexture.active=none、新功能测试对象残留 0。Console 出现缺失 `no_such_track` 的既有负向测试预期日志（TrackDataLoaderTests 使用 LogAssert.Expect），没有编译/资源导入错误。原有两份 TD-002 测试源和中文字体基线 SHA256 保持一致；并行任务更新的历史文档保留，场景与 Prefab 未改。

## 视觉与试听

![六队操作器预览](team-gear-2026-10-11/six-team-overview.png)

截图是独立组件离屏渲染（每队 520×480），已核查文字、挡位状态、操作器差异和无重叠。真实指针测试另外在 1920×1080 离屏画布经过 RaceUILayoutController 布局后点击各可见挡位、± 与确认。它们不代表完整比赛屏幕的人工操作验收。

[试听 WAV](team-audio-preview-2026-10-11.wav) 使用实际资源与配置 pitch/gain 合成展示，每队依次为短移动、升挡、长移动拉转、降挡。时间索引见 [JSON](team-audio-preview-index-2026-10-11.json)：

| 车队 | 起始秒 |
|---|---:|
| UK | 0.25 |
| DE | 4.50 |
| IT | 8.75 |
| US | 13.00 |
| CN | 17.25 |
| JP | 21.50 |

素材是原创确定性程序合成的车队性格设计，未使用特定品牌实录。实际 AudioSource 播放与音高变化已验证；主观音色、混音舒适度和操作手感仍需用户试听反馈。生成脚本为 [build_team_vehicle_audio.py](../../../tools/Audio/build_team_vehicle_audio.py)，配置/资源来源与哈希位于 [team_vehicle_profiles.json](../../../Assets/Resources/Audio/team_vehicle_profiles.json)。

本次不替代已有普通赛/教程/生涯完整视觉与真实存档验收，也不关闭技术债。后续挡位玩法重做另行确定各队规则。
