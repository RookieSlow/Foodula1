# Balance Check Report: Team × Track Runtime Benchmark

**Date:** 2026-08-25
**Overall Health:** CONCERNS

## Data Sources

- `Assets/Scripts/Core/TeamVehicleRules.cs`
- `Assets/Scripts/Core/ChinaGearShiftRules.cs`
- `Assets/Scripts/Core/RaceSession.cs`
- `Assets/Scripts/AI/AIController.cs`
- `Assets/Scripts/Editor/TrackTeamBalanceBenchmark.cs`
- `design/gdd/foodula-1-teams-cars.md`
- `design/gdd/foodula-1-ai.md`
- `design/balance/track-team-benchmark-2026-08-15.md`

## Health Summary

| Area | Status | Evidence |
|---|---|---|
| Benchmark rule fidelity | HEALTHY after fixes | Heat uses real hand-zone cards, China first-Go risk uses 3 cards after Recover, lookahead/AI thresholds match runtime `6` and `0.7/0.5/0.3`, per-player finish turns are recorded, and team insertion order rotates evenly. |
| Race safety | HEALTHY | Corrected benchmark: 9 tracks × 12 races × 6 entries, all team/track finish rates are 100%; spin averages are 0.00–0.83. |
| United States | HEALTHY for current baseline | Removing accidental profile stacking reduced US from 67–100% wins on most tracks to 0–33%; final average rank is about 2.52. |
| Italy | HEALTHY after fixes | Italy averages rank 2.31 and about 26% wins. Its undocumented permanent straight `+1` was removed while handling `+2` and the documented next-turn corner-exit bonus remain. |
| China | CONCERNS, improved | China averages rank 4.54 and about 7% wins with 0% DNF. It may accept one payable corner heat, reducing average Recover turns from 25.8 under the strict guard to 22.5. |
| UK / Japan identity coverage | CONCERNS | The benchmark exercises passive runtime effects but does not strategically play interactive trick cards, so these teams' intended high-variance strengths are underrepresented. |

## Outliers

### 1. Italy's former passive package dominated every track family

The former runtime combined handling `+2`, an undocumented permanent straight `+1`, and a same-turn corner-exit `+1`. A controlled run that removed only the straight bonus changed Italy's average rank from `1.37` to `2.02` and average win rate from `74%` to `35%`. The final implementation also corrected the timing to the next turn; after aligning lookahead and all benchmark AI thresholds, the regenerated matrix settles at rank `2.31` and about `26%` wins.

### 2. China is safe but consistently slow

After real heat-card payments restored Recover cooling, China finishes every benchmark race. Its earlier mode guard compared corners against the highest possible Go hand, even though corner card selection then used the lowest cards. The final policy sums heat across unique apexes crossed by the lowest legal Go hand and accepts at most one heat only when overclock, missing-card, and corner costs all fit the engine reserve. Against the strict zero-heat policy, average rank improves from `4.79` to `4.54`, average Recover turns fall from `25.8` to `22.5`, and DNF remains `0%`.

### 3. United States slipstream conversion is intentionally high

US gains `+3` movement per normal-weather slipstream segment (`+2` base plus team `+1`), versus `+2` for most teams. The new two-segment chain can therefore yield `+6`. Results remain competitive rather than dominant after Straight Roar is limited to one flat `+1`, so no immediate slipstream nerf is supported by this run.

## Degenerate or Misleading Patterns Found

