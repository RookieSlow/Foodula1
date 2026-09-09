using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Runtime-built settings overlay for the current uGUI menu.</summary>
public sealed class GameSettingsUI : MonoBehaviour
{
    private static readonly float[] AnimationSpeeds = { 0.5f, 1f, 1.5f, 2f };
    private static readonly InRaceConfirmationAction[] ConfirmationActions =
    {
        InRaceConfirmationAction.GearSelection,
        InRaceConfirmationAction.GearCommit,
        InRaceConfirmationAction.CardAction,
        InRaceConfirmationAction.DriverSkill,
        InRaceConfirmationAction.ResetRace,
        InRaceConfirmationAction.ReturnToMenu,
        InRaceConfirmationAction.PitDecision,
        InRaceConfirmationAction.LaneChange
    };

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private Action onTutorialReset;
    private GameEncyclopediaUI encyclopediaUI;
    private GameObject overlay;
    private GameSettingsData working;
    private int resolutionIndex;
    private TMP_Text masterValue;
    private TMP_Text musicValue;
    private TMP_Text sfxValue;
    private TMP_Text displayValue;
    private TMP_Text resolutionValue;
    private TMP_Text animationValue;
    private TMP_Text motionValue;
    private TMP_Text tutorialValue;
    private TMP_Text statusValue;
    private GameObject confirmationPanel;
    private readonly Dictionary<InRaceConfirmationAction, TMP_Text> confirmationValues =
        new Dictionary<InRaceConfirmationAction, TMP_Text>();

    public void Initialize(Action tutorialResetCallback)
    {
        onTutorialReset = tutorialResetCallback;
        encyclopediaUI = GetComponent<GameEncyclopediaUI>();
        if (encyclopediaUI == null)
            encyclopediaUI = gameObject.AddComponent<GameEncyclopediaUI>();
        encyclopediaUI.Initialize();
        if (overlay == null)
            Build();
        Hide();
    }

    public void Show()
    {
        if (overlay == null)
            Build();
        working = GameSettingsRuntime.Current.Clone();
        BuildResolutionList();
        RefreshValues();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (overlay != null)
            overlay.SetActive(false);
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
        AudioService.ApplySettings(GameSettingsRuntime.Current);
    }

