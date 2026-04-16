using UnityEngine;
using UnityEngine.InputSystem;
using Mirror;

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

    private float stepTimer;
    private CharacterController controller;
    private Vector3 velocity;
    private bool isGrounded;
    private float currentSpeed;

    private void OnEnable()
    {
        if (isLocalPlayer && moveAction != null)
            ToggleInputs(true);
    }

    private void OnDisable()
    {
        if (isLocalPlayer && moveAction != null)
            ToggleInputs(false);
    }

    private void ToggleInputs(bool state)
    {
        if (moveAction == null)
            return;

        if (state)
        {
            moveAction.action.Enable();
            sprintAction.action.Enable();
            jumpAction.action.Enable();
            crouchAction.action.Enable();
        }
        else
        {
            moveAction.action.Disable();
            sprintAction.action.Disable();
            jumpAction.action.Disable();
            crouchAction.action.Disable();
        }
    }

    void Start()
    {
        controller = GetComponent<CharacterController>();

        if (!isLocalPlayer)
        {
            if (playerCamera != null)
                playerCamera.enabled = false;

            if (playerListener != null)
                playerListener.enabled = false;

            this.enabled = false;
            return;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (!isLocalPlayer)
            return;

        var rewind = GetComponent<PlayerRewind>();
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
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        if (crouchAction.action.IsPressed())
            currentSpeed = crouchSpeed;
        else if (sprintAction.action.IsPressed())
            currentSpeed = sprintSpeed;
        else
            currentSpeed = walkSpeed;

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;
        controller.Move(move * currentSpeed * Time.deltaTime);
    }

    private void HandleJump()
    {
        if (jumpAction.action.WasPressedThisFrame() && isGrounded)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    private void HandleCrouch()
    {
        if (crouchAction.action.IsPressed())
            controller.height = crouchHeight;
        else
            controller.height = standingHeight;
    }

    private void HandleFootsteps()
    {
        if (!isLocalPlayer)
            return;

        if (!isGrounded)
            return;

        Vector2 input = moveAction.action.ReadValue<Vector2>();
        bool isMoving = input.magnitude > 0.1f;

        if (!isMoving)
        {
            stepTimer = 0;
            return;
        }

        float interval = stepInterval;

        if (sprintAction.action.IsPressed())
            interval *= 0.6f;

        if (crouchAction.action.IsPressed())
            interval *= 1.5f;

        stepTimer -= Time.deltaTime;

        if (stepTimer <= 0f)
        {
            PlayFootstep();
            CmdPlayFootstep();
            stepTimer = interval;
        }
    }

    private void PlayFootstep()
    {
        if (footstepClips.Length == 0 || footstepSource == null)
            return;

        int index = Random.Range(0, footstepClips.Length);
        footstepSource.PlayOneShot(footstepClips[index]);
    }

    [Command]
    void CmdPlayFootstep()
    {
        RpcPlayFootstep();
    }

    [ClientRpc]
    void RpcPlayFootstep()
    {
        if (isLocalPlayer)
            return;

        PlayFootstep();
    }
}