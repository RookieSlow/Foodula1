using System.Collections.Generic;

/// <summary>
/// Factory for creating the built-in TechTreeDatabase with all ~36 tech nodes.
///
/// Design: hardcoded C# for rapid iteration. Can be ported to JSON loading later
/// via TechTreeDatabase.LoadFromJson(TextAsset).
/// </summary>
public static class TechTreeDatabaseFactory
{
    /// <summary>
    /// Create the complete built-in tech tree database for all 6 countries.
    /// </summary>
    public static TechTreeDatabase CreateDefault()
    {
        var db = new TechTreeDatabase();

        AddCommonL1(db);
        AddCommonL2(db);
        AddCnEvVariants(db);

        AddUK(db);
        AddDE(db);
        AddIT(db);
        AddUS(db);
        AddCN(db);
        AddJP(db);

        return db;
    }

    // ═══════════════════════════════════════════════════════════════════
    // L1 Common (4 nodes)
    // ═══════════════════════════════════════════════════════════════════

    static void AddCommonL1(TechTreeDatabase db)
    {
        db.Add(new TechNodeDef(
            id: "common-l1-heat-coating",
            name: "耐热涂层",
            nameEn: "Heat-Resistant Coating",
            tier: TechTreeTier.L1,
            teamId: null,
            index: 1,
            rpCost: 2500,
            prerequisites: new string[0],
            upgradesTo: "common-l2-ceramic-coating",
            effects: new[] { new TechEffect(TechEffectType.HeatReductionPerLap, 1) },
            description: "每圈1次，弯道超速判定时产生的热量-1（最少为1）"
        ));

        db.Add(new TechNodeDef(
            id: "common-l1-lightweight-chassis",
            name: "轻量化底盘",
            nameEn: "Lightweight Chassis",
            tier: TechTreeTier.L1,
            teamId: null,
            index: 2,
            rpCost: 2000,
            prerequisites: new string[0],
            upgradesTo: "common-l2-carbon-fiber",
            effects: new[] { new TechEffect(TechEffectType.SpeedBonusStraight, 1) },
            description: "每回合打出的速度牌数值总和+1（仅直道）"
        ));

        db.Add(new TechNodeDef(
            id: "common-l1-track-memory",
            name: "赛道记忆",
            nameEn: "Track Memory",
            tier: TechTreeTier.L1,
            teamId: null,
            index: 3,
            rpCost: 3000,
            prerequisites: new string[0],
            upgradesTo: "common-l2-extreme-handling",
            effects: new[] { new TechEffect(TechEffectType.CornerLimitBonus, 1) },
            description: "弯心限速+1"
        ));

        db.Add(new TechNodeDef(
            id: "common-l1-expanded-tank",
            name: "扩容油箱",
            nameEn: "Expanded Fuel Tank",
            tier: TechTreeTier.L1,
            teamId: null,
            index: 4,
            rpCost: 3500,
            prerequisites: new string[0],
            upgradesTo: "common-l2-reserve-tank",
            effects: new[] { new TechEffect(TechEffectType.DurabilityBonus, 1) },
            description: "耐久+1"
        ));
    }

    // ═══════════════════════════════════════════════════════════════════
    // L2 Common (5 nodes)
    // ═══════════════════════════════════════════════════════════════════

