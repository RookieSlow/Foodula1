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
public static class RacingTechPrimaryActionsCapture
{
    [Serializable] private sealed class Record
    {
        public string screenshot, team, fixture;
        public int width, height, checkedLabels;
        public int actualGear, pendingGear, actualHeat, heatCapacity, projectedHeat, engineLights;
        public bool controlsInteractable, locked, hasForecast;
        public List<string> overflows = new List<string>();
        public List<LabelGeometry> labels = new List<LabelGeometry>();
        public List<ButtonGeometry> buttons = new List<ButtonGeometry>();
        public bool confirmAndPlayBothActive;
        public string topPrimaryButton;
    }
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
        public string kind = "Controlled EditMode real RaceCanvas action-button preview; owned never-active manager and pure Session/Player supply real phase/label APIs. No manager Awake/Start/GameLoop, live track or player persistence.";
        public string utc, stage;
        public string scaling = "Production reference 1920x1080, width/height match 0.5; same-aspect captures use width/1920";
        public string lifecycle = "HUD.Start and original ApplyLayout on owned prefab. Never-active owned manager phase/input APIs, original CardHand.SetGearSelectionMode and HUD.RefreshDriverSkill generate labels. Empty speed hand exercises real missing-card end label. Original overlapping primary buttons stay active as production; interactability and sibling order recorded without inventing mutual visibility. After adds five real ShowHand/selection/skill fixtures x3 sizes; CardUI disabled before programmatic selection to use its existing instant path, never start a selection coroutine. Native Selectable instant states are controlled preview only. No onClick/audio; cloned static font/material and full preview cleanup.";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            throw new InvalidOperationException("Capture requires idle EditMode.");
        if (stage != "before" && stage != "after") throw new ArgumentException("Use before or after.", nameof(stage));
        var manifest = new Manifest { utc = DateTime.UtcNow.ToString("o"), stage = stage };
        string directory = Path.GetFullPath("production/qa/evidence/racing-tech-primary-actions-2026-10-15/" + stage);
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
            foreach (string state in new[] { "gear", "card" })
            {
                ConfigureFixture(hud, team, state, hand, manager);
                Render(state, team, size, directory, manifest, root, camera, target, fonts, owned);
            }
            if (manifest.stage == "after" && team == TeamId.CN)
            {
                // Clone the owned card template while inactive, before any TMP rendering.
                GameObject cardTemplate = Object.Instantiate(hand.cardPrefab, staging.transform, false);
                cardTemplate.SetActive(false);
                foreach (TMP_Text text in cardTemplate.GetComponentsInChildren<TMP_Text>(true)) CloneFont(text, fonts, owned);
                hand.cardPrefab = cardTemplate;
                foreach (string dynamicState in new[] { "gear-skill-ready", "card-single-skill-active", "card-multi-skill-active", "card-trick", "card-discard" })
                {
                    ConfigureDynamicFixture(hud, hand, manager, dynamicState);
                    Render(dynamicState, team, size, directory, manifest, root, camera, target, fonts, owned);
                }
            }
            if (manifest.stage == "after" && team == TeamId.CN)
                foreach (string state in new[] { "hover", "pressed", "disabled" })
                {
                    ConfigureFixture(hud, team, "card", hand, manager);
                    Button button = hand.playCardsButton;
                    button.interactable = state != "disabled";
                    MethodInfo transition = typeof(Selectable).GetMethod("DoStateTransition", BindingFlags.Instance | BindingFlags.NonPublic);
                    Type selection = typeof(Selectable).GetNestedType("SelectionState", BindingFlags.NonPublic);
                    transition.Invoke(button, new[] { Enum.Parse(selection, state == "hover" ? "Highlighted" : state == "pressed" ? "Pressed" : "Disabled"), (object)true });
                    Render("card-" + state, team, size, directory, manifest, root, camera, target, fonts, owned);
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

    private static void ConfigureDynamicFixture(HUDUI hud, CardHandUI hand, MVPGameManager manager, string state)
    {
        ConfigureFixture(hud, TeamId.CN, "gear", hand, manager);
        PlayerState player = manager.Player;
        player.driverSkill.Initialize(player.DriverProfile, 3, true);
        if (state == "gear-skill-ready") { hud.RefreshDriverSkill(manager, player); return; }
        bool skillActive = state.EndsWith("skill-active", StringComparison.Ordinal);
        if (skillActive)
        {
            var context = new DriverSkillActivationContext(true, player.lap, 3, 12, 12, 1);
            if (!player.driverSkill.TryActivate(player.DriverProfile, context, out string reason))
                throw new InvalidOperationException("Owned legal skill activation failed: " + reason);
        }
        var cards = new List<CardData>();
        if (state == "card-trick") cards.Add(CardData.CreateTrick("cn-hotpot-base"));
        else { cards.Add(new CardData(CardType.Speed, 2)); if (state == "card-multi-skill-active") cards.Add(new CardData(CardType.Speed, 3)); }
        player.deck.InitializeExactOrder(cards, new HeatPool(12));
        player.deck.DrawToHand(cards.Count);
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        ((RacePhaseState)typeof(MVPGameManager).GetField("phaseState", flags).GetValue(manager)).BeginCardSelection();
        ((RaceInputState)typeof(MVPGameManager).GetField("inputState", flags).GetValue(manager)).BeginCardSelection();
        hand.SetGearSelectionMode(false);
        hand.ShowHand(manager, player);
        if (state == "card-discard") hand.SetDiscardMode(true);
        foreach (CardUI card in hand.GetComponentsInChildren<CardUI>(true)) card.enabled = false;
        // Authored handContainer is a sibling of the CardHandUI component.
        foreach (CardUI card in hand.handContainer.GetComponentsInChildren<CardUI>(true))
        { card.enabled = false; card.SetSelectedWithoutNotify(true); }
        typeof(CardHandUI).GetMethod("UpdateActionButtonLabel", flags).Invoke(hand, null);
        typeof(MVPGameManager).GetMethod("SetGearControlsInteractable", flags).Invoke(manager, new object[] { false });
        hud.RefreshDriverSkill(manager, player);
        hud.SetStatus("受控真实动态标签夹具 · " + state);
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
        Button[] controls = { hud.confirmGearButton, hud.driverSkillButton, hud.resetButton, hud.returnToMenuButton, hand.playCardsButton };
        foreach (Button button in controls)
        {
            if (button == null) throw new InvalidOperationException("Scoped action button missing.");
            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
            Rect rect = button.GetComponent<RectTransform>().rect;
            record.buttons.Add(new ButtonGeometry { name = button.name, text = label != null ? label.text : "",
                active = button.gameObject.activeInHierarchy, interactable = button.interactable,
                raycastTarget = button.targetGraphic != null && button.targetGraphic.raycastTarget,
                width = rect.width, height = rect.height, sibling = button.transform.GetSiblingIndex() });
            if (!button.gameObject.activeInHierarchy || label == null) continue;
            record.checkedLabels++;
            record.labels.Add(new LabelGeometry { name = button.name + "/" + label.name, text = label.text,
                width = label.rectTransform.rect.width, height = label.rectTransform.rect.height,
                preferredWidth = label.preferredWidth, preferredHeight = label.preferredHeight, tmpOverflow = label.isTextOverflowing });
            if (label.isTextOverflowing) record.overflows.Add(button.name + ": " + label.text);
        }
        record.confirmAndPlayBothActive = hud.confirmGearButton.gameObject.activeInHierarchy && hand.playCardsButton.gameObject.activeInHierarchy;
        record.topPrimaryButton = hand.playCardsButton.gameObject.activeInHierarchy &&
            (!hud.confirmGearButton.gameObject.activeInHierarchy || hand.playCardsButton.transform.GetSiblingIndex() > hud.confirmGearButton.transform.GetSiblingIndex())
            ? hand.playCardsButton.name : hud.confirmGearButton.name;
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
