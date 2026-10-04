using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class BankuruwaseTurnRulesTests
{
    private TechTreeDatabase db;
    private TechTreeState state;

    [SetUp]
    public void SetUp()
    {
        db = TechTreeDatabaseFactory.CreateDefault();
        state = new TechTreeState(TeamId.JP, TechTreeRules.DEMO_BUDGET);
        state.activeNodeIds.Add("jp-l3-bankuruwase");
    }

    [TestCase(5, BankuruwaseTurnTransition.Activated)]
    [TestCase(6, BankuruwaseTurnTransition.Activated)]
    [TestCase(4, BankuruwaseTurnTransition.None)]
    public void RankBoundaryControlsActivation(int rank, BankuruwaseTurnTransition expected)
    {
        Assert.AreEqual(expected, BankuruwaseTurnRules.Advance(state, db, rank, 6));
        Assert.AreEqual(expected == BankuruwaseTurnTransition.Activated, state.bankuruwaseActive);
    }

    [Test]
    public void ActiveRotorCountsThreeConsecutiveTurnsEvenAfterRankImproves()
    {
        Assert.AreEqual(BankuruwaseTurnTransition.Activated,
            BankuruwaseTurnRules.Advance(state, db, 6, 6));
        Assert.AreEqual(3, state.bankuruwaseTurnsLeft);

        Assert.AreEqual(BankuruwaseTurnTransition.Continued,
            BankuruwaseTurnRules.Advance(state, db, 1, 6));
        Assert.AreEqual(2, state.bankuruwaseTurnsLeft);
        Assert.AreEqual(BankuruwaseTurnTransition.Continued,
            BankuruwaseTurnRules.Advance(state, db, 1, 6));
        Assert.AreEqual(1, state.bankuruwaseTurnsLeft);
        Assert.AreEqual(BankuruwaseTurnTransition.Expired,
            BankuruwaseTurnRules.Advance(state, db, 6, 6));
        Assert.IsFalse(state.bankuruwaseActive, "Expiry must not reactivate on the same turn boundary.");
        Assert.AreEqual(BankuruwaseTurnTransition.Activated,
            BankuruwaseTurnRules.Advance(state, db, 6, 6));
    }

    [Test]
    public void MissingStateOrTechnologyCannotActivate()
    {
        Assert.AreEqual(BankuruwaseTurnTransition.None,
            BankuruwaseTurnRules.Advance(null, db, 6, 6));
        state.activeNodeIds.Clear();
        Assert.AreEqual(BankuruwaseTurnTransition.None,
            BankuruwaseTurnRules.Advance(state, db, 6, 6));
        Assert.IsFalse(state.bankuruwaseActive);
    }
}

/// <summary>Participant-level turn transition stays in race registration order.</summary>
public class RaceSessionBankuruwaseTests
{
    private static PlayerState Add(RaceSession session, string name, int position, bool rotor)
    {
        var player = new PlayerState(name, false, position, 1)
        {
            teamId = rotor ? TeamId.JP : TeamId.DE,
            techState = rotor ? new TechTreeState(TeamId.JP) : null
        };
        if (rotor) player.techState.activeNodeIds.Add("jp-l3-bankuruwase");
        session.Players.Add(player);
        return player;
    }

    [Test]
    public void OnlyBottomTwoRotorsActivateAndNotificationsFollowRegistrationOrder()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var secondLast = Add(session, "second", 20, true);
        Add(session, "leader", 40, false);
        var last = Add(session, "last", 10, true);
        var middle = Add(session, "middle", 30, true);
        var notifications = new System.Collections.Generic.List<string>();

        session.AdvanceBankuruwaseForAll((p, transition) =>
            notifications.Add(p.name + ":" + transition));

