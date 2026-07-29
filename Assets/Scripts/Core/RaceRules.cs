using System.Collections.Generic;
using UnityEngine;

/// <summary>Result of resolving a requested gear change.</summary>
public readonly struct GearShiftResult
{
    public GearShiftResult(int targetGear, int heatCost)
    {
        TargetGear = targetGear;
        HeatCost = heatCost;
    }

    public int TargetGear { get; }
    public int HeatCost { get; }
}

/// <summary>Pure, deterministic rules shared by player and AI race flow.</summary>
public static class RaceRules
{
    public static GearShiftResult ResolveGearShift(
        int currentGear,
        int requestedGear,
        int minimumGear,
        int maximumGear,
        int twoGearShiftHeatCost)
    {
        int clampedRequest = Mathf.Clamp(requestedGear, minimumGear, maximumGear);
        int delta = clampedRequest - currentGear;
        if (Mathf.Abs(delta) <= 1)
        {
            return new GearShiftResult(clampedRequest, 0);
        }

        int targetGear = Mathf.Clamp(
            currentGear + System.Math.Sign(delta) * 2,
            minimumGear,
            maximumGear);
        return new GearShiftResult(targetGear, Mathf.Max(0, twoGearShiftHeatCost));
    }

    public static int GetCooldown(int gear, int gearOneCooldown, int gearTwoCooldown)
    {
        if (gear == 1)
        {
            return Mathf.Max(0, gearOneCooldown);
        }

        if (gear == 2)
        {
            return Mathf.Max(0, gearTwoCooldown);
        }

        return 0;
    }

    public static int SumCardValues(IReadOnlyList<CardData> cards)
    {
        int sum = 0;
        if (cards == null)
        {
            return sum;
        }

        for (int i = 0; i < cards.Count; i++)
        {
            sum += cards[i].value;
        }

        return sum;
    }

    public static int GetMissingSpeedCardCount(int gear, int selectedSpeedCardCount)
    {
        return Mathf.Max(0, gear - selectedSpeedCardCount);
    }
}
