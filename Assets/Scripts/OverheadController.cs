using UnityEngine;

public class OverheadController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float rotationSmoothTime = 0.1f;

    [Header("Sprint")]
    [Tooltip("Hold this key to sprint.")]
    public KeyCode sprintKey = KeyCode.LeftShift;

    [Header("Jump")]
    [Tooltip("Press this key to jump.")]
    public KeyCode jumpKey = KeyCode.J;

    [Tooltip("Approximate jump height used to derive the vertical launch speed.")]
    public float jumpHeight = 1.5f;

    [Header("Camera Settings")]
    public Transform cameraTransform;
    public float cameraHeight = 15f;
    public float cameraAngle = 45f;
    public float cameraDistance = 10f;
    public bool useOrthographic = true;
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
    public LayerMask wallLayerMask = -1;
    public float cameraCollisionRadius = 0.5f;
    public float minCameraDistance = 2f;
    public float collisionSmoothTime = 0.2f;

    [Header("Camera Controls")]
    public bool allowCameraRotation = false;
    public float cameraRotationSpeed = 2f;

    [Header("Camera Smoothing")]
    public float cameraFollowSpeed = 10f;
    public bool useSmoothFollow = true;

    private CharacterController characterController;
    private PlayerMotor playerMotor;
    private Camera cam;
    private float rotationVelocity;
    private Vector3 velocity;
    private bool isGrounded;
    private Vector3 cameraOffset;
    private Vector3 cameraVelocity;
    private FrogAnimationDriver frogAnimationDriver;

    private float currentCameraDistance;
    private float targetCameraDistance;
    private float cameraDistanceVelocity;

    private bool movementEnabled = true;
    private int pendingSnapSteps = 0;

    // ─────────────────────────────────────────────────────────────────────────

    void Start()
    {
        characterController = GetComponent<CharacterController>();
        playerMotor = GetComponent<PlayerMotor>();

        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;

        cam = cameraTransform.GetComponent<Camera>();

        defaultCameraHeight = cameraHeight;
        defaultCameraDistance = cameraDistance;
        defaultCameraAngle = cameraAngle;
        defaultOrthographicSize = orthographicSize;

        currentCameraDistance = cameraDistance;
        targetCameraDistance = cameraDistance;
        targetCameraAngle = 0f;
        currentCameraAngle = 0f;

        SetupCamera();

        EnsureFrogAnimationDriver();

        // Initial offset bake — camera not yet following, so direct call is fine here.
        CalculateCameraOffset();
    }

    void SetupCamera()
    {
        if (cam == null) return;

        if (useOrthographic)
        {
            cam.orthographic = true;
            cam.orthographicSize = orthographicSize;
        }
        else
        {
            cam.orthographic = false;
            cam.fieldOfView = 60f;
        }

        PositionCamera();
    }

    // Only called from Start/SetupCamera (once) and from SetCameraAngle (editor-time).
    // Never called mid-frame from Update — use LateUpdate path for that.
    void CalculateCameraOffset()
    {
        Vector3 direction = Quaternion.Euler(cameraAngle, currentCameraAngle, 0) * Vector3.back;
        cameraOffset = direction * currentCameraDistance + Vector3.up * cameraHeight;
    }

    // Only used at Start to snap camera into position immediately.
    void PositionCamera()
    {
        if (cameraTransform == null) return;

        cameraTransform.position = transform.position + cameraOffset;
        cameraTransform.LookAt(transform.position + Vector3.up);

        Vector3 euler = cameraTransform.eulerAngles;
        euler.x = cameraAngle;
        cameraTransform.rotation = Quaternion.Euler(euler);
    }

    // ── Update: game logic & input only — no camera transform writes ─────────

    void Update()
    {
        HandleGroundCheck();
        HandleMovement();
        HandleJump();
        HandleCameraControls();
        HandleUpArrowAdjustment();   // updates cameraHeight/Angle/Distance targets only
        HandleSnapRotation();        // updates currentCameraAngle only
        UpdateCameraCollisionTarget(); // updates targetCameraDistance only
        HandleGravity();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = Cursor.lockState == CursorLockMode.Locked
                ? CursorLockMode.None
                : CursorLockMode.Locked;
        }
    }

    // ── LateUpdate: ALL camera transform writes happen here, once per frame ──
    // Runs after Rigidbody interpolation has settled — eliminates jitter.

    void LateUpdate()
    {
        // 1. Smooth the collision-adjusted distance towards its target.
        currentCameraDistance = Mathf.SmoothDamp(
            currentCameraDistance, targetCameraDistance,
            ref cameraDistanceVelocity, collisionSmoothTime);

        // 2. Bake offset once with fully settled angle + distance values.
        CalculateCameraOffset();

        // 3. Move and orient the camera.
        HandleCameraFollow();
    }

    // ─────────────────────────────────────────────────────────────────────────

    // Updates cameraHeight, cameraDistance, cameraAngle scalars only.
    // Does NOT call CalculateCameraOffset — that happens in LateUpdate.
    void HandleUpArrowAdjustment()
    {
        if (!enableUpArrowAdjustment) return;

        bool upPressed = Input.GetKey(KeyCode.G);

        if (upPressed && !isUpViewActive)
            isUpViewActive = true;
        else if (!upPressed && isUpViewActive)
            isUpViewActive = false;

        float targetHeight = isUpViewActive ? upViewCameraHeight : defaultCameraHeight;
        float targetDistance = isUpViewActive ? upViewCameraDistance : defaultCameraDistance;
        float targetAngle = isUpViewActive ? upViewCameraAngle : defaultCameraAngle;
        float targetOrthoSize = isUpViewActive ? upViewOrthographicSize : defaultOrthographicSize;

        cameraHeight = Mathf.Lerp(cameraHeight, targetHeight, Time.deltaTime * upViewTransitionSpeed);
        cameraDistance = Mathf.Lerp(cameraDistance, targetDistance, Time.deltaTime * upViewTransitionSpeed);
        cameraAngle = Mathf.Lerp(cameraAngle, targetAngle, Time.deltaTime * upViewTransitionSpeed);

        if (useOrthographic && cam != null)
        {
            orthographicSize = Mathf.Lerp(orthographicSize, targetOrthoSize, Time.deltaTime * upViewTransitionSpeed);
            cam.orthographicSize = orthographicSize;
        }

        // Keep the collision target in sync with the (possibly lerping) cameraDistance.
        // The actual currentCameraDistance is smoothed in LateUpdate.
        targetCameraDistance = cameraDistance;
    }

    // Updates currentCameraAngle only.
    // Does NOT call CalculateCameraOffset — that happens in LateUpdate.
    void HandleSnapRotation()
    {
        if (!useSnapRotation) return;

        bool qPressedThisFrame = Input.GetKeyDown(KeyCode.Q);
        bool ePressedThisFrame = Input.GetKeyDown(KeyCode.E);

        if (qPressedThisFrame)
        {
            if (!isRotating) { targetCameraAngle -= 90f; isRotating = true; }
            else pendingSnapSteps -= 1;
        }
        else if (ePressedThisFrame)
        {
            if (!isRotating) { targetCameraAngle += 90f; isRotating = true; }
            else pendingSnapSteps += 1;
        }

        while (targetCameraAngle < 0) targetCameraAngle += 360f;
        while (targetCameraAngle >= 360f) targetCameraAngle -= 360f;

        if (isRotating)
        {
            float maxDelta = snapRotationSpeed * 90f * Time.deltaTime;
            currentCameraAngle = Mathf.MoveTowardsAngle(currentCameraAngle, targetCameraAngle, maxDelta);

            if (Mathf.Abs(Mathf.DeltaAngle(currentCameraAngle, targetCameraAngle)) < snapRotationThreshold)
            {
                currentCameraAngle = targetCameraAngle;

                if (pendingSnapSteps != 0)
                {
                    int stepDir = pendingSnapSteps > 0 ? 1 : -1;
                    targetCameraAngle += 90f * stepDir;
                    pendingSnapSteps -= stepDir;
                }
                else
                {
                    isRotating = false;
                }
            }

            while (currentCameraAngle < 0) currentCameraAngle += 360f;
            while (currentCameraAngle >= 360f) currentCameraAngle -= 360f;

            // NOTE: no CalculateCameraOffset() here — LateUpdate handles it.
        }
    }

    // Renamed from HandleCameraCollision.
    // Only updates targetCameraDistance — no smoothing, no offset bake, no transform writes.
    void UpdateCameraCollisionTarget()
    {
        if (!enableCameraCollision || cameraTransform == null)
            targetCameraDistance = cameraDistance;
        else
            CheckCameraCollision();

        // Smoothing and offset bake have been moved to LateUpdate.
    }

    void CheckCameraCollision()
    {
        Vector3 playerPosition = transform.position;
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
            Vector3 horizontalDir = new Vector3(directionToHit.x, 0, directionToHit.z);
            float horizontalDist = horizontalDir.magnitude;
            float adjustedDistance = horizontalDist / desiredCameraDirection.magnitude;

            targetCameraDistance = Mathf.Max(adjustedDistance, minCameraDistance);
        }
        else
        {
            targetCameraDistance = cameraDistance;
        }
    }

    void HandleGroundCheck()
    {
        if (playerMotor != null)
            isGrounded = playerMotor.IsGrounded();
        else if (characterController != null)
            isGrounded = characterController.isGrounded;
    }

    void HandleMovement()
    {
        if (!movementEnabled) return;

        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        Vector3 inputDirection = new Vector3(horizontal, 0f, vertical).normalized;

        bool sprintHeld = Input.GetKey(sprintKey);
        if (playerMotor != null)
            playerMotor.SetSprinting(sprintHeld && inputDirection.magnitude >= 0.1f);

        if (inputDirection.magnitude >= 0.1f)
        {
            Vector3 moveDirection;

            if (useSnapRotation)
            {
                float cameraRad = currentCameraAngle * Mathf.Deg2Rad;
                Vector3 cameraForward = new Vector3(Mathf.Sin(cameraRad), 0, Mathf.Cos(cameraRad));
                Vector3 cameraRight = new Vector3(Mathf.Cos(cameraRad), 0, -Mathf.Sin(cameraRad));
                moveDirection = cameraForward * inputDirection.z + cameraRight * inputDirection.x;
            }
            else if (allowCameraRotation)
            {
                Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
                Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
                moveDirection = cameraForward * inputDirection.z + cameraRight * inputDirection.x;
            }
            else
            {
                moveDirection = inputDirection;
            }

            if (playerMotor != null)
            {
                playerMotor.ApplyHorizontalVelocity(moveDirection * moveSpeed);
                playerMotor.SetFacingDirection(moveDirection);
            }
            else if (characterController != null)
                characterController.Move(moveDirection * moveSpeed * Time.deltaTime);

            if (moveDirection != Vector3.zero)
            {
                if (playerMotor == null)
                {
                    float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
                    float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle,
                                                         ref rotationVelocity, rotationSmoothTime);
                    transform.rotation = Quaternion.Euler(0f, angle, 0f);
                }
            }
        }
        else
        {
            if (playerMotor != null)
            {
                playerMotor.SetSprinting(false);
                playerMotor.SetFacingDirection(Vector3.zero);
            }
        }
    }

    void HandleJump()
    {
        if (!movementEnabled)
            return;

        if (playerMotor != null)
            return;

        if (!Input.GetKeyDown(jumpKey) || !isGrounded)
            return;

        velocity.y = Mathf.Sqrt(jumpHeight * -2f * Physics.gravity.y);
    }

    void HandleCameraControls()
    {
        if (allowCameraRotation && !useSnapRotation)
        {
            if (Input.GetKey(KeyCode.Q)) RotateCamera(-cameraRotationSpeed * Time.deltaTime);
            if (Input.GetKey(KeyCode.E)) RotateCamera(cameraRotationSpeed * Time.deltaTime);
        }

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

    // Updates currentCameraAngle only — no CalculateCameraOffset call.
    void RotateCamera(float rotationAmount)
    {
        currentCameraAngle += rotationAmount;
        targetCameraAngle = currentCameraAngle;
        // Offset will be recalculated in LateUpdate.
    }

    // The ONLY place that writes to cameraTransform.position / rotation.
    void HandleCameraFollow()
    {
        if (cameraTransform == null) return;

        // cameraOffset was just baked in LateUpdate before this call,
        // and transform.position is the Rigidbody-interpolated position — no stale data.
        Vector3 targetPosition = transform.position + cameraOffset;

        if (useSmoothFollow && playerMotor != null)
        {
            // Rigidbody interpolation already smooths sub-step movement.
            // Keep smoothTime tight so SmoothDamp adds minimal extra lag.
            float smoothTime = 1f / Mathf.Max(cameraFollowSpeed, 0.01f);
            cameraTransform.position = Vector3.SmoothDamp(
                cameraTransform.position, targetPosition, ref cameraVelocity, smoothTime);
        }
        else
        {
            // Direct assignment is perfectly clean in LateUpdate with interpolation on.
            cameraTransform.position = targetPosition;
        }

        cameraTransform.LookAt(transform.position + Vector3.up);
    }

    void HandleGravity()
    {
        if (playerMotor != null) return;
        if (characterController == null) return;

        if (isGrounded && velocity.y < 0)
            velocity.y = -2f;

        velocity.y += Physics.gravity.y * Time.deltaTime;
        characterController.Move(velocity * Time.deltaTime);
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void SetCameraHeight(float height)
    {
        defaultCameraHeight = height;
        if (!isUpViewActive) cameraHeight = height;
        // Offset recalculated next LateUpdate automatically.
    }

    public void SetCameraAngle(float angle)
    {
        defaultCameraAngle = angle;
        if (!isUpViewActive)
        {
            cameraAngle = angle;
            // PositionCamera is only safe here if called outside of play-mode LateUpdate;
            // during play the LateUpdate path will correct it within one frame.
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
        if (cam == null || !useOrthographic) return;
        defaultOrthographicSize = size;
        if (!isUpViewActive)
        {
            orthographicSize = size;
            cam.orthographicSize = orthographicSize;
        }
    }

    public void SnapRotateLeft()
    {
        if (useSnapRotation && !isRotating) { targetCameraAngle -= 90f; isRotating = true; }
    }

    public void SnapRotateRight()
    {
        if (useSnapRotation && !isRotating) { targetCameraAngle += 90f; isRotating = true; }
    }

    public void SetUpView(bool enabled) => isUpViewActive = enabled;

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;
        Debug.Log($"OverheadController movement enabled: {enabled}");
        if (playerMotor != null) playerMotor.SetMovementEnabled(enabled);
    }

    public bool IsMovementEnabled() => movementEnabled;

    void EnsureFrogAnimationDriver()
    {
        if (frogAnimationDriver == null)
        {
            frogAnimationDriver = GetComponent<FrogAnimationDriver>();
        }

        if (frogAnimationDriver == null)
        {
            frogAnimationDriver = gameObject.AddComponent<FrogAnimationDriver>();
        }
    }

    // ── Debug Gizmos ─────────────────────────────────────────────────────────

    void OnDrawGizmosSelected()
    {
        if (!enableCameraCollision || cameraTransform == null) return;

        Vector3 playerPosition = transform.position;
        Vector3 desiredCameraDirection = Quaternion.Euler(cameraAngle, currentCameraAngle, 0) * Vector3.back;
        Vector3 desiredCameraPosition = playerPosition + desiredCameraDirection * cameraDistance + Vector3.up * cameraHeight;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(playerPosition, desiredCameraPosition);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(cameraTransform.position, cameraCollisionRadius);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(playerPosition + desiredCameraDirection * minCameraDistance + Vector3.up * cameraHeight, cameraCollisionRadius);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, Quaternion.Euler(0, currentCameraAngle, 0) * Vector3.forward * 3f);

        if (isRotating)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, Quaternion.Euler(0, targetCameraAngle, 0) * Vector3.forward * 3f);
        }
    }
}