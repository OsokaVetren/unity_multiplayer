using UnityEngine;
using Mirror;

public class PlayerScore : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnScoreChanged))]
    public int kills = 0;

    [SyncVar]
    public string playerName;

    void OnScoreChanged(int oldKills, int newKills)
    {
        // Обновляем UI (реализуем позже)
        if (MatchUI.instance != null) MatchUI.instance.UpdateScoreBoard();
    }

    [ClientRpc]
    public void RpcResetPosition(Vector3 spawnPos)
    {
        // Отключаем CharacterController/Rigidbody перед перемещением, если они есть
        transform.position = spawnPos;
        // Сбрасываем углы вращения, если нужно
    }
}