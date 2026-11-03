using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Presentation-only IT tuning control sharing the actual action slot without owning race state.</summary>
public sealed class ItalyPrecisionTuneActionUI : MonoBehaviour
{
    private Button button, source, fallbackSource;
    private TMP_Text label, sourceLabel;
    private UnityAction clicked;
    private RectState sourceRect, sourceLabelRect;
    private Navigation sourceNavigation;
    private float sourceFontSize, sourceFontMin, sourceFontMax;
    private bool sourceAutoSize, sourceWrapping, split;
    private string sourceText;
    private Transform sourceParent;
    private CanvasGroup sourceVisibility;
    private float sourceAlpha;
    private bool sourceInteraction, sourceRaycasts, fullSlot;

    public Button Button => button;

    private readonly struct RectState
    {
        private readonly Vector2 min, max, pivot, offsetMin, offsetMax;
        private readonly Vector3 scale;
        private readonly Quaternion rotation;
        public RectState(RectTransform rect)
        {
            min = rect.anchorMin; max = rect.anchorMax; pivot = rect.pivot;
            offsetMin = rect.offsetMin; offsetMax = rect.offsetMax;
            scale = rect.localScale; rotation = rect.localRotation;
        }
        public void Apply(RectTransform rect)
        {
            rect.anchorMin = min; rect.anchorMax = max; rect.pivot = pivot;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
            rect.localScale = scale; rect.localRotation = rotation;
        }
        public void ApplyHalf(RectTransform rect, bool right)
        {
            Apply(rect);
            float middleAnchor = (min.y + max.y) * 0.5f;
            float middleOffset = (offsetMin.y + offsetMax.y) * 0.5f;
            if (right)
            {
                rect.anchorMax = new Vector2(max.x, middleAnchor);
                rect.offsetMax = new Vector2(offsetMax.x, middleOffset - 2f);
            }
            else
            {
                rect.anchorMin = new Vector2(min.x, middleAnchor);
                rect.offsetMin = new Vector2(offsetMin.x, middleOffset + 2f);
            }
        }
    }

    /// <summary>Attach once beside the existing driver action. The current visible source is selected by HUD.</summary>
    public static ItalyPrecisionTuneActionUI Attach(HUDUI hud, UnityAction onClicked)
    {
        if (hud == null || hud.driverSkillButton == null || hud.driverSkillButton.transform.parent == null) return null;
        Transform parent = hud.driverSkillButton.transform.parent;
        Transform existing = parent.Find("ItalyPrecisionTuneButton");
        var view = existing != null ? existing.GetComponent<ItalyPrecisionTuneActionUI>() : null;
        if (view == null)
        {
            var host = new GameObject("ItalyPrecisionTuneButton", typeof(RectTransform), typeof(Image), typeof(Button));
            host.SetActive(false); host.layer = hud.driverSkillButton.gameObject.layer; host.transform.SetParent(parent, false);
            view = host.AddComponent<ItalyPrecisionTuneActionUI>();
            view.button = host.GetComponent<Button>(); view.button.targetGraphic = host.GetComponent<Image>();
            var textHost = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            textHost.layer = host.layer; textHost.transform.SetParent(host.transform, false);
            view.label = textHost.GetComponent<TMP_Text>(); view.label.raycastTarget = false;
            view.label.alignment = TextAlignmentOptions.Center; view.label.enableWordWrapping = true;
            PadLabel(view.label);
            RacingTechActionButtonStyle.Apply(view.button, false);
            view.button.onClick.AddListener(view.DispatchClick);
        }
        if (onClicked != null) view.clicked = onClicked;
        return view;
    }

    /// <summary>Restore the full slot before another phase-exclusive control copies its geometry.</summary>
    public void RestoreSlot()
    {
        if (!split) return;
        split = false;
        bool wasFullSlot = fullSlot;
        if (fullSlot && sourceVisibility != null)
        {
            sourceVisibility.alpha = sourceAlpha; sourceVisibility.interactable = sourceInteraction;
            sourceVisibility.blocksRaycasts = sourceRaycasts; fullSlot = false;
        }
        // Full-slot presentation changes only the CanvasGroup. An authoritative
        // layout may have docked the source since the snapshot was captured.
        bool restoreShared = !wasFullSlot && source != null && source.transform.parent == sourceParent;
        if (restoreShared)
        {
            sourceRect.Apply(source.transform as RectTransform);
            source.navigation = sourceNavigation;
        }
        if (restoreShared && sourceLabel != null)
        {
            sourceLabelRect.Apply(sourceLabel.rectTransform);
            sourceLabel.enableAutoSizing = sourceAutoSize;
            sourceLabel.fontSize = sourceFontSize; sourceLabel.fontSizeMin = sourceFontMin; sourceLabel.fontSizeMax = sourceFontMax;
            sourceLabel.enableWordWrapping = sourceWrapping;
            sourceLabel.text = sourceText;
        }
        source = null; sourceLabel = null;
    }

