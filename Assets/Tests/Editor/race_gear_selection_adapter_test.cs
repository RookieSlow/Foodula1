using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Real gear UI callbacks with an inactive manager; no scene startup or turn coroutine.</summary>
public class RaceGearSelectionAdapterTests
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
        host = new GameObject("Gear selection regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
        phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(manager);
        player = new PlayerState("driver", false, 0, 2) { teamId = TeamId.UK };
        var session = new RaceSession();
        session.Players.Add(player);
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
        phase.BeginGearSelection();
        input.BeginGearSelection(player.gear);
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(host);

    [Test]
    public void SelectionOnlyChangesPendingGearUntilConfirmation()
    {
        manager.OnGearButtonClicked(3);

        Assert.That(input.PendingGear, Is.EqualTo(3));
        Assert.That(input.PlayerGearChoice, Is.EqualTo(2));
        Assert.That(input.WaitingForGear, Is.True);
        Assert.That(player.gear, Is.EqualTo(2));

        manager.OnConfirmGearClicked();

        Assert.That(input.PlayerGearChoice, Is.EqualTo(3));
        Assert.That(input.WaitingForGear, Is.False);
        Assert.That(player.gear, Is.EqualTo(2)); // The turn coroutine applies the committed gear later.
    }

    [Test]
    public void LastSelectionWinsBeforeConfirmationAndLateClicksCannotChangeIt()
    {
        manager.OnGearButtonClicked(1);
        manager.OnGearButtonClicked(4);
        manager.OnConfirmGearClicked();
        manager.OnGearButtonClicked(2);
        manager.OnConfirmGearClicked();

        Assert.That(input.PendingGear, Is.EqualTo(4));
        Assert.That(input.PlayerGearChoice, Is.EqualTo(4));
        Assert.That(input.WaitingForGear, Is.False);
        Assert.That(player.gear, Is.EqualTo(2));
    }

    [TestCase(GamePhase.WaitingForCards)]
    [TestCase(GamePhase.Animating)]
    [TestCase(GamePhase.GameOver)]
    public void StaleGearGateDoesNotAcceptCallbacksOutsideGearPhase(GamePhase target)
    {
        switch (target)
        {
            case GamePhase.WaitingForCards: phase.BeginCardSelection(); break;
            case GamePhase.Animating: phase.BeginAnimation(); break;
            case GamePhase.GameOver: phase.CompleteGame(); break;
        }

        manager.OnGearButtonClicked(3);
        manager.OnConfirmGearClicked();

        Assert.That(input.WaitingForGear, Is.True);
        Assert.That(input.PendingGear, Is.EqualTo(2));
        Assert.That(input.PlayerGearChoice, Is.EqualTo(2));
    }

    [TestCase("cards")]
    [TestCase("discard")]
    [TestCase("lane")]
    [TestCase("pit")]
    public void AnotherInputModalRejectsGearCallbacks(string modal)
    {
        switch (modal)
        {
            case "cards": input.BeginCardSelection(); break;
            case "discard": input.BeginDiscardSelection(); break;
            case "lane": input.BeginLaneChangeSelection(); break;
            case "pit": input.BeginPitChoice(); break;
        }

        manager.OnGearButtonClicked(3);
        manager.OnConfirmGearClicked();

        Assert.That(manager.TutorialInputPhase, Is.EqualTo(modal));
        Assert.That(input.WaitingForGear, Is.False);
        Assert.That(input.PendingGear, Is.EqualTo(2));
        Assert.That(input.PlayerGearChoice, Is.EqualTo(2));
    }

    [TestCase(3)]
    [TestCase(4)]
    public void ChinaRejectsStandardHighGearsButCanStillConfirmGo(int invalidGear)
    {
        player.teamId = TeamId.CN;
        player.gear = ChinaGearShiftRules.RecoverGear;
        input.BeginGearSelection(player.gear);

        manager.OnGearButtonClicked(invalidGear);
        Assert.That(input.PendingGear, Is.EqualTo(ChinaGearShiftRules.RecoverGear));
        Assert.That(input.WaitingForGear, Is.True);

        manager.OnGearButtonClicked(ChinaGearShiftRules.GoGear);
        manager.OnConfirmGearClicked();

        Assert.That(input.PlayerGearChoice, Is.EqualTo(ChinaGearShiftRules.GoGear));
        Assert.That(input.WaitingForGear, Is.False);
        Assert.That(player.gear, Is.EqualTo(ChinaGearShiftRules.RecoverGear));
    }
}
