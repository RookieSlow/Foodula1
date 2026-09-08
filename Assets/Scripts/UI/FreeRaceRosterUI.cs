using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built free-race field editor. The first selected team is the
/// human car; every other selected team is controlled by the normal AI.
/// </summary>
public sealed class FreeRaceRosterUI : MonoBehaviour
{
    private readonly Dictionary<TeamId, Image> teamCardImages = new Dictionary<TeamId, Image>();
    private readonly Dictionary<TeamId, Button> teamToggleButtons = new Dictionary<TeamId, Button>();
    private readonly Dictionary<TeamId, TMP_Text> teamStatusTexts = new Dictionary<TeamId, TMP_Text>();
    private readonly Dictionary<string, Image> driverButtonImages = new Dictionary<string, Image>();
    private readonly Dictionary<string, Button> driverButtons = new Dictionary<string, Button>();

    private Action onConfirmed;
    private GameObject overlay;
    private Button thunderstormButton;
    private TMP_Text hintText;
    private TMP_Text selectionText;
    private TMP_Text errorText;
    private TMP_FontAsset font;
    private bool initialized;

    public void Initialize(Action confirmCallback)
    {
        onConfirmed = confirmCallback;
        if (initialized) return;

        font = GetComponentInChildren<TMP_Text>(true)?.font;
        BuildOverlay();
        initialized = true;
    }

