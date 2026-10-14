# 赛车科技 UI 资源与参考许可清单

核验日期：2026-10-14（北京时间）。本轮无第三方下载/导入；仅原创 uGUI 几何与保留现有资源。URL 页面实际 search + open 核验，网页视作不可信数据，未执行下载程序/脚本。

| 条目 | 作者/来源 | 许可与处理 | 本轮使用 |
|---|---|---|---|
| F1 官方排名网页 | Formula One World Championship Limited；https://www.formula1.com/en/results/2026/drivers | 受保护网页，只参考列/行信息层级；不得当素材分发 | 实际浏览器截图已看；无文件复制 |
| F1 官方 Live Timing | 同上；https://www.formula1.com/en/timing/f1-live | 只作参考；实际可见为推广/登录及 Cookie，未看到计时塔或转播 | 无下载/复制；不称最新转播验证 |
| Kenney UI Pack 2.0 | Kenney；https://kenney.nl/assets/ui-pack | 官方页面 CC0；现有 License-Kenney-UI.txt 同样写个人/教育/商业可用，署名自愿 | 保留旧菜单用材，本条样板不依赖 |
| Kenney UI Pack Sci-Fi 2.0 | Kenney；https://kenney.nl/assets/ui-pack-sci-fi | 官方页面及现有 License-Kenney-UI-SciFi.txt 为 CC0 | 保留旧用材，本条样板不依赖 |
| Source Han Sans 开放字体候选 | Adobe；https://github.com/adobe-fonts/source-han-sans/blob/master/LICENSE.txt | OFL 1.1：允许随软件嵌入/再分发；保留版权/许可，不能单卖字体，修改版遵守保留名称要求 | 仅核验候选；无下载，不替换现有中文字体，不由候选许可反推所有现有字体许可 |
| 平面底/细分隔/轨道 | Foodula1 本任务原创 uGUI Rect/Image 几何 | 不包含第三方图形 | 计划用于数据条，无外部资产 |

