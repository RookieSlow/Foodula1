using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Active standalone career Canvas in a preview scene: render to a fixed-size
/// target, hit-test real button geometry, then dispatch uGUI left-pointer clicks.
/// The four/eight-result histories are seeded through the
/// service, not raced here. This is not OS input, animation/audio, MainMenu
/// scene startup, real PlayerPrefs persistence or full-season Play Mode evidence.
/// </summary>
public sealed partial class CareerCompletedMenuPointerTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo AudioInstance = typeof(AudioService)
        .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
    private GameObject root;
    private CareerUiPointerTestScope pointerScope;
    private EventSystem eventSystem => pointerScope.EventSystem;
    private CareerModeUI ui;
    private CareerRepository repository;
    private CareerMemoryStore store;
    private RaceStartupTestScope startupScope;
    private object previousAudio;
    private Dictionary<TeamId, TechTreeState> cache;
    private readonly Dictionary<TeamId, TechTreeState> previousProfiles = new Dictionary<TeamId, TechTreeState>();
    private readonly Dictionary<TeamId, string> profileTokens = new Dictionary<TeamId, string>();
    private string completedToken;
    private int launchCalls;

    [SetUp]
    public void SetUp()
    {
        startupScope = new RaceStartupTestScope();
        previousAudio = AudioInstance.GetValue(null);
        AudioInstance.SetValue(null, null);
        cache = (Dictionary<TeamId, TechTreeState>)typeof(TechTreeProfileStore)
            .GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        previousProfiles.Clear();
        foreach (var pair in cache) previousProfiles.Add(pair.Key, pair.Value);
        cache.Clear();
        profileTokens.Clear();
        TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
        foreach (TeamId team in Enum.GetValues(typeof(TeamId)))
        {
            // Every possible UI selection is cached before the real creation
            // callback. GetOrCreate therefore cannot fall through to PlayerPrefs.
            cache.Add(team, TechTreeRules.CreateDemoProfile(team, database));
            profileTokens.Add(team, TechTreeProfileCodec.Encode(cache[team]));
        }

        pointerScope = CareerUiPointerTestScope.Create(() =>
            AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
        root = pointerScope.Root;
        ui = root.AddComponent<CareerModeUI>();
        store = new CareerMemoryStore();
        repository = new CareerRepository(store, new JsonUtilityCareerSerializer(),
            state => CareerTechSnapshotMapper.IsSeasonValidForDatabase(state, database));
        launchCalls = 0;
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            pointerScope?.Dispose();
        }
        finally
        {
            if (cache != null)
            {
                cache.Clear();
                foreach (var pair in previousProfiles) cache.Add(pair.Key, pair.Value);
            }
            AudioInstance.SetValue(null, previousAudio);
            startupScope?.Dispose();
        }
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.DE)]
    public void CompletedPrimary_CancelPreservesFinalStandingsAndDoesNotLaunchNinthRace(TeamId team)
    {
        ShowCompleted(team);
        AssertCompletedPage(team);
        Click("CareerPrimary");
        AssertSelection(team);
        TeamId replacement = OtherTeam(team);
        Click("CareerTeam_" + replacement);
        AssertSelection(replacement);
        Click("CareerCreate");
        AssertConfirmation("覆盖");
        AssertUnchangedCompleted(team, 0);
        Click("CareerConfirmCancel");
        Assert.That(Find("CareerConfirmation").activeInHierarchy, Is.False);
        DispatchLateConfirmation();
        AssertUnchangedCompleted(team, 0);
        Click("CareerClose");
        ui.Show();
        AssertCompletedPage(team);
        AssertUnchangedCompleted(team, 0);
        AssertProfilesUnchanged();
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.DE)]
    public void CloseAndReopen_InvalidatesBothReplacementAndAbandonConfirmation(TeamId team)
    {
        ShowCompleted(team);
        Click("CareerNewSeason");
        Click("CareerTeam_" + OtherTeam(team));
        Click("CareerCreate");
        AssertConfirmation("覆盖");
        CloseReopenAndRejectLateConfirmation(team);
        Click("CareerAbandon");
        AssertConfirmation("放弃");
        CloseReopenAndRejectLateConfirmation(team);
        Click("CareerPrimary");
        AssertSelection(team); // Previous replacement selection is not resumed.
        AssertProfilesUnchanged();
    }

    [TestCase(TeamId.CN)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.JP)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.DE)]
    public void ConfirmReplacement_FailedWritePreservesCompletedThenRetryCreatesOneFreshSeason(TeamId team)
    {
        ShowCompleted(team);
        Click("CareerPrimary");
        TeamId replacement = OtherTeam(team);
        Click("CareerTeam_" + replacement);
        Click("CareerCreate");
        AssertConfirmation("覆盖");
        store.FailWrites = true;
        Click("CareerConfirmAction");
        Assert.That(Find("CareerConfirmation").activeInHierarchy, Is.False);
        AssertSelection(replacement);
        AssertUnchangedCompleted(team, 1);
        DispatchLateConfirmation();
        AssertUnchangedCompleted(team, 1);

        store.FailWrites = false;
        Click("CareerCreate");
        AssertConfirmation("覆盖");
        AssertUnchangedCompleted(team, 1); // Retry still requires a fresh confirmation.
        Click("CareerConfirmAction");
        Assert.That(store.WriteAttempts, Is.EqualTo(2));
        Assert.That(store.SuccessfulWrites, Is.EqualTo(1));
        Assert.That(store.DeleteAttempts, Is.Zero);
        Assert.That(store.Token, Is.Not.EqualTo(completedToken));
        CareerSeasonState state = Service.CurrentState;
        Assert.That(repository.Load().Status, Is.EqualTo(CareerLoadStatus.Loaded));
        Assert.That(Serialize(repository.Load().State), Is.EqualTo(Serialize(state)));
        Assert.That(state.Phase, Is.EqualTo(CareerPhase.Racing));
        Assert.That(state.LockedTeam, Is.EqualTo(replacement));
        Assert.That(state.NextTrackIndex, Is.Zero);
        Assert.That(state.RaceResults, Is.Empty);
        Assert.That(state.SummerBreakUsed, Is.False);
        Assert.That(state.SummerBreakTechSnapshot, Is.Null);
        CollectionAssert.AreEqual(CareerMenuPresentation.BuildCompetitorField(replacement), state.Competitors);
        Assert.That(CareerTechSnapshotMapper.TryCapture(cache[replacement], out CareerTechSnapshot expected), Is.True);
        AssertSnapshot(expected, state.InitialTechSnapshot);
        AssertSnapshot(expected, state.ActiveTechSnapshot);
        Assert.That(state.InitialTechSnapshot, Is.Not.SameAs(state.ActiveTechSnapshot));
        Assert.That(Find("CareerOverview").activeInHierarchy, Is.True);
        Assert.That(Find("CareerTeamSelection").activeInHierarchy, Is.False);
        Assert.That(Button("CareerPrimary").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("开始第 1 站"));
        string freshToken = store.Token;
        DispatchLateConfirmation();
        ui.Hide();
        ui.Show();
        Assert.That(store.Token, Is.EqualTo(freshToken));
        Assert.That(store.WriteAttempts, Is.EqualTo(2));
        AssertNoLaunch();
        AssertProfilesUnchanged();
    }

    private CareerModeService Service => (CareerModeService)typeof(CareerModeUI).GetField("service", Private).GetValue(ui);

    private void ShowCompleted(TeamId team)
    {
        ShowSeason(team, 8);
        completedToken = store.Token;
    }

    private void ShowSeason(TeamId team, int races)
    {
        var seed = new CareerModeService(repository);
        Assert.That(CareerTechSnapshotMapper.TryCapture(new TechTreeState(team, 25000), out CareerTechSnapshot initial), Is.True);
        TeamId[] field = CareerMenuPresentation.BuildCompetitorField(team);
        Assert.That(seed.TryCreateNew(team, field, initial, false), Is.True);
        for (int race = 0; race < races; race++)
        {
            if (seed.CurrentState.Phase == CareerPhase.SummerBreak)
                Assert.That(seed.TryConfirmSummerBreak(initial), Is.True);
            Assert.That(seed.TryRecordRace(new CareerRaceResult("pointer-history-" + race,
                CareerModeRules.TrackSchedule[race], new[]
                {
                    new CareerCompetitorResult(field[1], 1), new CareerCompetitorResult(team, 2),
                    new CareerCompetitorResult(field[2], 3), new CareerCompetitorResult(field[3], 4)
                })), Is.True);
        }
        store.ResetCounts();
        ui.Initialize(repository, true, () => launchCalls++);
        ui.Show();
    }

    private void AssertCompletedPage(TeamId team)
    {
        Assert.That(Find("CareerOverview").activeInHierarchy, Is.True);
        Assert.That(Find("CareerTeamSelection").activeInHierarchy, Is.False);
        Assert.That(Find("CareerConfirmation").activeInHierarchy, Is.False);
        Assert.That(Text("CareerPlayerSummary"), Is.EqualTo(
            "锁定车队：" + CareerMenuPresentation.GetTeamLabel(team) + "　总分 48　最终第 2 名"));
        TeamId champion = CareerMenuPresentation.BuildCompetitorField(team)[1];
        Assert.That(Text("CareerStatus"), Is.EqualTo("八站生涯已完成。总冠军：" +
            CareerMenuPresentation.GetTeamLabel(champion) + "（80 分）；你的最终排名：第 2 名。"));
        string[] rows = Text("CareerStandings").Split('\n');
        Assert.That(rows.Length, Is.EqualTo(4));
        Assert.That(rows[0], Does.StartWith("1. " + CareerMenuPresentation.GetTeamLabel(champion)));
        Assert.That(rows[0], Does.Contain("80 分  8 胜"));
        Assert.That(rows[1], Does.StartWith("2. " + CareerMenuPresentation.GetTeamLabel(team)));
        Assert.That(rows[1], Does.Contain("48 分  0 胜"));
        Assert.That(Text("CareerCalendar").Count(character => character == '✓'), Is.EqualTo(8));
        Assert.That(Text("CareerCalendar"), Does.Not.Contain("▶"));
        Assert.That(Button("CareerPrimary").interactable, Is.True);
        Assert.That(Button("CareerPrimary").GetComponentInChildren<TMP_Text>().text, Is.EqualTo("开启新一轮生涯"));
    }

    private void AssertSelection(TeamId selected)
    {
        Assert.That(Find("CareerTeamSelection").activeInHierarchy, Is.True);
        Assert.That(Find("CareerOverview").activeInHierarchy, Is.False);
        Assert.That(Text("CareerSelectionStatus"), Does.Contain("当前选择：" + CareerMenuPresentation.GetTeamLabel(selected)));
    }

    private void AssertConfirmation(string action)
    {
        Assert.That(Find("CareerConfirmation").activeInHierarchy, Is.True);
        Assert.That(Text("ConfirmationText"), Does.Contain(action));
        Assert.That(Text("ConfirmationText"), Does.Contain("是否"));
    }

    private void CloseReopenAndRejectLateConfirmation(TeamId team)
    {
        Click("CareerClose");
        Assert.That(Find("CareerModeOverlay").activeInHierarchy, Is.False);
        Assert.That(Text("ConfirmationText"), Is.Empty);
        DispatchLateConfirmation();
        ui.Show();
        AssertCompletedPage(team);
        DispatchLateConfirmation();
        AssertUnchangedCompleted(team, 0);
    }

    private void AssertUnchangedCompleted(TeamId team, int attempts)
    {
        Assert.That(store.Token, Is.EqualTo(completedToken));
        Assert.That(Serialize(Service.CurrentState), Is.EqualTo(completedToken));
        Assert.That(Service.CurrentState.Phase, Is.EqualTo(CareerPhase.Completed));
        Assert.That(Service.CurrentState.LockedTeam, Is.EqualTo(team));
        Assert.That(Service.CurrentState.NextTrackIndex, Is.EqualTo(8));
        Assert.That(store.WriteAttempts, Is.EqualTo(attempts));
        Assert.That(store.SuccessfulWrites, Is.Zero);
        Assert.That(store.DeleteAttempts, Is.Zero);
        AssertNoLaunch();
    }

    private void AssertNoLaunch()
    {
        Assert.That(launchCalls, Is.Zero);
        Assert.That(CareerRaceLaunchState.Current, Is.Null);
        Assert.That(CareerRaceLaunchState.IsCareerMode, Is.False);
    }

    private void AssertProfilesUnchanged()
    {
        Assert.That(cache.Count, Is.EqualTo(6));
        foreach (var pair in profileTokens)
            Assert.That(TechTreeProfileCodec.Encode(cache[pair.Key]), Is.EqualTo(pair.Value), pair.Key.ToString());
    }

    private GameObject Find(string name) => root.GetComponentsInChildren<Transform>(true)
        .Single(item => item.name == name).gameObject;
    private Button Button(string name) => Find(name).GetComponent<Button>();
    private string Text(string name) => Find(name).GetComponent<TMP_Text>().text;
    private static TeamId OtherTeam(TeamId team) => team == TeamId.CN ? TeamId.US : TeamId.CN;

    private void Click(string name) => pointerScope.Click(Button(name));

    private void DispatchLateConfirmation()
    {
        // An already queued event must not invoke an inactive confirmation.
        Button confirm = Button("CareerConfirmAction");
        Assert.That(confirm.IsActive(), Is.False);
        ExecuteEvents.Execute(confirm.gameObject, new PointerEventData(eventSystem)
            { button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
    }

    private static string Serialize(CareerSeasonState state)
    {
        Assert.That(CareerSaveCodec.TryToData(state, out CareerSaveData data), Is.True);
        return new JsonUtilityCareerSerializer().Serialize(data);
    }

    private static void AssertSnapshot(CareerTechSnapshot expected, CareerTechSnapshot actual)
    {
        Assert.That(actual.TeamId, Is.EqualTo(expected.TeamId));
        Assert.That(actual.RpBalance, Is.EqualTo(expected.RpBalance));
        CollectionAssert.AreEqual(expected.UnlockedNodeIds, actual.UnlockedNodeIds);
        CollectionAssert.AreEqual(expected.ActiveNodeIds, actual.ActiveNodeIds);
        Assert.That(actual.SunNeverSetsTarget, Is.EqualTo(expected.SunNeverSetsTarget));
    }

    private sealed class CareerMemoryStore : ICareerKeyValueStore
    {
        internal string Token;
        internal bool FailWrites;
        internal int WriteAttempts;
        internal int SuccessfulWrites;
        internal int DeleteAttempts;
        public bool HasKey(string key) { RequireCareerKey(key); return Token != null; }
        public string GetString(string key) { RequireCareerKey(key); return Token; }
        public bool TrySetAndSave(string key, string value)
        {
            RequireCareerKey(key);
            WriteAttempts++;
            if (FailWrites) return false;
            Token = value;
            SuccessfulWrites++;
            return true;
        }
        public bool TryDeleteAndSave(string key)
        {
            RequireCareerKey(key);
            DeleteAttempts++;
            throw new InvalidOperationException("These flows must not delete a completed career.");
        }
        internal void ResetCounts() { WriteAttempts = 0; SuccessfulWrites = 0; DeleteAttempts = 0; }
        private static void RequireCareerKey(string key)
        {
            if (key != CareerRepository.SaveKey) throw new InvalidOperationException("Non-career key: " + key);
        }
    }
}
