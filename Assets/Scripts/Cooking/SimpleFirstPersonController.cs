using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class SimpleFirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float walkSpeed = 6.5f;
    [SerializeField] private float sprintSpeed = 9.5f;
    [SerializeField] private float gravity = 20f;
    [SerializeField] private float groundedStickForce = 2f;
    [SerializeField] private bool keepFixedHeight = true;
    [SerializeField] private float verticalMoveSpeed = 5.4f;

    [Header("Look")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float mouseSensitivity = 2.8f;
    [SerializeField] private float minPitch = -75f;
    [SerializeField] private float maxPitch = 80f;
    [SerializeField] private bool lockCursorOnStart = true;

    private CharacterController characterController;
    private float verticalVelocity;
    private float pitch;
    private float fixedHeight;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>(true);
        }
    }

    private void Start()
    {
        if (playerCamera != null)
        {
            pitch = playerCamera.transform.localEulerAngles.x;
            if (pitch > 180f)
            {
                pitch -= 360f;
            }
        }

        if (lockCursorOnStart)
        {
            LockCursor(true);
        }

        fixedHeight = transform.position.y;
    }

    private void Update()
    {
        if (ForestStoryIntroOverlay.IsBlockingInput)
        {
            LockCursor(false);
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            LockCursor(false);
        }
        else if (Input.GetMouseButtonDown(0))
        {
            LockCursor(true);
        }

        UpdateLook();
        UpdateMovement();
    }

    public void SetCamera(Camera targetCamera)
    {
        playerCamera = targetCamera;
    }

    public void SnapToHeight(float targetHeight)
    {
        fixedHeight = targetHeight;
        var position = transform.position;
        position.y = targetHeight;
        transform.position = position;
        verticalVelocity = 0f;
    }

    private void UpdateLook()
    {
        if (playerCamera == null || Cursor.lockState != CursorLockMode.Locked)
        {
            return;
        }

        var mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        var mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        transform.Rotate(0f, mouseX, 0f);

        pitch = Mathf.Clamp(pitch - mouseY, minPitch, maxPitch);
        playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private void UpdateMovement()
    {
        var input = new Vector3(
            Input.GetAxisRaw("Horizontal"),
            0f,
            Input.GetAxisRaw("Vertical"));
        input = Vector3.ClampMagnitude(input, 1f);

        var speed = Input.GetKey(KeyCode.LeftShift) ? sprintSpeed : walkSpeed;
        var move = transform.TransformDirection(input) * speed;

        if (keepFixedHeight)
        {
            var verticalInput = 0f;
            if (Input.GetKey(KeyCode.Q))
            {
                verticalInput += 1f;
            }

            if (Input.GetKey(KeyCode.E))
            {
                verticalInput -= 1f;
            }

            fixedHeight += verticalInput * verticalMoveSpeed * Time.deltaTime;
            verticalVelocity = 0f;
            move.y = 0f;
            characterController.Move(move * Time.deltaTime);

            var position = transform.position;
            position.y = fixedHeight;
            transform.position = position;
            return;
        }

        if (characterController.isGrounded)
        {
            verticalVelocity = -groundedStickForce;
        }
        else
        {
            verticalVelocity -= gravity * Time.deltaTime;
        }

        move.y = verticalVelocity;
        characterController.Move(move * Time.deltaTime);
    }

    private void LockCursor(bool shouldLock)
    {
        Cursor.lockState = shouldLock ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !shouldLock;
    }
}
