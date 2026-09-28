/// <summary>Pure spin outcome; heat recovery, movement and presentation stay in the coordinator.</summary>
public readonly struct RaceSpinOutcome
{
    public RaceSpinOutcome(int counter, bool eliminated)
    {
        Counter = counter;
        Eliminated = eliminated;
    }
    public int Counter { get; }
    public bool Eliminated { get; }
}

public static class RaceSpinRules
{
    /// <summary>Preserves weather increments and the unclamped counter. Does not apply Final Sprint's separate cost rules.</summary>
    public static RaceSpinOutcome Evaluate(int counter, WeatherType weather, int effectiveMax)
    {
        int next = counter + 1 + WeatherRules.GetExtraSpinCounter(weather);
        return new RaceSpinOutcome(next, next >= effectiveMax);
    }

    public static int GetRecoveryGear(TeamId team, int minGear)
        => TeamGearRules.IsChina(team) ? ChinaGearShiftRules.RecoverGear : minGear;
}
