using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Сетевой менеджер матча: спавнит GameManager на сервере и игроков в случайных точках спавна.
/// </summary>
[AddComponentMenu("Network/MatchNetworkManager")]
public class MatchNetworkManager : NetworkManager
{
    [Header("Match")]
    [Tooltip("Префаб GameManager (должен иметь NetworkIdentity и быть зарегистрирован в Spawnable Prefabs)")]
    public GameObject gameManagerPrefab;

    [Tooltip("Имя главного меню — на нём GameManager не спавнится")]
    public string mainMenuSceneName = "MainMenu";

    [Tooltip("Список имён игровых сцен")]
    public string[] gameSceneNames = new[] { "Demo_1", "Demo_02" };

    private GameObject spawnedGameManager;

    public override void OnStartServer()
    {
        base.OnStartServer();
        TrySpawnGameManager();
    }

    public override void OnServerSceneChanged(string sceneName)
    {
        base.OnServerSceneChanged(sceneName);

        if (!IsGameScene(sceneName))
            return;

        // После смены сцены GameManager мог быть уничтожен — пересоздадим
        TrySpawnGameManager();
    }

    public override void OnServerAddPlayer(NetworkConnectionToClient conn)
    {
        // Защита от двойного спавна
        if (conn.identity != null)
        {
            Debug.LogWarning($"[MatchNetworkManager] У соединения {conn.connectionId} уже есть игрок. Пропускаем.");
            return;
        }

        // Берём точку спавна из GameManager
        Transform spawn = null;
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RefreshSpawnPoints();
            spawn = GameManager.Instance.GetRandomSpawnPoint();
        }

        Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
        Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

        GameObject player = Instantiate(playerPrefab, pos, rot);
        player.name = $"{playerPrefab.name} [conn:{conn.connectionId}]";

        NetworkServer.AddPlayerForConnection(conn, player);

        Debug.Log($"[MatchNetworkManager] Игрок conn={conn.connectionId} заспавнен в {pos}");
    }

    public override void OnServerDisconnect(NetworkConnectionToClient conn)
    {
        // PlayerHealth.OnStopServer сам вызовет UnregisterPlayer в GameManager
        base.OnServerDisconnect(conn);
    }

    public override void OnStopServer()
    {
        if (spawnedGameManager != null)
        {
            NetworkServer.Destroy(spawnedGameManager);
            spawnedGameManager = null;
        }
        base.OnStopServer();
    }

    public override void OnStopHost()
    {
        base.OnStopHost();
        if (spawnedGameManager != null)
        {
            Destroy(spawnedGameManager);
            spawnedGameManager = null;
        }
    }

    // ----------------- HELPERS -----------------

    private void TrySpawnGameManager()
    {
        if (gameManagerPrefab == null)
            return;

        if (GameManager.Instance != null)
            return;

        // Спавним только в игровой сцене
        string current = SceneManager.GetActiveScene().name;
        if (!IsGameScene(current))
            return;

        spawnedGameManager = Instantiate(gameManagerPrefab);
        NetworkServer.Spawn(spawnedGameManager);

        Debug.Log("[MatchNetworkManager] GameManager заспавнен на сервере");
    }

    private bool IsGameScene(string scene)
    {
        if (gameSceneNames == null) return false;
        for (int i = 0; i < gameSceneNames.Length; i++)
            if (gameSceneNames[i] == scene) return true;
        return false;
    }
}
