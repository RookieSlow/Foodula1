# Foodular1 开发路线图

> 最后更新: 2026-08-21
> 关联: ADR-002 (当前架构)、ADR-004（TrackRuntimeContext）
> 新模块接入指引: `docs/module-integration-guide.md`

---

## 当前状态

- ✅ Demo 框架：主菜单 → 比赛 → 返回
- ✅ 核心 HEAT 循环：选档 → 选牌 → 移动 → 冷却 → 弯道判定
- ✅ 数据驱动赛道：8 条可选 JSON 赛道，另有隐藏的 `fallback_42.json` 兼容资源
- ✅ 纯函数层：RaceRules / TrackRules / AIPlanner / WeatherRules / PitLaneRules
- ✅ 确定性随机：IRandomSource 注入，种子可重现
- ✅ AI 对手：热量感知选牌 + 弯道风险判断
- ✅ UI 系统：Prefab 模式 + 硬编码回退模式双路径
- ✅ **5 核心系统接入（2026-08-03）**：多车 / 天气 / 维修区 / 特技牌 / 科技树
  全部接入 MVPGameManager 比赛循环，新增 RaceSession 纯 C# 聚合层

---

## 🔴 立即 (P0)

| # | 任务 | 类型 | 说明 |
|---|------|------|------|
| 1 | ~~删除 `Assets/Data/`~~ | 清理 | ✅ 已完成（目录 + Data.meta 已删） |
| 2 | ~~补充 `CardDeck` 单元测试~~ | 测试 | ✅ 已完成（`card_deck_test.cs`） |
| 3 | ~~补充 `TrackDataLoader` 单元测试~~ | 测试 | ✅ 已完成（`track_data_loader_test.cs`） |

---

## 🟡 短期 (P1) — Demo 完善

| # | 任务 | 类型 | 说明 |
|---|------|------|------|
| 4 | ~~从 `MVPGameManager` 抽取 `UIFactory`~~ | 重构 | ✅ 已完成：`Assets/Scripts/UI/RaceUIFactory.cs` 负责程序化 HUD/手牌构建，Manager 仅保留编排和回调接线 |
| 5 | 填充 `tr-registry.yaml` | 文档 | 从 GDD 提取技术需求 ID，建立可追溯性 |
| 6 | ~~赛道 JSON schema 校验工具~~ | 工具 | ✅ 已完成（`Assets/Scripts/Editor/TrackJsonValidator.cs`，Foodular1 > Tools 菜单） |
| 7 | ~~写 `PlayerState` 状态机测试~~ | 测试 | ✅ 已完成（`player_state_test.cs`） |
| 8 | ~~集成测试：完整比赛流程~~ | 测试 | ✅ 纯层版本完成（`race_simulation_test.cs`，3 玩家全比赛模拟）；MainMenu→Race Play Mode 冒烟已验证，完整多天气/多圈/维修区组合仍需人工走查 |
| 9 | ~~统一硬编码赛道为 JSON~~ | 重构 | ✅ 已完成（`fallback_42.json` 已导出并保留为隐藏 42 格兼容资源；正常 MainMenu 流程选择 8 条 JSON 赛道，代码内建仅作加载失败双保险） |
| 10 | ~~运行全部 EditMode 测试~~ | 验证 | ✅ 维护记录中最近一次全量为 399/399 通过；本次文档同步未重跑 Test Runner |

---

## 🟢 中期 (P2) — 功能扩展

| # | 任务 | 类型 | 说明 |
|---|------|------|------|
| 11 | ~~多车支持~~ | 功能 | ✅ 已完成：`RaceRanking` + N 玩家循环，`aiOpponentCount` 可配 |
| 12 | ~~天气系统~~ | 功能 | ✅ 已完成：开局抽天气 + 每圈换天；Sunny/Cloudy/LightRain/HeavyRain/Hot 五种画像统一接入纯规则层 |
| 13 | ~~维修区进站~~ | 功能 | ✅ 已完成：pit_entry 弹窗选择，冷却全部热量 + 停 1 回合 |
| 14 | ~~车队特技~~ | 功能 | ✅ 已完成：12 张特技牌接入比赛（`docs/module-integration-guide.md` §6.4） |
| 15 | ~~尾流系统~~ | 功能 | ✅ 已完成（`ComputeSlipstreamBonus`；帕尔玛/冰糕/筋斗云/范围科技全接入） |
| 16 | ~~科技树 UI~~ | 功能 | ✅ 主菜单入口、RP/解锁/激活持久化与比赛接线已完成；后续为视觉和数值平衡 |
| 17 | ~~赛车随赛道方向旋转~~ | 视觉 | ✅ 已完成（`carSpriteFacingAngle`/`carRotateSpeed` 配置，出生朝向 + 移动平滑旋转 + 传送后朝向） |
| 18 | ~~赛道背景图~~ | 视觉 | ✅ 8 条可选赛道的背景与运行时节点/弯道蒙版已集成；后续为表现细节打磨 |
| 19 | ~~独特科技补充~~ | 功能 | ✅ 已完成（MotherRoad / SchwarzbierFuel / FullEnglish / SunNeverSets / Broth / DriveThru / SmokedBBQ，见接入文档 §8） |

---

## 🔵 后期 (P3) — 打磨

| # | 任务 | 类型 | 说明 |
|---|------|------|------|
| 20 | 音效系统 | 音频 | 引擎声、弯道尖叫声、观众欢呼 |
| 21 | 粒子特效 | 视觉 | 轮胎烟尘、引擎火花、雨滴 |
| 22 | 存档系统 | 功能 | 比赛进度存档 / 读取 |
| 23 | 难度选择 | 功能 | AI 强度调节、赛道复杂度选择 |
| 24 | Steam Deck / 手柄支持 | 平台 | 输入适配 |

---

## 📋 技术债务

| # | 债务 | 严重度 | 说明 |
|---|------|--------|------|
| D1 | `TrackManager` 混合渲染 + 加载适配 | 中 | ✅ 运行时节点/坐标/天气/圈数已隔离到 `TrackRuntimeContext`；AI、镜头、HUD、车辆路径、移动计划/弯道结算、比赛编排和调试覆盖层查询已直接消费快照；Manager 仍保留加载、表现配置与兼容 API |
| D2 | `MVPGameManager` 双 UI 路径 | 低 | Prefab 模式 + `RaceUIFactory` 回退路径仍需双路径冒烟测试；UI 构建代码已从 Manager 移出 |
| D3 | 测试命名不统一 | 低 | `test_xxx_yyy` vs `testXxxYyy` — 统一为 `test_xxx_yyy` 格式 |
| D4 | ~~缺少 `.gitignore` 中 Unity 标准条目~~ | 低 | ✅ `Library/`, `Temp/`, `obj/`, `Logs/` 等已排除；后续只需在提交前审查新增工具文件 |

---

## 🏁 完成标准

每项任务完成前需满足：

- **代码类**：通过所有现有测试 + 新增对应测试
- **重构类**：Play Mode 冒烟测试无回归
- **功能类**：GDD 验收标准全部达成
- **文档类**：关联的 ADR / GDD 链接已更新
