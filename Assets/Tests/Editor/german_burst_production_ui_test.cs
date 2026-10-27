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

/// <summary>UI/integration evidence: owned preview Canvas and real HUD/MVP callbacks. Advisory visual gate; no match or hardware input.</summary>
public static class GermanBurstProductionUiCapture
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FieldInfo Settings = typeof(GameSettingsRuntime).GetField("current", BindingFlags.Static | BindingFlags.NonPublic);
    private static readonly string[] Fixtures = { "declare", "keyboard-cancel", "gear-lock", "three-fuels", "unpaid-a4-cancel", "frozen-receipts", "terminal", "exit" };

    [Serializable] public sealed class Record
    {
        public int width, height, mask, roster, engineAfter, fuelUses;
        public string fixture, team, driver, screenshot, burstText, fuelText;
        public string screenshotBoundary = "Payment/declaration UI before terminal or exit cleanup; final resource fields are measured after assertions.";
        public bool modal, blocked, declared, locked, raceUseConsumed, focus;
        public List<string> overflows = new List<string>();
    }
    [Serializable] private sealed class Manifest
    {
        public string utc, stage, runtimeMvid;
        public string kind = "Owned orthographic EditMode Canvas; real HUD.Start/action widgets/MVP/session. Normalized production 285x76 action slot and owned surrounding controls; EventSystem/GraphicRaycaster events, not full RaceCanvas, hardware input or complete modes.";
        public string themeBoundary = "Before preserves inherited white owned driver/card-action backdrop; old driver-label contrast is not accepted. After alone applies the already-existing production action style to those owned backdrop controls; that fixture-theme change is not a production layout change or a recreated historical before.";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage)
    {
        bool connected = typeof(MVPGameManager).GetMethod("OnGermanBurstButtonClicked") != null;
        if (stage == "before" && connected) throw new InvalidOperationException("Cannot fabricate old UI after migration.");
        if (stage != "before" && stage != "after") throw new ArgumentException("before or after");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || ModularRegressionRunner.NativeTestsRunning())
            throw new InvalidOperationException("Capture requires idle EditMode");
        string dir = Path.GetFullPath("production/qa/evidence/tech-driver-I03e-2026-10-27/" + stage + "/run-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        if (Directory.Exists(dir)) throw new IOException("Capture directory already exists");
        Directory.CreateDirectory(dir);
        var manifest = new Manifest { stage = stage, utc = DateTime.UtcNow.ToString("o"), runtimeMvid = typeof(RaceSession).Assembly.ManifestModule.ModuleVersionId.ToString() };
        foreach (var size in new[] { new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(1280,720) })
            for (int i = 0; i < Fixtures.Length; i++)
                manifest.records.Add(Exercise(size.x,size.y,i,(int)InRaceConfirmationAction.All,stage == "after",Path.Combine(dir,size.x+"x"+size.y+"-"+Fixtures[i]+".png")));
        File.WriteAllText(Path.Combine(dir,"manifest.json"),JsonUtility.ToJson(manifest,true));
        return Path.Combine(dir,"manifest.json");
    }

    public static Record Exercise(int width,int height,int index,int mask,bool after,string screenshot = null)
    {
        object oldSettings = Settings.GetValue(null);
        var audio = typeof(AudioService).GetField("instance",BindingFlags.Static|BindingFlags.NonPublic); object oldAudio = audio.GetValue(null);
        using var startup = new RaceStartupTestScope();
        EventSystem oldEvents = EventSystem.current;
        GameObject oldSelection = oldEvents != null ? oldEvents.currentSelectedGameObject : null;
        CareerUiPointerTestScope scope = null; HUDUI hud = null; EventSystem events = null; GameConfigSO config = null;
        try
        {
            var settings = GameSettingsData.CreateDefault(); settings.inRaceConfirmationMask = mask; settings.masterVolume = 0; settings.reduceMotion = true;
            Settings.SetValue(null,settings); audio.SetValue(null,null);
            scope = CareerUiPointerTestScope.CreateSized(width,height,()=>AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
            // Owned UI-only projection: avoids native perspective RT containment drift in synchronous EditMode.
            scope.Root.GetComponent<Canvas>().worldCamera.orthographic = true;
            hud = scope.Root.AddComponent<HUDUI>();
            var panel = new GameObject("Owned OperationPanel",typeof(RectTransform)); panel.transform.SetParent(scope.Root.transform,false);
            var panelRect = panel.GetComponent<RectTransform>(); panelRect.anchoredPosition = new Vector2(670,0); panelRect.sizeDelta = new Vector2(340,800);
            hud.driverSkillButton = MakeButton(panel.transform,scope.Font,"DriverSkill",Vector2.zero,new Vector2(285,76));
            var slot = hud.driverSkillButton.GetComponent<RectTransform>(); slot.anchorMin = RaceUILayoutController.DriverSkillAnchorMin; slot.anchorMax = RaceUILayoutController.DriverSkillAnchorMax; slot.offsetMin = slot.offsetMax = Vector2.zero;
            var gears = new GameObject("GearButtons",typeof(RectTransform)); gears.transform.SetParent(scope.Root.transform,false);
            var gearRect = gears.GetComponent<RectTransform>(); gearRect.anchoredPosition = new Vector2(-500,0); gearRect.sizeDelta = new Vector2(300,330);
            hud.gear1Button = MakeButton(gears.transform,scope.Font,"Legal gear return",Vector2.zero,new Vector2(250,70));
            hud.statusText = MakeLabel(scope.Root.transform,scope.Font,"Owned status",new Vector2(0,-340),new Vector2(1500,160));
            typeof(HUDUI).GetMethod("Start",Private).Invoke(hud,null);
            var eventHost = new GameObject("Owned active events",typeof(EventSystem)); eventHost.transform.SetParent(scope.Root.transform,false);
            events = eventHost.GetComponent<EventSystem>(); typeof(EventSystem).GetMethod("OnEnable",Private).Invoke(events,null); EventSystem.current = events;
            var host = new GameObject("Owned inactive manager"); host.SetActive(false); host.transform.SetParent(scope.Root.transform,false);
            var manager = host.AddComponent<MVPGameManager>(); manager.hudUI = hud;
            manager.cardHandUI = host.AddComponent<CardHandUI>();
            manager.cardHandUI.playCardsButton = MakeButton(scope.Root.transform,scope.Font,"Owned card action",new Vector2(-500,-280),new Vector2(300,70));
            manager.cardHandUI.handContainer = new GameObject("Owned inactive hand",typeof(RectTransform)).transform;
            manager.cardHandUI.handContainer.SetParent(host.transform,false);
            manager.cardHandUI.cardPrefab = new GameObject("Owned card prefab",typeof(RectTransform),typeof(Image),typeof(CardUI)); manager.cardHandUI.cardPrefab.transform.SetParent(host.transform,false);
            if(after)
            {
                RacingTechActionButtonStyle.Apply(hud.driverSkillButton,false);
                RacingTechActionButtonStyle.Apply(manager.cardHandUI.playCardsButton,false);
            }
            config = ScriptableObject.CreateInstance<GameConfigSO>(); config.enableTechTree = true; manager.config = config;
            var track = host.AddComponent<TrackManager>(); manager.trackManager = track;
            typeof(TrackManager).GetField("nodes",Private).SetValue(track,Enumerable.Range(0,60).Select(i=>new TrackNode(i,99)).ToList());
            var race = new RaceSession(new SystemRandomSource(13)) { TeamVehicleBonusesEnabled = false };
            var profile = DriverCatalog.All.Where(d=>d.Team == TeamId.DE).ElementAt(index%2);
            var p = new PlayerState("德国队·长中文车手1234567890",false,0,1) { teamId=TeamId.DE,driverId=profile.Id,techState=new TechTreeState(TeamId.DE) };
            p.driverSkill.Initialize(profile,7,true);
            p.techState.activeNodeIds.UnionWith(new[] {"de-l1-schwarzbier-fuel","de-l3-grill-spezial"});
            p.deck.InitializeExactOrder(new[] {new CardData(CardType.Speed,2),new CardData(CardType.Speed,3),new CardData(CardType.Speed,4)},new HeatPool(7)); p.deck.DrawToHand(3);
            race.Players.Add(p); race.BeginTurn(p,1); race.BeginDriverSkillInput(p,60);
            TeamId team = (TeamId)(index%6); int roster = new[] {2,4,6,12}[index%4];
            for(int i=1;i<roster;i++) race.Players.Add(new PlayerState("周边车队"+team+"·"+i,true,20+i,1){teamId=team});
            typeof(MVPGameManager).GetField("session",Private).SetValue(manager,race);
            typeof(MVPGameManager).GetField("raceTurnNumber",Private).SetValue(manager,1);
            var phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState",Private).GetValue(manager);
            var input = (RaceInputState)typeof(MVPGameManager).GetField("inputState",Private).GetValue(manager);
            phase.BeginGearSelection(); input.BeginGearSelection(1);
            if (!after && index >= 3) { phase.BeginCardSelection(); input.BeginCardSelection(); }
            hud.SetGameManager(manager); hud.RefreshDriverSkill(manager,p); hud.RefreshTeamGearPresentation();
            hud.SetStatus(p.name+" / 周边"+team+" / "+roster+"辆 · 生产受控夹具");
            var record = new Record {width=width,height=height,fixture=Fixtures[index],mask=mask,roster=roster,team=team.ToString(),driver=profile.Id,screenshot=screenshot};
            if(!after) { Render(scope,hud,screenshot,record); return record; }
            ExerciseAfter(scope,hud,manager,race,p,phase,input,events,index,mask,screenshot,record);
            record.engineAfter=p.deck.heatPool.remaining; record.fuelUses=p.techState.germanBurst.FuelUsesThisTurn;
            record.declared=p.techState.germanBurst.Declared; record.locked=p.techState.germanBurst.Locked; record.raceUseConsumed=p.techState.grillSpezialUsed;
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

    private static void ExerciseAfter(CareerUiPointerTestScope scope,HUDUI hud,MVPGameManager manager,RaceSession race,PlayerState p,
        RacePhaseState phase,RaceInputState input,EventSystem events,int index,int mask,string screenshot,Record record)
    {
        // Reflection keeps the pre-change capture compilable against old production APIs.
        Button burst = GetButton(hud,"GermanBurstButton");
        Assert.That(burst!=null && burst.gameObject.activeInHierarchy && burst.IsInteractable(),Is.True);
        AssertNeighborGeometry(hud.driverSkillButton,burst);
        int opening=p.deck.heatPool.remaining;
        CardData oldReceipt=null;
        if(index==5)
        {
            Assert.That(RaceHeatPaymentExecution.Pay(p,race,1,HeatPaymentDestination.Hand).Drawn,Is.EqualTo(1));
            oldReceipt=p.heatPaidCardsThisTurn[0]; opening--;
        }
        events.SetSelectedGameObject(burst.gameObject);
        if(index==1)
        {
            events.SetSelectedGameObject(hud.driverSkillButton.gameObject);
            ExecuteEvents.Execute(events.currentSelectedGameObject,new AxisEventData(events){moveDir=MoveDirection.Right},ExecuteEvents.moveHandler);
            Assert.That(events.currentSelectedGameObject,Is.SameAs(burst.gameObject),"A1 source action must navigate to its neighboring technology control");
            Submit(events);
        }
        else scope.Click(burst);
        Assert.That(p.techState.germanBurst.Declared,Is.True);
        Assert.That(p.techState.grillSpezialUsed,Is.False);
        Assert.That(p.deck.heatPool.remaining,Is.EqualTo(opening));
        Assert.That(hud.IsReturnToMenuConfirmationVisible,Is.False,"Free declaration must not create a second optional fee modal");
        if(index==0 || index==1)
        {
            Render(scope,hud,screenshot,record);
            if(index==1) Submit(events); else scope.Click(burst);
            Assert.That(p.techState.germanBurst.Declared,Is.False);
            Assert.That(p.techState.grillSpezialUsed,Is.False);
            Assert.That(p.deck.heatPool.remaining,Is.EqualTo(opening));
            scope.Click(burst); Assert.That(p.techState.germanBurst.Declared,Is.True);
            record.focus=events.currentSelectedGameObject!=null && events.currentSelectedGameObject.activeInHierarchy;
            Assert.That(record.focus,Is.True); return;
        }
        int targetGear=index==2 || index==5 ? 3 : 1;
        manager.OnGearButtonClicked(targetGear);
        manager.OnConfirmGearClicked();
        typeof(MVPGameManager).GetMethod("ApplyGearShift",Private).Invoke(manager,new object[]{p,targetGear});
        phase.BeginCardSelection(); input.BeginCardSelection(); hud.RefreshTeamGearPresentation();
        AssertNeighborGeometry(GetButton(hud,"GermanFuelButton"),burst);
        Assert.That(hud.driverSkillButton.gameObject.activeInHierarchy,Is.False,"A4 source action is fuel, not a second driver overlay");
        if(index==2)
        {
            Assert.That(p.techState.germanBurst.Locked && p.techState.grillSpezialUsed,Is.True);
            Assert.That(p.deck.heatPool.remaining,Is.EqualTo(opening-1));
            Assert.That(burst.IsInteractable(),Is.False); Render(scope,hud,screenshot,record); return;
        }
        if(index==4)
        {
            Assert.That(burst.IsInteractable(),Is.True,"Unpaid A4 intent remains cancellable");
            events.SetSelectedGameObject(burst.gameObject);
            scope.Click(burst); Assert.That(p.techState.germanBurst.Declared,Is.False);
            Assert.That(events.currentSelectedGameObject,Is.SameAs(GetButton(hud,"GermanFuelButton").gameObject),"Hiding the cancelled L3 action returns keyboard focus to the legal neighboring action");
            record.focus=true;
            Assert.That(RaceHeatPaymentExecution.Pay(p,race,1,HeatPaymentDestination.Hand).Drawn,Is.EqualTo(1));
            CardData paid=p.heatPaidCardsThisTurn[0]; Render(scope,hud,screenshot,record);
            typeof(MVPGameManager).GetMethod("CleanupTurn",Private).Invoke(manager,new object[]{p});
            Assert.That(p.deck.heatPool.remaining,Is.EqualTo(opening-1));
            Assert.That(p.deck.ContainsInHand(paid),Is.True,"A cancelled window must not fall back to legacy automatic refund");
            Assert.That(p.techState.grillSpezialUsed,Is.False); return;
        }
        Button fuel=GetButton(hud,"GermanFuelButton"); Assert.That(fuel,Is.Not.Null);
        int count=index==3 ? 3 : 1;
        var fuelReceipts=new List<CardData>();
        for(int i=0;i<count;i++)
        {
            int before=p.deck.heatPool.remaining;
            events.SetSelectedGameObject(fuel.gameObject);
            scope.Click(fuel);
            Assert.That(hud.IsReturnToMenuConfirmationVisible,Is.EqualTo(mask!=0));
            if(hud.IsReturnToMenuConfirmationVisible)
            {
                record.modal=true; record.blocked=Blocked(scope,hud.gear1Button,hud.returnToMenuConfirmPanel); Assert.That(record.blocked,Is.True);
                Assert.That(fuel.IsInteractable(),Is.False);
                Assert.That(burst.IsInteractable(),Is.False);
                int pendingVersion=p.techState.germanBurst.Version;
                typeof(MVPGameManager).GetMethod("OnGermanBurstButtonClicked").Invoke(manager,null);
                Assert.That(p.techState.germanBurst.Version,Is.EqualTo(pendingVersion),"A fixed-fee modal blocks the declaration toggle callback as well as underlying pointer hits");
                if(i==count-1 && index!=3) Render(scope,hud,screenshot,record);
                scope.Click(hud.confirmReturnToMenuButton);
            }
            Assert.That(p.deck.heatPool.remaining,Is.EqualTo(before-1));
            fuelReceipts.Add(p.heatPaidCardsThisTurn.Last());
        }
        if(!record.modal || index==3)
        {
            record.modal=hud.IsReturnToMenuConfirmationVisible;
            Render(scope,hud,screenshot,record);
        }
        Assert.That(p.techState.grillSpezialUsed,Is.True); Assert.That(p.techState.germanBurst.FuelUsesThisTurn,Is.EqualTo(count));
        if(index==3)
        {
            Assert.That(fuel.IsInteractable(),Is.False);
            Assert.That(fuel.GetComponentInChildren<TMP_Text>(true).text,Does.Contain("0/3"));
        }
        if(index==6 || index==7)
        {
            int before=p.deck.heatPool.remaining;
            if(index==6) {p.hasFinished=true;typeof(MVPGameManager).GetMethod("CleanupTurn",Private).Invoke(manager,new object[]{p});}
            else typeof(MVPGameManager).GetMethod("OnDisable",Private).Invoke(manager,null);
            Assert.That(p.deck.heatPool.remaining,Is.EqualTo(before)); Assert.That(p.techState.grillSpezialUsed,Is.True);
            Assert.That(p.techState.germanBurst.Declared,Is.False); return;
        }
        CardData speed=p.deck.Hand.First(c=>c.IsSpeed);
        Assert.That((bool)typeof(MVPGameManager).GetMethod("ConfirmPlayerSpeedCards",Private).Invoke(manager,new object[]{p,new[]{speed}}),Is.True);
        typeof(MVPGameManager).GetMethod("ComputeMovements",Private).Invoke(manager,new object[]{new List<PlayerState>{p},new HashSet<PlayerState>()});
        if(index==3) Assert.That(p.totalMovementThisTurn,Is.EqualTo(speed.value+6));
        typeof(MVPGameManager).GetMethod("ReactStep",Private).Invoke(manager,new object[]{p});
        int beforeCleanup=p.deck.heatPool.remaining;
        // Reaction cooling may already have returned some window receipts. Refund only surviving instances.
        int refundable=p.deck.CountRecordedHeat(p.techState.germanBurst.Receipts);
        typeof(MVPGameManager).GetMethod("CleanupTurn",Private).Invoke(manager,new object[]{p});
        Assert.That(p.deck.heatPool.remaining,Is.EqualTo(beforeCleanup+refundable));
        foreach(CardData paid in fuelReceipts) Assert.That(p.deck.IsUniquePermanentHeatInHandOrDiscard(paid),Is.False);
        if(oldReceipt!=null) Assert.That(p.deck.ContainsInHand(oldReceipt),Is.True,"Old paid prefix is not a window receipt");
        Assert.That(p.techState.germanBurst.Declared,Is.False);
    }

    private static Button GetButton(HUDUI hud,string name) => (Button)typeof(HUDUI).GetProperty(name).GetValue(hud);
    private static void AssertNeighborGeometry(Button source,Button technology)
    {
        Assert.That(source!=null && source.gameObject.activeInHierarchy,Is.True);
        Assert.That(technology!=null && technology.gameObject.activeInHierarchy,Is.True);
        Canvas.ForceUpdateCanvases();
        var left=new Vector3[4];var right=new Vector3[4];
        source.GetComponent<RectTransform>().GetWorldCorners(left);
        technology.GetComponent<RectTransform>().GetWorldCorners(right);
        Assert.That(left[2].x,Is.LessThan(right[0].x),"Driver/fuel and declaration controls must have a non-overlapping gap");
    }
    private static void Submit(EventSystem events) => ExecuteEvents.Execute(events.currentSelectedGameObject,new BaseEventData(events),ExecuteEvents.submitHandler);
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
        if(record!=null)
        {
            foreach(TMP_Text text in (hud.IsReturnToMenuConfirmationVisible?hud.returnToMenuConfirmPanel:scope.Root).GetComponentsInChildren<TMP_Text>())
                if(text.isTextOverflowing)record.overflows.Add(text.name+": "+text.text);
            var burstProperty=typeof(HUDUI).GetProperty("GermanBurstButton");
            Button burst=burstProperty!=null?(Button)burstProperty.GetValue(hud):null;
            if(burst!=null)record.burstText=burst.GetComponentInChildren<TMP_Text>(true).text;
            Button fuel=GetButton(hud,"GermanFuelButton");
            if(fuel!=null)record.fuelText=fuel.GetComponentInChildren<TMP_Text>(true).text;
        }
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

/// <summary>UI/integration: production-controlled interaction evidence (advisory visual gate), not complete-mode/hardware acceptance.</summary>
public class GermanBurstProductionUiTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        foreach(var size in new[]{new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(1280,720)})
            for(int i=0;i<8;i++)foreach(int mask in new[]{0,(int)InRaceConfirmationAction.All})yield return new TestCaseData(size.x,size.y,i,mask);
    }
    [TestCaseSource(nameof(Cases))]
    public void ActualHudDeclarationLockBudgetAndCleanupRetainExactInstances(int width,int height,int fixture,int mask)
    {Assert.That(GermanBurstProductionUiCapture.Exercise(width,height,fixture,mask,true).overflows,Is.Empty);}
}
