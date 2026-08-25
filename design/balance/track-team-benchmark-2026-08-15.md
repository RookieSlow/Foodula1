# Track × Team Balance Benchmark

> Deterministic pure-layer benchmark generated 2026-08-25 14:29:04.
> 12 six-team races per track, seeds `20260815..`, max 700 turns.
> All six teams run the same policy; ranks are compared within each track. Results are directional tuning evidence, not player skill data.
> Each turn freezes every non-slipstream plan, resolves up to two slipstream segments, then executes movement in rank order, matching the runtime phase boundary.
> AI tuning matches the active config baseline: lookahead 6, heat thresholds 0.7/0.5/0.3, China affordable corner heat 1.

## Runtime team profile used

| Team | Handling | Cooling | Durability | Straight base | Straight turn | Slipstream |
|---|---:|---:|---:|---:|---:|---:|
| UK | 1 | 1 | 7 | 0 | 0 | 0 |
| DE | 1 | 1 | 8 | 1 | 0 | 0 |
| IT | 2 | 0 | 6 | 0 | 0 | 0 |
| US | -1 | 0 | 8 | 0 | 1 | 1 |
| CN | 0 | 0 | 7 | 3 | 0 | 0 |
| JP | 1 | 0 | 6 | 0 | 0 | 0 |

## MVP 回退赛道 (42) (`fallback_42`)

Cells: 42; laps: 3; corner cells: 14; apexes: 5.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 5.08 | 0% | 100% | 0% | 24.4 | 0.00 | 4.1 | 10.4 | 20.8 |
| DE | 1.67 | 58% | 100% | 0% | 17.8 | 0.00 | 3.8 | 13.1 | 26.2 |
| IT | 2.58 | 17% | 100% | 0% | 18.7 | 0.17 | 2.9 | 11.1 | 22.2 |
| US | 2.42 | 25% | 100% | 0% | 18.8 | 0.08 | 5.7 | 10.7 | 32.0 |
| CN | 4.42 | 0% | 100% | 0% | 22.9 | 0.75 | 15.5 | 7.8 | 15.5 |
| JP | 4.83 | 0% | 100% | 0% | 24.4 | 0.00 | 5.2 | 10.5 | 21.0 |

China cadence per race: Go `10.4`, Recover `11.8`, corner-forced Recover `6.9`; raw-card movement `97.7`, non-slipstream movement `116.0`.

## 印第安纳波利斯汉堡赛道 (`indianapolis_burger`)

Cells: 42; laps: 3; corner cells: 8; apexes: 4.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.75 | 0% | 100% | 0% | 20.0 | 0.17 | 6.5 | 10.5 | 20.7 |
| DE | 2.58 | 25% | 100% | 0% | 16.8 | 0.00 | 6.3 | 9.4 | 18.8 |
| IT | 2.42 | 17% | 100% | 0% | 17.1 | 0.25 | 5.4 | 9.6 | 18.8 |
| US | 3.08 | 33% | 100% | 0% | 18.0 | 0.25 | 6.5 | 7.3 | 21.7 |
| CN | 3.75 | 17% | 100% | 0% | 18.5 | 0.25 | 16.0 | 8.5 | 16.9 |
| JP | 4.42 | 8% | 100% | 0% | 19.6 | 0.33 | 4.3 | 10.2 | 20.3 |

China cadence per race: Go `10.3`, Recover `8.0`, corner-forced Recover `3.3`; raw-card movement `92.8`, non-slipstream movement `113.2`.

## 勒芒旧慕尚赛道 (`le_mans_old_mulsanne`)

Cells: 142; laps: 2; corner cells: 23; apexes: 8.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 5.00 | 0% | 100% | 0% | 42.8 | 0.50 | 14.1 | 15.3 | 15.3 |
| DE | 1.50 | 58% | 100% | 0% | 35.0 | 0.00 | 8.8 | 15.2 | 15.2 |
| IT | 3.33 | 0% | 100% | 0% | 37.6 | 0.42 | 11.1 | 16.9 | 16.9 |
| US | 2.50 | 25% | 92% | 8% | 35.9 | 0.42 | 10.6 | 13.0 | 26.0 |
| CN | 3.50 | 17% | 100% | 0% | 38.1 | 0.42 | 19.6 | 10.3 | 10.3 |
| JP | 5.17 | 0% | 100% | 0% | 43.5 | 0.17 | 11.0 | 15.0 | 15.0 |

China cadence per race: Go `22.3`, Recover `15.2`, corner-forced Recover `4.6`; raw-card movement `208.5`, non-slipstream movement `282.2`.

## 蒙扎意面赛道 (`monza_pasta`)

