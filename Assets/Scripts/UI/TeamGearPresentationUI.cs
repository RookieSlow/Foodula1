using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Presents the six drivetrains inside the existing GearButtons tutorial target.
/// Only HUDUI dispatches input commands; current gear is always read from PlayerState.
/// </summary>
public sealed class TeamGearPresentationUI : MonoBehaviour
{
    private HUDUI owner;
    private Button[] buttons = new Button[4];
    private TMP_Text[] labels = new TMP_Text[4];
    private TeamGearGraphic[] faces = new TeamGearGraphic[4];
    private Button shiftDownButton;
    private Button shiftUpButton;
    private TMP_Text title;
    private TMP_Text stateLabel;
    private TMP_Text detailLabel;
    private TMP_Text gearNumber;
    private TeamGearGraphic assembly;
    private int minimumGear = 1;
    private int maximumGear = 4;
    private float engineIntensity;
    private bool initialized;
    private PlayerState lastPlayer;
    private int lastConsecutiveCount;
    private bool appliedWhileLocked;
    private RaceTelemetryStripUI telemetry;
    private TeamGearDragHandleUI dragHandle;
    private int? dragPreviewGear;

    public TeamId Team { get; private set; } = TeamId.UK;
    public int CurrentGear { get; private set; } = 1;
    public int PendingGear { get; private set; } = 1;
    public bool ControlsInteractable { get; private set; }
    public bool IsLocked { get; private set; }
    public Button ShiftDownButton => shiftDownButton;
    public Button ShiftUpButton => shiftUpButton;
    public TMP_Text StateLabel => stateLabel;
    public TMP_Text DetailLabel => detailLabel;
    public TeamGearGraphic AssemblyGraphic => assembly;
    public RaceTelemetryStripUI Telemetry => telemetry;
    public TeamGearDragHandleUI DragHandle => dragHandle;

    public static TeamGearPresentationUI Attach(Transform container, HUDUI hud,
        Button gear1, Button gear2, Button gear3, Button gear4)
    {
        if (container == null) return null;
        TeamGearPresentationUI view = container.GetComponent<TeamGearPresentationUI>();
        if (view == null) view = container.gameObject.AddComponent<TeamGearPresentationUI>();
        view.Initialize(hud, gear1, gear2, gear3, gear4);
        return view;
    }

    public void Initialize(HUDUI hud, Button gear1, Button gear2, Button gear3, Button gear4)
    {
        owner = hud;
        buttons = new[] { gear1, gear2, gear3, gear4 };
        LayoutGroup oldLayout = GetComponent<LayoutGroup>();
        if (oldLayout != null) oldLayout.enabled = false;
        GearDialPresentationUI oldDial = GetComponent<GearDialPresentationUI>();
        if (oldDial != null) oldDial.enabled = false;

        TMP_FontAsset font = null;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            labels[i] = buttons[i].GetComponentInChildren<TMP_Text>(true);
            if (labels[i] != null && labels[i].font != null) font = labels[i].font;
            Transform oldFace = buttons[i].transform.Find("DialFace");
            Transform oldTick = buttons[i].transform.Find("DialTick");
            if (oldFace != null) oldFace.gameObject.SetActive(false);
            if (oldTick != null) oldTick.gameObject.SetActive(false);
            faces[i] = EnsureGraphic(buttons[i].transform, "TeamGearFace", true);
            buttons[i].targetGraphic = faces[i];
        }
        if (font == null) font = TMP_Settings.defaultFontAsset;

