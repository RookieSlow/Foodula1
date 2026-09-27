using System;
using System.Collections.Generic;

public enum TutorialStepId
{
    ObjectiveAndInterface,
    TurnFlow,
    GearAndRequiredCards,
    SpeedCardsAndMovement,
    DeckHandDiscardAndRecycle,
    HeatPayment,
    HeatCardsAndCooling,
    MissingCardPenalty,
    CornerLimitAndSpin,
    Weather,
    Slipstream,
    PitSelection,
    PitDelayedResolution,
    UkScone,
    UkEnglishBreakfastTea,
    Review,
    ChinaFirstGo, ChinaConsecutiveGo, ChinaRecover, ChinaHotpot, ChinaIceJelly,
    UsStraight, UsCorner, UsSlipstream, UsFries, UsCola,
    DeStraight, DeSauerkraut, DeSchwarzbrot,
    ItCorner, ItCornerExit, ItParmigiano, ItChianti,
    UkSpecialtyScone, UkSpecialtyTea,
    JpKantoSkip, JpKantoRelease, JpTorpedo
}

public enum TutorialAction
{
    AcknowledgeObjective,
    CompleteTurnFlow,
    SelectRequiredGearAndCards,
    ResolveSpeedMovement,
    InspectCardZonesAndRecycle,
    PayHeat,
    CoolHeatCard,
    TriggerMissingCardPenalty,
    ResolveCornerSpin,
    ObserveWeatherEffect,
    ResolveSlipstream,
    SelectPit,
    ResolvePitOnNextTurn,
    PlayUkScone,
    PlayUkEnglishBreakfastTea,
    CompleteReview,
    CompletePracticeLap,
    CompleteChinaFirstGo, CompleteChinaConsecutiveGo, CompleteChinaRecover,
    PlayChinaHotpot, PlayChinaIceJelly, ResolveUsStraight, ResolveUsCorner,
    ResolveUsSlipstream, PlayUsFries, PlayUsCola,
    ResolveDeStraight, ResolveDeSauerkraut, ResolveDeSchwarzbrot,
    ResolveItCorner, ResolveItCornerExit, ResolveItParmigiano, ResolveItChianti,
    ResolveUkSpecialtyScone, ResolveUkSpecialtyTea,
    ResolveJpKantoSkip, ResolveJpKantoRelease, ResolveJpTorpedo
}

public enum TutorialRunPhase
{
    Guided,
    Practice,
    Completed,
    Exited
}

/// <summary>
/// Stable semantic targets for the tutorial spotlight. The scenario authors
/// name the concept to focus; the runtime UI resolves it to the current HUD.
/// </summary>
public enum TutorialFocusTarget
{
    RaceStatus,
    TurnPrompt,
    GearControls,
    Hand,
    CardPiles,
    EngineHeat,
    ActionButton,
    Track,
    Weather,
    PitChoice,
    UkSconeCard,
    UkTeaCard,
    Review,
    TeamTrickCard
}

[Serializable]
public sealed class TutorialCardSpec
{
    public CardType type;
    public int value;
    public string trickId;

    public TutorialCardSpec(CardType type, int value, string trickId = null)
    {
        this.type = type;
        this.value = value;
        this.trickId = trickId;
    }

    public CardData CreateCard()
    {
        return type == CardType.Trick
            ? CardData.CreateTrick(trickId)
            : new CardData(type, value);
    }

    public override string ToString()
    {
        if (type == CardType.Trick) return $"T[{trickId}]";
        return value.ToString();
    }
}

[Serializable]
public sealed class TutorialStepDefinition
{
    public TutorialStepId id;
    public TutorialAction requiredAction;
    public string instructionKey;
    public string sectionLabel;
    public string title;
    public string goal;
    public string currentState;
    public string actionPrompt;
    public string successSignal;
    public string recoveryHint;
    public TutorialFocusTarget focusTarget;
    public string focusIntroduction;
    public string manualAdvanceLabel;
    public string instruction;
    public bool allowManualAdvance;

