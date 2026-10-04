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
}
