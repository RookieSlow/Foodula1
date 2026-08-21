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
    public void YinYangChoiceGateIsMutuallyExclusiveAndResettable()
    {
        var state = new RaceInputState();
        state.BeginYinYangChoice();

        Assert.That(state.WaitingForYinYangChoice, Is.True);
        Assert.That(state.WaitingForCards, Is.False);
        Assert.That(state.WaitingForPitChoice, Is.False);

        state.BeginPitChoice();
        Assert.That(state.WaitingForYinYangChoice, Is.False);
        Assert.That(state.WaitingForPitChoice, Is.True);

        state.Reset();
        Assert.That(state.WaitingForYinYangChoice, Is.False);
        Assert.That(state.WaitingForPitChoice, Is.False);
    }
}
