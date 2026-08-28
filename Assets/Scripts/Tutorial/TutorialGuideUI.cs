using TMPro;
using UnityEngine;
using UnityEngine.UI;

public readonly struct TutorialGuideLayout
{
    public float Width { get; }
    public float Height { get; }
    public float Margin { get; }
    public float InstructionFontSize { get; }
    public bool IsCompact { get; }

    public TutorialGuideLayout(
        float width,
        float height,
        float margin,
        float instructionFontSize,
        bool isCompact)
    {
        Width = width;
        Height = height;
        Margin = margin;
        InstructionFontSize = instructionFontSize;
        IsCompact = isCompact;
    }
}

/// <summary>
/// Pure safe-area sizing for the runtime-built tutorial panel. The panel may
/// be collapsed whenever the player needs an unobstructed view of the HUD.
/// </summary>
public static class TutorialGuideLayoutRules
{
    public static TutorialGuideLayout Resolve(int screenWidth, int screenHeight, bool expanded)
    {
        float width = Mathf.Max(1f, screenWidth);
        float height = Mathf.Max(1f, screenHeight);
        float margin = width >= 1280f && height >= 720f ? 24f : 12f;
        float safeWidth = Mathf.Max(1f, width - margin * 2f);
        float safeHeight = Mathf.Max(1f, height - margin * 2f);

        if (!expanded)
        {
            float collapsedWidth = Mathf.Min(
                Mathf.Clamp(width * 0.4f, 280f, 420f),
                safeWidth);
            float collapsedHeight = Mathf.Min(104f, safeHeight);
            return new TutorialGuideLayout(
                collapsedWidth,
                collapsedHeight,
                margin,
                13f,
                isCompact: true);
        }

        float expandedWidth = Mathf.Min(
            Mathf.Clamp(width * 0.48f, 360f, 540f),
            safeWidth);
        float expandedHeight = Mathf.Min(
            Mathf.Clamp(height * 0.72f, 340f, 440f),
            safeHeight);
        bool compact = expandedWidth < 460f || expandedHeight < 380f;
        return new TutorialGuideLayout(
            expandedWidth,
            expandedHeight,
            margin,
            compact ? 13f : 15f,
            compact);
    }
}

