# Balance Check: 八张正式赛道现实对照与格距

## Data Sources Analyzed

- `Assets/Resources/Configs/Tracks/*.json`
- `design/gdd/foodula-1-tracks.md`
- Formula 1 官方上海、银石、蒙扎与铃鹿赛道资料
- 铃鹿赛道官方 Course Guide（主直道约 800m，西直道约 1000m）
- Nürburgring 官方 GP-Strecke / Nordschleife 资料
- ACO / 24 Hours of Le Mans 官方旧慕尚直道历史资料
- Indianapolis Motor Speedway 椭圆布局资料

## Health Summary: HEALTHY AFTER REALITY PASS

第一轮“每图至少一条 12 格直道”只解决了冲刺容量，没有保证同图各直道的相对长度。现实对照后，
改为按主直道、中距离直道、短连接段分级；高速假弯则不再沿用统一的限速 4 上限。

## Straight Distribution

| Track | Cell Count | Major / Medium / Short Straight Pattern | Reality Check |
|---|---:|---|---|
| 银石 | 77 | 12 / 10 / 10 / 9 / 7 / 4 / 3 / 1 / 1 | 机库最长，惠灵顿与汉密尔顿次之 |
| 纽博格林 GP | 59 | 12 / 6 / 5 / 4 / 4 / 3 / 2 / 1 / 1 / 1 / 1 | 终点直道最长，技术区保持短节奏 |
| 蒙扎 | 63 | 15 / 12 / 11 / 8 / 1 / 1 | 80% 全油特征由多条长、中直道共同表达 |
| 印第安纳波利斯 | 42 | 15 / 13 / 3 / 3 | 两条长直道、两条短槽，比例不再接近 3:1 |
| 上海 | 62 | 15 / 6 / 4 / 3 / 3 / 2 / 2 / 2 / 2 / 1 | 1.2km 后直道继续保持全图核心 |
| 铃鹿 | 60 | 12 / 10 / 3 / 2 / 2 / 2 / 2 / 1 | 西直道长于主直道，匹配官方约 1000m/800m |
| 纽博格林北环 | 219 | 63 / 5 / 4 / 4 | 保留既有等弧长密度与超长 Döttinger 区段 |
| 勒芒旧慕尚 | 142 | 61 / 37 / 21 | 旧慕尚直道继续占据压倒性长度 |

## Corner Speed Reality Pass

| Class | Game Limit | Examples |
|---|---:|---|
| 全油/假弯 | 6 | 上赛出阴阳、Abbey、Copse、Curva Grande、Schumacher-S、200R、130R、Schwedenkreuz |
| 高速走线弯 | 5 | Maggotts-Chapel、Tertre Rouge、Porsche Curves、Galgenkopf |
| 常规快弯 | 4 | Ascari、Dunlop、Parabolica、上海部分高速组合 |
| 中低速/重刹 | 3 / 2 | Lesmo、Stowe、发卡、减速弯与重刹区 |

上海 `yin_exit` 作为 T3 出弯/T4 过渡的六档全油假弯由限速 3 提升至 6；它仍保留弯道地标，
但不会再像中速弯一样惩罚一次 4+3 级别的高速通过。

## Density Rules

- 直道新增或删减节点均沿原中心线按 30×16.875 世界比例等弧长采样。
- 起终点、弯心、弯道格和维修区地标坐标保持不变。
- `gameCellCount`、节点索引、每格里程与实际数组长度同步更新。
- 八张正式地图均至少有一条连续 12 格直道。

## Verification Targets

- 所有正式地图索引连续、起终点唯一、弯道和弯心数量不变。
- 银石全图最大相邻格距不超过中位格距的 1.5 倍。
- 银石、蒙扎、印第和铃鹿的长中短直道序列由回归测试锁定。
- 代表性全油/高速弯限速由语义 ID 回归测试锁定，不依赖易漂移的节点序号。
