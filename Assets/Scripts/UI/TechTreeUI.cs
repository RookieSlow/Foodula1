using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built team technology screen. It deliberately depends on the
/// TechTreeRules/ProfileStore APIs rather than the race coordinator, so the
/// menu can be used before a Race scene exists and can be expanded later with
/// animations or a campaign shell without touching gameplay code.
/// </summary>
public sealed class TechTreeUI : MonoBehaviour
{
    private static readonly Color ActiveColor = new Color(0.12f, 0.43f, 0.32f, 1f);
    private static readonly Color UnlockedColor = new Color(0.16f, 0.25f, 0.38f, 1f);
    private static readonly Color AvailableColor = new Color(0.42f, 0.30f, 0.12f, 1f);
    private static readonly Color LockedColor = new Color(0.12f, 0.14f, 0.18f, 1f);

    private readonly TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault();
    private GameObject overlay;
    private Transform contentRoot;
    private TMP_Text teamText;
    private TMP_Text rpText;
    private TMP_Text hintText;
    private TMP_FontAsset font;
    private TeamId selectedTeam;
    private bool initialized;

    private static readonly TeamId[] Teams =
    {
        TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP
    };

    public void Initialize()
    {
        if (initialized) return;
        font = GetComponentInChildren<TMP_Text>(true)?.font;
        selectedTeam = DriverSelectionState.ResolveDriver(TeamId.CN).Team;
        BuildOverlay();
        initialized = true;
    }

    public void Show()
    {
        if (!initialized) Initialize();
        selectedTeam = DriverSelectionState.ResolveDriver(TeamId.CN).Team;
        Refresh();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    public void Hide()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    private void BuildOverlay()
    {
        overlay = CreateObject("TechTreeOverlay", transform);
        Stretch(overlay.GetComponent<RectTransform>());
        Image overlayImage = overlay.AddComponent<Image>();
        ModernUIStyle.ApplyOverlay(overlayImage);

        GameObject panel = CreateObject("TechTreePanel", overlay.transform);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1540f, 950f);
        panel.AddComponent<Image>();
        ModernUIStyle.ApplyPanel(panel, true);

        CreateText(panel.transform, "Title", "车队科技树", 46f,
            new Vector2(0f, 425f), new Vector2(700f, 58f), FontStyles.Bold);
        teamText = CreateText(panel.transform, "Team", string.Empty, 23f,
            new Vector2(-430f, 425f), new Vector2(420f, 38f), FontStyles.Bold,
            new Color(0.45f, 0.82f, 1f));
        rpText = CreateText(panel.transform, "RP", string.Empty, 21f,
            new Vector2(450f, 425f), new Vector2(430f, 38f), FontStyles.Bold,
            new Color(1f, 0.82f, 0.35f));

        GameObject tabs = CreateObject("TeamTabs", panel.transform);
        RectTransform tabsRect = tabs.GetComponent<RectTransform>();
        tabsRect.anchorMin = tabsRect.anchorMax = new Vector2(0.5f, 0.5f);
        tabsRect.pivot = new Vector2(0.5f, 0.5f);
        tabsRect.anchoredPosition = new Vector2(0f, 365f);
        tabsRect.sizeDelta = new Vector2(1410f, 54f);
        HorizontalLayoutGroup tabLayout = tabs.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 12f;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childForceExpandWidth = true;
        tabLayout.childForceExpandHeight = true;
        for (int i = 0; i < Teams.Length; i++)
        {
            TeamId team = Teams[i];
            Button button = CreateButton(tabs.transform, team.ToString(), GetTeamLabel(team),
                Color.Lerp(UnlockedColor, Color.white, 0.06f), 17f);
            AddTeamLogo(button, team);
            button.onClick.AddListener(() => SelectTeam(team));
        }

        contentRoot = CreateObject("TechColumns", panel.transform).transform;
        RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.pivot = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = new Vector2(0f, -25f);
        contentRect.sizeDelta = new Vector2(1440f, 690f);
        HorizontalLayoutGroup columns = contentRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        columns.spacing = 22f;
        columns.childAlignment = TextAnchor.UpperCenter;
        columns.childForceExpandWidth = true;
        columns.childForceExpandHeight = true;

        hintText = CreateText(panel.transform, "Hint",
            "点击“可研发”节点解锁；点击已解锁节点切换本场激活状态。",
            17f, new Vector2(0f, -420f), new Vector2(930f, 34f), FontStyles.Normal,
            new Color(0.62f, 0.72f, 0.84f));

        Button back = CreateButton(panel.transform, "BackButton", "返回",
            new Color(0.24f, 0.28f, 0.34f), 21f);
        RectTransform backRect = back.GetComponent<RectTransform>();
        backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0.5f);
        backRect.anchoredPosition = new Vector2(-630f, -420f);
        backRect.sizeDelta = new Vector2(180f, 52f);
        back.onClick.AddListener(Hide);

