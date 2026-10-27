# I01a 英国预备餐（2026-10-22 开始，10-23 收口）

生产代码已接入、定向自动验证通过；I01整体及本切片残余验收尚未完成。

按锁定Word UK A：精确弃1普通速度，冻结A2早餐后的H/D旧永久热，A4首张正式牌前使用，每车每圈一次；1/home2，退役后净回>0才可提交。UI、规则、运行时和Manager分层；旧热不足救援真移除，普通部分支付/失控保留。稳定科技ID/车手ID/XP和持久化规则未改。AI科技走同一事务、保留足够必需速度，未启用普通AI车手技能。

正文抽牌早餐后与P4选挡确认前冲突：真实流程A1→A2→A4不调序，具体映射见正式科技GDD。

测试：first-changed-native.json 2030总/2025过/5失败/0跳过，22.0103844s；4旧免单预期与1缺config夹具已修正。final-native.json 2034总/2034过/0失败/0跳过，21.3845409s，selectionMatches=true。新增UkPrepMealTests21、UkPrepMealManagerTests4，已登记RegressionModules的tech/ui。模块tech,drivers,cards-heat,gears,core,ui,tutorial；不跑长比赛/全量。

编译：runtime b6b8eb52-8e0d-4a58-83ea-f95795846022，editor f5986d9e-4059-4728-9fa2-e61d00d7ef6c；确认新测试类型已载入。最终MainMenu/isDirty=false/idle，不播放、不编译、不导入、许可证有效、原生tests=false。Console0error；既有CardHandUI无cardPrefab测试警告1。主观/完整模式不据此判通过。

截图：before/run-20261022-154227295/（3），after/run-20261023-083233617/（27），1920×1080、2560×1440、1280×720。明确生产RaceCanvas受控EditMode夹具，显式状态/示例文案，不是完整比赛。manifest0overflow。root查看全部改前3和改后10：normal入口1080、disabled720/1440、used1440、one1080、two1440、retirement720、second1080、cancel720、long720，未见新增裁切；未逐图查看其余17。

有限清单：
- [x] 精确费用/来源、取消、过期绑定、同圈/次圈、预测容器隔离；home矩阵及旧免单退出。
- [x] 原生产科技按钮到一次modal确认/取消，mask0/All与A1/A4显示互斥，实际Raycast pointer测试。
- [ ] 成功退役/net1、双UK独立次数、Hart/L1费用组合、完整A2/A4、AI整局和模式隔离长流程。
- [ ] 真实拖动/键盘焦点全路径、720主观舒适度、独立最终视觉/QA复审。

team-ui前置UX/引擎、code-review边界及QA矩阵已读审；UI和QA代理中途额度耗尽，由root续写测试/验证，独立代码复审随后完成APPROVED、无必须修复项（review.json）；独立视觉/QA最终复审仍待补，不伪称整包批准。无新素材，不改Word输入/草案，无commit/push。active.md和五份.Codex治理文件缺失只记录。
