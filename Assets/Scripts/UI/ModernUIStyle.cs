using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shared visual language for the runtime-built Foodula1 uGUI.
/// Kenney assets are optional Resources; the Texture2D fallback keeps the UI
/// usable before Unity has generated sprite importer metadata for new PNGs.
/// </summary>
public static class ModernUIStyle
{
    public static readonly Color Backdrop = new Color(0.008f, 0.014f, 0.028f, 0.965f);
    public static readonly Color Panel = new Color(0.035f, 0.058f, 0.095f, 0.985f);
    public static readonly Color PanelElevated = new Color(0.055f, 0.085f, 0.13f, 0.99f);
    public static readonly Color TextPrimary = new Color(0.91f, 0.95f, 1f, 1f);
    public static readonly Color TextSecondary = new Color(0.62f, 0.72f, 0.84f, 1f);
    public static readonly Color AccentBlue = new Color(0.22f, 0.64f, 0.96f, 1f);
    public static readonly Color AccentCyan = new Color(0.18f, 0.82f, 0.82f, 1f);
    public static readonly Color AccentPurple = new Color(0.62f, 0.34f, 0.92f, 1f);
    public static readonly Color AccentGold = new Color(0.92f, 0.68f, 0.22f, 1f);
    public static readonly Color AccentGreen = new Color(0.18f, 0.70f, 0.44f, 1f);
    public static readonly Color AccentRed = new Color(0.82f, 0.28f, 0.32f, 1f);

    private static readonly Dictionary<string, Sprite> SpriteCache =
        new Dictionary<string, Sprite>();

    public static void ApplyOverlay(Image image)
    {
        if (image == null) return;
        image.color = Backdrop;
        image.raycastTarget = true;
    }

    public static void ApplyPanel(GameObject panel, bool elevated = false)
    {
        if (panel == null) return;
        Image image = panel.GetComponent<Image>();
        if (image == null) image = panel.AddComponent<Image>();
        image.color = elevated ? PanelElevated : Panel;

        Outline outline = panel.GetComponent<Outline>();
        if (outline == null) outline = panel.AddComponent<Outline>();
        outline.effectColor = new Color(AccentBlue.r, AccentBlue.g, AccentBlue.b, 0.28f);
        outline.effectDistance = new Vector2(2f, -2f);
        outline.useGraphicAlpha = true;
    }

    public static void ApplyButton(Button button, Color accent, bool primary = false)
    {
        if (button == null) return;
        Image image = button.GetComponent<Image>();
        if (image == null) image = button.gameObject.AddComponent<Image>();

        Sprite buttonSprite = LoadSprite(primary
            ? "UiTheme/Kenney/header_large_rectangle"
            : "UiTheme/Kenney/button_rectangle_border");
        if (buttonSprite != null)
        {
            image.sprite = buttonSprite;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
        }

        Color safeAccent = EnsureVisibleAccent(accent);
        image.color = Color.Lerp(PanelElevated, safeAccent, primary ? 0.68f : 0.46f);
        image.raycastTarget = true;
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = Color.Lerp(image.color, TextPrimary, 0.22f);
        colors.pressedColor = Color.Lerp(image.color, Color.black, 0.18f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.28f, 0.33f, 0.40f, 0.62f);
        colors.colorMultiplier = 1f;
        colors.fadeDuration = 0.12f;
        button.colors = colors;
        ButtonClickAnimation.Attach(button);

        TMP_Text[] labels = button.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
            ApplyText(labels[i]);
    }

    /// <summary>
    /// Repairs legacy authored labels that kept a stale TMP material after
    /// their font asset was rebuilt. This is important for Chinese glyphs:
    /// the font asset can be correct while the old material still points at a
    /// different atlas.
    /// </summary>
    public static void ApplyText(TMP_Text label)
    {
        if (label == null) return;
        if (label.font != null && label.font.material != null)
            label.fontMaterial = label.font.material;
        label.color = TextPrimary;
        label.outlineColor = new Color(0f, 0f, 0f, 0.55f);
        label.outlineWidth = 0.12f;
    }

    public static void ApplyMenuButton(Button button, Color accent, bool primary = false)
    {
        ApplyButton(button, accent, primary);
        if (button == null) return;
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.fontStyle |= FontStyles.Bold;
            label.fontSize = Mathf.Max(label.fontSize, 22f);
        }
    }

    public static Sprite LoadSprite(string resourcePath)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;
        if (SpriteCache.TryGetValue(resourcePath, out Sprite cached)) return cached;

        Sprite sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Texture2D texture = Resources.Load<Texture2D>(resourcePath);
            if (texture != null)
            {
                sprite = Sprite.Create(texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f), 100f);
                sprite.name = resourcePath.Replace('/', '_');
            }
        }

        SpriteCache[resourcePath] = sprite;
        return sprite;
    }

    private static Color EnsureVisibleAccent(Color color)
    {
        if (color.a <= 0.01f) color.a = 1f;
        float luminance = color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
        if (luminance < 0.16f)
            color = Color.Lerp(color, AccentBlue, 0.48f);
        color.a = 1f;
        return color;
    }
}