    public TutorialStepDefinition(
        TutorialStepId id,
        TutorialAction requiredAction,
        string instructionKey,
        string sectionLabel,
        string title,
        string goal,
        string currentState,
        string actionPrompt,
        string successSignal,
        string recoveryHint,
        TutorialFocusTarget focusTarget,
        string focusIntroduction,
        string manualAdvanceLabel = "",
        bool allowManualAdvance = false)
    {
        this.id = id;
        this.requiredAction = requiredAction;
        this.instructionKey = instructionKey;
        this.sectionLabel = sectionLabel ?? string.Empty;
        this.title = title ?? string.Empty;
        this.goal = goal ?? string.Empty;
        this.currentState = currentState ?? string.Empty;
        this.actionPrompt = actionPrompt ?? string.Empty;
        this.successSignal = successSignal ?? string.Empty;
        this.recoveryHint = recoveryHint ?? string.Empty;
        this.focusTarget = focusTarget;
        this.focusIntroduction = focusIntroduction ?? string.Empty;
        this.manualAdvanceLabel = manualAdvanceLabel ?? string.Empty;
        instruction = BuildGuideText();
        this.allowManualAdvance = allowManualAdvance;
    }

    public string BuildGuideText()
    {
        return TutorialGuideTextBuilder.Build(
            goal,
            currentState,
            actionPrompt,
            successSignal,
            recoveryHint);
    }
}

public static class TutorialGuideTextBuilder
{
    public static string Build(
        string goal,
        string currentState,
        string actionPrompt,
        string successSignal,
        string recoveryHint)
    {
        var sections = new List<string>();
        AddPlainText(sections, goal);
        AddLabelledText(sections, "#8BD7FF", "现在场上", currentState);
        AddLabelledText(sections, "#FFD27A", "轮到你了", actionPrompt);
        AddLabelledText(sections, "#8FE0A6", "完成后", successSignal);
        if (!string.IsNullOrWhiteSpace(recoveryHint))
            sections.Add($"<size=90%><color=#B9C7D8>需要帮忙？{recoveryHint.Trim()}</color></size>");
        return string.Join("\n\n", sections);
    }

    private static void AddPlainText(ICollection<string> sections, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sections.Add(value.Trim());
    }

    private static void AddLabelledText(
        ICollection<string> sections,
        string color,
        string label,
        string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            sections.Add($"<color={color}><b>{label}</b></color>　{value.Trim()}");
    }
}

[Serializable]
public sealed class TutorialWeatherCue
{
    public TutorialStepId step;
    public string weatherId;

    public TutorialWeatherCue(TutorialStepId step, string weatherId)
    {
        this.step = step;
        this.weatherId = weatherId;
    }
}

[Serializable]
public sealed class TutorialOpponentCue
{
    public TutorialStepId step;
    public int leaderCell;
    public int playerCell;
    public int expectedSlipstreamDistance;

    public TutorialOpponentCue(
        TutorialStepId step,
        int leaderCell,
        int playerCell,
        int expectedSlipstreamDistance)
    {
        this.step = step;
        this.leaderCell = leaderCell;
        this.playerCell = playerCell;
        this.expectedSlipstreamDistance = expectedSlipstreamDistance;
    }
}

public static class TutorialOpponentCueRules
{
    public static bool AppliesWithPlayerCheckpoint(
        TutorialOpponentCue opponentCue,
        TutorialPlayerCheckpoint playerCheckpoint)
    {
        return opponentCue != null && playerCheckpoint != null &&
               opponentCue.step == playerCheckpoint.step;
    }

    public static bool IsSlipstreamCue(TutorialStepId step)
    {
        return step == TutorialStepId.Slipstream ||
               step == TutorialStepId.UsSlipstream ||
               step == TutorialStepId.ItParmigiano;
    }

    /// <summary>
    /// An authored tutorial cue has one teaching leader and one intended
    /// follower: the human player.  A wrong card selection must not let the
    /// AI become the visible beneficiary and teach the opposite rule.
    /// Normal races pass a null cue and keep the standard resolver unchanged.
    /// </summary>
    public static bool ShouldResolveSlipstreamForFollower(
        TutorialOpponentCue activeCue,
        bool isPlayer)
    {
        return activeCue == null ||
               (!IsSlipstreamCue(activeCue.step) &&
                activeCue.step != TutorialStepId.JpTorpedo) || isPlayer;
    }

    /// <summary>
    /// The authored teaching leader is a stationary reference for its single
    /// slipstream or overtake turn. Normal racers and every non-tutorial turn keep their
    /// planned movement unchanged.
    /// </summary>
    public static int ResolveLeaderMovement(
        TutorialOpponentCue activeCue,
        bool isTeachingLeader,
        int plannedMovement)
    {
        return activeCue != null &&
               (IsSlipstreamCue(activeCue.step) ||
                activeCue.step == TutorialStepId.JpTorpedo) &&
               isTeachingLeader
            ? 0
            : plannedMovement;
    }
}

