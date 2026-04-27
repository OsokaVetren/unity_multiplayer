using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Здоровье игрока + базовая логика смерти.
/// Респавн и счёт ведёт <see cref="GameManager"/>.
///
/// ИСПРАВЛЕНО:
/// - Debug.Log для каждого урона, смерти и респавна
/// - Респавн в точках спавна (NetworkSpawnPoint) через GameManager
/// - Если GameManager отсутствует — fallback респавн в случайной точке спавна
/// </summary>
[RequireComponent(typeof(NetworkIdentity))]
public class PlayerHealth : NetworkBehaviour
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;

    [SyncVar(hook = nameof(OnHealthChanged))]
    public int health = 100;

    [SyncVar] public bool isDead;

    /// <summary>Локальное событие для UI: (current, max).</summary>
    public event Action<int, int> OnHealthChangedEvent;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => health;

    // ---------------- SERVER LIFECYCLE ----------------

    public override void OnStartServer()
    {
        base.OnStartServer();
        health = maxHealth;
        isDead = false;

        Debug.Log($"<color=green>[PlayerHealth] {gameObject.name} появился на сервере, HP={health}/{maxHealth}</color>");

        // Сообщить менеджеру матча, что игрок появился
        if (GameManager.Instance != null)
            GameManager.Instance.RegisterPlayer(this);
    }

    public override void OnStopServer()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.UnregisterPlayer(this);

        base.OnStopServer();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        OnHealthChangedEvent?.Invoke(health, maxHealth);
    }

    // ---------------- DAMAGE ----------------

    /// <summary>Удобная перегрузка для урона от мира / суицида.</summary>
    [Server]
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null, 0UL);
    }

    /// <summary>
    /// Основной метод нанесения урона.
    /// </summary>
    /// <param name="damage">Количество урона (>0).</param>
    /// <param name="attacker">Нападавший (может быть null для урона от мира).</param>
    /// <param name="damageEventId">Идентификатор события для системы перемотки (0 = без перемотки).</param>
    [Server]
    public void TakeDamage(int damage, PlayerHealth attacker, ulong damageEventId)
    {
        if (damage <= 0 || isDead)
        {
            if (isDead)
                Debug.Log($"<color=gray>[PlayerHealth] {gameObject.name} уже мёртв, урон {damage} игнорируется</color>");
            return;
        }

        // Во время разогрева/конца раунда урон не идёт
        if (GameManager.Instance != null &&
            GameManager.Instance.State != GameManager.MatchState.RoundActive)
        {
            Debug.Log($"<color=gray>[PlayerHealth] Урон {damage} по {gameObject.name} заблокирован: состояние матча = {GameManager.Instance.State}</color>");
            return;
        }

        int oldHealth = health;
        health = Mathf.Max(0, health - damage);

        // FIX: Debug.Log для КАЖДОГО урона
        string attackerName = attacker != null ? attacker.gameObject.name : "World";
        Debug.Log($"<color=orange>[DAMAGE] {attackerName} -> {gameObject.name}: -{damage} HP ({oldHealth} -> {health})</color>");

        // Уведомить всех клиентов о нанесении урона (через RPC)
        RpcOnDamageTaken(damage, attackerName, health, maxHealth);

        // Зарегистрировать входящий урон у системы перемотки (для отката)
        if (attacker != null && damageEventId != 0UL)
        {
            PlayerRewind rewind = GetComponent<PlayerRewind>();
            if (rewind != null)
                rewind.RegisterIncomingDamage(attacker, damage, damageEventId);
        }

        if (health > 0)
            return;

        // Игрок умер — отдать управление GameManager (он начислит счёт и заспавнит)
        Die(attacker);
    }

    [Server]
    private void Die(PlayerHealth killer)
    {
        if (isDead) return;
        isDead = true;

        string killerName = killer != null ? killer.gameObject.name : "World";

        // FIX: Debug.Log для смерти
        Debug.Log($"<color=red>[DEATH] {gameObject.name} убит! Убийца: {killerName}</color>");

        // Отключаем стрельбу/перезарядку, чтобы труп не действовал
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null) shooting.CancelReload();

        // Уведомление ВСЕХ клиентов о смерти (для логов и UI)
        RpcOnPlayerDied(gameObject.name, killerName);

        // Уведомление клиента жертвы о смерти (для UI/звуков)
        if (connectionToClient != null)
            TargetOnDied(connectionToClient);

        // Делегируем респавн и подсчёт убийств менеджеру
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ReportKill(this, killer);
        }
        else
        {
            // FIX: Fallback: если GameManager нет — ищем точку спавна вместо текущей позиции
            Debug.Log($"<color=yellow>[PlayerHealth] GameManager отсутствует, используем fallback респавн</color>");
            Vector3 respawnPos = transform.position;
            Quaternion respawnRot = transform.rotation;

            // Попробуем найти точки спавна напрямую
            NetworkSpawnPoint[] spawnPoints = FindObjectsByType<NetworkSpawnPoint>(FindObjectsSortMode.None);
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var sp = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                respawnPos = sp.transform.position;
                respawnRot = sp.transform.rotation;
            }

            ServerRespawn(respawnPos, respawnRot);
        }
    }

    // ---------------- HEAL / RESTORE ----------------

    [Server]
    public void ServerHeal(int amount)
    {
        if (amount <= 0 || isDead) return;
        health = Mathf.Min(maxHealth, health + amount);
    }

    [Server]
    public void ServerSetHealth(int value)
    {
        health = Mathf.Clamp(value, 0, maxHealth);
        if (health > 0) isDead = false;
    }

    // ---------------- RESPAWN ----------------

    /// <summary>
    /// Серверный респавн игрока в указанной точке.
    /// Вызывается из <see cref="GameManager"/>.
    /// </summary>
    [Server]
    public void ServerRespawn(Vector3 position, Quaternion rotation)
    {
        health = maxHealth;
        isDead = false;

        // FIX: Debug.Log для респавна
        Debug.Log($"<color=green>[RESPAWN] {gameObject.name} возрождён в позиции {position}, HP={health}/{maxHealth}</color>");

        TeleportInternal(position, rotation);

        // На клиенте тоже зачистить velocity / телепортнуть CharacterController
        if (connectionToClient != null)
            TargetRespawn(connectionToClient, position, rotation);
    }

    [Server]
    private void TeleportInternal(Vector3 position, Quaternion rotation)
    {
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cc != null) cc.enabled = true;
    }

    // ---------------- TARGET RPCs ----------------

    [TargetRpc]
    private void TargetRespawn(NetworkConnection target, Vector3 position, Quaternion rotation)
    {
        // На локальном клиенте тоже надо принудительно переместить CC
        CharacterController cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        transform.SetPositionAndRotation(position, rotation);

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null && !rb.isKinematic)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (cc != null) cc.enabled = true;

        Debug.Log($"<color=green>[RESPAWN] Вы возродились! HP={health}/{maxHealth}</color>");
    }

    [TargetRpc]
    private void TargetOnDied(NetworkConnection target)
    {
        // Здесь можно проигрывать экран смерти, звук и т.п.
        Debug.Log("<color=red>Вы погибли. Ожидание респавна...</color>");
    }

    // FIX: Новый RPC — уведомление ВСЕХ клиентов о нанесении урона (для дебага)
    [ClientRpc]
    private void RpcOnDamageTaken(int damage, string attackerName, int currentHp, int maxHp)
    {
        Debug.Log($"<color=orange>[DAMAGE] {attackerName} -> {gameObject.name}: -{damage} (HP: {currentHp}/{maxHp})</color>");
    }

    // FIX: Новый RPC — уведомление ВСЕХ клиентов о смерти
    [ClientRpc]
    private void RpcOnPlayerDied(string victimName, string killerName)
    {
        Debug.Log($"<color=red>[DEATH] {victimName} убит игроком {killerName}!</color>");
    }

    // ---------------- SYNC HOOKS ----------------

    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        OnHealthChangedEvent?.Invoke(newHealth, maxHealth);
    }
}
