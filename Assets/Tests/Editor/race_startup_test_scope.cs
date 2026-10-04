using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

/// <summary>Restores exact session-only launch fields, including requested/active identity.</summary>
internal sealed class RaceStartupTestScope : IDisposable
{
    private readonly List<Action> restore = new List<Action>();

    internal RaceStartupTestScope(bool isolateTelemetry = false)
    {
        foreach (Type type in new[] { typeof(TutorialLaunchState), typeof(CareerRaceLaunchState),
                     typeof(FreeRaceRosterState), typeof(DriverSelectionState) })
        foreach (FieldInfo field in type.GetFields(BindingFlags.Static | BindingFlags.NonPublic))
        {
            object value = field.GetValue(null);
            if (value is IList list)
            {
                object[] items = list.Cast<object>().ToArray();
                restore.Add(() => { list.Clear(); foreach (object item in items) list.Add(item); });
            }
            else if (value is IDictionary dictionary)
            {
                var entries = new List<DictionaryEntry>();
                IDictionaryEnumerator enumerator = dictionary.GetEnumerator();
                while (enumerator.MoveNext()) entries.Add(enumerator.Entry);
                restore.Add(() => { dictionary.Clear(); foreach (var entry in entries) dictionary.Add(entry.Key, entry.Value); });
            }
            else if (!field.IsInitOnly && !field.IsLiteral)
                restore.Add(() => field.SetValue(null, value));
        }
        if (isolateTelemetry)
        {
            FieldInfo singleton = typeof(PlaytestTelemetryService)
                .GetField("instance", BindingFlags.Static | BindingFlags.NonPublic);
            object saved = singleton.GetValue(null);
            restore.Add(() => singleton.SetValue(null, saved));
            singleton.SetValue(null, null);
        }
        TutorialLaunchState.Clear();
        CareerRaceLaunchState.Clear();
        FreeRaceRosterState.Clear();
        DriverSelectionState.Reset();
    }

    public void Dispose()
    {
        for (int i = restore.Count - 1; i >= 0; i--) restore[i]();
    }
}
