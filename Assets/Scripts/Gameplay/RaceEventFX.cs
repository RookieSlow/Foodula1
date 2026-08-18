using System.Collections;
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

    private static readonly Color PanelColor = new Color(0.025f, 0.04f, 0.07f, 0.94f);
    private static readonly Color OvertakeColor = new Color(1f, 0.75f, 0.24f, 1f);
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
        float previousTimeScale = Time.timeScale;
        Time.timeScale = Mathf.Min(previousTimeScale, 0.28f);

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
            Time.timeScale = previousTimeScale;
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
            const float duration = 0.92f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float punch = 1f + Mathf.Sin(t * Mathf.PI) * 0.15f;
                car.localScale = originalScale * punch;
                car.rotation = originalRotation * Quaternion.Euler(0f, 0f, 360f * EaseOut(t));
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

    private void OnDisable()
    {
        Time.timeScale = 1f;
        effectBusy = false;
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

    private static float EaseOut(float t)
    {
        float inverse = 1f - t;
        return 1f - inverse * inverse * inverse;
    }
}
