using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Paired, deterministic driver-skill benchmark. Every enabled run is replayed
/// with the same track, seed, team order and AI policy as its disabled control;
/// the report therefore measures the marginal effect of one driver's current
/// runtime skill rather than comparing unrelated random races.
/// </summary>
public static class DriverBalanceBenchmark
{
    public const string ReportPath = "design/balance/driver-skill-benchmark-2026-09-08.md";
    public const int RacesPerConfiguration = 4;
    public const int BaseSeed = 20260908;
    private const int MaxTurns = 700;
    private const int SkillLevelOne = 3;
    private const int SkillLevelTwo = 7;

    private static readonly TeamId[] Teams =
    {
        TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP
    };

    public static IReadOnlyList<string> GetDriverIdsForTests()
    {
        var ids = new List<string>(DriverCatalog.All.Count);
        foreach (DriverProfile profile in DriverCatalog.All)
            ids.Add(profile.Id);
        return ids;
    }

    public static IReadOnlyList<int> GetLevelsForTests()
    {
        return new[] { SkillLevelOne, SkillLevelTwo };
    }

    /// <summary>Small result object exposed to EditMode tests and tooling.</summary>
    public readonly struct RaceSnapshot
    {
        public RaceSnapshot(
            string driverId,
            int level,
            bool enabled,
            int rank,
            bool finished,
            bool blown,
            int turns,
            int spins,
            int heatPaid,
            int activations,
            int slipstreamTriggers,
            int slipstreamMovement)
        {
            DriverId = driverId;
            Level = level;
            Enabled = enabled;
            Rank = rank;
            Finished = finished;
            Blown = blown;
            Turns = turns;
            Spins = spins;
            HeatPaid = heatPaid;
            Activations = activations;
            SlipstreamTriggers = slipstreamTriggers;
            SlipstreamMovement = slipstreamMovement;
        }

        public string DriverId { get; }
        public int Level { get; }
        public bool Enabled { get; }
        public int Rank { get; }
        public bool Finished { get; }
        public bool Blown { get; }
        public int Turns { get; }
        public int Spins { get; }
        public int HeatPaid { get; }
        public int Activations { get; }
        public int SlipstreamTriggers { get; }
        public int SlipstreamMovement { get; }
    }

    private sealed class BenchmarkConfig
    {
        public readonly int[] speedCards = { 1, 1, 1, 2, 2, 2, 2, 2, 3, 3, 3, 4 };
        public readonly int heatPoolPerPlayer = 6;
        public readonly int handSize = 7;
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

    private sealed class Aggregate
    {
        public int races;
        public int wins;
        public int finishes;
        public int blown;
        public int totalRank;
        public int totalTurns;
        public int totalSpins;
        public int totalHeat;
        public int totalActivations;
        public int totalSlipstreamTriggers;
        public int totalSlipstreamMovement;

        public void Add(RaceSnapshot result)
        {
            races++;
            totalRank += result.Rank;
            wins += result.Rank == 1 ? 1 : 0;
            finishes += result.Finished ? 1 : 0;
            blown += result.Blown ? 1 : 0;
            totalTurns += result.Turns;
            totalSpins += result.Spins;
            totalHeat += result.HeatPaid;
            totalActivations += result.Activations;
            totalSlipstreamTriggers += result.SlipstreamTriggers;
            totalSlipstreamMovement += result.SlipstreamMovement;
        }

        public float AverageRank => races == 0 ? 0f : (float)totalRank / races;
        public float WinRate => races == 0 ? 0f : (float)wins / races;
        public float FinishRate => races == 0 ? 0f : (float)finishes / races;
        public float DnfRate => races == 0 ? 0f : (float)blown / races;
        public float AverageTurns => finishes == 0 ? 0f : (float)totalTurns / finishes;
        public float AverageSpins => races == 0 ? 0f : (float)totalSpins / races;
        public float AverageHeat => races == 0 ? 0f : (float)totalHeat / races;
        public float AverageActivations => races == 0 ? 0f : (float)totalActivations / races;
        public float AverageSlipstreamTriggers => races == 0 ? 0f : (float)totalSlipstreamTriggers / races;
        public float AverageSlipstreamMovement => races == 0 ? 0f : (float)totalSlipstreamMovement / races;
    }

