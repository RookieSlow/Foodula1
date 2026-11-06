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

/// <summary>Owned real HUD.Start/CardHandUI/manager and camera-rendered uGUI. No scene or profile writes.</summary>
public static class ItalyPrecisionTuneProductionUiCapture
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    [Serializable] public sealed class Frame
    {
        public int width, height, roster;
        public string stage, team, screenshot, action, status;
        public List<string> overflows = new List<string>();
        public List<string> targetOverflows = new List<string>();
        public string telemetry, modalBody;
        public bool pending, used, modal;
    }
    [Serializable] private sealed class Manifest
    {
        public string runtimeMvid, stage;
        public string kind = "Owned production HUD.Start, CardHandUI prefab and manager; true GraphicRaycaster/EventSystem on camera Canvas. Synthetic roster/track; no full RaceCanvas, physical input or storage acceptance.";
        public List<Frame> frames = new List<Frame>();
    }

    public static string Capture(string stage, bool rewritten)
    {
        bool hasPlayerUi = typeof(HUDUI).GetProperty("ItalyPrecisionTuneButton") != null;
        if ((stage != "before" && stage != "after") || hasPlayerUi != rewritten)
            throw new InvalidOperationException("Stage must match the currently loaded production assembly.");
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating || ModularRegressionRunner.NativeTestsRunning())
            throw new InvalidOperationException("Idle editor required.");
        string dir = Path.GetFullPath("production/qa/evidence/tech-driver-I05b2-2026-11-03/" + stage + "/hud-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        Directory.CreateDirectory(dir);
        var manifest = new Manifest { runtimeMvid = typeof(RaceSession).Module.ModuleVersionId.ToString(), stage = stage };
        foreach (Vector2Int size in new[] { new Vector2Int(1920,1080), new Vector2Int(2560,1440), new Vector2Int(1280,720) })
            foreach (TeamId team in new[] { TeamId.IT, TeamId.UK })
                using (var fixture = new Context(size.x, size.y, team, team == TeamId.IT ? 4 : 12, (int)InRaceConfirmationAction.All))
                {
                    fixture.Cards[0].OnCardClicked();
                    Frame frame = fixture.Render(stage, Path.Combine(dir, size.x + "x" + size.y + "-" + team + "-ready.png"));
                    manifest.frames.Add(frame);
                    if (rewritten)
                    {
                        fixture.OpenTarget(); manifest.frames.Add(fixture.Render(stage,Path.Combine(dir,size.x+"x"+size.y+"-"+team+"-target.png")));
                        fixture.Click(fixture.Hud.confirmReturnToMenuButton); manifest.frames.Add(fixture.Render(stage,Path.Combine(dir,size.x+"x"+size.y+"-"+team+"-pending.png")));
                        fixture.Click(fixture.Hand.playCardsButton); manifest.frames.Add(fixture.Render(stage,Path.Combine(dir,size.x+"x"+size.y+"-"+team+"-confirm.png")));
                        fixture.Click(fixture.Hud.confirmReturnToMenuButton); manifest.frames.Add(fixture.Render(stage,Path.Combine(dir,size.x+"x"+size.y+"-"+team+"-used.png")));
                    }
                }
        string path = Path.Combine(dir, "manifest.json");
        File.WriteAllText(path, JsonUtility.ToJson(manifest, true));
        return path;
    }

    public static string CaptureVisibilityAndModes()
    {
        if(typeof(HUDUI).GetProperty("ItalyPrecisionTuneButton")==null || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || ModularRegressionRunner.NativeTestsRunning())
            throw new InvalidOperationException("Current idle UI assembly required.");
        string dir=Path.GetFullPath("production/qa/evidence/tech-driver-I05b2-2026-11-03/after/visibility-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));Directory.CreateDirectory(dir);
        var manifest=new Manifest{runtimeMvid=typeof(RaceSession).Module.ModuleVersionId.ToString(),stage="after-owned-mode-visibility"};
        foreach(Vector2Int size in new[]{new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(1280,720)})
        {
            foreach(TeamId team in new[]{TeamId.UK,TeamId.DE,TeamId.IT,TeamId.US,TeamId.CN,TeamId.JP})
                using(var f=new Context(size.x,size.y,team,new[]{2,4,6,12}[(int)team%4],0))
                    manifest.frames.Add(f.Render("after-mode-owned-normal",Path.Combine(dir,size.x+"x"+size.y+"-"+team+"-normal-disabled-or-hidden.png")));
            foreach(TeamId team in new[]{TeamId.IT,TeamId.UK})
                using(var f=new Context(size.x,size.y,team,team==TeamId.IT?6:12,0,true))
                {
                    f.SelectFirst();manifest.frames.Add(f.Render("after-owned-career-snapshot-not-launch",Path.Combine(dir,size.x+"x"+size.y+"-"+team+"-career-snapshot.png")));
                }
        }
        string path=Path.Combine(dir,"manifest.json");File.WriteAllText(path,JsonUtility.ToJson(manifest,true));return path;
    }

    public sealed class Context : IDisposable
    {
        private readonly object oldSettings, oldAudio;
        private readonly EventSystem previousEvents;
        private readonly GameObject previousSelection;
        private readonly RaceStartupTestScope startup;
        private readonly FieldInfo settingsField = typeof(GameSettingsRuntime).GetField("current", BindingFlags.NonPublic | BindingFlags.Static);
        private readonly FieldInfo audioField = typeof(AudioService).GetField("instance", BindingFlags.NonPublic | BindingFlags.Static);
        internal readonly CareerUiPointerTestScope Scope;
        public readonly HUDUI Hud;
        public readonly CardHandUI Hand;
        public readonly MVPGameManager Manager;
        public readonly RaceSession Race;
        public readonly PlayerState Player;
        public readonly GameConfigSO Config;
        public readonly EventSystem Events;
        public readonly CareerTechSnapshot CareerSnapshot;
        public List<CardUI> Cards => Get<List<CardUI>>(Hand, "cardUIs");
        public Button Tune => (Button)typeof(HUDUI).GetProperty("ItalyPrecisionTuneButton")?.GetValue(Hud);
        public Context(int width, int height, TeamId team, int roster, int mask, bool career = false, bool oldHeat = false)
        {
            oldSettings = settingsField.GetValue(null); oldAudio = audioField.GetValue(null);
            previousEvents = EventSystem.current; previousSelection = previousEvents != null ? previousEvents.currentSelectedGameObject : null;
            startup = new RaceStartupTestScope();
            var settings = GameSettingsData.CreateDefault(); settings.inRaceConfirmationMask = mask; settings.masterVolume = 0; settings.reduceMotion = true;
            settingsField.SetValue(null, settings); audioField.SetValue(null, null);
            Scope = CareerUiPointerTestScope.CreateSized(width, height, () => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
            Scope.Root.GetComponent<Canvas>().worldCamera.orthographic = true;
            var bg = Scope.Root.AddComponent<Image>(); bg.color = RacingTechTheme.Graphite; bg.raycastTarget = false;
            Hud = Scope.Root.AddComponent<HUDUI>();
            Hud.driverSkillButton = MakeButton(Scope.Root.transform, Scope.Font, "Driver action", new Vector2(-580,170));
            Hud.statusText = Label(Scope.Root.transform, Scope.Font, "Status", new Vector2(0,-390), new Vector2(1500,110));
            var gearHost = new GameObject("GearButtons",typeof(RectTransform)); gearHost.transform.SetParent(Scope.Root.transform,false);
            Hud.gear1Button = MakeButton(gearHost.transform, Scope.Font, "Gear 1", new Vector2(580,170));
            Hud.gear2Button = MakeButton(gearHost.transform, Scope.Font, "Gear 2", new Vector2(580,40));
            var handHost = new GameObject("Owned production hand", typeof(RectTransform)); handHost.transform.SetParent(Scope.Root.transform,false);
            Hand = handHost.AddComponent<CardHandUI>(); Hand.enabled = false;
            Hand.cardPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CardPrefab.prefab");
            CardHandUI authoredHand = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RaceCanvas.prefab").GetComponentInChildren<CardHandUI>(true);
            Hand.speedBgSprite = authoredHand.speedBgSprite; Hand.heatBgSprite = authoredHand.heatBgSprite;
            Hand.selectedOverlaySprite = authoredHand.selectedOverlaySprite;
            Hand.numberSprites = (Sprite[])authoredHand.numberSprites.Clone(); Hand.heatIconSprite = authoredHand.heatIconSprite;
            var container = new GameObject("Hand cards", typeof(RectTransform)); container.transform.SetParent(Scope.Root.transform,false);
            container.GetComponent<RectTransform>().anchoredPosition = new Vector2(0,-105);
            Hand.handContainer = container.transform;
            Hand.SetPlayCardsButton(MakeButton(Scope.Root.transform, Scope.Font, "Play cards", new Vector2(0,-270)));
            typeof(HUDUI).GetMethod("Start",Private).Invoke(Hud,null);
            foreach (ButtonClickAnimation feedback in Scope.Root.GetComponentsInChildren<ButtonClickAnimation>(true))
                typeof(ButtonClickAnimation).GetMethod("Awake",Private).Invoke(feedback,null);
            var eventsHost = new GameObject("Owned tuning EventSystem", typeof(EventSystem)); eventsHost.transform.SetParent(Scope.Root.transform,false);
            Events = eventsHost.GetComponent<EventSystem>();
            if(!IsRegistered(Events)) typeof(EventSystem).GetMethod("OnEnable",Private).Invoke(Events,null);
            EventSystem.current = Events;
            var host = new GameObject("Owned inactive tuning manager"); host.transform.SetParent(Scope.Root.transform,false); host.SetActive(false);
            Manager = host.AddComponent<MVPGameManager>(); Config = ScriptableObject.CreateInstance<GameConfigSO>(); Config.enableTechTree = true; Config.totalLaps = 99;
            Manager.config = Config; Manager.hudUI = Hud; Manager.cardHandUI = Hand;
            Manager.trackManager = host.AddComponent<TrackManager>(); Manager.trackManager.config = Config;
            Set(Manager.trackManager,"nodes",Enumerable.Range(0,60).Select(i=>new TrackNode(i,99,isStartFinish:i==0)).ToList());
            Race = new RaceSession(new SystemRandomSource(97)) { TeamVehicleBonusesEnabled = false };
            Player = new PlayerState("长中文车手与数字1234567890",false,0,2) { teamId=team, driverId=DriverCatalog.All.First(d=>d.Team==team).Id, techState=new TechTreeState(team,987654321) };
            Player.driverSkill.Initialize(Player.DriverProfile,0,false);
            if (team==TeamId.IT) { TechTreeRules.UnlockNode(Player.techState,"it-l3-cavallino-rampante",Race.TechDb); TechTreeRules.SelectActiveNodes(Player.techState,new[]{"it-l3-cavallino-rampante"},Race.TechDb); }
            if (team==TeamId.UK)
            {
                foreach (TechNodeDef node in Race.TechDb.GetCommonInTier(TechTreeTier.L1).Take(3)) TechTreeRules.UnlockNode(Player.techState,node.id,Race.TechDb);
                TechTreeRules.UnlockNode(Player.techState,"uk-l1-fish-and-chips",Race.TechDb);
                TechTreeRules.UnlockNode(Player.techState,"uk-l3-sun-never-sets",Race.TechDb);
                TechTreeRules.SelectActiveNodes(Player.techState,new[]{"uk-l3-sun-never-sets","uk-l1-fish-and-chips"},Race.TechDb); Player.techState.sunNeverSetsTarget=TeamId.IT;
            }
            if (career)
            {
                Assert.That(CareerTechSnapshotMapper.TryCapture(Player.techState,out CareerTechSnapshot snapshot),Is.True);
                CareerSnapshot=snapshot;
                Assert.That(CareerTechSnapshotMapper.TryCreateRuntimeState(snapshot,Race.TechDb,out TechTreeState owned),Is.True);
                Player.techState=owned;
            }
            Player.deck.InitializeExactOrder(new[]{new CardData(CardType.Speed,3),new CardData(CardType.Speed,2),new CardData(CardType.Speed,1)},new HeatPool(6)); Player.deck.DrawToHand(3);
            if(oldHeat) Assert.That(Player.deck.DrawHeatFromPoolToHand(1),Is.EqualTo(1));
            Race.Players.Add(Player);
            foreach (var driver in DriverCatalog.All.Where(d=>d.Id!=Player.driverId).Take(roster-1)) Race.Players.Add(new PlayerState(driver.ShortName,true,20+Race.Players.Count,1){teamId=driver.Team,driverId=driver.Id});
            Set(Manager,"session",Race); Set(Manager,"raceTurnNumber",1);
            Race.BeginTurn(Player,1); Race.BeginItalyPrecisionTuneInput(Player,1); Race.BeginPrepMealInput(Player,1);
            Get<RacePhaseState>(Manager,"phaseState").BeginCardSelection(); Get<RaceInputState>(Manager,"inputState").BeginCardSelection();
            Hud.SetGameManager(Manager);
            Scope.Root.AddComponent<RaceUILayoutController>().ApplyLayout(Hud,Hand);
            Hand.ShowHand(Manager,Player); Hand.SetDiscardMode(false);
            for(int i=0;i<Cards.Count;i++)
            {
                Cards[i].GetComponent<RectTransform>().anchoredPosition=new Vector2((i-1)*220,0);
                foreach(TMP_Text t in Cards[i].GetComponentsInChildren<TMP_Text>(true)) {t.font=Scope.Font;t.color=RacingTechTheme.Primary;}
                // The authored persistent listener is RuntimeOnly. This synchronous EditMode
                // fixture enables that same listener on its owned clone, never on the asset.
                Button cardButton=Cards[i].GetComponent<Button>();
                Assert.That(cardButton.onClick.GetPersistentEventCount(),Is.EqualTo(1));
                Assert.That(cardButton.onClick.GetPersistentMethodName(0),Is.EqualTo("OnCardClicked"));
                Assert.That(cardButton.onClick.GetPersistentTarget(0),Is.SameAs(Cards[i]));
                if(!Application.isPlaying) cardButton.onClick.SetPersistentListenerState(0,UnityEngine.Events.UnityEventCallState.EditorAndRuntime);
            }
            Hud.RefreshDriverSkill(Manager,Player); Hud.RefreshTeamGearPresentation();
            Label(Scope.Root.transform,Scope.Font,"Owned context",new Vector2(0,420),new Vector2(1650,100)).text="生产HUD受控夹具 · "+team+" · "+Player.name+" · 参赛车"+Race.Players.Count+" · RP "+Player.techState.rpBalance.ToString("N0");
        }
        public Frame Render(string stage, string path=null)
        {
            for(int i=0;i<Cards.Count;i++) Cards[i].GetComponent<RectTransform>().anchoredPosition=new Vector2((i-1)*220,Cards[i].isSelected?24:0);
            var frame=new Frame{width=Scope.Target.width,height=Scope.Target.height,roster=Race.Players.Count,stage=stage,team=Player.teamId.ToString(),screenshot=path,action=Tune!=null?Tune.GetComponentInChildren<TMP_Text>(true).text:"当前生产程序集无精调入口",status=Hud.statusText.text,
                pending=Manager.HasPendingItalyPrecisionTune,used=Player.techState.precisionTune.UsedThisRace,modal=Hud.IsReturnToMenuConfirmationVisible,modalBody=Hud.IsReturnToMenuConfirmationVisible?Hud.returnToMenuConfirmText.text:"",telemetry=Hud.TeamGearPresentation.Telemetry.MovementText};
            foreach(Transform child in Scope.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer=5;
            Canvas.ForceUpdateCanvases(); foreach(TMP_Text t in Scope.Root.GetComponentsInChildren<TMP_Text>()) t.ForceMeshUpdate(); Canvas.ForceUpdateCanvases();
            foreach(TMP_Text t in (Hud.IsReturnToMenuConfirmationVisible?Hud.returnToMenuConfirmPanel:Scope.Root).GetComponentsInChildren<TMP_Text>()) if(t.isTextOverflowing) frame.overflows.Add(t.name+": "+t.text);
            IEnumerable<TMP_Text> targets=Hud.IsReturnToMenuConfirmationVisible?Hud.returnToMenuConfirmPanel.GetComponentsInChildren<TMP_Text>():
                (Tune!=null?Tune.GetComponentsInChildren<TMP_Text>():Array.Empty<TMP_Text>()).Concat(Hud.UkPrepMealButton!=null&&Hud.UkPrepMealButton.gameObject.activeSelf?Hud.UkPrepMealButton.GetComponentsInChildren<TMP_Text>():Array.Empty<TMP_Text>());
            foreach(TMP_Text t in targets) if(t.isTextOverflowing) frame.targetOverflows.Add(t.name+": "+t.text);
            RenderTexture old=RenderTexture.active; Texture2D image=null;
            try { Scope.Root.GetComponent<Canvas>().worldCamera.Render(); if(path!=null) {RenderTexture.active=Scope.Target;image=new Texture2D(Scope.Target.width,Scope.Target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,Scope.Target.width,Scope.Target.height),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());} }
            finally {RenderTexture.active=old;if(image!=null)Object.DestroyImmediate(image);} return frame;
        }
        public void Click(Button button) => Scope.Click(button);
        public void SelectFirst() {Click(Cards[0].GetComponent<Button>());Assert.That(Cards[0].isSelected,Is.True,"Owned real prefab Button must deliver its original CardUI callback.");}
        public void OpenTarget()
        {
            Assert.That(Tune,Is.Not.Null); Assert.That(Tune.IsInteractable(),Is.True);
            Click(Tune); Assert.That(Hud.IsReturnToMenuConfirmationVisible,Is.True);
            Assert.That(Hud.GetComponentInChildren<DriverSkillCardChoiceUI>(true).Selected,Is.SameAs(Cards[0].cardData));
            Assert.That(Player.techState.precisionTune.UsedThisRace,Is.False);
        }
        public void PrepareThroughUi() {SelectFirst();OpenTarget();Click(Hud.confirmReturnToMenuButton);Assert.That(Manager.HasPendingItalyPrecisionTune,Is.True);}
        public void Move(MoveDirection direction)
        {
            GameObject selected=Events.currentSelectedGameObject;Assert.That(selected,Is.Not.Null);
            ExecuteEvents.Execute(selected,new AxisEventData(Events){moveDir=direction},ExecuteEvents.moveHandler);
        }
        public bool IsTopReceiver(Button button)
        {
            Render("interaction");RectTransform rect=button.transform as RectTransform;
            var pointer=new PointerEventData(Events){position=RectTransformUtility.WorldToScreenPoint(Scope.Root.GetComponent<Canvas>().worldCamera,rect.TransformPoint(rect.rect.center))};
            var hits=new List<RaycastResult>();Scope.Root.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
            return hits.Count>0&&ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject)==button.gameObject;
        }
        public void Dispose()
        {
            try
            {
                if(Events!=null&&IsRegistered(Events)) typeof(EventSystem).GetMethod("OnDisable",Private).Invoke(Events,null);
                Scope?.Dispose(); if(Config!=null)Object.DestroyImmediate(Config);
            }
            finally
            {
                settingsField.SetValue(null,oldSettings);audioField.SetValue(null,oldAudio);
                if(previousEvents!=null&&IsRegistered(previousEvents))
                {
                    EventSystem.current=previousEvents;
                    if(previousEvents.isActiveAndEnabled)previousEvents.SetSelectedGameObject(previousSelection);
                }
                startup.Dispose();
            }
        }
        private static bool IsRegistered(EventSystem events)
            => ((List<EventSystem>)typeof(EventSystem).GetField("m_EventSystems",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null)).Contains(events);
    }
    private static Button MakeButton(Transform parent,TMP_FontAsset font,string name,Vector2 pos)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=pos;rect.sizeDelta=new Vector2(420,110);go.GetComponent<Image>().color=RacingTechTheme.Surface;var b=go.GetComponent<Button>();b.targetGraphic=go.GetComponent<Image>();
        TMP_Text label=Label(go.transform,font,"Label",Vector2.zero,rect.sizeDelta);label.text=name;
        label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(6,2);label.rectTransform.offsetMax=new Vector2(-6,-2);return b;
    }
    private static TMP_Text Label(Transform parent,TMP_FontAsset font,string name,Vector2 pos,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(TextMeshProUGUI));go.transform.SetParent(parent,false);var t=go.GetComponent<TMP_Text>();t.font=font;t.fontSize=20;t.color=RacingTechTheme.Primary;t.alignment=TextAlignmentOptions.Center;t.raycastTarget=false;t.rectTransform.anchoredPosition=pos;t.rectTransform.sizeDelta=size;return t;
    }
    private static T Get<T>(object target,string field)=>(T)target.GetType().GetField(field,Private).GetValue(target);
    private static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
}

