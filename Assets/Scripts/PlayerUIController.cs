using Mirror;
using UnityEngine;

public class PlayerUIController : MonoBehaviour
{
    private PlayerHUD hud;
    private PlayerHealth health;
    private PlayerShooting shooting;

    /// <summary>
    /// Инициализация контроллера интерфейса. 
    /// Вызывается обычно из скрипта игрока при старте локального игрока.
    /// </summary>
    public void Init(PlayerHUD hudRef, PlayerHealth hp, PlayerShooting shoot)
    {
        hud = hudRef;
        health = hp;
        shooting = shoot;

        // Подписки на события
        // ВАЖНО: В классе PlayerShooting должны быть публичные события OnWeaponChangedEvent и OnAmmoChangedEvent
        if (health != null)
        {
            health.OnHealthChangedEvent += OnHealthChanged;
        }

        if (shooting != null)
        {
            shooting.OnWeaponChangedEvent += OnWeaponChanged;
            shooting.OnAmmoChangedEvent += OnAmmoChanged;

            // Первичная инициализация данных при подключении
            OnHealthChanged(health.CurrentHealth, health.MaxHealth);
            OnWeaponChanged(shooting.CurrentWeapon, shooting.CurrentAmmo);
        }
    }

    private void OnHealthChanged(int current, int max)
    {
        if (hud != null)
        {
            hud.UpdateHealth(current, max);
        }
    }

    private void OnWeaponChanged(WeaponData data, int ammo)
    {
        if (hud != null)
        {
            hud.UpdateWeapon(data, ammo);
        }
    }

    private void OnAmmoChanged(int ammo, int max)
    {
        if (hud != null)
        {
            hud.UpdateAmmo(ammo, max);
        }
    }

    private void OnDestroy()
    {
        // Очистка подписок при уничтожении объекта для предотвращения утечек памяти
        if (health != null)
        {
            health.OnHealthChangedEvent -= OnHealthChanged;
        }

        if (shooting != null)
        {
            shooting.OnWeaponChangedEvent -= OnWeaponChanged;
            shooting.OnAmmoChangedEvent -= OnAmmoChanged;
        }
    }
}