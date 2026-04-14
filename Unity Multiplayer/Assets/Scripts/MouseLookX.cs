using UnityEngine;
using UnityEngine.InputSystem;
using Mirror; // Добавляем Mirror

public class MouseLookX : NetworkBehaviour // Наследуемся от NetworkBehaviour
{
    public float lookSensitivity = 0.5f;
    public InputActionReference lookAction;

    private float rotationY = 0f;

    private void OnEnable() 
    {
        // Включаем ввод только если это наш игрок
        if (isLocalPlayer && lookAction != null)
            lookAction.action.Enable();
    }

    private void OnDisable() 
    {
        if (isLocalPlayer && lookAction != null)
            lookAction.action.Disable();
    }

    void Start()
    {
        // Если это чужой игрок — выключаем скрипт, чтобы он не ел ресурсы
        if (!isLocalPlayer)
        {
            this.enabled = false;
            return;
        }
    }

    void Update()
    {
        // Проверка на всякий случай
        if (!isLocalPlayer) return;

        Vector2 input = lookAction.action.ReadValue<Vector2>();
        rotationY += input.x * lookSensitivity;

        transform.localRotation = Quaternion.Euler(0, rotationY, 0);
    }
}