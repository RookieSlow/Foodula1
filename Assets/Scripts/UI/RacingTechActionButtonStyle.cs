using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Flat styling for the five existing race actions; never owns action state or labels.</summary>
public static class RacingTechActionButtonStyle
{
    private static readonly Color PrimaryFill = new Color32(0x17, 0x4C, 0x63, 0xFF);
    private static readonly Color PrimaryHover = new Color32(0x20, 0x5B, 0x73, 0xFF);
    private static readonly Color SecondaryHover = new Color32(0x30, 0x39, 0x44, 0xFF);

    public static void Apply(Button button, bool primary)
    {
        if (button == null) return;
        // Preserve the former ModernUIStyle attachment: no new effect, and existing components/listeners are reused.
        ButtonClickAnimation.Attach(button);
        Image image = button.targetGraphic as Image;
        if (image != null)
        {
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.color = Color.white;
            foreach (Shadow shadow in image.GetComponents<Shadow>()) shadow.enabled = false;
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
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = button.colors;
        colors.normalColor = primary ? PrimaryFill : RacingTechTheme.Raised;
        colors.highlightedColor = primary ? PrimaryHover : SecondaryHover;
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
        // The existing fallback also supports a legacy Text label. Preserve its content/font.
        foreach (Text label in button.GetComponentsInChildren<Text>(true))
        {
            label.color = RacingTechTheme.Primary;
            label.raycastTarget = false;
        }
    }
}
