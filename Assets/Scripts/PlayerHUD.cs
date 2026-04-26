using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("Weapon")]
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private TextMeshProUGUI ammoText;

    [Header("Health")]
    [SerializeField] private Image healthBarFill;
    [SerializeField] private TextMeshProUGUI healthText;

    [Header("Match")]
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI stateText;

    public void UpdateWeapon(WeaponData data, int ammo)
    {
        if (weaponIcon != null)
        {
            weaponIcon.sprite = data != null ? data.weaponIcon : null;
            weaponIcon.enabled = data != null && data.weaponIcon != null;
        }

        if (weaponNameText != null)
            weaponNameText.text = data != null ? data.weaponName : string.Empty;

        UpdateAmmo(ammo, data != null ? data.maxAmmo : 0);
    }

    public void UpdateAmmo(int current, int max)
    {
        if (ammoText != null)
            ammoText.text = $"{current} / {max}";
    }

    public void UpdateHealth(int current, int max)
    {
        if (healthBarFill != null && max > 0)
            healthBarFill.fillAmount = (float)current / max;

        if (healthText != null)
            healthText.text = $"{current} / {max}";
    }

    public void UpdateScore(int kills, int deaths)
    {
        if (scoreText != null)
            scoreText.text = $"K {kills}  D {deaths}";
    }

    public void UpdateMatchInfo(int round, string state, float timeRemaining)
    {
        if (roundText != null)
            roundText.text = $"Round {round}";

        if (timerText != null)
            timerText.text = FormatTime(timeRemaining);

        if (stateText != null)
            stateText.text = state ?? string.Empty;
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        int total = Mathf.CeilToInt(seconds);
        int minutes = total / 60;
        int rest = total % 60;
        return $"{minutes:00}:{rest:00}";
    }
}