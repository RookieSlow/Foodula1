using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Production-prefab controlled EditMode evidence; never opens or saves a gameplay scene.</summary>
public static class RacingTechSelectedEffectCapture
{
    [Serializable] private sealed class Record
    {
        public string screenshot, team, fixture;
        public int width, height, checkedLabels;
        public List<string> overflows = new List<string>();
        public List<LabelGeometry> labels = new List<LabelGeometry>();
        public List<ButtonGeometry> buttons = new List<ButtonGeometry>();
        public int visibleHandCards, drawCount, discardCount, handCount, engineCount;
        public List<PileGeometry> piles = new List<PileGeometry>();
        public bool confirmAndPlayBothActive;
        public string topPrimaryButton;
        public string rawText, expectedFormattedText, parsedText, overflowMode;
        public int parsedCharacters, visibleCharacters, lastVisibleSourceIndex, originalLastNonWhitespaceIndex;
        public bool textTruncated, sourceTextMatches, completeRenderedText, clearHidPanel, reopenMatchesText;
        public float effectWidth, effectHeight, preferredEffectHeight;
    }
    [Serializable] private sealed class PileGeometry
    { public string name; public int displayedCards, total, stackLayers; public bool inspectInteractable; }
    [Serializable] private sealed class ButtonGeometry
    {
        public string name, text; public bool active, interactable, raycastTarget;
        public float width, height; public int sibling;
    }
    [Serializable] private sealed class LabelGeometry
    {
        public string name, text;
        public float width, height, preferredWidth, preferredHeight, viewportHeight;
        public float scrollbarSize, scrollbarValue, verticalNormalizedPosition;
        public bool tmpOverflow, scrollContent;
    }
    [Serializable] private sealed class Manifest
    {
        public string kind = "Controlled EditMode real production RaceCanvas/CardPrefab/CardHand selected-effect preview, never-active owned manager and pure Session/Player. No GameLoop/scene save/storage/audio.";
        public string utc, stage;
        public string scaling = "Production reference 1920x1080, width/height match 0.5; same-aspect captures use width/1920";
        public string lifecycle = "HUD.Start/original ApplyLayout/ShowHand with actual visible card instances and cloned static Chinese font/material. Twelve actual team tricks via original keyboard highlight (one CN sample discard mode), speed selected via original refresh, normal heat via diagnostic original private ShowSelectedCardEffect, US permanent heat only with actual BBQ eligibility from pure tech state and inactive60node TrackManager. Ellipsis measured using isTextTruncated, actual character/line information, parsed text and last visible source index. Original clear/reopen executed after each screenshot; no click/audio or gameplay loop. Background is a controlled prefab, no live track.";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "after") throw new ArgumentException("Use before or after.", nameof(stage));
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        string directory = Path.GetFullPath("production/qa/evidence/racing-tech-selected-effect-2026-10-16/" + stage);
        Directory.CreateDirectory(directory);
        foreach (TeamId team in new[] { TeamId.CN, TeamId.UK, TeamId.US, TeamId.DE, TeamId.IT, TeamId.JP })
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) })
                CaptureOne(team, size, directory, manifest);
        File.WriteAllText(Path.Combine(directory, "manifest.json"), JsonUtility.ToJson(manifest, true));
        int overflow = 0;
        foreach (Record record in manifest.records) overflow += record.overflows.Count;
        return JsonUtility.ToJson(new CaptureSummary { screenshots = manifest.records.Count, overflowRecords = overflow,
            manifest = Path.Combine(directory, "manifest.json") });
    }

    [Serializable] private sealed class CaptureSummary { public int screenshots, overflowRecords; public string manifest; }

    private static void CaptureOne(TeamId team, Vector2Int size, string directory, Manifest manifest)
    {
        Scene previewScene = default;
        GameObject staging = null, root = null, cameraHost = null;
        RenderTexture target = null;
        RenderTexture previous = RenderTexture.active;
        var fonts = new Dictionary<TMP_FontAsset, TMP_FontAsset>();
        var owned = new List<Object>();
        try
        {
            previewScene = EditorSceneManager.NewPreviewScene();
            staging = new GameObject("Racing tech inactive staging");
            staging.SetActive(false);
            SceneManager.MoveGameObjectToScene(staging, previewScene);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RaceCanvas.prefab");
            if (prefab == null) throw new InvalidOperationException("Production RaceCanvas is missing.");
            root = Object.Instantiate(prefab, staging.transform, false);
            root.SetActive(false);
            root.transform.SetParent(null, false);
            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
            foreach (MonoBehaviour script in root.GetComponentsInChildren<MonoBehaviour>(true))
                if (script != null && script.GetType().Assembly == typeof(HUDUI).Assembly) script.enabled = false;

            cameraHost = new GameObject("Racing tech preview camera", typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraHost, previewScene);
            Camera camera = cameraHost.GetComponent<Camera>();
            camera.enabled = false;
            camera.scene = previewScene;
            camera.cullingMask = 1 << 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.03f, 0.04f, 1f);
            target = new RenderTexture(size.x, size.y, 24);
            if (!target.Create()) throw new InvalidOperationException("Preview render allocation failed.");
            camera.targetTexture = target;
            Canvas canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            if (scaler != null) scaler.enabled = false;
            canvas.scaleFactor = size.x / 1920f;
            root.SetActive(true);
            HUDUI hud = root.GetComponentInChildren<HUDUI>(true);
            if (hud == null) throw new InvalidOperationException("Production prefab has no HUDUI.");
            typeof(HUDUI).GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hud, null);
            RaceUILayoutController layout = root.GetComponent<RaceUILayoutController>();
            if (layout == null) layout = root.AddComponent<RaceUILayoutController>();
            layout.ApplyLayout(hud, root.GetComponentInChildren<CardHandUI>(true));
            GameObject managerHost = new GameObject("Never-active owned label coordinator");
            managerHost.SetActive(false);
            managerHost.transform.SetParent(staging.transform, false);
            MVPGameManager manager = managerHost.AddComponent<MVPGameManager>();
            manager.config = ScriptableObject.CreateInstance<GameConfigSO>();
            manager.config.totalLaps = 3;
            owned.Add(manager.config);
            CardHandUI hand = root.GetComponentInChildren<CardHandUI>(true);
            if (hand == null) throw new InvalidOperationException("Production CardHand missing.");
            GameObject cardTemplate = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/CardPrefab.prefab"), staging.transform, false);
            cardTemplate.SetActive(false);
            foreach (TMP_Text text in cardTemplate.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
            hand.cardPrefab = cardTemplate;
            foreach (TrickCardDef definition in TrickCardDatabaseFactory.CreateDefault().GetForTeam(team))
            {
                ConfigureSelectedEffect(hud, team, definition.id, hand, manager);
                Render(definition.id, team, size, directory, manifest, root, camera, target, fonts, owned);
            }
            if(team == TeamId.CN)
                foreach(string fixture in new[]{"speed-selected","ordinary-heat-diagnostic"})
                {
                    ConfigureSelectedEffect(hud, team, fixture, hand, manager);
                    Render(fixture, team, size, directory, manifest, root, camera, target, fonts, owned);
                }
            if(team == TeamId.US)
            {
                ConfigureSelectedEffect(hud, team, "bbq-permanent-heat", hand, manager);
                Render("bbq-permanent-heat", team, size, directory, manifest, root, camera, target, fonts, owned);
            }
        }
        finally
        {
            RenderTexture.active = previous;
            var cleanupErrors = new List<Exception>();
            if (root != null)
            {
                // Runtime callbacks are not automatic in synchronous EditMode.
                // Detach only this preview's global TMP subscriptions explicitly.
                Cleanup(() =>
                {
                    foreach (RacingTechHudShellUI shell in root.GetComponentsInChildren<RacingTechHudShellUI>(true))
                        Cleanup(() => typeof(RacingTechHudShellUI).GetMethod("OnDisable",
                            BindingFlags.Instance | BindingFlags.NonPublic).Invoke(shell, null), cleanupErrors);
                }, cleanupErrors);
                Cleanup(() => Object.DestroyImmediate(root), cleanupErrors);
            }
            if (staging != null) Cleanup(() => Object.DestroyImmediate(staging), cleanupErrors);
            if (cameraHost != null) Cleanup(() => Object.DestroyImmediate(cameraHost), cleanupErrors);
            if (target != null)
            {
                Cleanup(() => target.Release(), cleanupErrors);
                Cleanup(() => Object.DestroyImmediate(target), cleanupErrors);
            }
            foreach (Object item in owned)
                if (item != null) Cleanup(() => Object.DestroyImmediate(item), cleanupErrors);
            if (previewScene.IsValid()) Cleanup(() => EditorSceneManager.ClosePreviewScene(previewScene), cleanupErrors);
            if (cleanupErrors.Count > 0) throw new AggregateException("Preview cleanup failed.", cleanupErrors);
        }
    }

    private static void Cleanup(Action action, List<Exception> errors)
    {
        try { action(); }
        catch (Exception exception) { errors.Add(exception); }
    }

    private static void ConfigureFixture(HUDUI hud, TeamId team, string state, CardHandUI hand, MVPGameManager manager)
    {
        bool gear = state == "gear";
        int actualGear = team == TeamId.CN ? 2 : 3;
        var player = new PlayerState("受控预览车手", false, 30, actualGear)
            { teamId = team, usesChinaGearSystem = team == TeamId.CN, lap = 2 };
        player.deck.InitializeExactOrder(new CardData[0], new HeatPool(12));
        player.driverSkill.Initialize(player.DriverProfile, 1, true);
        var session = new RaceSession(new SystemRandomSource(31));
        session.Players.Add(player);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(MVPGameManager).GetField("session",flags).SetValue(manager,session);
        RacePhaseState phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState",flags).GetValue(manager);
        RaceInputState input = (RaceInputState)typeof(MVPGameManager).GetField("inputState",flags).GetValue(manager);
        if (gear) { phase.BeginGearSelection(); input.BeginGearSelection(actualGear); }
        else { phase.BeginCardSelection(); input.BeginCardSelection(); }
        manager.hudUI = hud;
        manager.cardHandUI = hand;
        typeof(CardHandUI).GetField("gameManager",flags).SetValue(hand,manager);
        typeof(MVPGameManager).GetField("confirmGearControl",flags).SetValue(manager,hud.confirmGearButton);
        hud.ConfigureGearPresentation(team,actualGear);
        hud.RefreshPlayerResources(player);
        hand.SetGearSelectionMode(gear);
        typeof(MVPGameManager).GetMethod("SetGearControlsInteractable",flags).Invoke(manager,new object[] { gear });
        hud.RefreshDriverSkill(manager,player);
        hud.SetStatus("受控 UI 夹具 · " + (gear ? "选择挡位" : "选牌阶段：空速度牌手牌，原缺牌结算提示"));
        Canvas.ForceUpdateCanvases();
    }

    private static void ConfigureSelectedEffect(HUDUI hud, TeamId team, string fixture, CardHandUI hand, MVPGameManager manager)
    {
        ConfigureFixture(hud, team, "card", hand, manager);
        PlayerState player=manager.Player;
        manager.config.enableTechTree=false;
        bool heat=fixture.Contains("heat");
        bool bbq=fixture=="bbq-permanent-heat";
        CardData target=heat?null:fixture=="speed-selected"?new CardData(CardType.Speed,3):CardData.CreateTrick(fixture);
        var cards=new List<CardData>();
        if(target!=null)cards.Add(target);
        while(cards.Count<5)cards.Add(new CardData(CardType.Speed,cards.Count%4+1));
        player.deck.InitializeExactOrder(cards,new HeatPool(12));
        player.deck.DrawToHand(5);player.deck.DrawHeatFromPoolToHand(2);
        if(heat)target=player.deck.Hand[player.deck.Hand.Count-1];
        if(bbq)
        {
            manager.config.enableTechTree=true;
            player.position=5;
            player.techState=new TechTreeState(TeamId.US);
            player.techState.activeNodeIds.Add("us-l2-smoked-bbq");
            if(manager.trackManager==null)manager.trackManager=manager.gameObject.AddComponent<TrackManager>();
            var nodes=new List<TrackNode>();for(int i=0;i<60;i++)nodes.Add(new TrackNode(i,99));
            typeof(TrackManager).GetField("nodes",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(manager.trackManager,nodes);
            if(!manager.CanUseBBQHeatCards(player))throw new InvalidOperationException("Real BBQ fixture eligibility failed.");
        }
        hand.ShowHand(manager,player);
        CardUI targetView=null;
        foreach(CardUI card in hand.handContainer.GetComponentsInChildren<CardUI>(true))
        {
            card.enabled=false;card.gameObject.SetActive(true);
            if(ReferenceEquals(card.cardData,target))targetView=card;
        }
        if(targetView==null)throw new InvalidOperationException("Original ShowHand target instance missing.");
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        if(!heat && fixture!="speed-selected")
        {
            if(team==TeamId.CN && fixture==manager.Session.TrickDb.GetForTeam(team)[1].id)hand.SetDiscardMode(true);
            if(!hand.MoveKeyboardHighlight(1))throw new InvalidOperationException("Original keyboard focus failed.");
        }
        else
        {
            if(fixture!="ordinary-heat-diagnostic")targetView.SetSelectedWithoutNotify(true);
            typeof(CardHandUI).GetMethod("ShowSelectedCardEffect",flags).Invoke(hand,new object[]{targetView});
        }
        hand.UpdateDeckInfo(player);hud.RefreshPlayerResources(player);
        hud.SetStatus("受控原选中牌效果夹具 · "+fixture);
    }

    private static void CloneFont(TMP_Text text, Dictionary<TMP_FontAsset, TMP_FontAsset> fonts, List<Object> owned)
    {
        TMP_FontAsset source = text.font;
        if (source == null || owned.Contains(source)) return;
        if (!fonts.TryGetValue(source, out TMP_FontAsset clone))
        {
            clone = Object.Instantiate(source);
            owned.Add(clone);
            clone.atlasPopulationMode = AtlasPopulationMode.Static;
            clone.fallbackFontAssetTable = new List<TMP_FontAsset>();
            Material material = Object.Instantiate(source.material);
            owned.Add(material);
            clone.material = material;
            fonts.Add(source, clone);
        }
        text.font = clone;
        text.fontSharedMaterial = clone.material;
    }

    private static void Render(string fixture, TeamId team, Vector2Int size, string directory, Manifest manifest,
        GameObject root, Camera camera, RenderTexture target, Dictionary<TMP_FontAsset, TMP_FontAsset> fonts, List<Object> owned)
    {
        foreach (Transform node in root.GetComponentsInChildren<Transform>(true)) node.gameObject.layer = 5;
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        // Synchronous EditMode has no native player-loop LateUpdate. Run only
        // this preview's standings ScrollRect stage, never assign thumb size.
        MethodInfo scrollLateUpdate = typeof(ScrollRect).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
        if (scrollLateUpdate == null) throw new MissingMethodException(typeof(ScrollRect).FullName, "LateUpdate");
        foreach (RacingTechHudShellUI shell in root.GetComponentsInChildren<RacingTechHudShellUI>())
        {
            ScrollRect standingsScroll = shell.StandingsScroll;
            if (standingsScroll != null && standingsScroll.isActiveAndEnabled)
                scrollLateUpdate.Invoke(standingsScroll, null);
        }
        Canvas.ForceUpdateCanvases();
        camera.Render();
        var record = new Record { team = team.ToString(), width = size.x, height = size.y, fixture = fixture,
            screenshot = Path.Combine(directory, team + "-" + size.x + "x" + size.y + "-" + fixture + ".png") };
        HUDUI hud = root.GetComponentInChildren<HUDUI>(true);
        CardHandUI hand = root.GetComponentInChildren<CardHandUI>(true);
        const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
        TMP_Text effect=hand.selectedCardEffectText;
        GameObject panel=hand.selectedCardEffectPanel;
        CardUI targetCard=(CardUI)typeof(CardHandUI).GetField("displayedEffectCard",flags).GetValue(hand);
        MVPGameManager manager=(MVPGameManager)typeof(CardHandUI).GetField("gameManager",flags).GetValue(hand);
        if(effect==null||panel==null||!panel.activeInHierarchy||targetCard==null)throw new InvalidOperationException("Original effect panel not visible.");
        record.rawText=effect.text;
        record.expectedFormattedText=fixture=="bbq-permanent-heat"?"烟熏 BBQ：热量牌作为速度 2 打出，占 1 个速度牌槽；永久热量收尾入弃牌堆，限时热量收尾销毁。":CardSelectionEffectRules.Format(targetCard.cardData,manager.Session.TrickDb);
        record.sourceTextMatches=record.rawText==record.expectedFormattedText;
        record.parsedText=effect.GetParsedText();
        record.parsedCharacters=effect.textInfo.characterCount;
        for(int line=0;line<effect.textInfo.lineCount;line++)record.visibleCharacters+=effect.textInfo.lineInfo[line].visibleCharacterCount;
        record.lastVisibleSourceIndex=-1;
        for(int i=0;i<effect.textInfo.characterCount;i++)
            if(effect.textInfo.characterInfo[i].isVisible)record.lastVisibleSourceIndex=Math.Max(record.lastVisibleSourceIndex,effect.textInfo.characterInfo[i].index);
        record.originalLastNonWhitespaceIndex=effect.text.TrimEnd().Length-1;
        record.textTruncated=effect.isTextTruncated;
        record.overflowMode=effect.overflowMode.ToString();
        string expectedPlain=Regex.Replace(effect.text,"<[^>]*>",string.Empty);
        record.completeRenderedText=!record.textTruncated&&record.parsedText==expectedPlain&&record.lastVisibleSourceIndex>=record.originalLastNonWhitespaceIndex;
        record.effectWidth=effect.rectTransform.rect.width;record.effectHeight=effect.rectTransform.rect.height;
        record.preferredEffectHeight=effect.preferredHeight;
        record.checkedLabels=1;
        if(effect.isTextOverflowing)record.overflows.Add("TMP overflow: "+effect.text);
        if(!record.completeRenderedText)record.overflows.Add("Incomplete or ellipsized: "+effect.text);
        Texture2D texture = null;
        RenderTexture prior = RenderTexture.active;
        try
        {
            RenderTexture.active = target;
            texture = new Texture2D(size.x, size.y, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, size.x, size.y), 0, 0);
            texture.Apply();
            File.WriteAllBytes(record.screenshot, texture.EncodeToPNG());
        }
        finally { RenderTexture.active = prior; if (texture != null) Object.DestroyImmediate(texture); }
        hand.ClearPendingPlaySelection();
        record.clearHidPanel=!panel.activeSelf&&effect.text==string.Empty;
        typeof(CardHandUI).GetMethod("ShowSelectedCardEffect",flags).Invoke(hand,new object[]{targetCard});
        record.reopenMatchesText=panel.activeSelf&&effect.text==record.rawText;
        manifest.records.Add(record);
    }
}