- The old benchmark deducted an integer from the engine without creating a heat card. Cooling therefore had nothing to return, making all heat expenditure permanent and producing a false 92% China DNF at Le Mans.
- US previously received both the profile sum (`+3`) and the tuned Straight Roar (`+1`) on every straight turn. The effective `+4` contradicted both the tuning note and the intended fixed bonus.
- China corner-risk estimation treated any non-zero Recover chain as if the next Go required four cards. A Recover → Go switch actually resets the chain and requires three cards.
- Aggregate finish time previously used the last race turn for every finisher. It now records the turn in which each player crosses the line.
- Rotating the insertion order changed individual rates, but Italy remained dominant and China remained last or near-last; the overall spread is therefore not explained by the shared starting-cell tie order.
- Italy's acceleration profile was incorrectly added to every straight turn, although the GDD expresses that acceleration as a one-shot bonus on the turn after a successful corner. The bonus also resolved on the corner-crossing turn. Both mismatches are fixed and regression-covered.
- Removing China's corner-mode safety check entirely is unsafe: the controlled variant produced `83–100%` DNF on Nürburgring Bier, Shanghai, Silverstone, and Suzuka. The retained guard projects the lowest legal Go hand and enforces a bounded, payable heat budget.
- The benchmark previously used 18-cell lookahead and hardcoded AI heat thresholds (`0.6` for China gear choice, `0.55/0.25` for card choice, and `0.65/0.25` for standard gear choice) instead of runtime lookahead `6` and thresholds `0.7/0.5/0.3`. These values now come from the benchmark config and match the active game assets.

## Progression Curves

Not applicable to this benchmark. It evaluates a single demo race configuration and does not model career unlock pacing, economy, or long-term battery degradation.

## Values Requiring Design Attention

| Value / rule | Current | Concern | Recommendation |
|---|---:|---|---|
| Italy straight base | `0` | Corrected from undocumented `+1` | Keep; the controlled result restores the intended corner-specialist profile. |
| Italy handling | `+2` | Identity-defining and no longer stacked with permanent straight pace | Keep unless player evidence contradicts the final matrix. |
| Italy corner exit | next-turn first card `+1` | One-shot state can span an empty turn | Keep; now matches the GDD and has a regression test. |
| China Go straight base | `+3` | High nominal bonus still yields bottom-tier results | Do not buff yet; first improve/measure Go–Recover AI decisions and card-choice efficiency. |
| China corner-risk policy | lowest legal Go set; payable corner heat `1` | Silverstone/Suzuka remain weak despite shorter Recover chains | Keep this bounded policy; validate player feel before adding final-lap or route-specific aggression. |

## Recommendations

1. **High:** Confirm Italy's corrected next-turn effect and China's revised low-hand safety choice in one manual Play Mode race; deterministic AI evidence does not replace player feel.
2. **High:** Manually validate China's one-heat Go decisions on Silverstone/Suzuka before considering final-lap or route-specific aggression.
3. **Medium:** Add a player-policy benchmark that actively uses each team's trick identity before buffing UK or Japan.

## Applied in This Check

- Fixed US Straight Roar profile stacking and added a regression test.
- Fixed China Recover → Go card-count estimation in runtime AI and benchmark AI.
- Aligned benchmark heat payment with the runtime heat-card lifecycle.
- Added per-player finish turns and even team-order rotation to the benchmark.
- Added Go/Recover, forced-Recover, raw-card movement, and non-slipstream movement diagnostics.
- Corrected Italy's corner-exit timing, removed its undocumented permanent straight `+1`, and added regression coverage.
- Changed China corner mode safety from highest-hand risk to unavoidable lowest-hand risk; retained the guard after the no-guard variant caused severe DNF.
- Added a configurable one-heat affordable-risk budget that includes every unique crossed apex plus committed overclock/missing-card costs; four pure-rule boundary cases cover it.
- Aligned benchmark lookahead and AI warning/cautious/aggressive thresholds with runtime `6` and `0.7/0.5/0.3`.
- Regenerated the nine-track report with the final runtime rules.

## Verification Note

- The runtime/config changes and four new heat-budget cases completed a real Unity EditMode run at `417/417`.
- After the final benchmark-only lookahead/threshold wiring, the Unity menu benchmark compiled and executed successfully. A requested final full-suite rerun initialized `0/417` tests and timed out in the MCP test runner; it produced no assertion failure and was not reported as a pass. The local environment has no .NET SDK, so `dotnet build --no-restore` was unavailable as a fallback.
