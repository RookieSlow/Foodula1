using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// System.Serializable 标签让这个类可以在 Unity 的 Inspector 面板中显示和编辑
[System.Serializable]
public class TrackNode
{
    public int nodeIndex;       // 节点编号 (0 - 65)
    public int speedLimit;      // 该节点的限速。如果没有限速，我们可以设为一个很大的值，比如 99
    public string nodeName;     // 节点名称（用于在控制台打印调试信息）

    // 构造函数，用于快速创建节点
    public TrackNode(int index, int limit, string name = "普通直道")
    {
        nodeIndex = index;
        speedLimit = limit;
        nodeName = name;
    }
}