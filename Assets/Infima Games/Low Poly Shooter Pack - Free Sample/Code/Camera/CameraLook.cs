using UnityEngine;
using Mirror;

namespace InfimaGames.LowPolyShooterPack
{
    /// <summary>
    /// Camera look controller.
    /// Modified: uses NetworkIdentity from parent for proper ownership check.
    /// Also disables the camera component on non-local players to avoid
    /// multiple active cameras.
    /// </summary>
    public class CameraLook : MonoBehaviour
    {
        [SerializeField] private Vector2 sensitivity = new Vector2(1.5f, 1.5f);
        [SerializeField] private Vector2 yClamp = new Vector2(-80, 80);

        private CharacterBehaviour playerCharacter;
        private NetworkIdentity netIdentity;
        private Quaternion rotationCharacter;
        private Quaternion rotationCamera;
        private bool isLocal;

        private void Start()
        {
            netIdentity = GetComponentInParent<NetworkIdentity>();
            isLocal = (netIdentity != null) ? netIdentity.isLocalPlayer : true;

            playerCharacter = GetComponentInParent<CharacterBehaviour>();
            rotationCharacter = transform.root.localRotation;
            rotationCamera = transform.localRotation;

            // Disable camera & audio listener for non-local players
            if (!isLocal)
            {
                Camera cam = GetComponent<Camera>();
                if (cam != null) cam.enabled = false;

                AudioListener listener = GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (!isLocal) return;
            if (playerCharacter == null) return;

            Vector2 frameInput = playerCharacter.IsCursorLocked() ? playerCharacter.GetInputLook() : default;
            frameInput *= sensitivity;

            rotationCamera *= Quaternion.Euler(-frameInput.y, 0, 0);
            rotationCharacter *= Quaternion.Euler(0, frameInput.x, 0);

            rotationCamera = Clamp(rotationCamera);

            transform.localRotation = rotationCamera;
            transform.root.rotation = rotationCharacter;
        }

        private Quaternion Clamp(Quaternion q)
        {
            q.x /= q.w; q.y /= q.w; q.z /= q.w; q.w = 1.0f;
            float pitch = Mathf.Clamp(2.0f * Mathf.Rad2Deg * Mathf.Atan(q.x), yClamp.x, yClamp.y);
            q.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * pitch);
            return q;
        }
    }
}
