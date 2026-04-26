using UnityEngine;
using Mirror;

namespace InfimaGames.LowPolyShooterPack
{
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public class Movement : NetworkBehaviour
    {
        [Header("Speeds")]
        [SerializeField] private float speedWalking = 5.0f;
        [SerializeField] private float speedRunning = 9.0f;
        [SerializeField] private float jumpForce = 5.0f;
        [SerializeField] private LayerMask groundLayer = 1 << 0;

        private Rigidbody rigidBody;
        private CapsuleCollider capsule;
        private CharacterBehaviour playerCharacter;
        private bool grounded;

        private void Awake()
        {
            rigidBody = GetComponent<Rigidbody>();
            capsule = GetComponent<CapsuleCollider>();
            playerCharacter = GetComponent<CharacterBehaviour>();

            // Настройка физики
            rigidBody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void Update()
        {
            // Управляем только своим персонажем
            if (!isLocalPlayer) return;

            CheckGround();
            Move();
        }

        private void CheckGround()
        {
            Vector3 rayStart = transform.TransformPoint(capsule.center);
            float rayDistance = capsule.height * 0.5f + 0.1f;
            grounded = Physics.Raycast(rayStart, Vector3.down, rayDistance, groundLayer);
        }

        private void Move()
        {
            Vector2 input = playerCharacter.GetInputMovement();
            Vector3 direction = transform.TransformDirection(new Vector3(input.x, 0, input.y));
            float speed = playerCharacter.IsRunning() ? speedRunning : speedWalking;

            float yVel = rigidBody.linearVelocity.y;
            if (Input.GetKeyDown(KeyCode.Space) && grounded) yVel = jumpForce;

            rigidBody.linearVelocity = new Vector3(direction.x * speed, yVel, direction.z * speed);
        }
    }
}