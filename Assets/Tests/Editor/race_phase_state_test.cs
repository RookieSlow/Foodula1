using NUnit.Framework;

public class RacePhaseStateTests
{
    [Test]
    public void RaceStartsRunningAtGearSelection()
    {
        var state = new RacePhaseState();

        state.ResetForRace();

        Assert.That(state.Current, Is.EqualTo(GamePhase.WaitingForGear));
        Assert.That(state.IsRunning, Is.True);
    }

    [Test]
    public void PhaseTransitionsFollowTurnOrder()
    {
        var state = new RacePhaseState();

        state.BeginGearSelection();
        state.BeginCardSelection();
        Assert.That(state.Current, Is.EqualTo(GamePhase.WaitingForCards));

        state.BeginAnimation();
        Assert.That(state.Current, Is.EqualTo(GamePhase.Animating));

        state.CompleteGame();
        Assert.That(state.Current, Is.EqualTo(GamePhase.GameOver));
        Assert.That(state.IsRunning, Is.False);
    }

    [Test]
    public void InputAcceptanceRequiresMatchingPhaseAndGate()
    {
        var state = new RacePhaseState();
        var input = new RaceInputState();

        state.ResetForRace();
        Assert.That(state.CanAcceptGear(input), Is.False);
        input.BeginGearSelection(1);
        Assert.That(state.CanAcceptGear(input), Is.True);
        Assert.That(state.CanAcceptCards(input), Is.False);

        state.BeginCardSelection();
        Assert.That(state.CanAcceptGear(input), Is.False);
        input.BeginCardSelection();
        Assert.That(state.CanAcceptCards(input), Is.True);
    }
}
