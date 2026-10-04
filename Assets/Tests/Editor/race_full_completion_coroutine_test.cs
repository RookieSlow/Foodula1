using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

/// <summary>
/// Actual Start -> native GameLoop -> three authored Silverstone laps -> result.
/// Controlled speed-one decks and fixture configuration bound duration; no terminal
/// states, positions, lap counters or result calls are fabricated. Headless only.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator MissingCarsPreserveVisualTraversalCrossingOrder()
    {
        // Start, movement, marker, required laps: boundary/nonzero/zero/negative,
        // multi-wrap and finishing before all traversed crossings have completed.
        int[][] cases = { new[] { 6, 1, 0, 2 }, new[] { 1, 2, 3, 2 }, new[] { 1, 0, 3, 2 },
            new[] { 1, -5, 3, 2 }, new[] { 1, 20, 3, 2 }, new[] { 6, 18, 0, 3 } };
        foreach (int[] sample in cases)
        {
            string expectedState = null;
            string[] expectedEvents = null;
            // 0: real car token, 1: empty car list, 2: destroyed/null entry, 3: invalid index.
            for (int presentation = 0; presentation < 4; presentation++)
            {
                CreateRace(TeamId.UK);
                try
                {
                    config.totalLaps = sample[3];
                    config.movementFocusLeadDelay = config.movementFocusTrailDelay = config.nodeDelay = 0;
                    config.enableWeather = true;
                    session.WeatherPool = new[] { "sunny", "light_rain", "heavy_rain" };
                    Set(manager.trackManager, "nodes", Enumerable.Range(0, 8)
                        .Select(i => new TrackNode(i, 99, "straight", 0, i == sample[2])).ToList());
                    session.Human.position = sample[0]; // Initial condition, never a terminal/lap result.
                    var animator = new CrossingAnimator();
                    Set(manager, "carMovementAnimator", animator);
                    List<GameObject> cars = Get<List<GameObject>>(manager, "carInstances");
                    if (presentation == 0 || presentation == 3) cars.Add(Child("Movement car token"));
                    else if (presentation == 2) cars.Add(null);
                    Get<List<int>>(manager, "laneIndices").Add(0);
                    int carIndex = presentation == 3 ? -1 : 0;
                    IEnumerator movement = (IEnumerator)typeof(MVPGameManager)
                        .GetMethod("AnimateMovementByAmount", PrivateInstance)
                        .Invoke(manager, new object[] { session.Human, carIndex, sample[1], false });
                    // The EditMode runner permits nested iterators/null, not a
                    // Coroutine handle, even after entering native Play Mode.
                    bool completed = false;
                    manager.StartCoroutine(CompleteMovement(movement, () => completed = true));
                    yield return WaitFor(() => completed, "native crossing completion");
                    PlayerState player = session.Human;
                    int steps = Math.Max(0, sample[1]);
                    int crossings = TrackRules.CountStartFinishCrossings(manager.trackManager.Nodes,
                        sample[0], sample[0] + steps);
                    Assert.That(player.position, Is.EqualTo((sample[0] + steps) % 8));
                    Assert.That(player.lap, Is.EqualTo(Math.Min(crossings, sample[3])));
                    Assert.That(player.hasFinished, Is.EqualTo(crossings >= sample[3]));
                    Assert.That(player.finishOrder, Is.EqualTo(player.hasFinished ? 1 : 0));
                    Assert.That(session.NextFinishOrder, Is.EqualTo(player.hasFinished ? 2 : 1));
                    Assert.That(animator.Nodes, Is.EqualTo(presentation == 0 ? steps : 0));
                    string state = $"{player.position}:{player.lap}:{player.hasFinished}:{player.finishOrder}:" +
                        $"{session.NextFinishOrder}:{session.Weather}:{player.deck.heatPool.remaining}";
                    if (presentation == 0)
                    {
                        expectedState = state;
                        expectedEvents = messages.ToArray();
                    }
                    else
                    {
                        Assert.That(state, Is.EqualTo(expectedState), "Missing visuals must not change lap/weather/finish.");
                        Assert.That(messages, Is.EqualTo(expectedEvents), "Crossing effects must retain their original order.");
                    }
                }
                finally { DestroyRace(); }
                yield return null;
            }
            TestContext.WriteLine($"Native crossing equivalence: start={sample[0]}; steps={sample[1]}; " +
                $"marker={sample[2]}; requiredLaps={sample[3]}; visual/empty/null/invalid=equal");
        }
    }

    private sealed class CrossingAnimator : ICarMovementAnimator
    {
        internal int Nodes;
        public IEnumerator MoveToNode(GameObject car, Vector3 targetPosition)
        {
            Nodes++;
            car.transform.position = targetPosition;
            yield break;
        }
    }

    private static IEnumerator CompleteMovement(IEnumerator movement, Action completed)
    {
        yield return movement;
        completed();
    }

    [UnityTest]
    public IEnumerator RealStartupRunsAuthoredTrackToFinishAndIsolatedSettlement()
    {
        foreach (TeamId team in new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP })
            yield return CompleteAuthoredRace(team, 0, false);
        foreach (int roster in new[] { 2, 6, 12 })
            yield return CompleteAuthoredRace(TeamId.UK, roster, false);
        yield return CompleteAuthoredRace(TeamId.UK, 2, true);
    }

    private IEnumerator CompleteAuthoredRace(TeamId team, int rosterSize, bool retireHuman)
    {
        UnityEngine.Random.State savedRandom = UnityEngine.Random.state;
        using (var launch = new RaceStartupTestScope())
        try
        {
            CreateStartupRace(rosterSize, out List<string> reads);
            config.playerTeam = team;
            config.playerDriverId = DriverCatalog.GetDefaultForTeam(team).Id;
            config.aiTeams = FreeRaceRosterRules.AvailableTeams.Where(t => t != team).Take(3).ToArray();
            TrackConfig track = manager.trackManager.LoadedTrackConfig;
            config.totalLaps = track.laps;
            Assert.That(track.laps, Is.EqualTo(3));
            ConfigureHeadlessResultControls();
            var saves = new List<string>();
            Set(manager, "saveRaceTechState", (Action<TechTreeState>)(state =>
            {
                Assert.That(manager.Session.IsRaceOver(), Is.True, "No early settlement while racers remain active.");
                Assert.That(state, Is.SameAs(manager.Session.Human.techState));
                Assert.That(manager.Session.Human.driverXp, Is.EqualTo(137), "RP save must precede XP settlement.");
                Assert.That(manager.hudUI.gameOverPanel.activeSelf, Is.False, "Save before result presentation.");
                saves.Add("RP");
            }));
            Set(manager, "saveRaceDriverXp", (Action<string, int>)((id, xp) =>
            {
                Assert.That(id, Is.EqualTo(manager.Session.Human.driverId));
                Assert.That(xp, Is.EqualTo(manager.Session.Human.driverXp));
                Assert.That(saves, Is.EqualTo(new[] { "RP" }));
                saves.Add("XP");
            }));
            LogAssert.Expect(LogType.Warning, Camera.main == null
                ? "[MVPGameManager] Main camera not found; race camera setup skipped."
                : "[RaceCameraController] Race canvas not found; minimap UI skipped.");
            manager.enabled = true; // Let Unity invoke Start; never invoke ShowGameOver or GameLoop manually.
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "completion Start");
            session = manager.Session;
            int expectedField = rosterSize == 0 ? 4 : rosterSize;
            Assert.That(session.Players.Count, Is.EqualTo(expectedField));
            Assert.That(session.Human.teamId, Is.EqualTo(team));
            Assert.That(reads, Is.EqualTo(new[] { "DISPLAY", "XP:" + session.Human.driverId, "TECH:" + team }));
            Assert.That(session.Players.All(p => p.lap == 0 && !p.hasFinished && !p.isBlown && p.finishOrder == 0), Is.True);
            // Replace only card order after real startup. Movement, team bonuses, AI,
            // lap crossing, finish assignment, cleanup and settlement stay real.
            foreach (PlayerState player in session.Players)
            {
                player.deck.InitializeExactOrder(Enumerable.Range(0, 40)
                    .Select(_ => new CardData(CardType.Speed, 1)).ToArray(), new HeatPool(6));
                player.deck.DrawToHand(7);
            }
            var xpBefore = session.Players.ToDictionary(p => p, p => p.driverXp);
            var rpBefore = session.Players.ToDictionary(p => p, p => p.techState.rpBalance);
            var terminalProgress = new Dictionary<PlayerState, Tuple<int, int, int>>();
            int observedTurn = Turn;
            bool forcedRetirement = false;
            bool sawHumanTerminalBeforeOthers = false;
            int gearDecisions = 0;
            string logPath = manager.LastRaceLogPath;
            float deadline = Time.realtimeSinceStartup + 90f;
            while (manager.CurrentPhase != GamePhase.GameOver)
            {
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Completion watchdog team={team}, field={expectedField}, turn={Turn}, gate={manager.TutorialInputPhase}");
                Assert.That(Turn, Is.LessThanOrEqualTo(300), "A bounded controlled race must make progress.");
                Assert.That(saves, Is.Empty, "Do not settle before the native loop reaches game-over.");
                // Crossing sets hasFinished before that traversal's final position.
                // Freeze completed cars at the following turn boundary, not mid-move.
                if (Turn != observedTurn)
                {
                    foreach (PlayerState player in session.Players)
                    {
                        if (terminalProgress.TryGetValue(player, out Tuple<int, int, int> finished))
                            Assert.That(Tuple.Create(player.lap, player.position, player.finishOrder), Is.EqualTo(finished),
                                "Finished/DNF cars must not keep moving or receive a second place.");
                        else if (player.hasFinished || player.isBlown)
                            terminalProgress.Add(player, Tuple.Create(player.lap, player.position, player.finishOrder));
                    }
                    observedTurn = Turn;
                }
                if ((session.Human.hasFinished || session.Human.isBlown) && !session.IsRaceOver())
                {
                    sawHumanTerminalBeforeOthers = true;
                    Assert.That(manager.TutorialInputPhase, Is.EqualTo("none"), "Terminal human must not stall surviving AI.");
                }
                switch (manager.TutorialInputPhase)
                {
                    case "gear":
                        Assert.That(session.Human.hasFinished || session.Human.isBlown, Is.False);
                        gearDecisions++;
                        manager.OnGearButtonClicked(1);
                        manager.OnConfirmGearClicked();
                        break;
                    case "cards":
                        Assert.That(session.Human.playedSpeedCardsThisTurn, Is.Empty);
                        CardUI speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(c => c.cardData.IsSpeed);
                        speed.SetSelectedWithoutNotify(true);
                        manager.OnPlayCardsButtonClicked();
                        Assert.That(session.Human.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
                        manager.OnPlayCardsButtonClicked();
                        break;
                    case "discard":
                        if (retireHuman && !forcedRetirement)
                        {
                            for (int spin = 0; spin < 3; spin++)
                                manager.HandleSpin(session.Human, session.Human.position, "controlled regression retirement");
                            Assert.That(session.Human.isBlown, Is.True);
                            forcedRetirement = true;
                        }
                        manager.OnPlayCardsButtonClicked();
                        break;
                    case "none": break;
                    default: Assert.Fail("Unexpected modal in bounded configuration: " + manager.TutorialInputPhase); break;
                }
                manager.RequestPresentationSkip(); // Real public affordance, no time-scale or frame manipulation.
                yield return null;
            }
            Assert.That(Turn, Is.GreaterThan(1));
            Assert.That(gearDecisions, Is.GreaterThan(0));
            Assert.That(session.IsRaceOver(), Is.True);
            Assert.That(session.Players.Where(p => !p.isBlown).All(p => p.hasFinished && p.lap == track.laps), Is.True);
            Assert.That(session.Players.Count(p => p.hasFinished), Is.EqualTo(expectedField - (retireHuman ? 1 : 0)));
            Assert.That(session.Players.Where(p => p.hasFinished).Select(p => p.finishOrder).OrderBy(i => i),
                Is.EqualTo(Enumerable.Range(1, expectedField - (retireHuman ? 1 : 0))));
            Assert.That(session.NextFinishOrder, Is.EqualTo(expectedField + (retireHuman ? 0 : 1)));
            Assert.That(session.Human.isBlown, Is.EqualTo(retireHuman));
            if (retireHuman)
            {
                Assert.That(forcedRetirement && sawHumanTerminalBeforeOthers, Is.True);
                Assert.That(session.Human.finishOrder, Is.Zero);
                Assert.That(session.Human.driverXp, Is.EqualTo(137));
            }
            Assert.That(saves, Is.EqualTo(new[] { "RP", "XP" }), "Only human progression is persisted, exactly once.");
            foreach (RaceRanking.RankEntry entry in session.GetRankings())
            {
                PlayerState player = entry.player;
                Assert.That(player.techState.rpBalance,
                    Is.EqualTo(rpBefore[player] + TechTreeRules.CalculateRaceRP(entry.rank)));
                int earned = player.isBlown ? 0 : DriverProgression.CalculateRaceXp(
                    entry.rank, player.DriverProfile.TalentMultiplier, player.DriverProfile.Team);
                Assert.That(player.driverXp, Is.EqualTo(xpBefore[player] + earned));
            }
            Assert.That(manager.hudUI.gameOverPanel.activeSelf, Is.True);
            Assert.That(manager.hudUI.gameOverText.text, Does.Contain("RP 奖励").And.Contain("车手 XP"));
            Assert.That(manager.cardHandUI.playCardsButton.gameObject.activeSelf, Is.False);
            Assert.That(Get<List<CardUI>>(manager.cardHandUI, "cardUIs"), Is.Empty);
            Assert.That(manager.TutorialInputPhase, Is.EqualTo("none"));
            string contents = File.ReadAllText(logPath); // Actual End closed the writer, not a fabricated terminal trace.
            RaceLogAnalysisResult analysis = RaceLogAnalyzer.Analyze(contents);
            Assert.That(analysis.IsValid, Is.True, string.Join("; ", analysis.Errors));
            Assert.That(analysis.HasCompletedRaceEvidence, Is.True);
            Assert.That(analysis.Termination, Is.EqualTo(RaceLogTermination.Completed));
            Assert.That(analysis.TurnCount, Is.EqualTo(Turn));
            Assert.That(contents.Split(new[] { "[RACE_END]" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(contents, Does.Not.Contain("[CAREER_SETUP]").And.Not.Contain("[TUTORIAL_SETUP]"));
            if (rosterSize > 0) Assert.That(analysis.Mode, Is.EqualTo(RaceLogMode.FreeRace));
            int resultTurn = Turn;
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(Turn, Is.EqualTo(resultTurn));
            Assert.That(saves.Count, Is.EqualTo(2), "Completed loop must not repeat persistence on later frames.");
            TestContext.WriteLine($"Native full controlled race: team={team}; field={expectedField}; retiredHuman={retireHuman}; " +
                $"authoredNodes=77; laps={track.laps}; turns={Turn}; finishers={session.Players.Count(p => p.hasFinished)}; " +
                "persistence=RP,XP; log=Completed");
        }
        finally
        {
            DestroyRace();
            UnityEngine.Random.state = savedRandom;
        }
        yield return null;
    }

    private void ConfigureHeadlessResultControls()
    {
        manager.hudUI.gameOverPanel = Child("Headless result");
        manager.hudUI.gameOverPanel.AddComponent<Image>();
        manager.hudUI.gameOverPanel.GetComponent<RectTransform>().sizeDelta = new Vector2(800, 600);
        var text = Child("Result text");
        text.transform.SetParent(manager.hudUI.gameOverPanel.transform, false);
        manager.hudUI.gameOverText = text.AddComponent<TextMeshProUGUI>();
        manager.hudUI.gameOverPanel.SetActive(false);
        manager.cardHandUI.gearSelectionPanel = Child("Headless gear controls");
        manager.cardHandUI.playCardsButton = Child("Headless play button").AddComponent<Button>();
    }
}