Cells: 50; laps: 3; corner cells: 15; apexes: 7.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.50 | 0% | 100% | 0% | 29.0 | 0.17 | 9.9 | 11.3 | 21.4 |
| DE | 2.50 | 33% | 100% | 0% | 23.2 | 0.17 | 10.8 | 12.8 | 23.3 |
| IT | 2.25 | 25% | 100% | 0% | 23.2 | 0.50 | 8.3 | 10.7 | 19.2 |
| US | 2.25 | 33% | 100% | 0% | 23.1 | 0.25 | 10.6 | 9.3 | 26.2 |
| CN | 4.75 | 8% | 100% | 0% | 29.3 | 0.75 | 27.3 | 5.0 | 8.7 |
| JP | 4.75 | 0% | 100% | 0% | 29.8 | 0.50 | 12.6 | 11.1 | 21.2 |

China cadence per race: Go `13.7`, Recover `14.6`, corner-forced Recover `6.2`; raw-card movement `126.8`, non-slipstream movement `154.2`.

## 纽博格林北环 (`nurburgring_24h_endurance`)

Cells: 219; laps: 1; corner cells: 143; apexes: 18.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.75 | 0% | 100% | 0% | 43.3 | 0.00 | 2.4 | 25.2 | 25.2 |
| DE | 2.17 | 33% | 100% | 0% | 35.1 | 0.00 | 2.2 | 25.8 | 25.8 |
| IT | 2.25 | 8% | 100% | 0% | 35.3 | 0.00 | 1.4 | 26.0 | 26.0 |
| US | 1.58 | 58% | 100% | 0% | 35.2 | 0.00 | 2.4 | 17.9 | 35.8 |
| CN | 5.25 | 0% | 100% | 0% | 46.2 | 0.17 | 6.0 | 12.8 | 12.8 |
| JP | 5.00 | 0% | 100% | 0% | 44.8 | 0.00 | 2.1 | 21.5 | 21.5 |

China cadence per race: Go `15.2`, Recover `30.8`, corner-forced Recover `28.6`; raw-card movement `170.4`, non-slipstream movement `211.8`.

## 纽博格林啤酒赛道 (`nurburgring_bier`)

Cells: 55; laps: 3; corner cells: 19; apexes: 11.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.67 | 0% | 100% | 0% | 41.6 | 0.00 | 4.7 | 18.1 | 20.3 |
| DE | 2.17 | 17% | 100% | 0% | 32.2 | 0.00 | 6.3 | 21.8 | 22.8 |
| IT | 1.42 | 67% | 100% | 0% | 31.5 | 0.08 | 5.5 | 18.5 | 20.6 |
| US | 2.92 | 17% | 100% | 0% | 34.5 | 0.17 | 11.6 | 14.8 | 30.9 |
| CN | 4.83 | 0% | 100% | 0% | 43.7 | 0.75 | 11.6 | 14.5 | 18.2 |
| JP | 5.00 | 0% | 100% | 0% | 40.8 | 0.00 | 4.7 | 22.1 | 25.2 |

China cadence per race: Go `11.0`, Recover `31.9`, corner-forced Recover `29.8`; raw-card movement `140.1`, non-slipstream movement `156.8`.

## 上海点心赛道 (`shanghai_dim_sum`)

Cells: 62; laps: 3; corner cells: 22; apexes: 11.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.67 | 0% | 100% | 0% | 44.3 | 0.08 | 8.0 | 16.8 | 17.4 |
| DE | 2.58 | 17% | 100% | 0% | 34.7 | 0.08 | 7.2 | 18.6 | 19.7 |
| IT | 2.08 | 33% | 100% | 0% | 34.3 | 0.00 | 4.0 | 20.0 | 20.8 |
| US | 2.08 | 42% | 100% | 0% | 34.2 | 0.00 | 6.8 | 17.6 | 36.8 |
| CN | 4.58 | 0% | 100% | 0% | 42.9 | 0.67 | 15.5 | 18.6 | 19.6 |
| JP | 5.00 | 8% | 100% | 0% | 43.8 | 0.17 | 6.5 | 16.7 | 17.0 |

China cadence per race: Go `12.8`, Recover `29.4`, corner-forced Recover `24.3`; raw-card movement `150.3`, non-slipstream movement `169.3`.

## 银石下午茶赛道 (`silverstone_afternoon_tea`)

Cells: 60; laps: 3; corner cells: 20; apexes: 13.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.00 | 8% | 100% | 0% | 38.2 | 0.33 | 16.3 | 15.8 | 30.3 |
| DE | 2.42 | 42% | 92% | 8% | 31.3 | 0.75 | 15.2 | 12.0 | 23.7 |
| IT | 2.58 | 33% | 92% | 8% | 32.1 | 1.08 | 13.8 | 13.6 | 26.5 |
| US | 3.33 | 0% | 92% | 8% | 34.5 | 0.92 | 17.8 | 11.0 | 32.2 |
| CN | 4.42 | 17% | 83% | 25% | 39.8 | 1.92 | 28.3 | 6.8 | 13.5 |
| JP | 4.25 | 0% | 100% | 0% | 37.3 | 0.50 | 15.6 | 17.1 | 33.8 |

