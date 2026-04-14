using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerRewind : NetworkBehaviour
{
    [Header("Rewind")]
    [SerializeField] private float rewindWindowSeconds = 5f;
    [SerializeField] private float sampleInterval = 0.05f;
    [SerializeField] private float rewindCooldownSeconds = 8f;

    private PlayerHealth playerHealth;
    private PlayerShooting playerShooting;
    private CharacterController characterController;
    private Rigidbody rb;

    private float nextSampleTime;
    private float nextAllowedRewindTime;
    private ulong nextDamageEventId = 1;

    private readonly List<StateSnapshot> snapshots = new();
    private readonly List<DamageEvent> outgoingDamage = new();
    private readonly List<DamageEvent> incomingDamage = new();

    private struct StateSnapshot
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public int health;
        public int[] ammo;
    }

    private struct DamageEvent
    {
        public ulong id;
        public float time;
        public PlayerHealth otherPlayer;
        public int amount;
    }

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerShooting = GetComponent<PlayerShooting>();
        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
    }

    public override void OnStartServer()
    {
        nextSampleTime = Time.time;
        nextAllowedRewindTime = 0f;
    }

    [ServerCallback]
    private void Update()
    {
        if (Time.time < nextSampleTime)
            return;

        nextSampleTime = Time.time + sampleInterval;
        CaptureSnapshot();
        PruneHistory();
    }

    [Server]
    private void CaptureSnapshot()
    {
        snapshots.Add(new StateSnapshot
        {
            time = Time.time,
            position = transform.position,
            rotation = transform.rotation,
            health = playerHealth != null ? playerHealth.CurrentHealth : 0,
            ammo = playerShooting != null ? playerShooting.GetAmmoSnapshot() : null
        });
    }

    [Server]
    private void PruneHistory()
    {
        float minTime = Time.time - rewindWindowSeconds - 1f;

        snapshots.RemoveAll(s => s.time < minTime);
        outgoingDamage.RemoveAll(d => d.otherPlayer == null);
        incomingDamage.RemoveAll(d => d.otherPlayer == null);
    }

    [Command]
    public void CmdRequestRewind()
    {
        if (Time.time < nextAllowedRewindTime)
            return;

        TryRewindServer();
    }

    [Server]
    private void TryRewindServer()
    {
        float targetTime = Time.time - rewindWindowSeconds;

        if (!TryGetSnapshot(targetTime, out StateSnapshot snapshot))
            return;

        nextAllowedRewindTime = Time.time + rewindCooldownSeconds;

        RestoreSelfFromSnapshot(snapshot);
        UndoOutgoingDamageAfter(snapshot.time);
        PurgeIncomingDamageAfter(snapshot.time);
    }

    [Server]
    private bool TryGetSnapshot(float targetTime, out StateSnapshot snapshot)
    {
        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            if (snapshots[i].time <= targetTime)
            {
                snapshot = snapshots[i];
                return true;
            }
        }

        if (snapshots.Count > 0)
        {
            snapshot = snapshots[0];
            return true;
        }

        snapshot = default;
        return false;
    }

    [Server]
    private void RestoreSelfFromSnapshot(StateSnapshot snapshot)
    {
        if (characterController != null)
            characterController.enabled = false;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        transform.SetPositionAndRotation(snapshot.position, snapshot.rotation);

        if (playerHealth != null)
            playerHealth.ServerSetHealth(snapshot.health);

        if (playerShooting != null)
            playerShooting.RestoreAmmoSnapshot(snapshot.ammo);

        if (characterController != null)
            characterController.enabled = true;
    }

    [Server]
    public ulong RegisterOutgoingDamage(PlayerHealth target, int amount)
    {
        ulong id = nextDamageEventId++;

        outgoingDamage.Add(new DamageEvent
        {
            id = id,
            time = Time.time,
            otherPlayer = target,
            amount = amount
        });

        return id;
    }

    [Server]
    public void RegisterIncomingDamage(PlayerHealth attacker, int amount, ulong damageEventId)
    {
        incomingDamage.Add(new DamageEvent
        {
            id = damageEventId,
            time = Time.time,
            otherPlayer = attacker,
            amount = amount
        });
    }

    [Server]
    private void UndoOutgoingDamageAfter(float cutoffTime)
    {
        for (int i = outgoingDamage.Count - 1; i >= 0; i--)
        {
            DamageEvent ev = outgoingDamage[i];

            if (ev.time <= cutoffTime)
                continue;

            if (ev.otherPlayer != null)
            {
                ev.otherPlayer.ServerHeal(ev.amount);

                PlayerRewind targetRewind = ev.otherPlayer.GetComponent<PlayerRewind>();
                if (targetRewind != null)
                    targetRewind.RemoveIncomingDamage(ev.id);
            }

            outgoingDamage.RemoveAt(i);
        }
    }

    [Server]
    private void PurgeIncomingDamageAfter(float cutoffTime)
    {
        for (int i = incomingDamage.Count - 1; i >= 0; i--)
        {
            DamageEvent ev = incomingDamage[i];

            if (ev.time <= cutoffTime)
                continue;

            if (ev.otherPlayer != null)
            {
                PlayerRewind attackerRewind = ev.otherPlayer.GetComponent<PlayerRewind>();
                if (attackerRewind != null)
                    attackerRewind.RemoveOutgoingDamage(ev.id);
            }

            incomingDamage.RemoveAt(i);
        }
    }

    [Server]
    public void RemoveOutgoingDamage(ulong damageEventId)
    {
        for (int i = outgoingDamage.Count - 1; i >= 0; i--)
        {
            if (outgoingDamage[i].id == damageEventId)
            {
                outgoingDamage.RemoveAt(i);
                return;
            }
        }
    }

    [Server]
    public void RemoveIncomingDamage(ulong damageEventId)
    {
        for (int i = incomingDamage.Count - 1; i >= 0; i--)
        {
            if (incomingDamage[i].id == damageEventId)
            {
                incomingDamage.RemoveAt(i);
                return;
            }
        }
    }
}