/// <summary>
/// Deterministic card contract for the US specialty tutorial.  The normal
/// race rules remain permissive; only the authored lessons reject an input
/// that would make the next demonstration ambiguous or impossible to reach.
/// </summary>
public static class TutorialSpecialtyCardRules
{
    public static bool JpKantoResolved(bool skipTurn, int gear,
        int carriedCards, int handHeatBefore, int handHeatAfter,
        int engineHeatBefore, int engineHeatAfter)
    {
        return skipTurn && gear == 2 && carriedCards == 2 &&
               handHeatBefore - handHeatAfter == 1 &&
               engineHeatAfter - engineHeatBefore == 1;
    }

    public static bool ValidateJpTrickSelection(TutorialStepId step, string trickId,
        out string reason)
    {
        reason = string.Empty;
        string expected = step == TutorialStepId.JpKantoSkip ? "jp-kanto-oden" :
            step == TutorialStepId.JpTorpedo ? "jp-torpedo-tempura" : null;
        if (expected == null || trickId == expected)
            return true;
        reason = step == TutorialStepId.JpKantoSkip
            ? "这一步只打关东慢煮，观察跳过回合与牌槽累积。"
            : "这一步先打鱼雷天妇罗，再出速度 3 超过前车。";
        return false;
    }

    public static bool ValidateJpSpeedSelection(TutorialStepId step,
        bool torpedoArmed, IReadOnlyList<CardData> played,
        IReadOnlyList<CardData> selected, out string reason)
    {
        reason = string.Empty;
        if (step == TutorialStepId.JpKantoSkip)
        {
            reason = "先打关东慢煮，本回合不出速度牌。";
            return false;
        }
        if (step != TutorialStepId.JpKantoRelease && step != TutorialStepId.JpTorpedo)
            return true;
        if (step == TutorialStepId.JpTorpedo && !torpedoArmed)
        {
            reason = "先打鱼雷天妇罗，再确认速度 3。";
            return false;
        }
        var cards = new List<CardData>();
        AddCards(cards, played);
        AddCards(cards, selected);
        if (step == TutorialStepId.JpKantoRelease && cards.Count <= 3 &&
            cards.TrueForAll(card => card.IsSpeed))
            return true;
        if (step == TutorialStepId.JpTorpedo && cards.Count <= 1 &&
            (cards.Count == 0 || (cards[0].IsSpeed && cards[0].value == 3)))
            return true;
        reason = step == TutorialStepId.JpKantoRelease
            ? "蓄力回合请确认 3 张速度牌，不要多选。"
            : "鱼雷示范只需 1 张速度 3。";
        return false;
    }

    public static bool ValidateJpSpeedCompletion(TutorialStepId step,
        bool torpedoArmed, int carrySlots, IReadOnlyList<CardData> played,
        out string reason)
    {
        reason = string.Empty;
        if (step == TutorialStepId.JpKantoRelease &&
            (carrySlots != 2 || played == null || played.Count != 3))
        {
            reason = "本回合 G1 加上关东慢煮留下的 2 个牌槽，请打满 3 张速度牌。";
            return false;
        }
        if (step == TutorialStepId.JpTorpedo &&
            (!torpedoArmed || played == null || played.Count != 1 ||
             played[0] == null || !played[0].IsSpeed || played[0].value != 3))
        {
            reason = "先打鱼雷天妇罗，再打 1 张速度 3，结束出牌等待超车。";
            return false;
        }
        return true;
    }

    public static bool ValidateUkTrickSelection(TutorialStepId step, string trickId,
        out string reason)
    {
        reason = string.Empty;
        string expected = step == TutorialStepId.UkSpecialtyScone ? "uk-scone" :
            step == TutorialStepId.UkSpecialtyTea ? "uk-english-breakfast-tea" : null;
        if (expected == null || trickId == expected)
            return true;
        reason = step == TutorialStepId.UkSpecialtyScone
            ? "这一步先单独打司康，观察引擎热量和移动加成。"
            : "这一步先单独打英式红茶，观察手牌热量回到引擎。";
        return false;
    }

    public static bool UkSconeResolved(int engineBefore, int engineAfter,
        int moveBonusBefore, int moveBonusAfter)
    {
        return engineBefore - engineAfter == 1 && moveBonusAfter - moveBonusBefore == 2;
    }

    public static bool UkTeaResolved(int heatInHandBefore, int heatInHandAfter,
        int engineBefore, int engineAfter)
    {
        return heatInHandBefore - heatInHandAfter == 1 &&
               engineAfter - engineBefore == 1;
    }

