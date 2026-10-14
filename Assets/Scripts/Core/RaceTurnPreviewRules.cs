using System;
using System.Collections.Generic;

/// <summary>Isolated input forecast. No real card, skill, payment, RNG or movement is committed.</summary>
public sealed class RaceTurnPreview
{
    public TurnMovementBreakdown Movement { get; internal set; }
    public HeatGaugeState ProjectedHeat { get; internal set; }
    public bool GearTrial { get; internal set; }
    public bool CanPay { get; internal set; } = true;
    public int Gear { get; internal set; }
    public TeamId Team { get; internal set; }
    public int CardCount { get; internal set; }
    public int RequiredCards { get; internal set; }
    public int CardSpeedTotal { get; internal set; }
    public int HeatPaid { get; internal set; }
    public int HeatCooled { get; internal set; }
    public int MissingCards { get; internal set; }
    public int CornerHeat { get; internal set; }
    public string Note { get; internal set; }
}

public static class RaceTurnPreviewRules
{
    public static RaceTurnPreview Evaluate(PlayerState player, RaceSession session, GameConfigSO config,
        IReadOnlyList<CardData> selected, bool gearTrial, int requestedGear,
        Func<int, int, bool> crossesCorner = null, Func<PlayerState, bool, int, int> nigiriBonus = null,
        int totalCells = 0, Func<int, int, IEnumerable<int>> crossedCorners = null,
        Func<int, int> cornerBaseLimit = null)
    {
        if (player == null || player.deck == null || config == null) return null;
        var copy = new PlayerState(player.name, player.isAI, player.position, player.gear) {
            teamId = player.teamId, usesChinaGearSystem = player.usesChinaGearSystem,
            chinaConsecutiveGearCount = player.chinaConsecutiveGearCount,
            driverId = player.driverId, driverXp = player.driverXp,
            driverSkill = player.driverSkill?.CreatePresentationCopy(), lap = player.lap,
            deck = player.deck.CreatePresentationCopy(),
            trickState = player.trickState?.CreatePresentationCopy() ?? new TrickCardState(),
            techState = player.techState?.CreatePresentationCopy(), extraCardSlotsThisTurn = player.extraCardSlotsThisTurn,
            trickMoveBonusThisTurn = player.trickMoveBonusThisTurn,
            hotpotAttackAppliedThisTurn = player.hotpotAttackAppliedThisTurn,
            hotpotAttackCardValueThisTurn = player.hotpotAttackCardValueThisTurn,
            italyCornerExitBoostReady = player.italyCornerExitBoostReady
        };
        copy.playedSpeedCardsThisTurn.AddRange(player.playedSpeedCardsThisTurn);
        var result = new RaceTurnPreview { GearTrial = gearTrial, Team = player.teamId, Movement = copy.turnMovement };
        Func<int, HeatPaymentDestination, bool> pay = (amount, destination) => {
            int cost = HeatPaymentCostRules.Resolve(copy, amount).Amount;
            RaceHeatPaymentResult payment = RaceHeatPaymentExecution.Pay(copy, session, cost, destination);
            result.HeatPaid += payment.Drawn;
            if (!payment.Sufficient) { result.CanPay = false; result.Note = "引擎热量不足，存在失控风险"; }
            return payment.Sufficient;
        };
        if (gearTrial)
        {
            var shift = TeamGearRules.Resolve(copy.teamId, copy.gear, copy.chinaConsecutiveGearCount,
                requestedGear, config.minGear, config.maxGear, config.twoGearShiftHeatCost,
                config.gearOneCooldown, config.gearTwoCooldown);
            bool paidShift = pay(DriverSkillRules.ApplyGearHeatCost(copy.driverSkill, shift.HeatCost, false), HeatPaymentDestination.Hand);
            copy.gear = shift.TargetGear;
            copy.chinaConsecutiveGearCount = shift.IsChina ? shift.ConsecutiveCount : 0;
            if (paidShift)
                pay(DriverSkillRules.ApplyGearHeatCost(copy.driverSkill, shift.AdditionalHeat, true), HeatPaymentDestination.Hand);
        }
        result.Gear = copy.gear;
        result.RequiredCards = CardPlayRules.GetSpeedCardRequirement(copy).TotalCardCount;
        var incoming = new List<CardData>();
        if (gearTrial)
        {
            bool barbecue = config.enableTechTree && CardPlayRules.CanUseHeatAsSpeed(copy, session, totalCells);
            incoming = CardPlayRules.GetSpeedCandidates(copy.deck, barbecue);
            int remaining = Math.Max(0, result.RequiredCards - copy.playedSpeedCardsThisTurn.Count);
            if (incoming.Count > remaining) incoming.RemoveRange(remaining, incoming.Count - remaining);
        }
        else if (selected != null)
        {
            foreach (CardData card in selected)
                if (card != null && copy.deck.ContainsInHand(card) && !incoming.Contains(card))
                    incoming.Add(card);
        }
        CardData trick = incoming.Find(card => card.IsTrick);
        if (trick != null && session?.TrickDb != null)
        {
            TrickPlayResult effect = session.PlayTrick(copy, trick);
            result.Note = effect.message;
            if (effect.success && pay(effect.heatToPay, HeatPaymentDestination.Hand))
            {
                if (effect.heatToCool > 0)
                    result.HeatCooled += copy.deck.CoolHeat(effect.heatToCool);
                copy.trickMoveBonusThisTurn += effect.extraMovement;
                if (effect.requiresSpeedDiscard)
                {
                    List<CardData> discard = copy.deck.GetBottomNSpeedCards(1);
                    copy.deck.RemoveFromHand(discard);
                    copy.deck.DiscardSpeedCards(discard);
                }
                if (TrickCardRules.HasTempHeat(copy.trickState))
                {
                    copy.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
                    TrickCardRules.ConsumeTempHeat(copy.trickState);
                }
                if (session.TrickDb.Get(trick.trickId).effectType == TrickEffectType.KantoOden)
                    copy.kantoOdenSkipThisTurn = true;
                // Drawing can shuffle and reveal unknown cards; never simulate it for display.
                if (effect.cardsToDraw > 0) result.Note += "（抽牌以确认后结果为准）";
            }
        }
        else
        {
            int room = Math.Max(0, result.RequiredCards - copy.playedSpeedCardsThisTurn.Count);
            bool barbecue = config.enableTechTree && session != null &&
                CardPlayRules.CanUseHeatAsSpeed(copy, session, totalCells);
            foreach (CardData card in incoming)
            {
                if (room <= 0) break;
                if (!card.IsSpeed && !(barbecue && card.IsHeat)) continue;
                if (copy.playedSpeedCardsThisTurn.Contains(card)) continue;
                copy.deck.RemoveFromHand(new List<CardData> { card });
                copy.playedSpeedCardsThisTurn.Add(card);
                if (TrickCardRules.ConsumeHotpotAttack(copy.trickState))
                {
                    copy.hotpotAttackAppliedThisTurn = true;
                    copy.hotpotAttackCardValueThisTurn = CardPlayRules.GetCommittedSpeedCardValue(card);
                }
                room--;
            }
        }
        result.CardCount = copy.playedSpeedCardsThisTurn.Count;
        result.MissingCards = copy.kantoOdenSkipThisTurn ? 0 : RaceRules.GetMissingSpeedCardCount(
            CardPlayRules.GetSpeedCardRequirement(copy).RequiredCardCount, result.CardCount);
        if (result.MissingCards > 0) pay(result.MissingCards, HeatPaymentDestination.Hand);
        result.CardSpeedTotal = CardPlayRules.SumCommittedSpeedCardValues(copy.playedSpeedCardsThisTurn);
        copy.turnMovement.BeginBasePlan();
        if (!copy.kantoOdenSkipThisTurn && (result.CardCount > 0 || copy.trickMoveBonusThisTurn != 0))
        {
            copy.cornerTotalThisTurn = RaceMovementRules.ComputeCornerSpeed(copy, null, false);
            int rawEnd = copy.position + copy.cornerTotalThisTurn;
            bool corner = crossesCorner != null && crossesCorner(copy.position, rawEnd);
            RaceMovementRules.RecordCardAndDriverMovementPlan(copy, corner);
            int bonus = RaceMovementRules.ComputeCardAndDriverMovementBonus(copy, corner);
            if (session != null)
            {
                bonus += session.ComputeMovementBonusWithBreakdown(copy, corner, copy.turnMovement);
                int italy = session.ConsumeItalyCornerExitBonus(copy);
                bonus += italy;
                copy.turnMovement.AddPlan("意大利：弯后出弯加速", italy, TurnMovementKind.Other);
                if (copy.techState != null && TechTreeRules.ShouldTriggerWurstplatte(copy.techState, session.TechDb, corner))
                {
                    bonus++;
                    copy.turnMovement.AddPlan("科技：香肠拼盘", 1, TurnMovementKind.Technology);
                }
                int nigiri = nigiriBonus != null ? nigiriBonus(copy, corner, rawEnd) : 0;
                bonus += nigiri;
                copy.turnMovement.AddPlan("科技：寿司精确过弯", nigiri, TurnMovementKind.Technology);
                if (session.CanTriggerSchwarzbierFuel(copy, config.enableTechTree) &&
                    pay(1, HeatPaymentDestination.Discard))
                {
                    bonus += 2;
                    copy.turnMovement.AddPlan("科技：黑啤酒燃料", 2, TurnMovementKind.Technology);
                }
                int landmark = totalCells > 0
                    ? session.ComputeDriveThruMovementBonus(copy, config.enableTechTree, rawEnd, totalCells) : 0;
                bonus += landmark;
                copy.turnMovement.AddPlan("科技：得来速地标", landmark, TurnMovementKind.Technology);
            }
            copy.totalMovementThisTurn = Math.Max(0, copy.cornerTotalThisTurn + bonus);
            copy.turnMovement.ReconcileBaseTotal(copy.totalMovementThisTurn);
        }
        int reactionHeat = DriverSkillRules.GetReactionHeatCost(copy.driverSkill);
        if (session != null && totalCells > 0 && !copy.kantoOdenSkipThisTurn)
        {
            copy.position = (copy.position + copy.turnMovement.TotalCells) % totalCells;
            session.SyncRegionalHeatCapacity(copy, totalCells, config.enableTechTree);
        }
        if (!copy.kantoOdenSkipThisTurn)
        {
            pay(reactionHeat, HeatPaymentDestination.Hand);
            int cooling = session != null
                ? session.ComputeReactionCooldown(copy, config.gearOneCooldown, config.gearTwoCooldown)
                : TeamGearRules.GetCooldown(copy.teamId, copy.gear, copy.chinaConsecutiveGearCount,
                    config.gearOneCooldown, config.gearTwoCooldown);
            if (cooling > 0) result.HeatCooled += copy.deck.CoolHeat(cooling);
        }
        if (!copy.kantoOdenSkipThisTurn && result.CanPay && session != null &&
            copy.cornerTotalThisTurn > 0 && crossedCorners != null && cornerBaseLimit != null)
        {
            int beforeCorners = result.HeatPaid;
            RaceCornerResolver.Resolve(copy, 0,
                crossedCorners(player.position, player.position + copy.cornerTotalThisTurn),
                id => session.EffectiveCornerLimit(copy, cornerBaseLimit(id)),
                id => "弯道 " + id, overspeed => session.ResolveOverspeedHeatCost(copy, overspeed),
                (amount, reason) => pay(amount, HeatPaymentDestination.Hand), _ => { });
            result.CornerHeat = result.HeatPaid - beforeCorners;
        }
        result.ProjectedHeat = result.CanPay ? HeatGaugeRules.Evaluate(copy.deck, copy.playedSpeedCardsThisTurn)
            : HeatGaugeRules.Evaluate(player.deck, player.playedSpeedCardsThisTurn);
        return result;
    }
}
