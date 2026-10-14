using NUnit.Framework;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class RaceResultPresentationTests
{
    [TestCase("normal")]
    [TestCase("tutorial")]
    [TestCase("career")]
    public void real_ranking_formats_only_standings_and_preserves_mode_copy(string mode)
    {
        var players = new List<PlayerState>();
        for (int i = 12; i >= 1; i--)
            players.Add(new PlayerState("ResultDriver" + i, i != 1, i, 1)
            { teamId = TeamId.UK, lap = 3, hasFinished = i != 12, isBlown = i == 12, finishOrder = i });
        string ranking = RaceRanking.FormatResults(players);
        string suffix = "\n\n1. 奖励说明\n2. 存档说明";
        string raw = mode == "tutorial" ? TutorialRaceResultPresentation.BuildSummary(ranking, false) + suffix :
            mode == "career" ? ranking + CareerRaceResultPresentation.BuildRejectedSummary("test rejection") + suffix :
            ranking + suffix;

        string formatted = RaceResultPresentationRules.FormatForRichText(raw);

        Assert.That(formatted, Does.Not.Contain("=== 比赛结果 ==="));
        string[] rankedLines = ranking.Split('\n');
        for (int rank = 1; rank <= 12; rank++)
        {
            string line = rankedLines[rank + 1];
            string expected = rank == 1 ? "<color=#FFD75A><b>【冠军】 " + line + "</b></color>" :
                rank == 2 ? "<color=#DDE7F2><b>【亚军】 " + line + "</b></color>" :
                rank == 3 ? "<color=#E7A56A><b>【季军】 " + line + "</b></color>" :
                "<color=#C2CDDD>" + line + "</color>";
            Assert.That(formatted, Does.Contain(expected));
        }
        Assert.That(formatted, Does.EndWith(suffix));
        Assert.That(formatted, Does.Not.Contain("【冠军】 1. 奖励说明"));
        if (mode == "tutorial") Assert.That(formatted, Does.Contain("本次练习未完成"));
        if (mode == "career") Assert.That(formatted, Does.Contain("test rejection"));
        Assert.That(players[0].name, Is.EqualTo("ResultDriver12"), "Formatting must not sort the source roster in place.");
        Assert.That(players[0].isBlown, Is.True);
        Assert.That(players[11].finishOrder, Is.EqualTo(1));
    }

    [TestCase(2)]
    [TestCase(4)]
    public void reused_result_panel_replaces_long_short_empty_content_without_duplicate_controls(int shortCount)
    {
        var root = new GameObject("ReusedResultCanvas", typeof(RectTransform), typeof(Canvas));
        try
        {
            var hudObject = new GameObject("HUD", typeof(RectTransform), typeof(HUDUI));
            hudObject.transform.SetParent(root.transform, false);
            var panel = new GameObject("GameOverPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(hudObject.transform, false);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 600f);
            var textObject = new GameObject("GameOverText", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(panel.transform, false);
            var hud = hudObject.GetComponent<HUDUI>();
            hud.gameOverPanel = panel;
            hud.gameOverText = textObject.GetComponent<TMP_Text>();
            string large = "=== 比赛结果 ===\n\n";
            for (int i = 1; i <= 12; i++) large += i + ". old-driver-" + i + " [完赛]\n";
            large += "\nRP 与 XP 奖励说明\n";
            for (int i = 1; i <= 12; i++) large += "old-driver-" + i + ": +100 RP, +10 XP\n";
            hud.ShowGameOver(large);
            ScrollRect scroll = hud.gameOverScrollRect;
            RectTransform viewport = scroll.viewport;
            Scrollbar scrollbar = scroll.verticalScrollbar;
            TMP_Text title = hud.gameOverTitle;
            int objectCount = panel.GetComponentsInChildren<Transform>(true).Length;
            float largeHeight = scroll.content.rect.height;
            Assert.That(largeHeight, Is.GreaterThan(viewport.rect.height),
                "The fixture must actually overflow before testing scroll reset and shrinking.");
            string small = "=== 比赛结果 ===\n\n";
            for (int i = 1; i <= shortCount; i++) small += i + ". new-driver-" + i + " [完赛]\n";
            foreach (string raw in new[] { small, string.Empty, large })
            {
                scroll.verticalNormalizedPosition = 0f;
                panel.SetActive(false);
                hud.ShowGameOver(raw);
                Assert.That(hud.gameOverText.text, Is.EqualTo(RaceResultPresentationRules.FormatForRichText(raw)));
                Assert.That(hud.gameOverScrollRect, Is.SameAs(scroll));
                Assert.That(scroll.viewport, Is.SameAs(viewport));
                Assert.That(scroll.verticalScrollbar, Is.SameAs(scrollbar));
                Assert.That(hud.gameOverTitle, Is.SameAs(title));
                Assert.That(scroll.content, Is.SameAs(hud.gameOverText.rectTransform));
                Assert.That(panel.GetComponentsInChildren<Transform>(true).Length, Is.EqualTo(objectCount));
                Assert.That(scroll.content.rect.height, Is.GreaterThanOrEqualTo(viewport.rect.height));
                // A fitting list has no scroll range; its normalized value can be zero.
                // Only the overflowing twelve-driver list has a meaningful top ratio.
                if (scroll.content.rect.height > viewport.rect.height + 0.01f)
                    Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.001f));
                Assert.That(panel.activeSelf, Is.True);
                if (raw != large)
                {
                    Assert.That(hud.gameOverText.text, Does.Not.Contain("old-driver"));
                    Assert.That(scroll.content.rect.height, Is.LessThan(largeHeight));
                }
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

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