    /// <summary>Shows a manager-owned tuning/cancel state beside the current source action.</summary>
    public void Refresh(Button currentSource, bool visible, bool interactable, string text, string companionText, bool shareSlot, Button continuation)
    {
        RestoreSlot();
        if (!visible || currentSource == null)
        {
            Hide(); return;
        }
        source = currentSource;
        sourceParent = currentSource.transform.parent;
        fallbackSource = currentSource;
        RectTransform from = source.transform as RectTransform;
        if (from == null) { Hide(); return; }
        if (transform.parent != from.parent) transform.SetParent(from.parent, false);
        sourceRect = new RectState(from); sourceNavigation = source.navigation;
        sourceLabel = source.GetComponentInChildren<TMP_Text>(true);
        if (sourceLabel != null)
        {
            sourceLabelRect = new RectState(sourceLabel.rectTransform); sourceText = sourceLabel.text;
            sourceAutoSize = sourceLabel.enableAutoSizing; sourceFontSize = sourceLabel.fontSize;
            sourceFontMin = sourceLabel.fontSizeMin; sourceFontMax = sourceLabel.fontSizeMax;
            sourceWrapping = sourceLabel.enableWordWrapping;
            label.font = sourceLabel.font; label.fontSharedMaterial = sourceLabel.fontSharedMaterial;
            label.fontStyle = sourceLabel.fontStyle; label.color = RacingTechTheme.Primary;
            FitLabel(label, sourceFontSize);
            if (shareSlot) { FitLabel(sourceLabel, sourceFontSize); sourceLabel.text = companionText; }
        }
        if (shareSlot)
        {
            sourceRect.ApplyHalf(from, false);
            sourceRect.ApplyHalf(transform as RectTransform, true);
        }
        else
        {
            sourceRect.Apply(transform as RectTransform);
            sourceVisibility = source.GetComponent<CanvasGroup>();
            if (sourceVisibility == null) sourceVisibility = source.gameObject.AddComponent<CanvasGroup>();
            sourceAlpha = sourceVisibility.alpha; sourceInteraction = sourceVisibility.interactable; sourceRaycasts = sourceVisibility.blocksRaycasts;
            sourceVisibility.alpha = 0f; sourceVisibility.interactable = false; sourceVisibility.blocksRaycasts = false; fullSlot = true;
        }
        split = true;
        var leftNavigation = sourceNavigation;
        leftNavigation.mode = Navigation.Mode.Explicit; leftNavigation.selectOnDown = button;
        if (shareSlot) source.navigation = leftNavigation;
        var rightNavigation = leftNavigation;
        rightNavigation.selectOnRight = continuation; rightNavigation.selectOnLeft = continuation;
        rightNavigation.selectOnDown = continuation;
        rightNavigation.mode = Navigation.Mode.Explicit; rightNavigation.selectOnUp = source.IsInteractable() ? source : continuation;
        button.navigation = shareSlot ? rightNavigation : sourceNavigation;
        label.text = text; button.interactable = interactable;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
    }

    /// <summary>Stops input and restores the neighboring action when the owning HUD is disabled.</summary>
    public void SuspendInteraction()
    {
        // HUD.OnDisable can be inside an ancestor SetActive transition; do not re-enter its hierarchy.
        RestoreSlot();
        if (button != null) button.interactable = false;
    }

    private void Hide()
    {
        EventSystem events = EventSystem.current;
        bool wasSelected = events != null && events.currentSelectedGameObject == gameObject;
        button.interactable = false;
        if (wasSelected)
            events.SetSelectedGameObject(fallbackSource != null && fallbackSource.isActiveAndEnabled &&
                fallbackSource.IsInteractable() ? fallbackSource.gameObject : null);
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    private void OnDisable() => RestoreSlot();
    private void OnDestroy() => RestoreSlot();
    private void DispatchClick()
    {
        if (button != null && button.isActiveAndEnabled && button.IsInteractable()) clicked?.Invoke();
    }
    private static void PadLabel(TMP_Text text)
    {
        RectTransform rect = text.rectTransform;
        rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(3f, 0f); rect.offsetMax = new Vector2(-3f, 0f);
    }
    private static void FitLabel(TMP_Text text, float maximum)
    {
        PadLabel(text);
        text.enableWordWrapping = false; text.enableAutoSizing = true;
        text.fontSizeMax = Mathf.Max(12f, maximum); text.fontSizeMin = 12f;
        text.fontSize = text.fontSizeMax;
    }
}
