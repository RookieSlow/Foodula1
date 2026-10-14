using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class TeamGearPresentationTests
{
    [Test]
    public void GearButtonsRefreshHandTrialAndHeatBeforeGearPayment()
    {
        Build(TeamId.UK, 1);
        manager.Session.TeamVehicleBonusesEnabled = false;
        player.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 4), new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 1) });
        player.deck.DrawHeatFromPool(2);
        hud.RefreshTeamGearPresentation();
        Assert.That(View.Telemetry.LivePreview.Movement.TotalCells, Is.EqualTo(4));
        Assert.That(View.Telemetry.ForecastHeatState.TotalHeat, Is.Zero);
        hud.gear3Button.onClick.Invoke();
        Assert.That(View.Telemetry.MovementText, Does.Contain("手牌试算 7"));
        Assert.That(View.Telemetry.ForecastHeatState.TotalHeat, Is.EqualTo(3));
        Assert.That(View.Telemetry.HeatText, Does.Contain("2→预计 3/6"));
        Assert.That(player.gear, Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
        Assert.That(player.turnMovement.Entries, Is.Empty);
        hud.gear2Button.onClick.Invoke();
        Assert.That(View.Telemetry.LivePreview.Movement.TotalCells, Is.EqualTo(6));
        Assert.That(View.Telemetry.ForecastHeatState.TotalHeat, Is.EqualTo(1));
    }

    [Test]
    public void RealCardClickCallbacksRefreshMetersOnSelectDeselectAndClearWithoutCommit()
    {
        Build(TeamId.UK, 2); manager.Session.TeamVehicleBonusesEnabled = false;
        phase.BeginCardSelection(); input.BeginCardSelection();
        var four = new CardData(CardType.Speed, 4); var two = new CardData(CardType.Speed, 2);
        player.deck.AddCardsToHand(new[] { four, two }); player.deck.DrawHeatFromPool(2);
        var cards = BindTelemetryHand(four, two);
        cards[0].OnCardClicked();
        Assert.That(View.Telemetry.MovementText, Does.Contain("预计 4"));
        cards[1].OnCardClicked();
        Assert.That(View.Telemetry.MovementText, Does.Contain("预计 6"));
        Assert.That(View.Telemetry.HeatText, Does.Contain("2→预计 1/6"));
        cards[0].OnCardClicked();
        Assert.That(View.Telemetry.MovementText, Does.Contain("预计 2"));
        manager.cardHandUI.ClearPendingPlaySelection();
        Assert.That(View.Telemetry.MovementText, Does.Contain("预计 0"));
        Assert.That(player.playedSpeedCardsThisTurn, Is.Empty);
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(4));
    }

    [Test]
    public void CommittingSelectedCardsKeepsTheForecastAndAuthoritativePlanRemovesPreview()
    {
        Build(TeamId.UK, 2); manager.Session.TeamVehicleBonusesEnabled = false;
        phase.BeginCardSelection(); input.BeginCardSelection();
        var four = new CardData(CardType.Speed, 4); var two = new CardData(CardType.Speed, 2);
        player.deck.AddCardsToHand(new[] { four, two });
        var cards = BindTelemetryHand(four, two);
        cards[0].OnCardClicked(); cards[1].OnCardClicked();
        Assert.That(View.Telemetry.LivePreview.Movement.TotalCells, Is.EqualTo(6));
        manager.OnPlayCardsButtonClicked();
        Assert.That(player.playedSpeedCardsThisTurn.Count, Is.EqualTo(2));
        Assert.That(View.Telemetry.LivePreview.Movement.TotalCells, Is.EqualTo(6));
        player.turnMovement.BeginBasePlan(); player.turnMovement.AddPlan("结算速度牌", 6, TurnMovementKind.Card);
        hud.RefreshTurnTelemetry(player);
        Assert.That(View.Telemetry.LivePreview, Is.Null);
        Assert.That(View.Telemetry.MovementText, Does.Contain("预计 6"));
        Assert.That(View.Telemetry.HeatText, Does.Not.Contain("预计"));
    }

    [Test]
    public void DragTrialRefreshesBeforeReleaseAndModalDoesNotChangeInputs()
    {
        using (var scope = CreateGearPointerScope())
        {
            BuildRenderedUs(scope, 2);
            manager.Session.TeamVehicleBonusesEnabled = false;
            player.deck.AddCardsToHand(new[] { new CardData(CardType.Speed, 4), new CardData(CardType.Speed, 2), new CardData(CardType.Speed, 1) });
            hud.RefreshTeamGearPresentation();
            BeginUsDrag(scope, 2, 3);
            Assert.That(input.PendingGear, Is.EqualTo(2));
            Assert.That(View.Telemetry.LivePreview.Gear, Is.EqualTo(3));
            Assert.That(View.Telemetry.LivePreview.Movement.TotalCells, Is.EqualTo(7));
            Assert.That(player.gear, Is.EqualTo(2));
        }
    }

    private CardUI[] BindTelemetryHand(params CardData[] data)
    {
        var host = new GameObject("Telemetry hand", typeof(RectTransform));
        host.transform.SetParent(root.transform, false);
        var hand = host.AddComponent<CardHandUI>();
        manager.cardHandUI = hand; hand.handContainer = host.transform;
        typeof(CardHandUI).GetField("gameManager", Private).SetValue(hand, manager);
        var list = (List<CardUI>)typeof(CardHandUI).GetField("cardUIs", Private).GetValue(hand);
        MethodInfo click = typeof(CardHandUI).GetMethod("OnCardClicked", Private);
        var callback = (Action<CardUI>)Delegate.CreateDelegate(typeof(Action<CardUI>), hand, click);
        var result = new List<CardUI>();
        foreach (CardData card in data)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(host.transform, false);
            var ui = go.AddComponent<CardUI>();
            ui.backgroundImage = go.GetComponent<Image>();
            ui.SetupCard(card, callback);
            list.Add(ui); result.Add(ui);
        }
        return result.ToArray();
    }
}
