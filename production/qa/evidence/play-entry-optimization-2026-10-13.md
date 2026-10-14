# Play Mode 入口优化验收（2026-10-13）

## 范围与原因

用户批准实施 Play Mode 等待优化。Unity 2022.3.62f3c1；保留场景重载，只有域重载在无脚本变动的播放入口跳过，不改赛事／教程／生涯规则或持久化进度。

修改前本机 Editor.log 的正常播放记录：域重载 25.892 s；编辑器程序集设置 10.675 s（含 InitializeOnLoad 8.726 s），对象恢复／Awake 12.466 s。含重载的刷新 26.959 s，编译 2 ms；Race 场景加载 0.523 s。阶段可能互相包含，不相加，也未逐插件归因。脚本修改导致的编译／导入重载另有 40.335 s 记录，不作为正常播放基线。

## 实施

- RuntimeSessionReset 在 SubsystemRegistration 清理赛道／车手选择、自由赛名单、教程／生涯启动请求、科技和设置的内存缓存；不删除 PlayerPrefs。场景间切换不调用此入口，保持同一播放会话的启动请求。
- ModernUIStyle 的会话入口清理精灵缓存，避免保留上一播放的运行时精灵引用；音频、车辆音频配置和遥测沿用已有入口复位。
- EditorPlayBootstrapper 记录 ExitingEditMode 到 EnteredPlayMode 的事件耗时，计时跨重载存于 SessionState；不把工具轮询时间算入。订阅先移除再添加。
- ProjectSettings/EditorSettings.asset：enabled=1，options=1（DisableDomainReload），DisableSceneReload 未启用。撤回加速可关闭 Enter Play Mode Options；原设置 enabled=0/options=3。

## 真实验证

普通 MainMenu 连续两次播放：3.762 s、1.700 s（Editor.log `[PlayEntry]`）。两次音频／遥测服务各 1；教程／生涯状态 false；第二次 9 个 Image 有有效精灵。普通播放 Console 错误／警告 0。与原正常入口的域重载阶段比较已显著减少等待；不是严格同口径的整段 A/B 基准，不声称固定提升百分比。

最终定向 EditMode：161/161，失败 0、跳过 0，作业 a6a75d8af58a4ff988e7cb8515a02c66；包括新增两例重复复位／请求和活动模式／存档不变，以及自由赛、设置、教程场景／目录回归。

启用快速模式后的第二组：35/35，失败 0、跳过 0，作业 329614336d454f9ba9b9d70ebc421b38。其中两个原生 EnterPlayMode/ExitPlayMode 协程：六队两回合／冲刺恢复退赛／名单 Start 输入，以及生涯夏休和完成状态启动拒绝；其余为教程热量和维修区反馈集成。原生 RunFinished：2026-10-13T05:43:44.2180522Z，total=35/passed=35/failed=0/skipped=0/inconclusive=0。

最终两组共 196/196。首次扩展组 161 项中新增 2 项失败：测试误用六车队生涯阵容（规则最多四队），改为合法两队夹具后重跑 161/161；不是通过记录。最初 11/11 旧夹具结果不累加。2904 是发现数，不是全量通过。

结束：MainMenu isDirty=false、未播放／编译；prepared=false、EvidenceArmed=false，Console 错误 0；协程无 RaceCanvas 夹具的小地图跳过提示保留。许可前置成功，未保存／替换用户场景；active.md 缺失未创建。

## 边界

脚本修改后 Unity 仍需编译与域重载；首次播放含资源加载仍可能慢于第二次。未跑完整默认赛事、所有连续生涯、各队教程完整流程或全量测试，不称为视觉／听觉完整验收。未来新增可变静态状态须补入口复位和回归。没有提交／推送、删除缓存或改系统设置。

收尾差异：启动时 284 个已有脏路径全部保留，281 个非本次文档路径 SHA256 完全一致（包括字体、音频、实时仪表和已有重构）。本次仅在其中三份维护文档头部追加状态；其余新增差异为四个此前干净的现有脚本、EditorSettings、两个新 C# 文件及其 Unity meta、一份验收文档。HEAD 仍为 7fe271b939d2bef8a61553b0d1bf9313198cc9b2，暂存为空，git diff --check 通过。
