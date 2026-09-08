# Foodula1 UI Modernization — Phase 1

状态：已实现第一阶段（运行时 uGUI 统一皮肤），保留后续真人验收入口。

## 目标

在不迁移 UI Toolkit、不中断现有菜单/比赛逻辑的前提下，把 Foodula1 的运行时界面统一成“赛事转播台 + 桌游卡牌”的现代视觉语言：深色信息底、清晰的青蓝主色、车队色作为语义强调，以及短促的按压反馈。

## 本阶段范围

- 主菜单：增加半透明菜单 dock，统一快速比赛、生涯、车库、科技树、教程、设置和退出按钮的层级与交互状态。
- 选择与管理页：赛道选择、车手选择、车队科技树、生涯、生涯科技调整统一 overlay、panel 描边和按钮状态。
- 设置/百科：继续使用现有可操作内容，只替换面板、滚动内容区和操作按钮的视觉 token。
- 比赛 HUD：所有由 `RaceUIFactory` 生成的档位、确认、出牌、重置和车手技能按钮使用同一套反馈与资源回退逻辑。

## 资源与回退

选用 Kenney 的 CC0 UI Pack 与 UI Pack Sci-Fi Expansion 的少量蓝色按钮、边框和条形资源，复制到 `Assets/Resources/UiTheme/Kenney/`。`ModernUIStyle` 优先读取 Sprite；若新 PNG 尚未生成 Sprite importer 元数据，则从 `Texture2D` 运行时创建 Sprite，因此首次导入不会让界面退回空白。

许可证原文随资源保存在同一目录：`License-Kenney-UI.txt` 与 `License-Kenney-UI-SciFi.txt`。资源包来源：

- https://kenney.nl/assets/ui-pack
- https://kenney.nl/assets/ui-pack-sci-fi

## 验收标准

1. 主菜单和所有运行时 overlay 的主按钮具有一致的 normal/highlighted/pressed/disabled 状态，且中文标签保持可读。
2. 设置、百科、车手、赛道、科技树和生涯页面仍由原有回调打开/关闭，皮肤更换不改变玩法状态。
3. 新增资源缺失或尚未被 Unity 识别为 Sprite 时，按钮仍显示颜色和文本，不出现空引用异常。
4. `ModernUIStyleTests` 通过，并在 Unity Editor 中完成一次菜单/overlay 的人工视觉验收；比赛 HUD 的行为回归沿用现有测试集。

## 下一阶段

先根据主菜单、设置/百科和比赛 HUD 的运行态截图进行间距、字号和对比度微调，再决定是否为牌堆、车手技能、尾流和维修区提示增加更具赛事感的图标与信息卡，不在这一阶段改动核心玩法或场景资产。
