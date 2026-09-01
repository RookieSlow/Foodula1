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
    public IReadOnlyList<TutorialCardSpec> exactDrawOrder;

    public TutorialPlayerCheckpoint(
        TutorialStepId step,
        int playerCell,
        int gear,
        int normalHandSize,
        int heatInHand,
        int heatInDiscard,
        IReadOnlyList<TutorialCardSpec> exactDrawOrder,
        bool beginAtCardSelection = false)
    {
        this.step = step;
        this.playerCell = playerCell;
        this.gear = gear;
        this.normalHandSize = normalHandSize;
        this.heatInHand = heatInHand;
        this.heatInDiscard = heatInDiscard;
        this.exactDrawOrder = exactDrawOrder;
        this.beginAtCardSelection = beginAtCardSelection;
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
                "起步", "欢迎加入车队",
                "欢迎来到围场，新车手。我是你的教练，今天陪你跑完第一堂勒芒训练。别急，我们一次只练一个动作。",
                "你驾驶的是 UK 炸鱼薯条赛车。它有四个挡位，性能均衡，很适合用来熟悉比赛节奏。",
                "先看看高光里的比赛状态。准备好了，就和我一起开始第一回合。",
                "很好，赛车已经就位。之后跟着“轮到你了”操作就行，我会在每个关键点提醒你。",
                "如果面板挡住了想看的位置，可以先收起指引，看清后再展开。",
                TutorialFocusTarget.RaceStatus,
                "比赛状态在这里。它会告诉你当前阶段、圈数，以及接下来该做什么。",
                "开始第一回合", true),
            Step(TutorialStepId.TurnFlow, TutorialAction.CompleteTurnFlow,
                "基础驾驶", "先跑一个完整回合",
                "先记住最基本的节奏：选挡、出牌，然后看赛车移动和赛道结算。回合最后才会处理弃牌与补牌。",
                "现在轮到你起步，G1 是最稳妥的选择。",
                "选择 G1，再挑 1 张速度牌确认。慢慢来，先把完整流程走一遍。",
                "做得好。赛车会完成移动、赛道结算和回合清理，然后回到新的回合。",
                "按钮暂时不能点时，通常只是动画还在播放，等回合提示更新即可。",
                TutorialFocusTarget.TurnPrompt,
                "回合提示就像我们的战术板。拿不准下一步时，先看这里。"),
            Step(TutorialStepId.GearAndRequiredCards, TutorialAction.SelectRequiredGearAndCards,
                "基础驾驶", "试着升到 G2",
                "挡位决定本回合需要打出的速度牌张数：G2 就要打 2 张。挡位越高，选择越大胆，也越需要确保手牌跟得上。",
                "新回合已经准备好，我们来练一次稳稳的升挡。",
                "选择 G2，再从手牌中挑 2 张速度牌确认。",
                "看到 2/2 就说明配合正确。接下来一起看看它们怎样推动赛车。",
                "如果不小心多选了，再点一次那张牌就能取消。留下正好 2 张即可。",
                TutorialFocusTarget.GearControls,
                "挡位区在这里。挡位数字，就是本回合要打出的速度牌张数。"),
            Step(TutorialStepId.SpeedCardsAndMovement, TutorialAction.ResolveSpeedMovement,
                "基础驾驶", "看看速度怎样变成距离",
                "速度牌上的数字会相加，合计值就是赛车这一回合的基础移动格数。规则不复杂，关键是学会估算落点。",
                "你刚才选的两张牌已经确认，赛车正在执行这次移动。",
                "这一步不用操作，跟着镜头观察赛车沿赛道前进。",
                "赛车落位后，我们就能看到这组速度牌带来的实际距离。",
                "镜头还在跟车时稍等一下，赛车停稳后训练会自动继续。",
                TutorialFocusTarget.Track,
                "看赛道上的落点。赛车先按速度总和前进，停稳后才结算赛道效果。"),
            Step(TutorialStepId.DeckHandDiscardAndRecycle, TutorialAction.InspectCardZonesAndRecycle,
                "卡牌循环", "整理一下我们的牌区",
                "用过的牌不会丢失，它们会进入弃牌堆。抽牌堆用完后，可用牌会重新回到抽牌堆，继续为后面的回合服务。",
                "刚才打出的速度牌已经收进弃牌堆，手牌也补回了上限。",
                "看看高光区域里的抽牌堆、引擎库和弃牌堆数量。看明白后告诉我，我们继续。",
                "很好。接下来我会把牌区整理好，带你认识引擎热量。",
                "",
                TutorialFocusTarget.CardPiles,
                "这是牌区：牌从抽牌堆来到手中，用过后进入弃牌堆；引擎库保存可支付的热量。",
                "牌区已看懂", true),
            Step(TutorialStepId.HeatPayment, TutorialAction.PayHeat,
                "热量管理", "大胆升挡，也要照顾引擎",
                "从 G1 直接升到 G3 能快速提速，但引擎要为这次大幅换挡支付 1 热量。支付后的热量牌会进入手牌。",
                "赛车已经停在 24 格和 G1，引擎库有 6 热量，手牌状态也准备好了。",
                "这次直接选择 G3，注意观察高光中的引擎数字。",
                "你会看到引擎从 6 变成 5，手牌里多出 1 张不能用来移动的热量牌。",
                "如果赛车和数字还在调整，先等画面稳定，再选择 G3。",
                TutorialFocusTarget.EngineHeat,
                "这里是引擎库。可用热量从这里支付，支付后就会变成占用手牌的热量牌。"),
            Step(TutorialStepId.HeatCardsAndCooling, TutorialAction.CoolHeatCard,
                "热量管理", "给引擎一点喘息时间",
                "热量牌会占住手牌位置，也不能提供速度。别担心，低挡行驶时可以在反应阶段把它冷回引擎。",
                "新回合已经准备好：赛车在 G1，手牌里有 1 张热量牌和 6 张普通牌。",
                "挡位已经替你选好。挑 1 张速度牌确认，然后观察移动后的冷却。",
                "很好，那张热量牌会离开手牌，重新回到引擎库。",
                "热量牌不能用来移动。如果移动结束后它还在手里，稍等冷却阶段完成。",
                TutorialFocusTarget.Hand,
                "这就是热量牌。它占用手牌但不提供速度，低挡可以把它冷回引擎。"),
            Step(TutorialStepId.MissingCardPenalty, TutorialAction.TriggerMissingCardPenalty,
                "热量管理", "安全地看一次缺牌代价",
                "如果没有满足挡位要求，每缺 1 张速度牌就要额外支付 1 热量。训练时犯错没关系，我们用准备好的状态看清这条规则。",
                "现在是 G2，但手牌只有 1 张速度牌，引擎也只剩 1 热量。",
                "挡位已经锁定。打出唯一的速度牌，再点击高光中的结束出牌。",
                "系统会提示缺少 1 张牌，并把最后 1 热量支付到手牌。",
                "选好速度牌后，别忘了点击结束出牌，缺牌结算才会开始。",
                TutorialFocusTarget.ActionButton,
                "结束出牌可以提前结束选择，但每缺少 1 张挡位要求的牌，都要支付 1 热量。"),
            Step(TutorialStepId.CornerLimitAndSpin, TutorialAction.ResolveCornerSpin,
                "赛道规则", "快车手也要懂得收弯",
                "通过弯心时，总速度超过限速就要支付差额热量。如果引擎付不起，赛车会打转并回退。这里我们安全地演示一次。",
                "赛车已在 Dunlop 弯前 8 格，挡位是 G2，前两张速度牌为 3 和 2，弯心限速为 3。",
                "挡位已经选好。打出 3 和 2，观察赛车通过高光弯道时发生什么。",
                "看到了吗？需要支付的热量超过引擎余量时，赛车就会打滑。正式比赛里要给自己留余地。",
                "",
                TutorialFocusTarget.Track,
                "弯心旁的数字就是允许通过的总速度上限。进弯前记得先估算速度。"),
            Step(TutorialStepId.Weather, TutorialAction.ObserveWeatherEffect,
                "赛道规则", "出发前，也要抬头看看天气",
                "天气不只是赛道背景。它会影响弯道、尾流或冷却，所以有经验的车手每回合都会看一眼。",
                "刚才的打转状态已经清除，训练场现在切换到了雨天。",
                "看看高光中的天气标签，再对照弯道提示。雨天会让弯道更难控制。",
                "",
                "",
                TutorialFocusTarget.Weather,
                "天气显示在这里。它会真正改变弯道、尾流或冷却规则。",
                "天气规则已看懂", true),
            Step(TutorialStepId.Slipstream, TutorialAction.ResolveSlipstream,
                "赛道互动", "学会借前车的风",
                "尾流会在所有赛车完成基础移动后判定，而且只帮助距离合适的后车。跟住前车，往往能多争取一点距离。",
                "训练领航车会在结算前停到你前方 2 格，给你一个稳定的练习机会。",
                "跑一个普通 G1 回合：选择 1 张速度牌确认，然后观察高光里的两辆赛车。",
                "回合末只有后方的 UK 获得额外移动，前方 JP 不会被反向推动。漂亮，这就是尾流。",
                "",
                TutorialFocusTarget.Track,
                "留意两车距离：尾流在回合末判定，只会把符合条件的后车向前带。"),
            Step(TutorialStepId.PitSelection, TutorialAction.SelectPit,
                "维修区", "提前告诉维修组你的计划",
                "进站要提前预约：入口前做出选择，赛车越过入口后，要到下一回合才真正停靠。维修组也需要准备时间。",
                "赛车在 130 格和 G2，前两张速度牌是 1+1，训练入口位于 132 格。",
                "挡位已经选好。打出两张 1；维修选择出现后，点击高光中的“预定进站”。",
                "很好，系统会记下进站意图，本回合仍会照常完成。",
                "",
                TutorialFocusTarget.PitChoice,
                "这里是在预约进站。赛车不会立刻停下，也不会突然改变位置。"),
            Step(TutorialStepId.PitDelayedResolution, TutorialAction.ResolvePitOnNextTurn,
                "维修区", "维修组会在下一回合接车",
                "预约不会打断当前回合。赛车先越过入口，等下一回合开始时才会执行停靠。",
                "进站意图已经登记，本回合还会继续通过 132 格入口。",
                "让这一回合自然跑完。下一回合开始时不用操作，维修会自动执行。",
                "赛车将停一回合、冷却全部热量，再从训练出口重新上路。",
                "如果还在当前回合，就耐心等移动和清理完成，不需要重新开始。",
                TutorialFocusTarget.TurnPrompt,
                "看着回合提示。进入下一回合时，预约的维修才会正式执行。"),
            Step(TutorialStepId.UkScone, TutorialAction.PlayUkScone,
                "UK 特殊牌", "来尝尝车队的司康",
                "每支车队都有自己的拿手战术。UK 的司康是一张主动特技牌：支付 1 热量，换取本回合额外 +2 移动。用对时机，它就是一次漂亮的冲刺。",
                "赛车已在 16 格，司康就在手牌里，引擎也留好了至少 1 热量。",
                "挡位已经选好。点击高光中的司康确认，再用速度牌正常结束本回合出牌。",
                "不错。引擎支付 1 热量，本回合获得 +2 移动，司康随后进入弃牌堆。",
                "",
                TutorialFocusTarget.UkSconeCard,
                "这张是司康：支付 1 引擎热量，让本回合额外前进 2 格。"),
            Step(TutorialStepId.UkEnglishBreakfastTea, TutorialAction.PlayUkEnglishBreakfastTea,
                "UK 特殊牌", "喝口早餐茶，整理好引擎",
                "英式早餐茶不会直接加速，但能把 1 张手牌热量冷回引擎，为后面的回合腾出空间。稳住节奏和冲刺同样重要。",
                "新回合已经准备好，红茶和可冷却的热量牌都在手中。",
                "到了下一回合，挡位已经选好。点击高光中的早餐茶确认，再用速度牌完成出牌。",
                "很好，1 张手牌热量回到引擎，早餐茶本身进入弃牌堆。",
                "",
                TutorialFocusTarget.UkTeaCard,
                "这是英式早餐茶：把 1 张手牌热量送回引擎，但不提供额外移动。"),
            Step(TutorialStepId.Review, TutorialAction.CompleteReview,
                "总结", "很好，现在跑一圈给我看看",
                "关键机制你都亲手练过了。接下来这一圈不再限制选择，把刚学到的判断自然地连起来。",
                "天气已经恢复阴天，刚才用于演示的状态不会带进自由练习。",
                "最后记住节奏：挡位决定张数，速度牌决定移动；随后处理热量、弯道、尾流与弃牌，预约维修则在下一回合执行。",
                "确认后会重建 UK 赛车、勒芒赛道、精确牌组和 6 热量。放松一点，按自己的判断跑完一圈。",
                "训练中随时可以重新开始，也可以从面板重播引导。教练一直在这里。",
                TutorialFocusTarget.Review,
                "整场比赛就是“挡位 → 手牌 → 赛道结算 → 补牌”的循环。稳稳跑好每一个回合。",
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
