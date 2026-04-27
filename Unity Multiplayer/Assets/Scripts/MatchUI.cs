using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro; 

public class MatchUI : MonoBehaviour
{
    public static MatchUI instance;

    public TMP_Text scoreText;
    public GameObject victoryPanel;
    public TMP_Text victoryText;
    public Button restartButton;

    private void Awake() => instance = this;

    void Start()
    {
        victoryPanel.SetActive(false);
        // Только сервер (хост) может нажать кнопку перезапуска
        restartButton.gameObject.SetActive(NetworkServer.active);
        restartButton.onClick.AddListener(OnRestartPressed);
    }

    public void UpdateScoreBoard()
    {
        string info = "СЧЁТ:\n";
        foreach (var p in GameObject.FindObjectsOfType<PlayerScore>())
        {
            info += $"{p.playerName}: {p.kills}\n";
        }
        scoreText.text = info;
    }

    public void ShowVictory(string winner)
    {
        victoryPanel.SetActive(true);
        victoryText.text = $"ПОБЕДИТЕЛЬ: {winner}";
    }

    void OnRestartPressed()
    {
        if (NetworkServer.active)
        {
            victoryPanel.SetActive(false);
            MatchManager.instance.ServerRestartGame();
        }
    }
}