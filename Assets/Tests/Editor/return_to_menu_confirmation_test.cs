using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public class ReturnToMenuConfirmationTests
{
    [Test]
    public void race_return_button_opens_modal_without_loading_scene()
    {
        GameObject root = CreateRoot(out HUDUI hud, out Button returnButton, out _);
        try
        {
            string activeSceneBefore = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;

            InvokeStart(hud);
            Assert.That(hud.returnToMenuConfirmPanel, Is.Not.Null);
            Assert.That(hud.returnToMenuConfirmText, Is.Not.Null);
            Assert.That(hud.returnToMenuConfirmText.text, Does.Contain("退出当前比赛"));
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);

            returnButton.onClick.Invoke();

            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                Is.EqualTo(activeSceneBefore));
            Assert.That(hud.returnToMenuConfirmPanel.GetComponent<Image>().raycastTarget, Is.True,
                "The full-screen modal must block accidental clicks on race controls.");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void cancel_closes_modal_and_second_return_click_can_reopen_it()
    {
        GameObject root = CreateRoot(out HUDUI hud, out Button returnButton, out _);
        try
        {
            InvokeStart(hud);

            returnButton.onClick.Invoke();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);

            hud.cancelReturnToMenuButton.onClick.Invoke();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.False);

            returnButton.onClick.Invoke();
            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void game_over_return_button_uses_the_same_confirmation_gate()
    {
        GameObject root = CreateRoot(out HUDUI hud, out _, out Button backToMenuButton);
        try
        {
            InvokeStart(hud);

            backToMenuButton.onClick.Invoke();

            Assert.That(hud.IsReturnToMenuConfirmationVisible, Is.True);
            Assert.That(hud.confirmReturnToMenuButton, Is.Not.Null);
            Assert.That(hud.cancelReturnToMenuButton, Is.Not.Null);
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    private static GameObject CreateRoot(out HUDUI hud, out Button returnButton, out Button backToMenuButton)
    {
        GameObject root = new GameObject("ReturnToMenuConfirmationTest", typeof(RectTransform));
        root.SetActive(false);
        hud = root.AddComponent<HUDUI>();
        returnButton = CreateButton(root.transform, "ReturnToMenuBtn");
        backToMenuButton = CreateButton(root.transform, "BackToMenuBtn");
        hud.returnToMenuButton = returnButton;
        hud.backToMenuButton = backToMenuButton;
        return root;
    }

    private static Button CreateButton(Transform parent, string name)
    {
        GameObject buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;
        return buttonObject.GetComponent<Button>();
    }

    private static void InvokeStart(HUDUI hud)
    {
        hud.gameObject.SetActive(true);
        hud.GetType().GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(hud, null);
    }
}
