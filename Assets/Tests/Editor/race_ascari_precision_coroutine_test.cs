using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;

/// <summary>
/// Integration; Assets/Tests/Editor; BLOCKING. Actual native GameLoop, original
/// selected-card API commits, reduced base route, settled safety-gated driver tail
/// and ordinary new-apex/missing-card penalties. Owned exact decks and synthetic
/// track; not player target UI, screenshots, storage or complete-mode acceptance.
/// </summary>
public partial class RaceGameLoopCoroutineTests
{
    [UnityTest]
    public IEnumerator AscariSelectedOriginalActuallySlowsBaseAndRewardsOnlyAfterSafeApexWithOrdinaryTailFee()
    {
        CreateRace(TeamId.IT);
        try
        {
            PlayerState human = ConfigureAscariNativeLoop(3, 2, false);
            AddAscariNativeApex(3, 5, 1, "Ascari base safe apex");
            AddAscariNativeApex(6, 3, 2, "Ascari reward-only paid apex");
            yield return BeginAscariNativeCards(2);
            CardUI two = AscariNativeSpeed(2), four = AscariNativeSpeed(4);
            CardData target = two.cardData, other = four.cardData;
            two.OnCardClicked(); four.OnCardClicked();
            Assert.That(two.isSelected && four.isSelected, Is.True);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));

