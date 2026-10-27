# I02b 曼森连续追近被动：生产接线与原生 A2 验证

2026-10-26；执行指针保持 I02。本轮使用 code-review 与 regression-suite 工作流，按 ADR-002 保持运行时、Session与Manager适配分层。

## 实装范围

接续上轮留下的 MansellPursuitRuntime、RaceSession A1/A2 与Manager A2接线，完成专用回归和真实GameLoop验证。最终终差距按被动三级为1–2/1–3/1–4格，初次被动观察没有主动6格上限；同最近前车原实例及ID、正差距严格缩小才能结算。每圈首次合格无热也过期。A1冻结旧手永久热，A2普通抽牌/早餐后最多冷却1；弃区付热、新抽永久热、临时热和重复区域原实例不能补位。报告区分实际移除、区域退役与净回。

本轮生产修复：预约进站未执行的最近前车不得被忽略；冻结HeatPool引用，A1/A2换池拒绝过期结算；删除已废弃的旧超车奖励待兑现状态，保留兼容调用入口但不再授予旧奖励。未改变两段主动、其他十位车手、普通部分支付、ID、XP键、等级、UK2/2/3次数、存档或AI启用策略。

## 真实验证与失败记录

- 新48个纯运行时案例登记drivers；新1个真实Manager GameLoop A1→A2→预备餐快照案例沿RaceGameLoopCoroutineTests既有long-races归属。
- 首轮tech/drivers/cards-heat/core/tutorial/tools原生1824总、1822过、2失败、0跳过/不确定，26.4781307秒。失败为重复热夹具未成功造出非法实例，及一处旧Mansell位移奖励断言遗漏。失败原件保留，未弱化新规则正向断言。
- 修正后drivers/core/tools原生RunFinished790/790、失败/跳过/不确定0，14.0069824秒，selectionMatches=true；此前tech/cards-heat/tutorial未变的通过证据保留，不伪称最终重跑全量。
- 精确真实GameLoop案例1/1通过，1.9035575秒；独立SessionState原生RunFinished在final-unity-state.json中确认total1/pass1/zero failures。MCP发现3323是库存数，不是本轮执行数。
- 首次新协程误用NUnit字符串Contain导致1个编译错误（compile-console-final.json），修复后确认新runtime/editor MVID才运行。最终runtime b9a158f7-eea3-454b-897f-e88fbdd3b667，editor 06b50765-73af-46c8-8c60-7f55bb2fc9f7。
- 独立Unity静态审查有界APPROVED；QA建议补真正已超越负赛程差已落实。审查不替代原生结果。

## Unity、来源与验收边界

最终MainMenu，所有loaded scene干净，未播放/编译/导入/测试；ready=true且非stale，Console0error/0warning，未清Console。Personal（HasPro=false），Editor.log成功解析entitlement；未安装包/改设置/打断播放/保存脏场景。

两份采用Word SHA256仍为科技281e077813344ab1e3627bb075dcc28d361a0626c8c39b3492f016a9f62d7bb3、车手94eb7763825f777d477b3fcb8fee92bad27e6353e42d8b2cdeb98f3889914f46；未覆盖Word/Markdown反馈原件。production/session-state/active.md与.Codex/docs治理文件缺失，仅记录。

本包未改UI/文案布局，无新截图，未进行三尺寸视觉验收；技能面板旧名称与说明仍待I02展示接入。完整被动赛事、模式/存档兼容、硬件操作/720p舒适度仍未验。正常Initialize重建配额已测，不声称任意非法跨RaceSession复用同runtime安全。

本包只完成被动规则/生产接线/有界A2自动执行，玩家可操作整包与完整模式未勾选。下一I02c接曼森首段弃普通速度并固定付热1、下一本人输入同目标严格追近后可选付热1的第二段；界面及三尺寸验收仍需后续完成，I03尚未开始。无提交、推送、清理用户文件或恢复其他自动化。

## 证据

此目录保存原生结果、MVID、Console/ready及基线SHA；完整status/diff和源码快照清单在 D:/UnityProject/Foodula1/Library/AutomationReview/tech-driver-I02b-20261026/。最终保留审计见 preservation-audit.json。
