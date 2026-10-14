using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Four detents with a visual-only drag preview and one HUD request on release.</summary>
public sealed class TeamGearDragHandleUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private TeamGearPresentationUI view;
    private RectTransform handle;
    private bool allowed;
    private bool pressed;
    private bool dragging;
    private int pointerId;
    private int capturedActual;
    private int actual;
    private int pending;
    private int preview;
    public bool IsDragging => dragging;
    public int PreviewGear => preview;

    public static TeamGearDragHandleUI Attach(Transform parent, TeamGearPresentationUI owner)
    {
        Transform old = parent.Find("UsDragRail");
        GameObject host = old != null ? old.gameObject : new GameObject("UsDragRail", typeof(RectTransform), typeof(CanvasRenderer), typeof(TeamGearGraphic));
        host.transform.SetParent(parent, false);
        TeamGearGraphic rail = host.GetComponent<TeamGearGraphic>();
        rail.Configure(TeamGearGraphic.Shape.DragRail, TeamId.US);
        rail.raycastTarget = true;
        TeamGearDragHandleUI controller = host.GetComponent<TeamGearDragHandleUI>();
        if (controller == null) controller = host.AddComponent<TeamGearDragHandleUI>();
        controller.view = owner;
        Transform existing = host.transform.Find("DragGrip");
        GameObject grip = existing != null ? existing.gameObject : new GameObject("DragGrip", typeof(RectTransform), typeof(CanvasRenderer), typeof(TeamGearGraphic));
        grip.transform.SetParent(host.transform, false);
        grip.GetComponent<TeamGearGraphic>().Configure(TeamGearGraphic.Shape.DragHandle, TeamId.US);
        grip.GetComponent<TeamGearGraphic>().raycastTarget = false;
        controller.handle = grip.GetComponent<RectTransform>();
        return controller;
    }

    public void SetState(bool interactable, int current, int selected)
    {
        actual = current;
        pending = selected;
        allowed = interactable;
        if ((pressed || dragging) && (!allowed || capturedActual != current)) Cancel();
        if (!pressed && !dragging) MoveGrip(selected);
    }

    public void OnPointerDown(PointerEventData data)
    {
        if (!allowed || data.button != PointerEventData.InputButton.Left) return;
        pressed = true;
        pointerId = data.pointerId;
        capturedActual = actual;
        preview = pending;
    }

    public void OnBeginDrag(PointerEventData data)
    {
        if (!pressed || data.pointerId != pointerId || !allowed) return;
        dragging = true;
        data.eligibleForClick = false;
        Preview(data);
    }

    public void OnDrag(PointerEventData data)
    {
        if (!dragging || data.pointerId != pointerId || !allowed) return;
        Preview(data);
    }

    public void OnPointerUp(PointerEventData data)
    {
        if (!pressed || data.pointerId != pointerId || dragging) return;
        Preview(data);
        Release();
    }

    public void OnEndDrag(PointerEventData data)
    {
        if (!dragging || data.pointerId != pointerId) return;
        Preview(data);
        Release();
    }

    private void Preview(PointerEventData data)
    {
        RectTransform rail = transform as RectTransform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rail, data.position, data.pressEventCamera, out Vector2 local)) return;
        float t = Mathf.InverseLerp(rail.rect.yMin, rail.rect.yMax, local.y);
        preview = view.ResolveAbsoluteTarget(Mathf.Clamp(Mathf.RoundToInt((0.875f - t) / 0.25f) + 1, 1, 4));
        view.SetDragPreview(preview);
        MoveGrip(preview);
    }

    private void Release()
    {
        int target = preview;
        bool send = allowed && capturedActual == actual;
        pressed = false;
        dragging = false;
        view.SetDragPreview(null);
        MoveGrip(pending);
        if (send) view.RequestDraggedGear(target, capturedActual);
    }

    private void Cancel()
    {
        pressed = false;
        dragging = false;
        if (view != null) view.SetDragPreview(null);
        MoveGrip(pending);
    }

    private void MoveGrip(int gear)
    {
        if (handle == null) return;
        float y = 0.875f - (Mathf.Clamp(gear, 1, 4) - 1) * 0.25f;
        handle.anchorMin = new Vector2(0.12f, y);
        handle.anchorMax = new Vector2(0.88f, y);
        handle.pivot = new Vector2(0.5f, 0.5f);
        handle.anchoredPosition = Vector2.zero;
        handle.sizeDelta = new Vector2(0f, 12f);
        handle.GetComponent<TeamGearGraphic>().SetGateState(gear == actual, true, allowed);
    }

    private void OnDisable() { if (pressed || dragging) Cancel(); }
}
