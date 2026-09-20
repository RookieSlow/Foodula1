using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RaceResultPresentationTests
{
    [Test]
    public void podium_places_use_color_and_text_labels()
    {
        string raw = "=== 比赛结果 ===\n\n1. [UK] A - [完赛]\n2. [CN] B - [完赛]\n3. [JP] C - [完赛]\n4. [US] D - [完赛]\n\n奖励说明";

        string formatted = RaceResultPresentationRules.FormatForRichText(raw);

        Assert.That(formatted, Does.Contain("#FFD75A><b>【冠军】 1."));
        Assert.That(formatted, Does.Contain("#DDE7F2><b>【亚军】 2."));
        Assert.That(formatted, Does.Contain("#E7A56A><b>【季军】 3."));
        Assert.That(formatted, Does.Contain("#C2CDDD>4."));
        Assert.That(formatted, Does.Contain("奖励说明"));
        Assert.That(formatted, Does.Not.Contain("=== 比赛结果 ==="));
    }

    [Test]
    public void game_over_panel_builds_masked_scroll_list_for_large_field()
    {
        GameObject canvasObject = new GameObject("ResultCanvas", typeof(RectTransform), typeof(Canvas));
        GameObject hudObject = new GameObject("HUD", typeof(RectTransform), typeof(HUDUI));
        GameObject panelObject = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
        GameObject textObject = new GameObject("GameOverText", typeof(RectTransform), typeof(TextMeshProUGUI));
        try
        {
            hudObject.transform.SetParent(canvasObject.transform, false);
            panelObject.transform.SetParent(hudObject.transform, false);
            textObject.transform.SetParent(panelObject.transform, false);
            panelObject.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 600f);

            HUDUI hud = hudObject.GetComponent<HUDUI>();
            hud.gameOverPanel = panelObject;
            hud.gameOverText = textObject.GetComponent<TMP_Text>();
            string result = "=== 比赛结果 ===\n\n";
            for (int i = 1; i <= 12; i++)
                result += $"{i}. [UK] 车手{i} - 3圈 位60 [完赛]\n";
            result += "\nRP 与车手经验结算说明";

            hud.ShowGameOver(result);

            Assert.That(hud.gameOverScrollRect, Is.Not.Null);
            Assert.That(hud.gameOverScrollRect.vertical, Is.True);
            Assert.That(hud.gameOverScrollRect.horizontal, Is.False);
            Assert.That(hud.gameOverScrollRect.verticalScrollbar, Is.Not.Null);
            Assert.That(hud.gameOverScrollRect.viewport.GetComponent<RectMask2D>(), Is.Not.Null);
            Assert.That(hud.gameOverScrollRect.content, Is.SameAs(hud.gameOverText.rectTransform));
            Assert.That(hud.gameOverScrollRect.content.rect.height,
                Is.GreaterThan(hud.gameOverScrollRect.viewport.rect.height),
                "A 12-driver result must remain scrollable instead of expanding beyond the panel.");
            Assert.That(hud.gameOverText.text, Does.Contain("【冠军】"));
            Assert.That(hud.gameOverText.text, Does.Contain("12. [UK]"));
            Assert.That(hud.gameOverPanel.activeSelf, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(canvasObject);
        }
    }
}
