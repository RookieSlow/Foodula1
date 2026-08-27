using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 场景切换工具类 — 对 SceneManager.LoadScene 的静态封装。
/// 所有场景间跳转统一通过此类，避免各处硬编码场景名字符串。
/// </summary>
public static class SceneLoader
{
    public const string MAIN_MENU = "MainMenu";
    public const string RACE = "Race";

    /// <summary>加载主菜单场景。</summary>
    public static void LoadMainMenu()
    {
        Debug.Log("[SceneLoader] Loading MainMenu...");
        TutorialLaunchState.Clear();
        LoadSceneInternal(MAIN_MENU);
    }

    /// <summary>加载比赛场景。</summary>
    public static void LoadRace()
    {
        Debug.Log("[SceneLoader] Loading Race...");
        PrepareForRaceTransition();
        LoadSceneInternal(RACE);
    }

    /// <summary>按场景名称加载。</summary>
    public static void LoadScene(string sceneName)
    {
        Debug.Log($"[SceneLoader] Loading scene: {sceneName}");
        LoadSceneInternal(sceneName);
    }

    private static void LoadSceneInternal(string sceneName)
    {
        // 检查场景是否在 Build Settings 中
        if (!IsSceneInBuildSettings(sceneName))
        {
            Debug.LogError(
                $"[SceneLoader] 场景 '{sceneName}' 不在 Build Settings 中！\n" +
                "请在 Unity 中: File → Build Settings → 将场景拖入 Scenes In Build。");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>
    /// Hides menu canvases before the synchronous scene switch.  This prevents
    /// a dynamically-built track-selection overlay from being visible during
    /// the frame in which the Race scene is activated, including canvases that
    /// came from a persistent UI root.
    /// </summary>
    private static void PrepareForRaceTransition()
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }

        MainMenuUI[] menus = Object.FindObjectsOfType<MainMenuUI>(true);
        for (int i = 0; i < menus.Length; i++)
        {
            if (menus[i] != null)
                menus[i].gameObject.SetActive(false);
        }

        GameObject[] objects = Object.FindObjectsOfType<GameObject>(true);
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject candidate = objects[i];
            if (candidate != null && IsStaleMenuObject(candidate.name))
                candidate.SetActive(false);
        }
    }

    private static bool IsStaleMenuObject(string objectName)
    {
        return objectName == "MainMenuCanvas"
            || objectName == "TrackSelectionOverlay"
            || objectName == "TrackSelectionPanel"
            || objectName == "DriverSelectionOverlay"
            || objectName == "DriverSelectionPanel";
    }

    /// <summary>检查场景是否在 Build Settings 中（Editor + Runtime 通用）。</summary>
    private static bool IsSceneInBuildSettings(string sceneName)
    {
        int count = SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < count; i++)
        {
            string path = SceneUtility.GetScenePathByBuildIndex(i);
            // 从路径提取场景名（不含 .unity）
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name == sceneName)
                return true;
        }
        return false;
    }
}
