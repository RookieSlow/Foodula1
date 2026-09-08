using System;
using System.Collections.Generic;

/// <summary>Driver play style from the driver design document.</summary>
public enum DriverStyle
{
    Aggressive,
    Balanced,
    Technical
}

/// <summary>
/// Immutable driver card data. Runtime skill execution deliberately lives in a
/// separate rules layer; this type is safe to use from menus, saves and tests.
/// </summary>
public sealed class DriverProfile
{
    public DriverProfile(
        string id,
        string displayName,
        string shortName,
        TeamId team,
        DriverStyle style,
        float talentMultiplier,
        string passiveName,
        string passiveSummary,
        string activeName,
        string activeSummary)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("Driver id is required.", nameof(id));
        if (string.IsNullOrEmpty(displayName)) throw new ArgumentException("Driver display name is required.", nameof(displayName));
        if (talentMultiplier <= 0f) throw new ArgumentOutOfRangeException(nameof(talentMultiplier));

        Id = id;
        DisplayName = displayName;
        ShortName = shortName ?? displayName;
        Team = team;
        Style = style;
        TalentMultiplier = talentMultiplier;
        PassiveName = passiveName ?? string.Empty;
        PassiveSummary = passiveSummary ?? string.Empty;
        ActiveName = activeName ?? string.Empty;
        ActiveSummary = activeSummary ?? string.Empty;
    }

    public string Id { get; }
    public string DisplayName { get; }
    public string ShortName { get; }
    public TeamId Team { get; }
    public DriverStyle Style { get; }
    public float TalentMultiplier { get; }
    public string PassiveName { get; }
    public string PassiveSummary { get; }
    public string ActiveName { get; }
    public string ActiveSummary { get; }
}

/// <summary>Shared XP and unlock rules for all drivers.</summary>
public static class DriverProgression
{
    private static readonly int[] LevelThresholds = { 0, 100, 250, 500, 1000, 2000, 4000 };

    public static IReadOnlyList<int> XpThresholds => LevelThresholds;

    public static int GetLevel(int xp)
    {
        int clampedXp = Math.Max(0, xp);
        int level = 1;
        for (int i = 1; i < LevelThresholds.Length; i++)
        {
            if (clampedXp < LevelThresholds[i]) break;
            level = i + 1;
        }

        return level;
    }

    public static int GetPassiveTier(int level)
    {
        if (level < 2) return 0;
        return Math.Min(3, ((level - 2) / 2) + 1);
    }

    public static int GetActiveTier(int level)
    {
        if (level < 3) return 0;
        return Math.Min(3, ((level - 3) / 2) + 1);
    }

    public static int GetActiveUsesPerRace(int level, TeamId team)
    {
        int uses = GetActiveTier(level) == 0 ? 0 : (level >= 7 ? 2 : 1);
        if (team == TeamId.UK && uses > 0) uses += 1;
        return uses;
    }

    /// <summary>
    /// Calculates the documented post-race XP reward before persistence.
    /// Finish position 0 means the race was not completed and earns no XP.
    /// </summary>
    public static int CalculateRaceXp(int finishPosition, float talentMultiplier, TeamId team)
    {
        if (finishPosition <= 0 || talentMultiplier <= 0f) return 0;

        int placementBonus;
        switch (finishPosition)
        {
            case 1: placementBonus = 100; break;
            case 2: placementBonus = 60; break;
            case 3: placementBonus = 30; break;
            default: placementBonus = 10; break;
        }

        double reward = (50 + placementBonus) * talentMultiplier;
        if (team == TeamId.UK) reward *= 1.2;
        return Math.Max(0, (int)Math.Round(reward, MidpointRounding.AwayFromZero));
    }
}