    public static bool ValidateItCornerSpeedSelection(
        TutorialStepId step, IReadOnlyList<CardData> played,
        IReadOnlyList<CardData> selected, out string reason)
    {
        reason = string.Empty;
        if (step != TutorialStepId.ItCorner && step != TutorialStepId.ItCornerExit)
            return true;
        var cards = new List<CardData>();
        AddCards(cards, played);
        AddCards(cards, selected);
        if (cards.Count <= 1 &&
            (cards.Count == 0 || (cards[0].IsSpeed && cards[0].value == 1)))
            return true;
        reason = "本步只出 1 张速度 1，方便看清移动加成。";
        return false;
    }

    public static bool ValidateItCornerSpeedCompletion(
        TutorialStepId step, IReadOnlyList<CardData> played, out string reason)
    {
        reason = string.Empty;
        if ((step != TutorialStepId.ItCorner && step != TutorialStepId.ItCornerExit) ||
            (played != null && played.Count == 1 && played[0] != null &&
             played[0].IsSpeed && played[0].value == 1))
            return true;
        reason = "先确认 1 张速度 1，再结束出牌观察弯道。";
        return false;
    }

    public static bool ValidateItSlipstreamSpeedSelection(
        TutorialStepId step, bool trickArmed,
        IReadOnlyList<CardData> alreadyPlayed, IReadOnlyList<CardData> selected,
        out string reason)
    {
        reason = string.Empty;
        if (step != TutorialStepId.ItParmigiano)
            return true;
        if (!trickArmed)
        {
            reason = "先打帕尔马干酪，再确认 1 张速度 1。";
            return false;
        }
        var cards = new List<CardData>();
        AddCards(cards, alreadyPlayed);
        AddCards(cards, selected);
        if (cards.Count <= 1 &&
            (cards.Count == 0 || (cards[0].IsSpeed && cards[0].value == 1)))
            return true;
        reason = "跟车示范只出 1 张速度 1，别从领航车旁冲过去。";
        return false;
    }

    public static bool ValidateItSlipstreamSpeedCompletion(
        TutorialStepId step, bool trickArmed,
        IReadOnlyList<CardData> played, out string reason)
    {
        reason = string.Empty;
        if (step != TutorialStepId.ItParmigiano ||
            (trickArmed && played != null && played.Count == 1 &&
             played[0] != null && played[0].IsSpeed && played[0].value == 1))
            return true;
        reason = trickArmed
            ? "还要确认 1 张速度 1，才能结束出牌等回合末尾流。"
            : "先打帕尔马干酪，再打速度 1。";
        return false;
    }

    public static bool ValidateDeStraightSpeedSelection(
        TutorialStepId step, IReadOnlyList<CardData> alreadyPlayed,
        IReadOnlyList<CardData> selected, out string reason)
    {
        reason = string.Empty;
        if (step != TutorialStepId.DeStraight)
            return true;
        var cards = new List<CardData>();
        AddCards(cards, alreadyPlayed);
        AddCards(cards, selected);
        if (cards.Count <= 1 &&
            (cards.Count == 0 || (cards[0].IsSpeed && cards[0].value == 1)))
            return true;
        reason = "德国直道示范只需要 1 张速度 1。取消多余选择后再确认。";
        return false;
    }

    public static bool ValidateDeStraightSpeedCompletion(
        TutorialStepId step, IReadOnlyList<CardData> played, out string reason)
    {
        reason = string.Empty;
        if (step != TutorialStepId.DeStraight ||
            (played != null && played.Count == 1 && played[0] != null &&
             played[0].IsSpeed && played[0].value == 1))
            return true;
        reason = "先打出 1 张速度 1，才能结束直道示范。";
        return false;
    }

    public static bool ValidateDeEffectSpeedSelection(
        TutorialStepId step, bool trickArmed,
        IReadOnlyList<CardData> alreadyPlayed, IReadOnlyList<CardData> selected,
        out string reason)
    {
        reason = string.Empty;
        if (step != TutorialStepId.DeSauerkraut && step != TutorialStepId.DeSchwarzbrot)
            return true;
        if (!trickArmed)
        {
            reason = step == TutorialStepId.DeSauerkraut
                ? "先单独打出酸菜发酵，再选 1 张速度 1。"
                : "先单独打出黑面包垫底，再选 1 张速度 1。";
            return false;
        }
        var cards = new List<CardData>();
        AddCards(cards, alreadyPlayed);
        AddCards(cards, selected);
        if (cards.Count <= 1 &&
            (cards.Count == 0 || (cards[0].IsSpeed && cards[0].value == 1)))
            return true;
        reason = "这一步只出 1 张速度 1；取消多余选择再确认。";
        return false;
    }

