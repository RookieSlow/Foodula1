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

    [Header("日志")]
    public TMP_Text logText;
    public int maxLogLines = 6;

    [Header("档位按钮")]
    public UnityEngine.UI.Button gear1Button;
    public UnityEngine.UI.Button gear2Button;
    public UnityEngine.UI.Button gear3Button;
    public UnityEngine.UI.Button gear4Button;

    [Header("操作按钮")]
    public UnityEngine.UI.Button resetButton;

    [Header("游戏结束面板")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverText;

    private MVPGameManager gameManager;
    private string logBuffer = "";

    void Start()
    {
        // 绑定档位按钮
        BindGearButton(gear1Button, 1);
        BindGearButton(gear2Button, 2);
        BindGearButton(gear3Button, 3);
        BindGearButton(gear4Button, 4);

        if (resetButton != null)
            resetButton.onClick.AddListener(OnResetClicked);

        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
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

    // ====== 刷新 ======

    /// <summary>
    /// 刷新所有 HUD 显示。
    /// </summary>
    public void Refresh(MVPGameManager gm, PlayerState player, PlayerState ai)
    {
        gameManager = gm;

        if (gearText != null)
            gearText.text = $"Gear: {player.gear}";

        if (heatText != null)
            heatText.text = $"HeatPool: {gm.SharedHeatPool.remaining} | Hand Heat: {player.deck.CountHeatInHand()}";

        if (lapText != null)
            lapText.text = $"Lap: {player.lap}/{gm.Config.totalLaps}";

        if (positionText != null)
            positionText.text = $"Pos: {player.position}/{gm.Track.TotalNodes}";

        if (aiStatusText != null)
        {
            aiStatusText.text = ai.isBlown
                ? "<color=red>AI: BLOWN!</color>"
                : ai.hasFinished
                    ? "<color=green>AI: FINISHED!</color>"
                    : $"AI: G{ai.gear} | Lap {ai.lap} | Pos {ai.position}";
        }
    }

    public void SetStatus(string msg)
    {
        if (statusText != null)
            statusText.text = msg;
    }

    // ====== 日志 ======

    public void AppendLog(string msg)
    {
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

    private void OnResetClicked()
    {
        gameManager?.ResetGame();
        if (gameOverPanel != null)
            gameOverPanel.SetActive(false);
    }
}
