# Quick Design Spec: 比赛返回主菜单二次确认

**Type**: Addition
**System**: Race HUD navigation safety
**GDD Reference**: `design/gdd/foodula-1-core-mechanics.md` §1.11；`design/ux/interaction-patterns.md`
**Date**: 2026-09-09
**Status**: Implemented; runtime visual acceptance pending

## Change Summary

比赛中的“返回主菜单”不再直接切换场景。第一次点击只打开覆盖全屏的确认弹窗；玩家点击“留在比赛”关闭弹窗，点击“返回主菜单”才调用主菜单场景切换。比赛结束面板内的返回入口使用同一确认门，避免两条入口行为不一致。其他局内动作使用各自语义的确认按钮文案，避免复用“返回”造成误解。

## Motivation

返回主菜单会放弃当前比赛进度，且按钮位于比赛操作区。增加一次明确确认，降低误触造成的比赛丢失，同时不改变任何比赛规则、牌堆或存档状态。

## Design Delta

当前核心机制文档只规定比赛 HUD 提供返回主菜单入口，未规定其直接切换场景。本 spec 将其明确为：

1. 点击任一比赛返回入口时，若确认弹窗未打开，则打开确认弹窗，不调用 `SceneLoader.LoadMainMenu()`。
2. 返回弹窗显示“留在比赛”和“返回主菜单”两个动作，并用全屏可点击遮罩阻断底层比赛输入。
3. “留在比赛”关闭弹窗且不改变比赛状态；再次点击返回入口可以重新打开弹窗。
4. 只有“返回主菜单”在弹窗可见时才清除自由赛事/教程/生涯过渡请求并加载主菜单。
5. 旧版或手动编辑的 RaceCanvas 若没有确认面板，`HUDUI` 在运行时创建兼容面板，不覆盖现有布局。

## Affected Systems

| System | Impact | Action Required |
|---|---|---|
| Race HUD | 新增一个模态 UI 状态 | 更新 `HUDUI` 并补 EditMode 回归 |
| Scene navigation | 由直接跳转改为显式确认后跳转 | 保持 `SceneLoader` 作为唯一跳转入口 |
| Race rules | 无规则变化 | 无 |

## Acceptance Criteria

- [x] 第一次点击任一比赛返回按钮只打开确认弹窗，不切换场景。
- [x] 弹窗遮罩拦截底层 UI 点击；取消后比赛输入恢复。
- [x] 确认按钮只在弹窗可见时允许调用主菜单跳转。
- [x] 运行时缺少该面板的旧 RaceCanvas 会自动获得兼容 UI。
- [ ] 在 16:9 常用分辨率下完成一次 Play Mode 视觉走查，确认弹窗不被赛道/游戏结束面板遮挡。

## GDD Update Required?

Yes — interaction behavior is synchronized in `design/ux/interaction-patterns.md`; no core gameplay rule changes are required.
