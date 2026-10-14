using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Production HUD commands and six team layouts. Managers remain inactive; pointer cases render owned previews.</summary>
public sealed partial class TeamGearPresentationTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    private GameObject root;
    private GameConfigSO config;
    private MVPGameManager manager;
    private HUDUI hud;
    private PlayerState player;
    private RaceInputState input;
    private RacePhaseState phase;
    private object previousAudio;
    private object previousSettings;
    private FieldInfo audioInstanceField;
    private FieldInfo settingsCurrentField;
    private RaceStartupTestScope startupScope;
    private TeamGearPresentationUI View => hud.TeamGearPresentation;

    [SetUp]
    public void SetUp()
    {
        startupScope = new RaceStartupTestScope();
        audioInstanceField = typeof(AudioService).GetField("instance", Static);
        settingsCurrentField = typeof(GameSettingsRuntime).GetField("current", Static);
        Assert.That(audioInstanceField, Is.Not.Null, "AudioService singleton field must be discoverable");
        Assert.That(settingsCurrentField, Is.Not.Null, "GameSettingsRuntime settings field must be discoverable");
        previousAudio = audioInstanceField.GetValue(null);
        audioInstanceField.SetValue(null, null);
        previousSettings = settingsCurrentField.GetValue(null);
        settingsCurrentField.SetValue(null,
            new GameSettingsData { inRaceConfirmationMask = 0 });
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            if (root != null) Object.DestroyImmediate(root);
            if (config != null) Object.DestroyImmediate(config);
        }
        finally
        {
            audioInstanceField?.SetValue(null, previousAudio);
            settingsCurrentField?.SetValue(null, previousSettings);
            startupScope?.Dispose();
        }
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void TeamLayoutRetainsTeachingTargetAndShowsActualAndPendingSeparately(TeamId team)
    {
        Build(team, team == TeamId.CN ? 1 : 2);
        Assert.That(View.Team, Is.EqualTo(team));
        Assert.That(View.name, Is.EqualTo("GearButtons"));
        Assert.That(View.AssemblyGraphic.raycastTarget, Is.False);
        Assert.That(View.StateLabel.raycastTarget || View.DetailLabel.raycastTarget, Is.False);
        Assert.That(hud.gear3Button.gameObject.activeSelf, Is.EqualTo(team != TeamId.CN));
        Assert.That(hud.gear4Button.gameObject.activeSelf, Is.EqualTo(team != TeamId.CN));
        bool hasStep = team == TeamId.DE || team == TeamId.JP;
        Assert.That(View.ShiftUpButton.gameObject.activeSelf, Is.EqualTo(hasStep));
        Assert.That(View.ShiftDownButton.gameObject.activeSelf, Is.EqualTo(hasStep));
        int previous = player.gear;
        hud.gear1Button.onClick.Invoke();
        Assert.That(View.PendingGear, Is.EqualTo(1));
        Assert.That(View.CurrentGear, Is.EqualTo(previous));
        Assert.That(player.gear, Is.EqualTo(previous));
        Assert.That(View.StateLabel.text, Does.Contain("实际").And.Contain("待选"));
        Assert.That(View.DetailLabel.text, Does.Contain("出牌 1张"));
    }

    [TestCase(TeamId.DE)]
    [TestCase(TeamId.JP)]
    public void StepButtonsAccumulatePendingButRespectActualTwoGearLimit(TeamId team)
    {
        Build(team, 1);
        View.ShiftUpButton.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(2));
        View.ShiftUpButton.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(3));
        View.ShiftUpButton.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(3));
        Assert.That(View.ShiftUpButton.interactable, Is.False);
        Assert.That(hud.gear4Button.interactable, Is.False);
        Assert.That(View.CurrentGear, Is.EqualTo(1));
        View.ShiftDownButton.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(2));
        Assert.That(View.DetailLabel.text, Does.Contain("换挡热 0"));
    }

    [Test]
    public void ConfirmLocksPendingAndOnlyActualApplyUpdatesCurrent()
    {
        Build(TeamId.DE, 2);
        View.ShiftUpButton.onClick.Invoke();
        hud.confirmGearButton.onClick.Invoke();
        Assert.That(View.CurrentGear, Is.EqualTo(2));
        Assert.That(View.PendingGear, Is.EqualTo(3));
        Assert.That(View.IsLocked, Is.True);
        Assert.That(View.ControlsInteractable, Is.False);
        Assert.That(View.StateLabel.text, Does.Contain("已锁 G3"));
        View.ShiftDownButton.onClick.Invoke();
        hud.gear1Button.onClick.Invoke();
        Assert.That(input.PlayerGearChoice, Is.EqualTo(3));
        Assert.That(player.gear, Is.EqualTo(2));
        ApplyChoice();
        Assert.That(player.gear, Is.EqualTo(3));
        Assert.That(View.CurrentGear, Is.EqualTo(3));
    }

    [Test]
    public void SelectionConfirmationBlocksOtherControlsAndCancelRestoresTheOriginalChoice()
    {
        Build(TeamId.US, 2);
        GameSettingsRuntime.Current.inRaceConfirmationMask = (int)InRaceConfirmationAction.GearSelection;
        hud.gear3Button.onClick.Invoke();
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        Assert.That(input.PendingGear, Is.EqualTo(2));
        Assert.That(View.ControlsInteractable, Is.False);
        View.ShiftDownButton.onClick.Invoke();
        hud.gear1Button.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(2));
        hud.cancelReturnToMenuButton.onClick.Invoke();
        Assert.That(View.ControlsInteractable, Is.True);
        hud.gear3Button.onClick.Invoke();
        hud.confirmReturnToMenuButton.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(3));
        Assert.That(View.CurrentGear, Is.EqualTo(2));
        Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
    }

    [TestCase(GamePhase.WaitingForCards)]
    [TestCase(GamePhase.Animating)]
    [TestCase(GamePhase.GameOver)]
    public void ClosedRacePhaseRejectsLateDirectAndStepCallbacks(GamePhase target)
    {
        Build(TeamId.JP, 2);
        if (target == GamePhase.WaitingForCards) phase.BeginCardSelection();
        else if (target == GamePhase.Animating) phase.BeginAnimation();
        else phase.CompleteGame();
        hud.RefreshTeamGearPresentation();
        View.ShiftUpButton.onClick.Invoke();
        hud.gear3Button.onClick.Invoke();
        hud.confirmGearButton.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(2));
        Assert.That(input.WaitingForGear, Is.True);
        Assert.That(View.ControlsInteractable, Is.False);
    }

    [Test]
    public void ReinitializeDoesNotDuplicateStepListenersOrReactivateTheLegacyDial()
    {
        Build(TeamId.DE, 2);
        View.Initialize(hud, hud.gear1Button, hud.gear2Button, hud.gear3Button, hud.gear4Button);
        View.Initialize(hud, hud.gear1Button, hud.gear2Button, hud.gear3Button, hud.gear4Button);
        hud.RefreshTeamGearPresentation();
        View.ShiftUpButton.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(3), "A duplicate listener would step twice to G4.");
        Assert.That(View.GetComponents<TeamGearPresentationUI>().Length, Is.EqualTo(1));
        Assert.That(View.GetComponent<GearDialPresentationUI>(), Is.Null,
            "New HUDs must not autoattach the replaced stove dial.");
    }

    [Test]
    public void ChinaHasOnlyTwoModesAndPreviewsTheNextChainWithoutAdvancingActualState()
    {
        Build(TeamId.CN, 2);
        player.chinaConsecutiveGearCount = 1;
        hud.RefreshTeamGearPresentation();
        Assert.That(View.DetailLabel.text, Does.Contain("出牌 4张").And.Contain("连用热 +1"));
        hud.gear3Button.onClick.Invoke();
        hud.gear4Button.onClick.Invoke();
        Assert.That(input.PendingGear, Is.EqualTo(2));
        Assert.That(player.chinaConsecutiveGearCount, Is.EqualTo(1));
        hud.confirmGearButton.onClick.Invoke();
        ApplyChoice();
        Assert.That(player.chinaConsecutiveGearCount, Is.EqualTo(2));
        Assert.That(View.DetailLabel.text, Does.Contain("连用热 +1"),
            "A committed choice must not display the following turn's +2 heat.");
        input.BeginGearSelection(player.gear);
        hud.SetTeamGearControlsInteractable(true);
        hud.gear1Button.onClick.Invoke();
        Assert.That(View.DetailLabel.text, Does.Contain("出牌 1张").And.Contain("回收 3热"));
        Assert.That(player.gear, Is.EqualTo(2));
    }

    [Test]
    public void MovementIntensityDrivesBoostGaugeWithoutChangingResourcesOrChoices()
    {
        Build(TeamId.JP, 2);
        int heat = player.deck.heatPool.remaining;
        hud.SetEnginePresentation(0.35f);
        Assert.That(View.AssemblyGraphic.EngineLightCount, Is.EqualTo(4));
        hud.SetEnginePresentation(1f);
        Assert.That(View.AssemblyGraphic.EngineLightCount, Is.EqualTo(10));
        hud.SetEnginePresentation(0f);
        Assert.That(View.AssemblyGraphic.EngineLightCount, Is.Zero);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(heat));
        Assert.That(View.CurrentGear, Is.EqualTo(2));
        Assert.That(input.PendingGear, Is.EqualTo(2));
    }

    [TestCase(TeamId.DE, "de_sebastian_vettel")]
    [TestCase(TeamId.CN, "cn_ma_qinghua")]
    public void PreviewUsesActiveDriverGearHeatReductionWithoutConsumingTheSkill(TeamId team, string driverId)
    {
        Build(team, team == TeamId.CN ? 2 : 1);
        if (team == TeamId.CN) player.chinaConsecutiveGearCount = 1;
        else hud.gear3Button.onClick.Invoke();
        hud.RefreshTeamGearPresentation();
        Assert.That(View.DetailLabel.text, Does.Contain(team == TeamId.CN ? "连用热 +1" : "换挡热 1"));
        Assert.That(DriverCatalog.TryGet(driverId, out DriverProfile driver), Is.True);
        player.driverSkill.Initialize(driver, 5, true);
        Assert.That(player.driverSkill.TryActivate(driver,
            new DriverSkillActivationContext(true, 0, 3, 6, 6, 0), out _), Is.True);
        int turns = player.driverSkill.ActiveTurnsRemaining;
        int uses = player.driverSkill.UsesRemaining;
        hud.RefreshTeamGearPresentation();
        hud.RefreshTeamGearPresentation();
        Assert.That(View.DetailLabel.text, Does.Contain(team == TeamId.CN ? "连用热 +0" : "换挡热 0"));
        Assert.That(player.driverSkill.ActiveTurnsRemaining, Is.EqualTo(turns));
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void RenderedPointerCanReachEachTeamControlsAfterOuterHudLayout(TeamId team)
    {
        using (var scope = CareerUiPointerTestScope.Create(() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
            "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset")))
        {
            Build(team, team == TeamId.CN ? 1 : 2, scope.Root.transform, scope.Font);
            root.SetActive(true);
            RaceUILayoutController layout = root.AddComponent<RaceUILayoutController>();
            layout.ApplyLayout(hud, null);
            Assert.That(View.transform.parent, Is.SameAs(layout.OperationPanel));
            if (team == TeamId.DE || team == TeamId.JP)
            {
                scope.Click(View.ShiftUpButton);
                Assert.That(input.PendingGear, Is.EqualTo(3));
                scope.Click(View.ShiftDownButton);
                Assert.That(input.PendingGear, Is.EqualTo(2));
            }
            Button[] choices = { hud.gear1Button, hud.gear2Button, hud.gear3Button, hud.gear4Button };
            int visible = team == TeamId.CN ? 2 : 4;
            for (int i = 0; i < visible; i++)
            {
                ClickAuthoredButton(scope.Root, scope, choices[i]);
                Assert.That(input.PendingGear, Is.EqualTo(i + 1));
            }
            scope.Click(hud.confirmGearButton);
            Assert.That(input.WaitingForGear, Is.False);
            Assert.That(View.CurrentGear, Is.EqualTo(team == TeamId.CN ? 1 : 2));
            Assert.That(View.PendingGear, Is.EqualTo(visible));
            Assert.That(View.ControlsInteractable, Is.False);
            Assert.That(phase.Current, Is.EqualTo(GamePhase.WaitingForGear));
        }
    }

    private void ApplyChoice() => typeof(MVPGameManager).GetMethod("ApplyGearShift", Private)
        .Invoke(manager, new object[] { player, input.PlayerGearChoice });

    private void Build(TeamId team, int gear, Transform parent = null, TMP_FontAsset font = null)
    {
        root = new GameObject("Team gear UI regression", typeof(RectTransform));
        root.SetActive(false);
        if (parent != null) root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        hud = root.AddComponent<HUDUI>();
        GameObject managerHost = new GameObject("Inactive gear UI manager");
        managerHost.SetActive(false);
        managerHost.transform.SetParent(root.transform, false);
        manager = managerHost.AddComponent<MVPGameManager>();
        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
        phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(manager);
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        player = new PlayerState("gear driver", false, 0, gear) {
            teamId = team, deck = new CardDeck { heatPool = new HeatPool(6) }
        };
        var session = new RaceSession();
        session.Players.Add(player);
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
        manager.config = config;
        manager.hudUI = hud;
        phase.BeginGearSelection();
        input.BeginGearSelection(gear);
        var container = new GameObject("GearButtons", typeof(RectTransform));
        container.transform.SetParent(root.transform, false);
        hud.gear1Button = CreateButton(container.transform, "Gear1Btn", font);
        hud.gear2Button = CreateButton(container.transform, "Gear2Btn", font);
        hud.gear3Button = CreateButton(container.transform, "Gear3Btn", font);
        hud.gear4Button = CreateButton(container.transform, "Gear4Btn", font);
        hud.confirmGearButton = CreateButton(root.transform, "ConfirmGearBtn", font);
        typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
        hud.SetGameManager(manager);
        hud.ConfigureGearPresentation(team, gear);
        hud.SetTeamGearControlsInteractable(true);
    }

    private static Button CreateButton(Transform parent, string name, TMP_FontAsset font)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        host.transform.SetParent(parent, false);
        var label = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        label.transform.SetParent(host.transform, false);
        if (font != null) label.GetComponent<TMP_Text>().font = font;
        label.GetComponent<TMP_Text>().raycastTarget = false;
        Button button = host.GetComponent<Button>();
        button.targetGraphic = host.GetComponent<Image>();
        return button;
    }
}
