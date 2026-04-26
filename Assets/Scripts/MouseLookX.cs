using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class MouseLookX : NetworkBehaviour
{
    public float lookSensitivity = 0.5f;
    public InputActionReference lookAction;

    private float rotationY = 0f;

    private void OnEnable()
    {
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
        if (!isLocalPlayer)
        {
            this.enabled = false;
            return;
        }
    }

    void Update()
    {
        if (!isLocalPlayer)
            return;

        var rewind = GetComponent<PlayerRewind>();
        if (rewind != null && rewind.IsRewinding)
            return;

        Vector2 input = lookAction.action.ReadValue<Vector2>();
        rotationY += input.x * lookSensitivity;
        transform.localRotation = Quaternion.Euler(0, rotationY, 0);
    }
}