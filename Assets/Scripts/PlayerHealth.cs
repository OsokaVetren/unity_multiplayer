using System;
using Mirror;
using UnityEngine;

/// <summary>
/// Здоровье игрока + базовая логика смерти.
/// Респавн и счёт ведёт <see cref="GameManager"/>.
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
            return;

        // Во время разогрева/конца раунда урон не идёт
        if (GameManager.Instance != null &&
            GameManager.Instance.State != GameManager.MatchState.RoundActive)
            return;

        health = Mathf.Max(0, health - damage);

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

        // Отключаем стрельбу/перезарядку, чтобы труп не действовал
        PlayerShooting shooting = GetComponent<PlayerShooting>();
        if (shooting != null) shooting.CancelReload();

        // Уведомление клиента о смерти (для UI/звуков)
        if (connectionToClient != null)
            TargetOnDied(connectionToClient);

        // Делегируем респавн и подсчёт убийств менеджеру
        if (GameManager.Instance != null)
            GameManager.Instance.ReportKill(this, killer);
        else
        {
            // Fallback: если GameManager нет — просто восстановим хп
            ServerRespawn(transform.position, transform.rotation);
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
    }

    [TargetRpc]
    private void TargetOnDied(NetworkConnection target)
    {
        // Здесь можно проигрывать экран смерти, звук и т.п.
        Debug.Log("<color=red>Вы погибли. Ожидание респавна...</color>");
    }

    // ---------------- SYNC HOOKS ----------------

    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        OnHealthChangedEvent?.Invoke(newHealth, maxHealth);
    }
}
