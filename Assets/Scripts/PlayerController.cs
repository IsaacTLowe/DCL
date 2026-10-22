using UnityEngine;
using UnityEngine.InputSystem;

// Basic first-person movement using the new Input System.
// WASD to move, mouse to look, Space to jump, Left Shift to sprint.
// Requires a CharacterController on the same GameObject.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform cameraTransform; // child camera at head height

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float jumpHeight = 1.2f;
    [SerializeField] private float gravity = -9.81f;

    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 0.1f;
    [SerializeField] private float maxLookAngle = 85f;

    private CharacterController controller;
    private Vector3 velocity;
    private float pitch;        // up/down camera rotation

    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction sprintAction;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;

        moveAction = new InputAction("Move", InputActionType.Value);
        moveAction.AddCompositeBinding("2DVector")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        jumpAction = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
    }

    private void OnEnable()
    {
        moveAction.Enable();
        lookAction.Enable();
        jumpAction.Enable();
        sprintAction.Enable();
    }

    private void OnDisable()
    {
        moveAction.Disable();
        lookAction.Disable();
        jumpAction.Disable();
        sprintAction.Disable();
    }

    private void OnDestroy()
    {
        moveAction.Dispose();
        lookAction.Dispose();
        jumpAction.Dispose();
        sprintAction.Dispose();
    }

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        HandleLook();
        HandleMovement();
    }

    private void HandleLook()
    {
        Vector2 look = lookAction.ReadValue<Vector2>() * mouseSensitivity;

        transform.Rotate(Vector3.up * look.x);

        pitch -= look.y;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        cameraTransform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void HandleMovement()
    {
        bool grounded = controller.isGrounded;

        if (grounded && velocity.y < 0f)
            velocity.y = -2f;


        Vector2 input = moveAction.ReadValue<Vector2>();
        Vector3 move = transform.right * input.x + transform.forward * input.y;

        float speed = sprintAction.IsPressed() ? sprintSpeed : walkSpeed;
        controller.Move(move * speed * Time.deltaTime);

        if (grounded && jumpAction.WasPressedThisFrame())
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}