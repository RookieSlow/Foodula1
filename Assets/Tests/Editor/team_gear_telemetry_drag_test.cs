using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed partial class TeamGearPresentationTests
{
    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void ActualAndPendingHaveDifferentFilledGeometryAndOverlapRetainsBothColors(TeamId team)
    {
        Build(team, 1);
        hud.gear2Button.onClick.Invoke();
        AssertFaceHasColor(hud.gear1Button, RacingTechInstrumentStyle.ActualFill);
        AssertFaceHasColor(hud.gear2Button, RacingTechInstrumentStyle.PendingFill);
        hud.confirmGearButton.onClick.Invoke();
        AssertFaceHasColor(hud.gear1Button, RacingTechInstrumentStyle.ActualFill);
        Assert.That(player.gear, Is.EqualTo(1), "Confirm must not move the actual fill to pending.");
        ApplyChoice();
        AssertFaceHasColor(hud.gear2Button, RacingTechInstrumentStyle.ActualFill);
        AssertFaceHasColor(hud.gear2Button, RacingTechInstrumentStyle.PendingFill);
    }

    [TestCase(1, 4, 3)]
    [TestCase(4, 1, 2)]
    [TestCase(2, 4, 4)]
    public void RenderedUsDragPreviewsWithoutCommandsThenRequestsOneLegalChoiceOnRelease(int actual, int target, int expected)
    {
        using (var scope = CreateGearPointerScope())
        {
            BuildRenderedUs(scope, actual);
            PointerEventData pointer = BeginUsDrag(scope, actual, target);
            Assert.That(View.DragHandle.IsDragging, Is.True);
            Assert.That(View.DragHandle.PreviewGear, Is.EqualTo(expected));
            Assert.That(input.PendingGear, Is.EqualTo(actual));
            Assert.That(player.gear, Is.EqualTo(actual));
            Assert.That(View.DetailLabel.text, Does.Contain($"出牌 {expected}张"));
            ExecuteEvents.Execute(View.DragHandle.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(View.DragHandle.IsDragging, Is.False);
            Assert.That(input.PendingGear, Is.EqualTo(expected));
            Assert.That(View.CurrentGear, Is.EqualTo(actual));
            // PointerUp after EndDrag must not dispatch a second selection.
            ExecuteEvents.Execute(View.DragHandle.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            Assert.That(input.PendingGear, Is.EqualTo(expected));
            scope.Click(hud.confirmGearButton);
            Assert.That(input.WaitingForGear, Is.False);
            Assert.That(View.CurrentGear, Is.EqualTo(actual));
        }
    }

    [Test]
    public void UsDragReleaseUsesExistingSelectionConfirmationAndCancelRestoresControl()
    {
        using (var scope = CreateGearPointerScope())
        {
            BuildRenderedUs(scope, 2);
            GameSettingsRuntime.Current.inRaceConfirmationMask = (int)InRaceConfirmationAction.GearSelection;
            PointerEventData pointer = BeginUsDrag(scope, 2, 4);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False, "Preview must not open confirmation.");
            ExecuteEvents.Execute(View.DragHandle.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(input.PendingGear, Is.EqualTo(2));
            Assert.That(View.ControlsInteractable, Is.False);
            scope.Click(hud.cancelReturnToMenuButton);
            Assert.That(View.ControlsInteractable, Is.True);
            pointer = BeginUsDrag(scope, 2, 3);
            ExecuteEvents.Execute(View.DragHandle.gameObject, pointer, ExecuteEvents.endDragHandler);
            scope.Click(hud.confirmReturnToMenuButton);
            Assert.That(input.PendingGear, Is.EqualTo(3));
            Assert.That(View.CurrentGear, Is.EqualTo(2));
        }
    }

    [TestCase(false)]
    [TestCase(true)]
    public void UsDragCancelsIfPhaseOrControlsCloseDuringTheGesture(bool explicitGate)
    {
        using (var scope = CreateGearPointerScope())
        {
            BuildRenderedUs(scope, 2);
            PointerEventData pointer = BeginUsDrag(scope, 2, 4);
            if (explicitGate) hud.SetTeamGearControlsInteractable(false);
            else { phase.BeginCardSelection(); hud.RefreshTeamGearPresentation(); }
            Assert.That(View.DragHandle.IsDragging, Is.False);
            ExecuteEvents.Execute(View.DragHandle.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(input.PendingGear, Is.EqualTo(2));
            Assert.That(player.gear, Is.EqualTo(2));
        }
    }

    [Test]
    public void UsDragRejectsAnActualGearChangeEvenWithoutARefreshDuringTheGesture()
    {
        using (var scope = CreateGearPointerScope())
        {
            BuildRenderedUs(scope, 1);
            PointerEventData pointer = BeginUsDrag(scope, 1, 4);
            player.gear = 4;
            ExecuteEvents.Execute(View.DragHandle.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(input.PendingGear, Is.EqualTo(1));
            Assert.That(View.CurrentGear, Is.EqualTo(4));
        }
    }

    [Test]
    public void TelemetryReadsRecordedSourcesAndRealHeatWithoutChangingLedgerOrAudioState()
    {
        Build(TeamId.JP, 2);
        player.turnMovement.BeginBasePlan();
        player.turnMovement.AddPlan("速度牌", 4, TurnMovementKind.Card);
        player.turnMovement.AddPlan("车手", 2, TurnMovementKind.Driver);
        player.turnMovement.AddPlan("科技", 1, TurnMovementKind.Technology);
        player.turnMovement.AddPlan("地标", 1, TurnMovementKind.Other);
        player.turnMovement.ReconcileBaseTotal(8);
        player.turnMovement.Advance(3);
        player.turnMovement.RecordRewind(2);
        player.deck.DrawHeatFromPool(2);
        player.deck.AddCardsToHand(new[] { CardData.CreateTempHeat() });
        hud.RefreshPlayerResources(player);
        Assert.That(View.Telemetry.MovementText, Does.Contain("已前进 3 / 预计 8"));
        Assert.That(View.Telemetry.VisiblePortionCount, Is.EqualTo(8));
        Assert.That(View.Telemetry.HeatState.TotalHeat, Is.EqualTo(3));
        Assert.That(View.Telemetry.HeatState.Capacity, Is.EqualTo(6));
        Assert.That(View.Telemetry.HeatState.WarningLevel, Is.EqualTo(HeatWarningLevel.Elevated));
        string copy = View.Telemetry.FormatDetails();
        Assert.That(copy, Does.Contain("速度牌").And.Contain("车手").And.Contain("科技").And.Contain("地标"));
        Assert.That(copy, Does.Contain("回退：2").And.Contain("临时热量 1"));
        hud.SetEnginePresentation(1f);
        hud.RefreshTurnTelemetry(player);
        Assert.That(View.Telemetry.MovementText, Does.Contain("已前进 3 / 预计 8"));
        Assert.That(View.Telemetry.HeatState.TotalHeat, Is.EqualTo(3));
        Assert.That(player.turnMovement.ActualCells, Is.EqualTo(3));
        Assert.That(player.turnMovement.RewindCells, Is.EqualTo(2));
    }

    [Test]
    public void DetailsExplainPositiveContributionsCancelledByAnAuthoritativeCorrection()
    {
        Build(TeamId.UK, 2);
        player.turnMovement.BeginBasePlan();
        player.turnMovement.AddPlan("速度牌", 4, TurnMovementKind.Card);
        player.turnMovement.ReconcileBaseTotal(0);
        hud.RefreshTurnTelemetry(player);
        Assert.That(View.Telemetry.FormatDetails(), Does.Contain("速度牌    +4 格 · 结算后 0 格").And.Contain("-4 格（修正）"));
        Assert.That(View.Telemetry.MovementText, Does.Contain("已前进 0 / 预计 0"));
        Assert.That(View.Telemetry.VisiblePortionCount, Is.Zero);
    }

    [Test]
    public void CommittedCardForecastIsReadOnlyAndDisappearsWhenTheBasePlanArrives()
    {
        Build(TeamId.CN, 2);
        phase.BeginCardSelection(); input.BeginCardSelection();
        manager.Session.TeamVehicleBonusesEnabled = false;
        player.playedSpeedCardsThisTurn.Add(new CardData(CardType.Speed, 4));
        player.turnMovement.RecordInstant("维修区", 2, TurnMovementKind.Other);
        hud.RefreshTurnTelemetry(player);
        Assert.That(View.Telemetry.MovementText, Does.Contain("已前进 2 / 预计 6"));
        Assert.That(View.Telemetry.FormatDetails(), Does.Contain("卡牌预估    +4 格（待结算）"));
        Assert.That(player.turnMovement.TotalCells, Is.EqualTo(2), "UI must not create a gameplay plan.");
        Assert.That(player.turnMovement.HasBasePlan, Is.False);
        player.turnMovement.BeginBasePlan();
        player.turnMovement.AddPlan("速度牌", 4, TurnMovementKind.Card);
        player.turnMovement.ReconcileBaseTotal(4);
        hud.RefreshTurnTelemetry(player);
        Assert.That(View.Telemetry.MovementText, Does.Contain("已前进 2 / 预计 6"));
        Assert.That(View.Telemetry.FormatDetails(), Does.Not.Contain("卡牌预估"));
    }

    [Test]
    public void RenderedMovementStripOpensAnIndependentScrollableModalWithoutGearConfirmation()
    {
        using (var scope = CreateGearPointerScope())
        {
            BuildRenderedUs(scope, 2);
            player.turnMovement.BeginBasePlan();
            for (int i = 0; i < 30; i++) player.turnMovement.AddPlan("来源 " + i, 1, TurnMovementKind.Other);
            player.turnMovement.ReconcileBaseTotal(30);
            hud.RefreshTurnTelemetry(player);
            Vector2 point = ScreenPoint(View.Telemetry.MovementButton.transform as RectTransform, new Vector2(0.5f, 0.5f), scope);
            Assert.That(hud.IsPointerOverTelemetry(point), Is.True, "The opening click must be protected before click dispatch.");
            scope.Click(View.Telemetry.MovementButton);
            Assert.That(hud.IsTelemetryDetailsOpen, Is.True);
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);
            Assert.That(View.ControlsInteractable, Is.False);
            Assert.That(hud.IsPointerOverTelemetry(Vector2.zero), Is.True, "The modal blocks movement skip over its entire surface.");
            hud.gear3Button.onClick.Invoke();
            hud.confirmGearButton.onClick.Invoke();
            Assert.That(input.PendingGear, Is.EqualTo(2));
            Assert.That(input.WaitingForGear, Is.True);
            ScrollRect scroll = scope.Root.transform.Find("TurnTelemetryDetails/DetailsCard/DetailsViewport").GetComponent<ScrollRect>();
            Canvas.ForceUpdateCanvases();
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.horizontal, Is.False);
            Assert.That(scroll.content.GetComponent<TMP_Text>().fontSize, Is.EqualTo(18f));
            player.turnMovement.Advance(2);
            hud.RefreshTurnTelemetry(player);
            Assert.That(scroll.content.GetComponent<TMP_Text>().text, Does.Contain("已前进 2 格"));
            scope.Click(scope.Root.transform.Find("TurnTelemetryDetails/DetailsCard/CloseTelemetryDetailsButton").GetComponent<Button>());
            Assert.That(hud.IsTelemetryDetailsOpen, Is.False);
            Assert.That(View.ControlsInteractable, Is.True);
            Assert.That(hud.IsPointerOverTelemetry(Vector2.zero), Is.False);
            PointerEventData pointer = BeginUsDrag(scope, 2, 3);
            ExecuteEvents.Execute(View.DragHandle.gameObject, pointer, ExecuteEvents.endDragHandler);
            Assert.That(input.PendingGear, Is.EqualTo(3));
        }
    }

    [Test]
    public void DetailsCloseForReconfigurationNewPlayerGameOverAndDisableCallback()
    {
        using (var scope = CreateGearPointerScope())
        {
            BuildRenderedUs(scope, 2);
            hud.ShowTelemetryDetails();
            hud.ConfigureGearPresentation(TeamId.US, 2);
            Assert.That(hud.IsTelemetryDetailsOpen, Is.False, "Reconfiguration must close the old detail window.");
            hud.ShowTelemetryDetails();
            var replacement = new PlayerState("replacement", false, 0, 2) { teamId = TeamId.US, deck = new CardDeck { heatPool = new HeatPool(8) } };
            hud.RefreshTurnTelemetry(replacement);
            Assert.That(hud.IsTelemetryDetailsOpen, Is.False, "Binding a replacement player must close the old detail window.");
            Assert.That(View.Telemetry.HeatState.Capacity, Is.EqualTo(8));
            hud.ShowTelemetryDetails();
            hud.ShowGameOver("结果");
            Assert.That(hud.IsTelemetryDetailsOpen, Is.False, "Game over must close the detail window.");
            hud.ShowTelemetryDetails();
            hud.enabled = false;
            // Like Start in Build, dispatch the production callback explicitly in synchronous EditMode.
            // This checks its cleanup, not Unity's player-loop scheduling of OnDisable.
            typeof(HUDUI).GetMethod("OnDisable", Private).Invoke(hud, null);
            Assert.That(hud.IsTelemetryDetailsOpen, Is.False, "The disable callback must close the detail window.");
        }
    }

    private static CareerUiPointerTestScope CreateGearPointerScope() => CareerUiPointerTestScope.Create(() =>
        AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TmpFont/StreamingAssets/Fonts & Materials/Chinese/SourceHanSansTC-Medium-HQ.asset"));

    [TestCase(TeamId.UK)]
    [TestCase(TeamId.DE)]
    [TestCase(TeamId.IT)]
    [TestCase(TeamId.US)]
    [TestCase(TeamId.CN)]
    [TestCase(TeamId.JP)]
    public void AuthoredRaceCanvasDocksSiblingHudButtonsAndRetainsReadableStableGeometry(TeamId team)
    {
        using (var scope = CreateGearPointerScope())
        {
            GameObject authored = null;
            try
            {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/RaceCanvas.prefab");
            Assert.That(prefab, Is.Not.Null);
            root = new GameObject("Authored canvas UI regression", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(scope.Root.transform, false);
            RectTransform hostRect = root.GetComponent<RectTransform>();
            hostRect.anchorMin = Vector2.zero; hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = hostRect.offsetMax = Vector2.zero;
            authored = Object.Instantiate(prefab, root.transform, false);
            foreach (TMP_Text label in authored.GetComponentsInChildren<TMP_Text>(true))
            {
                label.font = scope.Font;
                label.fontSharedMaterial = scope.Font.material;
            }
            // Match the real RaceCanvas and the screenshot fixture: one root screen-space Canvas.
            // A nested screen-space Canvas adds another camera plane transform in EditMode.
            authored.SetActive(false);
            authored.transform.SetParent(null, false);
            root = authored;
            Canvas outer = scope.Root.GetComponent<Canvas>();
            scope.Root.GetComponent<CanvasScaler>().enabled = false;
            outer.scaleFactor = 1f;
            Canvas canvas = authored.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = outer.worldCamera;
            canvas.planeDistance = 1f;
            CanvasScaler authoredScaler = authored.GetComponent<CanvasScaler>();
            if (authoredScaler != null) authoredScaler.enabled = false;
            canvas.scaleFactor = 1f;
            RectTransform canvasRect = authored.transform as RectTransform;
            canvasRect.anchorMin = Vector2.zero; canvasRect.anchorMax = Vector2.one;
            canvasRect.offsetMin = canvasRect.offsetMax = Vector2.zero;
            hud = authored.GetComponentInChildren<HUDUI>(true);
            GameObject managerHost = new GameObject("Inactive authored canvas manager");
            managerHost.SetActive(false);
            managerHost.transform.SetParent(authored.transform, false);
            manager = managerHost.AddComponent<MVPGameManager>();
            input = (RaceInputState)typeof(MVPGameManager).GetField("inputState", Private).GetValue(manager);
            phase = (RacePhaseState)typeof(MVPGameManager).GetField("phaseState", Private).GetValue(manager);
            config = ScriptableObject.CreateInstance<GameConfigSO>();
            player = new PlayerState("authored UI driver", false, 0, team == TeamId.CN ? 1 : 2) {
                teamId = team, deck = new CardDeck { heatPool = new HeatPool(6) }
            };
            var session = new RaceSession();
            session.Players.Add(player);
            typeof(MVPGameManager).GetField("session", Private).SetValue(manager, session);
            manager.config = config; manager.hudUI = hud;
            phase.BeginGearSelection(); input.BeginGearSelection(player.gear);
            typeof(HUDUI).GetMethod("Start", Private).Invoke(hud, null);
            hud.SetGameManager(manager);
            hud.ConfigureGearPresentation(team, player.gear);
            hud.SetTeamGearControlsInteractable(true);
            RaceUILayoutController layout = authored.GetComponent<RaceUILayoutController>();
            if (layout == null) layout = authored.AddComponent<RaceUILayoutController>();
            layout.ApplyLayout(hud, authored.GetComponentInChildren<CardHandUI>(true));
            root.SetActive(true);
            Assert.That(View.transform.IsChildOf(hud.transform), Is.False,
                "This prefab regression must retain the authored sibling layout that the logical fixture lacks.");
            Assert.That(View.transform.parent, Is.SameAs(layout.OperationPanel));
            RectTransform gearRect = View.transform as RectTransform;
            Assert.That(gearRect.anchorMin, Is.EqualTo(RaceUILayoutController.GearGridAnchorMin));
            ClickAuthoredButton(authored, scope, hud.gear2Button);
            Assert.That(gearRect.rect.height, Is.InRange(205f, 215f));
            if (team == TeamId.US)
                foreach (Button button in new[] { hud.gear1Button, hud.gear2Button, hud.gear3Button, hud.gear4Button })
                    Assert.That(button.GetComponent<RectTransform>().rect.height, Is.GreaterThanOrEqualTo(22f));
            foreach (TMP_Text label in View.GetComponentsInChildren<TMP_Text>())
                Assert.That(label.isTextOverflowing, Is.False, team + ": " + label.name + " must fit its actual rendered rect.");
            Vector2 size = gearRect.rect.size;
            Vector3 position = gearRect.position;
            layout.ApplyLayout(hud, null);
            View.ApplyLayout();
            hud.RefreshTeamGearPresentation();
            Canvas.ForceUpdateCanvases();
            Assert.That(gearRect.rect.size, Is.EqualTo(size));
            Assert.That(gearRect.position, Is.EqualTo(position));
            ClickAuthoredButton(authored, scope, View.Telemetry.MovementButton);
            Assert.That(hud.IsTelemetryDetailsOpen, Is.True);
            Assert.That(input.WaitingForGear, Is.True);
            Assert.That(player.gear, Is.EqualTo(team == TeamId.CN ? 1 : 2));
            }
            finally
            {
                // Destroy its TMP users before the scope releases its isolated font/material.
                if (authored != null) Object.DestroyImmediate(authored);
            }
        }
    }

    private static void ClickAuthoredButton(GameObject canvasRoot, CareerUiPointerTestScope scope, Button button)
    {
        foreach (Transform child in canvasRoot.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text label in canvasRoot.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        Camera camera = scope.Root.GetComponent<Canvas>().worldCamera;
        RenderTexture previous = RenderTexture.active;
        try { camera.Render(); } finally { RenderTexture.active = previous; }
        Vector2 center = ScreenPoint(button.transform as RectTransform, new Vector2(0.5f, 0.5f), scope);
        // Hardware mouse positions are integer pixels. Exact projected float centers can land on
        // a native quad diagonal and miss RectangleContainsScreenPoint through rounding error.
        center = new Vector2(Mathf.Round(center.x), Mathf.Round(center.y));
        Canvas owningCanvas = button.targetGraphic.canvas;
        Assert.That(owningCanvas.gameObject.scene, Is.EqualTo(scope.PreviewScene));
        Assert.That(button.transform.IsChildOf(canvasRoot.transform), Is.True);
        GraphicRaycaster raycaster = owningCanvas.GetComponent<GraphicRaycaster>();
        Assert.That(raycaster, Is.Not.Null, "Use the rendered Graphic's Canvas registry, including nested authored Canvases.");
        Assert.That(button.targetGraphic.depth, Is.GreaterThanOrEqualTo(0), button.name + " must be rendered before raycasting.");
        var pointer = new PointerEventData(scope.EventSystem) {
            button = PointerEventData.InputButton.Left,
            position = center
        };
        var hits = new List<RaycastResult>();
        raycaster.Raycast(pointer, hits);
        Assert.That(hits, Is.Not.Empty, button.name + " canvas=" + owningCanvas.name +
            " point=" + center + " contains=" + RectTransformUtility.RectangleContainsScreenPoint(button.targetGraphic.rectTransform, center, camera));
        GameObject receiver = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        Assert.That(receiver, Is.SameAs(button.gameObject), button.name + " must be the actual prefab top hit.");
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerClickHandler);
    }

    private void BuildRenderedUs(CareerUiPointerTestScope scope, int actual)
    {
        Build(TeamId.US, actual, scope.Root.transform, scope.Font);
        root.SetActive(true);
        root.AddComponent<RaceUILayoutController>().ApplyLayout(hud, null);
    }

    private PointerEventData BeginUsDrag(CareerUiPointerTestScope scope, int from, int to)
    {
        foreach (Transform child in scope.Root.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 5;
        Canvas.ForceUpdateCanvases();
        foreach (TMP_Text label in scope.Root.GetComponentsInChildren<TMP_Text>()) label.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();
        Camera camera = scope.Root.GetComponent<Canvas>().worldCamera;
        RenderTexture previous = RenderTexture.active;
        try { camera.Render(); }
        finally { RenderTexture.active = previous; }
        RectTransform rail = View.DragHandle.transform as RectTransform;
        var pointer = new PointerEventData(scope.EventSystem) {
            pointerId = -1, button = PointerEventData.InputButton.Left, eligibleForClick = true,
            position = ScreenPoint(rail, new Vector2(0.5f, 0.875f - (from - 1) * 0.25f), scope)
        };
        var hits = new List<RaycastResult>();
        GraphicRaycaster raycaster = scope.Root.GetComponent<GraphicRaycaster>();
        raycaster.Raycast(pointer, hits);
        Assert.That(hits, Is.Not.Empty);
        GameObject receiver = ExecuteEvents.GetEventHandler<IPointerDownHandler>(hits[0].gameObject);
        Assert.That(receiver, Is.SameAs(View.DragHandle.gameObject), "Only the left lever may intercept the drag gesture.");
        pointer.pointerPressRaycast = hits[0];
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.pointerDownHandler);
        pointer.position = ScreenPoint(rail, new Vector2(0.5f, 0.875f - (to - 1) * 0.25f), scope);
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.beginDragHandler);
        ExecuteEvents.Execute(receiver, pointer, ExecuteEvents.dragHandler);
        return pointer;
    }

    private static Vector2 ScreenPoint(RectTransform rect, Vector2 normalized, CareerUiPointerTestScope scope)
    {
        Vector2 local = rect.rect.min + Vector2.Scale(rect.rect.size, normalized);
        return RectTransformUtility.WorldToScreenPoint(scope.Root.GetComponent<Canvas>().worldCamera, rect.TransformPoint(local));
    }

    private static void AssertFaceHasColor(Button button, Color expected)
    {
        TeamGearGraphic graphic = button.targetGraphic as TeamGearGraphic;
        RectTransform rect = graphic.rectTransform;
        // In inactive logical tests there is no Canvas layout pass; use a real local rect for geometry.
        if (rect.rect.width <= 0f || rect.rect.height <= 0f) rect.sizeDelta = new Vector2(60f, 32f);
        Mesh mesh = new Mesh();
        try
        {
            using (var helper = new VertexHelper())
            {
                typeof(TeamGearGraphic).GetMethod("OnPopulateMesh", Private, null,
                    new[] { typeof(VertexHelper) }, null).Invoke(graphic, new object[] { helper });
                helper.FillMesh(mesh);
            }
            Assert.That(mesh.colors32, Does.Contain((Color32)expected), "Actual and pending colors must be present in the generated filled geometry.");
        }
        finally { Object.DestroyImmediate(mesh); }
    }
}
