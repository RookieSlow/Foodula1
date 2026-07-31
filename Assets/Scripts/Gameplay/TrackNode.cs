using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 赛道节点数据。含有弯道分组信息，支持 per-corner-segment 判定。
/// </summary>
[System.Serializable]
public class TrackNode
{
    public int nodeIndex;
    public int speedLimit;
    public string nodeName;

    /// <summary>
    /// 弯道分段 ID。同一弯道占据多个连续节点时共享同一个 cornerId。
    /// 0 = 直道（不限速），>0 = 弯道段编号。
    /// </summary>
    public int cornerId;

    /// <summary>
    /// 是否为起点/终点线所在节点。
    /// </summary>
    public bool isStartFinish;

    /// <summary>
    /// Whether this node is an apex that triggers corner-speed resolution.
    /// </summary>
    public bool isApex;

    /// <summary>Whether this node is a pit lane entry point.</summary>
    public bool isPitEntry;

    /// <summary>Whether this node is a pit lane exit point.</summary>
    public bool isPitExit;

    public TrackNode(
        int index,
        int limit,
        string name = "Straight",
        int cornerId = 0,
        bool isStartFinish = false,
        bool isApex = false,
        bool isPitEntry = false,
        bool isPitExit = false)
    {
        nodeIndex = index;
        speedLimit = limit;
        nodeName = name;
        this.cornerId = cornerId;
        this.isStartFinish = isStartFinish;
        this.isApex = isApex;
        this.isPitEntry = isPitEntry;
        this.isPitExit = isPitExit;
    }
}