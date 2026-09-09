# Balance Check: 正式赛道大直道容量

> 已由 `balance-check-track-reality-2026-09-01.md` 的全图现实对照校准取代；本文件保留第一轮审计记录。

## Data Sources Analyzed

- `Assets/Resources/Configs/Tracks/*.json`
- `Assets/Scripts/Core/TrackSelectionState.cs`
- `Assets/Scripts/Gameplay/TrackDataLoader.cs`
- `design/gdd/foodula-1-tracks.md`

## Health Summary: HEALTHY AFTER FIX

以一轮 `4+3+3+2 = 12` 格作为完整大冲刺基准。审计八张正式地图的环形连续非弯道格后，
银石、蒙扎和纽博格林 GP 未达标；其余五张地图已经满足要求。

## Outliers Detected And Corrected

| Track | Before | After | Cell Count Change | Result |
|---|---:|---:|---:|---|
| 银石 | 8 | 12 | 60 → 64 | 机库直道可完成完整大冲刺 |
| 蒙扎 | 11 | 12 | 50 → 51 | 主直道可完成完整大冲刺 |
| 纽博格林 GP | 11 | 12 | 55 → 56 | 终点直道可完成完整大冲刺 |
| 印第安纳波利斯 | 14 | 14 | 不变 | 已达标 |
| 上海 | 15 | 15 | 不变 | 已达标 |
| 铃鹿 | 15 | 15 | 不变 | 已达标 |
| 纽博格林北环 | 63 | 63 | 不变 | 已达标 |
| 勒芒旧慕尚 | 61 | 44（当前 JSON；另有 37 / 21 / 16） | 高速偏弯拆分原长直道 | 已达标 |

> **2026-09-09 追踪说明**：本文件是第一轮大直道审计记录。加入第 60 格
> `mulsanne_kink` 后，`61` 不再是当前运行时直道长度；当前数据以 JSON 的
> `44 / 37 / 21 / 16` 为准，最长 44 格仍达到 12 格冲刺门槛。

## Progression Analysis

新增格只分配到既有大直道，并重新计算每格里程，使赛道实际总里程、圈数、线路形状、弯道数量、
弯道限速和弯心保持不变。银石新增 4 格；另外两图仅补足各自缺少的 1 格。

## Verification

- 八张正式地图最长连续非弯道格均不低于 12。
- 三张调整地图的 `gameCellCount` 与实际 `cells.Length` 一致，节点索引连续。
- Unity EditMode `573/573` 通过。
- `Assembly-CSharp-Editor` 重建 0 编译错误；仅保留既有 MCP 程序集版本警告。

## Recommendation

保留“正式地图至少一条 12 格大直道”作为数据回归门槛。未来新增地图时先验证该门槛，再进行车队
胜率模拟；不要为了达标修改弯道格或实际赛道里程。
