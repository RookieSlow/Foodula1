using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Second-level hub for the foundation and six team courses.</summary>
public sealed class TutorialSelectionUI : MonoBehaviour
{
    private Action startFoundationTutorial;
    private GameObject panelRoot;
    private TMP_Text courseTitle;
    private TMP_Text courseMeta;
    private TMP_Text courseBody;
    private RectTransform courseBodyRect;
    private ScrollRect courseScroll;
    private Button primaryButton;
    private TMP_Text primaryLabel;
    private TeamTutorialCourseDefinition selectedCourse;

    public void Initialize(Action onStartFoundationTutorial)
    {
        startFoundationTutorial = onStartFoundationTutorial;
        EnsurePanel();
        Hide();
    }

    public void Show()
    {
        EnsurePanel();
        panelRoot.SetActive(true);
        SelectCourse(TeamTutorialCatalog.All[0]);
    }

    public void Hide()
    {
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void EnsurePanel()
    {
        if (panelRoot != null) return;

        panelRoot = CreateRect("TutorialSelectionPanel", transform, Vector2.zero, Vector2.one);
        Image dimmer = panelRoot.AddComponent<Image>();
        dimmer.color = new Color(0.006f, 0.012f, 0.025f, 0.94f);

        GameObject window = CreateRect("Window", panelRoot.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.sizeDelta = new Vector2(1340f, 800f);
        ModernUIStyle.ApplyPanel(window, true);

        CreateText("Title", window.transform, "教程中心", 42f,
            new Vector2(0f, 346f), new Vector2(1180f, 60f), TextAlignmentOptions.Center);
        CreateText("Subtitle", window.transform,
            "先完成基础新手教程，再按需查看各车队专项课程。专项对局将按课程大纲逐队接入。",
            21f, new Vector2(0f, 300f), new Vector2(1180f, 48f), TextAlignmentOptions.Center,
            new Color(0.72f, 0.82f, 0.92f));

        GameObject listPanel = CreateRect("CourseList", window.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RectTransform listRect = listPanel.GetComponent<RectTransform>();
        listRect.anchoredPosition = new Vector2(-445f, -30f);
        listRect.sizeDelta = new Vector2(360f, 610f);
        ModernUIStyle.ApplyPanel(listPanel);

        var courses = TeamTutorialCatalog.All;
        for (int i = 0; i < courses.Count; i++)
        {
            TeamTutorialCourseDefinition course = courses[i];
            Button button = CreateButton(listPanel.transform, $"Course_{course.Id}",
                course.DisplayName, new Vector2(0f, 252f - i * 78f),
                new Vector2(320f, 62f), GetCourseColor(course));
            button.onClick.AddListener(() => SelectCourse(course));
        }

        GameObject detailPanel = CreateRect("CourseDetail", window.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RectTransform detailRect = detailPanel.GetComponent<RectTransform>();
        detailRect.anchoredPosition = new Vector2(180f, -30f);
        detailRect.sizeDelta = new Vector2(850f, 610f);
        ModernUIStyle.ApplyPanel(detailPanel);

        courseTitle = CreateText("CourseTitle", detailPanel.transform, string.Empty, 34f,
            new Vector2(0f, 252f), new Vector2(760f, 50f), TextAlignmentOptions.Left);
        courseMeta = CreateText("CourseMeta", detailPanel.transform, string.Empty, 19f,
            new Vector2(0f, 205f), new Vector2(760f, 36f), TextAlignmentOptions.Left,
            new Color(0.45f, 0.83f, 1f));
        GameObject scrollObject = CreateRect("CourseScroll", detailPanel.transform,
            new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RectTransform scrollRect = scrollObject.GetComponent<RectTransform>();
        scrollRect.anchoredPosition = new Vector2(0f, 10f);
        scrollRect.sizeDelta = new Vector2(770f, 350f);
        courseScroll = scrollObject.AddComponent<ScrollRect>();
        courseScroll.horizontal = false;
        courseScroll.vertical = true;
        courseScroll.movementType = ScrollRect.MovementType.Clamped;
        courseScroll.scrollSensitivity = 28f;

        GameObject viewport = CreateRect("Viewport", scrollObject.transform, Vector2.zero, Vector2.one);
        viewport.AddComponent<RectMask2D>();
        courseScroll.viewport = viewport.GetComponent<RectTransform>();

        GameObject content = CreateRect("Content", viewport.transform,
            new Vector2(0f, 1f), new Vector2(1f, 1f));
        courseBodyRect = content.GetComponent<RectTransform>();
        courseBodyRect.pivot = new Vector2(0.5f, 1f);
        courseBodyRect.anchoredPosition = Vector2.zero;
        courseBodyRect.sizeDelta = new Vector2(0f, 350f);
        courseBody = content.AddComponent<TextMeshProUGUI>();
        courseBody.fontSize = 19f;
        courseBody.alignment = TextAlignmentOptions.TopLeft;
        courseBody.color = new Color(0.88f, 0.92f, 0.97f);
        courseBody.enableWordWrapping = true;
        courseBody.overflowMode = TextOverflowModes.Overflow;
        courseBody.raycastTarget = false;
        TMP_Text sourceFont = GetComponentInChildren<TMP_Text>(true);
        if (sourceFont != null) courseBody.font = sourceFont.font;
        courseScroll.content = courseBodyRect;

        primaryButton = CreateButton(detailPanel.transform, "StartCourse", "开始基础教程",
            new Vector2(205f, -252f), new Vector2(350f, 62f), ModernUIStyle.AccentGreen);
        primaryButton.onClick.AddListener(OnPrimaryClicked);
        primaryLabel = primaryButton.GetComponentInChildren<TMP_Text>(true);

        Button close = CreateButton(detailPanel.transform, "Close", "返回主菜单",
            new Vector2(-205f, -252f), new Vector2(300f, 62f), ModernUIStyle.AccentRed);
        close.onClick.AddListener(Hide);
    }

    private void SelectCourse(TeamTutorialCourseDefinition course)
    {
        selectedCourse = course;
        courseTitle.text = course.DisplayName;
        courseMeta.text = $"难度：{course.Difficulty}　推荐赛道：{GetTrackName(course.RecommendedTrackId)}";
        courseBody.text = BuildCourseBody(course);
        Canvas.ForceUpdateCanvases();
        courseBodyRect.sizeDelta = new Vector2(0f, Mathf.Max(350f, courseBody.preferredHeight + 20f));
        courseBodyRect.anchoredPosition = Vector2.zero;
        courseScroll.verticalNormalizedPosition = 1f;
        primaryButton.interactable = course.IsPlayable;
        primaryLabel.text = course.IsPlayable ? "开始基础教程" : "专项训练开发中";
    }

    private void OnPrimaryClicked()
    {
        if (selectedCourse == null || !selectedCourse.IsPlayable) return;
        Hide();
        startFoundationTutorial?.Invoke();
    }

    private static string BuildCourseBody(TeamTutorialCourseDefinition course)
    {
        string text = course.Summary;
        for (int i = 0; i < course.Lessons.Count; i++)
        {
            TeamTutorialLessonDefinition lesson = course.Lessons[i];
            text += $"\n\n<b>{i + 1}. {lesson.Title}</b>\n机制：{lesson.Mechanic}" +
                    $"\n操作：{lesson.PlayerAction}\n验收：{lesson.SuccessSignal}";
        }
        return text;
    }

    private static string GetTrackName(string id)
    {
        switch (id)
        {
            case "silverstone_afternoon_tea": return "银石";
            case "nurburgring_bier": return "纽博格林 GP";
            case "monza_pasta": return "蒙扎";
            case "indianapolis_burger": return "印第安纳波利斯";
            case "shanghai_dim_sum": return "上海";
            case "suzuka_sushi": return "铃鹿";
            case TutorialScenarioDefinition.TrackId: return "勒芒";
            default: return id;
        }
    }

    private static Color GetCourseColor(TeamTutorialCourseDefinition course)
    {
        if (!course.HasTeam) return ModernUIStyle.AccentGreen;
        switch (course.Team)
        {
            case TeamId.UK: return new Color(0.18f, 0.45f, 0.82f);
            case TeamId.DE: return new Color(0.68f, 0.56f, 0.22f);
            case TeamId.IT: return new Color(0.18f, 0.62f, 0.36f);
            case TeamId.US: return new Color(0.75f, 0.24f, 0.22f);
            case TeamId.CN: return new Color(0.85f, 0.25f, 0.16f);
            case TeamId.JP: return new Color(0.62f, 0.28f, 0.66f);
            default: return ModernUIStyle.AccentBlue;
        }
    }

    private TMP_Text CreateText(string name, Transform parent, string text, float fontSize,
        Vector2 position, Vector2 size, TextAlignmentOptions alignment, Color? color = null)
    {
        GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.alignment = alignment;
        label.color = color ?? Color.white;
        label.raycastTarget = false;
        TMP_Text source = GetComponentInChildren<TMP_Text>(true);
        if (source != null) label.font = source.font;
        return label;
    }

    private Button CreateButton(Transform parent, string name, string text,
        Vector2 position, Vector2 size, Color accent)
    {
        GameObject go = CreateRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        go.AddComponent<Image>();
        Button button = go.AddComponent<Button>();
        CreateText("Label", go.transform, text, 21f, Vector2.zero, size, TextAlignmentOptions.Center);
        ModernUIStyle.ApplyMenuButton(button, accent);
        ButtonClickAnimation.Attach(button);
        return button;
    }

    private static GameObject CreateRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return go;
    }
}
