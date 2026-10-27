using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Temporary modal selection presentation. Retains references, never spends or edits cards.</summary>
public sealed class DriverSkillCardChoiceUI : MonoBehaviour
{
    private readonly List<CardData> cards = new List<CardData>();
    private readonly List<RectSnapshot> originalLayout = new List<RectSnapshot>();
    private readonly List<NavigationSnapshot> originalNavigation = new List<NavigationSnapshot>();
    private Button previousButton, nextButton, cancellationButton, confirmationButton;
    private TMP_Text choiceLabel;
    private Action hidden;
    private int selectedIndex;
    private bool originalConfirmInteractable;
    private bool requiresCard = true;

    public CardData Selected => cards.Count > 0 ? cards[selectedIndex] : null;

    public static DriverSkillCardChoiceUI Attach(Transform card, TMP_FontAsset font)
    {
        Transform existing = card.Find("DriverSkillCardChoiceRow");
        if (existing != null) return existing.GetComponent<DriverSkillCardChoiceUI>();
        var row = new GameObject("DriverSkillCardChoiceRow", typeof(RectTransform));
        row.SetActive(false);
        row.transform.SetParent(card, false);
        SetRect(row.GetComponent<RectTransform>(), new Vector2(0f, -34f), new Vector2(540f, 52f));
        DriverSkillCardChoiceUI view = row.AddComponent<DriverSkillCardChoiceUI>();
        view.previousButton = CreateButton(row.transform, "DriverSkillChoicePreviousButton", "上一张", -230f, font);
        view.nextButton = CreateButton(row.transform, "DriverSkillChoiceNextButton", "下一张", 230f, font);
        view.choiceLabel = CreateLabel(row.transform, "DriverSkillChoiceLabel", font, 20f);
        SetRect(view.choiceLabel.rectTransform, Vector2.zero, new Vector2(360f, 72f));
        return view;
    }

    public void Show(IReadOnlyList<CardData> choices, TMP_Text title, TMP_Text body,
        Button cancel, Button confirm, bool canConfirm, Action onHidden, bool requiresCard = true)
    {
        Clear();
        this.requiresCard = requiresCard;
        previousButton.gameObject.SetActive(requiresCard);
        nextButton.gameObject.SetActive(requiresCard);
        if (choices != null)
            for (int i = 0; i < choices.Count; i++)
                if (choices[i] != null) cards.Add(choices[i]);
        selectedIndex = 0;
        cancellationButton = cancel;
        confirmationButton = confirm;
        originalConfirmInteractable = confirm.interactable;
        SaveNavigation(previousButton);
        SaveNavigation(nextButton);
        SaveNavigation(cancel);
        SaveNavigation(confirm);
        SaveLayout((RectTransform)transform.parent);
        SaveLayout(title != null ? title.rectTransform : null);
        SaveLayout(body != null ? body.rectTransform : null);
        SaveLayout((RectTransform)cancel.transform);
        SaveLayout((RectTransform)confirm.transform);
        SetRect((RectTransform)transform.parent, Vector2.zero, new Vector2(620f, 390f));
        if (title != null) SetRect(title.rectTransform, new Vector2(0f, 145f), new Vector2(520f, 50f));
        // Paid continuations need four full lines; retain the font and the existing modal/focus geometry.
        if (body != null) SetRect(body.rectTransform, new Vector2(0f, 66f), new Vector2(496f, 120f));
        SetRect((RectTransform)cancel.transform, new Vector2(-112f, -130f), new Vector2(190f, 52f));
        SetRect((RectTransform)confirm.transform, new Vector2(112f, -130f), new Vector2(190f, 52f));
        hidden = onHidden;
        previousButton.onClick.AddListener(Previous);
        nextButton.onClick.AddListener(Next);
        confirm.interactable = canConfirm && (!requiresCard || cards.Count > 0);
        RefreshLabel();
        gameObject.SetActive(true);
    }

    public void HideAndClear(bool hideObject = true)
    {
        Clear();
        if (hideObject) gameObject.SetActive(false);
    }

    /// <summary>Call after the modal is active; HUD retains ownership of return focus.</summary>
    public void FocusWithinModal()
    {
        EventSystem events = EventSystem.current;
        if (!isActiveAndEnabled || events == null || !events.isActiveAndEnabled) return;
        Button target = FirstNavigable(cancellationButton, confirmationButton, previousButton, nextButton);
        if (target != null && target.isActiveAndEnabled) events.SetSelectedGameObject(target.gameObject);
    }

    private void Previous()
    {
        if (selectedIndex <= 0) return;
        selectedIndex--;
        RefreshLabel();
    }

    private void Next()
    {
        if (selectedIndex >= cards.Count - 1) return;
        selectedIndex++;
        RefreshLabel();
    }

    /// <summary>Action-specific copy for a no-card confirmation; never changes a cost or selection.</summary>
    public void SetNoCardDescription(string text)
    {
        if (!requiresCard && choiceLabel != null) choiceLabel.text = text;
    }

    private void RefreshLabel()
    {
        // Unity clears EventSystem selection as soon as the selected Button is disabled.
        // Capture it before changing interactable so paging can repair that cleared focus.
        EventSystem events = EventSystem.current;
        GameObject focused = events != null ? events.currentSelectedGameObject : null;
        Button pagingFocus = focused == previousButton.gameObject ? previousButton :
            focused == nextButton.gameObject ? nextButton : null;
        previousButton.interactable = cards.Count > 0 && selectedIndex > 0;
        nextButton.interactable = cards.Count > 0 && selectedIndex < cards.Count - 1;
        choiceLabel.text = !requiresCard ? "第二段无需弃速度牌\n仅支付1张永久热" : cards.Count == 0
            ? "没有可选速度牌\n0 / 0"
            : $"普通速度牌 {cards[selectedIndex].value}\n{selectedIndex + 1} / {cards.Count}";
        RefreshNavigation();
        if (pagingFocus != null && !IsNavigable(pagingFocus)) FocusWithinModal();
    }

