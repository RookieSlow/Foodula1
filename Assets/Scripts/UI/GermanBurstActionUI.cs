using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Presentation-only DE declaration control sharing the current action slot without owning race state.</summary>
public sealed class GermanBurstActionUI : MonoBehaviour
{
    private Button button, source, fallbackSource;
    private TMP_Text label, sourceLabel;
    private UnityAction clicked;
    private RectState sourceRect, sourceLabelRect;
    private Navigation sourceNavigation;
    private float sourceFontSize, sourceFontMin, sourceFontMax;
    private bool sourceAutoSize, sourceWrapping, split;

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
            float middleAnchor = (min.x + max.x) * 0.5f;
            float middleOffset = (offsetMin.x + offsetMax.x) * 0.5f;
            if (right)
            {
                rect.anchorMin = new Vector2(middleAnchor, min.y);
                rect.offsetMin = new Vector2(middleOffset + 4f, offsetMin.y);
            }
            else
            {
                rect.anchorMax = new Vector2(middleAnchor, max.y);
                rect.offsetMax = new Vector2(middleOffset - 4f, offsetMax.y);
            }
        }
    }

    /// <summary>Attach once beside the existing driver action. The current visible source is selected by HUD.</summary>
    public static GermanBurstActionUI Attach(HUDUI hud, UnityAction onClicked)
    {
        if (hud == null || hud.driverSkillButton == null || hud.driverSkillButton.transform.parent == null) return null;
        Transform parent = hud.driverSkillButton.transform.parent;
        Transform existing = parent.Find("GermanBurstButton");
        var view = existing != null ? existing.GetComponent<GermanBurstActionUI>() : null;
        if (view == null)
        {
            var host = new GameObject("GermanBurstButton", typeof(RectTransform), typeof(Image), typeof(Button));
            host.SetActive(false); host.layer = hud.driverSkillButton.gameObject.layer; host.transform.SetParent(parent, false);
            view = host.AddComponent<GermanBurstActionUI>();
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
        if (source != null)
        {
            sourceRect.Apply(source.transform as RectTransform);
            source.navigation = sourceNavigation;
        }
        if (sourceLabel != null)
        {
            sourceLabelRect.Apply(sourceLabel.rectTransform);
            sourceLabel.enableAutoSizing = sourceAutoSize;
            sourceLabel.fontSize = sourceFontSize; sourceLabel.fontSizeMin = sourceFontMin; sourceLabel.fontSizeMax = sourceFontMax;
            sourceLabel.enableWordWrapping = sourceWrapping;
        }
        source = null; sourceLabel = null;
    }

    /// <summary>Shows a manager-owned declaration/cancel state beside the current source action.</summary>
    public void Refresh(Button currentSource, bool visible, bool interactable, string text)
    {
        RestoreSlot();
        if (!visible || currentSource == null)
        {
            Hide(); return;
        }
        source = currentSource;
        fallbackSource = currentSource;
        RectTransform from = source.transform as RectTransform;
        if (from == null) { Hide(); return; }
        if (transform.parent != from.parent) transform.SetParent(from.parent, false);
        sourceRect = new RectState(from); sourceNavigation = source.navigation;
        sourceLabel = source.GetComponentInChildren<TMP_Text>(true);
        if (sourceLabel != null)
        {
            sourceLabelRect = new RectState(sourceLabel.rectTransform);
            sourceAutoSize = sourceLabel.enableAutoSizing; sourceFontSize = sourceLabel.fontSize;
            sourceFontMin = sourceLabel.fontSizeMin; sourceFontMax = sourceLabel.fontSizeMax;
            sourceWrapping = sourceLabel.enableWordWrapping;
            label.font = sourceLabel.font; label.fontSharedMaterial = sourceLabel.fontSharedMaterial;
            label.fontStyle = sourceLabel.fontStyle; label.color = RacingTechTheme.Primary;
            FitLabel(sourceLabel, sourceFontSize); FitLabel(label, sourceFontSize);
        }
        sourceRect.ApplyHalf(from, false);
        sourceRect.ApplyHalf(transform as RectTransform, true);
        split = true;
        var leftNavigation = sourceNavigation;
        if (sourceNavigation.mode == Navigation.Mode.Automatic)
        {
            leftNavigation.selectOnUp = source.FindSelectableOnUp();
            leftNavigation.selectOnDown = source.FindSelectableOnDown();
            leftNavigation.selectOnLeft = source.FindSelectableOnLeft();
        }
        leftNavigation.mode = Navigation.Mode.Explicit; leftNavigation.selectOnRight = button;
        source.navigation = leftNavigation;
        var rightNavigation = leftNavigation;
        rightNavigation.selectOnRight = sourceNavigation.selectOnRight;
        rightNavigation.mode = Navigation.Mode.Explicit; rightNavigation.selectOnLeft = source;
        button.navigation = rightNavigation;
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
        rect.offsetMin = new Vector2(6f, 2f); rect.offsetMax = new Vector2(-6f, -2f);
    }
    private static void FitLabel(TMP_Text text, float maximum)
    {
        PadLabel(text);
        text.enableWordWrapping = true; text.enableAutoSizing = true;
        text.fontSizeMax = Mathf.Max(12f, maximum); text.fontSizeMin = 12f;
        text.fontSize = text.fontSizeMax;
    }
}
