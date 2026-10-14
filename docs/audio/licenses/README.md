# Sonniss GDC 包内许可原件归档

归档日期：2026-10-15（北京时间）。只复制以下两个原件，未移动或修改下载目录。

源目录：`C:/Users/Admin/Downloads/Sonniss.com-GDC2026-GameAudioBundle4of5/`。Readme 自称 Part 9，和项目 SFX manifest 的包名/机械触点来源一致。

| 原件/本目录副本 | 字节 | SHA256（源与副本一致） |
|---|---:|---|
| License - GDC Game Audio.pdf | 60375 | 8A26E0113E8B00A4DC953A2D568D326BC780EBD792165F9BB604CB26010DEE27 |
| Readme.txt | 3291 | 8B44CB570C662883C81232D9478F96499EDDD2D7F3DE612B190BC8E59A997E1B |

PDF 为 4 页，已实际提取并阅读全部条款。其许可包含商业/个人项目使用、修改、同步到游戏，禁止冒称原录音作者、原样出售与 AI 训练；包内副本没有授予独立音效库再发行。Readme 是说明，不替代完整许可。

## 公开源码仓库边界：尚未取得明确许可

2026-10-15 实际打开 [Sonniss 当前许可](https://sonniss.com/gdc-bundle-license/) 与 [官方历史版本](https://sonniss.com/gdc-bundle-license/previous-versions/)。官网当前为 2026-08-27 生效的 2.0：允许成品游戏及内部项目团队共享，限制把原始或重新设计的声音当作音效文件供应给公众。官网明确实际下载日网页版本决定适用条款，旧包内 PDF 不能自动把旧条款延续到新下载。

本地文件 CreationTimeUtc 为 2026-09-03T07:38:10Z 左右，仅证明本地创建时间，不能独立证明最初下载日期。尚无更早下载凭据或公开源码仓库内裸 WAV 分发的专项书面许可。因此本归档支持项目来源与游戏内使用核对，**不作为公开 GitHub 裸 WAV 再分发已获准的结论**。

历史十二份 `team_shift_{uk/de/it/us/cn/jp}_{up/down}.wav` 含既有 `gear_shift.wav` 的 Sonniss 派生机械层，仍是受许可素材；本段**不涵盖新文件 `team_shift_cc0_*`**。删掉独立试听文件不能自动解除旧十二份裸 WAV 的公开分发限制。

用户于 2026-10-15 明确选择 CC0 替换方案。新十二份 `team_shift_cc0_*` 已由实现工作包接入，仅由已核验 CC0 的引擎/电机声层派生，不含旧 Sonniss 接触层。历史十二份 WAV 及其十二份 meta 保留本地，不纳入本次公开提交；旧试听/比较音同样保留本地排除。新构建、配置与哈希以 [CC0 换挡来源记录](../team-shift-cc0-provenance.md) 为权威，不由本档案代替其构建/测试证据。

## 新 CC0 来源独立核验（2026-10-15）

本次实际打开两个发布者的素材原页及 Creative Commons 许可页面：

- [muted.io Performance Cars](https://muted.io/performance-cars/)：原页 pack info 明示 CC0，68 份、96kHz/24bit/立体声录音。此许可用于原始录音，不从低质量 MP3 试听或封面照片反推授权；本轮复用已核验本地声层，没有重新下载。
- [BigSoundBank s1263](https://bigsoundbank.com/car-antenna-exit-s1263.html)：原页 License 明示 CC0，作者 Joseph Sardin；FAQ 明确允许处理、修改和再分发，包括商业游戏/应用，署名非强制。其录音为伸缩天线电机，不作为品牌赛车引擎真实性证明。
- [CC0 1.0 说明](https://creativecommons.org/publicdomain/zero/1.0/)及其 [法律文本](https://creativecommons.org/publicdomain/zero/1.0/legalcode)：允许复制、修改及商业再分发。因此这些已声明 CC0 的来源可随游戏及公开源码仓库分发其派生音频；不授予商标权，也不表示发布者背书 Foodula1。

上述结论仅用于 CC0 来源的新声层与派生短音，不解除历史 Sonniss 素材限制。保留作者、原页和原始/派生哈希以供核查；完整可复现处理见新权威记录。

## 官方 CC0 法律原文离线副本

2026-10-15（北京时间）从 `https://creativecommons.org/publicdomain/zero/1.0/legalcode.en` 直接下载官方响应，原样保存为 [CC0-1.0-original.html](CC0-1.0-original.html)，未执行网页内容，也未以模型摘要替代原文。文件 32,223 字节，SHA256：`B43732F4C1463B087CF10D4DAF8D8766514B2BF8F21BC5EF50905F95BE227726`。已核验标题与 Waiver / Public License Fallback 正文存在；HTML 包含官网外观标记，联网样式不是阅读法律正文的前提。

此 CC0 原文用于两个已明确声明 CC0 的录音来源及其十八份 `team_engine_*` 声层、十二份新 `team_shift_cc0_*` 派生短音的许可归档，具体输入/成品哈希仍以 [CC0 构建来源](../team-shift-cc0-provenance.md) 为准。附录法律文本不对历史 Sonniss 十二份换挡音或其他未声明 CC0 的资源重新授权；现有 Sonniss PDF 原件与副本保持不变。
