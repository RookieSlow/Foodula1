# I03a 德国黑啤固定费用切片

接续I03锁定方案，先修复一条实际生产付款链；本包不是完整德国有限爆发或手动操作接入。

生产四文件：

- CardDeck：TryPayFixedPermanentHeatToDiscard，验证引擎至少1，生成唯一永久热原实例、整笔扣1并入弃区。空引擎不借手牌/临时热，不失控、不记次。
- RaceSession：TryCommitSchwarzbierFuel，效应/圈/资源/终态校验后提交，再登记同一收据、真实付热计数和圈标记；无L1不因借L3赠燃料。
- MVPGameManager.ComputeMovements：只在实际固定支付成功后加2，保留热量表现；不走普通HeatPaymentCostRules。
- RaceTurnPreviewRules：副本共用固定提交，计HeatPaid1；终态标记也复制；不动真实牌值、资源、收据、圈标记或普通折费状态。

明确诊断更正：当前ApplyHeatMultiplier为恒等，PerfectLap的免热仅作用普通角费。旧泛用黑啤路径确定会消耗舒马赫被动折费（可能折到0）和黑面包次数；不能把主动免角费/倍率说成当前免费黑啤根因。普通HeatPaymentCostRules与RaceHeatPaymentExecution完全未修改，原部分支付/失控沿用。

## 当前真实验证

| 项目 | 结果 |
|---|---|
| 原生模块 | tech/drivers/cards-heat/core/tracks-weather-pit |
| RunFinished | 2286总/2286过/0失败/0跳过/0不确定，27.771329秒 |
| 精确选择 | selectionMatches=true |
| 新增固定费用/预测 |20例，SchwarzbierFixedPaymentTests登记tech |
| 新增MVP真实移动适配器 |8个参数，RacePitTurnBoundaryTests原tracks-weather-pit归属 |
| 新runtime |2f6be9ac-e642-4a6b-a4ae-d9e3429edeea |
| 新editor |561035bf-5f01-4907-aa0d-0fb7cf3d95c9 |
| GameLoop/PlayMode/长比赛/全量 |本轮未运行，不算历史通过 |
| 新截图绝对路径 |无；没有新HUD布局/文案/手动入口，不宣称视觉验收 |

原生结果：D:/UnityProject/Foodula1/Library/Foodula1/RegressionRuns/20261026T223137017Z-aac08e04/result.json；本包native-summary.json为摘要，完整副本与全部编译/Console/状态原件在Library/AutomationReview/tech-driver-I03a-20261027。

案例覆盖实付原实例、空资源/缺池/缺牌组/缺L1/禁科技/终态、同圈跨回合去重/下一圈、最后一张热、同DE参赛车独立、借L3不送L1、普通折费/黑面包保留、免角费仍生效、三次预测隔离、终态预测；MVP适配器实际调用ComputeMovements，对0/1热×无效果/免角费/被动ready/黑面包进行断言。AI标记只用于夹具禁音，不是游戏普通AI技能策略变更。

初始fixture有两条CS1503：NUnit Does.Not.Contain误用字符串重载，改CollectionAssert后，确认DLL时间新于最后测试源码、加载新editor且Console无编译错误，才开始原生测试，未用旧程序集冒充。静态反馈还纠正了免角费前置方法和终态选牌没入手的假通过。引擎/QA独立审查给了初步反馈，随后因额度失败，未完成最终独立审批；父任务已自审修正及原生验证，不声称完整独立复审通过。

## 安全与未验收边界

MainMenu及全部loaded scene干净，未播放/编译/导入/原生/模块测试；许可日志Successfully resolved entitlement details。最终Console error1（预期no_such_track负面夹具），warning0，无编译错误，未清Console。ready资源stale_status，直接状态查询与原生RunFinished保留，不当作新鲜ready。MCP旧会话404已重新initialize，没有安装/重启系统服务。

当前L1自动时机和旧L3自动返热仍保留；**手动L1/L3声明、确认前窗口、首笔真实支付锁每场机会、本回合黑啤合计最多3次、冻结窗口收据/收尾返还、取消/进站/跳过/终态/退出/重赛及预测集合清理尚未按新案接入**。I03进行中，不进入I04；不因本包付款修复声称德国技能/新主动已完整实装。下一I03b接有限窗口和实例返还，随后手动UI按三尺寸真实改前后验收。I13完整模式/存档仍待后续。

Git及SHA在Library/AutomationReview/tech-driver-I03a-20261027，含baseline、status/diff前后及final-audit。Word原件、32场景（含InitTestScene）、排除文件和所有既有改动保留；无commit/push/reset/checkout/rebase/删除移动清理/系统设置/快速播放设置修改。active.md及治理文件缺失只沿记录，不建占位。
