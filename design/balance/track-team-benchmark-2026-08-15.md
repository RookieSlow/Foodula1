# Track × Team Balance Benchmark

> Deterministic pure-layer benchmark generated 2026-08-31 18:24:51.
> 12 six-team races per track, seeds `20260815..`, max 700 turns.
> All six teams run the same policy; ranks are compared within each track. Results are directional tuning evidence, not player skill data.
> Each turn executes the non-slipstream movement in rank order, then resolves up to two slipstream segments from settled positions and applies the bonus movement, matching the runtime phase boundary.
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
| UK | 4.83 | 0% | 100% | 0% | 26.3 | 0.00 | 3.3 | 6.8 | 13.7 |
| DE | 1.75 | 58% | 100% | 0% | 19.6 | 0.00 | 7.8 | 5.2 | 10.3 |
| IT | 2.67 | 17% | 100% | 0% | 20.9 | 0.08 | 3.8 | 6.4 | 12.8 |
| US | 2.58 | 17% | 100% | 0% | 21.0 | 0.08 | 4.9 | 6.8 | 20.5 |
| CN | 4.33 | 8% | 100% | 0% | 24.5 | 0.17 | 13.0 | 4.3 | 8.5 |
| JP | 4.83 | 0% | 100% | 0% | 26.8 | 0.00 | 4.0 | 6.5 | 13.0 |

China cadence per race: Go `11.3`, Recover `13.1`, corner-forced Recover `8.9`; raw-card movement `105.1`, non-slipstream movement `125.8`.

## 印第安纳波利斯汉堡赛道 (`indianapolis_burger`)

Cells: 42; laps: 3; corner cells: 8; apexes: 4.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 5.08 | 8% | 100% | 0% | 22.3 | 0.42 | 8.3 | 5.8 | 11.4 |
| DE | 2.25 | 17% | 100% | 0% | 17.1 | 0.00 | 12.2 | 5.3 | 10.3 |
| IT | 2.50 | 25% | 100% | 0% | 17.6 | 0.17 | 5.6 | 6.5 | 12.9 |
| US | 3.08 | 25% | 100% | 0% | 18.7 | 0.17 | 5.1 | 5.3 | 15.4 |
| CN | 3.17 | 17% | 100% | 0% | 18.7 | 0.17 | 12.5 | 5.2 | 10.1 |
| JP | 4.92 | 8% | 100% | 0% | 21.7 | 0.25 | 5.5 | 4.7 | 9.2 |

China cadence per race: Go `10.8`, Recover `7.8`, corner-forced Recover `3.1`; raw-card movement `97.0`, non-slipstream movement `119.0`.

## 勒芒旧慕尚赛道 (`le_mans_old_mulsanne`)

Cells: 142; laps: 2; corner cells: 23; apexes: 8.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 5.42 | 0% | 100% | 0% | 45.3 | 0.25 | 10.8 | 6.5 | 6.5 |
| DE | 1.17 | 83% | 100% | 0% | 35.4 | 0.08 | 11.1 | 8.0 | 8.0 |
| IT | 2.50 | 17% | 100% | 0% | 37.6 | 0.25 | 10.0 | 12.0 | 12.0 |
| US | 2.83 | 0% | 100% | 0% | 37.8 | 0.08 | 10.3 | 9.6 | 19.2 |
| CN | 3.67 | 0% | 100% | 0% | 39.3 | 0.17 | 19.5 | 7.8 | 7.8 |
| JP | 5.42 | 0% | 100% | 0% | 44.7 | 0.17 | 13.8 | 7.6 | 7.6 |

China cadence per race: Go `22.0`, Recover `17.0`, corner-forced Recover `6.2`; raw-card movement `208.9`, non-slipstream movement `280.9`.

## 蒙扎意面赛道 (`monza_pasta`)

Cells: 50; laps: 3; corner cells: 15; apexes: 7.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.42 | 8% | 100% | 0% | 30.5 | 0.50 | 11.8 | 9.8 | 18.3 |
| DE | 2.08 | 42% | 100% | 0% | 24.8 | 0.42 | 18.4 | 5.5 | 10.3 |
| IT | 2.58 | 25% | 100% | 0% | 25.4 | 0.50 | 9.0 | 5.5 | 10.9 |
| US | 3.25 | 0% | 100% | 0% | 27.0 | 0.67 | 18.6 | 6.3 | 18.2 |
| CN | 3.67 | 25% | 92% | 8% | 26.3 | 1.00 | 22.1 | 5.2 | 9.6 |
| JP | 5.00 | 0% | 100% | 0% | 32.7 | 0.75 | 12.3 | 6.7 | 12.3 |

