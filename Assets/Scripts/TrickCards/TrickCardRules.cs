using System.Collections.Generic;

// ═══════════════════════════════════════════════════════════════════════════════
// TrickCardRules.cs — Trick card database factory + effect resolution rules.
// Pure C#, no Unity deps. Follows ADR-002.
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Factory for the built-in trick card database (12 cards, 6 countries).</summary>
public static class TrickCardDatabaseFactory
{
    public static TrickCardDatabase CreateDefault()
    {
        var db = new TrickCardDatabase();

        // ── UK: 炸鱼薯条车队 ──
        db.Add(new TrickCardDef(
            "uk-scone", "司康", "Scone", TeamId.UK, TrickCardType.Attack,
            TrickEffectType.Scone,
            "从引擎支付1张热量牌 → 本回合前进+2格", "🍪"));

        db.Add(new TrickCardDef(
            "uk-english-breakfast-tea", "英式红茶", "English Breakfast Tea",
            TeamId.UK, TrickCardType.Defense, TrickEffectType.EnglishBreakfastTea,
            "冷却手牌中1张热量牌", "☕"));

        // ── DE: 啤酒黑面包车队 ──
        db.Add(new TrickCardDef(
            "de-sauerkraut", "酸菜发酵", "Sauerkraut", TeamId.DE, TrickCardType.Attack,
            TrickEffectType.Sauerkraut,
            "本回合经过了弯道→出弯后额外+2移动。未经过弯道→仅+1", "🥬"));

        db.Add(new TrickCardDef(
            "de-schwarzbrot", "黑面包垫底", "Schwarzbrot", TeamId.DE, TrickCardType.Defense,
            TrickEffectType.Schwarzbrot,
            "本回合下一次从引擎支付热量时，少付1张（最少为1）", "🍞"));

        // ── IT: 意面披萨车队 ──
        db.Add(new TrickCardDef(
            "it-parmigiano", "帕尔马干酪", "Parmigiano", TeamId.IT, TrickCardType.Attack,
            TrickEffectType.Parmigiano,
            "本回合尾流加成+2（基础+2→总共+4）", "🧀"));

        db.Add(new TrickCardDef(
            "it-chianti", "基安蒂红酒", "Chianti", TeamId.IT, TrickCardType.Defense,
            TrickEffectType.Chianti,
            "弃掉手中任意1张速度牌→冷却1张热量牌", "🍷"));

        // ── US: 汉堡烤肉车队 ──
        db.Add(new TrickCardDef(
            "us-fries", "薯条", "Fries", TeamId.US, TrickCardType.Attack,
            TrickEffectType.Fries,
            "若上回合经过了地标所在格→获得1张限时热量牌（本回合可用，回合结束销毁）", "🍟"));

        db.Add(new TrickCardDef(
            "us-cola", "可乐", "Cola", TeamId.US, TrickCardType.Defense,
            TrickEffectType.Cola,
            "若上回合经过了地标所在格→抽1张牌", "🥤"));

        // ── CN: 茶点车队 ──
        db.Add(new TrickCardDef(
            "cn-hotpot-base", "火锅底料", "Hotpot Base", TeamId.CN, TrickCardType.Attack,
            TrickEffectType.HotpotBase,
            "Go模式→再出1张速度牌标记为ATTACK牌：该牌速度+1，且+1不计入弯道限速判定", "🍲"));

        db.Add(new TrickCardDef(
            "cn-ice-jelly", "冰糕", "Ice Jelly", TeamId.CN, TrickCardType.Defense,
            TrickEffectType.IceJelly,
            "Recover模式→身后赛车无法享受尾流", "🍧"));

        // ── JP: 寿司拉面车队 ──
        db.Add(new TrickCardDef(
            "jp-torpedo-tempura", "鱼雷天妇罗", "Torpedo Tempura",
            TeamId.JP, TrickCardType.Attack, TrickEffectType.TorpedoTempura,
            "本回合超车时→获得+1速度。被超车时→对方获得+1速度", "🍤"));

        db.Add(new TrickCardDef(
            "jp-kanto-oden", "关东慢煮", "Kanto Oden", TeamId.JP, TrickCardType.Defense,
            TrickEffectType.KantoOden,
            "跳过本回合。将本回合档位的出牌数累加到下一回合", "🍢"));

        return db;
    }
}

