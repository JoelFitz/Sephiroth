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

    [Header("Grounding")]
    public LayerMask groundMask = ~0;
    public float groundCheckDistance = 0.3f;

    private Rigidbody rb;
    private bool movementEnabled = true;
    private bool isGrounded;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        SetupRigidbody();
    }

    void SetupRigidbody()
    {
        // Let Unity handle gravity; we only control horizontal velocity.
        rb.useGravity = true;

        // Prevent physics from tipping the frog over; we rotate via transform instead.
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void FixedUpdate()
    {
        UpdateGrounded();
        ApplyHorizontalDecelerationIfNeeded();
    }

    void UpdateGrounded()
    {
        Vector3 origin = transform.position + Vector3.up * 0.1f;
        isGrounded = Physics.Raycast(origin, Vector3.down, groundCheckDistance, groundMask, QueryTriggerInteraction.Ignore);
    }

    void ApplyHorizontalDecelerationIfNeeded()
    {
        if (!movementEnabled)
            return;

        // When no explicit movement command is given, gently slow down.
        // OverheadController will call ApplyHorizontalVelocity every frame while input is active.
    }

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

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontal = new Vector3(currentVelocity.x, 0f, currentVelocity.z);

        // Clamp to max speed to keep movement responsive but bounded.
        Vector3 target = Vector3.ClampMagnitude(desiredHorizontalVelocity, maxSpeed);

        // Smoothly move towards the target horizontal velocity.
        float maxDelta = acceleration * Time.fixedDeltaTime;
        Vector3 newHorizontal = Vector3.MoveTowards(currentHorizontal, target, maxDelta);

        rb.linearVelocity = new Vector3(newHorizontal.x, currentVelocity.y, newHorizontal.z);
    }

    /// <summary>
    /// Instantly move the player to a world-space position using Rigidbody.
    /// Used by grapples, elevators, and spawn logic to avoid tunneling.
    /// </summary>
    public void MoveTo(Vector3 worldPosition)
    {
        if (rb != null)
        {
            rb.MovePosition(worldPosition);
        }
        else
        {
            transform.position = worldPosition;
        }
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

    public bool IsMovementEnabled()
    {
        return movementEnabled;
    }

    public bool IsGrounded()
    {
        return isGrounded;
    }

    public Rigidbody GetRigidbody()
    {
        return rb;
    }
}

