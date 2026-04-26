using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Стрельба, перезарядка, инвентарь патронов.
/// Урон проводится через PlayerHealth.TakeDamage с регистрацией в PlayerRewind.
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerShooting : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject hudPrefab;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private GameObject hitEffectPrefab;

    [Header("Setup")]
    public WeaponData[] loadout;
    public Transform weaponHolder;
    public Recoil recoilScript;

    [Header("Input")]
    public InputActionReference fireAction;

    // События для UI
    public System.Action<WeaponData, int> OnWeaponChangedEvent;
    public System.Action<int, int> OnAmmoChangedEvent;

    [SyncVar(hook = nameof(OnWeaponChanged))]
    private int currentWeaponIndex = -1;

    public readonly SyncList<int> ammoInventory = new SyncList<int>();

    [SyncVar] public bool isReloading;

    private WeaponData currentWeapon;
    private float nextFireTime;
    private PlayerHealth playerHealth;
    private PlayerRewind playerRewind;
    private PlayerHUD hud;
    private Coroutine reloadRoutine;
    private Camera localCamera;

    public WeaponData CurrentWeapon => currentWeapon;
    public int CurrentAmmo => (currentWeaponIndex >= 0 && currentWeaponIndex < ammoInventory.Count) ? ammoInventory[currentWeaponIndex] : 0;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerRewind = GetComponent<PlayerRewind>();

        // Ищем камеру в собственной иерархии, а не через Camera.main
        var fpsInput = GetComponent<FPSInput>();
        if (fpsInput != null && fpsInput.PlayerCamera != null)
            localCamera = fpsInput.PlayerCamera;
        else
            localCamera = GetComponentInChildren<Camera>(true);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        ammoInventory.Clear();
        if (loadout != null)
            foreach (var w in loadout) ammoInventory.Add(w != null ? w.maxAmmo : 0);
        currentWeaponIndex = (loadout != null && loadout.Length > 0) ? 0 : -1;
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        ammoInventory.Callback += OnAmmoInventoryChanged;

        if (fireAction != null) fireAction.action.Enable();

        if (hudPrefab != null)
        {
            GameObject hudObj = Instantiate(hudPrefab);
            hud = hudObj.GetComponentInChildren<PlayerHUD>(true);
            RefreshHud();
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || currentWeapon == null || isReloading) return;

        // Не стреляем во время смерти/перемотки
        if (playerHealth != null && playerHealth.isDead) return;
        if (playerRewind != null && playerRewind.IsRewinding) return;

        bool isFiring = fireAction != null && fireAction.action.IsPressed();

        if (isFiring && Time.time >= nextFireTime)
        {
            if (CurrentAmmo > 0) Shoot();
            else if (fireAction.action.WasPressedThisFrame()) PlayEmptySound();
        }

        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame && CurrentAmmo < currentWeapon.maxAmmo)
            CmdReload();
    }

    private void Shoot()
    {
        nextFireTime = Time.time + currentWeapon.fireRate;
        if (recoilScript) recoilScript.FireRecoil(currentWeapon);
        if (audioSource && currentWeapon.shootSound) audioSource.PlayOneShot(currentWeapon.shootSound, currentWeapon.volume);

        // Камера локального игрока для расчёта raycast'a на сервере
        if (localCamera == null)
            localCamera = GetComponentInChildren<Camera>(true);
        Transform camTransform = localCamera != null ? localCamera.transform : transform;
        CmdShoot(camTransform.position, camTransform.forward);
    }

    [Command]
    void CmdShoot(Vector3 origin, Vector3 direction)
    {
        if (CurrentAmmo <= 0 || isReloading) return;
        if (playerHealth != null && playerHealth.isDead) return;

        // Урон применяем только во время активного раунда
        bool roundActive = GameManager.Instance == null
                           || GameManager.Instance.State == GameManager.MatchState.RoundActive;

        ammoInventory[currentWeaponIndex]--;

        RpcPlayShootSound();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, currentWeapon.range, ~0, QueryTriggerInteraction.Ignore))
        {
            PlayerHealth target = hit.transform.GetComponentInParent<PlayerHealth>();
            if (target != null && target != playerHealth && roundActive)
            {
                // Регистрируем "исходящий" урон в системе перемотки атакующего
                ulong damageEventId = 0UL;
                if (playerRewind != null)
                    damageEventId = playerRewind.RegisterOutgoingDamage(target, currentWeapon.damage);

                target.TakeDamage(currentWeapon.damage, playerHealth, damageEventId);
                RpcShowHitEffect(hit.point, hit.normal, Color.red);
            }
            else
            {
                RpcShowHitEffect(hit.point, hit.normal, Color.white);
            }
        }
    }

    [Command]
    void CmdReload()
    {
        if (isReloading) return;
        if (playerHealth != null && playerHealth.isDead) return;
        reloadRoutine = StartCoroutine(ReloadCoroutine());
    }

    /// <summary>Обрывает текущую перезарядку (вызывается из PlayerRewind / при смерти).</summary>
    [Server]
    public void CancelReload()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }
        isReloading = false;
    }

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(currentWeapon.reloadTime);

        if (currentWeaponIndex >= 0 && currentWeaponIndex < ammoInventory.Count)
            ammoInventory[currentWeaponIndex] = currentWeapon.maxAmmo;

        isReloading = false;
        reloadRoutine = null;
    }

    // Снимок патронов для PlayerRewind
    public int[] GetAmmoSnapshot()
    {
        int[] snapshot = new int[ammoInventory.Count];
        for (int i = 0; i < ammoInventory.Count; i++) snapshot[i] = ammoInventory[i];
        return snapshot;
    }

    [Server]
    public void RestoreAmmoSnapshot(int[] snapshot)
    {
        if (snapshot == null || snapshot.Length != ammoInventory.Count) return;
        for (int i = 0; i < snapshot.Length; i++) ammoInventory[i] = snapshot[i];
    }

    void OnWeaponChanged(int oldIdx, int newIdx)
    {
        if (loadout != null && newIdx >= 0 && newIdx < loadout.Length)
        {
            currentWeapon = loadout[newIdx];
            if (isLocalPlayer) RefreshHud();
        }
    }

    void OnAmmoInventoryChanged(SyncList<int>.Operation op, int index, int oldVal, int newVal)
    {
        if (isLocalPlayer && index == currentWeaponIndex && currentWeapon != null)
        {
            if (hud) hud.UpdateAmmo(newVal, currentWeapon.maxAmmo);
            OnAmmoChangedEvent?.Invoke(newVal, currentWeapon.maxAmmo);
        }
    }

    private void RefreshHud()
    {
        if (hud && currentWeapon)
        {
            hud.UpdateWeapon(currentWeapon, CurrentAmmo);
            OnWeaponChangedEvent?.Invoke(currentWeapon, CurrentAmmo);
        }
    }

    [ClientRpc]
    void RpcPlayShootSound()
    {
        if (!isLocalPlayer && audioSource && currentWeapon != null && currentWeapon.shootSound)
            audioSource.PlayOneShot(currentWeapon.shootSound, currentWeapon.volume);
    }

    void PlayEmptySound()
    {
        if (audioSource && currentWeapon != null && currentWeapon.emptySound)
            audioSource.PlayOneShot(currentWeapon.emptySound, currentWeapon.no_ammo_volume);
    }

    [ClientRpc]
    void RpcShowHitEffect(Vector3 pos, Vector3 normal, Color color)
    {
        if (hitEffectPrefab)
        {
            GameObject effect = Instantiate(hitEffectPrefab, pos, Quaternion.LookRotation(normal));
            Destroy(effect, 2f);
        }
    }
}
