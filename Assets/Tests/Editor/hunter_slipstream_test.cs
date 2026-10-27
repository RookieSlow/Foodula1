using System.Collections.Generic;
using NUnit.Framework;

/// <summary>Production pure-runtime tests. Can also run independently; not a Unity movement/visual acceptance.</summary>
public class HunterSlipstreamTests
{
    private RaceSession race;
    private PlayerState owner, target;
    private const int Nodes = 60;

    [SetUp]
    public void SetUp()
    {
        race = new RaceSession(new SystemRandomSource(9)) { TeamVehicleBonusesEnabled = false };
        owner = new PlayerState("Hart", false, 0, 1) { teamId = TeamId.UK, driverId = "uk_hunter_hart" };
        target = new PlayerState("Target", true, 1, 1) { teamId = TeamId.DE, driverId = "de_michael_schumacher" };
        race.Players.Add(owner); race.Players.Add(target);
        owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
    }

    private void Begin() { race.BeginTurn(owner); race.BeginDriverSkillInput(owner, Nodes); }

    private SlipstreamChainResult Resolve()
    {
        var results = new Dictionary<PlayerState, SlipstreamChainResult>();
        RaceSettledSlipstreamResolution.Execute(race, new[] { owner }, new HashSet<PlayerState>(),
            Nodes, owner, default, results, null);
        return results[owner];
    }

    private void Move(int cells)
    {
        int raw = owner.position + cells; owner.lap += raw / Nodes; owner.position = raw % Nodes;
    }

    [TestCase(2, 3, true)] [TestCase(2, 4, false)]
    [TestCase(4, 4, true)] [TestCase(4, 5, false)]
    [TestCase(6, 5, true)] [TestCase(6, 6, false)]
    [TestCase(1, 1, false)] [TestCase(7, 0, false)] [TestCase(7, -1, false)]
    public void LocksNearestActualProgressWithinTierRange(int level, int gap, bool expected)
    {
        owner.position = 10; target.position = 10 + gap;
        owner.driverSkill.Initialize(owner.DriverProfile, level, true);
        Begin();
        Assert.AreEqual(expected ? target : null, owner.driverSkill.Hunting.Target);
    }

