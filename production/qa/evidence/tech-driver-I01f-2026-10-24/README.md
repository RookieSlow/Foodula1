# I01f 实际确认风险提示与 UK 有界操作交接

日期：2026-10-25（heartbeat触发UTC2026-10-24T23:48:37.746Z）。当前执行指针交接I02；I01仅源码与有界操作通过，完整模式未完成。

## 实装

MVPGameManager.TryRequestHunterSkillSelection 的真实必选费用正文改为三行：目标/距离；弃1张普通速度牌、本次移动收益、余次；“新增弯心仍按原速度付热，热量不足会失控”。沿用原合并确认、选牌和费用计划；未改规则数值、角速、次数、ID/XP、存档、其他车手、AI或普通支付。

新增36项 HunterSkillProductionUiTests，登记RegressionModules.json的ui；原CareerUiPointerTestScope.Create仍保持1920×1080，新增CreateSized用于三尺寸。自有预览Canvas上实际HUD.Start/真实driver button listener/MVP计划，按钮点击与真实GraphicRaycaster、Move/Submit路径检验同值第二费用实例、确认去重、取消/重开不扣费、遮罩阻断及合法焦点返回。设置、会话、音频服务引用、原EventSystem/焦点、字体副本及RT在finally恢复/释放。

## 当前原生证据

- [native-result.json](native-result.json)：原生RunFinished **1394总/1394过/0失败/0跳过/0不确定，10.1942977秒**，selectionMatches=true；UTC00:05:26.9445604Z–00:05:39.2806701Z。
- 选择ui、drivers、tech、gears、cards-heat；含36新增、7项既有gear拖动案例，非全量，未重复I01e十九长流程。
- 最终已加载runtime `6c8c3586-de04-4d92-b2c8-5603486e8a98`、editor `cf76e26f-bdad-4fd0-b702-491f73561dc6`；先确认新程序集再跑，详见[final-loaded-assemblies.json](final-loaded-assemblies.json)。

## 真实改前/最终改后

- BEFORE绝对目录：`D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I01f-2026-10-24/before/run-20261024-235938537/`，18PNG，旧runtimefd9e07a1；[manifest](before/run-20261024-235938537/manifest.json)。
- 最终AFTER绝对目录：`D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I01f-2026-10-24/after/run-20261025-000512846/`，18PNG，最终新runtime；[manifest](after/run-20261025-000512846/manifest.json)。
- 1920×1080、2560×1440、1280×720；normal/equal-second/long-copy/keyboard/cancel-reopen/unavailable。每套18逐张查看，TMP溢出0；root before9、UX before9，最终root6、UX12，合并各18张。有限独立复审APPROVED，见[review.json](review.json)。1440图原尺寸保存，查看工具可能缩到2048预览。
- 六队为真实目标车手身份，roster2/4/6/12及长中文数字覆盖；owner始终Hart，不代表其他队技能或整场模式已实现。
- 首次捕获因夹具把Canvas当GearButtons导致入口射线被挡，修正夹具层级后再拍before；[失败原件](before-capture-initial-failure.json)及空目录均保留。中间after `run-20261025-000134385`为“整笔”旧措辞，保留但不计最终18图。没有覆盖/删除旧证据。

## 验收边界与 Unity

这是明确标记的生产弹窗受控夹具；背景入口/挡位为自有原语，不是完整RaceCanvas排版/样式验收。原生EventSystem/GraphicRaycaster派发不是OS硬件输入；没有完整比赛、教程/生涯组合和存档兼容收口，720p舒适度也不能据零溢出判定。I01源码/有界操作交接后进入I02曼森两段同目标追近，剩余上述项目在I13持续未勾选。

最终[Unity](final-unity-state.json)：MainMenu及所有loaded scene干净，未播放/编译/导入/原生测试占用，Personal许可。最终[Console](final-console.json)error0，既有TeamGearPresentationTests缺cardPrefab夹具warning1，未清Console。editor/state资源[仍stale_status/ready=false](final-ready-resource.json)，没有伪称缓存ready；实时直接查询/原生安全门/RunFinished是本包证据。无服务重启、安装、场景保存或用户播放中断。

## 保留及来源

两份Word SHA均与锁定计划一致，无改正文/表格/反馈；详见最终hash与preservation-audit。开始基线4236个tracked/untracked文件，无任何基线文件删除；生产源仅上述文案变化。保留已有字体、设置、音频及所有先前代码/资料。当前Assets没有InitTestScene匹配文件，未创建、保存、移动或删除任何场景。active.md及五份治理引用缺失继续只记。没有commit/push/reset/checkout/rebase。

正式GDD、UX/checklist、tests/regression-suite、三份项目连续性及plan/progress同步本包已实装范围；历史章节保留当时边界，以本节覆盖最新状态，不把I02–I13写成完成。
