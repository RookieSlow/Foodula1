# Current Task List

> Updated: 2026-08-26
> Sources: Claude Code project memory, active session state, session history,
> current Git worktree, and current Unity project structure.

## 本轮手动试玩视觉回归（2026-08-26）

- [x] 分析最新试玩日志：`C:\Users\Admin\AppData\LocalLow\DefaultCompany\Foodula1\race-logs\race-20260826-052850-silverstone_afternoon_tea-0f67c27acc98460ca100e7a2e0605e01.log`。
  日志覆盖 7 个回合，规则层已记录多段 `[SLIPSTREAM]`，没有记录视觉层异常；它不能单独证明
  尾流特写是否可见。
- [x] 修正主动弃牌表现：数据层返回实际从手牌移入弃牌堆的卡牌实例，动画逐张捕获对应手牌位置，
  然后只隐藏/移除已弃置的手牌 UI，不再整手销毁重建；未选中的牌保持在原手牌展示中，不会再被
  表现为飞入弃牌堆。新增 `[DISCARD] selected=N discarded=M` 日志，便于下一次手动试玩核对规则与视觉。
- [x] 将尾流明确拆为回合末独立阶段：所有车辆先完成基础移动、反应与弯道判定，日志记录
  `[MOVE_PHASE] end` 后才按实际落位结算尾流；已有卡牌飞行结束后显示“尾流阶段”并聚焦车辆，
  使用 0.95 秒、0.28 倍时间尺度的独立特写，`[SLIPSTREAM_PHASE] end` 后才执行额外移动。
- [x] 新增回合末实际落位的尾流回归覆盖，并保留弃牌实例与温度计绑定回归；本轮定向 Unity
  EditMode `56/56`、全量 `446/446` 通过，0 失败、0 跳过。尚未用新日志完成包含弃牌和尾流
  画面的人工验收。
- [x] 分析最新试玩日志 `C:\Users\Admin\AppData\LocalLow\DefaultCompany\Foodula1\race-logs\race-20260826-081928-silverstone_afternoon_tea-df5ea074251143398bf63204c684030c.log`：两车基础移动后同落第 9 格，旧规则将同格前向距离 0 双向判为前车。已按设计案接入“基础移动力优先、同值按到达顺序”的同格判定，并让运行时、纯模拟和车队基准在统一解析后再应用奖励移动。
- [ ] 下一步重跑一小段包含主动弃牌与尾流的 Play Mode：确认只有选中牌飞行，且尾流特写完整结束后
  才开始移动；同时核对新日志中的 `[DISCARD]`、`[CARD_PHASE]`、`[SLIPSTREAM_PHASE]`、`[MOVE_PHASE]` 顺序。

## 本轮验证（2026-08-26 HUD 表现回归）

- [x] 按更新后的自动化流程调用 `sprint-status` skill；项目当前没有
  `production/sprints/`、`sprint-status.yaml` 或可执行 story，已按回退规则继续从本清单推进，
  未创建流程文件或 `production/session-state/active.md`。
- [x] HUD/牌堆/挡位反馈定向 EditMode `21/21` 通过，全量 EditMode `444/444` 通过，0 失败、
  0 跳过；测试期间唯一的缺失赛道 Console 错误由 `LogAssert` 明确预期，已清理。
- [x] Unity Play Mode 启动冒烟运行 5 秒：进入 `MainMenu` 后无项目错误/警告；退出后编辑器回到
  非播放、非编译、`ready_for_tools=true`，`Race` 场景 `isDirty=false`。
- [x] 按 `consistency-check` skill 定向核对核心 GDD：实体注册表当前为空，未产生实体冲突；已修正
  MVP 尾流旧状态、尾流/移动阶段顺序、雨天失控计数描述和过弯热量去向，并同步当时的 `436/436`
  回归证据。

## 本轮完成（2026-08-26 AI 尾流策略接入）

- [x] 修正 AI 行为树中明确标注“P3 尾流 — MVP 跳过”但运行时没有主动规划的问题：低热量、
  无弯道风险且前车当前前向距离在配置窗口内时，AI 会估算前车计划移动并尝试精确组合当前
  档位的速度牌，使计划终点保持 1 格尾流触发距离；没有精确组合时安全回退到原策略。
- [x] 修正 AI 已经落后前车 1 格时被错误排除在主动规划窗口外的问题；现在会按前车预估
  移动量寻找维持 1 格尾流距离的精确牌组，仍将同格车辆交给回合末到达顺序判定。
- [x] 新增 `GameConfigSO.aiSlipstreamPlanningRange`（默认 2）和 `AIPlanner` 纯函数：覆盖
  环形赛道前向距离、前车移动估算后的目标移动力、精确卡牌组合与不可达回退边界。
- [x] 历史 Unity EditMode AI 定向测试 `12/12`、全量 `441/441` 通过，0 失败、0 跳过；
  本轮新增 2 个“一格尾流规划”边界用例。完整多车
  Play Mode 调参仍未完成。
- [x] AI 主动尾流候选与运行时规则对齐：不同圈车辆不再作为规划目标；新增 1 个跨圈
  集成回归用例。
- [x] Unity 编辑器恢复后完成 AI 尾流定向回归 `15/15`、全量 EditMode `451/451`，
  0 失败、0 跳过；覆盖同圈主动规划、一格边界、不可达回退和跨圈候选过滤。
