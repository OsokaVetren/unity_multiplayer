using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

public class MouseLookY : MonoBehaviour
{
    public float lookSensitivity = 1.0f;
    public InputActionReference lookAction;

    private float xRotation = 0f;
    public float minLookAngle = -50f;
    public float maxLookAngle = 50f;

    private NetworkIdentity playerNetIdentity;

    void Start()
    {
        playerNetIdentity = GetComponentInParent<NetworkIdentity>();

        if (playerNetIdentity != null && !playerNetIdentity.isLocalPlayer)
        {
            this.enabled = false;
            return;
        }

        if (lookAction != null)
            lookAction.action.Enable();
    }

    void Update()
    {
        if (playerNetIdentity == null || !playerNetIdentity.isLocalPlayer)
            return;

        var rewind = GetComponentInParent<PlayerRewind>();
        if (rewind != null && rewind.IsRewinding)
            return;

        Vector2 input = lookAction.action.ReadValue<Vector2>();
        xRotation += -input.y * lookSensitivity;
        xRotation = Mathf.Clamp(xRotation, minLookAngle, maxLookAngle);
        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
    }
}