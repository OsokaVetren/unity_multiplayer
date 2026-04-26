using UnityEngine;
using Mirror;

namespace InfimaGames.LowPolyShooterPack
{
    public class CameraLook : NetworkBehaviour
    {
        [SerializeField] private Vector2 sensitivity = new Vector2(1.5f, 1.5f);
        [SerializeField] private Vector2 yClamp = new Vector2(-80, 80);

        private CharacterBehaviour playerCharacter;
        private Quaternion rotationCharacter;
        private Quaternion rotationCamera;

        private void Start()
        {
            playerCharacter = GetComponentInParent<CharacterBehaviour>();
            rotationCharacter = transform.root.localRotation;
            rotationCamera = transform.localRotation;
        }

        private void LateUpdate()
        {
            if (!isLocalPlayer) return;

            Vector2 frameInput = playerCharacter.IsCursorLocked() ? playerCharacter.GetInputLook() : default;
            frameInput *= sensitivity;

            rotationCamera *= Quaternion.Euler(-frameInput.y, 0, 0);
            rotationCharacter *= Quaternion.Euler(0, frameInput.x, 0);

            rotationCamera = Clamp(rotationCamera);

            transform.localRotation = rotationCamera; // Наклон головы (синхронится через NetTransform)
            transform.root.rotation = rotationCharacter; // Поворот тела
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