using TMPro;
using UnityEngine;
using Mirror;

/// <summary>
/// Одна строка в таблице счёта.
/// Префаб должен содержать 4 TextMeshProUGUI: имя, киллы, смерти, победы в раундах.
/// </summary>
public class ScoreboardEntry : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI deathsText;
    [SerializeField] private TextMeshProUGUI roundsWonText;
    [SerializeField] private UnityEngine.UI.Image background;
    [SerializeField] private Color localPlayerHighlight = new Color(0.2f, 0.6f, 1f, 0.35f);
    [SerializeField] private Color defaultBg = new Color(0f, 0f, 0f, 0.35f);

    public void Set(PlayerScore score)
    {
        if (nameText != null) nameText.text = score.PlayerName;
        if (killsText != null) killsText.text = score.TotalKills.ToString();
        if (deathsText != null) deathsText.text = score.TotalDeaths.ToString();
        if (roundsWonText != null) roundsWonText.text = score.RoundWins.ToString();

        if (background != null)
        {
            bool isMe = NetworkClient.localPlayer != null && NetworkClient.localPlayer.netId == score.NetId;
            background.color = isMe ? localPlayerHighlight : defaultBg;
        }
    }
}
