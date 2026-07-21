using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 手牌 UI 管理器 — 在手牌区域生成/刷新卡牌 GameObject，
/// 处理卡牌选择交互，提供档位选择模式切换。
/// </summary>
public class CardHandUI : MonoBehaviour
{
    [Header("Prefab & 容器")]
    public GameObject cardPrefab;
    public Transform handContainer;

    [Header("档位选择 UI")]
    public GameObject gearSelectionPanel;
    public TMP_Text gearPromptText;
    public TMP_Text deckInfoText;

    [Header("出牌按钮")]
    public UnityEngine.UI.Button playCardsButton;

    private MVPGameManager gameManager;
    private List<CardUI> cardUIs = new List<CardUI>();
    private bool isGearSelectionMode;

    void Start()
    {
        if (playCardsButton != null)
            playCardsButton.onClick.AddListener(OnPlayClicked);
    }

    /// <summary>
    /// 刷新手牌显示 — 清空旧卡、为手牌中每张牌实例化 CardUI。
    /// </summary>
    public void ShowHand(MVPGameManager gm, PlayerState player)
    {
        gameManager = gm;
        ClearHand();

        if (cardPrefab == null)
        {
            Debug.LogWarning("CardHandUI: cardPrefab is not set. Drag CardPrefab into the Inspector.");
            return;
        }

        foreach (CardData card in player.deck.Hand)
        {
            GameObject cardObj = Instantiate(cardPrefab, handContainer);
            CardUI ui = cardObj.GetComponent<CardUI>();
            if (ui != null)
            {
                // 如果档位已决定且需要选牌，设为可选
                ui.SetupCard(card, OnCardClicked);
            }
            cardUIs.Add(ui);
        }
    }

    /// <summary>
    /// 清除所有手牌 GameObject。
    /// </summary>
    public void ClearHand()
    {
        for (int i = cardUIs.Count - 1; i >= 0; i--)
        {
            if (cardUIs[i] != null) Destroy(cardUIs[i].gameObject);
        }
        cardUIs.Clear();
    }

    /// <summary>
    /// 隐藏所有 UI（游戏结束时调用）。
    /// </summary>
    public void HideAll()
    {
        ClearHand();
        if (gearSelectionPanel != null) gearSelectionPanel.SetActive(false);
        if (playCardsButton != null) playCardsButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// 切换档位选择模式 / 卡牌选择模式。
    /// </summary>
    public void SetGearSelectionMode(bool isGearMode)
    {
        isGearSelectionMode = isGearMode;
        if (gearSelectionPanel != null)
            gearSelectionPanel.SetActive(isGearMode);
        if (playCardsButton != null)
            playCardsButton.gameObject.SetActive(!isGearMode);

        if (gearPromptText != null)
        {
            gearPromptText.text = isGearMode
                ? "Select gear (+1 up, any down)"
                : "";
        }
    }

    /// <summary>
    /// 获取当前选中的卡牌列表。
    /// </summary>
    public List<CardData> GetSelectedCards()
    {
        List<CardData> selected = new List<CardData>();
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null && ui.isSelected)
            {
                selected.Add(ui.cardData);
            }
        }
        return selected;
    }

    /// <summary>
    /// 获取当前选中的速度牌数量。
    /// </summary>
    public int GetSelectedSpeedCount()
    {
        int count = 0;
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null && ui.isSelected && ui.cardData.IsSpeed)
                count++;
        }
        return count;
    }

    // ====== 回调 ======

    private void OnCardClicked(CardUI card)
    {
        if (gameManager == null) return;
        if (isGearSelectionMode) return;

        int gear = gameManager.Player.gear;

        // card.isSelected 已在 CardUI.OnCardClicked 中翻转完毕
        if (card.isSelected)
        {
            // 刚刚被选中 → 检查是否超出限制
            int speedCount = GetSelectedSpeedCount();
            int heatCount = GetSelectedHeatCount();

            if (card.cardData.IsSpeed && speedCount > gear)
            {
                card.SetSelectedWithoutNotify(false); // 超限，撤销
                return;
            }
            if (card.cardData.IsHeat && heatCount > gear)
            {
                card.SetSelectedWithoutNotify(false); // 超限，撤销
                return;
            }

            if (gameManager.hudUI != null)
                gameManager.hudUI.SetStatus($"Gear {gear} - {speedCount}/{gear} speed, {heatCount}/{gear} heat");
        }
        else
        {
            // 取消选中
            int speedCount = GetSelectedSpeedCount();
            if (gameManager.hudUI != null)
                gameManager.hudUI.SetStatus($"Gear {gear} - {speedCount}/{gear} speed");
        }
    }

    private int GetSelectedHeatCount()
    {
        int count = 0;
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null && ui.isSelected && ui.cardData.IsHeat)
                count++;
        }
        return count;
    }

    private void OnPlayClicked()
    {
        gameManager?.OnPlayCardsButtonClicked();
    }

    public void UpdateDeckInfo(PlayerState player)
    {
        if (deckInfoText != null && player != null)
        {
            int spd = player.deck.CountSpeedInDeck();
            int heat = player.deck.CountHeatInDeck();
            deckInfoText.text = $"Deck: {spd}S + {heat}H";
        }
    }
}
