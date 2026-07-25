# 赛道系统 — Demo 制作指南

> **状态**：数据层 + 加载器已完成，等待 Unity 场景集成  
> **最后更新**：2026-07-25

---

## 快速开始（在 Unity 中切换赛道）

1. 在 Project 窗口找到 `GameConfigSO`（`Assets/ScriptableObjects/` 或创建路径）
2. 在 Inspector 中设置 **Track Id** 为以下任一值：

| Track Id | 赛道名 | 格数 | 圈数 | 特点 |
|----------|--------|------|------|------|
| `suzuka_sushi` | 铃鹿寿司 | 62 | 3 | 8字形，弯道占比最高 |
| `silverstone_afternoon_tea` | 银石下午茶 | 60 | 3 | 高速赛道 |
| `shanghai_dim_sum` | 上海点心 | 62 | 3 | 唯一有维修区 |
| `monza_pasta` | 蒙扎意面 | 50 | 3 | 速度殿堂，直道最多 |
| `nurburgring_bier` | 纽博格林啤酒 | 55 | 3 | 综合型 |
| `indianapolis_burger` | 印第安纳波利斯汉堡 | 42 | 3 | 纯椭圆，仅Lv1弯 |
| `le_mans_old_mulsanne` | 勒芒旧慕尚 | 142 | 2 | Bonus，天气待配 |
| `nurburgring_24h_endurance` | 纽北24H | 267 | 1 | Bonus，天气待配 |

3. **Track Id 留空** → 回退到硬编码 42 节点测试赛道（MVP 兼容）

4. **Track World Size** 控制世界空间缩放，默认 30（归一化坐标 ×30 = ±15 范围）

---

## 加载流程

```
TrackManager.Awake()
  ├─ config.trackId != ""  →  TrackDataLoader.LoadConfig(trackId)
  │   ├─ Resources.Load<TextAsset>("Configs/Tracks/{trackId}")
  │   ├─ JsonUtility.FromJson<TrackConfig>(json)
  │   ├─ TrackDataLoader.ConfigToNodes(config) → List<TrackNode>
  │   ├─ TrackDataLoader.ConfigToWorldPositions(config, worldSize) → Vector2[]
  │   └─ config.totalLaps / trackNodeCount 自动覆盖
  │
  └─ config.trackId == ""  →  BuildHardcodedTrack()  (42节点MVP赛道)
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
}
```

### CellData（JSON 映射，`TrackConfig.cs`）

| JSON 字段 | 类型 | 说明 |
|-----------|------|------|
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

- [ ] **TrackManager** — `GameConfigSO` 引用已配置，`trackId` 非空则加载 JSON
- [ ] **赛车移动** — `nodes.Count` 取代硬编码 42，支持任意长度赛道
- [ ] **弯道判定** — `GetUniqueCornersCrossed(from, to)` 仅触发 apex 节点
- [ ] **圈数计数** — `CrossesStartFinish()` 按 `isStartFinish` 判定
- [ ] **维修区** — 上海赛道 cell[36]=pit_entry, cell[37]=pit_exit
- [ ] **UI** — `GetCornerName()` / `GetCornerSpeedLimit()` 显示弯道信息
- [ ] **LineRenderer** — 自动从 `ConfigToWorldPositions()` 读取坐标
- [ ] **天气** — 从 `config.weatherPool` 随机抽取，影响全局规则
- [ ] **Resources 文件夹** — `Assets/Resources/Configs/Tracks/*.json` 已存在

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
- **双顶点弯**（如 Spoon）：允许 2 个 `isApex: true`，这是正确的——每个顶点独立判定
- **Le Mans / Nürburgring 24H**：`weatherConfigurationStatus: "pending_design"`，天气池为空，暂不可用