    public static bool ValidateDeEffectSpeedCompletion(
        TutorialStepId step, bool trickArmed,
        IReadOnlyList<CardData> played, out string reason)
    {
        reason = string.Empty;
        if (step != TutorialStepId.DeSauerkraut && step != TutorialStepId.DeSchwarzbrot)
            return true;
        if (trickArmed && played != null && played.Count == 1 &&
            played[0] != null && played[0].IsSpeed && played[0].value == 1)
            return true;
        reason = trickArmed
            ? "还需要确认 1 张速度 1，再结束出牌。"
            : step == TutorialStepId.DeSauerkraut
                ? "先打酸菜发酵，再打速度 1。"
                : "先打黑面包垫底，再打速度 1。";
        return false;
    }

    public static bool ValidateTrickLessonSpeedSelection(
        TutorialStepId step,
        bool hotpotArmed,
        IReadOnlyList<CardData> selected,
        out string reason)
    {
        reason = string.Empty;
        if (step == TutorialStepId.ChinaHotpot)
        {
            if (!hotpotArmed)
            {
                reason = "先打出火锅底料，再单独确认 1 张速度牌，让它获得 ATTACK。";
                return false;
            }
            if (selected == null || selected.Count != 1 ||
                selected[0] == null || !selected[0].IsSpeed)
            {
                reason = "火锅底料已待命：本次只确认 1 张速度牌，才能看清 ATTACK 给了哪张牌。";
                return false;
            }
        }
        else if (step == TutorialStepId.ChinaIceJelly ||
                 step == TutorialStepId.UsFries ||
                 step == TutorialStepId.UsCola ||
                 step == TutorialStepId.ItChianti ||
                 step == TutorialStepId.UkSpecialtyScone ||
                 step == TutorialStepId.UkSpecialtyTea)
        {
            reason = step == TutorialStepId.ChinaIceJelly
                ? "这一步先打冰糕；速度牌留到下一步。"
                : step == TutorialStepId.UsFries
                    ? "这一步先打薯条；速度牌留到下一步。"
                    : step == TutorialStepId.UsCola
                        ? "这一步先打可乐；速度牌留到下一步。"
                        : step == TutorialStepId.ItChianti
                            ? "这一步先打基安蒂红酒；速度牌留到下一步。"
                            : step == TutorialStepId.UkSpecialtyScone
                                ? "这一步只打司康；速度牌留到练习圈。"
                                : "这一步只打英式红茶；速度牌留到练习圈。";
            return false;
        }
        return true;
    }

    public static bool ValidateTrickLessonCompletion(
        TutorialStepId step, bool stepComplete, out string reason)
    {
        reason = string.Empty;
        if (stepComplete)
            return true;
        switch (step)
        {
            case TutorialStepId.ChinaHotpot:
                reason = "先打火锅底料，再单独确认 1 张速度牌，看到 ATTACK 后才能继续。";
                return false;
            case TutorialStepId.ChinaIceJelly:
                reason = "先在 Recover 挡打出冰糕，再结束本步。";
                return false;
            case TutorialStepId.UsFries:
                reason = "先打出薯条特技牌，再结束本步。";
                return false;
            case TutorialStepId.UsCola:
                reason = "先打出可乐特技牌，再结束本步。";
                return false;
            case TutorialStepId.ItChianti:
                reason = "先打出基安蒂红酒，让速度牌进入弃牌堆并冷却一张热量。";
                return false;
            case TutorialStepId.UkSpecialtyScone:
                reason = "先打出司康，让引擎支付 1 热并获得 +2 移动。";
                return false;
            case TutorialStepId.UkSpecialtyTea:
                reason = "先打出英式红茶，让手牌热量回到引擎。";
                return false;
            default:
                return true;
        }
    }

