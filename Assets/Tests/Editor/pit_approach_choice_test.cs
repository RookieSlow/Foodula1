using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public class PitApproachChoiceRulesTests
{
    [TestCase(0f, false)]
    [TestCase(0.5999999f, false)]
    [TestCase(0.6f, true)]
    [TestCase(0.6000001f, true)]
    [TestCase(1f, true)]
    [TestCase(-0.1f, false)]
    [TestCase(float.NaN, false)]
    [TestCase(float.PositiveInfinity, true)]
    public void AIReservationUsesTheUnclampedLegacyHeatThreshold(float ratio, bool expected)
    {
        Assert.AreEqual(expected, PitLaneRules.ShouldAIReservePit(ratio));
        Assert.AreEqual(ratio >= 0.6f, PitLaneRules.ShouldAIReservePit(ratio));
    }

    [TestCase(0, true, 10)]
    [TestCase(9, true, 1)]
    [TestCase(10, false, 0)]
    [TestCase(19, false, 11)]
    [TestCase(20, true, 10)]
    [TestCase(-11, true, 1)]
    public void WindowUsesNormalizedDistanceAndExcludesEntry(int position, bool offered, int distance)
    {
        var player = new PlayerState("driver", false, position, 2) { skipNextTurn = true };
        Assert.AreEqual(offered, PitLaneRules.TryGetApproachChoiceDistance(
            player, RacePitStopExecutionTests.Track(), true, out int actual));
        Assert.AreEqual(distance, actual);
        Assert.IsTrue(player.skipNextTurn); // Existing offer gate does not inspect recovery skips.
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
    }

    [TestCase("null")]
    [TestCase("disabled")]
    [TestCase("no-pit")]
    [TestCase("blown")]
    [TestCase("finished")]
    [TestCase("resolved")]
    [TestCase("requested")]
    [TestCase("scheduled")]
    public void IneligibleOfferDoesNotConsumeAnyState(string reason)
    {
        var player = new PlayerState("driver", false, 9, 2)
        {
            isBlown = reason == "blown", hasFinished = reason == "finished",
            pitChoiceResolvedThisLap = reason == "resolved",
            pitStopRequested = reason == "requested", pitStopScheduled = reason == "scheduled"
        };
        Assert.IsFalse(PitLaneRules.TryGetApproachChoiceDistance(reason == "null" ? null : player,
            RacePitStopExecutionTests.Track(reason != "no-pit"), reason != "disabled", out int distance));
        Assert.AreEqual(-1, distance);
        Assert.AreEqual(reason == "resolved", player.pitChoiceResolvedThisLap);
        Assert.AreEqual(reason == "requested", player.pitStopRequested);
        Assert.AreEqual(reason == "scheduled", player.pitStopScheduled);
        Assert.AreEqual(9, player.position);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RecordingChoiceOnlyChangesTheTwoReservationFields(bool enter)
    {
        var player = RacePitStopExecutionTests.Player();
        PitLaneRules.RecordApproachChoice(player, enter);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(enter, player.pitStopRequested);
        Assert.IsTrue(player.pitStopScheduled);
        Assert.IsTrue(player.skipNextTurn);
        Assert.AreEqual(10, player.position);
        Assert.AreEqual(4, player.gear);
        Assert.AreEqual(2, player.deck.heatPool.remaining);
        Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
        PitLaneRules.RecordApproachChoice(null, enter);
    }
}

/// <summary>Actual callback and synchronous coroutine paths; no scene startup or GameLoop.</summary>
public class PitApproachChoiceBoundaryTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private RaceInputState input;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Pit choice regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        manager.trackManager = host.AddComponent<TrackManager>();
        SetField("tutorialPitRuleNodes", RacePitStopExecutionTests.Track());
        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
        player = new PlayerState("driver", false, 9, 2);
        SetField("pitWaitingPlayer", player);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    private void SetField(string name, object value) =>
        typeof(MVPGameManager).GetField(name, Private).SetValue(manager, value);

    private object Invoke(string name, params object[] args) =>
        typeof(MVPGameManager).GetMethod(name, Private).Invoke(manager, args);

    [TestCase(TeamId.CN, false)]
    [TestCase(TeamId.CN, true)]
    [TestCase(TeamId.US, false)]
    [TestCase(TeamId.US, true)]
    public void AuthoredShanghaiChoiceWaitResumesIntoReservationAndNextTurn(TeamId team, bool enter)
    {
        // Exercise the real waiting iterator and public callback without starting
        // a scene, GameLoop, pause menu, tutorial, or persistent reward storage.
        var authored = TrackDataLoader.LoadConfig("shanghai_dim_sum");
        Assert.IsNotNull(authored);
        var nodes = TrackDataLoader.ConfigToNodes(authored);
        Assert.IsTrue(PitLaneRules.HasPitLane(nodes));
        int entry = PitLaneRules.FindPitEntry(nodes);
        int exit = PitLaneRules.FindPitExit(nodes);
        typeof(TrackManager).GetField("nodes", Private).SetValue(manager.trackManager, nodes);
        SetField("tutorialPitRuleNodes", null); // Use actual TrackManager data, not tutorial fallback.
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        try
        {
            config.enablePitLane = true;
            config.enableTechTree = false;
            config.minGear = 2;
            config.pitExitMoveBonus = 1;
            manager.config = config;
            var session = new RaceSession(new SystemRandomSource(1));
            SetField("session", session);
            player = RacePitStopExecutionTests.Player(team);
            player.position = (entry - 1 + nodes.Count) % nodes.Count;
            player.pitStopScheduled = player.skipNextTurn = false;
            player.pitStopRequested = player.pitChoiceResolvedThisLap = false;
            SetField("pitWaitingPlayer", player);
            session.Players.Add(player);
            var panel = new GameObject("Isolated pit choice panel");
            panel.transform.SetParent(host.transform, false);
            panel.SetActive(false);
            SetField("pitChoicePanel", panel);
            Assert.IsTrue(PitLaneRules.TryGetApproachChoiceDistance(player, nodes, true, out int distance));
            Assert.AreEqual(1, distance);

            var routine = (IEnumerator)Invoke("WaitForPitChoice", distance);
            Assert.IsTrue(routine.MoveNext());
            Assert.IsTrue(panel.activeSelf);
            Assert.IsTrue(input.WaitingForPitChoice);
            var wait = routine.Current as WaitWhile;
            Assert.IsNotNull(wait);
            // Polling the suspended wait must not reserve, move, cool, or advance the turn.
            for (int i = 0; i < 3; i++) Assert.IsTrue(wait.keepWaiting);
            Assert.IsFalse(player.pitStopRequested);
            Assert.AreEqual(2, player.deck.heatPool.remaining);
            Assert.AreEqual(4, player.heatPaidCardsThisTurn.Count);
            int start = player.position;
            manager.ChoosePit(enter);
            manager.ChoosePit(!enter); // Late/double input cannot reverse the accepted decision.
            Assert.IsFalse(wait.keepWaiting);
            Assert.IsFalse(routine.MoveNext());
            Assert.IsFalse(panel.activeSelf);
            Assert.IsFalse(input.WaitingForPitChoice);
            Assert.AreEqual(start, player.position);
            Assert.AreEqual(enter, player.pitStopRequested);
            Assert.IsFalse(player.pitStopScheduled);

            player.position = (start + 2) % nodes.Count; // Settled movement crosses the authored entry.
            Invoke("RegisterPitEntryCrossing", player, start, start + 2);
            Assert.AreEqual(enter, player.pitStopScheduled);
            Assert.IsFalse(player.pitStopRequested);
            session.BeginTurn(player);
            Assert.AreEqual(enter ? RaceTurnStartAction.ExecuteScheduledPitStop : RaceTurnStartAction.SelectGear,
                RaceTurnRules.GetStartAction(player));
            if (enter)
            {
                Invoke("ExecuteScheduledPitStop", player);
                Assert.AreEqual((exit + 1) % nodes.Count, player.position);
                Assert.AreEqual(team == TeamId.CN ? 1 : 2, player.gear);
                Assert.AreEqual(6, player.deck.heatPool.remaining);
                Assert.AreEqual(0, player.deck.CountPermanentHeatOutsideEngine());
            }
            else
            {
                Assert.AreEqual((start + 2) % nodes.Count, player.position);
                Assert.AreEqual(4, player.gear);
                Assert.AreEqual(2, player.deck.heatPool.remaining);
                Assert.AreEqual(4, player.deck.CountPermanentHeatOutsideEngine());
            }
            Assert.AreEqual(2, player.lap); // Stop/crossing adapters must not invent lap completion.
            Assert.IsFalse(player.pitStopScheduled);
            Assert.IsFalse(player.skipNextTurn);
            Assert.IsFalse(player.pitChoiceResolvedThisLap);
            session.BeginTurn(player);
            Assert.AreEqual(RaceTurnStartAction.SelectGear, RaceTurnRules.GetStartAction(player));
            player.position = start + nodes.Count;
            Assert.IsTrue(PitLaneRules.TryGetApproachChoiceDistance(player, nodes, true, out distance));
            Assert.AreEqual(1, distance);
        }
        finally
        {
            manager.config = null;
            Object.DestroyImmediate(config);
        }
    }

    [TestCase("closed")]
    [TestCase("gear")]
    [TestCase("cards")]
    [TestCase("discard")]
    [TestCase("lane")]
    public void LatePitClickLeavesOtherInputGateAndParticipantUntouched(string gate)
    {
        if (gate == "gear") input.BeginGearSelection(3);
        if (gate == "cards") input.BeginCardSelection();
        if (gate == "discard") input.BeginDiscardSelection();
        if (gate == "lane") input.BeginLaneChangeSelection();
        manager.ChoosePit(true);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.IsFalse(player.pitStopRequested);
        Assert.AreEqual(gate == "gear", input.WaitingForGear);
        Assert.AreEqual(gate == "cards", input.WaitingForCards);
        Assert.AreEqual(gate == "discard", input.WaitingForDiscard);
        Assert.AreEqual(gate == "lane", input.WaitingForLaneChange);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AcceptedClickClosesGateAndDuplicateClickCannotReverseChoice(bool enter)
    {
        input.BeginPitChoice();
        manager.ChoosePit(enter);
        manager.ChoosePit(!enter);
        Assert.IsFalse(input.WaitingForPitChoice);
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(enter, player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.AreEqual(9, player.position);
    }

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    [TestCase(true, true)]
    public void RenderedPitButtonsRespectConfirmationCancellationAndFirstAcceptedChoice(
        bool enter, bool confirmationEnabled)
    {
        const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
        FieldInfo settings = typeof(GameSettingsRuntime).GetField("current", StaticPrivate);
        FieldInfo repository = typeof(GameSettingsRuntime).GetField("repository", StaticPrivate);
        FieldInfo audio = typeof(AudioService).GetField("instance", StaticPrivate);
        object previousSettings = settings.GetValue(null);
        object previousRepository = repository.GetValue(null);
        object previousAudio = audio.GetValue(null);
        try
        {
            // Replace only the in-memory settings view; never save preferences or change display/audio.
            settings.SetValue(null, new GameSettingsData
            {
                inRaceConfirmationMask = confirmationEnabled ? (int)InRaceConfirmationAction.PitDecision : 0
            });
            audio.SetValue(null, null);
            using (var scope = CareerUiPointerTestScope.Create(() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset")))
            {
                GameObject hudHost = new GameObject("Pit pointer HUD", typeof(RectTransform));
                hudHost.SetActive(false);
                hudHost.transform.SetParent(scope.Root.transform, false);
                HUDUI hud = hudHost.AddComponent<HUDUI>();
                manager.hudUI = hud;
                Invoke("CreatePitChoiceUI");
                hud.EnsureReturnToMenuConfirmationUI();
                // Production Start binds these listeners; avoid starting an unrelated HUD lifecycle.
                hud.cancelReturnToMenuButton.onClick.AddListener(hud.CancelReturnToMenu);
                hud.confirmReturnToMenuButton.onClick.AddListener(hud.ConfirmReturnToMenu);
                foreach (TMP_Text label in scope.Root.GetComponentsInChildren<TMP_Text>(true))
                    label.font = scope.Font;

                var routine = (IEnumerator)Invoke("WaitForPitChoice", 1);
                Assert.IsTrue(routine.MoveNext());
                var wait = (WaitWhile)routine.Current;
                Button selected = (Button)typeof(MVPGameManager).GetField(
                    enter ? "pitEnterButton" : "pitSkipButton", Private).GetValue(manager);
                Button opposite = (Button)typeof(MVPGameManager).GetField(
                    enter ? "pitSkipButton" : "pitEnterButton", Private).GetValue(manager);
                scope.Click(selected);
                if (confirmationEnabled)
                {
                    Assert.IsTrue(hud.IsReturnToMenuConfirmationVisible);
                    Assert.IsTrue(wait.keepWaiting);
                    Assert.IsFalse(player.pitChoiceResolvedThisLap);
                    scope.Click(hud.cancelReturnToMenuButton);
                    Assert.IsFalse(hud.IsReturnToMenuConfirmationVisible);
                    Assert.IsTrue(wait.keepWaiting);
                    Assert.IsFalse(player.pitStopRequested);
                    scope.Click(selected);
                    scope.Click(hud.confirmReturnToMenuButton);
                }
                Assert.IsFalse(hud.IsReturnToMenuConfirmationVisible);
                Assert.IsFalse(wait.keepWaiting);
                Assert.IsTrue(player.pitChoiceResolvedThisLap);
                Assert.AreEqual(enter, player.pitStopRequested);
                Assert.IsFalse(player.pitStopScheduled);
                Assert.AreEqual(9, player.position);

                // A second geometry-verified click before the waiter hides the panel cannot reverse settlement.
                scope.Click(opposite);
                if (confirmationEnabled) scope.Click(hud.confirmReturnToMenuButton);
                Assert.AreEqual(enter, player.pitStopRequested);
                Assert.IsFalse(input.WaitingForPitChoice);
                Assert.IsFalse(routine.MoveNext());
                Assert.IsFalse(selected.gameObject.activeInHierarchy);
            }
        }
        finally
        {
            manager.hudUI = null;
            settings.SetValue(null, previousSettings);
            repository.SetValue(null, previousRepository);
            audio.SetValue(null, previousAudio);
        }
    }

    [Test]
    public void MissingWaitingPlayerStillClosesThePitGate()
    {
        SetField("pitWaitingPlayer", null);
        input.BeginPitChoice();
        manager.ChoosePit(true);
        Assert.IsFalse(input.WaitingForPitChoice);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
    }

    [Test]
    public void MissingPanelDefaultsToContinueWithoutOpeningAnInputGate()
    {
        var routine = (IEnumerator)Invoke("WaitForPitChoice", 1);
        Assert.IsFalse(routine.MoveNext());
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.IsFalse(player.pitStopRequested);
        Assert.IsFalse(input.WaitingForPitChoice);
    }

    [TestCase(2, false, 5)]
    [TestCase(3, true, 5)]
    [TestCase(4, true, 5)]
    [TestCase(0, false, 0)]
    [TestCase(3, false, 6)]
    [TestCase(4, true, 6)]
    [TestCase(6, true, 6)]
    public void ActualAIOfferRetainsSixtyPercentThreshold(int heatCards, bool enter, int handSize)
    {
        player.isAI = true;
        var speeds = new CardData[handSize - heatCards];
        for (int i = 0; i < speeds.Length; i++) speeds[i] = new CardData(CardType.Speed, 1);
        player.deck.InitializeExactOrder(speeds, new HeatPool(6));
        player.deck.DrawToHand(speeds.Length);
        player.deck.DrawHeatFromPool(heatCards, HeatPaymentDestination.Hand);
        int poolBefore = player.deck.heatPool.remaining;
        var routine = (IEnumerator)Invoke("ResolvePitApproachChoice", player);
        Assert.IsFalse(routine.MoveNext());
        Assert.IsTrue(player.pitChoiceResolvedThisLap);
        Assert.AreEqual(enter, player.pitStopRequested);
        Assert.IsFalse(player.pitStopScheduled);
        Assert.AreEqual(9, player.position);
        Assert.IsFalse(input.WaitingForPitChoice);
        Assert.IsFalse(((IEnumerator)Invoke("ResolvePitApproachChoice", player)).MoveNext());
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(poolBefore), "Reservation must not pay or cool heat.");
        Assert.That(player.deck.HandCount, Is.EqualTo(handSize));
        Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(heatCards));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void PassingWithoutReservationReopensTheNextLapOffer(bool ai)
    {
        player.isAI = ai;
        if (ai) Assert.IsFalse(((IEnumerator)Invoke("ResolvePitApproachChoice", player)).MoveNext());
        else { input.BeginPitChoice(); manager.ChoosePit(false); }
        Assert.IsFalse(PitLaneRules.TryGetApproachChoiceDistance(player,
            RacePitStopExecutionTests.Track(), true, out _));
        Invoke("RegisterPitEntryCrossing", player, 9, 12);
        Assert.IsFalse(player.pitChoiceResolvedThisLap);
        Assert.IsFalse(player.pitStopScheduled);
        player.position = 12;
        Assert.IsFalse(PitLaneRules.TryGetApproachChoiceDistance(player,
            RacePitStopExecutionTests.Track(), true, out _));
        player.position = 29; // Next lap, one cell before entry after normalization.
        Assert.IsTrue(PitLaneRules.TryGetApproachChoiceDistance(player,
            RacePitStopExecutionTests.Track(), true, out int distance));
        Assert.AreEqual(1, distance);
    }
}
