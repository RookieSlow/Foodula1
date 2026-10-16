using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RacingTechCardZoneTests
{
    private static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");
    private static readonly string[] Zones = { "DrawPilePanel", "EnginePanel", "HandPanel", "DiscardPilePanel" };
    private static readonly string[] Titles = { "DrawPileTitle", "EnginePileTitle", "HandTitle", "DiscardPileTitle" };

    [TestCase(false)]
    [TestCase(true)]
    public void BothLayoutPathsThemeFourZonesAndKeepCountBinding(bool authored)
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            CardHandUI hand = scope.Root.AddComponent<CardHandUI>();
            hand.deckInfoText = Label(scope.Root.transform, "DeckInfo", scope.Font);
            hand.deckInfoText.text = "统计原文 999";
            if (authored)
            {
                foreach (string name in new[] { "OperationPanel", "ScoreboardPanel", "TrackFrame", "DeckTablePanel" })
                    Panel(scope.Root.transform, name);
                Transform deck = scope.Root.transform.Find("DeckTablePanel");
                for (int i = 0; i < Zones.Length; i++) Label(Panel(deck, Zones[i]), Titles[i], scope.Font);
                hand.drawPileText = Label(deck.Find(Zones[0]), "DrawPileInfo", scope.Font);
                hand.enginePileText = hand.deckInfoText;
                hand.discardPileText = Label(deck.Find(Zones[3]), "DiscardPileInfo", scope.Font);
            }
            var layout = scope.Root.AddComponent<RaceUILayoutController>();
            layout.ApplyLayout(null, hand);
            Assert.That(layout.UsesAuthoredLayout, Is.EqualTo(authored));
            Transform actualDeck = scope.Root.transform.Find("DeckTablePanel");
            for (int i = 0; i < Zones.Length; i++)
            {
                Transform zone = actualDeck.Find(Zones[i]);
                Assert.That(zone.GetComponent<Image>().color, Is.EqualTo(i == 2 ? RacingTechTheme.Surface : RacingTechTheme.Raised));
                Assert.That(zone.GetComponent<Image>().sprite, Is.Null);
                Assert.That(zone.Find(Titles[i]).GetComponent<TMP_Text>().color, Is.EqualTo(RacingTechTheme.Primary));
            }
            Assert.That(hand.enginePileText, Is.SameAs(hand.deckInfoText));
            Assert.That(hand.enginePileText.color, Is.EqualTo(RacingTechTheme.Primary));
            Assert.That(hand.deckInfoText.text, Is.EqualTo("统计原文 999"));
            int count = scope.Root.GetComponentsInChildren<Transform>(true).Length;
            layout.ApplyLayout(null, hand);
            Assert.That(scope.Root.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(count));
        }
    }

    [Test]
    public void RepeatedShellStylePreservesAuthoredGeometryFontInputAndNestedCardArt()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            Transform deck = Panel(scope.Root.transform, "DeckTablePanel");
            Transform zone = Panel(deck, "DrawPilePanel");
            var rect = (RectTransform)zone;
            rect.anchorMin = new Vector2(.12f,.21f); rect.anchorMax = new Vector2(.37f,.86f);
            rect.offsetMin = new Vector2(3f,7f); rect.offsetMax = new Vector2(-11f,-5f);
            var before = new[] { rect.anchorMin, rect.anchorMax, rect.offsetMin, rect.offsetMax };
            Image image = zone.GetComponent<Image>(); image.raycastTarget = true;
            Button button = zone.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            button.transition = Selectable.Transition.None; button.interactable = false;
            var navigation = button.navigation; navigation.mode = Navigation.Mode.Explicit; button.navigation = navigation;
            ColorBlock colors = button.colors; int calls = 0; button.onClick.AddListener(() => calls++);
            Outline outline = zone.gameObject.AddComponent<Outline>();
            Shadow shadow = zone.gameObject.AddComponent<Shadow>();
            TMP_Text title = Label(zone, "DrawPileTitle", scope.Font); title.text = "原标题 999"; title.fontSize = 23f;
            Material material = title.fontSharedMaterial; title.raycastTarget = true;
            Transform nested = Panel(zone, "CardThumb0");
            Image art = nested.GetComponent<Image>(); art.color = Color.magenta;
            Outline cardOutline = nested.gameObject.AddComponent<Outline>(); cardOutline.effectDistance = new Vector2(8f,-8f);
            int children = deck.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 5; i++) RacingTechCardZoneStyle.ApplyLayout(deck, null);
            Assert.That(new[] {rect.anchorMin,rect.anchorMax,rect.offsetMin,rect.offsetMax}, Is.EqualTo(before));
            Assert.That(image.raycastTarget, Is.True);
            Assert.That(button.targetGraphic, Is.SameAs(image)); Assert.That(button.colors, Is.EqualTo(colors));
            Assert.That(button.transition, Is.EqualTo(Selectable.Transition.None)); Assert.That(button.interactable, Is.False);
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.Explicit));
            button.onClick.Invoke(); Assert.That(calls, Is.EqualTo(1));
            Assert.That(title.text, Is.EqualTo("原标题 999")); Assert.That(title.fontSize, Is.EqualTo(23f));
            Assert.That(title.font, Is.SameAs(scope.Font)); Assert.That(title.fontSharedMaterial, Is.SameAs(material));
            Assert.That(title.raycastTarget, Is.True);
            Assert.That(shadow.enabled, Is.False); Assert.That(outline.effectDistance, Is.EqualTo(new Vector2(1f,-1f)));
            Assert.That(outline.effectColor, Is.EqualTo(RacingTechTheme.Separator));
            Assert.That(art.color, Is.EqualTo(Color.magenta));
            Assert.That(cardOutline.effectDistance, Is.EqualTo(new Vector2(8f,-8f)));
            Assert.That(deck.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
        }
    }

    [Test]
    public void IndependentStatisticsAreSecondaryButAliasedExactCountsWin()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            CardHandUI hand = scope.Root.AddComponent<CardHandUI>();
            hand.deckInfoText = Label(scope.Root.transform, "Stats", scope.Font);
            hand.enginePileText = Label(scope.Root.transform, "Count", scope.Font);
            RacingTechCardZoneStyle.ApplyLayout(scope.Root.transform, hand);
            Assert.That(hand.deckInfoText.color, Is.EqualTo(RacingTechTheme.Secondary));
            Assert.That(hand.enginePileText.color, Is.EqualTo(RacingTechTheme.Primary));
            hand.deckInfoText = hand.enginePileText;
            RacingTechCardZoneStyle.ApplyLayout(scope.Root.transform, hand);
            Assert.That(hand.deckInfoText.color, Is.EqualTo(RacingTechTheme.Primary));
        }
    }

    [Test]
    public void LazyLabelsStayThemedAcrossRepeatedConfigureRefreshAndEmptyPile()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            Transform zone = Panel(scope.Root.transform, "DrawPilePanel");
            CardPilePreviewUI preview = zone.gameObject.AddComponent<CardPilePreviewUI>();
            var pile = new List<CardData> { new CardData(CardType.Speed,1), CardData.CreateTrick("uk-scone"), new CardData(CardType.Heat,0) };
            preview.Configure(null,null,null,null,scope.Font,false);
            preview.ConfigureInspection(TrickCardDatabaseFactory.CreateDefault(), "抽牌堆", true);
            preview.Refresh(pile,999);
            TMP_Text count = zone.Find("CountBadge/Count").GetComponent<TMP_Text>();
            TMP_Text empty = zone.Find("EmptyLabel").GetComponent<TMP_Text>();
            TMP_Text hint = zone.Find("InspectHint").GetComponent<TMP_Text>();
            Material material = count.fontSharedMaterial;
            int children = zone.GetComponentsInChildren<Transform>(true).Length;
            for (int i = 0; i < 4; i++)
            {
                preview.Configure(null,null,null,null,scope.Font,false);
                preview.ConfigureInspection(TrickCardDatabaseFactory.CreateDefault(), "抽牌堆", true);
                preview.Refresh(pile,999);
            }
            Assert.That(count.text, Is.EqualTo("999")); Assert.That(count.color, Is.EqualTo(RacingTechTheme.Primary));
            Assert.That(count.transform.parent.GetComponent<Image>().color, Is.EqualTo(RacingTechTheme.Graphite));
            Assert.That(empty.color, Is.EqualTo(RacingTechTheme.Secondary)); Assert.That(hint.color, Is.EqualTo(RacingTechTheme.Secondary));
            Assert.That(count.raycastTarget || empty.raycastTarget || hint.raycastTarget, Is.False);
            Assert.That(count.font, Is.SameAs(scope.Font)); Assert.That(count.fontSharedMaterial, Is.SameAs(material));
            Assert.That(preview.DisplayedCardCount, Is.EqualTo(3)); Assert.That(preview.DisplayedStackLayerCount, Is.EqualTo(7));
            var snapshot = (List<CardData>)typeof(CardPilePreviewUI).GetField("pileSnapshot", BindingFlags.Instance|BindingFlags.NonPublic).GetValue(preview);
            Assert.That(snapshot, Is.EqualTo(pile)); Assert.That(snapshot[0], Is.SameAs(pile[0]));
            Assert.That(zone.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(children));
            preview.Refresh(new List<CardData>(),0);
            Assert.That(count.text, Is.EqualTo("0")); Assert.That(empty.gameObject.activeSelf, Is.True);
            Assert.That(hint.text, Is.EqualTo("暂无卡牌")); Assert.That(preview.DisplayedStackLayerCount, Is.Zero);
            Assert.That(zone.GetComponent<Button>().interactable, Is.False);
        }
    }

    [Test]
    public void RealPilePointerOpensOneOriginalInspectorAndEmptyDisabledRejectsClick()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            Transform zone = Panel(scope.Root.transform, "DrawPilePanel");
            ((RectTransform)zone).sizeDelta = new Vector2(260f,260f);
            CardPilePreviewUI preview = zone.gameObject.AddComponent<CardPilePreviewUI>();
            preview.Configure(null,null,null,null,scope.Font,false);
            preview.ConfigureInspection(TrickCardDatabaseFactory.CreateDefault(), "抽牌堆", true);
            var pile = new List<CardData> {new CardData(CardType.Speed,2)};
            for (int i = 0; i < 3; i++) preview.ConfigureInspection(TrickCardDatabaseFactory.CreateDefault(), "抽牌堆", true);
            preview.Refresh(pile,1);
            Button button = zone.GetComponent<Button>(); ColorBlock colors = button.colors;
            RacingTechCardZoneStyle.ApplyLayout(scope.Root.transform,null);
            Assert.That(button.colors, Is.EqualTo(colors));
            scope.Click(button);
            Transform overlay = scope.Root.transform.Find("CardPileInspectorOverlay");
            Assert.That(overlay, Is.Not.Null);
            Assert.That(scope.Root.transform.Cast<Transform>().Count(t => t.name == "CardPileInspectorOverlay"), Is.EqualTo(1));
            Assert.That(overlay.GetComponentInChildren<ScrollRect>(true), Is.Not.Null);
            Assert.That(overlay.GetComponentsInChildren<TMP_Text>(true).Any(t=>t.text.Contains("速度牌 2")), Is.True);
            Object.DestroyImmediate(overlay.gameObject);
            preview.Refresh(new List<CardData>(),0);
            ExecuteEvents.Execute(button.gameObject, new PointerEventData(scope.EventSystem) {button=PointerEventData.InputButton.Left}, ExecuteEvents.pointerClickHandler);
            Assert.That(scope.Root.transform.Find("CardPileInspectorOverlay"), Is.Null);
        }
    }

    [Test]
    public void PilePointerTintStatesKeepEffectiveTextContrast()
    {
        using (var scope = CareerUiPointerTestScope.Create(LoadFont))
        {
            Transform zone = Panel(scope.Root.transform,"DrawPilePanel");
            var preview = zone.gameObject.AddComponent<CardPilePreviewUI>();
            preview.Configure(null,null,null,null,scope.Font,false);
            preview.ConfigureInspection(TrickCardDatabaseFactory.CreateDefault(),"抽牌堆",true);
            preview.Refresh(new List<CardData> { new CardData(CardType.Speed,1) },1);
            Button button = zone.GetComponent<Button>();
            var nav=button.navigation; nav.mode=Navigation.Mode.None; button.navigation=nav;
            var colors=button.colors; colors.fadeDuration=0f; button.colors=colors;
            RacingTechCardZoneStyle.ApplyLayout(scope.Root.transform,null);
            var pointer=new PointerEventData(scope.EventSystem) {button=PointerEventData.InputButton.Left};
            CheckContrast(button); button.OnPointerEnter(pointer); CheckContrast(button);
            button.OnPointerDown(pointer); CheckContrast(button); button.OnPointerUp(pointer); CheckContrast(button);
            button.OnPointerExit(pointer); button.OnSelect(new BaseEventData(scope.EventSystem)); CheckContrast(button);
            button.interactable=false; CheckContrast(button);
            Assert.That(button.colors,Is.EqualTo(colors));
        }
    }

    [Test]
    public void OnlyTwoShortTitlesGrowDownwardOnceAndUnresolvedRectsAreSkipped()
    {
        using (var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            Transform deck=Panel(scope.Root.transform,"DeckTablePanel");
            Transform hand=Panel(deck,"HandPanel");
            TMP_Text handTitle=Label(hand,"HandTitle",scope.Font);
            TMP_Text tableTitle=Label(deck,"DeckTableTitle",scope.Font);
            TMP_Text other=Label(Panel(deck,"EnginePanel"),"EnginePileTitle",scope.Font);
            foreach(TMP_Text title in new[]{handTitle,tableTitle,other})
            {
                title.text="牌桌"; title.fontSize=19f; title.enableWordWrapping=false;
                title.rectTransform.sizeDelta=new Vector2(160f,18f);
            }
            Canvas.ForceUpdateCanvases();
            Vector2 anchors=handTitle.rectTransform.anchorMin, top=handTitle.rectTransform.offsetMax;
            Vector2 otherLow=other.rectTransform.offsetMin;
            float oldHeight=handTitle.rectTransform.rect.height;
            Assert.That(handTitle.preferredHeight,Is.GreaterThan(oldHeight));
            RacingTechCardZoneStyle.ApplyLayout(deck,null);
            Assert.That(handTitle.rectTransform.rect.height,Is.GreaterThanOrEqualTo(handTitle.preferredHeight));
            Assert.That(tableTitle.rectTransform.rect.height,Is.GreaterThanOrEqualTo(tableTitle.preferredHeight));
            Assert.That(handTitle.rectTransform.anchorMin,Is.EqualTo(anchors));
            Assert.That(handTitle.rectTransform.offsetMax,Is.EqualTo(top));
            Assert.That(other.rectTransform.offsetMin,Is.EqualTo(otherLow));
            Vector2 fixedLow=handTitle.rectTransform.offsetMin;
            RacingTechCardZoneStyle.ApplyLayout(deck,null);
            Assert.That(handTitle.rectTransform.offsetMin,Is.EqualTo(fixedLow));
            handTitle.rectTransform.sizeDelta=Vector2.zero;
            Vector2 unresolved=handTitle.rectTransform.offsetMin;
            RacingTechCardZoneStyle.ApplyLayout(deck,null);
            Assert.That(handTitle.rectTransform.offsetMin,Is.EqualTo(unresolved));
        }
    }

    private static void CheckContrast(Button button)
    {
        Color fill=button.targetGraphic.color*button.targetGraphic.canvasRenderer.GetColor();
        // The zone sits on the existing graphite deck shell, including disabled tint alpha.
        Color opaque=Color.Lerp(RacingTechTheme.Surface,fill,fill.a); opaque.a=1f;
        Assert.That(Ratio(RacingTechTheme.Primary,opaque),Is.GreaterThanOrEqualTo(4.5f));
        Assert.That(Ratio(RacingTechTheme.Secondary,opaque),Is.GreaterThanOrEqualTo(4.5f));
    }
    private static float Ratio(Color a,Color b)
    {
        float x=Luminance(a),y=Luminance(b); return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);
    }
    private static float Luminance(Color c) => .2126f*Linear(c.r)+.7152f*Linear(c.g)+.0722f*Linear(c.b);
    private static float Linear(float c) => c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f);
    private static Transform Panel(Transform parent,string name)
    {
        var obj=new GameObject(name,typeof(RectTransform),typeof(Image)); obj.transform.SetParent(parent,false);
        obj.GetComponent<Image>().raycastTarget=false;
        return obj.transform;
    }
    private static TMP_Text Label(Transform parent,string name,TMP_FontAsset font)
    {
        var obj=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI)); obj.transform.SetParent(parent,false);
        TMP_Text text=obj.GetComponent<TMP_Text>(); text.font=font; text.text=name; text.raycastTarget=false;
        return text;
    }
}
