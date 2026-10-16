using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Appearance of the existing pile inspector only; no card, sorting or scene ownership.</summary>
public static class RacingTechPileInspectorStyle
{
    public static void Apply(Transform panel, TMP_Text title, TMP_Text note, Button close, ScrollRect scroll)
    {
        if (panel == null) return;
        Image panelImage = panel.GetComponent<Image>();
        RacingTechTheme.ApplySurface(panelImage);
        // The existing outside-mask dismiss must not receive clicks on readable content.
        // This local blocker has no callbacks or animation; scroll/drag still route to ScrollRect.
        if (panelImage != null && panel.GetComponent<Button>() == null)
        {
            Button contentBlocker = panel.gameObject.AddComponent<Button>();
            contentBlocker.targetGraphic = panelImage;
            contentBlocker.transition = Selectable.Transition.None;
            Navigation navigation = contentBlocker.navigation;
            navigation.mode = Navigation.Mode.None;
            contentBlocker.navigation = navigation;
        }
        Outline outline = panel.GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = RacingTechTheme.Separator;
            outline.effectDistance = new Vector2(1f, -1f);
            outline.useGraphicAlpha = true;
        }
        SetColor(title, RacingTechTheme.Primary);
        SetColor(note, RacingTechTheme.Secondary);
        // Dark base with the original tint preserves Transition.None and the existing button events.
        if (close != null)
        {
            RacingTechTheme.ApplySurface(close.targetGraphic as Image, true);
            Transform label = close.transform.Find("Label");
            if (label != null) SetColor(label.GetComponent<TMP_Text>(), RacingTechTheme.Primary);
        }
        if (scroll == null) return;
        Image viewport = scroll.viewport != null ? scroll.viewport.GetComponent<Image>() : null;
        if (viewport != null)
        {
            RacingTechTheme.ApplySurface(viewport);
            viewport.color = RacingTechTheme.Graphite;
        }
        if (scroll.content == null) return;
        for (int i = 0; i < scroll.content.childCount; i++)
        {
            Transform row = scroll.content.GetChild(i);
            if (row.name != "CardEntry") continue;
            RacingTechTheme.ApplySurface(row.GetComponent<Image>(), true);
            Transform heading = row.Find("Heading"), effect = row.Find("Effect");
            if (heading != null) SetColor(heading.GetComponent<TMP_Text>(), RacingTechTheme.Primary);
            if (effect != null) SetColor(effect.GetComponent<TMP_Text>(), RacingTechTheme.Secondary);
            // MiniCard and Glyph preserve the original heat/speed/trick identity.
        }
    }

    private static void SetColor(TMP_Text text, Color color)
    {
        if (text != null) text.color = color;
    }
}