/// <summary>UI; owned production geometry and real uGUI callbacks. Not full race/storage/hardware acceptance.</summary>
public class ItalyPrecisionTuneProductionUiTests
{
    private static IEnumerable<TestCaseData> Cases()
    {
        foreach(Vector2Int s in new[]{new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(1280,720)})
            foreach(TeamId team in new[]{TeamId.IT,TeamId.UK}) foreach(int mask in new[]{0,(int)InRaceConfirmationAction.All}) yield return new TestCaseData(s.x,s.y,team,mask);
    }
    [TestCaseSource(nameof(Cases))]
    public void RealChooserAndMergedCardConfirmationHaveOneUseAndNoResourceDebit(int w,int h,TeamId team,int mask)
    {
        using var f=new ItalyPrecisionTuneProductionUiCapture.Context(w,h,team,team==TeamId.IT?4:12,mask);
        Assert.That(f.Tune.IsInteractable(),Is.False);Assert.That(f.Tune.GetComponentInChildren<TMP_Text>().text,Does.Contain("先选牌"));
        int rp=f.Player.techState.rpBalance,heat=f.Player.deck.heatPool.remaining;CardData original=f.Cards[0].cardData;
        f.PrepareThroughUi(); Assert.That(f.Render("pending").targetOverflows,Is.Empty);
        Assert.That(f.Hud.TeamGearPresentation.Telemetry.LivePreview.Movement.TotalCells,Is.EqualTo(2));
        Assert.That(f.Hud.TeamGearPresentation.Telemetry.MovementText,Does.Contain("2"));
        f.Click(f.Hand.playCardsButton);
        bool confirm=mask!=0;Assert.That(f.Hud.IsReturnToMenuConfirmationVisible,Is.EqualTo(confirm));
        if(confirm)
        {
            Assert.That(f.Player.techState.precisionTune.UsedThisRace,Is.False);
            Assert.That(f.Render("final").targetOverflows,Is.Empty);Assert.That(f.IsTopReceiver(f.Tune),Is.False);
            Assert.That(f.Hud.TryCancelItalyPrecisionTuneKeyboardAction(),Is.True);
            Assert.That(f.Manager.HasPendingItalyPrecisionTune,Is.False);Assert.That(f.Cards[0].isSelected,Is.True);
            Assert.That(f.Hud.TeamGearPresentation.Telemetry.LivePreview.Movement.TotalCells,Is.EqualTo(3));
            f.OpenTarget();f.Click(f.Hud.confirmReturnToMenuButton);f.Click(f.Hand.playCardsButton);f.Click(f.Hud.confirmReturnToMenuButton);
        }
        Assert.That(f.Player.techState.precisionTune.UsedThisRace,Is.True);Assert.That(original.value,Is.EqualTo(3));
        Assert.That(f.Player.playedSpeedCardsThisTurn,Is.EqualTo(new[]{original}));Assert.That(f.Player.deck.HandCount,Is.EqualTo(2));
        Assert.That(f.Player.techState.rpBalance,Is.EqualTo(rp));Assert.That(f.Player.deck.heatPool.remaining,Is.EqualTo(heat));
        Assert.That(f.Hud.IsReturnToMenuConfirmationVisible,Is.False);Assert.That(f.Tune.IsInteractable(),Is.False);
        Assert.That(f.Tune.GetComponentInChildren<TMP_Text>().text,Does.Contain("0/1"));Assert.That(f.Render("used").targetOverflows,Is.Empty);
    }
    [TestCase(1920,1080)] [TestCase(2560,1440)] [TestCase(1280,720)]
    public void PendingSpaceStaleModalAndEscapeCannotFallBackToOrdinaryCommit(int w,int h)
    {
        using var f=new ItalyPrecisionTuneProductionUiCapture.Context(w,h,TeamId.IT,2,(int)InRaceConfirmationAction.All);
        f.PrepareThroughUi();Assert.That(f.Hand.TriggerActionShortcut(),Is.True);Assert.That(f.Hud.IsReturnToMenuConfirmationVisible,Is.True);
        f.Manager.OnSpeedCardSelectionChanged();Assert.That(f.Manager.HasPendingItalyPrecisionTune,Is.False);
        f.Click(f.Hud.confirmReturnToMenuButton);
        Assert.That(f.Player.playedSpeedCardsThisTurn,Is.Empty);Assert.That(f.Player.deck.HandCount,Is.EqualTo(3));Assert.That(f.Player.techState.precisionTune.UsedThisRace,Is.False);
        f.OpenTarget();f.Click(f.Hud.confirmReturnToMenuButton);Assert.That(f.Hand.TriggerActionShortcut(),Is.True);
        Assert.That(f.Hud.TryCancelItalyPrecisionTuneKeyboardAction(),Is.True);
        Assert.That(f.Manager.HasPendingItalyPrecisionTune,Is.False);Assert.That(f.Cards[0].isSelected,Is.True);
        Assert.That(f.Hud.TeamGearPresentation.Telemetry.LivePreview.Movement.TotalCells,Is.EqualTo(3));Assert.That(f.Hud.TeamGearPresentation.Telemetry.MovementText,Does.Contain("3"));
        Assert.That(f.Events.currentSelectedGameObject,Is.Not.Null);Assert.That(f.Events.currentSelectedGameObject.activeInHierarchy,Is.True);
        f.Hud.ShowTechnologyCardChoice("原预备餐费用选择","这是原费用UI复用测试",new[]{f.Cards[0].cardData},_=>{},()=>{});
        TMP_Text label=f.Hud.GetComponentInChildren<DriverSkillCardChoiceUI>(true).GetComponentsInChildren<TMP_Text>().First(t=>t.name=="DriverSkillChoiceLabel");
        Assert.That(label.text,Does.Not.Contain("→"));Assert.That(f.Hud.confirmReturnToMenuButton.GetComponentInChildren<TMP_Text>().text,Is.EqualTo("弃牌并回热"));
        f.Hud.CancelReturnToMenu();Assert.That(f.Player.techState.precisionTune.UsedThisRace,Is.False);
    }
    [TestCase(1920,1080)] [TestCase(2560,1440)] [TestCase(1280,720)]
    public void BritishPreparationAndBorrowedTuneStayIndependentlyReachableWithinRealSlot(int w,int h)
    {
        using var f=new ItalyPrecisionTuneProductionUiCapture.Context(w,h,TeamId.UK,12,(int)InRaceConfirmationAction.All,oldHeat:true);
        f.SelectFirst(); Assert.That(f.Hud.UkPrepMealButton.IsInteractable(),Is.True);Assert.That(f.Tune.IsInteractable(),Is.True);
        f.Hud.RefreshUkPrepMeal(f.Manager,f.Player);f.Hud.RefreshGermanFuel(f.Manager,f.Player);
        var prep=f.Hud.UkPrepMealButton.transform as RectTransform;var tune=f.Tune.transform as RectTransform;
        Assert.That(prep.anchoredPosition.y,Is.GreaterThan(tune.anchoredPosition.y));Assert.That(f.Render("coexist").targetOverflows,Is.Empty);
        Assert.That(f.IsTopReceiver(f.Hud.UkPrepMealButton),Is.True);Assert.That(f.IsTopReceiver(f.Tune),Is.True);
        f.Events.SetSelectedGameObject(f.Hud.UkPrepMealButton.gameObject);f.Move(MoveDirection.Down);Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.Tune.gameObject));
        f.Move(MoveDirection.Down);Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.Hand.playCardsButton.gameObject));
        int heat=f.Player.deck.heatPool.remaining;
        f.Click(f.Hud.UkPrepMealButton);Assert.That(f.Hud.IsReturnToMenuConfirmationVisible,Is.True);f.Hud.CancelReturnToMenu();
        Assert.That(f.Player.deck.heatPool.remaining,Is.EqualTo(heat));Assert.That(f.Player.techState.precisionTune.UsedThisRace,Is.False);
        f.OpenTarget();Assert.That(f.IsTopReceiver(f.Hud.UkPrepMealButton),Is.False);
        Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.Hud.cancelReturnToMenuButton.gameObject));
        f.Move(MoveDirection.Right);Assert.That(f.Events.currentSelectedGameObject,Is.SameAs(f.Hud.confirmReturnToMenuButton.gameObject));
        ExecuteEvents.Execute(f.Events.currentSelectedGameObject,new BaseEventData(f.Events),ExecuteEvents.submitHandler);
        Assert.That(f.Manager.HasPendingItalyPrecisionTune,Is.True);Assert.That(f.Player.deck.heatPool.remaining,Is.EqualTo(heat));
        f.Click(f.Tune);Assert.That(f.Manager.HasPendingItalyPrecisionTune,Is.False);Assert.That(f.Hud.TeamGearPresentation.Telemetry.LivePreview.Movement.TotalCells,Is.EqualTo(3));
    }
    [TestCase(TeamId.UK,false)] [TestCase(TeamId.DE,false)] [TestCase(TeamId.IT,false)] [TestCase(TeamId.US,false)] [TestCase(TeamId.CN,false)] [TestCase(TeamId.JP,false)]
    [TestCase(TeamId.UK,true)] [TestCase(TeamId.DE,true)] [TestCase(TeamId.IT,true)] [TestCase(TeamId.US,true)] [TestCase(TeamId.CN,true)] [TestCase(TeamId.JP,true)]
    public void SixTeamsAndOwnedCareerSnapshotsUseOnlyTheirOwnModeTechnology(TeamId team,bool career)
    {
        using var f=new ItalyPrecisionTuneProductionUiCapture.Context(1920,1080,team,new[]{2,4,6,12}[(int)team%4],0,career);
        bool visible=team==TeamId.IT||team==TeamId.UK;
        Assert.That(f.Tune!=null&&f.Tune.gameObject.activeSelf,Is.EqualTo(visible));
        if(!visible)return;
        int rp=f.Player.techState.rpBalance;f.PrepareThroughUi();f.Click(f.Hand.playCardsButton);Assert.That(f.Player.techState.rpBalance,Is.EqualTo(rp));
        if(career)Assert.That(f.CareerSnapshot.RpBalance,Is.EqualTo(rp),"Owned immutable career snapshot is not the live race state or a normal profile.");
        f.Config.enableTechTree=false;f.Hud.RefreshItalyPrecisionTune(f.Manager,f.Player);Assert.That(f.Tune.gameObject.activeSelf,Is.False);
    }
    [TestCase(1920,1080)] [TestCase(2560,1440)] [TestCase(1280,720)]
    public void PendingSpaceHonorsDisabledCardConfirmationAndCommitsDirectly(int w,int h)
    {
        using var f=new ItalyPrecisionTuneProductionUiCapture.Context(w,h,TeamId.IT,6,0);
        f.PrepareThroughUi();Assert.That(f.Hand.TriggerActionShortcut(),Is.True);
        Assert.That(f.Hud.IsReturnToMenuConfirmationVisible,Is.False);Assert.That(f.Player.techState.precisionTune.UsedThisRace,Is.True);
        Assert.That(f.Player.playedSpeedCardsThisTurn.Count,Is.EqualTo(1));Assert.That(f.Player.playedSpeedCardsThisTurn[0].value,Is.EqualTo(3));
    }
    [TestCase(1920,1080)] [TestCase(2560,1440)] [TestCase(1280,720)]
    public void NonReducibleCardAndTutorialGateCannotOpenTheTargetChooser(int w,int h)
    {
        using var f=new ItalyPrecisionTuneProductionUiCapture.Context(w,h,TeamId.IT,4,0);
        f.Click(f.Cards[2].GetComponent<Button>());Assert.That(f.Tune.IsInteractable(),Is.False);
        f.Manager.OnItalyPrecisionTuneButtonClicked();Assert.That(f.Hud.IsReturnToMenuConfirmationVisible,Is.False);
        Assert.That(f.Player.techState.precisionTune.UsedThisRace,Is.False);Assert.That(f.Player.deck.HandCount,Is.EqualTo(3));
        typeof(MVPGameManager).GetField("tutorialScenario",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(f.Manager,TutorialScenarioDefinition.CreateLeMansUk());
        f.Hud.RefreshItalyPrecisionTune(f.Manager,f.Player);Assert.That(f.Tune.gameObject.activeSelf,Is.False);
        f.Manager.OnItalyPrecisionTuneButtonClicked();Assert.That(f.Hud.IsReturnToMenuConfirmationVisible,Is.False);
    }
}

