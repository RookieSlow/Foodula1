using System.Collections.Generic;

/// <summary>
/// Authored team specialty scenarios, separated from the shared scenario data model.
/// </summary>
public sealed partial class TutorialScenarioDefinition
{
    private static TutorialScenarioDefinition CreateJapanSpecialty()
    {
        var deck = new List<TutorialCardSpec>
        {
            Speed(1), Speed(2), Speed(2), Speed(3), Speed(1),
            Trick("jp-kanto-oden"), Trick("jp-torpedo-tempura"),
            Speed(2), Speed(1), Speed(3), Speed(2), Speed(1),
            Trick("jp-kanto-oden"), Speed(2), Trick("jp-torpedo-tempura"), Speed(3)
        };
        var steps = new List<TutorialStepDefinition>
        {
            Step(TutorialStepId.ObjectiveAndInterface, TutorialAction.AcknowledgeObjective,
                "专项训练", "JP 蓄力与超车", "今天练关东慢煮的跨回合牌槽，以及鱼雷天妇罗的超车奖励。",
                "赛道是铃鹿；科技和车手技能关闭，只有当前实装的专属特技生效。",
                "看清赛车与手牌后点击开始。", "先牺牲一个回合，为下一回合蓄力。",
                "需要时可回看上一步。", TutorialFocusTarget.RaceStatus,
                "先确认这堂课的两种进攻时机。", "开始训练", true),
            Step(TutorialStepId.JpKantoSkip, TutorialAction.ResolveJpKantoSkip,
                "专属特技", "关东慢煮：先蓄力", "G2 打出关东慢煮会跳过本回合，留下 2 个额外出牌槽给下回合。",
                "G2 已选好；关东慢煮与一张热量在手牌中，打出时还会冷却这张热量。",
                "只确认关东慢煮，不出速度牌；观察跳过回合和牌槽记录。",
                "本回合不移动，下回合有 2 个额外牌槽。", "请在任何速度牌之前打出关东慢煮。",
                TutorialFocusTarget.TeamTrickCard, "先单独确认关东慢煮。"),
            Step(TutorialStepId.JpKantoRelease, TutorialAction.ResolveJpKantoRelease,
                "跨回合牌槽", "把蓄力打出去", "上回合 G2 留下 2 个牌槽；本回合 G1 可确认 3 张速度牌。",
                "已经进入下一回合，G1 与三张速度牌已准备好。",
                "确认三张速度牌，再结束出牌，观察牌槽用尽。",
                "本次确认 3 张，额外牌槽仅在本回合有效。", "少出牌不会展示完整蓄力效果。",
                TutorialFocusTarget.Hand, "本回合请打满 3 张速度牌。"),
            Step(TutorialStepId.JpTorpedo, TutorialAction.ResolveJpTorpedo,
                "专属特技", "鱼雷天妇罗：瞄准超车", "当前版本中，鱼雷天妇罗在自己实际超车时给本车 +1 移动。",
                "领航车固定在前方两格的直道，鱼雷与速度 3 已在手牌中。",
                "先单独打鱼雷天妇罗，再打速度 3 并结束出牌，等车辆实际超车。",
                "赛车超过领航车，获得鱼雷额外 1 格。", "单纯打出鱼雷但没有超车不算完成。",
                TutorialFocusTarget.TeamTrickCard, "先找鱼雷，再用速度 3 越过前车。"),
            Step(TutorialStepId.Review, TutorialAction.CompleteReview,
                "自由练习", "独立跑完一圈", "用蓄力与超车选择自己的铃鹿节奏。",
                "练习不写入普通赛事奖励、科技或车手经验。", "点击下一步开始；可重练或退出。",
                "完成一圈后会显示反馈。", "别把条件触发牌当成固定加速。",
                TutorialFocusTarget.Review, "按你的判断完成这一圈。", "开始练习", true)
        };
        var checkpoints = new List<TutorialPlayerCheckpoint>
        {
            TeamCheckpoint(TutorialStepId.JpKantoSkip, 37, 2, 0, 1, true,
                Trick("jp-kanto-oden"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("jp-torpedo-tempura")),
            TeamCheckpoint(TutorialStepId.JpKantoRelease, 37, 1, 0, 0, true,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("jp-torpedo-tempura"), Trick("jp-kanto-oden"),
                kantoCarry: 2),
            TeamCheckpoint(TutorialStepId.JpTorpedo, 37, 1, 0, 0, true,
                Trick("jp-torpedo-tempura"), Speed(3), Speed(1), Speed(2), Speed(2), Speed(1), Trick("jp-kanto-oden"))
        };
        var opponents = new List<TutorialOpponentCue>
        {
            new TutorialOpponentCue(TutorialStepId.JpTorpedo, 39, 37, 2)
        };
        return new TutorialScenarioDefinition(deck, steps,
            new List<TutorialWeatherCue>(), opponents, checkpoints, null,
            "tutorial_team_jp_v1", "suzuka_sushi", TeamId.JP, true, TeamId.DE);
    }

    private static TutorialScenarioDefinition CreateUnitedKingdomSpecialty()
    {
        var deck = new List<TutorialCardSpec>
        {
            Speed(1), Speed(2), Speed(2), Speed(3), Speed(1),
            Trick("uk-scone"), Trick("uk-english-breakfast-tea"),
            Speed(2), Speed(1), Speed(3), Speed(2), Speed(1),
            Trick("uk-scone"), Speed(2), Trick("uk-english-breakfast-tea"), Speed(3)
        };
        var steps = new List<TutorialStepDefinition>
        {
            Step(TutorialStepId.ObjectiveAndInterface, TutorialAction.AcknowledgeObjective,
                "专项训练", "UK 热量换节奏", "今天只练英国队两张已实现的专属特技牌：司康与英式红茶。",
                "赛道是银石；科技和车手技能关闭，因此这堂课不演示成长增幅。",
                "看清引擎和手牌，然后点击开始训练。", "先用司康把一张引擎热量换成移动。",
                "随时可以回看上一步。", TutorialFocusTarget.RaceStatus,
                "先认识这次训练要交换的两种资源。", "开始训练", true),
            Step(TutorialStepId.UkSpecialtyScone, TutorialAction.ResolveUkSpecialtyScone,
                "专属特技", "司康冲刺", "司康从引擎支付 1 张热量，给本回合 +2 移动。",
                "G1 已选好，手牌左侧是司康；引擎里有足够的热量。",
                "只确认司康，观察引擎热量减少 1、本回合移动加成增加 2。",
                "两项变化都发生后才能继续。", "司康不能在引擎没有可支付热量时使用。",
                TutorialFocusTarget.UkSconeCard, "先找到司康，单独确认它。"),
            Step(TutorialStepId.UkSpecialtyTea, TutorialAction.ResolveUkSpecialtyTea,
                "专属特技", "英式红茶", "英式红茶把手牌中 1 张热量冷却回引擎。",
                "G1 已选好，红茶和一张热量已在手牌中。",
                "只确认英式红茶，观察手牌热量减少 1、引擎热量恢复 1。",
                "热量完成手牌到引擎的转移后才能继续。", "没有手牌热量时，红茶不能冷却。",
                TutorialFocusTarget.UkTeaCard, "先确认手牌里有热量，再打英式红茶。"),
            Step(TutorialStepId.Review, TutorialAction.CompleteReview,
                "自由练习", "独立跑完一圈", "把司康冲刺与红茶控热串成自己的银石节奏。",
                "练习不会写入奖励、科技或车手经验。", "点击下一步开始练习；可重练或退出。",
                "跑完一圈后会显示反馈。", "科技增幅要到普通比赛另行体验。",
                TutorialFocusTarget.Review, "现在按自己的判断完成这一圈。", "开始练习", true)
        };
        var checkpoints = new List<TutorialPlayerCheckpoint>
        {
            TeamCheckpoint(TutorialStepId.UkSpecialtyScone, 16, 1, 0, 0, true,
                Trick("uk-scone"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("uk-english-breakfast-tea")),
            TeamCheckpoint(TutorialStepId.UkSpecialtyTea, 20, 1, 0, 1, true,
                Trick("uk-english-breakfast-tea"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("uk-scone"))
        };
        return new TutorialScenarioDefinition(deck, steps,
            new List<TutorialWeatherCue>(), new List<TutorialOpponentCue>(), checkpoints, null,
            "tutorial_team_uk_v1", "silverstone_afternoon_tea", TeamId.UK, true, TeamId.DE);
    }

    private static TutorialScenarioDefinition CreateGermanySpecialty()
    {
        var deck = new List<TutorialCardSpec>
        {
            Speed(1), Speed(2), Speed(2), Speed(3), Speed(1),
            Trick("de-sauerkraut"), Trick("de-schwarzbrot"),
            Speed(2), Speed(1), Speed(3), Speed(2), Speed(1),
            Trick("de-sauerkraut"), Speed(2), Trick("de-schwarzbrot"), Speed(3)
        };
        var steps = new List<TutorialStepDefinition>
        {
            Step(TutorialStepId.ObjectiveAndInterface, TutorialAction.AcknowledgeObjective,
                "专项训练", "DE 精密巡航", "今天练德国车的直道保底和两张专属特技。",
                "引擎容量为 8；科技与车手增益关闭，车队固有性能保留。",
                "看清赛道和手牌后，点击开始训练。", "先用一张低值牌试试直道巡航。",
                "需要时可以回看上一步。", TutorialFocusTarget.RaceStatus,
                "先确认车队与训练目标。", "开始训练", true),
            Step(TutorialStepId.DeStraight, TutorialAction.ResolveDeStraight,
                "直道巡航", "低值牌也能跑稳", "直道上的速度 1 按 2 计算，另有车队直道 +1；弯道不享受这两项。",
                "车辆已停在纽博格林直道，手牌有速度 1。",
                "选择 G1，只打出 1 张速度 1，再结束出牌，等车辆移动结算。",
                "这张速度 1 的直道移动合计为 3。", "请不要同时选择其他速度牌。",
                TutorialFocusTarget.Hand, "先找到手牌里的速度 1。"),
            Step(TutorialStepId.DeSauerkraut, TutorialAction.ResolveDeSauerkraut,
                "专属特技", "酸菜发酵", "酸菜在本回合移动结算时生效：经过弯道 +2，否则 +1。",
                "车辆已在舒马赫 S 弯顶点前，手牌左侧是酸菜发酵。",
                "先单独打出酸菜发酵，再确认 1 张速度 1，结束出牌并等待车辆过弯。",
                "跨过弯顶点后移动 3 格：速度 1 加酸菜 2。", "酸菜出牌时只待命，过弯后才有移动反馈。",
                TutorialFocusTarget.TeamTrickCard, "先找到酸菜；再用速度 1 跨过前方弯顶点。"),
            Step(TutorialStepId.DeSchwarzbrot, TutorialAction.ResolveDeSchwarzbrot,
                "专属特技", "黑面包垫底", "黑面包让本回合下一次引擎热量支付少 1，但至少仍付 1。",
                "G3 已锁定，需要 3 张速度牌；手牌左侧是黑面包垫底。",
                "先单独打出黑面包，再确认 1 张速度 1，点击结束出牌。少 2 张通常需支付 2 热。",
                "本次实付 1 热，减免只触发一次。", "特技出牌本身不支付热量；要结束出牌才看得到。",
                TutorialFocusTarget.TeamTrickCard, "先打黑面包，再故意少出两张牌观察付热。"),
            Step(TutorialStepId.Review, TutorialAction.CompleteReview,
                "自由练习", "独立跑完一圈", "现在把直道节奏和两张特技串起来，跑完纽博格林一圈。",
                "练习不会写入奖励、科技或车手经验。", "点击下一步开始练习；可重练或退出。",
                "完成后会显示练习反馈。", "留意弯道，直道保底不等于弯道免罚。",
                TutorialFocusTarget.Review, "用你自己的节奏把这一圈跑完。", "开始练习", true)
        };
        var checkpoints = new List<TutorialPlayerCheckpoint>
        {
            TeamCheckpoint(TutorialStepId.DeStraight, 28, 1, 0, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("de-sauerkraut"), Trick("de-schwarzbrot")),
            TeamCheckpoint(TutorialStepId.DeSauerkraut, 26, 1, 0, 0, true,
                Trick("de-sauerkraut"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("de-schwarzbrot")),
            TeamCheckpoint(TutorialStepId.DeSchwarzbrot, 28, 3, 0, 0, true,
                Trick("de-schwarzbrot"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("de-sauerkraut"))
        };
        return new TutorialScenarioDefinition(deck, steps,
            new List<TutorialWeatherCue>(), new List<TutorialOpponentCue>(), checkpoints, null,
            "tutorial_team_de_v1", "nurburgring_bier", TeamId.DE, true, TeamId.UK);
    }

    private static TutorialScenarioDefinition CreateItalySpecialty()
    {
        var deck = new List<TutorialCardSpec>
        {
            Speed(1), Speed(2), Speed(2), Speed(3), Speed(1),
            Trick("it-parmigiano"), Trick("it-chianti"),
            Speed(2), Speed(1), Speed(3), Speed(2), Speed(1),
            Trick("it-parmigiano"), Speed(2), Trick("it-chianti"), Speed(3)
        };
        var steps = new List<TutorialStepDefinition>
        {
            Step(TutorialStepId.ObjectiveAndInterface, TutorialAction.AcknowledgeObjective,
                "专项训练", "IT 弯道节奏", "今天练意大利车的过弯操控、放大尾流和热量回收。",
                "科技和车手增益关闭，车队固有操控 +2、耐久 6 保留。",
                "看清赛车位置和手牌，点击开始。", "先平稳通过一处弯顶点。",
                "每一步都有固定的训练起点。", TutorialFocusTarget.RaceStatus,
                "先认识意大利队的弯道优势。", "开始训练", true),
            Step(TutorialStepId.ItCorner, TutorialAction.ResolveItCorner,
                "操控", "从容过弯", "意大利固有操控让最终弯道限速获得 +2，不能把它当成额外移动。",
                "赛车已停在蒙扎第一个弯顶点前。",
                "选择 G1，打一张速度 1，结束出牌并观察过弯结算。",
                "车辆安全越过弯顶点；留意限速明细里的车队修正。",
                "如果面板挡住限速，可先收起指引。", TutorialFocusTarget.Track,
                "操控修正作用于限速，不改变牌面速度。"),
            Step(TutorialStepId.ItCornerExit, TutorialAction.ResolveItCornerExit,
                "出弯加速", "把过弯势头带出去", "刚才安全过弯，意大利赛车已为下一次出牌存好 +1 移动。",
                "训练起点已经放到直道；这份加速只在下一次实际打出速度牌时使用一次。",
                "只打 1 张速度 1，结束出牌，观察赛车实际前进 2 格。",
                "速度牌给 1 格，出弯势头再给 1 格；用过就消失。",
                "空过一回合不会消耗这份加速。", TutorialFocusTarget.Hand,
                "看好速度 1：这回合它会带你前进 2 格。"),
            Step(TutorialStepId.ItParmigiano, TutorialAction.ResolveItParmigiano,
                "专属特技", "帕尔马干酪", "帕尔马让本回合尾流额外 +2；标准尾流 +2，因此成功跟车时合计 +4。",
                "领航车由训练脚本固定在前方，帕尔马干酪和速度 1 已在手牌中。",
                "先单独打帕尔马干酪，再确认 1 张速度 1，结束出牌，等回合末尾流。",
                "只有后车在基础移动后获得 +4 尾流；前车不会被向前推。",
                "若选错牌可取消选择，保持一张速度 1。", TutorialFocusTarget.TeamTrickCard,
                "先找干酪牌；尾流要到回合末才判定。"),
            Step(TutorialStepId.ItChianti, TutorialAction.ResolveItChianti,
                "专属特技", "基安蒂红酒", "打出红酒会弃掉一张手牌速度牌；若手牌有热量，同时冷却 1 张。",
                "手牌左侧是红酒，另有热量和可弃速度牌。",
                "只打出基安蒂红酒并确认，观察弃牌堆与引擎热量。",
                "一张速度牌进弃牌堆，一张热量回引擎。",
                "红酒不能凭空冷却：手牌没有热量时只弃速度牌。",
                TutorialFocusTarget.TeamTrickCard, "先确认手牌有速度牌和热量，再使用红酒。"),
            Step(TutorialStepId.Review, TutorialAction.CompleteReview,
                "自由练习", "独立跑完一圈", "把操控、跟车和热量回收串成自己的蒙扎节奏。",
                "练习不写入普通赛事奖励、科技或车手经验。",
                "点击下一步开始一圈练习；可重练或退出。",
                "跑完一圈后会显示反馈。", "尾流只属于符合距离的后车。",
                TutorialFocusTarget.Review, "按自己的判断完成这一圈。", "开始练习", true)
        };
        var checkpoints = new List<TutorialPlayerCheckpoint>
        {
            TeamCheckpoint(TutorialStepId.ItCorner, 8, 1, 0, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("it-parmigiano"), Trick("it-chianti")),
            TeamCheckpoint(TutorialStepId.ItCornerExit, 18, 1, 0, 0, true,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("it-parmigiano"), Trick("it-chianti"),
                italyCornerExitReady: true),
            TeamCheckpoint(TutorialStepId.ItParmigiano, 20, 1, 0, 0, true,
                Trick("it-parmigiano"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("it-chianti")),
            TeamCheckpoint(TutorialStepId.ItChianti, 24, 1, 0, 1, true,
                Trick("it-chianti"), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick("it-parmigiano"))
        };
        var opponents = new List<TutorialOpponentCue>
        {
            new TutorialOpponentCue(TutorialStepId.ItParmigiano, 22, 20, 1)
        };
        return new TutorialScenarioDefinition(deck, steps,
            new List<TutorialWeatherCue>(), opponents, checkpoints, null,
            "tutorial_team_it_v1", "monza_pasta", TeamId.IT, true, TeamId.DE);
    }
}
