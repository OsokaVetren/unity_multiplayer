using System;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHealthChanged))]
    public int health = 100;

    [SerializeField] private int maxHealth = 100;

    public int MaxHealth => maxHealth;
    public int CurrentHealth => health;

    public event Action<int, int> OnHealthChangedEvent;

    private Image healthBarFill;
    private Vector3 spawnPoint;

    private void Start()
    {
        spawnPoint = transform.position;
    }

    public override void OnStartServer()
    {
        health = maxHealth;
    }

    [Server]
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, null, 0);
    }

    [Server]
    public void TakeDamage(int damage, PlayerHealth attacker, ulong damageEventId)
    {
        if (damage <= 0 || health <= 0)
            return;

        health = Mathf.Max(0, health - damage);

        if (attacker != null && damageEventId != 0)
        {
            PlayerRewind rewind = GetComponent<PlayerRewind>();
            if (rewind != null)
                rewind.RegisterIncomingDamage(attacker, damage, damageEventId);
        }
    }

    [Server]
    public void ServerSetHealth(int value)
    {
        health = Mathf.Clamp(value, 0, maxHealth);
    }

    [Server]
    public void ServerHeal(int amount)
    {
        if (amount <= 0)
            return;

        health = Mathf.Min(maxHealth, health + amount);
    }

    private void Respawn()
    {
        health = maxHealth;

        if (connectionToClient != null)
            TargetRespawn(connectionToClient, spawnPoint);
    }

    [TargetRpc]
    private void TargetRespawn(NetworkConnection target, Vector3 position)
    {
        CharacterController controller = GetComponent<CharacterController>();
        if (controller != null)
            controller.enabled = false;

        transform.position = position;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (controller != null)
            controller.enabled = true;
    }

    private void OnHealthChanged(int oldHealth, int newHealth)
    {
        if (isLocalPlayer)
            OnHealthChangedEvent?.Invoke(newHealth, maxHealth);
    }
}