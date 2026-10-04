using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class ReturnToMenuConfirmationTests
{
    [Test]
    public void race_return_button_opens_modal_without_loading_scene()
    {
        GameObject root = CreateRoot(out HUDUI hud, out Button returnButton, out _);
        try
        {
            string activeSceneBefore = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            InvokeStart(hud);
            Assert.That(hud.returnToMenuConfirmPanel, Is.Not.Null);
            Assert.That(hud.returnToMenuConfirmText, Is.Not.Null);
            Assert.That(hud.returnToMenuConfirmText.text, Does.Contain("退出当前比赛"));
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);

            returnButton.onClick.Invoke();

            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                Is.EqualTo(activeSceneBefore));
            Assert.That(hud.returnToMenuConfirmPanel.GetComponent<Image>().raycastTarget, Is.True,
                "The full-screen modal must block accidental clicks on race controls.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void cancel_closes_modal_and_second_return_click_can_reopen_it()
    {
        GameObject root = CreateRoot(out HUDUI hud, out Button returnButton, out _);
        try
        {
            InvokeStart(hud);

            returnButton.onClick.Invoke();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);

            hud.cancelReturnToMenuButton.onClick.Invoke();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);

            returnButton.onClick.Invoke();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void game_over_return_button_uses_the_same_confirmation_gate()
    {
        GameObject root = CreateRoot(out HUDUI hud, out _, out Button backToMenuButton);
        try
        {
            InvokeStart(hud);

            backToMenuButton.onClick.Invoke();

            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(hud.confirmReturnToMenuButton, Is.Not.Null);
            Assert.That(hud.cancelReturnToMenuButton, Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void space_adapter_never_confirms_destructive_return_prompt()
    {
        GameObject root = CreateRoot(out HUDUI hud, out Button returnButton, out _);
        try
        {
            InvokeStart(hud);
            returnButton.onClick.Invoke();

            Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateRoot(out HUDUI hud, out Button returnButton, out Button backToMenuButton)
    {
        GameObject root = new GameObject("ReturnToMenuConfirmationTest", typeof(RectTransform));
        root.SetActive(false);
        hud = root.AddComponent<HUDUI>();
        returnButton = CreateButton(root.transform, "ReturnToMenuBtn");
        backToMenuButton = CreateButton(root.transform, "BackToMenuBtn");
        hud.returnToMenuButton = returnButton;
        hud.backToMenuButton = backToMenuButton;
        return root;
    }

    private static Button CreateButton(Transform parent, string name)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return buttonObject.GetComponent<Button>();
    }

    private static void InvokeStart(HUDUI hud)
    {
        hud.gameObject.SetActive(true);
        hud.GetType().GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(hud, null);
    }
}

/// <summary>Real UI/exit adapters with recorded scene I/O, never player storage.</summary>
public sealed class ReturnToMenuLifecycleAdapterTests
{
    private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
    private const BindingFlags InstanceMembers = BindingFlags.Instance | BindingFlags.NonPublic;
    private readonly Dictionary<FieldInfo, object> savedFields = new Dictionary<FieldInfo, object>();
    private List<TeamId> savedTeams;
    private Dictionary<TeamId, string> savedDrivers;
    private GameObject root;
    private HUDUI hud;
    private UnityEngine.Random.State savedRandom;

    private static IEnumerable<TestCaseData> ExitCases()
    {
        foreach (TeamId team in FreeRaceRosterRules.AvailableTeams)
            foreach (bool active in new[] { false, true })
                yield return new TestCaseData(team, active);
    }

    [SetUp]
    public void SetUp()
    {
        savedRandom = UnityEngine.Random.state;
        foreach (Type type in new[] { typeof(TutorialLaunchState), typeof(CareerRaceLaunchState),
                     typeof(FreeRaceRosterState), typeof(GameSettingsRuntime) })
            foreach (FieldInfo field in type.GetFields(StaticFields))
                if (!field.IsInitOnly) savedFields.Add(field, field.GetValue(null));
        savedTeams = Teams().ToList();
        savedDrivers = new Dictionary<TeamId, string>(Drivers());
        typeof(GameSettingsRuntime).GetField("current", StaticFields)
            .SetValue(null, GameSettingsData.CreateDefault());
        root = new GameObject("ExitAdapterTest", typeof(RectTransform));
        root.SetActive(false);
        hud = root.AddComponent<HUDUI>();
        typeof(HUDUI).GetMethod("Start", InstanceMembers).Invoke(hud, null);
    }

    [TearDown]
    public void TearDown()
    {
        if (root != null) Object.DestroyImmediate(root);
        Teams().Clear();
        Teams().AddRange(savedTeams);
        Drivers().Clear();
        foreach (var item in savedDrivers) Drivers().Add(item.Key, item.Value);
        foreach (var item in savedFields) item.Key.SetValue(null, item.Value);
        savedFields.Clear();
        UnityEngine.Random.state = savedRandom;
    }

    [TestCaseSource(nameof(ExitCases))]
    public void CancelPreservesRequests_ConfirmConsumesOnceAndClearsBeforeSceneIO(TeamId team, bool active)
    {
        var tutorial = TutorialScenarioDefinition.CreateTeamSpecialty(team);
        TutorialLaunchState.Request(tutorial);
        var season = new CareerSeasonState();
        TeamId[] field = new[] { team }.Concat(FreeRaceRosterRules.AvailableTeams
            .Where(candidate => candidate != team).Take(3)).ToArray();
        Assert.That(CareerModeRules.TryStartSeason(season, team, field), Is.True);
        Assert.That(CareerRaceLaunchState.Request(season, "exit-" + team), Is.True);
        var career = CareerRaceLaunchState.Current;
        if (active)
        {
            TutorialLaunchState.ActivateRequested();
            CareerRaceLaunchState.ActivateRequested();
        }
        DriverProfile driver = DriverCatalog.GetDefaultForTeam(team);
        if (active) FreeRaceRosterState.InitializeThunderstorm(driver);
        else FreeRaceRosterState.InitializeDefault(driver, field.Skip(1).ToArray());
        var roster = FreeRaceRosterState.SelectedTeams.ToArray();
        var drivers = roster.Select(FreeRaceRosterState.GetDriverId).ToArray();
        string seasonBefore = Serialize(season);
        string track = TrackSelectionState.SelectedTrackId;
        var settings = GameSettingsRuntime.Current;
        string settingsBefore = JsonUtility.ToJson(settings);
        string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
        int loads = 0;
        UnityEngine.Events.UnityAction exit = () => ReturnToMenu(name =>
        {
            Assert.That(name, Is.EqualTo(SceneLoader.MAIN_MENU));
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            Assert.That(Pending("pendingConfirmationAction"), Is.Null);
            Assert.That(Pending("pendingConfirmationType"), Is.Null);
            AssertCleared();
            loads++;
        });

        Request(exit);
        Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        hud.cancelReturnToMenuButton.onClick.Invoke();
        hud.confirmReturnToMenuButton.onClick.Invoke();
        Assert.That(loads, Is.Zero);
        Assert.That(TutorialLaunchState.Scenario, Is.SameAs(tutorial));
        Assert.That(CareerRaceLaunchState.Current, Is.SameAs(career));
        Assert.That(TutorialLaunchState.IsActive, Is.EqualTo(active));
        Assert.That(CareerRaceLaunchState.IsActive, Is.EqualTo(active));
        Assert.That(FreeRaceRosterState.SelectedTeams, Is.EqualTo(roster));
        Assert.That(roster.Select(FreeRaceRosterState.GetDriverId), Is.EqualTo(drivers));
        Assert.That(FreeRaceRosterState.IsConfigured, Is.True);
        Assert.That(FreeRaceRosterState.IsThunderstorm, Is.EqualTo(active));

        Request(exit);
        hud.confirmReturnToMenuButton.onClick.Invoke();
        hud.confirmReturnToMenuButton.onClick.Invoke();
        Assert.That(loads, Is.EqualTo(1));
        Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
        Assert.That(Serialize(season), Is.EqualTo(seasonBefore));
        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(track));
        Assert.That(GameSettingsRuntime.Current, Is.SameAs(settings));
        Assert.That(JsonUtility.ToJson(settings), Is.EqualTo(settingsBefore));
        Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path, Is.EqualTo(scene));
    }

    [Test]
    public void DisabledReturnGate_UsesSameCleanupWithoutOpeningModal()
    {
        GameSettingsRuntime.Current.inRaceConfirmationMask = 0;
        TutorialLaunchState.Request(TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.US));
        int calls = 0;
        Request(() => ReturnToMenu(name => { AssertCleared(); calls++; }));
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
        Assert.That(Pending("pendingConfirmationAction"), Is.Null);
    }

    [TestCase(InRaceConfirmationAction.ReturnToMenu)]
    [TestCase(InRaceConfirmationAction.ResetRace)]
    public void DestructivePromptReplacementAndFailure_DoNotReplayConsumedCallback(InRaceConfirmationAction action)
    {
        int oldCalls = 0;
        int calls = 0;
        hud.RequestInRaceAction(action, "old", "old", () => oldCalls++);
        hud.RequestInRaceAction(action, "new", "new", () =>
        {
            calls++;
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            Assert.That(Pending("pendingConfirmationAction"), Is.Null);
            throw new InvalidOperationException("recorded transition failure");
        });
        Assert.That(hud.TryConfirmKeyboardAction(), Is.False);
        Assert.Throws<InvalidOperationException>(() => hud.confirmReturnToMenuButton.onClick.Invoke());
        hud.confirmReturnToMenuButton.onClick.Invoke();
        Assert.That(calls, Is.EqualTo(1));
        Assert.That(oldCalls, Is.Zero);
        hud.RequestInRaceAction(action, "retry", "retry", () => calls++);
        hud.confirmReturnToMenuButton.onClick.Invoke();
        Assert.That(calls, Is.EqualTo(2));
    }

    [Test]
    public void FailedSceneIO_StillClearsSessionRequestsAsBefore()
    {
        TutorialLaunchState.Request(TutorialScenarioDefinition.CreateTeamSpecialty(TeamId.CN));
        FreeRaceRosterState.InitializeThunderstorm(DriverCatalog.GetDefaultForTeam(TeamId.CN));
        var exception = Assert.Throws<TargetInvocationException>(() => ReturnToMenu(name =>
        {
            AssertCleared();
            throw new InvalidOperationException("scene unavailable");
        }));
        Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
        AssertCleared();
    }

    private object Pending(string name) => typeof(HUDUI).GetField(name, InstanceMembers).GetValue(hud);
    private void Request(UnityEngine.Events.UnityAction exit) => typeof(HUDUI)
        .GetMethod("RequestReturnToMenu", InstanceMembers).Invoke(hud, new object[] { exit });
    private static void ReturnToMenu(Action<string> load) => typeof(SceneLoader)
        .GetMethod("ReturnToMainMenu", StaticFields).Invoke(null, new object[] { load });
    private static List<TeamId> Teams() => (List<TeamId>)typeof(FreeRaceRosterState)
        .GetField("selectedTeams", StaticFields).GetValue(null);
    private static Dictionary<TeamId, string> Drivers() => (Dictionary<TeamId, string>)typeof(FreeRaceRosterState)
        .GetField("driverByTeam", StaticFields).GetValue(null);
    private static string Serialize(CareerSeasonState season)
    {
        Assert.That(CareerSaveCodec.TryToData(season, out CareerSaveData data), Is.True);
        return new JsonUtilityCareerSerializer().Serialize(data);
    }
    private static void AssertCleared()
    {
        Assert.That(TutorialLaunchState.IsTutorialMode, Is.False);
        Assert.That(CareerRaceLaunchState.IsCareerMode, Is.False);
        Assert.That(FreeRaceRosterState.IsConfigured, Is.False);
        Assert.That(FreeRaceRosterState.IsThunderstorm, Is.False);
        Assert.That(FreeRaceRosterState.SelectedTeams, Is.Empty);
    }
}
