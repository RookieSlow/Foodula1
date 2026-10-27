using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>Actual native GameLoop adapters for the new paid API; the human skill button is not migrated here.</summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator MansellPaidTwoLegsObserveActualOtherCarActionsAndNeverSpendAnotherRaceUse()
    {
        CreateRace(TeamId.UK);
        try
        {
            var owner = session.Human;
            owner.driverId = "uk_nigel_mansell"; owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
            session.TeamVehicleBonusesEnabled = false;
            session.Players[1].position = 6;
            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Mansell first A1");
            CommitMansellLeg(owner, false);
            yield return ConfirmTurnCards();
            yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "Mansell second real A1");
            Assert.That(owner.position, Is.EqualTo(3));
            Assert.That(session.Players[1].position, Is.EqualTo(7), "The target must actually act between the two snapshots.");
            Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.True);
            Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(5));
            CommitMansellLeg(owner, true);
            yield return ConfirmTurnCards();
            yield return WaitFor(() => Turn == 3 && manager.TutorialInputPhase == "gear", "Mansell after two paid legs");
            Assert.That(owner.position, Is.GreaterThanOrEqualTo(7)); // Original native slipstream can add more.
            Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2));
            Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(4));
            Assert.That(owner.driverSkill.Charge.IsSecondLegAvailable, Is.False);
            Assert.That(DriverSkillRules.GetMovementBonus(owner.driverSkill, false), Is.Zero);
            Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator MansellPaidNewTailApexKeepsOrdinaryPartialPaymentAndSpin()
    {
        foreach (int available in new[] { 6, 1 })
        {
            CreateRace(TeamId.UK);
            try
            {
                var owner = session.Human;
                owner.driverId = "uk_nigel_mansell"; owner.driverSkill.Initialize(owner.DriverProfile, 7, true);
                session.TeamVehicleBonusesEnabled = false; session.Players[1].position = 6;
                owner.deck.InitializeExactOrder(Enumerable.Range(0, 10)
                    .Select(_ => new CardData(CardType.Speed, 2)).ToArray(), new HeatPool(6));
                owner.deck.DrawToHand(3);
                var nodes = Get<List<TrackNode>>(manager.trackManager, "nodes");
                nodes[3] = new TrackNode(3, 1, "paid Mansell tail apex", 1, false, true);
                Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits")[1] = 1;
                Get<Dictionary<int, string>>(manager.trackManager, "cornerNames")[1] = "paid Mansell tail apex";
                if (available < 6) owner.deck.DrawHeatFromPool(6 - available);
                manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
                yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Mansell paid apex A1");
                CommitMansellLeg(owner, false);
                manager.OnGearButtonClicked(1); manager.OnConfirmGearClicked();
                yield return WaitFor(() => manager.TutorialInputPhase == "cards", "paid apex speed input");
                CardUI speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(x => x.cardData.IsSpeed);
                speed.SetSelectedWithoutNotify(true); manager.OnPlayCardsButtonClicked(); manager.OnPlayCardsButtonClicked();
                // A spin skips the ordinary discard prompt; observe the actual resolution instead.
                yield return WaitFor(() => owner.spinCounter > 0 || manager.TutorialInputPhase == "discard", "Mansell actual extra-apex settlement");
                Assert.That(owner.driverSkill.UsesRemaining, Is.EqualTo(2), "An ordinary penalty never refunds this race use.");
                Assert.That(owner.spinCounter, Is.EqualTo(available == 1 ? 1 : 0));
                Assert.That(owner.deck.heatPool.remaining, Is.EqualTo(available == 1 ? 6 : 4));
                Assert.That(messages.Any(x => x.Contains(available == 1 ? "SPINS OUT" : "paid Mansell tail apex")), Is.True, string.Join("\n", messages));
                Assert.That(owner.deck.heatPool.remaining + owner.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
            }
            finally { DestroyRace(); }
            yield return null;
        }
    }

    private void CommitMansellLeg(PlayerState owner, bool second)
    {
        var context = new DriverSkillActivationContext(true, owner.lap + 1, config.totalLaps,
            owner.deck.heatPool.remaining, 6, 0);
        Assert.That(session.TryPrepareMansellCharge(owner, manager.trackManager.TotalNodes,
            manager.trackManager.StartFinishNodeIndex, Turn, 1, Turn, context, out var plan, out string reason), Is.True, reason);
        Assert.That(plan.IsSecondLeg, Is.EqualTo(second));
        Assert.That(plan.TryCommit(session, owner, manager.trackManager.TotalNodes,
            manager.trackManager.StartFinishNodeIndex, Turn, 1, Turn, context,
            second ? null : plan.Choices[0], out reason), Is.True, reason);
    }
}