    static void AddCommonL2(TechTreeDatabase db)
    {
        db.Add(new TechNodeDef(
            id: "common-l2-ceramic-coating",
            name: "陶瓷隔热层",
            nameEn: "Ceramic Thermal Barrier",
            tier: TechTreeTier.L2,
            teamId: null,
            index: 1,
            rpCost: 3500,
            prerequisites: new[] { "common-l1-heat-coating" },
            upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.HeatReductionPerLap, 2) },
            description: "每圈1次，弯道超速判定时产生的热量-2（最少为1）。升级覆盖L1#1。"
        ));

        db.Add(new TechNodeDef(
            id: "common-l2-carbon-fiber",
            name: "碳纤维车身",
            nameEn: "Carbon Fiber Body",
            tier: TechTreeTier.L2,
            teamId: null,
            index: 2,
            rpCost: 3000,
            prerequisites: new[] { "common-l1-lightweight-chassis" },
            upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.SpeedBonusStraight, 2) },
            description: "速度牌数值总和+2（仅直道）。升级覆盖L1#2。"
        ));

        db.Add(new TechNodeDef(
            id: "common-l2-extreme-handling",
            name: "极限操控",
            nameEn: "Extreme Handling",
            tier: TechTreeTier.L2,
            teamId: null,
            index: 3,
            rpCost: 4000,
            prerequisites: new[] { "common-l1-track-memory" },
            upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.CornerLimitBonus, 2) },
            description: "弯心限速+2。升级覆盖L1#3。"
        ));

        db.Add(new TechNodeDef(
            id: "common-l2-reserve-tank",
            name: "备用油箱",
            nameEn: "Reserve Fuel Tank",
            tier: TechTreeTier.L2,
            teamId: null,
            index: 4,
            rpCost: 4000,
            prerequisites: new[] { "common-l1-expanded-tank" },
            upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.DurabilityBonus, 2) },
            description: "耐久+2。升级覆盖L1#4。"
        ));

        db.Add(new TechNodeDef(
            id: "common-l2-slipstream",
            name: "真空尾流",
            nameEn: "Vacuum Slipstream",
            tier: TechTreeTier.L2,
            teamId: null,
            index: 5,
            rpCost: 4000,
            prerequisites: new string[0],
            upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.SlipstreamRangeBonus, 1) },
            description: "尾流触发距离从1格→2格。无L1前置。"
        ));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CN EV Variants — same effects, Chinese market names
    // ═══════════════════════════════════════════════════════════════════

    static void AddCnEvVariants(TechTreeDatabase db)
    {
        // L1 EV variants
        db.Add(new TechNodeDef(
            id: "cn-ev-l1-heat-pump",
            name: "热泵空调",
            nameEn: "Heat Pump AC",
            tier: TechTreeTier.L1, teamId: null, index: 1, rpCost: 2500,
            prerequisites: new string[0], upgradesTo: "cn-ev-l2-phase-change",
            effects: new[] { new TechEffect(TechEffectType.HeatReductionPerLap, 1) },
            description: "[CN EV] 每圈1次，弯道超速-1热（最少为1）"
        ));

        db.Add(new TechNodeDef(
            id: "cn-ev-l1-pmsm",
            name: "永磁同步电机",
            nameEn: "Permanent Magnet Synchronous Motor",
            tier: TechTreeTier.L1, teamId: null, index: 2, rpCost: 2000,
            prerequisites: new string[0], upgradesTo: "cn-ev-l2-dual-motor",
            effects: new[] { new TechEffect(TechEffectType.SpeedBonusStraight, 1) },
            description: "[CN EV] Go模式时本回合+1移动（仅直道）"
        ));

        db.Add(new TechNodeDef(
            id: "cn-ev-l1-torque-vector",
            name: "扭矩矢量控制",
            nameEn: "Torque Vector Control",
            tier: TechTreeTier.L1, teamId: null, index: 3, rpCost: 3000,
            prerequisites: new string[0], upgradesTo: "cn-ev-l2-active-suspension",
            effects: new[] { new TechEffect(TechEffectType.CornerLimitBonus, 1) },
            description: "[CN EV] 弯心限速+1"
        ));

        db.Add(new TechNodeDef(
            id: "cn-ev-l1-solid-state",
            name: "固态电池",
            nameEn: "Solid-State Battery",
            tier: TechTreeTier.L1, teamId: null, index: 4, rpCost: 3500,
            prerequisites: new string[0], upgradesTo: "cn-ev-l2-graphene",
            effects: new[] { new TechEffect(TechEffectType.DurabilityBonus, 1) },
            description: "[CN EV] 耐久+1"
        ));

        // L2 EV variants
        db.Add(new TechNodeDef(
            id: "cn-ev-l2-phase-change",
            name: "相变材料",
            nameEn: "Phase-Change Material",
            tier: TechTreeTier.L2, teamId: null, index: 1, rpCost: 3500,
            prerequisites: new[] { "cn-ev-l1-heat-pump" }, upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.HeatReductionPerLap, 2) },
            description: "[CN EV] 每圈1次，弯道超速-2热（最少为1）"
        ));

        db.Add(new TechNodeDef(
            id: "cn-ev-l2-dual-motor",
            name: "双电机四驱",
            nameEn: "Dual-Motor AWD",
            tier: TechTreeTier.L2, teamId: null, index: 2, rpCost: 3000,
            prerequisites: new[] { "cn-ev-l1-pmsm" }, upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.SpeedBonusStraight, 2) },
            description: "[CN EV] Go模式时本回合+2移动（仅直道）"
        ));

        db.Add(new TechNodeDef(
            id: "cn-ev-l2-active-suspension",
            name: "主动悬架",
            nameEn: "Active Suspension",
            tier: TechTreeTier.L2, teamId: null, index: 3, rpCost: 4000,
            prerequisites: new[] { "cn-ev-l1-torque-vector" }, upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.CornerLimitBonus, 2) },
            description: "[CN EV] 弯心限速+2"
        ));

        db.Add(new TechNodeDef(
            id: "cn-ev-l2-graphene",
            name: "石墨烯电池",
            nameEn: "Graphene Battery",
            tier: TechTreeTier.L2, teamId: null, index: 4, rpCost: 4000,
            prerequisites: new[] { "cn-ev-l1-solid-state" }, upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.DurabilityBonus, 2) },
            description: "[CN EV] 耐久+2"
        ));

        db.Add(new TechNodeDef(
            id: "cn-ev-l2-active-aero",
            name: "主动空力套件",
            nameEn: "Active Aero Kit",
            tier: TechTreeTier.L2, teamId: null, index: 5, rpCost: 4000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.SlipstreamRangeBonus, 1) },
            description: "[CN EV] 尾流触发距离1格→2格"
        ));

        // Mark CN EV variants — these are functionally common techs but with
        // Chinese-market names. They are NOT counted in commonNodeIds for tier gates
        // (Chinese teams pick either the standard or EV variant set, not both).
        db.cnEvNodeIds = new List<string>
        {
            "cn-ev-l1-heat-pump", "cn-ev-l1-pmsm", "cn-ev-l1-torque-vector", "cn-ev-l1-solid-state",
            "cn-ev-l2-phase-change", "cn-ev-l2-dual-motor", "cn-ev-l2-active-suspension",
            "cn-ev-l2-graphene", "cn-ev-l2-active-aero"
        };

        // Remove CN EV variants from commonNodeIds — they are an alternative set
        foreach (var id in db.cnEvNodeIds)
            db.commonNodeIds.Remove(id);
    }

    // ═══════════════════════════════════════════════════════════════════
    // UK — 炸鱼薯条车队
    // ═══════════════════════════════════════════════════════════════════

    static void AddUK(TechTreeDatabase db)
    {
        db.Add(new TechNodeDef(
            id: "uk-l1-fish-and-chips",
            name: "炸鱼薯条",
            nameEn: "Fish & Chips",
            tier: TechTreeTier.L1, teamId: TeamId.UK, index: 1, rpCost: 5000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.FishAndChips, 1) },
            description: "每场限1次。需要支付热量但引擎不足时，忽略这次热量判定，并从一处移回1张热量牌至引擎"
        ));

        db.Add(new TechNodeDef(
            id: "uk-l2-full-english",
            name: "全套英式早餐",
            nameEn: "Full English Breakfast",
            tier: TechTreeTier.L2, teamId: TeamId.UK, index: 1, rpCost: 8000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.FullEnglish, 1) },
            description: "手牌中同时含有热量牌、速度牌、特技牌时，本回合获得1点临时热力+尾流距离+1"
        ));

        db.Add(new TechNodeDef(
            id: "uk-l3-sun-never-sets",
            name: "日不落引擎",
            nameEn: "The Sun Never Sets",
            tier: TechTreeTier.L3, teamId: TeamId.UK, index: 1, rpCost: 12000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.SunNeverSets, 1) },
            description: "获得当前赛道所属国家车队的L2和L3专属科技。英国主场时，额外触发一次全套英式早餐效果。"
        ));
    }

    // ═══════════════════════════════════════════════════════════════════
    // DE — 啤酒黑面包车队
    // ═══════════════════════════════════════════════════════════════════

    static void AddDE(TechTreeDatabase db)
    {
        db.Add(new TechNodeDef(
            id: "de-l1-schwarzbier-fuel",
            name: "黑啤酒燃料",
            nameEn: "Schwarzbier Fuel",
            tier: TechTreeTier.L1, teamId: TeamId.DE, index: 1, rpCost: 5000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.SchwarzbierFuel, 1) },
            description: "任意时刻，将引擎中1张热量牌支付到弃牌堆，前进2格。"
        ));

        db.Add(new TechNodeDef(
            id: "de-l2-wurstplatte",
            name: "香肠拼盘悬架",
            nameEn: "Wurstplatte Suspension",
            tier: TechTreeTier.L2, teamId: TeamId.DE, index: 1, rpCost: 8000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.WurstplatteSuspension, 1) },
            description: "出弯后，赛车前进1格且不参与弯心限速判定。（实现：先判定超速，再+1。）"
        ));

        db.Add(new TechNodeDef(
            id: "de-l3-grill-spezial",
            name: "纽博格林限定烤肉",
            nameEn: "Nürburgring Grill-Spezial",
            tier: TechTreeTier.L3, teamId: TeamId.DE, index: 1, rpCost: 12000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.GrillSpezial, 1) },
            description: "每场1次。本回合从引擎支付的所有热量牌，在回合结束时从弃牌堆自动冷却回引擎。"
        ));
    }

    // ═══════════════════════════════════════════════════════════════════
    // IT — 意面披萨车队
    // ═══════════════════════════════════════════════════════════════════

    static void AddIT(TechTreeDatabase db)
    {
        db.Add(new TechNodeDef(
            id: "it-l1-pizza-sottile",
            name: "薄底手拍披萨底盘",
            nameEn: "Pizza Sottile",
            tier: TechTreeTier.L1, teamId: TeamId.IT, index: 1, rpCost: 5000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.LightweightDoubler, 1) },
            description: "通用科技中「轻量化」分类的效果翻倍。"
        ));

        db.Add(new TechNodeDef(
            id: "it-l2-lasagna",
            name: "千层面复合单体壳",
            nameEn: "Lasagna Monoscocca",
            tier: TechTreeTier.L2, teamId: TeamId.IT, index: 1, rpCost: 8000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[]
            {
                new TechEffect(TechEffectType.SpinCounterMaxBonus, 1),
                new TechEffect(TechEffectType.HandSizeBonus, 1),
                new TechEffect(TechEffectType.EngineCapacityBonus, 1)
            },
            description: "失控计数器上限+1，手牌上限+1，引擎容量+1。"
        ));

        db.Add(new TechNodeDef(
            id: "it-l3-cavallino-rampante",
            name: "红色跃马认证",
            nameEn: "Cavallino Rampante",
            tier: TechTreeTier.L3, teamId: TeamId.IT, index: 1, rpCost: 12000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.CavallinoRampante, 1) },
            description: "冠军时RP收益×1.5（向上取整）。蒙扎主场+意大利车手：再×1.5。"
        ));
    }

    // ═══════════════════════════════════════════════════════════════════
    // US — 汉堡烤肉车队
    // ═══════════════════════════════════════════════════════════════════

    static void AddUS(TechTreeDatabase db)
    {
        db.Add(new TechNodeDef(
            id: "us-l1-drive-thru",
            name: "得来速",
            nameEn: "Drive-Thru",
            tier: TechTreeTier.L1, teamId: TeamId.US, index: 1, rpCost: 5000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.DriveThru, 1) },
            description: "正向经过地标所在格时，本次行动结束后额外前进1格。"
        ));

        db.Add(new TechNodeDef(
            id: "us-l2-smoked-bbq",
            name: "烟熏BBQ",
            nameEn: "Smoked BBQ",
            tier: TechTreeTier.L2, teamId: TeamId.US, index: 1, rpCost: 8000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.SmokedBBQ, 1) },
            description: "位于地标±5格范围内时：热牌可视为速度2打出、身后赛车无尾流、引擎容量+2。"
        ));

        db.Add(new TechNodeDef(
            id: "us-l3-mother-road",
            name: "母亲之路",
            nameEn: "The Mother Road",
            tier: TechTreeTier.L3, teamId: TeamId.US, index: 1, rpCost: 12000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.MotherRoad, 1) },
            description: "地标追踪经过次数：繁荣(1-2次,冷却2)→衰败(3+,付热修复)→复兴(大招:手牌热→位移)。"
        ));
    }

    // ═══════════════════════════════════════════════════════════════════
    // CN — 茶点车队
    // ═══════════════════════════════════════════════════════════════════

    static void AddCN(TechTreeDatabase db)
    {
        db.Add(new TechNodeDef(
            id: "cn-l1-yin-yang-tea",
            name: "阴阳茶",
            nameEn: "Yin-Yang Tea",
            tier: TechTreeTier.L1, teamId: TeamId.CN, index: 1, rpCost: 5000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.YinYangTea, 1) },
            description: "回合结束时由玩家选择：阴→付1热+1格；阳→按手牌、牌库、弃牌堆顺序冷却1张。AI使用自动策略。"
        ));

        db.Add(new TechNodeDef(
            id: "cn-l2-dim-sum-combo",
            name: "促销！早茶拼盘送续杯",
            nameEn: "Dim Sum Combo",
            tier: TechTreeTier.L2, teamId: TeamId.CN, index: 1, rpCost: 8000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.DimSumCombo, 1) },
            description: "按特技牌→速度牌→支付热量顺序出牌，额外触发一次阴阳茶效果。"
        ));

        db.Add(new TechNodeDef(
            id: "cn-l3-somersault-cloud",
            name: "筋斗云型 ATTACK",
            nameEn: "Somersault Cloud Attack",
            tier: TechTreeTier.L3, teamId: TeamId.CN, index: 1, rpCost: 12000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.SomersaultCloud, 1) },
            description: "ATTACK特技牌永久升级：尾流+2、弯心+1（在原有效果基础上叠加）。"
        ));
    }

    // ═══════════════════════════════════════════════════════════════════
    // JP — 寿司拉面车队
    // ═══════════════════════════════════════════════════════════════════

    static void AddJP(TechTreeDatabase db)
    {
        db.Add(new TechNodeDef(
            id: "jp-l1-nigiri",
            name: "寿司一握",
            nameEn: "Nigiri",
            tier: TechTreeTier.L1, teamId: TeamId.JP, index: 1, rpCost: 5000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.Nigiri, 1) },
            description: "过弯时若通过速度恰好等于弯心限速值，出弯结束后额外前进2格。"
        ));

        db.Add(new TechNodeDef(
            id: "jp-l2-broth-selection",
            name: "汤底定味",
            nameEn: "Broth Selection",
            tier: TechTreeTier.L2, teamId: TeamId.JP, index: 1, rpCost: 8000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.BrothSelection, 1) },
            description: "游戏开始时从豚骨(直道+1)/酱油(弯道+1)/味噌(尾流+1)/盐味(冷却1)中选1个全局被动。"
        ));

        db.Add(new TechNodeDef(
            id: "jp-l3-bankuruwase",
            name: "番狂わせ",
            nameEn: "Bankuruwase",
            tier: TechTreeTier.L3, teamId: TeamId.JP, index: 1, rpCost: 12000,
            prerequisites: new string[0], upgradesTo: "",
            effects: new[] { new TechEffect(TechEffectType.Bankuruwase, 1) },
            description: "排名倒数第一/二时触发转子引擎：连续3回合冷却1+全流派Buff。"
        ));
    }
}
