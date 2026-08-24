using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class CardSelectionVisualTests
{
    [Test]
    public void SelectedCardScalesAndLiftsWithoutChangingLayoutSlot()
    {
        GameObject hand = CreateHand();
        CardUI first = CreateCard(hand.transform, "FirstCard");
        CardUI second = CreateCard(hand.transform, "SecondCard");
        RectTransform firstRect = (RectTransform)first.transform;
        RectTransform secondRect = (RectTransform)second.transform;

        try
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)hand.transform);
            Vector2 firstPosition = firstRect.anchoredPosition;
            Vector2 secondPosition = secondRect.anchoredPosition;
            Vector2 slotSize = firstRect.sizeDelta;

            first.SetSelectedWithoutNotify(true);

            Assert.That(firstRect.localScale.x, Is.EqualTo(1.08f).Within(0.001f));
            Assert.That(firstRect.anchoredPosition,
                Is.EqualTo(firstPosition + Vector2.up * 24f));
            Assert.That(firstRect.sizeDelta, Is.EqualTo(slotSize));
            Assert.That(secondRect.anchoredPosition, Is.EqualTo(secondPosition));
        }
        finally
        {
            Object.DestroyImmediate(hand);
        }
    }

    [Test]
    public void DeselectRestoresTheOriginalCardTransform()
    {
        GameObject hand = CreateHand();
        CardUI card = CreateCard(hand.transform, "Card");
        RectTransform rect = (RectTransform)card.transform;

        try
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)hand.transform);
            Vector2 originalPosition = rect.anchoredPosition;
            Vector3 originalScale = rect.localScale;

            card.SetSelectedWithoutNotify(true);
            card.SetSelectedWithoutNotify(false);

            Assert.That(rect.anchoredPosition, Is.EqualTo(originalPosition));
            Assert.That(rect.localScale, Is.EqualTo(originalScale));
            Assert.That(card.isSelected, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(hand);
        }
    }

    [Test]
    public void SelectedCardUsesShadowWithoutChangingItsLayoutSize()
    {
        GameObject hand = CreateHand();
        CardUI card = CreateCard(hand.transform, "ShadowCard");
        RectTransform rect = (RectTransform)card.transform;

        try
        {
            Vector2 size = rect.sizeDelta;
            if (!card.TryGetComponent(out Shadow shadow))
                throw new AssertionException("Selected card did not receive a Shadow component.");

            Assert.That(shadow.effectColor.a, Is.EqualTo(0f));

            card.SetSelectedWithoutNotify(true);

            Assert.That(shadow.effectColor.a, Is.GreaterThan(0f));
            Assert.That(shadow.effectDistance, Is.EqualTo(new Vector2(3f, -3f)));
            Assert.That(rect.sizeDelta, Is.EqualTo(size));
        }
        finally
        {
            Object.DestroyImmediate(hand);
        }
    }

    private static GameObject CreateHand()
    {
        GameObject hand = new GameObject("CardSelectionTestHand", typeof(RectTransform));
        RectTransform rect = (RectTransform)hand.transform;
        rect.sizeDelta = new Vector2(500f, 220f);

        HorizontalLayoutGroup layout = hand.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 12f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        return hand;
    }

    private static CardUI CreateCard(Transform parent, string name)
    {
        GameObject cardObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(CardUI));
        cardObject.transform.SetParent(parent, false);

        if (!cardObject.TryGetComponent(out RectTransform rect)
            || !cardObject.TryGetComponent(out Image background)
            || !cardObject.TryGetComponent(out CardUI card))
        {
            throw new AssertionException("Card test object did not receive its UI components.");
        }
        rect.sizeDelta = new Vector2(112f, 180f);
        LayoutElement layout = cardObject.AddComponent<LayoutElement>();
        layout.preferredWidth = rect.sizeDelta.x;
        layout.preferredHeight = rect.sizeDelta.y;

        card.backgroundImage = background;
        card.selectionDuration = 0f;
        card.SetupCard(new CardData(CardType.Speed, 2), null);
        return card;
    }
}
