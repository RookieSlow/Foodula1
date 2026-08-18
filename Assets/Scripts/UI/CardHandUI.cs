using System.Collections;
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
    [Tooltip("卡牌实例化后的尺寸覆盖。")]
    public Vector2 cardSizeOverride = new Vector2(170, 255);

    [Header("卡牌精灵图")]
    public Sprite speedBgSprite;
    public Sprite heatBgSprite;
    public Sprite selectedOverlaySprite;
    public Sprite[] numberSprites = new Sprite[4];
    public Sprite heatIconSprite;

    [Header("档位选择 UI")]
    public GameObject gearSelectionPanel;
    public TMP_Text gearPromptText;
    public TMP_Text deckInfoText;
    [Tooltip("Optional layout labels populated by RaceUILayoutController.")]
    public TMP_Text drawPileText;
    public TMP_Text enginePileText;
    public TMP_Text discardPileText;

    [Header("出牌按钮")]
    public UnityEngine.UI.Button playCardsButton;

    private MVPGameManager gameManager;
    private List<CardUI> cardUIs = new List<CardUI>();
    private bool isGearSelectionMode;
    private bool isDiscardMode;
    // Normal play supports a multi-selected speed-card group, while a trick
    // card is intentionally restricted to one selected card at a time.
    private CardUI pendingPlayCard;
    private Coroutine actionButtonCooldown;

    /// <summary>Compatibility accessor for callers that expect a single selection.</summary>
    public CardData PendingPlayCard
    {
        get
        {
            List<CardData> selected = GetSelectedPlayCards();
            return selected.Count == 1 ? selected[0] : null;
        }
    }

    void Start()
    {
        BindPlayCardsButton();
    }

    void OnDestroy()
    {
        if (playCardsButton != null)
            playCardsButton.onClick.RemoveListener(OnPlayClicked);
    }

    /// <summary>Assigns the action button and guarantees a single CardHandUI listener.</summary>
    public void SetPlayCardsButton(UnityEngine.UI.Button button)
    {
        if (playCardsButton != null)
            playCardsButton.onClick.RemoveListener(OnPlayClicked);
        playCardsButton = button;
        BindPlayCardsButton();
        UpdateActionButtonLabel();
    }

    private void BindPlayCardsButton()
    {
        if (playCardsButton == null) return;
        playCardsButton.onClick.RemoveListener(OnPlayClicked);
        playCardsButton.onClick.AddListener(OnPlayClicked);
    }

    /// <summary>
    /// Briefly blocks the shared confirm/end button after a card is confirmed,
    /// preventing a physical double-click from immediately ending the play phase.
    /// </summary>
    public void BlockActionButtonBriefly(float seconds = 0.25f)
    {
        if (playCardsButton == null || !isActiveAndEnabled) return;
        if (actionButtonCooldown != null)
            StopCoroutine(actionButtonCooldown);
        actionButtonCooldown = StartCoroutine(ActionButtonCooldown(seconds));
    }

    private IEnumerator ActionButtonCooldown(float seconds)
    {
        playCardsButton.interactable = false;
        yield return new WaitForSecondsRealtime(seconds);
        if (playCardsButton != null)
            playCardsButton.interactable = true;
        actionButtonCooldown = null;
    }

    private void CancelActionButtonCooldown()
    {
        if (actionButtonCooldown != null)
        {
            StopCoroutine(actionButtonCooldown);
            actionButtonCooldown = null;
        }
        if (playCardsButton != null)
            playCardsButton.interactable = true;
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
                // 注入精灵图引用
                ui.SetSprites(speedBgSprite, heatBgSprite, selectedOverlaySprite,
                    numberSprites, heatIconSprite);

                // 特技牌显示名称（icon + 中文名）
                string label = null;
                if (card.IsTrick && gameManager != null && gameManager.Session != null)
                {
                    var def = gameManager.Session.TrickDb.Get(card.trickId);
                    if (def != null) label = $"{def.icon} {def.name}";
                }
                ui.SetupCard(card, OnCardClicked, label);
            }
            cardUIs.Add(ui);
        }

        UpdateActionButtonLabel();
    }

    /// <summary>
    /// 清除所有手牌 GameObject。
    /// </summary>
    public void ClearHand()
    {
        pendingPlayCard = null;
        for (int i = cardUIs.Count - 1; i >= 0; i--)
        {
            if (cardUIs[i] != null) Destroy(cardUIs[i].gameObject);
        }
        cardUIs.Clear();
    }

    /// <summary>
    /// 从 UI 中移除单张卡牌（不重建整个手牌，保留已有选中状态）。
    /// 调用方负责确保该卡已在数据层被移除。
    /// </summary>
    public void RemoveCardUI(CardData card)
    {
        for (int i = cardUIs.Count - 1; i >= 0; i--)
        {
            if (cardUIs[i] != null && cardUIs[i].cardData == card)
            {
                if (pendingPlayCard == cardUIs[i])
                    pendingPlayCard = null;
                Destroy(cardUIs[i].gameObject);
                cardUIs.RemoveAt(i);
                break; // 只移除第一个匹配的（同一 CardData 引用不会重复出现）
            }
        }
        UpdateActionButtonLabel();
    }

    /// <summary>
    /// 隐藏所有 UI（游戏结束时调用）。
    /// </summary>
    public void HideAll()
    {
        CancelActionButtonCooldown();
        ClearHand();
        if (gearSelectionPanel != null) gearSelectionPanel.SetActive(false);
        if (playCardsButton != null) playCardsButton.gameObject.SetActive(false);
    }

    /// <summary>
    /// 切换档位选择模式 / 卡牌选择模式。
    /// 进入正常选牌模式（isGearMode=false）时清除残留的弃牌标记，
    /// 防止 Turn 2+ 的选牌阶段被  Turn 1 结束时 SetDiscardMode 锁死。
    /// </summary>
    public void SetGearSelectionMode(bool isGearMode)
    {
        CancelActionButtonCooldown();
        isGearSelectionMode = isGearMode;
        ClearPendingPlaySelection();
        if (!isGearMode)
            isDiscardMode = false; // 离开档位模式 → 一定是正常选牌，清除弃牌残留

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
        UpdateActionButtonLabel();
    }

    /// <summary>
    /// 切换弃牌模式 — 可选中任意非热量牌弃掉。
    /// </summary>
    public void SetDiscardMode(bool isDiscard)
    {
        CancelActionButtonCooldown();
        ClearPendingPlaySelection();
        isDiscardMode = isDiscard;
        isGearSelectionMode = false;
        if (gearSelectionPanel != null)
            gearSelectionPanel.SetActive(false);
        if (playCardsButton != null)
            playCardsButton.gameObject.SetActive(true);
        UpdateActionButtonLabel();
    }

    /// <summary>
    /// 获取当前选中的可弃置牌列表（速度牌或特技牌；热量牌不可选中）。
    /// </summary>
    public List<CardData> GetSelectedCards()
    {
        List<CardData> selected = new List<CardData>();
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null && ui.isSelected && ui.cardData != null && !ui.cardData.IsHeat)
            {
                selected.Add(ui.cardData);
            }
        }
        return selected;
    }

    /// <summary>
    /// Returns the normal play selection. Speed cards may contain multiple
    /// cards; trick cards are limited to one by the click handler.
    /// </summary>
    public List<CardData> GetSelectedPlayCards()
    {
        List<CardData> selected = new List<CardData>();
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null && ui.isSelected && ui.cardData != null &&
                (ui.cardData.IsSpeed || ui.cardData.IsTrick))
                selected.Add(ui.cardData);
        }
        return selected;
    }

    /// <summary>Returns the one selected trick card, if any.</summary>
    public CardData GetSelectedTrickCard()
    {
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null && ui.isSelected && ui.cardData != null && ui.cardData.IsTrick)
                return ui.cardData;
        }
        return null;
    }

    /// <summary>
    /// 获取当前选中的速度牌数量。
    /// </summary>
    public int GetSelectedSpeedCount()
    {
        int count = 0;
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null && ui.isSelected && ui.cardData != null && ui.cardData.IsSpeed)
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

        // 弃牌阶段允许多选任意非热量牌；确认后只弃置，不发动效果。
        if (isDiscardMode)
        {
            card.SetSelectedWithoutNotify(!card.isSelected);
            UpdateActionButtonLabel();
            return;
        }

        if (gameManager.CurrentPhase != GamePhase.WaitingForCards) return;

        var player = gameManager.Player;
        if (player == null) return;
        int gear = player.gear;
        int maxCards = gameManager.GetMaxSpeedCardsThisTurn(player);

        int selectedSpeedCount = GetSelectedSpeedCount();
        if (card.cardData.IsTrick && selectedSpeedCount > 0)
        {
            if (gameManager.hudUI != null)
                gameManager.hudUI.SetStatus("速度牌已选中；请先确认速度牌，特技牌必须单独打出");
            return;
        }

        if (card.cardData.IsSpeed && GetSelectedTrickCard() != null)
        {
            if (gameManager.hudUI != null)
                gameManager.hudUI.SetStatus("特技牌已选中；请先确认特技牌，不能与速度牌混选");
            return;
        }

        if (card.cardData.IsSpeed && !card.isSelected &&
            player.playedSpeedCardsThisTurn.Count + selectedSpeedCount >= maxCards)
        {
            if (gameManager.hudUI != null)
                gameManager.hudUI.SetStatus($"<color=orange>{TeamGearRules.GetDisplayName(player.teamId, gear)} 档已打满 {maxCards} 张速度牌</color>");
            return;
        }

        // Speed cards toggle independently so the player can select a group.
        // A trick card remains a one-card selection and is confirmed alone.
        if (card.cardData.IsSpeed)
        {
            card.SetSelectedWithoutNotify(!card.isSelected);
            pendingPlayCard = null;
        }
        else
        {
            if (pendingPlayCard == card)
            {
                card.SetSelectedWithoutNotify(false);
                pendingPlayCard = null;
            }
            else
            {
                // Defensive cleanup in case a stale UI contains two tricks.
                foreach (CardUI ui in cardUIs)
                {
                    if (ui != null && ui != card && ui.cardData != null && ui.cardData.IsTrick)
                        ui.SetSelectedWithoutNotify(false);
                }
                pendingPlayCard = card;
                card.SetSelectedWithoutNotify(true);
            }
        }

        UpdateActionButtonLabel();
        UpdatePendingCardStatus(player, maxCards);
    }

    private void OnPlayClicked()
    {
        gameManager?.OnPlayCardsButtonClicked();
    }

    /// <summary>Clears all play selections without changing the underlying hand.</summary>
    public void ClearPendingPlaySelection()
    {
        foreach (CardUI ui in cardUIs)
        {
            if (ui != null)
                ui.SetSelectedWithoutNotify(false);
        }
        pendingPlayCard = null;
        UpdateActionButtonLabel();
    }

    private void UpdatePendingCardStatus(PlayerState player, int maxCards)
    {
        if (gameManager == null || gameManager.hudUI == null || player == null) return;

        int played = player.playedSpeedCardsThisTurn.Count;
        List<CardData> selected = GetSelectedPlayCards();
        if (selected.Count == 0)
        {
            gameManager.hudUI.SetStatus($"{TeamGearRules.GetDisplayName(player.teamId, player.gear)} 档 - 已打出 {played}/{maxCards} 张速度牌；可多选速度牌后确认");
            return;
        }

        if (selected[0].IsSpeed)
        {
            int total = 0;
            for (int i = 0; i < selected.Count; i++) total += selected[i].value;
            gameManager.hudUI.SetStatus(
                $"待确认：{selected.Count} 张速度牌（速度总和 {total}，本回合 {played + selected.Count}/{maxCards}）");
            return;
        }

        var def = gameManager.Session != null
            ? gameManager.Session.TrickDb.Get(selected[0].trickId)
            : null;
        string label = def != null ? def.name : "特技牌";
        gameManager.hudUI.SetStatus($"待确认：{label}（确认后立即发动）");
    }

    private void UpdateActionButtonLabel()
    {
        if (playCardsButton == null) return;

        string label;
        if (isDiscardMode)
            label = "确认弃牌";
        else
        {
            List<CardData> selected = GetSelectedPlayCards();
            if (selected.Count == 0)
                label = "结束出牌";
            else if (selected[0].IsTrick)
                label = "确认特技牌";
            else
                label = selected.Count == 1 ? "确认速度牌" : $"确认速度牌 ({selected.Count})";
        }

        TMP_Text tmp = playCardsButton.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = label;
            return;
        }

        UnityEngine.UI.Text legacy = playCardsButton.GetComponentInChildren<UnityEngine.UI.Text>(true);
        if (legacy != null)
            legacy.text = label;
    }

    public void UpdateDeckInfo(PlayerState player)
    {
        UpdatePileInfoSafe(player);
        if (deckInfoText != null && deckInfoText != enginePileText && player != null)
        {
            int spd = player.deck.CountSpeedInDeck();
            int heat = player.deck.CountHeatInDeck();
            int trick = player.deck.CountTricksInDeck();
            int handHeat = player.deck.CountHeatInHand();
            deckInfoText.text = $"牌堆: {spd}速 + {trick}特 + {heat}热 | 手牌热量: {handHeat}";
        }
    }

    #if false
    private void UpdatePileInfo(PlayerState player)
    {
        if (player == null || player.deck == null)
            return;

        if (drawPileText != null)
            drawPileText.text = $"抽牌堆\n{player.deck.DrawPileCount} 张";

        if (enginePileText != null)
            enginePileText.text = $"引擎库\n{player.deck.heatPool.remaining} 热量";

        if (discardPileText != null)
            discardPileText.text = $"弃牌堆\n{player.deck.DiscardPileCount} 张";
    }
    #endif

    private void UpdatePileInfoSafe(PlayerState player)
    {
        if (player == null || player.deck == null)
            return;

        if (drawPileText != null)
            drawPileText.text = "Draw pile\n" + player.deck.DrawPileCount + " cards";
        if (enginePileText != null)
            enginePileText.text = "Engine\n" + player.deck.heatPool.remaining + " heat";
        if (discardPileText != null)
            discardPileText.text = "Discard pile\n" + player.deck.DiscardPileCount + " cards";
    }
}