    public static bool ValidateUsSpeedSelection(
        TutorialStepId step,
        IReadOnlyList<CardData> alreadyPlayed,
        IReadOnlyList<CardData> selected,
        out string reason)
    {
        reason = string.Empty;
        if (step != TutorialStepId.UsStraight &&
            step != TutorialStepId.UsCorner &&
            step != TutorialStepId.UsSlipstream)
            return true;

        var combined = new List<CardData>();
        AddCards(combined, alreadyPlayed);
        AddCards(combined, selected);
        for (int i = 0; i < combined.Count; i++)
        {
            CardData card = combined[i];
            if (card == null || !card.IsSpeed)
            {
                reason = "本步只需要速度牌；特技牌请留到对应的特技步骤。";
                return false;
            }
        }

        if (step == TutorialStepId.UsStraight || step == TutorialStepId.UsSlipstream)
        {
            if (combined.Count > 1 || (combined.Count == 1 && combined[0].value != 1))
            {
                reason = step == TutorialStepId.UsStraight
                    ? "直道示范只出 1 张速度 1。请取消当前选择，再选任意一张速度 1。"
                    : "尾流示范只出 1 张速度 1。请取消当前选择，再选任意一张速度 1。";
                return false;
            }
            return true;
        }

        if (combined.Count > 2)
        {
            reason = "弯道示范只需要两张牌：速度 3 + 速度 2。";
            return false;
        }

        int threes = 0;
        int twos = 0;
        for (int i = 0; i < combined.Count; i++)
        {
            if (combined[i].value == 3) threes++;
            else if (combined[i].value == 2) twos++;
            else
            {
                reason = "弯道示范只需要速度 3 + 速度 2，不要选速度 1。";
                return false;
            }
        }

        if (threes > 1 || twos > 1)
        {
            reason = "弯道示范需要一张速度 3 和一张速度 2，各一张即可。";
            return false;
        }
        return true;
    }

    public static bool ValidateUsSpeedPhaseCompletion(
        TutorialStepId step,
        IReadOnlyList<CardData> played,
        out string reason)
    {
        reason = string.Empty;
        if (step == TutorialStepId.UsStraight || step == TutorialStepId.UsSlipstream)
        {
            if (played == null || played.Count != 1 || played[0] == null ||
                !played[0].IsSpeed || played[0].value != 1)
            {
                reason = step == TutorialStepId.UsStraight
                    ? "本步还没完成：请出 1 张速度 1。"
                    : "本步还没完成：请出 1 张速度 1，才能触发尾流。";
                return false;
            }
            return true;
        }

        if (step == TutorialStepId.UsCorner)
        {
            bool valid = played != null && played.Count == 2;
            int threes = 0;
            int twos = 0;
            if (valid)
            {
                for (int i = 0; i < played.Count; i++)
                {
                    if (played[i] == null || !played[i].IsSpeed)
                    {
                        valid = false;
                        break;
                    }
                    if (played[i].value == 3) threes++;
                    if (played[i].value == 2) twos++;
                }
                valid = valid && threes == 1 && twos == 1;
            }

            if (!valid)
            {
                reason = "本步还没完成：请出一张速度 3 和一张速度 2，再结束出牌。";
                return false;
            }
        }
        return true;
    }

    private static void AddCards(List<CardData> target, IReadOnlyList<CardData> source)
    {
        if (source == null)
            return;
        for (int i = 0; i < source.Count; i++)
            if (source[i] != null)
                target.Add(source[i]);
    }
}

[Serializable]
public sealed class TutorialPitLaneDefinition
{
    public int entryCell;
    public int exitCell;

    public TutorialPitLaneDefinition(int entryCell, int exitCell)
    {
        this.entryCell = entryCell;
        this.exitCell = exitCell;
    }
}

/// <summary>
/// Authored safe state for one guided mechanic. It defines every card and heat
/// zone that affects the demonstration so recovery never depends on prior play.
/// </summary>
[Serializable]
public sealed class TutorialPlayerCheckpoint
{
    public TutorialStepId step;
    public int playerCell;
    public int gear;
    public int normalHandSize;
    public int heatInHand;
    public int heatInDiscard;
    public bool beginAtCardSelection;
    public int chinaConsecutiveGearCount;
    public bool crossedLandmarkLastTurn;
    public bool italyCornerExitBoostReady;
    public int kantoCarryCards;
    public IReadOnlyList<TutorialCardSpec> exactDrawOrder;

    public TutorialPlayerCheckpoint(
        TutorialStepId step,
        int playerCell,
        int gear,
        int normalHandSize,
        int heatInHand,
        int heatInDiscard,
        IReadOnlyList<TutorialCardSpec> exactDrawOrder,
        bool beginAtCardSelection = false,
        int chinaConsecutiveGearCount = 0,
        bool crossedLandmarkLastTurn = false,
        bool italyCornerExitBoostReady = false,
        int kantoCarryCards = 0)
    {
        this.step = step;
        this.playerCell = playerCell;
        this.gear = gear;
        this.normalHandSize = normalHandSize;
        this.heatInHand = heatInHand;
        this.heatInDiscard = heatInDiscard;
        this.exactDrawOrder = exactDrawOrder;
        this.beginAtCardSelection = beginAtCardSelection;
        this.chinaConsecutiveGearCount = chinaConsecutiveGearCount;
        this.crossedLandmarkLastTurn = crossedLandmarkLastTurn;
        this.italyCornerExitBoostReady = italyCornerExitBoostReady;
        this.kantoCarryCards = kantoCarryCards;
    }