        CollectionAssert.AreEqual(new[] { "second:Activated", "last:Activated" }, notifications);
        Assert.IsTrue(secondLast.techState.bankuruwaseActive);
        Assert.IsTrue(last.techState.bankuruwaseActive);
        Assert.IsFalse(middle.techState.bankuruwaseActive);
        Assert.AreEqual(3, secondLast.techState.bankuruwaseTurnsLeft);
    }

    [Test]
    public void RankRecoveryDoesNotPauseCountdownOrReactivateOnExpiryBoundary()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var rotor = Add(session, "rotor", 10, true);
        Add(session, "other", 20, false);
        var notifications = new System.Collections.Generic.List<BankuruwaseTurnTransition>();
        System.Action<PlayerState, BankuruwaseTurnTransition> report = (p, transition) =>
            notifications.Add(transition);

        session.AdvanceBankuruwaseForAll(report);
        rotor.position = 30;
        session.AdvanceBankuruwaseForAll(report);
        Assert.AreEqual(2, rotor.techState.bankuruwaseTurnsLeft);
        session.AdvanceBankuruwaseForAll(report);
        Assert.AreEqual(1, rotor.techState.bankuruwaseTurnsLeft);
        session.AdvanceBankuruwaseForAll(report);
        Assert.IsFalse(rotor.techState.bankuruwaseActive);
        CollectionAssert.AreEqual(new[] {
            BankuruwaseTurnTransition.Activated,
            BankuruwaseTurnTransition.Expired
        }, notifications);

        rotor.position = 10;
        session.AdvanceBankuruwaseForAll(report);
        Assert.IsTrue(rotor.techState.bankuruwaseActive);
        Assert.AreEqual(3, notifications.Count);
    }

    [Test]
    public void MissingTechnologyIsSkippedAndNullReporterStillAdvancesState()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var noTechnology = Add(session, "none", 20, false);
        var rotor = Add(session, "rotor", 10, true);

        session.AdvanceBankuruwaseForAll(null);

        Assert.IsNull(noTechnology.techState);
        Assert.IsTrue(rotor.techState.bankuruwaseActive);
        Assert.AreEqual(3, rotor.techState.bankuruwaseTurnsLeft);
    }

    [Test]
    public void ReporterFailureStopsBeforeLaterParticipantsAreAdvanced()
    {
        var session = new RaceSession(new SystemRandomSource(1));
        var first = Add(session, "first", 20, true);
        var second = Add(session, "second", 10, true);
        Add(session, "leader", 30, false);

        Assert.Throws<System.InvalidOperationException>(() =>
            session.AdvanceBankuruwaseForAll((p, transition) =>
                throw new System.InvalidOperationException("presentation failed")));

        Assert.IsTrue(first.techState.bankuruwaseActive);
        Assert.IsFalse(second.techState.bankuruwaseActive);
    }
}

