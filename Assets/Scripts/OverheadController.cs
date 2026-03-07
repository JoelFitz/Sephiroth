using UnityEngine;

public class OverheadController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSmoothTime = 0.1f;

    [Header("Camera Settings")]
    public Transform cameraTransform;
    public float cameraHeight = 15f;
    public float cameraAngle = 45f; // Angle for isometric
    public float cameraDistance = 10f; // How far back the camera sits
    public bool useOrthographic = true; // animal crossing
    public float orthographicSize = 8f;

    [Header("Animal Crossing Style Camera")]
    [SerializeField] private bool useSnapRotation = true;
    [SerializeField] private float snapRotationSpeed = 5f;
    [SerializeField] private float snapRotationThreshold = 0.1f;
    private float targetCameraAngle = 0f;
    private bool isRotating = false;

    [Header("Up Arrow Camera Adjustment")]
    [SerializeField] private bool enableUpArrowAdjustment = true;
    [SerializeField] private float upViewCameraHeight = 8f;
    [SerializeField] private float upViewCameraDistance = 5f;
    [SerializeField] private float upViewCameraAngle = 25f;
    [SerializeField] private float upViewTransitionSpeed = 3f;
    [SerializeField] private float upViewOrthographicSize = 12f;

    private float defaultCameraHeight;
    private float defaultCameraDistance;
    private float defaultCameraAngle;
    private float defaultOrthographicSize;
    private bool isUpViewActive = false;

    private float currentCameraAngle = 0f;

    [Header("Camera Collision")]
    public bool enableCameraCollision = true;
    public LayerMask wallLayerMask = -1; // What layers count as walls
    public float cameraCollisionRadius = 0.5f; // Camera collision sphere radius
    public float minCameraDistance = 2f; // Minimum distance camera can be from player
    public float collisionSmoothTime = 0.2f; // How quickly camera adjusts when hitting walls

    [Header("Camera Controls")]
    public bool allowCameraRotation = false;
    public float cameraRotationSpeed = 2f;

    [Header("Camera Smoothing")]
    public float cameraFollowSpeed = 10f; // Adjustable smoothing speed
    public bool useSmoothFollow = true;

    private CharacterController characterController;
    private PlayerMotor playerMotor;
    private Camera cam;
    private float rotationVelocity;
    private Vector3 velocity;
    private bool isGrounded;
    private Vector3 cameraOffset;
    private Vector3 cameraVelocity; // for SmoothDamp

    // Camera collision variables
    private float currentCameraDistance;
    private float targetCameraDistance;
    private float cameraDistanceVelocity;

    // Input handling
    private bool qPressed = false;
    private bool ePressed = false;
    private bool movementEnabled = true;
    private int pendingSnapSteps = 0;

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        playerMotor = GetComponent<PlayerMotor>();

        // Set up camera if not assigned
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main.transform;
        }

        cam = cameraTransform.GetComponent<Camera>();

        // Store default camera values
        defaultCameraHeight = cameraHeight;
        defaultCameraDistance = cameraDistance;
        defaultCameraAngle = cameraAngle;
        defaultOrthographicSize = orthographicSize;

        // Initialize camera distance and angle
        currentCameraDistance = cameraDistance;
        targetCameraDistance = cameraDistance;
        targetCameraAngle = 0f;
        currentCameraAngle = 0f;

        SetupCamera();
        CalculateCameraOffset();
    }

    void SetupCamera()
    {
        if (cam == null) return;

        // Set camera projection
        if (useOrthographic)
        {
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }
        else
        {
            cam.orthographic = false;
            cam.fieldOfView = 60f; // Perspective FOV
        }

        // Position and angle the camera for overhead view
        PositionCamera();
    }

    void CalculateCameraOffset()
    {
        // Use the collision-adjusted distance
        Vector3 direction = Quaternion.Euler(cameraAngle, currentCameraAngle, 0) * Vector3.back;
        cameraOffset = direction * currentCameraDistance + Vector3.up * cameraHeight;
    }

    void PositionCamera()
    {
        if (cameraTransform == null) return;

        // Position camera above and at an angle
        cameraTransform.position = transform.position + cameraOffset;

        // Look at the player with the specified angle
        cameraTransform.LookAt(transform.position + Vector3.up);

        // Adjust the angle for isometric 
        Vector3 euler = cameraTransform.eulerAngles;
        euler.x = cameraAngle;
        cameraTransform.rotation = Quaternion.Euler(euler);
    }

    void Update()
    {
        HandleGroundCheck();
        HandleMovement();
        HandleCameraControls();
        HandleUpArrowAdjustment();
        HandleSnapRotation();
        HandleCameraCollision();
        HandleGravity();

        // UI interaction
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked ?
                CursorLockMode.None : CursorLockMode.Locked;
        }
    }

    void LateUpdate()
    {
        // Follow the player after physics has updated to reduce jitter
        HandleCameraFollow();
    }

    void HandleUpArrowAdjustment()
    {
        if (!enableUpArrowAdjustment) return;

        bool upPressed = Input.GetKey(KeyCode.G);

        if (upPressed && !isUpViewActive)
        {
            // Transition to up view
            isUpViewActive = true;
        }
        else if (!upPressed && isUpViewActive)
        {
            // Transition back to normal view
            isUpViewActive = false;
        }

        // Smoothly interpolate between normal and up view settings
        float targetHeight = isUpViewActive ? upViewCameraHeight : defaultCameraHeight;
        float targetDistance = isUpViewActive ? upViewCameraDistance : defaultCameraDistance;
        float targetAngle = isUpViewActive ? upViewCameraAngle : defaultCameraAngle;
        float targetOrthoSize = isUpViewActive ? upViewOrthographicSize : defaultOrthographicSize;

        // Smooth transitions
        cameraHeight = Mathf.Lerp(cameraHeight, targetHeight, Time.deltaTime * upViewTransitionSpeed);
        cameraDistance = Mathf.Lerp(cameraDistance, targetDistance, Time.deltaTime * upViewTransitionSpeed);
        cameraAngle = Mathf.Lerp(cameraAngle, targetAngle, Time.deltaTime * upViewTransitionSpeed);

        if (useOrthographic && cam != null)
        {
            orthographicSize = Mathf.Lerp(orthographicSize, targetOrthoSize, Time.deltaTime * upViewTransitionSpeed);
            cam.orthographicSize = orthographicSize;
        }

        // Update target camera distance for collision system
        targetCameraDistance = cameraDistance;
    }


    void HandleSnapRotation()
    {
        if (!useSnapRotation) return;

        // Handle Q and E input for 90-degree snaps
        bool qPressedThisFrame = Input.GetKeyDown(KeyCode.Q);
        bool ePressedThisFrame = Input.GetKeyDown(KeyCode.E);

        if (qPressedThisFrame)
        {
            // Queue a 90-degree counter-clockwise step
            if (!isRotating)
            {
                targetCameraAngle -= 90f;
                isRotating = true;
            }
            else
            {
                pendingSnapSteps -= 1;
            }
        }
        else if (ePressedThisFrame)
        {
            // Queue a 90-degree clockwise step
            if (!isRotating)
            {
                targetCameraAngle += 90f;
                isRotating = true;
            }
            else
            {
                pendingSnapSteps += 1;
            }
        }

        // Normalize target angle to 0-360 range
        while (targetCameraAngle < 0) targetCameraAngle += 360f;
        while (targetCameraAngle >= 360f) targetCameraAngle -= 360f;

        // Smooth rotation towards target
        if (isRotating)
        {
            float maxDelta = snapRotationSpeed * 90f * Time.deltaTime; // degrees per second scaled
            currentCameraAngle = Mathf.MoveTowardsAngle(currentCameraAngle, targetCameraAngle, maxDelta);

            // If we've effectively reached the target angle, either consume a queued step or stop.
            if (Mathf.Abs(Mathf.DeltaAngle(currentCameraAngle, targetCameraAngle)) < snapRotationThreshold)
            {
                currentCameraAngle = targetCameraAngle;

                if (pendingSnapSteps != 0)
                {
                    int stepDirection = pendingSnapSteps > 0 ? 1 : -1;
                    targetCameraAngle += 90f * stepDirection;
                    pendingSnapSteps -= stepDirection;
                }
                else
                {
                    isRotating = false;
                }
            }

            // Normalize current angle
            while (currentCameraAngle < 0) currentCameraAngle += 360f;
            while (currentCameraAngle >= 360f) currentCameraAngle -= 360f;

            CalculateCameraOffset();
        }
    }

    void HandleCameraCollision()
    {
        if (!enableCameraCollision || cameraTransform == null)
        {
            targetCameraDistance = cameraDistance;
        }
        else
        {
            CheckCameraCollision();
        }

        // Smoothly adjust camera distance
        currentCameraDistance = Mathf.SmoothDamp(currentCameraDistance, targetCameraDistance,
            ref cameraDistanceVelocity, collisionSmoothTime);

        // Recalculate offset with new distance
        CalculateCameraOffset();
    }

    void CheckCameraCollision()
    {
        Vector3 playerPosition = transform.position;
        // Use the current camera angle for collision direction
        Vector3 desiredCameraDirection = Quaternion.Euler(cameraAngle, currentCameraAngle, 0) * Vector3.back;
        Vector3 desiredCameraPosition = playerPosition + desiredCameraDirection * cameraDistance + Vector3.up * cameraHeight;

        Vector3 rayDirection = (desiredCameraPosition - playerPosition).normalized;
        float maxDistance = Vector3.Distance(playerPosition, desiredCameraPosition);

        RaycastHit hit;
        if (Physics.SphereCast(playerPosition, cameraCollisionRadius, rayDirection, out hit, maxDistance, wallLayerMask))
        {
            float safeDistance = hit.distance - cameraCollisionRadius;
            Vector3 hitPoint = playerPosition + rayDirection * safeDistance;
            Vector3 directionToHit = hitPoint - playerPosition;

            Vector3 horizontalDirection = new Vector3(directionToHit.x, 0, directionToHit.z);
            float horizontalDistance = horizontalDirection.magnitude;

            float adjustedCameraDistance = horizontalDistance / desiredCameraDirection.magnitude;

            targetCameraDistance = Mathf.Max(adjustedCameraDistance, minCameraDistance);
        }
        else
        {
            targetCameraDistance = cameraDistance;
        }
    }

    void HandleGroundCheck()
    {
        if (playerMotor != null)
        {
            // PlayerMotor handles its own grounded logic; OverheadController only needs a flag.
            isGrounded = playerMotor.IsGrounded();
        }
        else if (characterController != null)
        {
            isGrounded = characterController.isGrounded;
        }
    }

    void HandleMovement()
    {
        if (!movementEnabled) return;

        // Get input
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        if (inputDirection.magnitude >= 0.1f)
        {
            // For overhead view, we want movement relative to current camera angle
            Vector3 moveDirection;

            if (useSnapRotation)
            {
                // Move relative to current camera orientation (Animal Crossing style)
                float cameraRad = currentCameraAngle * Mathf.Deg2Rad;
                Vector3 cameraForward = new Vector3(Mathf.Sin(cameraRad), 0, Mathf.Cos(cameraRad));
                Vector3 cameraRight = new Vector3(Mathf.Cos(cameraRad), 0, -Mathf.Sin(cameraRad));

                moveDirection = (cameraForward * inputDirection.z + cameraRight * inputDirection.x);
            }
            else if (allowCameraRotation)
            {
                // Move relative to camera orientation
                Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;

                moveDirection = (cameraForward * inputDirection.z + cameraRight * inputDirection.x);
            }
            else
            {
                // Move relative to world axes (classic style)
                moveDirection = inputDirection;
            }

            // Move character via physics motor if available, otherwise fall back to CharacterController.
            if (playerMotor != null)
            {
                playerMotor.ApplyHorizontalVelocity(moveDirection * moveSpeed);
            }
            else if (characterController != null)
            {
                characterController.Move(moveDirection * moveSpeed * Time.deltaTime);
            }

            // Rotate character to face movement direction
            if (moveDirection != Vector3.zero)
            {
                float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref rotationVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
        }
    }

    void HandleCameraControls()
    {
        // Only handle continuous rotation if not using snap rotation
        if (allowCameraRotation && !useSnapRotation)
        {
            // camera rotation Q and E
            if (Input.GetKey(KeyCode.Q))
            {
                RotateCamera(-cameraRotationSpeed * Time.deltaTime);
            }
            if (Input.GetKey(KeyCode.E))
            {
                RotateCamera(cameraRotationSpeed * Time.deltaTime);
            }
        }

        // Zoom in/out with scroll wheel
        if (useOrthographic && cam != null && !isUpViewActive)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll != 0f)
            {
                defaultOrthographicSize = Mathf.Clamp(defaultOrthographicSize - scroll * 2f, 3f, 15f);
                orthographicSize = defaultOrthographicSize;
                cam.orthographicSize = orthographicSize;
            }
        }
    }

    void RotateCamera(float rotationAmount)
    {
        currentCameraAngle += rotationAmount;
        targetCameraAngle = currentCameraAngle;
        CalculateCameraOffset();
    }

    void HandleCameraFollow()
    {
        if (cameraTransform == null) return;

        // Get the target position (player + offset)
        Vector3 targetPosition = transform.position + cameraOffset;

        if (useSmoothFollow && playerMotor != null)
        {
            // Use SmoothDamp for stable, non-jittery camera following of a physics-driven player
            float smoothTime = 1f / Mathf.Max(cameraFollowSpeed, 0.01f);
            cameraTransform.position = Vector3.SmoothDamp(
                cameraTransform.position,
                targetPosition,
                ref cameraVelocity,
                smoothTime);
        }
        else
        {
            // Direct following (legacy path)
            cameraTransform.position = Vector3.Lerp(
                cameraTransform.position,
                targetPosition,
                Time.deltaTime * 5f);
        }

        // Always look at the player
        cameraTransform.LookAt(transform.position + Vector3.up);
    }

    void HandleGravity()
    {
        // If using physics-based motor, let Rigidbody + Unity gravity handle Y motion.
        if (playerMotor != null)
        {
            return;
        }

        if (characterController == null) return;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += Physics.gravity.y * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    public void SetCameraHeight(float height)
    {
        defaultCameraHeight = height;
        if (!isUpViewActive)
        {
            cameraHeight = height;
            CalculateCameraOffset();
        }
    }

    public void SetCameraAngle(float angle)
    {
        defaultCameraAngle = angle;
        if (!isUpViewActive)
        {
            cameraAngle = angle;
            CalculateCameraOffset();
            PositionCamera();
        }
    }

    public void SetCameraDistance(float distance)
    {
        defaultCameraDistance = distance;
        if (!isUpViewActive)
        {
            cameraDistance = distance;
            targetCameraDistance = distance;
        }
    }

    public void SetOrthographicSize(float size)
    {
        if (cam != null && useOrthographic)
        {
            defaultOrthographicSize = size;
            if (!isUpViewActive)
            {
                orthographicSize = size;
                cam.orthographicSize = orthographicSize;
            }
        }
    }

    // Public methods for external control
    public void SnapRotateLeft()
    {
        if (useSnapRotation && !isRotating)
        {
            targetCameraAngle -= 90f;
            isRotating = true;
        }
    }

    public void SnapRotateRight()
    {
        if (useSnapRotation && !isRotating)
        {
            targetCameraAngle += 90f;
            isRotating = true;
        }
    }

    public void SetUpView(bool enabled)
    {
        isUpViewActive = enabled;
    }

    // Debug visualization
    void OnDrawGizmosSelected()
    {
        if (!enableCameraCollision || cameraTransform == null) return;

        // Draw the collision detection ray
        Vector3 playerPosition = transform.position;
        Vector3 desiredCameraDirection = Quaternion.Euler(cameraAngle, currentCameraAngle, 0) * Vector3.back;
        Vector3 desiredCameraPosition = playerPosition + desiredCameraDirection * cameraDistance + Vector3.up * cameraHeight;

        // Draw ray from player to desired camera position
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(playerPosition, desiredCameraPosition);

        // Draw collision sphere at camera position
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(cameraTransform.position, cameraCollisionRadius);

        // Draw min distance sphere
        Gizmos.color = Color.green;
        Vector3 minDistancePos = playerPosition + desiredCameraDirection * minCameraDistance + Vector3.up * cameraHeight;
        Gizmos.DrawWireSphere(minDistancePos, cameraCollisionRadius);

        // Draw camera angle indicators
        Gizmos.color = Color.blue;
        Vector3 forward = Quaternion.Euler(0, currentCameraAngle, 0) * Vector3.forward;
        Gizmos.DrawRay(transform.position, forward * 3f);

        // Draw target angle if rotating
        if (isRotating)
        {
            Gizmos.color = Color.cyan;
            Vector3 targetForward = Quaternion.Euler(0, targetCameraAngle, 0) * Vector3.forward;
            Gizmos.DrawRay(transform.position, targetForward * 3f);
        }
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        Debug.Log($"OverheadController movement enabled: {enabled}");

        if (playerMotor != null)
        {
            playerMotor.SetMovementEnabled(enabled);
        }
    }

    public bool IsMovementEnabled()
    {
        return movementEnabled;
    }
}

