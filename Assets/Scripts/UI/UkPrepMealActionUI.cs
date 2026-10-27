using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>A technology action in the existing phase-exclusive action slot. No gameplay state.</summary>
public sealed class UkPrepMealActionUI : MonoBehaviour
{
    private Button source, button;
    private TMP_Text label;
    private UnityAction clicked;
    private bool showing, driverWasActive;

    public Button Button => button;

    public static UkPrepMealActionUI Attach(HUDUI hud, UnityAction onClicked)
    {
        if (hud == null || hud.driverSkillButton == null) return null;
        Button source = hud.driverSkillButton;
        if (source.transform.parent == null) return null;
        Transform existing = source.transform.parent.Find("UkPrepMealButton");
        UkPrepMealActionUI view = existing != null ? existing.GetComponent<UkPrepMealActionUI>() : null;
        if (view == null)
        {
            var host = new GameObject("UkPrepMealButton", typeof(RectTransform), typeof(Image), typeof(Button));
            host.SetActive(false);
            host.transform.SetParent(source.transform.parent, false);
            view = host.AddComponent<UkPrepMealActionUI>();
            view.source = source;
            view.button = host.GetComponent<Button>();
            view.button.targetGraphic = host.GetComponent<Image>();
            var labelHost = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelHost.transform.SetParent(host.transform, false);
            view.label = labelHost.GetComponent<TMP_Text>();
            RectTransform labelRect = view.label.rectTransform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 2f);
            labelRect.offsetMax = new Vector2(-6f, -2f);
            view.label.raycastTarget = false;
            view.label.enableWordWrapping = true;
            view.label.alignment = TextAlignmentOptions.Center;
            RacingTechActionButtonStyle.Apply(view.button, false);
            view.button.onClick.AddListener(view.DispatchClick);
        }
        if (onClicked != null) view.clicked = onClicked;
        view.CopySlot();
        return view;
    }

    public void Refresh(bool visible, bool interactable, string text)
    {
        if (source == null || button == null) return;
        if (visible)
        {
            CopySlot();
            if (!showing) driverWasActive = source.gameObject.activeSelf;
            showing = true;
            if (source.gameObject.activeSelf) source.gameObject.SetActive(false);
            label.text = text;
            button.interactable = interactable;
            if (!gameObject.activeSelf) gameObject.SetActive(true);
        }
        else
        {
            button.interactable = false;
            if (showing)
            {
                source.gameObject.SetActive(driverWasActive);
                showing = false;
            }
            EventSystem events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == gameObject)
                events.SetSelectedGameObject(source.isActiveAndEnabled && source.IsInteractable() ? source.gameObject : null);
            if (gameObject.activeSelf) gameObject.SetActive(false);
        }
    }

    public void SuspendInteraction()
    {
        if (button != null) button.interactable = false;
    }

    private void DispatchClick()
    {
        if (button != null && button.isActiveAndEnabled && button.IsInteractable()) clicked?.Invoke();
    }

    private void CopySlot()
    {
        if (source == null) return;
        RectTransform from = source.transform as RectTransform;
        RectTransform to = transform as RectTransform;
        if (from != null && to != null)
        {
            if (to.parent != from.parent) to.SetParent(from.parent, false);
            to.anchorMin = from.anchorMin;
            to.anchorMax = from.anchorMax;
            to.pivot = from.pivot;
            to.offsetMin = from.offsetMin;
            to.offsetMax = from.offsetMax;
            to.localScale = from.localScale;
            to.localRotation = from.localRotation;
        }
        button.navigation = source.navigation;
        TMP_Text original = source.GetComponentInChildren<TMP_Text>(true);
        if (original == null) return;
        label.font = original.font;
        label.fontSharedMaterial = original.fontSharedMaterial;
        label.fontSize = original.fontSize;
        label.enableAutoSizing = original.enableAutoSizing;
        label.fontSizeMin = original.fontSizeMin;
        label.fontSizeMax = original.fontSizeMax;
        label.fontStyle = original.fontStyle;
        label.color = RacingTechTheme.Primary;
    }
}
