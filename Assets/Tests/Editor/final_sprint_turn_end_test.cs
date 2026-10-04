using NUnit.Framework;
using System;
using System.Collections.Generic;

public class FinalSprintTurnEndTests
{
    [TestCase(0, 3, 1, 1, false, true)]
    [TestCase(0, 3, 2, 1, false, true)]
    [TestCase(0, 3, 3, 1, false, false)]
    [TestCase(2, 3, 1, 3, true, false)]
    [TestCase(2, 3, 3, 3, true, false)]
    [TestCase(3, 5, 2, 4, false, true)]
    [TestCase(8, 5, 3, 5, true, false)]
    [TestCase(0, 0, 3, 0, true, false)]
    public void CostPreservesSpinCapAndRecoveryTier(int current, int cap, int tier,
        int next, bool blown, bool recovery)
    {
        var cost = DriverSkillRules.EvaluateFinalSprintCost(current, cap, tier);
        Assert.AreEqual(next, cost.SpinCounter);
        Assert.AreEqual(blown, cost.BlowsEngine);
        Assert.AreEqual(recovery, cost.RequiresRecovery);
    }

    [Test]
    public void GateRequiresActualFinalSprintActivationThisTurn()
    {
        Assert.IsFalse(DriverSkillRules.ShouldResolveFinalSprintCost(null));
        DriverCatalog.TryGet("it_tazio_nuvolari", out DriverProfile profile);
        var state = new DriverSkillRuntimeState();
        state.Initialize(profile, 3, true);
        Assert.IsFalse(DriverSkillRules.ShouldResolveFinalSprintCost(state));
        Assert.IsTrue(state.TryActivate(profile, new DriverSkillActivationContext(true, 0, 3, 0, 6, 0), out _));
        Assert.IsTrue(DriverSkillRules.ShouldResolveFinalSprintCost(state));
        state.BeginTurn();
        Assert.IsFalse(DriverSkillRules.ShouldResolveFinalSprintCost(state));
        DriverCatalog.TryGet("us_tony_stewart", out profile);
        state.Initialize(profile, 3, true);
        Assert.IsTrue(state.TryActivate(profile, new DriverSkillActivationContext(true, 0, 3, 0, 6, 0), out _));
        Assert.IsFalse(DriverSkillRules.ShouldResolveFinalSprintCost(state));
    }
}

