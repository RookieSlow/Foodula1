using NUnit.Framework;

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
