using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Actual coordinator cleanup adapter + session turn reset, without scene startup or persistence.</summary>
public class RaceTurnBoundaryIntegrationTests
{
    private GameObject host;
    private MVPGameManager manager;
    private RaceSession session;
    private const BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Turn boundary integration");
        host.SetActive(false); // Do not run Start, load scenes, or initialize a real race.
        manager = host.AddComponent<MVPGameManager>();
        session = new RaceSession(new SystemRandomSource(1));
        FieldInfo field = typeof(MVPGameManager).GetField("session", PrivateInstance);
        Assert.IsNotNull(field, "Coordinator session adapter changed; update this integration fixture.");
        field.SetValue(manager, session);
    }

    [TearDown]
    public void TearDown()
    {
        if (host != null) Object.DestroyImmediate(host);
    }

    private PlayerState CreatePlayer(TeamId team)
    {
        var player = new PlayerState("driver", false, 8, team == TeamId.CN ? 2 : 1)
        {
            teamId = team, techState = new TechTreeState(team, 0),
            italyCornerExitBoostReady = true
        };
        player.deck.InitializeExactOrder(new[]
        {
            new CardData(CardType.Speed, 3), new CardData(CardType.Speed, 1), new CardData(CardType.Speed, 2)
        }, new HeatPool(6));
        player.deck.DrawToHand(2);
        CardData played = player.deck.Hand[0];
        player.playedSpeedCardsThisTurn.AddRange(player.deck.RemoveFromHand(new List<CardData> { played }));
        player.deck.DrawHeatFromPoolToHand(2);
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        session.Players.Add(player);
        return player;
    }

    private void Cleanup(PlayerState player)
    {
        MethodInfo method = typeof(MVPGameManager).GetMethod("CleanupTurn", PrivateInstance);
        Assert.IsNotNull(method, "Coordinator cleanup adapter changed; update this integration fixture.");
        method.Invoke(manager, new object[] { player });
    }

    [TestCase(HeatPaymentDestination.Hand)]
    [TestCase(HeatPaymentDestination.Discard)]
    public void RealBreadPaymentUsesAdjustedAmountAndRequestedDestination(HeatPaymentDestination target)
    {
        var player = CreatePlayer(TeamId.DE);
        player.trickState.schwarzbrotActive = true;
        player.trickState.schwarzbrotRemaining = 1;
        Assert.IsTrue(manager.TryPayHeat(player, 2, 8, "engine failure", target));
        Assert.AreEqual(3, player.deck.heatPool.remaining);
        Assert.AreEqual(1, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(3, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(target == HeatPaymentDestination.Hand ? 0 : 1, player.deck.CountHeatInDiscardPile());
        Assert.IsFalse(player.trickState.schwarzbrotActive);
    }

    [Test]
    public void RealPassiveFreePaymentLeavesBreadArmedForNextPayment()
    {
        var player = CreatePlayer(TeamId.DE);
        DriverCatalog.TryGet("de_michael_schumacher", out var driver);
        player.driverSkill.Initialize(driver, 2, true);
        for (int i = 0; i < 3; i++) player.driverSkill.BeginTurn();
        player.trickState.schwarzbrotActive = true;
        player.trickState.schwarzbrotRemaining = 1;
        Assert.IsTrue(manager.TryPayHeat(player, 1, 8, "discount"));
        Assert.IsEmpty(player.heatPaidCardsThisTurn);
        Assert.AreEqual(4, player.deck.heatPool.remaining);
        Assert.IsTrue(player.trickState.schwarzbrotActive);
        Assert.IsTrue(manager.TryPayHeat(player, 2, 8, "next payment"));
        Assert.AreEqual(1, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(3, player.deck.heatPool.remaining);
        Assert.IsFalse(player.trickState.schwarzbrotActive);
    }

    [Test]
    public void RealPartialPaymentRetainsFishAndChipsRescueAndActualReceipt()
    {
        var player = CreatePlayer(TeamId.UK);
        session.TechDb = TechTreeDatabaseFactory.CreateDefault();
        player.techState.activeNodeIds.Add("uk-l1-fish-and-chips");
        player.deck.DrawHeatFromPool(3); // Only one engine heat remains.
        Assert.IsTrue(manager.TryPayHeat(player, 2, 8, "short engine"));
        Assert.IsTrue(player.techState.fishAndChipsUsed);
        Assert.AreEqual(1, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(1, player.deck.heatPool.remaining);
        Assert.AreEqual(6, player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(0, player.spinCounter);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealGenericCoolingAdapterReturnsActualCountWithoutChangingPaymentReceipts(bool ai)
    {
        var player = CreatePlayer(TeamId.DE);
        player.isAI = ai;
        player.deck.DrawHeatFromPool(1, HeatPaymentDestination.Discard, player.heatPaidCardsThisTurn);
        var method = typeof(MVPGameManager).GetMethod("CoolHeatWithPresentation", PrivateInstance);
        Assert.IsNotNull(method);
        Assert.AreEqual(4, method.Invoke(manager, new object[] { player, 99 }));
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.AreEqual(0, player.deck.CountTemporaryHeatOutsideEngine());
        Assert.AreEqual(1, player.heatPaidCardsThisTurn.Count);
        Assert.AreEqual(0, player.deck.CountRecordedHeat(player.heatPaidCardsThisTurn));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RealGrillPaymentAndCleanupPreserveOldHeatAndOnlyTriggerOnce(bool ai)
    {
        var player = CreatePlayer(TeamId.DE);
        player.isAI = ai;
        session.TechDb = TechTreeDatabaseFactory.CreateDefault();
        player.techState.activeNodeIds.Add("de-l3-grill-spezial");
        player.deck.DrawHeatFromPool(1); // Previous-turn discard heat is not a receipt.
        Assert.IsTrue(manager.TryPayHeat(player, 2, 8, "regression"));
        Assert.AreEqual(2, player.heatPaidCardsThisTurn.Count);
        Cleanup(player);
        Assert.IsTrue(player.techState.grillSpezialUsed);
        Assert.AreEqual(3, player.deck.heatPool.remaining);
        Assert.AreEqual(2, player.deck.CountHeatInHand()); // Old hand heat remains.
        Assert.AreEqual(1, player.deck.CountHeatInDiscardPile());
        session.BeginTurn(player);
        Assert.IsTrue(manager.TryPayHeat(player, 1, 8, "second turn"));
        Cleanup(player);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        Assert.AreEqual(3, player.deck.CountHeatInHand());
        Assert.AreEqual(6, player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine());
    }

    [TestCase(false, 1)]
    [TestCase(true, 0)]
    public void RealRecoverTechnologyCoolsHandAfterTemporaryCleanup(bool combo, int expectedHandHeat)
    {
        var player = CreatePlayer(TeamId.CN);
        player.gear = 1;
        session.TechDb = TechTreeDatabaseFactory.CreateDefault();
        player.techState.activeNodeIds.UnionWith(new[] { "cn-l1-yin-yang-tea", "cn-l2-dim-sum-combo" });
        player.techState.dimSumPlayedTrick = combo;
        player.techState.dimSumPlayedSpeed = combo;
        player.techState.dimSumPaidHeat = combo;
        Cleanup(player);
        int handHeat = 0;
        foreach (var card in player.deck.Hand) if (card.IsHeat) handHeat++;
        Assert.AreEqual(expectedHandHeat, handHeat);
        Assert.AreEqual(6 - expectedHandHeat, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountTemporaryHeatOutsideEngine());
        Assert.AreEqual(1, player.deck.DiscardPileCount);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(8, player.position);
    }

    [TestCase(3, 0, false, false, 1, false, true)]
    [TestCase(5, 0, false, false, 1, false, true)]
    [TestCase(7, 0, false, false, 1, false, false)]
    [TestCase(7, 0, false, true, 1, false, true)]
    [TestCase(7, 0, true, false, 1, true, false)]
    [TestCase(3, 2, false, false, 3, true, false)]
    public void RealSkillCleanupPreservesFinalSprintCostAndExistingFlags(
        int level, int spins, bool alreadyBlown, bool alreadySkipping,
        int expectedSpins, bool expectedBlown, bool expectedSkip)
    {
        var player = CreatePlayer(TeamId.IT);
        DriverCatalog.TryGet("it_tazio_nuvolari", out DriverProfile profile);
        player.driverSkill.Initialize(profile, level, true);
        Assert.IsTrue(player.driverSkill.TryActivate(profile,
            new DriverSkillActivationContext(true, 0, 3, 0, 6, 0), out _));
        player.spinCounter = spins;
        player.isBlown = alreadyBlown;
        player.skipNextTurn = alreadySkipping;
        // A live technology adapter requires session.TechDb. If the skill blows the engine,
        // the technology adapter must never execute, even with no session available.
        if (expectedBlown)
            typeof(MVPGameManager).GetField("session", PrivateInstance).SetValue(manager, null);
        Cleanup(player);
        Assert.AreEqual(expectedSpins, player.spinCounter);
        Assert.AreEqual(expectedBlown, player.isBlown);
        Assert.AreEqual(expectedSkip, player.skipNextTurn);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(1, player.deck.DiscardPileCount);
        Assert.AreEqual(0, player.deck.CountTemporaryHeatOutsideEngine());
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void RealCleanupThenNextTurnKeepsExactCardOrderAndHeatCapacity(TeamId team)
    {
        var player = CreatePlayer(team);
        CardData played = player.playedSpeedCardsThisTurn[0];
        Cleanup(player);
        Assert.AreSame(played, player.deck.DiscardPile[0]);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(0, player.deck.CountTemporaryHeatOutsideEngine());
        session.BeginTurn(player);
        Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));
        Assert.IsTrue(player.deck.DrawToHand(5));
        var speeds = new List<int>();
        foreach (CardData card in player.deck.Hand)
            if (card.IsSpeed) speeds.Add(card.value);
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, speeds);
        Assert.AreSame(played, player.deck.Hand[4]);
        Assert.AreEqual(0, player.deck.DiscardPileCount);
        Assert.AreEqual(6, player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine());
        Assert.IsTrue(player.italyCornerExitBoostReady, "Persistent exit boost must survive cleanup/reset.");
        Assert.AreEqual(8, player.positionAtTurnStart);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void JapanSkippedTurnCarrySurvivesCleanupAndIsConsumedOnlyOnce(bool techEnabled)
    {
        var player = CreatePlayer(TeamId.JP);
        if (!techEnabled) player.techState = null;
        player.kantoOdenSkipThisTurn = true;
        player.trickState.kantoOdenActive = true;
        player.trickState.kantoOdenAccumulatedCards = 2;
        Assert.AreEqual(RaceTurnStartAction.ResolveSkip, RaceTurnRules.GetStartAction(player));
        Cleanup(player);
        Assert.AreEqual(2, player.trickState.kantoOdenAccumulatedCards);
        session.BeginTurn(player);
        Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));
        Assert.AreEqual(2, player.extraCardSlotsThisTurn);
        Assert.IsFalse(player.trickState.kantoOdenActive);
        session.BeginTurn(player);
        Assert.AreEqual(0, player.extraCardSlotsThisTurn);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void TerminalParticipantsStillCleanCardsAndRemainExcludedNextTurn(bool blown)
    {
        var player = CreatePlayer(TeamId.CN);
        player.isBlown = blown;
        player.hasFinished = !blown;
        player.finishOrder = blown ? 0 : 1;
        player.pitStopScheduled = true;
        Cleanup(player);
        session.BeginTurn(player);
        Assert.AreEqual(RaceTurnStartAction.ExcludeTerminal, RaceTurnRules.GetStartAction(player));
        Assert.AreEqual(blown ? 0 : 1, player.finishOrder);
        Assert.AreEqual(8, player.position);
        Assert.AreEqual(1, player.deck.DiscardPileCount);
        Assert.IsEmpty(player.playedSpeedCardsThisTurn);
        Assert.AreEqual(0, player.deck.CountTemporaryHeatOutsideEngine());
    }

    [Test]
    public void BeginTurnKeepsPitAndRecoveryFlagsAndConsumedSkipEvidence()
    {
        var player = CreatePlayer(TeamId.US);
        player.skipNextTurn = true;
        player.pitStopRequested = true;
        player.pitStopScheduled = true;
        var skipped = new HashSet<PlayerState> { player };
        Cleanup(player);
        session.BeginTurn(player);
        Assert.AreEqual(RaceTurnStartAction.ExecuteScheduledPitStop, RaceTurnRules.GetStartAction(player));
        Assert.IsTrue(player.skipNextTurn);
        Assert.IsTrue(player.pitStopRequested);
        // Consumed decisions remain inactive even after their flags are cleared by the coordinator.
        player.pitStopScheduled = false;
        player.skipNextTurn = false;
        Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));
        Assert.IsTrue(RaceTurnRules.IsInactive(player, skipped));
        Assert.IsFalse(RaceTurnRules.IsInactive(player, new HashSet<PlayerState>()));
    }
}
