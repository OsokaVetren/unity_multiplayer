using Mirror;
using UnityEngine;

public class PlayerUIController : MonoBehaviour
{
    private PlayerHUD hud;
    private PlayerHealth health;
    private PlayerShooting shooting;

    public void Init(PlayerHUD hudRef, PlayerHealth hp, PlayerShooting shoot)
    {
        hud = hudRef;
        health = hp;
        shooting = shoot;

        // Подписки
        health.OnHealthChangedEvent += OnHealthChanged;
        shooting.OnWeaponChangedEvent += OnWeaponChanged;
        shooting.OnAmmoChangedEvent += OnAmmoChanged;

        // Первичная инициализация
        OnHealthChanged(health.CurrentHealth, health.MaxHealth);
        OnWeaponChanged(shooting.CurrentWeapon, shooting.CurrentAmmo);
    }

    private void OnHealthChanged(int current, int max)
    {
        hud.UpdateHealth(current, max);
    }

    private void OnWeaponChanged(WeaponData data, int ammo)
    {
        hud.UpdateWeapon(data, ammo);
    }

    private void OnAmmoChanged(int ammo, int max)
    {
        hud.UpdateAmmo(ammo, max);
    }
}