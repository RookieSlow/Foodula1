using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 主菜单 UI 控制器 — 挂载在 MainMenuCanvas 上。
/// 按钮引用由 MainMenuBuilder Editor 工具赋值，Start() 中绑定回调。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("菜单按钮")]
    public Button startRaceButton;
    public Button quitButton;

    void Start()
    {
        if (startRaceButton != null)
        {
            startRaceButton.onClick.AddListener(OnStartRace);
        }
        else
        {
            Debug.LogError("[MainMenuUI] startRaceButton 未赋值！请在 Inspector 中拖入按钮。");
        }

        if (quitButton != null)
        {
            quitButton.onClick.AddListener(OnQuit);
        }

        // 车队选择按钮由 Builder 设为 interactable = false，无需绑定回调
    }

    /// <summary>开始比赛 → 加载 Race 场景。</summary>
    public void OnStartRace()
    {
        SceneLoader.LoadRace();
    }

    /// <summary>退出游戏。</summary>
    public void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