/// <summary>Authoritative catalog for the twelve drivers in the design document.</summary>
public static class DriverCatalog
{
    private static readonly DriverProfile[] Profiles =
    {
        new DriverProfile("uk_hunter_hart", "詹姆斯·猎人·哈特", "猎人·哈特", TeamId.UK, DriverStyle.Aggressive, 1.3f,
            "狩猎本能", "落后前车 3 格内时速度牌 +1", "最后的圈速", "最后一圈速度提升，但热量加倍"),
        new DriverProfile("uk_nigel_mansell", "奈杰尔·雄狮·曼塞尔", "雄狮·曼塞尔", TeamId.UK, DriverStyle.Aggressive, 1.2f,
            "雄狮之心", "成功超车后下回合移动 +1；高阶额外冷却 1", "愤怒冲锋", "本回合获得额外移动，强势完成超车"),
        new DriverProfile("de_michael_schumacher", "米夏尔·教授·舒马赫", "教授·舒马赫", TeamId.DE, DriverStyle.Balanced, 1.5f,
            "工程师调校", "每 3 回合下一次热量支付 -1；高阶周期缩短", "完美一圈", "本回合不产生弯道热量"),
        new DriverProfile("de_sebastian_vettel", "塞巴斯蒂安·海绵·维特尔", "海绵·维特尔", TeamId.DE, DriverStyle.Technical, 1.3f,
            "信息海绵", "每 4 回合从牌库顶 3 张中优先抽取最高速度牌", "完美节奏", "持续期间跨两挡不产生换挡热量"),
        new DriverProfile("it_alberto_ascari", "阿尔贝托·蓝衣·阿斯卡里", "蓝衣·阿斯卡里", TeamId.IT, DriverStyle.Technical, 1.2f,
            "蓝色幸运", "连续无热量回合后提升下一回合弯道限速", "精密过弯", "本回合弯道超速热量支付 -1"),
        new DriverProfile("it_tazio_nuvolari", "塔齐奥·飞人·诺瓦", "飞人·诺瓦", TeamId.IT, DriverStyle.Aggressive, 1.0f,
            "不屈传奇", "热量达到警告区时速度牌 +1", "最后冲刺", "爆缸边缘时无视弯道限速并强制打转"),
        new DriverProfile("us_tony_stewart", "托尼·烟雾·斯图尔特", "烟雾·斯图尔特", TeamId.US, DriverStyle.Aggressive, 1.2f,
            "赛道怒火", "被超车后下回合第一张速度牌 +1", "真实自我", "短时间内直道速度 +2，但每回合产热"),
        new DriverProfile("us_kyle_busch", "凯尔·捣蛋鬼·布什", "捣蛋鬼·布什", TeamId.US, DriverStyle.Aggressive, 1.3f,
            "反派光环", "超越对手后对其施加热量", "挑衅鞠躬", "身后 3 格内有 1 名对手即可发动，提升直道速度并阻挡尾流"),
        new DriverProfile("cn_zhou_guanyu", "周·破风者·冠宇", "破风者·冠宇", TeamId.CN, DriverStyle.Balanced, 1.3f,
            "沉稳之心", "每回合首次天气弯道惩罚 -1", "中国速度", "短时间免疫天气及对手效果造成的负面修正"),
        new DriverProfile("cn_ma_qinghua", "马·全能者·青骅", "全能者·青骅", TeamId.CN, DriverStyle.Technical, 1.1f,
            "全面适应", "每回合首次进入弯道时限速 +1；高阶首次弯道超速热量 -1", "极限切换", "切换档位时的惩罚减半"),
        new DriverProfile("jp_keiichi_tsuchiya", "土屋·漂移之王·圭一", "漂移之王·圭一", TeamId.JP, DriverStyle.Technical, 1.2f,
            "漂移本能", "弯道出弯后下回合第一张速度牌 +1", "烟雾弹", "短时间阻断身后对手的尾流"),
        new DriverProfile("jp_takumi_fujiwara", "藤原·豆腐小子·拓海", "豆腐小子·拓海", TeamId.JP, DriverStyle.Technical, 1.0f,
            "水杯训练", "连续无热量回合后速度牌提升", "排水沟过弯", "本回合无视指定弯道限速，不再附带移动惩罚")
    };

    private static readonly Dictionary<string, DriverProfile> ById = BuildIndex();

    public static IReadOnlyList<DriverProfile> All => Profiles;

    public static bool TryGet(string driverId, out DriverProfile profile)
    {
        if (!string.IsNullOrEmpty(driverId) && ById.TryGetValue(driverId, out profile)) return true;
        profile = null;
        return false;
    }

    public static DriverProfile GetDefaultForTeam(TeamId team)
    {
        for (int i = 0; i < Profiles.Length; i++)
        {
            if (Profiles[i].Team == team) return Profiles[i];
        }

        return Profiles[0];
    }

    private static Dictionary<string, DriverProfile> BuildIndex()
    {
        var index = new Dictionary<string, DriverProfile>(StringComparer.Ordinal);
        for (int i = 0; i < Profiles.Length; i++) index.Add(Profiles[i].Id, Profiles[i]);
        return index;
    }
}
