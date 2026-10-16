using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Appearance of the existing selected-card explanation, without owning selection or copy.</summary>
public static class RacingTechSelectedEffectStyle
{
    public static void Apply(GameObject panel, TMP_Text text)
    {
        if (panel != null)
        {
            RacingTechTheme.ApplySurface(panel.GetComponent<Image>());
            foreach (Shadow effect in panel.GetComponents<Shadow>())
            {
                if (effect is Outline outline)
                {
                    outline.effectColor = RacingTechTheme.Separator;
                    outline.effectDistance = new Vector2(1f, -1f);
                    outline.useGraphicAlpha = true;
                }
                else effect.enabled = false;
            }
        }
        if (text != null) text.color = RacingTechTheme.Primary;
    }
}
