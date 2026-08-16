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
    private TechTreeUI techTreeUI;
    private Button driverSelectionButton;
    private Button techTreeButton;

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

        techTreeUI = GetComponent<TechTreeUI>();
        if (techTreeUI == null)
            techTreeUI = gameObject.AddComponent<TechTreeUI>();
        techTreeUI.Initialize();

        if (startRaceButton != null)
        {
            ButtonClickAnimation.Attach(startRaceButton);
            startRaceButton.onClick.AddListener(OnStartRace);
        }
        else
        {
            Debug.LogError("[MainMenuUI] startRaceButton 未赋值！请在 Inspector 中拖入按钮。");
        }

        if (quitButton != null)
        {
            ButtonClickAnimation.Attach(quitButton);
            quitButton.onClick.AddListener(OnQuit);
        }

        Transform driverButtonTransform = transform.Find("GarageBtn");
        if (driverButtonTransform != null)
        {
            driverSelectionButton = driverButtonTransform.GetComponent<Button>();
            if (driverSelectionButton != null)
            {
                ButtonClickAnimation.Attach(driverSelectionButton);
                driverSelectionButton.interactable = true;
                driverSelectionButton.onClick.RemoveAllListeners();
                driverSelectionButton.onClick.AddListener(driverSelectionUI.Show);
                UpdateDriverButtonLabel();
            }
        }

        EnsureTechTreeButton();
    }

    /// <summary>
    /// Binds the authored TechTreeBtn when present and creates a compatible
    /// runtime fallback for older menu scenes. This keeps scene upgrades
    /// backwards compatible while the editor builder catches up.
    /// </summary>
    private void EnsureTechTreeButton()
    {
        Transform buttonTransform = transform.Find("TechTreeBtn");
        if (buttonTransform == null)
        {
            GameObject buttonObject = new GameObject("TechTreeBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -80f);
            rect.sizeDelta = new Vector2(320f, 64f);
            buttonObject.GetComponent<Image>().color = new Color(0.42f, 0.28f, 0.14f);
            ButtonClickAnimation.Attach(buttonObject.GetComponent<Button>());

            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = "车队科技树";
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            TMP_Text sourceFont = GetComponentInChildren<TMP_Text>(true);
            if (sourceFont != null) label.font = sourceFont.font;
            buttonTransform = buttonObject.transform;

            // Preserve the original vertical rhythm when upgrading a legacy
            // scene that only had Start/Garage/Quit.
            MoveMenuButton("GarageBtn", -170f);
            MoveMenuButton("QuitBtn", -260f);
        }

        techTreeButton = buttonTransform.GetComponent<Button>();
        if (techTreeButton != null)
        {
            ButtonClickAnimation.Attach(techTreeButton);
            techTreeButton.onClick.RemoveAllListeners();
            techTreeButton.onClick.AddListener(techTreeUI.Show);
            PlaceTechTreeButtonInMenuLayer(buttonTransform);
        }
    }

    /// <summary>
    /// Runtime overlays are appended after the authored menu children. Keep
    /// the tech-tree entry beside Garage/Quit so TrackSelectionUI and
    /// DriverSelectionUI overlays render above it instead of leaving a
    /// stray button visible over their panels.
    /// </summary>
    private void PlaceTechTreeButtonInMenuLayer(Transform buttonTransform)
    {
        Transform garage = transform.Find("GarageBtn");
        if (garage == null || buttonTransform == null) return;

        buttonTransform.SetSiblingIndex(garage.GetSiblingIndex() + 1);
    }

    private void MoveMenuButton(string objectName, float y)
    {
        Transform button = transform.Find(objectName);
        if (button == null) return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect != null) rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, y);
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
