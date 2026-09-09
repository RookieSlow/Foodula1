using System;
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
    [Tooltip("Lv1 高速弯：安全绿。")]
    public Color cornerLevelOneColor = new Color(0.247f, 0.725f, 0.314f, 0.38f);
    [Tooltip("Lv2 中速弯：琥珀黄。")]
    public Color cornerLevelTwoColor = new Color(0.824f, 0.600f, 0.133f, 0.46f);
    [Tooltip("Lv3 低速弯：警示珊瑚红。")]
    public Color cornerLevelThreeColor = new Color(0.969f, 0.506f, 0.400f, 0.54f);
    [Tooltip("等级色弯道带下方的深色外沿。")]
    public Color cornerEdgeColor = new Color(0.025f, 0.04f, 0.06f, 0.72f);
    [Tooltip("游玩时覆盖弯心的红色半透明蒙版。")]
    public Color apexMaskColor = new Color(1f, 0.12f, 0.08f, 0.72f);
    [Min(0.01f)]
    public float cornerMaskWidth = 1.05f;
    [Min(0.01f)]
    public float apexMaskDiameter = 0.72f;
    [Range(1, 8)]
    [Tooltip("每两个赛道格之间仅供渲染使用的曲线采样数；不会改变游戏节点。")]
    public int cornerCurveSubdivisions = 5;
    [Tooltip("调试模式：游玩时按 F8 显示/隐藏带编号的节点覆盖层。")]
    public bool showDebugTrackNodesInPlay;

    // --- 运行时数据 ---
    private List<TrackNode> nodes = new List<TrackNode>();
    private List<GameObject> nodeObjects = new List<GameObject>();
    private List<LineRenderer> laneLineRenderers = new List<LineRenderer>();
    private List<LineRenderer> cornerEdgeRenderers = new List<LineRenderer>();
    private List<LineRenderer> cornerMaskRenderers = new List<LineRenderer>();
    private List<GameObject> apexMaskObjects = new List<GameObject>();
    private List<GameObject> speedLimitLabelObjects = new List<GameObject>();
    private List<CornerSpeedLimitLabel> speedLimitLabels = new List<CornerSpeedLimitLabel>();
    private Vector2[] worldPathCoordinates = new Vector2[0];
    private float[] laneOffsets = new float[0];
    private TrackReadabilityOverlay readabilityOverlay;
    private float medianNodeSpacing;
    private float roadVisualWidth;

    /// <summary>弯道 ID → 限速的快速查找表。</summary>
    private Dictionary<int, int> cornerSpeedLimits = new Dictionary<int, int>();

    /// <summary>弯道 ID → 名称。</summary>
    private Dictionary<int, string> cornerNames = new Dictionary<int, string>();
    private Func<int, int, CornerLimitBreakdown> cornerLimitBreakdownProvider;
    private Action<int, int> cornerLimitClickHandler;

    /// <summary>当前加载的赛道配置（JSON 模式非 null）。</summary>
    public TrackConfig LoadedTrackConfig { get; private set; }

    /// <summary>统一硬编码赛道为 JSON 后的回退赛道 ID（roadmap P1 #9）。</summary>
    public const string FallbackTrackId = "fallback_42";
    private const float FallbackWorldWidth = 31.9f;   // 原始 42 节点形状包围盒
    private const float FallbackWorldHeight = 12.3f;

    // --- 公开属性 ---
    public int TotalNodes => nodes.Count;
    public int LaneCount => laneOffsets.Length > 0 ? laneOffsets.Length : 1;
    public float MedianNodeSpacing => medianNodeSpacing;
    public float RoadVisualWidth => roadVisualWidth > 0f ? roadVisualWidth : Mathf.Max(0.01f, cornerMaskWidth);
    public Sprite NodeVisualSprite => nodePrefab != null
        ? nodePrefab.GetComponent<SpriteRenderer>()?.sprite
        : null;
    public IReadOnlyList<TrackNode> Nodes => nodes;
    public int StartFinishNodeIndex => TrackRules.FindStartFinishNodeIndex(nodes);
    public string TrackId => LoadedTrackConfig != null
        ? LoadedTrackConfig.trackId
        : (config != null ? RaceModeLaunchResolver.ResolveTrackId(config.trackId) : FallbackTrackId);
    public bool AllowsStartFinishLaneChange
    {
        get
        {
            string trackId = LoadedTrackConfig != null
                ? LoadedTrackConfig.trackId
                : (config != null ? config.trackId : string.Empty);
            return LoadedTrackConfig != null
                ? LoadedTrackConfig.allowStartFinishLaneChange
                : TrackPresentationRules.AllowsStartFinishLaneChange(trackId);
        }
    }

    void Awake()
    {
        string configuredTrackId = config != null ? config.trackId : string.Empty;
        string trackId = RaceModeLaunchResolver.ResolveTrackId(configuredTrackId);

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

        readabilityOverlay = GetComponent<TrackReadabilityOverlay>();
        if (readabilityOverlay == null)
            readabilityOverlay = gameObject.AddComponent<TrackReadabilityOverlay>();
        readabilityOverlay.Configure(this, FindObjectOfType<TMP_Text>()?.font);

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
        nodes = TrackDataLoader.ConfigToNodes(cfg);

        // 覆盖配置中的圈数
        if (config != null)
        {
            config.totalLaps = cfg.laps;
            config.trackNodeCount = cfg.gameCellCount;

            // fallback 赛道使用自身包围盒尺寸，保持与旧硬编码渲染一致
            if (trackId == FallbackTrackId)
            {
                config.trackWorldSize = FallbackWorldWidth;
                config.trackWorldHeight = FallbackWorldHeight;
            }
        }

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
        return TrackRules.GetUniqueApexCornersCrossed(nodes, fromPos, toPos);
    }

    public bool CrossesStartFinish(int fromPos, int toPos, out int startFinishIndex)
    {
        return TrackRules.CrossesStartFinish(nodes, fromPos, toPos, out startFinishIndex);
    }

    public int GetApexNodeIndex(int cornerId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].cornerId == cornerId && nodes[i].isApex)
                return i;
        }
        return -1;
    }

    public int GetCornerSpeedLimit(int cornerId)
    {
        if (cornerSpeedLimits.TryGetValue(cornerId, out int limit))
            return limit;
        return 99;
    }

    public int GetCornerSpeedLimit(int cornerId, int laneIndex)
    {
        int baseLimit = GetCornerSpeedLimit(cornerId);
        if (baseLimit >= 99)
            return baseLimit;

        if (LoadedTrackConfig != null &&
            LoadedTrackConfig.laneCornerSpeedLimits != null &&
            LoadedTrackConfig.laneCornerSpeedLimits.Length >= LaneCount)
        {
            int innerToOuterIndex = TrackPresentationRules.GetLaneRankFromInside(
                LoadedTrackConfig.trackId,
                laneIndex);
            return LoadedTrackConfig.laneCornerSpeedLimits[innerToOuterIndex];
        }

        string trackId = LoadedTrackConfig != null
            ? LoadedTrackConfig.trackId
            : (config != null ? config.trackId : string.Empty);
        return TrackPresentationRules.GetLaneAdjustedCornerSpeedLimit(
            trackId,
            baseLimit,
            laneIndex);
    }

    public string GetCornerName(int cornerId)
    {
        if (cornerNames.TryGetValue(cornerId, out string name))
            return name;
        return "Unknown";
    }

    /// <summary>
    /// Connects the live race formula and click handler to the world-space
    /// speed-limit labels. The provider is optional so editor/fallback tracks
    /// still render their authored base values.
    /// </summary>
    public void ConfigureCornerLimitPresentation(
        Func<int, int, CornerLimitBreakdown> breakdownProvider,
        Action<int, int> clickHandler)
    {
        cornerLimitBreakdownProvider = breakdownProvider;
        cornerLimitClickHandler = clickHandler;
        RefreshCornerLimitLabels();
    }

    /// <summary>Refreshes all visible corner numbers from the current race state.</summary>
    public void RefreshCornerLimitLabels()
    {
        for (int i = 0; i < speedLimitLabels.Count; i++)
        {
            CornerSpeedLimitLabel label = speedLimitLabels[i];
            if (label == null)
                continue;

            int displayedLimit = label.BaseLimit;
            if (cornerLimitBreakdownProvider != null)
            {
                CornerLimitBreakdown breakdown = cornerLimitBreakdownProvider(
                    label.CornerId,
                    label.LaneIndex);
                displayedLimit = breakdown.EffectiveLimit;
            }
            label.SetDisplayedLimit(displayedLimit);
        }
    }

    /// <summary>Called by a clicked world-space limit number.</summary>
    public void HandleCornerLimitLabelClicked(int cornerId, int laneIndex)
    {
        cornerLimitClickHandler?.Invoke(cornerId, laneIndex);
    }

    public Vector3 GetNodePosition(int index)
    {
        if (nodes.Count == 0)
            return Vector3.zero;

        int clamped = index % nodes.Count;
        if (clamped < 0) clamped += nodes.Count;
        if (clamped < worldPathCoordinates.Length)
        {
            Vector2 centerline = worldPathCoordinates[clamped];
            return new Vector3(centerline.x, centerline.y, 0f);
        }
        if (clamped < nodeObjects.Count)
            return nodeObjects[clamped].transform.position;
        return Vector3.zero;
    }

    public Vector3 GetNodePosition(int index, int laneIndex)
    {
        if (nodes.Count == 0 || worldPathCoordinates.Length == 0)
            return Vector3.zero;

        int clamped = index % nodes.Count;
        if (clamped < 0) clamped += nodes.Count;
        int safeLane = Mathf.Clamp(laneIndex, 0, LaneCount - 1);
        Vector2 position = worldPathCoordinates[clamped] + GetPathNormal(clamped) * laneOffsets[safeLane];
        return new Vector3(position.x, position.y, 0f);
    }

    public Vector2 GetNodeNormal(int index)
    {
        if (worldPathCoordinates == null || worldPathCoordinates.Length < 2)
            return Vector2.up;
        return GetPathNormal(TrackPresentationRules.WrapNodeIndex(index, worldPathCoordinates.Length));
    }

    public int GetCornerLevelAtNode(int index)
    {
        if (nodes.Count == 0)
            return 0;

        int safeIndex = TrackPresentationRules.WrapNodeIndex(index, nodes.Count);
        if (LoadedTrackConfig != null && LoadedTrackConfig.cells != null)
        {
            if (safeIndex < LoadedTrackConfig.cells.Length && LoadedTrackConfig.cells[safeIndex] != null
                && LoadedTrackConfig.cells[safeIndex].index == safeIndex)
            {
                return Mathf.Clamp(LoadedTrackConfig.cells[safeIndex].cornerLevel, 0, 3);
            }

            for (int i = 0; i < LoadedTrackConfig.cells.Length; i++)
            {
                CellData cell = LoadedTrackConfig.cells[i];
                if (cell != null && cell.index == safeIndex)
                    return Mathf.Clamp(cell.cornerLevel, 0, 3);
            }
        }

        TrackNode node = nodes[safeIndex];
        return node.cornerId > 0
            ? TrackPresentationRules.InferCornerLevel(node.speedLimit)
            : 0;
    }

    public Color GetCornerVisualColorAtNode(int index)
    {
        int level = GetCornerLevelAtNode(index);
        return TrackPresentationRules.GetCornerLevelColor(
            level,
            cornerLevelOneColor,
            cornerLevelTwoColor,
            cornerLevelThreeColor);
    }

    public Color GetNodeReadabilityColor(int index)
    {
        TrackNode node = GetNode(TrackPresentationRules.WrapNodeIndex(index, nodes.Count));
        if (node.isStartFinish)
            return new Color(0.247f, 0.725f, 0.314f, 0.94f);
        if (node.cornerId > 0)
        {
            Color cornerColor = GetCornerVisualColorAtNode(index);
            cornerColor.a = 0.92f;
            return cornerColor;
        }
        return new Color(0.90f, 0.93f, 0.96f, 0.66f);
    }

    public void BindPlayerReadability(Transform playerCar)
    {
        if (readabilityOverlay == null)
        {
            readabilityOverlay = GetComponent<TrackReadabilityOverlay>();
            if (readabilityOverlay == null)
                readabilityOverlay = gameObject.AddComponent<TrackReadabilityOverlay>();
            readabilityOverlay.Configure(this, FindObjectOfType<TMP_Text>()?.font);
        }
        readabilityOverlay.BindPlayer(playerCar);
    }

    public int GetDefaultLaneIndex(bool isAi)
    {
        if (LaneCount <= 1) return 0;
        if (!TrackPresentationRules.IsIndianapolis(TrackId))
            return TrackPresentationRules.GetInnerLaneIndex(TrackId);

        int leftMiddle = (LaneCount - 1) / 2;
        return isAi ? Mathf.Min(LaneCount - 1, leftMiddle + 1) : leftMiddle;
    }

    public int GetLaneTowardsInside(int laneIndex)
    {
        if (LaneCount <= 1) return 0;
        return Mathf.Max(0, laneIndex - 1);
    }

    public int GetLaneTowardsOutside(int laneIndex)
    {
        if (LaneCount <= 1) return 0;
        return Mathf.Min(LaneCount - 1, laneIndex + 1);
    }

    public TrackNode GetNode(int index)
    {
        return nodes[index % nodes.Count];
    }

    public bool IsInCorner(int position)
    {
        return nodes[position % nodes.Count].cornerId > 0;
    }

    // ===================================================================
    // RENDERING
    // ===================================================================

    private void RenderTrack()
    {
        Vector2[] pathCoords = GetPathCoordinates();
        worldPathCoordinates = pathCoords;
        string trackId = LoadedTrackConfig != null
            ? LoadedTrackConfig.trackId
            : (config != null ? config.trackId : string.Empty);
        laneOffsets = TrackPresentationRules.CalculateCenteredLaneOffsets(
            TrackPresentationRules.GetLaneCount(trackId),
            config != null ? config.trackLaneSpacing : 0.28f);
        roadVisualWidth = GetEffectiveCornerMaskWidth();
        float medianSpacing = TrackPresentationRules.CalculateMedianNeighborDistance(pathCoords);
        medianNodeSpacing = medianSpacing;
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
        if (LoadedTrackConfig != null)
        {
            float worldWidth = config != null ? config.trackWorldSize : 30f;
            float worldHeight = config != null ? config.trackWorldHeight : worldWidth;
            return TrackDataLoader.ConfigToWorldPositions(LoadedTrackConfig, worldWidth, worldHeight);
        }
        return GetTrackShape42();
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
        cornerEdgeRenderers.Clear();
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

            float effectiveMaskWidth = roadVisualWidth;
            Color cornerColor = GetCornerVisualColorAtNode(indices[0]);
            Vector2[] smoothPath = TrackPresentationRules.BuildSmoothCornerPath(
                pathCoords,
                indices,
                cornerCurveSubdivisions);

            LineRenderer edge = CreateCornerRibbon(
                $"CornerEdge_{pair.Key}",
                smoothPath,
                effectiveMaskWidth + 0.18f,
                cornerEdgeColor,
                -3);
            cornerEdgeRenderers.Add(edge);

            LineRenderer mask = CreateCornerRibbon(
                $"CornerMask_{pair.Key}",
                smoothPath,
                effectiveMaskWidth,
                cornerColor,
                -2);
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
                CreateApexMask(pathCoords[apexIndex], pair.Key, cornerColor);
                CreateSpeedLimitLabels(pathCoords, apexIndex, pair.Key);
            }
        }
    }

    private LineRenderer CreateCornerRibbon(
        string objectName,
        IReadOnlyList<Vector2> positions,
        float width,
        Color color,
        int sortingOrder)
    {
        GameObject ribbonObject = new GameObject(objectName);
        LineRenderer ribbon = ribbonObject.AddComponent<LineRenderer>();
        ribbon.useWorldSpace = true;
        ribbon.material = new Material(Shader.Find("Sprites/Default"));
        ribbon.startColor = color;
        ribbon.endColor = color;
        ribbon.startWidth = width;
        ribbon.endWidth = width;
        ribbon.numCapVertices = 10;
        ribbon.numCornerVertices = 10;
        ribbon.sortingOrder = sortingOrder;
        ribbon.positionCount = positions != null ? positions.Count : 0;
        for (int i = 0; positions != null && i < positions.Count; i++)
            ribbon.SetPosition(i, new Vector3(positions[i].x, positions[i].y, 0f));
        return ribbon;
    }

    private float GetEffectiveCornerMaskWidth()
    {
        float configuredWidth = Mathf.Max(0.01f, cornerMaskWidth);
        float laneSpan = LaneCount > 1
            ? (LaneCount - 1) * (config != null ? config.trackLaneSpacing : 0.28f)
            : 0f;
        return Mathf.Max(configuredWidth, laneSpan + 0.45f);
    }

    private void CreateApexMask(Vector2 position, int cornerId, Color cornerColor)
    {
        GameObject apex = new GameObject($"ApexMask_{cornerId}");
        apex.transform.position = new Vector3(position.x, position.y, 0f);

        GameObject borderObject = new GameObject("Border");
        borderObject.transform.SetParent(apex.transform, false);
        SpriteRenderer border = borderObject.AddComponent<SpriteRenderer>();
        border.sprite = nodePrefab != null
            ? nodePrefab.GetComponent<SpriteRenderer>()?.sprite
            : null;
        border.color = cornerEdgeColor;
        border.sortingOrder = -1;
        borderObject.transform.localScale = Vector3.one * 1.24f;

        SpriteRenderer renderer = apex.AddComponent<SpriteRenderer>();
        renderer.sprite = nodePrefab != null
            ? nodePrefab.GetComponent<SpriteRenderer>()?.sprite
            : null;
        cornerColor.a = Mathf.Max(cornerColor.a, apexMaskColor.a);
        renderer.color = cornerColor;
        renderer.sortingOrder = 0;
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

        CornerSpeedLimitLabel clickTarget = labelObject.AddComponent<CornerSpeedLimitLabel>();
        clickTarget.Initialize(this, label, cornerId, laneIndex, speedLimit);
        BoxCollider2D collider = labelObject.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.5f, 1.35f);

        speedLimitLabelObjects.Add(labelObject);
        speedLimitLabels.Add(clickTarget);
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
        foreach (LineRenderer edge in cornerEdgeRenderers)
        {
            if (edge != null)
                Destroy(edge.gameObject);
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
        speedLimitLabels.Clear();
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

    private Vector2 GetPathNormal(int index)
    {
        int previous = (index - 1 + worldPathCoordinates.Length) % worldPathCoordinates.Length;
        int next = (index + 1) % worldPathCoordinates.Length;
        Vector2 tangent = (worldPathCoordinates[next] - worldPathCoordinates[previous]).normalized;
        return new Vector2(-tangent.y, tangent.x);
    }
}