    private void RefreshNavigation()
    {
        Button topLeft = FirstNavigable(previousButton, nextButton, cancellationButton, confirmationButton);
        Button topRight = FirstNavigable(nextButton, previousButton, confirmationButton, cancellationButton);
        Button bottomLeft = FirstNavigable(cancellationButton, confirmationButton, previousButton, nextButton);
        Button bottomRight = FirstNavigable(confirmationButton, cancellationButton, nextButton, previousButton);
        SetNavigation(previousButton, topLeft, topRight, topLeft, bottomLeft);
        SetNavigation(nextButton, topLeft, topRight, topRight, bottomRight);
        SetNavigation(cancellationButton, bottomLeft, bottomRight, topLeft, bottomLeft);
        SetNavigation(confirmationButton, bottomLeft, bottomRight, topRight, bottomRight);
        EventSystem events = EventSystem.current;
        GameObject selected = events != null ? events.currentSelectedGameObject : null;
        if (selected == null) return;
        Button selectedButton = selected.GetComponent<Button>();
        if ((selectedButton == previousButton || selectedButton == nextButton ||
             selectedButton == cancellationButton || selectedButton == confirmationButton) &&
            !IsNavigable(selectedButton)) FocusWithinModal();
    }

    private static bool IsNavigable(Button button) =>
        button != null && button.enabled && button.gameObject.activeSelf && button.IsInteractable();

    private static Button FirstNavigable(Button first, Button second, Button third, Button fourth)
    {
        if (IsNavigable(first)) return first;
        if (IsNavigable(second)) return second;
        if (IsNavigable(third)) return third;
        return IsNavigable(fourth) ? fourth : null;
    }

    private static void SetNavigation(Button button, Selectable left, Selectable right, Selectable up, Selectable down)
    {
        if (button == null) return;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.Explicit;
        navigation.selectOnLeft = left;
        navigation.selectOnRight = right;
        navigation.selectOnUp = up;
        navigation.selectOnDown = down;
        button.navigation = navigation;
    }

    private void Clear()
    {
        hidden = null;
        cards.Clear();
        selectedIndex = 0;
        if (previousButton != null) { previousButton.onClick.RemoveAllListeners(); previousButton.interactable = false; }
        if (nextButton != null) { nextButton.onClick.RemoveAllListeners(); nextButton.interactable = false; }
        if (choiceLabel != null) choiceLabel.text = "";
        foreach (RectSnapshot snapshot in originalLayout) snapshot.Restore();
        originalLayout.Clear();
        foreach (NavigationSnapshot snapshot in originalNavigation) snapshot.Restore();
        originalNavigation.Clear();
        if (confirmationButton != null) confirmationButton.interactable = originalConfirmInteractable;
        cancellationButton = null;
        confirmationButton = null;
    }

    private void OnDisable() => NotifyHidden();
    private void OnDestroy() => NotifyHidden();

    private void NotifyHidden()
    {
        Action callback = hidden;
        Clear();
        callback?.Invoke();
    }

    private void SaveLayout(RectTransform rect)
    {
        if (rect != null) originalLayout.Add(new RectSnapshot(rect));
    }

    private void SaveNavigation(Button button)
    {
        if (button != null) originalNavigation.Add(new NavigationSnapshot(button));
    }

    private sealed class NavigationSnapshot
    {
        private readonly Button button;
        private readonly Navigation navigation;
        internal NavigationSnapshot(Button button) { this.button = button; navigation = button.navigation; }
        internal void Restore() { if (button != null) button.navigation = navigation; }
    }

    private sealed class RectSnapshot
    {
        private readonly RectTransform rect;
        private readonly Vector2 anchorMin, anchorMax, pivot, offsetMin, offsetMax;
        internal RectSnapshot(RectTransform rect)
        {
            this.rect = rect;
            anchorMin = rect.anchorMin; anchorMax = rect.anchorMax; pivot = rect.pivot;
            offsetMin = rect.offsetMin; offsetMax = rect.offsetMax;
        }
        internal void Restore()
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.pivot = pivot;
            rect.offsetMin = offsetMin; rect.offsetMax = offsetMax;
        }
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
        rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static Button CreateButton(Transform parent, string name, string text, float x, TMP_FontAsset font)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        host.transform.SetParent(parent, false);
        SetRect((RectTransform)host.transform, new Vector2(x, 0f), new Vector2(80f, 48f));
        Image image = host.GetComponent<Image>();
        image.color = Color.white;
        Button button = host.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = RacingTechTheme.Raised;
        colors.highlightedColor = new Color32(0x30, 0x39, 0x44, 0xFF);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = RacingTechTheme.Graphite;
        colors.disabledColor = RacingTechTheme.Surface;
        colors.fadeDuration = 0f;
        button.colors = colors;
        TMP_Text label = CreateLabel(host.transform, "Label", font, 18f);
        label.text = text;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = label.rectTransform.offsetMax = Vector2.zero;
        return button;
    }

    private static TMP_Text CreateLabel(Transform parent, string name, TMP_FontAsset font, float size)
    {
        var host = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        host.transform.SetParent(parent, false);
        TMP_Text label = host.GetComponent<TMP_Text>();
        label.font = font;
        label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.color = RacingTechTheme.Primary;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        return label;
    }
}