        assembly = EnsureGraphic(transform, "TeamGearAssembly", false);
        assembly.transform.SetAsFirstSibling();
        title = EnsureText(transform, "TeamGearTitle", font, 13f);
        stateLabel = EnsureText(transform, "TeamGearState", font, 12f);
        detailLabel = EnsureText(transform, "TeamGearDetail", font, 11.5f);
        gearNumber = EnsureText(transform, "TeamGearNumber", font, 28f);
        telemetry = RaceTelemetryStripUI.Attach(transform, hud, font);
        dragHandle = TeamGearDragHandleUI.Attach(transform, this);
        shiftDownButton = EnsureStepButton("TeamShiftDown", font);
        shiftUpButton = EnsureStepButton("TeamShiftUp", font);
        // Named callbacks are removed before reattaching, including after race reset.
        shiftDownButton.onClick.RemoveListener(OnShiftDown);
        shiftDownButton.onClick.AddListener(OnShiftDown);
        shiftUpButton.onClick.RemoveListener(OnShiftUp);
        shiftUpButton.onClick.AddListener(OnShiftUp);
        initialized = true;
        ApplyLayout();
    }

    public void Configure(TeamId team, int currentGear)
    {
        bool changed = Team != team;
        Team = team;
        CurrentGear = ClampGear(currentGear);
        PendingGear = CurrentGear;
        if (initialized && changed) ApplyLayout();
        UpdateDisplay(null, null, true);
    }

    /// <summary>Reapplied after the outer HUD has finished docking and styling its controls.</summary>
    public void ApplyLayout()
    {
        if (!initialized) return;
        Dock(assembly.transform, Vector2.zero, Vector2.one);
        Dock(title.transform, new Vector2(0f, 0.895f), new Vector2(1f, 1f));
        Dock(stateLabel.transform, new Vector2(0f, 0.095f), new Vector2(1f, 0.2f));
        Dock(detailLabel.transform, Vector2.zero, new Vector2(1f, 0.095f));
        Dock(gearNumber.transform, new Vector2(0.33f, 0.355f), new Vector2(0.67f, 0.61f));
        title.text = GetTitle(Team);
        title.color = RacingTechTheme.Primary;
        stateLabel.color = RacingTechTheme.Primary;
        detailLabel.color = RacingTechTheme.Secondary;
        gearNumber.color = RacingTechTheme.Primary;

        bool sequential = Team == TeamId.DE || Team == TeamId.JP;
        shiftDownButton.gameObject.SetActive(sequential);
        shiftUpButton.gameObject.SetActive(sequential);
        gearNumber.gameObject.SetActive(Team == TeamId.DE);
        dragHandle.gameObject.SetActive(Team == TeamId.US);
        Dock(dragHandle.transform, new Vector2(0.04f, 0.2f), new Vector2(0.43f, 0.625f));

        if (Team == TeamId.CN)
        {
            DockButton(buttons[0], new Vector2(0.07f, 0.245f), new Vector2(0.45f, 0.595f));
            DockButton(buttons[1], new Vector2(0.55f, 0.245f), new Vector2(0.93f, 0.595f));
        }
        else if (Team == TeamId.UK)
        {
            DockButton(buttons[0], new Vector2(0.08f, 0.455f), new Vector2(0.42f, 0.61f));
            DockButton(buttons[1], new Vector2(0.08f, 0.21f), new Vector2(0.42f, 0.365f));
            DockButton(buttons[2], new Vector2(0.58f, 0.455f), new Vector2(0.92f, 0.61f));
            DockButton(buttons[3], new Vector2(0.58f, 0.21f), new Vector2(0.92f, 0.365f));
        }
        else if (Team == TeamId.IT)
        {
            DockButton(buttons[0], new Vector2(0.36f, 0.455f), new Vector2(0.64f, 0.61f));
            for (int i = 1; i < 4; i++)
                DockButton(buttons[i], new Vector2(0.025f + (i - 1) * 0.33f, 0.21f), new Vector2(0.305f + (i - 1) * 0.33f, 0.365f));
        }
        else if (Team == TeamId.US)
        {
            for (int i = 0; i < 4; i++)
                DockButton(buttons[i], new Vector2(0.5f, 0.52f - i * 0.1075f), new Vector2(0.97f, 0.625f - i * 0.1075f));
        }
        else
        {
            for (int i = 0; i < 4; i++)
                DockButton(buttons[i], new Vector2(0.01f + i * 0.25f, 0.21f), new Vector2(0.24f + i * 0.25f, 0.335f));
            Dock(shiftDownButton.transform, new Vector2(0.01f, 0.34f), new Vector2(0.28f, 0.61f));
            Dock(shiftUpButton.transform, new Vector2(0.72f, 0.34f), new Vector2(0.99f, 0.61f));
            if (Team == TeamId.JP)
            {
                Dock(shiftUpButton.transform, new Vector2(0.76f, 0.485f), new Vector2(0.99f, 0.615f));
                Dock(shiftDownButton.transform, new Vector2(0.76f, 0.345f), new Vector2(0.99f, 0.475f));
            }
        }

        assembly.Configure(TeamGearGraphic.Shape.Assembly, Team);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            buttons[i].gameObject.SetActive(Team != TeamId.CN || i < 2);
            StyleButton(buttons[i], faces[i]);
            faces[i].Configure(GetGateShape(Team), Team);
            if (labels[i] != null)
            {
                Dock(labels[i].transform, Vector2.zero, Vector2.one);
                labels[i].fontSize = Team == TeamId.CN || Team == TeamId.US ? 13f : 14f;
                labels[i].alignment = TextAlignmentOptions.Center;
                labels[i].fontStyle = FontStyles.Bold;
                labels[i].raycastTarget = false;
                labels[i].margin = Vector4.zero;
                labels[i].enableWordWrapping = false;
            }
        }
        StyleStepButton(shiftDownButton, Team == TeamId.DE ? "-" : Team == TeamId.US ? "推 -" : "- 快排");
        StyleStepButton(shiftUpButton, Team == TeamId.DE ? "+" : Team == TeamId.US ? "拉 +" : "+ 快排");
        UpdateDisplay(null, null, true);
    }

    public void Refresh(PlayerState player, int pendingGear, bool canSelect, bool locked,
        GameConfigSO config, bool previewChoice)
    {
        if (player == null) return;
        if (Team != player.teamId) Configure(player.teamId, player.gear);
        if (!locked || lastPlayer != player)
            appliedWhileLocked = false;
        else if (CurrentGear != ClampGear(player.gear) || lastConsecutiveCount != player.chinaConsecutiveGearCount)
            appliedWhileLocked = true;
        minimumGear = config != null ? Mathf.Clamp(config.minGear, 1, 4) : 1;
        maximumGear = config != null ? Mathf.Clamp(config.maxGear, minimumGear, 4) : 4;
        CurrentGear = ClampGear(player.gear);
        PendingGear = ClampGear(pendingGear);
        ControlsInteractable = canSelect;
        IsLocked = locked;
        UpdateDisplay(player, config, previewChoice && !appliedWhileLocked);
        lastPlayer = player;
        lastConsecutiveCount = player.chinaConsecutiveGearCount;
    }

    public void SetEngineIntensity(float intensity)
    {
        engineIntensity = Mathf.Clamp01(intensity);
        assembly?.SetState(PendingGear, engineIntensity);
    }

    /// <summary>Repeated steps accumulate from pending, with the legal range measured from actual gear.</summary>
    public int ResolveStepTarget(int direction)
    {
        int low = Team == TeamId.CN ? 1 : Mathf.Max(minimumGear, CurrentGear - 2);
        int high = Team == TeamId.CN ? 2 : Mathf.Min(maximumGear, CurrentGear + 2);
        return Mathf.Clamp(PendingGear + (direction < 0 ? -1 : 1), low, high);
    }

    private int ClampGear(int gear) => Team == TeamId.CN
        ? ChinaGearShiftRules.ClampMode(gear) : Mathf.Clamp(gear, minimumGear, maximumGear);

    private void UpdateDisplay(PlayerState player, GameConfigSO config, bool previewChoice)
    {
        if (!initialized) return;
        string actual = TeamGearRules.GetDisplayName(Team, CurrentGear);
        string pending = TeamGearRules.GetDisplayName(Team, PendingGear);
        stateLabel.text = $"实际 {actual} / {(IsLocked ? "已锁" : "待选")} {pending}";
        gearNumber.text = CurrentGear.ToString();
        int shownPending = dragPreviewGear ?? PendingGear;
        assembly.SetState(shownPending, engineIntensity);
        if (dragPreviewGear.HasValue) stateLabel.text = $"实际 {actual} / 拖动预览 G{shownPending}";
        dragHandle.SetState(ControlsInteractable && Team == TeamId.US, CurrentGear, PendingGear);

        int shiftHeat = config != null ? config.twoGearShiftHeatCost : 1;
        int consecutive = player != null ? player.chinaConsecutiveGearCount : 0;
        TeamGearRules.Resolution choice = TeamGearRules.Resolve(Team, CurrentGear, consecutive,
            shownPending, minimumGear, maximumGear, shiftHeat, 3, 1);
        int presentedShiftHeat = DriverSkillRules.ApplyGearHeatCost(player?.driverSkill, choice.HeatCost, false);
        if (Team == TeamId.CN)
        {
            int cards = previewChoice ? choice.CardCount : TeamGearRules.GetSpeedCardCount(Team, CurrentGear, consecutive, 0);
            int heat = previewChoice ? choice.AdditionalHeat : Mathf.Min(3, Mathf.Max(0, consecutive - 1));
            heat = DriverSkillRules.ApplyGearHeatCost(player?.driverSkill, heat, true);
            int cooling = previewChoice ? choice.Cooldown : ChinaGearShiftRules.GetCooldown(CurrentGear, consecutive);
            detailLabel.text = ChinaGearShiftRules.IsGo(shownPending)
                ? $"出牌 {cards}张 · 连用热 +{heat}" : $"出牌 {cards}张 · 回收 {cooling}热";
        }
        else
            detailLabel.text = shownPending == choice.TargetGear
                ? $"出牌 {choice.CardCount}张 · 换挡热 {presentedShiftHeat}"
                : $"目标 G{choice.TargetGear} · 出牌 {choice.CardCount}张 · 热 {presentedShiftHeat}";

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            bool legal = Team == TeamId.CN ? i < 2 : i + 1 >= minimumGear && i + 1 <= maximumGear && Mathf.Abs(i + 1 - CurrentGear) <= 2;
            buttons[i].interactable = ControlsInteractable && legal;
            faces[i].SetGateState(i + 1 == CurrentGear, i + 1 == shownPending, buttons[i].interactable);
            if (labels[i] != null)
            {
                labels[i].text = Team == TeamId.CN ? (i == 0 ? "Recover" : "Go") : (i + 1).ToString();
                labels[i].color = legal ? RacingTechTheme.Primary : RacingTechTheme.Secondary;
            }
        }
        shiftDownButton.interactable = ControlsInteractable && ResolveStepTarget(-1) != PendingGear;
        shiftUpButton.interactable = ControlsInteractable && ResolveStepTarget(1) != PendingGear;
        UpdateStepFace(shiftDownButton);
        UpdateStepFace(shiftUpButton);
        if (owner != null && player != null) owner.RefreshGearTelemetryPreview(player, shownPending);
    }

    private void OnShiftDown() => RequestStep(-1);
    private void OnShiftUp() => RequestStep(1);

    private void RequestStep(int direction)
    {
        owner?.RefreshTeamGearPresentation();
        if (!ControlsInteractable || owner == null) return;
        int target = ResolveStepTarget(direction);
        if (target != PendingGear) owner.RequestGearSelection(target);
    }

    public int ResolveAbsoluteTarget(int target) => Mathf.Clamp(target,
        Mathf.Max(minimumGear, CurrentGear - 2), Mathf.Min(maximumGear, CurrentGear + 2));

    public void SetDragPreview(int? target)
    {
        dragPreviewGear = target;
        if (initialized) UpdateDisplay(lastPlayer, owner != null ? owner.GameConfigForPresentation : null, true);
    }

    public void RequestDraggedGear(int target, int? expectedCurrentGear = null)
    {
        owner?.RefreshTeamGearPresentation();
        if (ControlsInteractable && Team == TeamId.US && target != PendingGear &&
            (!expectedCurrentGear.HasValue || CurrentGear == expectedCurrentGear.Value))
            owner?.RequestGearSelection(ResolveAbsoluteTarget(target));
    }

    private void OnDestroy()
    {
        if (shiftDownButton != null) shiftDownButton.onClick.RemoveListener(OnShiftDown);
        if (shiftUpButton != null) shiftUpButton.onClick.RemoveListener(OnShiftUp);
    }

    private static TeamGearGraphic.Shape GetGateShape(TeamId team)
    {
        switch (team)
        {
            case TeamId.UK: return TeamGearGraphic.Shape.RoundGate;
            case TeamId.CN: return TeamGearGraphic.Shape.ElectricMode;
            case TeamId.US: return TeamGearGraphic.Shape.SequentialGate;
            case TeamId.JP: return TeamGearGraphic.Shape.QuickShiftGate;
            default: return TeamGearGraphic.Shape.MetalGate;
        }
    }

    public static Color GetAccent(TeamId team)
    {
        switch (team)
        {
            case TeamId.UK: return new Color(0.85f, 0.65f, 0.32f);
            case TeamId.DE: return new Color(0.38f, 0.78f, 1f);
            case TeamId.IT: return new Color(1f, 0.3f, 0.26f);
            case TeamId.US: return new Color(1f, 0.53f, 0.18f);
            case TeamId.CN: return new Color(0.2f, 0.94f, 0.73f);
            case TeamId.JP: return new Color(0.79f, 0.49f, 1f);
            default: return Color.white;
        }
    }

    private static string GetTitle(TeamId team)
    {
        switch (team)
        {
            case TeamId.UK: return "英国 · 经典 H 档";
            case TeamId.DE: return "德国 · 方向盘拨片";
            case TeamId.IT: return "意大利 · 金属开槽";
            case TeamId.US: return "美国 · 顺序推拉";
            case TeamId.CN: return "中国 · 双模式电驱";
            case TeamId.JP: return "日本 · 增压快排";
            default: return "换挡选择";
        }
    }

    private Button EnsureStepButton(string name, TMP_FontAsset font)
    {
        Transform child = transform.Find(name);
        GameObject host = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(Button));
        host.transform.SetParent(transform, false);
        Button button = host.GetComponent<Button>();
        TeamGearGraphic face = EnsureGraphic(host.transform, "TeamGearFace", true);
        button.targetGraphic = face;
        EnsureText(host.transform, "Label", font, 14f);
        ButtonClickAnimation.Attach(button);
        return button;
    }

    private void StyleStepButton(Button button, string text)
    {
        TeamGearGraphic face = button.targetGraphic as TeamGearGraphic;
        face.Configure(TeamGearGraphic.Shape.Paddle, Team);
        StyleButton(button, face);
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        label.text = text;
        label.fontSize = Team == TeamId.DE ? 25f : 13f;
        Dock(label.transform, Vector2.zero, Vector2.one);
    }

    private static void UpdateStepFace(Button button)
    {
        TeamGearGraphic face = button.targetGraphic as TeamGearGraphic;
        if (face != null) face.SetGateState(false, false, button.interactable);
    }

    private static void StyleButton(Button button, TeamGearGraphic face)
    {
        Image image = button.GetComponent<Image>();
        if (image != null) { image.color = Color.clear; image.raycastTarget = false; }
        button.targetGraphic = face;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 1f, 0.88f);
        colors.selectedColor = Color.white;
        colors.pressedColor = new Color(0.7f, 0.75f, 0.8f);
        colors.disabledColor = Color.white;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        face.raycastTarget = true;
        ButtonClickAnimation.Attach(button);
    }

    private static TeamGearGraphic EnsureGraphic(Transform parent, string name, bool interactive)
    {
        Transform existing = parent.Find(name);
        GameObject host = existing != null ? existing.gameObject : new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TeamGearGraphic));
        host.transform.SetParent(parent, false);
        Dock(host.transform, Vector2.zero, Vector2.one);
        host.transform.SetAsFirstSibling();
        TeamGearGraphic graphic = host.GetComponent<TeamGearGraphic>();
        graphic.raycastTarget = interactive;
        return graphic;
    }

    private static TMP_Text EnsureText(Transform parent, string name, TMP_FontAsset font, float size)
    {
        Transform existing = parent.Find(name);
        GameObject host = existing != null ? existing.gameObject : new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        host.transform.SetParent(parent, false);
        TMP_Text text = host.GetComponent<TMP_Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = RacingTechTheme.Primary;
        text.raycastTarget = false;
        text.enableWordWrapping = false;
        text.margin = Vector4.zero;
        return text;
    }

    private static void DockButton(Button button, Vector2 min, Vector2 max)
    {
        if (button != null) Dock(button.transform, min, max);
    }

    private static void Dock(Transform child, Vector2 min, Vector2 max)
    {
        RectTransform rect = child as RectTransform;
        if (rect == null) return;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
