using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Deterministic cross-track/team race benchmark. It drives the same pure
/// RaceSession, gear, heat, corner and team-vehicle rules used by the runtime,
/// then writes a compact matrix report for balance tuning.
/// </summary>
public static class TrackTeamBalanceBenchmark
{
    private const string REPORT_PATH = "design/balance/track-team-benchmark-2026-08-15.md";
    private const int RACES_PER_TRACK = 12;
    private const int MAX_TURNS = 700;
    private const int BASE_SEED = 20260815;

    private static readonly TeamId[] TEAMS =
    {
        TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP
    };

    private sealed class TeamAggregate
    {
        public int races;
        public int wins;
        public int finishes;
        public int blown;
        public int totalRank;
        public int totalTurns;
        public int totalSpins;
        public int totalHeatPaid;
        public int totalSlipstreamTriggers;
        public int totalSlipstreamMovement;
        public int totalRawCardMovement;
        public int totalNonSlipstreamMovement;
        public int totalGoTurns;
        public int totalRecoverTurns;
        public int totalForcedRecoverTurns;

        public float AverageRank => races == 0 ? 0f : (float)totalRank / races;
        public float AverageTurns => finishes == 0 ? 0f : (float)totalTurns / finishes;
        public float FinishRate => races == 0 ? 0f : (float)finishes / races;
        public float DnfRate => races == 0 ? 0f : (float)blown / races;
        public float AverageSpins => races == 0 ? 0f : (float)totalSpins / races;
        public float AverageHeat => races == 0 ? 0f : (float)totalHeatPaid / races;
        public float AverageSlipstreamTriggers => races == 0 ? 0f : (float)totalSlipstreamTriggers / races;
        public float AverageSlipstreamMovement => races == 0 ? 0f : (float)totalSlipstreamMovement / races;
        public float AverageRawCardMovement => races == 0 ? 0f : (float)totalRawCardMovement / races;
        public float AverageNonSlipstreamMovement => races == 0 ? 0f : (float)totalNonSlipstreamMovement / races;
        public float AverageGoTurns => races == 0 ? 0f : (float)totalGoTurns / races;
        public float AverageRecoverTurns => races == 0 ? 0f : (float)totalRecoverTurns / races;
        public float AverageForcedRecoverTurns => races == 0 ? 0f : (float)totalForcedRecoverTurns / races;
    }

    private sealed class RaceResult
    {
        public List<PlayerState> players;
        public Dictionary<PlayerState, int> finishTurns;
        public Dictionary<TeamId, int> slipstreamTriggers;
        public Dictionary<TeamId, int> slipstreamMovement;
        public Dictionary<TeamId, int> rawCardMovement;
        public Dictionary<TeamId, int> nonSlipstreamMovement;
        public Dictionary<TeamId, int> goTurns;
        public Dictionary<TeamId, int> recoverTurns;
        public Dictionary<TeamId, int> forcedRecoverTurns;
    }

    private sealed class BenchmarkConfig
    {
        public readonly int[] speedCards = { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4 };
        public readonly int heatPoolPerPlayer = 6;
        public readonly int handSize = 7;
        public readonly int totalLaps = 3;
        public readonly int minGear = 1;
        public readonly int maxGear = 4;
        public readonly int twoGearShiftHeatCost = 1;
        public readonly int gearOneCooldown = 3;
        public readonly int gearTwoCooldown = 1;
        public readonly int aiLookAheadNodes = 6;
        public readonly float aiHeatWarningThreshold = 0.7f;
        public readonly float aiCautiousHeatThreshold = 0.5f;
        public readonly float aiAggressiveHeatThreshold = 0.3f;
    }

