using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Flat racing HUD tokens. Styling never owns data, input or font selection.</summary>
public static class RacingTechTheme
{
    public static readonly Color Graphite = new Color32(0x10, 0x13, 0x17, 0xFF);
    public static readonly Color Surface = new Color32(0x17, 0x1B, 0x20, 0xFF);
    public static readonly Color Raised = new Color32(0x22, 0x28, 0x30, 0xFF);
    public static readonly Color Primary = new Color32(0xF4, 0xF6, 0xF8, 0xFF);
    public static readonly Color Secondary = new Color32(0xB6, 0xC0, 0xCA, 0xFF);
    public static readonly Color Separator = new Color32(0x46, 0x51, 0x5E, 0xFF);
    public static readonly Color Actual = new Color32(0x67, 0xC6, 0xE5, 0xFF);
    public static readonly Color Projected = new Color32(0xF2, 0xC6, 0x6D, 0xFF);
    public static readonly Color Critical = new Color32(0xFF, 0x81, 0x78, 0xFF);
    public const float TelemetryFontSize = 13f;

    /// <summary>Preserves raycastTarget so the caller remains the input authority.</summary>
    public static void ApplySurface(Image image, bool raised = false)
    {
        if (image == null) return;
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = raised ? Raised : Surface;
    }

    /// <summary>Uses the supplied existing TMP font; does not replace localized text.</summary>
    public static void ApplyTelemetryText(TMP_Text label, bool secondary = false)
    {
        if (label == null) return;
        label.color = secondary ? Secondary : Primary;
        label.fontSize = TelemetryFontSize;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.fontStyle = FontStyles.Normal;
        label.enableWordWrapping = false;
        label.margin = Vector4.zero;
    }

    /// <summary>Only button tints change; listeners, interaction gates and navigation remain.</summary>
    public static void ApplyTelemetryButton(Button button)
    {
        if (button == null) return;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1.16f, 1.16f, 1.16f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        colors.disabledColor = new Color(0.6f, 0.6f, 0.6f, 1f);
        colors.fadeDuration = 0f;
        button.colors = colors;
    }
}
