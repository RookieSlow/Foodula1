using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Shared confirmation appearance only; HUD retains every action and interaction gate.</summary>
public static class RacingTechConfirmationStyle
{
    private static readonly Color Confirm = new Color32(0x17, 0x4C, 0x63, 0xFF);
    private static readonly Color ConfirmHover = new Color32(0x20, 0x5B, 0x73, 0xFF);
    private static readonly Color CancelHover = new Color32(0x30, 0x39, 0x44, 0xFF);

    public static void Apply(GameObject panel, TMP_Text title, TMP_Text body, Button cancel, Button confirm)
    {
        if (panel == null) return;
        Image overlay = panel.GetComponent<Image>();
        if (overlay != null)
        {
            overlay.sprite = null;
            overlay.type = Image.Type.Simple;
            Color dim = RacingTechTheme.Graphite;
            dim.a = .86f;
            overlay.color = dim;
        }
        Transform card = panel.transform.Find("ReturnToMenuConfirmCard");
        if (card != null)
        {
            RacingTechTheme.ApplySurface(card.GetComponent<Image>());
            RemoveHeavyEffects(card.gameObject);
            AddDivider(card);
        }
        if (title != null) { title.color = RacingTechTheme.Primary; title.raycastTarget = false; }
        if (body != null) { body.color = RacingTechTheme.Secondary; body.raycastTarget = false; }
        StyleButton(cancel, false);
        StyleButton(confirm, true);
    }

    private static void StyleButton(Button button, bool primary)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            // ColorBlock contains absolute colors; a white base avoids multiplying two dark fills.
            image.color = Color.white;
            RemoveHeavyEffects(image.gameObject);
            if (primary)
            {
                Outline edge = image.GetComponent<Outline>();
                if (edge == null) edge = image.gameObject.AddComponent<Outline>();
                edge.effectColor = RacingTechTheme.Actual;
                edge.effectDistance = new Vector2(1f, -1f);
                edge.useGraphicAlpha = true;
                edge.enabled = true;
            }
        }
        // These two known controls use flat color states even in legacy authored Transition.None layouts.
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = primary ? Confirm : RacingTechTheme.Raised;
        colors.highlightedColor = primary ? ConfirmHover : CancelHover;
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = RacingTechTheme.Graphite;
        colors.disabledColor = RacingTechTheme.Raised;
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0f;
        button.colors = colors;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            label.color = RacingTechTheme.Primary;
            label.raycastTarget = false;
        }
    }

    private static void RemoveHeavyEffects(GameObject target)
    {
        foreach (Shadow shadow in target.GetComponents<Shadow>()) shadow.enabled = false;
    }

    private static void AddDivider(Transform card)
    {
        Transform existing = card.Find("RacingTechConfirmationDivider");
        if (existing != null) return;
        var line = new GameObject("RacingTechConfirmationDivider", typeof(RectTransform), typeof(Image));
        line.transform.SetParent(card, false);
        RectTransform rect = line.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(20f, -2f);
        rect.offsetMax = new Vector2(-20f, -1f);
        Image image = line.GetComponent<Image>();
        image.color = RacingTechTheme.Separator;
        image.raycastTarget = false;
    }
}