- [x] 读取最新人工日志 `C:\Users\Admin\AppData\LocalLow\DefaultCompany\Foodula1\race-logs\race-20260826-085042-silverstone_afternoon_tea-f5f194d14b67484292b35b8db9c240fc.log`：
  `CARD_PHASE end → MOVE_PHASE end → SLIPSTREAM → SLIPSTREAM_PHASE → SLIPSTREAM_MOVE_PHASE`
  顺序正确，且本局只有玩家获得 1 次 `+2` 尾流移动；本局在 `RACE_END` 前没有进入弃牌阶段，
  因而不能替代弃牌视觉验收。
- [ ] 下一步人工构造相邻 AI/玩家位置，确认 AI 实际选牌后日志出现 `[SLIPSTREAM]`，并观察
  尾流特写与后续回合；这与现有规则层/受控事件链测试互补。

## 本次完成（2026-08-25 链式尾流与车队平衡闭环）

- [x] 按核心 GDD 补齐每回合最多两段的链式尾流：第一段奖励会推进模拟位置，再寻找
  更前方且尚未跟随过的车辆；不会重复吸同一辆车，也不会绕过开启冰糕的最近前车。
- [x] 尾流判定改为所有车辆完成基础移动、反应和弯道判定后的回合末实际落位；科技、特技
  和车队加成先完成基础移动，尾流不再在回合开始或基础移动前触发，避免受到阶段顺序影响。
- [x] 尾流日志现在逐段记录 `chain`、前车、单段奖励和总奖励；表现层会把链中每一段
  都纳入同一个尾流特写阶段。新增 5 项链式、上限、多云、完整移动和冰糕阻断回归。
- [x] Unity EditMode 定向测试 47/47、全量测试 411/411 通过；编译后二次完整刷新
  Console 为 0 错误/警告，`Race` 场景保持未修改。
- [x] 将 `race_simulation_test` 和 `TrackTeamBalanceBenchmark` 与运行时统一为：全员选牌 → 依排名
  执行基础移动/反应/弯道 → 按回合末实际落位解析链式尾流 → 应用额外移动。纯模拟同时补上
  进站成功后的出口位置应用；确定性完整比赛测试 1/1 通过。
- [x] 实际重跑 9 个赛道配置 × 每图 12 场六车平衡基准；报告新增每队平均尾流触发
  次数和尾流移动收益，证明离线基准确实走到链式尾流路径。完整 EditMode 仍为 411/411。
- [x] 完成车队平衡审计并修正三项基准/运行时错误：美国直道旧面板 `+3` 不再与固定
  `+1` 重复叠加；中国 Recover→Go 风险按首轮 3 张牌估算；基准支付热量会生成真实
  手牌热量供冷却。重跑后 648 个车队参赛样本全部完赛，原中国勒芒 92% DNF 被确认
  为基准假象。基准同时改为记录个人完赛回合并轮换车队插入顺序。
- [x] 修正意大利“激情过弯”时序：成功通过弯心后挂起增益，在后续首个实际打出
  速度牌的回合一次性 `+1`；空回合保留，失控不挂起，不再错误加在过弯当回合。
- [x] 用受控基准确认意大利面板加速被误算为常驻直道 `+1`：旧规则 9 图平均排名
  `1.37`、胜率 `74%`；仅移除该未写入 GDD 的加成后回到排名 `2.02`、胜率 `35%`。
  最终运行时只保留操控 `+2` 与下一回合一次性出弯 `+1`；对齐 AI 阈值后的矩阵约为
  排名 `2.31`、胜率 `26%`。
- [x] 平衡基准新增中国 Go/Recover、弯前强制 Recover、原始牌面移动与非尾流移动
  计数。AI 弯前模式判断改用“最低合法 Go 手牌”而非最高牌；中国平均排名由约
  `5.55` 改善到 `4.90`，仍保持 0% DNF。完全移除保护的对照在四条密集弯道出现
  `83–100%` DNF，因此保留“最低牌仍必然超速才 Recover”的安全边界。
- [x] 在上述安全边界上继续加入可负担风险：最低合法 Go 组合会累计本回合跨过的唯一
  弯心成本，只有预计弯道热量 ≤ `1`，且引擎还能支付超频和缺牌成本时才保持 Go。
  同步修正基准的 6 格预判和 AI 热量阈值以匹配运行时 `0.7/0.5/0.3`；最终中国九图
  平均排名 `4.54`、胜率约 `7%`、0% DNF，平均 Recover 从严格零热策略的 `25.8`
  降到 `22.5`。
- [x] 本轮新增意大利时序与中国热量预算回归，Unity EditMode 全量 `417/417` 通过；九赛道基准已按
  最终规则重跑，设计说明、平衡报告和项目记忆已同步。
- [x] 参数接线后的最终 Unity EditMode 全量回归已真实通过 `435/435`；本轮结束时
  Console 清理后为 0 错误/警告。当前环境没有可用的 .NET SDK，因此
  `dotnet build --no-restore` 仍不能作为替代验证。
- [ ] 平衡后续：中国在银石/铃鹿仍偏弱；先做人工手感验证，再考虑最后一圈风险容忍或
  路线策略。英国/日本的主动特技未被纯车体基准充分使用，不应直接按当前排名加数值。