China cadence per race: Go `13.2`, Recover `13.0`, corner-forced Recover `5.9`; raw-card movement `122.6`, non-slipstream movement `149.6`.

## 纽博格林北环 (`nurburgring_24h_endurance`)

Cells: 219; laps: 1; corner cells: 143; apexes: 18.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 5.17 | 0% | 100% | 0% | 46.3 | 0.00 | 2.9 | 16.3 | 16.3 |
| DE | 1.58 | 58% | 100% | 0% | 36.5 | 0.00 | 2.8 | 13.1 | 13.1 |
| IT | 2.58 | 17% | 100% | 0% | 37.7 | 0.00 | 1.6 | 12.3 | 12.3 |
| US | 1.83 | 25% | 100% | 0% | 36.9 | 0.00 | 2.5 | 14.0 | 28.0 |
| CN | 4.58 | 0% | 100% | 0% | 46.3 | 0.08 | 6.0 | 13.8 | 13.8 |
| JP | 5.25 | 0% | 100% | 0% | 46.8 | 0.00 | 2.8 | 17.3 | 17.3 |

China cadence per race: Go `14.9`, Recover `31.3`, corner-forced Recover `28.8`; raw-card movement `172.0`, non-slipstream movement `209.7`.

## 纽博格林啤酒赛道 (`nurburgring_bier`)

Cells: 55; laps: 3; corner cells: 19; apexes: 11.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.75 | 0% | 100% | 0% | 43.6 | 0.00 | 3.6 | 11.3 | 11.9 |
| DE | 1.67 | 50% | 100% | 0% | 33.2 | 0.00 | 6.5 | 9.3 | 9.6 |
| IT | 1.83 | 33% | 100% | 0% | 33.4 | 0.08 | 3.3 | 9.4 | 10.3 |
| US | 2.67 | 17% | 100% | 0% | 35.5 | 0.08 | 9.8 | 9.8 | 20.6 |
| CN | 5.25 | 0% | 100% | 0% | 47.3 | 0.67 | 8.2 | 9.5 | 10.3 |
| JP | 4.83 | 0% | 100% | 0% | 44.0 | 0.00 | 4.8 | 10.5 | 12.3 |

China cadence per race: Go `10.6`, Recover `36.1`, corner-forced Recover `34.8`; raw-card movement `146.5`, non-slipstream movement `164.5`.

## 上海点心赛道 (`shanghai_dim_sum`)

Cells: 62; laps: 3; corner cells: 22; apexes: 11.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.92 | 0% | 100% | 0% | 44.3 | 0.17 | 8.3 | 15.3 | 16.3 |
| DE | 1.42 | 75% | 100% | 0% | 34.4 | 0.08 | 12.1 | 14.4 | 14.8 |
| IT | 2.25 | 17% | 100% | 0% | 36.1 | 0.17 | 7.0 | 12.3 | 12.8 |
| US | 2.75 | 8% | 100% | 0% | 36.3 | 0.08 | 9.3 | 13.5 | 27.8 |
| CN | 5.08 | 0% | 100% | 0% | 44.9 | 0.50 | 14.2 | 13.3 | 13.7 |
| JP | 4.58 | 0% | 100% | 0% | 43.8 | 0.08 | 5.8 | 17.9 | 18.7 |

China cadence per race: Go `13.1`, Recover `31.3`, corner-forced Recover `27.4`; raw-card movement `157.1`, non-slipstream movement `176.1`.

## 银石下午茶赛道 (`silverstone_afternoon_tea`)

Cells: 60; laps: 3; corner cells: 20; apexes: 13.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 4.33 | 8% | 100% | 0% | 39.8 | 0.67 | 14.3 | 12.4 | 23.6 |
| DE | 2.33 | 25% | 100% | 0% | 33.6 | 0.75 | 21.2 | 7.0 | 13.4 |
| IT | 2.42 | 25% | 92% | 8% | 32.5 | 0.83 | 16.7 | 8.3 | 15.6 |
| US | 3.17 | 25% | 92% | 8% | 34.7 | 1.17 | 18.8 | 6.6 | 19.3 |
| CN | 4.83 | 8% | 58% | 50% | 39.4 | 2.17 | 28.2 | 2.0 | 3.8 |
| JP | 3.92 | 8% | 100% | 0% | 40.6 | 1.08 | 16.7 | 9.2 | 17.3 |

China cadence per race: Go `12.8`, Recover `21.3`, corner-forced Recover `16.2`; raw-card movement `132.8`, non-slipstream movement `144.2`.

