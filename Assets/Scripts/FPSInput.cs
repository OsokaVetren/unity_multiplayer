using Mirror;
using UnityEngine;
using UnityEngine.InputSystem;

public class FPSInput : NetworkBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public AudioListener playerListener;

    [Header("Input Actions")]
    public InputActionReference moveAction;
    public InputActionReference sprintAction;
    public InputActionReference jumpAction;
    public InputActionReference crouchAction;

    [Header("Movement Settings")]
    public float walkSpeed = 5f;
    public float sprintSpeed = 10f;
    public float jumpHeight = 2f;
    public float gravity = -19.62f;

    [Header("Crouch Settings")]
    public float standingHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchSpeed = 2.5f;

    [Header("Footsteps")]
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioClip[] footstepClips;
    [SerializeField] private float stepInterval = 0.5f;

    private CharacterController controller;
    private Vector3 velocity;
    private float stepTimer;
    private float currentSpeed;
    private bool isGrounded;

    public Camera PlayerCamera => playerCamera;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        EnsureVisualReferences();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        EnsureVisualReferences();

        if (!isLocalPlayer)
            SetVisualsEnabled(false);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        EnsureVisualReferences();
        SetVisualsEnabled(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EnableInputActions(true);
    }

    public override void OnStopLocalPlayer()
    {
        base.OnStopLocalPlayer();
        EnableInputActions(false);
    }

    private void Start()
    {
        EnsureVisualReferences();

        if (!isLocalPlayer)
        {
            SetVisualsEnabled(false);
            return;
        }

        SetVisualsEnabled(true);
        EnableInputActions(true);
    }

    private void OnDestroy()
    {
        if (isLocalPlayer)
            EnableInputActions(false);
    }

    private void EnsureVisualReferences()
    {
        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (playerListener == null)
            playerListener = GetComponentInChildren<AudioListener>(true);
    }

    private void SetVisualsEnabled(bool state)
    {
        if (playerCamera != null)
            playerCamera.enabled = state;

        if (playerListener != null)
            playerListener.enabled = state;
    }

    private void EnableInputActions(bool state)
    {
        if (moveAction != null)
        {
            if (state) moveAction.action.Enable(); else moveAction.action.Disable();
        }
        if (sprintAction != null)
        {
            if (state) sprintAction.action.Enable(); else sprintAction.action.Disable();
        }
        if (jumpAction != null)
        {
            if (state) jumpAction.action.Enable(); else jumpAction.action.Disable();
        }
        if (crouchAction != null)
        {
            if (state) crouchAction.action.Enable(); else crouchAction.action.Disable();
        }
    }

    private void Update()
    {
        if (!isLocalPlayer)
            return;

        PlayerRewind rewind = GetComponent<PlayerRewind>();
        if (rewind != null && rewind.IsRewinding)
            return;

        HandleMovement();
        HandleJump();
        HandleCrouch();
        HandleFootsteps();
    }

    public void Launch(float force)
    {
        velocity.y = force;
    }

    private void HandleMovement()
    {
        if (controller == null || moveAction == null || sprintAction == null || crouchAction == null)
            return;

        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0f)
            velocity.y = -2f;

        currentSpeed = crouchAction.action.IsPressed()
            ? crouchSpeed
            : sprintAction.action.IsPressed()
                ? sprintSpeed
                : walkSpeed;

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        controller.Move(move * currentSpeed * Time.deltaTime);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (controller == null || jumpAction == null)
            return;

        if (jumpAction.action.WasPressedThisFrame() && isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void HandleCrouch()
    {
        if (controller == null || crouchAction == null)
            return;

        controller.height = crouchAction.action.IsPressed() ? crouchHeight : standingHeight;
    }

    private void HandleFootsteps()
    {
        if (footstepSource == null || footstepClips == null || footstepClips.Length == 0 || moveAction == null)
            return;

        if (!isGrounded)
        {
            stepTimer = 0f;
            return;
        }

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        if (input.magnitude <= 0.1f)
        {
            stepTimer = 0f;
            return;
        }

        float interval = stepInterval;

        if (sprintAction != null && sprintAction.action.IsPressed())
            interval *= 0.6f;

        if (crouchAction != null && crouchAction.action.IsPressed())
            interval *= 1.5f;

        stepTimer -= Time.deltaTime;
        if (stepTimer > 0f)
            return;

        PlayFootstep();
        CmdPlayFootstep();
        stepTimer = interval;
    }

    private void PlayFootstep()
    {
        if (footstepClips == null || footstepClips.Length == 0 || footstepSource == null)
            return;

        int index = Random.Range(0, footstepClips.Length);
        footstepSource.PlayOneShot(footstepClips[index]);
    }

    [Command]
    private void CmdPlayFootstep()
    {
        RpcPlayFootstep();
    }

    [ClientRpc]
    private void RpcPlayFootstep()
    {
        if (isLocalPlayer)
            return;

        PlayFootstep();
    }
}