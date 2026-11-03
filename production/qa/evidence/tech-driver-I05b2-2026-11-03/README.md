# I05b2 精准调校真实HUD入口有界接入

本包接真实入口、已选原牌目标、CardAction一次合并确认、pending取消与Space同门控、UK自有预备餐与借IT分槽共存；不重设计I05b1规则，不新增单场RP消费。受控玩家可操作与三尺寸HUD已验；科技树L3节点说明/节点屏另留I05b2c，阿斯卡里未接，整体L3/I05未完成。

最终模块3957/3957、56.4134289秒；分开实际GameLoop6/6、11.0206667秒。精确选集、MVID及历史失败见[summary.json](summary.json)，原始证据在native/。

首before helper CS0052为0测试/0图；第一手工420宽槽且未配真实CardHandUI卡面6帧，被拒作最终几何证据，原图保留，不外推旧玩家UI损坏。最终before/after采用生产RaceUILayoutController与真实RaceCanvas卡面；实际查看清单和绝对路径在summary.visual。当前生产没有卡牌拖动，本包未新增或声称验收卡牌拖动；既有挡位拖动保留，物理拖动未验。受控夹具不等于完整RaceCanvas、硬件/存储/全部模式或720p舒适度验收。

此前未知字体变化已是本轮02:30基线，保留现状不回退；本轮保护文件比较见结构化preservation。下一实际生产切片接阿斯卡里首个目标/同卡互斥提交，并同步补L3节点说明及三尺寸节点UI；I05b2c保留为欠验子项，不能以单独文案或截图冒充新实装。I05保持进行中，无提交/推送。

## 最终验收与边界

最终当前 runtime `395216e3-1c1e-45e6-91cc-eb8b744a75f5` / editor `ddb74de6-a18e-4436-b089-f7207f296f66`：11模块 **3957/3957**、失败/跳过/不确定0、**56.4134289秒**；另6项实际GameLoop **6/6**、失败/跳过0、**11.0206667秒**。模块与长流程为3963个不重叠NUnit案例；36项定向UI及重复运行不另累计。新增两项长流程复用单一活动EventSystem并验证恢复，最终无重复EventSystem警告。

历史首模块3957/3957、首长流程6/6含重复EventSystem警告均保留；第二次长流程原生RunFinished为 **6总数/4通过/2失败/0跳过**，失败为夹具焦点恢复的Unity fake-null断言，耗时缺失，不推算。最终修复只改新夹具，生产runtime与已有截图一致。首两次定向UI失败的MCP摘要不完整，原始失败列表保留。详见[native历史与最终证据](native/recovery-0730/)及[结构化记录](summary.json)。

最终Unity停留 `Assets/Scenes/MainMenu.unity`，场景干净、未播放、未编译/导入、无原生测试运行；许可有效，`ready_for_tools=true`。Console原样保留 **1 error / 19 warnings / 101 logs**：error为 `TrackDataLoaderTest.test_load_config_missing_track_returns_null` 已用LogAssert.Expect声明的no_such_track负例（源码track_data_loader_test.cs:189），意外错误0；19条warning为既有owned夹具未配CardHandUI.cardPrefab。6项长流程输出另有5条无完整场景相机配置的RaceCameraController提示。本包未清Console、保存场景或改快速播放。见[Console](native/recovery-0730/console-final.json)、[Unity最终状态](native/recovery-0730/unity-final.json)与[ready](native/recovery-0730/ready-final.json)。

## 实际截图查看

采用生产布局/真实卡面受控夹具，覆盖1920×1080、2560×1440、1280×720。专项逐张实际查看最终改后 **54张**（IT/UK五状态30张，六队普通门控及IT/UK owned生涯快照24张）；根另复核以下6张。最终改前6张由专项全部查看，根看3张。这里只验该控件与夹具，不等于完整RaceCanvas、实体键鼠/挡位拖动、真实生涯启动、存储兼容或720p主观舒适度；L3节点屏和阿斯卡里待续。

- 改前最终6图：[D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/before/hud-20261102-184011180](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/before/hud-20261102-184011180)
- 改后主状态30图：[D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/hud-20261102-235123151](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/hud-20261102-235123151)
- 改后六队/快照24图：[D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/visibility-20261102-235125244](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/visibility-20261102-235125244)

根另复核：

- [1280x720-UK-confirm.png](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/hud-20261102-235123151/1280x720-UK-confirm.png)
- [1920x1080-IT-target.png](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/hud-20261102-235123151/1920x1080-IT-target.png)
- [2560x1440-UK-pending.png](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/hud-20261102-235123151/2560x1440-UK-pending.png)
- [1280x720-IT-used.png](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/hud-20261102-235123151/1280x720-IT-used.png)
- [1920x1080-UK-ready.png](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/hud-20261102-235123151/1920x1080-UK-ready.png)
- [2560x1440-IT-confirm.png](D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I05b2-2026-11-03/after/hud-20261102-235123151/2560x1440-IT-confirm.png)

专项逐图清单与SHA见[visual-review-final.json](native/recovery-0730/visual-review-final.json)。源29项、保护44项与本包基线无关82项均匹配；旧字体未知变化已在本包基线中保留，未回退。Git diff --check通过，未提交/推送。
