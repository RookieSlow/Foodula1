using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. Native GameLoop, actual selected-card
/// manager API/commit, movement/apex and ordinary partial-payment spin/cleanup.
/// Owned exact decks and synthetic track; not player tune UI, storage or complete modes.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator ItalyPrecisionTuneActualCommitReducesMovementAndApexSpeedOnceWithoutChangingCard()
    {
        CreateRace(TeamId.IT);
        try
        {
            ConfigureItalyPrecisionTuneLoop();
            PlayerState human = session.Human;
            Get<List<TrackNode>>(manager.trackManager, "nodes")[2] =
                new TrackNode(2, 2, "Precision tune actual apex", 1, false, true);
            Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits")[1] = 2;
            Get<Dictionary<int, string>>(manager.trackManager, "cornerNames")[1] = "Precision tune actual apex";
            int balanceAfterUnlock = human.techState.rpBalance;

            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Italy precision first actual gear input");
            manager.OnGearButtonClicked(1);
            manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Italy precision actual post-draw card input");
            CardUI selected = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(card => card.cardData.IsSpeed);
            CardData original = selected.cardData;
            Assert.That(original.value, Is.EqualTo(3));
            selected.OnCardClicked(); // Actual CardUI -> CardHandUI selection callback, without physical input.
            Assert.That(selected.isSelected, Is.True);

            Assert.That(manager.TryPrepareItalyPrecisionTune(original, out string reason), Is.True, reason);
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.False, "Preparing a target does not consume the race use.");
            selected.OnCardClicked();
            Assert.That(selected.isSelected, Is.False);
            selected.OnCardClicked();
            Assert.That(selected.isSelected, Is.True);
            Assert.That(manager.BuildTurnTelemetryPreview(human).Movement.TotalCells, Is.EqualTo(3),
                "Leaving and restoring the same selection invalidates the old request version through the real callback.");
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.False);
            Assert.That(manager.TryPrepareItalyPrecisionTune(original, out reason), Is.True, reason);
            manager.CancelItalyPrecisionTunePlan();
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.False, "Cancellation is free.");
            Assert.That(manager.TryPrepareItalyPrecisionTune(original, out reason), Is.True, reason);
            int handCount = human.deck.HandCount;
            RaceTurnPreview preview = manager.BuildTurnTelemetryPreview(human);
            Assert.That(preview.Movement.TotalCells, Is.EqualTo(2), "Preview includes the pending independent -1.");
            Assert.That(preview.CornerHeat, Is.Zero, "The actual apex limit is2; tuned card contribution is2.");
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.False, "Preview never consumes the real use.");
            Assert.That(human.deck.HandCount, Is.EqualTo(handCount));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            Assert.That(original.value, Is.EqualTo(3));

            manager.OnPlayCardsButtonClicked(); // Actual manager final selected-card commit.
            Assert.That(human.playedSpeedCardsThisTurn, Is.EqualTo(new[] { original }));
            Assert.That(human.deck.ContainsInHand(original), Is.False);
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.True);
            Assert.That(human.techState.precisionTune.CommittedTarget, Is.SameAs(original));
            Assert.That(original.value, Is.EqualTo(3), "Shared CardData is never rewritten to2.");
            Assert.That(human.techState.rpBalance, Is.EqualTo(balanceAfterUnlock), "L3B has no per-race RP debit.");
            manager.OnPlayCardsButtonClicked(); // Empty refreshed selection finishes formal card input.
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Italy precision real movement and apex settlement");
            Assert.That(human.position, Is.EqualTo(2));
            Assert.That(human.cornerTotalThisTurn, Is.EqualTo(2));
            Assert.That(human.totalMovementThisTurn, Is.EqualTo(2));
            Assert.That(human.turnMovement.ActualCells, Is.EqualTo(2));
            Assert.That(human.heatPaidCardsThisTurn, Is.Empty, "Tuned2 safely reaches limit2, with no heat fee.");
            Assert.That(human.spinCounter, Is.Zero);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => Turn == 2 && manager.TutorialInputPhase == "gear", "Italy precision next actual owner input after cleanup");
            Assert.That(human.playedSpeedCardsThisTurn, Is.Empty);
            Assert.That(human.deck.DiscardPileCount, Is.EqualTo(1));
            Assert.That(human.techState.precisionTune.CommittedTarget, Is.Null, "Cleanup releases only the turn target.");
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.True, "Cleanup cannot refund the once-per-race use.");
            manager.OnGearButtonClicked(1);
            manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Italy precision second actual card input");
            CardUI next = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(card => card.cardData.IsSpeed);
            next.OnCardClicked();
            Assert.That(manager.TryPrepareItalyPrecisionTune(next.cardData, out _), Is.False);
            Assert.That(human.techState.rpBalance, Is.EqualTo(balanceAfterUnlock));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native GameLoop L3B: legal12000 unlock, prepare/cancel/isolated forecast, actual selected-card commit3->2, safe apex2, actual movement2 and cleanup/next-input once-use rejection. API-controlled, no tune UI/storage acceptance.");
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator ItalyPrecisionTuneCommittedUseSurvivesOrdinaryPartialMissingCardPaymentAndSpinCleanup()
    {
        CreateRace(TeamId.IT);
        try
        {
            ConfigureItalyPrecisionTuneLoop();
            PlayerState human = session.Human;
            config.maxGear = 3;
            config.gearTwoCooldown = 0;
            human.gear = 3;
            Assert.That(human.deck.DrawHeatFromPoolToHand(5), Is.EqualTo(5));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(1));
            int balanceAfterUnlock = human.techState.rpBalance;
            manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
            yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Italy precision partial-payment gear input");
            manager.OnGearButtonClicked(3);
            manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Italy precision partial-payment card input");
            CardUI selected = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(card => card.cardData.IsSpeed);
            CardData original = selected.cardData;
            selected.OnCardClicked();
            Assert.That(manager.TryPrepareItalyPrecisionTune(original, out string reason), Is.True, reason);
            manager.OnPlayCardsButtonClicked();
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.True);
            Assert.That(human.playedSpeedCardsThisTurn, Is.EqualTo(new[] { original }));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(1), "The tune itself costs no heat.");
            manager.OnPlayCardsButtonClicked(); // G3 missing2: the ordinary path pays its real remaining1 before spinning.
            Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(1), "Ordinary insufficient payment retains its one real receipt.");
            Assert.That(human.spinCounter, Is.EqualTo(1));
            Assert.That(human.isBlown, Is.False);
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.True, "Later ordinary failure does not refund a successful tune commit.");
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6), "Spin recovery conserves the six real permanent heat instances.");
            Assert.That(human.deck.CountPermanentHeatOutsideEngine(), Is.Zero);
            Assert.That(original.value, Is.EqualTo(3));
            Assert.That(human.techState.rpBalance, Is.EqualTo(balanceAfterUnlock));
            yield return WaitFor(() => Turn == 3 && manager.TutorialInputPhase == "gear", "Italy precision post-spin skipped turn and next owner input");
            Assert.That(human.playedSpeedCardsThisTurn, Is.Empty);
            Assert.That(human.techState.precisionTune.CommittedTarget, Is.Null);
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.True);
            Assert.That(human.position, Is.Zero, "Failed ordinary mandatory-card payment prevents the planned movement.");
            manager.OnGearButtonClicked(3);
            manager.OnConfirmGearClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Italy precision new post-spin card input");
            CardUI next = Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(card => card.cardData.IsSpeed);
            next.OnCardClicked();
            Assert.That(manager.TryPrepareItalyPrecisionTune(next.cardData, out _), Is.False);
            Assert.That(human.techState.rpBalance, Is.EqualTo(balanceAfterUnlock));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native GameLoop L3B: actual successful tune commit precedes ordinary G3 missing2/remaining1 partial payment, spin and real skipped-input cleanup; use/RP never refunded. API-controlled, no player tune UI or real profile writes.");
        }
        finally { DestroyRace(); }
    }

    private void ConfigureItalyPrecisionTuneLoop()
    {
        config.enableTechTree = true;
        session.TeamVehicleBonusesEnabled = false;
        PlayerState human = session.Human;
        human.driverSkill.Initialize(human.DriverProfile, 0, false);
        human.techState = new TechTreeState(TeamId.IT, 50000);
        Assert.That(session.TechDb.Get("it-l3-cavallino-rampante").rpCost, Is.EqualTo(12000));
        Assert.That(TechTreeRules.UnlockNode(human.techState, "it-l3-cavallino-rampante", session.TechDb), Is.True);
        TechTreeRules.SelectActiveNodes(human.techState, new[] { "it-l3-cavallino-rampante" }, session.TechDb);
        Assert.That(human.techState.rpBalance, Is.EqualTo(38000));
        Assert.That(human.techState.activeNodeIds, Is.EquivalentTo(new[] { "it-l3-cavallino-rampante" }));
        human.deck.InitializeExactOrder(Enumerable.Range(0, 40).Select(_ => new CardData(CardType.Speed, 3)).ToArray(), new HeatPool(6));
        human.deck.DrawToHand(3);
    }
}
