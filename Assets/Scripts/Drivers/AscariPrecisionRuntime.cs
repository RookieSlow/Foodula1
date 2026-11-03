using System;
using System.Collections.Generic;

/// <summary>D05 participant-local input scope. A committed use is never returned by interruption.</summary>
public sealed class AscariPrecisionRuntime
{
    private RaceSession session;
    private PlayerState owner;
    private CardDeck deck;
    private DriverSkillRuntimeState skill;
    private bool presentationOnly;
    public bool InputOpen { get; private set; }
    public int InputsStarted { get; private set; }
    public int Version { get; private set; }
    public CardData CommittedTarget { get; private set; }
    public bool CommittedThisInput { get; private set; }
    public bool RewardConsumed { get; private set; }

    internal bool BeginInput(RaceSession race, PlayerState player)
    {
        if (presentationOnly || race == null || player == null || player.isAI ||
            player.driverSkill == null || !player.driverSkill.Enabled ||
            player.driverSkill.Skill != DriverActiveSkillId.PrecisionCornering ||
            InputOpen && HasInputScope(race, player)) return false;
        CloseInput(); session = race; owner = player; deck = player.deck; skill = player.driverSkill;
        InputOpen = true; InputsStarted++; Version++; return true;
    }
    internal bool HasInputScope(RaceSession race, PlayerState player)
        => InputOpen && ReferenceEquals(session, race) && ReferenceEquals(owner, player) &&
            ReferenceEquals(deck, player?.deck) && ReferenceEquals(skill, player?.driverSkill) &&
            ReferenceEquals(this, player?.driverSkill?.Precision);

    internal bool IsCommittedFor(PlayerState player)
        => CommittedThisInput && CommittedTarget != null && ReferenceEquals(owner, player) &&
            ReferenceEquals(deck, player?.deck) && player.playedSpeedCardsThisTurn.Contains(CommittedTarget);

    internal void CommitValidated(CardData target)
    { CommittedTarget = target; CommittedThisInput = true; RewardConsumed = false; Version++; }

    /// <summary>Actual base distance and first-apex limit validate before the one-shot exit reward.</summary>
    public int ConsumeSettledReward(RaceSession race, PlayerState player, IReadOnlyList<TrackNode> nodes,
        int baseStart, int sourceLap, int totalLaps, int actualBaseCells, Func<int, int> effectiveLimit)
    {
        if (!HasInputScope(race, player) || !IsCommittedFor(player) || RewardConsumed ||
            RaceTurnRules.IsTerminal(player) || RaceTurnRules.ShouldSkip(player) || effectiveLimit == null) return 0;
        // Consume even a failed first-apex observation: later safe corners cannot replace it.
        RewardConsumed = true; Version++;
        if (!AscariPrecisionRules.TryGetFirstBaseApex(nodes, baseStart, AscariPrecisionRules.GetCommittedBaseSpeed(player),
            totalLaps - sourceLap, out int first, out int distance) || actualBaseCells < distance ||
            player.cornerTotalThisTurn > effectiveLimit(first)) return 0;
        return AscariPrecisionRules.GetExitReward(skill.Tier);
    }

    public void CloseInput()
    {
        InputOpen = false; CommittedThisInput = false; RewardConsumed = false; CommittedTarget = null;
        session = null; owner = null; deck = null; skill = null; Version++;
    }
    public void Interrupt() => CloseInput();
    public AscariPrecisionRuntime CreatePresentationCopy()
    { var copy = (AscariPrecisionRuntime)MemberwiseClone(); copy.presentationOnly = true; return copy; }
    internal void RebindPresentationCopy(RaceSession race, PlayerState original, PlayerState copy)
    {
        if (!presentationOnly || !ReferenceEquals(session, race) || !ReferenceEquals(owner, original)) return;
        owner = copy; deck = copy.deck; skill = copy.driverSkill;
    }
}
