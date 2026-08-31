using System;
using System.Collections.Generic;
using NUnit.Framework;

public class CareerModeRulesTests
{
    private static readonly TeamId[] DefaultField =
    {
        TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US
    };

    [Test]
    public void TrackSchedule_ReusesEightUniqueOfficialTracksInCatalogOrder()
    {
        Assert.That(CareerModeRules.TrackSchedule.Count, Is.EqualTo(8));
        Assert.That(
            new HashSet<string>(CareerModeRules.TrackSchedule).Count,
            Is.EqualTo(8));

        for (int i = 0; i < TrackSelectionState.AvailableTracks.Count; i++)
        {
            Assert.That(
                CareerModeRules.TrackSchedule[i],
                Is.EqualTo(TrackSelectionState.AvailableTracks[i].TrackId));
        }
    }

    [Test]
    public void TryStartSeason_AllowsEveryTeamAndLocksTheSelection()
    {
        TeamId[] teams = (TeamId[])Enum.GetValues(typeof(TeamId));
        for (int playerIndex = 0; playerIndex < teams.Length; playerIndex++)
        {
            var field = new List<TeamId> { teams[playerIndex] };
            for (int offset = 1; field.Count < 4; offset++)
                field.Add(teams[(playerIndex + offset) % teams.Length]);

            var state = new CareerSeasonState();
            Assert.That(
                CareerModeRules.TryStartSeason(state, teams[playerIndex], field),
                Is.True,
                teams[playerIndex].ToString());
            Assert.That(state.HasLockedTeam, Is.True);
            Assert.That(state.LockedTeam, Is.EqualTo(teams[playerIndex]));
            Assert.That(state.Phase, Is.EqualTo(CareerPhase.Racing));

            Assert.That(
                CareerModeRules.TrySetPlayerTeam(state, teams[(playerIndex + 1) % teams.Length]),
                Is.False,
                "The rules layer must reject a team change after confirmation.");
            Assert.That(state.LockedTeam, Is.EqualTo(teams[playerIndex]));
        }
    }

