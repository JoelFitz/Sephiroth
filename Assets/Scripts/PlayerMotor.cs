using UnityEngine;

/// <summary>
/// Physics-based movement motor for the frog player.
/// Wraps a Rigidbody and exposes simple horizontal movement + teleport helpers
/// so higher-level controllers (like OverheadController, grapples, elevators)
/// don't need to talk to CharacterController directly.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
public class PlayerMotor : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Maximum horizontal speed for regular movement.")]
    public float maxSpeed = 5f;

    [Tooltip("How quickly we accelerate towards the desired horizontal velocity.")]
    public float acceleration = 25f;

    [Tooltip("How quickly we decelerate when there is no input.")]
    public float deceleration = 30f;

    [Header("Sprint")]
    [Tooltip("Multiplier applied to maxSpeed while sprinting.")]
    public float sprintSpeedMultiplier = 1.8f;

    [Tooltip("How quickly we accelerate to sprint speed.")]
    public float sprintAcceleration = 40f;

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public float groundCheckDistance = 0.3f;

    [Header("Cliff / Steep-Wall Blocking")]
    [Tooltip("Angle (degrees from vertical) above which a surface is treated as a climbable cliff. " +
             "E.g. 45 means anything steeper than 45° from horizontal is blocked.")]
    public float maxWallAngle = 45f;

    [Tooltip("How far ahead to probe for steep walls each physics step.")]
    public float wallProbeDistance = 0.15f;

    [Tooltip("Layer mask used for steep-wall detection. Should match your terrain/geometry layers.")]
    public LayerMask wallMask = ~0;

    [Header("Facing")]
    [Tooltip("How quickly the Rigidbody rotates toward the movement direction.")]
    public float turnSpeed = 12f;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private bool movementEnabled = true;
    private bool isGrounded;
    private bool isSprinting;
    private Vector3 desiredFacingDirection = Vector3.zero;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        SetupRigidbody();
    }

    void SetupRigidbody()
    {
        // Let Unity handle gravity; we only control horizontal velocity.
        rb.useGravity = true;

        // Prevent physics from tipping the frog over while still allowing yaw rotation.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Enable interpolation so camera following the Rigidbody is smoother.
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    void FixedUpdate()
    {
        UpdateGrounded();
        ApplyHorizontalDecelerationIfNeeded();
        ApplyFacingRotation();

        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
        }
    }

    void UpdateGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask,
                                     QueryTriggerInteraction.Ignore);
    }

    void ApplyHorizontalDecelerationIfNeeded()
    {
        if (!movementEnabled)
            return;

        // When no explicit movement command is given, gently slow down.
        // OverheadController will call ApplyHorizontalVelocity every frame while input is active.
    }

    // ─── Sprint ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Tell the motor whether the player is currently sprinting.
    /// Called each frame by OverheadController based on input.
    /// </summary>
    public void SetSprinting(bool sprinting)
    {
        isSprinting = sprinting;
    }

    public bool IsSprinting() => isSprinting;

    // ─── Movement ────────────────────────────────────────────────────────────

    /// <summary>
    /// Set the desired horizontal velocity (world space).
    /// Y component is ignored; gravity and jumps (if any) are handled separately.
    /// </summary>
    public void ApplyHorizontalVelocity(Vector3 desiredHorizontalVelocity)
    {
        if (rb == null) return;

        if (!movementEnabled)
        {
            desiredHorizontalVelocity = Vector3.zero;
        }

        desiredHorizontalVelocity.y = 0f;

        // Choose speed cap and acceleration based on sprint state.
        float speedCap = isSprinting ? maxSpeed * sprintSpeedMultiplier : maxSpeed;
        float accel = isSprinting ? sprintAcceleration : acceleration;

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        Vector3 target = Vector3.ClampMagnitude(desiredHorizontalVelocity, speedCap);
        float maxDelta = accel * Time.fixedDeltaTime;
        Vector3 newHorizontal = Vector3.MoveTowards(currentHorizontal, target, maxDelta);

        // Cliff-blocking: cancel velocity components that would push into a steep wall.
        newHorizontal = BlockCliffVelocity(newHorizontal);

        rb.linearVelocity = new Vector3(newHorizontal.x, currentVelocity.y, newHorizontal.z);
    }

    public void SetFacingDirection(Vector3 direction)
    {
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            desiredFacingDirection = Vector3.zero;
            return;
        }

        desiredFacingDirection = direction.normalized;
    }

    private void ApplyFacingRotation()
    {
        if (!movementEnabled || rb == null)
            return;

        if (desiredFacingDirection.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(desiredFacingDirection, Vector3.up);
        Quaternion newRotation = Quaternion.Slerp(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(newRotation);
    }

    // ─── Cliff / Steep-Wall Blocking ─────────────────────────────────────────

    /// <summary>
    /// Projects the candidate horizontal velocity against any steep surface
    /// directly ahead of the player. If a cliff face is detected the component
    /// of velocity pointing into the wall is removed, letting the player slide
    /// along it instead of climbing.
    /// </summary>
    private Vector3 BlockCliffVelocity(Vector3 horizontalVelocity)
    {
        if (horizontalVelocity.sqrMagnitude < 0.0001f) return horizontalVelocity;

        // Build a world-space capsule for the probe.
        float radius = capsule.radius * 0.99f;               // slight inset to avoid ground self-hit
        float halfH = Mathf.Max(capsule.height * 0.5f - capsule.radius, 0f);
        Vector3 center = transform.TransformPoint(capsule.center);
        Vector3 point1 = center + Vector3.up * halfH;         // top sphere center
        Vector3 point2 = center + Vector3.down * halfH;         // bottom sphere center

        Vector3 direction = horizontalVelocity.normalized;
        float castDist = wallProbeDistance + radius;

        RaycastHit hit;
        if (Physics.CapsuleCast(point1, point2, radius, direction, out hit, castDist,
                                 wallMask, QueryTriggerInteraction.Ignore))
        {
            // Measure how far the normal tilts from vertical.
            // A perfectly vertical wall has normal.y == 0  → angle from horizontal == 0°.
            // We block if the surface is steeper than maxWallAngle from horizontal.
            float angleFromHorizontal = Vector3.Angle(hit.normal, Vector3.up) - 90f;
            // angleFromHorizontal > 0 → wall leans over player (overhang).
            // For a sheer cliff, hit.normal.y ≈ 0 → angleFromHorizontal ≈ 0.
            // We want to block when the slope is steep, i.e. the normal is close to horizontal.
            float slopeAngle = 90f - Vector3.Angle(hit.normal, Vector3.up); // angle above horizontal

            if (slopeAngle < maxWallAngle)
            {
                // Project horizontal normal (ignore Y so we only affect XZ motion).
                Vector3 wallNormalHoriz = new Vector3(hit.normal.x, 0f, hit.normal.z).normalized;

                // Remove the component of velocity that points into the wall.
                float intoWall = Vector3.Dot(horizontalVelocity, -wallNormalHoriz);
                if (intoWall > 0f)
                {
                    horizontalVelocity += wallNormalHoriz * intoWall; // cancel penetrating component
                }
            }
        }

        return horizontalVelocity;
    }

    // ─── Teleport / Utility ──────────────────────────────────────────────────

    /// <summary>
    /// Instantly move the player to a world-space position using Rigidbody.
    /// Used by grapples, elevators, and spawn logic to avoid tunneling.
    /// </summary>
    public void MoveTo(Vector3 worldPosition)
    {
        if (rb != null)
            rb.MovePosition(worldPosition);
        else
            transform.position = worldPosition;
    }

    public void SetMovementEnabled(bool enabled)
    {
        movementEnabled = enabled;

        if (!enabled && rb != null)
        {
            // Stop horizontal motion but keep vertical velocity (e.g. falling).
            Vector3 v = rb.linearVelocity;
            rb.linearVelocity = new Vector3(0f, v.y, 0f);
        }
    }

    public bool IsMovementEnabled() => movementEnabled;
    public bool IsGrounded() => isGrounded;
    public Rigidbody GetRigidbody() => rb;
}