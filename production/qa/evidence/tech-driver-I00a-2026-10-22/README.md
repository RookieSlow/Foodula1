# I00a 英国车手本人输入时钟

2026-10-22。I00 的第一个实际生产切片已经接入；I00 整体仍在进行中，尚未接入新费用、旧来源目标或待确认计划。未修改 Word、身份、XP、RP、普通支付/挡位规则、场景或快速播放设置。

## 实际增量

- DriverSkillRules 显式只迁移哈特与曼森。DriverSkillRuntime 区分全局清理和本人驾驶输入，每轮去重，新计数和门控为标量，预测复制保持独立。其他十位车手沿用旧时钟。
- PlayerState 仍做全局临时状态清理；RaceSession 根据真实 A1 优先级开始本人技能时钟，MVPGameManager 在选挡前接该入口。DriverBalanceBenchmark 使用同一入口，避免旧模拟把英国持续期保留整场。
- 已登记进站、恢复/跳过、完赛或爆缸中断窗口，已花次数不返还；回合末终态与跳过也立即清理。曼森待兑现旧被动在本人输入准备，跳过丢弃，不延期。原新技能数值和触发尚待后包。

## 本轮原生验证

| 执行 | 总数 | 通过 | 失败 | 跳过/不确定 | 原生耗时 |
|---|---:|---:|---:|---:|---:|
| 改前 tech drivers core | 1051 | 1051 | 0 | 0/0 | 14.8281679 秒 |
| 首个改后 tech drivers core tutorial | 1325 | 1324 | 1 | 0/0 | 15.9599215 秒 |
| 最终 tech drivers core tutorial | 1329 | 1329 | 0 | 0/0 | 17.031282 秒 |

三次均为本轮实际原生 RunFinished，不把重复执行相加成覆盖数；最终 selectionMatches=true。首个改后失败是旧 RealCleanupRegistersCurrentOvertakesForNextTurnOnly(1) 在全局 BeginTurn 后立即期待被动 +2；保留其超越数、移动/冷却和一次性原断言，改为进入本人输入后兑现，之前明确为零。另补两位英国重新初始化及曼森正常/跳过共四例。失败原件留存，不当成曾经通过。

共新增 43 项：UK 两人三级、重复调用、禁用、同队独立、预测副本、进站/恢复/跳过/终态、清理不退次数、重赛复位、被动不延期，以及其他十人旧时钟。真实 GameLoop 的 A1 前缀由手动推进实际迭代器验证；该测试没有进入 Play Mode，不证明完整比赛、物理输入或全新技能已完成。原两 fixture 自动归属 drivers/core，RegressionModules.json 不变。当前发现 3045，最终执行 1329、未选择 1716，长流程选择 0；未选择不记跳过。

新程序集的运行时 MVID 已从 d8eb8299-ce73-4597-b94c-7f15c13c1622 变为 a5f6b157-de12-4b7a-864c-f5b9910f5f96，BeginDrivingInput 与新增测试均通过反射确认在新程序集；测试修订后的 Editor MVID 为 346239b4-6b91-4a62-a676-e7dff345f31a。独立 Unity/QA 只读审查通过，复位与跳过建议已补测试。

## 编辑器与边界

收尾 MainMenu 干净，未播放、编译或导入，ready=true，原生和模块化测试空闲，许可 HasFreeLicense=true。Console 留有技术快照无效负向用例预期 Error 1 和开局 ATTACK 负向用例预期 Warning 1，其 LogAssert.Expect 在 race_technology_setup_test.cs 的 631/666 行；没有新增编译或非预期错误。没有清除这些 Console 记录。

本包没有改 UI 源码/几何/字体/文案，未新增截图。三尺寸视觉、新费用/目标计划、实际新技能、完整默认模式及长期平衡未验收。没有运行 Play Mode、长流程或全量；I00 费用与目标承诺仍未完成。未提交或推送，全部既有修改与未跟踪文件保留，详见 preservation-report.json。

## 下一有界包

I00b：以英国预备餐或哈特的一个真实动作接入 session/参赛车/输入版本的待确认计划、弃牌目标与旧热来源校验，取消不消耗、最终提交去重；连到现有确认入口，不单独造空壳框架。接 UI 时再提供真实三尺寸与交互证据，随后进入 I01。
