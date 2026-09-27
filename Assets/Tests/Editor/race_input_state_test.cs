using NUnit.Framework;

public class RaceInputStateTests
{
    [Test]
    public void GearSelectionStartsWithCurrentGearAndCommitsOnce()
    {
        var state = new RaceInputState();
        state.BeginGearSelection(2);

        Assert.That(state.PendingGear, Is.EqualTo(2));
        Assert.That(state.SelectGear(4), Is.True);
        Assert.That(state.ConfirmGear(), Is.True);
        Assert.That(state.PlayerGearChoice, Is.EqualTo(4));
        Assert.That(state.WaitingForGear, Is.False);
        Assert.That(state.ConfirmGear(), Is.False);
    }

    [Test]
    public void CardAndDiscardGatesAreMutuallyExclusive()
    {
        var state = new RaceInputState();
        state.BeginCardSelection();
        Assert.That(state.WaitingForCards, Is.True);
        Assert.That(state.WaitingForDiscard, Is.False);

        state.BeginDiscardSelection();
        Assert.That(state.WaitingForCards, Is.False);
        Assert.That(state.WaitingForDiscard, Is.True);

        state.EndDiscardSelection();
        Assert.That(state.WaitingForDiscard, Is.False);
    }

    [Test]
    public void TrackChoiceGatesCloseEveryOtherInputGate()
    {
        var state = new RaceInputState();
        state.BeginCardSelection();
        state.BeginLaneChangeSelection();

        Assert.That(state.WaitingForLaneChange, Is.True);
        Assert.That(state.WaitingForCards, Is.False);
        Assert.That(state.WaitingForPitChoice, Is.False);

        state.BeginPitChoice();
        Assert.That(state.WaitingForPitChoice, Is.True);
        Assert.That(state.WaitingForLaneChange, Is.False);
    }

    [Test]
    public void EverySelectionTransitionLeavesExactlyOneOpenGate()
    {
        var state = new RaceInputState();

        state.BeginGearSelection(2);
        AssertGates(state, gear: true);
        state.BeginCardSelection();
        AssertGates(state, cards: true);
        state.BeginDiscardSelection();
        AssertGates(state, discard: true);
        state.BeginLaneChangeSelection();
        AssertGates(state, lane: true);
        state.BeginPitChoice();
        AssertGates(state, pit: true);

        state.Reset();
        AssertGates(state);
    }

    [Test]
    public void StaleCloseCallsDoNotCancelTheCurrentSelection()
    {
        var state = new RaceInputState();
        state.BeginCardSelection();
        state.BeginPitChoice();
        state.EndCardSelection();
        AssertGates(state, pit: true);

        state.BeginLaneChangeSelection();
        state.EndPitChoice();
        AssertGates(state, lane: true);

        state.BeginDiscardSelection();
        state.EndLaneChangeSelection();
        AssertGates(state, discard: true);

        state.BeginGearSelection(3);
        state.EndDiscardSelection();
        AssertGates(state, gear: true);
        Assert.That(state.ConfirmGear(), Is.True);
        AssertGates(state);
    }

    [Test]
    public void ResetClosesTrackChoiceGates()
    {
        var state = new RaceInputState();
        state.BeginLaneChangeSelection();
        state.BeginPitChoice();
        state.Reset();

        Assert.That(state.WaitingForLaneChange, Is.False);
        Assert.That(state.WaitingForPitChoice, Is.False);
    }

    [Test]
    public void ResetClosesEveryGateAndClearsChoices()
    {
        var state = new RaceInputState();
        state.BeginGearSelection(3);
        state.SelectGear(4);
        state.Reset();

        Assert.That(state.WaitingForGear, Is.False);
        Assert.That(state.WaitingForCards, Is.False);
        Assert.That(state.WaitingForDiscard, Is.False);
        Assert.That(state.PendingGear, Is.EqualTo(0));
        Assert.That(state.PlayerGearChoice, Is.EqualTo(0));
    }

    [Test]
    public void CardKeyboardNavigationWrapsAndSkipsHeatCards()
    {
        bool[] playable = { true, false, true, false };

        Assert.That(CardKeyboardNavigationRules.FindNextIndex(playable, -1, 1), Is.EqualTo(0));
        Assert.That(CardKeyboardNavigationRules.FindNextIndex(playable, 0, 1), Is.EqualTo(2));
        Assert.That(CardKeyboardNavigationRules.FindNextIndex(playable, 2, 1), Is.EqualTo(0));
        Assert.That(CardKeyboardNavigationRules.FindNextIndex(playable, 0, -1), Is.EqualTo(2));
    }

    [Test]
    public void CardKeyboardNavigationRejectsHandsWithoutPlayableCards()
    {
        Assert.That(CardKeyboardNavigationRules.FindNextIndex(
            new[] { false, false }, 0, 1), Is.EqualTo(-1));
    }

    [Test]
    public void SpaceShortcutPlaysSelectionAndNeverEndsAnEmptyCardPhase()
    {
        Assert.That(CardActionShortcutRules.Resolve(false, 0),
            Is.EqualTo(CardActionShortcutIntent.None));
        Assert.That(CardActionShortcutRules.Resolve(false, 2),
            Is.EqualTo(CardActionShortcutIntent.SubmitSelection));
        Assert.That(CardActionShortcutRules.Resolve(true, 0),
            Is.EqualTo(CardActionShortcutIntent.ConfirmDiscard));
    }

    private static void AssertGates(
        RaceInputState state,
        bool gear = false,
        bool cards = false,
        bool discard = false,
        bool lane = false,
        bool pit = false)
    {
        Assert.That(state.WaitingForGear, Is.EqualTo(gear));
        Assert.That(state.WaitingForCards, Is.EqualTo(cards));
        Assert.That(state.WaitingForDiscard, Is.EqualTo(discard));
        Assert.That(state.WaitingForLaneChange, Is.EqualTo(lane));
        Assert.That(state.WaitingForPitChoice, Is.EqualTo(pit));
    }
}
