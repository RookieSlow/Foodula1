using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Owned production inspector fixture. No gameplay scene, manager, persistence or audio.</summary>
public static class RacingTechPileInspectorCapture
{
    [Serializable] private sealed class Manifest
    {
        public string utc, stage;
        public string fixture = "Controlled EditMode owned Canvas. Original CardPilePreviewUI.Configure/ConfigureInspection/Refresh/ShowInspector creates all modal UI. Actual default catalog speed/trick cards; discard also heat. Diagnostic catalog inventory, not a normal race deck. No game manager or live scene.";
        public string lifecycle = "Cloned static Chinese font/material; native layout rebuild and ScrollRect.LateUpdate simulate synchronous preview lifecycle. Native normalized scroll position only, never assign content dimensions. After additionally centers the original longest effect row using native normalized scroll position. Selectable instant hover/pressed/disabled states are controlled visuals. Pointer routing is sampled with actual GraphicRaycaster and GetEventHandler, no content/close callback dispatched in EditMode. Native tests must prove deferred Destroy/close. All owned resources disposed; EventSystem inactive, user current event system untouched.";
        public List<Record> records = new List<Record>();
    }
    [Serializable] private sealed class Record
    {
        public string screenshot, mode, state;
        public int width, height, pileCount, entryCount;
        public float contentHeight, viewportHeight, normalizedPosition;
        public bool closeInteractable;
        public List<Label> labels = new List<Label>();
        public List<Route> routes = new List<Route>();
        public List<string> overflows = new List<string>();
    }
    [Serializable] private sealed class Label
    {
        public string name, text;
        public float width, height, preferredHeight;
        public bool tmpOverflow, withinScroll, intersectsViewport;
    }
    [Serializable] private sealed class Route
    {
        public string probe, topHit, clickHandler, directHandler;
        public float x, y;
    }
    [Serializable] private sealed class Summary { public int screenshots, overflowRecords; public string manifest; }

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Requires idle EditMode.");
        if (stage != "before" && stage != "after") throw new ArgumentException("Use before or after.", nameof(stage));
        string directory = Path.GetFullPath("production/qa/evidence/racing-tech-pile-inspector-2026-10-16/" + stage);
        Directory.CreateDirectory(directory);
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        var states = new List<string> { "draw-top", "draw-bottom", "discard-top", "discard-bottom", "close-hover", "close-pressed", "close-disabled" };
        if(stage == "after") states.Add("draw-longest-effect");
        foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(1280,720) })
            foreach (string state in states)
                CaptureOne(size, state, directory, manifest);
        string manifestPath = Path.Combine(directory, "manifest.json");
        File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true));
        int overflow = 0;
        foreach (Record record in manifest.records) overflow += record.overflows.Count;
        return JsonUtility.ToJson(new Summary { screenshots=manifest.records.Count, overflowRecords=overflow, manifest=manifestPath });
    }

    private static void CaptureOne(Vector2Int size, string state, string directory, Manifest manifest)
    {
        Scene scene = default;
        GameObject root=null, cameraHost=null, eventHost=null;
        TMP_FontAsset font=null;
        Material material=null;
        RenderTexture target=null, previous=RenderTexture.active;
        try
        {
            scene=EditorSceneManager.NewPreviewScene();
            root=new GameObject("Owned production pile inspector",typeof(RectTransform),typeof(Canvas),typeof(GraphicRaycaster));
            SceneManager.MoveGameObjectToScene(root,scene);
            cameraHost=new GameObject("Owned inspector camera",typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost,scene);
            Camera camera=cameraHost.GetComponent<Camera>();
            camera.enabled=false; camera.scene=scene; camera.cullingMask=1<<5;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.06f,.07f,.09f,1f);
            target=new RenderTexture(size.x,size.y,24);
            if (!target.Create()) throw new InvalidOperationException("Render allocation failed.");
            camera.targetTexture=target;
            Canvas canvas=root.GetComponent<Canvas>();
            canvas.renderMode=RenderMode.ScreenSpaceCamera; canvas.worldCamera=camera;
            canvas.planeDistance=1f; canvas.scaleFactor=size.x/1920f; canvas.sortingOrder=10000;
            eventHost=new GameObject("Inactive owned pointer event data");
            eventHost.SetActive(false); SceneManager.MoveGameObjectToScene(eventHost,scene);
            EventSystem events=eventHost.AddComponent<EventSystem>();
            TMP_FontAsset source=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset");
            if(source==null) throw new InvalidOperationException("Existing Chinese font missing.");
            font=Object.Instantiate(source); font.atlasPopulationMode=AtlasPopulationMode.Static;
            font.fallbackFontAssetTable=new List<TMP_FontAsset>();
            material=Object.Instantiate(source.material); font.material=material;
            var host=new GameObject("Owned pile source",typeof(RectTransform),typeof(Image),typeof(CardPilePreviewUI));
            host.transform.SetParent(root.transform,false);
            bool discard=state.StartsWith("discard",StringComparison.Ordinal);
            var database=TrickCardDatabaseFactory.CreateDefault();
            var pile=new List<CardData>();
            for(int i=0;i<8;i++) pile.Add(new CardData(CardType.Speed,i%4+1));
            foreach(TrickCardDef definition in database.cards.Values) pile.Add(CardData.CreateTrick(definition.id));
            if(discard){pile.Add(new CardData(CardType.Heat,0));pile.Add(new CardData(CardType.Heat,0));}
            CardPilePreviewUI preview=host.GetComponent<CardPilePreviewUI>();
            preview.Configure(null,null,null,null,font,discard);
            preview.ConfigureInspection(database,discard?"弃牌堆":"抽牌堆",!discard);
            preview.Refresh(pile,pile.Count);
            typeof(CardPilePreviewUI).GetMethod("ShowInspector",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(preview,null);
            Transform overlay=root.transform.Find("CardPileInspectorOverlay");
            if(overlay==null) throw new InvalidOperationException("Original inspector was not created.");
            Transform panel=overlay.Find("CardPileInspectorPanel");
            ScrollRect scroll=panel.GetComponentInChildren<ScrollRect>();
            Button close=panel.Find("CloseButton").GetComponent<Button>();
            foreach(TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)){text.font=font;text.fontSharedMaterial=material;}
            foreach(Transform node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=5;
            Rebuild(scroll);
            scroll.verticalNormalizedPosition=state.EndsWith("bottom",StringComparison.Ordinal)?0f:1f;
            Rebuild(scroll);
            if(state == "draw-longest-effect")
            {
                TMP_Text longest = null;
                foreach(TMP_Text text in scroll.content.GetComponentsInChildren<TMP_Text>())
                    if(text.name == "Effect" && (longest == null || text.text.Length > longest.text.Length)) longest = text;
                if(longest == null) throw new InvalidOperationException("Original effect row missing.");
                RectTransform row = (RectTransform)longest.transform.parent;
                float centerY = scroll.content.InverseTransformPoint(row.TransformPoint(row.rect.center)).y;
                float range = scroll.content.rect.height - scroll.viewport.rect.height;
                if(range > 0f)
                    scroll.verticalNormalizedPosition = 1f - Mathf.Clamp01((scroll.content.rect.yMax - centerY - scroll.viewport.rect.height * .5f) / range);
                Rebuild(scroll);
            }
            if(state.StartsWith("close-",StringComparison.Ordinal))
            {
                close.interactable=state!="close-disabled";
                string selection=state=="close-hover"?"Highlighted":state=="close-pressed"?"Pressed":"Disabled";
                Type enumType=typeof(Selectable).GetNestedType("SelectionState",BindingFlags.NonPublic);
                typeof(Selectable).GetMethod("DoStateTransition",BindingFlags.Instance|BindingFlags.NonPublic)
                    .Invoke(close,new[]{Enum.Parse(enumType,selection),(object)true});
            }
            Canvas.ForceUpdateCanvases(); camera.Render();
            var record=new Record { width=size.x,height=size.y,state=state,mode=discard?"discard-newest-first":"draw-grouped-hidden-order",
                pileCount=pile.Count,entryCount=scroll.content.childCount,contentHeight=scroll.content.rect.height,
                viewportHeight=scroll.viewport.rect.height,normalizedPosition=scroll.verticalNormalizedPosition,
                closeInteractable=close.interactable,screenshot=Path.Combine(directory,size.x+"x"+size.y+"-"+state+".png") };
            Rect viewportScreen=ScreenRect(scroll.viewport,camera);
            foreach(TMP_Text label in overlay.GetComponentsInChildren<TMP_Text>())
            {
                bool inScroll=label.transform.IsChildOf(scroll.content);
                record.labels.Add(new Label { name=label.transform.parent.name+"/"+label.name,text=label.text,
                    width=label.rectTransform.rect.width,height=label.rectTransform.rect.height,preferredHeight=label.preferredHeight,
                    tmpOverflow=label.isTextOverflowing,withinScroll=inScroll,
                    intersectsViewport=!inScroll||viewportScreen.Overlaps(ScreenRect(label.rectTransform,camera)) });
                if(label.isTextOverflowing)record.overflows.Add(label.transform.parent.name+"/"+label.name+": "+label.text);
            }
            GraphicRaycaster raycaster=root.GetComponent<GraphicRaycaster>();
            Probe(record,"panel-empty",(RectTransform)panel,camera,events,raycaster,new Vector2(0f,-310f));
            Probe(record,"viewport",scroll.viewport,camera,events,raycaster,null);
            foreach(Transform row in scroll.content)
            {
                Rect clipped=Intersect(ScreenRect((RectTransform)row,camera),viewportScreen);
                if(clipped.width<=0f||clipped.height<=12f)continue;
                ProbeAt(record,"row",row.gameObject,clipped.center,events,raycaster);
                Transform heading=row.Find("Heading");
                Rect headingClipped=Intersect(ScreenRect((RectTransform)heading,camera),viewportScreen);
                if(headingClipped.width>0f&&headingClipped.height>0f)
                    ProbeAt(record,"text",heading.gameObject,headingClipped.center,events,raycaster);
                break;
            }
            Probe(record,"close",close.GetComponent<RectTransform>(),camera,events,raycaster,null);
            Save(target,size,record.screenshot);
            manifest.records.Add(record);
        }
        finally
        {
            RenderTexture.active=previous;
            var failures=new List<Exception>();
            if(root!=null)Cleanup(()=>Object.DestroyImmediate(root),failures);
            if(eventHost!=null)Cleanup(()=>Object.DestroyImmediate(eventHost),failures);
            if(cameraHost!=null)Cleanup(()=>Object.DestroyImmediate(cameraHost),failures);
            if(target!=null){Cleanup(()=>target.Release(),failures);Cleanup(()=>Object.DestroyImmediate(target),failures);}
            if(font!=null)Cleanup(()=>Object.DestroyImmediate(font),failures);
            if(material!=null)Cleanup(()=>Object.DestroyImmediate(material),failures);
            if(scene.IsValid())Cleanup(()=>EditorSceneManager.ClosePreviewScene(scene),failures);
            if(failures.Count>0)throw new AggregateException("Inspector preview cleanup failed.",failures);
        }
    }
    private static void Rebuild(ScrollRect scroll)
    {
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(scroll.content);
        foreach(TMP_Text label in scroll.GetComponentsInChildren<TMP_Text>())label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        typeof(ScrollRect).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(scroll,null);
        Canvas.ForceUpdateCanvases();
    }
    private static Rect ScreenRect(RectTransform rect,Camera camera)
    {
        Vector3[] corners=new Vector3[4];rect.GetWorldCorners(corners);
        Vector2 a=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
        Vector2 b=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return Rect.MinMaxRect(a.x,a.y,b.x,b.y);
    }
    private static Rect Intersect(Rect a,Rect b)=>Rect.MinMaxRect(Mathf.Max(a.xMin,b.xMin),Mathf.Max(a.yMin,b.yMin),Mathf.Min(a.xMax,b.xMax),Mathf.Min(a.yMax,b.yMax));
    private static void Probe(Record record,string name,RectTransform rect,Camera camera,EventSystem events,GraphicRaycaster raycaster,Vector2? local)
    {
        Vector2 point=RectTransformUtility.WorldToScreenPoint(camera,rect.TransformPoint(local??rect.rect.center));
        ProbeAt(record,name,rect.gameObject,point,events,raycaster);
    }
    private static void ProbeAt(Record record,string name,GameObject source,Vector2 point,EventSystem events,GraphicRaycaster raycaster)
    {
        var pointer=new PointerEventData(events){position=point,button=PointerEventData.InputButton.Left};
        var hits=new List<RaycastResult>();raycaster.Raycast(pointer,hits);
        GameObject top=hits.Count>0?hits[0].gameObject:null;
        GameObject handler=top!=null?ExecuteEvents.GetEventHandler<IPointerClickHandler>(top):null;
        GameObject direct=ExecuteEvents.GetEventHandler<IPointerClickHandler>(source);
        record.routes.Add(new Route{probe=name,x=point.x,y=point.y,topHit=top!=null?top.name:"none",clickHandler=handler!=null?handler.name:"none",directHandler=direct!=null?direct.name:"none"});
    }
    private static void Save(RenderTexture target,Vector2Int size,string path)
    {
        Texture2D texture=null; RenderTexture previous=RenderTexture.active;
        try{RenderTexture.active=target;texture=new Texture2D(size.x,size.y,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,size.x,size.y),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{RenderTexture.active=previous;if(texture!=null)Object.DestroyImmediate(texture);}
    }
    private static void Cleanup(Action action,List<Exception> errors){try{action();}catch(Exception error){errors.Add(error);}}
}
