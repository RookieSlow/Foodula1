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

/// <summary>Owned production technology screens. No PlayerPrefs/profile persistence or full-menu acceptance.</summary>
public static class ItalyL2ProductionUiCapture
{
    private const string NodeId = "it-l2-lasagna";
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
    [Serializable] public sealed class Record
    {
        public int width, height, screen, rp, cost, visitedTeams;
        public float targetWidth, targetHeight;
        public string description, label, screenshot;
        public bool overlayBlocked, closed, sourceSnapshotUnchanged, normalProfilesUnchanged;
        public List<string> targetOverflows = new List<string>();
    }
    [Serializable] private sealed class Manifest
    {
        public string stage, runtimeMvid;
        public string boundary = "Owned orthographic preview Canvas and production TechTreeUI/CareerTechTreeUI. Normal profile cache is temporarily seeded and restored: read-only tabs/description/back, no normal node unlock/toggle/persistence. Owned old normal columns are explicitly cleaned before tab callbacks because its production Refresh uses deferred Destroy. Career draft callbacks are memory-only. Not full MainMenu, storage, hardware or automatic focus/navigation acceptance.";
        public List<Record> records = new List<Record>();
    }

    public static string Capture(string stage, bool rewritten)
    {
        if (stage != "before" && stage != "after" || rewritten != (stage == "after"))
            throw new ArgumentException("Capture stage must match authored source.");
        CheckDatabase(TechTreeDatabaseFactory.CreateDefault(), rewritten);
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || ModularRegressionRunner.NativeTestsRunning())
            throw new InvalidOperationException("Idle editor required.");
        string directory = Path.GetFullPath("production/qa/evidence/tech-driver-I05a-2026-11-02/" + stage +
            "/tech-ui-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmssfff"));
        if (Directory.Exists(directory)) throw new IOException("Capture directory exists.");
        Directory.CreateDirectory(directory);
        var manifest = new Manifest { stage = stage, runtimeMvid = typeof(TechTreeRules).Assembly.ManifestModule.ModuleVersionId.ToString() };
        foreach (var size in Sizes())
            for (int screen = 0; screen < 2; screen++)
                manifest.records.Add(Exercise(size.x, size.y, screen, 0, rewritten,
                    Path.Combine(directory, size.x + "x" + size.y + (screen == 0 ? "-normal.png" : "-career.png"))));
        string path = Path.Combine(directory, "manifest.json");
        File.WriteAllText(path, JsonUtility.ToJson(manifest, true)); return path;
    }

    public static Record Exercise(int width, int height, int screen, int outcome, bool rewritten, string png = null)
    {
        TechTreeDatabase database = TechTreeDatabaseFactory.CreateDefault(); CheckDatabase(database, rewritten);
        var cache = (Dictionary<TeamId, TechTreeState>)typeof(TechTreeProfileStore).GetField("Cache", Static).GetValue(null);
        var previousProfiles = new Dictionary<TeamId, TechTreeState>(cache);
        var profileTokens = new Dictionary<TeamId, string>();
        FieldInfo driverChoice = typeof(DriverSelectionState).GetField("selectedDriverId", Static);
        FieldInfo settings = typeof(GameSettingsRuntime).GetField("current", Static);
        FieldInfo audio = typeof(AudioService).GetField("instance", Static);
        object oldDriver = driverChoice.GetValue(null), oldSettings = settings.GetValue(null), oldAudio = audio.GetValue(null);
        CareerUiPointerTestScope scope = null;
        try
        {
            // Seed every possible tab before production GetOrCreate, so it cannot read or create a real profile.
            cache.Clear();
            foreach (TeamId team in Enum.GetValues(typeof(TeamId)))
            {
                cache.Add(team, new TechTreeState(team, 987654321));
                profileTokens.Add(team, TechTreeProfileCodec.Encode(cache[team]));
            }
            Assert.That(DriverSelectionState.TrySelect(DriverCatalog.All.First(d => d.Team == TeamId.IT).Id), Is.True);
            GameSettingsData ownedSettings = GameSettingsData.CreateDefault();
            ownedSettings.masterVolume = 0; ownedSettings.reduceMotion = true;
            settings.SetValue(null, ownedSettings); audio.SetValue(null, null);
            scope = CareerUiPointerTestScope.CreateSized(width, height, () => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                "Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));
            scope.Root.GetComponent<Canvas>().worldCamera.orthographic = true;
            Image background = scope.Root.AddComponent<Image>(); background.color = RacingTechTheme.Graphite; background.raycastTarget = false;
            Button lower = CreateUnderlyingButton(scope);
            var record = new Record { width = width, height = height, screen = screen, screenshot = png,
                description = database.Get(NodeId).description, cost = database.Get(NodeId).rpCost };
            if (screen == 0)
            {
                var ui = scope.Root.AddComponent<TechTreeUI>(); ui.Show();
                foreach (TeamId team in new[] { TeamId.UK, TeamId.DE, TeamId.IT, TeamId.US, TeamId.CN, TeamId.JP, TeamId.IT })
                {
                    // Only owned prior columns are removed. This is not acceptance of production's deferred-destruction lifecycle.
                    Transform columns = (Transform)typeof(TechTreeUI).GetField("contentRoot", Instance).GetValue(ui);
                    for (int i = columns.childCount - 1; i >= 0; i--) Object.DestroyImmediate(columns.GetChild(i).gameObject);
                    scope.Click(FindButton(scope, team.ToString()));
                    Assert.That((TeamId)typeof(TechTreeUI).GetField("selectedTeam", Instance).GetValue(ui), Is.EqualTo(team));
                    record.visitedTeams++;
                }
                Button target = FindButton(scope, NodeId);
                record.label = target.GetComponentInChildren<TMP_Text>().text;
                Assert.That(record.label, Does.Contain(record.description).And.Contain("RP 8,000").And.Contain("锁定"));
                Assert.That(TechTreeRules.CanUnlock(cache[TeamId.IT], NodeId, database), Is.False);
                record.rp = cache[TeamId.IT].rpBalance;
                Assert.That(scope.Root.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("987,654,321")), Is.True);
                Render(scope, target, lower, record);
                scope.Click(FindButton(scope, "BackButton"));
                record.closed = !scope.Root.transform.Find("TechTreeOverlay").gameObject.activeSelf;
                Assert.That(record.closed, Is.True);
            }
            else
            {
                CareerSeasonState season = SummerBreak(database);
                CareerTechSnapshot source = season.ActiveTechSnapshot;
                int balance = source.RpBalance, closed = 0, confirmed = 0;
                CareerTechSnapshot accepted = null;
                var ui = scope.Root.AddComponent<CareerTechTreeUI>();
                Assert.That(ui.Show(season, snapshot => { confirmed++; accepted = snapshot; return outcome != 2; }, () => closed++), Is.True);
                Button target = FindButton(scope, NodeId);
                record.label = target.GetComponentInChildren<TMP_Text>().text;
                Assert.That(record.label, Does.Contain(record.description).And.Contain("RP 8,000").And.Contain("可研发"));
                record.rp = balance;
                Render(scope, target, lower, record);
                scope.Click(target);
                var draft = (CareerTechTreeDraft)typeof(CareerTechTreeUI).GetField("draft", Instance).GetValue(ui);
                Assert.That(draft.IsActive(NodeId), Is.True);
                Assert.That(draft.RpBalance, Is.EqualTo(balance - record.cost));
                record.sourceSnapshotUnchanged = !source.UnlockedNodeIds.Contains(NodeId) && source.RpBalance == balance;
                Assert.That(record.sourceSnapshotUnchanged, Is.True);
                if (outcome == 0) scope.Click(FindButton(scope, "CareerTechCancel"));
                else
                {
                    Button confirm = FindButton(scope, "CareerTechConfirm");
                    ExecuteEvents.Execute(confirm.gameObject, new BaseEventData(scope.EventSystem), ExecuteEvents.submitHandler);
                    Assert.That(confirmed, Is.EqualTo(1));
                    Assert.That(accepted.ActiveNodeIds, Contains.Item(NodeId));
                    Assert.That(accepted.RpBalance, Is.EqualTo(balance - record.cost));
                    if (outcome == 2)
                    {
                        Assert.That(closed, Is.Zero);
                        Assert.That(scope.Root.GetComponentsInChildren<TMP_Text>().Any(t => t.text.Contains("保存失败")), Is.True);
                        scope.Click(FindButton(scope, "CareerTechCancel"));
                    }
                }
                record.closed = !scope.Root.transform.Find("CareerTechTreeOverlay").gameObject.activeSelf;
                Assert.That(record.closed, Is.True); Assert.That(closed, Is.EqualTo(1));
                Assert.That(source.UnlockedNodeIds, Does.Not.Contain(NodeId)); Assert.That(source.RpBalance, Is.EqualTo(balance));
                Assert.That(confirmed, Is.EqualTo(outcome == 0 ? 0 : 1));
            }
            record.normalProfilesUnchanged = cache.All(p => profileTokens[p.Key] == TechTreeProfileCodec.Encode(p.Value));
            Assert.That(record.normalProfilesUnchanged, Is.True);
            return record;
        }
        finally
        {
            try { scope?.Dispose(); }
            finally
            {
                cache.Clear(); foreach (var pair in previousProfiles) cache.Add(pair.Key, pair.Value);
                driverChoice.SetValue(null, oldDriver); settings.SetValue(null, oldSettings); audio.SetValue(null, oldAudio);
            }
        }
    }

    private static CareerSeasonState SummerBreak(TechTreeDatabase database)
    {
        var unlocked = database.GetAllForTeamInTier(TeamId.IT, TechTreeTier.L1, false).Where(n => n.IsCommon).Select(n => n.id)
            .Concat(database.GetAllForTeamInTier(TeamId.IT, TechTreeTier.L2, false).Where(n => n.IsCommon).Take(3).Select(n => n.id)).ToArray();
        Assert.That(CareerTechSnapshot.TryCreate(TeamId.IT, 123456789, unlocked, unlocked, null, out var source), Is.True);
        var season = new CareerSeasonState();
        TeamId[] field = { TeamId.IT, TeamId.UK, TeamId.DE, TeamId.CN };
        Assert.That(CareerModeRules.TryStartSeason(season, TeamId.IT, field, source), Is.True);
        for (int i = 0; i < 4; i++)
            Assert.That(CareerModeRules.TryRecordRace(season, new CareerRaceResult("owned-it-tech-" + i,
                CareerModeRules.GetNextTrackId(season), field.Select((team, index) => new CareerCompetitorResult(team, index + 1)).ToArray())), Is.True);
        return season;
    }
    private static Button FindButton(CareerUiPointerTestScope scope, string name) => scope.Root.GetComponentsInChildren<Button>(true).Single(b => b.name == name);
    private static Button CreateUnderlyingButton(CareerUiPointerTestScope scope)
    {
        var host = new GameObject("OwnedUnderlay", typeof(RectTransform), typeof(Image), typeof(Button)); host.transform.SetParent(scope.Root.transform, false);
        host.GetComponent<RectTransform>().sizeDelta = new Vector2(100, 100); return host.GetComponent<Button>();
    }
    private static void Render(CareerUiPointerTestScope scope, Button target, Button lower, Record record)
    {
        foreach (Transform child in scope.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases(); foreach (TMP_Text text in scope.Root.GetComponentsInChildren<TMP_Text>()) text.ForceMeshUpdate(); Canvas.ForceUpdateCanvases();
        Camera camera = scope.Root.GetComponent<Canvas>().worldCamera; RenderTexture previous = RenderTexture.active; Texture2D image = null;
        try
        {
            camera.Render(); Assert.That(target.targetGraphic.depth, Is.GreaterThanOrEqualTo(0));
            record.targetWidth = target.GetComponent<RectTransform>().rect.width;
            record.targetHeight = target.GetComponent<RectTransform>().rect.height;
            foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>()) if (text.isTextOverflowing) record.targetOverflows.Add(text.text);
            Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, lower.transform.position);
            var hits = new List<RaycastResult>(); scope.Root.GetComponent<GraphicRaycaster>().Raycast(new PointerEventData(scope.EventSystem) { position = point }, hits);
            record.overlayBlocked = hits.Count > 0 && hits[0].gameObject != lower.gameObject;
            Assert.That(record.overlayBlocked, Is.True);
            if (record.screenshot == null) return;
            RenderTexture.active = scope.Target; image = new Texture2D(scope.Target.width, scope.Target.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, scope.Target.width, scope.Target.height), 0, 0); image.Apply(); File.WriteAllBytes(record.screenshot, image.EncodeToPNG());
        }
        finally { RenderTexture.active = previous; if (image != null) Object.DestroyImmediate(image); }
    }
    private static void CheckDatabase(TechTreeDatabase database, bool rewritten)
    {
        TechNodeDef node = database.Get(NodeId); Assert.That(node.rpCost, Is.EqualTo(8000));
        Assert.That(node.effects.Count(e => e.type == TechEffectType.HandSizeBonus && e.value == 1), Is.EqualTo(1));
        Assert.That(node.effects.Length, Is.EqualTo(rewritten ? 1 : 3), "Capture current authored database, never synthesized old text.");
        if (rewritten)
        { Assert.That(node.description, Does.Contain("手牌上限+1")); Assert.That(node.description, Does.Not.Contain("失控计数器上限+1").And.Not.Contain("引擎容量+1")); }
        else Assert.That(node.description, Does.Contain("失控计数器上限+1").And.Contain("引擎容量+1"));
    }
    public static IEnumerable<Vector2Int> Sizes() => new[] { new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1280, 720) };
}