/// <summary>Ascari D05 target selection, stale cancellation, and merged final confirmation on the owned HUD.</summary>
public class AscariPrecisionProductionUiTests
{
    [TestCase(1920, 1080, 0)] [TestCase(2560, 1440, 0)] [TestCase(1280, 720, 0)]
    [TestCase(1920, 1080, (int)InRaceConfirmationAction.All)]
    [TestCase(2560, 1440, (int)InRaceConfirmationAction.All)]
    [TestCase(1280, 720, (int)InRaceConfirmationAction.All)]
    public void AscariTargetChoiceUsesRealButtonAndOneMergedCommit(int width, int height, int mask)
    {
        using var f = new ItalyPrecisionTuneProductionUiCapture.Context(width, height, TeamId.IT, 4, mask);
        ReplaceWithReachableApex(f.Manager.trackManager);
        f.Player.driverSkill.Initialize(f.Player.DriverProfile, 3, true);
        Assert.That(f.Race.BeginDriverSkillInput(f.Player), Is.True, "BeginDriverSkillInput must open Ascari owner scope");
        f.SelectFirst();
        f.Hud.RefreshDriverSkill(f.Manager, f.Player);

        Button action = f.Hud.driverSkillButton;
        Assert.That(action.GetComponentInChildren<TMP_Text>().text, Does.Contain("选择目标"));
        if (!action.IsInteractable())
        {
            string managerLabel = f.Manager.GetDriverSkillButtonLabel(f.Player, out bool managerInteractable);
            Assert.Fail($"Ascari action button disabled: ui={action.IsInteractable()} manager={managerInteractable} label={managerLabel} selected={f.Cards[0].isSelected} hand={f.Player.deck.HandCount} skill={f.Player.driverSkill.Skill} tier={f.Player.driverSkill.Tier} enabled={f.Player.driverSkill.Enabled} open={f.Player.driverSkill.IsDrivingInputOpen}");
        }
        int uses = f.Player.driverSkill.UsesRemaining;
        int rp = f.Player.techState.rpBalance;
        int heat = f.Player.deck.heatPool.remaining;
        CardData target = f.Cards[0].cardData;

        f.Manager.OnDriverSkillButtonClicked();
        Assert.That(f.Hud.IsReturnToMenuConfirmationVisible, Is.True, "Ascari target modal must open from the real driver button");
        Assert.That(f.Hud.GetComponentInChildren<DriverSkillCardChoiceUI>(true).Selected, Is.SameAs(target));
        Assert.That(f.Player.driverSkill.UsesRemaining, Is.EqualTo(uses));
        Assert.That(f.Render("ascari-target").targetOverflows, Is.Empty);
        f.Click(f.Hud.confirmReturnToMenuButton);
        Assert.That(f.Manager.HasPendingAscariPrecision, Is.True, $"Ascari target choice must create an unpaid plan; status={f.Hud.statusText.text}");
        Assert.That(f.Player.driverSkill.UsesRemaining, Is.EqualTo(uses));

        Assert.That(f.Hand.TriggerActionShortcut(), Is.True, "Space action must route through merged precision commit");
        Assert.That(f.Hud.IsReturnToMenuConfirmationVisible, Is.EqualTo(mask != 0), "Card confirmation switch must control the merged modal");
        if (mask != 0)
        {
            f.Manager.OnSpeedCardSelectionChanged();
            f.Click(f.Hud.confirmReturnToMenuButton);
            Assert.That(f.Player.playedSpeedCardsThisTurn, Is.Empty);
            Assert.That(f.Manager.HasPendingAscariPrecision, Is.False, "Stale final confirmation must cancel the Ascari plan");
            Assert.That(f.Player.driverSkill.UsesRemaining, Is.EqualTo(uses));

            Assert.That(f.Cards[0].isSelected, Is.True);
            f.Manager.OnDriverSkillButtonClicked();
            f.Click(f.Hud.confirmReturnToMenuButton);
            Assert.That(f.Manager.HasPendingAscariPrecision, Is.True, "Reopened target must create a fresh plan");
            Assert.That(f.Hand.TriggerActionShortcut(), Is.True, "Fresh plan must reopen final confirmation");
            Assert.That(f.Hud.IsReturnToMenuConfirmationVisible, Is.True);
            f.Click(f.Hud.confirmReturnToMenuButton);
        }

        Assert.That(f.Manager.HasPendingAscariPrecision, Is.False, "Successful ordinary card commit must close Ascari plan");
        Assert.That(f.Player.driverSkill.UsesRemaining, Is.EqualTo(uses - 1));
        Assert.That(f.Player.playedSpeedCardsThisTurn, Is.EqualTo(new[] { target }));
        Assert.That(target.value, Is.EqualTo(3));
        Assert.That(f.Player.techState.rpBalance, Is.EqualTo(rp));
        Assert.That(f.Player.deck.heatPool.remaining, Is.EqualTo(heat));
        Assert.That(f.Render("ascari-used").targetOverflows, Is.Empty);
    }

    private static void ReplaceWithReachableApex(TrackManager manager)
    {
        var nodes = new List<TrackNode>();
        for (int i = 0; i < 20; i++) nodes.Add(new TrackNode(i, 99, isStartFinish: i == 0));
        nodes[2] = new TrackNode(2, 5, "Ascari first apex", 9, false, true);
        nodes[5] = new TrackNode(5, 3, "Ascari tail apex", 2, false, true);
        typeof(TrackManager).GetField("nodes", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(manager, nodes);
    }
}
