using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. D04 Word 242/246/250/255/258.
/// Real Manager/Session native-source, owner-input and after-draw adapters. Movement
/// endpoint and movement ledger are advanced at the adapter seam; animation/React ordering is proven by
/// the separate native GameLoop case. No direct runtime RecordSource fabrication.
/// </summary>
public class VettelFeedbackIntegrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Audio = typeof(AudioService).GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    private object previousAudio;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession race;
    private PlayerState player;
    private RaceInputState Input => Get<RaceInputState>(manager, "inputState");
    private RacePhaseState Phase => Get<RacePhaseState>(manager, "phaseState");

    [SetUp]
    public void SetUp()
    {
        previousAudio = Audio.GetValue(null); Audio.SetValue(null, null);
        host = new GameObject("Owned Vettel source adapters"); host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>(); config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true; config.enableWeather = false; config.totalLaps = 99;
        config.minGear = 1; config.maxGear = 4; config.gearOneCooldown = config.gearTwoCooldown = 0;
        manager.config = config; manager.trackManager = host.AddComponent<TrackManager>(); manager.trackManager.config = config;
        Set(manager.trackManager, "nodes", Enumerable.Range(0, 60).Select(i => new TrackNode(i, 99)).ToList());
        Set(manager.trackManager, "cornerSpeedLimits", new Dictionary<int, int> { { 1, 99 } });
        Set(manager.trackManager, "cornerNames", new Dictionary<int, string> { { 1, "Owned safe corner" } });
        race = new RaceSession(new SystemRandomSource(91)) { TeamVehicleBonusesEnabled = true }; Set(manager, "session", race);
    }

    [TearDown]
    public void TearDown()
    {
        try { if (host != null) Object.DestroyImmediate(host); if (config != null) Object.DestroyImmediate(config); }
        finally { Audio.SetValue(null, previousAudio); }
    }

    private void Create(int level = 2, int value = 1, int oldHeat = 2)
    {
        player = new PlayerState("Owned Vettel", false, 8, 1)
        { driverId = "de_sebastian_vettel", teamId = TeamId.DE, lap = 1, techState = new TechTreeState(TeamId.DE) };
        player.deck.InitializeExactOrder(Enumerable.Range(0, 40).Select(_ => new CardData(CardType.Speed, value)).ToArray(), new HeatPool(12));
        player.deck.DrawToHand(4); player.deck.DrawHeatFromPoolToHand(oldHeat);
        player.driverSkill.Initialize(player.DriverProfile, level, true); race.Players.Add(player); Begin(1);
    }
    private void Begin(int turn)
    {
        race.BeginTurn(player, turn); race.BeginDriverSkillInput(player, 60);
        Set(manager, "raceTurnNumber", turn); Phase.BeginGearSelection(); Input.BeginGearSelection(player.gear);
    }
    private void PlaySpeed(int count = 1)
    {
        var cards = player.deck.Hand.Where(c => c.IsSpeed).Take(count).ToList(); Assert.That(cards.Count, Is.EqualTo(count));
        Assert.That(CardPlayRules.CommitSpeedCards(player, cards, count, race, 60), Is.EqualTo(SpeedCardCommitResult.Success));
    }
    private void Compute() => Invoke("ComputeMovements", new List<PlayerState> { player }, new HashSet<PlayerState>());
    private void CompletePlannedMovement(bool completedCorner = false)
    {
        int old = player.position, lap = player.lap, absoluteEnd = old + player.totalMovementThisTurn;
        player.turnMovement.Advance(player.totalMovementThisTurn); // Explicit synthetic adapter settlement, not animation.
        player.position = absoluteEnd % 60; player.lap += absoluteEnd / 60;
        Invoke("ResolveVettelFeedbackAfterMovement", player, old, lap, completedCorner);
    }
    private void Cruise()
    {
        PlaySpeed(); Compute(); CompletePlannedMovement();
    }
    private int NextAfterDraw(int turn = 2)
    {
        Invoke("CleanupTurn", player); Begin(turn);
        player.deck.DrawToHand(8); return race.ResolveVettelFeedbackAfterDraw(player);
    }

    [TestCase(2)] [TestCase(4)] [TestCase(6)]
    public void RealSettledCruisePreparesNextOwnerInputAndReturnsOnlyOneOldHandOriginal(int level)
    {
        Create(level); var old = player.deck.Hand.Where(c => c.IsHeat).ToArray(); int uses = player.driverSkill.UsesRemaining;
        PlaySpeed(); Compute();
        Assert.That(player.driverSkill.Feedback.HasPending, Is.False, "Movement planning is not a settled native source.");
        CompletePlannedMovement();
        Assert.That(player.driverSkill.Feedback.HasPending, Is.True);
        Assert.That(player.driverSkill.Feedback.PendingSource, Is.EqualTo(VettelNativeSource.BasicCruise));
        Assert.That(race.ResolveVettelFeedbackAfterDraw(player), Is.Zero, "Source input cannot collect its own feedback.");
        Assert.That(NextAfterDraw(), Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
        Assert.That(old.Count(player.deck.ContainsInHand), Is.EqualTo(1));
        Assert.That(player.driverSkill.Feedback.LastRestored, Is.EqualTo(1)); Assert.That(player.driverSkill.Feedback.LastRetired, Is.Zero);
        Assert.That(race.ResolveVettelFeedbackAfterDraw(player), Is.Zero);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses)); Assert.That(player.driverSkill.IsActive, Is.False);
        Assert.That(player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(12));
    }

    [TestCase("chassis-only")] [TestCase("crossed-corner")] [TestCase("vehicle-disabled")]
    [TestCase("no-formal-card")] [TestCase("disabled-skill")] [TestCase("locked-passive")] [TestCase("ai")]
    public void IneligibleOrZeroNativeCruiseDoesNotPrepareFeedback(string boundary)
    {
        Create(value: boundary == "chassis-only" ? 2 : 1);
        if (boundary == "crossed-corner") Get<List<TrackNode>>(manager.trackManager, "nodes")[9] = new TrackNode(9, 99, "Cruise not straight", 1, false, true);
        if (boundary == "vehicle-disabled") race.TeamVehicleBonusesEnabled = false;
        if (boundary == "disabled-skill") player.driverSkill.Initialize(player.DriverProfile, 2, false);
        if (boundary == "locked-passive") player.driverSkill.Initialize(player.DriverProfile, 1, true);
        if (boundary == "ai") player.isAI = true;
        if (boundary != "no-formal-card") PlaySpeed();
        Compute(); CompletePlannedMovement(boundary == "crossed-corner");
        Assert.That(player.driverSkill.Feedback.HasPending, Is.False, boundary);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
    }

    [TestCase("position")] [TestCase("lap")] [TestCase("partial-move")] [TestCase("new-input")] [TestCase("position-only")]
    public void SettledSourceRejectsMismatchedFrozenMovementBoundary(string mismatch)
    {
        Create(); PlaySpeed(); Compute(); int old = player.position, lap = player.lap;
        player.position += player.totalMovementThisTurn;
        player.turnMovement.Advance(mismatch == "position-only" ? 0 : player.totalMovementThisTurn - (mismatch == "partial-move" ? 1 : 0));
        if (mismatch == "partial-move") player.position--;
        if (mismatch == "new-input") Begin(2);
        Invoke("ResolveVettelFeedbackAfterMovement", player, old + (mismatch == "position" ? 1 : 0), lap + (mismatch == "lap" ? 1 : 0), false);
        Assert.That(player.driverSkill.Feedback.HasPending, Is.False);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
    }

    [TestCase(2, false)] [TestCase(4, true)] [TestCase(6, true)]
    public void RealManagerBeerPaymentRegistersOnlyAllowedPassiveTierAndNeverCoolsItsDiscardReceipt(int level, bool expected)
    {
        Create(level, 2); player.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel");
        Phase.BeginCardSelection(); Input.BeginCardSelection();
        Assert.That(manager.TryPrepareGermanFuelRequest(out var plan, out string reason), Is.True, reason);
        Assert.That(player.driverSkill.Feedback.HasPending, Is.False, "Unpaid preview does not count.");
        Assert.That(manager.TryConfirmGermanFuelRequest(plan, out reason), Is.True, reason);
        Assert.That(player.deck.DiscardPile.Contains(plan.PaidHeat), Is.True);
        Assert.That(player.driverSkill.Feedback.HasPending, Is.EqualTo(expected));
        Assert.That(NextAfterDraw(), Is.EqualTo(expected ? 1 : 0));
        Assert.That(player.deck.DiscardPile.Contains(plan.PaidHeat), Is.True, "Feedback never retrieves the fixed discard fee.");
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(expected ? 10 : 9));
    }

    [TestCase("cancel")] [TestCase("empty-engine")] [TestCase("stale")]
    public void UnpaidOrExpiredRealBeerRequestCannotPrepareFeedback(string reasonKind)
    {
        Create(4, 2); player.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel");
        Phase.BeginCardSelection(); Input.BeginCardSelection();
        if (reasonKind == "empty-engine")
        {
            player.deck.DrawHeatFromPoolToHand(player.deck.heatPool.remaining);
            Assert.That(manager.TryPrepareGermanFuelRequest(out _, out _), Is.False);
        }
        else
        {
            Assert.That(manager.TryPrepareGermanFuelRequest(out var plan, out string reason), Is.True, reason);
            if (reasonKind == "cancel") manager.CancelGermanFuelRequest(plan);
            else player.deck.DrawHeatFromPoolToHand(1);
            int engine = player.deck.heatPool.remaining;
            Assert.That(manager.TryConfirmGermanFuelRequest(plan, out _), Is.False);
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
        }
        Assert.That(player.driverSkill.Feedback.HasPending, Is.False);
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.Zero);
    }

    [Test]
    public void DeclaredThreeRealBeerPaymentsPrepareOnlyOneFeedbackAndDoNotRefreshFuelBudget()
    {
        Create(6, 2);
        player.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l3-grill-spezial" });
        Assert.That(race.TryDeclareGermanBurst(player, true, 1), Is.True);
        Phase.BeginCardSelection(); Input.BeginCardSelection();
        for (int i = 0; i < 3; i++)
        {
            Assert.That(manager.TryPrepareGermanFuelRequest(out var plan, out string reason), Is.True, reason);
            Assert.That(manager.TryConfirmGermanFuelRequest(plan, out reason), Is.True, reason);
            Assert.That(player.driverSkill.Feedback.HasPending, Is.True);
        }
        Assert.That(player.techState.germanBurst.FuelUsesThisTurn, Is.EqualTo(3));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(7));
        Assert.That(manager.TryPrepareGermanFuelRequest(out _, out _), Is.False);
        Assert.That(NextAfterDraw(), Is.EqualTo(1));
        Assert.That(player.driverSkill.Feedback.HasPending, Is.False);
        Assert.That(player.techState.grillSpezialUsed, Is.True);
        Assert.That(player.techState.schwarzbierFuelLastLap, Is.EqualTo(1));
        Phase.BeginCardSelection(); Input.BeginCardSelection();
        Assert.That(manager.TryPrepareGermanFuelRequest(out _, out _), Is.False, "Feedback cooling does not reopen same-lap beer allowance.");
    }
    [TestCase(4, false)] [TestCase(6, true)]
    public void RealSettledGermanCornerExitRegistersOnlyTierThree(int level, bool expected)
    {
        Create(level, 2); player.techState.activeNodeIds.Add("de-l2-wurstplatte");
        Get<List<TrackNode>>(manager.trackManager, "nodes")[9] = new TrackNode(9, 99, "Owned exit source", 1, false, true);
        PlaySpeed(); Compute(); Assert.That(player.driverSkill.Feedback.HasPending, Is.False);
        CompletePlannedMovement(true);
        Assert.That(player.driverSkill.Feedback.HasPending, Is.EqualTo(expected));
        if (expected) Assert.That(player.driverSkill.Feedback.PendingSource, Is.EqualTo(VettelNativeSource.GermanCornerExit));
        Assert.That(NextAfterDraw(), Is.EqualTo(expected ? 1 : 0));
    }

    [Test]
    public void OncePerLapFeedbackDoesNotRenewAfterConsumptionAndNewLapCanPrepareAgain()
    {
        Create(6); Cruise(); Assert.That(NextAfterDraw(), Is.EqualTo(1));
        Cruise(); Assert.That(player.driverSkill.Feedback.HasPending, Is.False);
        Invoke("CleanupTurn", player); player.lap = 2; Begin(3); player.deck.DrawToHand(8);
        Cruise(); Assert.That(player.driverSkill.Feedback.HasPending, Is.True);
        Assert.That(player.driverSkill.Feedback.HasTriggeredInLap(1) && player.driverSkill.Feedback.HasTriggeredInLap(2), Is.True);
        Assert.That(NextAfterDraw(4), Is.EqualTo(1)); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(12));
    }

    [Test]
    public void SourceCrossingFinishConsumesSourceLapRatherThanNewLap()
    {
        Create(); player.position = 59; Cruise();
        Assert.That(player.lap, Is.EqualTo(2)); Assert.That(player.driverSkill.Feedback.HasTriggeredInLap(1), Is.True);
        Assert.That(player.driverSkill.Feedback.HasTriggeredInLap(2), Is.False);
    }

    [Test]
    public void NonzeroFinishCrossingUsesActualCellLedgerAndOriginalSourceLap()
    {
        Create(); player.position = 10;
        Get<List<TrackNode>>(manager.trackManager, "nodes")[11].isStartFinish = true;
        Assert.That(manager.trackManager.StartFinishNodeIndex, Is.EqualTo(11));
        PlaySpeed(); Compute();
        player.position = 13; player.lap = 2; player.turnMovement.Advance(3);
        Invoke("ResolveVettelFeedbackAfterMovement", player, 10, 1, false);
        Assert.That(player.driverSkill.Feedback.HasPending, Is.True);
        Assert.That(player.driverSkill.Feedback.HasTriggeredInLap(1), Is.True);
        Assert.That(player.driverSkill.Feedback.HasTriggeredInLap(2), Is.False);
    }

    [Test]
    public void ActualTemporaryValueOneSpeedKeepsNativeConversionAndCanPreparePermanentOldHeatFeedback()
    {
        Create(value: 2); var temporarySpeed = new CardData(CardType.Speed, 1) { isTemp = true };
        player.deck.AddCardsToHand(new[] { temporarySpeed });
        Assert.That(CardPlayRules.CommitSpeedCards(player, new[] { temporarySpeed }, 1), Is.EqualTo(SpeedCardCommitResult.Success));
        Compute(); Assert.That(player.totalMovementThisTurn, Is.EqualTo(3)); CompletePlannedMovement();
        Assert.That(player.driverSkill.Feedback.HasPending, Is.True);
        Assert.That(player.driverSkill.Feedback.PendingSource, Is.EqualTo(VettelNativeSource.BasicCruise));
        Assert.That(NextAfterDraw(), Is.EqualTo(1)); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(11));
        Assert.That(temporarySpeed.type, Is.EqualTo(CardType.Speed)); Assert.That(temporarySpeed.value, Is.EqualTo(1));
    }
    [TestCase("empty-at-A1")] [TestCase("ordinary-cooled")] [TestCase("discarded")]
    public void AfterDrawCannotReplaceMissingFrozenOldHeatWithNewOrTemporaryHeat(string missing)
    {
        Create(oldHeat: missing == "empty-at-A1" ? 0 : 1); Cruise(); Invoke("CleanupTurn", player); Begin(2);
        var old = player.deck.Hand.FirstOrDefault(c => c.IsHeat);
        if (missing == "ordinary-cooled") { Invoke("ReactStep", player); Assert.That(player.deck.ContainsInHand(old), Is.False); }
        if (missing == "discarded")
        {
            player.deck.RemoveFromHand(new List<CardData> { old }); player.deck.DiscardPlayedSpeedCards(new List<CardData> { old });
        }
        player.deck.DrawToHand(8); player.deck.DrawHeatFromPoolToHand(1); var fresh = player.deck.Hand.Last();
        var temporary = CardData.CreateTempHeat(); player.deck.AddCardsToHand(new[] { temporary });
        int engine = player.deck.heatPool.remaining;
        Assert.That(race.ResolveVettelFeedbackAfterDraw(player), Is.Zero);
        Assert.That(player.deck.ContainsInHand(fresh) && player.deck.ContainsInHand(temporary), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine)); Assert.That(player.driverSkill.Feedback.HasPending, Is.False);
        if (missing == "discarded") Assert.That(player.deck.DiscardPile.Contains(old), Is.True);
    }

    [TestCase("pit")] [TestCase("skip")] [TestCase("finished")] [TestCase("blown")] [TestCase("exit")]
    public void ProductionLifecycleEndsFeedbackWithoutCoolingOrChangingSpentUses(string boundary)
    {
        Create(); Cruise(); int uses = player.driverSkill.UsesRemaining;
        if (boundary == "exit") Invoke("OnDisable");
        else
        {
            if (boundary == "pit") player.pitStopScheduled = true;
            if (boundary == "skip") player.skipNextTurn = true;
            if (boundary == "finished") player.hasFinished = true;
            if (boundary == "blown") player.isBlown = true;
            Invoke("CleanupTurn", player);
        }
        Assert.That(player.driverSkill.Feedback.HasPending, Is.False); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [Test]
    public void SameTeamOtherParticipantCannotPrepareOrConsumeOwnersFeedback()
    {
        Create();
        var other = new PlayerState("Other DE", false, 20, 1) { teamId = TeamId.DE, driverId = "de_sebastian_vettel", lap = 1 };
        other.driverSkill.Initialize(other.DriverProfile, 6, true);
        other.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(6)); other.deck.DrawToHand(1);
        race.Players.Add(other); race.BeginTurn(other, 1); race.BeginDriverSkillInput(other, 60);
        Assert.That(CardPlayRules.CommitSpeedCards(other, new[] { other.deck.Hand[0] }, 1), Is.EqualTo(SpeedCardCommitResult.Success));
        Invoke("ComputeMovements", new List<PlayerState> { other }, new HashSet<PlayerState>());
        int old = other.position; other.position += other.totalMovementThisTurn; other.turnMovement.Advance(other.totalMovementThisTurn);
        Invoke("ResolveVettelFeedbackAfterMovement", other, old, 1, false);
        Assert.That(player.driverSkill.Feedback.HasPending, Is.False); Assert.That(other.driverSkill.Feedback.HasPending, Is.True);
        Assert.That(race.ResolveVettelFeedbackAfterDraw(player), Is.Zero); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(10));
    }

    [Test]
    public void ForecastCannotCreateRealFeedbackOrConsumePendingOldHeatOrMutateSharedCardValues()
    {
        Create(); var selected = player.deck.Hand.Where(c => c.IsSpeed).Take(1).ToArray();
        for (int i = 0; i < 2; i++)
        {
            var forecast = RaceTurnPreviewRules.Evaluate(player, race, config, selected, false, 1, totalCells: 60);
            Assert.That(forecast.Movement.TotalCells, Is.EqualTo(3));
            Assert.That(player.driverSkill.Feedback.HasPending, Is.False); Assert.That(selected[0].value, Is.EqualTo(1));
        }
        Cruise(); Invoke("CleanupTurn", player); Begin(2); player.deck.DrawToHand(8);
        var hand = player.deck.Hand.ToArray(); int engine = player.deck.heatPool.remaining;
        for (int i = 0; i < 2; i++)
        {
            RaceTurnPreviewRules.Evaluate(player, race, config, player.deck.Hand.Where(c => c.IsSpeed).Take(1).ToArray(), false, 1, totalCells: 60);
            CollectionAssert.AreEqual(hand, player.deck.Hand); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine));
            Assert.That(player.driverSkill.Feedback.HasPending, Is.True);
        }
        Assert.That(race.ResolveVettelFeedbackAfterDraw(player), Is.EqualTo(1));
    }

    [Test]
    public void FourOwnerInputsNeverUseRetiredLookaheadAndNewRhythmCannotWaiveOrdinaryShift()
    {
        Create(5, 2, 0); int uses = player.driverSkill.UsesRemaining;
        for (int turn = 1; turn <= 4; turn++)
        {
            if (turn > 1) { Invoke("CleanupTurn", player); Begin(turn); }
            Assert.That(player.driverSkill.TryConsumePassiveDeckLookahead(out int lookahead), Is.False); Assert.That(lookahead, Is.Zero);
        }
        Assert.That(manager.TryPrepareVettelRhythmRequest(out var plan, out string reason), Is.True, reason);
        Assert.That(manager.TryConfirmVettelRhythmRequest(plan, VettelNativeSource.BasicCruise, out reason), Is.True, reason);
        Assert.That(player.driverSkill.IsActive, Is.True);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        int engine = player.deck.heatPool.remaining; Invoke("ApplyGearShift", player, 3);
        Assert.That(player.gear, Is.EqualTo(3)); Assert.That(player.deck.heatPool.remaining, Is.EqualTo(engine - 1));
    }

    private object Invoke(string name, params object[] args)
    {
        var method = typeof(MVPGameManager).GetMethod(name, Private); Assert.That(method, Is.Not.Null, name);
        return method.Invoke(manager, args);
    }
    private static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
}

/// <summary>UI; production/qa/evidence; ADVISORY. Only D04 passive overflow is accepted here.</summary>
public class VettelFeedbackProfileUiTests
{
    [TestCase(1920, 1080)] [TestCase(2560, 1440)] [TestCase(1280, 720)]
    public void RealProductionD04PassiveSummaryFitsThreeRequiredSizes(int width, int height)
    {
        var record = SchumacherPassiveProfileUiCapture.Exercise(width, height, true);
        Assert.That(DriverCatalog.All.First(d => d.Id == "de_sebastian_vettel").PassiveSummary, Does.Contain("旧手永久热"));
        Assert.That(record.allOverflows.Where(issue => issue.Contains("Driver_de_sebastian_vettel/Passive:")), Is.Empty,
            "Only changed D04 passive is accepted; unrelated existing full-page overflows remain outside this gate.");
        TestContext.WriteLine("D04 passive fits " + width + "x" + height + "; other profile overflow count=" + record.allOverflows.Count);
    }
}