/// <summary>
/// Runtime-built tutorial guide panel. It presents authored scenario text and
/// delegates every state change to MVPGameManager/TutorialRuntimeDirector.
/// </summary>
public sealed class TutorialGuideUI : MonoBehaviour
{
    private MVPGameManager manager;
    private TutorialOverlayAuthoring authoring;
    [Header("Prefab 文本与按钮引用")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text completionText;
    [SerializeField] private TMP_Text instructionText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private Button continueButton;
    [SerializeField] private TMP_Text continueLabel;
    [SerializeField] private Button modeButton;
    [SerializeField] private TMP_Text modeLabel;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button collapseButton;
    [SerializeField] private TMP_Text collapseLabel;
    [SerializeField] private TutorialFocusHighlightUI focusHighlighter;

    [Header("布局模式")]
    [Tooltip("启用后保留 Prefab 中手工调整的展开布局；收起后再展开会恢复该布局。")]
    [SerializeField] private bool useAuthoredLayout;
    private bool primaryRestartsPractice;
    private bool isExpanded = true;
    private bool authoredLayoutCaptured;
    private RectTransformSnapshot panelSnapshot;
    private RectTransformSnapshot titleSnapshot;
    private RectTransformSnapshot progressSnapshot;
    private RectTransformSnapshot collapseSnapshot;
    private float authoredTitleFontSize;
    private float authoredProgressFontSize;

    public static TutorialGuideUI Create(
        MVPGameManager manager,
        Canvas canvas,
        TMP_FontAsset font)
    {
        if (manager == null || canvas == null)
            return null;

        TutorialOverlayAuthoring authored =
            canvas.GetComponentInChildren<TutorialOverlayAuthoring>(true);
        if (authored == null)
        {
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/TutorialOverlay");
            if (prefab != null)
            {
                GameObject instance = Instantiate(prefab, canvas.transform, false);
                instance.name = "TutorialOverlay";
                authored = instance.GetComponent<TutorialOverlayAuthoring>();
            }
        }

        if (authored != null && authored.Guide != null)
        {
            authored.gameObject.SetActive(true);
            authored.Bind(manager, canvas);
            authored.Guide.gameObject.SetActive(true);
            authored.Guide.Refresh();
            return authored.Guide;
        }

        Transform existing = canvas.transform.Find("TutorialGuidePanel");
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject("TutorialGuidePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsLastSibling();

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.09f, 0.96f);

        TutorialGuideUI guide = panel.GetComponent<TutorialGuideUI>();
        if (guide == null)
            guide = panel.AddComponent<TutorialGuideUI>();
        guide.manager = manager;
        guide.useAuthoredLayout = false;
        guide.Build(font);
        guide.focusHighlighter = TutorialFocusHighlightUI.Create(canvas, manager, font);
        panel.transform.SetAsLastSibling();
        guide.Refresh();
        return guide;
    }

    public void Bind(
        MVPGameManager targetManager,
        TutorialOverlayAuthoring source,
        TutorialFocusHighlightUI highlight)
    {
        manager = targetManager;
        authoring = source;
        focusHighlighter = highlight;
        CaptureAuthoredLayout();
        BindButtonListeners();
    }

    public void ConfigureAsAuthoredLayout(bool enabled)
    {
        useAuthoredLayout = enabled;
        authoredLayoutCaptured = false;
    }

    public void PreviewAuthoredStep(
        TutorialStepPresentation presentation,
        int oneBasedIndex,
        int totalSteps)
    {
        if (presentation == null || !HasPresentationReferences())
            return;

        gameObject.SetActive(true);
        isExpanded = true;
        ApplyLayout();
        titleText.text = presentation.title;
        completionText.text = "先看高光区域，再完成这一小步";
        instructionText.text = presentation.BuildGuideText();
        progressText.text =
            $"{presentation.sectionLabel} · 第 {oneBasedIndex}/{totalSteps} 步";
        SetContinueState(
            !string.IsNullOrWhiteSpace(presentation.manualAdvanceLabel),
            string.IsNullOrWhiteSpace(presentation.manualAdvanceLabel)
                ? "等待本步操作"
                : presentation.manualAdvanceLabel);
        SetModeState(true, "跳过引导");
    }

    public void PreviewAuthoredPractice(
        string previewTitle,
        string previewCompletion,
        string previewInstruction,
        int totalSteps,
        bool completed)
    {
        if (!HasPresentationReferences())
            return;

        gameObject.SetActive(true);
        isExpanded = true;
        ApplyLayout();
        titleText.text = previewTitle;
        completionText.text = previewCompletion;
        instructionText.text = previewInstruction;
        progressText.text = $"{totalSteps}/{totalSteps}";
        SetContinueState(true, completed ? "再练一圈" : "重新开始");
        SetModeState(true, "重播引导");
    }

    private bool HasPresentationReferences()
    {
        return titleText != null &&
               completionText != null &&
               instructionText != null &&
               progressText != null &&
               continueButton != null &&
               continueLabel != null &&
               modeButton != null &&
               modeLabel != null;
    }

    public void Refresh()
    {
        TutorialRuntimeDirector director = manager != null ? manager.TutorialDirector : null;
        if (director == null)
        {
            focusHighlighter?.Hide();
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        ApplyLayout();
        TutorialStepDefinition step = director.CurrentStep;
        if (step == null)
        {
            focusHighlighter?.Hide();
            bool completed = director.Phase == TutorialRunPhase.Completed;
            titleText.text = authoring != null
                ? authoring.GetPracticeTitle(completed)
                : completed ? "练习圈完成" : "勒芒自由练习";
            completionText.text = authoring != null
                ? authoring.GetPracticeCompletion(completed)
                : completed
                    ? "✓ 一整圈练习已经完成"
                    : "✓ 引导已结束，比赛状态已完整重置";
            instructionText.text = authoring != null
                ? authoring.GetPracticeInstruction(completed)
                : completed
                    ? "你已完成一整圈勒芒练习。该结果不会发放 RP、车手 XP、解锁或赛事进度。可以再练一圈、重播引导或退出。"
                    : "状态已重置为 UK、零科技和教程精确牌组。自由完成一整圈；本圈使用脚本阴天，仍不写入正常奖励与进度。";
            progressText.text = $"{director.CompletedStepCount}/{director.StepCount}";
            primaryRestartsPractice = true;
            SetContinueState(true, completed ? "再练一圈" : "重新开始");
            SetModeState(true, "重播引导");
            return;
        }

        primaryRestartsPractice = false;
        TutorialStepPresentation presentation = authoring != null
            ? authoring.Find(step.id)
            : null;
        focusHighlighter?.Show(
            step.focusTarget,
            presentation != null ? presentation.focusIntroduction : step.focusIntroduction);
        titleText.text = presentation != null ? presentation.title : step.title;
        TutorialStepDefinition completedStep = director.LastCompletedStep;
        TutorialStepPresentation completedPresentation =
            completedStep != null && authoring != null
                ? authoring.Find(completedStep.id)
                : null;
        completionText.text = completedStep != null
            ? $"✓ 做得好：{(completedPresentation != null ? completedPresentation.successSignal : completedStep.successSignal)}"
            : "先看高光区域，再完成这一小步";
        instructionText.text = presentation != null
            ? presentation.BuildGuideText()
            : step.BuildGuideText();
        progressText.text = $"{(presentation != null ? presentation.sectionLabel : step.sectionLabel)} · 第 {director.CompletedStepCount + 1}/{director.StepCount} 步";
        SetContinueState(
            step.allowManualAdvance,
            step.allowManualAdvance
                ? (string.IsNullOrWhiteSpace(
                        presentation != null
                            ? presentation.manualAdvanceLabel
                            : step.manualAdvanceLabel)
                    ? "继续"
                    : presentation != null
                        ? presentation.manualAdvanceLabel
                        : step.manualAdvanceLabel)
                : "等待本步操作");
        SetModeState(true, "跳过引导");
    }

    private void Build(TMP_FontAsset font)
    {
        ClearChildren();
        var factory = new RaceUIFactory(font);
        titleText = factory.CreateText(transform, "TutorialTitle", "", 24,
            new Vector2(0f, 190f), new Vector2(500f, 34f));
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.35f, 0.82f, 1f);

        completionText = factory.CreateText(transform, "TutorialCompletion", "", 14,
            new Vector2(0f, 158f), new Vector2(500f, 26f));
        completionText.alignment = TextAlignmentOptions.Center;
        completionText.color = new Color(0.45f, 0.92f, 0.62f);

        instructionText = factory.CreateText(transform, "TutorialInstruction", "", 15,
            new Vector2(0f, 34f), new Vector2(500f, 224f));
        instructionText.alignment = TextAlignmentOptions.TopLeft;
        instructionText.enableWordWrapping = true;
        instructionText.richText = true;
        instructionText.lineSpacing = 3f;
        instructionText.color = Color.white;

        progressText = factory.CreateText(transform, "TutorialProgress", "", 14,
            new Vector2(0f, -96f), new Vector2(260f, 26f));
        progressText.alignment = TextAlignmentOptions.Center;
        progressText.color = new Color(0.75f, 0.82f, 0.9f);

        continueButton = factory.CreateActionButton(transform, "TutorialContinueButton", "继续",
            new Vector2(0f, -174f), new Color(0.25f, 0.68f, 0.95f), OnContinue);
        RectTransform buttonRect = continueButton.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(142f, 42f);
        continueLabel = continueButton.GetComponentInChildren<TMP_Text>(true);
        continueLabel.fontSize = 16f;

        modeButton = factory.CreateActionButton(transform, "TutorialModeButton", "跳过引导",
            new Vector2(-170f, -174f), new Color(0.24f, 0.42f, 0.62f), OnModeAction);
        modeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(142f, 42f);
        modeLabel = modeButton.GetComponentInChildren<TMP_Text>(true);
        modeLabel.fontSize = 15f;

        exitButton = factory.CreateActionButton(transform, "TutorialExitButton", "退出教程",
            new Vector2(170f, -174f), new Color(0.55f, 0.28f, 0.28f), OnExit);
        exitButton.GetComponent<RectTransform>().sizeDelta = new Vector2(142f, 42f);
        exitButton.GetComponentInChildren<TMP_Text>(true).fontSize = 15f;

        collapseButton = factory.CreateActionButton(transform, "TutorialCollapseButton", "收起指引",
            new Vector2(220f, 190f), new Color(0.18f, 0.32f, 0.48f), OnToggleExpanded);
        collapseButton.GetComponent<RectTransform>().sizeDelta = new Vector2(84f, 30f);
        collapseLabel = collapseButton.GetComponentInChildren<TMP_Text>(true);
        collapseLabel.fontSize = 13f;

        ApplyLayout();
    }

    private void ApplyLayout()
    {
        if (titleText == null || collapseButton == null)
            return;

        if (useAuthoredLayout)
        {
            ApplyAuthoredLayout();
            return;
        }

        TutorialGuideLayout layout = TutorialGuideLayoutRules.Resolve(
            Screen.width,
            Screen.height,
            isExpanded);
        RectTransform panelRect = (RectTransform)transform;
        panelRect.anchoredPosition = new Vector2(-layout.Margin, -layout.Margin);
        panelRect.sizeDelta = new Vector2(layout.Width, layout.Height);

        float halfWidth = layout.Width * 0.5f;
        float halfHeight = layout.Height * 0.5f;
        RectTransform collapseRect = collapseButton.GetComponent<RectTransform>();

        if (!isExpanded)
        {
            SetExpandedContentActive(false);
            SetRect(titleText.rectTransform,
                new Vector2(-40f, 24f),
                new Vector2(Mathf.Max(150f, layout.Width - 120f), 32f));
            titleText.fontSize = layout.Width < 340f ? 18f : 20f;
            SetRect(progressText.rectTransform,
                new Vector2(-40f, -22f),
                new Vector2(Mathf.Max(150f, layout.Width - 120f), 24f));
            progressText.fontSize = 12f;
            SetRect(collapseRect,
                new Vector2(halfWidth - 50f, 0f),
                new Vector2(84f, 38f));
            collapseLabel.text = "展开指引";
            return;
        }

        SetExpandedContentActive(true);
        bool compact = layout.IsCompact;
        SetRect(titleText.rectTransform,
            new Vector2(-45f, halfHeight - 30f),
            new Vector2(Mathf.Max(200f, layout.Width - 130f), 34f));
        titleText.fontSize = compact ? 20f : 24f;
        SetRect(collapseRect,
            new Vector2(halfWidth - 50f, halfHeight - 29f),
            new Vector2(84f, 30f));
        collapseLabel.text = "收起指引";

        SetRect(completionText.rectTransform,
            new Vector2(0f, halfHeight - 62f),
            new Vector2(Mathf.Max(220f, layout.Width - 40f), 26f));
        completionText.fontSize = compact ? 12f : 14f;

        float instructionTop = halfHeight - 82f;
        float instructionBottom = -halfHeight + 126f;
        float instructionHeight = Mathf.Max(100f, instructionTop - instructionBottom);
        SetRect(instructionText.rectTransform,
            new Vector2(0f, (instructionTop + instructionBottom) * 0.5f),
            new Vector2(Mathf.Max(220f, layout.Width - 40f), instructionHeight));
        instructionText.fontSize = layout.InstructionFontSize;

        SetRect(progressText.rectTransform,
            new Vector2(0f, -halfHeight + 116f),
            new Vector2(Mathf.Min(300f, layout.Width - 40f), 26f));
        progressText.fontSize = compact ? 12f : 14f;

        float buttonWidth = Mathf.Min(142f, Mathf.Max(86f, (layout.Width - 64f) / 3f));
        float sideOffset = buttonWidth + 12f;
        float buttonY = -halfHeight + 46f;
        SetRect(continueButton.GetComponent<RectTransform>(),
            new Vector2(0f, buttonY), new Vector2(buttonWidth, 42f));
        SetRect(modeButton.GetComponent<RectTransform>(),
            new Vector2(-sideOffset, buttonY), new Vector2(buttonWidth, 42f));
        SetRect(exitButton.GetComponent<RectTransform>(),
            new Vector2(sideOffset, buttonY), new Vector2(buttonWidth, 42f));
        continueLabel.fontSize = compact ? 13f : 16f;
        modeLabel.fontSize = compact ? 12f : 15f;
        exitButton.GetComponentInChildren<TMP_Text>(true).fontSize = compact ? 12f : 15f;
    }

    private void SetExpandedContentActive(bool active)
    {
        completionText.gameObject.SetActive(active);
        instructionText.gameObject.SetActive(active);
        continueButton.gameObject.SetActive(active);
        modeButton.gameObject.SetActive(active);
        exitButton.gameObject.SetActive(active);
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        if (rect == null)
            return;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
    }

    private void BindButtonListeners()
    {
        BindButton(continueButton, OnContinue);
        BindButton(modeButton, OnModeAction);
        BindButton(exitButton, OnExit);
        BindButton(collapseButton, OnToggleExpanded);
    }

    private static void BindButton(Button button, UnityEngine.Events.UnityAction action)
    {
        if (button == null)
            return;
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(action);
    }

    private void CaptureAuthoredLayout()
    {
        if (!useAuthoredLayout || authoredLayoutCaptured || titleText == null ||
            progressText == null || collapseButton == null)
            return;

        panelSnapshot = RectTransformSnapshot.Capture((RectTransform)transform);
        titleSnapshot = RectTransformSnapshot.Capture(titleText.rectTransform);
        progressSnapshot = RectTransformSnapshot.Capture(progressText.rectTransform);
        collapseSnapshot = RectTransformSnapshot.Capture(
            collapseButton.GetComponent<RectTransform>());
        authoredTitleFontSize = titleText.fontSize;
        authoredProgressFontSize = progressText.fontSize;
        authoredLayoutCaptured = true;
    }

    private void ApplyAuthoredLayout()
    {
        CaptureAuthoredLayout();
        if (!authoredLayoutCaptured)
            return;

        if (isExpanded)
        {
            SetExpandedContentActive(true);
            panelSnapshot.Restore();
            titleSnapshot.Restore();
            progressSnapshot.Restore();
            collapseSnapshot.Restore();
            titleText.fontSize = authoredTitleFontSize;
            progressText.fontSize = authoredProgressFontSize;
            collapseLabel.text = "收起指引";
            return;
        }

        SetExpandedContentActive(false);
        RectTransform panelRect = (RectTransform)transform;
        panelRect.sizeDelta = new Vector2(panelSnapshot.Size.x, 104f);
        float halfWidth = panelSnapshot.Size.x * 0.5f;
        SetRect(titleText.rectTransform,
            new Vector2(-40f, 24f),
            new Vector2(Mathf.Max(150f, panelSnapshot.Size.x - 120f), 32f));
        SetRect(progressText.rectTransform,
            new Vector2(-40f, -22f),
            new Vector2(Mathf.Max(150f, panelSnapshot.Size.x - 120f), 24f));
        SetRect(collapseButton.GetComponent<RectTransform>(),
            new Vector2(halfWidth - 50f, 0f),
            new Vector2(84f, 38f));
        collapseLabel.text = "展开指引";
    }

    private readonly struct RectTransformSnapshot
    {
        private readonly RectTransform rect;
        public Vector2 Position { get; }
        public Vector2 Size { get; }

        private RectTransformSnapshot(RectTransform rect, Vector2 position, Vector2 size)
        {
            this.rect = rect;
            Position = position;
            Size = size;
        }

        public static RectTransformSnapshot Capture(RectTransform rect)
        {
            return new RectTransformSnapshot(rect, rect.anchoredPosition, rect.sizeDelta);
        }

        public void Restore()
        {
            if (rect == null)
                return;
            rect.anchoredPosition = Position;
            rect.sizeDelta = Size;
        }
    }

    private void SetContinueState(bool interactable, string label)
    {
        if (continueButton != null)
            continueButton.interactable = interactable;
        if (continueLabel != null)
            continueLabel.text = label;
    }

    private void SetModeState(bool interactable, string label)
    {
        if (modeButton != null)
            modeButton.interactable = interactable;
        if (modeLabel != null)
            modeLabel.text = label;
        if (exitButton != null)
            exitButton.interactable = true;
    }

    private void OnContinue()
    {
        if (primaryRestartsPractice)
            manager?.RestartTutorialPracticeLap();
        else
            manager?.OnTutorialContinueClicked();
    }

    private void OnModeAction()
    {
        TutorialRuntimeDirector director = manager != null ? manager.TutorialDirector : null;
        if (director == null)
            return;

        if (director.Phase == TutorialRunPhase.Guided)
            manager.SkipTutorialGuidedSection();
        else
            manager.RestartTutorialGuidedSection();
    }

    private void OnExit()
    {
        manager?.ExitTutorialToMainMenu();
    }

    private void OnToggleExpanded()
    {
        isExpanded = !isExpanded;
        ApplyLayout();
    }
}