    [Test]
    public void TwoNativeLegsRemainAndExtraCellDoesNotFindThirdCar()
    {
        var second = new PlayerState("second", true, 3, 1);
        var third = new PlayerState("third", true, 5, 1);
        race.Players.Add(second); race.Players.Add(third); Begin();
        var chain = Resolve();
        Assert.AreEqual(2, chain.Steps.Count); Assert.AreEqual(4, chain.TotalBonus);
        Assert.AreEqual(-1, owner.driverSkill.Hunting.LastUsedLap, "Resolution alone is not actual traversal.");
        Assert.IsNull(owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, 0));
        Move(chain.TotalBonus);
        var reward = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus);
        Assert.IsNotNull(reward);
        Assert.IsNull(owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus), "No duplicate receipt.");
        Move(1); Assert.IsTrue(reward.Complete(1)); Assert.IsFalse(reward.Complete(1));
        Assert.AreEqual(5, owner.position); Assert.AreEqual(0, owner.driverSkill.Hunting.LastUsedLap);
        Assert.AreEqual(2, chain.Steps.Count); Assert.AreEqual(4, chain.TotalBonus, "Native chain stays immutable.");
        Assert.AreEqual(5, third.position);
    }

    [Test]
    public void QueriesAndForecastCopyNeverSpendRealOpportunity()
    {
        Begin(); var copy = owner.driverSkill.CreatePresentationCopy();
        Assert.AreNotSame(copy.Hunting, owner.driverSkill.Hunting);
        for (int i = 0; i < 3; i++) race.ComputeSlipstreamChain(owner, race.Players, Nodes);
        copy.Hunting.CloseInput();
        Assert.AreSame(target, owner.driverSkill.Hunting.Target);
        Assert.AreEqual(-1, owner.driverSkill.Hunting.LastUsedLap);
        var chain = Resolve(); Move(chain.TotalBonus);
        Assert.IsNotNull(owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus));
    }

    [TestCase("blocked")] [TestCase("different leader")] [TestCase("same cell")]
    [TestCase("removed")] [TestCase("replaced id")]
    public void NoLegalLockedPositiveGapSourceDoesNotAward(string reason)
    {
        Begin();
        if (reason == "blocked") { target.trickState.iceJellyActive = true; }
        if (reason == "different leader")
        {
            target.position = 9; race.Players.Add(new PlayerState("other", true, 1, 1));
        }
        if (reason == "same cell") { target.position = 0; target.totalMovementThisTurn = 5; }
        if (reason == "removed") race.Players.Remove(target);
        if (reason == "replaced id") target.driverId = "de_sebastian_vettel";
        var chain = Resolve(); Move(chain.TotalBonus);
        Assert.IsNull(owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus));
        Assert.AreEqual(-1, owner.driverSkill.Hunting.LastUsedLap);
    }

    [Test]
    public void OncePerOriginLapAndNextLapRearmsWithoutResettingRace()
    {
        Begin(); var chain = Resolve(); Move(chain.TotalBonus);
        var reward = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus);
        Move(1); Assert.IsTrue(reward.Complete(1));
        owner.position = 10; target.position = 11; Begin();
        Assert.IsNull(owner.driverSkill.Hunting.Target);
        owner.lap = target.lap = 1; Begin();
        Assert.AreSame(target, owner.driverSkill.Hunting.Target);
    }

    [Test]
    public void CrossingLineDuringNativeChainChargesOnlyInputOriginLap()
    {
        owner.position = 58; target.position = 59; Begin();
        var chain = Resolve(); Move(chain.TotalBonus);
        var reward = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus);
        Assert.IsNotNull(reward); Move(1); Assert.IsTrue(reward.Complete(1));
        Assert.AreEqual(1, owner.lap); Assert.AreEqual(0, owner.driverSkill.Hunting.LastUsedLap);
    }

    [TestCase("skip")] [TestCase("finished")] [TestCase("new input")]
    [TestCase("initialize")] [TestCase("close")]
    public void InterruptionAfterPreparationInvalidatesReward(string reason)
    {
        Begin(); var chain = Resolve(); Move(chain.TotalBonus);
        if (reason == "skip") owner.skipNextTurn = true;
        if (reason == "pit") owner.pitStopScheduled = true;
        if (reason == "finished") owner.hasFinished = true;
        if (reason == "new input") race.BeginTurn(owner);
        if (reason == "initialize") owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
        if (reason == "close") owner.driverSkill.Hunting.CloseInput();
        Assert.IsNull(owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus));
    }

    [TestCase(false)] [TestCase(true)]
    public void ActualExtraCellIncludingFinishConsumesOnlyAfterTraversal(bool finish)
    {
        Begin(); var chain = Resolve(); Move(chain.TotalBonus);
        var reward = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus);
        Assert.IsNotNull(reward);
        if (finish) { Move(1); owner.hasFinished = true; }
        Assert.AreEqual(finish, reward.Complete(finish ? 1 : 0));
        Assert.AreEqual(finish ? 0 : -1, owner.driverSkill.Hunting.LastUsedLap);
    }

    [TestCase(false)] [TestCase(true)]
    public void DisabledModeOrOrdinaryAiDoesNotLock(bool ai)
    {
        owner.isAI = ai; owner.driverSkill.Initialize(owner.DriverProfile, 7, ai);
        Begin(); Assert.IsNull(owner.driverSkill.Hunting.Target);
    }

    [Test]
    public void RepeatedInputDoesNotRetargetAndTwoHartRacersDoNotShareQuota()
    {
        Begin(); target.position = 7;
        var other = new PlayerState("nearer", true, 1, 1); race.Players.Add(other);
        Assert.IsFalse(race.BeginDriverSkillInput(owner, Nodes));
        Assert.AreSame(target, owner.driverSkill.Hunting.Target);
        var secondHart = new PlayerState("Hart2", false, 2, 1) { teamId = TeamId.UK, driverId = "uk_hunter_hart" };
        race.Players.Add(secondHart); secondHart.driverSkill.Initialize(secondHart.DriverProfile, 7, true);
        race.BeginTurn(secondHart); race.BeginDriverSkillInput(secondHart, Nodes);
        Assert.AreSame(target, secondHart.driverSkill.Hunting.Target);
        Assert.AreNotSame(owner.driverSkill.Hunting, secondHart.driverSkill.Hunting);
        Assert.AreEqual(-1, secondHart.driverSkill.Hunting.LastUsedLap);
    }

    [TestCase(0)] [TestCase(1)]
    public void PartialNativeTraversalCannotAward(int actualCells)
    {
        Begin(); Resolve(); Move(actualCells);
        Assert.IsNull(owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, actualCells));
        Assert.AreEqual(-1, owner.driverSkill.Hunting.LastUsedLap);
    }

    [Test]
    public void LockedTargetCanBeTheSecondNativeLeg()
    {
        target.position = 3; Begin();
        var intervening = new PlayerState("intervening", true, 1, 1); race.Players.Add(intervening);
        var chain = Resolve(); Assert.AreEqual(2, chain.Steps.Count);
        Assert.AreSame(target, chain.Steps[1].Leader);
        Move(chain.TotalBonus);
        var reward = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus);
        Assert.IsNotNull(reward); Move(1); Assert.IsTrue(reward.Complete(1));
    }

    [Test]
    public void ReplacingTargetWithSameIdDoesNotTransferLock()
    {
        Begin(); race.Players.Remove(target);
        race.Players.Add(new PlayerState("replacement", true, 1, 1) { driverId = target.driverId });
        var chain = Resolve(); Move(chain.TotalBonus);
        Assert.IsNull(owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus));
    }

    [Test]
    public void StaleReceiptCannotCloseNewInput()
    {
        Begin(); var chain = Resolve(); Move(chain.TotalBonus);
        var old = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus);
        Assert.IsNotNull(old);
        owner.position = 10; target.position = 11; Begin();
        Assert.IsFalse(old.Complete(1));
        Assert.AreSame(target, owner.driverSkill.Hunting.Target);
        chain = Resolve(); Move(chain.TotalBonus);
        var current = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus);
        Assert.IsNotNull(current); Move(1); Assert.IsTrue(current.Complete(1));
    }

    [TestCase(true)] [TestCase(false)]
    public void NonzeroFinishSupportsLineCrossingAndArrayWrap(bool crossFinish)
    {
        owner.position = crossFinish ? 28 : 58;
        target.position = crossFinish ? 29 : 0;
        race.SlipstreamRangeOverride = 2;
        race.BeginTurn(owner); race.BeginDriverSkillInput(owner, Nodes, 30);
        Assert.AreSame(target, owner.driverSkill.Hunting.Target);
        var chain = Resolve(); Assert.AreEqual(2, chain.TotalBonus);
        owner.position = crossFinish ? 30 : 0; owner.lap = crossFinish ? 1 : 0;
        var reward = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, 2);
        Assert.IsNotNull(reward); owner.position++; Assert.IsTrue(reward.Complete(1));
        Assert.AreEqual(0, owner.driverSkill.Hunting.LastUsedLap);
    }

    [Test]
    public void LaterTargetMovementAndLandmarkMovementDoNotChangeFrozenNativeFact()
    {
        Begin(); var chain = Resolve(); Move(chain.TotalBonus);
        target.position += 10; Move(3); // Separate earlier target/landmark effects, not native receipt cells.
        owner.pitStopScheduled = true; // This input drove; actual stop will clear the next input.
        var reward = owner.driverSkill.Hunting.BeginReward(race, owner, Nodes, chain.TotalBonus);
        Assert.IsNotNull(reward); Move(1); Assert.IsTrue(reward.Complete(1));
    }
}
