using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. Native GameLoop with actual API fee,
/// gear callbacks, CardUI formal selection and cleanup across preparation/two executions.
/// Synthetic track/headless adapters; no visual, authored-track or storage acceptance.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator SchumacherPaidStrategyCompletesPreparationAndTwoConsecutiveNativeInputs()
    {
        foreach (int level in new[] { 3, 5 })
        {
            CreateRace(TeamId.DE);
            session.TeamVehicleBonusesEnabled = false; // Isolate paid strategy from DE's ordinary straight reward.
            config.maxGear = 4; config.gearTwoCooldown = 0; config.twoGearShiftHeatCost = 1;
            PlayerState human = session.Human;
            human.driverId = "de_michael_schumacher";
            human.driverSkill.Initialize(human.DriverProfile, level, true);
            human.deck.DrawToHand(7);
            int uses = human.driverSkill.UsesRemaining;
            int bonus = level == 3 ? 2 : 3;
            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Schumacher preparation input");
            var discardedOriginal = human.deck.Hand.First(c => c.IsSpeed && !c.isTemp);
            manager.OnDriverSkillButtonClicked();
            Assert.That(manager.hudUI.IsReturnToMenuConfirmationVisible, Is.True, "Preparation always requires exact original card choice.");
            manager.hudUI.ConfirmReturnToMenu();
            Assert.That(human.deck.DiscardPile.Contains(discardedOriginal), Is.True);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
            yield return SchumacherConfirmFormalInput(1, 1, 6);
            manager.OnPlayCardsButtonClicked(); // Actual discard completion.
            for (int turn = 2; turn <= 3; turn++)
            {
                int expectedTurn = turn;
                yield return WaitFor(() => Turn == expectedTurn && manager.TutorialInputPhase == "gear", "Schumacher execution " + turn);
                manager.OnGearButtonClicked(3);
                manager.OnDriverSkillButtonClicked();
                if (manager.hudUI.IsReturnToMenuConfirmationVisible) manager.hudUI.ConfirmReturnToMenu();
                Assert.That(human.driverSkill.Strategy.ExecutionNumber, Is.EqualTo(turn - 1));
                Assert.That(human.driverSkill.Strategy.CommittedThisInput, Is.True);
                Assert.That(human.deck.Hand.Contains(human.heatPaidCardsThisTurn.Last()), Is.True);
                Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
                int expectedEngine = turn == 2 ? 4 : 3; // Two-stage shift costs ordinary one on turn 2.
                yield return SchumacherConfirmFormalInput(3, 1 + (turn - 1) * (3 + bonus), expectedEngine);
                manager.OnPlayCardsButtonClicked();
            }
            yield return WaitFor(() => Turn == 4 && manager.TutorialInputPhase == "gear", "Schumacher sequence complete");
            Assert.That(human.driverSkill.Strategy.IsExecutionAvailable, Is.False);
            Assert.That(human.driverSkill.Strategy.MovementBonusThisInput, Is.Zero);
            Assert.That(human.driverSkill.Strategy.ManualPath, Is.True);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native paid Schumacher sequence completed: level=" + level + ", four gear-input boundaries, three driven turns.");
            DestroyRace();
            yield return null;
        }
    }

    private IEnumerator SchumacherConfirmFormalInput(int gear, int expectedPosition, int expectedEngine)
    {
        manager.OnGearButtonClicked(gear); manager.OnConfirmGearClicked();
        yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Schumacher formal card input");
        List<CardUI> cards = Get<List<CardUI>>(manager.cardHandUI, "cardUIs");
        var chosen = cards.Where(c => c.cardData.type == CardType.Speed && !c.cardData.isTemp).Take(gear).ToArray();
        Assert.That(chosen.Length, Is.EqualTo(gear));
        foreach (var card in chosen) card.SetSelectedWithoutNotify(true);
        manager.OnPlayCardsButtonClicked();
        Assert.That(session.Human.playedSpeedCardsThisTurn.Count, Is.EqualTo(gear));
        manager.OnPlayCardsButtonClicked();
        yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Schumacher settled movement");
        Assert.That(session.Human.position, Is.EqualTo(expectedPosition));
        Assert.That(session.Human.deck.heatPool.remaining, Is.EqualTo(expectedEngine));
        Assert.That(session.Human.spinCounter, Is.Zero); AssertActiveAndHeatConserved();
    }
}
