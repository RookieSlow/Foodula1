using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object=UnityEngine.Object;

public sealed class RacingTechSelectedEffectTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static TMP_FontAsset LoadFont()=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");

    [TestCase(false)]
    [TestCase(true)]
    public void EnsureThemesCreatedAndReferencedPanelWithoutReplacingCopyFontOrVisibility(bool referenced)
    {
        using(var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            var hand=scope.Root.AddComponent<CardHandUI>();
            RectTransform deck=MakeDeck(scope.Root.transform);
            if(referenced)
            {
                var panel=new GameObject("AuthoredEffect",typeof(RectTransform),typeof(Image),typeof(Outline)); panel.transform.SetParent(deck,false);
                var textObject=new GameObject("AuthoredText",typeof(RectTransform),typeof(TextMeshProUGUI)); textObject.transform.SetParent(panel.transform,false);
                hand.selectedCardEffectPanel=panel; hand.selectedCardEffectText=textObject.GetComponent<TMP_Text>();
                hand.selectedCardEffectText.font=scope.Font; hand.selectedCardEffectText.text="\u4fdd\u7559\u5168\u6587 999";
            }
            hand.EnsureSelectedCardEffectUI(deck,scope.Font);
            GameObject original=hand.selectedCardEffectPanel; TMP_Text label=hand.selectedCardEffectText;
            Assert.That(original,Is.Not.Null); Assert.That(label,Is.Not.Null);
            Assert.That(original.activeSelf,Is.EqualTo(referenced));
            Assert.That(label.text,Is.EqualTo(referenced?"\u4fdd\u7559\u5168\u6587 999":string.Empty));
            Assert.That(original.GetComponent<Image>().color,Is.EqualTo(RacingTechTheme.Surface));
            Assert.That(label.color,Is.EqualTo(RacingTechTheme.Primary)); Assert.That(label.font,Is.SameAs(scope.Font));
            int count=scope.Root.GetComponentsInChildren<Transform>(true).Length;
            hand.EnsureSelectedCardEffectUI(deck,scope.Font);
            Assert.That(hand.selectedCardEffectPanel,Is.SameAs(original)); Assert.That(hand.selectedCardEffectText,Is.SameAs(label));
            Assert.That(scope.Root.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
        }
    }

    [Test]
    public void ThemePreservesAuthoredRectsRichTextMaterialAndDoesNotRecolorNestedCardArt()
    {
        using(var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            var panel=new GameObject("Effect",typeof(RectTransform),typeof(Image),typeof(Outline)); panel.transform.SetParent(scope.Root.transform,false);
            var textObject=new GameObject("EffectText",typeof(RectTransform),typeof(TextMeshProUGUI)); textObject.transform.SetParent(panel.transform,false);
            TMP_Text text=textObject.GetComponent<TMP_Text>(); text.font=scope.Font; text.fontSize=21f;
            text.text=CardSelectionEffectRules.Format(new CardData(CardType.Speed,4),null);
            Material material=text.fontSharedMaterial; string copy=text.text;
            RectTransform rect=panel.GetComponent<RectTransform>(); rect.anchorMin=new Vector2(.17f,.61f); rect.anchorMax=new Vector2(.85f,.91f);
            rect.offsetMin=new Vector2(4f,7f); rect.offsetMax=new Vector2(-8f,-11f);
            Vector2 min=rect.anchorMin,max=rect.anchorMax,low=rect.offsetMin,high=rect.offsetMax;
            Image image=panel.GetComponent<Image>(); image.raycastTarget=false; text.raycastTarget=false;
            var art=new GameObject("NestedCard",typeof(RectTransform),typeof(Image),typeof(Outline)); art.transform.SetParent(panel.transform,false);
            art.GetComponent<Image>().color=Color.magenta; art.GetComponent<Outline>().effectDistance=new Vector2(7f,-7f);
            int count=panel.GetComponentsInChildren<Transform>(true).Length;
            for(int i=0;i<4;i++) RacingTechSelectedEffectStyle.Apply(panel,text);
            Assert.That(rect.anchorMin,Is.EqualTo(min)); Assert.That(rect.anchorMax,Is.EqualTo(max));
            Assert.That(rect.offsetMin,Is.EqualTo(low)); Assert.That(rect.offsetMax,Is.EqualTo(high));
            Assert.That(text.text,Is.EqualTo(copy)); Assert.That(text.fontSize,Is.EqualTo(21f));
            Assert.That(text.font,Is.SameAs(scope.Font)); Assert.That(text.fontSharedMaterial,Is.SameAs(material));
            Assert.That(image.raycastTarget||text.raycastTarget,Is.False);
            Assert.That(art.GetComponent<Image>().color,Is.EqualTo(Color.magenta));
            Assert.That(art.GetComponent<Outline>().effectDistance,Is.EqualTo(new Vector2(7f,-7f)));
            Assert.That(panel.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(count));
        }
    }

    [Test]
    public void OriginalKeyboardFocusSelectionAndClearKeepExactCardsAndFormattedExplanation()
    {
        using(var scope=CareerUiPointerTestScope.Create(LoadFont))
        using(var fixture=new HandFixture(scope.Root,scope.Font))
        {
            CardHandUI hand=fixture.Hand;
            CardData[] original=fixture.Player.deck.Hand.ToArray();
            Assert.That(hand.MoveKeyboardHighlight(1),Is.True);
            Assert.That(hand.IsSelectedCardEffectVisible,Is.True);
            Assert.That(hand.selectedCardEffectText.text,Is.EqualTo(CardSelectionEffectRules.Format(original[0],fixture.Session.TrickDb)));
            Assert.That(hand.GetSelectedCards(),Is.Empty,"Focus alone must not select or spend a card.");
            Assert.That(hand.ToggleKeyboardHighlightedCard(),Is.True);
            Assert.That(hand.GetSelectedCards(),Is.EqualTo(new[]{original[0]}));
            Assert.That(hand.MoveKeyboardHighlight(1),Is.True);
            Assert.That(hand.selectedCardEffectText.text,Is.EqualTo(CardSelectionEffectRules.Format(original[1],fixture.Session.TrickDb)));
            Assert.That(fixture.Player.deck.Hand,Is.EqualTo(original));
            hand.ClearPendingPlaySelection();
            Assert.That(hand.IsSelectedCardEffectVisible,Is.False); Assert.That(hand.selectedCardEffectText.text,Is.Empty);
            Assert.That(hand.GetSelectedCards(),Is.Empty); Assert.That(fixture.Player.deck.Hand,Is.EqualTo(original));
            hand.MoveKeyboardHighlight(1); hand.ClearHand();
            Assert.That(hand.IsSelectedCardEffectVisible,Is.False); Assert.That(hand.selectedCardEffectText.text,Is.Empty);
            Assert.That(fixture.Player.deck.Hand,Is.EqualTo(original));
        }
    }

    [Test]
    public void ExplanationHasReadablePrimaryAndOriginalRichTitleOnSurface()
    {
        Color gold=new Color32(0xFF,0xD2,0x7A,0xFF);
        Assert.That(Ratio(RacingTechTheme.Primary,RacingTechTheme.Surface),Is.GreaterThanOrEqualTo(4.5f));
        Assert.That(Ratio(gold,RacingTechTheme.Surface),Is.GreaterThanOrEqualTo(4.5f));
        Assert.DoesNotThrow(()=>RacingTechSelectedEffectStyle.Apply(null,null));
    }

    [UnityTest]
    public IEnumerator NativeShowSwitchHideReopenKeepsThemeAndEffectStripDoesNotBlockExistingPointer()
    {
        yield return new EnterPlayMode();
        Scene active=SceneManager.GetActiveScene(),scene=SceneManager.CreateScene("Owned selected effect runtime");
        GameObject root=null,eventHost=null; TMP_FontAsset font=null; Material material=null; HandFixture fixture=null;
        try
        {
            TMP_FontAsset source=LoadFont(); bool dirty=EditorUtility.IsDirty(source);
            font=Object.Instantiate(source); font.atlasPopulationMode=AtlasPopulationMode.Static;
            font.fallbackFontAssetTable=new List<TMP_FontAsset>(); material=Object.Instantiate(source.material); font.material=material;
            root=new GameObject("Owned selected effect canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster)); SceneManager.MoveGameObjectToScene(root,scene);
            root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var underlay=new GameObject("Owned original underlying action",typeof(RectTransform),typeof(Image),typeof(Button));
            underlay.transform.SetParent(root.transform,false);
            var underlayRect=underlay.GetComponent<RectTransform>(); underlayRect.anchorMin=Vector2.zero; underlayRect.anchorMax=Vector2.one;
            underlayRect.offsetMin=underlayRect.offsetMax=Vector2.zero;
            Button underlying=underlay.GetComponent<Button>(); underlying.targetGraphic=underlay.GetComponent<Image>();
            int clicks=0; underlying.onClick.AddListener(()=>clicks++);
            fixture=new HandFixture(root,font);
            eventHost=new GameObject("Owned selected effect pointer data"); eventHost.SetActive(false); SceneManager.MoveGameObjectToScene(eventHost,scene);
            EventSystem events=eventHost.AddComponent<EventSystem>();
            yield return null;
            Assert.That(fixture.Hand.MoveKeyboardHighlight(1),Is.True); yield return null;
            Assert.That(fixture.Hand.IsSelectedCardEffectVisible,Is.True);
            string first=fixture.Hand.selectedCardEffectText.text;
            Assert.That(fixture.Hand.MoveKeyboardHighlight(1),Is.True); yield return null;
            Assert.That(fixture.Hand.selectedCardEffectText.text,Is.Not.EqualTo(first));
            Canvas.ForceUpdateCanvases();
            var strip=fixture.Hand.selectedCardEffectPanel.GetComponent<RectTransform>();
            var pointer=new PointerEventData(events){position=RectTransformUtility.WorldToScreenPoint(null,strip.TransformPoint(strip.rect.center))};
            var hits=new List<RaycastResult>(); root.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
            Assert.That(hits.Exists(h=>h.gameObject.transform.IsChildOf(strip)||h.gameObject==strip.gameObject),Is.False);
            Assert.That(hits,Is.Not.Empty);
            GameObject receiver=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
            Assert.That(receiver,Is.SameAs(underlying.gameObject));
            Assert.That(ExecuteEvents.Execute(receiver,pointer,ExecuteEvents.pointerClickHandler),Is.True);
            Assert.That(clicks,Is.EqualTo(1)); Assert.That(fixture.Hand.GetSelectedCards(),Is.Empty);
            fixture.Hand.SetGearSelectionMode(true); yield return null;
            Assert.That(fixture.Hand.IsSelectedCardEffectVisible,Is.False);
            fixture.Hand.SetDiscardMode(true); fixture.Hand.MoveKeyboardHighlight(1); yield return null;
            Assert.That(fixture.Hand.IsSelectedCardEffectVisible,Is.True);
            Assert.That(fixture.Hand.selectedCardEffectText.color,Is.EqualTo(RacingTechTheme.Primary));
            fixture.Hand.HideAll(); yield return null;
            Assert.That(fixture.Hand.IsSelectedCardEffectVisible,Is.False);
            Assert.That(EditorUtility.IsDirty(source),Is.EqualTo(dirty)); Assert.That(SceneManager.GetActiveScene(),Is.EqualTo(active));
        }
        finally
        {
            fixture?.Dispose(); if(root!=null) Object.DestroyImmediate(root); if(eventHost!=null) Object.DestroyImmediate(eventHost);
            if(font!=null) Object.DestroyImmediate(font); if(material!=null) Object.DestroyImmediate(material);
        }
        yield return SceneManager.UnloadSceneAsync(scene); yield return new ExitPlayMode();
    }
    [UnityTearDown] public IEnumerator EnsureEditMode(){if(EditorApplication.isPlaying) yield return new ExitPlayMode();}

    private sealed class HandFixture:System.IDisposable
    {
        public CardHandUI Hand; public PlayerState Player; public RaceSession Session;
        private GameObject managerHost,prefabHost; private GameConfigSO config;
        private object savedAudio; private readonly FieldInfo audio=typeof(AudioService).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic);
        public HandFixture(GameObject root,TMP_FontAsset font)
        {
            savedAudio=audio.GetValue(null); audio.SetValue(null,null);
            try
            {
            managerHost=new GameObject("Owned inactive selected effect manager"); managerHost.SetActive(false);
            SceneManager.MoveGameObjectToScene(managerHost,root.scene);
            MVPGameManager manager=managerHost.AddComponent<MVPGameManager>(); config=ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree=false; manager.config=config;
            Session=new RaceSession(new SystemRandomSource(17));
            typeof(MVPGameManager).GetField("session",Private).SetValue(manager,Session);
            Player=new PlayerState("Owned explanation player",false,0,1){teamId=TeamId.UK};
            Player.deck.InitializeExactOrder(new[]{new CardData(CardType.Speed,2),CardData.CreateTrick("uk-scone")},new HeatPool(6));
            Player.deck.DrawToHand(2); Player.deck.DrawHeatFromPoolToHand(1); Session.Players.Add(Player);
            RectTransform deck=MakeDeck(root.transform);
            Hand=root.AddComponent<CardHandUI>();
            var container=new GameObject("Owned hand cards",typeof(RectTransform)); container.transform.SetParent(deck,false); Hand.handContainer=container.transform;
            prefabHost=new GameObject("Owned inactive card prefab parent"); prefabHost.SetActive(false); SceneManager.MoveGameObjectToScene(prefabHost,root.scene);
            var prefab=new GameObject("Owned card prefab",typeof(RectTransform),typeof(Image),typeof(CardUI)); prefab.transform.SetParent(prefabHost.transform,false);
            CardUI card=prefab.GetComponent<CardUI>(); card.backgroundImage=prefab.GetComponent<Image>(); card.selectionDuration=0f;
            var value=new GameObject("Value",typeof(RectTransform),typeof(TextMeshProUGUI)); value.transform.SetParent(prefab.transform,false);
            card.valueText=value.GetComponent<TMP_Text>(); card.valueText.font=font;
            Hand.cardPrefab=prefab; Hand.EnsureSelectedCardEffectUI(deck,font); Hand.SetDiscardMode(true); Hand.ShowHand(manager,Player);
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            try {if(managerHost!=null) Object.DestroyImmediate(managerHost); if(prefabHost!=null) Object.DestroyImmediate(prefabHost); if(config!=null) Object.DestroyImmediate(config);}
            finally {audio.SetValue(null,savedAudio);}
        }
    }
    private static RectTransform MakeDeck(Transform root)
    {
        var deck=new GameObject("DeckTablePanel",typeof(RectTransform)); deck.transform.SetParent(root,false);
        var rect=deck.GetComponent<RectTransform>(); rect.anchorMin=Vector2.zero; rect.anchorMax=Vector2.one; rect.offsetMin=rect.offsetMax=Vector2.zero; return rect;
    }
    private static float Ratio(Color a,Color b){float x=Luminance(a),y=Luminance(b);return(Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);}
    private static float Luminance(Color c)=>.2126f*Linear(c.r)+.7152f*Linear(c.g)+.0722f*Linear(c.b);
    private static float Linear(float c)=>c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f);
}