    private void Build()
    {
        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(true)?.font;
        var factory = new RaceUIFactory(font);

        overlay = new GameObject("SettingsOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        RectTransform overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        ModernUIStyle.ApplyOverlay(overlay.GetComponent<Image>());

        GameObject panel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(overlay.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(920f, 820f);
        ModernUIStyle.ApplyPanel(panel, true);

        TMP_Text title = factory.CreateText(panel.transform, "SettingsTitle", "设置", 36,
            new Vector2(0f, 360f), new Vector2(780f, 54f));
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.35f, 0.82f, 1f);

        CreateStepper(factory, panel.transform, "主音量", 270f,
            out masterValue, () => ChangeVolume(VolumeChannel.Master, -0.1f),
            () => ChangeVolume(VolumeChannel.Master, 0.1f));
        CreateStepper(factory, panel.transform, "音乐音量", 205f,
            out musicValue, () => ChangeVolume(VolumeChannel.Music, -0.1f),
            () => ChangeVolume(VolumeChannel.Music, 0.1f));
        CreateStepper(factory, panel.transform, "音效音量", 140f,
            out sfxValue, () => ChangeVolume(VolumeChannel.Sfx, -0.1f),
            () => ChangeVolume(VolumeChannel.Sfx, 0.1f));

        TMP_Text audioNote = factory.CreateText(panel.transform, "AudioNote",
            "音量调整实时试听；UI 声音沿用音效音量，保存后持久化。", 14,
            new Vector2(0f, 96f), new Vector2(800f, 30f));
        audioNote.alignment = TextAlignmentOptions.Center;
        audioNote.color = new Color(0.72f, 0.78f, 0.86f);

        CreateToggleRow(factory, panel.transform, "显示模式", 42f, out displayValue, ToggleFullscreen);
        CreateStepper(factory, panel.transform, "分辨率", -23f, out resolutionValue,
            () => CycleResolution(-1), () => CycleResolution(1));
        CreateStepper(factory, panel.transform, "动画速度", -88f, out animationValue,
            () => CycleAnimation(-1), () => CycleAnimation(1));
        CreateToggleRow(factory, panel.transform, "减少动态效果", -153f, out motionValue, ToggleMotion);
        CreateToggleRow(factory, panel.transform, "教程状态", -218f, out tutorialValue, ResetTutorial);

        Button confirmation = factory.CreateActionButton(panel.transform, "OpenInRaceConfirmations", "局内二次确认",
            new Vector2(-150f, -268f), new Color(0.62f, 0.34f, 0.72f), ShowConfirmationSettings);
        confirmation.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 44f);

        Button encyclopedia = factory.CreateActionButton(panel.transform, "OpenEncyclopedia", "游戏百科",
            new Vector2(150f, -268f), new Color(0.2f, 0.48f, 0.65f), encyclopediaUI.Show);
        encyclopedia.GetComponent<RectTransform>().sizeDelta = new Vector2(260f, 44f);

        Button defaults = factory.CreateActionButton(panel.transform, "SettingsDefaults", "恢复默认",
            new Vector2(-245f, -332f), new Color(0.33f, 0.37f, 0.44f), RestoreDefaults);
        defaults.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 48f);
        Button apply = factory.CreateActionButton(panel.transform, "SettingsApply", "保存并应用",
            new Vector2(0f, -332f), new Color(0.18f, 0.62f, 0.38f), SaveAndApply);
        apply.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 48f);
        Button close = factory.CreateActionButton(panel.transform, "SettingsClose", "关闭",
            new Vector2(245f, -332f), new Color(0.52f, 0.25f, 0.25f), Hide);
        close.GetComponent<RectTransform>().sizeDelta = new Vector2(190f, 48f);

        statusValue = factory.CreateText(panel.transform, "SettingsStatus", "", 15,
            new Vector2(0f, -382f), new Vector2(800f, 28f));
        statusValue.alignment = TextAlignmentOptions.Center;
        statusValue.color = new Color(0.45f, 0.92f, 0.68f);

        BuildConfirmationSettingsPanel(factory);
    }

    private void BuildConfirmationSettingsPanel(RaceUIFactory factory)
    {
        confirmationValues.Clear();
        confirmationPanel = new GameObject(
            "InRaceConfirmationSettingsPanel",
            typeof(RectTransform),
            typeof(Image));
        confirmationPanel.transform.SetParent(overlay.transform, false);
        RectTransform overlayRect = confirmationPanel.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;
        Image overlayImage = confirmationPanel.GetComponent<Image>();
        overlayImage.color = new Color(0.005f, 0.012f, 0.025f, 0.82f);
        overlayImage.raycastTarget = true;

        GameObject card = new GameObject("InRaceConfirmationSettingsCard", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(confirmationPanel.transform, false);
        RectTransform cardRect = card.GetComponent<RectTransform>();
        cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
        cardRect.anchoredPosition = Vector2.zero;
        cardRect.sizeDelta = new Vector2(720f, 540f);
        ModernUIStyle.ApplyPanel(card, true);

        TMP_Text title = factory.CreateText(card.transform, "InRaceConfirmationTitle", "局内二次确认", 30,
            new Vector2(0f, 224f), new Vector2(620f, 44f));
        title.alignment = TextAlignmentOptions.Center;
        title.fontStyle = FontStyles.Bold;
        title.color = new Color(0.72f, 0.48f, 1f);

        TMP_Text note = factory.CreateText(card.transform, "InRaceConfirmationNote",
            "玩家可以逐项决定哪些局内操作需要再次确认；关闭后点击会立即执行。",
            15, new Vector2(0f, 184f), new Vector2(640f, 30f));
        note.alignment = TextAlignmentOptions.Center;
        note.color = ModernUIStyle.TextSecondary;

        for (int i = 0; i < ConfirmationActions.Length; i++)
        {
            InRaceConfirmationAction action = ConfirmationActions[i];
            int column = i % 2;
            int row = i / 2;
            float x = column == 0 ? -176f : 176f;
            float y = 126f - row * 58f;
            string objectSuffix = action.ToString();

            TMP_Text label = factory.CreateText(card.transform,
                "Confirmation" + objectSuffix + "Label",
                InRaceConfirmationRules.GetDisplayName(action),
                17, new Vector2(x - 56f, y), new Vector2(180f, 34f));
            label.alignment = TextAlignmentOptions.MidlineLeft;

            Button toggle = factory.CreateActionButton(card.transform,
                "Confirmation" + objectSuffix + "Toggle", "",
                new Vector2(x + 76f, y), new Color(0.2f, 0.42f, 0.62f),
                () => ToggleConfirmation(action));
            toggle.GetComponent<RectTransform>().sizeDelta = new Vector2(108f, 36f);
            TMP_Text value = toggle.GetComponentInChildren<TMP_Text>(true);
            if (value != null)
                value.fontSize = 16f;
            confirmationValues[action] = value;
        }

        Button close = factory.CreateActionButton(card.transform, "CloseInRaceConfirmations",
            "返回设置", new Vector2(0f, -198f), ModernUIStyle.AccentBlue, HideConfirmationSettings);
        close.GetComponent<RectTransform>().sizeDelta = new Vector2(220f, 44f);
        confirmationPanel.SetActive(false);
    }

    private void ShowConfirmationSettings()
    {
        if (working == null)
            working = GameSettingsRuntime.Current.Clone();
        if (confirmationPanel == null)
            Build();
        RefreshConfirmationValues();
        if (confirmationPanel != null)
        {
            confirmationPanel.transform.SetAsLastSibling();
            confirmationPanel.SetActive(true);
        }
    }

    private void HideConfirmationSettings()
    {
        if (confirmationPanel != null)
            confirmationPanel.SetActive(false);
    }

    private void ToggleConfirmation(InRaceConfirmationAction action)
    {
        if (working == null)
            return;
        int mask = InRaceConfirmationRules.NormalizeMask(working.inRaceConfirmationMask);
        int bit = (int)action;
        working.inRaceConfirmationMask = (mask & bit) != 0
            ? mask & ~bit
            : mask | bit;
        RefreshConfirmationValues();
    }

    private void RefreshConfirmationValues()
    {
        if (working == null)
            return;
        foreach (KeyValuePair<InRaceConfirmationAction, TMP_Text> pair in confirmationValues)
        {
            if (pair.Value != null)
                pair.Value.text = working.IsInRaceConfirmationEnabled(pair.Key) ? "开启" : "关闭";
        }
    }

    private static void CreateStepper(
        RaceUIFactory factory,
        Transform parent,
        string label,
        float y,
        out TMP_Text valueText,
        UnityEngine.Events.UnityAction previous,
        UnityEngine.Events.UnityAction next)
    {
        TMP_Text rowLabel = factory.CreateText(parent, label.Replace("（预留）", "") + "Label", label, 18,
            new Vector2(-255f, y), new Vector2(300f, 38f));
        rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
        valueText = factory.CreateText(parent, label.Replace("（预留）", "") + "Value", "", 18,
            new Vector2(80f, y), new Vector2(210f, 38f));
        valueText.alignment = TextAlignmentOptions.Center;

        Button left = factory.CreateActionButton(parent, label + "Previous", "−",
            new Vector2(-60f, y), new Color(0.2f, 0.4f, 0.58f), previous);
        left.GetComponent<RectTransform>().sizeDelta = new Vector2(52f, 38f);
        Button right = factory.CreateActionButton(parent, label + "Next", "+",
            new Vector2(220f, y), new Color(0.2f, 0.4f, 0.58f), next);
        right.GetComponent<RectTransform>().sizeDelta = new Vector2(52f, 38f);
    }

    private static void CreateToggleRow(
        RaceUIFactory factory,
        Transform parent,
        string label,
        float y,
        out TMP_Text valueText,
        UnityEngine.Events.UnityAction toggle)
    {
        TMP_Text rowLabel = factory.CreateText(parent, label + "Label", label, 18,
            new Vector2(-255f, y), new Vector2(300f, 38f));
        rowLabel.alignment = TextAlignmentOptions.MidlineLeft;
        Button button = factory.CreateActionButton(parent, label + "Toggle", "",
            new Vector2(105f, y), new Color(0.2f, 0.4f, 0.58f), toggle);
        button.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 40f);
        valueText = button.GetComponentInChildren<TMP_Text>(true);
        valueText.fontSize = 17f;
    }

    private enum VolumeChannel { Master, Music, Sfx }

    private void ChangeVolume(VolumeChannel channel, float delta)
    {
        if (working == null) return;
        switch (channel)
        {
            case VolumeChannel.Master:
                working.masterVolume = Mathf.Clamp01(working.masterVolume + delta);
                break;
            case VolumeChannel.Music:
                working.musicVolume = Mathf.Clamp01(working.musicVolume + delta);
                break;
            case VolumeChannel.Sfx:
                working.soundEffectsVolume = Mathf.Clamp01(working.soundEffectsVolume + delta);
                break;
        }
        AudioService.ApplySettings(working);
        RefreshValues();
    }

    private void ToggleFullscreen()
    {
        working.fullscreen = !working.fullscreen;
        RefreshValues();
    }

    private void ToggleMotion()
    {
        working.reduceMotion = !working.reduceMotion;
        RefreshValues();
    }

    private void ResetTutorial()
    {
        working.tutorialCompleted = false;
        GameSettingsRuntime.ResetTutorialProgress();
        onTutorialReset?.Invoke();
        statusValue.text = "教程完成标记已重置，可从主菜单重新开始。";
        RefreshValues(keepStatus: true);
    }

    private void CycleResolution(int direction)
    {
        if (resolutions.Count == 0) return;
        resolutionIndex = (resolutionIndex + direction + resolutions.Count) % resolutions.Count;
        Vector2Int selected = resolutions[resolutionIndex];
        working.resolutionWidth = selected.x;
        working.resolutionHeight = selected.y;
        RefreshValues();
    }

    private void CycleAnimation(int direction)
    {
        int currentIndex = 0;
        float normalized = GameSettingsData.NormalizeAnimationSpeed(working.animationSpeed);
        for (int i = 0; i < AnimationSpeeds.Length; i++)
            if (Mathf.Approximately(AnimationSpeeds[i], normalized)) currentIndex = i;
        currentIndex = (currentIndex + direction + AnimationSpeeds.Length) % AnimationSpeeds.Length;
        working.animationSpeed = AnimationSpeeds[currentIndex];
        RefreshValues();
    }

    private void SaveAndApply()
    {
        GameSettingsRuntime.SaveAndApply(working);
        working = GameSettingsRuntime.Current.Clone();
        statusValue.text = "设置已保存并实时应用。";
        RefreshValues(keepStatus: true);
    }

    private void RestoreDefaults()
    {
        bool tutorialCompleted = working != null && working.tutorialCompleted;
        working = GameSettingsData.CreateDefault(Screen.width, Screen.height);
        working.tutorialCompleted = tutorialCompleted;
        BuildResolutionList();
        AudioService.ApplySettings(working);
        statusValue.text = "已载入默认值；点击“保存并应用”后持久化。";
        RefreshValues(keepStatus: true);
    }

    private void BuildResolutionList()
    {
        resolutions.Clear();
        AddResolution(1280, 720);
        AddResolution(1600, 900);
        AddResolution(1920, 1080);
        AddResolution(2560, 1440);
        AddResolution(working.resolutionWidth, working.resolutionHeight);
        resolutions.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
        resolutionIndex = resolutions.FindIndex(item =>
            item.x == working.resolutionWidth && item.y == working.resolutionHeight);
        if (resolutionIndex < 0) resolutionIndex = 0;
    }

    private void AddResolution(int width, int height)
    {
        if (width < 640 || height < 360) return;
        if (!resolutions.Exists(item => item.x == width && item.y == height))
            resolutions.Add(new Vector2Int(width, height));
    }

    private void RefreshValues(bool keepStatus = false)
    {
        if (working == null) return;
        working.Normalize(Screen.width, Screen.height);
        masterValue.text = $"{Mathf.RoundToInt(working.masterVolume * 100f)}%";
        musicValue.text = $"{Mathf.RoundToInt(working.musicVolume * 100f)}%";
        sfxValue.text = $"{Mathf.RoundToInt(working.soundEffectsVolume * 100f)}%";
        displayValue.text = working.fullscreen ? "全屏窗口" : "窗口模式";
        resolutionValue.text = $"{working.resolutionWidth} × {working.resolutionHeight}";
        animationValue.text = $"{working.animationSpeed:0.0}×";
        motionValue.text = working.reduceMotion ? "开启（跳过可选动态）" : "关闭";
        tutorialValue.text = working.tutorialCompleted ? "已完成 · 点击重置" : "可重播 · 点击重置";
        RefreshConfirmationValues();
        if (!keepStatus && statusValue != null)
            statusValue.text = string.Empty;
    }
}
