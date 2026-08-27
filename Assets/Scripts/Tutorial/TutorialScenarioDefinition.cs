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
    Review
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
    CompletePracticeLap
}

public enum TutorialRunPhase
{
    Guided,
    Practice,
    Completed,
    Exited
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
    public string title;
    public string instruction;
    public bool allowManualAdvance;

    public TutorialStepDefinition(
        TutorialStepId id,
        TutorialAction requiredAction,
        string instructionKey,
        string title = "",
        string instruction = "",
        bool allowManualAdvance = false)
    {
        this.id = id;
        this.requiredAction = requiredAction;
        this.instructionKey = instructionKey;
        this.title = title ?? string.Empty;
        this.instruction = instruction ?? string.Empty;
        this.allowManualAdvance = allowManualAdvance;
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
    public IReadOnlyList<TutorialCardSpec> exactDrawOrder;

    public TutorialPlayerCheckpoint(
        TutorialStepId step,
        int playerCell,
        int gear,
        int normalHandSize,
        int heatInHand,
        int heatInDiscard,
        IReadOnlyList<TutorialCardSpec> exactDrawOrder)
    {
        this.step = step;
        this.playerCell = playerCell;
        this.gear = gear;
        this.normalHandSize = normalHandSize;
        this.heatInHand = heatInHand;
        this.heatInDiscard = heatInDiscard;
        this.exactDrawOrder = exactDrawOrder;
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
public sealed class TutorialScenarioDefinition
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
        TutorialPitLaneDefinition tutorialPitLane)
    {
        id = ScenarioId;
        trackId = TrackId;
        playerTeam = TeamId.UK;
        openingHandSize = 7;
        engineHeatCapacity = 6;
        opponentCount = 1;
        opponentTeam = TeamId.JP;
        guidedStartWeatherId = "sunny";
        practiceWeatherId = "cloudy";
        techTreeEnabled = false;
        driverSkillsEnabled = false;
        normalRewardsEnabled = false;
        normalProgressionWritesEnabled = false;
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

    public static TutorialScenarioDefinition CreateLeMansUk()
    {
        var drawOrder = new List<TutorialCardSpec>
        {
            Speed(1), Speed(2), Speed(2), Speed(3), Speed(4), Speed(1), Trick("uk-scone"),
            Speed(3), Trick("uk-english-breakfast-tea"), Speed(2), Speed(1), Speed(3),
            Trick("uk-english-breakfast-tea"), Speed(2), Speed(2), Trick("uk-scone")
        };

        var steps = new List<TutorialStepDefinition>
        {
            Step(TutorialStepId.ObjectiveAndInterface, TutorialAction.AcknowledgeObjective,
                "目标与界面", "先完成一圈勒芒。留意左侧操作、底部手牌、右侧牌堆与热量，以及顶部天气和圈数。", true),
            Step(TutorialStepId.TurnFlow, TutorialAction.CompleteTurnFlow,
                "回合流程", "完成一个完整回合：选挡、抽牌、出牌、移动、弯道/尾流结算、弃牌与清理。"),
            Step(TutorialStepId.GearAndRequiredCards, TutorialAction.SelectRequiredGearAndCards,
                "挡位与出牌张数", "选择挡位并打满该挡位要求的速度牌张数；挡位决定本回合最多且必须打出的速度牌数。"),
            Step(TutorialStepId.SpeedCardsAndMovement, TutorialAction.ResolveSpeedMovement,
                "速度牌与移动", "确认速度牌后观察数值总和如何转化为逐格移动。"),
            Step(TutorialStepId.DeckHandDiscardAndRecycle, TutorialAction.InspectCardZonesAndRecycle,
                "牌库、手牌与弃牌", "查看右侧牌库/弃牌数量：打出的速度牌在回合清理时进入弃牌堆，牌库耗尽后按确定顺序回收。", true),
            Step(TutorialStepId.HeatPayment, TutorialAction.PayHeat,
                "支付热量", "检查点已恢复为 G1 和 6 张引擎热量。切换到 G3，观察跨两挡支付 1 张热量到手牌。"),
            Step(TutorialStepId.HeatCardsAndCooling, TutorialAction.CoolHeatCard,
                "热量牌与冷却", "检查点保证手牌有 1 张热量。选择 G1 并完成回合；冷却会把热量送回引擎，热量牌不能当速度牌。"),
            Step(TutorialStepId.MissingCardPenalty, TutorialAction.TriggerMissingCardPenalty,
                "缺牌惩罚", "检查点只提供 1 张速度牌。选择 G2，只打这 1 张后确认；缺少的 1 张会支付最后 1 份引擎热量。"),
            Step(TutorialStepId.CornerLimitAndSpin, TutorialAction.ResolveCornerSpin,
                "弯道限速与打转", "检查点位于 Dunlop 弯前且引擎已空。选择 G2 并打出 3+2；越过限速 3 的弯心时无法支付全部热量，将确定性打转。"),
            Step(TutorialStepId.Weather, TutorialAction.ObserveWeatherEffect,
                "天气影响", "脚本已切换为雨天。雨天会修改弯道和失控风险；天气规则来自正常比赛系统。", true),
            Step(TutorialStepId.Slipstream, TutorialAction.ResolveSlipstream,
                "回合末尾流", "本步骤会在尾流结算前把教学领航车放到前方 2 格。只有后车在回合结束判定并获得移动。"),
            Step(TutorialStepId.PitSelection, TutorialAction.SelectPit,
                "维修区预定", "教程检查点位于虚拟入口前 2 格。选择 G2、打出两张 1，出现提示时预定进站；选择本身不会立即传送或停靠。"),
            Step(TutorialStepId.PitDelayedResolution, TutorialAction.ResolvePitOnNextTurn,
                "维修区延迟执行", "本回合继续越过入口；预定只会变为待执行状态。下一回合开始才停靠、冷却全部热量并从教程出口前移。"),
            Step(TutorialStepId.UkScone, TutorialAction.PlayUkScone,
                "UK 特殊牌：司康", "检查点保证司康在手且引擎可支付。打出司康，支付 1 张引擎热量并获得 +2 移动。"),
            Step(TutorialStepId.UkEnglishBreakfastTea, TutorialAction.PlayUkEnglishBreakfastTea,
                "UK 特殊牌：英式早餐茶", "下一回合检查点保证红茶和 1 张热量在手。打出英式红茶，将这张手牌热量冷却回引擎。"),
            Step(TutorialStepId.Review, TutorialAction.CompleteReview,
                "总结复习", "回顾：挡位控制张数，速度牌决定移动，热量在引擎与牌区间守恒，弯道、天气、尾流和维修区都在固定时机结算。", true)
        };

        return new TutorialScenarioDefinition(
            drawOrder,
            steps,
            new List<TutorialWeatherCue>
            {
                new TutorialWeatherCue(TutorialStepId.Weather, "rain"),
                new TutorialWeatherCue(TutorialStepId.Review, "cloudy")
            },
            new List<TutorialOpponentCue>
            {
                new TutorialOpponentCue(TutorialStepId.Slipstream, 42, 40, 2)
            },
            CreatePlayerCheckpoints(),
            new TutorialPitLaneDefinition(entryCell: 132, exitCell: 4));
    }

    private static List<TutorialPlayerCheckpoint> CreatePlayerCheckpoints()
    {
        return new List<TutorialPlayerCheckpoint>
        {
            Checkpoint(TutorialStepId.HeatPayment, 24, 1, 7, 0, 0,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(4), Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.HeatCardsAndCooling, 30, 1, 6, 1, 0,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.MissingCardPenalty, 34, 2, 2, 5, 0,
                Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.CornerLimitAndSpin, 8, 2, 7, 0, 6,
                Speed(3), Speed(2), Speed(1), Speed(1), Speed(2),
                Trick("uk-scone"), Trick("uk-english-breakfast-tea")),
            Checkpoint(TutorialStepId.Weather, 36, 1, 7, 0, 0,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(4), Speed(1), Trick("uk-scone")),
            Checkpoint(TutorialStepId.PitSelection, 130, 2, 7, 0, 0,
                Speed(1), Speed(1), Speed(2), Speed(2), Speed(3),
                Trick("uk-scone"), Trick("uk-english-breakfast-tea")),
            Checkpoint(TutorialStepId.UkScone, 16, 1, 7, 0, 0,
                Trick("uk-scone"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Speed(4)),
            Checkpoint(TutorialStepId.UkEnglishBreakfastTea, 20, 1, 6, 1, 0,
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
            exactOrder);
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
        string title,
        string instruction,
        bool allowManualAdvance = false)
    {
        return new TutorialStepDefinition(
            id,
            action,
            $"tutorial.step.{id}",
            title,
            instruction,
            allowManualAdvance);
    }
}
