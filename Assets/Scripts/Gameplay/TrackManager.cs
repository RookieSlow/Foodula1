using System.Collections.Generic;
using UnityEngine;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 赛道管理器 — 支持从 JSON 加载赛道或使用硬编码赛道。
/// JSON 模式：从 Resources/Configs/Tracks/[trackId].json 加载。
/// 硬编码模式：使用 42 节点的 MVP 测试赛道。
/// </summary>
public class TrackManager : MonoBehaviour
{
    [Header("配置")]
    public GameConfigSO config;

    [Header("Prefab")]
    public GameObject nodePrefab;

    [Header("赛道渲染")]
    public float lineWidth = 0.5f;
    public Color lineColor = Color.white;
    public Color straightNodeColor = Color.white;
    public Color cornerNodeColor = new Color(1f, 0.5f, 0f, 1f);
    public Color apexNodeColor = Color.red;
    public Color startFinishNodeColor = Color.green;

    [Header("游玩时赛道蒙版")]
    [Tooltip("游玩时覆盖弯道道路区域的黄色半透明蒙版。")]
    public Color cornerMaskColor = new Color(1f, 0.82f, 0.05f, 0.34f);
    [Tooltip("游玩时覆盖弯心的红色半透明蒙版。")]
    public Color apexMaskColor = new Color(1f, 0.12f, 0.08f, 0.72f);
    [Min(0.01f)]
    public float cornerMaskWidth = 1.05f;
    [Min(0.01f)]
    public float apexMaskDiameter = 0.72f;
    [Tooltip("调试模式：游玩时按 F8 显示/隐藏带编号的节点覆盖层。")]
    public bool showDebugTrackNodesInPlay;

    // --- 运行时数据 ---
    private List<TrackNode> nodes = new List<TrackNode>();
    private List<GameObject> nodeObjects = new List<GameObject>();
    private List<LineRenderer> laneLineRenderers = new List<LineRenderer>();
    private List<LineRenderer> cornerMaskRenderers = new List<LineRenderer>();
    private List<GameObject> apexMaskObjects = new List<GameObject>();
    private List<GameObject> speedLimitLabelObjects = new List<GameObject>();
    private TrackRuntimeContext runtimeContext;

    /// <summary>弯道 ID → 限速的快速查找表。</summary>
    private Dictionary<int, int> cornerSpeedLimits = new Dictionary<int, int>();

    /// <summary>弯道 ID → 名称。</summary>
    private Dictionary<int, string> cornerNames = new Dictionary<int, string>();

    /// <summary>当前加载的赛道配置（JSON 模式非 null）。</summary>
    public TrackConfig LoadedTrackConfig { get; private set; }

    /// <summary>
    /// The loaded track snapshot used by gameplay and presentation adapters.
    /// The legacy TrackManager methods below remain as a compatibility facade.
    /// </summary>
    public TrackRuntimeContext Runtime
    {
        get
        {
            EnsureRuntimeContext();
            return runtimeContext;
        }
    }

    /// <summary>统一硬编码赛道为 JSON 后的回退赛道 ID（roadmap P1 #9）。</summary>
    public const string FallbackTrackId = "fallback_42";
    private const float FallbackWorldWidth = 31.9f;   // 原始 42 节点形状包围盒
    private const float FallbackWorldHeight = 12.3f;

    // --- 公开属性 ---
    public int TotalNodes => Runtime.TotalNodes;
    public int TotalLaps => Runtime.TotalLaps;
    public int LaneCount => Runtime.LaneCount;
    public IReadOnlyList<TrackNode> Nodes => Runtime.Nodes;
    public int StartFinishNodeIndex => Runtime.StartFinishNodeIndex;
    public string TrackId => Runtime.TrackId;
    public string TrackName => Runtime.TrackName;
    public string Country => Runtime.Country;
    public string DefaultWeather => Runtime.DefaultWeather;
    public string[] WeatherPool => Runtime.WeatherPool;
    public bool AllowsStartFinishLaneChange => Runtime.AllowsStartFinishLaneChange;

