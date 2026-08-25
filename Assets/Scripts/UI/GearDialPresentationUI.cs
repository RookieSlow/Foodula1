using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Styles and arranges the existing functional gear buttons as a compact stove
/// dial. It does not own input; MVPGameManager remains the sole rule handler.
/// </summary>
public sealed class GearDialPresentationUI : MonoBehaviour
{
    private static readonly Color SelectedColor = new Color(0.345f, 0.651f, 1f, 1f);
    private static readonly Color IdleColor = new Color(0.12f, 0.18f, 0.27f, 0.96f);

    [SerializeField] private Button[] buttons = new Button[4];
    [SerializeField] private int selectedGear;
    [SerializeField] private bool chinaMode;

    public int SelectedGear => selectedGear;
    public bool ChinaMode => chinaMode;

    public static GearDialPresentationUI Attach(
        Transform container,
        Button gear1,
        Button gear2,
        Button gear3,
        Button gear4)
    {
        if (container == null)
            return null;

        GearDialPresentationUI presentation = container.GetComponent<GearDialPresentationUI>();
        if (presentation == null)
            presentation = container.gameObject.AddComponent<GearDialPresentationUI>();
        presentation.Initialize(gear1, gear2, gear3, gear4);
        return presentation;
    }

    public void Initialize(Button gear1, Button gear2, Button gear3, Button gear4)
    {
        buttons = new[] { gear1, gear2, gear3, gear4 };
        for (int i = 0; i < buttons.Length; i++)
            StyleButton(buttons[i]);
        Configure(chinaMode, selectedGear);
    }

    public void Configure(bool isChina, int currentGear)
    {
        chinaMode = isChina;
        selectedGear = currentGear;

        if (isChina)
        {
            SetAnchors(buttons[0], new Vector2(0.12f, 0.12f), new Vector2(0.48f, 0.88f));
            SetAnchors(buttons[1], new Vector2(0.52f, 0.12f), new Vector2(0.88f, 0.88f));
        }
        else
        {
            SetAnchors(buttons[0], new Vector2(0.00f, 0.02f), new Vector2(0.25f, 0.48f));
            SetAnchors(buttons[1], new Vector2(0.24f, 0.48f), new Vector2(0.50f, 0.98f));
            SetAnchors(buttons[2], new Vector2(0.50f, 0.48f), new Vector2(0.76f, 0.98f));
            SetAnchors(buttons[3], new Vector2(0.75f, 0.02f), new Vector2(1.00f, 0.48f));
        }

        SetSelectedGear(currentGear);
    }

    public void SetSelectedGear(int gear)
    {
        selectedGear = gear;
        for (int i = 0; i < buttons.Length; i++)
        {
            Button button = buttons[i];
            if (button == null)
                continue;
            GearDialGraphic dial = button.GetComponentInChildren<GearDialGraphic>(true);
            if (dial != null)
                dial.color = i + 1 == gear ? SelectedColor : IdleColor;
        }
    }

    private static void StyleButton(Button button)
    {
        if (button == null)
            return;

        Image image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = Color.clear;
            image.raycastTarget = false;
        }

        Transform existingFace = button.transform.Find("DialFace");
        GameObject faceObject = existingFace != null
            ? existingFace.gameObject
            : new GameObject("DialFace", typeof(RectTransform), typeof(CanvasRenderer), typeof(GearDialGraphic));
        faceObject.transform.SetParent(button.transform, false);
        RectTransform faceRect = faceObject.GetComponent<RectTransform>();
        faceRect.anchorMin = Vector2.zero;
        faceRect.anchorMax = Vector2.one;
        faceRect.offsetMin = Vector2.zero;
        faceRect.offsetMax = Vector2.zero;
        faceObject.transform.SetAsFirstSibling();

        GearDialGraphic dial = faceObject.GetComponent<GearDialGraphic>();
        dial.color = IdleColor;
        dial.raycastTarget = true;
        button.targetGraphic = dial;

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = Color.white;
            label.fontSize = Mathf.Min(label.fontSize, 14f);
            label.fontStyle = FontStyles.Bold;
        }

        Transform existingTick = button.transform.Find("DialTick");
        GameObject tickObject = existingTick != null
            ? existingTick.gameObject
            : new GameObject("DialTick", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        tickObject.transform.SetParent(button.transform, false);
        RectTransform tick = tickObject.GetComponent<RectTransform>();
        tick.anchorMin = new Vector2(0.48f, 0.73f);
        tick.anchorMax = new Vector2(0.52f, 0.94f);
        tick.offsetMin = Vector2.zero;
        tick.offsetMax = Vector2.zero;
        Image tickImage = tickObject.GetComponent<Image>();
        tickImage.color = new Color(0.86f, 0.94f, 1f, 0.95f);
        tickImage.raycastTarget = false;
    }

    private static void SetAnchors(Button button, Vector2 min, Vector2 max)
    {
        if (button == null)
            return;
        RectTransform rect = button.GetComponent<RectTransform>();
        if (rect == null)
            return;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
    }
}
