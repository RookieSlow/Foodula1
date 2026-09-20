# Quick Design Spec: 车队专项教程中心

**Type**: Addition
**System**: Tutorial, Settings & Encyclopedia
**GDD Reference**: `design/gdd/foodula-1-tutorial-settings-encyclopedia.md`
**Date**: 2026-09-21

## Change Summary

主菜单“新手教程”不再直接进入比赛，而是打开统一的二级教程中心。中心保留原有 UK/勒芒
基础教程，并增加六支车队的专项课程目录；每门课程只教学当前运行时已经生效的车队属性、
两张特技牌和必要的节奏判断。

## Motivation

基础教程能够解释共通操作，但无法说明六支车队为何需要不同的出牌、热量和位置策略。
统一入口让玩家先完成基础规则，再按所选车队定向学习，也避免把车队说明分散在百科、
车手选择和科技树中。

## Design Delta

当前 GDD 只有单一 `tutorial_le_mans_uk_v1` 入口。本规格增加两级结构：

1. 一级仍是主菜单“新手教程”。
2. 二级教程中心第一项为可玩的“基础新手教程”。
3. 其后依次列出 UK、DE、IT、US、CN、JP 六门专项课程。
4. 专项课程详情固定显示定位、难度、推荐赛道、机制、玩家操作和成功信号。
5. 专项对局未接入前，详情可读但开始按钮明确显示“专项训练开发中”，不得误导为可玩。

## New Rules / Values

- 基础教程继续使用无科技、无车手增益、固定牌序的 UK/勒芒隔离会话。
- 专项教程未来同样关闭科技和车手增益，保证只观察车队固有规则与车队特技牌。
- 推荐实装顺序：DE → IT → US → UK → JP → CN。
- 每门专项教程由 3–4 个短课组成，单课只验证一个可观察结论。
- 禁止教学未接入机制：CN 电池衰减、JP 随机秘方牌池等仅在运行时生效后才能加入课程。

## Affected Systems

| System | Impact | Action Required |
|---|---|---|
| Main Menu | 新手教程改为打开二级菜单 | 接入 `TutorialSelectionUI` |
| Tutorial | 增加纯数据课程目录 | 后续逐队实现确定性场景 |
| Team/Trick Cards | 作为课程事实来源 | 保持与当前规则代码一致 |
| Documentation | 增加课程范围与顺序 | 更新教程 GDD、路线图与任务清单 |

## Acceptance Criteria

- [x] 二级菜单同时展示基础教程和六支车队课程。
- [x] 基础教程仍可从二级菜单正常启动。
- [x] 六队课程都有唯一 ID、推荐赛道、至少三节课及明确验收信号。
- [x] 课程目录不宣称 CN 电池衰减或 JP 随机秘方牌池已经生效。
- [ ] 六支车队专项对局按推荐顺序逐一接入固定牌序、检查点和回归。
- [ ] 完成 16:9 Play Mode 菜单布局和返回流程验收。
- [ ] No regression: 快速比赛、生涯、设置、百科和原基础教程启动隔离不变。

## GDD Update Required?

Yes — 在 `foodula-1-tutorial-settings-encyclopedia.md` 增加教程中心、六队课程范围和分阶段接入说明。
