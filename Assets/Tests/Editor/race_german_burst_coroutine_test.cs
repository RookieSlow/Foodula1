using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>Actual native GameLoop declaration, human fuel input, AI choice and cleanup. No real progression writes.</summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator GermanDeclaredThreeFuelUsesReturnOnlyWindowReceiptsAfterActualGameLoop()
    {
        CreateRace(TeamId.DE);
        try
        {
            ConfigureGermanBurstLoopTech();
            PlayerState human = session.Human;
            PlayerState ai = session.Players.First(p => p.isAI);
            human.deck.DrawHeatFromPool(1, HeatPaymentDestination.Hand);
            CardData oldHeat = human.deck.Hand.Last();
            Assert.That(oldHeat.IsHeat && !oldHeat.isTemp, Is.True);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(5));

            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "German declaration A1");
            manager.OnGermanBurstButtonClicked();
            Assert.That(human.techState.germanBurst.Declared, Is.True);
            Assert.That(human.techState.grillSpezialUsed, Is.False);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(5), "A declaration cannot reserve or spend heat");
            manager.OnGearButtonClicked(1); manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "German declared A4");

            var receipts = new List<CardData>();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(manager.TryPrepareGermanFuelRequest(out var request, out string reason), Is.True, reason);
                Assert.That(manager.TryConfirmGermanFuelRequest(request, out reason), Is.True, reason);
                receipts.Add(request.PaidHeat);
            }
            Assert.That(human.techState.germanBurst.FuelUsesThisTurn, Is.EqualTo(3));
            Assert.That(human.techState.grillSpezialUsed, Is.True);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(2));
            Assert.That(manager.TryPrepareGermanFuelRequest(out _, out _), Is.False, "No fourth or extra ordinary fuel use");
            Assert.That(receipts.Distinct().Count(), Is.EqualTo(3));

            CardUI speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(card => card.cardData.IsSpeed);
            Assert.That(speed.cardData.value, Is.EqualTo(1));
            speed.SetSelectedWithoutNotify(true);
            manager.OnPlayCardsButtonClicked();
            Assert.That(human.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
            manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "German actual movement and optional discard");
            Assert.That(human.position, Is.EqualTo(7), "One speed plus three actual +2 fuel uses");
            manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "German actual cleanup complete");

            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(5));
            Assert.That(human.deck.ContainsInHand(oldHeat), Is.True, "Pre-declaration old heat cannot substitute for window receipts");
            foreach (CardData receipt in receipts)
                Assert.That(human.deck.IsUniquePermanentHeatInHandOrDiscard(receipt), Is.False);
            Assert.That(human.techState.grillSpezialUsed, Is.True);
            Assert.That(human.techState.germanBurst.Declared, Is.False);
            Assert.That(human.techState.germanBurst.FuelUsesThisTurn, Is.Zero, "Only the new actual global turn resets fuel budget");
            Assert.That(ai.deck.heatPool.remaining, Is.EqualTo(6));
            Assert.That(ai.techState.grillSpezialUsed, Is.True, "The AI declared before its actual automatic fuel payment");
            AssertActiveAndHeatConserved();
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator GermanCancelledDeclarationCannotRefundActualMissingCardPayment()
    {
        foreach (bool cancelled in new[] { false, true })
        {
            CreateRace(TeamId.DE);
            try
            {
                ConfigureGermanBurstLoopTech();
                PlayerState human = session.Human;
                manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
                yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "German missing-card declaration A1");
                manager.OnGermanBurstButtonClicked();
                Assert.That(human.techState.germanBurst.Declared, Is.True);
                if (cancelled) manager.OnGermanBurstButtonClicked();
                Assert.That(human.techState.germanBurst.Declared, Is.EqualTo(!cancelled));
                Assert.That(human.techState.grillSpezialUsed, Is.False);
                Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
                manager.OnGearButtonClicked(1); manager.OnConfirmGearClicked();
                yield return WaitFor(() => manager.TutorialInputPhase == "cards", "German missing-card A4");

                // No selected CardUI: this is the real missing-card payment path, not fabricated played state.
                manager.OnPlayCardsButtonClicked();
                Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(1));
                CardData receipt = human.heatPaidCardsThisTurn[0];
                Assert.That(human.deck.heatPool.remaining, Is.EqualTo(5));
                Assert.That(human.techState.grillSpezialUsed, Is.EqualTo(!cancelled));
                Assert.That(human.techState.germanBurst.FuelUsesThisTurn, Is.Zero);
                yield return WaitFor(() => manager.TutorialInputPhase == "discard", "German missing-card ordinary reaction");
                Assert.That(human.position, Is.Zero, "No fuel can be automatically bought after cancellation or an empty selection");
                manager.OnPlayCardsButtonClicked();
                yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "German missing-card cleanup complete");

                Assert.That(human.deck.heatPool.remaining, Is.EqualTo(cancelled ? 5 : 6));
                Assert.That(human.deck.ContainsInHand(receipt), Is.EqualTo(cancelled));
                Assert.That(human.techState.grillSpezialUsed, Is.EqualTo(!cancelled));
                Assert.That(human.techState.germanBurst.Declared, Is.False);
                AssertActiveAndHeatConserved();
            }
            finally { DestroyRace(); }
            yield return null;
        }
    }

    private void ConfigureGermanBurstLoopTech()
    {
        config.enableTechTree = true;
        session.TeamVehicleBonusesEnabled = false;
        foreach (PlayerState participant in session.Players)
        {
            participant.techState = new TechTreeState(TeamId.DE);
            participant.techState.activeNodeIds.UnionWith(new[] { "de-l1-schwarzbier-fuel", "de-l3-grill-spezial" });
            participant.driverSkill.Initialize(participant.DriverProfile, 0, false);
        }
    }
}
