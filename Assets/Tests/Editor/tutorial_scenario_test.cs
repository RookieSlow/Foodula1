using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public class TutorialScenarioTests
{
    [SetUp]
    public void SetUp()
    {
        TutorialLaunchState.Clear();
        TrackSelectionState.Reset();
    }

    [TearDown]
    public void TearDown()
    {
        TutorialLaunchState.Clear();
        TrackSelectionState.Reset();
    }

    [Test]
    public void LeMansScenarioIsIsolatedUkWithNoProgressionBenefits()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();

        Assert.That(scenario.id, Is.EqualTo("tutorial_le_mans_uk_v1"));
        Assert.That(scenario.trackId, Is.EqualTo("le_mans_old_mulsanne"));
        Assert.That(scenario.playerTeam, Is.EqualTo(TeamId.UK));
        Assert.That(scenario.techTreeEnabled, Is.False);
        Assert.That(scenario.driverSkillsEnabled, Is.False);
        Assert.That(scenario.normalRewardsEnabled, Is.False);
        Assert.That(scenario.normalProgressionWritesEnabled, Is.False);
        Assert.That(scenario.opponentScript.Single().expectedSlipstreamDistance, Is.EqualTo(2));
        Assert.That(scenario.opponentCount, Is.EqualTo(1));
        Assert.That(scenario.opponentTeam, Is.EqualTo(TeamId.JP));
        Assert.That(scenario.guidedStartWeatherId, Is.EqualTo("sunny"));
        Assert.That(scenario.practiceWeatherId, Is.EqualTo("cloudy"));
    }

    [Test]
    public void TutorialLaunchOverridesTrackWithoutMutatingQuickRaceSelection()
    {
        const string quickRaceTrack = "monza_pasta";
        Assert.That(TrackSelectionState.TrySelect(quickRaceTrack), Is.True);

        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialLaunchState.Request(scenario);

        Assert.That(TutorialLaunchState.IsRequested, Is.True);
        Assert.That(TutorialLaunchState.ResolveTrackId("silverstone_afternoon_tea"),
            Is.EqualTo(TutorialScenarioDefinition.TrackId));
        Assert.That(TrackSelectionState.SelectedTrackId, Is.EqualTo(quickRaceTrack));

        Assert.That(TutorialLaunchState.ActivateRequested(), Is.SameAs(scenario));
        Assert.That(TutorialLaunchState.IsActive, Is.True);
        Assert.That(TutorialLaunchState.ResolveTrackId("silverstone_afternoon_tea"),
            Is.EqualTo(TutorialScenarioDefinition.TrackId));

        TutorialLaunchState.Clear();
        Assert.That(TutorialLaunchState.ResolveTrackId("silverstone_afternoon_tea"),
            Is.EqualTo(quickRaceTrack));
    }

    [Test]
    public void TeachingOpponentDeckIsExactSpeedOnlyAndRepeatable()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        List<CardData> first = scenario.CreateOpponentDeck();
        List<CardData> second = scenario.CreateOpponentDeck();

        Assert.That(first.Select(CardLabel), Is.EqualTo(second.Select(CardLabel)));
        Assert.That(first.Count, Is.EqualTo(12));
        Assert.That(first.All(card => card.type == CardType.Speed), Is.True);

        var deck = new CardDeck();
        deck.InitializeExactOrder(first, new HeatPool(scenario.engineHeatCapacity));
        Assert.That(deck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(deck.Hand.Select(card => card.value), Is.EqualTo(new[] { 1, 1, 2, 2, 3, 1, 2 }));
    }

    [Test]
    public void TutorialSessionCanDisableUkVehicleHandlingWithoutChangingNormalDefault()
    {
        var uk = new PlayerState("UK", false, 0, 1) { teamId = TeamId.UK };
        var normalSession = new RaceSession(new SystemRandomSource(1));
        normalSession.Weather = WeatherType.Sunny;
        Assert.That(normalSession.EffectiveCornerLimit(uk, 4), Is.EqualTo(5));

        var tutorialSession = new RaceSession(new SystemRandomSource(1))
        {
            TeamVehicleBonusesEnabled = false,
            Weather = WeatherType.Sunny
        };
        Assert.That(tutorialSession.EffectiveCornerLimit(uk, 4), Is.EqualTo(4));
        Assert.That(tutorialSession.EffectiveHeatPoolSize(uk, 6), Is.EqualTo(6));
    }

    [Test]
    public void ExactDeckMatchesAuthoredOpeningAndFutureDrawsWithoutSeed()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var deck = new CardDeck();
        deck.InitializeExactOrder(scenario.CreateExactDeck(), new HeatPool(scenario.engineHeatCapacity));

        Assert.That(deck.UsesExactOrder, Is.True);
        Assert.That(deck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(deck.Hand.Select(CardLabel), Is.EqualTo(new[]
        {
            "1", "2", "2", "3", "4", "1", "T[uk-scone]"
        }));

        var played = new List<CardData> { deck.Hand[0], deck.Hand[1] };
        deck.RemoveFromHand(played);
        deck.DiscardSpeedCards(played);
        Assert.That(deck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(deck.Hand.Skip(5).Select(CardLabel), Is.EqualTo(new[]
        {
            "3", "T[uk-english-breakfast-tea]"
        }));
    }

    [Test]
    public void ExactDeckRecyclePreservesDiscardChronologyAndIgnoresShuffleCalls()
    {
        var deck = new CardDeck();
        deck.InitializeExactOrder(new[]
        {
            new CardData(CardType.Speed, 1),
            new CardData(CardType.Speed, 2),
            new CardData(CardType.Speed, 3)
        }, new HeatPool(6));

        deck.ShuffleDrawPile();
        Assert.That(deck.DrawToHand(3), Is.True);
        var firstCycle = new List<CardData>(deck.Hand);
        deck.RemoveFromHand(firstCycle);
        deck.DiscardSpeedCards(firstCycle);

        Assert.That(deck.DrawToHand(3), Is.True);
        Assert.That(deck.Hand.Select(card => card.value), Is.EqualTo(new[] { 1, 2, 3 }));
    }

    [Test]
    public void GuidedStepsRejectOutOfOrderActionsAndEnterPracticeInAuthoredOrder()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var machine = new TutorialStateMachine(scenario);

        Assert.That(machine.TryPerform(TutorialAction.PayHeat, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("expected_AcknowledgeObjective"));
        Assert.That(machine.CurrentStep.id, Is.EqualTo(TutorialStepId.ObjectiveAndInterface));

        foreach (TutorialStepDefinition step in scenario.steps)
        {
            Assert.That(machine.CurrentStep.id, Is.EqualTo(step.id));
            Assert.That(machine.TryPerform(step.requiredAction, out reason), Is.True, reason);
        }

        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(machine.CompletedStepCount, Is.EqualTo(scenario.steps.Count));
        Assert.That(machine.Events.Any(entry => entry.ToString().Contains("event=practice_started")), Is.True);
    }

    [Test]
    public void SkipRestartCompleteAndExitHaveExplicitRecoverableStates()
    {
        var machine = new TutorialStateMachine(TutorialScenarioDefinition.CreateLeMansUk());

        machine.SkipGuidedSection();
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Practice));

        machine.RestartPracticeLap();
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(machine.TryPerform(TutorialAction.CompletePracticeLap, out _), Is.True);
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Completed));

        machine.RestartGuidedSection();
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Guided));
        Assert.That(machine.CurrentStep.id, Is.EqualTo(TutorialStepId.ObjectiveAndInterface));

        machine.ExitTutorial();
        Assert.That(machine.Phase, Is.EqualTo(TutorialRunPhase.Exited));
    }

    [Test]
    public void ScenarioContainsEveryRequiredGuidedTopicOnce()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialStepId[] ids = scenario.steps.Select(step => step.id).ToArray();

        Assert.That(ids, Is.EqualTo((TutorialStepId[])System.Enum.GetValues(typeof(TutorialStepId))));
        Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length));
        Assert.That(scenario.weatherScript.Any(cue => cue.step == TutorialStepId.Weather), Is.True);
        Assert.That(scenario.opponentScript.Any(cue => cue.step == TutorialStepId.Slipstream), Is.True);
        Assert.That(scenario.playerCheckpoints.Select(cue => cue.step).Distinct().Count(),
            Is.EqualTo(scenario.playerCheckpoints.Count));
        Assert.That(scenario.tutorialPitLane, Is.Not.Null);
    }

    [Test]
    public void GuidedStepsContainAuthoredPresentationAndOnlyKnowledgeStepsAdvanceManually()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();

        Assert.That(scenario.steps.All(step => !string.IsNullOrWhiteSpace(step.sectionLabel)), Is.True);
        Assert.That(scenario.steps.All(step => !string.IsNullOrWhiteSpace(step.title)), Is.True);
        Assert.That(scenario.steps.All(step => !string.IsNullOrWhiteSpace(step.goal)), Is.True);
        Assert.That(scenario.steps.All(step => !string.IsNullOrWhiteSpace(step.actionPrompt)), Is.True);
        Assert.That(scenario.steps.All(step => !string.IsNullOrWhiteSpace(step.focusIntroduction)), Is.True);
        Assert.That(scenario.steps.All(step => !string.IsNullOrWhiteSpace(step.instruction)), Is.True);
        Assert.That(scenario.steps.All(step => step.instruction.Contains("<b>轮到你了</b>")), Is.True);
        Assert.That(scenario.steps.All(step => !step.instruction.Contains("<b>目标</b>")), Is.True);
        Assert.That(
            scenario.steps.Select(step => step.sectionLabel),
            Is.EqualTo(new[]
            {
                "起步",
                "基础驾驶", "基础驾驶", "基础驾驶",
                "卡牌循环",
                "热量管理", "热量管理", "热量管理",
                "赛道规则", "赛道规则", "赛道互动",
                "维修区", "维修区",
                "UK 特殊牌", "UK 特殊牌",
                "总结"
            }));
        Assert.That(
            scenario.steps.Where(step => step.allowManualAdvance).Select(step => step.id),
            Is.EqualTo(new[]
            {
                TutorialStepId.ObjectiveAndInterface,
                TutorialStepId.DeckHandDiscardAndRecycle,
                TutorialStepId.Weather,
                TutorialStepId.Review
            }));
        Assert.That(
            scenario.steps.Where(step => step.allowManualAdvance).Select(step => step.manualAdvanceLabel),
            Is.EqualTo(new[]
            {
                "开始第一回合",
                "牌区已看懂",
                "天气规则已看懂",
                "重置并开始练习"
            }));
        Assert.That(
            scenario.steps.Where(step => !step.allowManualAdvance)
                .All(step => string.IsNullOrEmpty(step.manualAdvanceLabel)),
            Is.True);
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.PitDelayedResolution)
                .actionPrompt,
            Does.Contain("下一回合"));
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.UkEnglishBreakfastTea)
                .actionPrompt,
            Does.Contain("下一回合"));
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.ObjectiveAndInterface)
                .recoveryHint,
            Does.Contain("收起指引"));
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.ObjectiveAndInterface).goal,
            Does.Contain("欢迎来到围场"));
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.Review).goal,
            Does.Contain("已经分别用过"));
    }

    [Test]
    public void EveryGuidedFocusTargetHasOneAuthoredStandaloneIntroduction()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialFocusTarget[] requiredTargets =
            (TutorialFocusTarget[])System.Enum.GetValues(typeof(TutorialFocusTarget));

        Assert.That(scenario.steps.Select(step => step.focusTarget).Distinct(),
            Is.EquivalentTo(requiredTargets));
        Assert.That(scenario.steps.All(step => step.focusIntroduction.Length >= 12), Is.True);
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.GearAndRequiredCards)
                .focusTarget,
            Is.EqualTo(TutorialFocusTarget.GearControls));
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.Weather)
                .focusTarget,
            Is.EqualTo(TutorialFocusTarget.Weather));
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.PitSelection)
                .focusTarget,
            Is.EqualTo(TutorialFocusTarget.PitChoice));
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.UkScone)
                .focusTarget,
            Is.EqualTo(TutorialFocusTarget.UkSconeCard));
        Assert.That(
            scenario.steps.Single(step => step.id == TutorialStepId.UkEnglishBreakfastTea)
                .focusTarget,
            Is.EqualTo(TutorialFocusTarget.UkTeaCard));
    }

    [Test]
    public void TutorialOverlayPrefabExposesAllEditableStepCopyAndLayoutComponents()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/TutorialOverlay");

        Assert.That(prefab, Is.Not.Null);
        TutorialOverlayAuthoring authoring = prefab.GetComponent<TutorialOverlayAuthoring>();
        Assert.That(authoring, Is.Not.Null);
        Assert.That(authoring.Guide, Is.Not.Null);
        Assert.That(authoring.FocusHighlight, Is.Not.Null);
        Assert.That(authoring.Steps.Count, Is.EqualTo(16));
        Assert.That(authoring.Steps.Select(step => step.id).Distinct().Count(), Is.EqualTo(16));
        Assert.That(authoring.Find(TutorialStepId.GearAndRequiredCards).title,
            Is.EqualTo("来一次换挡"));
        Assert.That(authoring.Find(TutorialStepId.UkScone).focusIntroduction,
            Does.Contain("司康"));
        Assert.That(prefab.transform.Find("TutorialGuidePanel"), Is.Not.Null);
        Assert.That(prefab.transform.Find("TutorialFocusHighlight"), Is.Not.Null);
    }

    [Test]
    public void TutorialOverlayAuthoringCanPreviewStepAndPracticeWithoutRuntimeDirector()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/TutorialOverlay");
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            TutorialOverlayAuthoring authoring =
                instance.GetComponent<TutorialOverlayAuthoring>();
            Transform panel = instance.transform.Find("TutorialGuidePanel");

            Assert.That(authoring.PreviewStep(TutorialStepId.UkScone), Is.True);
            Assert.That(
                panel.Find("TutorialTitle").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo(authoring.Find(TutorialStepId.UkScone).title));
            Assert.That(
                panel.Find("TutorialInstruction").GetComponent<TMPro.TMP_Text>().text,
                Does.Contain("轮到你了"));

            Assert.That(authoring.PreviewPractice(completed: true), Is.True);
            Assert.That(
                panel.Find("TutorialTitle").GetComponent<TMPro.TMP_Text>().text,
                Is.EqualTo("练习圈完成"));
            Assert.That(
                panel.Find("TutorialContinueButton")
                    .GetComponentInChildren<TMPro.TMP_Text>(true).text,
                Is.EqualTo("再练一圈"));
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    [Test]
    public void TutorialOverlayAuthoringValidationAcceptsCompletePrefabCopy()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/TutorialOverlay");
        TutorialOverlayAuthoring authoring =
            prefab.GetComponent<TutorialOverlayAuthoring>();

        Assert.That(authoring.CollectValidationIssues(), Is.Empty);
    }

    [Test]
    public void OptionalGuideSectionsAreOmittedWhenAuthorLeavesThemBlank()
    {
        var presentation = new TutorialStepPresentation
        {
            goal = "先理解核心规则。",
            currentState = " ",
            actionPrompt = "选择一张速度牌。",
            successSignal = "",
            recoveryHint = null
        };

        string text = presentation.BuildGuideText();

        Assert.That(text, Does.Contain("先理解核心规则。"));
        Assert.That(text, Does.Contain("<b>轮到你了</b>"));
        Assert.That(text, Does.Not.Contain("<b>现在场上</b>"));
        Assert.That(text, Does.Not.Contain("<b>完成后</b>"));
        Assert.That(text, Does.Not.Contain("没反应？"));
        Assert.That(text, Does.Not.Contain("\n\n\n"));
    }

    [Test]
    public void PrefabCopyMatchesScenarioFallbackCopy()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/TutorialOverlay");
        TutorialOverlayAuthoring authoring = prefab.GetComponent<TutorialOverlayAuthoring>();

        foreach (TutorialStepDefinition definition in scenario.steps)
        {
            TutorialStepPresentation presentation = authoring.Find(definition.id);
            Assert.That(presentation, Is.Not.Null, definition.id.ToString());
            Assert.That(presentation.sectionLabel, Is.EqualTo(definition.sectionLabel), definition.id.ToString());
            Assert.That(presentation.title, Is.EqualTo(definition.title), definition.id.ToString());
            Assert.That(presentation.goal, Is.EqualTo(definition.goal), definition.id.ToString());
            Assert.That(presentation.currentState, Is.EqualTo(definition.currentState), definition.id.ToString());
            Assert.That(presentation.actionPrompt, Is.EqualTo(definition.actionPrompt), definition.id.ToString());
            Assert.That(presentation.successSignal, Is.EqualTo(definition.successSignal), definition.id.ToString());
            Assert.That(presentation.recoveryHint, Is.EqualTo(definition.recoveryHint), definition.id.ToString());
            Assert.That(presentation.focusIntroduction, Is.EqualTo(definition.focusIntroduction), definition.id.ToString());
            Assert.That(presentation.manualAdvanceLabel, Is.EqualTo(definition.manualAdvanceLabel), definition.id.ToString());
        }
    }

    [Test]
    public void TutorialOverlayValidationReportsDuplicateMissingAndBlankCopy()
    {
        TutorialScenarioDefinition scenario =
            TutorialScenarioDefinition.CreateLeMansUk();
        var presentations = scenario.steps
            .Select(TutorialStepPresentation.FromDefinition)
            .ToList();
        presentations[1].id = presentations[0].id;
        presentations[2].title = " ";

        var issues = TutorialOverlayValidation.CollectIssues(
            presentations,
            guide: null,
            focusHighlight: null);

        Assert.That(issues, Has.Some.Contains("ID 重复"));
        Assert.That(issues, Has.Some.Contains("缺少步骤 ID"));
        Assert.That(issues, Has.Some.Contains("标题为空"));
        Assert.That(issues, Has.Some.Contains("缺少 TutorialGuideUI"));
        Assert.That(issues, Has.Some.Contains("缺少 TutorialFocusHighlightUI"));
    }

    [Test]
    public void TutorialOverlayAuthoringUsesDedicatedEditorInspector()
    {
        GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/TutorialOverlay");
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        UnityEditor.Editor editor = null;
        try
        {
            TutorialOverlayAuthoring authoring =
                instance.GetComponent<TutorialOverlayAuthoring>();
            editor = UnityEditor.Editor.CreateEditor(authoring);

            Assert.That(editor, Is.TypeOf<TutorialOverlayAuthoringEditor>());
        }
        finally
        {
            if (editor != null)
                UnityEngine.Object.DestroyImmediate(editor);
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    [TestCase(1920, 1080, false)]
    [TestCase(1280, 720, false)]
    [TestCase(960, 540, false)]
    [TestCase(854, 480, true)]
    public void TutorialGuideLayoutFitsCommon16By9SafeAreaAndCanCollapse(
        int screenWidth,
        int screenHeight,
        bool expectedCompact)
    {
        TutorialGuideLayout expanded = TutorialGuideLayoutRules.Resolve(
            screenWidth, screenHeight, expanded: true);
        TutorialGuideLayout collapsed = TutorialGuideLayoutRules.Resolve(
            screenWidth, screenHeight, expanded: false);

        Assert.That(expanded.Width + expanded.Margin * 2f,
            Is.LessThanOrEqualTo(screenWidth + 0.01f));
        Assert.That(expanded.Height + expanded.Margin * 2f,
            Is.LessThanOrEqualTo(screenHeight + 0.01f));
        Assert.That(collapsed.Width + collapsed.Margin * 2f,
            Is.LessThanOrEqualTo(screenWidth + 0.01f));
        Assert.That(collapsed.Height + collapsed.Margin * 2f,
            Is.LessThanOrEqualTo(screenHeight + 0.01f));
        Assert.That(expanded.Width, Is.GreaterThan(collapsed.Width));
        Assert.That(expanded.Height, Is.GreaterThan(collapsed.Height));
        Assert.That(collapsed.Height, Is.LessThanOrEqualTo(104f));
        Assert.That(collapsed.IsCompact, Is.True);
        Assert.That(expanded.IsCompact, Is.EqualTo(expectedCompact));
        Assert.That(expanded.InstructionFontSize,
            Is.EqualTo(expectedCompact ? 13f : 15f));
    }

    [Test]
    public void TutorialGuideLayoutGrowsForLongCopyAndStopsAtSafeArea()
    {
        TutorialGuideLayout roomy = TutorialGuideLayoutRules.Resolve(
            1920, 1080, expanded: true, preferredExpandedHeight: 720f);
        TutorialGuideLayout constrained = TutorialGuideLayoutRules.Resolve(
            854, 480, expanded: true, preferredExpandedHeight: 720f);

        Assert.That(roomy.Height, Is.EqualTo(720f));
        Assert.That(constrained.Height + constrained.Margin * 2f,
            Is.EqualTo(480f).Within(0.01f));
    }

    [Test]
    public void TutorialFocusDismissesOncePerStepAndDoesNotReappearOnRefresh()
    {
        var state = new TutorialFocusDismissState();

        state.Show(TutorialStepId.ObjectiveAndInterface, pointerHeld: false);
        Assert.That(state.IsVisible, Is.True);
        Assert.That(state.Update(pointerHeld: true, pointerPressedThisFrame: true), Is.True);
        Assert.That(state.IsVisible, Is.False);

        state.Show(TutorialStepId.ObjectiveAndInterface, pointerHeld: false);
        Assert.That(state.IsVisible, Is.False, "same-step refresh must not resurrect focus");

        state.Show(TutorialStepId.TurnFlow, pointerHeld: false);
        Assert.That(state.IsVisible, Is.True, "a new step should receive its own focus");
    }

    [Test]
    public void TutorialFocusIgnoresClickThatOpenedStepUntilPointerIsReleased()
    {
        var state = new TutorialFocusDismissState();

        state.Show(TutorialStepId.TurnFlow, pointerHeld: true);
        Assert.That(state.IsWaitingForPointerRelease, Is.True);
        Assert.That(state.Update(pointerHeld: true, pointerPressedThisFrame: true), Is.False);
        Assert.That(state.IsVisible, Is.True);

        state.Update(pointerHeld: false, pointerPressedThisFrame: false);
        Assert.That(state.IsWaitingForPointerRelease, Is.False);
        Assert.That(state.Update(pointerHeld: true, pointerPressedThisFrame: true), Is.True);
        Assert.That(state.IsVisible, Is.False);
    }

    [Test]
    public void FifthStepManualAcknowledgementIsAcceptedDuringAnimationCleanup()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialStepDefinition fifthStep = scenario.steps[4];

        Assert.That(fifthStep.id, Is.EqualTo(TutorialStepId.DeckHandDiscardAndRecycle));
        Assert.That(fifthStep.allowManualAdvance, Is.True);
        Assert.That(
            MVPGameManager.CanRequestTutorialManualAdvance(
                fifthStep,
                GamePhase.Animating),
            Is.True,
            "the card-zone acknowledgement must not deadlock during movement cleanup");
        Assert.That(
            MVPGameManager.CanRequestTutorialManualAdvance(
                fifthStep,
                GamePhase.WaitingForGear),
            Is.True);
        Assert.That(
            MVPGameManager.CanRequestTutorialManualAdvance(
                scenario.steps[5],
                GamePhase.WaitingForGear),
            Is.False,
            "action-driven lessons must remain protected from manual skipping");
        Assert.That(
            MVPGameManager.CanRequestTutorialManualAdvance(
                fifthStep,
                GamePhase.GameOver),
            Is.False);
    }

    [Test]
    public void GearCardZoneAndCoolingLessonsWaitForFreshTurnPresentation()
    {
        Assert.That(
            TutorialGuideTimingRules.StartsAtNextTurn(
                TutorialStepId.GearAndRequiredCards),
            Is.True,
            "step 3 must not appear during the previous turn cleanup");
        Assert.That(
            TutorialGuideTimingRules.StartsAtNextTurn(
                TutorialStepId.HeatCardsAndCooling),
            Is.True,
            "step 7 must not appear immediately after paying heat on gear shift");
        Assert.That(
            TutorialGuideTimingRules.StartsAtNextTurn(
                TutorialStepId.DeckHandDiscardAndRecycle),
            Is.True,
            "card-zone lesson must wait until cleanup and a visible hand refill complete");
        Assert.That(
            TutorialGuideTimingRules.RequiresFullHandPresentation(
                TutorialStepId.DeckHandDiscardAndRecycle),
            Is.True);
        Assert.That(
            TutorialGuideTimingRules.SkipsOptionalDiscardBeforePresentation(
                TutorialStepId.DeckHandDiscardAndRecycle),
            Is.True,
            "the hidden optional-discard prompt must not stall the movement lesson");

        Assert.That(
            TutorialGuideTimingRules.StartsAtNextTurn(
                TutorialStepId.SpeedCardsAndMovement),
            Is.False,
            "movement explanation remains the immediate result of card confirmation");
        Assert.That(
            TutorialGuideTimingRules.RequiresFullHandPresentation(
                TutorialStepId.SpeedCardsAndMovement),
            Is.False,
            "movement lesson must focus the track instead of rebuilding the spent hand");
        Assert.That(
            TutorialGuideTimingRules.SkipsOptionalDiscardBeforePresentation(
                TutorialStepId.SpeedCardsAndMovement),
            Is.False);
        Assert.That(
            TutorialGuideTimingRules.StartsAtNextTurn(
                TutorialStepId.HeatPayment),
            Is.False,
            "heat payment remains the immediate result of selecting G3");
    }

    [Test]
    public void MovementLessonFocusesTrackAfterCardsLeaveHand()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialStepDefinition movement = scenario.steps[3];

        Assert.That(movement.id, Is.EqualTo(TutorialStepId.SpeedCardsAndMovement));
        Assert.That(movement.focusTarget, Is.EqualTo(TutorialFocusTarget.Track));
        StringAssert.DoesNotContain("手牌", movement.actionPrompt);
        StringAssert.Contains("赛道", movement.focusIntroduction);
    }

    [Test]
    public void DeferredLessonWaitsUntilPlayerTurnPresentationIsReady()
    {
        Assert.That(
            TutorialGuideTimingRules.IsTurnPresentationReady(GamePhase.Animating),
            Is.False,
            "guide must not appear before the checkpoint view has changed");
        Assert.That(
            TutorialGuideTimingRules.IsTurnPresentationReady(GamePhase.WaitingForGear),
            Is.True,
            "camera, HUD and gear input are ready at this presentation point");
        Assert.That(
            TutorialGuideTimingRules.IsTurnPresentationReady(GamePhase.WaitingForCards),
            Is.False);
        Assert.That(
            TutorialGuideTimingRules.IsTurnPresentationReady(GamePhase.GameOver),
            Is.False);
    }

    [Test]
    public void InitialGuideWaitsForRaceSceneCameraAndInputPresentation()
    {
        Assert.That(
            TutorialGuideTimingRules.IsInitialPresentationReady(
                raceSceneLoaded: false,
                cameraInitialized: true,
                GamePhase.WaitingForGear),
            Is.False,
            "the authoring overlay must not appear while MainMenu is still active");
        Assert.That(
            TutorialGuideTimingRules.IsInitialPresentationReady(
                raceSceneLoaded: true,
                cameraInitialized: false,
                GamePhase.WaitingForGear),
            Is.False,
            "the guide must not precede the initial race-camera snap");
        Assert.That(
            TutorialGuideTimingRules.IsInitialPresentationReady(
                raceSceneLoaded: true,
                cameraInitialized: true,
                GamePhase.Animating),
            Is.False,
            "the guide must not appear before the player input presentation is ready");
        Assert.That(
            TutorialGuideTimingRules.IsInitialPresentationReady(
                raceSceneLoaded: true,
                cameraInitialized: true,
                GamePhase.WaitingForGear),
            Is.True);
    }

    [Test]
    public void CompletedStepSuccessRemainsAvailableUntilNextGuidedAction()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var machine = new TutorialStateMachine(scenario);

        Assert.That(machine.LastCompletedStep, Is.Null);
        Assert.That(machine.TryPerform(TutorialAction.AcknowledgeObjective, out string reason),
            Is.True, reason);
        Assert.That(machine.LastCompletedStep.id, Is.EqualTo(TutorialStepId.ObjectiveAndInterface));
        Assert.That(machine.LastCompletedStep.successSignal,
            Is.EqualTo(scenario.steps[0].successSignal));
        Assert.That(machine.CurrentStep.id, Is.EqualTo(TutorialStepId.TurnFlow));

        Assert.That(machine.TryPerform(TutorialAction.CompleteTurnFlow, out reason),
            Is.True, reason);
        Assert.That(machine.LastCompletedStep.id, Is.EqualTo(TutorialStepId.TurnFlow));

        machine.RestartGuidedSection();
        Assert.That(machine.LastCompletedStep, Is.Null);
        Assert.That(machine.CurrentStep.id, Is.EqualTo(TutorialStepId.ObjectiveAndInterface));
    }

    [Test]
    public void RuntimeDirectorEmitsStateEventsExactlyOnceAndRejectsWrongActionWithoutCue()
    {
        var director = new TutorialRuntimeDirector(TutorialScenarioDefinition.CreateLeMansUk());

        Assert.That(director.DrainNewEvents().Select(entry => entry.eventId),
            Is.EqualTo(new[] { "guided_started" }));
        Assert.That(director.BlocksRaceInput, Is.True);
        Assert.That(director.DrainNewEvents(), Is.Empty);
        Assert.That(director.TakePendingCue(), Is.Null);

        Assert.That(director.TryPerform(TutorialAction.PayHeat, out string reason), Is.False);
        Assert.That(reason, Is.EqualTo("expected_AcknowledgeObjective"));
        Assert.That(director.TakePendingCue(), Is.Null);
        Assert.That(director.DrainNewEvents().Single().eventId, Is.EqualTo("action_rejected"));

        Assert.That(director.TryPerform(TutorialAction.AcknowledgeObjective, out reason), Is.True);
        Assert.That(director.BlocksRaceInput, Is.False);
    }

    [Test]
    public void RuntimeDirectorProducesWeatherAndOpponentCuesOnlyWhenTheirStepsStart()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var director = new TutorialRuntimeDirector(scenario);

        for (int i = 0; i < scenario.steps.Count; i++)
        {
            TutorialStepDefinition step = scenario.steps[i];
            Assert.That(director.CurrentStep.id, Is.EqualTo(step.id));
            Assert.That(director.TryPerform(step.requiredAction, out string reason), Is.True, reason);

            TutorialCheckpointCue cue = director.TakePendingCue();
            TutorialStepId? nextStep = i + 1 < scenario.steps.Count
                ? scenario.steps[i + 1].id
                : (TutorialStepId?)null;
            TutorialPlayerCheckpoint expectedPlayer = nextStep.HasValue
                ? scenario.playerCheckpoints.SingleOrDefault(item => item.step == nextStep.Value)
                : null;

            if (nextStep == TutorialStepId.Weather)
            {
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.Weather.weatherId, Is.EqualTo("rain"));
                Assert.That(cue.Opponent, Is.Null);
            }
            else if (nextStep == TutorialStepId.Slipstream)
            {
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.Weather, Is.Null);
                Assert.That(cue.Opponent.leaderCell, Is.EqualTo(42));
                Assert.That(cue.Opponent.playerCell, Is.EqualTo(40));
                Assert.That(cue.Opponent.expectedSlipstreamDistance, Is.EqualTo(2));
            }
            else if (nextStep == TutorialStepId.Review)
            {
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.Weather.weatherId, Is.EqualTo("cloudy"));
                Assert.That(cue.Opponent, Is.Null);
            }
            else if (expectedPlayer == null)
            {
                Assert.That(cue, Is.Null);
            }

            if (expectedPlayer != null)
            {
                Assert.That(cue, Is.Not.Null);
                Assert.That(cue.Player, Is.SameAs(expectedPlayer));
            }
        }

        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Practice));
    }

    [Test]
    public void PracticeBootStartsAtOneLapBoundaryWithoutReplayingGuidedCues()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var director = new TutorialRuntimeDirector(scenario, startInPractice: true);

        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(director.CurrentStep, Is.Null);
        Assert.That(director.CompletedStepCount, Is.EqualTo(scenario.steps.Count));
        Assert.That(director.BlocksRaceInput, Is.False);
        Assert.That(director.TakePendingCue(), Is.Null);
        Assert.That(director.DrainNewEvents().Select(entry => entry.eventId),
            Is.EqualTo(new[] { "practice_started" }));
        Assert.That(
            TutorialPracticeRules.GetRequiredLapCount(director.Phase, normalLapCount: 3),
            Is.EqualTo(1));
    }

    [Test]
    public void PracticeCompletionRestartReplayAndExitRemainRecoverable()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var director = new TutorialRuntimeDirector(scenario, startInPractice: true);
        director.DrainNewEvents();

        Assert.That(director.CompletePracticeLap(out string reason), Is.True, reason);
        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Completed));
        Assert.That(TutorialPracticeRules.ShouldEndImmediately(director.Phase), Is.True);
        Assert.That(director.DrainNewEvents().Single().eventId, Is.EqualTo("practice_completed"));

        director.RestartPracticeLap();
        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Practice));
        Assert.That(director.DrainNewEvents().Single().eventId, Is.EqualTo("practice_restarted"));

        director.RestartGuidedSection();
        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Guided));
        Assert.That(director.CurrentStep.id, Is.EqualTo(TutorialStepId.ObjectiveAndInterface));
        Assert.That(director.TakePendingCue(), Is.Null);

        director.ExitTutorial();
        Assert.That(director.Phase, Is.EqualTo(TutorialRunPhase.Exited));
        Assert.That(TutorialPracticeRules.ShouldEndImmediately(director.Phase), Is.True);
    }

    [Test]
    public void PracticeResetRecreatesSameExactOpeningWhileNormalLapRulesStayUnchanged()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var firstDeck = new CardDeck();
        var restartedDeck = new CardDeck();
        firstDeck.InitializeExactOrder(
            scenario.CreateExactDeck(),
            new HeatPool(scenario.engineHeatCapacity));
        restartedDeck.InitializeExactOrder(
            scenario.CreateExactDeck(),
            new HeatPool(scenario.engineHeatCapacity));

        Assert.That(firstDeck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(restartedDeck.DrawToHand(scenario.openingHandSize), Is.True);
        Assert.That(firstDeck.Hand.Select(CardLabel),
            Is.EqualTo(restartedDeck.Hand.Select(CardLabel)));
        Assert.That(firstDeck.UsesExactOrder, Is.True);
        Assert.That(restartedDeck.UsesExactOrder, Is.True);
        Assert.That(TutorialPracticeRules.GetRequiredLapCount(null, normalLapCount: 3),
            Is.EqualTo(3));
        Assert.That(TutorialPracticeRules.GetRequiredLapCount(TutorialRunPhase.Guided, 3),
            Is.EqualTo(3));
    }

    [Test]
    public void TutorialSlipstreamCueKeepsNormalRearOnlyBenefitRule()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialOpponentCue cue = scenario.opponentScript.Single(item => item.step == TutorialStepId.Slipstream);
        var player = new PlayerState("player", false, cue.playerCell, 1) { teamId = TeamId.UK };
        var leader = new PlayerState("leader", true, cue.leaderCell, 1) { teamId = TeamId.JP };
        var session = new RaceSession(new SystemRandomSource(1))
        {
            TeamVehicleBonusesEnabled = false,
            SlipstreamRangeOverride = cue.expectedSlipstreamDistance,
            Weather = WeatherType.Sunny
        };
        session.Players.Add(player);
        session.Players.Add(leader);

        var settled = new Dictionary<PlayerState, int>
        {
            [player] = 0,
            [leader] = 0
        };
        SlipstreamChainResult playerChain = session.ComputeSlipstreamChain(
            player, session.Players, 60, settled, 2, session.Players);
        SlipstreamChainResult leaderChain = session.ComputeSlipstreamChain(
            leader, session.Players, 60, settled, 2, session.Players);

        Assert.That(RaceSession.ForwardDistance(player.position, leader.position, 60),
            Is.EqualTo(cue.expectedSlipstreamDistance));
        Assert.That(playerChain.Triggered, Is.True);
        Assert.That(playerChain.TotalBonus, Is.EqualTo(2));
        Assert.That(leaderChain.Triggered, Is.False);
    }

    [Test]
    public void PlayerCheckpointsRebuildExactZonesAndPreserveHeatConservation()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();

        foreach (TutorialPlayerCheckpoint checkpoint in scenario.playerCheckpoints)
        {
            var player = new PlayerState("player", false, 99, 4)
            {
                teamId = TeamId.UK,
                spinCounter = 2,
                skipNextTurn = true,
                pitStopRequested = true,
                pitStopScheduled = true
            };

            TutorialCheckpointApplyResult result = TutorialCheckpointRules.ApplyPlayerCheckpoint(
                scenario, checkpoint, player);

            Assert.That(result.success, Is.True, $"Checkpoint {checkpoint.step}: {result.failureReason}");
            Assert.That(player.position, Is.EqualTo(checkpoint.playerCell), checkpoint.step.ToString());
            Assert.That(player.gear, Is.EqualTo(checkpoint.gear), checkpoint.step.ToString());
            Assert.That(player.deck.HandCount,
                Is.EqualTo(checkpoint.normalHandSize + checkpoint.heatInHand));
            Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(checkpoint.heatInHand));
            Assert.That(player.deck.CountHeatInDiscardPile(), Is.EqualTo(checkpoint.heatInDiscard));
            Assert.That(player.deck.heatPool.remaining + player.deck.CountPermanentHeatOutsideEngine(),
                Is.EqualTo(scenario.engineHeatCapacity));
            Assert.That(player.deck.UsesExactOrder, Is.True);
            Assert.That(player.spinCounter, Is.Zero);
            Assert.That(player.skipNextTurn, Is.False);
            Assert.That(player.pitStopRequested, Is.False);
            Assert.That(player.pitStopScheduled, Is.False);
        }
    }

    [Test]
    public void MissingCardAndSpinCheckpointsGuaranteeTheirFailureConditions()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        TutorialPlayerCheckpoint missing = scenario.playerCheckpoints.Single(
            item => item.step == TutorialStepId.MissingCardPenalty);
        TutorialPlayerCheckpoint spin = scenario.playerCheckpoints.Single(
            item => item.step == TutorialStepId.CornerLimitAndSpin);
        var player = new PlayerState("player", false, 0, 1) { teamId = TeamId.UK };

        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, missing, player).success, Is.True);
        Assert.That(player.deck.CountSpeedInHand(), Is.EqualTo(1));
        Assert.That(player.deck.heatPool.remaining, Is.EqualTo(1));
        Assert.That(RaceRules.GetMissingSpeedCardCount(2, player.deck.CountSpeedInHand()), Is.EqualTo(1));

        Assert.That(TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, spin, player).success, Is.True);
        Assert.That(player.position, Is.EqualTo(8));
        Assert.That(player.deck.heatPool.remaining, Is.Zero);
        Assert.That(player.deck.Hand.Take(2).Sum(card => card.value), Is.EqualTo(5));
        Assert.That(player.deck.CountPermanentHeatOutsideEngine(), Is.EqualTo(6));
    }

    [Test]
    public void TutorialVirtualPitReusesNormalPitRulesWithoutMutatingOfficialNodes()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var officialNodes = Enumerable.Range(0, 142)
            .Select(index => new TrackNode(index, 0, "Official"))
            .ToList();
        IReadOnlyList<TrackNode> tutorialNodes = TutorialCheckpointRules.CreateVirtualPitRuleNodes(
            officialNodes.Count, scenario.tutorialPitLane);

        Assert.That(PitLaneRules.HasPitLane(officialNodes), Is.False);
        Assert.That(PitLaneRules.HasPitLane(tutorialNodes), Is.True);
        Assert.That(PitLaneRules.GetDistanceToPitEntry(130, tutorialNodes), Is.EqualTo(2));
        Assert.That(PitLaneRules.CrossedPitEntry(130, 132, tutorialNodes), Is.True);
        Assert.That(officialNodes.Any(node => node.isPitEntry || node.isPitExit), Is.False);

        var player = new PlayerState("player", false, 132, 1) { teamId = TeamId.UK };
        PitStopResult result = PitLaneRules.EnterPit(player, tutorialNodes, exitMoveBonus: 1);

        Assert.That(result.success, Is.True);
        Assert.That(result.pitExitPosition, Is.EqualTo(scenario.tutorialPitLane.exitCell));
        Assert.That(result.exitPosition, Is.EqualTo(scenario.tutorialPitLane.exitCell + 1));
    }

    [Test]
    public void SconeAndTeaCheckpointsGuaranteeActualUkCardsAndRequiredHeat()
    {
        TutorialScenarioDefinition scenario = TutorialScenarioDefinition.CreateLeMansUk();
        var player = new PlayerState("player", false, 0, 1) { teamId = TeamId.UK };

        TutorialPlayerCheckpoint scone = scenario.playerCheckpoints.Single(
            item => item.step == TutorialStepId.UkScone);
        TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, scone, player);
        Assert.That(player.deck.Hand.Any(card => card.trickId == "uk-scone"), Is.True);
        Assert.That(player.deck.heatPool.remaining, Is.GreaterThanOrEqualTo(1));

        TutorialPlayerCheckpoint tea = scenario.playerCheckpoints.Single(
            item => item.step == TutorialStepId.UkEnglishBreakfastTea);
        TutorialCheckpointRules.ApplyPlayerCheckpoint(scenario, tea, player);
        Assert.That(player.deck.Hand.Any(card => card.trickId == "uk-english-breakfast-tea"), Is.True);
        Assert.That(player.deck.CountHeatInHand(), Is.EqualTo(1));
    }

    private static string CardLabel(CardData card)
    {
        return card.ToString();
    }
}