- [x] 受控 Play Mode 尾流冒烟已实际走通：临时将运行时配置设为玩家 + 3 AI，注入相邻
  计划终点后，日志出现 `AI3 follows=AI2 bonus=2` 与 `AI2 follows=AI3 bonus=2`；
  `RaceEventFX` 在场且尾流阶段可进入，证明运行时事件链和表现组件均被调用。日志路径为
  `C:/Users/Admin/AppData/LocalLow/DefaultCompany/Foodula1/race-logs/race-20260825-124729-silverstone_afternoon_tea-5e5ee1a868b04cf9ba61ee3303abde34.log`。
  本次是受控单段命中冒烟，不等同于两段链式人工验收；完整人工对局仍需确认气流连线、
  特写遮罩、后续回合推进，以及修正后的美国直道 `+1` 与中国 Go/Recover 节奏。

- [x] 分析 2026-08-25 11:34 的中国队印第安纳波利斯日志：第 1 回合 Go 的有效上限为
  4 张，是基础 Go 3 张加火锅底料的 1 个 ATTACK 额外槽；第 2 回合连续 Go 的基础上限
  才是 4 张。第 3 回合失控进入 Recover 后计数清零，第 4 回合重新 Go 的基础上限已重置
  为 3 张，但当回合再次打出火锅底料后有效上限仍为 4 张。定向 EditMode 133/133、全量
  EditMode 436/436 通过；当前日志未出现尾流事件。

- [x] 2026-08-25 受控上海/中国队首回合走查：日志记录了 AI 档位与出牌、玩家挡位缺牌后
  `Engine failure! Missing 1 speed card(s). +1 Heat to hand.`，以及中国队 Recover 冷却
  1 张热量回引擎；日志路径为
  `C:/Users/Admin/AppData/LocalLow/DefaultCompany/Foodula1/race-logs/race-20260825-110823-shanghai_dim_sum-dbfbc7c7595a4e3db687d3597f296342.log`。
  本轮定向 EditMode 195/195、全量 EditMode 435/435 通过，Race 场景退出后保持未修改，Console
  清理后为 0 条；该日志只覆盖首回合，未出现 `[SLIPSTREAM]`，不能替代尾流人工验收。
- [x] 复核受控输入中 `Animating` 超过 10 秒的现象：代码确认移动后会进入 `DiscardStep()`，
  由 `inputState.WaitingForDiscard` 等待玩家确认弃牌，而阶段枚举在收尾前仍保持
  `Animating`；本次退出发生在确认弃牌之前，因此不是已证实的移动动画死锁。
- [ ] 继续完整人工走查：下一轮先确认弃牌完成收尾，再用不插入查询的方式单独构造相邻车辆
  尾流，确认气流连线、`[SLIPSTREAM]` 日志、尾流奖励和后续回合是否正常推进。

## 待推进（2026-08-23 比赛视觉信息强化）

- [x] 赛道目录健康检查：Unity `Foodula1/Tools/Validate Track JSONs` 已校验全部
  9 条赛道，节点连续性、起终点、弯心/限速、维修区成对、坐标、圈数、天气与闭环
  布局全部通过；维修区、天气、尾流、纯层完整比赛和全部赛道显示回归定向测试
  `67/67` 通过。
- [x] Play Mode 已逐条抽查全部 8 条官方赛道：银石、纽博格林大奖、蒙扎、印第安纳波利斯、
  上海、铃鹿、纽博格林北环和勒芒旧慕尚。分别确认 JSON 加载、2/4 车道布局、格数/弯道
  标识、玩家光环、四区 HUD 与缩略图均可见，最终 Console 无错误/警告。
- [ ] 完成包含牌堆变化、挡位缺牌、尾流与维修区的完整人工走查；本轮仅验证了各赛道的
  初始 Play Mode 画面与加载状态。

- [x] 根据 2026-08-25 上海完整对局日志修复终局锁定：玩家第 38 回合完赛后不再继续
  触发阴阳茶、支付热量或移动，避免已完赛状态在等待 AI 时反向变成爆缸；新增终态规则和
  RaceSession 回归。原始日志已整理为 `production/qa/playtests/playtest-2026-08-25-manual-full-race.md`。
- [x] 增加卡牌区域流转初版：速度/特技/实际被选中的主动弃牌飞向弃牌堆，热量支付从引擎飞向规则
  目标，冷却按真实来源飞回引擎；牌堆厚度改为每 3 张增加 1 层、最多 7 层，精确徽标保留。
  针对性 EditMode 69/69、全量 EditMode 430/430 通过；Play Mode 运行时注入热量支付动画
  可见，Race 场景退出播放后保持 `isDirty=false`，Console 0 错误/警告。
- [ ] Play Mode 人工走查上述动画的遮罩、弧线方向、批量节奏和牌堆层距，并重跑一次
  “玩家先完赛、AI 后完赛”的中国队对局，确认赛后无阴阳茶/爆缸、结果与 XP 一致。

- [x] 根据 2026-08-24 上海半局日志修正多云尾流语义：多云不再把基础 1 格触发距离
  压成 0，而是按 GDD 的“尾流效率 -1”将最终尾流移动奖励减少 1；大雨仍完全禁用。
  已补天气纯规则与 RaceSession 回归，Unity EditMode 全量 406/406 通过；Play Mode
  仍需实际触发确认表现。