/// <summary>
/// Pure-function trick card rules engine.
/// Resolves trick card effects — immediate, turn-modifier, and conditional.
/// </summary>
public static class TrickCardRules
{
    public const int MAX_TRICKS_PER_TURN = 1;
    public const int TRICK_OVERFLOW_THRESHOLD = 3; // ≥3 in hand → can discard
    public const int INITIAL_TRICK_CARDS_PER_TEAM = 4; // 2 attack + 2 defense

    // ═══════════════════════════════════════════════════════════════════
    // Play Validation
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Can a trick card be played this turn?</summary>
    public static bool CanPlayTrick(TrickCardState state)
    {
        return !state.trickPlayedThisTurn && !state.kantoOdenActive;
    }

    /// <summary>
    /// Can a specific trick card be played given the current game mode?
    /// Some cards require specific modes (CN: Go/Recover).
    /// </summary>
    public static bool CanPlaySpecificTrick(
        TrickCardDef def, bool isGoMode, bool isRecoverMode)
    {
        switch (def.effectType)
        {
            case TrickEffectType.HotpotBase:
                return isGoMode;
            case TrickEffectType.IceJelly:
                return isRecoverMode;
            default:
                return true; // Unconditional
        }
    }

    /// <summary>Should the trick overflow protection trigger?</summary>
    public static bool ShouldTriggerOverflow(CardDeck deck)
    {
        return CountTricksInHand(deck) >= TRICK_OVERFLOW_THRESHOLD;
    }

    // ═══════════════════════════════════════════════════════════════════
    // Effect Resolution
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Resolve the immediate effect of playing a trick card.
    /// Returns a TrickPlayResult with the effects to apply.
    /// Turn-modifier effects (Parmigiano, etc.) are handled by setting flags on state.
    /// </summary>
    public static TrickPlayResult ResolvePlay(
        TrickCardDef def, TrickCardState state,
        bool hasHeatInEngine, bool hasHeatInHand,
        bool hasSpeedInHand, bool crossedLandmarkLastTurn)
    {
        TrickPlayResult result;
        switch (def.effectType)
        {
            // ── UK ──
            case TrickEffectType.Scone:
                result = ResolveScone(hasHeatInEngine);
                break;

            case TrickEffectType.EnglishBreakfastTea:
                result = ResolveEnglishBreakfastTea(hasHeatInHand);
                break;

            // ── DE ──
            case TrickEffectType.Sauerkraut:
                result = ResolveSauerkraut(state);
                break;

            case TrickEffectType.Schwarzbrot:
                result = ResolveSchwarzbrot(state);
                break;

            // ── IT ──
            case TrickEffectType.Parmigiano:
                result = ResolveParmigiano(state);
                break;

            case TrickEffectType.Chianti:
                result = ResolveChianti(hasSpeedInHand, hasHeatInHand);
                break;

            // ── US ──
            case TrickEffectType.Fries:
                result = ResolveFries(state, crossedLandmarkLastTurn);
                break;

            case TrickEffectType.Cola:
                result = ResolveCola(state, crossedLandmarkLastTurn);
                break;

            // ── CN ──
            case TrickEffectType.HotpotBase:
                result = ResolveHotpotBase(state);
                break;

            case TrickEffectType.IceJelly:
                result = ResolveIceJelly(state);
                break;

            // ── JP ──
            case TrickEffectType.TorpedoTempura:
                result = ResolveTorpedoTempura(state);
                break;

            case TrickEffectType.KantoOden:
                result = ResolveKantoOden(state, hasHeatInHand); // card count = gear
                break;

            default:
                result = TrickPlayResult.Fail($"Unknown trick effect: {def.effectType}");
                break;
        }

        // A failed play must not consume the once-per-turn trick slot.  This is
        // especially important for resource-gated cards such as Scone and Tea.
        if (result.success)
        {
            state.trickPlayedThisTurn = true;
            state.trickPlayedThisTurnId = def.id;
        }
        return result;
    }

    // ── UK Attack: Scone — pay 1 heat from engine → +2 move ──
    private static TrickPlayResult ResolveScone(bool hasHeatInEngine)
    {
        if (!hasHeatInEngine)
            return TrickPlayResult.Fail("引擎无热量牌可支付");
        return new TrickPlayResult
        {
            success = true,
            message = "司康: 支付1热 → +2格",
            heatToPay = 1,
            extraMovement = 2
        };
    }

