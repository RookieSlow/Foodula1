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

    [TestCase(GamePhase.WaitingForGear, 0, false, false)]
    [TestCase(GamePhase.WaitingForGear, 1, true, false)]
    [TestCase(GamePhase.WaitingForGear, 2, false, false)]
    [TestCase(GamePhase.WaitingForCards, 0, false, false)]
    [TestCase(GamePhase.WaitingForCards, 1, false, false)]
    [TestCase(GamePhase.WaitingForCards, 2, false, true)]
    [TestCase(GamePhase.Animating, 0, false, false)]
    [TestCase(GamePhase.Animating, 1, false, false)]
    [TestCase(GamePhase.Animating, 2, false, false)]
    [TestCase(GamePhase.GameOver, 0, false, false)]
    [TestCase(GamePhase.GameOver, 1, false, false)]
    [TestCase(GamePhase.GameOver, 2, false, false)]
    public void InputAcceptanceMatrixRequiresPhaseAndMatchingInputGate(
        GamePhase phase,
        int inputGate,
        bool acceptsGear,
        bool acceptsCards)
    {
        RacePhaseState state = CreateStateAt(phase);
        RaceInputState input = CreateInputWithGate(inputGate);

        Assert.That(state.CanAcceptGear(input), Is.EqualTo(acceptsGear));
        Assert.That(state.CanAcceptCards(input), Is.EqualTo(acceptsCards));
    }

    [TestCase(GamePhase.WaitingForGear)]
    [TestCase(GamePhase.WaitingForCards)]
    [TestCase(GamePhase.Animating)]
    [TestCase(GamePhase.GameOver)]
    public void NullInputNeverOpensAnAcceptanceGate(GamePhase phase)
    {
        RacePhaseState state = CreateStateAt(phase);

        Assert.That(state.CanAcceptGear(null), Is.False);
        Assert.That(state.CanAcceptCards(null), Is.False);
    }

    [Test]
    public void TurnPhaseSequenceRejectsStaleGatesAtEachBoundary()
    {
        var state = new RacePhaseState();
        var input = new RaceInputState();
        state.ResetForRace();
        input.BeginGearSelection(2);
        Assert.That(state.CanAcceptGear(input), Is.True);

        state.BeginCardSelection();
        input.BeginCardSelection();
        Assert.That(state.CanAcceptGear(input), Is.False);
        Assert.That(state.CanAcceptCards(input), Is.True);

        state.BeginAnimation();
        Assert.That(state.CanAcceptGear(input), Is.False);
        Assert.That(state.CanAcceptCards(input), Is.False,
            "A stale card gate must not accept input while movement is resolving.");

        state.CompleteGame();
        Assert.That(state.CanAcceptGear(input), Is.False);
        Assert.That(state.CanAcceptCards(input), Is.False,
            "Completing the race must reject any still-open input gate.");
    }

    private static RacePhaseState CreateStateAt(GamePhase phase)
    {
        var state = new RacePhaseState();
        switch (phase)
        {
            case GamePhase.WaitingForGear:
                state.ResetForRace();
                break;
            case GamePhase.WaitingForCards:
                state.ResetForRace();
                state.BeginCardSelection();
                break;
            case GamePhase.Animating:
                state.ResetForRace();
                state.BeginAnimation();
                break;
            case GamePhase.GameOver:
                state.CompleteGame();
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(phase), phase, null);
        }

        return state;
    }

    private static RaceInputState CreateInputWithGate(int inputGate)
    {
        var input = new RaceInputState();
        switch (inputGate)
        {
            case 0:
                break;
            case 1:
                input.BeginGearSelection(2);
                break;
            case 2:
                input.BeginCardSelection();
                break;
            default:
                throw new System.ArgumentOutOfRangeException(nameof(inputGate), inputGate, null);
        }

        return input;
    }
}
