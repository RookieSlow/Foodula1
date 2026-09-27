using System;
using System.Collections.Generic;

public enum TutorialCourseKind
{
    Foundation,
    TeamSpecialty
}

public sealed class TeamTutorialLessonDefinition
{
    public string Title { get; }
    public string Mechanic { get; }
    public string PlayerAction { get; }
    public string SuccessSignal { get; }
    public IReadOnlyList<TutorialStepId> ScenarioStepIds { get; }

    public TeamTutorialLessonDefinition(
        string title, string mechanic, string playerAction, string successSignal,
        params TutorialStepId[] scenarioStepIds)
    {
        Title = title ?? string.Empty;
        Mechanic = mechanic ?? string.Empty;
        PlayerAction = playerAction ?? string.Empty;
        SuccessSignal = successSignal ?? string.Empty;
        ScenarioStepIds = Array.AsReadOnly(scenarioStepIds == null
            ? Array.Empty<TutorialStepId>()
            : (TutorialStepId[])scenarioStepIds.Clone());
    }
}

/// <summary>
/// Menu-facing course metadata. Courses describe only mechanics that exist in
/// runtime code; future mechanics must not be advertised before implementation.
/// </summary>
public sealed class TeamTutorialCourseDefinition
{
    public string Id { get; }
    public TutorialCourseKind Kind { get; }
    public TeamId Team { get; }
    public bool HasTeam { get; }
    public string DisplayName { get; }
    public string Difficulty { get; }
    public string Summary { get; }
    public string RecommendedTrackId { get; }
    private readonly bool configuredPlayable;
    public bool IsPlayable => configuredPlayable &&
        (Kind != TutorialCourseKind.TeamSpecialty || TeamTutorialCatalog.IsScenarioAligned(this));
    public IReadOnlyList<TeamTutorialLessonDefinition> Lessons { get; }

    public TeamTutorialCourseDefinition(
        string id, TutorialCourseKind kind, TeamId team, bool hasTeam,
        string displayName, string difficulty, string summary,
        string recommendedTrackId, bool isPlayable,
        params TeamTutorialLessonDefinition[] lessons)
    {
        Id = id ?? throw new ArgumentNullException(nameof(id));
        Kind = kind;
        Team = team;
        HasTeam = hasTeam;
        DisplayName = displayName ?? string.Empty;
        Difficulty = difficulty ?? string.Empty;
        Summary = summary ?? string.Empty;
        RecommendedTrackId = recommendedTrackId ?? string.Empty;
        configuredPlayable = isPlayable;
        Lessons = Array.AsReadOnly(lessons == null
            ? Array.Empty<TeamTutorialLessonDefinition>()
            : (TeamTutorialLessonDefinition[])lessons.Clone());
    }
}

public static class TeamTutorialCatalog
{
    private static readonly IReadOnlyList<TeamTutorialCourseDefinition> Courses =
        new List<TeamTutorialCourseDefinition>
        {
            Foundation(), UnitedKingdom(), Germany(), Italy(),
            UnitedStates(), China(), Japan()
        }.AsReadOnly();

    public static IReadOnlyList<TeamTutorialCourseDefinition> All => Courses;

    public static TeamTutorialCourseDefinition GetForTeam(TeamId team)
    {
        for (int i = 0; i < Courses.Count; i++)
        {
            if (Courses[i].HasTeam && Courses[i].Team == team)
                return Courses[i];
        }
        return null;
    }

    public static bool IsScenarioAligned(TeamTutorialCourseDefinition course)
    {
        if (course == null || !course.HasTeam || course.Kind != TutorialCourseKind.TeamSpecialty)
            return false;

        TutorialScenarioDefinition scenario;
        try
        {
            scenario = TutorialScenarioDefinition.CreateTeamSpecialty(course.Team);
        }
        catch (ArgumentOutOfRangeException)
        {
            return false;
        }

        var expected = new HashSet<TutorialStepId>();
        for (int i = 0; i < scenario.steps.Count; i++)
        {
            TutorialStepId stepId = scenario.steps[i].id;
            if (stepId != TutorialStepId.ObjectiveAndInterface)
                expected.Add(stepId);
        }

        if (expected.Count == 0)
            return false;

        var mapped = new HashSet<TutorialStepId>();
        for (int lessonIndex = 0; lessonIndex < course.Lessons.Count; lessonIndex++)
        {
            TeamTutorialLessonDefinition lesson = course.Lessons[lessonIndex];
            if (lesson == null)
                return false;

            IReadOnlyList<TutorialStepId> lessonSteps = lesson.ScenarioStepIds;
            if (lessonSteps == null || lessonSteps.Count == 0)
                return false;

            for (int stepIndex = 0; stepIndex < lessonSteps.Count; stepIndex++)
            {
                TutorialStepId stepId = lessonSteps[stepIndex];
                if (!expected.Contains(stepId) || !mapped.Add(stepId))
                    return false;
            }
        }

        return mapped.SetEquals(expected);
    }

