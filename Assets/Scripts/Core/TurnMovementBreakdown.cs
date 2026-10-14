using System;
using System.Collections.Generic;

public enum TurnMovementKind { Card, Driver, Technology, Other }

/// <summary>A recorded contribution, rather than another evaluation of its gameplay effect.</summary>
public sealed class TurnMovementEntry
{
    public string Label { get; }
    public int Cells { get; }
    public TurnMovementKind Kind { get; }
    public int EffectiveCells { get; internal set; }
    public int ActualCells { get; internal set; }
    internal bool BasePlan { get; }

    internal TurnMovementEntry(string label, int cells, TurnMovementKind kind, bool basePlan)
    {
        Label = label;
        Cells = cells;
        Kind = kind;
        EffectiveCells = Math.Max(0, cells);
        BasePlan = basePlan;
    }
}

/// <summary>
/// Per-turn presentation ledger. It never pays heat, consumes skills or changes movement.
/// Settled instant movement survives base-plan construction; rewinds are recorded separately.
/// </summary>
public sealed class TurnMovementBreakdown
{
    private readonly List<TurnMovementEntry> entries = new List<TurnMovementEntry>();
    private readonly IReadOnlyList<TurnMovementEntry> readOnlyEntries;
    public IReadOnlyList<TurnMovementEntry> Entries => readOnlyEntries;
    public bool HasBasePlan { get; private set; }
    public int RewindCells { get; private set; }
    public int ActualCells
    {
        get { int sum = 0; foreach (var entry in entries) sum += entry.ActualCells; return sum; }
    }
    public int TotalCells
    {
        get { int sum = 0; foreach (var entry in entries) sum += entry.EffectiveCells; return sum; }
    }

    public TurnMovementBreakdown() { readOnlyEntries = entries.AsReadOnly(); }
    public void Reset()
    {
        entries.Clear();
        HasBasePlan = false;
        RewindCells = 0;
    }

    public void BeginBasePlan()
    {
        entries.RemoveAll(entry => entry.BasePlan);
        HasBasePlan = true;
    }

    public void AddPlan(string label, int cells, TurnMovementKind kind)
    {
        if (cells == 0) return;
        entries.Add(new TurnMovementEntry(label, cells, kind, true));
    }

    /// <summary>Reconcile only the base plan with the authoritative clamped/tutorial total.</summary>
    public void ReconcileBaseTotal(int total)
    {
        int signedSum = 0;
        foreach (var entry in entries) if (entry.BasePlan) signedSum += entry.Cells;
        int correction = Math.Max(0, total) - signedSum;
        AddPlan("结算修正（限值／教程）", correction, TurnMovementKind.Other);
        foreach (var entry in entries)
            if (entry.BasePlan) entry.EffectiveCells = Math.Max(0, entry.Cells);
        int positive = 0;
        foreach (var entry in entries) if (entry.BasePlan) positive += entry.EffectiveCells;
        int remove = Math.Max(0, positive - Math.Max(0, total));
        for (int i = entries.Count - 1; i >= 0 && remove > 0; i--)
        {
            var entry = entries[i];
            if (!entry.BasePlan) continue;
            int reduction = Math.Min(remove, entry.EffectiveCells);
            entry.EffectiveCells -= reduction;
            entry.ActualCells = Math.Min(entry.ActualCells, entry.EffectiveCells);
            remove -= reduction;
        }
    }

    /// <summary>Called once for each successfully traversed cell, including skipped visuals.</summary>
    public void Advance(int cells)
    {
        int remaining = Math.Max(0, cells);
        foreach (var entry in entries)
        {
            int amount = Math.Min(remaining, entry.EffectiveCells - entry.ActualCells);
            entry.ActualCells += amount;
            remaining -= amount;
            if (remaining == 0) break;
        }
        // Presentation-only callers may have no planning phase; never lose actual traversal.
        if (remaining > 0) RecordInstant("其他前进", remaining, TurnMovementKind.Other);
    }

    public void RecordInstant(string label, int cells, TurnMovementKind kind)
    {
        if (cells <= 0) return;
        var entry = new TurnMovementEntry(label, cells, kind, false) { ActualCells = cells };
        entries.Add(entry);
    }

    public void RecordRewind(int cells) { RewindCells += Math.Max(0, cells); }
}