- [x] 将比赛最终四区 HUD 烘焙进 `RaceCanvas.prefab`，运行时优先保留 Prefab 中人工
  调整的 RectTransform；旧 Canvas 缺少主面板时才自动补齐。新增 Race UI Authoring
  编辑器入口和已编排/回退两条布局回归测试；Unity EditMode 全量回归 403/403 通过，
  Play Mode 确认采用 authored layout、画面与基线一致且 Console 0 错误/警告。
- [x] 比赛中“返回主菜单”按钮移入左侧操作栏动作栈，放在重新开始按钮下方、
  提示日志面板上方，避免覆盖操作栏标题和赛道画面；Play Mode 截图和 Console
  走查通过，并增加运行时锚点回归测试。
- [x] P0：增强挡位出牌要求反馈，显示有效要求值、已出/要求数量和缺牌引擎故障预警；
  保持现有缺牌惩罚规则不变。EditMode 已覆盖正常、待确认和引擎热量不足提示，
  Play Mode 人工走查仍待完成。
- [x] P1：选中卡牌缩放并上移，保持布局槽位不变；选中态使用 1.08 倍缩放、上移
  24px、0.14 秒未缩放时间缓动和蓝色阴影，已覆盖多选、特技单选和布局槽位不变。
  Unity EditMode 当前全量回归 403/403 通过；Play Mode 遮罩边界仍待人工走查。
- [x] P1：为抽牌堆和弃牌堆增加牌背叠放、实际卡牌缩略图、数量徽标和牌堆变化刷新；
  抽牌堆按实际抽取顺序预览，弃牌堆按最近弃入顺序预览，热量牌按真实区域显示。
  Unity EditMode 当前全量回归 403/403 通过；Play Mode 尺寸与可读性仍待人工走查。
- [x] P1：基于赛道 JSON 增加科技蓝玩家光环、1-based `格 X/N` 和玩家前后各 6 格
  的局部刻度；密集节点会自动抽样文字。弯道覆盖层同步重制为 Lv1 绿 / Lv2 黄 /
  Lv3 红的圆角平滑双层曲线带，弯心和限速徽标保持清晰。Unity EditMode 全量回归
  403/403 通过，并覆盖全部 8 条官方 JSON 与 fallback 的弯道曲线采样；8 条官方赛道
  均已完成 Play Mode 截图与 Console 走查。
- [x] P2：尾流结算现在返回命中的前车与最终加成，并在基础移动、反应和弯道判定结束后把
  同回合全部事件合并为 0.95 秒独立表现：两车同步聚焦、科技蓝流动虚线和“尾流 +N”提示；
  特写使用 0.28 倍时间尺度且计时使用 unscaled time，结束后才应用额外移动。
  Unity EditMode 当前全量回归 442/442 通过，
  实际画面仍待包含尾流的一局 Play Mode 人工走查。
- [ ] 验收：相关 EditMode 测试通过，并完成至少一局包含牌堆变化、挡位缺牌和尾流的
  Play Mode 人工走查。

## 本次修复（2026-08-23 手动日志回归）

- [x] 修正 RaceTestLogWriter 的赛道元数据来源：优先记录 TrackManager 实际加载的
  赛道 ID，避免配置默认值与菜单选择不一致。
- [x] 修正比赛终止条件：玩家爆缸只将玩家标记为 DNF，剩余非爆缸赛车继续比赛，
  直到所有活动赛车完成或退赛。
- [x] 修正比赛结果文本中的国旗和状态 Emoji 字形警告，改用稳定的车队代码和中文
  状态标签，避免 TMP 显示方框。
- [x] 增加 RaceRanking 回归测试，并让纯层比赛模拟覆盖玩家 DNF 后其余赛车继续比赛；
  Unity EditMode 全量回归 403/403 通过。
- [ ] 仍需用新的手动 Play Mode 日志确认：玩家 DNF 后 AI 会继续完成并正确记录最终结果。

## 本次完成（2026-08-23 热量牌生命周期、阴阳茶与维修区）

- [x] 修正热量牌生命周期：`initialHeatCards` 不再加入普通牌组；开局与普通补牌只抽
  速度/特技牌。永久热量只能从独立引擎池经明确支付/效果进入手牌或弃牌堆，再由冷却
  或明确回收效果返回引擎。
- [x] 将自动冷却统一到 `CardDeck.CoolHeat()`，严格按手牌→抽牌堆→弃牌堆
  顺序处理热量；永久热量回引擎，限时热量销毁。
- [x] 将中国队阴阳茶改为按 Go/Recover 模式结算：Go 支付引擎热量到弃牌堆并
  前进 1 格，Recover 从手牌冷却 1 张。
- [x] 维修区保持停 1 回合，同时在 `pit_exit` 后按配置前移；新增中国队“快充技术”
  科技修正。
- [ ] 仍需在 Play Mode 完成多天气、多圈和完整进站流程的人工走查；本次未修改场景。

## 本次推进（2026-08-23 热量支付与维修区时序）

- [x] 标准热量支付改为默认直接进入手牌，热量会占用手牌；显式弃牌堆路径保留给
  阴阳茶 Go 等明确效果；普通抽牌仍不会抽取热量。