    void Awake()
    {
        string configuredTrackId = config != null ? config.trackId : string.Empty;
        string trackId = TrackSelectionState.ResolveTrackId(configuredTrackId);

        if (config == null)
        {
            Debug.LogWarning("[TrackManager] GameConfigSO reference is missing — using defaults.");
            BuildHardcodedTrack();
        }
        else if (!string.IsNullOrEmpty(trackId))
        {
            if (!LoadTrackFromJson(trackId))
            {
                Debug.LogWarning($"[TrackManager] Failed to load '{trackId}', falling back to hardcoded track.");
                BuildHardcodedTrack();
            }
        }
        else
        {
            // 无配置赛道 → 使用 JSON 回退赛道（原硬编码 42 节点导出，roadmap P1 #9）
            if (!LoadTrackFromJson(FallbackTrackId))
            {
                Debug.LogWarning($"[TrackManager] {FallbackTrackId}.json 加载失败，退回代码内建赛道。");
                BuildHardcodedTrack();
            }
        }

        RenderTrack();

        TrackDebugOverlay overlay = GetComponent<TrackDebugOverlay>();
        if (overlay == null)
            overlay = gameObject.AddComponent<TrackDebugOverlay>();
        overlay.Initialize(this);
    }

    // ===================================================================
    // JSON TRACK LOADING
    // ===================================================================

    /// <summary>
    /// 尝试从 JSON 加载赛道。成功返回 true。
    /// </summary>
    private bool LoadTrackFromJson(string trackId)
    {
        TrackConfig cfg = TrackDataLoader.LoadConfig(trackId);
        if (cfg == null) return false;

        LoadedTrackConfig = cfg;
        runtimeContext = null;
        nodes = TrackDataLoader.ConfigToNodes(cfg);

        TrackDataLoader.BuildCornerMaps(cfg, nodes, out cornerSpeedLimits, out cornerNames);

        Debug.Log($"[TrackManager] JSON track loaded: {cfg.trackName} ({nodes.Count} nodes, {cfg.laps} laps, " +
                  $"Lv1={CountCornerLevel(cfg, 1)} Lv2={CountCornerLevel(cfg, 2)} Lv3={CountCornerLevel(cfg, 3)})");
        return true;
    }

    private static int CountCornerLevel(TrackConfig cfg, int level)
    {
        int count = 0;
        foreach (var c in cfg.cells)
            if (c.IsCorner && c.cornerLevel == level) count++;
        return count;
    }

    // ===================================================================
    // HARDCODED FALLBACK TRACK (42 nodes)
    // ===================================================================

    private void BuildHardcodedTrack()
    {
        LoadedTrackConfig = null;
        runtimeContext = null;
        nodes.Clear();

        Vector2[] rawShape = GetTrackShape42();

        for (int i = 0; i < rawShape.Length; i++)
        {
            nodes.Add(new TrackNode(i, 99, $"Straight {i}"));
        }

        DefineApex(9, 1, 3, "T1 Parabolica");
        DefineApex(17, 2, 2, "T2 Grand Hotel");
        DefineApex(25, 3, 4, "T3 Copse");
        DefineApex(31, 4, 2, "T4 Chicane");
        DefineApex(37, 5, 3, "T5 Lesmo");

        nodes[0].isStartFinish = true;
        nodes[0].nodeName = "Start/Finish";

        cornerSpeedLimits.Clear();
        cornerNames.Clear();
        foreach (var node in nodes)
        {
            if (node.cornerId > 0 && !cornerSpeedLimits.ContainsKey(node.cornerId))
            {
                cornerSpeedLimits[node.cornerId] = node.speedLimit;
                cornerNames[node.cornerId] = node.nodeName;
            }
        }
    }