/// <summary>
/// Real turn-start adapter on inactive objects, with an in-memory HUD sink.
/// Does not start GameLoop, load scenes, or read/write player progression.
/// </summary>
public class RaceBankuruwaseAdapterTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private GameObject host;
    private MVPGameManager manager;
    private GameConfigSO config;
    private RaceSession session;
    private HUDUI hud;
    private Random.State previousRandom;
    private readonly List<string> messages = new List<string>();

    [SetUp]
    public void SetUp()
    {
        previousRandom = Random.state;
        host = new GameObject("Bankuruwase adapter regression");
        host.SetActive(false);
        manager = host.AddComponent<MVPGameManager>();
        hud = host.AddComponent<HUDUI>();
        config = ScriptableObject.CreateInstance<GameConfigSO>();
        config.enableTechTree = true;
        manager.config = config;
        manager.hudUI = hud;
        session = new RaceSession(new SystemRandomSource(1));
        typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
        messages.Clear();
        hud.SetLogSink(messages.Add);
    }

    [TearDown]
    public void TearDown()
    {
        hud.SetLogSink(null);
        Object.DestroyImmediate(host);
        Object.DestroyImmediate(config);
        Random.state = previousRandom;
    }

    private PlayerState Add(string name, int position, bool rotor = true)
    {
        var player = new PlayerState(name, false, position, 1)
        {
            teamId = rotor ? TeamId.JP : TeamId.DE,
            techState = rotor ? new TechTreeState(TeamId.JP) : null
        };
        if (rotor) player.techState.activeNodeIds.Add("jp-l3-bankuruwase");
        session.Players.Add(player);
        return player;
    }

    private void Tick() => typeof(MVPGameManager).GetMethod("TickBankuruwaseForAll", Private)
        .Invoke(manager, null);

    private static string Activation(string name) =>
        $"<color=cyan>{name} 番狂わせ激活：连续 3 回合获得四种汤底效果，转子引擎额外冷却 1。</color>";

    [TestCase(false)]
    [TestCase(true)]
    public void DisabledTechnologyLeavesActivationAndCountdownUntouched(bool alreadyActive)
    {
        var rotor = Add("rotor", 10);
        Add("leader", 20, false);
        rotor.techState.bankuruwaseActive = alreadyActive;
        rotor.techState.bankuruwaseTurnsLeft = alreadyActive ? 2 : 0;
        config.enableTechTree = false;

        Tick();

        Assert.AreEqual(alreadyActive, rotor.techState.bankuruwaseActive);
        Assert.AreEqual(alreadyActive ? 2 : 0, rotor.techState.bankuruwaseTurnsLeft);
        Assert.IsEmpty(messages);
    }

    [Test]
    public void ActivationReportsOnlyEligibleRotorsInRegistrationOrder()
    {
        var secondLast = Add("second", 20);
        Add("leader", 40, false);
        var last = Add("last", 10);
        var middle = Add("middle", 30);

        Tick();

        CollectionAssert.AreEqual(new[] { Activation("second"), Activation("last") }, messages);
        Assert.AreEqual(3, secondLast.techState.bankuruwaseTurnsLeft);
        Assert.AreEqual(3, last.techState.bankuruwaseTurnsLeft);
        Assert.IsFalse(middle.techState.bankuruwaseActive);
    }

    [Test]
    public void ContinuingTurnsStaySilentAndExpiryWaitsUntilNextBoundaryToReactivate()
    {
        var rotor = Add("rotor", 10);
        Add("other", 20, false);
        Add("leader", 30, false);
        Tick();
        rotor.position = 40;
        Tick();
        Assert.AreEqual(2, rotor.techState.bankuruwaseTurnsLeft);
        Tick();
        Assert.AreEqual(1, rotor.techState.bankuruwaseTurnsLeft);
        CollectionAssert.AreEqual(new[] { Activation("rotor") }, messages);

        rotor.position = 10;
        Tick();

        Assert.IsFalse(rotor.techState.bankuruwaseActive);
        Assert.AreEqual(0, rotor.techState.bankuruwaseTurnsLeft);
        CollectionAssert.AreEqual(new[] { Activation("rotor"), "rotor 番狂わせ效果结束。" }, messages);
        Tick();
        Assert.AreEqual(3, rotor.techState.bankuruwaseTurnsLeft);
        CollectionAssert.AreEqual(new[] {
            Activation("rotor"), "rotor 番狂わせ效果结束。", Activation("rotor")
        }, messages);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void MissingHudDoesNotSuppressActivationOrExpiry(bool alreadyActive)
    {
        var rotor = Add("rotor", 10);
        Add("leader", 20, false);
        rotor.techState.bankuruwaseActive = alreadyActive;
        rotor.techState.bankuruwaseTurnsLeft = alreadyActive ? 1 : 0;
        manager.hudUI = null;

        Tick();

        Assert.AreEqual(!alreadyActive, rotor.techState.bankuruwaseActive);
        Assert.AreEqual(alreadyActive ? 0 : 3, rotor.techState.bankuruwaseTurnsLeft);
        Assert.IsEmpty(messages);
    }

    [Test]
    public void HudFailureKeepsCurrentTransitionAndStopsLaterParticipant()
    {
        var first = Add("first", 20);
        var later = Add("later", 10);
        Add("leader", 30, false);
        hud.SetLogSink(message => {
            messages.Add(message);
            throw new System.InvalidOperationException("HUD sink failed");
        });

        var exception = Assert.Throws<TargetInvocationException>(() => Tick());

        Assert.IsInstanceOf<System.InvalidOperationException>(exception.InnerException);
        Assert.AreEqual(3, first.techState.bankuruwaseTurnsLeft);
        Assert.IsFalse(later.techState.bankuruwaseActive);
        CollectionAssert.AreEqual(new[] { Activation("first") }, messages);
    }

    [Test]
    public void HudCallbackRankChangeIsReadForNextParticipant()
    {
        var first = Add("first", 20);
        var later = Add("later", 10);
        Add("leader", 30, false);
        hud.SetLogSink(message => { messages.Add(message); later.position = 40; });

        Tick();

        Assert.IsTrue(first.techState.bankuruwaseActive);
        Assert.AreEqual(1, session.GetRank(later));
        Assert.IsFalse(later.techState.bankuruwaseActive,
            "Ranks must be read at each participant, not snapshotted before HUD callbacks.");
        CollectionAssert.AreEqual(new[] { Activation("first") }, messages);
    }
}