## 铃鹿寿司赛道 (`suzuka_sushi`)

Cells: 62; laps: 3; corner cells: 26; apexes: 11.

| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| UK | 5.25 | 0% | 100% | 0% | 51.5 | 0.17 | 4.7 | 6.7 | 13.3 |
| DE | 1.67 | 50% | 100% | 0% | 36.0 | 0.00 | 8.3 | 5.3 | 10.5 |
| IT | 2.00 | 33% | 100% | 0% | 37.3 | 0.00 | 5.0 | 6.6 | 13.2 |
| US | 3.00 | 8% | 100% | 0% | 40.8 | 0.00 | 7.8 | 6.3 | 19.0 |
| CN | 4.83 | 8% | 92% | 8% | 49.5 | 0.67 | 13.3 | 6.7 | 13.3 |
| JP | 4.25 | 0% | 100% | 0% | 47.3 | 0.08 | 6.9 | 11.5 | 22.9 |

China cadence per race: Go `10.3`, Recover `40.0`, corner-forced Recover `36.5`; raw-card movement `153.2`, non-slipstream movement `179.2`.

## Controlled variant: Germany Schwarzbier Fuel cadence

> Current uses the runtime once-per-lap gate. Legacy uses the former every-turn trigger with the same heat reserve, seeds, team order, AI policy and track configuration.

| Track | Current rank | Current win | Current triggers | Current move | Legacy rank | Legacy win | Legacy triggers | Legacy move |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| MVP 回退赛道 (42) | 1.75 | 58% | 3.0 | 6.0 | 1.25 | 75% | 16.5 | 33.0 |
| 印第安纳波利斯汉堡赛道 | 2.25 | 17% | 3.0 | 6.0 | 1.25 | 75% | 14.3 | 28.5 |
| 勒芒旧慕尚赛道 | 1.17 | 83% | 2.0 | 4.0 | 1.00 | 100% | 30.4 | 60.8 |
| 蒙扎意面赛道 | 2.08 | 42% | 3.0 | 6.0 | 1.00 | 100% | 18.8 | 37.5 |
| 纽博格林北环 | 1.58 | 58% | 1.0 | 2.0 | 1.00 | 100% | 28.8 | 57.5 |
| 纽博格林啤酒赛道 | 1.67 | 50% | 3.0 | 6.0 | 1.00 | 100% | 25.0 | 50.0 |
| 上海点心赛道 | 1.42 | 75% | 3.0 | 6.0 | 1.00 | 100% | 27.8 | 55.7 |
| 银石下午茶赛道 | 2.33 | 25% | 3.0 | 6.0 | 1.08 | 92% | 24.8 | 49.7 |
| 铃鹿寿司赛道 | 1.67 | 50% | 3.0 | 6.0 | 1.00 | 100% | 27.8 | 55.7 |

## Controlled variant: China uses a strict zero-heat corner guard

> Baseline uses the runtime one-heat tolerance when the engine can also pay overclock and missing-card costs. The benchmark-only variant forces Recover for any projected corner heat.

| Track | Baseline rank | Baseline win | Baseline DNF | Variant rank | Variant win | Variant DNF | Variant Go / Recover |
|---|---:|---:|---:|---:|---:|---:|---:|
| MVP 回退赛道 (42) | 4.33 | 8% | 0% | 4.67 | 8% | 0% | 8.8 / 17.8 |
| 印第安纳波利斯汉堡赛道 | 3.17 | 17% | 0% | 3.42 | 8% | 0% | 10.6 / 8.5 |
| 勒芒旧慕尚赛道 | 3.67 | 0% | 0% | 3.67 | 0% | 0% | 21.5 / 17.8 |
| 蒙扎意面赛道 | 3.67 | 25% | 8% | 4.17 | 8% | 8% | 12.8 / 15.3 |
| 纽博格林北环 | 4.58 | 0% | 0% | 5.75 | 0% | 0% | 12.3 / 41.7 |
| 纽博格林啤酒赛道 | 5.25 | 0% | 0% | 5.42 | 0% | 0% | 8.9 / 39.6 |
| 上海点心赛道 | 5.08 | 0% | 0% | 5.33 | 8% | 0% | 10.3 / 37.1 |
| 银石下午茶赛道 | 4.83 | 8% | 50% | 5.42 | 0% | 17% | 13.6 / 29.5 |
| 铃鹿寿司赛道 | 4.83 | 8% | 8% | 5.08 | 0% | 8% | 9.2 / 41.5 |

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
