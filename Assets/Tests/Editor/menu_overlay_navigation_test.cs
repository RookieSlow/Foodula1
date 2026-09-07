using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class MenuOverlayNavigationTests
{
    private GameObject root;
    private MainMenuUI menu;
    private CareerRepository repository;
    private string previousDriver;

    [SetUp]
    public void SetUp()
    {
        previousDriver = DriverSelectionState.SelectedDriverId;
        root = new GameObject("MenuOverlayTest", typeof(RectTransform));
        // Keep this fixture inactive to avoid running scene-wide Awake logic.
        root.SetActive(false);
        menu = root.AddComponent<MainMenuUI>();
        menu.startRaceButton = CreateButton("StartRaceBtn");
        CreateButton("GarageBtn");
        menu.quitButton = CreateButton("QuitBtn");
        Invoke(menu, "Start");
        repository = new CareerRepository(new MemoryStore(), new JsonUtilityCareerSerializer());
        root.GetComponent<CareerModeUI>().Initialize(repository, true);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(root);
        DriverSelectionState.Reset();
        if (!string.IsNullOrEmpty(previousDriver)) DriverSelectionState.TrySelect(previousDriver);
    }

    [Test]
    public void late_career_button_and_other_entries_stay_below_driver_overlay()
    {
        Transform driver = root.transform.Find("DriverSelectionOverlay");
        Assert.That(root.transform.Find("CareerBtn").GetSiblingIndex(), Is.LessThan(driver.GetSiblingIndex()));
        CreateButton("LateMenuEntry");
        Click("GarageBtn");
        AssertOnlySurface("DriverSelectionOverlay");
        Assert.That(driver.GetSiblingIndex(), Is.EqualTo(root.transform.childCount - 1));
        ClickWithin("DriverSelectionOverlay", "BackButton");
        Assert.That(driver.gameObject.activeSelf, Is.False);
    }

    [TestCase("GarageBtn", "DriverSelectionOverlay")]
    [TestCase("StartRaceBtn", "TrackSelectionOverlay")]
    [TestCase("SettingsBtn", "SettingsOverlay")]
    public void navigation_closes_career_and_return_does_not_reveal_previous_surface(string button, string target)
    {
        Click("CareerBtn");
        Click(button);
        AssertOnlySurface(target);
        Click("CareerBtn");
        AssertOnlySurface("CareerModeOverlay");
        Click("CareerClose");
        Assert.That(root.transform.Cast<Transform>().Any(t => t.name.EndsWith("Overlay") && t.gameObject.activeSelf), Is.False);
    }

    [TestCase("GarageBtn", "DriverSelectionOverlay")]
    [TestCase("StartRaceBtn", "TrackSelectionOverlay")]
    [TestCase("SettingsBtn", "SettingsOverlay")]
    public void navigation_hides_summer_tech_without_consuming_gate(string button, string target)
    {
        LoadSummerBreak();
        Click("CareerBtn");
        Click("CareerPrimary");
        AssertOnlySurface("CareerTechTreeOverlay");
        Click(button);
        AssertOnlySurface(target);
        Assert.That(repository.Load().State.Phase, Is.EqualTo(CareerPhase.SummerBreak));
        Click("CareerBtn");
        AssertOnlySurface("CareerModeOverlay");
        Click("CareerPrimary");
        AssertOnlySurface("CareerTechTreeOverlay");
    }

    [Test]
    public void reopening_career_clears_confirmation_and_replacement_view()
    {
        LoadSummerBreak();
        Click("CareerBtn");
        Click("CareerNewSeason");
        Click("CareerCreate");
        Assert.That(Find("CareerConfirmation").gameObject.activeSelf, Is.True);
        Click("GarageBtn");
        Click("CareerBtn");
        Assert.That(Find("CareerConfirmation").gameObject.activeSelf, Is.False);
        Assert.That(Find("ConfirmationText").GetComponent<TMP_Text>().text, Is.Empty);
        Assert.That(Find("CareerTeamSelection").gameObject.activeSelf, Is.False);
        Assert.That(Find("CareerOverview").gameObject.activeSelf, Is.True);
    }

    [Test]
    public void driver_labels_refresh_after_selection_and_reopening()
    {
        DriverProfile driver = DriverSelectionState.AvailableDrivers.Last();
        Click("GarageBtn");
        Click("Driver_" + driver.Id);
        Assert.That(Find("GarageBtn").GetComponentInChildren<TMP_Text>(true).text, Does.Contain(driver.ShortName));
        Click("CareerBtn");
        DriverProfile other = DriverSelectionState.AvailableDrivers.First();
        DriverSelectionState.TrySelect(other.Id);
        Click("GarageBtn");
        Assert.That(Find("SelectedDriver").GetComponent<TMP_Text>().text, Does.Contain(other.DisplayName));
        Assert.That(Find("GarageBtn").GetComponentInChildren<TMP_Text>(true).text, Does.Contain(other.ShortName));
    }

    private void LoadSummerBreak()
    {
        var state = new CareerSeasonState();
        Assert.That(CareerTechSnapshot.TryCreate(TeamId.UK, 100, Array.Empty<string>(),
            Array.Empty<string>(), null, out CareerTechSnapshot snapshot), Is.True);
        TeamId[] field = { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US };
        Assert.That(CareerModeRules.TryStartSeason(state, TeamId.UK, field, snapshot), Is.True);
        for (int race = 0; race < 4; race++)
            Assert.That(CareerModeRules.TryRecordRace(state, new CareerRaceResult("menu-test-" + race,
                CareerModeRules.GetNextTrackId(state), field.Select((team, index) =>
                    new CareerCompetitorResult(team, index + 1)).ToArray())), Is.True);
        Assert.That(repository.Save(state), Is.True);
        root.GetComponent<CareerModeUI>().Initialize(repository, true);
    }

    private Button CreateButton(string name)
    {
        var obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(root.transform, false);
        var label = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(obj.transform, false);
        label.GetComponent<TMP_Text>().font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/ttf/msyh SDF.asset");
        return obj.GetComponent<Button>();
    }

    private Transform Find(string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    private void Click(string name) => Find(name).GetComponent<Button>().onClick.Invoke();
    private void ClickWithin(string parentName, string name) =>
        Find(parentName).GetComponentsInChildren<Transform>(true)
            .First(t => t.name == name).GetComponent<Button>().onClick.Invoke();

    private void AssertOnlySurface(string name)
    {
        string[] visible = root.transform.Cast<Transform>()
            .Where(t => t.name.EndsWith("Overlay") && t.gameObject.activeSelf).Select(t => t.name).ToArray();
        Assert.That(visible, Is.EqualTo(new[] { name }));
        Assert.That(root.transform.Find(name).GetSiblingIndex(), Is.EqualTo(root.transform.childCount - 1));
    }

    private static void Invoke(object target, string method) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);

    private sealed class MemoryStore : ICareerKeyValueStore
    {
        private string value;
        public bool HasKey(string key) => value != null;
        public string GetString(string key) => value;
        public bool TrySetAndSave(string key, string serialized) { value = serialized; return true; }
        public bool TryDeleteAndSave(string key) { value = null; return true; }
    }
}