- [x] 维修区选择窗口改为 `pit_entry` 前 1–10 格；选择进站只登记预定状态，
  越过入口的本回合继续移动，下一回合开始才执行停站、全热量冷却和出口前移。
- [x] 增加维修区入口前窗口、跨圈入口检测和“预定后延后一回合执行”的 EditMode 覆盖。
- [x] 本轮 Unity EditMode 全量回归 403/403 通过；Play Mode 进站人工走查仍待验证。

## 本次完成（2026-08-19 赛道天气规则边界）

- [x] 将赛道 JSON 的 `sunny/cloudy/light_rain/heavy_rain/hot` 映射为独立
  `WeatherType`，并保留旧 `WeatherType.Rainy` 作为小雨兼容别名。
- [x] 将弯道限速、尾流范围/禁用、热天冷却惩罚、湿地失控计数器增量和 HUD
  文案统一收敛到 `WeatherModifiers` / `WeatherRules`，管理器仅负责调用。
- [x] 增加 `RaceLapWeatherRules`，让运行时比赛与纯模拟共用起终点过线、每圈天气门控
  和完赛判定顺序；新增跨圈转场回归测试。Unity EditMode：377/377 通过，
  `dotnet build Foodula1.sln --no-restore`：0 错误。
- [x] Play Mode 启动冒烟通过：5 秒运行期间 Console 0 条错误/警告/日志；未修改场景。
- [ ] 仍需在 Play Mode 完成多天气、多圈和完整进站流程的人工走查；本轮未修改场景。

## 本次完成（2026-08-18 出牌与赛事表现）

- [x] 速度牌支持多选后一次确认，也支持选中一张后单张确认；选择数量由本回合
  出牌上限实时限制，提交在 `CardPlayRules` 中原子完成。
- [x] 特技牌仍严格保持单张选择、即时结算，并禁止与速度牌混选。
- [x] 赛车图标显示缩放改为 `GameConfigSO.carSpriteScale`，默认由 0.2 调整为 0.28。
- [x] 新增运行时 `RaceEventFX`：超车慢放特写、失控旋转提示与爆缸退赛提示，均不改变
  规则层状态。
- [x] Unity 编辑器回归测试已完成：EditMode 365/365 通过，Play Mode 启动冒烟无项目
  错误或警告（仅 Unity MCP 自身 WebSocket 重连警告）。

## 本次完成（2026-08-18 模块化推进）

- [x] 将 `MVPGameManager.AutoCreateUI()` 的程序化 HUD、档位按钮、动作按钮、手牌容器
  和卡牌预制体回退逻辑抽取到 `Assets/Scripts/UI/RaceUIFactory.cs`。
- [x] 保留 Prefab 优先、旧场景回退、按钮回调修复和 TMP 字体复用行为；Manager 只负责
  传入回调与接收 UI 引用，不再持有主要 UI 构建细节。
- [x] Unity EditMode：321/321 通过；`dotnet build Foodula1.sln --no-restore`：0 错误。
- [x] Unity Play Mode 启动冒烟无项目错误/警告；MCP 仅记录自身 WebSocket 重连警告。
- [x] 将出生朝向、传送朝向和逐帧旋转的 Unity 适配逻辑抽取到
  `Assets/Scripts/Gameplay/CarOrientationController.cs`；角度规则仍由
  `CarOrientationRules` 纯函数负责。
- [x] 新增朝向偏移与旋转速度回归测试；Unity EditMode：323/323 通过，Dotnet 编译 0 错误。
- [x] 将车队赛车精灵槽位与缺失精灵时的备用颜色映射抽取到
  `Assets/Scripts/Gameplay/TeamCarPresentationRules.cs`；新增 4 项边界回归测试，避免
  `MVPGameManager` 直接维护车队外观身份映射。
- [x] 将节点间车辆插值与到达阈值抽取到
  `Assets/Scripts/Gameplay/CarMovementAnimator.cs` / `CarMovementRules.cs`；通过注入
  deltaTime 的回归测试保持原有移动速度、终点吸附与朝向更新行为。
- [x] 将环形赛道超车判定抽取到 `Assets/Scripts/Core/RaceMovementRules.cs`；通过注入
  跳过回合谓词覆盖普通超车、失控/维修区跳过和无效赛道长度边界。
- [x] 将普通赛道并排时的后车外线判定抽取到
  `Assets/Scripts/Core/RaceLaneRules.cs`；覆盖同节点、不同圈/节点、完赛退赛和无效索引边界。
- [x] 将档位、卡牌和弃牌阶段的玩家输入等待状态抽取到
  `Assets/Scripts/Core/RaceInputState.cs`；回合协程与 UI 回调共享同一门控状态，并覆盖
  确认一次、阶段互斥和重置边界。
- [x] 将起终点过线后的圈数递增与完赛边界抽取到
  `Assets/Scripts/Core/RaceLapRules.cs`；天气掷骰、科技重置和 UI 日志仍由管理器编排。
- [x] 将印地换道与维修区选择的等待门控并入
  `Assets/Scripts/Core/RaceInputState.cs`；场景面板和选择后的车辆/维修效果仍由管理器编排。
- [x] 将回合跳过、爆缸和完赛后的参与资格抽取到
  `Assets/Scripts/Core/RaceTurnRules.cs`；A1 的跳过消费和各阶段副作用仍由管理器编排。
