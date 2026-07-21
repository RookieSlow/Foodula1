using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 赛道管理器 — 生成/管理赛道节点、渲染 LineRenderer、提供弯道段查询。
/// </summary>
public class TrackManager : MonoBehaviour
{
    [Header("Prefab")]
    public GameObject nodePrefab;

    [Header("赛道渲染")]
    public float lineWidth = 0.5f;
    public Color lineColor = Color.white;
    public Color cornerNodeColor = Color.yellow;

    private List<TrackNode> nodes = new List<TrackNode>();
    private List<GameObject> nodeObjects = new List<GameObject>();
    private LineRenderer lineRenderer;

    /// <summary>弯道 ID → 限速 的快速查找表。</summary>
    private Dictionary<int, int> cornerSpeedLimits = new Dictionary<int, int>();

    /// <summary>弯道 ID → 名称。</summary>
    private Dictionary<int, string> cornerNames = new Dictionary<int, string>();

    // --- 公开属性 ---
    public int TotalNodes => nodes.Count;
    public IReadOnlyList<TrackNode> Nodes => nodes;

    void Awake()
    {
        BuildTrack();
        RenderTrack();
    }

    /// <summary>
    /// 构建 42 节点简化赛道，5 个命名弯道段。
    /// 弯道段用连续节点共享同一个 cornerId，确保 per-corner-segment 判定正确。
    /// </summary>
    private void BuildTrack()
    {
        nodes.Clear();

        // === 赛道定义: 42 节点，使用旧版 GetTrackShape() 的子采样版本 ===
        // 简化说明: 保留原 85 节点赛道的形状骨架，每隔约 2 个点取 1 个，
        // 并在弯道位置保留足够的节点密度。

        Vector2[] rawShape = GetTrackShape42();

        for (int i = 0; i < rawShape.Length; i++)
        {
            nodes.Add(new TrackNode(i, 99, $"Straight {i}"));
        }

        // === 弯道段定义: (起始节点, 结束节点, cornerId, 限速, 名称) ===
        // cornerId 相同的连续节点属于同一弯道段，每回合只判定一次。
        DefineCorner(8,  10,  1, 3, "T1 Parabolica");
        DefineCorner(16, 18,  2, 2, "T2 Grand Hotel");
        DefineCorner(24, 26,  3, 4, "T3 Copse");
        DefineCorner(30, 33,  4, 2, "T4 Chicane");
        DefineCorner(36, 38,  5, 3, "T5 Lesmo");

        // 起点/终点线
        nodes[0].isStartFinish = true;
        nodes[0].nodeName = "Start/Finish";

        // 构建弯道查找字典
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

    /// <summary>
    /// 将指定范围的节点标记为同一弯道段。
    /// </summary>
    private void DefineCorner(int fromIndex, int toIndex, int cornerId, int speedLimit, string name)
    {
        for (int i = fromIndex; i <= toIndex; i++)
        {
            nodes[i].cornerId = cornerId;
            nodes[i].speedLimit = speedLimit;
            nodes[i].nodeName = name;
        }
    }

    /// <summary>
    /// 获取从 fromPos 移动到 toPos 路径上经过的唯一弯道 ID 集合。
    /// 这是 per-corner-segment 判定的核心 — 同一 cornerId 只出现一次。
    /// </summary>
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

    /// <summary>
    /// 检查从 fromPos 到 toPos 的路径是否穿过起点/终点线（正向）。
    /// 用于圈数计数。
    /// </summary>
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

    /// <summary>
    /// 获取弯道限速。cornerId=0 或不存在时返回 99（无限制）。
    /// </summary>
    public int GetCornerSpeedLimit(int cornerId)
    {
        if (cornerSpeedLimits.TryGetValue(cornerId, out int limit))
            return limit;
        return 99;
    }

    /// <summary>
    /// 获取弯道名称。
    /// </summary>
    public string GetCornerName(int cornerId)
    {
        if (cornerNames.TryGetValue(cornerId, out string name))
            return name;
        return "Unknown";
    }

    /// <summary>
    /// 获取节点的世界坐标。
    /// </summary>
    public Vector3 GetNodePosition(int index)
    {
        int clamped = index % nodes.Count;
        if (clamped < nodeObjects.Count)
            return nodeObjects[clamped].transform.position;
        return Vector3.zero;
    }

    /// <summary>
    /// 获取指定位置节点（处理环绕）。
    /// </summary>
    public TrackNode GetNode(int index)
    {
        return nodes[index % nodes.Count];
    }

    /// <summary>
    /// 检查位置是否在弯道段内。
    /// </summary>
    public bool IsInCorner(int position)
    {
        return nodes[position % nodes.Count].cornerId > 0;
    }

    // ====== 赛道渲染 ======

    private void RenderTrack()
    {
        Vector2[] pathCoords = GetTrackShape42();

        // 生成节点 GameObject
        for (int i = 0; i < nodes.Count; i++)
        {
            Vector3 spawnPos = new Vector3(pathCoords[i].x, pathCoords[i].y, 0);
            GameObject obj = nodePrefab != null
                ? Instantiate(nodePrefab, spawnPos, Quaternion.identity)
                : new GameObject($"Node_{i}") { transform = { position = spawnPos } };
            obj.name = $"Node_{i}";

            // 弯道节点标黄色
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null && nodes[i].cornerId > 0)
            {
                sr.color = cornerNodeColor;
            }

            // 起点/终点线标绿色
            if (nodes[i].isStartFinish && sr != null)
            {
                sr.color = Color.green;
            }

            nodeObjects.Add(obj);
        }

        // LineRenderer 画赛道线
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

        // 标注弯道限速
        AddCornerLabels(pathCoords);
    }