    public void Show()
    {
        if (!initialized) Initialize(null);
        if (!FreeRaceRosterState.IsConfigured)
        {
            FreeRaceRosterState.InitializeDefault(
                DriverSelectionState.ResolveDriver(TeamId.CN),
                new[] { TeamId.UK, TeamId.DE, TeamId.IT });
        }

        SetError(string.Empty);
        RefreshVisuals();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    private void BuildOverlay()
    {
        overlay = CreateObject("FreeRaceRosterOverlay", transform);
        Stretch(overlay.GetComponent<RectTransform>());
        Image overlayImage = overlay.AddComponent<Image>();
        ModernUIStyle.ApplyOverlay(overlayImage);

        GameObject panel = CreateObject("FreeRaceRosterPanel", overlay.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1480f, 930f);
        panel.AddComponent<Image>();
        ModernUIStyle.ApplyPanel(panel, true);

        CreateText(panel.transform, "Title", "自由赛事 · 自定义混战", 44f,
            new Vector2(0f, 420f), new Vector2(1000f, 64f), FontStyles.Bold);
        hintText = CreateText(panel.transform, "Hint",
            "勾选 2–6 支车队，并为每支车队安排一名车手；第一支车队由你驾驶，其余为 AI。",
            20f, new Vector2(0f, 378f), new Vector2(1320f, 42f), FontStyles.Normal,
            new Color(0.62f, 0.72f, 0.84f));

        GameObject grid = CreateObject("TeamGrid", panel.transform);
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        gridRect.anchorMin = gridRect.anchorMax = new Vector2(0.5f, 0.5f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.anchoredPosition = new Vector2(0f, 35f);
        gridRect.sizeDelta = new Vector2(1380f, 640f);
        GridLayoutGroup layout = grid.AddComponent<GridLayoutGroup>();
        layout.cellSize = new Vector2(430f, 285f);
        layout.spacing = new Vector2(20f, 22f);
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = 3;
        layout.childAlignment = TextAnchor.MiddleCenter;

        for (int i = 0; i < FreeRaceRosterRules.AvailableTeams.Count; i++)
            CreateTeamCard(grid.transform, FreeRaceRosterRules.AvailableTeams[i]);

        selectionText = CreateText(panel.transform, "SelectionSummary", string.Empty, 19f,
            new Vector2(0f, -360f), new Vector2(1100f, 32f), FontStyles.Normal,
            new Color(0.45f, 0.82f, 1f));
        errorText = CreateText(panel.transform, "ValidationMessage", string.Empty, 18f,
            new Vector2(0f, -391f), new Vector2(1100f, 34f), FontStyles.Normal,
            new Color(1f, 0.48f, 0.42f));

        Button backButton = CreateButton(panel.transform, "BackButton", "返回",
            new Vector2(-580f, -421f), new Vector2(190f, 54f),
            new Color(0.24f, 0.28f, 0.34f), 22f);
        backButton.onClick.AddListener(Hide);

        Button confirmButton = CreateButton(panel.transform, "ConfirmButton", "确认阵容",
            new Vector2(580f, -421f), new Vector2(230f, 54f),
            ModernUIStyle.AccentBlue, 22f);
        confirmButton.onClick.AddListener(ConfirmRoster);

        thunderstormButton = CreateButton(panel.transform, "ThunderstormButton",
            "雷霆大混战（6队12车）", new Vector2(0f, -421f), new Vector2(330f, 54f),
            new Color(0.66f, 0.25f, 0.10f), 19f);
        thunderstormButton.onClick.AddListener(ToggleThunderstorm);

        overlay.SetActive(false);
    }

    private void CreateTeamCard(Transform parent, TeamId team)
    {
        GameObject card = CreateObject("Team_" + team, parent);
        Image background = card.AddComponent<Image>();
        background.color = GetTeamColor(team);
        teamCardImages[team] = background;

        AddTeamLogo(card.transform, team, new Vector2(-174f, 98f), new Vector2(52f, 52f));
        CreateText(card.transform, "TeamName", CareerMenuPresentation.GetTeamLabel(team), 25f,
            new Vector2(-35f, 101f), new Vector2(250f, 42f), FontStyles.Bold);

        Button toggle = CreateButton(card.transform, "ToggleTeam", "加入车队",
            new Vector2(140f, 101f), new Vector2(116f, 38f),
            new Color(0.15f, 0.26f, 0.40f), 15f);
        TeamId capturedTeam = team;
        toggle.onClick.AddListener(() => ToggleTeam(capturedTeam));
        teamToggleButtons[team] = toggle;

        teamStatusTexts[team] = CreateText(card.transform, "TeamStatus", string.Empty, 15f,
            new Vector2(0f, 62f), new Vector2(400f, 26f), FontStyles.Normal,
            new Color(0.78f, 0.88f, 0.96f));

        IReadOnlyList<DriverProfile> drivers = DriverSelectionState.AvailableDrivers;
        int driverCount = 0;
        for (int i = 0; i < drivers.Count; i++)
        {
            if (drivers[i].Team != team) continue;
            float x = driverCount == 0 ? -105f : 105f;
            CreateDriverButton(card.transform, drivers[i], new Vector2(x, -38f));
            driverCount++;
        }
    }

    private void CreateDriverButton(Transform parent, DriverProfile driver, Vector2 position)
    {
        Button button = CreateButton(parent, "RosterDriver_" + driver.Id, driver.ShortName,
            position, new Vector2(190f, 108f), GetTeamColor(driver.Team), 18f);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = $"{driver.ShortName}\n<size=13>{FormatStyle(driver.Style)}</size>";
            label.alignment = TextAlignmentOptions.Center;
        }

        string capturedDriverId = driver.Id;
        button.onClick.AddListener(() => AssignDriver(driver.Team, capturedDriverId));
        driverButtons[driver.Id] = button;
        driverButtonImages[driver.Id] = button.GetComponent<Image>();
    }

    private void ToggleTeam(TeamId team)
    {
        SetError(string.Empty);
        bool selecting = !FreeRaceRosterState.IsSelected(team);
        if (!FreeRaceRosterState.TrySetTeamSelected(team, selecting))
        {
            SetError($"最多只能选择 {FreeRaceRosterRules.MaxParticipants} 支车队。");
            return;
        }

        RefreshVisuals();
    }

    private void AssignDriver(TeamId team, string driverId)
    {
        SetError(string.Empty);
        if (!FreeRaceRosterState.IsSelected(team))
        {
            SetError("请先加入该车队，再安排车手。");
            return;
        }

        if (!FreeRaceRosterState.TryAssignDriver(team, driverId))
        {
            SetError("车手必须属于当前车队。");
            return;
        }

        RefreshVisuals();
    }

    private void ConfirmRoster()
    {
        if (!FreeRaceRosterState.TryBuildRoster(out _, out string error))
        {
            SetError(error);
            RefreshVisuals();
            return;
        }

        FreeRaceRosterState.ApplyPrimaryDriverSelection();
        onConfirmed?.Invoke();
        if (onConfirmed == null) Hide();
    }

    private void ToggleThunderstorm()
    {
        SetError(string.Empty);
        DriverProfile primary = FreeRaceRosterState.SelectedTeams.Count > 0
            ? FreeRaceRosterState.GetDriver(FreeRaceRosterState.SelectedTeams[0])
            : DriverSelectionState.ResolveDriver(TeamId.CN);

        if (FreeRaceRosterState.IsThunderstorm)
        {
            FreeRaceRosterState.InitializeDefault(primary, new[] { TeamId.UK, TeamId.DE, TeamId.IT });
        }
        else
        {
            FreeRaceRosterState.InitializeThunderstorm(primary);
        }

        RefreshVisuals();
    }

    private void RefreshVisuals()
    {
        IReadOnlyList<TeamId> selectedTeams = FreeRaceRosterState.SelectedTeams;
        TeamId primaryTeam = selectedTeams.Count > 0 ? selectedTeams[0] : TeamId.UK;
        bool thunderstorm = FreeRaceRosterState.IsThunderstorm;

        if (hintText != null)
        {
            hintText.text = thunderstorm
                ? "六支车队、十二名车手全部出场；第一支车队的当前车手由你驾驶，其余 11 辆车由 AI 控制。"
                : "勾选 2–6 支车队，并为每支车队安排一名车手；第一支车队由你驾驶，其余为 AI。";
        }

        SetButtonLabel(thunderstormButton,
            thunderstorm ? "返回自定义阵容" : "雷霆大混战（6队12车）");

        for (int i = 0; i < FreeRaceRosterRules.AvailableTeams.Count; i++)
        {
            TeamId team = FreeRaceRosterRules.AvailableTeams[i];
            bool selected = FreeRaceRosterState.IsSelected(team);
            bool primary = selected && team == primaryTeam;
            Color baseColor = GetTeamColor(team);
            teamCardImages[team].color = selected
                ? Color.Lerp(baseColor, Color.white, primary ? 0.25f : 0.12f)
                : Color.Lerp(baseColor, Color.black, 0.35f);

            Button toggle = teamToggleButtons[team];
            toggle.interactable = !thunderstorm &&
                (selected || selectedTeams.Count < FreeRaceRosterRules.MaxParticipants);
            SetButtonLabel(toggle, thunderstorm
                ? "雷霆参赛"
                : selected ? "退出车队" : "加入车队");
            teamStatusTexts[team].text = !selected
                ? "未参赛"
                : thunderstorm
                    ? primary
                        ? "玩家双车 · 另一名车手由 AI 控制"
                        : "AI 双车 · 雷霆参赛"
                : primary
                    ? $"玩家 · {FreeRaceRosterState.GetDriver(team).ShortName}"
                    : $"AI · {FreeRaceRosterState.GetDriver(team).ShortName}";
        }

        foreach (KeyValuePair<string, Button> entry in driverButtons)
        {
            if (!DriverCatalog.TryGet(entry.Key, out DriverProfile driver)) continue;
            bool selected = FreeRaceRosterState.IsSelected(driver.Team);
            bool assigned = selected && (thunderstorm ||
                FreeRaceRosterState.GetDriverId(driver.Team) == driver.Id);
            entry.Value.interactable = selected && !thunderstorm;
            driverButtonImages[entry.Key].color = assigned
                ? Color.Lerp(GetTeamColor(driver.Team), Color.white, 0.28f)
                : selected
                    ? GetTeamColor(driver.Team)
                    : Color.Lerp(GetTeamColor(driver.Team), Color.black, 0.40f);
        }

        string summary = thunderstorm
            ? $"雷霆大混战：{FreeRaceRosterRules.AvailableTeams.Count} 支车队 / {FreeRaceRosterRules.ThunderstormParticipants} 名车手　玩家：{FreeRaceRosterState.GetDriver(primaryTeam).ShortName}"
            : selectedTeams.Count >= FreeRaceRosterRules.MinParticipants &&
            selectedTeams.Count <= FreeRaceRosterRules.MaxParticipants
            ? $"已选 {selectedTeams.Count}/{FreeRaceRosterRules.MaxParticipants} 支车队　玩家：{FreeRaceRosterState.GetDriver(primaryTeam).ShortName}"
            : $"已选 {selectedTeams.Count} 支车队（需要 {FreeRaceRosterRules.MinParticipants}-{FreeRaceRosterRules.MaxParticipants} 支）";
        if (selectionText != null) selectionText.text = summary;
    }

    private void SetError(string message)
    {
        if (errorText != null) errorText.text = message ?? string.Empty;
    }

    private static string FormatStyle(DriverStyle style)
    {
        switch (style)
        {
            case DriverStyle.Aggressive: return "进攻型";
            case DriverStyle.Technical: return "技术型";
            default: return "均衡型";
        }
    }

    private static Color GetTeamColor(TeamId team)
    {
        return TeamCarPresentationRules.GetBadgeColor(team);
    }

    private static void AddTeamLogo(Transform parent, TeamId team, Vector2 position, Vector2 size)
    {
        Sprite sprite = BrandArtResources.LoadTeamLogo(team);
        if (sprite == null) return;
        GameObject logoObject = CreateObject("TeamLogo", parent);
        RectTransform rect = logoObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = logoObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
    }

    private TMP_Text CreateText(Transform parent, string objectName, string content, float size,
        Vector2 position, Vector2 dimensions, FontStyles style, Color? color = null)
    {
        GameObject textObject = CreateObject(objectName, parent);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = TextAlignmentOptions.Center;
        text.color = color ?? Color.white;
        text.raycastTarget = false;
        if (font != null) text.font = font;
        return text;
    }

    private Button CreateButton(Transform parent, string objectName, string label, Vector2 position,
        Vector2 dimensions, Color color, float size)
    {
        GameObject buttonObject = CreateObject(objectName, parent);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = dimensions;
        Image image = buttonObject.AddComponent<Image>();
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ModernUIStyle.ApplyButton(button, color);
        ButtonClickAnimation.Attach(button);
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, size,
            Vector2.zero, dimensions, FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
    }

    private static void SetButtonLabel(Button button, string value)
    {
        TMP_Text label = button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        if (label != null) label.text = value;
    }

    private static GameObject CreateObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
