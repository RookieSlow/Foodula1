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
/// Actual Start -> native GameLoop -> authored track laps -> result.
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

    private IEnumerator CompleteAuthoredRace(TeamId team, int rosterSize, bool retireHuman, bool isCareer = false,
        CareerCompletionFault fault = CareerCompletionFault.None, int careerRaceIndex = 0,
        bool useStartupDeck = false, int startupSeed = 0, bool useAuthoredWeather = false,
        bool useAuthoredPit = false)
    {
        UnityEngine.Random.State savedRandom = UnityEngine.Random.state;
        using (var launch = new RaceStartupTestScope())
        try
        {
            if (useStartupDeck) UnityEngine.Random.InitState(startupSeed);
            CreateStartupRace(rosterSize, out List<string> reads);
            config.enableWeather = useAuthoredWeather;
            if (useAuthoredPit)
            {
                Assert.That(isCareer, Is.False, "This pit fixture is isolated from career launch authority.");
                TrackConfig pitTrack = TrackDataLoader.LoadConfig("shanghai_dim_sum");
                typeof(TrackManager).GetProperty("LoadedTrackConfig").SetValue(manager.trackManager, pitTrack);
                Set(manager.trackManager, "nodes", TrackDataLoader.ConfigToNodes(pitTrack));
                config.enablePitLane = true;
                config.pitExitMoveBonus = 1;
                // No Canvas is created by the headless fixture. Supply only the
                // waiting panel so the real coroutine does not default to skip.
                GameObject pitPanel = Child("Headless native pit choice");
                pitPanel.SetActive(false);
                Set(manager, "pitChoicePanel", pitPanel);
            }
            if (useStartupDeck)
            {
                config.enableTrickCards = true;
                config.gearOneCooldown = 3; // Restore default cooling disabled by the controlled-deck fixture.
            }
            config.playerTeam = team;
            config.playerDriverId = DriverCatalog.GetDefaultForTeam(team).Id;
            config.aiTeams = FreeRaceRosterRules.AvailableTeams.Where(t => t != team).Take(3).ToArray();
            CareerCompletionContext career = isCareer ? PrepareCareerCompletion(team, reads, fault, careerRaceIndex) : null;
            TrackConfig track = manager.trackManager.LoadedTrackConfig;
            config.totalLaps = track.laps;
            Assert.That(track.laps, Is.EqualTo(isCareer && careerRaceIndex == 7 ? 2 : 3));
            ConfigureHeadlessResultControls();
            var saves = new List<string>();
            if (career == null)
            {
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
            } // Career retains CreateRace's fail-fast ordinary-save callbacks.
            LogAssert.Expect(LogType.Warning, Camera.main == null
                ? "[MVPGameManager] Main camera not found; race camera setup skipped."
                : "[RaceCameraController] Race canvas not found; minimap UI skipped.");
            manager.enabled = true; // Let Unity invoke Start; never invoke ShowGameOver or GameLoop manually.
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "completion Start");
            session = manager.Session;
            if (useAuthoredWeather)
            {
                CollectionAssert.AreEqual(track.weatherPool, session.WeatherPool);
                Assert.That(Get<RaceWeatherState>(manager, "weatherState").LastRolledLap, Is.Zero);
            }
            int expectedField = rosterSize == 0 ? 4 : rosterSize;
            Assert.That(session.Players.Count, Is.EqualTo(expectedField));
            Assert.That(session.Human.teamId, Is.EqualTo(team));
            if (career == null)
                Assert.That(reads, Is.EqualTo(new[] { "DISPLAY", "XP:" + session.Human.driverId, "TECH:" + team }));
            else career.AssertStartup(manager, reads);
            Assert.That(session.Players.All(p => p.lap == 0 && !p.hasFinished && !p.isBlown && p.finishOrder == 0), Is.True);
            if (career != null) career.ApplyFaultAfterStartup();
            // Existing bounded cases still use speed-one cards. The startup-deck
            // path does not reinitialize, add, remove or reorder any runtime card.
            List<CardData> initialStartupCards = null;
            if (useStartupDeck)
            {
                if (rosterSize > 0) AssertStartupRoster(rosterSize);
                initialStartupCards = AssertStartupDecks();
            }
            else foreach (PlayerState player in session.Players)
            {
                player.deck.InitializeExactOrder(Enumerable.Range(0, 40)
                    .Select(_ => new CardData(CardType.Speed, 1)).ToArray(), new HeatPool(6));
                player.deck.DrawToHand(7);
            }
            int pitPermanentHeat = session.Human.deck.heatPool.remaining
                + session.Human.deck.CountPermanentHeatOutsideEngine();
            if (useAuthoredPit && !useStartupDeck) session.Human.deck.DrawHeatFromPoolToHand(1);
            var xpBefore = session.Players.ToDictionary(p => p, p => p.driverXp);
            var rpBefore = session.Players.ToDictionary(p => p, p => p.techState.rpBalance);
            var terminalProgress = new Dictionary<PlayerState, Tuple<int, int, int>>();
            int observedTurn = Turn;
            bool forcedRetirement = false;
            bool sawHumanTerminalBeforeOthers = false;
            int gearDecisions = 0;
            int pitDecisions = 0, pitCrossingTurn = 0;
            bool sawPitExecution = false, sawInputAfterPit = false;
            string logPath = manager.LastRaceLogPath;
            float deadline = Time.realtimeSinceStartup + 90f;
            while (manager.CurrentPhase != GamePhase.GameOver)
            {
                if (useAuthoredPit)
                {
                    PlayerState human = session.Human;
                    if (human.pitStopScheduled && pitCrossingTurn == 0) pitCrossingTurn = Turn;
                    if (pitCrossingTurn > 0 && Turn == pitCrossingTurn + 1)
                    {
                        Assert.That(manager.TutorialInputPhase, Is.EqualTo("none"), "Scheduled stop consumes a turn without human input.");
                        if (!human.pitStopScheduled)
                        {
                            sawPitExecution = true;
                            Assert.That(human.position, Is.EqualTo((PitLaneRules.FindPitExit(manager.trackManager.Nodes) + 1) % manager.trackManager.TotalNodes));
                            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(pitPermanentHeat),
                                "Stop recovers the participant's original permanent heat, not the controlled fixture's six.");
                            Assert.That(human.deck.CountPermanentHeatOutsideEngine(), Is.Zero);
                            Assert.That(human.gear, Is.EqualTo(team == TeamId.CN ? 1 : config.minGear));
                            Assert.That(human.playedSpeedCardsThisTurn, Is.Empty);
                        }
                    }
                }
                if (useAuthoredWeather)
                {
                    Assert.That(Get<RaceWeatherState>(manager, "weatherState").LastRolledLap,
                        Is.EqualTo(session.Players.Max(player => player.lap)),
                        "Weather gate must follow the leader, never lagging crossings.");
                    Assert.That(track.weatherPool.Select(id => WeatherRules.ParseWeather(id)).Contains(session.Weather), Is.True);
                }
                Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                    $"Completion watchdog team={team}, field={expectedField}, turn={Turn}, gate={manager.TutorialInputPhase}");
                Assert.That(Turn, Is.LessThanOrEqualTo(300), "A bounded controlled race must make progress.");
                Assert.That(saves, Is.Empty, "Do not settle before the native loop reaches game-over.");
                if (career != null) career.AssertNotSettled();
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
                    case "pit":
                        Assert.That(useAuthoredPit, Is.True);
                        Assert.That(Get<PlayerState>(manager, "pitWaitingPlayer"), Is.SameAs(session.Human));
                        int waitTurn = Turn, waitPosition = session.Human.position;
                        int waitHeat = session.Human.deck.CountPermanentHeatOutsideEngine();
                        var waitWeather = session.Weather;
                        int waitWeatherLap = useAuthoredWeather
                            ? Get<RaceWeatherState>(manager, "weatherState").LastRolledLap : -1;
                        // Normal team cooling may already have recovered the initial
                        // heat. Do not inject heat here or claim a hot-stop scenario.
                        GameObject panel = Get<GameObject>(manager, "pitChoicePanel");
                        for (int frame = 0; frame < 3; frame++)
                        {
                            manager.RequestPresentationSkip(); // Animation skip cannot answer the pit modal.
                            yield return null;
                            Assert.That(manager.TutorialInputPhase, Is.EqualTo("pit"));
                            Assert.That(panel.activeSelf, Is.True);
                            Assert.That(Turn, Is.EqualTo(waitTurn));
                            Assert.That(session.Human.position, Is.EqualTo(waitPosition));
                            Assert.That(session.Human.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(waitHeat));
                            Assert.That(saves, Is.Empty);
                            if (useAuthoredWeather)
                            {
                                Assert.That(session.Weather, Is.EqualTo(waitWeather), "Pit wait cannot reroll weather.");
                                Assert.That(Get<RaceWeatherState>(manager, "weatherState").LastRolledLap,
                                    Is.EqualTo(waitWeatherLap), "Pit wait cannot advance the shared leader-lap gate.");
                            }
                        }
                        bool enter = ++pitDecisions == 1;
                        manager.ChoosePit(enter);
                        manager.ChoosePit(!enter);
                        Assert.That(session.Human.pitStopRequested, Is.EqualTo(enter));
                        Assert.That(session.Human.pitStopScheduled, Is.False);
                        yield return WaitFor(() => !panel.activeSelf, "native pit panel closes after input");
                        break;
                    case "gear":
                        Assert.That(session.Human.hasFinished || session.Human.isBlown, Is.False);
                        if (sawPitExecution)
                        {
                            Assert.That(Turn, Is.GreaterThan(pitCrossingTurn + 1));
                            sawInputAfterPit = true;
                        }
                        gearDecisions++;
                        manager.OnGearButtonClicked(1);
                        manager.OnConfirmGearClicked();
                        break;
                    case "cards":
                        Assert.That(session.Human.playedSpeedCardsThisTurn, Is.Empty);
                        CardUI speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs")
                            .Where(c => c.cardData.IsSpeed)
                            .OrderBy(c => useStartupDeck ? c.cardData.value : 0).FirstOrDefault();
                        if (speed != null)
                        {
                            speed.SetSelectedWithoutNotify(true);
                            manager.OnPlayCardsButtonClicked();
                            Assert.That(session.Human.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
                        }
                        else Assert.That(useStartupDeck, Is.True, "Controlled fixture always supplies speed cards");
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
                        if (useStartupDeck)
                            foreach (CardUI card in Get<List<CardUI>>(manager.cardHandUI, "cardUIs"))
                                if (card.cardData.IsTrick) card.SetSelectedWithoutNotify(true);
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
            if (useAuthoredWeather)
            {
                Assert.That(Get<RaceWeatherState>(manager, "weatherState").LastRolledLap, Is.EqualTo(track.laps));
                CollectionAssert.AreEqual(track.weatherPool, session.WeatherPool);
                TestContext.WriteLine($"Authored weather: seed={startupSeed}; final={session.Weather}; gate={track.laps}");
            }
            Assert.That(session.Players.Where(p => !p.isBlown).All(p => p.hasFinished && p.lap == track.laps), Is.True);
            int finishers = session.Players.Count(p => p.hasFinished);
            if (useStartupDeck)
            {
                Assert.That(finishers, Is.GreaterThan(0), "At least one real finisher is required, not all-DNF evidence");
                Assert.That(session.Players.All(p => p.hasFinished || p.isBlown), Is.True);
                Assert.That(session.Players.Any(p => p.hasFinished && p.isBlown), Is.False);
                AssertStartupCardsConserved(initialStartupCards);
            }
            else
            {
                Assert.That(finishers, Is.EqualTo(expectedField - (retireHuman ? 1 : 0)));
                Assert.That(session.Human.isBlown, Is.EqualTo(retireHuman));
            }
            Assert.That(session.Players.Where(p => p.hasFinished).Select(p => p.finishOrder).OrderBy(i => i),
                Is.EqualTo(Enumerable.Range(1, finishers)));
            Assert.That(session.NextFinishOrder, Is.EqualTo(finishers + 1));
            if (retireHuman)
            {
                Assert.That(forcedRetirement && sawHumanTerminalBeforeOthers, Is.True);
                Assert.That(session.Human.finishOrder, Is.Zero);
                Assert.That(session.Human.driverXp, Is.EqualTo(137));
            }
            Assert.That(saves, career == null ? Is.EqualTo(new[] { "RP", "XP" }) : Is.Empty,
                "Ordinary progression is persisted once only outside career mode.");
            foreach (RaceRanking.RankEntry entry in session.GetRankings())
            {
                PlayerState player = entry.player;
                Assert.That(player.techState.rpBalance,
                    Is.EqualTo(rpBefore[player] + (career == null ? TechTreeRules.CalculateRaceRP(entry.rank) : 0)));
                int earned = player.isBlown || career != null ? 0 : DriverProgression.CalculateRaceXp(
                    entry.rank, player.DriverProfile.TalentMultiplier, player.DriverProfile.Team);
                Assert.That(player.driverXp, Is.EqualTo(xpBefore[player] + earned));
            }
            Assert.That(manager.hudUI.gameOverPanel.activeSelf, Is.True);
            if (career == null)
                Assert.That(manager.hudUI.gameOverText.text, Does.Contain("RP 奖励").And.Contain("车手 XP"));
            else career.AssertSettlement(manager, retireHuman);
            Assert.That(manager.cardHandUI.playCardsButton.gameObject.activeSelf, Is.False);
            Assert.That(Get<List<CardUI>>(manager.cardHandUI, "cardUIs"), Is.Empty);
            Assert.That(manager.TutorialInputPhase, Is.EqualTo("none"));
            string contents = File.ReadAllText(logPath); // Actual End closed the writer, not a fabricated terminal trace.
            if (useAuthoredPit)
            {
                Assert.That(sawPitExecution, Is.True);
                Assert.That(sawInputAfterPit, Is.True, "The next playable turn must reopen gear input after the consumed stop.");
                Assert.That(pitDecisions, Is.GreaterThanOrEqualTo(2), "Later lap must reopen the real choice coroutine.");
                Assert.That(contents.Split(new[] { session.Human.name + " 进站执行：" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
                TestContext.WriteLine($"Authored pit: team={team}; choices={pitDecisions}; crossingTurn={pitCrossingTurn}; stoppedTurn={pitCrossingTurn + 1}; stop=once");
            }
            if (useAuthoredWeather)
                Assert.That(contents, Does.Contain("今日天气: "), "Start binds the HUD sink to the real race log.");
            RaceLogAnalysisResult analysis = RaceLogAnalyzer.Analyze(contents);
            Assert.That(analysis.IsValid, Is.True, string.Join("; ", analysis.Errors));
            Assert.That(analysis.HasCompletedRaceEvidence, Is.True);
            Assert.That(analysis.Termination, Is.EqualTo(RaceLogTermination.Completed));
            Assert.That(analysis.TurnCount, Is.EqualTo(Turn));
            Assert.That(contents.Split(new[] { "[RACE_END]" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            if (career == null)
            {
                Assert.That(contents, Does.Not.Contain("[CAREER_SETUP]").And.Not.Contain("[TUTORIAL_SETUP]"));
                if (rosterSize > 0) Assert.That(analysis.Mode, Is.EqualTo(RaceLogMode.FreeRace));
            }
            else career.AssertLog(contents, analysis);
            int resultTurn = Turn;
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(Turn, Is.EqualTo(resultTurn));
            Assert.That(saves.Count, Is.EqualTo(career == null ? 2 : 0),
                "Completed loop must not repeat persistence on later frames.");
            if (career != null) career.AssertSettledOnce();
            // First completion always came from GameLoop; only the fault fixture
            // requests later callbacks against this genuinely completed race.
            if (career != null) career.AssertRecovery(manager, retireHuman, logPath, contents);
            if (career != null) career.AssertSummerBreakAndResume(manager, logPath, contents);
            if (career != null) career.AssertFifthCompletion(manager, logPath, contents);
            if (career != null) career.AssertFinaleCompletion(manager, logPath, contents);
            TestContext.WriteLine($"Native full race: startupDeck={useStartupDeck}; seed={startupSeed}; career={isCareer}; team={team}; field={expectedField}; retiredHuman={session.Human.isBlown}; " +
                $"track={track.trackId}; authoredNodes={manager.trackManager.TotalNodes}; laps={track.laps}; turns={Turn}; finishers={session.Players.Count(p => p.hasFinished)}; " +
                $"persistence={(isCareer ? "career-only" : "RP,XP")}; log=Completed");
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
