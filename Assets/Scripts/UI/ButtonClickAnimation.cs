using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Small, unscaled press feedback shared by every uGUI button.
/// The animation is intentionally short so it never delays a scene change or
/// a race action, while still making the click target feel responsive.
/// </summary>
[RequireComponent(typeof(Button))]
public sealed class ButtonClickAnimation : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [SerializeField, Range(0.85f, 0.99f)] private float pressedScale = 0.94f;
    [SerializeField, Min(0.01f)] private float pressDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float releaseDuration = 0.14f;

    private Vector3 baseScale;
    private Coroutine scaleRoutine;
    private Button button;

    private void Awake()
    {
        baseScale = transform.localScale;
        button = GetComponent<Button>();
        if (button != null)
            button.onClick.AddListener(PlayConfirmSound);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(PlayConfirmSound);
    }

    private static void PlayConfirmSound()
    {
        AudioService.PlayUi(AudioEventNames.UiConfirm);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        AnimateTo(baseScale * pressedScale, pressDuration);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        AnimateTo(baseScale, releaseDuration);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(baseScale, releaseDuration);
    }

    private void OnDisable()
    {
        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);
        scaleRoutine = null;
        transform.localScale = baseScale;
    }

    private void AnimateTo(Vector3 target, float duration)
    {
        if (!isActiveAndEnabled) return;
        if (scaleRoutine != null)
            StopCoroutine(scaleRoutine);
        float scaledDuration = GameSettingsRuntime.ScaleAnimationDuration(duration);
        if (scaledDuration <= 0f)
        {
            transform.localScale = target;
            scaleRoutine = null;
            return;
        }
        scaleRoutine = StartCoroutine(ScaleRoutine(target, scaledDuration));
    }

    private IEnumerator ScaleRoutine(Vector3 target, float duration)
    {
        Vector3 start = transform.localScale;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = t * t * (3f - 2f * t);
            transform.localScale = Vector3.LerpUnclamped(start, target, t);
            yield return null;
        }

        transform.localScale = target;
        scaleRoutine = null;
    }

    /// <summary>Attach the feedback component once to a button.</summary>
    public static void Attach(Button button)
    {
        if (button == null) return;
        if (button.GetComponent<ButtonClickAnimation>() == null)
            button.gameObject.AddComponent<ButtonClickAnimation>();
    }
}
