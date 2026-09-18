<p align="center">
  <img src="Assets/Resources/Brand/foodula1_logo.png" width="520" alt="Foodula1 Logo">
</p>

<h1 align="center">Foodula1</h1>

<p align="center">
  一款把卡牌管理、热量控制和赛车策略结合起来的 2D 美食赛车游戏。
</p>

<p align="center">
  <a href="https://github.com/RookieSlow/Foodula1/releases/tag/demo-v0.1.1"><img src="https://img.shields.io/badge/Demo-v0.1.1-orange" alt="Demo v0.1.1"></a>
  <img src="https://img.shields.io/badge/Unity-2022.3.62f3c1-black?logo=unity" alt="Unity 2022.3.62f3c1">
  <img src="https://img.shields.io/badge/Platform-Windows%2064--bit-0078D6?logo=windows" alt="Windows 64-bit">
  <img src="https://img.shields.io/badge/Language-简体中文-red" alt="简体中文">
</p>

> 当前版本是面向玩家测试的 Demo，并非最终成品。欢迎试玩并反馈规则、平衡、界面、性能和教程问题。

## 下载试玩

**[下载 Foodula1 Demo v0.1.1（Windows 64 位，73.20 MiB）](https://github.com/RookieSlow/Foodula1/releases/download/demo-v0.1.1/Foodula1-Demo-v0.1.1-Windows.zip)**

1. 下载并完整解压 ZIP。
2. 运行 `Foodula1.exe`，无需安装。
3. 推荐使用 1920×1080 或更高的 16:9 分辨率。

SHA-256：

```text
59BB7C732A0EE2F70D8AA7522CDB437E0FEB4CF7E1E1984B98EC4338C0A42BD7
```

当前构建没有进行 Windows 代码签名，系统可能显示“未知发布者”提示。请只从本仓库的
[正式 Release](https://github.com/RookieSlow/Foodula1/releases/tag/demo-v0.1.1) 下载，并在需要时核对上述哈希值。

## 游戏内容

Foodula1 以卡牌驱动车辆。玩家需要选择挡位、打出规定数量的速度牌，在前进速度、弯道限速、
引擎热量和手牌循环之间作出取舍。

当前 Demo 包含：

- 六支风格不同的美食车队、十二名车手和八条正式赛道。
- 自由赛事、自定义阵容、12 车“雷霆大混战”。
- 八站生涯赛季、车队科技树和车手主动技能。
- 固定牌序的新手教程，以及教程结束后的一圈练习赛。
- 挡位、热量、弯道、天气、尾流、维修区、失控和特技牌等完整比赛机制。
- 游戏百科、可配置二次确认、音乐/SFX 与本地试玩日志导出。

这是一个独立游戏 Demo，与任何现实赛车赛事、车队、车手或赛道运营方不存在隶属或官方合作关系。

## 基础操作

鼠标可以完成全部操作；键盘快捷键用于提高比赛操作效率。

| 操作 | 按键 |
|---|---|
| 选择挡位 | `1`–`4`（含小键盘） |
| 在可用卡牌间移动高亮 | `A` / `D` 或方向键 |
| 选择或取消当前高亮牌 | `F` |
| 打出或弃置已经选中的牌 | `Space` |
| 跳过车辆移动、尾流等当前演出 | 演出期间按 `Space` 或单击 |
| 标记试玩问题并保存截图 | `F8` |

正常出牌阶段没有选中牌时，`Space` 不会直接结束阶段；请点击界面中的明确确认按钮结束出牌或回合。
返回主菜单、重开比赛及其他关键操作的二次确认可以在设置中分别调整。

## 反馈与试玩日志

请通过 [GitHub Issues](https://github.com/RookieSlow/Foodula1/issues) 提交问题。建议包含：

- 游戏版本、比赛模式、车队/车手和赛道。
- 问题发生前后的操作步骤，以及预期结果和实际结果。
- 截图或录像；问题发生时可以先按 `F8` 添加时间标记。
- 如愿意，可在主菜单“设置”中选择“导出测试日志”，再将导出的 ZIP 附到反馈中。

试玩记录只保存在本地，**不会自动上传**。日志不会记录输入文字、剪贴板、玩家姓名、账号或设备 ID；
导出包可能包含玩家主动生成的问题截图、基础硬件信息、操作时间线和比赛日志，请在上传前自行检查。

## 当前验证状态

- Unity EditMode：`688/688` 通过，0 失败、0 跳过。
- Windows 64 位包由 Unity 2022.3.62f3c1 从 `demo-v0.1.1` 标签生成。
- Direct3D 11 / NVIDIA GeForce RTX 4070 启动冒烟 15 秒，无匹配到的异常或崩溃。
- 详细构建证据见 [V0.1.1 发布记录](production/releases/demo-v0.1.1.md)。

尚未建立正式最低配置矩阵，也未完成手柄、超宽屏和所有硬件组合测试。V0.1.2 将重点跟进百科内容一致性、
完整教程流程和剩余分辨率验收。

## 从源码运行

开发环境：

- Unity `2022.3.62f3c1`
- Windows 10/11 64 位
- Git + Git LFS

```powershell
git lfs install
git clone https://github.com/RookieSlow/Foodula1.git
cd Foodula1
git checkout demo-v0.1.1
```

使用对应版本 Unity 打开项目。主要场景为：

- `Assets/Scenes/MainMenu.unity`
- `Assets/Scenes/Race.unity`

可复现 Windows 构建入口位于 `Assets/Scripts/Editor/DemoBuild.cs`，方法为
`DemoBuild.BuildWindowsDemo`。

## 项目文档

- [核心机制 GDD](design/gdd/foodula-1-core-mechanics.md)
- [系统索引](design/gdd/systems-index.md)
- [开发路线图](design/planning/roadmap.md)
- [资源清单](design/planning/asset-manifest.md)
- [V0.1.1 发布记录](production/releases/demo-v0.1.1.md)

## 资源与许可

仓库包含项目代码、开发框架、字体、第三方 UI 资源、参考资料以及用户生成/项目生成的美术和音频。
不同资源可能适用不同许可；仓库公开不代表所有资源均可在仓库之外自由再分发或商用。

- Kenney UI 资源采用 CC0，许可文本保存在 `Assets/Resources/UiTheme/Kenney/`。
- 赛道布局参考来源与许可记录在
  [`design/references/track-layouts/ATTRIBUTION.md`](design/references/track-layouts/ATTRIBUTION.md)。
- TextMesh Pro、字体及其他第三方内容请以其随附许可与来源条款为准。

在复制或再分发源码和资源前，请分别核对根目录 `LICENSE`、各资源随附许可及资源清单中的来源记录。
