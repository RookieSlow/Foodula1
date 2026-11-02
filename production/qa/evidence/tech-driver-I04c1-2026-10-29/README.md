# I04c1 — 维特尔被动生产接入与有界验收

每圈首次实际非零本队原生收益记录反馈：T1基础巡航，T2加实付黑啤，T3加真实德式出弯；来源圈号在结算前冻结。下次本人合格驾驶输入开始锁定旧手永久热，常规抽牌后最多回收一张仍在手中的原实例；无牌失效，不从弃牌/临时热/新抽热替代，净回与退役分开。预览深复制集合并重绑定独立牌堆/热池，不触发真实资源；跳过、进站、失控、终态、退出/重赛清理，普通AI/教程不启用。

本包只替换被动；维特尔主动/按钮/来源确认仍旧。I04保持进行中。

## 原生结果

- 11模块tech/drivers/cards-heat/core/gears/ui/tutorial/tracks-weather-pit/career/ai/tools：3714/3714、失败/跳过/不确定0，35.3502378s。原生结果在native/modules-final.json。
- 首轮3713/3714，35.3438921s；预测副本未重绑定的拒绝守卫误清副本，已修并保留原断言。
- 实际GameLoop首轮7例6通过1失败；唯一失败是新增夹具硬编码圈号1，而PlayerState.lap从0计已完成圈数。没有改生产行为以迁就测试。
- 仅修夹具取真实来源圈号并补诊断，fresh editor assembly后单例1/1通过、0失败/跳过、1.5235029s。首轮其他6未重跑，不称最终7/7，不叠加成重复唯一案例数。native/gameloop-vettel-final.json含真实位置7/账本7、反馈input1→2、回收原实例证据。
- 新例94=51纯规则+39整合+3profile+1GameLoop；RegressionModules.json已登记，native partial沿原long-races模块。

模块runtime MVID f0d11d5d-3edd-46cf-a8dc-d5dd756a1bca，editor68bfbb8e-c587-420b-a27a-8df8998ebba1；夹具修正后runtime不变，editor21ecc0f7-b3c5-437c-9131-d32622a5d279，编译实际加载。未重复无变化全量。

## 生产接线

VettelFeedbackRuntime保存独立本人输入/来源圈与旧手实例；RaceSession在真实永久热黑啤成功扣费处记录；MVPGameManager在最终移动前冻结巡航/出弯来源，实际移动账本足额结算及弯道结果后再记录，在A2普通抽牌后回收。捕获沿原生1值转换判定（含合法临时速度1），不把底盘pace当巡航；实际账本避开非0起终点距离误判。RaceTurnPreviewRules显式重绑定隔离副本。技能生命周期清理接原适配。旧周期抽高牌失效，主动保留旧实现。

## 三尺寸受控生产UI

改前目录：D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04c1-2026-10-29/before/profile-20261028-231355670

改后截图绝对路径：

- D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04c1-2026-10-29/after/profile-final/1920x1080.png
- D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04c1-2026-10-29/after/profile-final/2560x1440.png
- D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04c1-2026-10-29/after/profile-final/1280x720.png

改前3/改后3全帧已查看；生产DriverSelectionUI六队12卡，点击/Submit/Back/焦点/下层遮罩由夹具实际操作。D04被动文案无溢出，只有该字段复用12..14autosize，几何不变。截图默认选中D03，但D04新文案可见于第一排右侧，遍历选择有records.json。其它14项既有溢出未接受，720p舒适度/完整RaceCanvas/硬件/实际拖动/全模式/存档兼容未在此包验收。

## 状态与保留

MainMenu干净，未播放/编译/导入/测试，Console0错误0警告未清，ready true。Editor.log成功解析许可。8Word/32场景/4既有排除文件SHA不变，原未跟踪条目保留；status/diff与源文件SHA见native、manifest-sha256.json。active.md及既有治理缺文件只记录，未建占位。独立专员审查与QA结果见summary；未提交、推送、清理、安装或修改快速播放设置。

下一包：I04c2：维特尔主动固定来源选择、永久热固定费用与有限本人输入增幅的运行态/生产API；随后真实按钮合并确认与三尺寸交互。I04保持进行中，完整模式/存档/硬件组合留I13。
