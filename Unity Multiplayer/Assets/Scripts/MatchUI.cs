using UnityEngine;
using UnityEngine.UI;
using Mirror;
using TMPro; 
using System.Collections.Generic;
using System.Linq;
using Mirror.Examples.TopDownShooter;

public class MatchUI : MonoBehaviour
{
    public static MatchUI instance;

    [Header("Score Settings")]
    public TMP_Text leftScoreText;  // Перетащите сюда левый текст
    public TMP_Text rightScoreText; // Перетащите сюда правый текст
    
    public GameObject victoryPanel;
    public TMP_Text victoryText;
    public Button restartButton;

    private void Awake() => instance = this;

    void Start()
    {
        victoryPanel.SetActive(false);
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(NetworkServer.active);
            restartButton.onClick.AddListener(OnRestartPressed);
        }
    }

    public void UpdateScoreBoard()
    {
        var players = GameObject.FindObjectsOfType<PlayerScore>();
        
        leftScoreText.text = "";
        rightScoreText.text = "";

        foreach (var p in players)
        {
            string line = $"{p.playerName}: {p.kills}";
            
            if (p.isLocalPlayer) 
            {
                // Вы — всегда слева (зеленым, например)
                leftScoreText.text = $"<color=green>YOU: {p.kills}</color>";
            }
            else 
            {
                // Враг — всегда справа
                rightScoreText.text = $"<color=red>ENEMY: {p.kills}</color>";
            }
        }
    }

    // Остальные методы без изменений
    public void ShowVictory(string winner)
    {
        victoryPanel.SetActive(true);
        victoryText.text = $"ТЫ ПОБЕДИЛ, ТЫ МОЛОДЧИНА!";
    }

    public void HideVictory()
    {
        victoryPanel.SetActive(false);
    }

    void OnRestartPressed()
    {
        if (NetworkServer.active)
        {
            MatchManager.instance.ServerRestartGame();
            HideVictory();
        }
    }
}