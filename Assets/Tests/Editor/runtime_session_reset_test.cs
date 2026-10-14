using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class RuntimeSessionResetTests
{
    private readonly Dictionary<FieldInfo, object> saved = new Dictionary<FieldInfo, object>();
    private Dictionary<TeamId, TechTreeState> profiles;
    private Dictionary<TeamId, TechTreeState> savedProfiles;
    private List<TeamId> teams;
    private List<TeamId> savedTeams;
    private Dictionary<TeamId, string> drivers;
    private Dictionary<TeamId, string> savedDrivers;
    private const BindingFlags Fields = BindingFlags.NonPublic | BindingFlags.Static;

    [SetUp]
    public void SetUp()
    {
        foreach (Type type in new[] { typeof(TrackSelectionState), typeof(DriverSelectionState),
            typeof(FreeRaceRosterState), typeof(TutorialLaunchState), typeof(CareerRaceLaunchState), typeof(GameSettingsRuntime) })
            foreach (FieldInfo field in type.GetFields(Fields))
                if (!field.IsInitOnly && !field.IsLiteral) saved.Add(field, field.GetValue(null));
        profiles = (Dictionary<TeamId, TechTreeState>)typeof(TechTreeProfileStore).GetField("Cache", Fields).GetValue(null);
        savedProfiles = new Dictionary<TeamId, TechTreeState>(profiles);
        teams = (List<TeamId>)typeof(FreeRaceRosterState).GetField("selectedTeams", Fields).GetValue(null);
        savedTeams = new List<TeamId>(teams);
        drivers = (Dictionary<TeamId, string>)typeof(FreeRaceRosterState).GetField("driverByTeam", Fields).GetValue(null);
        savedDrivers = new Dictionary<TeamId, string>(drivers);
    }

    [TearDown]
    public void TearDown()
    {
        foreach (var entry in saved) entry.Key.SetValue(null, entry.Value);
        saved.Clear();
        profiles.Clear(); foreach (var entry in savedProfiles) profiles.Add(entry.Key, entry.Value);
        teams.Clear(); teams.AddRange(savedTeams);
        drivers.Clear(); foreach (var entry in savedDrivers) drivers.Add(entry.Key, entry.Value);
    }

    [TestCase(false)] [TestCase(true)]
    public void FreshSessionClearsLaunchesSelectionsAndCachesWithoutDeletingSaves(bool activeTutorial)
    {
        TrackSelectionState.TrySelect("silverstone_afternoon_tea");
        DriverSelectionState.TrySelect(DriverCatalog.GetDefaultForTeam(TeamId.US).Id);
        FreeRaceRosterState.InitializeDefault(DriverCatalog.GetDefaultForTeam(TeamId.US), new[] { TeamId.UK });
        TutorialLaunchState.Request(TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.CN));
        if (activeTutorial) TutorialLaunchState.ActivateRequested();
        var career = new CareerSeasonState();
        Assert.IsTrue(CareerModeRules.TryStartSeason(career, TeamId.US, new[] { TeamId.US, TeamId.UK }));
        Assert.IsTrue(CareerRaceLaunchState.Request(career, "reset-regression"));
        if (activeTutorial) CareerRaceLaunchState.ActivateRequested();
        profiles[TeamId.US] = new TechTreeState(TeamId.US, 77);
        typeof(GameSettingsRuntime).GetField("current", Fields).SetValue(null, GameSettingsData.CreateDefault());
        string[] keys = { "Foodula1.Settings.V1", CareerRepository.SaveKey,
            "Foodula1.TechTree.US", "Foodular1.TechTree.US" };
        var preferences = new Dictionary<string, string>();
        foreach (string key in keys) preferences.Add(key, PlayerPrefs.GetString(key, "missing"));

        RuntimeSessionReset.Reset();
        RuntimeSessionReset.Reset();

        Assert.That(TrackSelectionState.SelectedTrackId, Is.Empty);
        Assert.That(DriverSelectionState.SelectedDriverId, Is.Empty);
        Assert.That(FreeRaceRosterState.IsConfigured, Is.False);
        Assert.That(FreeRaceRosterState.IsThunderstorm, Is.False);
        Assert.That(FreeRaceRosterState.SelectedTeams, Is.Empty);
        Assert.That(TutorialLaunchState.IsTutorialMode, Is.False);
        Assert.That(CareerRaceLaunchState.IsCareerMode, Is.False);
        Assert.That(profiles, Is.Empty);
        Assert.That(typeof(GameSettingsRuntime).GetField("current", Fields).GetValue(null), Is.Null);
        Assert.That(typeof(GameSettingsRuntime).GetField("repository", Fields).GetValue(null), Is.Null);
        foreach (string key in keys)
            Assert.That(PlayerPrefs.GetString(key, "missing"), Is.EqualTo(preferences[key]));
    }
}
