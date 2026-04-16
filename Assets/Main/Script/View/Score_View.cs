using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class Score_View : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI scoreText;

    [SerializeField]
    private TextMeshProUGUI historyText;

    private List<string> scoreHistory = new List<string>();

    private float score = 0;

    // スコアのプロパティ.
    public float Score => score;

    private void Start()
    {
        // GridManagerに自身を登録.
        if (GridManager.Instance() != null)
        {
            GridManager.Instance().SetScoreView(this);
        }

        // スコア表示を初期化.
        UpdateScoreDisplay();
    }

    // スコアを加算する.
    public void AddScore(float addScore, string historyEntry)
    {
        score += addScore;

        // 履歴に追加.
        scoreHistory.Add(historyEntry);

        // 表示を更新.
        UpdateScoreDisplay();
        UpdateHistoryDisplay();
    }

    // スコア表示を更新.
    private void UpdateScoreDisplay()
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString("F0") + " pt";
        }
    }

    // 履歴表示を更新.
    private void UpdateHistoryDisplay()
    {
        if (historyText != null)
        {
            // 最新5件を表示.
            int startIndex = Mathf.Max(0, scoreHistory.Count - 5);
            string historyStr = "";
            for (int i = startIndex; i < scoreHistory.Count; i++)
            {
                historyStr += scoreHistory[i] + "\n";
            }
            historyText.text = historyStr;
        }
    }

    // スコアをリセット.
    public void ResetScore()
    {
        score = 0;
        scoreHistory.Clear();
        UpdateScoreDisplay();
        UpdateHistoryDisplay();
    }
}