    private static TeamTutorialLessonDefinition Lesson(
        string title, string mechanic, string action, string success,
        params TutorialStepId[] scenarioStepIds)
    {
        return new TeamTutorialLessonDefinition(title, mechanic, action, success, scenarioStepIds);
    }

    private static TeamTutorialCourseDefinition Foundation()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-foundation-le-mans-uk-v1", TutorialCourseKind.Foundation,
            TeamId.UK, false, "基础新手教程", "入门",
            "使用无科技加成的 UK 赛车，在勒芒依次学习挡位、出牌、热量、弯道、天气、尾流与维修区。",
            TutorialScenarioDefinition.TrackId, true,
            Lesson("比赛基本流程", "选挡、出牌、移动与回合清理", "完成一个受引导回合", "能独立完成选挡与出牌"),
            Lesson("风险管理", "热量、缺牌惩罚与弯道限速", "完成支付、冷却和过弯演示", "能读懂热量及限速反馈"),
            Lesson("赛道机制", "天气、尾流与维修区", "完成三个脚本化演示", "能判断何时利用或规避机制"),
            Lesson("完整练习", "把所有基础规则串联", "独立跑完一圈", "完成教程练习圈"));
    }

    private static TeamTutorialCourseDefinition UnitedKingdom()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-uk-v1", TutorialCourseKind.TeamSpecialty, TeamId.UK, true,
            "UK · 热量换节奏", "入门", "在银石练习司康冲刺与英式红茶控热；科技和车手增幅不在隔离教程中生效。",
            "silverstone_afternoon_tea", true,
            Lesson("司康冲刺", "从引擎支付 1 热量，本回合获得 2 格移动加成", "单独打出司康", "引擎 -1 热且移动加成 +2", TutorialStepId.UkSpecialtyScone),
            Lesson("英式红茶", "手牌中的 1 张热量回到引擎", "在预置热量手牌时单独打出红茶", "手牌热量 -1 且引擎 +1", TutorialStepId.UkSpecialtyTea),
            Lesson("完整练习", "科技和车手技能关闭，只保留真实特技效果", "独立跑完银石一圈", "不写入普通比赛奖励或成长", TutorialStepId.Review));
    }

    private static TeamTutorialCourseDefinition Germany()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-de-v1", TutorialCourseKind.TeamSpecialty, TeamId.DE, true,
            "DE · 精密巡航", "入门", "用直道低值牌保底、高耐久和较轻失控惩罚建立稳定节奏。",
            "nurburgring_bier", true,
            Lesson("直道保底", "直道上的速度 1 按 2 计算，弯道仍按原值", "在直道打出速度 1", "移动值显示保底加成", TutorialStepId.DeStraight),
            Lesson("酸菜发酵", "经过弯道后 +2 移动，否则仅 +1", "在会跨弯的落点前使用", "看到条件差异而非固定奖励", TutorialStepId.DeSauerkraut),
            Lesson("黑面包垫底", "本回合下一次引擎热量支付减少 1，最低仍支付 1", "支付热量前使用", "热量减免只消费一次", TutorialStepId.DeSchwarzbrot),
            Lesson("完整练习", "把直道保底与两张专属特技串联", "独立跑完纽博格林一圈", "不写入普通比赛奖励或成长", TutorialStepId.Review));
    }

    private static TeamTutorialCourseDefinition Italy()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-it-v1", TutorialCourseKind.TeamSpecialty, TeamId.IT, true,
            "IT · 弯道节奏", "进阶", "利用操控 +2 和一次性出弯加速，在弯道密集赛道保持速度。",
            "monza_pasta", true,
            Lesson("操控优势", "弯道最终限速获得车队 +2 修正", "选择 G1，只打 1 张速度 1 并观察过弯", "限速明细显示车队修正，赛车安全越过弯顶点", TutorialStepId.ItCorner),
            Lesson("出弯加速", "完成过弯后，下一回合第一张速度牌 +1", "只打 1 张速度 1，再结束出牌", "速度 1 加一次性出弯加速，共前进 2 格", TutorialStepId.ItCornerExit),
            Lesson("帕尔马干酪", "本回合尾流额外 +2，总尾流达到 +4", "先单独打帕尔马，再打 1 张速度 1 并结束回合", "回合末只有符合距离的后车获得 +4 尾流", TutorialStepId.ItParmigiano),
            Lesson("基安蒂红酒", "弃 1 张速度牌；若手牌有热量，再冷却 1 张", "只打基安蒂红酒，观察手牌中的速度牌与热量", "速度牌进入弃牌堆；可用热量回到引擎", TutorialStepId.ItChianti),
            Lesson("完整练习", "把操控、出弯、尾流与热量回收串联", "独立跑完蒙扎一圈", "不写入普通比赛奖励或成长", TutorialStepId.Review));
    }

    private static TeamTutorialCourseDefinition UnitedStates()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-us-v1", TutorialCourseKind.TeamSpecialty, TeamId.US, true,
            "US · 直线咆哮", "进阶", "掌握直道固定 +1、尾流 +1 与弯道额外热量之间的风险交换。",
            "indianapolis_burger", true,
            Lesson("直道爆发", "直道回合打出速度牌后，移动总值固定 +1", "选择 G1，只打 1 张速度 1", "基础 1 加车队 1，共前进 2 格", TutorialStepId.UsStraight),
            Lesson("弯道代价", "弯道超速额外支付 1 热量", "选择 G2，只打速度 3 + 速度 2", "区分超速差额与车队额外 1 热量", TutorialStepId.UsCorner),
            Lesson("强化尾流", "车队尾流奖励比标准车队多 1", "选择 G1，只打 1 张速度 1 并等待回合末", "只有符合距离的后车获得增强尾流", TutorialStepId.UsSlipstream),
            Lesson("薯条", "经过地标后提供本回合限时热量", "只打薯条特技牌", "限时热量只在本回合有效", TutorialStepId.UsFries),
            Lesson("可乐", "经过地标后额外抽 1 张牌", "只打可乐特技牌", "抽到的牌立即进入手牌", TutorialStepId.UsCola),
            Lesson("完整练习", "把直道、弯道、尾流与地标特技串联", "独立跑完印第安纳波利斯一圈", "不写入普通比赛奖励或成长", TutorialStepId.Review));
    }

    private static TeamTutorialCourseDefinition China()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-cn-v1", TutorialCourseKind.TeamSpecialty, TeamId.CN, true,
            "CN · 电动双档", "专家", "在上海练习 Go/Recover 的牌数与热量，再用火锅底料和冰糕体验进攻与防守。",
            "shanghai_dim_sum", true,
            Lesson("Go 节奏", "首次 Go 打 3 张；连续第二次起打 4 张并递增产热", "依次完成首次 Go 与连续 Go，观察 3/4 张要求和产热", "首 Go 为 3/3；连续 Go 为 4/4 并支付过载热量", TutorialStepId.ChinaFirstGo, TutorialStepId.ChinaConsecutiveGo),
            Lesson("Recover 回收", "只打 1 张；连续冷却效率按 3、2、1、0 递减", "选择 Recover，打 1 张速度牌并结束回合", "热量回到引擎，Go 连续计数重置", TutorialStepId.ChinaRecover),
            Lesson("火锅底料", "Go 下强化下一张正常速度牌：+1 且整张不计入弯道限速", "先打火锅底料，再单独确认 1 张速度牌", "目标牌获得 ATTACK 标记，不增加必出牌数", TutorialStepId.ChinaHotpot),
            Lesson("冰糕防守", "Recover 下阻止后车享受你的尾流", "在 Recover 下只打冰糕特技牌", "系统记录防尾流状态，前车不受影响", TutorialStepId.ChinaIceJelly),
            Lesson("完整练习", "把 Go/Recover 与两张专属特技串联", "独立跑完上海一圈", "不写入普通比赛奖励或成长", TutorialStepId.Review));
    }

    private static TeamTutorialCourseDefinition Japan()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-jp-v1", TutorialCourseKind.TeamSpecialty, TeamId.JP, true,
            "JP · 时机博弈", "专家", "围绕鱼雷天妇罗的超车判断与关东慢煮的跨回合牌数累积制造爆发。",
            "suzuka_sushi", true,
            Lesson("关东慢煮", "G2 跳过当前回合，冷却一张手牌热量并留下 2 个牌槽", "先单独打出关东慢煮", "回合跳过且热量回引擎", TutorialStepId.JpKantoSkip),
            Lesson("蓄力释放", "下一回合 G1 可打出 3 张速度牌", "确认三张速度牌并结束出牌", "额外牌槽被本回合使用", TutorialStepId.JpKantoRelease),
            Lesson("鱼雷天妇罗", "本回合自己超车时移动 +1", "先打鱼雷，再用速度 3 超过领航车", "实际超车并前进 4 格", TutorialStepId.JpTorpedo),
            Lesson("完整练习", "两张特技分别依赖跨回合牌槽与真实超车", "独立跑完铃鹿一圈", "不写入普通比赛奖励或成长", TutorialStepId.Review));
    }
}
