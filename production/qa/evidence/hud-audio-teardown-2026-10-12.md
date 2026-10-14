# HUD 先销毁时的引擎声音清理修复

用户提供实际退出日志：MVPGameManager.OnDisable／OnDestroy → StopMovementAudio → HUDUI.SetEnginePresentation → EnsurePresentation，在 HUD 已销毁后抛 MissingReferenceException。修复前在独立预览场景中用实际销毁的 HUD 和这两个回调均复现；Unity 判空为 true，托管 ReferenceEquals(null) 为 false。

管理器的声音更新、等待换道停声和终态停声改用 Unity 对象判空。HUD 的强度更新只写仍存活的既有 TeamGearPresentationUI，不再调用 EnsurePresentation；创建仍由启动／配置入口负责。音频停止与归属／步数清理先完成，不依赖 HUD 存活。不改变赛车挡位、支付、移动规则或音色。

新增六项 EditMode 边界案例，覆盖两种回调重复清理、迟到进度、裸 HUD／已销毁 HUD 不创建界面及已销毁视觉子对象。声音移动与六队 UI 组合 **60/60 Passed**，失败／跳过 0，作业 `692a69a42fd8410586d81d72c11bc1c1`。正常 JP 仪表更新等已有 UI 回归仍通过。

新增实际播放器 UnityTest **1/1 Passed**，失败／跳过／不确定 0，原生 RunFinished `2026-10-12T15:13:06.6938341Z`，作业 `b174087d9a224d48ac27363ed34d85f7`。用 Unity 原生停用与销毁回调，并在退出播放模式时保留一个曾激活、现在停用、仍持有已销毁 HUD 的管理器；三层引擎均停止／clip 清空／音量归零，归属与移动进度清空，玩家位置和挡位不变。未让真实比赛 Start 运行，也未写玩家存档。见 [结构化结果](hud-audio-teardown-2026-10-12.json)。

MCP 的 stale-job 警告来自 TestJobManager 恢复过期的 SessionState 作业记录，不是赛车异常。原作业 `26015c97f87e4c4eac2a642abfea116a` 的 [2872/2872 原生结果](team-vehicle-full-results-2026-10-12.json) 已保存。此次播放器域重载也使 MCP 进度留在 0/2879，只有原生完成、armed=false、MainMenu 干净／idle 后才清理遗留工具作业；2879 是发现数，未重跑全量。插件代码与 PackageCache 未修改。

最终 316 份 C# 文件在播放器验证期间哈希不变，原字体哈希保留、基线路径未丢失、场景／Prefab 无落盘编辑。MainMenu isDirty=false、sceneCount=1，未播放／编译／导入；Console 无新的 HUD 异常或编译错误，仅工具重连／作业清理提示。差异检查通过，未暂存／提交／推送。
