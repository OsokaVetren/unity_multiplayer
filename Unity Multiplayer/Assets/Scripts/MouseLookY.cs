using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class MouseLookY : MonoBehaviour // Здесь можно оставить MonoBehaviour
{
    public float lookSensitivity = 1.0f;
    public InputActionReference lookAction;
    private float xRotation = 0f;
    public float minLookAngle = -50f;
    public float maxLookAngle = 50f;

    private NetworkIdentity playerNetIdentity;

    void Start()
    {
        // Ищем NetworkIdentity на родителе
        playerNetIdentity = GetComponentInParent<NetworkIdentity>();

        // Если это камера ЧУЖОГО игрока — выключаем скрипт управления
        if (playerNetIdentity != null && !playerNetIdentity.isLocalPlayer)
        {
            this.enabled = false;
            return;
        }

        // Включаем ввод (только для себя)
        if (lookAction != null) lookAction.action.Enable();
    }

    void Update()
    {
        // Если мы каким-то образом не нашли Identity или это не наш игрок — стоп
        if (playerNetIdentity == null || !playerNetIdentity.isLocalPlayer) return;

        Vector2 input = lookAction.action.ReadValue<Vector2>();

        // Инвертируем Y (обычно в FPS так)
        xRotation += -input.y * lookSensitivity;
        xRotation = Mathf.Clamp(xRotation, minLookAngle, maxLookAngle);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}