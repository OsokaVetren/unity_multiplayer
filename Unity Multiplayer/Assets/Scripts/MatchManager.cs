using UnityEngine;
using Mirror;
using System.Collections.Generic;
using System.Linq;

public class MatchManager : NetworkBehaviour
{
    public static MatchManager instance;

    [SyncVar] public bool isGameOver = false;
    [SyncVar] public string winnerName = "";
    
    public int targetKills = 5;

    private void Awake() => instance = this;

    // Эту функцию должен вызывать сервер, когда кто-то погибает
    [Server]
    public void OnPlayerKilled(PlayerScore killer, PlayerScore victim)
    {
        if (isGameOver) return;

        killer.kills++;

        if (killer.kills >= targetKills)
        {
            EndMatch(killer.playerName);
        }
        else
        {
            RestartRound();
        }
    }

    [Server]
    void RestartRound()
    {
        // Получаем все точки спавна
        Transform[] spawnPoints = NetworkManager.startPositions.ToArray();
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        print(players.Length);
        for (int i = 0; i < players.Length; i++)
        {
            print("b");
            Vector3 spawnPos = spawnPoints[i % spawnPoints.Length].position;
            players[i].GetComponent<PlayerHealth>().Respawn(spawnPos);
        }
    }

    [Server]
    void EndMatch(string winner)
    {
        isGameOver = true;
        winnerName = winner;
        RpcShowVictoryScreen(winner);
    }

    [ClientRpc]
    void RpcShowVictoryScreen(string winner)
    {
        MatchUI.instance.ShowVictory(winner);
    }

    [Server]
    public void ServerRestartGame()
    {
        // Сброс очков
        foreach (var p in GameObject.FindObjectsOfType<PlayerScore>())
        {
            p.kills = 0;
        }
        isGameOver = false;
        RestartRound();
    }
}