public sealed class ItalyL2ProductionUiTests
{
    [TestCase(1920, 1080)] [TestCase(2560, 1440)] [TestCase(1280, 720)]
    public void NormalReadOnlyTabsShowRealItalianCostAndDescriptionWithoutProfileWrites(int width, int height)
    {
        var record = ItalyL2ProductionUiCapture.Exercise(width, height, 0, 0, true);
        Assert.That(record.visitedTeams, Is.EqualTo(7)); Assert.That(record.targetOverflows, Is.Empty);
        Assert.That(record.targetWidth, Is.GreaterThan(400f)); Assert.That(record.targetHeight, Is.EqualTo(106f).Within(1f));
    }
    private static IEnumerable<TestCaseData> CareerCases()
    {
        foreach (var size in ItalyL2ProductionUiCapture.Sizes())
            for (int outcome = 0; outcome < 3; outcome++) yield return new TestCaseData(size.x, size.y, outcome);
    }
    [TestCaseSource(nameof(CareerCases))]
    public void CareerOwnedDraftKeepsNormalRpAndOriginalSnapshotIsolated(int width, int height, int outcome)
    {
        var record = ItalyL2ProductionUiCapture.Exercise(width, height, 1, outcome, true);
        Assert.That(record.targetOverflows, Is.Empty); Assert.That(record.sourceSnapshotUnchanged, Is.True);
        Assert.That(record.targetWidth, Is.GreaterThan(400f)); Assert.That(record.targetHeight, Is.EqualTo(106f).Within(1f));
    }
}
