using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. Real native GameLoop settlement,
/// DE cruise source, ordinary reaction cooling, next A1 freeze and A2 post-draw return.
/// Synthetic track and headless UI; no authored-track, visual or persistence claim.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator VettelActualCruisePreparesNextOwnerPostDrawCoolingOfOneRemainingOldHandHeat()
    {
        CreateRace(TeamId.DE);
        try
        {
            session.TeamVehicleBonusesEnabled = true; // Native basic cruise is the source under test.
            config.maxGear = 3; config.gearTwoCooldown = 0;
            PlayerState human = session.Human;
            human.driverId = "de_sebastian_vettel"; human.gear = 3;
            human.driverSkill.Initialize(human.DriverProfile, 2, true);
            human.deck.DrawHeatFromPoolToHand(3);
            var oldHeat = human.deck.Hand.Where(c => c.IsHeat && !c.isTemp).ToArray();
            Assert.That(oldHeat.Length, Is.EqualTo(3)); Assert.That(human.deck.heatPool.remaining, Is.EqualTo(3));
            int uses = human.driverSkill.UsesRemaining;
            int sourceLap = human.lap; // PlayerState laps count completed laps and this owned race starts at zero.
            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Vettel source input A1");
            Assert.That(human.driverSkill.Feedback.InputsStarted, Is.EqualTo(1));
            Assert.That(human.driverSkill.Feedback.HasPending, Is.False);
            manager.OnGearButtonClicked(3); manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Vettel source cards");
            var selected = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").Where(c => c.cardData.IsSpeed).Take(3).ToArray();
            Assert.That(selected.Length, Is.EqualTo(3));
            foreach (CardUI card in selected) { Assert.That(card.cardData.value, Is.EqualTo(1)); card.SetSelectedWithoutNotify(true); }
            manager.OnPlayCardsButtonClicked(); Assert.That(human.playedSpeedCardsThisTurn.Count, Is.EqualTo(3));
            manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Vettel actual source settles after reaction");
            Assert.That(human.position, Is.EqualTo(7), "Three value-one cards, three native DE conversions and one chassis cell.");
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(4), "DE's ordinary one cooling happens before source settlement.");
            Assert.That(oldHeat.Count(human.deck.ContainsInHand), Is.EqualTo(2));
            TestContext.WriteLine(VettelFeedbackDiagnostic(human));
            Assert.That(human.driverSkill.Feedback.HasPending, Is.True, "Vettel feedback must exist at this checkpoint: " + VettelFeedbackDiagnostic(human));
            Assert.That(human.driverSkill.Feedback.PendingSource, Is.EqualTo(VettelNativeSource.BasicCruise));
            Assert.That(human.driverSkill.Feedback.HasTriggeredInLap(sourceLap), Is.True, "The real source lap is the completed-lap counter captured before movement: sourceLap=" + sourceLap + "; " + VettelFeedbackDiagnostic(human));
            Assert.That(human.driverSkill.IsActive, Is.False); Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses));
            manager.OnPlayCardsButtonClicked();

            yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "Vettel next owner input A1 freezes remaining old heat");
            Assert.That(human.driverSkill.Feedback.InputsStarted, Is.EqualTo(2));
            TestContext.WriteLine(VettelFeedbackDiagnostic(human));
            Assert.That(human.driverSkill.Feedback.HasPending, Is.True, "Vettel feedback must exist at this checkpoint: " + VettelFeedbackDiagnostic(human));
            var frozenRemaining = oldHeat.Where(human.deck.ContainsInHand).ToArray(); Assert.That(frozenRemaining.Length, Is.EqualTo(2));
            manager.OnGearButtonClicked(3); manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Vettel next A2 draw then feedback");
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(5));
            Assert.That(human.deck.HandCount, Is.EqualTo(session.EffectiveHandSize(human, config.handSize) - 1), "Ordinary draw completes before one old permanent hand card returns.");
            Assert.That(frozenRemaining.Count(human.deck.ContainsInHand), Is.EqualTo(1));
            Assert.That(oldHeat.Count(human.deck.ContainsInHand), Is.EqualTo(1), "Already normally cooled first original cannot return twice.");
            Assert.That(human.driverSkill.Feedback.LastRestored, Is.EqualTo(1)); Assert.That(human.driverSkill.Feedback.LastRetired, Is.Zero);
            Assert.That(human.driverSkill.Feedback.HasPending, Is.False);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses)); Assert.That(human.driverSkill.IsActive, Is.False);
            Assert.That(session.ResolveVettelFeedbackAfterDraw(human), Is.Zero);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(5));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native Vettel cruise source -> next owner A1 -> ordinary draw -> one old-hand permanent heat return completed.");
        }
        finally { DestroyRace(); }
    }

    private string VettelFeedbackDiagnostic(PlayerState participant)
    {
        var feedback = participant.driverSkill.Feedback;
        var observations = Get<IDictionary>(manager, "vettelMovementFeedback");
        return "turn=" + Turn + ";phase=" + manager.TutorialInputPhase + ";lap=" + participant.lap +
            ";position=" + participant.position + ";gear=" + participant.gear +
            ";input=" + feedback.InputsStarted + ";open=" + feedback.IsInputOpen +
            ";pending=" + feedback.HasPending + ";granted=" + feedback.GrantedInput +
            ";ready=" + feedback.ReadyInput + ";source=" + feedback.PendingSource +
            ";ledgerActual=" + participant.turnMovement.ActualCells + ";ledgerTotal=" + participant.turnMovement.TotalCells +
            ";movement=" + participant.totalMovementThisTurn + ";observations=" + observations.Count +
            ";engine=" + participant.deck.heatPool.remaining;
    }
}
