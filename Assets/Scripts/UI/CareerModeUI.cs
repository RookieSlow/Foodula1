using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built main-menu surface for the persistent eight-race career.
/// Race and career-tech editing use career-owned session state; this screen
/// never mutates Quick Race selections or the normal technology profile.
/// </summary>
public sealed class CareerModeUI : MonoBehaviour
{
    private enum PendingAction { None, Replace, Abandon }

    private readonly Dictionary<TeamId, Button> teamButtons = new Dictionary<TeamId, Button>();
    private CareerModeService service;
    private CareerTechTreeUI summerTechUI;
    private GameObject summerTechOverlay;
    private Action requestRaceLaunch;
    private bool raceLaunchAvailable;
    private GameObject overlay;
    private GameObject selectionPanel;
    private GameObject careerPanel;
    private GameObject confirmationPanel;
    private TMP_Text selectionStatus;
    private TMP_Text careerStatus;
    private TMP_Text playerSummary;
    private TMP_Text calendarText;
    private TMP_Text standingsText;
    private TMP_Text confirmationText;
    private TMP_Text primaryLabel;
    private Button primaryButton;
    private TeamId selectedTeam = TeamId.CN;
    private PendingAction pendingAction;
    private bool choosingReplacement;

    public void Initialize(
        CareerRepository repository,
        bool canLaunchRace = false,
        Action launchCallback = null)
    {
        if (repository == null) throw new ArgumentNullException(nameof(repository));
        service = new CareerModeService(repository);
        raceLaunchAvailable = canLaunchRace;
        requestRaceLaunch = launchCallback;
        if (overlay == null) Build();
        summerTechUI = GetComponent<CareerTechTreeUI>();
        if (summerTechUI == null)
            summerTechUI = gameObject.AddComponent<CareerTechTreeUI>();
        Hide();
    }

    public void Show()
    {
        if (service == null)
            Initialize(CareerRuntimeRepository.CreateDefault());
        Hide();
        Refresh();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        // The summer editor is a sibling overlay, not a child of this panel.
        // Its next Show creates a fresh draft, so leaving never saves edits.
        if (summerTechOverlay != null) summerTechOverlay.SetActive(false);
        pendingAction = PendingAction.None;
        choosingReplacement = false;
        if (confirmationPanel != null) confirmationPanel.SetActive(false);
        if (confirmationText != null) confirmationText.text = string.Empty;
        if (overlay != null) overlay.SetActive(false);
    }

    private void Build()
    {
        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(true)?.font;
        var factory = new RaceUIFactory(font);

        overlay = new GameObject("CareerModeOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        ModernUIStyle.ApplyOverlay(overlay.GetComponent<Image>());

        GameObject shell = CreatePanel(overlay.transform, "CareerModePanel", new Vector2(1500f, 860f));
        TMP_Text title = factory.CreateText(shell.transform, "CareerTitle", MainMenuLabels.Career, 44,
            new Vector2(0f, 365f), new Vector2(900f, 62f));
        Format(title, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.35f, 0.82f, 1f));

        selectionPanel = new GameObject("CareerTeamSelection", typeof(RectTransform));
        selectionPanel.transform.SetParent(shell.transform, false);
        Stretch(selectionPanel.GetComponent<RectTransform>());
        BuildTeamSelection(factory, selectionPanel.transform);

        careerPanel = new GameObject("CareerOverview", typeof(RectTransform));
        careerPanel.transform.SetParent(shell.transform, false);
        Stretch(careerPanel.GetComponent<RectTransform>());
        BuildOverview(factory, careerPanel.transform);

        Button close = factory.CreateActionButton(shell.transform, "CareerClose", "返回",
            new Vector2(-620f, -370f), new Color(0.28f, 0.32f, 0.4f), Hide);
        close.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 52f);