    public List<CardData> CreateExactDeck()
    {
        var cards = new List<CardData>(exactDrawOrder.Count);
        foreach (TutorialCardSpec spec in exactDrawOrder)
            cards.Add(spec.CreateCard());
        return cards;
    }
}

/// <summary>
/// Immutable authored inputs for the isolated Le Mans tutorial. Runtime scene
/// wiring consumes this definition; it must not mutate normal race config or
/// progression stores.
/// </summary>
public sealed partial class TutorialScenarioDefinition
{
    public const string ScenarioId = "tutorial_le_mans_uk_v1";
    public const string TrackId = "le_mans_old_mulsanne";
    public const int RuntimeSeed = 20260827;

    public string id { get; }
    public string trackId { get; }
    public TeamId playerTeam { get; }
    public int openingHandSize { get; }
    public int engineHeatCapacity { get; }
    public int opponentCount { get; }
    public TeamId opponentTeam { get; }
    public string guidedStartWeatherId { get; }
    public string practiceWeatherId { get; }
    public bool techTreeEnabled { get; }
    public bool driverSkillsEnabled { get; }
    public bool normalRewardsEnabled { get; }
    public bool normalProgressionWritesEnabled { get; }
    public bool teamVehicleBonusesEnabled { get; }
    public IReadOnlyList<TutorialCardSpec> exactDrawOrder { get; }
    public IReadOnlyList<TutorialStepDefinition> steps { get; }
    public IReadOnlyList<TutorialWeatherCue> weatherScript { get; }
    public IReadOnlyList<TutorialOpponentCue> opponentScript { get; }
    public IReadOnlyList<TutorialPlayerCheckpoint> playerCheckpoints { get; }
    public TutorialPitLaneDefinition tutorialPitLane { get; }

    private TutorialScenarioDefinition(
        IReadOnlyList<TutorialCardSpec> exactDrawOrder,
        IReadOnlyList<TutorialStepDefinition> steps,
        IReadOnlyList<TutorialWeatherCue> weatherScript,
        IReadOnlyList<TutorialOpponentCue> opponentScript,
        IReadOnlyList<TutorialPlayerCheckpoint> playerCheckpoints,
        TutorialPitLaneDefinition tutorialPitLane,
        string scenarioId = ScenarioId,
        string scenarioTrackId = TrackId,
        TeamId scenarioTeam = TeamId.UK,
        bool enableTeamVehicleBonuses = false,
        TeamId scenarioOpponentTeam = TeamId.JP)
    {
        id = scenarioId;
        trackId = scenarioTrackId;
        playerTeam = scenarioTeam;
        openingHandSize = 7;
        engineHeatCapacity = enableTeamVehicleBonuses
            ? TeamVehicleRules.GetBaseHeatPoolSize(scenarioTeam, 6)
            : 6;
        opponentCount = 1;
        opponentTeam = scenarioOpponentTeam;
        guidedStartWeatherId = "sunny";
        practiceWeatherId = "cloudy";
        techTreeEnabled = false;
        driverSkillsEnabled = false;
        normalRewardsEnabled = false;
        normalProgressionWritesEnabled = false;
        teamVehicleBonusesEnabled = enableTeamVehicleBonuses;
        this.exactDrawOrder = exactDrawOrder;
        this.steps = steps;
        this.weatherScript = weatherScript;
        this.opponentScript = opponentScript;
        this.playerCheckpoints = playerCheckpoints;
        this.tutorialPitLane = tutorialPitLane;
    }

    public List<CardData> CreateExactDeck()
    {
        var cards = new List<CardData>(exactDrawOrder.Count);
        foreach (TutorialCardSpec spec in exactDrawOrder)
            cards.Add(spec.CreateCard());
        return cards;
    }

