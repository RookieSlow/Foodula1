using System;

/// <summary>
/// Applies one non-guided start/finish crossing in its existing order.
/// Tutorial completion and scene presentation remain with the coordinator.
/// </summary>
public static class RaceLapCrossingExecution
{
    public static RaceLapWeatherTransition Execute(
        PlayerState player,
        RaceSession session,
        RaceWeatherState weatherState,
        int requiredLaps,
        bool allowWeatherRoll,
        Action<bool> playCrossingSound,
        Action<string> showMessage)
    {
        RaceLapWeatherTransition transition = RaceLapWeatherRules.Advance(
            player.lap, requiredLaps, weatherState.LastRolledLap, allowWeatherRoll);
        player.lap = transition.Lap;
        session.OnNewLap(player);
        playCrossingSound(transition.HasFinished);
        showMessage?.Invoke($"{player.name} 完成第 {player.lap} 圈！");

        // Commit the shared gate before rolling. Later cars crossing the same
        // lap must not roll again, even if the weather itself stays unchanged.
        if (transition.ShouldRollWeather)
        {
            weatherState.MarkLapRolled(transition.Lap);
            WeatherType before = session.Weather;
            WeatherType after = session.RollWeatherForLap();
            if (after != before)
                showMessage?.Invoke(
                    $"<color=cyan>天气变化: {WeatherRules.GetDisplayName(before)} → {session.WeatherLabel}</color>");
        }

        if (transition.HasFinished)
        {
            player.hasFinished = true;
            session.AssignFinish(player);
            showMessage?.Invoke($"<color=green><b>{player.name} 完赛！</b></color>");
        }

        return transition;
    }
}
