using System;
using System.Collections;
using Mirror;
using UnityEngine;

public class PlayerShooting : NetworkBehaviour
{
    [SerializeField] private GameObject hudPrefab;
    private PlayerHUD hud;

    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Setup")]
    public WeaponData[] loadout;
    public Transform weaponHolder;

    [Header("State")]
    [SyncVar(hook = nameof(OnWeaponChanged))]
    private int currentWeaponIndex = -1;

    public readonly SyncList<int> ammoInventory = new SyncList<int>();

    private WeaponData currentWeapon;
    private float nextFireTime;
    private Coroutine reloadRoutine;

    public Recoil recoilScript;

    [SyncVar]
    public bool isReloading = false;

    [SerializeField] private GameObject hitEffectPrefab;

    public WeaponData CurrentWeapon => currentWeapon;

    private ParticleSystem currentMuzzleFlash;

    [SerializeField] private WeaponRecoil weaponRecoil;

    public int CurrentAmmo
    {
        get
        {
            if (currentWeaponIndex < 0 || currentWeaponIndex >= ammoInventory.Count)
                return 0;

            return ammoInventory[currentWeaponIndex];
        }
    }

    public event Action<WeaponData, int> OnWeaponChangedEvent;
    public event Action<int, int> OnAmmoChangedEvent;

    private bool isInitialized = false;

    public override void OnStartServer()
    {
        if (loadout == null || loadout.Length == 0)
            return;

        ammoInventory.Clear();

        for (int i = 0; i < loadout.Length; i++)
            ammoInventory.Add(loadout[i].maxAmmo);

        currentWeaponIndex = 0;
        currentWeapon = loadout[0];
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        StartCoroutine(DelayedInit());
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        if (hudPrefab != null)
        {
            GameObject hudObj = Instantiate(hudPrefab);
            hud = hudObj.GetComponentInChildren<PlayerHUD>(true);

            PlayerUIController uiController = gameObject.AddComponent<PlayerUIController>();
            uiController.Init(hud, GetComponent<PlayerHealth>(), this);
        }

        ammoInventory.Callback += OnAmmoInventoryChanged;
        InitializeState();
    }

    private void OnDestroy()
    {
        if (isLocalPlayer)
            ammoInventory.Callback -= OnAmmoInventoryChanged;
    }

    private IEnumerator DelayedInit()
    {
        yield return null;
        InitializeState();
    }

    public void InitializeState()
    {
        if (isInitialized)
            return;

        if (currentWeaponIndex < 0)
            return;

        if (loadout == null || loadout.Length == 0)
            return;

        if (ammoInventory.Count == 0)
            return;

        if (currentWeaponIndex >= loadout.Length || currentWeaponIndex >= ammoInventory.Count)
            return;

        isInitialized = true;
        currentWeapon = loadout[currentWeaponIndex];

        int ammo = ammoInventory[currentWeaponIndex];
        OnWeaponChangedEvent?.Invoke(currentWeapon, ammo);
        OnAmmoChangedEvent?.Invoke(ammo, currentWeapon.maxAmmo);
    }

    private void UpdateWeaponVisuals(int index)
    {
        if (loadout == null || index < 0 || index >= loadout.Length)
            return;

        currentWeapon = loadout[index];

        if (weaponHolder != null)
        {
            for (int i = weaponHolder.childCount - 1; i >= 0; i--)
                Destroy(weaponHolder.GetChild(i).gameObject);

            if (currentWeapon.visualPrefab != null){
                GameObject weaponObj = Instantiate(currentWeapon.visualPrefab, weaponHolder);
                currentMuzzleFlash = weaponObj.GetComponentInChildren<ParticleSystem>();
            }
        }

        if (isLocalPlayer && index < ammoInventory.Count)
        {
            int ammo = ammoInventory[index];
            OnWeaponChangedEvent?.Invoke(currentWeapon, ammo);
            OnAmmoChangedEvent?.Invoke(ammo, currentWeapon.maxAmmo);
        }
    }

    private void OnAmmoInventoryChanged(SyncList<int>.Operation op, int index, int oldItem, int newItem)
    {
        if (!isLocalPlayer)
            return;

        if (index == currentWeaponIndex && currentWeaponIndex >= 0 && currentWeaponIndex < loadout.Length)
        {
            OnAmmoChangedEvent?.Invoke(newItem, loadout[index].maxAmmo);
        }
    }

    private void Update()
    {
        if (!isLocalPlayer || currentWeapon == null)
            return;

        var rewind = GetComponent<PlayerRewind>();
        if (rewind != null && rewind.IsRewinding)
            return;

        if (isReloading)
            return;

        if (Input.GetButton("Fire1") && Time.time >= nextFireTime)
        {
            if (CurrentAmmo > 0)
                Shoot();
            else if (Input.GetButtonDown("Fire1"))
                PlayEmptySound();
        }

        if (Input.GetKeyDown(KeyCode.R) && CurrentAmmo < currentWeapon.maxAmmo)
            CmdReload();

        if (Input.GetKeyDown(KeyCode.Alpha1))
            CmdChangeWeapon(0);

        if (Input.GetKeyDown(KeyCode.Alpha2))
            CmdChangeWeapon(1);
    }

