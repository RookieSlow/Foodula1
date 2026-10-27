using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Real manager skill callback and label gate, without scene startup or GameLoop.</summary>
public class RaceDriverSkillActivationAdapterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private RaceInputState input;
    private RacePhaseState phase;
    private PlayerState player;

    [SetUp]
    public void SetUp()
    {
        host = new GameObject("Driver skill callback regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
        phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(manager);
        player = new PlayerState("Tony", false, 0, 2);
        var session = new RaceSession();
        session.Players.Add(player);
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
        InitializeDriver("us_tony_stewart", TeamId.US, 3, true);
        phase.BeginGearSelection();
        input.BeginGearSelection(player.gear);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    private void InitializeDriver(string id, TeamId team, int level, bool enabled)
    {
        Assert.That(DriverCatalog.TryGet(id, out DriverProfile profile), Is.True);
        player.driverId = id;
        player.teamId = team;
        player.driverSkill.Initialize(profile, level, enabled);
    }

    [Test]
    public void ValidGearWindowActivatesOnceWithoutChangingGearInput()
    {
        int originalUses = player.driverSkill.UsesRemaining;
        manager.OnDriverSkillButtonClicked();

        Assert.That(player.driverSkill.IsActive, Is.True);
        Assert.That(player.driverSkill.ActivatedThisTurn, Is.True);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(originalUses - 1));
        Assert.That(input.WaitingForGear, Is.True);
        Assert.That(input.PendingGear, Is.EqualTo(2));
        Assert.That(input.PlayerGearChoice, Is.EqualTo(2));
        Assert.That(player.gear, Is.EqualTo(2));
    }

    [Test]
    public void RepeatedClickCannotSpendAnotherUseWhileSkillIsActive()
    {
        manager.OnDriverSkillButtonClicked();
        int remaining = player.driverSkill.UsesRemaining;

        manager.OnDriverSkillButtonClicked();

        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(remaining));
        Assert.That(input.WaitingForGear, Is.True);
    }

    [TestCase(GamePhase.WaitingForCards)]
    [TestCase(GamePhase.Animating)]
    [TestCase(GamePhase.GameOver)]
    public void StaleGearInputCannotActivateOutsideGearPhase(GamePhase target)
    {
        switch (target)
        {
            case GamePhase.WaitingForCards: phase.BeginCardSelection(); break;
            case GamePhase.Animating: phase.BeginAnimation(); break;
            case GamePhase.GameOver: phase.CompleteGame(); break;
        }
        int originalUses = player.driverSkill.UsesRemaining;

        manager.OnDriverSkillButtonClicked();

        Assert.That(player.driverSkill.IsActive, Is.False);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(originalUses));
        Assert.That(input.WaitingForGear, Is.True);
    }

    [TestCase("cards")]
    [TestCase("discard")]
    [TestCase("lane")]
    [TestCase("pit")]
    public void AnotherInputModalCannotActivateSkill(string modal)
    {
        switch (modal)
        {
            case "cards": input.BeginCardSelection(); break;
            case "discard": input.BeginDiscardSelection(); break;
            case "lane": input.BeginLaneChangeSelection(); break;
            case "pit": input.BeginPitChoice(); break;
        }
        int originalUses = player.driverSkill.UsesRemaining;

        manager.OnDriverSkillButtonClicked();

        Assert.That(manager.TutorialInputPhase, Is.EqualTo(modal));
        Assert.That(player.driverSkill.IsActive, Is.False);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(originalUses));
    }

    [Test]
    public void DisabledSkillCannotActivateEvenDuringGearSelection()
    {
        InitializeDriver("us_tony_stewart", TeamId.US, 3, false);
        int originalUses = player.driverSkill.UsesRemaining;

        manager.OnDriverSkillButtonClicked();

        Assert.That(player.driverSkill.IsActive, Is.False);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(originalUses));
    }

    [Test]
    public void LockedLevelCannotActivateEvenDuringGearSelection()
    {
        InitializeDriver("us_tony_stewart", TeamId.US, 2, true);

        manager.OnDriverSkillButtonClicked();

        Assert.That(player.driverSkill.IsActive, Is.False);
        Assert.That(player.driverSkill.Tier, Is.Zero);
    }

    [Test]
    public void DriverSpecificPrerequisiteStillAppliesThroughManagerContext()
    {
        InitializeDriver("us_kyle_busch", TeamId.US, 3, true);
        int originalUses = player.driverSkill.UsesRemaining;

        manager.OnDriverSkillButtonClicked();

        Assert.That(player.driverSkill.IsActive, Is.False);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(originalUses));
    }

    [Test]
    public void ButtonInteractivityTracksTheSameGearInputGate()
    {
        string ready = manager.GetDriverSkillButtonLabel(player, out bool before);
        input.BeginCardSelection();
        string blocked = manager.GetDriverSkillButtonLabel(player, out bool after);

        Assert.That(before, Is.True);
        Assert.That(ready, Does.Contain("×"));
        Assert.That(after, Is.False);
        Assert.That(blocked, Does.Contain("仅可在档位确认前发动"));
    }

    [Test]
    public void MissingHumanPlayerIsAHarmlessNoOp()
    {
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, null);

        Assert.DoesNotThrow(() => manager.OnDriverSkillButtonClicked());
        Assert.That(manager.GetDriverSkillButtonLabel(null, out bool interactable), Is.EqualTo("车手技能"));
        Assert.That(interactable, Is.False);
    }

    [TestCase(false, 0)]
    [TestCase(true, 0)]
    [TestCase(false, (int)InRaceConfirmationAction.All)]
    [TestCase(true, (int)InRaceConfirmationAction.All)]
    public void HunterActualHudRequiresChoiceAndCommitsSelectedEqualValueInstance(bool directManagerCall, int mask)
    {
        WithHunterHud((hud, scope, cards) =>
        {
            GameSettingsRuntime.Current.inRaceConfirmationMask = mask;
            if (directManagerCall) manager.OnDriverSkillButtonClicked();
            else scope.Click(hud.driverSkillButton);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(player.driverSkill.IsActive, Is.False);
            Assert.That(player.deck.DiscardPileCount, Is.Zero);
            UnityEngine.UI.Button next = hud.returnToMenuConfirmPanel.transform
                .Find("ReturnToMenuConfirmCard/DriverSkillCardChoiceRow/DriverSkillChoiceNextButton")
                .GetComponent<UnityEngine.UI.Button>();
            scope.Click(next);
            scope.Click(hud.confirmReturnToMenuButton);
            hud.ConfirmReturnToMenu();
            Assert.That(player.deck.DiscardPile, Is.EqualTo(new[] { cards[1] }));
            Assert.That(player.deck.ContainsInHand(cards[0]), Is.True);
            Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(2));
            Assert.That(player.driverSkill.IsActive, Is.True);
            Assert.That(input.WaitingForGear, Is.True);
            Assert.That(player.gear, Is.EqualTo(2));
            Assert.That(player.deck.heatPool.remaining, Is.EqualTo(6));
        });
    }

    [TestCase("cancel")]
    [TestCase("gear confirmed")]
    [TestCase("target moved")]
    [TestCase("session replaced")]
    [TestCase("manager disabled")]
    [TestCase("cards phase")]
    [TestCase("gear changed and restored")]
    [TestCase("runtime rebuilt")]
    public void HunterActualPendingUiCannotSpendAfterCancellationOrExpiry(string change)
    {
        WithHunterHud((hud, scope, cards) =>
        {
            manager.OnDriverSkillButtonClicked();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            var staleCallback = (UnityEngine.Events.UnityAction<CardData>)typeof(HUDUI)
                .GetField("pendingDriverSkillCardConfirm", Private).GetValue(hud);
            var race = (RaceSession)typeof(MVPGameManager).GetField("session", Private).GetValue(manager);
            switch (change)
            {
                case "cancel": scope.Click(hud.cancelReturnToMenuButton); break;
                case "gear confirmed": manager.OnConfirmGearClicked(); break;
                case "target moved": race.Players[1].position++; break;
                case "session replaced":
                    var replacement = new RaceSession();
                    replacement.Players.AddRange(race.Players);
                    typeof(MVPGameManager).GetField("session", Private).SetValue(manager, replacement);
                    break;
                case "manager disabled": typeof(MVPGameManager).GetMethod("OnDisable", Private).Invoke(manager, null); break;
                case "cards phase": phase.BeginCardSelection(); break;
                case "gear changed and restored": manager.OnGearButtonClicked(1); manager.OnGearButtonClicked(2); break;
                case "runtime rebuilt":
                    typeof(MVPGameManager).GetMethod("RebuildRaceRuntime", Private).Invoke(manager, new object[]
                    {
                        new System.Action<AIController>(_ => { }), new System.Action(() => { }),
                        new System.Action(() => { }), new System.Action(() => { })
                    });
                    break;
            }
            hud.ConfirmReturnToMenu();
            staleCallback.Invoke(cards[1]);
            Assert.That(player.deck.Hand, Is.EqualTo(cards));
            Assert.That(player.deck.DiscardPileCount, Is.Zero);
            Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(3));
            Assert.That(player.driverSkill.IsActive, Is.False);
        });
    }

    [Test]
    public void HunterRequiresSecondSpeedAndNearbyTargetThroughActualButtonGate()
    {
        WithHunterHud((hud, scope, cards) =>
        {
            manager.GetDriverSkillButtonLabel(player, out bool ready);
            Assert.That(ready, Is.True);
            player.deck.RemoveFromHand(new System.Collections.Generic.List<CardData> { cards[1], cards[2] });
            Assert.That(manager.GetDriverSkillButtonLabel(player, out bool lacking), Does.Contain("两张普通速度牌"));
            Assert.That(lacking, Is.False);
            manager.OnDriverSkillButtonClicked();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(3));
        });
    }

    private void WithHunterHud(System.Action<HUDUI, CareerUiPointerTestScope, CardData[]> test)
    {
        var settingsField = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
        object savedSettings = settingsField.GetValue(null);
        settingsField.SetValue(null, GameSettingsData.CreateDefault());
        try
        {
        using (var scope = CareerUiPointerTestScope.Create(() => UnityEditor.AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>(
            "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset")))
        {
            HUDUI hud = scope.Root.AddComponent<HUDUI>();
            var skillHost = new GameObject("Bound skill button", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            skillHost.transform.SetParent(scope.Root.transform, false);
            var skillRect = (RectTransform)skillHost.transform;
            skillRect.anchorMin = skillRect.anchorMax = new Vector2(.5f, .5f);
            skillRect.anchoredPosition = new Vector2(-400f, 0f);
            skillRect.sizeDelta = new Vector2(200f, 64f);
            hud.driverSkillButton = skillHost.GetComponent<UnityEngine.UI.Button>();
            typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
            InitializeDriver("uk_hunter_hart", TeamId.UK, 7, true);
            player.driverSkill.BeginGlobalTurn();
            player.driverSkill.BeginDrivingInput();
            var cards = new[] { new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 3) };
            player.deck.InitializeExactOrder(cards, new HeatPool(6));
            player.deck.DrawToHand(3);
            var race = (RaceSession)typeof(MVPGameManager).GetField("session", Private).GetValue(manager);
            race.Players.Add(new PlayerState("nearest", true, 3, 1));
            TrackManager track = host.AddComponent<TrackManager>();
            var nodes = new System.Collections.Generic.List<TrackNode>();
            for (int i = 0; i < 60; i++) nodes.Add(new TrackNode(i, 0));
            typeof(TrackManager).GetField("nodes", Private).SetValue(track, nodes);
            manager.trackManager = track;
            manager.hudUI = hud;
            hud.SetGameManager(manager);
            try { test(hud, scope, cards); }
            finally { hud.CancelReturnToMenu(); manager.hudUI = null; }
        }
        }
        finally { settingsField.SetValue(null, savedSettings); }
    }
}