            Assert.That(manager.TryPrepareAscariPrecision(target, out string reason), Is.True, reason);
            Assert.That(manager.HasPendingAscariPrecision, Is.True);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));
            manager.CancelAscariPrecisionPlan();
            Assert.That(manager.HasPendingAscariPrecision, Is.False);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1), "Cancellation never spends the race opportunity.");
            Assert.That(manager.TryPrepareAscariPrecision(target, out reason), Is.True, reason);

            int hand = human.deck.HandCount;
            for (int preview = 0; preview < 2; preview++)
            {
                RaceTurnPreview forecast = manager.BuildTurnTelemetryPreview(human);
                Assert.That(forecast.Movement.TotalCells, Is.EqualTo(7), "Reduced base5 reaches a safe first apex, then adds tail2 once.");
                Assert.That(forecast.CornerHeat, Is.EqualTo(2), "The new apex6 is outside the base path and retains ordinary overspeed2.");
                Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));
                Assert.That(human.driverSkill.Precision.CommittedThisInput, Is.False);
                Assert.That(human.driverSkill.Precision.RewardConsumed, Is.False);
                Assert.That(human.deck.HandCount, Is.EqualTo(hand));
                Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
                Assert.That(target.value, Is.EqualTo(2)); Assert.That(other.value, Is.EqualTo(4));
            }

            manager.OnPlayCardsButtonClicked();
            Assert.That(human.playedSpeedCardsThisTurn, Is.EqualTo(new[] { target, other }));
            Assert.That(human.driverSkill.Precision.CommittedTarget, Is.SameAs(target));
            Assert.That(human.driverSkill.UsesRemaining, Is.Zero);
            Assert.That(human.deck.HandCount, Is.EqualTo(hand - 2));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6), "The slowdown commit has no fixed heat or RP fee.");
            manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Ascari actual reduced base and new-tail apex settlement");
            Assert.That(human.position, Is.EqualTo(7));
            Assert.That(human.cornerTotalThisTurn, Is.EqualTo(5));
            Assert.That(human.totalMovementThisTurn, Is.EqualTo(7));
            Assert.That(human.turnMovement.ActualCells, Is.EqualTo(7));
            Assert.That(human.driverSkill.Precision.RewardConsumed, Is.True);
            Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(2), "Old PrecisionCornering heat-1 must not survive the migration.");
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(4));
            Assert.That(human.spinCounter, Is.Zero);
            Assert.That(messages.Any(x => x.Contains("Ascari reward-only paid apex")), Is.True, string.Join("\n", messages));
            Assert.That(target.value, Is.EqualTo(2)); Assert.That(other.value, Is.EqualTo(4));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native Ascari Lv3: original2->effective1 with other4, base5 safely reaches apex3, then tail2 reaches7; new apex6 limit3 pays ordinary2. Prepare/cancel and repeated forecasts do not consume use, heat, card instances or shared values. API only, no player target UI/storage acceptance.");
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator AscariAndItalyTuneRejectSameOriginalInBothOrdersAndActuallyCommitDifferentCards()
    {
        CreateRace(TeamId.IT);
        try
        {
            PlayerState human = ConfigureAscariNativeLoop(5, 2, true);
            AddAscariNativeApex(3, 4, 1, "Ascari plus tech base safe apex");
            AddAscariNativeApex(6, 2, 2, "Ascari plus tech reward paid apex");
            int balance = human.techState.rpBalance;
            yield return BeginAscariNativeCards(2);
            CardUI two = AscariNativeSpeed(2), four = AscariNativeSpeed(4);
            CardData driverTarget = two.cardData, techTarget = four.cardData;
            two.OnCardClicked(); four.OnCardClicked();

            Assert.That(manager.TryPrepareItalyPrecisionTune(driverTarget, out string reason), Is.True, reason);
            Assert.That(manager.TryPrepareAscariPrecision(driverTarget, out _), Is.False, "A pending tech target cannot also receive the driver's -1.");
            Assert.That(manager.HasPendingItalyPrecisionTune, Is.True);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.False);
            manager.CancelItalyPrecisionTunePlan();

            Assert.That(manager.TryPrepareAscariPrecision(driverTarget, out reason), Is.True, reason);
            Assert.That(manager.TryPrepareItalyPrecisionTune(driverTarget, out _), Is.False, "A pending driver target also blocks the reverse same-original request.");
            Assert.That(manager.HasPendingAscariPrecision, Is.True);
            Assert.That(manager.TryPrepareItalyPrecisionTune(techTarget, out reason), Is.True, reason);
            Assert.That(manager.HasPendingAscariPrecision && manager.HasPendingItalyPrecisionTune, Is.True);
            int hand = human.deck.HandCount;
            RaceTurnPreview forecast = manager.BuildTurnTelemetryPreview(human);
            Assert.That(forecast.Movement.TotalCells, Is.EqualTo(7), "Independent targets make base4; Lv5 rewards only tail3 after the safe first apex.");
            Assert.That(forecast.CornerHeat, Is.EqualTo(2));
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.False);
            Assert.That(human.driverSkill.Precision.CommittedTarget, Is.Null);
            Assert.That(human.deck.HandCount, Is.EqualTo(hand));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));

            manager.OnPlayCardsButtonClicked();
            Assert.That(human.playedSpeedCardsThisTurn, Is.EqualTo(new[] { driverTarget, techTarget }));
            Assert.That(human.driverSkill.Precision.CommittedTarget, Is.SameAs(driverTarget));
            Assert.That(human.techState.precisionTune.CommittedTarget, Is.SameAs(techTarget));
            Assert.That(human.driverSkill.UsesRemaining, Is.Zero);
            Assert.That(human.techState.precisionTune.UsedThisRace, Is.True);
            Assert.That(human.techState.rpBalance, Is.EqualTo(balance));
            manager.OnPlayCardsButtonClicked();
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Ascari and different-card tech actual two reductions and tail");
            Assert.That(human.position, Is.EqualTo(7));
            Assert.That(human.cornerTotalThisTurn, Is.EqualTo(4));
            Assert.That(human.totalMovementThisTurn, Is.EqualTo(7));
            Assert.That(human.turnMovement.ActualCells, Is.EqualTo(7));
            Assert.That(human.driverSkill.Precision.RewardConsumed, Is.True);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(4));
            Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(2));
            Assert.That(human.spinCounter, Is.Zero);
            Assert.That(human.techState.rpBalance, Is.EqualTo(balance));
            Assert.That(driverTarget.value, Is.EqualTo(2)); Assert.That(techTarget.value, Is.EqualTo(4));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native Ascari Lv5 plus IT L3B: same-original pending target rejected in both orders without uses/fees, different originals commit together; base4 plus safety-gated tail3, new limit2 apex pays2 heat. Original values2/4 and RP unchanged; no player target UI/storage acceptance.");
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator AscariCannotBorrowUnsettledTailToReachFirstApexAndCommittedUseSurvivesOrdinarySpinCleanup()
    {
        CreateRace(TeamId.IT);
        try
        {
            PlayerState human = ConfigureAscariNativeLoop(3, 3, false);
            AddAscariNativeApex(3, 5, 1, "Ascari base-only reach gate apex");
            Assert.That(human.deck.DrawHeatFromPoolToHand(6), Is.EqualTo(6));
            yield return BeginAscariNativeCards(3);
            CardUI two = AscariNativeSpeed(2), four = AscariNativeSpeed(4);
            CardData target = two.cardData, other = four.cardData;
            two.OnCardClicked();
            Assert.That(manager.TryPrepareAscariPrecision(target, out _), Is.False,
                "Reduced base1 cannot reach apex3 even though the unearned tail2 could bridge the gap.");
            Assert.That(manager.HasPendingAscariPrecision, Is.False);
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));
            four.OnCardClicked();
            Assert.That(manager.TryPrepareAscariPrecision(target, out string reason), Is.True, reason);
            two.OnCardClicked(); two.OnCardClicked();
            Assert.That(manager.HasPendingAscariPrecision, Is.False, "Changing then restoring selection invalidates the old captured request.");
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));
            Assert.That(manager.TryPrepareAscariPrecision(target, out reason), Is.True, reason);
            manager.OnPlayCardsButtonClicked();
            Assert.That(human.playedSpeedCardsThisTurn, Is.EquivalentTo(new[] { target, other }));
            Assert.That(human.driverSkill.UsesRemaining, Is.Zero);
            Assert.That(human.driverSkill.Precision.CommittedThisInput, Is.True);
            Assert.That(human.deck.heatPool.remaining, Is.Zero);
            manager.OnPlayCardsButtonClicked(); // G3 requires3; ordinary missing1 and remaining0 spins before any movement.
            Assert.That(human.spinCounter, Is.EqualTo(1));
            Assert.That(human.isBlown, Is.False);
            Assert.That(human.position, Is.Zero);
            Assert.That(human.driverSkill.UsesRemaining, Is.Zero, "Ordinary mandatory-card failure cannot refund a successful driver commit.");
            Assert.That(human.driverSkill.Precision.RewardConsumed, Is.False);
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            Assert.That(target.value, Is.EqualTo(2)); Assert.That(other.value, Is.EqualTo(4));
            yield return WaitFor(() => Turn == 3 && manager.TutorialInputPhase == "gear", "Ascari ordinary spin skip cleanup and next owner driving input");
            Assert.That(manager.HasPendingAscariPrecision, Is.False);
            Assert.That(human.playedSpeedCardsThisTurn, Is.Empty);
            Assert.That(human.driverSkill.Precision.CommittedTarget, Is.Null);
            Assert.That(human.driverSkill.Precision.CommittedThisInput, Is.False);
            Assert.That(human.driverSkill.Precision.RewardConsumed, Is.False);
            Assert.That(human.driverSkill.Precision.InputsStarted, Is.EqualTo(2), "Skipped global round does not count as the owner's driving input.");
            Assert.That(human.driverSkill.UsesRemaining, Is.Zero);
            Assert.That(human.position, Is.Zero);
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native Ascari: reduced base cannot pre-borrow tail to reach apex; real selection changes expire pending intent freely. G3 two-card commit consumes one use, then ordinary missing1/remaining0 spins without refund or reward; skipped round and next owner input clear target while conserving permanent heat. API only.");
        }
        finally { DestroyRace(); }
    }

    [UnityTest]
    public IEnumerator AscariHotpotAttackOrdinaryBodyReachesSafeZeroSpeedApexAndActuallyEarnsOneTail()
    {
        CreateRace(TeamId.IT);
        try
        {
            PlayerState human = ConfigureAscariNativeLoop(3, 3, false);
            config.enableTrickCards = true;
            AddAscariNativeApex(1, 0, 1, "Ascari ATTACK ordinary-body safe apex");
            yield return BeginAscariNativeCards(3);
            Assert.That(session.IsGoMode(human), Is.True, "IT G3 legally satisfies the Hotpot Go precondition.");
            CardData hotpot = CardData.CreateTrick("cn-hotpot-base");
            human.deck.AddCardsToHand(new[] { hotpot });
            manager.OnTrickCardClicked(hotpot); // Production mode/card validation and trick discard, never a fabricated active flag.
            Assert.That(TrickCardRules.HasHotpotAttack(human.trickState), Is.True);
            Assert.That(human.deck.ContainsInHand(hotpot), Is.False);
            Assert.That(human.trickState.trickPlayedThisTurnId, Is.EqualTo("cn-hotpot-base"));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            CardUI speed = AscariNativeSpeed(2);
            CardData target = speed.cardData;
            speed.OnCardClicked();
            Assert.That(manager.TryPrepareAscariPrecision(target, out string reason), Is.True, reason);
            int hand = human.deck.HandCount;
            RaceTurnPreview forecast = manager.BuildTurnTelemetryPreview(human);
            Assert.That(forecast.Movement.TotalCells, Is.EqualTo(4), "Ordinary body2-1 reaches apex1; Hotpot adds1, then the earned driver tail adds2.");
            Assert.That(forecast.CornerHeat, Is.Zero, "ATTACK preserves corner speed0 at the actual limit0 apex.");
            Assert.That(forecast.MissingCards, Is.EqualTo(2), "Keeping legal G3 still requires3 ordinary speed cards.");
            Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));
            Assert.That(human.driverSkill.Precision.CommittedThisInput, Is.False);
            Assert.That(human.driverSkill.Precision.RewardConsumed, Is.False);
            Assert.That(TrickCardRules.HasHotpotAttack(human.trickState), Is.True);
            Assert.That(human.hotpotAttackAppliedThisTurn, Is.False);
            Assert.That(human.deck.HandCount, Is.EqualTo(hand));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6));
            Assert.That(target.value, Is.EqualTo(2));

            manager.OnPlayCardsButtonClicked();
            Assert.That(human.playedSpeedCardsThisTurn, Is.EqualTo(new[] { target }));
            Assert.That(human.driverSkill.Precision.CommittedTarget, Is.SameAs(target));
            Assert.That(human.driverSkill.UsesRemaining, Is.Zero);
            Assert.That(human.hotpotAttackAppliedThisTurn, Is.True);
            Assert.That(TrickCardRules.HasHotpotAttack(human.trickState), Is.False);
            Assert.That(human.hotpotAttackCardValueThisTurn, Is.EqualTo(2));
            Assert.That(RaceMovementRules.ComputeCornerSpeed(human, null, false), Is.Zero);
            Assert.That(AscariPrecisionRules.GetCommittedBaseSpeed(human), Is.EqualTo(1));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(6), "Neither target commit nor Hotpot creates a fixed heat fee.");
            manager.OnPlayCardsButtonClicked(); // G3 missing2 pays its ordinary fee; it is not an apex or driver charge.
            Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(2));
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(4));
            yield return WaitFor(() => manager.TutorialInputPhase == "discard", "Ascari Hotpot zero-corner-speed ordinary-body actual reward");
            Assert.That(human.position, Is.EqualTo(4));
            Assert.That(human.cornerTotalThisTurn, Is.Zero);
            Assert.That(human.totalMovementThisTurn, Is.EqualTo(4));
            Assert.That(human.turnMovement.ActualCells, Is.EqualTo(4));
            Assert.That(human.driverSkill.Precision.RewardConsumed, Is.True);
            Assert.That(human.heatPaidCardsThisTurn.Count, Is.EqualTo(2), "Only the already-paid mandatory-card fee remains; safe apex adds zero.");
            Assert.That(human.deck.heatPool.remaining, Is.EqualTo(4));
            Assert.That(human.spinCounter, Is.Zero);
            Assert.That(target.value, Is.EqualTo(2));
            AssertActiveAndHeatConserved();
            TestContext.WriteLine("Native Ascari plus legally played Go/G3 Hotpot: ordinary ATTACK2-1 body reaches apex1 safely with corner0, Hotpot1 makes base2 and earned driver tail2 makes actual4. Single speed leaves G3 missing2: ordinary2 heat is paid, corner fee0 and no driver fixed fee. Forecast does not alter real use/heat/ATTACK/original instance. Owned supplied cross-team trick fixture, no normal deck generation/player target UI/storage acceptance.");
        }
        finally { DestroyRace(); }
    }

    private PlayerState ConfigureAscariNativeLoop(int level, int gear, bool technology)
    {
        config.maxGear = gear; config.gearTwoCooldown = 0;
        config.enableTechTree = technology;
        session.TeamVehicleBonusesEnabled = false;
        PlayerState human = session.Human;
        human.driverId = "it_alberto_ascari";
        human.driverSkill.Initialize(human.DriverProfile, level, true);
        human.gear = gear;
        human.deck.InitializeExactOrder(Enumerable.Range(0, 40)
            .Select(i => new CardData(CardType.Speed, i % 2 == 0 ? 2 : 4)).ToArray(), new HeatPool(6));
        human.deck.DrawToHand(3);
        Assert.That(human.driverId, Is.EqualTo("it_alberto_ascari"));
        Assert.That(human.driverSkill.Skill, Is.EqualTo(DriverActiveSkillId.PrecisionCornering));
        Assert.That(human.driverSkill.UsesRemaining, Is.EqualTo(1));
        Assert.That(human.driverXp, Is.Zero, "Fixture chooses runtime level without modifying real XP or progression keys.");
        if (technology)
        {
            human.techState = new TechTreeState(TeamId.IT, 50000);
            Assert.That(session.TechDb.Get("it-l3-cavallino-rampante").rpCost, Is.EqualTo(12000));
            Assert.That(TechTreeRules.UnlockNode(human.techState, "it-l3-cavallino-rampante", session.TechDb), Is.True);
            TechTreeRules.SelectActiveNodes(human.techState, new[] { "it-l3-cavallino-rampante" }, session.TechDb);
            Assert.That(human.techState.rpBalance, Is.EqualTo(38000));
        }
        return human;
    }

    private void AddAscariNativeApex(int node, int limit, int id, string name)
    {
        Get<List<TrackNode>>(manager.trackManager, "nodes")[node] = new TrackNode(node, limit, name, id, false, true);
        Get<Dictionary<int, int>>(manager.trackManager, "cornerSpeedLimits")[id] = limit;
        Get<Dictionary<int, string>>(manager.trackManager, "cornerNames")[id] = name;
    }

    private IEnumerator BeginAscariNativeCards(int gear)
    {
        manager.StartCoroutine((IEnumerator)typeof(MVPGameManager).GetMethod("GameLoop", PrivateInstance).Invoke(manager, null));
        yield return WaitFor(() => Turn == 1 && manager.TutorialInputPhase == "gear", "Ascari first real owner gear input");
        manager.OnGearButtonClicked(gear); manager.OnConfirmGearClicked();
        yield return WaitFor(() => manager.TutorialInputPhase == "cards", "Ascari actual post-draw selected-card input");
    }

    private CardUI AscariNativeSpeed(int value)
        => Get<List<CardUI>>(manager.cardHandUI, "cardUIs").First(card => card.cardData.IsSpeed && card.cardData.value == value);
}
