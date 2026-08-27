using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built tutorial guide panel. It presents authored scenario text and
/// delegates every state change to MVPGameManager/TutorialRuntimeDirector.
/// </summary>
public sealed class TutorialGuideUI : MonoBehaviour
{
    private MVPGameManager manager;
    private TMP_Text titleText;
    private TMP_Text instructionText;
    private TMP_Text progressText;
    private Button continueButton;
    private TMP_Text continueLabel;
    private Button modeButton;
    private TMP_Text modeLabel;
    private Button exitButton;
    private bool primaryRestartsPractice;

    public static TutorialGuideUI Create(
        MVPGameManager manager,
        Canvas canvas,
        TMP_FontAsset font)
    {
        if (manager == null || canvas == null)
            return null;

        Transform existing = canvas.transform.Find("TutorialGuidePanel");
        GameObject panel = existing != null
            ? existing.gameObject
            : new GameObject("TutorialGuidePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        panel.transform.SetAsLastSibling();

        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.anchoredPosition = new Vector2(-24f, -24f);
        rect.sizeDelta = new Vector2(500f, 304f);
        panel.GetComponent<Image>().color = new Color(0.035f, 0.055f, 0.09f, 0.96f);

        TutorialGuideUI guide = panel.GetComponent<TutorialGuideUI>();
        if (guide == null)
            guide = panel.AddComponent<TutorialGuideUI>();
        guide.manager = manager;
        guide.Build(font);
        guide.Refresh();
        return guide;
    }

    public void Refresh()
    {
        TutorialRuntimeDirector director = manager != null ? manager.TutorialDirector : null;
        if (director == null)
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);
        TutorialStepDefinition step = director.CurrentStep;
        if (step == null)
        {
            bool completed = director.Phase == TutorialRunPhase.Completed;
            titleText.text = completed ? "练习圈完成" : "勒芒自由练习";
            instructionText.text = completed
                ? "你已完成一整圈勒芒练习。该结果不会发放 RP、车手 XP、解锁或赛事进度。可以再练一圈、重播引导或退出。"
                : "状态已重置为 UK、零科技和教程精确牌组。自由完成一整圈；本圈使用脚本阴天，仍不写入正常奖励与进度。";
            progressText.text = $"{director.CompletedStepCount}/{director.StepCount}";
            primaryRestartsPractice = true;
            SetContinueState(true, completed ? "再练一圈" : "重新开始");
            SetModeState(true, "重播引导");
            return;
        }

        primaryRestartsPractice = false;
        titleText.text = step.title;
        instructionText.text = step.instruction;
        progressText.text = $"步骤 {director.CompletedStepCount + 1}/{director.StepCount}";
        SetContinueState(
            step.allowManualAdvance,
            step.allowManualAdvance ? "继续" : "请完成实际操作");
        SetModeState(true, "跳过引导");
    }

    private void Build(TMP_FontAsset font)
    {
        ClearChildren();
        var factory = new RaceUIFactory(font);
        titleText = factory.CreateText(transform, "TutorialTitle", "", 24,
            new Vector2(0f, 122f), new Vector2(450f, 34f));
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = new Color(0.35f, 0.82f, 1f);

        instructionText = factory.CreateText(transform, "TutorialInstruction", "", 17,
            new Vector2(0f, 45f), new Vector2(450f, 116f));
        instructionText.alignment = TextAlignmentOptions.TopLeft;
        instructionText.enableWordWrapping = true;
        instructionText.color = Color.white;

        progressText = factory.CreateText(transform, "TutorialProgress", "", 14,
            new Vector2(0f, -42f), new Vector2(220f, 26f));
        progressText.alignment = TextAlignmentOptions.Center;
        progressText.color = new Color(0.75f, 0.82f, 0.9f);

        continueButton = factory.CreateActionButton(transform, "TutorialContinueButton", "继续",
            new Vector2(0f, -108f), new Color(0.25f, 0.68f, 0.95f), OnContinue);
        RectTransform buttonRect = continueButton.GetComponent<RectTransform>();
        buttonRect.sizeDelta = new Vector2(142f, 42f);
        continueLabel = continueButton.GetComponentInChildren<TMP_Text>(true);
        continueLabel.fontSize = 16f;

        modeButton = factory.CreateActionButton(transform, "TutorialModeButton", "跳过引导",
            new Vector2(-164f, -108f), new Color(0.24f, 0.42f, 0.62f), OnModeAction);
        modeButton.GetComponent<RectTransform>().sizeDelta = new Vector2(142f, 42f);
        modeLabel = modeButton.GetComponentInChildren<TMP_Text>(true);
        modeLabel.fontSize = 15f;

        exitButton = factory.CreateActionButton(transform, "TutorialExitButton", "退出教程",
            new Vector2(164f, -108f), new Color(0.55f, 0.28f, 0.28f), OnExit);
        exitButton.GetComponent<RectTransform>().sizeDelta = new Vector2(142f, 42f);
        exitButton.GetComponentInChildren<TMP_Text>(true).fontSize = 15f;
    }

    private void ClearChildren()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
            Destroy(transform.GetChild(i).gameObject);
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
}
