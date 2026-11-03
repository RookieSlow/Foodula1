using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. Real GameLoop draw, formal card/discard
/// and ordinary failed-payment spin with the selected IT L2. Owned exact decks and
/// synthetic track reuse CreateRace; not full startup, storage, UI or mode acceptance.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator ItalyLasagnaActualDrawAddsOnlyOneHandSlotAndOrdinaryThirdSpinRetires()
    {
        CreateRace(TeamId.IT);
        try
        {
            config.enableTechTree = true;
            config.handSize = 7;
            var human = session.Human;
            human.techState = new TechTreeState(TeamId.IT, 100000);
            foreach (TechNodeDef node in session.TechDb.GetCommonInTier(TechTreeTier.L1))
                Assert.That(TechTreeRules.UnlockNode(human.techState, node.id, session.TechDb), Is.True, node.id);
            foreach (TechNodeDef node in session.TechDb.GetCommonInTier(TechTreeTier.L2).Take(3))
                Assert.That(TechTreeRules.UnlockNode(human.techState, node.id, session.TechDb), Is.True, node.id);
            Assert.That(TechTreeRules.UnlockNode(human.techState, "it-l2-lasagna", session.TechDb), Is.True);
            TechTreeRules.SelectActiveNodes(human.techState, new[] { "it-l2-lasagna" }, session.TechDb);
            Assert.That(human.techState.activeNodeIds, Is.EquivalentTo(new[] { "it-l2-lasagna" }),
                "Legal common prerequisites remain unlocked but inactive, so they add no capacity or hand effect.");
            Assert.That(human.deck.HandCount, Is.EqualTo(3), "Owned initial deck has not run the real A2 draw yet.");
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "IT Lasagna first real gear input");
            manager.OnGearButtonClicked(1);
            manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "IT Lasagna actual first A2 draw");
            Assert.That(human.deck.HandCount, Is.EqualTo(8), "Real A2 replenishes base7 plus the sole Lasagna hand slot.");
            Assert.That(session.EffectiveHeatPoolSize(human, 6), Is.EqualTo(6));
            Assert.That(HeatGaugeRules.Evaluate(human.deck).Capacity, Is.EqualTo(6));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            Assert.That(session.EffectiveSpinMax(human), Is.EqualTo(3));
            var speed = Get<System.Collections.Generic.List<CardUI>>(manager.cardHandUI, "cardUIs")
                .Find(card => card.cardData.IsSpeed);
            Assert.That(speed, Is.Not.Null);
            speed.SetSelectedWithoutNotify(true);
            manager.OnPlayCardsButtonClicked();
            Assert.That(human.playedSpeedCardsThisTurn.Count, Is.EqualTo(1));
            Assert.That(human.deck.HandCount, Is.EqualTo(7));
            manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "IT Lasagna first actual discard gate");
            Assert.That(human.spinCounter, Is.Zero);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "IT Lasagna next driven input after real cleanup");
            Assert.That(human.playedSpeedCardsThisTurn, Is.Empty);
            Assert.That(human.deck.DiscardPileCount, Is.EqualTo(1));
            Assert.That(human.deck.HandCount, Is.EqualTo(7));
            AssertActiveAndHeatConserved();
            manager.OnGearButtonClicked(1);
            manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "IT Lasagna actual next A2 draw");
            Assert.That(human.deck.HandCount, Is.EqualTo(8), "The slot remains an actual per-input hand limit after cleanup.");
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            human.spinCounter = 2;
            Assert.That(human.deck.DrawHeatFromPoolToHand(6), Is.EqualTo(6));
            Assert.That(human.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
            Assert.That(human.deck.heatPool.remaining, Is.Zero);
            manager.OnPlayCardsButtonClicked(); // No committed speed: the ordinary required-card fee cannot be paid.
            Assert.That(human.spinCounter, Is.EqualTo(3));
            Assert.That(human.isBlown, Is.True, "Lasagna no longer grants a fourth-spin allowance.");
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6), "Ordinary spin recovery returns only the six real permanent heats.");
            Assert.That(human.deck.CountPermanentHeatOutsideEngine(), Is.Zero);
            yield return WaitFor(() => Turn == 3 && session.Players[1].playedSpeedCardsThisTurn.Count > 0,
                "IT Lasagna third-spin retirement excludes next human input");
            Assert.That(manager.TutorialInputPhase, Is.EqualTo("none"));
            Assert.That(human.totalMovementThisTurn, Is.Zero);
            Assert.That(human.playedSpeedCardsThisTurn, Is.Empty);
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Real GameLoop IT Lasagna: A2 hand8, formal card/discard, next A2 hand8; capacity6 and ordinary missing-card third-spin retirement conserved. Owned synthetic setup, not full startup.");
        }
        finally { DestroyRace(); }
    }
}
