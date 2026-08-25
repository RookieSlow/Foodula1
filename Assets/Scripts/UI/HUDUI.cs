using System;
using UnityEngine;
using TMPro;

/// <summary>
/// HUD UI 管理器 — 显示比赛状态（档位、热量池、圈数、位置），
/// 提供档位按钮、出牌按钮、重置按钮，以及状态文本和日志输出。
/// </summary>
public class HUDUI : MonoBehaviour
{
    [Header("状态文本")]
    public TMP_Text statusText;
    public TMP_Text gearText;
    public TMP_Text heatText;
    public TMP_Text lapText;
    public TMP_Text positionText;
    public TMP_Text aiStatusText;

    [Header("5 系统接入显示 (可选，未赋值则跳过)")]
    public TMP_Text weatherText;
    public TMP_Text standingsText;

    [Header("日志")]
    public TMP_Text logText;
    public int maxLogLines = 6;

    [Header("档位按钮")]
    public UnityEngine.UI.Button gear1Button;
    public UnityEngine.UI.Button gear2Button;
    public UnityEngine.UI.Button gear3Button;
    public UnityEngine.UI.Button gear4Button;

    [Header("操作按钮")]
    public UnityEngine.UI.Button confirmGearButton;
    public UnityEngine.UI.Button resetButton;
    public UnityEngine.UI.Button returnToMenuButton;
    public UnityEngine.UI.Button backToMenuButton;

    [Header("游戏结束面板")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;

    private MVPGameManager gameManager;
    private string logBuffer = "";
    private Action<string> logSink;
    [SerializeField] private HeatThermometerUI heatThermometer;
    [SerializeField] private GearDialPresentationUI gearDialPresentation;

    public HeatThermometerUI HeatThermometer => heatThermometer;
    public GearDialPresentationUI GearDialPresentation => gearDialPresentation;

    void Start()
    {
        // 绑定档位按钮
        BindGearButton(gear1Button, 1);
        BindGearButton(gear2Button, 2);
        BindGearButton(gear3Button, 3);
        BindGearButton(gear4Button, 4);

        if (confirmGearButton != null)
            confirmGearButton.onClick.AddListener(OnConfirmGearClicked);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        if (returnToMenuButton != null)
            returnToMenuButton.onClick.AddListener(OnBackToMenuClicked);

        if (backToMenuButton != null)
            backToMenuButton.onClick.AddListener(OnBackToMenuClicked);

        ButtonClickAnimation.Attach(gear1Button);
        ButtonClickAnimation.Attach(gear2Button);
        ButtonClickAnimation.Attach(gear3Button);
        ButtonClickAnimation.Attach(gear4Button);
        ButtonClickAnimation.Attach(confirmGearButton);
        ButtonClickAnimation.Attach(resetButton);
        ButtonClickAnimation.Attach(returnToMenuButton);
        ButtonClickAnimation.Attach(backToMenuButton);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);

        EnsurePresentation();
    }

    private void BindGearButton(UnityEngine.UI.Button btn, int gear)
    {
        if (btn != null)
        {
            btn.onClick.AddListener(() => OnGearClicked(gear));
        }
    }

    /// <summary>在游戏初始化后设置引用。</summary>
    public void SetGameManager(MVPGameManager gm)
    {
        gameManager = gm;
    }

    /// <summary>Sets an optional sink for full-fidelity manual playtest logs.</summary>
    public void SetLogSink(Action<string> sink)
    {
        logSink = sink;
    }

    // ====== 刷新 ======

    /// <summary>
    /// 刷新所有 HUD 显示（2 人兼容重载）。
    /// </summary>
    public void Refresh(MVPGameManager gm, PlayerState player, PlayerState ai)
    {
        Refresh(gm, player, ai, null);
    }

    /// <summary>
    /// 刷新所有 HUD 显示 — 多车模式传入全部玩家以显示排名。
    /// </summary>
    public void Refresh(MVPGameManager gm, PlayerState player, PlayerState ai, System.Collections.Generic.IReadOnlyList<PlayerState> allPlayers)
    {
        gameManager = gm;

        RefreshPlayerResources(player);

        if (lapText != null)
            lapText.text = $"圈数: {player.lap}/{gm.Config.totalLaps}";

        if (positionText != null)
        {
            if (allPlayers != null && allPlayers.Count > 0)
            {
                int rank = RaceRanking.GetCurrentRank(player, new System.Collections.Generic.List<PlayerState>(allPlayers));
                positionText.text = $"{TrackPresentationRules.FormatCellPosition(player.position, gm.Track.TotalNodes)} | 排名: {rank}/{allPlayers.Count}";
            }
            else
            {
                positionText.text = TrackPresentationRules.FormatCellPosition(player.position, gm.Track.TotalNodes);
            }
        }

        if (aiStatusText != null && ai != null)
        {
            aiStatusText.text = ai.isBlown
                ? "<color=red>AI: 爆缸!</color>"
                : ai.hasFinished
                    ? "<color=green>AI: 完赛!</color>"
                    : $"AI: {TeamGearRules.GetDisplayName(ai.teamId, ai.gear)} | 引擎:{ai.deck.heatPool.remaining} | 圈{ai.lap} | 格{TrackPresentationRules.WrapNodeIndex(ai.position, gm.Track.TotalNodes) + 1}";
        }

        // 天气显示
        if (weatherText != null)
            weatherText.text = gm.WeatherLabel;

        // 多车排行榜
        if (standingsText != null && allPlayers != null && allPlayers.Count > 1)
            standingsText.text = FormatStandings(allPlayers, player);
    }

