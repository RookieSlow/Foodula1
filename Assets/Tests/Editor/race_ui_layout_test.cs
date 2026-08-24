using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

public class RaceUILayoutTests
{
    [Test]
    public void return_to_menu_button_is_docked_in_operation_action_stack()
    {
        GameObject root = new GameObject(
            "RaceUILayoutTest",
            typeof(RectTransform),
            typeof(HUDUI),
            typeof(RaceUILayoutController));
        try
        {
            HUDUI hud = root.GetComponent<HUDUI>();
            RaceUILayoutController layout = root.GetComponent<RaceUILayoutController>();
            GameObject buttonObject = new GameObject(
                "ReturnToMenuBtn",
                typeof(RectTransform),
                typeof(Image),
                typeof(Button));
            buttonObject.transform.SetParent(root.transform, false);
            hud.returnToMenuButton = buttonObject.GetComponent<Button>();

            layout.ApplyLayout(hud, null);

            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            Assert.That(buttonRect.parent, Is.SameAs(layout.OperationPanel));
            Assert.That(buttonRect.anchorMin, Is.EqualTo(RaceUILayoutController.ReturnToMenuAnchorMin));
            Assert.That(buttonRect.anchorMax, Is.EqualTo(RaceUILayoutController.ReturnToMenuAnchorMax));
            Assert.That(buttonRect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(buttonRect.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(buttonRect.anchorMin.y, Is.GreaterThan(0.39f),
                "Button must stay above the operation prompt/log panel.");
            Assert.That(buttonRect.anchorMax.y, Is.LessThan(0.49f),
                "Button must stay below the reset action.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }
}
