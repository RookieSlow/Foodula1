using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Styles only the four existing card-zone shells and pile labels. No data or input ownership.</summary>
public static class RacingTechCardZoneStyle
{
    public static void ApplyLayout(Transform deckPanel, CardHandUI hand)
    {
        if (deckPanel == null) return;
        ApplyZone(deckPanel.Find("DrawPilePanel"), "DrawPileTitle", true);
        ApplyZone(deckPanel.Find("EnginePanel"), "EnginePileTitle", true);
        ApplyZone(deckPanel.Find("HandPanel"), "HandTitle", false);
        ApplyZone(deckPanel.Find("DiscardPilePanel"), "DiscardPileTitle", true);
        Transform handTitle = deckPanel.Find("HandPanel/HandTitle");
        if (handTitle != null) EnsureTitleHeight(handTitle.GetComponent<TMP_Text>());
        Transform tableTitle = deckPanel.Find("DeckTableTitle");
        if (tableTitle != null) EnsureTitleHeight(tableTitle.GetComponent<TMP_Text>());
        if (hand == null) return;
        SetColor(hand.drawPileText, RacingTechTheme.Primary);
        SetColor(hand.enginePileText, RacingTechTheme.Primary);
        SetColor(hand.discardPileText, RacingTechTheme.Primary);
        // The fallback deliberately aliases the aggregate label to the engine count.
        if (hand.deckInfoText != hand.enginePileText && hand.deckInfoText != hand.drawPileText &&
            hand.deckInfoText != hand.discardPileText)
            SetColor(hand.deckInfoText, RacingTechTheme.Secondary);
    }

    /// <summary>Called at the original lazy creation/reuse points; never traverses card faces.</summary>
    public static void ApplyPreviewLabels(TMP_Text count, TMP_Text empty, TMP_Text hint)
    {
        SetColor(count, RacingTechTheme.Primary);
        SetColor(empty, RacingTechTheme.Secondary);
        SetColor(hint, RacingTechTheme.Secondary);
        if (count == null || count.transform.parent == null) return;
        Image badge = count.transform.parent.GetComponent<Image>();
        if (badge == null) return;
        badge.sprite = null;
        badge.type = Image.Type.Simple;
        badge.color = RacingTechTheme.Graphite;
    }

    private static void ApplyZone(Transform zone, string titleName, bool raised)
    {
        if (zone == null) return;
        RacingTechTheme.ApplySurface(zone.GetComponent<Image>(), raised);
        // Same-object effects only: nested card art/selection/shadows remain untouched.
        foreach (Shadow effect in zone.GetComponents<Shadow>())
        {
            if (effect is Outline outline)
            {
                outline.effectColor = RacingTechTheme.Separator;
                outline.effectDistance = new Vector2(1f, -1f);
                outline.useGraphicAlpha = true;
            }
            else effect.enabled = false;
        }
        Transform title = zone.Find(titleName);
        if (title != null) SetColor(title.GetComponent<TMP_Text>(), RacingTechTheme.Primary);
    }

    private static void EnsureTitleHeight(TMP_Text label)
    {
        if (label == null || label.font == null) return;
        RectTransform rect = label.rectTransform;
        float height = rect.rect.height;
        // An unresolved first-layout rect must not acquire a large speculative offset.
        if (height <= 0f || rect.rect.width <= 0f) return;
        float preferred = label.preferredHeight;
        if (float.IsNaN(preferred) || float.IsInfinity(preferred) ||
            float.IsNaN(height) || float.IsInfinity(height) || preferred <= height) return;
        Vector2 offset = rect.offsetMin;
        offset.y -= Mathf.Ceil(preferred - height) + 1f;
        rect.offsetMin = offset;
    }

    private static void SetColor(TMP_Text label, Color color)
    {
        if (label != null) label.color = color;
    }
}
