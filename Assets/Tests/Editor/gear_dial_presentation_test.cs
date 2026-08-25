using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GearDialPresentationTests
{
    [Test]
    public void StandardDialUsesArcLayoutAndTracksSelection()
    {
        GameObject container = new GameObject("GearButtons", typeof(RectTransform));
        Button[] buttons = CreateButtons(container.transform);

        GearDialPresentationUI dial = GearDialPresentationUI.Attach(
            container.transform, buttons[0], buttons[1], buttons[2], buttons[3]);
        dial.Configure(false, 3);

        Assert.IsFalse(dial.ChinaMode);
        Assert.AreEqual(3, dial.SelectedGear);
        Assert.Greater(buttons[1].GetComponent<RectTransform>().anchorMin.y,
            buttons[0].GetComponent<RectTransform>().anchorMin.y);
        Assert.NotNull(buttons[0].GetComponentInChildren<GearDialGraphic>());
        Assert.NotNull(buttons[0].transform.Find("DialTick"));

        Object.DestroyImmediate(container);
    }

    [Test]
    public void ChinaDialPlacesRecoverAndGoOnOneRow()
    {
        GameObject container = new GameObject("GearButtons", typeof(RectTransform));
        Button[] buttons = CreateButtons(container.transform);
        GearDialPresentationUI dial = GearDialPresentationUI.Attach(
            container.transform, buttons[0], buttons[1], buttons[2], buttons[3]);

        dial.Configure(true, ChinaGearShiftRules.GoGear);

        Assert.IsTrue(dial.ChinaMode);
        Assert.AreEqual(ChinaGearShiftRules.GoGear, dial.SelectedGear);
        Assert.AreEqual(
            buttons[0].GetComponent<RectTransform>().anchorMin.y,
            buttons[1].GetComponent<RectTransform>().anchorMin.y,
            0.001f);

        Object.DestroyImmediate(container);
    }

    private static Button[] CreateButtons(Transform parent)
    {
        var result = new Button[4];
        for (int i = 0; i < result.Length; i++)
        {
            GameObject buttonObject = new GameObject(
                $"Gear{i + 1}Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            GameObject labelObject = new GameObject(
                "Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(buttonObject.transform, false);
            result[i] = buttonObject.GetComponent<Button>();
        }
        return result;
    }
}