    [Test]
    public void TryStartSeason_RejectsDuplicateOrPlayerlessField()
    {
        var state = new CareerSeasonState();
        Assert.That(
            CareerModeRules.TryStartSeason(
                state,
                TeamId.UK,
                new[] { TeamId.UK, TeamId.UK, TeamId.IT, TeamId.US }),
            Is.False);
        Assert.That(
            CareerModeRules.TryStartSeason(
                state,
                TeamId.JP,
                new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US }),
            Is.False);
        Assert.That(state.Phase, Is.EqualTo(CareerPhase.NotStarted));
    }

    [TestCase(1, false, 10)]
    [TestCase(2, false, 6)]
    [TestCase(3, false, 4)]
    [TestCase(4, false, 2)]
    [TestCase(0, false, 0)]
    [TestCase(5, false, 0)]
    [TestCase(1, true, 0)]
    public void GetPointsForFinish_UsesCentralFourCarTable(
        int finishPosition,
        bool didNotFinish,
        int expected)
    {
        Assert.That(
            CareerModeRules.GetPointsForFinish(finishPosition, didNotFinish),
            Is.EqualTo(expected));
    }

    [Test]
    public void TryRecordRace_RejectsWrongTrackAndDuplicateCallback()
    {
        CareerSeasonState state = StartDefaultSeason();
        CareerRaceResult wrongTrack = BuildResult("race-1", "fallback_42", 0, 1, 2, 3);
        Assert.That(CareerModeRules.TryRecordRace(state, wrongTrack), Is.False);
        Assert.That(state.NextTrackIndex, Is.Zero);

        CareerRaceResult valid = BuildExpectedResult(state, "race-1", 0, 1, 2, 3);
        Assert.That(CareerModeRules.TryRecordRace(state, valid), Is.True);
        Assert.That(state.NextTrackIndex, Is.EqualTo(1));
        Assert.That(CareerModeRules.TryRecordRace(state, valid), Is.False);
        Assert.That(state.NextTrackIndex, Is.EqualTo(1));
        Assert.That(state.RaceResults.Count, Is.EqualTo(1));
    }

    [Test]
    public void SummerBreak_OpensOnlyAfterRaceFourAndIsConsumedOnce()
    {
        CareerSeasonState state = StartDefaultSeason();
        Assert.That(CareerModeRules.CanAdjustTechTree(state), Is.False);

        for (int race = 0; race < 3; race++)
        {
            Assert.That(
                CareerModeRules.TryRecordRace(
                    state,
                    BuildExpectedResult(state, $"race-{race + 1}", 0, 1, 2, 3)),
                Is.True);
            Assert.That(CareerModeRules.CanAdjustTechTree(state), Is.False);
            Assert.That(state.Phase, Is.EqualTo(CareerPhase.Racing));
        }

        Assert.That(
            CareerModeRules.TryRecordRace(
                state,
                BuildExpectedResult(state, "race-4", 0, 1, 2, 3)),
            Is.True);
        Assert.That(state.NextTrackIndex, Is.EqualTo(4));
        Assert.That(state.Phase, Is.EqualTo(CareerPhase.SummerBreak));
        Assert.That(CareerModeRules.CanStartNextRace(state), Is.False);
        Assert.That(CareerModeRules.CanAdjustTechTree(state), Is.True);

        Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(state), Is.True);
        Assert.That(state.SummerBreakUsed, Is.True);
        Assert.That(state.Phase, Is.EqualTo(CareerPhase.Racing));
        Assert.That(CareerModeRules.CanAdjustTechTree(state), Is.False);
        Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(state), Is.False);
    }

    [Test]
    public void EighthRace_CompletesSeasonWithoutAnotherTechWindow()
    {
        CareerSeasonState state = StartDefaultSeason();
        for (int race = 0; race < CareerModeRules.RaceCount; race++)
        {
            Assert.That(
                CareerModeRules.TryRecordRace(
                    state,
                    BuildExpectedResult(state, $"race-{race + 1}", 0, 1, 2, 3)),
                Is.True);

            if (state.Phase == CareerPhase.SummerBreak)
                Assert.That(CareerModeRules.ConfirmSummerBreakTechTree(state), Is.True);
        }

        Assert.That(state.NextTrackIndex, Is.EqualTo(8));
        Assert.That(state.RaceResults.Count, Is.EqualTo(8));
        Assert.That(state.Phase, Is.EqualTo(CareerPhase.Completed));
        Assert.That(CareerModeRules.CanStartNextRace(state), Is.False);
        Assert.That(CareerModeRules.CanAdjustTechTree(state), Is.False);
        Assert.That(CareerModeRules.GetNextTrackId(state), Is.Empty);
    }

    [Test]
    public void GetStandings_UsesPointsThenWinsAsStableTieBreaker()
    {
        CareerSeasonState state = StartDefaultSeason();
        Assert.That(
            CareerModeRules.TryRecordRace(
                state,
                new CareerRaceResult(
                    "race-1",
                    CareerModeRules.GetNextTrackId(state),
                    new[]
                    {
                        new CareerCompetitorResult(TeamId.UK, 1),
                        new CareerCompetitorResult(TeamId.DE, 2),
                        new CareerCompetitorResult(TeamId.US, 3),
                        new CareerCompetitorResult(TeamId.IT, 0, true)
                    })),
            Is.True);
        Assert.That(
            CareerModeRules.TryRecordRace(
                state,
                BuildExpectedResult(state, "race-2", 2, 1, 3, 0)),
            Is.True);

        List<CareerStanding> table = CareerModeRules.GetStandings(state);

        Assert.That(table[0].TeamId, Is.EqualTo(TeamId.UK));
        Assert.That(table[0].Points, Is.EqualTo(12));
        Assert.That(table[0].Wins, Is.EqualTo(1));
        Assert.That(table[1].TeamId, Is.EqualTo(TeamId.DE));
        Assert.That(table[1].Points, Is.EqualTo(12));
        Assert.That(table[1].Wins, Is.Zero);
        Assert.That(table[0].Rank, Is.EqualTo(1));
        Assert.That(table[1].Rank, Is.EqualTo(2));
    }

    [Test]
    public void TryRecordRace_AcceptsMultipleDnfEntriesAndAwardsZero()
    {
        CareerSeasonState state = StartDefaultSeason();
        var result = new CareerRaceResult(
            "race-1",
            CareerModeRules.TrackSchedule[0],
            new[]
            {
                new CareerCompetitorResult(TeamId.UK, 1),
                new CareerCompetitorResult(TeamId.DE, 2),
                new CareerCompetitorResult(TeamId.IT, 0, true),
                new CareerCompetitorResult(TeamId.US, 0, true)
            });

        Assert.That(CareerModeRules.TryRecordRace(state, result), Is.True);
        List<CareerStanding> table = CareerModeRules.GetStandings(state);
        Assert.That(table.Find(entry => entry.TeamId == TeamId.IT).Points, Is.Zero);
        Assert.That(table.Find(entry => entry.TeamId == TeamId.US).Points, Is.Zero);
    }

    [Test]
    public void TryRecordRace_RejectsGappedClassifiedPositions()
    {
        CareerSeasonState state = StartDefaultSeason();
        var result = new CareerRaceResult(
            "race-1",
            CareerModeRules.TrackSchedule[0],
            new[]
            {
                new CareerCompetitorResult(TeamId.UK, 1),
                new CareerCompetitorResult(TeamId.DE, 4),
                new CareerCompetitorResult(TeamId.IT, 0, true),
                new CareerCompetitorResult(TeamId.US, 0, true)
            });

        Assert.That(CareerModeRules.TryRecordRace(state, result), Is.False);
        Assert.That(state.NextTrackIndex, Is.Zero);
        Assert.That(state.RaceResults, Is.Empty);
    }

    private static CareerSeasonState StartDefaultSeason()
    {
        var state = new CareerSeasonState();
        Assert.That(
            CareerModeRules.TryStartSeason(state, TeamId.UK, DefaultField),
            Is.True);
        return state;
    }

    private static CareerRaceResult BuildExpectedResult(
        CareerSeasonState state,
        string resultId,
        params int[] teamIndexesByPosition)
    {
        return BuildResult(
            resultId,
            CareerModeRules.GetNextTrackId(state),
            teamIndexesByPosition);
    }

    private static CareerRaceResult BuildResult(
        string resultId,
        string trackId,
        params int[] teamIndexesByPosition)
    {
        var entries = new List<CareerCompetitorResult>();
        for (int positionIndex = 0; positionIndex < teamIndexesByPosition.Length; positionIndex++)
        {
            entries.Add(new CareerCompetitorResult(
                DefaultField[teamIndexesByPosition[positionIndex]],
                positionIndex + 1));
        }

        return new CareerRaceResult(resultId, trackId, entries);
    }
}