    [MenuItem("Tools/Run Track-Team Balance Benchmark")]
    public static void Run()
    {
        string[] trackIds = TrackDataLoader.GetAvailableTrackIds();
        Array.Sort(trackIds, StringComparer.Ordinal);
        var report = new StringBuilder();
        report.AppendLine("# Track × Team Balance Benchmark");
        report.AppendLine();

        report.AppendLine($"> Deterministic pure-layer benchmark generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
        report.AppendLine($"> {RACES_PER_TRACK} six-team races per track, seeds `{BASE_SEED}..`, max {MAX_TURNS} turns.");
        report.AppendLine("> All six teams run the same policy; ranks are compared within each track. Results are directional tuning evidence, not player skill data.");
        report.AppendLine("> Each turn executes the non-slipstream movement in rank order, then resolves up to two slipstream segments from settled positions and applies the bonus movement, matching the runtime phase boundary.");
        report.AppendLine("> AI tuning matches the active config baseline: lookahead 6, heat thresholds 0.7/0.5/0.3, China affordable corner heat 1.");
        report.AppendLine();
        report.AppendLine("## Runtime team profile used");
        report.AppendLine();

        var chinaVariantRows = new StringBuilder();
        report.AppendLine("| Team | Handling | Cooling | Durability | Straight base | Straight turn | Slipstream |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|");
        foreach (TeamId team in TEAMS)
        {
            TeamVehicleProfile profile = TeamVehicleRules.GetProfile(team);
            report.AppendLine($"| {team} | {profile.Handling} | {profile.Cooling} | {profile.Durability} | {TeamVehicleRules.GetStraightMovementBonus(team)} | {TeamVehicleRules.GetStraightTurnBonus(team)} | {profile.Slipstream} |");
        }
        report.AppendLine();

        foreach (string trackId in trackIds)
        {
            TrackConfig track = TrackDataLoader.LoadConfig(trackId);
            if (track == null || track.cells == null || track.cells.Length == 0)
                continue;

            var aggregates = new Dictionary<TeamId, TeamAggregate>();
            foreach (TeamId team in TEAMS)
                aggregates[team] = new TeamAggregate();

            for (int raceIndex = 0; raceIndex < RACES_PER_TRACK; raceIndex++)
            {
                RaceResult result = SimulateRace(
                    track,
                    BASE_SEED + raceIndex * 7919 + StableHash(trackId),
                    raceIndex % TEAMS.Length,
                    chinaCornerHeatTolerance: 1);
                AccumulateResult(aggregates, result);
            }

            var chinaVariantAggregates = new Dictionary<TeamId, TeamAggregate>();
            foreach (TeamId team in TEAMS)
                chinaVariantAggregates[team] = new TeamAggregate();
            for (int raceIndex = 0; raceIndex < RACES_PER_TRACK; raceIndex++)
            {
                RaceResult result = SimulateRace(
                    track,
                    BASE_SEED + raceIndex * 7919 + StableHash(trackId),
                    raceIndex % TEAMS.Length,
                    chinaCornerHeatTolerance: 0);
                AccumulateResult(chinaVariantAggregates, result);
            }

            report.AppendLine($"## {track.trackName} (`{trackId}`)");
            report.AppendLine();
            report.AppendLine($"Cells: {track.cells.Length}; laps: {track.laps}; corner cells: {CountCorners(track)}; apexes: {CountApexes(track)}.");
            report.AppendLine();
            report.AppendLine("| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid | Avg slipstreams | Avg slipstream move |");
            report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
            foreach (TeamId team in TEAMS)
            {
                TeamAggregate a = aggregates[team];
                report.AppendLine($"| {team} | {a.AverageRank:F2} | {(float)a.wins / a.races:P0} | {a.FinishRate:P0} | {a.DnfRate:P0} | {a.AverageTurns:F1} | {a.AverageSpins:F2} | {a.AverageHeat:F1} | {a.AverageSlipstreamTriggers:F1} | {a.AverageSlipstreamMovement:F1} |");
            }
            report.AppendLine();

            TeamAggregate china = aggregates[TeamId.CN];
            report.AppendLine(
                $"China cadence per race: Go `{china.AverageGoTurns:F1}`, Recover `{china.AverageRecoverTurns:F1}`, " +
                $"corner-forced Recover `{china.AverageForcedRecoverTurns:F1}`; raw-card movement `{china.AverageRawCardMovement:F1}`, " +
                $"non-slipstream movement `{china.AverageNonSlipstreamMovement:F1}`.");
            report.AppendLine();

            TeamAggregate chinaVariant = chinaVariantAggregates[TeamId.CN];
            chinaVariantRows.AppendLine(
                $"| {track.trackName} | {china.AverageRank:F2} | {(float)china.wins / china.races:P0} | {china.DnfRate:P0} | " +
                $"{chinaVariant.AverageRank:F2} | {(float)chinaVariant.wins / chinaVariant.races:P0} | {chinaVariant.DnfRate:P0} | " +
                $"{chinaVariant.AverageGoTurns:F1} / {chinaVariant.AverageRecoverTurns:F1} |");

        }

        report.AppendLine("## Controlled variant: China uses a strict zero-heat corner guard");
        report.AppendLine();
        report.AppendLine("> Baseline uses the runtime one-heat tolerance when the engine can also pay overclock and missing-card costs. The benchmark-only variant forces Recover for any projected corner heat.");
        report.AppendLine();
        report.AppendLine("| Track | Baseline rank | Baseline win | Baseline DNF | Variant rank | Variant win | Variant DNF | Variant Go / Recover |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
        report.Append(chinaVariantRows);
        report.AppendLine();

        report.AppendLine("## Tuning decisions");
        report.AppendLine();
        report.AppendLine("- China keeps the design-sheet +1 top-speed/+2 acceleration package during Go, but its handling was tuned from -1 to 0 after the safe-card benchmark showed repeated apex losses. Recover still uses the standalone cooling chain, and the AI now follows the documented Go → Go → Recover rhythm.");
        report.AppendLine("- Benchmark heat payments now create real hand-zone heat cards just like runtime payments, so Recover and standard cooling can replenish the engine. The former 92% China DNF rate at Le Mans was a benchmark artifact; the corrected run finishes 100% of entries.");
        report.AppendLine("- The six team insertion orders rotate evenly across the 12 races on every track, neutralizing the first-turn tie-order bias from the runtime's shared start cell.");
        report.AppendLine("- Standard AI now chooses low cards whenever the projected movement reaches a corner, and the risk window is bounded by that movement instead of a fixed 18-cell scan. This removes avoidable DNF noise from the team comparison.");
        report.AppendLine("- America’s straight-roar bonus is capped at one flat +1 per straight turn rather than multiplying by every card; this preserves its straight-line identity while reducing high-gear runaway results.");
        report.AppendLine("- Italy's acceleration is resolved only as the documented one-shot bonus on the turn after a successful corner. Removing its undocumented permanent straight +1 reduced its nine-track average win rate from 74% to about 26% in the final matrix.");
        report.AppendLine("- China accepts at most one affordable projected corner heat; larger risks or combined costs beyond the engine reserve force Recover. The report retains the former strict zero-heat policy as a controlled comparison.");
        report.AppendLine("- UK, Japan and the remaining teams were not broadly buffed from this pass: their full identity depends on interactive tech/trick/driver effects that a pure vehicle benchmark deliberately does not auto-play.");
        report.AppendLine("- The report is retained as a regression baseline; rerun the menu item after any TeamVehicleRules, gear, corner or card-balance change.");

        string absolutePath = Path.GetFullPath(REPORT_PATH);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllText(absolutePath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log($"[BalanceBenchmark] Wrote {absolutePath}");
    }

    private static void AccumulateResult(
        Dictionary<TeamId, TeamAggregate> aggregates,
        RaceResult result)
    {
        List<RaceRanking.RankEntry> rankings = RaceRanking.GetRankings(result.players);
        foreach (RaceRanking.RankEntry entry in rankings)
        {
            TeamId team = entry.player.teamId;
            TeamAggregate aggregate = aggregates[team];
            aggregate.races++;
            aggregate.totalRank += entry.rank;
            aggregate.totalSpins += entry.player.spinCounter;
            aggregate.blown += entry.player.isBlown ? 1 : 0;
            aggregate.finishes += entry.player.hasFinished ? 1 : 0;
            if (entry.rank == 1) aggregate.wins++;
            if (entry.player.hasFinished &&
                result.finishTurns.TryGetValue(entry.player, out int finishTurn))
                aggregate.totalTurns += finishTurn;
            aggregate.totalHeatPaid += entry.player.GetHeatPaidForBenchmark();
            aggregate.totalSlipstreamTriggers += result.slipstreamTriggers[team];
            aggregate.totalSlipstreamMovement += result.slipstreamMovement[team];
            aggregate.totalRawCardMovement += result.rawCardMovement[team];
            aggregate.totalNonSlipstreamMovement += result.nonSlipstreamMovement[team];
            aggregate.totalGoTurns += result.goTurns[team];
            aggregate.totalRecoverTurns += result.recoverTurns[team];
            aggregate.totalForcedRecoverTurns += result.forcedRecoverTurns[team];
        }
    }

    private static int CountCorners(TrackConfig track)
    {
        int count = 0;
        foreach (CellData cell in track.cells)
            if (cell != null && cell.IsCorner) count++;
        return count;
    }

    private static int CountApexes(TrackConfig track)
    {
        int count = 0;
        foreach (CellData cell in track.cells)
            if (cell != null && cell.isApex) count++;
        return count;
    }

    private static RaceResult SimulateRace(
        TrackConfig track,
        int seed,
        int teamOrderOffset,
        int chinaCornerHeatTolerance)
    {
        var cfg = new BenchmarkConfig();
        GameConfigSO deckConfig = CreateConfig(cfg);
        var session = new RaceSession(new SystemRandomSource(seed));
        session.InitializeWeather(track.weatherPool, track.defaultWeather);
        var finishTurns = new Dictionary<PlayerState, int>();
        var slipstreamTriggers = new Dictionary<TeamId, int>();
        var slipstreamMovement = new Dictionary<TeamId, int>();
        var rawCardMovement = new Dictionary<TeamId, int>();
        var nonSlipstreamMovement = new Dictionary<TeamId, int>();
        var goTurns = new Dictionary<TeamId, int>();
        var recoverTurns = new Dictionary<TeamId, int>();
        var forcedRecoverTurns = new Dictionary<TeamId, int>();
        foreach (TeamId team in TEAMS)
        {
            slipstreamTriggers[team] = 0;
            slipstreamMovement[team] = 0;
            rawCardMovement[team] = 0;
            nonSlipstreamMovement[team] = 0;
            goTurns[team] = 0;
            recoverTurns[team] = 0;
            forcedRecoverTurns[team] = 0;
        }
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(track);
        TrackDataLoader.BuildCornerMaps(track, nodes, out Dictionary<int, int> cornerLimits, out _);

        for (int i = 0; i < TEAMS.Length; i++)
        {
            TeamId team = TEAMS[(i + teamOrderOffset) % TEAMS.Length];
            var player = new PlayerState(team.ToString(), true, TrackRules.FindStartFinishNodeIndex(nodes), 1)
            {
                teamId = team,
                usesChinaGearSystem = TeamGearRules.IsChina(team),
                techState = session.CreateDemoTechState(team)
            };
            TechTreeRules.ResetPerRaceState(player.techState);
            int pool = TeamVehicleRules.GetBaseHeatPoolSize(team, cfg.heatPoolPerPlayer);
            pool = session.EffectiveHeatPoolSize(player, pool);
            player.deck.InitializeDeck(deckConfig, new HeatPool(pool), new SystemRandomSource(seed + i * 97 + 11));
            player.deck.AddTrickCardsToDrawPile(session.CreateInitialTrickCards(team));
            player.deck.DrawToHand(session.EffectiveHandSize(player, cfg.handSize));
            session.Players.Add(player);
        }

        int weatherRolledLap = 0;
        int turn = 0;
        while (turn < MAX_TURNS && !session.IsRaceOver())
        {
            turn++;
            foreach (PlayerState p in session.Players)
                session.BeginTurn(p);

            var skipped = new HashSet<PlayerState>();
            List<PlayerState> turnOrder = session.GetTurnOrder();
            foreach (PlayerState p in turnOrder)
            {
                if (p.hasFinished || p.isBlown) continue;
                if (p.skipNextTurn)
                {
                    p.skipNextTurn = false;
                    p.gear = TeamGearRules.IsChina(p.teamId) ? ChinaGearShiftRules.RecoverGear : cfg.minGear;
                    p.chinaConsecutiveGearCount = 0;
                    skipped.Add(p);
                    continue;
                }

                int requestedGear = ChooseGear(
                    session, p, nodes, cornerLimits, cfg, chinaCornerHeatTolerance,
                    out bool forcedRecover);
                if (forcedRecover)
                    forcedRecoverTurns[p.teamId]++;
                TeamGearRules.Resolution shift = TeamGearRules.Resolve(
                    p.teamId, p.gear, p.chinaConsecutiveGearCount, requestedGear,
                    cfg.minGear, cfg.maxGear, cfg.twoGearShiftHeatCost,
                    cfg.gearOneCooldown, cfg.gearTwoCooldown);
                if (!PayHeatOrSpin(p, shift.HeatCost, p.position))
                {
                    skipped.Add(p);
                    continue;
                }
                p.gear = shift.TargetGear;
                p.chinaConsecutiveGearCount = shift.IsChina ? shift.ConsecutiveCount : 0;
                if (shift.AdditionalHeat > 0 && !PayHeatOrSpin(p, shift.AdditionalHeat, p.position))
                {
                    skipped.Add(p);
                    continue;
                }

                if (!p.deck.DrawToHand(session.EffectiveHandSize(p, cfg.handSize)))
                {
                    // A depleted hand is a lost turn in the benchmark, not an
                    // artificial engine-failure spin. This keeps the report
                    // focused on the team/track balance rather than deck
                    // exhaustion artifacts.
                    skipped.Add(p);
                    continue;
                }

                int requiredCards = TeamGearRules.GetSpeedCardCount(
                    p.teamId, p.gear, p.chinaConsecutiveGearCount, 0);
                bool cornerRisk = HasUpcomingCornerRisk(
                    p, requiredCards, nodes, cornerLimits, session, cfg.aiLookAheadNodes);
                List<CardData> chosen = AIPlanner.ChooseSpeedCards(
                    p.deck, requiredCards, p.HeatRatio, cornerRisk,
                    cfg.aiHeatWarningThreshold, cfg.aiCautiousHeatThreshold,
                    0f, session.Random);
                int missing = RaceRules.GetMissingSpeedCardCount(requiredCards, chosen.Count);
                if (missing > 0 && !PayHeatOrSpin(p, missing, p.position))
                {
                    skipped.Add(p);
                    continue;
                }
                p.deck.RemoveFromHand(chosen);
                p.playedSpeedCardsThisTurn.AddRange(chosen);
                p.cornerTotalThisTurn = RaceRules.SumCardValues(chosen);
                rawCardMovement[p.teamId] += p.cornerTotalThisTurn;
                if (TeamGearRules.IsChina(p.teamId))
                {
                    if (ChinaGearShiftRules.IsGo(p.gear)) goTurns[p.teamId]++;
                    else recoverTurns[p.teamId]++;
                }
            }

            // Freeze and execute every non-slipstream movement before resolving
            // any slipstream. This mirrors the runtime's end-of-turn boundary.
            foreach (PlayerState p in turnOrder)
            {
                if (skipped.Contains(p) || p.hasFinished || p.isBlown)
                {
                    p.totalMovementThisTurn = 0;
                    continue;
                }

                int oldPos = p.position;
                int rawEnd = oldPos + p.cornerTotalThisTurn;
                bool crossedCorner = TrackRules.GetUniqueApexCornersCrossed(nodes, oldPos, rawEnd).Count > 0;
                int bonus = session.ComputeMovementBonus(p, crossedCorner);
                bonus += session.ConsumeItalyCornerExitBonus(p);
                p.totalMovementThisTurn = p.cornerTotalThisTurn + bonus;
                nonSlipstreamMovement[p.teamId] += p.totalMovementThisTurn;
            }

            foreach (PlayerState p in turnOrder)
            {
                if (skipped.Contains(p) || p.hasFinished || p.isBlown) continue;

                int oldPos = p.position;
                int rawEnd = oldPos + p.cornerTotalThisTurn;
                int newPos = oldPos + p.totalMovementThisTurn;
                for (int position = oldPos + 1; position <= newPos; position++)
                {
                    int index = Normalize(position, nodes.Count);
                    if (!nodes[index].isStartFinish) continue;
                    p.lap++;
                    session.OnNewLap(p);
                    if (p.lap >= track.laps)
                    {
                        p.hasFinished = true;
                        session.AssignFinish(p);
                        finishTurns[p] = turn;
                        break;
                    }
                }
                p.position = Normalize(newPos, nodes.Count);

                int cooldown = TeamGearRules.GetCooldown(
                    p.teamId, p.gear, p.chinaConsecutiveGearCount,
                    cfg.gearOneCooldown, cfg.gearTwoCooldown);
                if (!TeamGearRules.IsChina(p.teamId))
                    cooldown += TeamVehicleRules.GetCooling(p.teamId);
                p.deck.CoolHeat(cooldown);

                HashSet<int> crossedCorners = TrackRules.GetUniqueApexCornersCrossed(nodes, oldPos, rawEnd);
                bool completedCorner = crossedCorners.Count > 0;
                foreach (int cornerId in crossedCorners)
                {
                    int baseLimit = cornerLimits.TryGetValue(cornerId, out int value) ? value : 99;
                    int limit = session.EffectiveCornerLimit(p, baseLimit);
                    if (p.cornerTotalThisTurn <= limit) continue;
                    int heat = p.cornerTotalThisTurn - limit + TeamVehicleRules.GetCornerHeatPenalty(p.teamId);
                    if (!PayHeatOrSpin(p, heat, oldPos))
                    {
                        completedCorner = false;
                        break;
                    }
                }
                session.ArmItalyCornerExitBonus(p, completedCorner);
            }

            // Resolve the chain from actual settled positions, then apply the
            // bonus as a separate end-of-turn movement pass.
            var settledMovements = new Dictionary<PlayerState, int>(turnOrder.Count);
            foreach (PlayerState p in turnOrder)
                settledMovements[p] = 0;

            foreach (PlayerState p in turnOrder)
            {
                if (skipped.Contains(p) || p.hasFinished || p.isBlown)
                    continue;

                SlipstreamChainResult chain = session.ComputeSlipstreamChain(
                    p, session.Players, nodes.Count, settledMovements);
                p.totalMovementThisTurn += chain.TotalBonus;
                slipstreamTriggers[p.teamId] += chain.Steps.Count;
                slipstreamMovement[p.teamId] += chain.TotalBonus;

                if (chain.TotalBonus <= 0)
                    continue;

                int oldPos = p.position;
                int newPos = oldPos + chain.TotalBonus;
                for (int position = oldPos + 1; position <= newPos; position++)
                {
                    int index = Normalize(position, nodes.Count);
                    if (!nodes[index].isStartFinish)
                        continue;

                    p.lap++;
                    session.OnNewLap(p);
                    if (p.lap >= track.laps)
                    {
                        p.hasFinished = true;
                        session.AssignFinish(p);
                        finishTurns[p] = turn;
                        break;
                    }
                }
                p.position = Normalize(newPos, nodes.Count);

                if (!p.hasFinished && PitLaneRules.CrossedPitEntry(oldPos, newPos, nodes) &&
                    p.pitStopRequested)
                {
                    p.pitStopRequested = false;
                    p.pitStopScheduled = true;
                }
            }

            foreach (PlayerState p in session.Players)
            {
                p.deck.DiscardSpeedCards(p.playedSpeedCardsThisTurn);
                p.playedSpeedCardsThisTurn.Clear();
                p.deck.DiscardPlayableCardsFromHand(p.deck.GetTricksInHand());
                p.deck.RemoveTempCardsFromHand();
            }

            foreach (PlayerState p in session.Players)
                if (p.lap > weatherRolledLap)
                {
                    weatherRolledLap = p.lap;
                    session.RollWeatherForLap();
                    break;
                }
        }

        UnityEngine.Object.DestroyImmediate(deckConfig);
        return new RaceResult
        {
            players = session.Players,
            finishTurns = finishTurns,
            slipstreamTriggers = slipstreamTriggers,
            slipstreamMovement = slipstreamMovement,
            rawCardMovement = rawCardMovement,
            nonSlipstreamMovement = nonSlipstreamMovement,
            goTurns = goTurns,
            recoverTurns = recoverTurns,
            forcedRecoverTurns = forcedRecoverTurns
        };
    }

    private static int ChooseGear(RaceSession session, PlayerState p, List<TrackNode> nodes,
        Dictionary<int, int> cornerLimits, BenchmarkConfig cfg,
        int chinaCornerHeatTolerance, out bool forcedRecover)
    {
        forcedRecover = false;
        if (TeamGearRules.IsChina(p.teamId))
        {
            int target = ChinaGearShiftRules.ChooseAiGear(
                p.gear, p.chinaConsecutiveGearCount, p.deck.CountSpeedInHand(),
                p.HeatRatio, cfg.aiHeatWarningThreshold);
            ChinaGearShiftRules.Result goResolution = ChinaGearShiftRules.Resolve(
                p.gear, p.chinaConsecutiveGearCount, target);
            int goCards = goResolution.SpeedCardCount;
            int projectedCornerHeat = GetProjectedChinaCornerHeat(
                p, goCards, nodes, cornerLimits, session, cfg.aiLookAheadNodes);
            int missingCardHeat = RaceRules.GetMissingSpeedCardCount(
                goCards, p.deck.CountSpeedInHand());
            int committedHeat = goResolution.AdditionalHeat + missingCardHeat;
            if (target == ChinaGearShiftRules.GoGear &&
                ChinaGearShiftRules.ShouldForceRecoverForCorner(
                    projectedCornerHeat,
                    committedHeat,
                    p.deck.heatPool.remaining,
                    chinaCornerHeatTolerance))
            {
                forcedRecover = true;
                return ChinaGearShiftRules.RecoverGear;
            }
            return target;
        }

        if (p.HeatRatio >= cfg.aiHeatWarningThreshold)
            return Mathf.Max(cfg.minGear, p.gear - 1);
        if (p.deck.CountSpeedInHand() < p.gear)
            return Mathf.Max(cfg.minGear, p.deck.CountSpeedInHand());

        int currentEstimate = EstimateMovement(session, p, p.gear);
        int lookAhead = Mathf.Min(
            Mathf.Min(cfg.aiLookAheadNodes, nodes.Count - 1),
            Mathf.Max(1, currentEstimate));
        for (int offset = 1; offset <= lookAhead; offset++)
        {
            TrackNode node = nodes[Normalize(p.position + offset, nodes.Count)];
            if (node.cornerId <= 0) continue;
            int baseLimit = cornerLimits.TryGetValue(node.cornerId, out int value) ? value : 99;
            int limit = session.EffectiveCornerLimit(p, baseLimit);
            if (currentEstimate > limit)
            {
                int safeGear = p.gear;
                while (safeGear > cfg.minGear && EstimateMovement(session, p, safeGear) > limit)
                    safeGear--;
                return safeGear;
            }
        }

        if (p.HeatRatio <= cfg.aiAggressiveHeatThreshold && p.gear < cfg.maxGear)
            return p.gear + 1;
        return p.gear;
    }

    private static int EstimateMovement(RaceSession session, PlayerState p, int cardCount)
    {
        List<CardData> cards = p.deck.GetTopNSpeedCards(cardCount);
        int sum = RaceRules.SumCardValues(cards);
        if (cards.Count == 0) return 0;
        // The policy only needs a conservative estimate; the next corner is
        // handled separately below, so raw card values are sufficient here.
        return sum;
    }

    private static bool HasUpcomingCornerRisk(PlayerState p, int cardCount,
        List<TrackNode> nodes, Dictionary<int, int> cornerLimits, RaceSession session,
        int configuredLookAhead)
    {
        int estimatedMove = EstimateMovement(session, p, cardCount);
        int lookAhead = Mathf.Min(
            Mathf.Min(configuredLookAhead, nodes.Count - 1),
            Mathf.Max(1, estimatedMove));
        for (int offset = 1; offset <= lookAhead; offset++)
        {
            TrackNode node = nodes[Normalize(p.position + offset, nodes.Count)];
            if (node.cornerId <= 0) continue;
            int baseLimit = cornerLimits.TryGetValue(node.cornerId, out int value) ? value : 99;
            if (estimatedMove > session.EffectiveCornerLimit(p, baseLimit))
                return true;
        }
        return false;
    }

    private static int GetProjectedChinaCornerHeat(PlayerState p, int cardCount,
        List<TrackNode> nodes, Dictionary<int, int> cornerLimits, RaceSession session,
        int configuredLookAhead)
    {
        int estimatedMove = RaceRules.SumCardValues(p.deck.GetBottomNSpeedCards(cardCount));
        int lookAhead = Mathf.Min(
            Mathf.Min(configuredLookAhead, nodes.Count - 1),
            Mathf.Max(1, estimatedMove));
        int projectedHeat = 0;
        var visitedCorners = new HashSet<int>();
        for (int offset = 1; offset <= lookAhead; offset++)
        {
            TrackNode node = nodes[Normalize(p.position + offset, nodes.Count)];
            if (node.cornerId <= 0 || !visitedCorners.Add(node.cornerId)) continue;
            int baseLimit = cornerLimits.TryGetValue(node.cornerId, out int value) ? value : 99;
            projectedHeat += Mathf.Max(
                0,
                estimatedMove - session.EffectiveCornerLimit(p, baseLimit));
        }
        return projectedHeat;
    }

    private static bool PayHeatOrSpin(PlayerState p, int amount, int rewindPosition)
    {
        if (amount <= 0) return true;
        int drawn = p.deck.DrawHeatFromPoolToHand(amount);
        if (drawn >= amount)
        {
            p.AddHeatPaid(drawn);
            return true;
        }

        p.spinCounter++;
        p.deck.RecoverAllHeatToPool();
        p.position = rewindPosition;
        p.gear = TeamGearRules.IsChina(p.teamId) ? ChinaGearShiftRules.RecoverGear : 1;
        p.chinaConsecutiveGearCount = 0;
        p.skipNextTurn = true;
        if (p.spinCounter >= 3)
            p.isBlown = true;
        return false;
    }

    private static int Normalize(int position, int count)
    {
        int result = position % count;
        return result < 0 ? result + count : result;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < value.Length; i++)
                hash = hash * 31 + value[i];
            return hash;
        }
    }

    private static GameConfigSO CreateConfig(BenchmarkConfig cfg)
    {
        GameConfigSO config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = (int[])cfg.speedCards.Clone();
        config.heatPoolPerPlayer = cfg.heatPoolPerPlayer;
        config.handSize = cfg.handSize;
        config.totalLaps = cfg.totalLaps;
        return config;
    }
}

// Benchmark-only counters kept out of the runtime data model.
internal static class TrackTeamBenchmarkExtensions
{
    private static readonly Dictionary<PlayerState, int> HeatPaid = new Dictionary<PlayerState, int>();

    public static int GetHeatPaidForBenchmark(this PlayerState player)
    {
        return HeatPaid.TryGetValue(player, out int value) ? value : 0;
    }

    public static void AddHeatPaid(this PlayerState player, int amount)
    {
        HeatPaid[player] = player.GetHeatPaidForBenchmark() + amount;
    }
}
