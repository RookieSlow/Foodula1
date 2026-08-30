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
    Review
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
            sections.Add($"<size=90%><color=#B9C7D8>没反应？{recoveryHint.Trim()}</color></size>");
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
                "起步", "欢迎来到勒芒",
                "欢迎来到围场，新人！赛车固然刺激，但驾驶起来并不困难。一步一步来，你就能成为冠军。",
                "现在，你正在驾驶属于UK的炸鱼薯条赛车。这辆传统的赛车很适合你冠军之旅的启程，它具有四个挡位，并有着均衡的性能。",
                "看看高光区域里的比赛状态。准备好后，我们就开始你的第一圈吧。",
                "五盏红灯熄灭！你可以上手了。接下来只需要跟着“轮到你了”完成操作。",
                "想看被面板挡住的位置，可先点“收起指引”，看完再展开。",
                TutorialFocusTarget.RaceStatus,
                "比赛状态：这里会告诉你当前阶段、圈数和下一件要做的事。",
                "开始第一回合", true),
            Step(TutorialStepId.TurnFlow, TutorialAction.CompleteTurnFlow,
                "基础驾驶", "起步",
                "一回合内的操作很简单：先选挡，再出牌。赛车移动后才会结算赛道效果，并进行弃牌和补牌。",
                "回合提示正在等你选择挡位，起步挡位是 G1。",
                "选 G1，再选 1 张速度牌并确认。",
                "提示会依次经过移动、赛道结算和回合清理，然后回到新回合。",
                "按钮暂时不可用通常代表动画还没结束，等提示变化即可。",
                TutorialFocusTarget.TurnPrompt,
                "回合提示：迷路时先看这里，它会告诉你现在处于哪个阶段。"),
            Step(TutorialStepId.GearAndRequiredCards, TutorialAction.SelectRequiredGearAndCards,
                "基础驾驶", "来一次换挡",
                "挡位指示了本回合必须打出的速度牌张数：G2 就是 2 张。如果你选择了过高的挡位而不能打出足量的牌，那就会受到惩罚，这一点我们稍后再说",
                "新的回合已经停在选挡阶段，G2 可以直接选择。",
                "先点 G2，再从手牌里选 2 张速度牌并确认。",
                "出牌计数会显示 2/2；接着我们观察这两张牌怎样驱动赛车。",
                "多选了一张就再点一次取消，留下恰好 2 张即可。",
                TutorialFocusTarget.GearControls,
                "挡位区：数字同时决定本回合需要打出的速度牌张数。"),
            Step(TutorialStepId.SpeedCardsAndMovement, TutorialAction.ResolveSpeedMovement,
                "基础驾驶", "打出速度牌让赛车前进",
                "速度牌很直观：本回合打出的数字相加，就是赛车的基础移动格数。",
                "刚才的两张牌已经确认，赛车正在按它们的总值移动。",
                "这一步不用再点。观察赛车沿高光赛道逐格前进。",
                "赛车落位后会记录起点、终点和总移动值。",
                "镜头还在跟车时耐心等一下，落位后教程会自动继续。",
                TutorialFocusTarget.Track,
                "赛道：赛车会按本回合速度牌总和逐格前进，落位后再结算赛道效果。"),
            Step(TutorialStepId.DeckHandDiscardAndRecycle, TutorialAction.InspectCardZonesAndRecycle,
                "卡牌循环", "看看牌去了哪里",
                "用过的牌不会消失：它们进入弃牌堆；抽牌堆空时，可用牌会按固定顺序回到抽牌堆。",
                "上回合的速度牌已经进入弃牌堆，底部手牌也补回了上限。",
                "沿着高光区域看一遍抽牌堆、引擎库和弃牌堆的数量，随后确认",
                "下一课会为热量机制准备一个干净的牌区状态。",
                "",
                TutorialFocusTarget.CardPiles,
                "牌区：从抽牌堆拿牌，用过后进弃牌堆；引擎库只保存可支付的热量。",
                "牌区已看懂", true),
            Step(TutorialStepId.HeatPayment, TutorialAction.PayHeat,
                "热量管理", "大幅升挡会让引擎升温",
                "从 G1 直接跳到 G3 很快，但代价是把 1 张引擎热量支付到手牌。",
                "赛车已停在 24 格和 G1；引擎库有 6 热量，手牌暂时都是普通牌。",
                "直接点 G3，留意高光区域的引擎数字怎样变化。",
                "引擎会从 6 变成 5，手牌里多出 1 张不能当速度使用的热量牌。",
                "先别出牌；若检查点仍在落位，等数字稳定后再点 G3。",
                TutorialFocusTarget.EngineHeat,
                "引擎库：这里的热量是可支付资源；支付后会变成占手牌的热量牌。"),
            Step(TutorialStepId.HeatCardsAndCooling, TutorialAction.CoolHeatCard,
                "热量管理", "低挡能把热量冷回引擎",
                "热量牌会占住手牌位置，也不能拿来移动；好消息是低挡能在反应阶段把它冷回引擎。",
                "赛车已在 G1，手牌里正好有 1 张热量牌和 6 张普通牌。",
                "保持 G1，只选 1 张速度牌并确认，然后等移动后的冷却阶段。",
                "高光中的热量牌会离开手牌，回到引擎库。",
                "不要选择热量牌；移动结束后它仍在手里时，再多等一会儿冷却结算。",
                TutorialFocusTarget.Hand,
                "热量牌：它会占住手牌但不能提供速度；低挡可以把它冷回引擎。"),
            Step(TutorialStepId.MissingCardPenalty, TutorialAction.TriggerMissingCardPenalty,
                "热量管理", "这次故意少打一张",
                "如果挡位要求没有满足，每缺 1 张速度牌就要额外支付 1 热量。我们用安全检查点看一次。",
                "当前是 G2，但手牌只有 1 张速度牌；引擎也刚好只剩 1 热量。",
                "打出这 1 张速度牌，然后点高光中的结束出牌按钮。",
                "系统会提示少 1 张，并把最后 1 张引擎热量支付到手牌。",
                "卡牌选中后还要点结束出牌，惩罚才会结算。",
                TutorialFocusTarget.ActionButton,
                "结束出牌：没凑齐挡位张数也能结束，但缺少的每张牌都要支付热量。"),
            Step(TutorialStepId.CornerLimitAndSpin, TutorialAction.ResolveCornerSpin,
                "赛道规则", "弯道不是越快越好",
                "穿过弯心时，总速度超过限速就要支付差额热量；付不起，赛车便会打转并回退。",
                "赛车已在 Dunlop 弯前 8 格，G2 手牌前两张是 3 和 2，弯心限速为 3。",
                "选 3 和 2 两张速度牌并确认，观察赛车穿过高光中的弯道。",
                "注意到热量不足以支付需要的数量了吗？这时赛车就打滑了",
                "",
                TutorialFocusTarget.Track,
                "赛道与弯心：弯心标出的数字是通过它时允许的总速度上限。"),
            Step(TutorialStepId.Weather, TutorialAction.ObserveWeatherEffect,
                "赛道规则", "天气也会改变比赛规则",
                "天气不是背景装饰：不同天气会改变弯道、尾流或冷却，所以每回合都值得看一眼。",
                "打转状态已经清除，脚本正把比赛切换为雨天。",
                "看高光中的天气标签，再对照弯道提示；雨天会让弯道更难控制。",
                "",
                "",
                TutorialFocusTarget.Weather,
                "天气：这里显示当前环境；它会真实改变弯道、尾流或冷却规则。",
                "天气规则已看懂", true),
            Step(TutorialStepId.Slipstream, TutorialAction.ResolveSlipstream,
                "赛道互动", "跟在前车后面吃尾流",
                "尾流只在所有赛车完成基础移动后检查，而且只帮助距离合适的后车。当回合结束时，你的车距离前车距离合适时，就会让你的赛车前进",
                "教学领航车会在结算前稳定停到你前方 2 格。",
                "跑一个普通 G1 回合：选 1 张速度牌并确认，然后看高光中的两辆赛车。",
                "回合末只有后方的 UK 获得额外移动，前方 JP 不会反向受益。",
                "",
                TutorialFocusTarget.Track,
                "两车距离：尾流在回合末判定，只把后车向前带，不会推动领航车。"),
            Step(TutorialStepId.PitSelection, TutorialAction.SelectPit,
                "维修区", "先预约，下一回合再维修",
                "进站分成两个时刻：入口前先做选择，越过入口后到下一回合才真正停靠。",
                "赛车已在 130 格和 G2，前两张速度牌是 1+1，教程入口位于 132 格。",
                "打出两张 1。维修选择出现后，点高光中的“预定进站”。",
                "系统只会记下进站意图，本回合仍会照常完成。",
                "",
                TutorialFocusTarget.PitChoice,
                "维修选择：现在只是预约进站，不会立刻停下或传送。"),
            Step(TutorialStepId.PitDelayedResolution, TutorialAction.ResolvePitOnNextTurn,
                "维修区", "让预约在下一回合生效",
                "预约不会打断当前回合。赛车先越过入口，下一回合开始时才执行停靠。",
                "进站意图已经登记，本回合还会继续穿过 132 格入口。",
                "让这一回合跑完；下一回合开始时先不要选牌，维修会自动执行。",
                "赛车会停一回合、冷却全部热量，再从教程出口前移。",
                "仍在当前回合就继续等移动和清理，不需要重置。",
                TutorialFocusTarget.TurnPrompt,
                "回合提示：当它进入下一回合时，预约的维修才会正式执行。"),
            Step(TutorialStepId.UkScone, TutorialAction.PlayUkScone,
                "UK 特殊牌", "尝一块司康，换一次冲刺",
                "特技牌是各个车队的特色之一，能够为赛场带来多变的因素，并提供特别的增益。\nUK 的司康是一张主动特殊牌：支付 1 热量，换取本回合额外 +2 移动。",
                "赛车已在 16 格，司康放进了手牌，引擎也准备好至少 1 热量。",
                "进入出牌阶段后点高光中的司康并确认，再照常结束本回合出牌。",
                "引擎支付 1 热量，本回合获得 +2 移动，司康随后进入弃牌堆。",
                "",
                TutorialFocusTarget.UkSconeCard,
                "司康：支付 1 引擎热量，让本回合额外前进 2 格。"),
            Step(TutorialStepId.UkEnglishBreakfastTea, TutorialAction.PlayUkEnglishBreakfastTea,
                "UK 特殊牌", "用英式早餐茶清理手牌热量",
                "英式早餐茶不加速，它会把 1 张手牌热量冷回引擎，为之后腾出空间。",
                "",
                "若还在上一回合，先用速度牌结束；下一回合点高光中的红茶并确认。",
                "1 张手牌热量会回到引擎，红茶本身进入弃牌堆。",
                "",
                TutorialFocusTarget.UkTeaCard,
                "英式早餐茶：把 1 张手牌热量送回引擎，不提供额外移动。"),
            Step(TutorialStepId.Review, TutorialAction.CompleteReview,
                "总结", "现在，把它们连成一圈",
                "你已经分别用过每个关键机制。接下来的一圈不再限制选择，让它们自然组合起来。",
                "天气已经回到阴天；刚才的引导状态不会带入自由练习。",
                "最后看一遍节奏：选挡定张数，速度牌定移动，随后结算热量、弯道、尾流与弃牌；维修在下一回合执行。",
                "确认后会重建 UK、勒芒、精确牌组和 6 热量，开始一整圈自由练习。",
                "练习中随时可以重新开始，也可以从面板重播引导。",
                TutorialFocusTarget.Review,
                "比赛全貌：每回合只需沿着“挡位 → 手牌 → 赛道结算 → 补牌”循环前进。",
                "重置并开始练习", true)
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
