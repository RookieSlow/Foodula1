using System;
using System.Linq;
using NUnit.Framework;

public sealed class RaceLogStartupRulesTests
{
    [TestCase(true, true, "loaded-id", "Loaded", "loaded-id", "Loaded")]
    [TestCase(true, false, "manager-id", null, "manager-id", "manager-id")]
    [TestCase(false, false, null, null, "fallback-id", "fallback-id")]
    [TestCase(false, true, null, "Loaded", "fallback-id", "Loaded")]
    [TestCase(true, true, "loaded-id", null, "loaded-id", null)]
    [TestCase(true, false, "", null, "", "")]
    public void TrackIdentityPreservesExistingActiveAndFallbackPrecedence(
        bool manager, bool loaded, string activeId, string loadedName,
        string expectedId, string expectedName)
    {
        var track = RaceLogStartupRules.ResolveTrack(
            manager, activeId, loaded, loadedName, "fallback-id");
        Assert.That(track.Id, Is.EqualTo(expectedId));
        Assert.That(track.Name, Is.EqualTo(expectedName));
    }

    [TestCase(true, true, true, "[TUTORIAL_SETUP]")]
    [TestCase(true, false, false, "[TUTORIAL_SETUP]")]
    [TestCase(false, true, true, "[CAREER_SETUP]")]
    [TestCase(false, true, false, "[CAREER_SETUP]")]
    [TestCase(false, false, true, "[FREE_RACE_SETUP]")]
    public void SetupSelectsOnlyHighestPriorityPresentMode(
        bool tutorial, bool career, bool freeRace, string prefix)
    {
        TutorialScenarioDefinition scenario = tutorial
            ? TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.US) : null;
        CareerRaceLaunchRequest launch = career ? CreateCareerLaunch() : null;
        FreeRaceRosterEntry[] roster = freeRace
            ? new[] { new FreeRaceRosterEntry(TeamId.US, "missing-driver") } : null;

        string[] lines = RaceLogStartupRules.BuildSetupLines(
            scenario, TutorialRunPhase.Practice, launch, roster).ToArray();

        Assert.That(lines, Is.Not.Empty);
        Assert.That(lines.All(line => line.StartsWith(prefix)), Is.True);
        if (tutorial)
            Assert.That(lines[0], Does.Contain("phase=Practice"));
        if (!tutorial && career)
            Assert.That(lines.Single(), Does.Contain("result_id=startup-test"));
        if (!tutorial && !career)
            Assert.That(lines.Single(), Does.Contain("player=US:missing-driver"));
    }

    [Test]
    public void NoModeProducesNoSetupLines()
    {
        Assert.That(RaceLogStartupRules.BuildSetupLines(
            null, TutorialRunPhase.Guided, null, null), Is.Empty);
    }

    [Test]
    public void InvalidFreeRosterStillFailsWhenEnumerated()
    {
        var lines = RaceLogStartupRules.BuildSetupLines(
            null, TutorialRunPhase.Guided, null, Array.Empty<FreeRaceRosterEntry>());
        Assert.Throws<IndexOutOfRangeException>(() => lines.ToArray());
    }

    private static CareerRaceLaunchRequest CreateCareerLaunch()
    {
        var season = new CareerSeasonState();
        Assert.That(CareerModeRules.TryStartSeason(season, TeamId.US,
            new[] { TeamId.US, TeamId.UK, TeamId.IT, TeamId.DE }), Is.True);
        Assert.That(CareerRaceLaunchRequest.TryCreate(season, "startup-test", out var launch), Is.True);
        return launch;
    }
}
