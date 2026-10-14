using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RacingTechThemeTests
{
    [Test]
    public void TextAndDataColorsKeepReadableContrastOnBothSurfaces()
    {
        foreach (Color background in new[] { RacingTechTheme.Surface, RacingTechTheme.Raised })
        {
            Assert.That(Contrast(RacingTechTheme.Primary, background), Is.GreaterThanOrEqualTo(7f));
            Assert.That(Contrast(RacingTechTheme.Secondary, background), Is.GreaterThanOrEqualTo(4.5f));
            foreach (Color data in new[] { RacingTechTheme.Actual, RacingTechTheme.Projected, RacingTechTheme.Critical })
                Assert.That(Contrast(data, background), Is.GreaterThanOrEqualTo(3f));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void StylingPreservesInputOwnershipTextAndExistingFont(bool raycast)
    {
        var root = new GameObject("Theme input fixture", typeof(RectTransform), typeof(Image), typeof(Button));
        var textHost = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textHost.transform.SetParent(root.transform, false);
        try
        {
            Image image = root.GetComponent<Image>();
            Button button = root.GetComponent<Button>();
            TMP_Text label = textHost.GetComponent<TMP_Text>();
            TMP_FontAsset font = label.font;
            image.raycastTarget = raycast;
            label.raycastTarget = raycast;
            label.text = "热量 12→预计 14/18";
            button.interactable = false;
            int calls = 0;
            button.onClick.AddListener(() => calls++);
            RacingTechTheme.ApplySurface(image);
            RacingTechTheme.ApplyTelemetryText(label);
            RacingTechTheme.ApplyTelemetryButton(button);
            Assert.That(image.raycastTarget, Is.EqualTo(raycast));
            Assert.That(label.raycastTarget, Is.EqualTo(raycast));
            Assert.That(label.font, Is.SameAs(font));
            Assert.That(label.text, Is.EqualTo("热量 12→预计 14/18"));
            Assert.That(button.interactable, Is.False);
            button.onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test]
    public void ReattachAndRefreshReuseGeometryAndKeepActualMovementAndClickTarget()
    {
        var root = new GameObject("Theme telemetry fixture", typeof(RectTransform));
        try
        {
            var ledger = new TurnMovementBreakdown();
            ledger.BeginBasePlan();
            ledger.AddPlan("卡牌", 8, TurnMovementKind.Card);
            ledger.AddPlan("车手", 2, TurnMovementKind.Driver);
            ledger.Advance(3);
            var heat = new HeatGaugeState(6, 4, 0, 10, 0.4f, HeatWarningLevel.Cool);
            RaceTelemetryStripUI strip = RaceTelemetryStripUI.Attach(root.transform, null, null);
            strip.Refresh(ledger, heat);
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            Button clickTarget = strip.MovementButton;
            int clicks = 0;
            clickTarget.onClick.AddListener(() => clicks++);
            for (int i = 0; i < 8; i++)
            {
                Assert.That(RaceTelemetryStripUI.Attach(root.transform, null, null), Is.SameAs(strip));
                strip.Refresh(ledger, heat);
            }
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            Assert.That(strip.MovementText, Is.EqualTo("已前进 3 / 预计 10 格"));
            Assert.That(strip.HeatText, Is.EqualTo("热量 4/10 · 40%"));
            Assert.That(ledger.ActualCells, Is.EqualTo(3));
            Assert.That(ledger.TotalCells, Is.EqualTo(10));
            Assert.That(strip.VisiblePortionCount, Is.EqualTo(4));
            Assert.That(strip.MovementButton, Is.SameAs(clickTarget));
            Assert.That(clickTarget.GetComponent<Image>().raycastTarget, Is.True);
            clickTarget.onClick.Invoke();
            Assert.That(clicks, Is.EqualTo(1));
            foreach (Graphic graphic in strip.GetComponentsInChildren<Graphic>(true))
                if (graphic.gameObject != clickTarget.gameObject)
                    Assert.That(graphic.raycastTarget, Is.False, graphic.name);
            Assert.That(clickTarget.GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Surface));
            ledger.Reset();
            strip.Refresh(ledger, heat);
            Assert.That(strip.VisiblePortionCount, Is.Zero);
            Assert.That(strip.MovementText, Is.EqualTo("待出牌 · 点击查看来源"));
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }
        finally { Object.DestroyImmediate(root); }
    }

    private static float Contrast(Color a, Color b)
    {
        float x = Luminance(a), y = Luminance(b);
        return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f);
    }

    private static float Luminance(Color c) =>
        0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);

    private static float Linear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
}