    private void DefineApex(int nodeIndex, int cornerId, int speedLimit, string name)
    {
        nodes[nodeIndex].cornerId = cornerId;
        nodes[nodeIndex].speedLimit = speedLimit;
        nodes[nodeIndex].nodeName = name;
        nodes[nodeIndex].isApex = true;
    }

    // ===================================================================
    // PUBLIC API
    // ===================================================================

    public HashSet<int> GetUniqueCornersCrossed(int fromPos, int toPos)
    {
        return Runtime.GetUniqueCornersCrossed(fromPos, toPos);
    }

    public bool CrossesStartFinish(int fromPos, int toPos, out int startFinishIndex)
    {
        return Runtime.CrossesStartFinish(fromPos, toPos, out startFinishIndex);
    }

    public int GetApexNodeIndex(int cornerId)
    {
        return Runtime.GetApexNodeIndex(cornerId);
    }

    public int GetCornerSpeedLimit(int cornerId)
    {
        return Runtime.GetCornerSpeedLimit(cornerId);
    }

    public int GetCornerSpeedLimit(int cornerId, int laneIndex)
    {
        return Runtime.GetCornerSpeedLimit(cornerId, laneIndex);
    }

    public string GetCornerName(int cornerId)
    {
        return Runtime.GetCornerName(cornerId);
    }

    public Vector3 GetNodePosition(int index)
    {
        return Runtime.GetNodePosition(index);
    }

    public Vector3 GetNodePosition(int index, int laneIndex)
    {
        return Runtime.GetNodePosition(index, laneIndex);
    }

    public int GetDefaultLaneIndex(bool isAi)
    {
        return Runtime.GetDefaultLaneIndex(isAi);
    }

    public int GetLaneTowardsInside(int laneIndex)
    {
        return Runtime.GetLaneTowardsInside(laneIndex);
    }

    public int GetLaneTowardsOutside(int laneIndex)
    {
        return Runtime.GetLaneTowardsOutside(laneIndex);
    }

    public TrackNode GetNode(int index)
    {
        return Runtime.GetNode(index);
    }

    public bool IsInCorner(int position)
    {
        return Runtime.IsInCorner(position);
    }

    // ===================================================================
    // RENDERING
    // ===================================================================

