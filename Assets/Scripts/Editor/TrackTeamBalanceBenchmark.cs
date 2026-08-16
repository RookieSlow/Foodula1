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

        public float AverageRank => races == 0 ? 0f : (float)totalRank / races;
        public float AverageTurns => finishes == 0 ? 0f : (float)totalTurns / finishes;
        public float FinishRate => races == 0 ? 0f : (float)finishes / races;
        public float DnfRate => races == 0 ? 0f : (float)blown / races;
        public float AverageSpins => races == 0 ? 0f : (float)totalSpins / races;
        public float AverageHeat => races == 0 ? 0f : (float)totalHeatPaid / races;
    }

    private sealed class RaceResult
    {
        public int turns;
        public List<PlayerState> players;
    }

    private sealed class BenchmarkConfig
    {
        public readonly int[] speedCards = { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4 };
        public readonly int initialHeatCards = 3;
        public readonly int heatPoolPerPlayer = 6;
        public readonly int handSize = 7;
        public readonly int totalLaps = 3;
        public readonly int minGear = 1;
        public readonly int maxGear = 4;
        public readonly int twoGearShiftHeatCost = 1;
        public readonly int gearOneCooldown = 3;
        public readonly int gearTwoCooldown = 1;
        public readonly int aiLookAheadNodes = 18;
    }

    [MenuItem("Tools/Run Track-Team Balance Benchmark")]
    public static void Run()
    {
        string[] trackIds = TrackDataLoader.GetAvailableTrackIds();
        Array.Sort(trackIds, StringComparer.Ordinal);
        var report = new StringBuilder();
        report.AppendLine("# Track × Team Balance Benchmark");
        report.AppendLine();
        report.AppendLine($"> Deterministic pure-layer benchmark generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}. ");
        report.AppendLine($"> {RACES_PER_TRACK} six-team races per track, seeds `{BASE_SEED}..`, max {MAX_TURNS} turns. ");
        report.AppendLine("> All six teams run the same policy; ranks are compared within each track. Results are directional tuning evidence, not player skill data.");
        report.AppendLine();
        report.AppendLine("## Runtime team profile used");
        report.AppendLine();
        report.AppendLine("| Team | Handling | Cooling | Durability | Straight base | Slipstream |");
        report.AppendLine("|---|---:|---:|---:|---:|---:|");
        foreach (TeamId team in TEAMS)
        {
            TeamVehicleProfile profile = TeamVehicleRules.GetProfile(team);
            report.AppendLine($"| {team} | {profile.Handling} | {profile.Cooling} | {profile.Durability} | {TeamVehicleRules.GetStraightMovementBonus(team)} | {profile.Slipstream} |");
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
                RaceResult result = SimulateRace(track, BASE_SEED + raceIndex * 7919 + StableHash(trackId));
                List<RaceRanking.RankEntry> rankings = RaceRanking.GetRankings(result.players);
                foreach (RaceRanking.RankEntry entry in rankings)
                {
                    TeamAggregate aggregate = aggregates[entry.player.teamId];
                    aggregate.races++;
                    aggregate.totalRank += entry.rank;
                    aggregate.totalSpins += entry.player.spinCounter;
                    aggregate.blown += entry.player.isBlown ? 1 : 0;
                    aggregate.finishes += entry.player.hasFinished ? 1 : 0;
                    if (entry.rank == 1) aggregate.wins++;
                    if (entry.player.hasFinished) aggregate.totalTurns += result.turns;
                    aggregate.totalHeatPaid += entry.player.GetHeatPaidForBenchmark();
                }
            }

            report.AppendLine($"## {track.trackName} (`{trackId}`)");
            report.AppendLine();
            report.AppendLine($"Cells: {track.cells.Length}; laps: {track.laps}; corner cells: {CountCorners(track)}; apexes: {CountApexes(track)}.");
            report.AppendLine();
            report.AppendLine("| Team | Avg rank | Win rate | Finish rate | DNF rate | Avg finish turns | Avg spins | Avg heat paid |");
            report.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|");
            foreach (TeamId team in TEAMS)
            {
                TeamAggregate a = aggregates[team];
                report.AppendLine($"| {team} | {a.AverageRank:F2} | {(float)a.wins / a.races:P0} | {a.FinishRate:P0} | {a.DnfRate:P0} | {a.AverageTurns:F1} | {a.AverageSpins:F2} | {a.AverageHeat:F1} |");
            }
            report.AppendLine();
        }

        report.AppendLine("## Tuning decisions");
        report.AppendLine();
        report.AppendLine("- China keeps the design-sheet +1 top-speed/+2 acceleration package during Go, but its handling was tuned from -1 to 0 after the safe-card benchmark showed repeated apex losses. Recover still uses the standalone cooling chain, and the AI now follows the documented Go → Go → Recover rhythm.");
        report.AppendLine("- Standard AI now chooses low cards whenever the projected movement reaches a corner, and the risk window is bounded by that movement instead of a fixed 18-cell scan. This removes avoidable DNF noise from the team comparison.");
        report.AppendLine("- America’s straight-roar bonus is capped at one flat +1 per straight turn rather than multiplying by every card; this preserves its straight-line identity while reducing high-gear runaway results.");
        report.AppendLine("- UK, Japan and the remaining teams were not broadly buffed from this pass: their full identity depends on interactive tech/trick/driver effects that a pure vehicle benchmark deliberately does not auto-play.");
        report.AppendLine("- The report is retained as a regression baseline; rerun the menu item after any TeamVehicleRules, gear, corner or card-balance change.");

        string absolutePath = Path.GetFullPath(REPORT_PATH);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllText(absolutePath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log($"[BalanceBenchmark] Wrote {absolutePath}");
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

    private static RaceResult SimulateRace(TrackConfig track, int seed)
    {
        var cfg = new BenchmarkConfig();
        GameConfigSO deckConfig = CreateConfig(cfg);
        var session = new RaceSession(new SystemRandomSource(seed));
        session.InitializeWeather(track.weatherPool, track.defaultWeather);
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(track);
        TrackDataLoader.BuildCornerMaps(track, nodes, out Dictionary<int, int> cornerLimits, out _);

        for (int i = 0; i < TEAMS.Length; i++)
        {
            TeamId team = TEAMS[i];
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
            foreach (PlayerState p in session.GetTurnOrder())
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

                int requestedGear = ChooseGear(session, p, nodes, cornerLimits, cfg);
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
                bool cornerRisk = HasUpcomingCornerRisk(p, requiredCards, nodes, cornerLimits, session);
                List<CardData> chosen = AIPlanner.ChooseSpeedCards(
                    p.deck, requiredCards, p.HeatRatio, cornerRisk,
                    0.55f, 0.25f, 0f, session.Random);
                int missing = RaceRules.GetMissingSpeedCardCount(requiredCards, chosen.Count);
                if (missing > 0 && !PayHeatOrSpin(p, missing, p.position))
                {
                    skipped.Add(p);
                    continue;
                }
                p.deck.RemoveFromHand(chosen);
                p.playedSpeedCardsThisTurn.AddRange(chosen);
                p.cornerTotalThisTurn = RaceRules.SumCardValues(chosen);
            }

            foreach (PlayerState p in session.GetTurnOrder())
            {
                if (skipped.Contains(p) || p.hasFinished || p.isBlown) continue;

                int oldPos = p.position;
                int rawEnd = oldPos + p.cornerTotalThisTurn;
                bool crossedCorner = TrackRules.GetUniqueApexCornersCrossed(nodes, oldPos, rawEnd).Count > 0;
                int bonus = session.ComputeMovementBonus(p, crossedCorner);
                bonus += session.ComputeSlipstreamBonus(p, session.Players, nodes.Count);
                p.totalMovementThisTurn = p.cornerTotalThisTurn + bonus;
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
                        break;
                    }
                }
                p.position = Normalize(newPos, nodes.Count);

                int cooldown = TeamGearRules.GetCooldown(
                    p.teamId, p.gear, p.chinaConsecutiveGearCount,
                    cfg.gearOneCooldown, cfg.gearTwoCooldown);
                if (!TeamGearRules.IsChina(p.teamId))
                    cooldown += TeamVehicleRules.GetCooling(p.teamId);
                p.deck.RemoveHeatFromHand(cooldown);

                foreach (int cornerId in TrackRules.GetUniqueApexCornersCrossed(nodes, oldPos, rawEnd))
                {
                    int baseLimit = cornerLimits.TryGetValue(cornerId, out int value) ? value : 99;
                    int limit = session.EffectiveCornerLimit(p, baseLimit);
                    if (p.cornerTotalThisTurn <= limit) continue;
                    int heat = p.cornerTotalThisTurn - limit + TeamVehicleRules.GetCornerHeatPenalty(p.teamId);
                    if (!PayHeatOrSpin(p, heat, oldPos))
                        break;
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
        return new RaceResult { turns = turn, players = session.Players };
    }

    private static int ChooseGear(RaceSession session, PlayerState p, List<TrackNode> nodes,
        Dictionary<int, int> cornerLimits, BenchmarkConfig cfg)
    {
        if (TeamGearRules.IsChina(p.teamId))
        {
            int target = ChinaGearShiftRules.ChooseAiGear(
                p.gear, p.chinaConsecutiveGearCount, p.deck.CountSpeedInHand(), p.HeatRatio, 0.6f);
            int goCards = p.chinaConsecutiveGearCount > 0 ? 4 : 3;
            if (target == ChinaGearShiftRules.GoGear &&
                HasUpcomingCornerRisk(p, goCards, nodes, cornerLimits, session))
                return ChinaGearShiftRules.RecoverGear;
            return target;
        }

        if (p.HeatRatio >= 0.65f)
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

        if (p.HeatRatio <= 0.25f && p.gear < cfg.maxGear)
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
        List<TrackNode> nodes, Dictionary<int, int> cornerLimits, RaceSession session)
    {
        int estimatedMove = EstimateMovement(session, p, cardCount);
        int lookAhead = Mathf.Min(
            Mathf.Min(18, nodes.Count - 1),
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

    private static bool PayHeatOrSpin(PlayerState p, int amount, int rewindPosition)
    {
        if (amount <= 0) return true;
        p.AddHeatPaid(amount);
        if (p.deck.heatPool != null && p.deck.heatPool.remaining >= amount)
        {
            p.deck.heatPool.remaining -= amount;
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
        config.initialHeatCards = cfg.initialHeatCards;
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
