using UnityEngine;


[CreateAssetMenu(fileName = "NewWeapon", menuName = "Shooter/Weapon")]
public class WeaponData : ScriptableObject
{
    public string weaponName;
    public int damage = 20;
    public int maxAmmo = 30;

    public int current_ammo = 30;
    public float fireRate = 0.1f;

    public float reloadTime = 2f;

    public float range = 100f;

    public GameObject visualPrefab;

    public AudioClip shootSound;
    public AudioClip emptySound;

    public Sprite weaponIcon;

    public float no_ammo_volume = 0.2f;
    [Range(0, 1)] public float volume = 0.5f; // Громкость для этой пушки
    
    [Header("Recoil Settings")]
    public float recoilX = -2f;      // Вверх
    public float recoilY = 0.5f;     // Влево-вправо
    public float recoilZ = 0.35f;    // В плечо
    public float snappiness = 6f;    // Скорость рывка
    public float returnSpeed = 2f;   // Скорость возврата
}  