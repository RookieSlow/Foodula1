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
            "狩猎本能", "输入锁定前方3格内最近车，真实尾流追近后额外+1，每圈一次；高阶范围4/5格，保留两段原生尾流", "决胜抉择", "前方 1–5 格有对手且至少有两张普通速度牌：弃一张，本次驾驶移动 +2；高阶 +3"),
        new DriverProfile("uk_nigel_mansell", "奈杰尔·雄狮·曼塞尔", "雄狮·曼塞尔", TeamId.UK, DriverStyle.Aggressive, 1.2f,
            "蓄势追近", "连续两次驾驶输入追近同一最近前车，每圈首次合格最多回收1张旧手永久热", "雄狮追击", "首段弃普通速度牌并付1热；下次继续追近同车可再付1热追击，不再消耗场次"),
        new DriverProfile("de_michael_schumacher", "米夏尔·教授·舒马赫", "教授·舒马赫", TeamId.DE, DriverStyle.Balanced, 1.5f,
            "工程师调校", "真实弯道付款满2热，下次本人首笔弯费-1；每圈一次，高阶门槛1热、等待更久", "三段战术", "弃速度牌以1/2挡准备；后两次本人输入3/4挡各付1热，移动+2（高阶+3）"),
        new DriverProfile("de_sebastian_vettel", "塞巴斯蒂安·海绵·维特尔", "海绵·维特尔", TeamId.DE, DriverStyle.Technical, 1.3f,
            "信息海绵", "每圈首次巡航反馈，下次输入回1旧手永久热；高阶含实付黑啤/德式出弯", "人车合拍", "付1永久热锁定巡航/已解锁黑啤/出弯，首次实效+2；高阶两次本人输入"),
        new DriverProfile("it_alberto_ascari", "阿尔贝托·蓝衣·阿斯卡里", "蓝衣·阿斯卡里", TeamId.IT, DriverStyle.Technical, 1.2f,
            "蓝色幸运", "连续无热量回合后提升下一回合弯道限速", "精密过弯", "本回合弯道超速热量支付 -1"),
        new DriverProfile("it_tazio_nuvolari", "塔齐奥·飞人·诺瓦", "飞人·诺瓦", TeamId.IT, DriverStyle.Aggressive, 1.0f,
            "不屈传奇", "本人输入时引擎余量≤2/3热，首个成功普通弯道支付后回收1/1/2张旧手牌永久热；普通罚热仍照付", "最后冲刺", "引擎余量≤2热且保留一次失控容错时发动：本回合仅免首弯限速并获+2/+3/+3移动，预付1次失控；Lv1/2恢复回合跳过，Lv3不跳过"),
        new DriverProfile("us_tony_stewart", "托尼·烟雾·斯图尔特", "烟雾·斯图尔特", TeamId.US, DriverStyle.Aggressive, 1.2f,
            "赛道怒火", "被超车后下回合第一张速度牌 +1", "真实自我", "短时间内直道速度 +2，但每回合产热"),
        new DriverProfile("us_kyle_busch", "凯尔·捣蛋鬼·布什", "捣蛋鬼·布什", TeamId.US, DriverStyle.Aggressive, 1.3f,
            "反派光环", "超越对手后对其施加热量", "挑衅鞠躬", "身后 3 格内有 1 名对手即可发动，提升直道速度并阻挡尾流"),
        new DriverProfile("cn_zhou_guanyu", "周·破风者·冠宇", "破风者·冠宇", TeamId.CN, DriverStyle.Balanced, 1.3f,
            "沉稳之心", "本人 Go 实付热后，普通冷却后回收最多1张旧手永久热；L2普通弯道实付热也可整备，L3最多2张", "稳守节奏", "牌阶段弃1张普通速度牌并回收旧手永久热；本次及后续本人输入可为冰果冻/土屋烟幕阻断的尾流补1格，次数1/1/2"),
        new DriverProfile("cn_ma_qinghua", "马·全能者·青骅", "全能者·青骅", TeamId.CN, DriverStyle.Technical, 1.1f,
            "全面适应", "上一笔真实 Recover 完成后，下一笔合格 Go 的首个弯限速 +1；高阶该弯超速热量 -1，每圈 1/1/2 次", "极限切换", "真实 Recover 冷却时留置 1 张旧手永久热；下一笔合格 Go 移动 +2/+3/+3，每场 1/1/2 次"),
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
