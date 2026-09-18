using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RaceUILayoutTests
{
    [Test]
    public void return_to_menu_button_is_docked_in_operation_action_stack()
    {
        GameObject root = new GameObject(
            "RaceUILayoutTest",
            typeof(RectTransform),
            typeof(HUDUI),
            typeof(RaceUILayoutController));
        try
        {
            HUDUI hud = root.GetComponent<HUDUI>();
            RaceUILayoutController layout = root.GetComponent<RaceUILayoutController>();
            GameObject buttonObject = new GameObject(
                "ReturnToMenuBtn",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            hud.returnToMenuButton = buttonObject.GetComponent<Button>();

            layout.ApplyLayout(hud, null);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            Assert.That(layout.UsesAuthoredLayout, Is.False);
            Assert.That(buttonRect.parent, Is.SameAs(layout.OperationPanel));
            Assert.That(buttonRect.anchorMin, Is.EqualTo(RaceUILayoutController.ReturnToMenuAnchorMin));
            Assert.That(buttonRect.anchorMax, Is.EqualTo(RaceUILayoutController.ReturnToMenuAnchorMax));
            Assert.That(buttonRect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(buttonRect.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(buttonRect.anchorMin.y, Is.GreaterThan(RaceUILayoutController.ResetAnchorMax.y),
                "Return must remain the top-most action below the panel title.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void authored_layout_preserves_custom_rects_and_card_size()
    {
        GameObject root = new GameObject(
            "AuthoredRaceUILayoutTest",
            typeof(RectTransform),
            typeof(HUDUI),
            typeof(CardHandUI),
            typeof(RaceUILayoutController));
        try
        {
            RectTransform operation = CreatePanel(root.transform, "OperationPanel",
                new Vector2(0.03f, 0.31f), new Vector2(0.19f, 0.97f));
            CreatePanel(root.transform, "ScoreboardPanel", Vector2.zero, Vector2.one);
            CreatePanel(root.transform, "TrackFrame", Vector2.zero, Vector2.one);
            CreatePanel(root.transform, "DeckTablePanel", Vector2.zero, Vector2.one);

            CardHandUI cardHand = root.GetComponent<CardHandUI>();
            cardHand.cardSizeOverride = new Vector2(126f, 196f);
            RaceUILayoutController layout = root.GetComponent<RaceUILayoutController>();

            layout.ApplyLayout(root.GetComponent<HUDUI>(), cardHand);

            Assert.That(layout.UsesAuthoredLayout, Is.True);
            Assert.That(layout.OperationPanel, Is.SameAs(operation));
            Assert.That(operation.anchorMin, Is.EqualTo(new Vector2(0.03f, 0.31f)));
            Assert.That(operation.anchorMax, Is.EqualTo(new Vector2(0.19f, 0.97f)));
            Assert.That(cardHand.cardSizeOverride, Is.EqualTo(new Vector2(126f, 196f)),
                "Authored card sizing must not be overwritten during runtime binding.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void operation_controls_follow_safe_top_to_bottom_order()
    {
        Assert.That(RaceUILayoutController.ReturnToMenuAnchorMin.y,
            Is.GreaterThan(RaceUILayoutController.ResetAnchorMax.y));
        Assert.That(RaceUILayoutController.ResetAnchorMin.y,
            Is.GreaterThan(RaceUILayoutController.GearGridAnchorMax.y));
        Assert.That(RaceUILayoutController.GearGridAnchorMin.y,
            Is.GreaterThan(RaceUILayoutController.DriverSkillAnchorMax.y));
        Assert.That(RaceUILayoutController.DriverSkillAnchorMin.y,
            Is.GreaterThan(RaceUILayoutController.PromptPanelAnchorMax.y));
        Assert.That(RaceUILayoutController.PromptPanelAnchorMin.y,
            Is.GreaterThan(RaceUILayoutController.PrimaryActionAnchorMax.y));
        Assert.That(RaceUILayoutController.LogScrollAnchorMin.y,
            Is.GreaterThan(RaceUILayoutController.PrimaryActionAnchorMax.y));
    }

    [Test]
    public void fallback_layout_wraps_log_text_in_a_fixed_scroll_view()
    {
        GameObject root = new GameObject(
            "ScrollableRaceLogTest",
            typeof(RectTransform),
            typeof(HUDUI),
            typeof(RaceUILayoutController));
        try
        {
            HUDUI hud = root.GetComponent<HUDUI>();
            GameObject logObject = new GameObject("LogText", typeof(RectTransform), typeof(TextMeshProUGUI));
            logObject.transform.SetParent(root.transform, false);
            hud.logText = logObject.GetComponent<TMP_Text>();

            RaceUILayoutController layout = root.GetComponent<RaceUILayoutController>();
            layout.ApplyLayout(hud, null);

            Assert.That(hud.logScrollRect, Is.Not.Null);
            Assert.That(hud.logScrollRect.viewport, Is.Not.Null);
            Assert.That(hud.logScrollRect.content, Is.SameAs(hud.logText.rectTransform));
            Assert.That(hud.logText.GetComponentInParent<RectMask2D>(), Is.Not.Null);
            Assert.That(hud.logScrollRect.GetComponent<RectTransform>().anchorMin,
                Is.EqualTo(RaceUILayoutController.LogScrollAnchorMin));
            Assert.That(hud.logScrollRect.GetComponent<RectTransform>().anchorMax,
                Is.EqualTo(RaceUILayoutController.LogScrollAnchorMax));
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static RectTransform CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax)
    {
        GameObject panel = new GameObject(name, typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        RectTransform rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        return rect;
    }
}