    // ── UK Defense: English Breakfast Tea — cool 1 heat from hand ──
    private static TrickPlayResult ResolveEnglishBreakfastTea(bool hasHeatInHand)
    {
        if (!hasHeatInHand)
            return TrickPlayResult.Fail("手牌无热量牌可冷却");
        return new TrickPlayResult
        {
            success = true,
            message = "英式红茶: 冷却1热",
            heatToCool = 1
        };
    }

    // ── DE Attack: Sauerkraut — crossed corner → +2; else +1 ──
    private static TrickPlayResult ResolveSauerkraut(TrickCardState state)
    {
        state.sauerkrautPlayed = true;
        // Actual move bonus is determined at turn resolution (after corner check)
        return TrickPlayResult.Ok("酸菜发酵: 待弯道判定后生效");
    }

    /// <summary>Get Sauerkraut movement bonus given whether corner was crossed.</summary>
    public static int GetSauerkrautBonus(TrickCardState state, bool crossedCorner)
    {
        if (!state.sauerkrautPlayed) return 0;
        return crossedCorner ? 2 : 1;
    }

    // ── DE Defense: Schwarzbrot — next heat payment -1 (min 1) ──
    private static TrickPlayResult ResolveSchwarzbrot(TrickCardState state)
    {
        state.schwarzbrotActive = true;
        state.schwarzbrotRemaining = 1;
        return TrickPlayResult.Ok("黑面包垫底: 下次热量支付-1");
    }

    /// <summary>Apply Schwarzbrot reduction to a heat payment. Returns reduced amount.</summary>
    public static int ApplySchwarzbrot(TrickCardState state, int heatAmount)
    {
        if (!state.schwarzbrotActive || state.schwarzbrotRemaining <= 0 || heatAmount <= 0)
            return heatAmount;

        state.schwarzbrotRemaining--;
        if (state.schwarzbrotRemaining <= 0)
            state.schwarzbrotActive = false;

        return heatAmount > 1 ? heatAmount - 1 : 1; // min 1
    }

    // ── IT Attack: Parmigiano — slipstream +2 this turn ──
    private static TrickPlayResult ResolveParmigiano(TrickCardState state)
    {
        state.parmigianoActive = true;
        return new TrickPlayResult
        {
            success = true,
            message = "帕尔马干酪: 尾流+2 (总+4)",
            slipstreamBonus = 2
        };
    }

    /// <summary>Get active Parmigiano slipstream bonus for this turn.</summary>
    public static int GetParmigianoBonus(TrickCardState state)
    {
        return state.parmigianoActive ? 2 : 0;
    }

    // ── IT Defense: Chianti — discard 1 speed → cool 1 heat ──
    private static TrickPlayResult ResolveChianti(bool hasSpeedInHand, bool hasHeatInHand)
    {
        if (!hasSpeedInHand)
            return TrickPlayResult.Fail("手牌无速度牌可弃");
        return new TrickPlayResult
        {
            success = true,
            message = "基安蒂红酒: 弃1速度牌→冷却1热",
            requiresSpeedDiscard = true,
            heatToCool = hasHeatInHand ? 1 : 0 // Cool if possible, discard always happens
        };
    }

    // ── US Attack: Fries — if crossed landmark last turn → 1 temp heat ──
    private static TrickPlayResult ResolveFries(TrickCardState state, bool crossedLandmark)
    {
        if (!crossedLandmark)
            return TrickPlayResult.Fail("上回合未经过地标");
        state.tempHeatAvailable = true;
        return TrickPlayResult.Ok("薯条: 获得1张限时热量牌（本回合可用）");
    }

    /// <summary>Check if temp heat from Fries is available.</summary>
    public static bool HasTempHeat(TrickCardState state) => state.tempHeatAvailable;

    /// <summary>Consume the temp heat (caller adds it as a playable heat card).</summary>
    public static void ConsumeTempHeat(TrickCardState state)
    {
        state.tempHeatAvailable = false;
    }

    // ── US Defense: Cola — if crossed landmark last turn → draw 1 ──
    private static TrickPlayResult ResolveCola(TrickCardState state, bool crossedLandmark)
    {
        if (!crossedLandmark)
            return TrickPlayResult.Fail("上回合未经过地标");
        return new TrickPlayResult
        {
            success = true,
            message = "可乐: 抽1张牌",
            cardsToDraw = 1
        };
    }