public class RaceTurnSkillCleanupTests
{
    [TestCase(3, 0, 3, 1, false, true)]
    [TestCase(7, 0, 5, 1, false, false)]
    [TestCase(7, 4, 5, 5, true, false)]
    public void CommitCostThenReportThenReadOvertakesWithoutConsumingActiveState(
        int level, int current, int cap, int expected, bool blown, bool recovery)
    {
        PlayerState player = CreateFinalSprint(level);
        player.spinCounter = current;
        int uses = player.driverSkill.UsesRemaining;
        int duration = player.driverSkill.ActiveTurnsRemaining;
        var calls = new List<string>();

        RaceTurnSkillCleanup.Execute(player, p =>
        {
            Assert.That(p, Is.SameAs(player));
            Assert.That(p.spinCounter, Is.EqualTo(current));
            Assert.That(p.isBlown, Is.False);
            Assert.That(p.skipNextTurn, Is.False);
            calls.Add("cap");
            return cap;
        }, p =>
        {
            Assert.That(calls, Is.EqualTo(new[] { "cap", "report" }));
            calls.Add("overtakes");
            return 0;
        }, (p, actualCap) =>
        {
            Assert.That(calls, Is.EqualTo(new[] { "cap" }));
            Assert.That(actualCap, Is.EqualTo(cap));
            Assert.That(p.spinCounter, Is.EqualTo(expected));
            Assert.That(p.isBlown, Is.EqualTo(blown));
            Assert.That(p.skipNextTurn, Is.EqualTo(recovery));
            calls.Add("report");
        });

        Assert.That(calls, Is.EqualTo(new[] { "cap", "report", "overtakes" }));
        Assert.That(player.driverSkill.ActivatedThisTurn, Is.True);
        Assert.That(player.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(player.driverSkill.ActiveTurnsRemaining, Is.EqualTo(duration));
    }

    [TestCase("inactive")]
    [TestCase("stale")]
    [TestCase("other")]
    [TestCase("disabled")]
    [TestCase("missing")]
    public void IneligibleCostStillReadsOvertakesWithoutQueryingSpinCap(string state)
    {
        var player = new PlayerState("driver", false, 0, 1);
        string driverId = state == "other" ? "us_tony_stewart" : "it_tazio_nuvolari";
        Assert.That(DriverCatalog.TryGet(driverId, out DriverProfile driver), Is.True);
        player.driverSkill.Initialize(driver, 3, state != "disabled");
        if (state == "stale" || state == "other")
            Assert.That(player.driverSkill.TryActivate(driver, CriticalEngine(), out _), Is.True);
        if (state == "stale") player.driverSkill.BeginTurn();
        if (state == "disabled")
            Assert.That(player.driverSkill.TryActivate(driver, CriticalEngine(), out _), Is.False);
        if (state == "missing") player.driverSkill = null;
        player.spinCounter = 2;
        int queries = 0;

        RaceTurnSkillCleanup.Execute(player,
            p => throw new InvalidOperationException("Inactive cost must not query spin cap."),
            p => { queries++; return 1; },
            (p, cap) => Assert.Fail("Inactive cost must not report."));

        Assert.That(queries, Is.EqualTo(1));
        Assert.That(player.spinCounter, Is.EqualTo(2));
        Assert.That(player.isBlown, Is.False);
        Assert.That(player.skipNextTurn, Is.False);
    }

    [Test]
    public void PassiveUsesRuntimeAndOvertakesChangedDuringReport()
    {
        PlayerState player = CreateFinalSprint(7);
        var overtakes = new Dictionary<PlayerState, int> { [player] = 0 };
        DriverSkillRuntimeState replacement = CreateMansell();

        RaceTurnSkillCleanup.Execute(player, p => 3, p => overtakes[p], (p, cap) =>
        {
            p.driverSkill = replacement;
            overtakes[p] = 2;
        });

        Assert.That(player.spinCounter, Is.EqualTo(1));
        replacement.BeginTurn();
        Assert.That(replacement.PassiveMovementBonusThisTurn, Is.EqualTo(2));
        Assert.That(replacement.PassiveCoolingBonusThisTurn, Is.EqualTo(1));
    }

    [TestCase("cap")]
    [TestCase("report")]
    [TestCase("overtakes")]
    public void ExceptionStopsLaterEffectsWithoutRollingBackEarlierCost(string stage)
    {
        PlayerState player = CreateFinalSprint(3);
        DriverSkillRuntimeState replacement = CreateMansell();
        var failure = new InvalidOperationException(stage);
        var calls = new List<string>();

        var raised = Assert.Throws<InvalidOperationException>(() => RaceTurnSkillCleanup.Execute(player,
            p =>
            {
                calls.Add("cap");
                if (stage == "cap") throw failure;
                return 3;
            }, p =>
            {
                calls.Add("overtakes");
                throw failure;
            }, (p, cap) =>
            {
                calls.Add("report");
                p.driverSkill = replacement;
                if (stage == "report") throw failure;
            }));

        Assert.That(raised, Is.SameAs(failure));
        Assert.That(calls, Is.EqualTo(stage == "cap" ? new[] { "cap" } :
            stage == "report" ? new[] { "cap", "report" } : new[] { "cap", "report", "overtakes" }));
        Assert.That(player.spinCounter, Is.EqualTo(stage == "cap" ? 0 : 1));
        Assert.That(player.skipNextTurn, Is.EqualTo(stage != "cap"));
        Assert.That(player.isBlown, Is.False);
        replacement.BeginTurn();
        Assert.That(replacement.PassiveMovementBonusThisTurn, Is.Zero);
    }

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(3)]
    public void OvertakeRewardIsArmedOnlyForNextTurnAndConsumedOnce(int overtakes)
    {
        var player = new PlayerState("driver", false, 0, 1) { driverSkill = CreateMansell() };
        RaceTurnSkillCleanup.Execute(player,
            p => throw new InvalidOperationException("Passive-only cleanup has no cost."),
            p => overtakes, (p, cap) => Assert.Fail("Passive-only cleanup has no report."));

        Assert.That(player.driverSkill.PassiveMovementBonusThisTurn, Is.Zero);
        player.driverSkill.BeginTurn();
        Assert.That(player.driverSkill.PassiveMovementBonusThisTurn, Is.EqualTo(overtakes > 0 ? 2 : 0));
        Assert.That(player.driverSkill.PassiveCoolingBonusThisTurn, Is.EqualTo(overtakes > 0 ? 1 : 0));
        player.driverSkill.BeginTurn();
        Assert.That(player.driverSkill.PassiveMovementBonusThisTurn, Is.Zero);
        Assert.That(player.driverSkill.PassiveCoolingBonusThisTurn, Is.Zero);
    }

    [Test]
    public void MissingReportDoesNotSuppressCostOrLaterOvertakeQuery()
    {
        PlayerState player = CreateFinalSprint(7);
        player.spinCounter = 2;
        int queries = 0;
        RaceTurnSkillCleanup.Execute(player, p => 3, p =>
        {
            Assert.That(p.isBlown, Is.True);
            queries++;
            return 0;
        }, null);
        Assert.That(player.spinCounter, Is.EqualTo(3));
        Assert.That(queries, Is.EqualTo(1));
    }

    private static PlayerState CreateFinalSprint(int level)
    {
        var player = new PlayerState("driver", false, 0, 1);
        Assert.That(DriverCatalog.TryGet("it_tazio_nuvolari", out DriverProfile driver), Is.True);
        player.driverSkill.Initialize(driver, level, true);
        Assert.That(player.driverSkill.TryActivate(driver, CriticalEngine(), out _), Is.True);
        return player;
    }

    private static DriverSkillRuntimeState CreateMansell()
    {
        Assert.That(DriverCatalog.TryGet("uk_nigel_mansell", out DriverProfile driver), Is.True);
        var state = new DriverSkillRuntimeState();
        state.Initialize(driver, 7, true);
        return state;
    }

    private static DriverSkillActivationContext CriticalEngine()
        => new DriverSkillActivationContext(true, 0, 3, 0, 6, 0);
}
