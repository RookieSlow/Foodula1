using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// 无论 Editor 中打开了哪个场景，Play 模式始终从 MainMenu 启动。
/// 利用 Unity 2019.3+ 的 playModeStartScene API。
/// </summary>
[InitializeOnLoad]
public static class EditorPlayBootstrapper
{
    private const string MAIN_MENU_PATH = "Assets/Scenes/MainMenu.unity";
    private const string ENTRY_TIME_KEY = "Foodula1.Editor.PlayEntry.Start";
    public const string EntrySecondsKey = "Foodula1.Editor.PlayEntry.Seconds";

    static EditorPlayBootstrapper()
    {
        EditorApplication.playModeStateChanged -= MeasurePlayEntry;
        EditorApplication.playModeStateChanged += MeasurePlayEntry;
        // 设置 Play 模式启动场景
        var mainMenuAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MAIN_MENU_PATH);
        if (mainMenuAsset != null)
        {
            EditorSceneManager.playModeStartScene = mainMenuAsset;
        }
    }

    private static void MeasurePlayEntry(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode)
            SessionState.SetString(ENTRY_TIME_KEY, System.DateTime.UtcNow.Ticks.ToString());
        else if (state == PlayModeStateChange.EnteredPlayMode &&
                 long.TryParse(SessionState.GetString(ENTRY_TIME_KEY, ""), out long started))
        {
            double seconds = (System.DateTime.UtcNow.Ticks - started) / (double)System.TimeSpan.TicksPerSecond;
            SessionState.SetString(EntrySecondsKey, seconds.ToString("F3", System.Globalization.CultureInfo.InvariantCulture));
            UnityEngine.Debug.Log($"[PlayEntry] {seconds:F3}s; domainReloadDisabled=" +
                (EditorSettings.enterPlayModeOptionsEnabled &&
                 (EditorSettings.enterPlayModeOptions & EnterPlayModeOptions.DisableDomainReload) != 0));
            SessionState.EraseString(ENTRY_TIME_KEY);
        }
    }
}
