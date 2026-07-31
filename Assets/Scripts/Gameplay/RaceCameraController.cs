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
    private bool initialized;

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
        CreateMinimap(raceCanvas);
        ReserveStatusArea();
        initialized = true;
        UpdateMainCamera(true);
        UpdateMinimapMarkers();
    }

    private void LateUpdate()
    {
        if (!initialized)
        {
            return;
        }

        UpdateMainCamera(false);
        UpdateMinimapMarkers();
    }

    private void OnDestroy()
    {
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
        Transform playerCar = gameManager.PlayerCarTransform;
        if (playerCar == null)
        {
            return;
        }

        int centerIndex = RaceCameraRules.FindClosestPositionIndex(
            trackPositions,
            playerCar.position);
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

    private void CreateMinimap(Canvas raceCanvas)
    {
        if (raceCanvas == null)
        {
            Debug.LogWarning("[RaceCameraController] Race canvas not found; minimap UI skipped.");
            return;
        }

        GameObject cameraObject = new GameObject("MinimapCamera");
        cameraObject.transform.SetParent(transform, false);
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

        GameObject frameObject = CreateUiObject("MinimapFrame", raceCanvas.transform);
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