- [x] 将比赛阶段枚举、阶段切换和输入可接受性抽取到
  `Assets/Scripts/Core/RacePhaseState.cs`；协程副作用和 UI 仍由管理器编排。
- [x] 将每圈天气只掷一次的门控抽取到
  `Assets/Scripts/Core/RaceWeatherState.cs`；天气池选择和 UI 日志仍由会话/管理器编排。
- [x] 增加 `RaceTestLogWriter` 手动测试日志：自动保存 HUD 事件、回合状态、档位、玩家/AI
  出牌、特技牌和移动计划；启动/结束时在 Console 输出日志绝对路径，文件写入或关闭失败
  不影响比赛。日志适配器通过 `GetDefaultDirectory()` 暴露默认目录，并覆盖重开比赛时的
  文件轮换。

## 本次修复（2026-08-18 档位确认卡死）

- [x] 修复 `RaceEventFX` 复用失效 `CanvasGroup` 导致 `MVPGameManager.Start()` 中断的问题。
- [x] 赛事特效初始化每次创建带必需组件的新根节点，并设置为可选表现；即使特效初始化
  失败也会继续启动比赛回合协程。
- [x] Unity 编辑器退出 Play Mode 并重载脚本后复测档位确认按钮和首回合推进；本轮
  EditMode 365/365 通过，启动冒烟未再出现 `CanvasGroup` 异常。

## P0 - Resume Approved Scheme A Refactor

- [x] Review all current C# files and reconcile the earlier whole-project
  review with the latest project state.
- [x] Integrate `Assets/Scripts/Core/RaceRules.cs` into
  `MVPGameManager.cs` so shared gear, cooling, movement, and selection rules
  have one source of truth.
- [x] Integrate `Assets/Scripts/AI/AIPlanner.cs` into `AIController.cs`.
- [x] Inject `IRandomSource` where deterministic gameplay or AI behavior is
  required.
- [x] Add EditMode tests for `RaceRules` (9 cases passing in Unity).
- [x] Add EditMode tests for `AIPlanner`, deterministic random behavior,
  and the AI spin-out card-conservation regression (7 cases passing in Unity).
- [x] Historical baseline: validated changed scripts and Unity console with no errors
  (16 EditMode tests at that stage; current full suite is 403/403).
- [x] Review the final diff for the approved refactor changes.
- [ ] Commit only with explicit user instruction; scheduled-task authorization
  does not include Git commits.

The four Scheme A source files and their `.meta` files were committed in
`58d1bba`. `RaceRules` and `AIPlanner` are now integrated into the runtime.
`AIController` and `CardDeck` accept injectable random sources, and the
refactor currently has 16 passing EditMode tests. The final review also fixed an
AI spin-out path that could remove selected speed cards without discarding them,
and standardized `IRandomSource.NextDouble` to the [0, 1) contract.
Manager-level seeded replay and broader integration coverage remain useful
follow-ups, but are not blocking the current Demo path.

The first data-driven track completion slice is also verified. `TrackNode` now
preserves JSON apex metadata, pure `TrackRules` owns wrapping traversal and
start/finish lookup, and `MVPGameManager` initializes and counts laps from the
runtime track rather than the legacy config index. Silverstone loads in the
Race scene with 60 nodes and 3 laps. Unity currently passes 20 EditMode tests
with 0 failures, warnings, or errors.

## P0 - Protect and Reconcile the Current Worktree

- [ ] Inspect the existing modifications to `MainMenu.unity` and
  `Race.unity`; preserve legitimate user scene edits.
- [ ] Verify the untracked `Assets/Data/Tracks/NewTrackData.asset` before
  deciding whether it belongs to the track-system work.
- [ ] Keep the Unity MCP package changes in `Packages/manifest.json` and
  `Packages/packages-lock.json` logically separate from gameplay refactoring.
- [ ] Avoid bundling unrelated scene, track-data, MCP installation, and
  refactor changes into one commit.

## P1 - Track System Decision and Completion

- [ ] Choose a reliable authoring workflow: GameObject child nodes, a simpler
  Editor script, or another explicitly approved approach.
- [ ] Decide whether AI-generated `track_layout_*.png` images are authoring
  references, runtime backgrounds, or both.
- [x] Configure `GameConfigSO.trackId` and verify JSON track loading in the
  Race scene (Silverstone, 60 nodes, 3 laps).
- [x] Validate arbitrary node counts throughout movement and UI; remove
  remaining hard-coded `42` display assumptions.
- [x] Preserve JSON `isApex` metadata and validate apex-only, deduplicated
  corner crossing across the lap boundary.
- [ ] Playtest speed-limit heat penalties through the full Race interaction.
- [x] Drive start/finish lookup and crossing from runtime track-node data;
  validate wrapping and a non-zero start/finish index in EditMode tests.
- [ ] Complete a multi-lap manual playthrough to validate finish timing.
- [x] Implement and unit-test pit entry and pit exit behavior; Play Mode manual validation remains open above.
- [x] Drive LineRenderer positions from loaded track coordinates and verify
  the Silverstone path in Play Mode.
- [x] Replace the inaccurate Nürburgring 24H combined bonus layout with a
  219-node standalone Nordschleife sampled from the referenced real layout;
  verify zero self-intersections and regenerate its guide/background.