    [MenuItem("Tools/Run Driver Skill Balance Benchmark")]
    public static void Run()
    {
        string[] trackIds = TrackDataLoader.GetAvailableTrackIds();
        Array.Sort(trackIds, StringComparer.Ordinal);
        var report = new StringBuilder();
        report.AppendLine("# Driver Skill Balance Benchmark");
        report.AppendLine();
        report.AppendLine($"> Deterministic paired benchmark generated {DateTime.Now:yyyy-MM-dd HH:mm:ss}.");
        report.AppendLine($"> {trackIds.Length} available tracks × 12 drivers × 2 levels × {RacesPerConfiguration} paired races.");
        report.AppendLine($"> Base seed `{BaseSeed}`; maximum {MaxTurns} turns per race; level 3 = first active/passive tier, level 7 = two-use/high-tier sample.");
        report.AppendLine(">");
        report.AppendLine("> Each enabled race has a disabled control with the identical seed, track and rotating team insertion order. Positive rank delta means the enabled skill improves average rank (control rank minus enabled rank).");
        report.AppendLine("> The benchmark uses the current runtime activation rules and a deterministic AI policy. It is directional balance evidence, not a prediction of expert human play.");
        report.AppendLine("> Team vehicle bonuses and the same demo technology template are held constant between each pair; one-shot trick/technology choices are not auto-played.");
        report.AppendLine();

        var allRows = new StringBuilder();
        var levelThreeRows = new StringBuilder();
        var levelSevenRows = new StringBuilder();
        foreach (DriverProfile profile in DriverCatalog.All)
        {
            foreach (int level in new[] { SkillLevelOne, SkillLevelTwo })
            {
                var enabled = new Aggregate();
                var control = new Aggregate();
                foreach (string trackId in trackIds)
                {
                    TrackConfig track = TrackDataLoader.LoadConfig(trackId);
                    if (track == null || track.cells == null || track.cells.Length == 0)
                        continue;

                    for (int raceIndex = 0; raceIndex < RacesPerConfiguration; raceIndex++)
                    {
                        int seed = BaseSeed + StableHash(profile.Id) + StableHash(trackId) +
                                   level * 1009 + raceIndex * 7919;
                        int teamOrderOffset = raceIndex % Teams.Length;
                        enabled.Add(SimulateForTests(trackId, profile.Id, level, true, seed, teamOrderOffset));
                        control.Add(SimulateForTests(trackId, profile.Id, level, false, seed, teamOrderOffset));
                    }
                }

                AppendRow(allRows, profile, level, enabled, control);
                (level == SkillLevelOne ? levelThreeRows : levelSevenRows).Append(
                    BuildRow(profile, level, enabled, control));
            }
        }

        report.AppendLine("## Paired result matrix");
        report.AppendLine();
        report.AppendLine("| Driver | Active / passive | Level | Control rank | Enabled rank | Rank delta | Enabled win | Finish | DNF | Activations | Heat paid | Tailwind move |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        report.Append(allRows);
        report.AppendLine();

        report.AppendLine("## Level 3 comparison");
        report.AppendLine();
        report.AppendLine("| Driver | Active / passive | Control rank | Enabled rank | Rank delta | Enabled win | Finish | DNF | Activations | Heat paid | Tailwind move |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        report.Append(levelThreeRows);
        report.AppendLine();

        report.AppendLine("## Level 7 comparison");
        report.AppendLine();
        report.AppendLine("| Driver | Active / passive | Control rank | Enabled rank | Rank delta | Enabled win | Finish | DNF | Activations | Heat paid | Tailwind move |");
        report.AppendLine("|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|");
        report.Append(levelSevenRows);
        report.AppendLine();

        report.AppendLine("## Reading and follow-up");
        report.AppendLine();
        report.AppendLine("- Rank delta is the primary signal. A skill that has a high activation count but little rank delta is giving feedback without materially changing race strength.");
        report.AppendLine("- Compare level 3 and level 7 separately; a high-tier skill should not be judged against the first-tier budget as if both were the same unlock.");
        report.AppendLine("- The five passives currently wired into runtime effects are expected to move paired results: LionsHeart, EngineersTune, InformationSponge, SteadyHeart and AllRounder. The remaining seven passive descriptions are intentionally not granted a simulated effect until their runtime implementation is approved.");
        report.AppendLine("- Schumacher's Perfect Lap is especially sensitive to track corner density: use rank delta together with DNF and heat-paid columns before changing its tier or use count.");
        report.AppendLine("- Re-run this menu item after changing DriverSkillRules, driver activation gates, corner/heat rules or the skill button flow. This generated report is evidence for tuning, not a replacement for a manual skill walkthrough.");

        string absolutePath = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
        File.WriteAllText(absolutePath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        Debug.Log($"[DriverBalanceBenchmark] Wrote {absolutePath}");
    }

    /// <summary>
    /// Runs one complete paired-race condition. This public entry point is
    /// deliberately deterministic so EditMode tests can verify repeatability.
    /// </summary>
    public static RaceSnapshot SimulateForTests(
        string trackId,
        string driverId,
        int level,
        bool enabled,
        int seed,
        int teamOrderOffset = 0)
    {
        if (!DriverCatalog.TryGet(driverId, out DriverProfile targetProfile))
            throw new ArgumentException("Unknown driver id.", nameof(driverId));

        TrackConfig track = TrackDataLoader.LoadConfig(trackId);
        if (track == null || track.cells == null || track.cells.Length == 0)
            throw new ArgumentException("Track config is missing or empty.", nameof(trackId));

        var cfg = new BenchmarkConfig();
        int totalLaps = track.laps > 0 ? track.laps : 3;
        GameConfigSO deckConfig = CreateConfig(cfg, track.laps);
        var session = new RaceSession(new SystemRandomSource(seed));
        session.InitializeWeather(track.weatherPool, track.defaultWeather);
        List<TrackNode> nodes = TrackDataLoader.ConfigToNodes(track);
        TrackDataLoader.BuildCornerMaps(track, nodes, out Dictionary<int, int> cornerLimits, out _);
        var heatPaid = new Dictionary<PlayerState, int>();
        var finishTurns = new Dictionary<PlayerState, int>();
        var slipstreamTriggers = new Dictionary<PlayerState, int>();
        var slipstreamMovement = new Dictionary<PlayerState, int>();

        for (int i = 0; i < Teams.Length; i++)
        {
            TeamId team = Teams[(i + teamOrderOffset) % Teams.Length];
            DriverProfile profile = team == targetProfile.Team
                ? targetProfile
                : DriverCatalog.GetDefaultForTeam(team);
            var player = new PlayerState(team.ToString(), true, TrackRules.FindStartFinishNodeIndex(nodes), 1)
            {
                teamId = team,
                driverId = profile.Id,
                usesChinaGearSystem = TeamGearRules.IsChina(team),
                techState = session.CreateDemoTechState(team)
            };
            TechTreeRules.ResetPerRaceState(player.techState);
            bool isTarget = string.Equals(profile.Id, targetProfile.Id, StringComparison.Ordinal);
            player.driverSkill.Initialize(profile, isTarget && enabled ? level : 0, isTarget && enabled);

            int pool = TeamVehicleRules.GetBaseHeatPoolSize(team, cfg.heatPoolPerPlayer);
            pool = session.EffectiveHeatPoolSize(player, pool);
            player.deck.InitializeDeck(
                deckConfig,
                new HeatPool(pool),
                new SystemRandomSource(seed + i * 97 + 11));
            player.deck.AddTrickCardsToDrawPile(session.CreateInitialTrickCards(team));
            player.deck.DrawToHand(session.EffectiveHandSize(player, cfg.handSize));
            session.Players.Add(player);
            heatPaid[player] = 0;
            slipstreamTriggers[player] = 0;
            slipstreamMovement[player] = 0;
        }

        PlayerState target = null;
        foreach (PlayerState player in session.Players)
            if (player.driverId == targetProfile.Id)
                target = player;

        int weatherRolledLap = 0;
        int turn = 0;
        while (turn < MaxTurns && !session.IsRaceOver())
        {
            turn++;
            foreach (PlayerState player in session.Players)
                session.BeginTurn(player);

            var skipped = new HashSet<PlayerState>();
            List<PlayerState> turnOrder = session.GetTurnOrder();
            foreach (PlayerState player in turnOrder)
            {
                if (player.hasFinished || player.isBlown)
                    continue;
                if (player.skipNextTurn)
                {
                    player.skipNextTurn = false;
                    player.gear = TeamGearRules.IsChina(player.teamId)
                        ? ChinaGearShiftRules.RecoverGear
                        : cfg.minGear;
                    player.chinaConsecutiveGearCount = 0;
                    skipped.Add(player);
                    continue;
                }

                TryActivateTargetSkill(session, player, targetProfile, enabled, track.laps, nodes.Count);
                int requestedGear = ChooseGear(
                    session, player, nodes, cornerLimits, cfg, out _);
                TeamGearRules.Resolution shift = TeamGearRules.Resolve(
                    player.teamId,
                    player.gear,
                    player.chinaConsecutiveGearCount,
                    requestedGear,
                    cfg.minGear,
                    cfg.maxGear,
                    cfg.twoGearShiftHeatCost,
                    cfg.gearOneCooldown,
                    cfg.gearTwoCooldown);
                if (!PayHeatOrSpin(session, player, shift.HeatCost, player.position, heatPaid))
                {
                    skipped.Add(player);
                    continue;
                }
                player.gear = shift.TargetGear;
                player.chinaConsecutiveGearCount = shift.IsChina ? shift.ConsecutiveCount : 0;
                if (shift.AdditionalHeat > 0 &&
                    !PayHeatOrSpin(session, player, shift.AdditionalHeat, player.position, heatPaid))
                {
                    skipped.Add(player);
                    continue;
                }

                if (player.driverSkill.TryConsumePassiveDeckLookahead(out int lookahead))
                    player.deck.DrawBestOfTopPlayableCardsToHand(lookahead);
                if (!player.deck.DrawToHand(session.EffectiveHandSize(player, cfg.handSize)))
                {
                    skipped.Add(player);
                    continue;
                }

                int requiredCards = TeamGearRules.GetSpeedCardCount(
                    player.teamId, player.gear, player.chinaConsecutiveGearCount, 0);
                bool cornerRisk = HasUpcomingCornerRisk(
                    player, requiredCards, nodes, cornerLimits, session, cfg.aiLookAheadNodes);
                List<CardData> chosen = AIPlanner.ChooseSpeedCards(
                    player.deck,
                    requiredCards,
                    player.HeatRatio,
                    cornerRisk,
                    cfg.aiHeatWarningThreshold,
                    cfg.aiCautiousHeatThreshold,
                    0f,
                    session.Random);
                int missing = RaceRules.GetMissingSpeedCardCount(requiredCards, chosen.Count);
                if (missing > 0 && !PayHeatOrSpin(session, player, missing, player.position, heatPaid))
                {
                    skipped.Add(player);
                    continue;
                }
                player.deck.RemoveFromHand(chosen);
                player.playedSpeedCardsThisTurn.AddRange(chosen);
                player.cornerTotalThisTurn = RaceRules.SumCardValues(chosen) +
                    DriverSkillRules.GetSpeedPerCardBonus(player.driverSkill) * chosen.Count;
            }

            foreach (PlayerState player in turnOrder)
            {
                if (RaceTurnRules.IsInactive(player, skipped))
                {
                    player.totalMovementThisTurn = 0;
                    continue;
                }

                int rawEnd = player.position + player.cornerTotalThisTurn;
                bool crossedCorner = TrackRules.GetUniqueApexCornersCrossed(
                    nodes, player.position, rawEnd).Count > 0;
                int bonus = session.ComputeMovementBonus(player, crossedCorner);
                bonus += session.ConsumeItalyCornerExitBonus(player);
                bonus += DriverSkillRules.GetMovementBonus(player.driverSkill, crossedCorner);
                bonus -= DriverSkillRules.GetGutterMovementPenalty(player.driverSkill);
                bonus += player.driverSkill.PassiveMovementBonusThisTurn;
                player.totalMovementThisTurn = Mathf.Max(0, player.cornerTotalThisTurn + bonus);
            }

            var overtakes = new Dictionary<PlayerState, int>();
            foreach (PlayerState player in turnOrder)
            {
                overtakes[player] = RaceTurnRules.IsInactive(player, skipped)
                    ? 0
                    : RaceMovementRules.CountOvertakes(
                        player, turnOrder, nodes.Count, true, RaceTurnRules.ShouldSkip);
                player.totalMovementThisTurn += DriverSkillRules.GetOvertakeBonus(
                    player.driverSkill, overtakes[player]);
            }

            foreach (PlayerState player in turnOrder)
            {
                if (RaceTurnRules.IsInactive(player, skipped))
                    continue;

                int oldPosition = player.position;
                int rawEnd = oldPosition + player.cornerTotalThisTurn;
                int newPosition = oldPosition + player.totalMovementThisTurn;
                AdvanceAndFinish(player, oldPosition, newPosition, nodes, totalLaps, session, finishTurns, turn);

                int cooldown = TeamGearRules.GetCooldown(
                    player.teamId,
                    player.gear,
                    player.chinaConsecutiveGearCount,
                    cfg.gearOneCooldown,
                    cfg.gearTwoCooldown);
                if (!TeamGearRules.IsChina(player.teamId))
                    cooldown += TeamVehicleRules.GetCooling(player.teamId);
                cooldown += player.driverSkill.PassiveCoolingBonusThisTurn;
                if (!DriverSkillRules.IsWeatherImmune(player.driverSkill))
                    cooldown = WeatherRules.ApplyWeatherToCooling(cooldown, session.Weather);
                player.deck.CoolHeat(cooldown);

                HashSet<int> crossedCorners = TrackRules.GetUniqueApexCornersCrossed(
                    nodes, oldPosition, rawEnd);
                bool completedCorner = crossedCorners.Count > 0;
                foreach (int cornerId in crossedCorners)
                {
                    if (player.driverSkill.TryConsumeCornerIgnore())
                        continue;
                    int baseLimit = cornerLimits.TryGetValue(cornerId, out int value) ? value : 99;
                    int limit = session.EffectiveCornerLimit(player, baseLimit);
                    if (player.cornerTotalThisTurn <= limit)
                        continue;

                    int heat = Mathf.Max(1, player.cornerTotalThisTurn - limit);
                    heat += TeamVehicleRules.GetCornerHeatPenalty(player.teamId);
                    heat = DriverSkillRules.ReduceCornerHeat(player.driverSkill, heat);
                    if (!PayHeatOrSpin(session, player, heat, oldPosition, heatPaid))
                    {
                        completedCorner = false;
                        break;
                    }
                }
                session.ArmItalyCornerExitBonus(player, completedCorner);
            }

            var settledMovements = new Dictionary<PlayerState, int>();
            foreach (PlayerState player in session.Players)
                settledMovements[player] = 0;
            var chains = new Dictionary<PlayerState, SlipstreamChainResult>();
            foreach (PlayerState player in turnOrder)
            {
                if (RaceTurnRules.IsInactive(player, skipped))
                    continue;
                SlipstreamChainResult chain = session.ComputeSlipstreamChain(
                    player, session.Players, nodes.Count, settledMovements, 2, turnOrder);
                chains[player] = chain;
                slipstreamTriggers[player] += chain.Steps.Count;
                slipstreamMovement[player] += chain.TotalBonus;
            }
            foreach (PlayerState player in turnOrder)
            {
                if (!chains.TryGetValue(player, out SlipstreamChainResult chain) || chain.TotalBonus <= 0)
                    continue;
                int oldPosition = player.position;
                player.totalMovementThisTurn += chain.TotalBonus;
                AdvanceAndFinish(player, oldPosition, oldPosition + chain.TotalBonus,
                    nodes, totalLaps, session, finishTurns, turn);
            }

            foreach (PlayerState player in session.Players)
            {
                player.driverSkill.ResolvePassiveTurnEnd(
                    overtakes.TryGetValue(player, out int count) ? count : 0);
                if (player.driverSkill.ActivatedThisTurn &&
                    player.driverSkill.Skill == DriverActiveSkillId.FinalSprint)
                {
                    player.spinCounter = Mathf.Min(session.EffectiveSpinMax(player), player.spinCounter + 1);
                    if (player.spinCounter >= session.EffectiveSpinMax(player))
                        player.isBlown = true;
                    else if (player.driverSkill.Tier < 3)
                        player.skipNextTurn = true;
                }
                player.deck.DiscardSpeedCards(player.playedSpeedCardsThisTurn);
                player.playedSpeedCardsThisTurn.Clear();
                player.deck.DiscardPlayableCardsFromHand(player.deck.GetTricksInHand());
                player.deck.RemoveTempCardsFromHand();
            }

            foreach (PlayerState player in session.Players)
            {
                if (player.lap > weatherRolledLap)
                {
                    weatherRolledLap = player.lap;
                    session.RollWeatherForLap();
                    break;
                }
            }
        }

        int rank = target != null ? session.GetRank(target) : 0;
        int targetTurns = target != null && finishTurns.TryGetValue(target, out int finishTurn)
            ? finishTurn
            : turn;
        int activations = target == null || !enabled
            ? 0
            : DriverProgression.GetActiveUsesPerRace(level, targetProfile.Team) - target.driverSkill.UsesRemaining;
        UnityEngine.Object.DestroyImmediate(deckConfig);
        return new RaceSnapshot(
            driverId,
            level,
            enabled,
            rank,
            target != null && target.hasFinished,
            target != null && target.isBlown,
            targetTurns,
            target != null ? target.spinCounter : 0,
            target != null ? heatPaid[target] : 0,
            activations,
            target != null ? slipstreamTriggers[target] : 0,
            target != null ? slipstreamMovement[target] : 0);
    }

    private static void TryActivateTargetSkill(
        RaceSession session,
        PlayerState player,
        DriverProfile targetProfile,
        bool enabled,
        int totalLaps,
        int totalNodes)
    {
        if (!enabled || player.driverId != targetProfile.Id)
            return;

        HeatGaugeState gauge = HeatGaugeRules.Evaluate(player.deck);
        int nearby = CountNearbyOpponentsBehind(player, session.Players, totalNodes, 3);
        var context = new DriverSkillActivationContext(
            true,
            player.lap,
            totalLaps,
            gauge.EngineRemaining,
            gauge.Capacity,
            nearby);
        player.driverSkill.TryActivate(targetProfile, context, out _);
    }

    private static int CountNearbyOpponentsBehind(
        PlayerState player,
        IReadOnlyList<PlayerState> players,
        int totalNodes,
        int range)
    {
        int count = 0;
        foreach (PlayerState opponent in players)
        {
            if (opponent == null || opponent == player || opponent.isBlown || opponent.hasFinished ||
                opponent.lap != player.lap)
                continue;
            int distance = RaceSession.ForwardDistance(opponent.position, player.position, totalNodes);
            if (distance > 0 && distance <= range)
                count++;
        }
        return count;
    }

    private static void AdvanceAndFinish(
        PlayerState player,
        int oldPosition,
        int newPosition,
        IReadOnlyList<TrackNode> nodes,
        int totalLaps,
        RaceSession session,
        Dictionary<PlayerState, int> finishTurns,
        int turn)
    {
        for (int position = oldPosition + 1; position <= newPosition; position++)
        {
            int index = Normalize(position, nodes.Count);
            if (!nodes[index].isStartFinish)
                continue;
            player.lap++;
            session.OnNewLap(player);
            if (player.lap >= totalLaps)
            {
                player.hasFinished = true;
                session.AssignFinish(player);
                finishTurns[player] = turn;
                break;
            }
        }
        player.position = Normalize(newPosition, nodes.Count);
    }

    private static int ChooseGear(
        RaceSession session,
        PlayerState player,
        List<TrackNode> nodes,
        Dictionary<int, int> cornerLimits,
        BenchmarkConfig cfg,
        out bool forcedRecover)
    {
        forcedRecover = false;
        if (TeamGearRules.IsChina(player.teamId))
        {
            int target = ChinaGearShiftRules.ChooseAiGear(
                player.gear,
                player.chinaConsecutiveGearCount,
                player.deck.CountSpeedInHand(),
                player.HeatRatio,
                cfg.aiHeatWarningThreshold);
            ChinaGearShiftRules.Result go = ChinaGearShiftRules.Resolve(
                player.gear, player.chinaConsecutiveGearCount, target);
            int projectedHeat = ProjectedCornerHeat(player, go.SpeedCardCount, nodes, cornerLimits, session, cfg.aiLookAheadNodes);
            int missing = RaceRules.GetMissingSpeedCardCount(go.SpeedCardCount, player.deck.CountSpeedInHand());
            int committed = go.AdditionalHeat + missing;
            if (target == ChinaGearShiftRules.GoGear &&
                ChinaGearShiftRules.ShouldForceRecoverForCorner(
                    projectedHeat, committed, player.deck.heatPool.remaining, 1))
            {
                forcedRecover = true;
                return ChinaGearShiftRules.RecoverGear;
            }
            return target;
        }

        if (player.HeatRatio >= cfg.aiHeatWarningThreshold)
            return Mathf.Max(cfg.minGear, player.gear - 1);
        if (player.deck.CountSpeedInHand() < player.gear)
            return Mathf.Max(cfg.minGear, player.deck.CountSpeedInHand());

        int estimate = EstimateMovement(player, player.gear);
        int lookAhead = Mathf.Min(Mathf.Min(cfg.aiLookAheadNodes, nodes.Count - 1), Mathf.Max(1, estimate));
        for (int offset = 1; offset <= lookAhead; offset++)
        {
            TrackNode node = nodes[Normalize(player.position + offset, nodes.Count)];
            if (node.cornerId <= 0 || !cornerLimits.TryGetValue(node.cornerId, out int limit))
                continue;
            if (estimate > session.EffectiveCornerLimit(player, limit, false))
                return Mathf.Max(cfg.minGear, player.gear - 1);
        }

        if (player.HeatRatio <= cfg.aiAggressiveHeatThreshold && player.gear < cfg.maxGear)
            return player.gear + 1;
        return player.gear;
    }

    private static int EstimateMovement(PlayerState player, int cardCount)
    {
        return RaceRules.SumCardValues(player.deck.GetTopNSpeedCards(cardCount));
    }

    private static bool HasUpcomingCornerRisk(
        PlayerState player,
        int cardCount,
        List<TrackNode> nodes,
        Dictionary<int, int> cornerLimits,
        RaceSession session,
        int configuredLookAhead)
    {
        int estimatedMove = EstimateMovement(player, cardCount);
        int lookAhead = Mathf.Min(Mathf.Min(configuredLookAhead, nodes.Count - 1), Mathf.Max(1, estimatedMove));
        for (int offset = 1; offset <= lookAhead; offset++)
        {
            TrackNode node = nodes[Normalize(player.position + offset, nodes.Count)];
            if (node.cornerId <= 0 || !cornerLimits.TryGetValue(node.cornerId, out int limit))
                continue;
            if (estimatedMove > session.EffectiveCornerLimit(player, limit, false))
                return true;
        }
        return false;
    }

    private static int ProjectedCornerHeat(
        PlayerState player,
        int cardCount,
        List<TrackNode> nodes,
        Dictionary<int, int> cornerLimits,
        RaceSession session,
        int configuredLookAhead)
    {
        int estimatedMove = RaceRules.SumCardValues(player.deck.GetBottomNSpeedCards(cardCount));
        int lookAhead = Mathf.Min(Mathf.Min(configuredLookAhead, nodes.Count - 1), Mathf.Max(1, estimatedMove));
        int projectedHeat = 0;
        var visited = new HashSet<int>();
        for (int offset = 1; offset <= lookAhead; offset++)
        {
            TrackNode node = nodes[Normalize(player.position + offset, nodes.Count)];
            if (node.cornerId <= 0 || !visited.Add(node.cornerId) || !cornerLimits.TryGetValue(node.cornerId, out int limit))
                continue;
            projectedHeat += Mathf.Max(0, estimatedMove - session.EffectiveCornerLimit(player, limit, false));
        }
        return projectedHeat;
    }

    private static bool PayHeatOrSpin(
        RaceSession session,
        PlayerState player,
        int amount,
        int rewindPosition,
        Dictionary<PlayerState, int> heatPaid)
    {
        if (amount <= 0)
            return true;

        int adjusted = DriverSkillRules.ApplyHeatMultiplier(player.driverSkill, amount);
        adjusted = Mathf.Max(0, adjusted - player.driverSkill.ConsumePassiveHeatDiscount());
        int drawn = player.deck.DrawHeatFromPoolToHand(adjusted);
        if (drawn >= adjusted)
        {
            heatPaid[player] += drawn;
            return true;
        }

        player.spinCounter++;
        player.deck.RecoverAllHeatToPool();
        player.position = rewindPosition;
        player.gear = TeamGearRules.IsChina(player.teamId)
            ? ChinaGearShiftRules.RecoverGear
            : 1;
        player.chinaConsecutiveGearCount = 0;
        player.skipNextTurn = true;
        if (player.spinCounter >= session.EffectiveSpinMax(player))
            player.isBlown = true;
        return false;
    }

    private static void AppendRow(
        StringBuilder allRows,
        DriverProfile profile,
        int level,
        Aggregate enabled,
        Aggregate control)
    {
        allRows.Append(BuildRow(profile, level, enabled, control, true));
    }

    private static string BuildRow(
        DriverProfile profile,
        int level,
        Aggregate enabled,
        Aggregate control,
        bool includeLevel = false)
    {
        float rankDelta = control.AverageRank - enabled.AverageRank;
        string label = $"{profile.ShortName} ({profile.Id})";
        string activePassive = $"{profile.ActiveName} / {profile.PassiveName}";
        string levelPart = includeLevel ? $"{level} | " : string.Empty;
        return $"| {label} | {activePassive} | {levelPart}{control.AverageRank:F2} | {enabled.AverageRank:F2} | {rankDelta:+0.00;-0.00;0.00} | {enabled.WinRate:P0} | {enabled.FinishRate:P0} | {enabled.DnfRate:P0} | {enabled.AverageActivations:F2} | {enabled.AverageHeat:F1} | {enabled.AverageSlipstreamMovement:F1} |{Environment.NewLine}";
    }

    private static GameConfigSO CreateConfig(BenchmarkConfig cfg, int totalLaps)
    {
        GameConfigSO config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.speedCardDistribution = (int[])cfg.speedCards.Clone();
        config.heatPoolPerPlayer = cfg.heatPoolPerPlayer;
        config.handSize = cfg.handSize;
        config.totalLaps = totalLaps > 0 ? totalLaps : 3;
        return config;
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
}
