# 赛道系统 — Demo 制作指南

> **状态**：JSON 数据层、加载器、赛道选择和 Race 场景集成已完成；天气池与视觉细节仍可扩展  
> **最后更新**：2026-08-21

---

## 快速开始（在 Unity 中切换赛道）

1. 正常流程从 MainMenu 的赛道选择面板选择赛道；选择状态由 `TrackSelectionState` 保存，并在进入 Race 时由 `TrackManager` 读取。
2. 若需要编辑器直连，可在 `GameConfigSO` 的 **Track Id** 中设置以下任一值：

| Track Id | 赛道名 | 格数 | 圈数 | 特点 |
|----------|--------|------|------|------|
| `suzuka_sushi` | 铃鹿寿司 | 62 | 3 | 8字形，弯道占比最高 |
| `silverstone_afternoon_tea` | 银石下午茶 | 60 | 3 | 高速赛道 |
| `shanghai_dim_sum` | 上海点心 | 62 | 3 | 唯一有维修区 |
| `monza_pasta` | 蒙扎意面 | 50 | 3 | 速度殿堂，直道最多 |
| `nurburgring_bier` | 纽博格林啤酒 | 55 | 3 | 综合型 |
| `indianapolis_burger` | 印第安纳波利斯汉堡 | 42 | 3 | 纯椭圆，仅Lv1弯 |
| `le_mans_old_mulsanne` | 勒芒旧慕尚 | 142 | 2 | 补充赛道，天气池待配 |
| `nurburgring_24h_endurance` | 纽博格林北环 | 219 | 1 | 补充赛道，天气池待配 |

3. MainMenu 当前提供 8 条可选 JSON 赛道；`fallback_42.json` 是隐藏的 42 格兼容资源，不在菜单中显示。正常空配置会由 `TrackSelectionState` 解析为菜单第一条赛道；只有显式兼容/加载回退路径才使用 `fallback_42`。

4. **Track World Size** 控制世界空间缩放，默认 30（归一化坐标 ×30 = ±15 范围）

---

## 加载流程

```
TrackManager.Awake()
  ├─ TrackSelectionState.ResolveTrackId(config.trackId) →  TrackDataLoader.LoadConfig(trackId)
  │   ├─ Resources.Load<TextAsset>("Configs/Tracks/{trackId}")
  │   ├─ JsonUtility.FromJson<TrackConfig>(json)
  │   ├─ TrackDataLoader.ConfigToNodes(config) → List<TrackNode>
  │   ├─ TrackDataLoader.ConfigToWorldPositions(config, worldSize) → Vector2[]
  │   └─ `TrackRuntimeContext` 优先读取 JSON 的 `laps`、`gameCellCount` 和布局字段
  │
  └─ 无有效 JSON/配置 → 代码内建 42 节点安全兼容路径
```

---

## 数据结构速查

### TrackNode（运行时，`TrackNode.cs`）

```csharp
public class TrackNode {
    int  nodeIndex;      // 0..N-1
    int  speedLimit;     // 99=直道, 4/3/2=弯道限速
    string nodeName;     // 中文名称
    int  cornerId;       // 0=直道, >0=弯道段编号（同段共享）
    bool isStartFinish;  // 起点/终点线
    bool isApex;         // 是否触发弯心判定
    bool isPitEntry;     // 是否为维修区入口
    bool isPitExit;      // 是否为维修区出口
}
```

### CellData（JSON 映射，`TrackConfig.cs`）

| JSON 字段 | 类型 | 说明 |
|-----------|------|------|
| `schemaVersion` | 1 | 当前 JSON schema 版本 |
| `gameCellCount` | int | 格子数量，必须等于 `cells.Length` |
| `laps` | int | JSON 赛道圈数；硬编码兼容路径才使用 `GameConfigSO.totalLaps` |
| `type` | `"start_finish"` / `"straight"` / `"corner"` / `"pit_entry"` / `"pit_exit"` | 格子类型 |
| `cornerLevel` | 1 / 2 / 3 | 弯道等级（仅 corner） |
| `cornerLimit` | 4 / 3 / 2 | 对应限速 |
| `isApex` | bool | 弯心判定点（每弯道段唯一） |
| `position` | `{x, y}` | 归一化坐标 0~1 |

### 弯道等级 → 限速 → 真实 F1 速度

| Level | Limit | F1 最低速 | 典型弯道 | 刹车 |
|-------|-------|-----------|----------|------|
| Lv1 | 4 | ~230+ km/h | 130R, Copse, Abbey, Curva Grande | 全油/微抬 |
| Lv2 | 3 | ~120-230 km/h | Spoon, Lesmo, Dunlop, Stowe | 轻刹/抬油 |
| Lv3 | 2 | <120 km/h | Hairpin, Chicane, Village, Degner 2 | 重刹 |

---

## 集成检查清单

Demo 制作时需要确认以下系统正确对接：

- [x] **TrackManager** — 读取 `TrackSelectionState`/`GameConfigSO` 并加载 JSON
- [x] **赛车移动** — `nodes.Count` 取代硬编码 42，支持任意长度赛道
- [x] **弯道判定** — `GetUniqueCornersCrossed(from, to)` 仅解析去重后的 apex 事件
- [x] **圈数计数** — `CrossesStartFinish()` 按 `isStartFinish` 判定
- [x] **维修区** — 当前 JSON 仅上海配置 `pit_entry`/`pit_exit`；所有符合条件的玩家/AI 均可选择标准进站，模拟前进 5 格、清空热量并停 1 回合
- [x] **UI** — 赛道名称、弯道限速和天气 HUD 已接入
- [x] **LineRenderer** — 从 `ConfigToWorldPositions()` 读取坐标
- [x] **天气** — 有天气池的赛道按 `config.weatherPool` 参与换圈规则；补充赛道天气池为空，使用默认天气并标记待设计
- [x] **Resources 文件夹** — `Assets/Resources/Configs/Tracks/*.json` 已存在

---

## 添加新赛道

1. 在 `Assets/Resources/Configs/Tracks/` 创建 `{trackId}.json`
2. 按现有 JSON 格式填充：`schemaVersion: 1`，完整的 `cells[]` 数组
3. 确保 `gameCellCount == cells.Length`
4. 弯道等级以真实 F1 过弯速度为准（见上表）
5. 每个弯道段必须有且仅有一个 `"isApex": true` 的节点
6. 更新 `design/gdd/foodula-1-tracks.md`

---

## 注意事项

- **`cornerId`（JSON 字符串）→ `cornerId`（运行时 int）**：加载器自动 hash 映射，同 `segmentId` 的格子共享同一 int ID
- **归一化坐标**：JSON 中 position 是 0~1 范围的拓扑示意，不用于物理计算
- **Corner apex data**: every `cornerId` must have exactly one `isApex: true`; a double-apex section must be split into two corner IDs to avoid repeated resolution across turns.
- **Le Mans / Nürburgring Nordschleife**：当前可加载并可从菜单选择；`weatherConfigurationStatus: "pending_design"` 表示天气池为空，动态天气配置仍待设计，不表示赛道不可用
