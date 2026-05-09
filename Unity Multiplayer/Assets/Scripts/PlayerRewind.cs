using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public class PlayerRewind : NetworkBehaviour
{
    [Header("Rewind")]
    [SerializeField] private float rewindWindowSeconds = 5f;
    [SerializeField] private float snapshotInterval = 0.10f;
    [SerializeField] private float rewindCooldownSeconds = 8f;
    [SerializeField] private float rewindPlaybackDuration = 0.45f;

    private PlayerHealth playerHealth;
    private PlayerShooting playerShooting;
    private CharacterController characterController;
    private Rigidbody rb;

    private readonly List<StateSnapshot> snapshots = new();
    private readonly List<DamageEvent> outgoingDamage = new();
    private readonly List<DamageEvent> incomingDamage = new();
    private readonly List<Behaviour> localControls = new();

    private float nextSampleTime;
    private float nextAllowedRewindTime;
    private ulong nextDamageEventId = 1;
    private bool isRewinding;

    public bool IsRewinding => isRewinding;

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

        CacheLocalControls();
    }

    private void CacheLocalControls()
    {
        AddControl(GetComponent<PlayerRewindInput>());
        AddControl(GetComponent<PlayerShooting>());
        AddControl(GetComponent<FPSInput>() ?? GetComponentInChildren<FPSInput>(true));
        AddControl(GetComponent<MouseLookX>() ?? GetComponentInChildren<MouseLookX>(true));
        AddControl(GetComponent<MouseLookY>() ?? GetComponentInChildren<MouseLookY>(true));
    }

    private void AddControl(Behaviour behaviour)
    {
        if (behaviour != null && !localControls.Contains(behaviour))
            localControls.Add(behaviour);
    }

    public override void OnStartServer()
    {
        nextSampleTime = Time.time;
        nextAllowedRewindTime = 0f;
        isRewinding = false;
    }

    [ServerCallback]
    private void Update()
    {
        if (isRewinding)
            return;

        if (Time.time < nextSampleTime)
            return;

        nextSampleTime = Time.time + snapshotInterval;
        CaptureSnapshot();
        PruneHistory();
    }

    [Server]
    private void CaptureSnapshot()
    {
        snapshots.Add(CaptureCurrentSnapshot());
    }

    private StateSnapshot CaptureCurrentSnapshot()
    {
        return new StateSnapshot
        {
            time = Time.time,
            position = transform.position,
            rotation = transform.rotation,
            health = playerHealth != null ? playerHealth.CurrentHealth : 0,
            ammo = playerShooting != null ? playerShooting.GetAmmoSnapshot() : null
        };
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
        if (isRewinding)
            return;

        if (Time.time < nextAllowedRewindTime)
            return;

        StartCoroutine(RewindRoutine());
    }

    private IEnumerator RewindRoutine()
    {
        isRewinding = true;
        nextAllowedRewindTime = Time.time + rewindCooldownSeconds;

        if (playerShooting != null)
            playerShooting.CancelReload();

        TargetSetLocalControls(connectionToClient, false);

        if (characterController != null)
            characterController.enabled = false;

        ZeroMotion();

        yield return null;

        float targetTime = Time.time - rewindWindowSeconds;
        int targetIndex = FindSnapshotIndexAtOrBefore(targetTime);

        if (targetIndex < 0 || snapshots.Count == 0)
        {
            FinishRewind();
            yield break;
        }

        StateSnapshot current = CaptureCurrentSnapshot();

        var path = new List<StateSnapshot>(snapshots.Count - targetIndex + 1);
        path.Add(current);

        for (int i = snapshots.Count - 1; i >= targetIndex; i--)
            path.Add(snapshots[i]);

        float segmentDuration = Mathf.Max(0.02f, rewindPlaybackDuration / Mathf.Max(1, path.Count - 1));

        for (int i = 0; i < path.Count - 1; i++)
        {
            StateSnapshot from = path[i];
            StateSnapshot to = path[i + 1];

            float elapsed = 0f;
            while (elapsed < segmentDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / segmentDuration);

                ApplyVisualPose(
                    Vector3.Lerp(from.position, to.position, t),
                    Quaternion.Slerp(from.rotation, to.rotation, t)
                );

                yield return null;
            }

            ApplyVisualPose(to.position, to.rotation);
        }

        StateSnapshot finalState = path[path.Count - 1];
        ApplyState(finalState);

        UndoOutgoingDamageAfter(finalState.time);
        PurgeIncomingDamageAfter(finalState.time);

        ZeroMotion();
        FinishRewind();
    }

    private void FinishRewind()
    {
        if (characterController != null)
            characterController.enabled = true;

        TargetSetLocalControls(connectionToClient, true);
        isRewinding = false;
    }

    private int FindSnapshotIndexAtOrBefore(float targetTime)
    {
        for (int i = snapshots.Count - 1; i >= 0; i--)
        {
            if (snapshots[i].time <= targetTime)
                return i;
        }

        return snapshots.Count > 0 ? 0 : -1;
    }

    private void ApplyVisualPose(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
    }

    [Server]
    private void ApplyState(StateSnapshot snapshot)
    {
        ApplyVisualPose(snapshot.position, snapshot.rotation);

        if (playerHealth != null)
            playerHealth.ServerSetHealth(snapshot.health);

        if (playerShooting != null)
            playerShooting.RestoreAmmoSnapshot(snapshot.ammo);
    }

    private void ZeroMotion()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    [TargetRpc]
    private void TargetSetLocalControls(NetworkConnection target, bool enabledState)
    {
        for (int i = 0; i < localControls.Count; i++)
        {
            Behaviour behaviour = localControls[i];
            if (behaviour != null)
                behaviour.enabled = enabledState;
        }
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

                PlayerRewind otherRewind = ev.otherPlayer.GetComponent<PlayerRewind>();
                if (otherRewind != null)
                    otherRewind.RemoveIncomingDamage(ev.id);
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

    [Server]
    public void ResetRewindSystem()
    {
        // 1. Останавливаем текущую корутину перемотки, если она шла
        StopAllCoroutines();
        
        // 2. Сбрасываем флаги состояния
        isRewinding = false;
        
        // 3. ОЧИЩАЕМ ИСТОРИЮ
        // Это важно: чтобы игрок не мог перемотаться "сквозь смерть" назад во времени
        snapshots.Clear();
        outgoingDamage.Clear();
        incomingDamage.Clear();

        // 4. Включаем управление обратно (на случай, если оно было выключено перемоткой)
        if (characterController != null)
            characterController.enabled = true;

        if (connectionToClient != null)
            TargetSetLocalControls(connectionToClient, true);

        // 5. Разрешаем новую перемотку (можно сбросить кулдаун или оставить как есть)
        // nextAllowedRewindTime = Time.time; 
    }
}