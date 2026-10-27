using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>D03 actual ordinary-corner payment, next owner input tuning, and one-use settlement through GameLoop.</summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator SchumacherActualCornerPaymentPreparesNextInputAndReducesOnlyItsFirstPositiveCorner()
    {
        CreateRace(TeamId.DE);
        try
        {
            PlayerState human = session.Human;
            human.driverId = "de_michael_schumacher";
            human.driverSkill.Initialize(human.DriverProfile, 2, true);
            session.TeamVehicleBonusesEnabled = false;
            human.deck.InitializeExactOrder(Enumerable.Range(0, 40)
                .Select(_ => new CardData(CardType.Speed, 3)).ToArray(), new HeatPool(6));
            human.deck.DrawToHand(3);
            List<TrackNode> nodes = Get<List<TrackNode>>(manager.trackManager, "nodes");
            nodes[1] = new TrackNode(1, 1, "Tuning source apex", 1, false, true);
            nodes[4] = new TrackNode(4, 1, "Tuning next-input apex", 2, false, true);
            var limits = Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits");
            limits[1] = 1; limits[2] = 1;
            var paid = new List<CardData>();

            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Schumacher source input A1");
            Assert.That(human.driverSkill.Tuning.InputsStarted, Is.EqualTo(1));
            Assert.That(human.driverSkill.IsActive, Is.False);
            manager.OnGearButtonClicked(1); manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Schumacher source input A4");
            CardUI speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(card => card.cardData.IsSpeed);
            Assert.That(speed.cardData.value, Is.EqualTo(3));
            speed.SetSelectedWithoutNotify(true); manager.OnPlayCardsButtonClicked(); manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Schumacher source apex paid before discard");
            Assert.That(human.position, Is.EqualTo(3));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(4));
            Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(2));
            Assert.That(human.driverSkill.Tuning.HasPending, Is.True, "Two actual ordinary-corner receipts prepare tier-one tuning");
            paid.AddRange(human.heatPaidCardsThisTurn);
            manager.OnPlayCardsButtonClicked();

            yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "Schumacher next owner input A1");
            Assert.That(human.driverSkill.Tuning.InputsStarted, Is.EqualTo(2));
            Assert.That(human.driverSkill.Tuning.HasPending, Is.True);
            Assert.That(human.driverSkill.IsActive, Is.False, "This test never activates legacy PerfectLap");
            manager.OnGearButtonClicked(1); manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Schumacher discounted input A4");
            speed = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(card => card.cardData.IsSpeed);
            Assert.That(speed.cardData.value, Is.EqualTo(3));
            speed.SetSelectedWithoutNotify(true); manager.OnPlayCardsButtonClicked(); manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Schumacher next apex paid before discard");
            Assert.That(human.position, Is.EqualTo(6));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(3), "Ordinary corner cost two becomes one exactly once");
            Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(1));
            Assert.That(human.driverSkill.Tuning.HasPending, Is.False);
            Assert.That(human.driverSkill.Tuning.HasGrantedInLap(human.lap), Is.True);
            paid.AddRange(human.heatPaidCardsThisTurn);
            manager.OnPlayCardsButtonClicked();

            yield return WaitFor(() => Turn == 3 && manager.TutorialInputPhase == "gear", "Schumacher third owner input without a refreshed mark");
            Assert.That(human.driverSkill.Tuning.InputsStarted, Is.EqualTo(3));
            Assert.That(human.driverSkill.Tuning.HasPending, Is.False);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(3));
            Assert.That(paid.Count, Is.EqualTo(3));
            Assert.That(paid.Distinct().Count(), Is.EqualTo(3));
            foreach (CardData card in paid) Assert.That(human.deck.IsUniquePermanentHeatInHandOrDiscard(card), Is.True);
            AssertActiveAndHeatConserved();
        }
        finally { DestroyRace(); }
    }
}