    private void Shoot()
    {
        nextFireTime = Time.time + currentWeapon.fireRate;

        if (recoilScript != null){
            recoilScript.FireRecoil(currentWeapon);
            weaponRecoil.Fire();
        }
        
        if (audioSource != null && currentWeapon.shootSound != null)
            audioSource.PlayOneShot(currentWeapon.shootSound, currentWeapon.volume);

        Camera cam = Camera.main;
        if (cam == null)
            return;
        CmdShoot(cam.transform.position, cam.transform.forward);
    }

    [ClientRpc]
    private void RpcPlayMuzzleFlash()
    {
        currentMuzzleFlash.Play();
    }

    [Command]
    private void CmdShoot(Vector3 origin, Vector3 direction)
    {
        if (currentWeaponIndex < 0 || currentWeaponIndex >= ammoInventory.Count)
            return;

        if (ammoInventory[currentWeaponIndex] <= 0 || isReloading)
            return;

        ammoInventory[currentWeaponIndex]--;
        currentMuzzleFlash.Play();
        RpcPlayShootSound();
        RpcPlayMuzzleFlash();

        if (Physics.Raycast(origin, direction, out RaycastHit hit, currentWeapon.range))
        {
            PlayerHealth targetHealth = hit.transform.GetComponentInParent<PlayerHealth>();

            if (targetHealth != null)
            {
                PlayerHealth attackerHealth = GetComponent<PlayerHealth>();
                PlayerRewind rewind = GetComponent<PlayerRewind>();

                ulong damageEventId = 0;
                if (rewind != null)
                    damageEventId = rewind.RegisterOutgoingDamage(targetHealth, currentWeapon.damage);

                targetHealth.TakeDamage(currentWeapon.damage, attackerHealth, damageEventId);
                RpcShowHitEffect(hit.point, hit.normal, Color.red);
            }
            else
            {
                Color surfaceColor = Color.gray;

                Renderer renderer = hit.collider.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                    surfaceColor = renderer.material.color;

                RpcShowHitEffect(hit.point, hit.normal, surfaceColor);
            }
        }
    }

    [Command]
    private void CmdReload()
    {
        if (isReloading)
            return;

        if (currentWeaponIndex < 0 || currentWeaponIndex >= loadout.Length)
            return;

        if (reloadRoutine != null)
            StopCoroutine(reloadRoutine);

        reloadRoutine = StartCoroutine(ReloadCoroutine());
    }

    private IEnumerator ReloadCoroutine()
    {
        isReloading = true;
        yield return new WaitForSeconds(loadout[currentWeaponIndex].reloadTime);
        ammoInventory[currentWeaponIndex] = loadout[currentWeaponIndex].maxAmmo;
        isReloading = false;
        reloadRoutine = null;
    }

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

    [Command]
    private void CmdChangeWeapon(int index)
    {
        if (loadout == null || index < 0 || index >= loadout.Length)
            return;

        currentWeaponIndex = index;
        currentWeapon = loadout[index];
    }

    private void OnWeaponChanged(int oldIndex, int newIndex)
    {
        UpdateWeaponVisuals(newIndex);
    }

    [ClientRpc]
    private void RpcPlayShootSound()
    {
        if (isLocalPlayer)
            return;

        if (audioSource != null && currentWeapon != null && currentWeapon.shootSound != null)
            audioSource.PlayOneShot(currentWeapon.shootSound, currentWeapon.volume);
    }

    private void PlayEmptySound()
    {
        if (audioSource != null && currentWeapon.emptySound != null)
        {
            audioSource.pitch = 1.0f;
            audioSource.PlayOneShot(currentWeapon.emptySound, currentWeapon.volume);
        }
    }

    [ClientRpc]
    private void RpcShowHitEffect(Vector3 pos, Vector3 normal, Color effectColor)
    {
        if (hitEffectPrefab == null)
            return;

        GameObject effect = Instantiate(hitEffectPrefab, pos, Quaternion.LookRotation(normal));
        ParticleSystem ps = effect.GetComponent<ParticleSystem>();

        if (ps != null)
        {
            var main = ps.main;
            main.startColor = effectColor;
        }

        Destroy(effect, 2f);
    }

    public int[] GetAmmoSnapshot()
    {
        int[] snapshot = new int[ammoInventory.Count];
        for (int i = 0; i < ammoInventory.Count; i++)
            snapshot[i] = ammoInventory[i];

        return snapshot;
    }

    [Server]
    public void RestoreAmmoSnapshot(int[] snapshot)
    {
        ammoInventory.Clear();

        if (snapshot == null)
            return;

        for (int i = 0; i < snapshot.Length; i++)
            ammoInventory.Add(snapshot[i]);
    }
}