using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CardUI : MonoBehaviour
{
    [Header("UI 组件")]
    public TMP_Text valueText;   // 显示卡牌点数的文字
    public Image backgroundImage; // 卡牌的背景图（用于变色提示选中）

    [Header("卡牌数据")]
    public int cardValue;        // 这张牌代表移动几格
    public bool isSelected = false; // 是否被玩家选中

    private GameManager gameManager;

    // 初始化这张卡牌
    public void SetupCard(int value, GameManager gm)
    {
        cardValue = value;
        gameManager = gm;
        valueText.text = value.ToString();
        backgroundImage.color = Color.white; // 默认白色
    }

    // 绑定给卡牌自身 Button 的点击事件
    public void OnCardClicked()
    {
        // 切换选中状态
        isSelected = !isSelected;

        // 选中时变成绿色，取消选中变回白色
        backgroundImage.color = isSelected ? Color.green : Color.white;

        // 告诉 GameManager 重新计算当前选中的总步数
        gameManager.CalculateSelectedSteps();
    }
}