using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Profiling;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent, privacy-safe operation recorder for external Demo playtests.
/// Logs stay local until the tester explicitly exports and sends a bundle.
/// </summary>
public sealed class PlaytestTelemetryService : MonoBehaviour
{
    private const int TargetPathDepth = 8;
    private const int LabelLength = 120;
    private const float PerformanceSampleInterval = 15f;
    private const float SlowFrameThreshold = 0.05f;
    private static readonly KeyCode[] RecordedKeys =
    {
        KeyCode.Escape, KeyCode.Return, KeyCode.Space,
        KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.UpArrow, KeyCode.DownArrow,
        KeyCode.A, KeyCode.D, KeyCode.F,
        KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4,
        KeyCode.Keypad1, KeyCode.Keypad2, KeyCode.Keypad3, KeyCode.Keypad4
    };

    private static PlaytestTelemetryService instance;
    private readonly ConcurrentQueue<PendingConsoleEvent> pendingConsoleEvents =
        new ConcurrentQueue<PendingConsoleEvent>();
    private PlaytestLogWriter writer;
    private string currentScene = string.Empty;
    private string userProfileRoot = string.Empty;
    private string dataPath = string.Empty;
    private string applicationRoot = string.Empty;
    private bool quitting;
    private float performanceWindowStarted;
    private float worstFrameDelta;
    private int performanceFrameCount;
    private int slowFrameCount;
    private int issueMarkerCount;
    private string markerToast = string.Empty;
    private float markerToastUntil;

    public static string DefaultLogRoot => Path.Combine(Application.persistentDataPath, "playtest-logs");
    public static string DefaultExportRoot => Path.Combine(Application.persistentDataPath, "playtest-exports");
    public static string CurrentSessionDirectory => instance?.writer?.SessionDirectory;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;

        var root = new GameObject("PlaytestTelemetryService");
        DontDestroyOnLoad(root);
        instance = root.AddComponent<PlaytestTelemetryService>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        currentScene = SceneManager.GetActiveScene().name;
        userProfileRoot = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        dataPath = Application.dataPath;
        applicationRoot = Directory.GetParent(dataPath)?.FullName ?? string.Empty;
        performanceWindowStarted = Time.realtimeSinceStartup;

        try
        {
            var metadata = new PlaytestSessionMetadata
            {
                appVersion = Application.version,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
                language = Application.systemLanguage.ToString(),
                screenWidth = Screen.width,
                screenHeight = Screen.height,
                fullscreen = Screen.fullScreen,
                operatingSystem = PlaytestLogWriter.SanitizeValue(SystemInfo.operatingSystem, 240),
                processorType = PlaytestLogWriter.SanitizeValue(SystemInfo.processorType, 240),
                graphicsDeviceName = PlaytestLogWriter.SanitizeValue(SystemInfo.graphicsDeviceName, 240),
                systemMemoryMb = SystemInfo.systemMemorySize,
                graphicsMemoryMb = SystemInfo.graphicsMemorySize
            };
            writer = new PlaytestLogWriter(DefaultLogRoot, metadata);
        }
        catch
        {
            writer = null;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Application.logMessageReceivedThreaded += OnConsoleMessage;
        Record("lifecycle", "session_start", details: BuildRuntimeSnapshot());
    }

    private void Update()
    {
        DrainConsoleEvents();

        for (int button = 0; button <= 2; button++)
        {
            if (Input.GetMouseButtonDown(button))
                RecordPointer("down", button);
            if (Input.GetMouseButtonUp(button))
                RecordPointer("up", button);
        }

        for (int i = 0; i < RecordedKeys.Length; i++)
        {
            if (Input.GetKeyDown(RecordedKeys[i]))
                Record("key", "down", RecordedKeys[i].ToString(), BuildRuntimeSnapshot());
        }

        if (Input.GetKeyDown(KeyCode.F8))
            CaptureIssueMarker("F8");

        SamplePerformance();
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(markerToast) || Time.realtimeSinceStartup > markerToastUntil)
            return;

