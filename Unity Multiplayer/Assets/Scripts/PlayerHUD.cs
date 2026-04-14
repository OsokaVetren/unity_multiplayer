using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerHUD : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI ammoText;

    [Header("Health")]
    [SerializeField] private Image healthBarFill;

    public void UpdateWeapon(WeaponData data, int ammo)
    {
        weaponIcon.sprite = data.weaponIcon;
        weaponIcon.enabled = data.weaponIcon != null;
        weaponNameText.text = data.weaponName;

        UpdateAmmo(ammo, data.maxAmmo);
    }

    public void UpdateAmmo(int current, int max)
    {
        ammoText.text = $"{current} / {max}";
    }

    public void UpdateHealth(int current, int max)
    {
        healthBarFill.fillAmount = 1f - (float)current / max;
    }
}