using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 主菜单 UI 控制器 — 挂载在 MainMenuCanvas 上。
/// 按钮引用由 MainMenuBuilder Editor 工具赋值，Start() 中绑定回调。
/// </summary>
public class MainMenuUI : MonoBehaviour
{
    [Header("菜单按钮")]
    public Button startRaceButton;
    public Button quitButton;

    private TrackSelectionUI trackSelectionUI;
    private DriverSelectionUI driverSelectionUI;
    private Button driverSelectionButton;

    void Start()
    {
        trackSelectionUI = GetComponent<TrackSelectionUI>();
        if (trackSelectionUI == null)
        {
            trackSelectionUI = gameObject.AddComponent<TrackSelectionUI>();
        }
        trackSelectionUI.Initialize(OnTrackSelected);

        driverSelectionUI = GetComponent<DriverSelectionUI>();
        if (driverSelectionUI == null)
        {
            driverSelectionUI = gameObject.AddComponent<DriverSelectionUI>();
        }
        driverSelectionUI.Initialize(OnDriverSelected);

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

        Transform driverButtonTransform = transform.Find("GarageBtn");
        if (driverButtonTransform != null)
        {
            driverSelectionButton = driverButtonTransform.GetComponent<Button>();
            if (driverSelectionButton != null)
            {
                driverSelectionButton.interactable = true;
                driverSelectionButton.onClick.RemoveAllListeners();
                driverSelectionButton.onClick.AddListener(driverSelectionUI.Show);
                UpdateDriverButtonLabel();
            }
        }
    }

    /// <summary>开始比赛 → 加载 Race 场景。</summary>
    public void OnStartRace()
    {
        trackSelectionUI.Show();
    }

    /// <summary>Loads the Race scene after a valid track has been selected.</summary>
    public void OnTrackSelected(string trackId)
    {
        Debug.Log($"[MainMenuUI] Starting race on track: {trackId}");
        if (trackSelectionUI != null)
            trackSelectionUI.Hide();
        if (driverSelectionUI != null)
            driverSelectionUI.Hide();
        SceneLoader.LoadRace();
    }

    private void OnDriverSelected(string driverId)
    {
        UpdateDriverButtonLabel();
    }

    private void UpdateDriverButtonLabel()
    {
        if (driverSelectionButton == null) return;
        DriverProfile driver = DriverSelectionState.ResolveDriver(TeamId.CN);
        TMP_Text label = driverSelectionButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null) label.text = $"车手：{driver.ShortName}";
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