        overlay.SetActive(false);
    }

    private static void AddTeamLogo(Button button, TeamId team)
    {
        Sprite sprite = BrandArtResources.LoadTeamLogo(team);
        if (sprite == null || button == null) return;
        GameObject logoObject = CreateObject("TeamLogo", button.transform);
        RectTransform rect = logoObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(10f, 0f);
        rect.sizeDelta = new Vector2(36f, 36f);
        Image image = logoObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.rectTransform.offsetMin = new Vector2(48f, label.rectTransform.offsetMin.y);
            label.rectTransform.offsetMax = new Vector2(-6f, label.rectTransform.offsetMax.y);
        }
    }

    private void Refresh()
    {
        TechTreeState state = TechTreeProfileStore.GetOrCreate(selectedTeam, database);
        if (teamText != null) teamText.text = $"{GetTeamLabel(selectedTeam)} 科技配置";
        if (rpText != null) rpText.text = $"RP：{state.rpBalance:N0}";

        for (int i = contentRoot.childCount - 1; i >= 0; i--)
            Destroy(contentRoot.GetChild(i).gameObject);

        BuildTierColumn(state, TechTreeTier.L1, "L1 基础层");
        BuildTierColumn(state, TechTreeTier.L2, "L2 进阶层");
        BuildTierColumn(state, TechTreeTier.L3, "L3 大师层");
    }

    private void BuildTierColumn(TechTreeState state, TechTreeTier tier, string title)
    {
        GameObject column = CreateObject(title, contentRoot);
        VerticalLayoutGroup layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        CreateText(column.transform, "TierTitle", title, 25f,
            Vector2.zero, new Vector2(450f, 38f), FontStyles.Bold,
            new Color(0.45f, 0.82f, 1f));

        bool useEv = selectedTeam == TeamId.CN;
        List<TechNodeDef> nodes = database.GetAllForTeamInTier(selectedTeam, tier, useEv);
        foreach (TechNodeDef node in nodes)
            BuildNodeButton(column.transform, state, node);
    }

    private void BuildNodeButton(Transform parent, TechTreeState state, TechNodeDef node)
    {
        bool unlocked = state.IsUnlocked(node.id);
        bool active = state.IsActive(node.id);
        bool available = !unlocked && TechTreeRules.CanUnlock(state, node.id, database);
        Color color = active ? ActiveColor : unlocked ? UnlockedColor : available ? AvailableColor : LockedColor;
        Button button = CreateButton(parent, node.id, string.Empty, color, 15f);
        LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = node.tier == TechTreeTier.L3 ? 125f : 106f;
        element.minHeight = element.preferredHeight;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            string status = active ? "已激活" : unlocked ? "已解锁" : available ? "可研发" : "锁定";
            label.text = $"{status}  {node.name}\n<size=13>RP {node.rpCost:N0} · {node.description}</size>";
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = true;
        }
        button.onClick.AddListener(() => OnNodeClicked(node.id));
    }

    private void SelectTeam(TeamId team)
    {
        selectedTeam = team;
        Refresh();
    }

    private void OnNodeClicked(string nodeId)
    {
        TechTreeState state = TechTreeProfileStore.GetOrCreate(selectedTeam, database);
        if (!state.IsUnlocked(nodeId))
        {
            if (!TechTreeProfileStore.TryUnlock(state, nodeId, database) && hintText != null)
                hintText.text = "该节点仍未满足 RP、前置或层级门槛。";
        }
        else
        {
            if (state.activeNodeIds.Contains(nodeId)) state.activeNodeIds.Remove(nodeId);
            else state.activeNodeIds.Add(nodeId);
            TechTreeProfileStore.Save(state);
        }
        Refresh();
    }

    private static string GetTeamLabel(TeamId team)
    {
        switch (team)
        {
            case TeamId.UK: return "英国队";
            case TeamId.DE: return "德国队";
            case TeamId.IT: return "意大利队";
            case TeamId.US: return "美国队";
            case TeamId.CN: return "中国队";
            case TeamId.JP: return "日本队";
            default: return team.ToString();
        }
    }

    private TMP_Text CreateText(Transform parent, string objectName, string content,
        float size, Vector2 position, Vector2 dimensions, FontStyles style,
        Color? color = null)
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

    private Button CreateButton(Transform parent, string objectName, string label,
        Color color, float fontSize)
    {
        GameObject buttonObject = CreateObject(objectName, parent);
        Image image = buttonObject.AddComponent<Image>();
        Button button = buttonObject.AddComponent<Button>();
        button.targetGraphic = image;
        ModernUIStyle.ApplyButton(button, color);
        ButtonClickAnimation.Attach(button);
        TMP_Text text = CreateText(buttonObject.transform, "Label", label, fontSize,
            Vector2.zero, new Vector2(420f, 100f), FontStyles.Bold);
        Stretch(text.rectTransform);
        return button;
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
