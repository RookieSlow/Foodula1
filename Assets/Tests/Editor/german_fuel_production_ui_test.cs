using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Owned Canvas with real HUD.Start/Manager callbacks, production-sized action slot; no match or hardware input.</summary>
public static class GermanFuelProductionUiCapture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly string[] Fixtures = { "confirm", "keyboard", "cancel-movement", "empty", "used", "stale-input", "lifecycle", "reopen" };
    [Serializable] public sealed class Record
    {
        public int width, height, mask, roster, paid;
        public string fixture, team, driver, screenshot;
        public bool modal, blocked, focus;
        public List<string> overflows = new List<string>();
    }
    [Serializable] private sealed class Manifest
    {
        public string utc, stage, runtimeMvid;
        public string kind = "Owned EditMode Canvas, real HUD.Start/action widget/Manager fixed-cost plan. Production normalized action-slot dimensions; owned surrounding controls. EventSystem/GraphicRaycaster dispatch, not full RaceCanvas, hardware input or complete modes.";
        public List<Record> records = new List<Record>();
    }
    public static string Capture(string stage)
    {
        bool connected = typeof(MVPGameManager).GetMethod("OnGermanFuelButtonClicked") != null;
        if (stage == "before" && connected) throw new InvalidOperationException("Cannot fabricate old UI after migration.");
        if (stage != "before" && stage != "after") throw new ArgumentException("before or after");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || ModularRegressionRunner.NativeTestsRunning()) throw new InvalidOperationException("Capture requires idle EditMode");
        string dir = Path.GetFullPath("production/qa/evidence/tech-driver-I03d-2026-10-27/" + stage + "/run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(dir);
        var manifest = new Manifest { stage = stage, utc = DateTime.UtcNow.ToString("o"), runtimeMvid = typeof(RaceSession).Assembly.ManifestModule.ModuleVersionId.ToString() };
        foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(1280,720) })
            for (int i = 0; i < Fixtures.Length; i++) manifest.records.Add(Exercise(size.x,size.y,i,(int)InRaceConfirmationAction.All,stage == "after",Path.Combine(dir,size.x+"x"+size.y+"-"+Fixtures[i]+".png")));
        File.WriteAllText(Path.Combine(dir,"manifest.json"),JsonUtility.ToJson(manifest,true)); return Path.Combine(dir,"manifest.json");
    }
    public static Record Exercise(int width,int height,int index,int mask,bool after,string screenshot = null)
    {
        object oldSettings = Settings.GetValue(null);
        var audio = typeof(AudioService).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic); object oldAudio = audio.GetValue(null);
        using var startup = new RaceStartupTestScope();
        EventSystem oldEvents = EventSystem.current; GameObject oldSelection = oldEvents != null ? oldEvents.currentSelectedGameObject : null;
        CareerUiPointerTestScope scope = null; HUDUI hud = null; EventSystem events = null; GameConfigSO config = null;
        try
        {
            var settings = GameSettingsData.CreateDefault(); settings.inRaceConfirmationMask = mask; settings.masterVolume = 0; settings.reduceMotion = true;
            Settings.SetValue(null,settings); audio.SetValue(null,null);
            scope = CareerUiPointerTestScope.CreateSized(width,height,()=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
            hud = scope.Root.AddComponent<HUDUI>();
            var panel = new GameObject("Owned OperationPanel",typeof(RectTransform)); panel.transform.SetParent(scope.Root.transform,false);
            var panelRect = panel.GetComponent<RectTransform>(); panelRect.anchoredPosition = new Vector2(670,0); panelRect.sizeDelta = new Vector2(340,800);
            hud.driverSkillButton = MakeButton(panel.transform,scope.Font,"DriverSkill",new Vector2(0,0),new Vector2(285,76));
            var slot = hud.driverSkillButton.GetComponent<RectTransform>(); slot.anchorMin = RaceUILayoutController.DriverSkillAnchorMin; slot.anchorMax = RaceUILayoutController.DriverSkillAnchorMax; slot.offsetMin = slot.offsetMax = Vector2.zero;
            var gears = new GameObject("GearButtons",typeof(RectTransform)); gears.transform.SetParent(scope.Root.transform,false);
            var gearRect = gears.GetComponent<RectTransform>(); gearRect.anchoredPosition = new Vector2(-500,0); gearRect.sizeDelta = new Vector2(300,330);
            hud.gear1Button = MakeButton(gears.transform,scope.Font,"Legal return",Vector2.zero,new Vector2(250,70));
            hud.statusText = MakeLabel(scope.Root.transform,scope.Font,"Owned status",new Vector2(0,-340),new Vector2(1500,160));
            typeof(HUDUI).GetMethod("Start",Private).Invoke(hud,null);
            var eventHost = new GameObject("Owned active events",typeof(EventSystem)); eventHost.transform.SetParent(scope.Root.transform,false); events = eventHost.GetComponent<EventSystem>();
            typeof(EventSystem).GetMethod("OnEnable",Private).Invoke(events,null); EventSystem.current = events;
            var host = new GameObject("Owned inactive manager"); host.SetActive(false); host.transform.SetParent(scope.Root.transform,false);
            var manager = host.AddComponent<MVPGameManager>(); manager.hudUI = hud;
            manager.cardHandUI = host.AddComponent<CardHandUI>();
            manager.cardHandUI.playCardsButton = MakeButton(scope.Root.transform,scope.Font,"Owned card action",new Vector2(-500,-280),new Vector2(300,70));
            manager.cardHandUI.handContainer = new GameObject("Owned inactive hand",typeof(RectTransform)).transform;
            manager.cardHandUI.handContainer.SetParent(host.transform,false);
            manager.cardHandUI.cardPrefab = new GameObject("Owned card prefab",typeof(RectTransform),typeof(Image),typeof(CardUI));
            manager.cardHandUI.cardPrefab.transform.SetParent(host.transform,false);
            config = ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree = true; manager.config = config;
            var track = host.AddComponent<TrackManager>(); manager.trackManager = track;
            typeof(TrackManager).GetField("nodes",Private).SetValue(track,Enumerable.Range(0,60).Select(i=>new TrackNode(i,99)).ToList());
            var race = new RaceSession(new SystemRandomSource(13)) { TeamVehicleBonusesEnabled = false };
            var profile = DriverCatalog.All.Where(d=>d.Team == TeamId.DE).ElementAt(index%2);
            var p = new PlayerState("德国队·长中文车手1234567890",false,0,1) { teamId=TeamId.DE,driverId=profile.Id,techState=new TechTreeState(TeamId.DE) };
            p.driverSkill.Initialize(profile,7,true); p.techState.activeNodeIds.Add("de-l1-schwarzbier-fuel");
            p.deck.InitializeExactOrder(new[] {new CardData(CardType.Speed,2),new CardData(CardType.Speed,3)},new HeatPool(5)); p.deck.DrawToHand(2);
            race.Players.Add(p); race.BeginTurn(p,1);
            TeamId team = (TeamId)(index%6); int roster = new[] {2,4,6,12}[index%4];
            for(int i=1;i<roster;i++) race.Players.Add(new PlayerState("周边车队"+team+"·"+i,true,20+i,1){teamId=team});
            typeof(MVPGameManager).GetField("session",Private).SetValue(manager,race); typeof(MVPGameManager).GetField("raceTurnNumber",Private).SetValue(manager,1);
            var phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState",Private).GetValue(manager); phase.BeginCardSelection();
            var input = (RaceInputState)typeof(MVPGameManager).GetField("inputState",Private).GetValue(manager); input.BeginCardSelection();
            if(index==3) p.deck.heatPool.remaining=0;
            if(index==4) Assert.That(race.TryCommitSchwarzbierFuel(p,true,out _),Is.True);
            hud.SetGameManager(manager); hud.RefreshDriverSkill(manager,p); hud.RefreshTeamGearPresentation();
            hud.SetStatus(p.name+" / 周边"+team+" / "+roster+"辆 · 生产受控夹具");
            var record = new Record {width=width,height=height,fixture=Fixtures[index],mask=mask,roster=roster,team=team.ToString(),driver=profile.Id,screenshot=screenshot};
            if(!after) { Render(scope,hud,screenshot,record); return record; }
            var button = (Button)typeof(HUDUI).GetProperty("GermanFuelButton").GetValue(hud);
            Assert.That(button,Is.Not.Null); Assert.That(button.gameObject.activeInHierarchy,Is.True);
            int engine = p.deck.heatPool.remaining;
            if(index==3 || index==4)
            { Assert.That(button.IsInteractable(),Is.False); Render(scope,hud,screenshot,record); return record; }
            events.SetSelectedGameObject(button.gameObject);
            if(index==1) ExecuteEvents.Execute(button.gameObject,new BaseEventData(events),ExecuteEvents.submitHandler);
            else scope.Click(button);
            record.modal=hud.IsReturnToMenuConfirmationVisible;
            Assert.That(record.modal,Is.EqualTo(mask != 0));
            if(record.modal)
            {
                record.blocked=Blocked(scope,hud.gear1Button,hud.returnToMenuConfirmPanel); Assert.That(record.blocked,Is.True);
                if(index!=2) Render(scope,hud,screenshot,record);
                if(index==2 || index==7)
                {
                    scope.Click(hud.cancelReturnToMenuButton); Assert.That(p.deck.heatPool.remaining,Is.EqualTo(engine));
                    if(index==7) { scope.Click(button); Render(scope,hud,screenshot,record); scope.Click(hud.cancelReturnToMenuButton); }
                    else Render(scope,hud,screenshot,record);
                }
                else if(index==6)
                { hud.gameObject.SetActive(false); typeof(HUDUI).GetMethod("OnDisable",Private).Invoke(hud,null); hud.gameObject.SetActive(true); hud.RefreshTeamGearPresentation(); }
                else
                {
                    if(index==5) input.EndCardSelection();
                    if(index==1) { events.SetSelectedGameObject(hud.confirmReturnToMenuButton.gameObject); ExecuteEvents.Execute(events.currentSelectedGameObject,new BaseEventData(events),ExecuteEvents.submitHandler); }
                    else scope.Click(hud.confirmReturnToMenuButton);
                }
            }
            else Render(scope,hud,screenshot,record);
            bool committed = index==0 || index==1;
            Assert.That(p.deck.heatPool.remaining,Is.EqualTo(engine-(committed?1:0)));
            record.paid=committed?1:0;
            hud.ConfirmReturnToMenu(); Assert.That(p.deck.heatPool.remaining,Is.EqualTo(engine-(committed?1:0)));
            // Actual coordinator movement after cancel/expired request must not silently buy fuel.
            if(index==5) input.BeginCardSelection(); // A new valid input does not revive the failed prior request.
            Assert.That((bool)typeof(MVPGameManager).GetMethod("ConfirmPlayerSpeedCards",Private)
                .Invoke(manager,new object[]{p,new[]{p.deck.Hand[0]}}),Is.True);
            typeof(MVPGameManager).GetMethod("ComputeMovements",Private).Invoke(manager,new object[]{new List<PlayerState>{p},new HashSet<PlayerState>()});
            Assert.That(p.deck.heatPool.remaining,Is.EqualTo(engine-(committed?1:0)));
            Assert.That(p.totalMovementThisTurn,Is.EqualTo(2+(committed?2:0)));
            record.focus=events.currentSelectedGameObject!=null && events.currentSelectedGameObject.activeInHierarchy;
            if(index!=5 && index!=6) Assert.That(record.focus,Is.True);
            return record;
        }
        finally
        {
            if(hud!=null) hud.SetGameManager(null);
            if(events!=null) typeof(EventSystem).GetMethod("OnDisable",Private).Invoke(events,null);
            scope?.Dispose(); if(config!=null) Object.DestroyImmediate(config); Settings.SetValue(null,oldSettings); audio.SetValue(null,oldAudio);
            if(oldEvents!=null && oldEvents.isActiveAndEnabled) {EventSystem.current=oldEvents;if(oldSelection!=null && oldSelection.activeInHierarchy) oldEvents.SetSelectedGameObject(oldSelection);}
        }
    }
    private static bool Blocked(CareerUiPointerTestScope scope,Button lower,GameObject overlay)
    {
        Render(scope,null,null,null); var rect=lower.GetComponent<RectTransform>();
        var pointer=new PointerEventData(scope.EventSystem){position=RectTransformUtility.WorldToScreenPoint(scope.Root.GetComponent<Canvas>().worldCamera,rect.TransformPoint(rect.rect.center))};
        var hits=new List<RaycastResult>();scope.Root.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);return hits.Count>0 && hits[0].gameObject==overlay;
    }
    private static void Render(CareerUiPointerTestScope scope,HUDUI hud,string path,Record record)
    {
        foreach(Transform t in scope.Root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=5;
        Canvas.ForceUpdateCanvases();foreach(TMP_Text text in scope.Root.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();Canvas.ForceUpdateCanvases();
        if(record!=null)foreach(TMP_Text text in (hud.IsReturnToMenuConfirmationVisible?hud.returnToMenuConfirmPanel:scope.Root).GetComponentsInChildren<TMP_Text>())if(text.isTextOverflowing)record.overflows.Add(text.name+": "+text.text);
        RenderTexture old=RenderTexture.active;Texture2D img=null;
        try {scope.Root.GetComponent<Canvas>().worldCamera.Render();if(path==null)return;RenderTexture.active=scope.Target;img=new Texture2D(scope.Target.width,scope.Target.height,TextureFormat.RGB24,false);img.ReadPixels(new Rect(0,0,scope.Target.width,scope.Target.height),0,0);img.Apply();File.WriteAllBytes(path,img.EncodeToPNG());}
        finally {RenderTexture.active=old;if(img!=null)Object.DestroyImmediate(img);}
    }
    private static Button MakeButton(Transform parent,TMP_FontAsset font,string name,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=pos;rect.sizeDelta=size;
        var b=go.GetComponent<Button>();b.targetGraphic=go.GetComponent<Image>();MakeLabel(go.transform,font,"Label",Vector2.zero,size).text=name;return b;
    }
    private static TMP_Text MakeLabel(Transform parent,TMP_FontAsset font,string name,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var text=go.GetComponent<TMP_Text>();text.font=font;text.fontSize=18;text.enableAutoSizing=true;text.fontSizeMin=12;text.fontSizeMax=18;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;text.rectTransform.anchoredPosition=pos;text.rectTransform.sizeDelta=size;return text;
    }
}
public class GermanFuelProductionUiTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(1280,720)})
            for(int i=0;i<8;i++)foreach(int mask in new[]{0,(int)InRaceConfirmationAction.All})
                if(mask!=0 || i==0 || i==1 || i==3 || i==4)yield return new TestCaseData(size.x,size.y,i,mask);
    }
    [TestCaseSource(nameof(Cases))]
    public void ActualHudFuelConfirmsOrCancelsAndLaterMovementCannotAutoPay(int width,int height,int fixture,int mask)
    {Assert.That(GermanFuelProductionUiCapture.Exercise(width,height,fixture,mask,true).overflows,Is.Empty);}
}
