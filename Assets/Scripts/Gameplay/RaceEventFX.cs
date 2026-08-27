using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Lightweight, asset-free race-event presentation layer.
/// It keeps spectacle out of the rules and movement code while still working
/// in both the authored RaceCanvas and the procedural fallback HUD.
/// </summary>
public sealed class RaceEventFX : MonoBehaviour
{
    public readonly struct SlipstreamVisualEvent
    {
        public Transform Follower { get; }
        public Transform Leader { get; }
        public int Bonus { get; }

        public SlipstreamVisualEvent(Transform follower, Transform leader, int bonus)
        {
            Follower = follower;
            Leader = leader;
            Bonus = bonus;
        }
    }

    private Canvas canvas;
    private RectTransform overlayRoot;
    private CanvasGroup canvasGroup;
    private Image panelImage;
    private Image accentImage;
    private TMP_Text titleText;
    private TMP_Text detailText;
    private TMP_FontAsset font;
    private bool initialized;
    private bool effectBusy;
    private bool timeScaleOverrideActive;

    private const float GameplayTimeScale = 1f;

    [Header("尾流阶段")]
    [Min(0.8f)]
    public float slipstreamDuration = 0.95f;
    [Tooltip("尾流特写与尾流奖励移动期间的全局时间倍率；阶段结束后恢复为 1")]
    [Range(0.1f, 1f)]
    public float slipstreamTimeScale = 0.28f;
    [Min(0f)]
    public float slipstreamLeadInDuration = 0.16f;
    [Min(0f)]
    public float slipstreamPostGapDuration = 0.16f;

    [Header("失控阶段")]
    [Tooltip("失控旋转提示的持续时间 (秒)，设计案基准为 1 秒")]
    [Min(0.01f)]
    public float spinOutDuration = 1f;
    [Tooltip("失控阶段赛车完成的旋转角度，设计案基准为 360 度")]
    [Min(0f)]
    public float spinOutRotationDegrees = 360f;

    /// <summary>
    /// Starts the scoped slow-motion window used by the separate tailwind
    /// bonus movement. The visual close-up has its own scope; this second
    /// window keeps the actual reward movement visibly slowed as well.
    /// </summary>
    public void BeginSlipstreamBonusMovementSlowMotion()
    {
        BeginSlowMotion(slipstreamTimeScale);
    }

    /// <summary>Ends the tailwind bonus-movement slow-motion window safely.</summary>
    public void EndSlipstreamBonusMovementSlowMotion()
    {
        EndSlowMotion();
    }

    private static readonly Color PanelColor = new Color(0.025f, 0.04f, 0.07f, 0.94f);
    private static readonly Color OvertakeColor = new Color(1f, 0.75f, 0.24f, 1f);
    private static readonly Color SlipstreamColor = new Color(0.12f, 0.76f, 1f, 1f);
    private static readonly Color SpinColor = new Color(1f, 0.47f, 0.22f, 1f);
    private static readonly Color BlowupColor = new Color(1f, 0.16f, 0.12f, 1f);

    /// <summary>Builds the centered event overlay once the race canvas exists.</summary>
    public void Initialize(Canvas targetCanvas, TMP_FontAsset targetFont)
    {
        if (initialized && canvas == targetCanvas)
            return;

        canvas = targetCanvas;
        font = targetFont;
        if (canvas == null)
        {
            initialized = false;
            return;
        }

        // Never reuse a runtime overlay by name. Unity can retain a destroyed
        // component wrapper on an editor-reloaded GameObject; reusing that
        // object would make a later property access throw MissingComponentException.
        // A fresh root is cheap and guarantees the required components exist.
        string overlayName = canvas.transform.Find("RaceEventOverlay") == null
            ? "RaceEventOverlay"
            : $"RaceEventOverlay_{GetInstanceID()}";
        GameObject root = new GameObject(
            overlayName,
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));
        root.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = GetOrAddComponent<RectTransform>(root);
        overlayRoot = rootRect;
        rootRect.anchorMin = rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.anchoredPosition = new Vector2(0f, 45f);
        rootRect.sizeDelta = new Vector2(760f, 156f);

