using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class RacingTechPileInspectorTests
{
    private static TMP_FontAsset LoadFont() => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
        "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");

    [TestCase(false)]
    [TestCase(true)]
    public void ActualInspectorKeepsExactDrawGroupingOrDiscardOrderAndSourceInstances(bool discard)
    {
        using(var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            var pile=new List<CardData> {new CardData(CardType.Speed,3),new CardData(CardType.Speed,1),new CardData(CardType.Speed,3),CardData.CreateTrick("uk-scone"),new CardData(CardType.Heat,0)};
            CardData[] before=pile.ToArray();
            var preview=Preview(scope.Root.transform,scope.Font,discard);
            preview.Refresh(pile,pile.Count);
            scope.Click(preview.GetComponent<Button>());
            Transform panel=scope.Root.transform.Find("CardPileInspectorOverlay/CardPileInspectorPanel");
            Assert.That(panel,Is.Not.Null);
            var expected=CardPileInspectorRules.BuildEntries(pile,discard,!discard,TrickCardDatabaseFactory.CreateDefault());
            Transform content=panel.Find("Viewport/Content");
            Assert.That(content.childCount,Is.EqualTo(discard?5:4));
            for(int i=0;i<expected.Count;i++)
            {
                Transform row=content.GetChild(i);
                string heading=expected[i].Count>1?$"{expected[i].Title}  ×{expected[i].Count}":expected[i].Title;
                Assert.That(row.Find("Heading").GetComponent<TMP_Text>().text,Is.EqualTo(heading));
                Assert.That(row.Find("Effect").GetComponent<TMP_Text>().text,Is.EqualTo(expected[i].Effect));
                Assert.That(row.Find("Heading").GetComponent<TMP_Text>().color,Is.EqualTo(RacingTechTheme.Primary));
                Assert.That(row.Find("Effect").GetComponent<TMP_Text>().color,Is.EqualTo(RacingTechTheme.Secondary));
                Assert.That(row.GetComponent<Image>().color,Is.EqualTo(RacingTechTheme.Raised));
                Assert.That(pile[i],Is.SameAs(before[i]));
            }
            Assert.That(pile,Is.EqualTo(before));
            Assert.That(panel.Find("Title").GetComponent<TMP_Text>().text,Is.EqualTo((discard?"弃牌堆":"抽牌堆")+" · 5 张"));
            Assert.That(panel.Find("OrderNote").GetComponent<TMP_Text>().text,Is.EqualTo(discard?"按弃牌时间从新到旧显示。":"为避免泄露抽牌顺序，这里按种类汇总显示牌堆组成。"));
            Assert.That(panel.GetComponent<Image>().color,Is.EqualTo(RacingTechTheme.Surface));
            Button blocker=panel.GetComponent<Button>();
            Assert.That(blocker,Is.Not.Null); Assert.That(blocker.transition,Is.EqualTo(Selectable.Transition.None));
            Assert.That(blocker.navigation.mode,Is.EqualTo(Navigation.Mode.None));
            Assert.That(blocker.targetGraphic,Is.SameAs(panel.GetComponent<Image>()));
            Assert.That(blocker.onClick.GetPersistentEventCount(),Is.Zero);
            Assert.That(panel.Find("Viewport").GetComponent<Image>().color,Is.EqualTo(RacingTechTheme.Graphite));
        }
    }

    [Test]
    public void RepeatedStyleKeepsGeometryFontsMaskScrollButtonsAndMiniCardIdentity()
    {
        using(var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            var preview=Preview(scope.Root.transform,scope.Font,false);
            preview.Refresh(new[]{new CardData(CardType.Heat,0),new CardData(CardType.Speed,2),CardData.CreateTrick("uk-scone")},3);
            preview.GetComponent<Button>().onClick.Invoke();
            Transform overlay=scope.Root.transform.Find("CardPileInspectorOverlay");
            Transform panel=overlay.Find("CardPileInspectorPanel");
            RectTransform[] rects=overlay.GetComponentsInChildren<RectTransform>(true);
            Vector2[] mins=rects.Select(r=>r.anchorMin).ToArray(),maxs=rects.Select(r=>r.anchorMax).ToArray(),sizes=rects.Select(r=>r.sizeDelta).ToArray(),positions=rects.Select(r=>r.anchoredPosition).ToArray();
            TMP_Text[] labels=overlay.GetComponentsInChildren<TMP_Text>(true);
            string[] text=labels.Select(t=>t.text).ToArray(); float[] size=labels.Select(t=>t.fontSize).ToArray();
            Material[] materials=labels.Select(t=>t.fontSharedMaterial).ToArray();
            Image[] mini=overlay.GetComponentsInChildren<Image>(true).Where(i=>i.name=="MiniCard").ToArray();
            Color[] miniColors=mini.Select(i=>i.color).ToArray();
            Button dismiss=overlay.GetComponent<Button>(),close=panel.Find("CloseButton").GetComponent<Button>();
            Color mask=overlay.GetComponent<Image>().color; ColorBlock states=close.colors;
            var nav=close.navigation; var transition=close.transition; Graphic target=close.targetGraphic;
            var scroll=panel.Find("Viewport").GetComponent<ScrollRect>();
            RectTransform viewport=scroll.viewport,content=scroll.content;
            float sensitivity=scroll.scrollSensitivity; var movement=scroll.movementType;
            int children=overlay.GetComponentsInChildren<Transform>(true).Length;
            TMP_Text title=panel.Find("Title").GetComponent<TMP_Text>(),note=panel.Find("OrderNote").GetComponent<TMP_Text>();
            for(int i=0;i<5;i++) RacingTechPileInspectorStyle.Apply(panel,title,note,close,scroll);
            Assert.That(overlay.GetComponentsInChildren<Transform>(true).Length,Is.EqualTo(children));
            for(int i=0;i<rects.Length;i++)
            {
                Assert.That(rects[i].anchorMin,Is.EqualTo(mins[i])); Assert.That(rects[i].anchorMax,Is.EqualTo(maxs[i]));
                Assert.That(rects[i].sizeDelta,Is.EqualTo(sizes[i])); Assert.That(rects[i].anchoredPosition,Is.EqualTo(positions[i]));
            }
            for(int i=0;i<labels.Length;i++)
            {
                Assert.That(labels[i].text,Is.EqualTo(text[i])); Assert.That(labels[i].fontSize,Is.EqualTo(size[i]));
                Assert.That(labels[i].font,Is.SameAs(scope.Font)); Assert.That(labels[i].fontSharedMaterial,Is.SameAs(materials[i]));
            }
            for(int i=0;i<mini.Length;i++) Assert.That(mini[i].color,Is.EqualTo(miniColors[i]));
            Assert.That(overlay.GetComponent<Image>().color,Is.EqualTo(mask)); Assert.That(mask.a,Is.EqualTo(.72f));
            Assert.That(dismiss.targetGraphic,Is.SameAs(overlay.GetComponent<Image>()));
            Assert.That(close.targetGraphic,Is.SameAs(target)); Assert.That(close.colors,Is.EqualTo(states));
            Assert.That(close.transition,Is.EqualTo(transition)); Assert.That(close.navigation,Is.EqualTo(nav));
            Assert.That(scroll.viewport,Is.SameAs(viewport)); Assert.That(scroll.content,Is.SameAs(content));
            Assert.That(scroll.scrollSensitivity,Is.EqualTo(sensitivity)); Assert.That(scroll.movementType,Is.EqualTo(movement));
            Assert.That(scroll.GetComponent<RectMask2D>(),Is.Not.Null);
        }
    }

    [Test]
    public void ClosePointerStatesStayReadableWithoutChangingExistingTintOrDisabledGate()
    {
        using(var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            var preview=Preview(scope.Root.transform,scope.Font,false);
            preview.Refresh(new[]{new CardData(CardType.Speed,1)},1); preview.GetComponent<Button>().onClick.Invoke();
            Transform panel=scope.Root.transform.Find("CardPileInspectorOverlay/CardPileInspectorPanel");
            Button close=panel.Find("CloseButton").GetComponent<Button>();
            var nav=close.navigation; nav.mode=Navigation.Mode.None; close.navigation=nav;
            var colors=close.colors; colors.fadeDuration=0f; close.colors=colors;
            var pointer=new PointerEventData(scope.EventSystem){button=PointerEventData.InputButton.Left};
            Contrast(close); close.OnPointerEnter(pointer); Contrast(close); close.OnPointerDown(pointer); Contrast(close);
            close.OnPointerUp(pointer); Contrast(close); close.OnPointerExit(pointer);
            close.OnSelect(new BaseEventData(scope.EventSystem)); Contrast(close);
            close.interactable=false; Contrast(close);
            ExecuteEvents.Execute(close.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(panel!=null,Is.True); Assert.That(close.colors,Is.EqualTo(colors));
            close.transition=Selectable.Transition.None;
            RacingTechPileInspectorStyle.Apply(panel,null,null,close,null); Contrast(close);
            Assert.That(close.transition,Is.EqualTo(Selectable.Transition.None));
        }
    }

    [Test]
    public void EmptyPileCannotOpenInspectorAndMissingOptionalStyleReferencesAreSafe()
    {
        using(var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            var preview=Preview(scope.Root.transform,scope.Font,false);
            preview.Refresh(new List<CardData>(),0);
            ExecuteEvents.Execute(preview.gameObject,new PointerEventData(scope.EventSystem){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            Assert.That(scope.Root.transform.Find("CardPileInspectorOverlay"),Is.Null);
            Assert.DoesNotThrow(()=>RacingTechPileInspectorStyle.Apply(null,null,null,null,null));
            Assert.DoesNotThrow(()=>RacingTechPileInspectorStyle.Apply(scope.Root.transform,null,null,null,null));
        }
    }

    [Test]
    public void RestylingPreservesExistingScrollPositionAndLayoutConfiguration()
    {
        using(var scope=CareerUiPointerTestScope.Create(LoadFont))
        {
            var preview=Preview(scope.Root.transform,scope.Font,true);
            var pile=new List<CardData>(); for(int i=0;i<12;i++) pile.Add(new CardData(CardType.Speed,1+i%4));
            preview.Refresh(pile,12); preview.GetComponent<Button>().onClick.Invoke();
            Transform panel=scope.Root.transform.Find("CardPileInspectorOverlay/CardPileInspectorPanel");
            ScrollRect scroll=panel.Find("Viewport").GetComponent<ScrollRect>();
            LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
            scroll.verticalNormalizedPosition=.37f;
            Vector2 position=scroll.content.anchoredPosition;
            var layout=scroll.content.GetComponent<VerticalLayoutGroup>();
            var fitter=scroll.content.GetComponent<ContentSizeFitter>();
            float spacing=layout.spacing; int padding=layout.padding.top;
            var fit=fitter.verticalFit; float height=scroll.content.rect.height;
            RacingTechPileInspectorStyle.Apply(panel,null,null,null,scroll);
            Assert.That(scroll.content.anchoredPosition,Is.EqualTo(position));
            Assert.That(scroll.content.rect.height,Is.EqualTo(height));
            Assert.That(layout.spacing,Is.EqualTo(spacing)); Assert.That(layout.padding.top,Is.EqualTo(padding));
            Assert.That(fitter.verticalFit,Is.EqualTo(fit));
            Assert.That(scroll.content.childCount,Is.EqualTo(12));
            Assert.That(scroll.horizontal,Is.False); Assert.That(scroll.vertical,Is.True);
        }
    }

    [UnityTest]
    public IEnumerator NativeInspectorWheelReachesLastRowAndOriginalCloseMaskReopenLifecycleWorks()
    {
        yield return new EnterPlayMode();
        Scene active=SceneManager.GetActiveScene();
        Scene scene=SceneManager.CreateScene("Owned pile inspector runtime");
        GameObject root=null,eventHost=null;
        TMP_FontAsset font=null; Material material=null;
        try
        {
            TMP_FontAsset source=LoadFont(); bool dirty=EditorUtility.IsDirty(source);
            font=Object.Instantiate(source); font.atlasPopulationMode=AtlasPopulationMode.Static;
            font.fallbackFontAssetTable=new List<TMP_FontAsset>(); material=Object.Instantiate(source.material); font.material=material;
            root=new GameObject("Owned pile inspector canvas",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root,scene); root.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=root.AddComponent<CanvasScaler>(); scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution=new Vector2(1920f,1080f);
            eventHost=new GameObject("Owned inactive pile event data"); eventHost.SetActive(false); SceneManager.MoveGameObjectToScene(eventHost,scene);
            EventSystem events=eventHost.AddComponent<EventSystem>();
            var underlay=new GameObject("Owned underlying action",typeof(RectTransform),typeof(Image),typeof(Button));
            underlay.transform.SetParent(root.transform,false);
            var underlayRect=underlay.GetComponent<RectTransform>(); underlayRect.anchorMin=Vector2.zero; underlayRect.anchorMax=Vector2.one;
            underlayRect.offsetMin=underlayRect.offsetMax=Vector2.zero;
            Button underlying=underlay.GetComponent<Button>(); underlying.targetGraphic=underlay.GetComponent<Image>();
            int underlyingCalls=0; underlying.onClick.AddListener(()=>underlyingCalls++);
            var preview=Preview(root.transform,font,true);
            var pile=new List<CardData>(); for(int i=0;i<12;i++) pile.Add(new CardData(CardType.Speed,1+i%4));
            preview.Refresh(pile,12); preview.GetComponent<Button>().onClick.Invoke();
            yield return null; Canvas.ForceUpdateCanvases();
            Transform overlay=root.transform.Find("CardPileInspectorOverlay"),panel=overlay.Find("CardPileInspectorPanel");
            ScrollRect scroll=panel.Find("Viewport").GetComponent<ScrollRect>();
            Assert.That(scroll.content.rect.height,Is.GreaterThan(scroll.viewport.rect.height));
            Vector2 contentPoint=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center));
            ClickAt(root,events,contentPoint,panel.gameObject);
            yield return null;
            Assert.That(overlay!=null && overlay.gameObject.activeInHierarchy,Is.True,"Content click must retain the original overlay.");
            Assert.That(underlyingCalls,Is.Zero);
            float wheelDistance=(scroll.content.rect.height-scroll.viewport.rect.height)/scroll.scrollSensitivity;
            var wheel=new PointerEventData(events){scrollDelta=new Vector2(0f,-wheelDistance)};
            Assert.That(ExecuteEvents.Execute(scroll.gameObject,wheel,ExecuteEvents.scrollHandler),Is.True);
            yield return null; Canvas.ForceUpdateCanvases();
            Assert.That(scroll.verticalNormalizedPosition,Is.LessThan(.01f));
            RectTransform last=(RectTransform)scroll.content.GetChild(11);
            Vector2 point=RectTransformUtility.WorldToScreenPoint(null,last.TransformPoint(last.rect.center));
            Assert.That(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport,point),Is.True);
            Click(root,events,panel.Find("CloseButton").GetComponent<RectTransform>());
            yield return null; Assert.That(overlay==null,Is.True); Assert.That(underlyingCalls,Is.Zero);
            preview.GetComponent<Button>().onClick.Invoke(); yield return null;
            overlay=root.transform.Find("CardPileInspectorOverlay");
            Assert.That(overlay,Is.Not.Null);
            // A corner outside the centered modal remains the existing dismiss mask target.
            ClickAt(root,events,new Vector2(2f,2f),overlay.gameObject);
            yield return null; Assert.That(overlay==null,Is.True); Assert.That(underlyingCalls,Is.Zero);
            Assert.That(pile.Count,Is.EqualTo(12)); Assert.That(EditorUtility.IsDirty(source),Is.EqualTo(dirty));
            Assert.That(SceneManager.GetActiveScene(),Is.EqualTo(active));
        }
        finally
        {
            if(root!=null) Object.DestroyImmediate(root); if(eventHost!=null) Object.DestroyImmediate(eventHost);
            if(font!=null) Object.DestroyImmediate(font); if(material!=null) Object.DestroyImmediate(material);
        }
        yield return SceneManager.UnloadSceneAsync(scene);
        yield return new ExitPlayMode();
    }

    [UnityTearDown] public IEnumerator EnsureEditMode(){if(EditorApplication.isPlaying) yield return new ExitPlayMode();}
    private static void Click(GameObject root,EventSystem events,RectTransform rect)
    {
        Vector2 point=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        ClickAt(root,events,point,rect.gameObject);
    }
    private static void ClickAt(GameObject root,EventSystem events,Vector2 point,GameObject expected)
    {
        Canvas.ForceUpdateCanvases();
        var pointer=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>(); root.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
        Assert.That(hits,Is.Not.Empty);
        GameObject target=ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.That(target,Is.SameAs(expected));
        Assert.That(ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler),Is.True);
    }
    private static CardPilePreviewUI Preview(Transform parent,TMP_FontAsset font,bool discard)
    {
        var host=new GameObject(discard?"DiscardPilePanel":"DrawPilePanel",typeof(RectTransform),typeof(Image)); host.transform.SetParent(parent,false);
        host.GetComponent<RectTransform>().sizeDelta=new Vector2(260f,260f);
        var preview=host.AddComponent<CardPilePreviewUI>(); preview.Configure(null,null,null,null,font,discard);
        preview.ConfigureInspection(TrickCardDatabaseFactory.CreateDefault(),discard?"弃牌堆":"抽牌堆",!discard);
        return preview;
    }
    private static void Contrast(Button close)
    {
        TMP_Text label=close.GetComponentInChildren<TMP_Text>();
        Color a=label.color*label.canvasRenderer.GetColor(),b=close.targetGraphic.color*close.targetGraphic.canvasRenderer.GetColor();
        float x=Luminance(a),y=Luminance(b);
        Assert.That((Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f),Is.GreaterThanOrEqualTo(4.5f));
    }
    private static float Luminance(Color c)=>.2126f*Linear(c.r)+.7152f*Linear(c.g)+.0722f*Linear(c.b);
    private static float Linear(float c)=>c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f);
}
