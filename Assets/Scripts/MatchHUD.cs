using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mirror;

/// <summary>
/// HUD матча: верх экрана — таймер, раунд, состояние; справа — мини-счёт.
/// Подключается на тот же Canvas, где PlayerHUD (или отдельным).
///
/// В инспекторе перетащите ссылки:
///   - timerText (TextMeshProUGUI)
///   - roundText (TextMeshProUGUI)
///   - stateText (TextMeshProUGUI)
///   - killsText (TextMeshProUGUI)  (мой счёт)
///   - deathsText (TextMeshProUGUI)
///   - scoreboardPanel (GameObject) — панель таблицы по Tab
///   - scoreboardEntryPrefab (GameObject) — префаб строки таблицы (3 TMP_Text: name, kills, deaths)
///   - scoreboardContent (Transform) — родитель строк (Vertical Layout Group)
/// </summary>
public class MatchHUD : MonoBehaviour
{
    [Header("Top Bar")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI stateText;

    [Header("My Score")]
    [SerializeField] private TextMeshProUGUI killsText;
    [SerializeField] private TextMeshProUGUI deathsText;

    [Header("Scoreboard (Tab)")]
    [SerializeField] private GameObject scoreboardPanel;
    [SerializeField] private Transform scoreboardContent;
    [SerializeField] private GameObject scoreboardEntryPrefab;
    [SerializeField] private KeyCode scoreboardKey = KeyCode.Tab;

    [Header("Center Banner")]
    [SerializeField] private GameObject bannerPanel;
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private float bannerHideDelay = 3f;

    private GameManager gm;
    private float bannerHideTime;

    private void Start()
    {
        if (scoreboardPanel != null) scoreboardPanel.SetActive(false);
        if (bannerPanel != null) bannerPanel.SetActive(false);
        TryBind();
    }

    private void TryBind()
    {
        gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnMatchStateChanged += HandleStateChanged;
        gm.OnRoundNumberChanged += HandleRoundChanged;
        gm.OnTimeRemainingChanged += HandleTimeChanged;
        gm.OnScoreUpdated += HandleScoreUpdated;

        // Сразу синкаем
        HandleStateChanged(gm.State);
        HandleRoundChanged(gm.CurrentRound);
        HandleTimeChanged(gm.TimeRemaining);
        HandleScoreUpdated();
    }

    private void OnDisable()
    {
        if (gm != null)
        {
            gm.OnMatchStateChanged -= HandleStateChanged;
            gm.OnRoundNumberChanged -= HandleRoundChanged;
            gm.OnTimeRemainingChanged -= HandleTimeChanged;
            gm.OnScoreUpdated -= HandleScoreUpdated;
        }
    }

    private void Update()
    {
        // Если GameManager появился позже — биндимся
        if (gm == null && GameManager.Instance != null)
            TryBind();

        // Toggle scoreboard
        if (scoreboardPanel != null)
        {
            if (Input.GetKeyDown(scoreboardKey))
            {
                scoreboardPanel.SetActive(true);
                RebuildScoreboard();
            }
            if (Input.GetKeyUp(scoreboardKey))
                scoreboardPanel.SetActive(false);
        }

        // Auto-hide banner
        if (bannerPanel != null && bannerPanel.activeSelf && Time.time > bannerHideTime)
            bannerPanel.SetActive(false);
    }

    private void HandleTimeChanged(float seconds)
    {
        if (timerText == null) return;
        int s = Mathf.CeilToInt(Mathf.Max(0f, seconds));
        int m = s / 60;
        int sec = s % 60;
        timerText.text = $"{m:00}:{sec:00}";
    }

    private void HandleRoundChanged(int round)
    {
        if (roundText != null)
            roundText.text = round > 0 ? $"Раунд {round}" : "—";
    }

    private void HandleStateChanged(GameManager.MatchState state)
    {
        if (stateText != null)
        {
            stateText.text = state switch
            {
                GameManager.MatchState.Warmup => "Разогрев",
                GameManager.MatchState.RoundActive => "Бой",
                GameManager.MatchState.RoundEnd => "Раунд окончен",
                GameManager.MatchState.MatchEnd => "МАТЧ ОКОНЧЕН",
                _ => state.ToString()
            };
        }

        // Баннеры
        if (bannerPanel != null && bannerText != null)
        {
            switch (state)
            {
                case GameManager.MatchState.Warmup:
                    ShowBanner("Подготовка...", bannerHideDelay);
                    break;
                case GameManager.MatchState.RoundActive:
                    ShowBanner($"Раунд {gm.CurrentRound} — В БОЙ!", bannerHideDelay);
                    break;
                case GameManager.MatchState.RoundEnd:
                    string winner = GetPlayerName(gm.LastRoundWinnerNetId);
                    ShowBanner(string.IsNullOrEmpty(winner) ? "Раунд окончен — ничья" : $"Раунд взял: {winner}", bannerHideDelay);
                    break;
                case GameManager.MatchState.MatchEnd:
                    ShowBanner("МАТЧ ОКОНЧЕН", 9999f);
                    break;
            }
        }

        HandleScoreUpdated();
    }

    private void ShowBanner(string text, float duration)
    {
        if (bannerPanel == null || bannerText == null) return;
        bannerText.text = text;
        bannerPanel.SetActive(true);
        bannerHideTime = Time.time + duration;
    }

    private string GetPlayerName(uint netId)
    {
        if (gm == null || netId == 0) return null;
        var s = gm.GetScore(netId);
        return s.HasValue ? s.Value.PlayerName : null;
    }

    private void HandleScoreUpdated()
    {
        if (gm == null) return;

        // Мой счёт (по локальному игроку)
        if (NetworkClient.localPlayer != null)
        {
            uint myId = NetworkClient.localPlayer.netId;
            var mine = gm.GetScore(myId);
            if (mine.HasValue)
            {
                if (killsText != null) killsText.text = $"K: {mine.Value.RoundKills} ({mine.Value.TotalKills})";
                if (deathsText != null) deathsText.text = $"D: {mine.Value.RoundDeaths} ({mine.Value.TotalDeaths})";
            }
            else
            {
                if (killsText != null) killsText.text = "K: 0";
                if (deathsText != null) deathsText.text = "D: 0";
            }
        }

        if (scoreboardPanel != null && scoreboardPanel.activeSelf)
            RebuildScoreboard();
    }

    private void RebuildScoreboard()
    {
        if (scoreboardContent == null || scoreboardEntryPrefab == null || gm == null) return;

        // Очищаем
        for (int i = scoreboardContent.childCount - 1; i >= 0; i--)
            Destroy(scoreboardContent.GetChild(i).gameObject);

        // Сортируем по очкам в раундах -> киллам
        var list = new System.Collections.Generic.List<PlayerScore>();
        for (int i = 0; i < gm.Scores.Count; i++) list.Add(gm.Scores[i]);
        list.Sort((a, b) =>
        {
            int rw = b.RoundWins.CompareTo(a.RoundWins);
            if (rw != 0) return rw;
            int tk = b.TotalKills.CompareTo(a.TotalKills);
            if (tk != 0) return tk;
            return a.TotalDeaths.CompareTo(b.TotalDeaths);
        });

        foreach (var s in list)
        {
            var go = Instantiate(scoreboardEntryPrefab, scoreboardContent);
            var entry = go.GetComponent<ScoreboardEntry>();
            if (entry != null) entry.Set(s);
            else
            {
                // Fallback: ищем 3 первых TMP-текста по дочерним объектам
                var texts = go.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (texts.Length >= 1) texts[0].text = s.PlayerName;
                if (texts.Length >= 2) texts[1].text = s.TotalKills.ToString();
                if (texts.Length >= 3) texts[2].text = s.TotalDeaths.ToString();
                if (texts.Length >= 4) texts[3].text = s.RoundWins.ToString();
            }
        }
    }
}
