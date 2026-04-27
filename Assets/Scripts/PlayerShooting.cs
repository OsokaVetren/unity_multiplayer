using System.Collections;
using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Стрельба, перезарядка, инвентарь патронов.
/// Урон проводится через PlayerHealth.TakeDamage с регистрацией в PlayerRewind.
/// 
/// ИСПРАВЛЕНО:
/// - Вспышка и hit effect видны всем клиентам
/// - Смена оружия (визуальная модель + AnimatorController) синхронизируется для всех клиентов
/// - Анимация перезарядки корректно играет для текущего оружия на всех клиентах
/// - Урон по игроку работает + Debug.Log вывод
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

    [SyncVar(hook = nameof(OnWeaponIndexChanged))]
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

        // IMPORTANT: SyncVar hooks don't fire on the server during initial assignment.
        // We must set currentWeapon manually on the server so CmdShoot can use it.
        if (loadout != null && currentWeaponIndex >= 0 && currentWeaponIndex < loadout.Length)
            currentWeapon = loadout[currentWeaponIndex];
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

    public override void OnStartClient()
    {
        base.OnStartClient();

        // FIX: На всех клиентах при подключении синхронизируем визуальное оружие
        // SyncVar hook может не вызваться если значение было установлено до подключения
        if (currentWeaponIndex >= 0 && loadout != null && currentWeaponIndex < loadout.Length)
        {
            currentWeapon = loadout[currentWeaponIndex];
            // Обновляем визуальную модель оружия для всех клиентов
            SyncVisualWeapon(currentWeaponIndex);
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

        // Смена оружия клавишами 1, 2, 3...
        if (Keyboard.current != null && loadout != null)
        {
            for (int i = 0; i < loadout.Length && i < 9; i++)
            {
                Key key = (Key)((int)Key.Digit1 + i);
                if (Keyboard.current[key].wasPressedThisFrame && i != currentWeaponIndex)
                {
                    CmdSwitchWeapon(i);
                    break;
                }
            }
        }
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
        if (currentWeapon == null)
        {
            Debug.LogWarning($"[PlayerShooting] CmdShoot: currentWeapon is null for {gameObject.name}");
            return;
        }
        if (CurrentAmmo <= 0 || isReloading) return;
        if (playerHealth != null && playerHealth.isDead) return;

        // Урон применяем только во время активного раунда
        bool roundActive = GameManager.Instance == null
                           || GameManager.Instance.State == GameManager.MatchState.RoundActive;

        ammoInventory[currentWeaponIndex]--;

        // FIX: Передаём индекс оружия для корректной вспышки
        RpcPlayShootEffects(currentWeaponIndex);

        // Temporarily disable own colliders so we don't hit ourselves
        Collider[] ownColliders = GetComponentsInChildren<Collider>();
        foreach (var col in ownColliders) col.enabled = false;

        bool didHit = Physics.Raycast(origin, direction, out RaycastHit hit, currentWeapon.range, ~0, QueryTriggerInteraction.Ignore);

        // Re-enable own colliders
        foreach (var col in ownColliders) col.enabled = true;

        if (didHit)
        {
            PlayerHealth target = hit.transform.GetComponentInParent<PlayerHealth>();
            if (target != null && target != playerHealth && roundActive)
            {
                // FIX: Добавлены Debug.Log для урона
                Debug.Log($"<color=orange>[DAMAGE] {gameObject.name} нанёс {currentWeapon.damage} урона игроку {target.gameObject.name} (HP: {target.health} -> {target.health - currentWeapon.damage})</color>");

                // Регистрируем "исходящий" урон в системе перемотки атакующего
                ulong damageEventId = 0UL;
                if (playerRewind != null)
                    damageEventId = playerRewind.RegisterOutgoingDamage(target, currentWeapon.damage);

                target.TakeDamage(currentWeapon.damage, playerHealth, damageEventId);
                RpcShowHitEffect(hit.point, hit.normal, true);
            }
            else
            {
                RpcShowHitEffect(hit.point, hit.normal, false);
            }
        }
    }

    // ========== WEAPON SWITCH ==========

    /// <summary>
    /// Публичный метод для вызова из Character.OnTryInventoryNext().
    /// Character вызывает это при смене оружия колесиком мыши.
    /// </summary>
    public void CmdSwitchWeaponFromCharacter(int newIndex)
    {
        CmdSwitchWeapon(newIndex);
    }

    /// <summary>
    /// Команда смены оружия от клиента.
    /// </summary>
    [Command]
    void CmdSwitchWeapon(int newIndex)
    {
        if (newIndex < 0 || loadout == null || newIndex >= loadout.Length) return;
        if (newIndex == currentWeaponIndex) return;
        if (isReloading) CancelReload();

        Debug.Log($"[PlayerShooting] {gameObject.name} переключает оружие: {currentWeaponIndex} -> {newIndex}");

        // Обновляем SyncVar — hook OnWeaponIndexChanged вызовется на всех клиентах
        currentWeaponIndex = newIndex;

        // На сервере тоже обновляем currentWeapon напрямую (hook не вызывается на сервере для своей SyncVar)
        if (loadout != null && newIndex >= 0 && newIndex < loadout.Length)
            currentWeapon = loadout[newIndex];
    }

    // ========== RELOAD ==========

    [Command]
    void CmdReload()
    {
        if (isReloading) return;
        if (playerHealth != null && playerHealth.isDead) return;
        if (currentWeapon == null) return;

        Debug.Log($"[PlayerShooting] {gameObject.name} начинает перезарядку оружия '{currentWeapon.weaponName}'");

        reloadRoutine = StartCoroutine(ReloadCoroutine());

        // FIX: Отправляем RPC для проигрывания анимации перезарядки на всех клиентах
        RpcPlayReloadAnimation(currentWeaponIndex);
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

    // ========== SYNC HOOKS ==========

    /// <summary>
    /// SyncVar hook: вызывается на всех клиентах при смене currentWeaponIndex.
    /// Обновляет currentWeapon (для звуков/логики) и визуальную модель в Inventory.
    /// </summary>
    void OnWeaponIndexChanged(int oldIdx, int newIdx)
    {
        if (loadout != null && newIdx >= 0 && newIdx < loadout.Length)
        {
            currentWeapon = loadout[newIdx];
            if (isLocalPlayer) RefreshHud();
        }

        // FIX: Обновляем визуальное оружие (модель + AnimatorController) на ВСЕХ клиентах
        SyncVisualWeapon(newIdx);
    }

    /// <summary>
    /// Переключает визуальное оружие в Inventory (активирует нужный GameObject,
    /// обновляет AnimatorController) — вызывается на клиентах.
    /// </summary>
    private void SyncVisualWeapon(int weaponIndex)
    {
        var character = GetComponentInChildren<InfimaGames.LowPolyShooterPack.CharacterBehaviour>(true);
        if (character == null) return;

        var inventory = character.GetInventory();
        if (inventory == null) return;

        // Переключаем визуальное оружие в инвентаре (активирует нужный GameObject)
        int currentEquippedIdx = inventory.GetEquippedIndex();
        if (currentEquippedIdx != weaponIndex)
        {
            var equippedWeapon = inventory.Equip(weaponIndex);

            // Обновляем AnimatorController для корректных анимаций
            if (equippedWeapon != null)
            {
                var characterAnimator = GetComponentInChildren<Animator>(true);
                if (characterAnimator != null)
                {
                    var animController = equippedWeapon.GetAnimatorController();
                    if (animController != null)
                    {
                        characterAnimator.runtimeAnimatorController = animController;
                        Debug.Log($"[PlayerShooting] {gameObject.name}: визуальное оружие обновлено на индекс {weaponIndex}, AnimatorController: {animController.name}");
                    }
                }
            }
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

    // ========== RPCs ==========

    /// <summary>
    /// Проигрывает эффекты стрельбы на всех клиентах:
    /// - Звук выстрела для удалённых клиентов
    /// - Вспышка (muzzle flash) через Weapon.Fire() для удалённых клиентов
    /// - Анимация стрельбы
    /// </summary>
    [ClientRpc]
    void RpcPlayShootEffects(int weaponIdx)
    {
        // Звук выстрела для удалённых клиентов
        if (!isLocalPlayer && audioSource && currentWeapon != null && currentWeapon.shootSound)
            audioSource.PlayOneShot(currentWeapon.shootSound, currentWeapon.volume);

        // FIX: Для удалённых игроков вызываем Fire() на оружии для визуальных эффектов
        // (muzzle flash, weapon animation, casing ejection).
        // Локальный игрок уже делает это через Character.Fire(), поэтому пропускаем.
        if (!isLocalPlayer)
        {
            var character = GetComponentInChildren<InfimaGames.LowPolyShooterPack.CharacterBehaviour>(true);
            if (character != null)
            {
                var inventory = character.GetInventory();
                if (inventory != null)
                {
                    // FIX: Убедимся что у удалённого клиента экипировано правильное оружие
                    int equippedIdx = inventory.GetEquippedIndex();
                    if (equippedIdx != weaponIdx)
                    {
                        // Переключаем визуальное оружие перед стрельбой
                        SyncVisualWeapon(weaponIdx);
                    }

                    var weapon = inventory.GetEquipped();
                    if (weapon != null)
                    {
                        // Вызываем Fire() — это запускает muzzle flash, weapon animation, casing ejection
                        weapon.Fire();
                        Debug.Log($"[PlayerShooting] RPC: Стрельба другого игрока {gameObject.name} — вспышка проиграна");
                    }
                    else
                    {
                        Debug.LogWarning($"[PlayerShooting] RPC: weapon == null для {gameObject.name}, weaponIdx={weaponIdx}");
                    }
                }
            }

            // Запускаем анимацию стрельбы на body animator
            var charAnimator = GetComponentInChildren<Animator>(true);
            if (charAnimator != null)
            {
                int overlayIdx = charAnimator.GetLayerIndex("Layer Overlay");
                if (overlayIdx >= 0)
                    charAnimator.CrossFade("Fire", 0.05f, overlayIdx, 0);
            }
        }
    }

    /// <summary>
    /// Проигрывает анимацию перезарядки на всех клиентах.
    /// FIX: Ранее отсутствовал — перезарядка не была видна другим игрокам.
    /// </summary>
    [ClientRpc]
    void RpcPlayReloadAnimation(int weaponIdx)
    {
        // FIX: Для всех клиентов (включая удалённых) проигрываем анимацию перезарядки
        // Для локального игрока анимация уже может играть через Character, но мы делаем на всякий случай
        if (!isLocalPlayer)
        {
            var character = GetComponentInChildren<InfimaGames.LowPolyShooterPack.CharacterBehaviour>(true);
            if (character != null)
            {
                var inventory = character.GetInventory();
                if (inventory != null)
                {
                    // Убедимся что визуальное оружие правильное
                    int equippedIdx = inventory.GetEquippedIndex();
                    if (equippedIdx != weaponIdx)
                        SyncVisualWeapon(weaponIdx);

                    var weapon = inventory.GetEquipped();
                    if (weapon != null)
                    {
                        // Вызываем анимацию перезарядки на оружии
                        weapon.Reload();
                    }
                }
            }

            // Запускаем анимацию перезарядки на body animator
            var charAnimator = GetComponentInChildren<Animator>(true);
            if (charAnimator != null)
            {
                int actionsLayer = charAnimator.GetLayerIndex("Layer Actions");
                if (actionsLayer >= 0)
                {
                    // Определяем какую анимацию перезарядки играть
                    string stateName = "Reload";  // По умолчанию обычная перезарядка
                    charAnimator.Play(stateName, actionsLayer, 0.0f);
                    Debug.Log($"[PlayerShooting] RPC: Анимация перезарядки другого игрока {gameObject.name}");
                }
            }
        }
    }

    void PlayEmptySound()
    {
        if (audioSource && currentWeapon != null && currentWeapon.emptySound)
            audioSource.PlayOneShot(currentWeapon.emptySound, currentWeapon.no_ammo_volume);
    }

    [ClientRpc]
    void RpcShowHitEffect(Vector3 pos, Vector3 normal, bool isPlayerHit)
    {
        if (hitEffectPrefab)
        {
            GameObject effect = Instantiate(hitEffectPrefab, pos, Quaternion.LookRotation(normal));
            Destroy(effect, 2f);
        }
    }
}
