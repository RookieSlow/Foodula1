# 第三包：温度计与六队挡位

证据日期目录沿用 2026-10-14；2026-10-15 恢复收尾。第三包 UI 与用户另行授权的 CC0 音频分发整改已完成本轮验证，整项目 UI 升级仍有后续页面。

范围仅 `HeatThermometerUI`、`TeamGearGraphic`、`TeamGearPresentationUI` 的科技风视觉。保留实际/预计热量、CN Go/Recover、六队机构轮廓与原点击/拖动/确认/禁用逻辑。车身徽章、卡牌、弹窗和其他屏幕不属于本包。

## 基线与恢复审计

- `baseline.json` 保存 HEAD、全部已有 dirty/untracked 593 个文件哈希和状态；`baseline-diff.txt` 保存原差异。
- `source-baseline.json` 与五份 `*-source-before.cs.txt` 保存相关原源码，含本包不修改的 GearDial/Badge。
- `resume-audit.json` 记录恢复时逐项对照；593 个基线文件无缺失。原中文字体缓存变化保留本地，不由本任务恢复或提交。
- `baseline-tests.json`：根任务报告的 MCP completed 定向结果 83/83，失败/跳过 0，2.8487942 秒。此轮未 arm 原生 RunFinished，不能冒称原生最终测试。

## 真实夹具与截图

`before/manifest.json` 与 54 张 PNG：生产 `RaceCanvas.prefab` 的隔离 EditMode 预览，六队 × 1920×1080 / 2560×1440 / 1280×720 × 三状态。每张保留完整 Canvas 上下文。不是实时赛事、场景播放或玩法结算证明。

| 状态 | 数据 |
|---|---|
| selected | 实际热量 5/12（42%），独立预计 9/12（75%）；实际/待选挡位分开，CN Recover→Go，其他 G2→G3 |
| confirmed | 实际 9/12（75%），当前挡位已锁定，无预计列 |
| disabled-zero-capacity | 输入关闭，实际 0/0；使用既有零热池保护，未构造零挡位规则 |

夹具克隆静态字体/材质，不修改源字体、主场景或管理器。手动执行 HUD 初始化、既有布局/显示接口与原生 ScrollRect LateUpdate；finally 恢复 RenderTexture.active、解绑本夹具 TMP 事件并清理预览资源。完整生命周期说明与逐标签几何记录见 manifest。改前 manifest 54 张、0 文字溢出；几何统计不替代实际图片审查。

## 改后审查与定向结果

`after/manifest.json` 独立核对54条/54张PNG，文字溢出0。`visual-review.json` 记录实际查看34张：六队三尺寸全部18张待选、六队720p已锁/禁用12张、大尺寸状态代表4张。与改前代表对照，白字深底、简化木纹/金属纹与六队原机构明确；未见新增裁切遮挡。实际42%/预计75%、锁定75%、零容量0%与夹具数据相符。1440p图片在读取工具中缩放到2048×1152显示，原文件仍2560×1440。

`contrast-review.json` 仅计算不透明常态sRGB文字对比：白字对实际底8.614:1、待选底7.654:1、Raised底13.707:1；不替代字号、色觉或交互验收。

`focused-102-native.json` 原生 RunFinished：102/102通过，失败/跳过/不确定0。首次 `focused-attempt-1-native.json` 为95/102通过、7失败，证据保留，不声称首次通过。

`full-ui-2931-native.json` 原生 RunFinished：2931/2931通过，失败/跳过/不确定0，结束时间2026-10-14T16:32:58Z。该结果对应 UI 包收尾；之后新接入的 CC0 换挡资源须另跑音频定向和最终全量。

## 另行授权的音频分发整改

用户授权以 CC0 原始录音替代12个含Sonniss触点的换挡成品。根任务使用新资源名接入，旧12个WAV与meta保留本地；不删除或覆盖已有文件。`cc0-audio-focused-41-native.json` 原生41/41通过，失败/跳过/不确定0；`full-final-2932-native.json` 为整改后最终原生2932/2932通过，失败/跳过/不确定0，结束2026-10-14T16:40:05.768Z。新增1项原生用例逐一验证12份PCM加载及播放，不替代完整赛事主观听验。来源、CC0原文归档与输入/输出哈希见 `docs/audio/team-shift-cc0-provenance.md`、`docs/audio/licenses/README.md` 和 `cc0-integration-audit.json`。

`commit-exclusions.json` 记录提交排除文件及哈希：旧12成品+12meta、3个独立试听WAV、12个CC0 staging重复WAV、用户字体缓存和EditorSettings。记录时均未stage。只有已准备索引，不能描述成已commit/push；根任务最终核验并记录真实Git结果。

## 最终状态与边界

`final-unity-state.json`：MainMenu 干净，未播放/编译/导入；根任务另外读取 resource idle、ready=true。Console 只有 `no_such_track` 缺失赛道测试预期 error，无 warning；不写作无日志。`final-hash-manifest.json` 保存原/现源码、108截图、测试和音频审计等证据的SHA256；HEAD为提交前快照，实际提交/推送由根任务另记。

1280×720 原有紧凑文字仍须明确报告可读性边界；受控截图不代表完整自由赛/教程/生涯已视觉验收。没有本包新增第三方下载，参考与许可记录见 `docs/ui/racing-tech-resource-licenses.md`。
