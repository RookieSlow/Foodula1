using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ModernUIStyleTests
{
    [Test]
    public void ApplyButton_UsesSharedInteractiveStatePalette()
    {
        GameObject buttonObject = new GameObject("ModernUiStyleTestButton");
        try
        {
            Image image = buttonObject.AddComponent<Image>();
            Button button = buttonObject.AddComponent<Button>();

            ModernUIStyle.ApplyButton(button, ModernUIStyle.AccentCyan, true);

            Assert.AreSame(image, button.targetGraphic);
            Assert.AreNotEqual(button.colors.normalColor, button.colors.highlightedColor);
            Assert.Greater(button.colors.fadeDuration, 0f);
            Assert.IsNotNull(buttonObject.GetComponent<ButtonClickAnimation>());
        }
        finally
        {
            Object.DestroyImmediate(buttonObject);
        }
    }

    [Test]
    public void ApplyPanel_AddsReadableAccentOutline()
    {
        GameObject panel = new GameObject("ModernUiStyleTestPanel");
        try
        {
            panel.AddComponent<Image>();
            ModernUIStyle.ApplyPanel(panel, true);

            Image image = panel.GetComponent<Image>();
            Outline outline = panel.GetComponent<Outline>();
            Assert.AreEqual(ModernUIStyle.PanelElevated, image.color);
            Assert.IsNotNull(outline);
            Assert.Greater(outline.effectDistance.sqrMagnitude, 0f);
        }
        finally
        {
            Object.DestroyImmediate(panel);
        }
    }

    [Test]
    public void ApplyText_UsesFontAssetMaterialInsteadOfLegacyOverride()
    {
        GameObject labelObject = new GameObject("ModernUiStyleTestLabel");
        try
        {
            TMP_Text label = labelObject.AddComponent<TextMeshProUGUI>();
            if (label.font == null || label.font.material == null)
                Assert.Ignore("The editor test environment has no default TMP font asset.");

            Material expected = label.font.material;
            ModernUIStyle.ApplyText(label);

            Assert.AreEqual(expected.shader, label.fontMaterial.shader);
            Assert.AreSame(expected.mainTexture, label.fontMaterial.mainTexture);
            Assert.AreEqual(ModernUIStyle.TextPrimary, label.color);
        }
        finally
        {
            Object.DestroyImmediate(labelObject);
        }
    }
}