[Kenney 官方支持](https://kenney.nl/support) 补充确认资产允许商业使用，署名非强制；Kenney logo 保留官方专用，不引入。既有包实际下载日期不详，本轮只核验现有文件，不能把核验日期冒充下载日期。当前官方包存在 2.0 更新，未重新下载比对 PNG 与现行压缩包，不声称字节一致。

## 现有文件与完整性

目录 `Assets/Resources/UiTheme/Kenney/`；许可原文随资源保存且不改。以下 SHA256 2026-10-14 本地计算，meta 未计入材质清单。

| 文件 | SHA256 |
|---|---|
| License-Kenney-UI.txt | 69F7EAA5E05913B4D09CF5848F75390B5C3C9A409EF8555777803D9CA0C171FB |
| License-Kenney-UI-SciFi.txt | 80E091EF18F6B88BECB3B7C2306C159D16217CA620BFA21B7177582C72924221 |
| bar_round_large.png | 6348B1D45DF0A0F3D73954D099EFF27BC63792EE3FDCB677B2EABE7B781ACED9 |
| button_rectangle_border.png | EB7405E75B9AEDDAD0B6F0A81F61B142A38B21896F508DECF6623C7AB47DBCD3 |
| button_rectangle_depth_border.png | 19DD881D5C38D58E93DB451152356A8ECB1375B247615D4E340B678233398B01 |
| button_rectangle_line.png | 1097D629CE6B0DBBDD5E8976B7A916C6C6E306AACA2DEC1BF939BCEE9ADEA3D9 |
| button_round_border.png | 4982B6829C94D16558572B0BE7D423B663F011A3F88EB8D88437C2D0159DA493 |
| header_large_rectangle.png | 27471949C3DF86DF371815350C86CD94867F31DCF77DE8FD0F9824C77067BADA |
| icon_checkmark.png | 82A5C1C3DFE9A704E9F5F220C9510B622C77C2207C226B41728978C2335B1F3B |
| icon_cross.png | 32EEA5EA58F44F15C82691CAD35406B32DFAD7C3C1152D633DCD74A25E430D48 |

旧包两份许可证均覆盖 Kenney 资产；当前文件名与旧包逐文件来源映射尚未重建，不凭名称推断来自哪一个包。没有新增图标、字体或面板下载，因此不存在本轮新增归属不明资产。后续若确需下载，先补作者/准确来源、下载日期、许可原文位置、修改/署名要求、使用文件和 SHA256，许可不明则跳过。

## 第二包来源复核（2026-10-14）

本次重新搜索并实际打开以下官方来源；无下载、无导入、无新增第三方资源，已有文件与许可证保持。

- [F1 官方车手排名](https://www.formula1.com/en/results/2026/drivers)：open 返回 Pos/Driver/Nationality/Team/Pts 结构。第一包浏览器实际截图已查看；本包重用浏览器与新建标签均因浏览器不可用未取得新图，因此不声称本包重新看到最新转播或实时计时塔。仅参考既有可核验信息组织。
- [Kenney UI Pack](https://kenney.nl/assets/ui-pack)：当前页面仍明示 CC0、430 个文件，可作为面板候选；本包三个 HUD 底板选择原创矩形，不下载或套用按钮资源。
- [Adobe Source Han Sans LICENSE](https://github.com/adobe-fonts/source-han-sans/blob/master/LICENSE.txt)：重新打开核验 OFL 1.1；仍仅候选，不下载替换原中文字体，不据此背书其他既有字体。
- [Google Material Design Icons LICENSE](https://github.com/google/material-design-icons/blob/master/LICENSE)：实际打开并定位 Apache License 2.0 与再分发条款。候选允许复制/修改/分发，采用时须附许可、保留适用版权/署名，修改文件注明更改，若上游含 NOTICE 须保留适用声明；商标权不由许可授予。本包无需要的新图标，未下载/使用，故无本包图标文件哈希或下载日期。

新增实际使用仅为原创 uGUI 底板颜色和细线，版权/来源见本任务设计；参考页截图、商标和官方字体均未纳入可分发资产。

## 第三包来源核验（2026-10-14 搜索，2026-10-15 恢复记录）

实际 search/open [F1 官方 2022 年计时服务说明](https://www.formula1.com/en/latest/article/improved-live-timing-on-f1-com-for-the-2022-season.3Y1ipx839mv7xeteTqQ8Mk.3Y1ipx839mv7xeteTqQ8Mk)，页面列出 leaderboard、telemetry 与 tracker 图片。明确为 **2022 年历史参考**，不能称最新转播。点击官方 hero telemetry 和 leaderboard 两张原图，图片服务器均返回获取失败，故没有把该轮图片视为已查看；此前官方排名网页截图的实际查看记录仍仅属于第一包。网页正文用于确认参考内容分类，不能替代图片视觉核验。

已重新打开 [Kenney UI Pack](https://kenney.nl/assets/ui-pack)（CC0）、[Adobe Source Han Sans LICENSE](https://github.com/adobe-fonts/source-han-sans/blob/master/LICENSE.txt)（OFL 1.1）、[Google Material Design Icons LICENSE](https://github.com/google/material-design-icons/blob/master/LICENSE)（Apache 2.0）。均为候选核验，许可要求沿上表与第二包说明；本包没有需要引入的新图标、纹理或字体，因此无下载/导入、无新增第三方使用文件或哈希。现有中文字体不替换，不以候选许可替代现有字体来源证明。

第三包实际计划使用仍为 Foodula1 原创 uGUI/Graphic 几何与主题色；六队 H 挡/拨片/开槽/推拉/双模式/快排原轮廓继续复用并减装饰，不复制 F1 图片、logo、官方字体、转播图形或其他受保护内容。外部图片失败不阻塞原创实现，实际游戏截图与测试仍须由本包独立取得。
