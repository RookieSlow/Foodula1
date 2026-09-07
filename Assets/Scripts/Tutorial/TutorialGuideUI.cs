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
    public static TutorialGuideLayout Resolve(
        int screenWidth,
        int screenHeight,
        bool expanded,
        float preferredExpandedHeight = 0f)
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
        float defaultExpandedHeight = Mathf.Clamp(height * 0.72f, 340f, 440f);
        float expandedHeight = Mathf.Min(
            Mathf.Max(defaultExpandedHeight, preferredExpandedHeight),
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
    [SerializeField] private Button previousButton;
    [SerializeField] private TMP_Text previousLabel;
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
    private RectTransformSnapshot completionSnapshot;
    private RectTransformSnapshot instructionSnapshot;
    private RectTransformSnapshot continueSnapshot;
    private RectTransformSnapshot previousSnapshot;
    private RectTransformSnapshot modeSnapshot;
    private RectTransformSnapshot exitSnapshot;
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
        EnsurePreviousButton();
        CaptureAuthoredLayout();
        BindButtonListeners();
    }

    public void ConfigureAsAuthoredLayout(bool enabled)
    {
        useAuthoredLayout = enabled;
        authoredLayoutCaptured = false;
    }

    public void SuspendPresentation()
    {
        focusHighlighter?.Hide();
        gameObject.SetActive(false);
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
        titleText.text = presentation.title;
        SetOptionalText(completionText, string.Empty);
        instructionText.text = presentation.BuildGuideText();
        progressText.text =
            $"{presentation.sectionLabel} · 第 {oneBasedIndex}/{totalSteps} 步";
        SetContinueState(
            !string.IsNullOrWhiteSpace(presentation.manualAdvanceLabel),
            string.IsNullOrWhiteSpace(presentation.manualAdvanceLabel)
                ? "等待本步操作"
                : presentation.manualAdvanceLabel);
        SetPreviousState(false);
        SetModeState(true, "跳过引导");
        ApplyLayout();
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
        titleText.text = previewTitle;
        SetOptionalText(completionText, previewCompletion);
        instructionText.text = previewInstruction;
        progressText.text = $"{totalSteps}/{totalSteps}";
        SetContinueState(true, completed ? "再练一圈" : "重新开始");
        SetPreviousState(false);
        SetModeState(true, "重播引导");
        ApplyLayout();
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
        TutorialStepDefinition step = director.CurrentStep;
        if (step == null)
        {
            focusHighlighter?.Hide();
            bool completed = director.Phase == TutorialRunPhase.Completed;
            titleText.text = authoring != null
                ? authoring.GetPracticeTitle(completed)
                : completed ? "训练圈完成" : "勒芒自由练习";
            SetOptionalText(completionText, authoring != null
                ? authoring.GetPracticeCompletion(completed)
                : completed
                    ? "✓ 做得漂亮，你完成了整圈训练"
                    : "✓ 赛车和牌组都已重新准备好");
            instructionText.text = authoring != null
                ? authoring.GetPracticeInstruction(completed)
                : completed
                    ? "很好，新车手。你已经独立完成一整圈勒芒训练。这里不会发放 RP、车手 XP 或解锁；想再练一圈、重温引导或回到主菜单都可以。"
                    : "接下来由你自己做决定。用准备好的 UK 赛车和教程牌组跑完一圈；这只是训练，不会影响正常奖励或赛事进度。";
            progressText.text = $"{director.CompletedStepCount}/{director.StepCount}";
            primaryRestartsPractice = true;
            SetContinueState(true, completed ? "再练一圈" : "重新开始");
            SetPreviousState(false);
            SetModeState(true, "重播引导");
            ApplyLayout();
            return;
        }

        primaryRestartsPractice = false;
        TutorialStepPresentation presentation = authoring != null
            ? authoring.Find(step.id)
            : null;
        focusHighlighter?.Show(
            step.id,
            step.focusTarget,
            presentation != null ? presentation.focusIntroduction : step.focusIntroduction);
        titleText.text = presentation != null ? presentation.title : step.title;
        TutorialStepDefinition completedStep = director.LastCompletedStep;
        TutorialStepPresentation completedPresentation =
            completedStep != null && authoring != null
                ? authoring.Find(completedStep.id)
                : null;
        string completedMessage = completedPresentation != null
            ? completedPresentation.successSignal
            : completedStep != null
                ? completedStep.successSignal
                : string.Empty;
        SetOptionalText(
            completionText,
            string.IsNullOrWhiteSpace(completedMessage)
                ? string.Empty
                : $"✓ {completedMessage}");
        instructionText.text = presentation != null
            ? presentation.BuildGuideText()
            : step.BuildGuideText();
        progressText.text = $"{(presentation != null ? presentation.sectionLabel : step.sectionLabel)} · 第 {director.CurrentStepIndex + 1}/{director.StepCount} 步";
        SetContinueState(
            director.CanGoNext,
            director.CanGoNext ? "下一步" : "完成操作后下一步");
        SetPreviousState(director.CanGoPrevious);
        SetModeState(true, "跳过引导");
        ApplyLayout();
    }

    private static void SetOptionalText(TMP_Text target, string value)
    {
        if (target == null)
            return;
        bool visible = !string.IsNullOrWhiteSpace(value);
        target.gameObject.SetActive(visible);
        target.text = visible ? value.Trim() : string.Empty;
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

        previousButton = factory.CreateActionButton(transform, "TutorialPreviousButton", "上一步",
            new Vector2(-180f, -174f), new Color(0.22f, 0.36f, 0.54f), OnPrevious);
        previousButton.GetComponent<RectTransform>().sizeDelta = new Vector2(112f, 42f);
        previousLabel = previousButton.GetComponentInChildren<TMP_Text>(true);
        previousLabel.fontSize = 14f;

        modeButton = factory.CreateActionButton(transform, "TutorialModeButton", "跳过引导",
            new Vector2(60f, -174f), new Color(0.24f, 0.42f, 0.62f), OnModeAction);
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
            isExpanded,
            isExpanded ? CalculateRuntimePreferredHeight() : 0f);
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

        float buttonWidth = Mathf.Min(122f, Mathf.Max(76f, (layout.Width - 76f) / 4f));
        float buttonGap = 8f;
        float firstX = -1.5f * (buttonWidth + buttonGap);
        float buttonY = -halfHeight + 46f;
        SetRect(previousButton.GetComponent<RectTransform>(),
            new Vector2(firstX, buttonY), new Vector2(buttonWidth, 42f));
        SetRect(continueButton.GetComponent<RectTransform>(),
            new Vector2(firstX + buttonWidth + buttonGap, buttonY), new Vector2(buttonWidth, 42f));
        SetRect(modeButton.GetComponent<RectTransform>(),
            new Vector2(firstX + (buttonWidth + buttonGap) * 2f, buttonY), new Vector2(buttonWidth, 42f));
        SetRect(exitButton.GetComponent<RectTransform>(),
            new Vector2(firstX + (buttonWidth + buttonGap) * 3f, buttonY), new Vector2(buttonWidth, 42f));
        previousLabel.fontSize = compact ? 12f : 14f;
        continueLabel.fontSize = compact ? 13f : 16f;
        modeLabel.fontSize = compact ? 12f : 15f;
        exitButton.GetComponentInChildren<TMP_Text>(true).fontSize = compact ? 12f : 15f;
    }

    private void SetExpandedContentActive(bool active)
    {
        completionText.gameObject.SetActive(
            active && !string.IsNullOrWhiteSpace(completionText.text));
        instructionText.gameObject.SetActive(active);
        continueButton.gameObject.SetActive(active);
        if (previousButton != null)
            previousButton.gameObject.SetActive(active && previousButton.interactable);
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
        BindButton(previousButton, OnPrevious);
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
        completionSnapshot = RectTransformSnapshot.Capture(completionText.rectTransform);
        instructionSnapshot = RectTransformSnapshot.Capture(instructionText.rectTransform);
        continueSnapshot = RectTransformSnapshot.Capture(
            continueButton.GetComponent<RectTransform>());
        if (previousButton != null)
            previousSnapshot = RectTransformSnapshot.Capture(
                previousButton.GetComponent<RectTransform>());
        modeSnapshot = RectTransformSnapshot.Capture(modeButton.GetComponent<RectTransform>());
        exitSnapshot = RectTransformSnapshot.Capture(exitButton.GetComponent<RectTransform>());
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
            completionSnapshot.Restore();
            instructionSnapshot.Restore();
            continueSnapshot.Restore();
            previousSnapshot.Restore();
            modeSnapshot.Restore();
            exitSnapshot.Restore();

            float preferredInstructionHeight = ResolvePreferredTextHeight(
                instructionText,
                instructionSnapshot.Size.x,
                instructionSnapshot.Size.y);
            float safeHeight = Mathf.Max(1f, Screen.height - 24f);
            float desiredHeight = panelSnapshot.Size.y +
                                  Mathf.Max(
                                      0f,
                                      preferredInstructionHeight - instructionSnapshot.Size.y);
            float resolvedHeight = Mathf.Min(desiredHeight, safeHeight);
            float addedHeight = Mathf.Max(0f, resolvedHeight - panelSnapshot.Size.y);
            ApplyAuthoredExpandedHeight(addedHeight);
            LayoutAuthoredNavigationButtons();
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

    private float CalculateRuntimePreferredHeight()
    {
        if (instructionText == null)
            return 0f;

        float width = Mathf.Max(220f, instructionText.rectTransform.rect.width);
        float instructionHeight = ResolvePreferredTextHeight(
            instructionText,
            width,
            100f);
        return instructionHeight + 216f;
    }

    private static float ResolvePreferredTextHeight(
        TMP_Text text,
        float width,
        float minimumHeight)
    {
        if (text == null || string.IsNullOrWhiteSpace(text.text))
            return minimumHeight;

        text.ForceMeshUpdate(ignoreActiveState: true, forceTextReparsing: true);
        float preferred = text.GetPreferredValues(
            text.text,
            Mathf.Max(1f, width),
            0f).y;
        return Mathf.Max(minimumHeight, preferred + 12f);
    }

    private void ApplyAuthoredExpandedHeight(float addedHeight)
    {
        RectTransform panelRect = (RectTransform)transform;
        Vector2 panelSize = panelSnapshot.Size;
        panelSize.y += addedHeight;
        panelRect.sizeDelta = panelSize;

        // The authored panel is top anchored. Grow it downward while keeping
        // the title at the same screen position and the controls at the bottom.
        Vector2 panelPosition = panelSnapshot.Position;
        panelPosition.y -= addedHeight * (1f - panelRect.pivot.y);
        panelRect.anchoredPosition = panelPosition;

        float halfAdded = addedHeight * 0.5f;
        ShiftVertical(titleText.rectTransform, halfAdded);
        ShiftVertical(completionText.rectTransform, halfAdded);
        ShiftVertical(collapseButton.GetComponent<RectTransform>(), halfAdded);
        ShiftVertical(progressText.rectTransform, -halfAdded);
        ShiftVertical(continueButton.GetComponent<RectTransform>(), -halfAdded);
        if (previousButton != null)
            ShiftVertical(previousButton.GetComponent<RectTransform>(), -halfAdded);
        ShiftVertical(modeButton.GetComponent<RectTransform>(), -halfAdded);
        ShiftVertical(exitButton.GetComponent<RectTransform>(), -halfAdded);
        instructionText.rectTransform.sizeDelta = new Vector2(
            instructionSnapshot.Size.x,
            instructionSnapshot.Size.y + addedHeight);
    }

    private static void ShiftVertical(RectTransform rect, float delta)
    {
        if (rect == null)
            return;
        Vector2 position = rect.anchoredPosition;
        position.y += delta;
        rect.anchoredPosition = position;
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

    private void SetPreviousState(bool interactable)
    {
        if (previousButton != null)
        {
            previousButton.interactable = interactable;
            previousButton.gameObject.SetActive(isExpanded && interactable);
        }
        if (previousLabel != null)
            previousLabel.text = "上一步";
    }

    private void EnsurePreviousButton()
    {
        if (previousButton != null || modeButton == null)
            return;
        Transform existing = transform.Find("TutorialPreviousButton");
        GameObject buttonObject = existing != null
            ? existing.gameObject
            : Instantiate(modeButton.gameObject, transform, false);
        buttonObject.name = "TutorialPreviousButton";
        previousButton = buttonObject.GetComponent<Button>();
        previousLabel = buttonObject.GetComponentInChildren<TMP_Text>(true);
        if (previousLabel != null) previousLabel.text = "上一步";
    }

    private void LayoutAuthoredNavigationButtons()
    {
        if (previousButton == null) return;
        float buttonY = continueButton.GetComponent<RectTransform>().anchoredPosition.y;
        float width = Mathf.Min(112f, Mathf.Max(82f, (panelSnapshot.Size.x - 60f) / 4f));
        float gap = 8f;
        float firstX = -1.5f * (width + gap);
        SetRect(previousButton.GetComponent<RectTransform>(), new Vector2(firstX, buttonY), new Vector2(width, 42f));
        SetRect(continueButton.GetComponent<RectTransform>(), new Vector2(firstX + width + gap, buttonY), new Vector2(width, 42f));
        SetRect(modeButton.GetComponent<RectTransform>(), new Vector2(firstX + (width + gap) * 2f, buttonY), new Vector2(width, 42f));
        SetRect(exitButton.GetComponent<RectTransform>(), new Vector2(firstX + (width + gap) * 3f, buttonY), new Vector2(width, 42f));
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

    private void OnPrevious()
    {
        manager?.OnTutorialPreviousClicked();
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
