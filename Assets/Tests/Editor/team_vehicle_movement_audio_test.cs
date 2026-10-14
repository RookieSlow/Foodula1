using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>Actual movement progress and coordinator lifecycle boundaries for engine presentation.</summary>
public class TeamVehicleMovementAudioTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [Test]
    public void NodeProgressTracksFractionalTravelInsteadOfImmediatelyReportingFullThrottle()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        var car = new GameObject("Engine progress test car");
        var progress = new List<float>();
        try
        {
            config.nodeMoveDuration = 0.2f;
            var animator = new CarMovementAnimator(config, new CarOrientationController(config),
                () => 0.05f, () => false, value => progress.Add(value));
            IEnumerator movement = animator.MoveToNode(car, Vector3.right);
            Assert.That(movement.MoveNext(), Is.True);
            Assert.That(car.transform.position.x, Is.EqualTo(0.25f).Within(0.001f));
            Assert.That(progress[0], Is.EqualTo(0.25f).Within(0.001f));
            while (movement.MoveNext()) { }
            Assert.That(progress, Is.Ordered);
            Assert.That(progress[progress.Count - 1], Is.EqualTo(1f));
            Assert.That(car.transform.position, Is.EqualTo(Vector3.right));
        }
        finally { Object.DestroyImmediate(car); Object.DestroyImmediate(config); }
    }

    [Test]
    public void SkipSnapsToDestinationWithoutSendingFakeRemainingTravelToEngine()
    {
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        var car = new GameObject("Engine skip test car");
        var progress = new List<float>();
        bool skip = false;
        try
        {
            config.nodeMoveDuration = 0.2f;
            var animator = new CarMovementAnimator(config, new CarOrientationController(config),
                () => 0.05f, () => skip, value => progress.Add(value));
            IEnumerator movement = animator.MoveToNode(car, Vector3.right);
            Assert.That(movement.MoveNext(), Is.True);
            skip = true;
            Assert.That(movement.MoveNext(), Is.False);
            Assert.That(car.transform.position, Is.EqualTo(Vector3.right));
            Assert.That(progress.Count, Is.EqualTo(1));
            Assert.That(progress[0], Is.LessThan(1f));
        }
        finally { Object.DestroyImmediate(car); Object.DestroyImmediate(config); }
    }

    [Test]
    public void AcceptedGlobalSkipClearsTrackedAudioWithoutChangingRaceState()
    {
        var host = new GameObject("Engine coordinator skip");
        host.SetActive(false);
        var manager = host.AddComponent<MVPGameManager>();
        var player = new PlayerState("Driver", false, 4, 3) { teamId = TeamId.DE };
        try
        {
            Set(manager, "movementAudioPlayer", player);
            Set(manager, "movementAudioCompletedCells", 7);
            var skip = (RacePresentationSkipState)Get(manager, "presentationSkipState");
            Assert.That(manager.RequestPresentationSkip(), Is.False);
            Assert.That(Get(manager, "movementAudioPlayer"), Is.SameAs(player));
            skip.Begin();
            Assert.That(manager.RequestPresentationSkip(), Is.True);
            Assert.That(Get(manager, "movementAudioPlayer"), Is.Null);
            Assert.That(Get(manager, "movementAudioCompletedCells"), Is.EqualTo(0));
            Assert.That(player.position, Is.EqualTo(4));
            Assert.That(player.gear, Is.EqualTo(3));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void PendingGearPresentationDoesNotCommitAndSharesTheRealInputGate()
    {
        var host = new GameObject("Engine pending gear");
        host.SetActive(false);
        var manager = host.AddComponent<MVPGameManager>();
        var player = new PlayerState("Driver", false, 0, 2) { teamId = TeamId.UK };
        var session = new RaceSession();
        session.Players.Add(player);
        Set(manager, "session", session);
        try
        {
            var phase = (RacePhaseState)Get(manager, "phaseState");
            var input = (RaceInputState)Get(manager, "inputState");
            phase.BeginGearSelection();
            input.BeginGearSelection(2);
            manager.OnGearButtonClicked(3);
            Assert.That(manager.PendingGearForPresentation, Is.EqualTo(3));
            Assert.That(manager.CanSelectGearForPresentation, Is.True);
            Assert.That(player.gear, Is.EqualTo(2));
            manager.OnConfirmGearClicked();
            Assert.That(manager.PendingGearForPresentation, Is.EqualTo(3));
            Assert.That(manager.CanSelectGearForPresentation, Is.False);
            Assert.That(player.gear, Is.EqualTo(2));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InstantAccelerationIdentityIsClearedByActualSpinOrFinish(bool finish)
    {
        var host = new GameObject("Instant engine termination");
        host.SetActive(false);
        var manager = host.AddComponent<MVPGameManager>();
        var config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.totalLaps = 1;
        config.enableWeather = false;
        manager.config = config;
        var player = new PlayerState("Electric driver", false, 4, 2) { teamId = TeamId.CN };
        player.deck.InitializeExactOrder(new[] { new CardData(CardType.Speed, 3) }, new HeatPool(6));
        var session = new RaceSession();
        session.Players.Add(player);
        Set(manager, "session", session);
        Set(manager, "instantAudioPlayer", player);
        try
        {
            Assert.That(Get(manager, "movementAudioPlayer"), Is.Null);
            if (finish)
            {
                manager.OnPlayerCrossedStartFinish(player);
                Assert.That(player.hasFinished, Is.True);
            }
            else
            {
                manager.HandleSpin(player, 1, "second acceleration payment failed");
                Assert.That(player.position, Is.EqualTo(1));
                Assert.That(player.gear, Is.EqualTo(ChinaGearShiftRules.RecoverGear));
            }
            Assert.That(Get(manager, "instantAudioPlayer"), Is.Null);
        }
        finally { Object.DestroyImmediate(host); Object.DestroyImmediate(config); }
    }


    [TestCase("OnDisable")]
    [TestCase("OnDestroy")]
    public void DestroyedHudCannotInterruptRepeatedManagerAudioCleanup(string callback)
    {
        var host = new GameObject("Destroyed HUD audio coordinator");
        host.SetActive(false);
        var manager = host.AddComponent<MVPGameManager>();
        var hudHost = new GameObject("Destroyed audio HUD");
        hudHost.SetActive(false);
        var hud = hudHost.AddComponent<HUDUI>();
        manager.hudUI = hud;
        var player = new PlayerState("Teardown driver", false, 4, 3) { teamId = TeamId.DE };
        Set(manager, "movementAudioPlayer", player);
        Set(manager, "instantAudioPlayer", player);
        Set(manager, "movementAudioCompletedCells", 7);
        try
        {
            Object.DestroyImmediate(hudHost);
            Assert.That(manager.hudUI == null, Is.True, "Unity's destroyed-object check must differ from CLR null.");
            Assert.That(ReferenceEquals(manager.hudUI, null), Is.False);
            MethodInfo method = typeof(MVPGameManager).GetMethod(callback, Private);
            Assert.DoesNotThrow(() => method.Invoke(manager, null));
            Assert.DoesNotThrow(() => method.Invoke(manager, null));
            Assert.That(Get(manager, "movementAudioPlayer"), Is.Null);
            Assert.That(Get(manager, "instantAudioPlayer"), Is.Null);
            Assert.That(Get(manager, "movementAudioCompletedCells"), Is.EqualTo(0));
            Assert.That(player.position, Is.EqualTo(4));
            Assert.That(player.gear, Is.EqualTo(3));
        }
        finally
        {
            if (hudHost != null) Object.DestroyImmediate(hudHost);
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void LateNodeProgressDoesNotAccessDestroyedHudOrChangeRaceState()
    {
        var host = new GameObject("Late HUD engine progress");
        host.SetActive(false);
        var manager = host.AddComponent<MVPGameManager>();
        var hudHost = new GameObject("Late destroyed HUD");
        hudHost.SetActive(false);
        manager.hudUI = hudHost.AddComponent<HUDUI>();
        var player = new PlayerState("Late driver", false, 5, 2) { teamId = TeamId.JP };
        var session = new RaceSession();
        session.Players.Add(player);
        Set(manager, "session", session);
        Set(manager, "movementAudioPlayer", player);
        try
        {
            Object.DestroyImmediate(hudHost);
            Assert.DoesNotThrow(() => typeof(MVPGameManager).GetMethod("UpdateMovementAudio", Private)
                .Invoke(manager, new object[] { 2.5f }));
            Assert.That(Get(manager, "movementAudioPlayer"), Is.SameAs(player));
            Assert.That(player.position, Is.EqualTo(5));
            Assert.That(player.gear, Is.EqualTo(2));
        }
        finally
        {
            if (hudHost != null) Object.DestroyImmediate(hudHost);
            Object.DestroyImmediate(host);
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void EngineIntensityDoesNotCreatePresentationOrAccessDestroyedHud(bool destroyHud)
    {
        var host = new GameObject("Update-only engine HUD");
        host.SetActive(false);
        var hud = host.AddComponent<HUDUI>();
        var gearHost = new GameObject("GearButtons", typeof(RectTransform));
        gearHost.transform.SetParent(host.transform, false);
        var buttonHost = new GameObject("G1", typeof(RectTransform));
        buttonHost.transform.SetParent(gearHost.transform, false);
        hud.gear1Button = buttonHost.AddComponent<UnityEngine.UI.Button>();
        try
        {
            if (destroyHud) Object.DestroyImmediate(hud);
            Assert.DoesNotThrow(() => hud.SetEnginePresentation(0f));
            Assert.That(host.GetComponentsInChildren<TeamGearPresentationUI>(true), Is.Empty,
                "Intensity updates cannot construct presentation during scene teardown.");
            Assert.That(gearHost.transform.childCount, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(host); }
    }

    [Test]
    public void EngineIntensityIgnoresDestroyedVisualChildWithoutRecreatingIt()
    {
        var host = new GameObject("Engine HUD after visuals");
        host.SetActive(false);
        var hud = host.AddComponent<HUDUI>();
        var visualHost = new GameObject("Destroyed engine visual", typeof(RectTransform));
        visualHost.transform.SetParent(host.transform, false);
        var visual = visualHost.AddComponent<TeamGearPresentationUI>();
        Set(hud, "teamGearPresentation", visual);
        try
        {
            Object.DestroyImmediate(visualHost);
            Assert.That(ReferenceEquals(hud.TeamGearPresentation, null), Is.False);
            Assert.That(hud.TeamGearPresentation == null, Is.True);
            Assert.DoesNotThrow(() => hud.SetEnginePresentation(0.8f));
            Assert.That(host.GetComponentsInChildren<TeamGearPresentationUI>(true), Is.Empty);
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static object Get(object target, string field) => target.GetType().GetField(field, Private).GetValue(target);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
}
