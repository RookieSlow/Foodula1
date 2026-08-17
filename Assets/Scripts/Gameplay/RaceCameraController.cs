using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Follows the player's local track window and presents the complete track in a minimap.
/// </summary>
public sealed class RaceCameraController : MonoBehaviour
{
    private static readonly Color MinimapFrameColor = new Color(0.025f, 0.04f, 0.06f, 0.94f);
    private static readonly Color PlayerMarkerColor = new Color(1f, 0.28f, 0.18f, 1f);
    private static readonly Color AiMarkerColor = new Color(0.18f, 0.65f, 1f, 1f);

    private readonly List<Vector3> trackPositions = new List<Vector3>();
    private readonly RaceCameraFocusState focusState = new RaceCameraFocusState();

    private MVPGameManager gameManager;
    private TrackManager trackManager;
    private GameConfigSO config;
    private Camera mainCamera;
    private Camera minimapCamera;
    private RenderTexture minimapTexture;
    private RectTransform minimapContent;
    private RectTransform playerMarker;
    private RectTransform aiMarker;
    private Vector3 positionVelocity;
    private float zoomVelocity;
    private Transform automaticFocusTarget;
    private bool dragArmed;
    private Vector3 lastPointerPosition;
    private float maximumManualOrthographicSize;
    private bool initialized;

    public bool ManualOverrideThisTurn => focusState.ManualOverrideThisTurn;

    /// <summary>
    /// Connects the controller to the current race and creates its optional minimap UI.
    /// </summary>
    public void Initialize(MVPGameManager manager, Canvas raceCanvas)
    {
        gameManager = manager;
        trackManager = manager != null ? manager.Track : null;
        config = manager != null ? manager.Config : null;
        mainCamera = GetComponent<Camera>();

        if (mainCamera == null || trackManager == null || config == null)
        {
            Debug.LogWarning("[RaceCameraController] Missing camera, track, or config; camera setup skipped.");
            enabled = false;
            return;
        }

        CacheTrackPositions();
        if (trackPositions.Count == 0)
        {
            Debug.LogWarning("[RaceCameraController] Track has no positions; camera setup skipped.");
            enabled = false;
            return;
        }

        mainCamera.orthographic = true;
        ConfigureMainViewport(raceCanvas);
        CreateMinimap(raceCanvas);
        ReserveStatusArea();
        automaticFocusTarget = gameManager.PlayerCarTransform;
        focusState.BeginTurn();
        maximumManualOrthographicSize = CalculateFullTrackOrthographicSize();
        initialized = true;
        UpdateMainCamera(true);
        UpdateMinimapMarkers();
    }

    private void ConfigureMainViewport(Canvas raceCanvas)
    {
        mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
        if (raceCanvas == null)
            return;

        RaceUILayoutController layout = raceCanvas.GetComponent<RaceUILayoutController>();
        if (layout != null)
            mainCamera.rect = layout.TrackViewport;
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        HandleManualInput();
        if (focusState.AllowsAutomaticFocus)
            UpdateMainCamera(false);
        UpdateMinimapMarkers();
    }

    /// <summary>Restores automatic player focus at the beginning of a race turn.</summary>
    public void BeginTurn()
    {
        focusState.BeginTurn();
        dragArmed = false;
        SetAutomaticFocus(gameManager != null ? gameManager.PlayerCarTransform : null);
    }

    /// <summary>Returns to the player's car after card play, unless manually overridden.</summary>
    public void FocusPlayerAfterCardPlay()
    {
        SetAutomaticFocus(gameManager != null ? gameManager.PlayerCarTransform : null);
    }

    /// <summary>Follows the vehicle that is currently resolving movement.</summary>
    public void BeginVehicleMovement(Transform movingVehicle)
    {
        SetAutomaticFocus(movingVehicle);
    }

    /// <summary>Returns automatic focus to the player after movement finishes.</summary>
    public void EndVehicleMovement()
    {
        SetAutomaticFocus(gameManager != null ? gameManager.PlayerCarTransform : null);
    }

    private void OnDestroy()
    {
        if (minimapCamera != null)
        {
            Destroy(minimapCamera.gameObject);
            minimapCamera = null;
        }

        if (minimapTexture != null)
        {
            minimapTexture.Release();
            Destroy(minimapTexture);
        }
    }

    private void CacheTrackPositions()
    {
        trackPositions.Clear();
        for (int i = 0; i < trackManager.TotalNodes; i++)
        {
            trackPositions.Add(trackManager.GetNodePosition(i));
        }
    }