- [x] Standardize node colors across all tracks: apex red, other corner
  nodes orange, straights white, and start/finish green; enforce exactly one
  apex per corner group across every track config.
- [x] Add presentation-only lane slots to every track: two lanes for standard
  tracks and four lanes for Indianapolis; keep gameplay, camera, and minimap
  positions centerline-based, and regenerate backgrounds with matching lanes.
- [x] Add Indianapolis lane-specific corner limits (inner-to-outer 4/5/6/7,
  with outer-lane limit 7)
  and a player one-lane inward/outward choice at each start/finish crossing.
- [x] Replace runtime grid-like track visuals with selected layout backgrounds,
  yellow corner masks, red apex masks, visible speed-limit labels, and
  editor-only node metadata.
- [x] Make ordinary tracks use the inside lane by default, move only the
  trailing car outside when cars share a node, and keep Indianapolis vehicle
  placement tied to the player's explicit lane choice.
- [x] Preserve authored corners, start/finish, and pit landmarks while
  re-sampling only the straight runs at equal arc-length intervals; add a
  numbered F8 runtime node overlay and a regression test covering all tracks.
- [x] Keep the background generator on the same 16:9 world-space sampling
  metric as `TrackDataLoader`; regenerate all eight official layouts and
  verify every sampled node remains on the painted road centerline.
- [x] Reconcile Monza corner metadata with the visible turning sections;
  relocate the seven corner groups off the former straight-only cells and
  regenerate its background.
- [x] Reconcile Shanghai, Indianapolis, and Nürburgring GP corner metadata
  with their painted turning sections; move the stale straight-road masks,
  regenerate the three backgrounds, and add regression coverage for the
  corrected node ranges.
- [x] Re-audit all eight selectable tracks in Play Mode with the numbered debug
  overlay; confirm runtime nodes, road centerlines, corner/apex masks and speed
  limit labels remain aligned with the selected background art.
- [x] Add turn-scoped manual camera control to the race map: left/middle drag,
  mouse-wheel zoom, moving-vehicle focus, player focus outside movement, and
  automatic-focus suspension after manual input until the next turn.
- [x] Detach the minimap camera from the moving main camera so the complete-track
  view remains fixed while the player pans, zooms, or follows another vehicle.
- [x] Tune race presentation pacing with 0.14s movement focus lead-in, 0.06s
  per-node pause and 0.10s focus trail-out; keep the authored ±15-cell window.
- [x] Add a configurable test assist that guarantees the China player's
  `cn-hotpot-base` ATTACK card is present in the opening hand while preserving
  hand size and card conservation.
- [x] Rotate vehicles to follow the tangent between track nodes (default sprite offset corrected to 0° for right-facing car art).
- [x] Integrate track weather-pool selection after the core track path is stable;
  profile effects now resolve through `WeatherRules`, with Play Mode multi-weather
 走查 remaining.

## P2 - Demo Asset Replacement

- [x] Reconcile the Phase 2 planning document with assets already completed; remaining gaps are listed in `design/planning/asset-manifest.md`.
- [ ] Finish remaining UI panel artwork.
- [x] Replace gear-button placeholders with arc-arranged circular stove-dial
  controls; China uses a two-position Recover/Go layout and selected gear uses
  the technology-blue state.
- [x] Replace the heat text placeholder with a ten-segment vertical thermometer;
  it counts real heat across all zones, excludes temporary heat from capacity,
  and marks the 50%/70% warning thresholds.
- [ ] Replace remaining flag/UI-node placeholders; track layouts and runtime corner masks are already integrated.
- [ ] Verify card and vehicle sprites in both scenes at target resolution.

## P3 - Feature Completion

- [ ] Tune and Play Mode-verify configured multi-AI races.
- [x] Implement slipstream rules and team range modifiers; broader balance remains open.
- [x] Implement team vehicle attributes and tech modifiers; balance review remains open.
- [x] Shuffle team trick cards into the normal deck lifecycle and replace batch
  hand submission with one-card select/confirm play, immediate trick resolution,
  and explicit end-of-card-phase behavior.
- [x] Implement the driver-selection flow as a catalog, session state, and
  runtime-built main-menu panel; connect the selected driver to race setup.
- [ ] Add sound effects.
- [ ] Add card-play, vehicle movement, bounce, and spin-out animations.
- [x] Add weather gameplay after track data and race rules are stable; the five
  design profiles are now wired into limits, slipstream, cooling, spin-out and HUD.

## Open Decisions

- [ ] Select the replacement for the reverted Track Node Editor.
- [x] Treat Le Mans as a France expansion track with no home team; it is not one of the six national-team home circuits.
- [ ] Confirm whether Kanto Oden carry-over slots are mandatory (the current
  runtime behavior) or optional; Hotpot's additional slot is already optional.
- [ ] Decide the commit boundaries for current scene, data, MCP, and
  refactor changes.

## Completed Context

- [x] Main-menu and Race scene flow.
- [x] Chinese UI and Chinese font integration.
- [x] Card number and heat icon display.
- [x] Six national-team vehicle sprites.
- [x] Eight track-layout image prompts.
- [x] Unity MCP 10.1.0 package installed and connection verified.

## Maintenance Rule

