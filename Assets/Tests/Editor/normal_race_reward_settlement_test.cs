using System.Collections.Generic;
using NUnit.Framework;

public class NormalRaceRewardSettlementTest
{
    [Test]
    public void test_normal_race_grants_ranked_rewards_and_persists_only_human()
    {
        var session = new RaceSession(new SystemRandomSource(17));
        var human = new PlayerState("Human", false, 8, 1)
        {
            teamId = TeamId.UK, hasFinished = true, finishOrder = 1,
            techState = new TechTreeState(TeamId.UK, 200)
        };
        var ai = new PlayerState("AI", true, 4, 1)
        {
            teamId = TeamId.DE, hasFinished = true, finishOrder = 2,
            techState = new TechTreeState(TeamId.DE, 100)
        };
        session.Players.Add(human);
        session.Players.Add(ai);
        var savedTech = new List<TechTreeState>();
        var savedXp = new List<int>();

        string report = NormalRaceRewardSettlement.Settle(
            session, "UK", false, false,
            state => savedTech.Add(state), (id, xp) => savedXp.Add(xp));

        Assert.That(human.techState.rpBalance, Is.EqualTo(5200));
        Assert.That(ai.techState.rpBalance, Is.EqualTo(3600));
        Assert.That(human.driverXp, Is.EqualTo(
            DriverProgression.CalculateRaceXp(1, human.DriverProfile.TalentMultiplier, TeamId.UK)));
        Assert.That(ai.driverXp, Is.EqualTo(
            DriverProgression.CalculateRaceXp(2, ai.DriverProfile.TalentMultiplier, TeamId.DE)));
        Assert.That(savedTech, Is.EqualTo(new[] { human.techState }));
        Assert.That(savedXp, Is.EqualTo(new[] { human.driverXp }));
        StringAssert.Contains("RP 奖励:\n1. Human: +5000 RP", report);
        StringAssert.Contains("\n\n车手 XP:\n", report);
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public void test_tutorial_and_career_never_mutate_or_save_normal_rewards(
        bool tutorialActive, bool careerActive)
    {
        var session = new RaceSession(new SystemRandomSource(18));
        var human = new PlayerState("Human", false, 0, 1)
        {
            teamId = TeamId.CN, techState = new TechTreeState(TeamId.CN, 77), driverXp = 42
        };
        session.Players.Add(human);
        int saveCount = 0;

        string report = NormalRaceRewardSettlement.Settle(
            session, "CN", tutorialActive, careerActive,
            state => saveCount++, (id, xp) => saveCount++);

        Assert.That(report, Is.Empty);
        Assert.That(human.techState.rpBalance, Is.EqualTo(77));
        Assert.That(human.driverXp, Is.EqualTo(42));
        Assert.That(saveCount, Is.Zero);
    }

    [Test]
    public void test_cavallino_home_race_and_blown_driver_keep_existing_rules()
    {
        var session = new RaceSession(new SystemRandomSource(19));
        var winner = new PlayerState("Winner", false, 0, 1)
        {
            teamId = TeamId.IT, hasFinished = true, finishOrder = 1,
            techState = new TechTreeState(TeamId.IT)
        };
        winner.techState.activeNodeIds.Add("it-l3-cavallino-rampante");
        var blown = new PlayerState("Blown", true, 0, 1)
        {
            teamId = TeamId.US, isBlown = true, driverXp = 90
        };
        session.Players.Add(winner);
        session.Players.Add(blown);

        string report = NormalRaceRewardSettlement.Settle(
            session, "IT", false, false, state => { }, (id, xp) => { });

        Assert.That(winner.techState.rpBalance, Is.EqualTo(11250));
        Assert.That(blown.driverXp, Is.EqualTo(90));
        StringAssert.Contains("Winner: +11250 RP", report);
        StringAssert.Contains("Blown（", report);
        StringAssert.Contains("+0 XP", report);
    }
}
