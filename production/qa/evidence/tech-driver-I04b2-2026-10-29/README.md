# I04b2 舒马赫真实按钮、确认与三尺寸受控UI

### 2026-10-29 I04b2：舒马赫真实三段按钮与确认

HUD与Manager按钮已统一路由三段战术，失败也不落回旧免费PerfectLap；准备以一次必选原速度牌窗弃牌，执行1/2复用一次付1永久热确认，沿DriverSkill确认开关，关闭开关仍不能跳过准备选牌。显示准备/执行/已提交、费用收益、限制挡位、取消失去剩余段、余次与缺热/低挡禁用原因。已付准备限1/2、已付执行限3/4，挡位呈现同步禁用；取消/陈旧回调依原计划版本，保持1/1/2场次、ID/XP/解锁不变。正式车手页主动名/摘要已更新，仅D03主动12..14字号适配。

新增51UI参数（48HUD+3profile）登记ui；实际GameLoop既有三段案例升级为按钮入口→HUD确认，仍1案例内部level3/5两场。原生11模块3618/3618，0失败/跳过/不确定，34.3221161秒；另精确6条GameLoop6/6，6.803973秒。之后仅D03主动字号及3回归改变，新runtime a00e5717-db24-4c9a-b83d-7d49d9833c13/editor c3e1959e-bb44-405c-a813-7d417415e7a3加载，最终ui/drivers/tech1576/1576，0失败/跳过，20.7530156秒；各选择重叠，不累计为独立数，6条未冒称在最后字号程序集重跑。首轮48新UI失败结果MCP截断且无汇总，通过数计0；补齐夹具四挡/合法高挡焦点和300×330容器后断言保留并通过。最早新夹具不存在EventCamera编译错已改真实Canvas.camera；一次MCP重载警告已消失。新主动摘要三尺寸溢出在profile检查发现，局部字号修正后均0，其他14条旧全页问题仍未验收。

改前HUD3/profile3，改后HUD24/profile最终3；实际查看HUD改前3、改后15（准备/执行1/执行2/缺热/低挡）及profile前后6帧，三尺寸齐全。owned正交生产Canvas、GraphicRaycaster/Submit/Cancel焦点/遮罩与费用验证，不是全RaceCanvas/硬件/完整模式。改前HUD的合成gear容器仅G1且过小，初版白底白字被拒、先用深底重拍；改后夹具补四挡/容器，不能把整块gear布局前后差异当生产改版。既有拖动随gears/ui模块回归，未新增硬件拖动验收。截图绝对路径及记录见 production/qa/evidence/tech-driver-I04b2-2026-10-29/README.md。

独立代理仅完成前置建议后触发用量限制，未写生产/最终签审；根完成实现/源码复核/原生与视觉检查，不声称独立最终审查。最终MainMenu干净、未播放/编译/导入/测试，ready正常，Console0错误0警告未清，许可正常。8Word/32场景/4排除文件与原未跟踪条目保留；无提交推送或系统/快速播放设置修改。I04仍进行中，下一I04c维特尔；全模式/存档/硬件与720p主观舒适度留I13。

## 直接证据位置

- 改前HUD：`D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04b2-2026-10-29/before/hud-20261028-224125331`
- 改前车手页：`D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04b2-2026-10-29/before/profile-20261028-174153715`
- 改后HUD：`D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04b2-2026-10-29/after/hud-20261028-224557009`
- 最终车手页：`D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I04b2-2026-10-29/after/profile-final-20261028-224832541`
- `native-11modules.json`与`native-final-ui-drivers-tech.json`为完整原生RunFinished/selectionMatches；GameLoop逐条结果见`gameloop-final.json`，权威原生根见`final-0.json`。
- 原始前后status、SHA256、源增量及新夹具源文件均保留。active.md/control-manifest/technical-preferences缺失只记，不造占位；两份草案SHA未变，未改反馈原件。