        BuildConfirmation(factory, shell.transform);
        overlay.SetActive(false);
    }

    private void BuildTeamSelection(RaceUIFactory factory, Transform parent)
    {
        TMP_Text prompt = factory.CreateText(parent, "TeamPrompt",
            "选择本轮生涯车队（创建后八站内不可更换）", 25,
            new Vector2(0f, 285f), new Vector2(1100f, 42f));
        Format(prompt, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);

        IReadOnlyList<TeamId> teams = CareerMenuPresentation.AvailableTeams;
        for (int i = 0; i < teams.Count; i++)
        {
            TeamId team = teams[i];
            float x = -360f + (i % 3) * 360f;
            float y = 150f - (i / 3) * 130f;
            Button button = factory.CreateActionButton(parent, $"CareerTeam_{team}",
                CareerMenuPresentation.GetTeamLabel(team), new Vector2(x, y),
                TeamCarPresentationRules.GetBadgeColor(team), () => SelectTeam(team));
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(300f, 88f);
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            label.color = TeamCarPresentationRules.GetBadgeTextColor(team);
            AddTeamLogo(button.transform, team);
            teamButtons[team] = button;
        }

        selectionStatus = factory.CreateText(parent, "CareerSelectionStatus", string.Empty, 19,
            new Vector2(0f, -145f), new Vector2(1160f, 90f));
        Format(selectionStatus, TextAlignmentOptions.Center, FontStyles.Normal,
            new Color(0.72f, 0.82f, 0.92f));

        Button create = factory.CreateActionButton(parent, "CareerCreate", "确认车队并新建",
            new Vector2(0f, -245f), new Color(0.18f, 0.62f, 0.38f), RequestCreate);
        create.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 58f);
    }

    private static void AddTeamLogo(Transform parent, TeamId team)
    {
        Sprite sprite = BrandArtResources.LoadTeamLogo(team);
        if (sprite == null) return;
        GameObject logoObject = new GameObject("TeamLogo", typeof(RectTransform), typeof(Image));
        logoObject.transform.SetParent(parent, false);
        RectTransform rect = logoObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = new Vector2(-105f, 0f);
        rect.sizeDelta = new Vector2(56f, 56f);
        Image image = logoObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        TMP_Text label = parent.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.rectTransform.offsetMin = new Vector2(66f, label.rectTransform.offsetMin.y);
            label.rectTransform.offsetMax = new Vector2(-12f, label.rectTransform.offsetMax.y);
        }
    }

    private void BuildOverview(RaceUIFactory factory, Transform parent)
    {
        playerSummary = factory.CreateText(parent, "CareerPlayerSummary", string.Empty, 22,
            new Vector2(0f, 300f), new Vector2(1240f, 42f));
        Format(playerSummary, TextAlignmentOptions.Center, FontStyles.Bold,
            new Color(0.45f, 0.9f, 0.68f));
        careerStatus = factory.CreateText(parent, "CareerStatus", string.Empty, 19,
            new Vector2(0f, 250f), new Vector2(1240f, 58f));
        Format(careerStatus, TextAlignmentOptions.Center, FontStyles.Normal,
            new Color(0.76f, 0.84f, 0.94f));

        TMP_Text calendarHeader = factory.CreateText(parent, "CalendarHeader", "八站赛历", 25,
            new Vector2(-350f, 185f), new Vector2(520f, 42f));
        Format(calendarHeader, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        calendarText = factory.CreateText(parent, "CareerCalendar", string.Empty, 20,
            new Vector2(-350f, -15f), new Vector2(560f, 360f));
        Format(calendarText, TextAlignmentOptions.TopLeft, FontStyles.Normal, Color.white);

        TMP_Text standingsHeader = factory.CreateText(parent, "StandingsHeader", "积分榜", 25,
            new Vector2(350f, 185f), new Vector2(520f, 42f));
        Format(standingsHeader, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        standingsText = factory.CreateText(parent, "CareerStandings", string.Empty, 20,
            new Vector2(350f, -15f), new Vector2(560f, 360f));
        Format(standingsText, TextAlignmentOptions.TopLeft, FontStyles.Normal, Color.white);

        primaryButton = factory.CreateActionButton(parent, "CareerPrimary", string.Empty,
            new Vector2(0f, -280f), new Color(0.18f, 0.5f, 0.68f), OnPrimaryAction);
        primaryButton.GetComponent<RectTransform>().sizeDelta = new Vector2(390f, 58f);
        primaryLabel = primaryButton.GetComponentInChildren<TMP_Text>(true);

        Button newSeason = factory.CreateActionButton(parent, "CareerNewSeason", "新建生涯",
            new Vector2(360f, -350f), new Color(0.46f, 0.34f, 0.16f), RequestReplace);
        newSeason.GetComponent<RectTransform>().sizeDelta = new Vector2(210f, 50f);
        Button abandon = factory.CreateActionButton(parent, "CareerAbandon", "放弃本轮",
            new Vector2(590f, -350f), new Color(0.55f, 0.2f, 0.2f), RequestAbandon);
        abandon.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 50f);
    }

    private void BuildConfirmation(RaceUIFactory factory, Transform parent)
    {
        confirmationPanel = CreatePanel(parent, "CareerConfirmation", new Vector2(720f, 310f));
        ModernUIStyle.ApplyPanel(confirmationPanel, true);
        confirmationText = factory.CreateText(confirmationPanel.transform, "ConfirmationText", string.Empty, 22,
            new Vector2(0f, 55f), new Vector2(620f, 115f));
        Format(confirmationText, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        Button cancel = factory.CreateActionButton(confirmationPanel.transform, "CareerConfirmCancel", "取消",
            new Vector2(-150f, -85f), new Color(0.3f, 0.34f, 0.4f), CancelConfirmation);
        cancel.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 50f);
        Button confirm = factory.CreateActionButton(confirmationPanel.transform, "CareerConfirmAction", "确认",
            new Vector2(150f, -85f), new Color(0.62f, 0.2f, 0.2f), ConfirmPendingAction);
        confirm.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 50f);
        confirmationPanel.SetActive(false);
    }

    private void Refresh()
    {
        CareerMenuViewModel view = CareerMenuPresentation.Build(
            service.CurrentState, service.LoadStatus, raceLaunchAvailable);
        bool showSelection = !view.HasCareer || choosingReplacement;
        selectionPanel.SetActive(showSelection);
        careerPanel.SetActive(view.HasCareer && !choosingReplacement);
        confirmationPanel.SetActive(pendingAction != PendingAction.None);

        if (showSelection)
        {
            string status = choosingReplacement
                ? "选择新车队后，需要再次确认覆盖当前生涯。"
                : view.Status;
            selectionStatus.text = status + $"\n当前选择：{CareerMenuPresentation.GetTeamLabel(selectedTeam)}";
            foreach (KeyValuePair<TeamId, Button> entry in teamButtons)
            {
                Image image = entry.Value.GetComponent<Image>();
                Color color = TeamCarPresentationRules.GetBadgeColor(entry.Key);
                image.color = entry.Key == selectedTeam ? Color.Lerp(color, Color.white, 0.3f) : color;
            }
            return;
        }

        playerSummary.text = view.PlayerSummary;
        careerStatus.text = view.Status;
        calendarText.text = view.Calendar;
        standingsText.text = view.Standings;
        primaryLabel.text = view.PrimaryAction;
        primaryButton.interactable = view.CanAdjustTech || view.CanLaunchRace || view.CanStartNewSeason;
    }

    private void SelectTeam(TeamId team)
    {
        selectedTeam = team;
        Refresh();
    }

    private void RequestCreate()
    {
        if (service.HasStoredCareer)
        {
            ShowConfirmation(PendingAction.Replace,
                "将覆盖现有或无法读取的生涯存档。此操作不可撤销。是否继续？");
            return;
        }
        CreateSeason(false);
    }

    private void RequestReplace()
    {
        if (service.CurrentState != null && service.CurrentState.HasLockedTeam)
            selectedTeam = service.CurrentState.LockedTeam;
        choosingReplacement = true;
        Refresh();
    }

    private void RequestAbandon()
    {
        ShowConfirmation(PendingAction.Abandon, "放弃后将删除本轮生涯进度。是否确认放弃？");
    }

    private void ShowConfirmation(PendingAction action, string message)
    {
        pendingAction = action;
        confirmationText.text = message;
        confirmationPanel.SetActive(true);
        confirmationPanel.transform.SetAsLastSibling();
    }

    private void CancelConfirmation()
    {
        pendingAction = PendingAction.None;
        confirmationPanel.SetActive(false);
    }

    private void ConfirmPendingAction()
    {
        PendingAction action = pendingAction;
        pendingAction = PendingAction.None;
        confirmationPanel.SetActive(false);
        if (action == PendingAction.Replace) CreateSeason(true);
        else if (action == PendingAction.Abandon)
        {
            choosingReplacement = false;
            service.TryAbandon(true);
        }
        Refresh();
    }

    private void CreateSeason(bool confirmedReplace)
    {
        TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
        TechTreeState profile = TechTreeProfileStore.GetOrCreate(selectedTeam, database);
        bool created = CareerTechSnapshotMapper.TryCapture(profile, out CareerTechSnapshot snapshot) &&
            service.TryCreateNew(selectedTeam,
                CareerMenuPresentation.BuildCompetitorField(selectedTeam), snapshot, confirmedReplace);
        if (!created)
        {
            selectionStatus.text = "无法创建生涯，请检查存档或科技树数据后重试。";
        }
        else
        {
            choosingReplacement = false;
        }
        Refresh();
    }

    private void OnPrimaryAction()
    {
        if (service.CurrentState.Phase == CareerPhase.Completed)
        {
            RequestReplace();
            return;
        }

        if (CareerModeRules.CanAdjustTechTree(service.CurrentState))
        {
            if (!summerTechUI.Show(service.CurrentState, ConfirmSummerBreakTech, Show))
                careerStatus.text = "无法读取生涯科技快照；夏休仍未消费。";
            else
            {
                summerTechOverlay = transform.Find("CareerTechTreeOverlay").gameObject;
                overlay.SetActive(false);
            }
            return;
        }

        if (!raceLaunchAvailable || !CareerModeRules.CanStartNextRace(service.CurrentState))
            return;

        string resultId = $"career-{service.CurrentState.NextTrackIndex + 1}-{Guid.NewGuid():N}";
        if (!CareerRaceLaunchState.Request(service.CurrentState, resultId))
        {
            careerStatus.text = "无法准备下一站生涯比赛；进度未改变。";
            return;
        }
        requestRaceLaunch?.Invoke();
    }

    private bool ConfirmSummerBreakTech(CareerTechSnapshot snapshot)
    {
        bool saved = service.TryConfirmSummerBreak(snapshot);
        if (saved) Refresh();
        return saved;
    }

    private static GameObject CreatePanel(Transform parent, string name, Vector2 size)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        ModernUIStyle.ApplyPanel(panel);
        return panel;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Format(TMP_Text text, TextAlignmentOptions alignment, FontStyles style, Color color)
    {
        text.alignment = alignment;
        text.fontStyle = style;
        text.color = color;
    }
}