    /// <summary>Immediately refreshes the local player's gear and heat resources.</summary>
    public void RefreshPlayerResources(PlayerState player)
    {
        if (player == null || player.deck == null) return;

        if (gearText != null)
            gearText.text = $"档位: {TeamGearRules.GetDisplayName(player.teamId, player.gear)}";

        if (heatText != null)
        {
            EnsurePresentation();
            int handHeat = player.deck.CountHeatInHand();
            int drawHeat = player.deck.CountHeatInDrawPile();
            int discardHeat = player.deck.CountHeatInDiscardPile();
            HeatGaugeState gauge = HeatGaugeRules.Evaluate(player.deck);
            string heatColor = gauge.WarningLevel == HeatWarningLevel.Critical
                ? "#F74840"
                : gauge.WarningLevel == HeatWarningLevel.Elevated ? "#F79A3D" : "#58A6FF";
            string tempInfo = gauge.TemporaryHeat > 0 ? $" 临{gauge.TemporaryHeat}" : "";
            string spinInfo = player.spinCounter > 0 ? $" | 失控 {player.spinCounter}/3" : "";
            heatText.text = $"引擎 {gauge.EngineRemaining}/{gauge.Capacity}  <color={heatColor}>热量 {gauge.TotalHeat}</color>\n"
                + $"手{handHeat} 抽{drawHeat} 弃{discardHeat}{tempInfo}{spinInfo}";
            heatThermometer?.Refresh(player.deck);
        }
    }

    /// <summary>Builds the authored visual wrappers without changing button ownership.</summary>
    public void EnsurePresentation()
    {
        if (heatText != null && heatThermometer == null)
            heatThermometer = HeatThermometerUI.Attach(heatText);

        Transform gearContainer = gear1Button != null ? gear1Button.transform.parent : null;
        if (gearContainer != null && gearDialPresentation == null)
        {
            gearDialPresentation = GearDialPresentationUI.Attach(
                gearContainer, gear1Button, gear2Button, gear3Button, gear4Button);
        }
    }

    public void ConfigureGearPresentation(bool chinaMode, int currentGear)
    {
        EnsurePresentation();
        gearDialPresentation?.Configure(chinaMode, currentGear);
    }

    public void SelectGearPresentation(int gear)
    {
        EnsurePresentation();
        gearDialPresentation?.SetSelectedGear(gear);
    }

    /// <summary>生成多车排行榜文本（含自己的标记）。</summary>
    private string FormatStandings(System.Collections.Generic.IReadOnlyList<PlayerState> all, PlayerState self)
    {
        var rankings = RaceRanking.GetRankings(new System.Collections.Generic.List<PlayerState>(all));
        var sb = new System.Text.StringBuilder();
        foreach (var e in rankings)
        {
            string mark = e.player == self ? " ←你" : "";
            int totalNodes = gameManager != null && gameManager.Track != null
                ? gameManager.Track.TotalNodes
                : 0;
            string cell = totalNodes > 0
                ? (TrackPresentationRules.WrapNodeIndex(e.player.position, totalNodes) + 1).ToString()
                : "?";
            sb.AppendLine($"{e.rank}. {e.player.name} 圈{e.player.lap} 格{cell}{mark}");
        }
        return sb.ToString();
    }

    public void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }

    // ====== 日志 ======

    public void AppendLog(string msg)
    {
        logSink?.Invoke(msg);
        logBuffer = msg + "\n" + logBuffer;

        // 限制行数
        string[] lines = logBuffer.Split('\n');
        if (lines.Length > maxLogLines)
        {
            logBuffer = string.Join("\n", lines, 0, maxLogLines);
        }

        if (logText != null)
            logText.text = logBuffer;
    }

    // ====== 游戏结束 ======

    public void ShowGameOver(string result)
    {
        if (gameOverPanel != null)
            gameOverPanel.SetActive(true);

        if (gameOverText != null)
            gameOverText.text = result;
    }

    // ====== 按钮回调 ======

    private void OnGearClicked(int gear)
    {
        gameManager?.OnGearButtonClicked(gear);
    }

    private void OnConfirmGearClicked()
    {
        gameManager?.OnConfirmGearClicked();
    }

    private void OnResetClicked()
    {
        gameManager?.ResetGame();
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }

    private void OnBackToMenuClicked()
    {
        SceneLoader.LoadMainMenu();
    }
}
