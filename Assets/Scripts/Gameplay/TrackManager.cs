using System.Collections.Generic;
using UnityEngine;
using TMPro;

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
    public Color cornerNodeColor = Color.yellow;

    // --- 运行时数据 ---
    private List<TrackNode> nodes = new List<TrackNode>();
    private List<GameObject> nodeObjects = new List<GameObject>();
    private LineRenderer lineRenderer;

    /// <summary>弯道 ID → 限速的快速查找表。</summary>
    private Dictionary<int, int> cornerSpeedLimits = new Dictionary<int, int>();

    /// <summary>弯道 ID → 名称。</summary>
    private Dictionary<int, string> cornerNames = new Dictionary<int, string>();

    /// <summary>当前加载的赛道配置（JSON 模式非 null）。</summary>
    public TrackConfig LoadedTrackConfig { get; private set; }

    // --- 公开属性 ---
    public int TotalNodes => nodes.Count;
    public IReadOnlyList<TrackNode> Nodes => nodes;

    void Awake()
    {
        if (config == null)
        {
            Debug.LogWarning("[TrackManager] GameConfigSO reference is missing — using defaults.");
            BuildHardcodedTrack();
        }
        else if (!string.IsNullOrEmpty(config.trackId))
        {
            if (!LoadTrackFromJson(config.trackId))
            {
                Debug.LogWarning($"[TrackManager] Failed to load '{config.trackId}', falling back to hardcoded track.");
                BuildHardcodedTrack();
            }
        }
        else
        {
            BuildHardcodedTrack();
        }

        RenderTrack();
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
    }

    // ===================================================================
    // PUBLIC API
    // ===================================================================

    public HashSet<int> GetUniqueCornersCrossed(int fromPos, int toPos)
    {
        HashSet<int> corners = new HashSet<int>();
        int totalNodes = nodes.Count;

        for (int i = fromPos + 1; i <= toPos; i++)
        {
            int idx = i % totalNodes;
            if (nodes[idx].cornerId > 0)
            {
                corners.Add(nodes[idx].cornerId);
            }
        }
        return corners;
    }

    public bool CrossesStartFinish(int fromPos, int toPos, out int startFinishIndex)
    {
        startFinishIndex = -1;
        int totalNodes = nodes.Count;

        for (int i = fromPos + 1; i <= toPos; i++)
        {
            int idx = i % totalNodes;
            if (nodes[idx].isStartFinish)
            {
                startFinishIndex = idx;
                return true;
            }
        }
        return false;
    }

    public int GetApexNodeIndex(int cornerId)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].cornerId == cornerId)
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

    public string GetCornerName(int cornerId)
    {
        if (cornerNames.TryGetValue(cornerId, out string name))
            return name;
        return "Unknown";
    }

    public Vector3 GetNodePosition(int index)
    {
        int clamped = index % nodes.Count;
        if (clamped < nodeObjects.Count)
            return nodeObjects[clamped].transform.position;
        return Vector3.zero;
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

        // Spawn node GameObjects
        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 spawnPos = new Vector3(pathCoords[i].x, pathCoords[i].y, 0);
            GameObject obj = nodePrefab != null
                ? Instantiate(nodePrefab, spawnPos, Quaternion.identity)
                : new GameObject($"Node_{i}") { transform = { position = spawnPos } };
            obj.name = $"Node_{i}";

            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                if (nodes[i].isStartFinish)
                    sr.color = Color.green;
                else if (nodes[i].cornerId > 0)
                    sr.color = cornerNodeColor;
            }

            nodeObjects.Add(obj);
        }

        // LineRenderer
        GameObject lineObj = new GameObject("TrackLine");
        lineRenderer = lineObj.AddComponent<LineRenderer>();
        lineRenderer.positionCount = pathCoords.Length + 1;
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth;
        lineRenderer.useWorldSpace = true;
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startColor = lineColor;
        lineRenderer.endColor = lineColor;
        lineRenderer.sortingOrder = -1;

        for (int i = 0; i < pathCoords.Length; i++)
        {
            lineRenderer.SetPosition(i, new Vector3(pathCoords[i].x, pathCoords[i].y, 0));
        }
        lineRenderer.SetPosition(pathCoords.Length, new Vector3(pathCoords[0].x, pathCoords[0].y, 0));

        AddCornerLabels(pathCoords);
    }

    /// <summary>
    /// 获取赛道坐标。JSON 模式下从配置中读取并缩放；硬编码模式使用旧坐标。
    /// </summary>
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

    private void AddCornerLabels(Vector2[] pathCoords)
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
            bg.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
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
        if (lineRenderer != null)
            Destroy(lineRenderer.gameObject);
    }
}