    private void UpdateMainCamera(bool snap)
    {
        Transform focusTarget = automaticFocusTarget != null
            ? automaticFocusTarget
            : gameManager.PlayerCarTransform;
        if (focusTarget == null)
        {
            return;
        }

        int centerIndex = RaceCameraRules.FindClosestPositionIndex(
            trackPositions,
            focusTarget.position);
        Bounds windowBounds = RaceCameraRules.CalculateWindowBounds(
            trackPositions,
            centerIndex,
            config.cameraCellsBehind,
            config.cameraCellsAhead);

        Vector3 targetPosition = new Vector3(
            windowBounds.center.x,
            windowBounds.center.y,
            mainCamera.transform.position.z);
        float targetSize = RaceCameraRules.CalculateOrthographicSize(
            windowBounds,
            mainCamera.aspect,
            config.cameraPaddingMultiplier,
            config.cameraMinimumOrthographicSize);

        if (snap)
        {
            mainCamera.transform.position = targetPosition;
            mainCamera.orthographicSize = targetSize;
            return;
        }

        mainCamera.transform.position = Vector3.SmoothDamp(
            mainCamera.transform.position,
            targetPosition,
            ref positionVelocity,
            Mathf.Max(0.01f, config.cameraPositionSmoothTime));
        mainCamera.orthographicSize = Mathf.SmoothDamp(
            mainCamera.orthographicSize,
            targetSize,
            ref zoomVelocity,
            Mathf.Max(0.01f, config.cameraZoomSmoothTime));
    }

    private void SetAutomaticFocus(Transform target)
    {
        if (!focusState.AllowsAutomaticFocus || target == null)
            return;

        automaticFocusTarget = target;
    }

    private void HandleManualInput()
    {
        if (mainCamera == null)
            return;

        Vector3 pointerPosition = Input.mousePosition;
        bool pointerInsideViewport = mainCamera.pixelRect.Contains(pointerPosition);

        if ((Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(2)) && pointerInsideViewport)
        {
            dragArmed = true;
            lastPointerPosition = pointerPosition;
        }

        if (dragArmed && (Input.GetMouseButton(0) || Input.GetMouseButton(2)))
        {
            Vector2 delta = pointerPosition - lastPointerPosition;
            lastPointerPosition = pointerPosition;
            if (delta.sqrMagnitude >= 0.25f)
            {
                TakeManualControl();
                mainCamera.transform.position += RaceCameraRules.CalculateDragWorldOffset(
                    delta,
                    mainCamera.orthographicSize,
                    mainCamera.pixelHeight,
                    config.cameraDragSensitivity);
            }
        }

        if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(2))
            dragArmed = false;

