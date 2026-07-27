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

    static EditorPlayBootstrapper()
    {
        // 设置 Play 模式启动场景
        var mainMenuAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(MAIN_MENU_PATH);
        if (mainMenuAsset != null)
        {
            EditorSceneManager.playModeStartScene = mainMenuAsset;
        }
    }
}