China cadence per race: Go `14.3`, Recover `23.0`, corner-forced Recover `17.2`; raw-card movement `145.3`, non-slipstream movement `156.9`.

## 铃鹿寿司赛道 (`suzuka_sushi`)

Cells: 62; laps: 3; corner cells: 26; apexes: 11.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.25 | 8% | 100% | 0% | 43.3 | 0.00 | 4.3 | 18.9 | 37.1 |
| DE | 2.17 | 25% | 100% | 0% | 33.3 | 0.08 | 3.1 | 15.1 | 29.8 |
| IT | 1.92 | 33% | 100% | 0% | 33.5 | 0.00 | 3.9 | 16.1 | 31.8 |
| US | 2.50 | 33% | 100% | 0% | 35.3 | 0.08 | 5.8 | 12.3 | 36.8 |
| CN | 5.33 | 0% | 100% | 0% | 49.2 | 0.75 | 13.1 | 8.5 | 16.8 |
| JP | 4.83 | 0% | 100% | 0% | 46.7 | 0.00 | 4.3 | 14.8 | 28.8 |

China cadence per race: Go `10.6`, Recover `37.8`, corner-forced Recover `34.5`; raw-card movement `149.9`, non-slipstream movement `177.9`.

## Controlled variant: China uses a strict zero-heat corner guard

> Baseline uses the runtime one-heat tolerance when the engine can also pay overclock and missing-card costs. The benchmark-only variant forces Recover for any projected corner heat.

| Track | Baseline rank | Baseline win | Baseline DNF | Variant rank | Variant win | Variant DNF | Variant Go / Recover |
|---|---:|---:|---:|---:|---:|---:|---:|
| MVP 回退赛道 (42) | 4.42 | 0% | 0% | 5.17 | 0% | 0% | 8.8 / 15.3 |
| 印第安纳波利斯汉堡赛道 | 3.75 | 17% | 0% | 3.42 | 17% | 0% | 10.0 / 8.1 |
| 勒芒旧慕尚赛道 | 3.50 | 17% | 0% | 3.67 | 8% | 0% | 21.6 / 16.9 |
| 蒙扎意面赛道 | 4.75 | 8% | 0% | 3.92 | 8% | 8% | 12.8 / 14.3 |
| 纽博格林北环 | 5.25 | 0% | 0% | 5.83 | 0% | 0% | 11.8 / 39.6 |
| 纽博格林啤酒赛道 | 4.83 | 0% | 0% | 5.17 | 0% | 0% | 8.1 / 36.8 |
| 上海点心赛道 | 4.58 | 0% | 0% | 5.58 | 0% | 0% | 10.1 / 37.8 |
| 银石下午茶赛道 | 4.42 | 17% | 25% | 4.75 | 17% | 33% | 12.5 / 21.5 |
| 铃鹿寿司赛道 | 5.33 | 0% | 0% | 5.58 | 0% | 0% | 8.9 / 42.3 |

## Tuning decisions

- China keeps the design-sheet +1 top-speed/+2 acceleration package during Go, but its handling was tuned from -1 to 0 after the safe-card benchmark showed repeated apex losses. Recover still uses the standalone cooling chain, and the AI now follows the documented Go → Go → Recover rhythm.
- Benchmark heat payments now create real hand-zone heat cards just like runtime payments, so Recover and standard cooling can replenish the engine. The former 92% China DNF rate at Le Mans was a benchmark artifact; the corrected run finishes 100% of entries.
- The six team insertion orders rotate evenly across the 12 races on every track, neutralizing the first-turn tie-order bias from the runtime's shared start cell.
- Standard AI now chooses low cards whenever the projected movement reaches a corner, and the risk window is bounded by that movement instead of a fixed 18-cell scan. This removes avoidable DNF noise from the team comparison.
- America’s straight-roar bonus is capped at one flat +1 per straight turn rather than multiplying by every card; this preserves its straight-line identity while reducing high-gear runaway results.
- Italy's acceleration is resolved only as the documented one-shot bonus on the turn after a successful corner. Removing its undocumented permanent straight +1 reduced its nine-track average win rate from 74% to about 26% in the final matrix.
- China accepts at most one affordable projected corner heat; larger risks or combined costs beyond the engine reserve force Recover. The report retains the former strict zero-heat policy as a controlled comparison.
- UK, Japan and the remaining teams were not broadly buffed from this pass: their full identity depends on interactive tech/trick/driver effects that a pure vehicle benchmark deliberately does not auto-play.
- The report is retained as a regression baseline; rerun the menu item after any TeamVehicleRules, gear, corner or card-balance change.
