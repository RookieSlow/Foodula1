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
    [Tooltip("卡牌实例化后的尺寸覆盖（原 CardPrefab 是 160×30 线条，太窄）。")]
    public Vector2 cardSizeOverride = new Vector2(180, 240);

    [Header("档位选择 UI")]
    public GameObject gearSelectionPanel;
    public TMP_Text gearPromptText;
    public TMP_Text deckInfoText;

    [Header("出牌按钮")]
    public UnityEngine.UI.Button playCardsButton;

    private MVPGameManager gameManager;
    private List<CardUI> cardUIs = new List<CardUI>();
    private bool isGearSelectionMode;
    private bool isDiscardMode;

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

            // 覆盖卡牌尺寸 — CardPrefab 原始 160×30 太小
            RectTransform crt = cardObj.GetComponent<RectTransform>();
            if (crt != null)
            {
                crt.sizeDelta = cardSizeOverride;
                // 添加 LayoutElement 锁定尺寸，防止 LayoutGroup 覆盖
                var le = cardObj.GetComponent<UnityEngine.UI.LayoutElement>();
                if (le == null) le = cardObj.AddComponent<UnityEngine.UI.LayoutElement>();
                le.preferredWidth = cardSizeOverride.x;
                le.preferredHeight = cardSizeOverride.y;
            }

            CardUI ui = cardObj.GetComponent<CardUI>();
            if (ui != null)
            {
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
                ? "Select gear (+1 free, +2 costs 1 Heat)"
                : "";
        }
    }

    /// <summary>
    /// 切换弃牌模式 — 可选中任意非热量牌弃掉。
    /// </summary>
    public void SetDiscardMode(bool isDiscard)
    {
        isDiscardMode = isDiscard;
        isGearSelectionMode = false;
        if (gearSelectionPanel != null)
            gearSelectionPanel.SetActive(false);
        if (playCardsButton != null)
            playCardsButton.gameObject.SetActive(true);
    }

    /// <summary>
    /// 获取当前选中的速度牌列表（热量牌不可选中）。
    /// </summary>
    public List<CardData> GetSelectedCards()
    {
        List<CardData> selected = new List<CardData>();
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null && ui.isSelected && ui.cardData.IsSpeed)
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

        // 热量牌不可打出/不可弃掉 — 忽略点击
        if (card.cardData.IsHeat) return;

        // 弃牌模式：无数量限制，任意选
        if (isDiscardMode) return;

        int gear = gameManager.Player.gear;

        // card.isSelected 已在 CardUI.OnCardClicked 中翻转完毕
        if (card.isSelected)
        {
            // 刚刚被选中 → 检查是否超出速度牌限制
            int speedCount = GetSelectedSpeedCount();

            if (speedCount > gear)
            {
                card.SetSelectedWithoutNotify(false); // 超限，撤销
                return;
            }

            if (gameManager.hudUI != null)
                gameManager.hudUI.SetStatus($"Gear {gear} - {speedCount}/{gear} speed cards selected");
        }
        else
        {
            // 取消选中
            int speedCount = GetSelectedSpeedCount();
            if (gameManager.hudUI != null)
                gameManager.hudUI.SetStatus($"Gear {gear} - {speedCount}/{gear} speed cards selected");
        }
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
            int handHeat = player.deck.CountHeatInHand();
            deckInfoText.text = $"Deck: {spd}S + {heat}H | Hand Heat: {handHeat}";
        }
    }
}