        // Runtime-created UI can survive a scene reload in the editor with a
        // stale/missing component reference. Resolve each component explicitly
        // and add it when absent so a visual effect can never abort the race
        // manager's Start() coroutine.
        canvasGroup = root.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            initialized = false;
            return;
        }
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        canvasGroup.alpha = 0f;

        panelImage = root.GetComponent<Image>();
        if (panelImage == null)
        {
            initialized = false;
            return;
        }
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        accentImage = CreateImage(root.transform, "Accent", new Vector2(8f, 132f), new Vector2(680f, 6f));
        titleText = CreateText(root.transform, "Title", 42, new Vector2(0f, 18f), new Vector2(720f, 62f));
        detailText = CreateText(root.transform, "Detail", 21, new Vector2(0f, -40f), new Vector2(720f, 42f));

        root.transform.SetAsLastSibling();
        initialized = true;
    }

    /// <summary>
    /// Dedicated overtake close-up. The game is slowed only while this visual
    /// cue is running; rules and card state are unaffected.
    /// </summary>
    public IEnumerator PlayOvertake(Transform car, int count)
    {
        if (!initialized || car == null)
            yield break;

        yield return AcquireEffectSlot();
        SetMessage("OVERTAKE!", count > 1 ? $"超车 ×{count} · 领先车手抓住了机会" : "超车成功 · 领先车手抓住了机会", OvertakeColor);
        Vector3 originalScale = car.localScale;
        Quaternion originalRotation = car.rotation;
        BeginSlowMotion(0.28f);

        try
        {
            float elapsed = 0f;
            const float duration = 0.78f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = 1f + Mathf.Sin(t * Mathf.PI * 2f) * 0.09f * (1f - t * 0.35f);
                car.localScale = originalScale * pulse;
                car.rotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 2f) * 4f);
                canvasGroup.alpha = t < 0.12f ? t / 0.12f : (t > 0.78f ? (1f - t) / 0.22f : 1f);
                yield return null;
            }
        }
        finally
        {
            car.localScale = originalScale;
            car.rotation = originalRotation;
            EndSlowMotion();
            effectBusy = false;
            HideMessage();
        }
    }

    /// <summary>
    /// One merged slipstream phase for the turn. Animated UI dashes connect
    /// every follower/leader pair while both cars receive a restrained focus pulse.
    /// </summary>
    public IEnumerator PlaySlipstreams(IReadOnlyList<SlipstreamVisualEvent> events)
    {
        if (!initialized || events == null || events.Count == 0 || canvas == null)
            yield break;

        yield return AcquireEffectSlot();

        int totalBonus = 0;
        var originalScales = new Dictionary<Transform, Vector3>();
        var airflowRoots = new List<RectTransform>();
        for (int i = 0; i < events.Count; i++)
        {
            SlipstreamVisualEvent visualEvent = events[i];
            if (visualEvent.Follower == null || visualEvent.Leader == null || visualEvent.Bonus <= 0)
                continue;

            totalBonus += visualEvent.Bonus;
            RememberScale(originalScales, visualEvent.Follower);
            RememberScale(originalScales, visualEvent.Leader);
            airflowRoots.Add(CreateAirflowStrip($"SlipstreamAirflow_{i}"));
        }

        if (airflowRoots.Count == 0)
        {
            effectBusy = false;
            yield break;
        }

        string detail = events.Count == 1
            ? $"尾流 +{totalBonus} · 气流牵引"
            : $"尾流 ×{airflowRoots.Count} · 总加成 +{totalBonus}";
        SetMessage("尾流阶段", "速度牌已锁定 · 正在进入气流", SlipstreamColor);
        if (slipstreamLeadInDuration > 0f)
            yield return new WaitForSecondsRealtime(slipstreamLeadInDuration);
        SetMessage("SLIPSTREAM!", detail, SlipstreamColor);

        BeginSlowMotion(slipstreamTimeScale);
        try
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.8f, slipstreamDuration);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.11f;
                foreach (KeyValuePair<Transform, Vector3> pair in originalScales)
                {
                    if (pair.Key != null)
                        pair.Key.localScale = pair.Value * pulse;
                }

                int airflowIndex = 0;
                for (int i = 0; i < events.Count && airflowIndex < airflowRoots.Count; i++)
                {
                    SlipstreamVisualEvent visualEvent = events[i];
                    if (visualEvent.Follower == null || visualEvent.Leader == null || visualEvent.Bonus <= 0)
                        continue;
                    UpdateAirflowStrip(airflowRoots[airflowIndex], visualEvent.Follower, visualEvent.Leader, elapsed);
                    airflowIndex++;
                }

                canvasGroup.alpha = t < 0.12f ? t / 0.12f : (t > 0.78f ? (1f - t) / 0.22f : 1f);
                yield return null;
            }
        }
        finally
        {
            // The close-up owns the only global time-scale override in the
            // race. Restore it before cleaning up visual children so an
            // interrupted/failed visual can never slow later movement.
            EndSlowMotion();
            foreach (KeyValuePair<Transform, Vector3> pair in originalScales)
            {
                if (pair.Key != null)
                    pair.Key.localScale = pair.Value;
            }
            foreach (RectTransform airflowRoot in airflowRoots)
            {
                if (airflowRoot != null)
                    Destroy(airflowRoot.gameObject);
            }
            effectBusy = false;
            HideMessage();
        }
    }

    /// <summary>Spin-out cue: a full rotation, shake and explicit status text.</summary>
    public IEnumerator PlaySpin(Transform car, string reason, bool blown)
    {
        if (!initialized || car == null)
            yield break;

        yield return AcquireEffectSlot();
        SetMessage("SPIN OUT!", blown ? "失控后引擎爆缸 · 赛车退赛" : $"失控 · {reason} · 回退并跳过下回合", blown ? BlowupColor : SpinColor);
        Vector3 originalScale = car.localScale;
        Quaternion originalRotation = car.rotation;
        try
        {
            float elapsed = 0f;
            float duration = Mathf.Max(0.01f, spinOutDuration);
            float rotationDegrees = Mathf.Max(0f, spinOutRotationDegrees);
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = RaceEventPresentationRules.GetSpinProgress(elapsed, duration);
                float punch = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                car.localScale = originalScale * punch;
                car.rotation = originalRotation * Quaternion.Euler(
                    0f,
                    0f,
                    RaceEventPresentationRules.GetSpinRotation(t, rotationDegrees));
                canvasGroup.alpha = t < 0.1f ? t / 0.1f : (t > 0.82f ? (1f - t) / 0.18f : 1f);
                yield return null;
            }

            car.localScale = originalScale;
            car.rotation = originalRotation;

            if (blown)
                yield return PlayBlowupPulse(car, originalScale, originalRotation);
        }
        finally
        {
            car.localScale = originalScale;
            car.rotation = originalRotation;
            effectBusy = false;
            HideMessage();
        }
    }

    private IEnumerator AcquireEffectSlot()
    {
        while (effectBusy)
            yield return null;
        effectBusy = true;
    }

    private static void RememberScale(Dictionary<Transform, Vector3> scales, Transform target)
    {
        if (target != null && !scales.ContainsKey(target))
            scales.Add(target, target.localScale);
    }

    private RectTransform CreateAirflowStrip(string name)
    {
        GameObject rootObject = new GameObject(name, typeof(RectTransform));
        rootObject.transform.SetParent(canvas.transform, false);
        RectTransform root = rootObject.GetComponent<RectTransform>();
        root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
        root.sizeDelta = Vector2.zero;

        const int dashCount = 8;
        for (int i = 0; i < dashCount; i++)
        {
            Image dash = CreateImage(root, $"Dash_{i}", Vector2.zero, new Vector2(20f, 5f));
            dash.color = SlipstreamColor;
        }
        root.SetAsLastSibling();
        if (overlayRoot != null)
            overlayRoot.SetAsLastSibling();
        return root;
    }

    private void UpdateAirflowStrip(RectTransform root, Transform follower, Transform leader, float elapsed)
    {
        if (root == null || follower == null || leader == null)
            return;

        Camera worldCamera = Camera.main;
        RectTransform canvasRect = canvas.transform as RectTransform;
        if (worldCamera == null || canvasRect == null)
            return;

        Vector2 followerScreen = worldCamera.WorldToScreenPoint(follower.position);
        Vector2 leaderScreen = worldCamera.WorldToScreenPoint(leader.position);
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, followerScreen, uiCamera, out Vector2 from) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, leaderScreen, uiCamera, out Vector2 to))
            return;

        Vector2 direction = to - from;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        int dashCount = root.childCount;
        for (int i = 0; i < dashCount; i++)
        {
            RectTransform dash = root.GetChild(i) as RectTransform;
            if (dash == null)
                continue;

            float flow = Mathf.Repeat((i + 1f) / (dashCount + 1f) + elapsed * 1.35f, 1f);
            dash.anchoredPosition = Vector2.Lerp(from, to, flow);
            dash.localRotation = Quaternion.Euler(0f, 0f, angle);
            float taper = Mathf.Lerp(0.65f, 1.15f, flow);
            dash.sizeDelta = new Vector2(20f * taper, 5f * taper);
            Image image = dash.GetComponent<Image>();
            if (image != null)
            {
                Color color = SlipstreamColor;
                color.a = Mathf.Sin(flow * Mathf.PI) * 0.92f;
                image.color = color;
            }
        }
    }

    private void OnDisable()
    {
        EndSlowMotion();
        // Safety net for a coroutine stopped before it could enter its
        // finally block. This project has no other gameplay time-scale owner.
        Time.timeScale = GameplayTimeScale;
        effectBusy = false;
    }

    private void OnDestroy()
    {
        EndSlowMotion();
        Time.timeScale = GameplayTimeScale;
    }

    private void BeginSlowMotion(float requestedScale)
    {
        if (timeScaleOverrideActive)
            return;

        timeScaleOverrideActive = true;
        Time.timeScale = Mathf.Clamp(requestedScale, 0.1f, GameplayTimeScale);
    }

    private void EndSlowMotion()
    {
        if (!timeScaleOverrideActive)
            return;

        timeScaleOverrideActive = false;
        Time.timeScale = GameplayTimeScale;
    }

    private IEnumerator PlayBlowupPulse(Transform car, Vector3 originalScale, Quaternion originalRotation)
    {
        SetMessage("BLOWN!", "爆缸 · 本车已退赛", BlowupColor);
        float elapsed = 0f;
        const float duration = 0.62f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = 1f + Mathf.Sin(t * Mathf.PI * 3f) * 0.2f * (1f - t);
            car.localScale = originalScale * pulse;
            car.rotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(t * Mathf.PI * 14f) * 5f * (1f - t));
            canvasGroup.alpha = t < 0.15f ? t / 0.15f : (t > 0.72f ? (1f - t) / 0.28f : 1f);
            yield return null;
        }
        car.localScale = originalScale;
        car.rotation = originalRotation;
    }

    private void SetMessage(string title, string detail, Color accentColor)
    {
        if (!initialized)
            return;

        titleText.text = title;
        detailText.text = detail;
        accentImage.color = accentColor;
        canvasGroup.alpha = 1f;
        if (overlayRoot != null)
            overlayRoot.SetAsLastSibling();
    }

    private void HideMessage()
    {
        if (canvasGroup != null)
            canvasGroup.alpha = 0f;
    }

    private TMP_Text CreateText(Transform parent, string name, int fontSize, Vector2 position, Vector2 size)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        TMP_Text text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.fontStyle = FontStyles.Bold;
        text.color = Color.white;
        text.raycastTarget = false;
        if (font != null)
            text.font = font;
        return text;
    }

    private Image CreateImage(Transform parent, string name, Vector2 position, Vector2 size)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = GetOrAddComponent<Image>(imageObject);
        image.raycastTarget = false;
        return image;
    }

    private static T GetOrAddComponent<T>(GameObject target) where T : Component
    {
        if (target == null)
            return null;

        T component;
        if (target.TryGetComponent<T>(out component) && component != null)
            return component;

        return target.AddComponent<T>();
    }

}

/// <summary>Pure timing and easing rules for race-event presentation.</summary>
public static class RaceEventPresentationRules
{
    /// <summary>Normalizes elapsed time to the requested effect duration.</summary>
    public static float GetSpinProgress(float elapsed, float duration)
    {
        if (duration <= 0f)
            return 1f;

        return Mathf.Clamp01(Mathf.Max(0f, elapsed) / duration);
    }

    /// <summary>
    /// Returns the eased rotation angle. The curve reaches the configured
    /// angle exactly at the end while remaining presentation-only.
    /// </summary>
    public static float GetSpinRotation(float progress, float degrees)
    {
        float t = Mathf.Clamp01(progress);
        float inverse = 1f - t;
        float eased = 1f - inverse * inverse * inverse;
        return Mathf.Max(0f, degrees) * eased;
    }
}