Update this file whenever a task is completed, superseded, or blocked.
Historical Claude Code files under `.claude/agent-memory/` and
`production/session-logs/` remain provenance only; this task list is the
maintained source for current work.

## 2026-08-15 Tech Tree and Team Gear Audit

- [x] Audited the existing tech-tree rules/database against the design docs;
  common, unique and China EV node pools are now selected through one database API.
- [x] Added the main-menu tech-tree entry and runtime-built configuration UI;
  RP, permanent unlocks and active race selections persist per team.
- [x] Replaced the race's demo-only human tech state with the saved profile;
  AI opponents retain transient demo profiles so a race cannot mutate campaign RP.
- [x] Added the China Go/Recover pure gear module and a team-aware facade;
  player controls, AI selection, card limits, overclock heat and Recover cooling
  all use the same rules.
- [x] Added `TeamVehicleRules` as the boundary for team profile values and base
  durability/heat-pool setup; full movement/handling balancing remains a follow-up.
- [x] 历史记录：该切片完成时 Unity EditMode 307/307 通过；当前总回归已更新为 403/403。
  该历史切片的 dotnet build 当时为 0 错误。
- [ ] Continue extracting orchestration from `MVPGameManager` into phase services
  once the next feature requires changes across multiple phases.

## 2026-08-05 Module Audit

- [x] Audited the modules introduced by the previous AI integration commit.
- [x] Fixed temporary-heat card injection and AI effective card-slot handling.
- [x] Connected AI corner risk to lane-specific limits and active weather/tech modifiers.
- [x] Accepted the existing fixed-default weather tracks in the JSON validator.
- [x] Run Unity EditMode/Play Mode tests through the open Unity instance or MCP (EditMode 262 passed; no PlayMode tests configured).
- [x] Corrected Indianapolis lane winding so lane 0 is inside and limits rise from 4 (inside) to 7 (outside).
- [x] Completed a full static + runtime audit: corrected stale stage metadata,
  restored Race speed-card/heat icon references, and removed unsupported emoji
  glyphs from runtime UI labels.
- [x] Added the driver data slice from `foodula-1-drivers.md`: 12 profiles,
  XP thresholds, tier unlocks, UK active-use bonus, XP reward calculation,
  selection state, menu panel, and 5 EditMode regression tests.
- [x] Re-ran Unity EditMode tests after the audit and driver slice: 273/273
  passed with no failures or skips; dotnet build has 0 errors.
- [x] Fixed the card-play lifecycle regression: opening tricks are randomly
  drawn, confirmed tricks enter discard immediately, confirmed speed cards stay
  in the played area until cleanup, and optional discard accepts any non-heat
  card. Unity EditMode tests now pass 287/287; runtime smoke verified the
  seven-card opening hand, button states, hand/UI synchronization, and trick
  transfer to discard.
- [x] Completed the follow-up runtime audit and repaired cross-system card/heat
  ownership: exact runtime card instances are consumed atomically, temporary
  heat can no longer inflate the permanent engine pool, and AI heat payments
  use the same canonical path as human payments.
- [x] Fixed turn-start and movement edge cases: Kanto Oden carry-over is
  consumed even when the tech tree is disabled, Hotpot grants movement only
  when its optional ATTACK slot is actually used, and teleports immediately
  restore the car's track-tangent facing.
- [x] Hardened card UI state: reset clears every interaction mode, heat cards
  are non-interactable, resource displays refresh after card/heat changes, and
  a short action-button debounce prevents a physical double-click from both
  confirming a card and ending the phase. Gear controls are now interactable
  only while the human player is actively choosing a gear.
- [x] Replaced the placeholder race simulation assertions with an actual
  draw/pay/commit/move/cleanup/reshuffle loop and added exact-ownership,
  temporary-heat, Kanto, Hotpot, shared AI heat-payment, and orientation tests.
  Final verification: Unity EditMode 301/301 passed; runtime smoke covered
  main menu -> track selection -> Race, gear/card/discard/reset interaction,
  disabled heat-card input, and post-teleport orientation with a clean console.
  `dotnet build Foodula1.sln --no-restore` reports 0 errors (two existing MCP
  assembly-version warnings remain).

## 本次完成（2026-08-15 赛事回归与平衡）

- [x] 比赛 HUD 增加“返回主菜单”按钮；修复 RaceCanvas 预制体重建后的旧引用导致重复 HUD 的问题。
- [x] 所有主要菜单/比赛控制按钮统一接入短按压/释放缩放动画；运行态确认按钮存在且可触发场景切换。
- [x] 新增 `TrackTeamBalanceBenchmark`，覆盖 Resources 中全部赛道与六支车队，每图 12 场确定性比赛，报告写入 `design/balance/track-team-benchmark-2026-08-15.md`。
- [x] 平衡收敛：标准 AI 在预计抵达弯道时优先低值牌，风险窗口按预计移动量计算；中国队恢复设计案 Go 直道输出、操控从 -1 调为 0，并默认采用 Go→Go→Recover；美国直道加成调整为每回合固定 +1。
- [x] 最终验证：Unity EditMode 308/308 通过；`dotnet build Foodula1.sln --no-restore` 0 错误；MainMenu→Race→返回主菜单运行态冒烟通过，单一 HUD、按钮动画组件和控制台均正常。
