# I01d 费用选择焦点隔离（UTC 2026-10-24 / 本地2026-10-25）

实际生产改动：DriverSkillCardChoiceUI保存四按钮原Navigation；仅连接窗内可用按钮。HUD激活面板并刷新控制后移焦取消。翻到首尾时在设置interactable之前捕获当前箭头，防止Unity自动清空后无法回退；关闭/生命周期恢复原Navigation，沿用HUD回调后合法原焦点恢复，不加新快捷键或费用逻辑。8项新测试进入既有HunterSkillCardChoiceUITests；两个既有真实GameLoop时钟用例补真实TrackManager/20节点依赖。RegressionModules.json已有这两个fixture，保持不变。

首轮原生RunFinished：2363总/2360通过/3失败/0跳过/0不确定，12.8776254s。真实焦点错误1项、旧时钟夹具缺赛道2项；完整失败及stack保留first-native-result.json。修复后同六模块2363/2363、失败/跳过/不确定0、12.1640629s，selectionMatches=true、无missing/unexpected。包含I01b的36项Hart规则与I01c两项正式提交按钮状态测试；不是全量，也不是新增2363项。见final-native-result.json与native-plan.json。

编译：最初隔离Roslyn生产/Editor exit0，1.4428788s/1.3471747s（初稿证据）；真正原生运行前显式刷新，请求脚本编译。最终运行时MVID de3963f6-f8d6-44a8-ae5f-11d15906f580，Editor MVID 2f97f193-8dd9-4c61-a1b1-9fc5ddc5b474，与首轮不同，新增用例反射可见。最终修复采用Unity实际编译加载的新程序集，不拿初稿独立编译冒充最终编译。静态QA APPROVED，首次静态未捕捉Unity禁用按钮清焦点，保留此证据边界。

Unity服务由拒连恢复healthy，当前工具清单未挂Unity工具，通过已配置127.0.0.1:8080/mcp原协议调用；无安装、服务/连接配置修改。直接实时查MainMenu/isDirty=false、所有loaded scene无dirty、未播放/编译/导入/native test或模块占用，Personal许可证；原生runner再自检。editor/state资源持续缓存stale_status，不能冒称ready=true；具体见unity-postflight.json。最后Console仅no_such_track一条error，对应track_data_loader_test.cs的LogAssert.Expect；首查另有VS UDP端口warning，未改网络或清日志。

三尺寸真实生产受控夹具新after：D:/UnityProject/Foodula1/production/qa/evidence/tech-driver-I00b-2026-10-22/after/run-20261024-175918440/manifest.json。复用已审查HunterSkillCommitCapture，产物仍按其既有I00b路径另起唯一run；15张原尺寸1920×1080、2560×1440、1280×720，5状态（普通/第二同值牌/长中文数字/空费用禁用/取消重开），0文字溢出。root看三张long-copy，以及1080-disabled/1440-second-card/720-cancel-reopen共6张：窗内正文与按钮无截断或遮挡，720小字舒适度仍未主观验收。捕获后恢复原EventSystem焦点，未加载比赛或保存场景。该helper禁用项目行为、用真实HUD回调；不能据它冒充六队/车手/参赛车数/模式或真实鼠标拖动。本轮没有新before截图，旧I00b before仅历史参照，改前后交互验收仍欠。

未验收：实际Manager尾流遍历/奖励协程、完整A2/A4、真实键盘确认与鼠标拖动三尺寸组合、完整自由赛/教程/生涯及存档兼容。I01不标完成，I02-I13不推进勾选。下一有界I01e完成实际环路夹具与相应操作验收；不重复无变化模块全量。

保留与治理：4184文件SHA256基线使用Git -z正确解码中文；两草案Word SHA不变，全部原文件/排除音频/InitTestScene/字体/settings保留，无删移/提交/推送/包安装。active.md和五治理文档仍缺失，只记录。最终preservation.json列基线差异；未知未来JSON字段保留。文档同步以此实际范围为准，不覆盖反馈原件。
