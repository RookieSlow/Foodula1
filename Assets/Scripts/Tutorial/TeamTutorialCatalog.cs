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

    public TeamTutorialLessonDefinition(string title, string mechanic, string playerAction, string successSignal)
    {
        Title = title ?? string.Empty;
        Mechanic = mechanic ?? string.Empty;
        PlayerAction = playerAction ?? string.Empty;
        SuccessSignal = successSignal ?? string.Empty;
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
    public bool IsPlayable { get; }
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
        IsPlayable = isPlayable;
        Lessons = lessons ?? Array.Empty<TeamTutorialLessonDefinition>();
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

    private static TeamTutorialLessonDefinition Lesson(string title, string mechanic, string action, string success)
    {
        return new TeamTutorialLessonDefinition(title, mechanic, action, success);
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
            Lesson("司康冲刺", "从引擎支付 1 热量，本回合获得 2 格移动加成", "单独打出司康", "引擎 -1 热且移动加成 +2"),
            Lesson("英式红茶", "手牌中的 1 张热量回到引擎", "在预置热量手牌时单独打出红茶", "手牌热量 -1 且引擎 +1"),
            Lesson("完整练习", "科技和车手技能关闭，只保留真实特技效果", "独立跑完银石一圈", "不写入普通比赛奖励或成长"));
    }

    private static TeamTutorialCourseDefinition Germany()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-de-v1", TutorialCourseKind.TeamSpecialty, TeamId.DE, true,
            "DE · 精密巡航", "入门", "用直道低值牌保底、高耐久和较轻失控惩罚建立稳定节奏。",
            "nurburgring_bier", true,
            Lesson("直道保底", "直道上的速度 1 按 2 计算，弯道仍按原值", "在直道打出速度 1", "移动值显示保底加成"),
            Lesson("高耐久巡航", "8 热量容量和冷却 +1 提供更高容错", "连续完成升挡与冷却", "不因热量堵手而中断节奏"),
            Lesson("酸菜发酵", "经过弯道后 +2 移动，否则仅 +1", "在会跨弯的落点前使用", "看到条件差异而非固定奖励"),
            Lesson("黑面包垫底", "本回合下一次引擎热量支付减少 1，最低仍支付 1", "支付热量前使用", "热量减免只消费一次"));
    }

    private static TeamTutorialCourseDefinition Italy()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-it-v1", TutorialCourseKind.TeamSpecialty, TeamId.IT, true,
            "IT · 弯道节奏", "进阶", "利用操控 +2 和一次性出弯加速，在弯道密集赛道保持速度。",
            "monza_pasta", true,
            Lesson("操控优势", "弯道限速获得 +2 修正", "比较基础限速和最终限速", "能看懂限速明细中的车队修正"),
            Lesson("出弯加速", "完成过弯后，下一回合第一张速度牌 +1", "保留低值牌承接出弯", "加成只作用一次"),
            Lesson("帕尔马干酪", "本回合尾流额外 +2，总尾流达到 +4", "在尾流距离内使用并结束回合", "尾流阶段单独显示增强移动"),
            Lesson("基安蒂红酒", "弃 1 张速度牌并冷却 1 张热量", "用低价值速度牌换取冷却", "弃牌与冷却数量守恒"));
    }

    private static TeamTutorialCourseDefinition UnitedStates()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-us-v1", TutorialCourseKind.TeamSpecialty, TeamId.US, true,
            "US · 直线咆哮", "进阶", "掌握直道固定 +1、尾流 +1 与弯道额外热量之间的风险交换。",
            "indianapolis_burger", true,
            Lesson("直道爆发", "直道回合打出速度牌后，移动总值固定 +1", "分别用低档和高档验证", "加成每回合只出现一次"),
            Lesson("弯道代价", "弯道超速额外支付 1 热量", "打开限速明细并选择安全落点", "能提前预判额外热量"),
            Lesson("强化尾流", "车队尾流奖励比标准车队多 1", "留在前车尾流距离结束回合", "尾流阶段显示车队修正"),
            Lesson("地标组合", "经过地标后，薯条给限时热量，可乐额外抽牌", "跨过地标后使用对应牌", "限时资源按回合正确消失"));
    }

    private static TeamTutorialCourseDefinition China()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-cn-v1", TutorialCourseKind.TeamSpecialty, TeamId.CN, true,
            "CN · 电动双档", "专家", "用 Go/Recover 管理连续档位计数、热量、阴阳特技和维修区节奏。",
            "shanghai_dim_sum", true,
            Lesson("Go 节奏", "首次 Go 打 3 张；连续第二次起打 4 张并递增产热", "连续执行两次 Go 并观察计数", "能解释第四张牌与产热来源"),
            Lesson("Recover 回收", "只打 1 张，连续冷却效率按 3、2、1、0 递减", "切换 Recover 冷却手牌热量", "Go 连续计数被重置"),
            Lesson("火锅底料", "Go 下强化下一张正常速度牌：+1 且整张不计入弯道限速", "在弯前使用并选择目标速度牌", "移动增加但弯道计速排除该牌"),
            Lesson("冰糕与快充", "Recover 下阻止后车尾流；带维修区赛道可预约进站清热", "完成一次防尾流与进站", "能判断防守和重置热量的时机"));
    }

    private static TeamTutorialCourseDefinition Japan()
    {
        return new TeamTutorialCourseDefinition(
            "tutorial-team-jp-v1", TutorialCourseKind.TeamSpecialty, TeamId.JP, true,
            "JP · 时机博弈", "专家", "围绕鱼雷天妇罗的超车判断与关东慢煮的跨回合牌数累积制造爆发。",
            "suzuka_sushi", true,
            Lesson("关东慢煮", "G2 跳过当前回合，冷却一张手牌热量并留下 2 个牌槽", "先单独打出关东慢煮", "回合跳过且热量回引擎"),
            Lesson("蓄力释放", "下一回合 G1 可打出 3 张速度牌", "确认三张速度牌并结束出牌", "额外牌槽被本回合使用"),
            Lesson("鱼雷天妇罗", "本回合自己超车时移动 +1", "先打鱼雷，再用速度 3 超过领航车", "实际超车并前进 4 格"),
            Lesson("完整练习", "两张特技分别依赖跨回合牌槽与真实超车", "独立跑完铃鹿一圈", "不写入普通比赛奖励或成长"));
    }
}