    // ── CN Attack: Hotpot Base — Go mode → 1 extra speed card as ATTACK ──
    private static TrickPlayResult ResolveHotpotBase(TrickCardState state)
    {
        // Mode check is done in CanPlaySpecificTrick before calling ResolvePlay
        state.hotpotBaseActive = true;
        return TrickPlayResult.Ok("火锅底料: 可再出1张ATTACK牌 (速度+1, 不计入弯道判定)");
    }

    /// <summary>Check if HotpotBase extra ATTACK card is available.</summary>
    public static bool HasHotpotAttack(TrickCardState state) => state.hotpotBaseActive;

    /// <summary>Get the speed bonus for a Hotpot-generated ATTACK card.</summary>
    public static int GetHotpotSpeedBonus() => 1;

    // ── CN Defense: Ice Jelly — Recover mode → block slipstream ──
    private static TrickPlayResult ResolveIceJelly(TrickCardState state)
    {
        // Mode check is done in CanPlaySpecificTrick before calling ResolvePlay
        state.iceJellyActive = true;
        return TrickPlayResult.Ok("冰糕: 身后赛车无法享受尾流");
    }

    /// <summary>Check if Ice Jelly is blocking slipstream this turn.</summary>
    public static bool IsIceJellyActive(TrickCardState state) => state.iceJellyActive;

    // ── JP Attack: Torpedo Tempura — overtaking → +1; overtaken → opponent +1 ──
    private static TrickPlayResult ResolveTorpedoTempura(TrickCardState state)
    {
        state.torpedoTempuraActive = true;
        return TrickPlayResult.Ok("鱼雷天妇罗: 超车时+1速度（被超车时对方+1）");
    }

    /// <summary>Check if Torpedo Tempura is active (overtake bonus available).</summary>
    public static bool IsTorpedoTempuraActive(TrickCardState state) => state.torpedoTempuraActive;

    /// <summary>Get the overtake speed bonus from Torpedo Tempura.</summary>
    public static int GetTorpedoOvertakeBonus() => 1;

    /// <summary>Get the penalty when being overtaken (opponent gets this).</summary>
    public static int GetTorpedoOvertakenPenalty() => 1;

    // ── JP Defense: Kanto Oden — skip turn, accumulate gear cards ──
    private static TrickPlayResult ResolveKantoOden(TrickCardState state, bool hasHeatInHand)
    {
        // Heat cool effect: "slow cooking" — cool 1 heat as part of the skip
        state.kantoOdenActive = true;
        return new TrickPlayResult
        {
            success = true,
            message = "关东慢煮: 跳过本回合，出牌数累加到下回合",
            heatToCool = hasHeatInHand ? 1 : 0
        };
    }

    /// <summary>Accumulate gear card count for Kanto Oden skip turn.</summary>
    public static void AccumulateKantoOden(TrickCardState state, int gear)
    {
        if (state.kantoOdenActive)
            state.kantoOdenAccumulatedCards += gear;
    }

    /// <summary>Consume accumulated cards and end Kanto Oden.</summary>
    public static int ConsumeKantoOden(TrickCardState state)
    {
        int cards = state.kantoOdenAccumulatedCards;
        state.kantoOdenAccumulatedCards = 0;
        state.kantoOdenActive = false;
        return cards;
    }

    /// <summary>Check if player should skip this turn (Kanto Oden).</summary>
    public static bool ShouldSkipTurn(TrickCardState state) => state.kantoOdenActive;

    // ═══════════════════════════════════════════════════════════════════
    // Hand Utilities
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Count trick cards in a player's hand.</summary>
    public static int CountTricksInHand(CardDeck deck)
    {
        int count = 0;
        foreach (var card in deck.Hand)
        {
            if (card.IsTrick) count++;
        }
        return count;
    }

    /// <summary>
    /// Create the initial trick cards for a team's deck.
    /// Returns 4 CardData: 2 attack + 2 defense.
    /// </summary>
    public static List<CardData> CreateInitialTrickCards(TeamId teamId, TrickCardDatabase db)
    {
        var cards = new List<CardData>();
        string attackId = db.GetAttackId(teamId);
        string defenseId = db.GetDefenseId(teamId);

        for (int i = 0; i < 2; i++)
        {
            if (!string.IsNullOrEmpty(attackId))
                cards.Add(CardData.CreateTrick(attackId));
            if (!string.IsNullOrEmpty(defenseId))
                cards.Add(CardData.CreateTrick(defenseId));
        }
        return cards;
    }
}