    /// <summary>
    /// Exact speed-only deck for the deterministic teaching leader. Tutorial
    /// checkpoints may reposition it later without changing normal AI rules.
    /// </summary>
    public List<CardData> CreateOpponentDeck()
    {
        return new List<CardData>
        {
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 3),
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 3),
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 3),
            new CardData(CardType.Speed, 4)
        };
    }

    private static TutorialPlayerCheckpoint TeamCheckpoint(
        TutorialStepId step, int cell, int gear, int consecutive, int heatHand,
        bool cardSelection, TutorialCardSpec first, TutorialCardSpec second,
        TutorialCardSpec third, TutorialCardSpec fourth, TutorialCardSpec fifth,
        TutorialCardSpec sixth, TutorialCardSpec seventh, bool landmark = false,
        bool italyCornerExitReady = false, int kantoCarry = 0)
    {
        var cards = new List<TutorialCardSpec>
        {
            first, second, third, fourth, fifth, sixth, seventh,
            Speed(2), Speed(1), Speed(3), Speed(2), Speed(1), Speed(3), Speed(2), Speed(1), Speed(2)
        };
        return new TutorialPlayerCheckpoint(step, cell, gear, 7 - heatHand,
            heatHand, 0, cards, cardSelection, consecutive, landmark, italyCornerExitReady,
            kantoCarry);
    }

    private static List<TutorialPlayerCheckpoint> CreatePlayerCheckpoints()
    {
        return new List<TutorialPlayerCheckpoint>
        {
            Checkpoint(TutorialStepId.HeatPayment, 24, 1, 7, 0, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(4), Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.HeatCardsAndCooling, 30, 1, 6, 1, 0, true,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.MissingCardPenalty, 34, 2, 2, 5, 0, true,
                Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.CornerLimitAndSpin, 8, 2, 7, 0, 6, true,
                Speed(3), Speed(2), Speed(1), Speed(1), Speed(2),
                Trick("uk-scone"), Trick("uk-english-breakfast-tea")),
            Checkpoint(TutorialStepId.Weather, 36, 1, 7, 0, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(4), Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.Slipstream, 40, 1, 7, 0, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(4), Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.PitSelection, 130, 2, 7, 0, 0, true,
                Speed(1), Speed(1), Speed(2), Speed(2), Speed(3),
                Trick("uk-scone"), Trick("uk-english-breakfast-tea")),
            Checkpoint(TutorialStepId.UkScone, 16, 1, 7, 0, 0, true,
                Trick("uk-scone"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Speed(4)),
            Checkpoint(TutorialStepId.UkEnglishBreakfastTea, 20, 1, 6, 1, 0, true,
                Trick("uk-english-breakfast-tea"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1))
        };
    }

    private static TutorialPlayerCheckpoint Checkpoint(
        TutorialStepId step,
        int playerCell,
        int gear,
        int normalHandSize,
        int heatInHand,
        int heatInDiscard,
        bool beginAtCardSelection = false,
        params TutorialCardSpec[] opening)
    {
        var exactOrder = new List<TutorialCardSpec>(opening);
        TutorialCardSpec[] repeatableFuture =
        {
            Speed(1), Speed(2), Speed(2), Speed(3), Speed(4), Speed(1),
            Trick("uk-scone"), Trick("uk-english-breakfast-tea"),
            Speed(3), Speed(2), Speed(1), Speed(2)
        };
        for (int i = 0; exactOrder.Count < 16; i++)
            exactOrder.Add(repeatableFuture[i % repeatableFuture.Length]);

        return new TutorialPlayerCheckpoint(
            step,
            playerCell,
            gear,
            normalHandSize,
            heatInHand,
            heatInDiscard,
            exactOrder,
            beginAtCardSelection);
    }

    private static TutorialCardSpec Speed(int value)
    {
        return new TutorialCardSpec(CardType.Speed, value);
    }

    private static TutorialCardSpec Trick(string trickId)
    {
        return new TutorialCardSpec(CardType.Trick, 0, trickId);
    }

    private static TutorialStepDefinition Step(
        TutorialStepId id,
        TutorialAction action,
        string sectionLabel,
        string title,
        string goal,
        string currentState,
        string actionPrompt,
        string successSignal,
        string recoveryHint,
        TutorialFocusTarget focusTarget,
        string focusIntroduction,
        string manualAdvanceLabel = "",
        bool allowManualAdvance = false)
    {
        return new TutorialStepDefinition(
            id,
            action,
            $"tutorial.step.{id}",
            sectionLabel,
            title,
            goal,
            currentState,
            actionPrompt,
            successSignal,
            recoveryHint,
            focusTarget,
            focusIntroduction,
            manualAdvanceLabel,
            allowManualAdvance);
    }
}
