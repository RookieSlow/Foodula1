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
    private FreeRaceRosterUI freeRaceRosterUI;
    private TechTreeUI techTreeUI;
    private GameSettingsUI gameSettingsUI;
    private CareerModeUI careerModeUI;
    private Button driverSelectionButton;
    private Button careerButton;
    private Button techTreeButton;
    private Button tutorialButton;
    private Button settingsButton;
    private bool deferredMenuStylesApplied;

    void Awake()
    {
        EnsureBrandArt();

        // The overlay may be kept in MainMenu for Prefab-layout authoring.
        // Hide that preview before the first rendered frame; the Race scene
        // creates and binds its own runtime instance after race presentation
        // initialization is complete.
        TutorialOverlayAuthoring[] authoringPreviews =
            FindObjectsOfType<TutorialOverlayAuthoring>(true);
        for (int i = 0; i < authoringPreviews.Length; i++)
        {
            if (authoringPreviews[i] != null)
                authoringPreviews[i].gameObject.SetActive(false);
        }
    }

    private void EnsureBrandArt()
    {
        Sprite backgroundSprite = BrandArtResources.LoadMainMenuBackground();
        if (backgroundSprite != null && transform.Find("BrandBackground") == null)
        {
            GameObject backgroundObject = new GameObject(
                "BrandBackground", typeof(RectTransform), typeof(Image), typeof(AspectRatioFitter));
            backgroundObject.transform.SetParent(transform, false);
            backgroundObject.transform.SetAsFirstSibling();
            RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            Image background = backgroundObject.GetComponent<Image>();
            background.sprite = backgroundSprite;
            background.preserveAspect = true;
            background.raycastTarget = false;
            AspectRatioFitter fitter = backgroundObject.GetComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = backgroundSprite.rect.width / backgroundSprite.rect.height;
        }

        Sprite logoSprite = BrandArtResources.LoadMainMenuLogo();
        if (logoSprite == null) return;

        Transform authoredTitle = transform.Find("TitleText");
        if (authoredTitle != null) authoredTitle.gameObject.SetActive(false);
        if (transform.Find("BrandLogo") != null) return;

        GameObject logoObject = new GameObject("BrandLogo", typeof(RectTransform), typeof(Image));
        logoObject.transform.SetParent(transform, false);
        RectTransform logoRect = logoObject.GetComponent<RectTransform>();
        logoRect.anchorMin = logoRect.anchorMax = new Vector2(0.5f, 0.5f);
        logoRect.pivot = new Vector2(0.5f, 0.5f);
        logoRect.anchoredPosition = new Vector2(0f, 330f);
        logoRect.sizeDelta = new Vector2(720f, 270f);
        Image logo = logoObject.GetComponent<Image>();
        logo.sprite = logoSprite;
        logo.preserveAspect = true;
        logo.raycastTarget = false;
    }

    void Start()
    {
        GameSettingsRuntime.EnsureLoadedAndApplyDisplay();

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

        freeRaceRosterUI = GetComponent<FreeRaceRosterUI>();
        if (freeRaceRosterUI == null)
            freeRaceRosterUI = gameObject.AddComponent<FreeRaceRosterUI>();
        freeRaceRosterUI.Initialize(OnFreeRaceRosterConfirmed);

        techTreeUI = GetComponent<TechTreeUI>();
        if (techTreeUI == null)
            techTreeUI = gameObject.AddComponent<TechTreeUI>();
        techTreeUI.Initialize();

        gameSettingsUI = GetComponent<GameSettingsUI>();
        if (gameSettingsUI == null)
            gameSettingsUI = gameObject.AddComponent<GameSettingsUI>();
        gameSettingsUI.Initialize(UpdateTutorialButtonLabel);

        careerModeUI = GetComponent<CareerModeUI>();
        if (careerModeUI == null)
            careerModeUI = gameObject.AddComponent<CareerModeUI>();
        careerModeUI.Initialize(CareerRuntimeRepository.CreateDefault(), true, OnStartCareerRace);

        if (startRaceButton != null)
        {
            ButtonClickAnimation.Attach(startRaceButton);
            startRaceButton.onClick.AddListener(OnStartRace);
            SetButtonLabel(startRaceButton, MainMenuLabels.QuickRace);
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
                driverSelectionButton.onClick.AddListener(OnShowDrivers);
                UpdateDriverButtonLabel();
            }
        }

        EnsureTechTreeButton();
        EnsureCareerButton();
        EnsureTutorialButton();
        EnsureSettingsButton();
        EnsureMenuDock();
        ApplyMenuButtonStyles();
        ApplyMenuLayout();
    }

    // Authored buttons can be touched by other UI components during their
    // Start methods. Re-apply the shared style once after all scene bindings
    // have completed so legacy TMP material overrides cannot win the frame.
    void LateUpdate()
    {
        if (deferredMenuStylesApplied) return;
        ApplyMenuButtonStyles();
        deferredMenuStylesApplied = true;
    }

    private void EnsureMenuDock()
    {
        Transform existing = transform.Find("MenuDock");
        if (existing != null) return;

        GameObject dock = new GameObject("MenuDock", typeof(RectTransform), typeof(Image));
        dock.transform.SetParent(transform, false);
        RectTransform rect = dock.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(0f, -70f);
        rect.sizeDelta = new Vector2(780f, 520f);
        ModernUIStyle.ApplyPanel(dock, true);
        Image image = dock.GetComponent<Image>();
        image.color = new Color(0.018f, 0.032f, 0.058f, 0.84f);
        image.raycastTarget = false;
        dock.transform.SetSiblingIndex(1);
    }

    private void ApplyMenuButtonStyles()
    {
        ModernUIStyle.ApplyMenuButton(startRaceButton, ModernUIStyle.AccentBlue, true);
        ModernUIStyle.ApplyMenuButton(careerButton, ModernUIStyle.AccentPurple);
        ModernUIStyle.ApplyMenuButton(techTreeButton, ModernUIStyle.AccentGold);
        ModernUIStyle.ApplyMenuButton(driverSelectionButton, ModernUIStyle.AccentCyan);
        ModernUIStyle.ApplyMenuButton(tutorialButton, ModernUIStyle.AccentGreen);
        ModernUIStyle.ApplyMenuButton(settingsButton, ModernUIStyle.AccentBlue);
        ModernUIStyle.ApplyMenuButton(quitButton, ModernUIStyle.AccentRed);
    }

    private void EnsureCareerButton()
    {
        Transform buttonTransform = transform.Find("CareerBtn");
        if (buttonTransform == null)
        {
            GameObject buttonObject = CreateRuntimeMenuButton(
                "CareerBtn", MainMenuLabels.Career, new Color(0.5f, 0.25f, 0.62f));
            buttonTransform = buttonObject.transform;
        }

        careerButton = buttonTransform.GetComponent<Button>();
        if (careerButton == null) return;
        ButtonClickAnimation.Attach(careerButton);
        careerButton.onClick.RemoveAllListeners();
        careerButton.onClick.AddListener(OnShowCareer);
        SetButtonLabel(careerButton, MainMenuLabels.Career);
        PlaceButtonInMenuLayer(buttonTransform);
    }

    private GameObject CreateRuntimeMenuButton(string name, string text, Color color)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(transform, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(320f, 64f);
        buttonObject.GetComponent<Image>().color = color;

        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = text;
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        TMP_Text sourceFont = GetComponentInChildren<TMP_Text>(true);
        if (sourceFont != null) label.font = sourceFont.font;
        ModernUIStyle.ApplyMenuButton(buttonObject.GetComponent<Button>(), color);
        return buttonObject;
    }

    private void ApplyMenuLayout()
    {
        PlaceMenuButton("StartRaceBtn", new Vector2(-170f, 20f));
        PlaceMenuButton("CareerBtn", new Vector2(170f, 20f));
        PlaceMenuButton("TechTreeBtn", new Vector2(-170f, -70f));
        PlaceMenuButton("GarageBtn", new Vector2(170f, -70f));
        PlaceMenuButton("TutorialBtn", new Vector2(-170f, -160f));
        PlaceMenuButton("SettingsBtn", new Vector2(170f, -160f));
        PlaceMenuButton("QuitBtn", new Vector2(0f, -250f));
    }

    private void PlaceMenuButton(string objectName, Vector2 position)
    {
        Transform button = transform.Find(objectName);
        RectTransform rect = button != null ? button.GetComponent<RectTransform>() : null;
        if (rect != null) rect.anchoredPosition = position;
    }

    private static void SetButtonLabel(Button button, string text)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = text;
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
            techTreeButton.onClick.AddListener(OnShowTechTree);
            PlaceButtonInMenuLayer(buttonTransform);
        }
    }

    /// <summary>
    /// Runtime overlays are appended after the authored menu children. Keep
    /// runtime entries beside Garage/Quit so TrackSelectionUI and
    /// DriverSelectionUI overlays render above it instead of leaving a
    /// stray button visible over their panels.
    /// </summary>
    private void PlaceButtonInMenuLayer(Transform buttonTransform)
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

    private void EnsureTutorialButton()
    {
        Transform buttonTransform = transform.Find("TutorialBtn");
        if (buttonTransform == null)
        {
            GameObject buttonObject = new GameObject(
                "TutorialBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -260f);
            rect.sizeDelta = new Vector2(320f, 64f);
            buttonObject.GetComponent<Image>().color = new Color(0.16f, 0.47f, 0.56f);

            GameObject labelObject = new GameObject(
                "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = "新手教程";
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            TMP_Text sourceFont = GetComponentInChildren<TMP_Text>(true);
            if (sourceFont != null) label.font = sourceFont.font;

            buttonTransform = buttonObject.transform;
            MoveMenuButton("QuitBtn", -350f);
        }

        tutorialButton = buttonTransform.GetComponent<Button>();
        if (tutorialButton == null) return;

        ButtonClickAnimation.Attach(tutorialButton);
        tutorialButton.onClick.RemoveAllListeners();
        tutorialButton.onClick.AddListener(OnStartTutorial);
        UpdateTutorialButtonLabel();

        Transform quit = transform.Find("QuitBtn");
        if (quit != null)
            buttonTransform.SetSiblingIndex(quit.GetSiblingIndex());
    }

    private void EnsureSettingsButton()
    {
        Transform buttonTransform = transform.Find("SettingsBtn");
        if (buttonTransform == null)
        {
            GameObject buttonObject = new GameObject(
                "SettingsBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(transform, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, -350f);
            rect.sizeDelta = new Vector2(320f, 64f);
            buttonObject.GetComponent<Image>().color = new Color(0.28f, 0.34f, 0.48f);

            GameObject labelObject = new GameObject(
                "Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TMP_Text label = labelObject.GetComponent<TMP_Text>();
            label.text = "设置";
            label.fontSize = 28f;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            TMP_Text sourceFont = GetComponentInChildren<TMP_Text>(true);
            if (sourceFont != null) label.font = sourceFont.font;

            buttonTransform = buttonObject.transform;
            MoveMenuButton("QuitBtn", -440f);
        }

        settingsButton = buttonTransform.GetComponent<Button>();
        if (settingsButton == null) return;
        ButtonClickAnimation.Attach(settingsButton);
        settingsButton.onClick.RemoveAllListeners();
        settingsButton.onClick.AddListener(OnShowSettings);

        Transform quit = transform.Find("QuitBtn");
        if (quit != null)
            buttonTransform.SetSiblingIndex(quit.GetSiblingIndex());
    }

    private void UpdateTutorialButtonLabel()
    {
        if (tutorialButton == null) return;
        TMP_Text label = tutorialButton.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
            label.text = GameSettingsRuntime.Current.tutorialCompleted
                ? "重播新手教程"
                : "新手教程";
    }

    /// <summary>开始比赛 → 加载 Race 场景。</summary>
    public void OnStartRace()
    {
        HideMenuSurfaces();
        TutorialLaunchState.Clear();
        CareerRaceLaunchState.Clear();
        FreeRaceRosterState.InitializeDefault(
            DriverSelectionState.ResolveDriver(TeamId.CN),
            new[] { TeamId.UK, TeamId.DE, TeamId.IT });
        freeRaceRosterUI.Show();
    }

    private void OnFreeRaceRosterConfirmed()
    {
        HideMenuSurfaces();
        trackSelectionUI.Show();
    }

    private void OnStartCareerRace()
    {
        HideMenuSurfaces();
        TutorialLaunchState.Clear();
        SceneLoader.LoadRace();
    }

    /// <summary>Starts the isolated Le Mans tutorial without changing quick-race selections.</summary>
    public void OnStartTutorial()
    {
        HideMenuSurfaces();
        CareerRaceLaunchState.Clear();
        TutorialLaunchState.Request(TutorialScenarioDefinition.CreateLeMansUk());
        Debug.Log($"[MainMenuUI] Starting tutorial: {TutorialScenarioDefinition.ScenarioId}");
        SceneLoader.LoadRace();
    }

    /// <summary>Loads the Race scene after a valid track has been selected.</summary>
    public void OnTrackSelected(string trackId)
    {
        TutorialLaunchState.Clear();
        CareerRaceLaunchState.Clear();
        Debug.Log($"[MainMenuUI] Starting race on track: {trackId}");
        HideMenuSurfaces();
        SceneLoader.LoadRace();
    }

    private void OnShowDrivers()
    {
        HideMenuSurfaces();
        driverSelectionUI.Show();
        UpdateDriverButtonLabel();
    }

    private void OnShowCareer()
    {
        HideMenuSurfaces();
        careerModeUI.Show();
    }

    private void OnShowTechTree()
    {
        HideMenuSurfaces();
        techTreeUI.Show();
    }

    private void OnShowSettings()
    {
        HideMenuSurfaces();
        gameSettingsUI.Show();
    }

    private void HideMenuSurfaces()
    {
        if (trackSelectionUI != null) trackSelectionUI.Hide();
        if (driverSelectionUI != null) driverSelectionUI.Hide();
        if (freeRaceRosterUI != null) freeRaceRosterUI.Hide();
        if (techTreeUI != null) techTreeUI.Hide();
        if (gameSettingsUI != null) gameSettingsUI.Hide();
        if (careerModeUI != null) careerModeUI.Hide();
        GameEncyclopediaUI encyclopedia = GetComponent<GameEncyclopediaUI>();
        if (encyclopedia != null) encyclopedia.Hide();
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
