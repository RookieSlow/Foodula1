using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Runtime-built summer-break technology editor. All changes stay in an
/// isolated CareerTechTreeDraft until the player explicitly confirms them.
/// </summary>
public sealed class CareerTechTreeUI : MonoBehaviour
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
    private CareerTechTreeDraft draft;
    private Func<CareerTechSnapshot, bool> confirmDraft;
    private Action closeCallback;

    public bool Show(
        CareerSeasonState state,
        Func<CareerTechSnapshot, bool> confirmCallback,
        Action onClosed)
    {
        if (!CareerModeRules.CanAdjustTechTree(state) ||
            !CareerTechTreeDraft.TryCreate(state.ActiveTechSnapshot, database, out draft))
        {
            return false;
        }

        confirmDraft = confirmCallback;
        closeCallback = onClosed;
        if (overlay == null) Build();
        hintText.text = "调整仅属于本轮生涯；取消不会保存。确认后将进入第 5 站且本赛季不能再次调整。";
        Refresh();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        return true;
    }

    private void Build()
    {
        TMP_FontAsset font = GetComponentInChildren<TMP_Text>(true)?.font;
        var factory = new RaceUIFactory(font);

        overlay = new GameObject("CareerTechTreeOverlay", typeof(RectTransform), typeof(Image));
        overlay.transform.SetParent(transform, false);
        Stretch(overlay.GetComponent<RectTransform>());
        ModernUIStyle.ApplyOverlay(overlay.GetComponent<Image>());

        GameObject panel = CreatePanel(overlay.transform, new Vector2(1540f, 950f));
        TMP_Text title = factory.CreateText(panel.transform, "Title", "夏休 · 生涯科技调整", 42,
            new Vector2(0f, 425f), new Vector2(800f, 58f));
        Format(title, TextAlignmentOptions.Center, FontStyles.Bold, Color.white);
        teamText = factory.CreateText(panel.transform, "Team", string.Empty, 22,
            new Vector2(-430f, 425f), new Vector2(430f, 38f));
        Format(teamText, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.45f, 0.82f, 1f));
        rpText = factory.CreateText(panel.transform, "RP", string.Empty, 21,
            new Vector2(450f, 425f), new Vector2(430f, 38f));
        Format(rpText, TextAlignmentOptions.Center, FontStyles.Bold, new Color(1f, 0.82f, 0.35f));

        contentRoot = new GameObject("CareerTechColumns", typeof(RectTransform)).transform;
        contentRoot.SetParent(panel.transform, false);
        RectTransform contentRect = contentRoot.GetComponent<RectTransform>();
        contentRect.anchorMin = contentRect.anchorMax = new Vector2(0.5f, 0.5f);
        contentRect.anchoredPosition = new Vector2(0f, -20f);
        contentRect.sizeDelta = new Vector2(1440f, 690f);
        HorizontalLayoutGroup columns = contentRoot.gameObject.AddComponent<HorizontalLayoutGroup>();
        columns.spacing = 22f;
        columns.childAlignment = TextAnchor.UpperCenter;
        columns.childForceExpandWidth = true;
        columns.childForceExpandHeight = true;

        hintText = factory.CreateText(panel.transform, "Hint", string.Empty, 17,
            new Vector2(0f, -420f), new Vector2(900f, 45f));
        Format(hintText, TextAlignmentOptions.Center, FontStyles.Normal, new Color(0.68f, 0.78f, 0.9f));

        Button cancel = factory.CreateActionButton(panel.transform, "CareerTechCancel", "取消调整",
            new Vector2(-630f, -420f), new Color(0.28f, 0.32f, 0.4f), Cancel);
        cancel.GetComponent<RectTransform>().sizeDelta = new Vector2(200f, 52f);
        Button confirm = factory.CreateActionButton(panel.transform, "CareerTechConfirm", "确认并进入第 5 站",
            new Vector2(610f, -420f), new Color(0.18f, 0.62f, 0.38f), Confirm);
        confirm.GetComponent<RectTransform>().sizeDelta = new Vector2(280f, 52f);
        overlay.SetActive(false);
    }

    private void Refresh()
    {
        teamText.text = $"锁定车队：{CareerMenuPresentation.GetTeamLabel(draft.TeamId)}";
        rpText.text = $"生涯 RP：{draft.RpBalance:N0}";
        for (int i = contentRoot.childCount - 1; i >= 0; i--)
        {
            GameObject child = contentRoot.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(child);
            else
                DestroyImmediate(child);
        }

        BuildTier(TechTreeTier.L1, "L1 基础层");
        BuildTier(TechTreeTier.L2, "L2 进阶层");
        BuildTier(TechTreeTier.L3, "L3 大师层");
    }

    private void BuildTier(TechTreeTier tier, string title)
    {
        GameObject column = new GameObject(title, typeof(RectTransform), typeof(VerticalLayoutGroup));
        column.transform.SetParent(contentRoot, false);
        VerticalLayoutGroup layout = column.GetComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var factory = new RaceUIFactory(GetComponentInChildren<TMP_Text>(true)?.font);
        TMP_Text header = factory.CreateText(column.transform, "TierTitle", title, 25,
            Vector2.zero, new Vector2(450f, 38f));
        Format(header, TextAlignmentOptions.Center, FontStyles.Bold, new Color(0.45f, 0.82f, 1f));

        IReadOnlyList<TechNodeDef> nodes = draft.GetNodes(tier);
        for (int i = 0; i < nodes.Count; i++)
            BuildNode(factory, column.transform, nodes[i]);
    }

    private void BuildNode(RaceUIFactory factory, Transform parent, TechNodeDef node)
    {
        bool unlocked = draft.IsUnlocked(node.id);
        bool active = draft.IsActive(node.id);
        bool available = !unlocked && draft.CanUnlock(node.id);
        Color color = active ? ActiveColor : unlocked ? UnlockedColor : available ? AvailableColor : LockedColor;
        string status = active ? "已激活" : unlocked ? "已解锁" : available ? "可研发" : "锁定";
        Button button = factory.CreateActionButton(parent, node.id,
            $"{status}  {node.name}\n<size=13>RP {node.rpCost:N0} · {node.description}</size>",
            Vector2.zero, color, () => OnNodeClicked(node.id));
        LayoutElement element = button.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = node.tier == TechTreeTier.L3 ? 125f : 106f;
        element.minHeight = element.preferredHeight;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        label.fontSize = 15f;
        label.enableWordWrapping = true;
    }

    private void OnNodeClicked(string nodeId)
    {
        if (!draft.TryToggleOrUnlock(nodeId))
            hintText.text = "该节点仍未满足生涯 RP、前置或层级门槛。";
        else
            hintText.text = "草稿已更新；只有点击“确认并进入第 5 站”才会保存。";
        Refresh();
    }

    private void Confirm()
    {
        if (!draft.TryBuildSnapshot(out CareerTechSnapshot snapshot) ||
            confirmDraft == null || !confirmDraft(snapshot))
        {
            hintText.text = "保存失败，夏休仍未消费；请重试或取消返回。";
            return;
        }
        Close();
    }

    private void Cancel()
    {
        Close();
    }

    private void Close()
    {
        if (overlay != null) overlay.SetActive(false);
        draft = null;
        confirmDraft = null;
        Action callback = closeCallback;
        closeCallback = null;
        callback?.Invoke();
    }

    private static GameObject CreatePanel(Transform parent, Vector2 size)
    {
        GameObject panel = new GameObject("CareerTechTreePanel", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        ModernUIStyle.ApplyPanel(panel, true);
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
