using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>Real coordinator bonus traversal and UK draw/input gates, using the existing isolated native fixture.</summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator HartNativeTwoLegsThenOneCellPaysOnlyTheNewApexAndKeepsPartialPaymentSpin()
    {
        foreach (int available in new[] { 6, 1 })
        {
            CreateRace(TeamId.UK);
            try
            {
                var owner = session.Human;
                owner.driverId = "uk_hunter_hart";
                owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
                session.TeamVehicleBonusesEnabled = false;
                session.Players[1].position = 1;
                Participant(TeamId.IT, true, 3);
                PlayerState third = Participant(TeamId.JP, true, 5);
                List<TrackNode> nodes = Get<List<TrackNode>>(manager.trackManager, "nodes");
                nodes[1] = new TrackNode(1, 1, "native-only apex", 1, false, true);
                nodes[5] = new TrackNode(5, 2, "Hart new apex", 2, false, true);
                var limits = Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits");
                limits[1] = 1; limits[2] = 2;
                if (available == 1) owner.deck.DrawHeatFromPool(5, HeatPaymentDestination.Discard);
                session.BeginTurn(owner);
                owner.cornerTotalThisTurn = 4;
                Assert.That(session.BeginDriverSkillInput(owner, manager.trackManager.TotalNodes), Is.True);
                owner.turnMovement.BeginBasePlan();
                InvokeUkAdapter("ResolveSlipstreamsAtTurnEnd", session.Players, new HashSet<PlayerState>());
                var chains = Get<Dictionary<PlayerState, SlipstreamChainResult>>(manager, "slipstreamsThisTurn");
                Assert.That(chains[owner].Steps.Count, Is.EqualTo(2));
                Assert.That(chains[owner].TotalBonus, Is.EqualTo(4));
                bool done = false;
                IEnumerator move = (IEnumerator)InvokeUkAdapter("ApplySlipstreamMovement",
                    new List<PlayerState> { owner }, new HashSet<PlayerState>());
                manager.StartCoroutine(CompleteMovement(move, () => done = true));
                yield return WaitFor(() => done, "Hart actual native plus one traversal");
                Assert.That(owner.turnMovement.ActualCells, Is.EqualTo(5));
                Assert.That(owner.driverSkill.Hunting.LastUsedLap, Is.EqualTo(0));
                Assert.That(chains[owner].Steps.Count, Is.EqualTo(2), "The reward must not search a third leader.");
                Assert.That(third.position, Is.EqualTo(5));
                Assert.That(owner.position, Is.EqualTo(available == 1 ? 4 : 5));
                Assert.That(owner.spinCounter, Is.EqualTo(available == 1 ? 1 : 0));
                Assert.That(owner.skipNextTurn, Is.EqualTo(available == 1));
                Assert.That(owner.turnMovement.RewindCells, Is.EqualTo(available == 1 ? 1 : 0));
                Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(available == 1 ? 6 : 4));
                Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
                Assert.That(messages.Any(x => x.Contains("native-only apex")), Is.False,
                    "Ordinary native slipstream retains its original corner exemption.");
                Assert.That(messages.Any(x => x.Contains(available == 1 ? "SPINS OUT" : "超 2")), Is.True);
            }
            finally { DestroyRace(); }
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator HartActiveNewApexSettlesBeforeFinishOrPitReservation()
    {
        foreach (bool finalLap in new[] { true, false })
        foreach (int available in new[] { 6, 1 })
        {
            CreateRace(TeamId.UK);
            try
            {
                var owner = session.Human;
                owner.driverId = "uk_hunter_hart";
                owner.driverSkill.Initialize(owner.DriverProfile, 3, true);
                session.TeamVehicleBonusesEnabled = false;
                // One speed-four card, plus Hart tier-one's whole-movement +2.
                owner.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 4), new CardData(CardType.Speed, 1) }, new HeatPool(6));
                owner.deck.DrawToHand(2);
                if (available == 1) owner.deck.DrawHeatFromPool(5, HeatPaymentDestination.Discard);
                session.Players[1].position = 1;
                config.totalLaps = finalLap ? 1 : 99; config.handSize = 2;
                config.enablePitLane = true;
                var nodes = Get<List<TrackNode>>(manager.trackManager, "nodes");
                nodes[0].isStartFinish = !finalLap;
                nodes[6].isStartFinish = finalLap;
                nodes[5] = new TrackNode(5, 2, "pre-finish Hart apex", 1, false, true, true);
                nodes[100].isPitExit = true;
                PitLaneRules.RecordApproachChoice(owner, true);
                Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits")[1] = 2;
                manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
                yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Hart active first input");
                var context = new DriverSkillActivationContext(true, 1, 2, owner.deck.heatPool.remaining, 6, 0);
                Assert.That(HunterSkillActivationPlan.TryCreate(session, owner, manager.trackManager.TotalNodes,
                    1, 1, 1, context, out var plan, out string reason), Is.True, reason);
                CardData cost = owner.deck.Hand.First(x => x.IsSpeed && x.value == 1);
                Assert.That(plan.TryCommit(session, owner, 1, 1, 1, context, cost, out reason), Is.True, reason);
                manager.OnGearButtonClicked(1); manager.OnConfirmGearClicked();
                yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Hart active A4");
                CardUI speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(x => x.cardData.IsSpeed);
                speed.SetSelectedWithoutNotify(true); manager.OnPlayCardsButtonClicked(); manager.OnPlayCardsButtonClicked();
                yield return WaitFor(() => owner.hasFinished || owner.spinCounter > 0 || owner.pitStopScheduled,
                    "Hart apex payment then finish or reservation");
                Assert.That(owner.cornerTotalThisTurn, Is.EqualTo(4), "Whole-movement reward never raises corner speed.");
                Assert.That(owner.hasFinished, Is.EqualTo(finalLap && available == 6));
                Assert.That(owner.finishOrder, Is.EqualTo(finalLap && available == 6 ? 1 : 0));
                Assert.That(messages.Any(x => x.Contains("已越过维修区入口")),
                    Is.EqualTo(!finalLap && available == 6), "A failed Hart path must not schedule a phantom pit stop.");
                Assert.That(owner.spinCounter, Is.EqualTo(available == 1 ? 1 : 0));
                Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(available == 1 ? 6 : 4));
                Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(1), "A failed ordinary fee never refunds the active skill.");
            }
            finally { DestroyRace(); }
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator InterruptedHartFinalCrossingDoesNotCommitOrLeakIntoNextRace()
    {
        CreateRace(TeamId.UK);
        var owner = session.Human;
        config.totalLaps = 1;
        Get<List<TrackNode>>(manager.trackManager, "nodes")[1].isStartFinish = true;
        Set(manager, "nodeWaitDuration", 0.1f);
        owner.totalMovementThisTurn = 3;
        bool deferred = false;
        IEnumerator move = (IEnumerator)InvokeUkAdapter("AnimateBaseMovementWithFinishGate", owner, -1,
            (Action)(() => deferred = true));
        manager.StartCoroutine(move);
        yield return WaitFor(() => deferred, "scoped deferred crossing");
        manager.StopAllCoroutines();
        Assert.That(owner.hasFinished, Is.False);
        Assert.That(owner.finishOrder, Is.Zero);
        Assert.That(session.NextFinishOrder, Is.EqualTo(1));
        DestroyRace();
        CreateRace(TeamId.UK);
        try
        {
            config.totalLaps = 1;
            Get<List<TrackNode>>(manager.trackManager, "nodes")[1].isStartFinish = true;
            bool done = false;
            IEnumerator fresh = (IEnumerator)InvokeUkAdapter("AnimateMovementByAmount", session.Human, -1, 1, false);
            manager.StartCoroutine(CompleteMovement(fresh, () => done = true));
            yield return WaitFor(() => done, "new race ordinary finish");
            Assert.That(session.Human.hasFinished, Is.True);
            Assert.That(session.Human.finishOrder, Is.EqualTo(1));
            Assert.That(session.NextFinishOrder, Is.EqualTo(2));
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator UkBreakfastThenPreparationUsesOldHeatAtRealA2A4AndClosesOnFormalCard()
    {
        CreateRace(TeamId.UK);
        try
        {
            config.enableTechTree = true;
            var owner = session.Human;
            owner.techState = new TechTreeState(TeamId.UK);
            owner.techState.activeNodeIds.UnionWith(new[] { "uk-l1-fish-and-chips", "uk-l2-full-english", "uk-l3-sun-never-sets" });
            Set(manager.trackManager, "<LoadedTrackConfig>k__BackingField", new TrackConfig { country = "UK" });
            owner.deck.AddCardsToHand(session.CreateInitialTrickCards(TeamId.UK));
            owner.deck.DrawHeatFromPoolToHand(2);
            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "UK first input");
            manager.OnGearButtonClicked(1); manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "UK real draw and preparation input");
            Assert.That(owner.deck.Hand.Count(x => x.isTemp), Is.EqualTo(2), "L2+L3 home breakfast remains exactly two.");
            Assert.That(owner.techState.prepMeal.OldHandHeat.Count, Is.EqualTo(2));
            Assert.That(owner.techState.prepMeal.OldHandHeat.All(x => !x.isTemp), Is.True);
            Assert.That(UkPrepMealActivationPlan.TryCreate(session, owner, Turn, 1, "UK", true, true,
                out var preparation, out string reason), Is.True, reason);
            Assert.That(preparation.TryCommit(session, owner, Turn, 1, "UK", true, true,
                owner.deck.Hand.First(x => x.IsSpeed), out _, out _, out _, out reason), Is.True, reason);
            Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(6));
            Assert.That(owner.deck.Hand.Count(x => x.isTemp), Is.EqualTo(2), "Removing old permanent heat cannot revoke breakfast.");
            CardUI speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(x => x.cardData.IsSpeed && owner.deck.ContainsInHand(x.cardData));
            speed.SetSelectedWithoutNotify(true); manager.OnPlayCardsButtonClicked();
            Assert.That(owner.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
            Assert.That(owner.techState.prepMeal.IsOpen, Is.False);
            Assert.That(owner.techState.prepMeal.LastUsedLap, Is.EqualTo(0));
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator MansellRealA2CoolsOnlyFrozenOldHandBeforePreparationSnapshot()
    {
        CreateRace(TeamId.UK);
        try
        {
            config.enableTechTree = true;
            var owner = session.Human;
            owner.driverId = "uk_nigel_mansell";
            owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
            owner.techState = new TechTreeState(TeamId.UK);
            owner.techState.activeNodeIds.UnionWith(new[] { "uk-l1-fish-and-chips", "uk-l2-full-english" });
            owner.deck.AddCardsToHand(session.CreateInitialTrickCards(TeamId.UK));
            owner.deck.DrawHeatFromPoolToHand(1);
            CardData old = owner.deck.Hand.Last();
            owner.deck.DrawHeatFromPool(1); // Prior payment in discard is not a passive source.
            CardData paid = owner.deck.DiscardPile.Last();
            var target = session.Players[1]; target.position = 8;
            session.BeginTurn(owner);
            Assert.That(session.BeginDriverSkillInput(owner, manager.trackManager.TotalNodes), Is.True);
            target.position = 2;

            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => manager.TutorialInputPhase == "gear", "Mansell second owner observation");
            Assert.That(owner.driverSkill.Pursuit.IsPending, Is.True);
            Assert.That(owner.deck.ContainsInHand(old), Is.True, "A1 must not cool before A2 draw.");
            manager.OnGearButtonClicked(1); manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Mansell actual A2 then preparation snapshot");

            Assert.That(owner.deck.ContainsInHand(old), Is.False);
            Assert.That(owner.deck.DiscardPile.Contains(paid), Is.True);
            Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(5));
            Assert.That(owner.deck.Hand.Count(x => x.isTemp), Is.EqualTo(1), "Breakfast's new temporary heat remains.");
            Assert.That(owner.techState.prepMeal.OldHandHeat.Contains(old), Is.False);
            Assert.That(owner.techState.prepMeal.OldHandHeat.All(x => !x.isTemp), Is.True);
            Assert.That(owner.driverSkill.Pursuit.IsPending, Is.False);
            Assert.That(session.ResolveMansellPursuitAfterDraw(owner).Triggered, Is.False);
            Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
        }
        finally { DestroyRace(); }
    }

    private object InvokeUkAdapter(string method, params object[] arguments)
        => typeof(MVPGameManager).GetMethod(method, PrivateInstance).Invoke(manager, arguments);
}