        float scrollDelta = Input.mouseScrollDelta.y;
        if (pointerInsideViewport && Mathf.Abs(scrollDelta) > 0.001f)
        {
            TakeManualControl();
            mainCamera.orthographicSize = RaceCameraRules.CalculateScrolledOrthographicSize(
                mainCamera.orthographicSize,
                scrollDelta,
                config.cameraZoomSensitivity,
                config.cameraMinimumOrthographicSize,
                maximumManualOrthographicSize);
        }
    }

    private void TakeManualControl()
    {
        focusState.TakeManualControl();
        positionVelocity = Vector3.zero;
        zoomVelocity = 0f;
    }

    private float CalculateFullTrackOrthographicSize()
    {
        Bounds trackBounds = RaceCameraRules.CalculateTrackBounds(trackPositions);
        return RaceCameraRules.CalculateOrthographicSize(
            trackBounds,
            Mathf.Max(0.01f, mainCamera.aspect),
            config.minimapWorldPaddingMultiplier,
            config.cameraMinimumOrthographicSize);
    }

    private void CreateMinimap(Canvas raceCanvas)
    {
        if (raceCanvas == null)
        {
            Debug.LogWarning("[RaceCameraController] Race canvas not found; minimap UI skipped.");
            return;
        }

        GameObject cameraObject = new GameObject("MinimapCamera");
        minimapCamera = cameraObject.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.clearFlags = mainCamera.clearFlags;
        minimapCamera.backgroundColor = mainCamera.backgroundColor;
        minimapCamera.cullingMask = mainCamera.cullingMask;
        minimapCamera.depth = mainCamera.depth - 1f;

        int resolutionWidth = Mathf.Max(64, config.minimapRenderWidth);
        int resolutionHeight = Mathf.Max(64, config.minimapRenderHeight);
        minimapTexture = new RenderTexture(
            resolutionWidth,
            resolutionHeight,
            16,
            RenderTextureFormat.ARGB32)
        {
            name = "RaceMinimapTexture",
            filterMode = FilterMode.Bilinear
        };
        minimapTexture.Create();
        minimapCamera.targetTexture = minimapTexture;

        Bounds trackBounds = RaceCameraRules.CalculateTrackBounds(trackPositions);
        float minimapAspect = (float)resolutionWidth / resolutionHeight;
        minimapCamera.orthographicSize = RaceCameraRules.CalculateOrthographicSize(
            trackBounds,
            minimapAspect,
            config.minimapWorldPaddingMultiplier,
            0.01f);
        minimapCamera.transform.position = new Vector3(
            trackBounds.center.x,
            trackBounds.center.y,
            mainCamera.transform.position.z);

        Transform minimapParent = raceCanvas.transform;
        RaceUILayoutController layout = raceCanvas.GetComponent<RaceUILayoutController>();
        if (layout != null && layout.TrackFrame != null)
            minimapParent = layout.TrackFrame;

        GameObject frameObject = CreateUiObject("MinimapFrame", minimapParent);
        RectTransform frameRect = frameObject.GetComponent<RectTransform>();
        frameRect.anchorMin = Vector2.one;
        frameRect.anchorMax = Vector2.one;
        frameRect.pivot = Vector2.one;
        frameRect.anchoredPosition = new Vector2(
            -config.minimapMargin.x,
            -config.minimapMargin.y);
        frameRect.sizeDelta = config.minimapSize;
        Image frameImage = frameObject.AddComponent<Image>();
        frameImage.color = MinimapFrameColor;

        GameObject contentObject = CreateUiObject("MinimapContent", frameObject.transform);
        minimapContent = contentObject.GetComponent<RectTransform>();
        minimapContent.anchorMin = Vector2.zero;
        minimapContent.anchorMax = Vector2.one;
        minimapContent.offsetMin = Vector2.one * config.minimapFrameThickness;
        minimapContent.offsetMax = -Vector2.one * config.minimapFrameThickness;
        RawImage rawImage = contentObject.AddComponent<RawImage>();
        rawImage.texture = minimapTexture;

        playerMarker = CreateMarker("PlayerMarker", minimapContent, PlayerMarkerColor);
        aiMarker = CreateMarker("AIMarker", minimapContent, AiMarkerColor);
    }

    private RectTransform CreateMarker(string markerName, Transform parent, Color color)
    {
        GameObject markerObject = CreateUiObject(markerName, parent);
        RectTransform markerRect = markerObject.GetComponent<RectTransform>();
        markerRect.anchorMin = Vector2.one * 0.5f;
        markerRect.anchorMax = Vector2.one * 0.5f;
        markerRect.sizeDelta = Vector2.one * config.minimapMarkerSize;
        Image markerImage = markerObject.AddComponent<Image>();
        markerImage.color = color;
        markerImage.raycastTarget = false;
        return markerRect;
    }

    private void UpdateMinimapMarkers()
    {
        if (minimapCamera == null || minimapContent == null)
        {
            return;
        }

        UpdateMarker(playerMarker, gameManager.PlayerCarTransform);
        UpdateMarker(aiMarker, gameManager.AICarTransform);
    }

    private void UpdateMarker(RectTransform marker, Transform car)
    {
        if (marker == null)
        {
            return;
        }

        marker.gameObject.SetActive(car != null);
        if (car == null)
        {
            return;
        }

        Vector3 viewport = minimapCamera.WorldToViewportPoint(car.position);
        marker.anchorMin = new Vector2(viewport.x, viewport.y);
        marker.anchorMax = marker.anchorMin;
        marker.anchoredPosition = Vector2.zero;
    }

    private void ReserveStatusArea()
    {
        if (gameManager.hudUI == null)
        {
            return;
        }

        // RaceUILayoutController gives the scoreboard its own reserved column;
        // shifting those labels for the minimap would push them out of place.
        if (gameManager.hudUI.GetComponentInParent<RaceUILayoutController>() != null)
        {
            return;
        }

        float verticalOffset = config.minimapSize.y + config.minimapMargin.y + 12f;
        MoveStatusText(gameManager.hudUI.gearText, verticalOffset);
        MoveStatusText(gameManager.hudUI.heatText, verticalOffset);
        MoveStatusText(gameManager.hudUI.lapText, verticalOffset);
        MoveStatusText(gameManager.hudUI.positionText, verticalOffset);
        MoveStatusText(gameManager.hudUI.aiStatusText, verticalOffset);
    }

    private static void MoveStatusText(Component textComponent, float verticalOffset)
    {
        if (textComponent == null)
        {
            return;
        }

        RectTransform rect = textComponent.GetComponent<RectTransform>();
        rect.anchoredPosition += Vector2.down * verticalOffset;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject uiObject = new GameObject(objectName, typeof(RectTransform));
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }
}
