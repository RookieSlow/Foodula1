using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Production-prefab controlled EditMode evidence; never opens or saves a gameplay scene.</summary>
public static class RacingTechCardZonesCapture
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
        public string kind = "Controlled EditMode production RaceCanvas/CardPrefab/CardHand.ShowHand and UpdateDeckInfo; pure owned Session/Player decks, no GameLoop, scene save, persistence or onClick.";
        public string utc, stage;
        public string scaling = "Production reference 1920x1080, width/height match 0.5; same-aspect captures use width/1920";
        public string lifecycle = "Original HUD.Start/ApplyLayout. Inactive owned manager supplies original label API. Real cloned CardPrefab instances explicitly activated and CardUI disabled before instant selection. Original deck APIs populate seven-card hands and nonempty piles; UpdateDeckInfo creates true stack/count/empty/inspect views. No inspector opened. CN extras empty piles, genuine zero-engine no-speed risk and native Selectable Selected instant controlled state. Static font/material clones; all preview resources and listeners cleaned. Background has no live race or track.";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "after") throw new ArgumentException("Use before or after.", nameof(stage));
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        string directory = Path.GetFullPath("production/qa/evidence/racing-tech-card-zones-2026-10-16/" + stage);
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
            var fixtures = new List<string> { "seven-hand-nonempty-piles" };
            if (team == TeamId.CN) fixtures.AddRange(new[] { "empty-piles", "zero-engine-spin-risk", "selected-native" });
            foreach (string fixture in fixtures)
            {
                ConfigureCardZones(hud, team, fixture, hand, manager);
                Render(fixture, team, size, directory, manifest, root, camera, target, fonts, owned);
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

    private static void ConfigureCardZones(HUDUI hud, TeamId team, string fixture, CardHandUI hand, MVPGameManager manager)
    {
        ConfigureFixture(hud, team, "card", hand, manager);
        PlayerState player = manager.Player;
        var cards = new List<CardData>();
        bool risk = fixture == "zero-engine-spin-risk";
        bool empty = fixture == "empty-piles";
        int discard = empty ? 0 : fixture == "selected-native" ? 1 : 3;
        int handTarget = empty ? 7 : 5;
        if (!risk)
            for (int i = 0; i < discard + handTarget + (empty ? 0 : 12); i++)
                cards.Add(new CardData(CardType.Speed, i % 4 + 1));
        else cards.Add(new CardData(CardType.Speed, 1));
        player.deck.InitializeExactOrder(cards, new HeatPool(risk ? 3 : 12));
        player.deck.DrawToHand(discard);
        player.deck.DiscardPlayableCardInstancesFromHand(new List<CardData>(player.deck.Hand));
        if (!risk) player.deck.DrawToHand(handTarget);
        player.deck.DrawHeatFromPoolToHand(risk ? 3 : empty ? 0 : 2);
        // Risk contains only paid heat cards: original feedback sees no speed cards and engine zero.
        hand.ShowHand(manager, player);
        foreach (CardUI card in hand.handContainer.GetComponentsInChildren<CardUI>(true))
        {
            card.enabled = false;
            card.gameObject.SetActive(true);
        }
        if (fixture == "selected-native")
        {
            foreach (CardUI card in hand.handContainer.GetComponentsInChildren<CardUI>(true))
                if (card.cardData != null && card.cardData.IsSpeed) { card.SetSelectedWithoutNotify(true); break; }
            typeof(CardHandUI).GetMethod("UpdateActionButtonLabel", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(hand, null);
            MethodInfo transition = typeof(Selectable).GetMethod("DoStateTransition", BindingFlags.Instance | BindingFlags.NonPublic);
            Type selection = typeof(Selectable).GetNestedType("SelectionState", BindingFlags.NonPublic);
            transition.Invoke(hand.playCardsButton, new[] { Enum.Parse(selection, "Selected"), (object)true });
        }
        if (risk && !hand.playCardsButton.GetComponentInChildren<TMP_Text>(true).text.Contains("可能失控"))
            throw new InvalidOperationException("Real zero-engine fixture did not yield original spin warning.");
        hand.UpdateDeckInfo(player);
        hud.RefreshPlayerResources(player);
        hud.SetStatus("受控真实牌桌夹具 · " + fixture);
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
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        MVPGameManager manager = (MVPGameManager)typeof(CardHandUI).GetField("gameManager", flags).GetValue(hand);
        PlayerState player = manager.Player;
        record.drawCount = player.deck.DrawPileCount;
        record.discardCount = player.deck.DiscardPileCount;
        record.handCount = player.deck.HandCount;
        record.engineCount = player.deck.heatPool.remaining;
        foreach (CardUI card in hand.handContainer.GetComponentsInChildren<CardUI>(true))
            if (card.gameObject.activeInHierarchy) record.visibleHandCards++;
        foreach (CardPilePreviewUI pile in root.GetComponentsInChildren<CardPilePreviewUI>())
        {
            Button inspect = (Button)typeof(CardPilePreviewUI).GetField("inspectButton", flags).GetValue(pile);
            record.piles.Add(new PileGeometry { name = pile.name, displayedCards = pile.DisplayedCardCount,
                total = pile.DisplayedPileCount, stackLayers = pile.DisplayedStackLayerCount,
                inspectInteractable = inspect != null && inspect.interactable });
        }
        foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>())
        {
            bool scoped = label.name == "DeckTableTitle" || label.name == "DrawPileTitle" || label.name == "EnginePileTitle" ||
                label.name == "HandTitle" || label.name == "DiscardPileTitle" || label == hand.enginePileText ||
                label.name == "EmptyLabel" || label.name == "InspectHint" || label.name == "Count" ||
                label.transform.IsChildOf(hand.playCardsButton.transform);
            if (!scoped) continue;
            record.checkedLabels++;
            record.labels.Add(new LabelGeometry { name = label.transform.parent.name + "/" + label.name, text = label.text,
                width = label.rectTransform.rect.width, height = label.rectTransform.rect.height,
                preferredWidth = label.preferredWidth, preferredHeight = label.preferredHeight, tmpOverflow = label.isTextOverflowing });
            if (label.isTextOverflowing) record.overflows.Add(label.transform.parent.name + "/" + label.name + ": " + label.text);
        }
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
        manifest.records.Add(record);
    }
}