    private void RenderTrack()
    {
        Vector2[] pathCoords = GetPathCoordinates();
        float medianSpacing = TrackPresentationRules.CalculateMedianNeighborDistance(pathCoords);
        float nodeScaleMultiplier = TrackPresentationRules.CalculateNodeScaleMultiplier(
            medianSpacing,
            GetNodeVisualDiameter(),
            config != null ? config.trackNodeSpacingFillRatio : 0.65f,
            config != null ? config.trackNodeMinimumScaleMultiplier : 0.03f);
        float adaptiveLineWidth = medianSpacing > 0f
            ? Mathf.Min(
                lineWidth,
                medianSpacing * (config != null ? config.trackLineSpacingFillRatio : 0.3f))
            : lineWidth;

        // Play-mode debugging is handled by TrackDebugOverlay so it can be
        // toggled with F8 without rebuilding masks or changing the track path.
        bool showDebugNodes = !Application.isPlaying;

        // Spawn node GameObjects only for editor/debug visualization. The
        // playable scene uses the track background plus corner masks instead.
        if (showDebugNodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3 spawnPos = new Vector3(pathCoords[i].x, pathCoords[i].y, 0);
                GameObject obj = nodePrefab != null
                    ? Instantiate(nodePrefab, spawnPos, Quaternion.identity)
                    : new GameObject($"Node_{i}") { transform = { position = spawnPos } };
                obj.name = $"Node_{i}";
                obj.transform.localScale = Vector3.Scale(
                    obj.transform.localScale,
                    new Vector3(nodeScaleMultiplier, nodeScaleMultiplier, 1f));

                SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = TrackPresentationRules.GetNodeColor(
                        nodes[i],
                        straightNodeColor,
                        cornerNodeColor,
                        apexNodeColor,
                        startFinishNodeColor);
                }

                nodeObjects.Add(obj);
            }
        }

        // Keep lane lines as an editor/debug aid only. In play mode the
        // generated background sprite is the sole road surface.
        if (showDebugNodes)
        {
            laneLineRenderers.Clear();
            for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
            {
                GameObject lineObj = new GameObject($"TrackLane_{laneIndex + 1}");
                LineRenderer laneRenderer = lineObj.AddComponent<LineRenderer>();
                laneRenderer.positionCount = pathCoords.Length + 1;
                laneRenderer.startWidth = adaptiveLineWidth;
                laneRenderer.endWidth = adaptiveLineWidth;
                laneRenderer.useWorldSpace = true;
                laneRenderer.material = new Material(Shader.Find("Sprites/Default"));
                laneRenderer.startColor = lineColor;
                laneRenderer.endColor = lineColor;
                laneRenderer.sortingOrder = -1;

                for (int i = 0; i < pathCoords.Length; i++)
                {
                    laneRenderer.SetPosition(i, GetNodePosition(i, laneIndex));
                }
                laneRenderer.SetPosition(pathCoords.Length, GetNodePosition(0, laneIndex));
                laneLineRenderers.Add(laneRenderer);
            }
        }

        if (Application.isPlaying)
        {
            RenderCornerMasks(pathCoords);
        }
        else
        {
            AddCornerLabels(pathCoords, nodeScaleMultiplier);
        }
    }

    /// <summary>
    /// 获取赛道坐标。JSON 模式下从配置中读取并缩放；硬编码模式使用旧坐标。
    /// </summary>
    private float GetNodeVisualDiameter()
    {
        if (nodePrefab == null)
        {
            return 0f;
        }

        Vector3 prefabScale = nodePrefab.transform.localScale;
        return Mathf.Max(
            Mathf.Abs(prefabScale.x),
            Mathf.Abs(prefabScale.y));
    }

    private Vector2[] GetPathCoordinates()
    {
        EnsureRuntimeContext();
        Vector2[] coordinates = new Vector2[Runtime.WorldPathCoordinates.Count];
        for (int i = 0; i < coordinates.Length; i++)
            coordinates[i] = Runtime.WorldPathCoordinates[i];
        return coordinates;
    }

    private void EnsureRuntimeContext()
    {
        if (runtimeContext != null)
            return;

        string trackId = LoadedTrackConfig != null
            ? LoadedTrackConfig.trackId
            : (config != null ? TrackSelectionState.ResolveTrackId(config.trackId) : FallbackTrackId);
        Vector2[] pathCoordinates = BuildPathCoordinates(trackId);
        float[] offsets = TrackPresentationRules.CalculateCenteredLaneOffsets(
            TrackPresentationRules.GetLaneCount(trackId),
            config != null ? config.trackLaneSpacing : 0.28f);
        int configuredLaps = config != null ? config.totalLaps : 3;

        runtimeContext = new TrackRuntimeContext(
            trackId,
            LoadedTrackConfig,
            nodes,
            pathCoordinates,
            offsets,
            cornerSpeedLimits,
            cornerNames,
            configuredLaps);
    }

    private Vector2[] BuildPathCoordinates(string trackId)
    {
        if (LoadedTrackConfig == null)
            return GetTrackShape42();

        // The fallback JSON retains the original non-square prototype bounds.
        // Keep this choice inside the track snapshot instead of mutating the
        // shared GameConfigSO when the JSON is loaded.
        float worldWidth = trackId == FallbackTrackId
            ? FallbackWorldWidth
            : (config != null ? config.trackWorldSize : 30f);
        float worldHeight = trackId == FallbackTrackId
            ? FallbackWorldHeight
            : (config != null && config.trackWorldHeight > 0f
                ? config.trackWorldHeight
                : worldWidth);
        return TrackDataLoader.ConfigToWorldPositions(
            LoadedTrackConfig,
            worldWidth,
            worldHeight);
    }

    private void AddCornerLabels(Vector2[] pathCoords, float nodeScaleMultiplier)
    {
        var cornerRanges = new Dictionary<int, (int first, int last, int limit)>();
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].cornerId > 0)
            {
                int cid = nodes[i].cornerId;
                if (!cornerRanges.ContainsKey(cid))
                    cornerRanges[cid] = (i, i, nodes[i].speedLimit);
                else
                {
                    var r = cornerRanges[cid];
                    r.last = i;
                    cornerRanges[cid] = r;
                }
            }
        }

        TMP_Text existingTmp = FindObjectOfType<TMP_Text>();
        TMP_FontAsset font = existingTmp != null ? existingTmp.font : null;

        foreach (var kv in cornerRanges)
        {
            int midIdx = (kv.Value.first + kv.Value.last) / 2;
            Vector3 centerPos = new Vector3(pathCoords[midIdx].x, pathCoords[midIdx].y, 0);
            Vector3 offset = GetCornerLabelOffset(midIdx, pathCoords);

            GameObject label = new GameObject($"CornerLabel_{kv.Key}");
            label.transform.position = centerPos + offset;

            SpriteRenderer bg = label.AddComponent<SpriteRenderer>();
            bg.sprite = nodePrefab != null ? nodePrefab.GetComponent<SpriteRenderer>()?.sprite : null;
            bg.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            bg.transform.localScale = new Vector3(0.8f, 0.8f, 1f) * nodeScaleMultiplier;
            bg.sortingOrder = 5;

            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(label.transform, false);
            textGO.transform.localPosition = Vector3.zero;

            TMPro.TextMeshPro tmpWorld = textGO.AddComponent<TMPro.TextMeshPro>();
            tmpWorld.text = kv.Value.limit.ToString();
            tmpWorld.fontSize = 6;
            tmpWorld.alignment = TMPro.TextAlignmentOptions.Center;
            tmpWorld.color = Color.white;
            tmpWorld.sortingOrder = 6;
            if (font != null) tmpWorld.font = font;
        }
    }

    private void RenderCornerMasks(Vector2[] pathCoords)
    {
        cornerMaskRenderers.Clear();
        apexMaskObjects.Clear();

        var cornerIndices = new Dictionary<int, List<int>>();
        for (int i = 0; i < nodes.Count; i++)
        {
            int cornerId = nodes[i].cornerId;
            if (cornerId <= 0)
                continue;

            if (!cornerIndices.TryGetValue(cornerId, out List<int> indices))
            {
                indices = new List<int>();
                cornerIndices.Add(cornerId, indices);
            }
            indices.Add(i);
        }

        foreach (KeyValuePair<int, List<int>> pair in cornerIndices)
        {
            List<int> indices = pair.Value;
            if (indices.Count == 0)
                continue;

            GameObject maskObject = new GameObject($"CornerMask_{pair.Key}");
            LineRenderer mask = maskObject.AddComponent<LineRenderer>();
            mask.useWorldSpace = true;
            mask.material = new Material(Shader.Find("Sprites/Default"));
            mask.startColor = cornerMaskColor;
            mask.endColor = cornerMaskColor;
            float effectiveMaskWidth = GetEffectiveCornerMaskWidth();
            mask.startWidth = effectiveMaskWidth;
            mask.endWidth = effectiveMaskWidth;
            mask.numCapVertices = 4;
            mask.numCornerVertices = 4;
            mask.sortingOrder = -2;

            int pointCount = indices.Count + 1;
            mask.positionCount = pointCount;
            for (int i = 0; i < indices.Count; i++)
            {
                mask.SetPosition(i, new Vector3(
                    pathCoords[indices[i]].x,
                    pathCoords[indices[i]].y,
                    0f));
            }

            int exitIndex = (indices[indices.Count - 1] + 1) % pathCoords.Length;
            mask.SetPosition(pointCount - 1, new Vector3(
                pathCoords[exitIndex].x,
                pathCoords[exitIndex].y,
                0f));
            cornerMaskRenderers.Add(mask);

            int apexIndex = -1;
            foreach (int index in indices)
            {
                if (nodes[index].isApex)
                {
                    apexIndex = index;
                    break;
                }
            }

            if (apexIndex >= 0)
            {
                CreateApexMask(pathCoords[apexIndex], pair.Key);
                CreateSpeedLimitLabels(pathCoords, apexIndex, pair.Key);
            }
        }
    }

    private float GetEffectiveCornerMaskWidth()
    {
        float configuredWidth = Mathf.Max(0.01f, cornerMaskWidth);
        float laneSpan = LaneCount > 1
            ? (LaneCount - 1) * (config != null ? config.trackLaneSpacing : 0.28f)
            : 0f;
        return Mathf.Max(configuredWidth, laneSpan + 0.45f);
    }

    private void CreateApexMask(Vector2 position, int cornerId)
    {
        GameObject apex = new GameObject($"ApexMask_{cornerId}");
        apex.transform.position = new Vector3(position.x, position.y, 0f);
        SpriteRenderer renderer = apex.AddComponent<SpriteRenderer>();
        renderer.sprite = nodePrefab != null
            ? nodePrefab.GetComponent<SpriteRenderer>()?.sprite
            : null;
        renderer.color = apexMaskColor;
        renderer.sortingOrder = -1;
        apex.transform.localScale = Vector3.one * apexMaskDiameter;
        apexMaskObjects.Add(apex);
    }

    private void CreateSpeedLimitLabels(Vector2[] pathCoords, int apexIndex, int cornerId)
    {
        bool hasLaneSpecificLimits = LoadedTrackConfig != null &&
            LoadedTrackConfig.laneCornerSpeedLimits != null &&
            LoadedTrackConfig.laneCornerSpeedLimits.Length >= LaneCount;
        int labelCount = hasLaneSpecificLimits ? LaneCount : 1;
        for (int laneIndex = 0; laneIndex < labelCount; laneIndex++)
        {
            Vector3 labelPosition = hasLaneSpecificLimits
                ? GetNodePosition(apexIndex, laneIndex)
                : new Vector3(pathCoords[apexIndex].x, pathCoords[apexIndex].y, 0f);
            int speedLimit = hasLaneSpecificLimits
                ? GetCornerSpeedLimit(cornerId, laneIndex)
                : GetCornerSpeedLimit(cornerId);
            CreateSpeedLimitLabel(labelPosition, speedLimit, cornerId, laneIndex);
        }
    }

    private void CreateSpeedLimitLabel(Vector3 position, int speedLimit, int cornerId, int laneIndex)
    {
        GameObject labelObject = new GameObject($"SpeedLimit_{cornerId}_{laneIndex + 1}");
        labelObject.transform.position = position;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = speedLimit.ToString();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 3.2f;
        label.fontStyle = FontStyles.Bold;
        label.color = Color.white;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color(0.05f, 0.05f, 0.05f, 0.95f);
        label.sortingOrder = 2;
        speedLimitLabelObjects.Add(labelObject);
    }

    private Vector3 GetCornerLabelOffset(int nodeIdx, Vector2[] coords)
    {
        int prev = (nodeIdx - 1 + coords.Length) % coords.Length;
        int next = (nodeIdx + 1) % coords.Length;
        Vector2 dir = (coords[next] - coords[prev]).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x);
        return new Vector3(normal.x, normal.y, 0) * 2.5f;
    }

    // ===================================================================
    // HARDCODED 42-NODE SHAPE
    // ===================================================================

    private Vector2[] GetTrackShape42()
    {
        return new Vector2[]
        {
            new Vector2(10, -5), new Vector2(8.5f, -5), new Vector2(7, -5), new Vector2(5.5f, -5),
            new Vector2(4, -5), new Vector2(2.5f, -5), new Vector2(1, -5), new Vector2(-0.5f, -5),
            new Vector2(-2.3f, -4.2f), new Vector2(-3.5f, -3.0f), new Vector2(-3.9f, -1.5f),
            new Vector2(-3.8f, -0.3f), new Vector2(-3.5f, 0.8f), new Vector2(-2.8f, 1.8f),
            new Vector2(-1.8f, 2.5f), new Vector2(-0.5f, 3.1f),
            new Vector2(1.0f, 3.4f), new Vector2(2.5f, 3.2f), new Vector2(3.8f, 2.6f),
            new Vector2(5.0f, 2.5f), new Vector2(6.2f, 2.8f), new Vector2(7.1f, 3.6f),
            new Vector2(7.7f, 4.8f), new Vector2(8.5f, 6.0f),
            new Vector2(9.5f, 6.8f), new Vector2(10.5f, 7.3f), new Vector2(11.8f, 7.1f),
            new Vector2(12.5f, 6.2f), new Vector2(13.1f, 5.0f), new Vector2(13.3f, 3.5f),
            new Vector2(13.3f, 2.2f), new Vector2(13.3f, 0.8f), new Vector2(13.3f, -0.8f), new Vector2(13.5f, -2.2f),
            new Vector2(14.5f, -3.3f), new Vector2(16.0f, -3.3f),
            new Vector2(17.5f, -2.8f), new Vector2(18.8f, -2.4f), new Vector2(19.8f, -3.3f),
            new Vector2(22.0f, -3.3f), new Vector2(25.0f, -3.3f), new Vector2(28.0f, -3.8f),
        };
    }

    void OnDestroy()
    {
        foreach (LineRenderer laneRenderer in laneLineRenderers)
        {
            if (laneRenderer != null)
                Destroy(laneRenderer.gameObject);
        }
        foreach (LineRenderer mask in cornerMaskRenderers)
        {
            if (mask != null)
                Destroy(mask.gameObject);
        }
        foreach (GameObject apex in apexMaskObjects)
        {
            if (apex != null)
                Destroy(apex);
        }
        foreach (GameObject label in speedLimitLabelObjects)
        {
            if (label != null)
                Destroy(label);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (Application.isPlaying || config == null)
            return;

        string trackId = TrackSelectionState.ResolveTrackId(config.trackId);
        TrackConfig editorConfig = string.IsNullOrEmpty(trackId)
            ? null
            : TrackDataLoader.LoadConfig(trackId);
        Vector2[] editorPath = editorConfig != null
            ? TrackDataLoader.ConfigToWorldPositions(
                editorConfig,
                config.trackWorldSize,
                config.trackWorldHeight > 0f ? config.trackWorldHeight : config.trackWorldSize)
            : GetTrackShape42();
        if (editorPath == null || editorPath.Length == 0)
            return;

        for (int i = 0; i < editorPath.Length; i++)
        {
            CellData cell = editorConfig != null && editorConfig.cells != null && i < editorConfig.cells.Length
                ? editorConfig.cells[i]
                : null;
            Color color = cell != null && cell.IsStartFinish
                ? startFinishNodeColor
                : cell != null && cell.IsCorner && cell.isApex
                    ? apexNodeColor
                    : cell != null && cell.IsCorner
                        ? cornerNodeColor
                        : straightNodeColor;
            Gizmos.color = color;
            Gizmos.DrawSphere(editorPath[i], 0.11f);
            Handles.color = color;
            string label = cell == null
                ? $"{i}"
                : cell.IsCorner
                    ? $"{i} {cell.cornerId}{(cell.isApex ? " apex" : "")} limit {cell.cornerLimit}"
                    : $"{i} {cell.type}";
            Handles.Label(editorPath[i] + Vector2.up * 0.16f, label);
        }
    }
#endif

}
