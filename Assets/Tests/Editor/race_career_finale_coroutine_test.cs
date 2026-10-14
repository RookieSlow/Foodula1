using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Genuine eighth-race completion, final presentation and closed-season gates.
/// Seven rotating classifications and summer confirmation are service setup,
/// not seven native races, rendered UI or real player-storage acceptance.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator RealCareerFinaleCompletesStandingsAndRejectsNinthRace()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
            yield return CompleteAuthoredRace(team, 0, false, true,
                CareerCompletionFault.None, 7);
        yield return CompleteAuthoredRace(TeamId.CN, 0, true, true,
            CareerCompletionFault.None, 7);
    }

    private static CareerRaceResult SeedFinaleHistory(CareerSeasonState state, TeamId[] field)
        => new CareerRaceResult("finale-seed-" + state.NextTrackIndex,
            CareerModeRules.GetNextTrackId(state), field.Select((team, index) =>
                new CareerCompetitorResult(team, (index + 4 - state.NextTrackIndex % 4) % 4 + 1)).ToArray());

    private sealed partial class CareerCompletionContext
    {
        // Literal totals for the seven rotating four-car classifications above.
        // They keep the oracle independent of GetStandings and its points table.
        private static readonly int[] FinaleSeedPoints = { 38, 40, 42, 34 };
        private static readonly int[] FinaleSeedWins = { 2, 2, 2, 1 };
        private static readonly int[] FinaleSeedPodiums = { 5, 5, 6, 5 };
        private static readonly int[] FinaleSeedRecent = { 3, 4, 1, 2 };

        internal void AssertFinaleStartup(MVPGameManager manager)
        {
            if (launch.RaceIndex != 7) return;
            Assert.That(launch.TrackId, Is.EqualTo("le_mans_old_mulsanne"));
            Assert.That(manager.trackManager.TotalNodes, Is.EqualTo(142));
            Assert.That(manager.trackManager.LoadedTrackConfig.laps, Is.EqualTo(2));
            Assert.That(launch.TechSnapshot.RpBalance, Is.EqualTo(22500));
            Assert.That(launch.TechSnapshot.ActiveNodeIds, Is.EqualTo(new[] { FifthHeatNode(field[0]) }));
            Assert.That(manager.Session.GetModifiers(manager.Session.Human).heatReductionPerLap, Is.EqualTo(1));
            CareerSeasonState before = repository.Load().State;
            Assert.That(before.Phase, Is.EqualTo(CareerPhase.Racing));
            Assert.That(before.RaceResults.Count, Is.EqualTo(7));
            Assert.That(before.SummerBreakUsed, Is.True);
            Assert.That(CareerModeRules.CanAdjustTechTree(before), Is.False);
            Assert.That(launch.Matches(before), Is.True);
            var standings = CareerModeRules.GetStandings(before);
            for (int index = 0; index < field.Length; index++)
            {
                CareerStanding entry = standings.Single(s => s.TeamId == field[index]);
                Assert.That(entry.Points, Is.EqualTo(FinaleSeedPoints[index]));
                Assert.That(entry.Wins, Is.EqualTo(FinaleSeedWins[index]));
                Assert.That(entry.Podiums, Is.EqualTo(FinaleSeedPodiums[index]));
                Assert.That(entry.BestFinish, Is.EqualTo(1));
                Assert.That(entry.MostRecentFinish, Is.EqualTo(FinaleSeedRecent[index]));
            }
        }

        internal void AssertFinaleCompletion(MVPGameManager manager, string logPath, string closedTrace)
        {
            if (launch.RaceIndex != 7) return;
            CareerLoadResult reloaded = repository.Load();
            Assert.That(reloaded.Status, Is.EqualTo(CareerLoadStatus.Loaded));
            CareerSeasonState completed = reloaded.State;
            Assert.That(completed.Phase, Is.EqualTo(CareerPhase.Completed));
            Assert.That(completed.NextTrackIndex, Is.EqualTo(8));
            Assert.That(completed.RaceResults.Count, Is.EqualTo(8));
            Assert.That(completed.RaceResults.Select(r => r.ResultId).Distinct().Count(), Is.EqualTo(8));
            Assert.That(completed.RaceResults.Select(r => r.TrackId), Is.EqualTo(OfficialTrackCatalog.Tracks.Select(t => t.TrackId)));
            Assert.That(completed.InitialTechSnapshot.RpBalance, Is.EqualTo(25000));
            Assert.That(completed.InitialTechSnapshot.UnlockedNodeIds, Is.Empty);
            Assert.That(completed.InitialTechSnapshot.ActiveNodeIds, Is.Empty);
            Assert.That(completed.SummerBreakTechSnapshot.RpBalance, Is.EqualTo(22500));
            Assert.That(completed.SummerBreakTechSnapshot.ActiveNodeIds, Is.EqualTo(launch.TechSnapshot.ActiveNodeIds));
            Assert.That(manager.Session.GetModifiers(manager.Session.Human).heatReductionPerLap, Is.EqualTo(1));

            // Add the actual terminal classification to known setup statistics.
            // DNF keeps the most recent classified finish, as the existing rule specifies.
            var expected = field.Select((team, index) =>
            {
                PlayerState player = manager.Session.Players.Single(p => p.teamId == team);
                return new
                {
                    Team = team, Index = index,
                    Points = FinaleSeedPoints[index] + (player.isBlown ? 0 : new[] { 10, 6, 4, 2 }[player.finishOrder - 1]),
                    Wins = FinaleSeedWins[index] + (!player.isBlown && player.finishOrder == 1 ? 1 : 0),
                    Podiums = FinaleSeedPodiums[index] + (!player.isBlown && player.finishOrder <= 3 ? 1 : 0),
                    Recent = player.isBlown ? FinaleSeedRecent[index] : player.finishOrder
                };
            }).OrderByDescending(s => s.Points).ThenByDescending(s => s.Wins)
                .ThenByDescending(s => s.Podiums).ThenBy(s => s.Recent).ThenBy(s => s.Index).ToArray();
            var actual = CareerModeRules.GetStandings(completed);
            Assert.That(actual.Select(s => s.TeamId), Is.EqualTo(expected.Select(s => s.Team)));
            for (int rank = 0; rank < expected.Length; rank++)
            {
                Assert.That(actual[rank].Rank, Is.EqualTo(rank + 1));
                Assert.That(actual[rank].Points, Is.EqualTo(expected[rank].Points));
                Assert.That(actual[rank].Wins, Is.EqualTo(expected[rank].Wins));
                Assert.That(actual[rank].Podiums, Is.EqualTo(expected[rank].Podiums));
                Assert.That(actual[rank].BestFinish, Is.EqualTo(1));
                Assert.That(actual[rank].MostRecentFinish, Is.EqualTo(expected[rank].Recent));
            }
            int playerRank = Array.FindIndex(expected, s => s.Team == field[0]) + 1;
            int playerPoints = expected[playerRank - 1].Points;
            Assert.That(manager.hudUI.gameOverText.text, Does.Contain("总分 " + playerPoints)
                .And.Contain("总排名第 " + playerRank + " 名")
                .And.Contain("八站生涯已完成。总冠军：" + expected[0].Team + "（" + expected[0].Points + " 分）"));
            Assert.That(closedTrace, Does.Contain("race=8/8 track=le_mans_old_mulsanne phase=Completed player_points=" +
                playerPoints + " player_rank=" + playerRank + " champion=" + expected[0].Team));
            CareerMenuViewModel view = CareerMenuPresentation.Build(completed, CareerLoadStatus.Loaded, true);
            Assert.That(view.CanLaunchRace || view.CanAdjustTech, Is.False);
            Assert.That(view.CanStartNewSeason, Is.True);
            Assert.That(view.PrimaryAction, Is.EqualTo("开启新一轮生涯"));
            Assert.That(view.Status, Does.Contain("总冠军：" + CareerMenuPresentation.GetTeamLabel(expected[0].Team))
                .And.Contain("你的最终排名：第 " + playerRank + " 名"));
            Assert.That(view.PlayerSummary, Does.Contain("总分 " + playerPoints).And.Contain("最终第 " + playerRank + " 名"));
            Assert.That(view.Calendar.Count(c => c == '✓'), Is.EqualTo(8));

            string token = store.Json;
            int attempts = store.WriteAttempts;
            var service = new CareerModeService(repository);
            Assert.That(CareerModeRules.GetNextTrackId(completed), Is.Empty);
            Assert.That(CareerModeRules.CanStartNextRace(completed), Is.False);
            Assert.That(CareerModeRules.CanAdjustTechTree(completed), Is.False);
            Assert.That(launch.Matches(completed), Is.False);
            Assert.That(CareerRaceLaunchRequest.TryCreate(completed, "native-ninth", out CareerRaceLaunchRequest ninth), Is.False);
            Assert.That(ninth, Is.Null);
            Assert.That(CareerRaceLaunchState.Request(completed, "native-ninth"), Is.False);
            Assert.That(CareerRaceLaunchState.Current, Is.SameAs(launch));
            Assert.That(CareerRaceLaunchState.IsActive, Is.True);
            Assert.That(CareerRaceLaunchState.IsRequested, Is.False);
            Assert.That(service.TryConfirmSummerBreak(launch.TechSnapshot), Is.False);
            Assert.That(service.TryRecordRace(completed.RaceResults[7]), Is.False);
            var invalidNinth = new CareerRaceResult("native-ninth", launch.TrackId, completed.RaceResults[7].Standings);
            Assert.That(service.TryRecordRace(invalidNinth), Is.False);
            var terminal = manager.Session.Players.Select(p => Tuple.Create(p.position, p.lap,
                p.finishOrder, p.isBlown, p.hasFinished, p.techState.rpBalance, p.driverXp)).ToArray();
            var resultEntry = typeof(MVPGameManager).GetMethod("ShowGameOver", PrivateInstance);
            for (int repeat = 0; repeat < 2; repeat++) resultEntry.Invoke(manager, null);
            Assert.That(store.Json, Is.EqualTo(token));
            Assert.That(store.WriteAttempts, Is.EqualTo(attempts));
            AssertSettledOnce();
            Assert.That(manager.Session.Players.Select(p => Tuple.Create(p.position, p.lap,
                p.finishOrder, p.isBlown, p.hasFinished, p.techState.rpBalance, p.driverXp)), Is.EqualTo(terminal));
            Assert.That(File.ReadAllText(logPath), Is.EqualTo(closedTrace));
            TestContext.WriteLine("Native finale: " + field[0] + "; champion=" + expected[0].Team +
                "; playerPoints=" + playerPoints + "; playerRank=" + playerRank +
                "; season=Completed; summer=locked; ninth=blocked; result=one-save; repeats=no-write.");
        }
    }
}
