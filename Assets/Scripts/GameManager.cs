using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    [Header("赛道数据")]
    public List<TrackNode> trackNodes = new List<TrackNode>();

    [Header("视觉与预制体 (Prefabs)")]
    public GameObject nodePrefab;
    public GameObject carPrefab;
    private GameObject carInstance;

    [Header("UI 引用")]
    public TMP_Text statusText;

    [Header("卡牌系统")]
    public GameObject cardPrefab;      // 卡牌的预制体
    public Transform handContainer;    // 存放手牌的 UI 容器 (父物体)
    public TMP_Text readyStepsText;    // 显示“当前准备移动 X 格”的 UI 文字

    private List<CardUI> cardsInHand = new List<CardUI>(); // 当前手里的牌
    private int currentSelectedSteps = 0; // 当前选中的总步数

    [Header("赛车当前状态")]
    public int currentCarPosition = 51; // 对应新的赛道，起点在 51
    public int currentHeat = 6;

    [Header("动画设置")]
    public float moveSpeed = 8f;
    private bool isMoving = false;

    private List<GameObject> nodeObjects = new List<GameObject>();

    void Start()
    {
        InitializeTrack();
        SpawnCar();
        UpdateUI("游戏开始！请点击选中卡牌，然后出牌。");

        // 发初始手牌
        DealStartingHand();
    }

    private void InitializeTrack()
    {
        for (int i = 0; i <= 84; i++)
        {
            trackNodes.Add(new TrackNode(i, 99));
        }

        trackNodes[13].speedLimit = 2; trackNodes[13].nodeName = "T1 中速直角弯";
        trackNodes[48].speedLimit = 3; trackNodes[48].nodeName = "T6 缓直角弯";
        trackNodes[53].speedLimit = 1; trackNodes[53].nodeName = "Chicane (入)";
        trackNodes[55].speedLimit = 1; trackNodes[55].nodeName = "Chicane (出)";
        trackNodes[66].speedLimit = 1; trackNodes[66].nodeName = "T7 发卡弯";

        Vector2[] pathCoords = GetTrackShape();

        for (int i = 0; i <= 84; i++)
        {
            Vector3 spawnPos = new Vector3(pathCoords[i].x, pathCoords[i].y, 0);
            GameObject newNode = Instantiate(nodePrefab, spawnPos, Quaternion.identity);
            newNode.name = "Node_" + i;

            if (trackNodes[i].speedLimit < 99)
            {
                newNode.GetComponent<SpriteRenderer>().color = Color.yellow;
            }
            nodeObjects.Add(newNode);
        }

        GameObject lineObj = new GameObject("TrackLine");
        LineRenderer line = lineObj.AddComponent<LineRenderer>();
        line.positionCount = pathCoords.Length + 1;
        line.startWidth = 0.5f;
        line.endWidth = 0.5f;
        line.useWorldSpace = true;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.white;
        line.endColor = Color.white;
        line.sortingOrder = -1;

        for (int i = 0; i < pathCoords.Length; i++)
        {
            line.SetPosition(i, new Vector3(pathCoords[i].x, pathCoords[i].y, 0));
        }
        line.SetPosition(pathCoords.Length, new Vector3(pathCoords[0].x, pathCoords[0].y, 0));
    }

    private Vector2[] GetTrackShape()
    {
        return new Vector2[]
        {
            new Vector2(10,-5), new Vector2(9,-5), new Vector2(8,-5), new Vector2(7,-5),
            new Vector2(6,-5), new Vector2(5,-5), new Vector2(4,-5), new Vector2(3,-5),
            new Vector2(2,-5), new Vector2(1,-5), new Vector2(0,-5), new Vector2(-1,-5), new Vector2(-2,-5),
            new Vector2(-3, -4.5f),
            new Vector2(-3.5f, -3.5f), new Vector2(-3.8f, -2.5f), new Vector2(-3.9f, -1.5f), new Vector2(-3.8f, -0.5f),
            new Vector2(-3.5f, 0.5f), new Vector2(-3.0f, 1.4f), new Vector2(-2.3f, 2.2f), new Vector2(-1.4f, 2.8f),
            new Vector2(-0.5f, 3.2f), new Vector2(0.5f, 3.4f), new Vector2(1.5f, 3.4f), new Vector2(2.5f, 3.1f),
            new Vector2(3.4f, 2.7f), new Vector2(4.3f, 2.5f), new Vector2(5.3f, 2.6f), new Vector2(6.2f, 3.0f), new Vector2(6.9f, 3.7f),
            new Vector2(7.4f, 4.5f), new Vector2(7.8f, 5.4f), new Vector2(8.3f, 6.2f), new Vector2(9.0f, 6.8f),
            new Vector2(9.8f, 7.2f), new Vector2(10.7f, 7.3f), new Vector2(11.6f, 7.1f), new Vector2(12.4f, 6.5f),
            new Vector2(12.9f, 5.7f), new Vector2(13.2f, 4.8f),
            new Vector2(13.3f, 3.8f), new Vector2(13.3f, 2.8f), new Vector2(13.3f, 1.8f), new Vector2(13.3f, 0.8f),
            new Vector2(13.3f, -0.2f), new Vector2(13.3f, -1.2f), new Vector2(13.3f, -2.2f),
            new Vector2(13.6f, -3.0f),
            new Vector2(14.4f, -3.3f), new Vector2(15.3f, -3.3f), new Vector2(16.2f, -3.3f), new Vector2(17.1f, -3.3f),
            new Vector2(17.8f, -2.4f), new Vector2(18.6f, -2.4f), new Vector2(19.3f, -3.3f),
            new Vector2(20.2f, -3.3f), new Vector2(21.1f, -3.3f), new Vector2(22.0f, -3.3f), new Vector2(22.9f, -3.3f),
            new Vector2(23.8f, -3.3f), new Vector2(24.7f, -3.3f), new Vector2(25.6f, -3.3f), new Vector2(26.5f, -3.3f),
            new Vector2(27.4f, -3.3f), new Vector2(28.3f, -3.3f),
            new Vector2(29.2f, -3.8f),
            new Vector2(28.5f, -4.5f), new Vector2(27, -5), new Vector2(26, -5), new Vector2(25, -5),
            new Vector2(24, -5), new Vector2(23, -5), new Vector2(22, -5), new Vector2(21, -5),
            new Vector2(20, -5), new Vector2(19, -5), new Vector2(18, -5), new Vector2(17, -5),
            new Vector2(16, -5), new Vector2(15, -5), new Vector2(14, -5), new Vector2(13, -5),
            new Vector2(12, -5), new Vector2(11, -5)
        };
    }

    private void SpawnCar()
    {
        Vector3 startPos = nodeObjects[currentCarPosition].transform.position;
        carInstance = Instantiate(carPrefab, startPos, Quaternion.identity);
        carInstance.name = "PlayerCar";
        carInstance.GetComponent<SpriteRenderer>().color = Color.red;
        carInstance.transform.localScale = new Vector3(0.6f, 0.6f, 1f);
    }

    // ====== 发牌机制 ======
    private void DealStartingHand()
    {
        int[] startingCards = { 1, 2, 3, 4, 1 };

        foreach (int val in startingCards)
        {
            GameObject newCardObj = Instantiate(cardPrefab, handContainer);
            CardUI card = newCardObj.GetComponent<CardUI>();
            card.SetupCard(val, this);
            cardsInHand.Add(card);
        }
        CalculateSelectedSteps();
    }

    // ====== 重点！这个 public 方法必须存在且没有被嵌套 ======
    public void CalculateSelectedSteps()
    {
        currentSelectedSteps = 0;
        foreach (CardUI card in cardsInHand)
        {
            if (card.isSelected)
            {
                currentSelectedSteps += card.cardValue;
            }
        }

        if (readyStepsText != null)
        {
            readyStepsText.text = $"即将移动: {currentSelectedSteps} 格";
        }
    }

    // ====== 出牌按钮绑定的方法 ======
    public void PlayTurn()
    {
        if (isMoving) return;
        if (currentHeat < 0)
        {
            UpdateUI("<color=red>车辆已爆缸！比赛结束！</color>");
            return;
        }

        int moveSteps = currentSelectedSteps;

        if (moveSteps == 0)
        {
            UpdateUI("<color=orange>请先点击选中至少一张卡牌！</color>");
            return;
        }

        int targetPosition = Mathf.Min(currentCarPosition + moveSteps, 84);
        int totalPenalty = 0;
        string logMsg = $"打出卡牌，向前移动 {moveSteps} 格。\n";

        for (int i = currentCarPosition + 1; i <= targetPosition; i++)
        {
            TrackNode node = trackNodes[i];
            if (moveSteps > node.speedLimit)
            {
                int penalty = moveSteps - node.speedLimit;
                totalPenalty += penalty;
                logMsg += $"节点 {node.nodeIndex} ({node.nodeName}) 超速！惩罚 {penalty}。\n";
            }
        }

        // 销毁打出的卡牌，并从手牌列表移除
        for (int i = cardsInHand.Count - 1; i >= 0; i--)
        {
            if (cardsInHand[i].isSelected)
            {
                Destroy(cardsInHand[i].gameObject);
                cardsInHand.RemoveAt(i);
            }
        }

        CalculateSelectedSteps(); // 归零显示
        StartCoroutine(MoveCarRoutine(targetPosition, totalPenalty, logMsg));
    }

    private IEnumerator MoveCarRoutine(int targetPosition, int totalPenalty, string logMsg)
    {
        isMoving = true;
        UpdateUI("赛车移动中...");

        for (int i = currentCarPosition + 1; i <= targetPosition; i++)
        {
            Vector3 targetPos = nodeObjects[i].transform.position;

            while (Vector3.Distance(carInstance.transform.position, targetPos) > 0.01f)
            {
                carInstance.transform.position = Vector3.MoveTowards(carInstance.transform.position, targetPos, moveSpeed * Time.deltaTime);
                yield return null;
            }

            carInstance.transform.position = targetPos;
            yield return new WaitForSeconds(0.05f);
        }

        currentCarPosition = targetPosition;

        if (totalPenalty > 0)
        {
            currentHeat -= totalPenalty;
            if (currentHeat < 0)
            {
                logMsg += $"\n<color=red><b>爆缸发生！冰鲜库被榨干！</b></color>";
            }
            else
            {
                logMsg += $"\n承受总惩罚 {totalPenalty}。";
            }
        }
        else
        {
            logMsg += "\n安全通过。";
        }

        UpdateUI(logMsg);
        isMoving = false;
    }

    private void UpdateUI(string message)
    {
        if (statusText != null)
        {
            statusText.text = $"当前位置: {currentCarPosition}\n冰鲜库容量: {currentHeat}\n\n【实况播报】\n{message}";
        }
    }

    // ====== 重新开始游戏 ======
    public void ResetGame()
    {
        // 1. 如果赛车还在开，强制解锁
        isMoving = false;

        // 2. 恢复初始数值（以美式汉堡车为例）
        currentCarPosition = 51; // 回到新的发车点
        currentHeat = 6;         // 冰鲜库加满

        // 3. 物理位置重置：瞬间把赛车传回发车点
        if (carInstance != null && nodeObjects.Count > currentCarPosition)
        {
            carInstance.transform.position = nodeObjects[currentCarPosition].transform.position;
        }

        // 4. 清空手里没打出去的牌
        for (int i = cardsInHand.Count - 1; i >= 0; i--)
        {
            if (cardsInHand[i] != null)
            {
                Destroy(cardsInHand[i].gameObject);
            }
        }
        cardsInHand.Clear();
        currentSelectedSteps = 0;

        // 5. 重新发牌并刷新界面
        DealStartingHand();
        UpdateUI("游戏已重置！请点击选中卡牌，然后出牌。");
    }
}