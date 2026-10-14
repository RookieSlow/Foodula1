using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RacingTechInstrumentsTests
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");

    [TestCase(false)]
    [TestCase(true)]
    public void AuthoredAndFallbackHeatKeepTenSegmentsFontTextAndNonInteractiveTheme(bool authored)
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            TMP_Text source = Text(scope.Root.transform, "HeatText", scope.Font);
            HeatThermometerUI gauge;
            if (authored)
            {
                var host = new GameObject("HeatThermometer", typeof(RectTransform), typeof(CanvasGroup));
                host.transform.SetParent(scope.Root.transform, false);
                for (int i = 1; i <= 10; i++) Image(host.transform, "Segment" + i.ToString("00"));
                Image(host.transform, "GaugeBackground");
                Image(host.transform, "Tick50");
                Image(host.transform, "Tick70");
                Text(host.transform, "HeatPercent", scope.Font);
                gauge = host.AddComponent<HeatThermometerUI>();
            }
            else gauge = HeatThermometerUI.Attach(source);
            gauge.RefreshSnapshot(State(7), State(9));
            Assert.That(gauge.SegmentCount, Is.EqualTo(10));
            Assert.That(gauge.CurrentState.TotalHeat, Is.EqualTo(7));
            Assert.That(gauge.ForecastState.Value.TotalHeat, Is.EqualTo(9));
            TMP_Text percent = gauge.transform.Find("HeatPercent").GetComponent<TMP_Text>();
            Assert.That(percent.font, Is.SameAs(scope.Font));
            Assert.That(percent.text, Is.EqualTo("70%\n预90%"));
            Assert.That(percent.color, Is.EqualTo(RacingTechTheme.Primary));
            Assert.That(gauge.transform.Find("GaugeBackground").GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Graphite));
            Assert.That(gauge.transform.Find("Segment01").GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Actual));
            Assert.That(gauge.transform.Find("Segment06").GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Projected));
            Assert.That(gauge.transform.Find("Segment08").GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Raised));
            CanvasGroup group = gauge.GetComponent<CanvasGroup>();
            Assert.That(group.alpha, Is.EqualTo(1f));
            Assert.That(group.blocksRaycasts || group.interactable, Is.False);
            foreach (Graphic graphic in gauge.GetComponentsInChildren<Graphic>(true))
                Assert.That(graphic.raycastTarget, Is.False, graphic.name);
        }
    }

    [Test]
    public void ForecastRemovalAndRepeatedRefreshPoolGeometryAndKeepHalfWidthForecast()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            HeatThermometerUI gauge = HeatThermometerUI.Attach(Text(scope.Root.transform, "HeatText", scope.Font));
            gauge.RefreshSnapshot(State(4), State(8));
            int count = gauge.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 10; i++)
            {
                gauge.RefreshSnapshot(State(4));
                Assert.That(gauge.ForecastState.HasValue, Is.False);
                Assert.That(gauge.transform.Find("Segment01/Forecast").gameObject.activeSelf, Is.False);
                gauge.RefreshSnapshot(State(4), State(8));
            }
            Assert.That(gauge.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            RectTransform forecast = (RectTransform)gauge.transform.Find("Segment01/Forecast");
            Assert.That(forecast.anchorMin, Is.EqualTo(new Vector2(0.5f, 0f)));
            Assert.That(forecast.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(forecast.GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Projected));
            Assert.That(gauge.CurrentState.TotalHeat, Is.EqualTo(4));
        }
    }

    [Test]
    public void CriticalHeatIsStaticAndDoesNotIntroduceAFrameLoopOrTouchGameplayDeck()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            var deck = new CardDeck { heatPool = new HeatPool(10) };
            deck.DrawHeatFromPoolToHand(8);
            HeatThermometerUI gauge = HeatThermometerUI.Attach(Text(scope.Root.transform, "HeatText", scope.Font));
            gauge.GetComponent<CanvasGroup>().alpha = 0.4f;
            gauge.Refresh(deck);
            Assert.That(gauge.CurrentState.WarningLevel, Is.EqualTo(HeatWarningLevel.Critical));
            Assert.That(gauge.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f));
            Assert.That(gauge.transform.Find("Segment08").GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Critical));
            Assert.That(deck.heatPool.remaining, Is.EqualTo(2));
            Assert.That(deck.CountHeatInHand(), Is.EqualTo(8));
            Assert.That(typeof(HeatThermometerUI).GetMethod("Update", Private), Is.Null);
        }
    }

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void TeamControlThemeKeepsInputsFontsActualPendingAndIdempotentGeometry(TeamId team)
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            var root = new GameObject("GearButtons", typeof(RectTransform));
            root.transform.SetParent(scope.Root.transform, false);
            root.GetComponent<RectTransform>().sizeDelta = new Vector2(250f, 220f);
            var buttons = new Button[4];
            int calls = 0;
            for (int i = 0; i < buttons.Length; i++)
            {
                var host = new GameObject("Gear" + i, typeof(RectTransform), typeof(Image), typeof(Button));
                host.transform.SetParent(root.transform, false);
                Text(host.transform, "Label", scope.Font);
                buttons[i] = host.GetComponent<Button>();
                buttons[i].onClick.AddListener(() => calls++);
            }
            TeamGearPresentationUI view = TeamGearPresentationUI.Attach(root.transform, null, buttons[0], buttons[1], buttons[2], buttons[3]);
            view.Configure(team, 1);
            var player = new PlayerState("test", false, 0, 1) { teamId = team };
            view.Refresh(player, 2, true, false, null, true);
            Assert.That(view.CurrentGear, Is.EqualTo(1));
            Assert.That(view.PendingGear, Is.EqualTo(2));
            Assert.That(view.StateLabel.text, Does.Contain("实际").And.Contain("待选"));
            Assert.That(view.DetailLabel.color, Is.EqualTo(RacingTechTheme.Secondary));
            Assert.That(view.StateLabel.font, Is.SameAs(scope.Font));
            int count = root.GetComponentsInChildren<Transform>(true).Length;
            Vector3[] positions = Vertices(view.AssemblyGraphic);
            view.ApplyLayout();
            view.Refresh(player, 2, true, false, null, true);
            Assert.That(root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
            CollectionAssert.AreEqual(positions, Vertices(view.AssemblyGraphic));
            Assert.That(view.AssemblyGraphic.raycastTarget, Is.False);
            Assert.That(buttons[0].targetGraphic.raycastTarget, Is.True);
            Assert.That(buttons[2].gameObject.activeSelf, Is.EqualTo(team != TeamId.CN));
            Assert.That(buttons[3].gameObject.activeSelf, Is.EqualTo(team != TeamId.CN));
            buttons[0].onClick.Invoke();
            Assert.That(calls, Is.EqualTo(1));
            Assert.That(player.gear, Is.EqualTo(1));
            view.Refresh(player, 2, false, true, null, false);
            Assert.That(buttons[0].interactable, Is.False);
            Assert.That(view.StateLabel.text, Does.Contain("已锁"));
        }
    }

    [Test]
    public void StateFaceColorsMeetTextContrastAndRetainBothActualAndPendingOnOverlap()
    {
        Assert.That(Contrast(RacingTechTheme.Primary, RacingTechInstrumentStyle.ActualFill), Is.GreaterThan(7f));
        Assert.That(Contrast(RacingTechTheme.Primary, RacingTechInstrumentStyle.PendingFill), Is.GreaterThan(7f));
        var host = new GameObject("State face", typeof(RectTransform), typeof(TeamGearGraphic));
        try
        {
            TeamGearGraphic face = host.GetComponent<TeamGearGraphic>();
            face.Configure(TeamGearGraphic.Shape.MetalGate, TeamId.IT);
            face.SetGateState(true, true, true);
            using (var helper = new VertexHelper())
            {
                typeof(TeamGearGraphic).GetMethod("OnPopulateMesh", Private, null,
                    new[] { typeof(VertexHelper) }, null).Invoke(face, new object[] { helper });
                var colors = new HashSet<Color32>();
                UIVertex vertex = default;
                for (int i = 0; i < helper.currentVertCount; i++) { helper.PopulateUIVertex(ref vertex, i); colors.Add(vertex.color); }
                Assert.That(colors.Contains((Color32)RacingTechInstrumentStyle.ActualFill), Is.True);
                Assert.That(colors.Contains((Color32)RacingTechInstrumentStyle.PendingFill), Is.True);
                Assert.That(colors.Contains((Color32)RacingTechTheme.Actual), Is.True);
            }
        }
        finally { Object.DestroyImmediate(host); }
    }

    private static Vector3[] Vertices(TeamGearGraphic graphic)
    {
        using (var helper = new VertexHelper())
        {
            typeof(TeamGearGraphic).GetMethod("OnPopulateMesh", Private, null,
                new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
            var result = new Vector3[helper.currentVertCount];
            UIVertex vertex = default;
            for (int i = 0; i < result.Length; i++) { helper.PopulateUIVertex(ref vertex, i); result[i] = vertex.position; }
            Assert.That(result.Length, Is.GreaterThan(0));
            return result;
        }
    }

    private static HeatGaugeState State(int heat) => new HeatGaugeState(10 - heat, heat, 0, 10, heat / 10f,
        heat >= 7 ? HeatWarningLevel.Critical : heat >= 5 ? HeatWarningLevel.Elevated : HeatWarningLevel.Cool);

    private static TMP_Text Text(Transform parent, string name, TMP_FontAsset font)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        host.transform.SetParent(parent, false);
        TMP_Text label = host.GetComponent<TMP_Text>(); label.font = font;
        return label;
    }

    private static void Image(Transform parent, string name)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        host.transform.SetParent(parent, false);
    }

    private static float Contrast(Color a, Color b)
    {
        float x = Luminance(a), y = Luminance(b);
        return (Mathf.Max(x, y) + 0.05f) / (Mathf.Min(x, y) + 0.05f);
    }
    private static float Luminance(Color c) => 0.2126f * Linear(c.r) + 0.7152f * Linear(c.g) + 0.0722f * Linear(c.b);
    private static float Linear(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
}
