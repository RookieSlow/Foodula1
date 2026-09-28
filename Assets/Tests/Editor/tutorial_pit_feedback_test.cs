using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Real pit adapters plus the authored Director; no UI, GameLoop or storage startup.</summary>
public class TutorialPitFeedbackIntegrationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private GameConfigSO config;
    private MVPGameManager manager;
    private RaceSession session;
    private TutorialRuntimeDirector director;
    private PlayerState player;
    private RaceInputState input;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Tutorial pit feedback regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.trackManager = host.AddComponent<TrackManager>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.minGear = 1;
        config.pitExitMoveBonus = 1;
        config.enableTechTree = false;
        manager.config = config;
        session = new RaceSession(new SystemRandomSource(1));
        SetField("session", session);
        SetField("tutorialPitRuleNodes", RacePitStopExecutionTests.Track());
        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
        player = RacePitStopExecutionTests.Player(TeamId.UK);
        player.pitStopRequested = false;
        player.pitStopScheduled = false;
        player.skipNextTurn = false;
        player.pitChoiceResolvedThisLap = false;
        SetField("pitWaitingPlayer", player);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(config);
    }

    private void SetField(string name, object value) =>
        typeof(MVPGameManager).GetField(name, Private).SetValue(manager, value);

    private void Invoke(string name, params object[] args) =>
        typeof(MVPGameManager).GetMethod(name, Private).Invoke(manager, args);

    private void Guide(TutorialAction action)
    {
        var scenario = TutorialScenarioDefinition.CreateLeMansUk();
        director = new TutorialRuntimeDirector(scenario);
        for (int i = 0; i < scenario.steps.Count && !director.IsExpecting(action); i++)
        {
            Assert.IsTrue(director.TryPerform(director.CurrentStep.requiredAction, out _));
            Assert.IsTrue(director.TryNext(out _));
        }
        Assert.IsTrue(director.IsExpecting(action));
        director.DrainNewEvents();
        SetField("tutorialDirector", director);
    }

    private void AssertFeedback(bool completes, int index, TutorialAction action)
    {
        Assert.AreEqual(completes, director.IsActiveStepComplete);
        Assert.AreEqual(index, director.CurrentStepIndex, "Real events latch completion, never navigate.");
        var events = director.DrainNewEvents();
        Assert.AreEqual(completes ? 1 : 0, events.Count);
        if (completes)
        {
            Assert.AreEqual("step_completed", events[0].eventId);
            Assert.AreEqual(director.CurrentStep.id, events[0].step);
            Assert.AreEqual(action.ToString(), events[0].detail);
        }
    }

    [TestCase(false, false, true, true, true)]
    [TestCase(true, false, true, true, false)]
    [TestCase(false, true, true, true, false)]
    [TestCase(false, false, false, true, false)]
    [TestCase(false, false, true, false, false)]
    public void ChoiceFeedbackRequiresHumanPresentedExpectedReservation(
        bool ai, bool pending, bool enter, bool expected, bool completes)
    {
        Guide(expected ? TutorialAction.SelectPit : TutorialAction.PayHeat);
        player.isAI = ai;
        SetField("pendingTutorialGuideRefreshAtTurnStart", pending);
        int index = director.CurrentStepIndex;
        input.BeginPitChoice();
        manager.ChoosePit(enter);
        Assert.IsFalse(input.WaitingForPitChoice);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(enter, player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        AssertFeedback(completes, index, TutorialAction.SelectPit);
        manager.ChoosePit(!enter);
        Assert.AreEqual(0, director.DrainNewEvents().Count);
        Assert.AreEqual(enter, player.pitStopRequested);
    }

    [TestCase(false, false, true, true, true)]
    [TestCase(true, false, true, true, false)]
    [TestCase(false, true, true, true, false)]
    [TestCase(false, false, false, true, false)]
    [TestCase(false, false, true, false, false)]
    public void StopFeedbackRequiresSuccessfulHumanPresentedExpectedExit(
        bool ai, bool pending, bool hasPit, bool expected, bool completes)
    {
        Guide(expected ? TutorialAction.ResolvePitOnNextTurn : TutorialAction.SelectPit);
        player.isAI = ai;
        player.pitStopScheduled = true;
        SetField("pendingTutorialGuideRefreshAtTurnStart", pending);
        SetField("tutorialPitRuleNodes", RacePitStopExecutionTests.Track(hasPit));
        int index = director.CurrentStepIndex;
        Invoke("ExecuteScheduledPitStop", player);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.AreEqual(hasPit ? 0 : 10, player.position);
        Assert.AreEqual(hasPit ? 6 : 2, player.deck.heatPool.remaining);
        AssertFeedback(completes, index, TutorialAction.ResolvePitOnNextTurn);
    }

    [Test]
    public void SelectionThenCrossingCannotCompleteExitUntilExplicitNextAndRealStop()
    {
        Guide(TutorialAction.SelectPit);
        int selectionIndex = director.CurrentStepIndex;
        input.BeginPitChoice();
        manager.ChoosePit(true);
        AssertFeedback(true, selectionIndex, TutorialAction.SelectPit);
        Invoke("RegisterPitEntryCrossing", player, 8, 12);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.AreEqual(selectionIndex, director.CurrentStepIndex);
        Assert.AreEqual(0, director.DrainNewEvents().Count);

        Assert.IsTrue(director.TryNext(out _));
        Assert.IsTrue(director.IsExpecting(TutorialAction.ResolvePitOnNextTurn));
        director.DrainNewEvents();
        int exitIndex = director.CurrentStepIndex;
        session.BeginTurn(player);
        Invoke("ExecuteScheduledPitStop", player);
        AssertFeedback(true, exitIndex, TutorialAction.ResolvePitOnNextTurn);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
        // A latched step is not completed twice by duplicate feedback.
        Invoke("ApplyPitChoice", player, true);
        Assert.AreEqual(exitIndex, director.CurrentStepIndex);
        Assert.AreEqual(0, director.DrainNewEvents().Count);
    }

    [Test]
    public void OrdinaryRaceWithoutDirectorStillResolvesChoiceAndExit()
    {
        input.BeginPitChoice();
        manager.ChoosePit(true);
        Invoke("RegisterPitEntryCrossing", player, 8, 12);
        Invoke("ExecuteScheduledPitStop", player);
        Assert.AreEqual(0, player.position);
        Assert.AreEqual(6, player.deck.heatPool.remaining);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.IsFalse(player.skipNextTurn);
    }
}
