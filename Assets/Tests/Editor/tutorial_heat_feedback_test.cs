using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class TutorialHeatFeedbackTests
{
    [TestCase(TeamId.DE, true, 2, 1, "engine failure", true)]
    [TestCase(TeamId.US, true, 2, 1, "engine failure", false)]
    [TestCase(TeamId.DE, false, 2, 1, "engine failure", false)]
    [TestCase(TeamId.DE, true, 3, 1, "engine failure", false)]
    [TestCase(TeamId.DE, true, 2, 2, "engine failure", false)]
    [TestCase(TeamId.DE, true, 2, 1, "Engine failure", false)]
    [TestCase(TeamId.DE, true, 2, 1, null, false)]
    public void PaymentSignalsPreserveExactBreadGateAndPayload(TeamId team, bool bread, int before, int paid, string reason, bool specialty)
    {
        var actions = new List<TutorialAction>();
        var details = new List<string>();
        TutorialHeatFeedbackRules.ReportPayment(team, new HeatPaymentCost(paid, before, bread), paid,
            HeatPaymentDestination.Discard, reason, (action, detail) => { actions.Add(action); details.Add(detail); return false; });
        Assert.AreEqual(specialty ? 2 : 1, actions.Count);
        if (specialty)
        {
            Assert.AreEqual(TutorialAction.ResolveDeSchwarzbrot, actions[0]);
            Assert.AreEqual("requested:2,paid:1,discount:1", details[0]);
        }
        Assert.AreEqual(TutorialAction.PayHeat, actions[actions.Count - 1]);
        Assert.AreEqual($"amount:{paid},destination:Discard,reason:{reason}", details[details.Count - 1]);
    }

    [TestCase(TeamId.CN, 1, 2)]
    [TestCase(TeamId.CN, 2, 1)]
    [TestCase(TeamId.US, 1, 1)]
    [TestCase(TeamId.UK, 1, 1)]
    public void CoolingReportsActualCountBeforeOptionalChinaSignal(TeamId team, int gear, int count)
    {
        var player = new PlayerState("driver", false, 0, gear) { teamId = team, chinaConsecutiveGearCount = 4 };
        var actions = new List<TutorialAction>();
        var details = new List<string>();
        TutorialHeatFeedbackRules.ReportCooling(player, 1, 3,
            (action, detail) => { actions.Add(action); details.Add(detail); return false; });
        Assert.AreEqual(count, actions.Count);
        Assert.AreEqual(TutorialAction.CoolHeatCard, actions[0]);
        Assert.AreEqual("cooled:1,requested:3", details[0]);
        if (count == 2)
        {
            Assert.AreEqual(TutorialAction.CompleteChinaRecover, actions[1]);
            Assert.AreEqual("cooled:1,consecutive:4", details[1]);
        }
        Assert.AreEqual(gear, player.gear);
        Assert.AreEqual(4, player.chinaConsecutiveGearCount);
    }

    [Test]
    public void CoolingReReadsStateAfterGenericSignal()
    {
        var player = new PlayerState("driver", false, 0, 2) { teamId = TeamId.CN };
        var details = new List<string>();
        TutorialHeatFeedbackRules.ReportCooling(player, 1, 3, (action, detail) =>
        {
            details.Add(detail);
            player.gear = ChinaGearShiftRules.RecoverGear;
            player.chinaConsecutiveGearCount = 7;
            return false;
        });
        CollectionAssert.AreEqual(new[] { "cooled:1,requested:3", "cooled:1,consecutive:7" }, details);
    }

    [Test]
    public void SignalExceptionStopsLaterDispatch()
    {
        int calls = 0;
        Assert.Throws<InvalidOperationException>(() => TutorialHeatFeedbackRules.ReportPayment(TeamId.DE,
            new HeatPaymentCost(1, 2, true), 1, HeatPaymentDestination.Hand, "engine failure",
            (_, __) => { calls++; throw new InvalidOperationException(); }));
        Assert.AreEqual(1, calls);
    }
}

public class TutorialHeatFeedbackIntegrationTests
{
    private GameObject host;
    private MVPGameManager manager;
    private TutorialRuntimeDirector director;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Tutorial heat feedback integration");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, new RaceSession(new SystemRandomSource(1)));
    }

    [TearDown]
    public void TearDown() => UnityEngine.Object.DestroyImmediate(host);

    private void Guide(TutorialScenarioDefinition scenario, TutorialAction action)
    {
        director = new TutorialRuntimeDirector(scenario);
        for (int i = 0; i < scenario.steps.Count && !director.IsExpecting(action); i++)
        {
            Assert.IsTrue(director.TryPerform(director.CurrentStep.requiredAction, out _));
            Assert.IsTrue(director.TryNext(out _));
        }
        Assert.IsTrue(director.IsExpecting(action));
        director.DrainNewEvents();
        typeof(MVPGameManager).GetField("tutorialDirector", Private).SetValue(manager, director);
    }

    private PlayerState Player(TeamId team, bool ai)
    {
        var player = new PlayerState("driver", ai, 0, 1) { teamId = team };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 1) }, new HeatPool(6));
        return player;
    }

    [TestCase(false, false, true, true)]
    [TestCase(true, false, true, false)]
    [TestCase(false, true, true, false)]
    [TestCase(false, false, false, false)]
    public void RealBreadPaymentRespectsHumanPendingAndEffectGates(bool ai, bool pending, bool bread, bool completes)
    {
        Guide(TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.DE), TutorialAction.ResolveDeSchwarzbrot);
        var player = Player(TeamId.DE, ai);
        player.trickState.schwarzbrotActive = bread;
        player.trickState.schwarzbrotRemaining = bread ? 1 : 0;
        typeof(MVPGameManager).GetField("pendingTutorialGuideRefreshAtTurnStart", Private).SetValue(manager, pending);
        int index = director.CurrentStepIndex;
        Assert.IsTrue(manager.TryPayHeat(player, 2, 0, "engine failure"));
        Assert.AreEqual(completes, director.IsActiveStepComplete);
        Assert.AreEqual(index, director.CurrentStepIndex, "Payment does not auto-advance to another lesson.");
        var events = director.DrainNewEvents();
        Assert.AreEqual(completes ? 1 : 0, events.Count);
        if (completes) Assert.AreEqual("step_completed", events[0].eventId);
    }

    [TestCase(0, false)]
    [TestCase(1, true)]
    public void RealGenericPaymentRequiresPositivePaidOperation(int amount, bool completes)
    {
        Guide(TutorialScenarioDefinition.CreateLeMansUk(), TutorialAction.PayHeat);
        Assert.IsTrue(manager.TryPayHeat(Player(TeamId.UK, false), amount, 0, "shift 2 gears"));
        Assert.AreEqual(completes, director.IsActiveStepComplete);
    }

    [TestCase(false, 1, true)]
    [TestCase(true, 1, false)]
    [TestCase(false, 0, false)]
    public void RealChinaCoolingRequiresActualHumanCooling(bool ai, int available, bool completes)
    {
        Guide(TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.CN), TutorialAction.CompleteChinaRecover);
        var player = Player(TeamId.CN, ai);
        player.deck.DrawHeatFromPool(available, HeatPaymentDestination.Hand);
        int index = director.CurrentStepIndex;
        int cooled = (int)typeof(MVPGameManager).GetMethod("CoolHeatWithPresentation", Private).Invoke(manager, new object[] { player, 3 });
        Assert.AreEqual(available, cooled);
        Assert.AreEqual(completes, director.IsActiveStepComplete);
        Assert.AreEqual(index, director.CurrentStepIndex);
    }
}