        const float width = 420f;
        GUI.Box(new Rect(Mathf.Max(12f, Screen.width - width - 20f), 20f, width, 48f), markerToast);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        Record("lifecycle", hasFocus ? "focus_gained" : "focus_lost", details: BuildRuntimeSnapshot());
    }

    private void OnApplicationPause(bool paused)
    {
        Record("lifecycle", paused ? "paused" : "resumed", details: BuildRuntimeSnapshot());
        writer?.Flush();
    }

    private void OnApplicationQuit()
    {
        quitting = true;
        DrainConsoleEvents(int.MaxValue);
        writer?.Complete("application_quit");
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        Application.logMessageReceivedThreaded -= OnConsoleMessage;
        if (!quitting)
            writer?.Complete("service_destroyed");
        if (instance == this)
            instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        currentScene = scene.name;
        Record("scene", "loaded", scene.name, $"mode={mode};{BuildRuntimeSnapshot()}");
    }

    private void OnActiveSceneChanged(Scene previous, Scene next)
    {
        currentScene = next.name;
        Record("scene", "active_changed", next.name, $"previous={previous.name};{BuildRuntimeSnapshot()}");
    }

    private void RecordPointer(string phase, int button)
    {
        Vector2 position = Input.mousePosition;
        float normalizedX = Screen.width > 0 ? position.x / Screen.width : -1f;
        float normalizedY = Screen.height > 0 ? position.y / Screen.height : -1f;
        string details;
        string target = ResolvePointerTarget(position, out details);
        details = $"button={button};{details};{BuildRuntimeSnapshot()}";
        Record("pointer", phase, target, details, normalizedX, normalizedY);
    }

    private static string ResolvePointerTarget(Vector2 screenPosition, out string details)
    {
        details = "hit=none";
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return string.Empty;

        var pointer = new PointerEventData(eventSystem) { position = screenPosition };
        var results = new List<RaycastResult>();
        eventSystem.RaycastAll(pointer, results);
        if (results.Count == 0 || results[0].gameObject == null)
        {
            Camera camera = Camera.main;
            if (camera == null)
                return string.Empty;
            Vector3 world = camera.ScreenToWorldPoint(screenPosition);
            Collider2D collider = Physics2D.OverlapPoint(new Vector2(world.x, world.y));
            if (collider == null)
                return string.Empty;
            details = "hit=Collider2D;interactable=true;label=";
            return BuildHierarchyPath(collider.transform);
        }

        GameObject hit = results[0].gameObject;
        Selectable selectable = hit.GetComponentInParent<Selectable>();
        GameObject target = selectable != null ? selectable.gameObject : hit;
        string label = ResolveAuthoredLabel(target);
        string component = selectable != null ? selectable.GetType().Name : hit.GetType().Name;
        details = $"hit={component};interactable={(selectable == null || selectable.IsInteractable())};label={label}";
        return BuildHierarchyPath(target.transform);
    }

    private static string ResolveAuthoredLabel(GameObject target)
    {
        if (target.GetComponentInParent<TMP_InputField>() != null
            || target.GetComponentInChildren<TMP_InputField>(true) != null
            || target.GetComponentInParent<InputField>() != null
            || target.GetComponentInChildren<InputField>(true) != null)
            return "[text input omitted]";

        TMP_Text tmp = target.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
            return PlaytestLogWriter.SanitizeValue(tmp.text, LabelLength);
        Text text = target.GetComponentInChildren<Text>(true);
        return text == null ? string.Empty : PlaytestLogWriter.SanitizeValue(text.text, LabelLength);
    }

    private static string BuildHierarchyPath(Transform target)
    {
        if (target == null)
            return string.Empty;
        var parts = new List<string>();
        Transform current = target;
        while (current != null && parts.Count < TargetPathDepth)
        {
            parts.Add(current.name);
            current = current.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    private string BuildRuntimeSnapshot()
    {
        return $"resolution={Screen.width}x{Screen.height};fullscreen={Screen.fullScreen};time_scale={Time.timeScale:F2}";
    }

    private void SamplePerformance()
    {
        float delta = Time.unscaledDeltaTime;
        if (delta > 0f && delta < 5f)
        {
            performanceFrameCount++;
            worstFrameDelta = Mathf.Max(worstFrameDelta, delta);
            if (delta >= SlowFrameThreshold)
                slowFrameCount++;
        }

        float now = Time.realtimeSinceStartup;
        float elapsed = now - performanceWindowStarted;
        if (elapsed < PerformanceSampleInterval)
            return;

        float averageFps = elapsed > 0f ? performanceFrameCount / elapsed : 0f;
        long allocatedMemoryMb = Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L);
        Record(
            "performance",
            "sample",
            details: $"window_s={elapsed:F1};average_fps={averageFps:F1};worst_frame_ms={worstFrameDelta * 1000f:F1};slow_frames={slowFrameCount};allocated_memory_mb={allocatedMemoryMb}");
        performanceWindowStarted = now;
        performanceFrameCount = 0;
        worstFrameDelta = 0f;
        slowFrameCount = 0;
    }

    private void CaptureIssueMarker(string source)
    {
        if (writer == null || string.IsNullOrEmpty(writer.SessionDirectory))
            return;

        try
        {
            issueMarkerCount++;
            string directory = Path.Combine(writer.SessionDirectory, "screenshots");
            Directory.CreateDirectory(directory);
            string filename = $"issue-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{issueMarkerCount:D2}.png";
            string path = Path.Combine(directory, filename);
            ScreenCapture.CaptureScreenshot(path);
            Record(
                "diagnostics",
                "issue_marked",
                source,
                $"screenshot=screenshots/{filename};{BuildRuntimeSnapshot()}");
            writer.Flush();
            markerToast = "问题已标记并截图，可在设置中导出测试日志（F8）";
            markerToastUntil = Time.realtimeSinceStartup + 3f;
        }
        catch (Exception exception)
        {
            Record("diagnostics", "issue_marker_failed", source, exception.GetType().Name);
            markerToast = "问题标记失败，日志记录仍会继续";
            markerToastUntil = Time.realtimeSinceStartup + 3f;
        }
    }

    private void OnConsoleMessage(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Log
            || (!string.IsNullOrEmpty(condition)
                && condition.StartsWith("[PlaytestLog]", StringComparison.Ordinal)))
            return;

        pendingConsoleEvents.Enqueue(new PendingConsoleEvent(condition, stackTrace, type));
    }

    private void DrainConsoleEvents(int maximum = 100)
    {
        int count = 0;
        while (count < maximum && pendingConsoleEvents.TryDequeue(out PendingConsoleEvent pending))
        {
            count++;
            string action = pending.Type == LogType.Warning ? "warning"
                : pending.Type == LogType.Exception ? "exception"
                : pending.Type == LogType.Assert ? "assert"
                : "error";
            string message = pending.Condition
                + (string.IsNullOrEmpty(pending.StackTrace) ? string.Empty : " | " + pending.StackTrace);
            Record("console", action, details: SanitizeLocalRoots(message));
        }
    }

    private string SanitizeLocalRoots(string value)
    {
        string sanitized = value ?? string.Empty;
        if (!string.IsNullOrEmpty(userProfileRoot))
            sanitized = sanitized.Replace(userProfileRoot, "%USERPROFILE%");
        if (!string.IsNullOrEmpty(dataPath))
            sanitized = sanitized.Replace(dataPath, "%GAME_DATA%");
        if (!string.IsNullOrEmpty(applicationRoot))
            sanitized = sanitized.Replace(applicationRoot, "%GAME_ROOT%");
        return PlaytestLogWriter.SanitizeValue(sanitized);
    }

    public static void Record(
        string category,
        string action,
        string target = "",
        string details = "",
        float pointerX = -1f,
        float pointerY = -1f)
    {
        PlaytestTelemetryService service = instance;
        if (service?.writer == null)
            return;
        service.writer.Record(
            Time.realtimeSinceStartup,
            service.currentScene,
            category,
            action,
            target,
            details,
            pointerX,
            pointerY,
            Time.timeScale);
    }

    public static void Flush()
    {
        instance?.writer?.Flush();
    }

    private readonly struct PendingConsoleEvent
    {
        public PendingConsoleEvent(string condition, string stackTrace, LogType type)
        {
            Condition = condition ?? string.Empty;
            StackTrace = stackTrace ?? string.Empty;
            Type = type;
        }

        public string Condition { get; }
        public string StackTrace { get; }
        public LogType Type { get; }
    }
}
