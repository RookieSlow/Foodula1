using System;
using System.Collections.Generic;

/// <summary>
/// China and United States specialty course authoring.
/// </summary>
public sealed partial class TutorialScenarioDefinition
{
    public static TutorialScenarioDefinition CreateTeamSpecialty(TeamId team)
    {
        if (team == TeamId.JP)
            return CreateJapanSpecialty();
        if (team == TeamId.UK)
            return CreateUnitedKingdomSpecialty();
        if (team == TeamId.DE)
            return CreateGermanySpecialty();
        if (team == TeamId.IT)
            return CreateItalySpecialty();
        if (team != TeamId.CN && team != TeamId.US)
            throw new ArgumentOutOfRangeException(nameof(team), "Unknown specialty team.");

        bool china = team == TeamId.CN;
        string firstTrick = china ? "cn-hotpot-base" : "us-fries";
        string secondTrick = china ? "cn-ice-jelly" : "us-cola";
        var deck = new List<TutorialCardSpec>
        {
            Speed(1), Speed(2), Speed(2), Speed(3), Speed(1),
            Trick(firstTrick), Trick(secondTrick),
            Speed(2), Speed(1), Speed(3), Speed(2), Speed(1),
            Trick(firstTrick), Speed(2), Trick(secondTrick), Speed(3)
        };
        var steps = new List<TutorialStepDefinition>
        {
            Step(TutorialStepId.ObjectiveAndInterface, TutorialAction.AcknowledgeObjective,
                "专项训练", china ? "CN 电动双档" : "US 直线咆哮",
                china ? "这堂课练 Go/Recover 的牌数与热量，以及两张专属特技牌。" :
                    "这堂课练直道加速、弯道代价、尾流与地标特技牌。",
                "科技和车手技能关闭，车队固有特性保留；所有示范使用固定牌序。",
                "阅读后点击下一步。每个操作都在真实对局中完成。",
                "现在开始专项驾驶。", "随时可回看上一步。",
                TutorialFocusTarget.RaceStatus, "先确认你的车队和训练目标。", "开始训练", true)
        };
        var checkpoints = new List<TutorialPlayerCheckpoint>();
        var opponents = new List<TutorialOpponentCue>();
        if (china)
        {
            steps.Add(Step(TutorialStepId.ChinaFirstGo, TutorialAction.CompleteChinaFirstGo,
                "双档动力", "第一次 Go：三张牌", "Go 首次需要 3 张速度牌。",
                "当前是 Recover，手牌已准备。", "选择 Go，打出 3 张速度牌并结束出牌。",
                "需求显示 3/3。", "热量牌不能充当速度牌。",
                TutorialFocusTarget.GearControls, "Go 第一次需要三张速度牌。"));
            steps.Add(Step(TutorialStepId.ChinaConsecutiveGo, TutorialAction.CompleteChinaConsecutiveGo,
                "双档动力", "连续 Go：四张牌", "第二次连续 Go 需要 4 张速度牌，并支付 1 热量。",
                "已准备一次 Go 后的安全状态。", "再次选择 Go，打满 4 张速度牌。",
                "确认 4/4，并观察引擎支付的热量。", "换成 Recover 会重置 Go 计数。",
                TutorialFocusTarget.Hand, "看清需求从 3 张变为 4 张。"));
            steps.Add(Step(TutorialStepId.ChinaRecover, TutorialAction.CompleteChinaRecover,
                "双档动力", "Recover 冷却", "Recover 只需 1 张牌，首次可冷却 3 张热量。",
                "手牌有热量，上一回合处于连续 Go。", "选择 Recover，打 1 张速度牌并完成回合。",
                "热量回到引擎，连续 Go 计数重置。", "等待移动后的反应阶段。",
                TutorialFocusTarget.GearControls, "Recover 是重置过热节奏的方式。"));
            steps.Add(Step(TutorialStepId.ChinaHotpot, TutorialAction.PlayChinaHotpot,
                "车队特技", "火锅底料 ATTACK", "Go 下让下一张速度牌 +1，并将整张牌从过弯计速中排除。",
                "Go 和火锅底料已准备好。", "先只打火锅底料，再单独选中 1 张速度牌并确认，观察 ATTACK 标记。不要一次提交多张。",
                "只有目标速度牌获得 ATTACK 标记，不会凭空增加一张牌。", "特技牌本身不占速度牌数。",
                TutorialFocusTarget.TeamTrickCard, "先打特技，再选受强化的速度牌。"));
            steps.Add(Step(TutorialStepId.ChinaIceJelly, TutorialAction.PlayChinaIceJelly,
                "车队特技", "冰糕防守", "Recover 下打出冰糕，可阻止身后赛车享受你的尾流。",
                "赛车已切换到 Recover，冰糕就在手牌中。", "先只打冰糕特技牌；看到防尾流反馈后再继续，不要提前结束出牌。",
                "后车不能借你的尾流。", "冰糕只有在 Recover 下生效。",
                TutorialFocusTarget.TeamTrickCard, "在 Recover 中打出冰糕。"));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.ChinaFirstGo, 42, 1, 0, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(firstTrick), Trick(secondTrick)));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.ChinaConsecutiveGo, 43, 2, 1, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(firstTrick), Trick(secondTrick)));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.ChinaRecover, 44, 2, 2, 3, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(firstTrick), Trick(secondTrick)));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.ChinaHotpot, 45, 2, 1, 0, true,
                Trick(firstTrick), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(secondTrick)));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.ChinaIceJelly, 46, 1, 1, 0, true,
                Trick(secondTrick), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(firstTrick)));
        }
        else
        {
            steps.Add(Step(TutorialStepId.UsStraight, TutorialAction.ResolveUsStraight,
                "直线性能", "每回合直道 +1", "在直道打出速度牌后，总移动额外 +1，每回合只加一次。",
                "已置于直道，手牌中有多张速度 1。", "选择 G1，只打出 1 张速度 1，然后点击“确认出牌”。其他牌不要选。",
                "基础 1 加车队 1，共前进 2 格。", "不论打几张牌，加成仍只有一次。",
                TutorialFocusTarget.Hand, "美国队的直道加速是回合固定值。"));
            steps.Add(Step(TutorialStepId.UsCorner, TutorialAction.ResolveUsCorner,
                "风险交换", "弯道额外热量", "超速过弯除了差额热量，还要多支付 1 热量。",
                "训练状态位于弯道前，手牌左侧依次有速度 3 和速度 2。", "选择 G2，只打出速度 3 + 速度 2 两张牌，然后点击“确认出牌”。不要选速度 1。",
                "区分基础超速差额与车队额外热量。", "引擎热量不足可能导致失控。",
                TutorialFocusTarget.Track, "过弯前检查限速明细。"));
            steps.Add(Step(TutorialStepId.UsSlipstream, TutorialAction.ResolveUsSlipstream,
                "位置博弈", "强化尾流", "美国队在正常尾流奖励上再加 1。",
                "前车位置由训练检查点固定，手牌中有速度 1。", "选择 G1，只打出 1 张速度 1，然后点击“确认出牌”。不要选速度 3。",
                "只有后车获得额外移动。", "尾流不是出牌时立刻触发。",
                TutorialFocusTarget.Track, "尾流在所有赛车基础移动后单独结算。"));
            steps.Add(Step(TutorialStepId.UsFries, TutorialAction.PlayUsFries,
                "地标特技", "薯条", "上回合经过地标后，薯条提供本回合限时热量。",
                "训练状态保留了上回合地标标记，薯条已在手牌左侧。", "只点击薯条特技牌，再点击“确认出牌”；本步不需要速度牌。",
                "限时热量只在本回合有效。", "没经过地标时不能发动。",
                TutorialFocusTarget.TeamTrickCard, "先确认地标条件，再打薯条。"));
            steps.Add(Step(TutorialStepId.UsCola, TutorialAction.PlayUsCola,
                "地标特技", "可乐", "上回合经过地标后，可乐额外抽 1 张牌。",
                "训练状态保留了上回合地标标记，可乐已在手牌左侧。", "只点击可乐特技牌，再点击“确认出牌”；本步不需要速度牌。",
                "抽到的牌立即进入手牌。", "没有地标标记时不能发动。",
                TutorialFocusTarget.TeamTrickCard, "可乐是在地标之后补牌。"));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.UsStraight, 15, 1, 0, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(firstTrick), Trick(secondTrick)));
            // The US straight bonus is deliberately excluded from corner-speed
            // resolution.  Starting at cell 1 would therefore make 3+2 end
            // at the first corner's entry cell (6) and skip its apex (7).
            // Cell 2 keeps the authored lesson immediately before the corner
            // while guaranteeing that the base speed 5 crosses the apex.
            checkpoints.Add(TeamCheckpoint(TutorialStepId.UsCorner, 2, 2, 0, 0, true,
                Speed(3), Speed(2), Speed(1), Speed(1), Speed(2), Trick(firstTrick), Trick(secondTrick)));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.UsSlipstream, 18, 1, 0, 0, false,
                Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(firstTrick), Trick(secondTrick)));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.UsFries, 25, 1, 0, 0, true,
                Trick(firstTrick), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(secondTrick), true));
            checkpoints.Add(TeamCheckpoint(TutorialStepId.UsCola, 28, 1, 0, 0, true,
                Trick(secondTrick), Speed(1), Speed(2), Speed(2), Speed(3), Speed(1), Trick(firstTrick), true));
            opponents.Add(new TutorialOpponentCue(TutorialStepId.UsSlipstream, 22, 18, 2));
        }

        steps.Add(Step(TutorialStepId.Review, TutorialAction.CompleteReview,
            "自由练习", "独立跑完一圈", "引导结束后重建固定牌组与热量，独立跑一圈。",
            "练习不会写入奖励、科技或车手经验。", "点击下一步，按自己的判断驾驶。",
            "完成一圈后可重练或返回菜单。", "教练一直在这里。",
            TutorialFocusTarget.Review, "把刚才学到的车队节奏串起来。", "开始练习", true));

        return new TutorialScenarioDefinition(deck, steps,
            new List<TutorialWeatherCue>(), opponents, checkpoints, null,
            china ? "tutorial_team_cn_v1" : "tutorial_team_us_v1",
            china ? "shanghai_dim_sum" : "indianapolis_burger",
            team, true, china ? TeamId.US : TeamId.CN);
    }
}
