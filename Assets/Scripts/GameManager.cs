using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

/// <summary>
/// Главный серверный менеджер матча: раунды, таймер, счёт, спавн.
/// На сцене должен быть один GameObject c этим скриптом и NetworkIdentity.
/// </summary>
public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum MatchState : byte
    {
        Warmup = 0,
        RoundActive = 1,
        RoundEnd = 2,
        MatchEnd = 3
    }

    [Header("Match Settings")]
    [Tooltip("Длительность одного раунда (сек)")]
    [SerializeField] private float roundDuration = 180f;

    [Tooltip("Длительность разогрева перед первым раундом (сек)")]
    [SerializeField] private float warmupDuration = 5f;

    [Tooltip("Длительность экрана 'раунд окончен' между раундами (сек)")]
    [SerializeField] private float roundEndDuration = 5f;

    [Tooltip("Сколько раундов всего в матче")]
    [SerializeField] private int totalRounds = 5;

    [Tooltip("До скольки убийств для победы в раунде (0 = играем только до таймера)")]
    [SerializeField] private int killsToWinRound = 0;

    [Header("Respawn")]
    [Tooltip("Через сколько секунд игрок респавнится после смерти")]
    [SerializeField] public float respawnDelay = 3f;

    // ============= СИНХРОНИЗИРУЕМОЕ СОСТОЯНИЕ =============

    [SyncVar(hook = nameof(OnStateChanged))]
    public MatchState State = MatchState.Warmup;

    [SyncVar(hook = nameof(OnRoundChanged))]
    public int CurrentRound = 0;

    /// <summary>Сколько секунд осталось до конца текущей фазы.</summary>
    [SyncVar(hook = nameof(OnTimeChanged))]
    public float TimeRemaining = 0f;

    [SyncVar]
    public uint LastRoundWinnerNetId = 0;

    /// <summary>Список всех игроков матча (синхронизируется автоматически)</summary>
    public readonly SyncList<PlayerScore> Scores = new SyncList<PlayerScore>();

    // События для UI
    public event Action OnScoreUpdated;
    public event Action<MatchState> OnMatchStateChanged;
    public event Action<int> OnRoundNumberChanged;
    public event Action<float> OnTimeRemainingChanged;

    // ============= ВНУТРЕННЕЕ =============
    private float _phaseEndTime;
    private readonly List<Transform> _spawnPoints = new();
    private int _lastSpawnIndex = -1;

    public IReadOnlyList<Transform> SpawnPoints => _spawnPoints;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        RefreshSpawnPoints();
        EnterWarmup();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Scores.Callback += OnScoreSyncListChanged;
        OnMatchStateChanged?.Invoke(State);
        OnRoundNumberChanged?.Invoke(CurrentRound);
        OnTimeRemainingChanged?.Invoke(TimeRemaining);
        OnScoreUpdated?.Invoke();
    }

    public override void OnStopClient()
    {
        Scores.Callback -= OnScoreSyncListChanged;
        base.OnStopClient();
    }

    private void OnScoreSyncListChanged(SyncList<PlayerScore>.Operation op, int index, PlayerScore oldItem, PlayerScore newItem)
    {
        OnScoreUpdated?.Invoke();
    }

    private void OnStateChanged(MatchState oldS, MatchState newS) => OnMatchStateChanged?.Invoke(newS);
    private void OnRoundChanged(int oldR, int newR) => OnRoundNumberChanged?.Invoke(newR);
    private void OnTimeChanged(float oldT, float newT) => OnTimeRemainingChanged?.Invoke(newT);

    // =====================================================
    // СЕРВЕРНАЯ ЛОГИКА ФАЗ МАТЧА
    // =====================================================

    [ServerCallback]
    private void Update()
    {
        TimeRemaining = Mathf.Max(0f, _phaseEndTime - Time.time);

        switch (State)
        {
            case MatchState.Warmup:
                if (Time.time >= _phaseEndTime)
                    StartNextRound();
                break;

            case MatchState.RoundActive:
                if (Time.time >= _phaseEndTime)
                    EndRound(FindLeaderNetId());
                break;

            case MatchState.RoundEnd:
                if (Time.time >= _phaseEndTime)
                {
                    if (CurrentRound >= totalRounds)
                        EnterMatchEnd();
                    else
                        StartNextRound();
                }
                break;

            case MatchState.MatchEnd:
                // Ждём рестарта от хоста
                break;
        }
    }

    [Server]
    private void EnterWarmup()
    {
        State = MatchState.Warmup;
        CurrentRound = 0;
        _phaseEndTime = Time.time + warmupDuration;
        Debug.Log($"[GameManager] Warmup started, {warmupDuration}s");
    }

    [Server]
    private void StartNextRound()
    {
        CurrentRound++;
        State = MatchState.RoundActive;
        _phaseEndTime = Time.time + roundDuration;

        // Сброс киллов/смертей для нового раунда (общий счёт побед сохраняется)
        for (int i = 0; i < Scores.Count; i++)
        {
            var s = Scores[i];
            s.RoundKills = 0;
            s.RoundDeaths = 0;
            Scores[i] = s;
        }

        RespawnAllPlayers();

        RpcAnnounceRoundStart(CurrentRound);
        Debug.Log($"[GameManager] Round {CurrentRound}/{totalRounds} started");
    }

    [Server]
    private void EndRound(uint winnerNetId)
    {
        LastRoundWinnerNetId = winnerNetId;

        if (winnerNetId != 0)
        {
            int idx = FindScoreIndexByNetId(winnerNetId);
            if (idx >= 0)
            {
                var s = Scores[idx];
                s.RoundWins++;
                Scores[idx] = s;
            }
        }

        State = MatchState.RoundEnd;
        _phaseEndTime = Time.time + roundEndDuration;
        RpcAnnounceRoundEnd(winnerNetId);
        Debug.Log($"[GameManager] Round {CurrentRound} ended, winner netId={winnerNetId}");
    }

    [Server]
    private void EnterMatchEnd()
    {
        State = MatchState.MatchEnd;
        _phaseEndTime = Time.time + 9999f;

        uint matchWinner = FindLeaderNetId();
        RpcAnnounceMatchEnd(matchWinner);
        Debug.Log($"[GameManager] Match ended, winner netId={matchWinner}");
    }

    /// <summary>Полный рестарт матча.</summary>
    [Server]
    public void RestartMatch()
    {
        for (int i = 0; i < Scores.Count; i++)
        {
            var s = Scores[i];
            s.RoundKills = 0;
            s.RoundDeaths = 0;
            s.TotalKills = 0;
            s.TotalDeaths = 0;
            s.RoundWins = 0;
            Scores[i] = s;
        }
        EnterWarmup();
    }

    // =====================================================
    // РЕГИСТРАЦИЯ ИГРОКОВ
    // =====================================================

    [Server]
    public void RegisterPlayer(PlayerHealth player)
    {
        if (player == null) return;
        uint netId = player.netId;
        if (FindScoreIndexByNetId(netId) >= 0) return;

        string name = $"Player {netId}";
        if (player.connectionToClient != null)
            name = $"Player {player.connectionToClient.connectionId}";

        Scores.Add(new PlayerScore
        {
            NetId = netId,
            PlayerName = name,
            RoundKills = 0,
            RoundDeaths = 0,
            TotalKills = 0,
            TotalDeaths = 0,
            RoundWins = 0
        });

        // Если матч уже идёт — сразу заспавним
        Transform sp = GetRandomSpawnPoint();
        if (sp != null)
            player.ServerRespawn(sp.position, sp.rotation);
    }

    [Server]
    public void UnregisterPlayer(PlayerHealth player)
    {
        if (player == null) return;
        int idx = FindScoreIndexByNetId(player.netId);
        if (idx >= 0) Scores.RemoveAt(idx);
    }

    [Server]
    public void ReportKill(PlayerHealth victim, PlayerHealth killer)
    {
        if (victim == null) return;

        // Если раунд не активен — счёт не считаем, но респавн всё равно делаем
        if (State != MatchState.RoundActive)
        {
            StartCoroutine(RespawnAfter(victim, respawnDelay));
            return;
        }

        // Жертва: +1 смерть
        int vIdx = FindScoreIndexByNetId(victim.netId);
        if (vIdx >= 0)
        {
            var s = Scores[vIdx];
            s.RoundDeaths++;
            s.TotalDeaths++;
            Scores[vIdx] = s;
        }

        // Киллер: +1 убийство (если это не самоубийство и не урон от мира)
        if (killer != null && killer != victim)
        {
            int kIdx = FindScoreIndexByNetId(killer.netId);
            if (kIdx >= 0)
            {
                var s = Scores[kIdx];
                s.RoundKills++;
                s.TotalKills++;
                Scores[kIdx] = s;

                // Условие победы по киллам
                if (killsToWinRound > 0 && s.RoundKills >= killsToWinRound)
                {
                    EndRound(killer.netId);
                    StartCoroutine(RespawnAfter(victim, 0.5f));
                    return;
                }
            }
        }

        StartCoroutine(RespawnAfter(victim, respawnDelay));

        uint killerNetId = killer != null ? killer.netId : 0;
        uint victimNetId = victim.netId;
        RpcOnKill(killerNetId, victimNetId);
    }

    [Server]
    private IEnumerator RespawnAfter(PlayerHealth player, float delay)
    {
        if (player == null) yield break;
        yield return new WaitForSeconds(delay);
        if (player == null) yield break;

        Transform spawn = GetRandomSpawnPoint();
        Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
        Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

        player.ServerRespawn(pos, rot);
    }

    [Server]
    private void RespawnAllPlayers()
    {
        foreach (var conn in NetworkServer.connections.Values)
        {
            if (conn?.identity == null) continue;
            var ph = conn.identity.GetComponent<PlayerHealth>();
            if (ph != null)
            {
                Transform sp = GetRandomSpawnPoint();
                Vector3 pos = sp != null ? sp.position : ph.transform.position;
                Quaternion rot = sp != null ? sp.rotation : ph.transform.rotation;
                ph.ServerRespawn(pos, rot);
            }
        }
    }

    // =====================================================
    // СПАВН ТОЧКИ
    // =====================================================

    [Server]
    public void RefreshSpawnPoints()
    {
        _spawnPoints.Clear();
        var pts = GameObject.FindObjectsByType<NetworkSpawnPoint>(FindObjectsSortMode.None);
        foreach (var p in pts)
            _spawnPoints.Add(p.transform);

        Debug.Log($"[GameManager] Found {_spawnPoints.Count} spawn points");
    }

    [Server]
    public Transform GetRandomSpawnPoint()
    {
        if (_spawnPoints.Count == 0) RefreshSpawnPoints();
        if (_spawnPoints.Count == 0) return null;

        int idx;
        if (_spawnPoints.Count == 1) idx = 0;
        else
        {
            // Стараемся не давать ту же точку, что в прошлый раз
            do { idx = UnityEngine.Random.Range(0, _spawnPoints.Count); }
            while (idx == _lastSpawnIndex);
        }

        _lastSpawnIndex = idx;
        return _spawnPoints[idx];
    }

    // =====================================================
    // СЛУЖЕБНОЕ
    // =====================================================

    private int FindScoreIndexByNetId(uint netId)
    {
        for (int i = 0; i < Scores.Count; i++)
            if (Scores[i].NetId == netId) return i;
        return -1;
    }

    [Server]
    private uint FindLeaderNetId()
    {
        if (Scores.Count == 0) return 0;
        int bestKills = -1;
        uint bestId = 0;
        for (int i = 0; i < Scores.Count; i++)
        {
            if (Scores[i].RoundKills > bestKills)
            {
                bestKills = Scores[i].RoundKills;
                bestId = Scores[i].NetId;
            }
        }
        return bestKills > 0 ? bestId : 0;
    }

    public PlayerScore? GetScore(uint netId)
    {
        int idx = FindScoreIndexByNetId(netId);
        if (idx < 0) return null;
        return Scores[idx];
    }

    // =====================================================
    // RPCs (для фидбэка клиентам)
    // =====================================================

    [ClientRpc]
    private void RpcAnnounceRoundStart(int roundNumber)
    {
        Debug.Log($"<color=cyan>=== Раунд {roundNumber} начался ===</color>");
    }

    [ClientRpc]
    private void RpcAnnounceRoundEnd(uint winnerNetId)
    {
        Debug.Log($"<color=yellow>=== Раунд окончен, победитель netId={winnerNetId} ===</color>");
    }

    [ClientRpc]
    private void RpcAnnounceMatchEnd(uint winnerNetId)
    {
        Debug.Log($"<color=magenta>=== МАТЧ ОКОНЧЕН, победитель netId={winnerNetId} ===</color>");
    }

    [ClientRpc]
    private void RpcOnKill(uint killerNetId, uint victimNetId)
    {
        Debug.Log($"[Kill] {killerNetId} → {victimNetId}");
    }
}

/// <summary>Структура счёта одного игрока (синхронизируется через SyncList).</summary>
[Serializable]
public struct PlayerScore : IEquatable<PlayerScore>
{
    public uint NetId;
    public string PlayerName;
    public int RoundKills;
    public int RoundDeaths;
    public int TotalKills;
    public int TotalDeaths;
    public int RoundWins;

    public bool Equals(PlayerScore other)
    {
        return NetId == other.NetId
            && PlayerName == other.PlayerName
            && RoundKills == other.RoundKills
            && RoundDeaths == other.RoundDeaths
            && TotalKills == other.TotalKills
            && TotalDeaths == other.TotalDeaths
            && RoundWins == other.RoundWins;
    }
}
