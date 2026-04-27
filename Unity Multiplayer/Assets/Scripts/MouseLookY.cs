using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

// Изменяем MonoBehaviour на NetworkBehaviour
public class MouseLookY : NetworkBehaviour
{
    public float lookSensitivity = 1.0f;
    public InputActionReference lookAction;

    [Header("Settings")]
    public float minLookAngle = -50f;
    public float maxLookAngle = 50f;

    // SyncVar заставляет сервер рассылать это значение всем клиентам
    // Мы используем hook, чтобы визуально обновлять поворот при получении значения
    [SyncVar(hook = nameof(OnRotationChanged))]
    private float xRotation = 0f;

    private void OnEnable()
    {
        // Включаем ввод только для локального игрока
        if (isLocalPlayer && lookAction != null)
            lookAction.action.Enable();
    }

    void Update()
    {
        // Только локальный игрок обрабатывает ввод и отправляет данные на сервер
        if (!isLocalPlayer)
            return;

        var rewind = GetComponentInParent<PlayerRewind>();
        if (rewind != null && rewind.IsRewinding)
            return;

        Vector2 input = lookAction.action.ReadValue<Vector2>();
        
        // Вычисляем локально
        float newRotation = xRotation + (-input.y * lookSensitivity);
        newRotation = Mathf.Clamp(newRotation, minLookAngle, maxLookAngle);

        // Отправляем на сервер (Command)
        CmdUpdateRotation(newRotation);
        
        // Применяем локально сразу для плавности (Client-side prediction)
        ApplyRotation(newRotation);
    }

    [Command]
    void CmdUpdateRotation(float newRot)
    {
        // Сервер принимает значение и благодаря SyncVar оно уйдет всем остальным
        xRotation = newRot;
    }

    // Эта функция вызывается у всех клиентов, когда SyncVar xRotation меняется на сервере
    void OnRotationChanged(float oldRot, float newRot)
    {
        // Локальному игроку не нужно обновлять из SyncVar, он уже обновил в Update
        if (!isLocalPlayer)
        {
            ApplyRotation(newRot);
        }
    }

    void ApplyRotation(float rot)
    {
        transform.localRotation = Quaternion.Euler(rot, 0f, 0f);
    }
}