    /// <summary>
    /// 在每个弯道段中间节点的外侧放置限速标签。
    /// </summary>
    private void AddCornerLabels(Vector2[] pathCoords)
    {
        // 收集每个弯道的节点范围
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

        // 获取字体
        TMP_Text existingTmp = FindObjectOfType<TMP_Text>();
        TMP_FontAsset font = existingTmp != null ? existingTmp.font : null;

        foreach (var kv in cornerRanges)
        {
            int midIdx = (kv.Value.first + kv.Value.last) / 2;
            Vector3 centerPos = new Vector3(pathCoords[midIdx].x, pathCoords[midIdx].y, 0);

            // 计算标签偏移（弯道外侧）
            Vector3 offset = GetCornerLabelOffset(midIdx, pathCoords);

            // 创建标签
            GameObject label = new GameObject($"CornerLabel_{kv.Key}");
            label.transform.position = centerPos + offset;

            // 背景圆点
            SpriteRenderer bg = label.AddComponent<SpriteRenderer>();
            bg.sprite = nodePrefab != null ? nodePrefab.GetComponent<SpriteRenderer>()?.sprite : null;
            bg.color = new Color(1f, 0.3f, 0.3f, 0.9f);
            bg.transform.localScale = new Vector3(0.8f, 0.8f, 1f);
            bg.sortingOrder = 5;

            // 限速数字
            GameObject textGO = new GameObject("Text");
            textGO.transform.SetParent(label.transform, false);
            textGO.transform.localPosition = Vector3.zero;

            TMP_Text tmp = textGO.AddComponent<TMPro.TextMeshProUGUI>();
            tmp.text = kv.Value.limit.ToString();
            tmp.fontSize = 10;
            tmp.alignment = TMPro.TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (font != null) tmp.font = font;

            // TextMeshProUGUI 需要 Canvas，改用 TextMeshPro
            Destroy(tmp);
            TMPro.TextMeshPro tmpWorld = textGO.AddComponent<TMPro.TextMeshPro>();
            tmpWorld.text = kv.Value.limit.ToString();
            tmpWorld.fontSize = 6;
            tmpWorld.alignment = TMPro.TextAlignmentOptions.Center;
            tmpWorld.color = Color.white;
            tmpWorld.sortingOrder = 6;
            if (font != null) tmpWorld.font = font;
        }
    }

    /// <summary>
    /// 计算弯道标签的外侧偏移方向（粗略法线）。
    /// </summary>
    private Vector3 GetCornerLabelOffset(int nodeIdx, Vector2[] coords)
    {
        int prev = (nodeIdx - 1 + coords.Length) % coords.Length;
        int next = (nodeIdx + 1) % coords.Length;
        Vector2 dir = (coords[next] - coords[prev]).normalized;
        Vector2 normal = new Vector2(-dir.y, dir.x); // 顺时针旋转 90°
        return new Vector3(normal.x, normal.y, 0) * 2.5f;
    }

    // ====== 42 节点赛道坐标 ======

    private Vector2[] GetTrackShape42()
    {
        // 在原 85 节点赛道基础上采样/简化为 42 节点
        return new Vector2[]
        {
            // 起点直道 (0-7): 8 nodes
            new Vector2(10, -5), new Vector2(8.5f, -5), new Vector2(7, -5), new Vector2(5.5f, -5),
            new Vector2(4, -5), new Vector2(2.5f, -5), new Vector2(1, -5), new Vector2(-0.5f, -5),

            // T1 中速弯 (8-10): 3 nodes, limit=3
            new Vector2(-2.3f, -4.2f), new Vector2(-3.5f, -3.0f), new Vector2(-3.9f, -1.5f),

            // 短直道 (11-15): 5 nodes
            new Vector2(-3.8f, -0.3f), new Vector2(-3.5f, 0.8f), new Vector2(-2.8f, 1.8f),
            new Vector2(-1.8f, 2.5f), new Vector2(-0.5f, 3.1f),

            // T2 发卡弯 (16-18): 3 nodes, limit=2
            new Vector2(1.0f, 3.4f), new Vector2(2.5f, 3.2f), new Vector2(3.8f, 2.6f),

            // 短直道 (19-23): 5 nodes
            new Vector2(5.0f, 2.5f), new Vector2(6.2f, 2.8f), new Vector2(7.1f, 3.6f),
            new Vector2(7.7f, 4.8f), new Vector2(8.5f, 6.0f),

            // T3 高速弯 (24-26): 3 nodes, limit=4
            new Vector2(9.5f, 6.8f), new Vector2(10.5f, 7.3f), new Vector2(11.8f, 7.1f),

            // 短直道 (27-29): 3 nodes (top section of original)
            new Vector2(12.5f, 6.2f), new Vector2(13.1f, 5.0f), new Vector2(13.3f, 3.5f),

            // T4 减速弯 (30-33): 4 nodes, limit=2
            new Vector2(13.3f, 2.2f), new Vector2(13.3f, 0.8f), new Vector2(13.3f, -0.8f), new Vector2(13.5f, -2.2f),

            // 短直道 (34-35): 2 nodes (right vertical)
            new Vector2(14.5f, -3.3f), new Vector2(16.0f, -3.3f),

            // T5 中速弯 (36-38): 3 nodes, limit=3
            new Vector2(17.5f, -2.8f), new Vector2(18.8f, -2.4f), new Vector2(19.8f, -3.3f),

            // 终点直道 (39-41): 3 nodes → 回到起点
            new Vector2(22.0f, -3.3f), new Vector2(25.0f, -3.3f), new Vector2(28.0f, -3.8f),
        };
    }

    void OnDestroy()
    {
        if (lineRenderer != null)
            Destroy(lineRenderer.gameObject);
    }
}
