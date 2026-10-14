# 选挡／选牌实时仪表验收

日期：2026-10-13（北京时间）；引擎 Unity 2022.3.62f3c1。用户要求：换挡和选中牌时立即计算并反映速度、热量。

## 完成的行为

- 选挡、US 拖杆过程中即刷新“手牌试算”：使用当前可见手牌中的最高速度组合和待选挡位容量，包含换挡／CN 连用热量；不提前抽牌。
- 选中、取消、清空、确认一组牌与启用车手技能时即刷新本回合预计格数及来源。选牌预估按“以当前已确认＋当前选中组合结束回合”计算，包含必选牌不足费用；可选槽位不罚热。
- 热量预估计入特技、反应冷却和当前路线的弯道限速／超速费用。横条上排金色预计、下排实际；温度计左侧实际、右侧金色预计，数字“预”单列。
- 预测使用独立的牌区、引擎池、特技、车手和科技状态及独立 RNG；不改真实手牌、挡位、费用、技能次数或结算账本。
- 输入关闭后撤销热量预估，避免再次预付已经支付的缺牌费用；正式移动计划产生后继续显示实际权威账本。
- 后续抽牌、尾流、超车、地标修复及对手行动以实际结算为准，详情已说明。费用无法支付时显示风险，不模拟真实失控。

## 验证

最终组合 **185/185 通过，失败 0、跳过 0、不确定 0**，包括 184 项 EditMode 案例及 1 项原生 Enter/ExitPlayMode UI 案例。不是全量测试。

组合范围：RaceTurnPreviewTests、TeamGearPresentationTests、TurnMovementBreakdownTests、RaceGearSelectionAdapterTests、RacePlayerCardPhaseAdapterTests、HeatGaugeRulesTests、RaceCoroutineEvidenceRegistryTests、LiveTurnTelemetryRuntimeTests。

- 原生 RunFinished：`2026-10-12T17:14:12.8366702Z`。
- 实际 UI 案例：`2026-10-12T17:14:11.2933162Z`，Passed。实际 RaceCanvas 与 CardPrefab 在真实播放器帧中验证改挡、选 4、再选 2、取消 4、普通资源刷新和关闭输入后的温度计恢复。管理器／牌局状态为隔离夹具，没有启动完整默认赛事或修改存档。
- MCP 作业 `c940b3fbb6e2435d8d7146c52d41b53d` 的进度因域重载遗留；原生完成、armed=false、MainMenu 干净、无播放／编译／导入后才清理。
- 编译无错误。最终 Console 错误 0；1 条 cardPrefab 缺失警告来自原有非激活选牌测试夹具，原生实际 UI 案例使用预制体真实 CardPrefab 引用。

六队、1920×1080 与 2560×1440、选挡和选牌两种状态，共 **24 个受控状态、56 PNG、252 条仪表文字记录，溢出 0**。只核查换挡区仪表和温度计文字，不把受控 HUD 图当作完整赛事／赛道视觉验收。

![六队选挡试算](live-turn-telemetry-2026-10-13/trial/six-teams-1920x1080.png)

![六队当前选牌](live-turn-telemetry-2026-10-13/cards/six-teams-1920x1080.png)

[UK 实际 HUD 选牌示例](live-turn-telemetry-2026-10-13/cards/UK-1920x1080-hud.png) · [预估详情](live-turn-telemetry-2026-10-13/cards/UK-1920x1080-details.png) · [选挡文字几何](live-turn-telemetry-2026-10-13/trial/text-geometry.csv) · [选牌文字几何](live-turn-telemetry-2026-10-13/cards/text-geometry.csv) · [机器证据](live-turn-telemetry-2026-10-13.json)

## 修复过程与边界

初轮 133 项有 2 项夹具断言失败：夹具把热量放入弃牌堆，却期待手牌数和手牌茶冷却；改为显式支付入手牌后通过。原生 UI 夹具前三次在取卡牌后出现同一空引用：实际 prefab 的 HandContainer 并非 CardHand 子对象；原生堆栈定位测试第 89 行，改为从真实 handContainer 查找后通过。绘图脚本同步使用真实 manager.cardHandUI 引用。温度计两行数字最初出现 24 条溢出，扩大顶部标签空间后最终 0。上述失败没有被当作通过。

本次没有重跑全量，没有验证所有车队的完整赛事／所有科技组合。预估显示已确定部分，特殊后续结果按实际更新。

## 工作区保持

212 个原有脏／未跟踪路径基线中无非本任务变动；最终测试期间 356 个 C# 文件哈希保持一致。原 SourceHanSans 字体仍为 34,156,615 字节、哈希与本任务开始相同，原有脏状态保留。

MainMenu 为唯一普通场景、isDirty=false，无播放／编译／导入；没有保存场景或 prefab、没有暂存、提交或推送。HEAD 保持 `7fe271b939d2bef8a61553b0d1bf9313198cc9b2`。
