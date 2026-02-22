using UnityEngine;
using System.Collections;

public class ShyShiitakePersonality : MushroomPersonality
{
    [Header("Shy Behavior")]
    public float hideTime = 10000f;
    public float alertTime = 0.5f;
    public float fleeTime = 3f;
    public float fleeDistance = 8f;
    public float fleeDirectionUpdateRate = 0.2f;

    [Header("Burrowing Animation")]
    public float burrowTime = 1.5f; // Time to burrow into ground
    public float popOutTime = 0.8f; // Time to pop out of ground
    public float spinSpeed = 360f; // Degrees per second while burrowing
    public float proximityTime = 2f; // Time player must be near to trigger pop out
    public float proximityDistance = 3f; // Distance for proximity detection while hidden

    [Header("Ground Detection")]
    public float groundOffset = 0.1f; // Small offset above ground
    public float raycastDistance = 50f; // How far to raycast down
    public LayerMask terrainLayerMask = -1; // Include all layers by default

    private Vector3 fleeStartPosition;
    private float lastFleeDirectionUpdate = 0f;
    private float lastGroundUpdate = 0f;
    private Rigidbody rb;
    private bool wasKinematic; // Store original kinematic state

    // Burrowing state
    private bool isBurrowing = false;
    private bool isPoppingOut = false;
    private bool isFullyBurrowed = false;
    private float proximityTimer = 0f;
    private Vector3 surfacePosition;
    private Vector3 burrowedPosition;
    private Coroutine burrowCoroutine;

    public override void Initialize(MushroomAI ai, MushroomData mushroomData)
    {
        base.Initialize(ai, mushroomData);
        rb = GetComponent<Rigidbody>();

        if (rb != null)
        {
            wasKinematic = rb.isKinematic; // Store original kinematic state
        }

        SnapToGround();

        // Store surface and burrowed positions
        surfacePosition = transform.position;
        burrowedPosition = surfacePosition - Vector3.up * data.hideDepth;
    }

    void SnapToGround()
    {
        Vector3 worldPos = transform.position;
        float groundHeight = GetGroundHeight(worldPos);

        if (groundHeight != worldPos.y) // Ground was detected
        {
            Vector3 newPosition = new Vector3(worldPos.x, groundHeight + groundOffset, worldPos.z);

            // Use Rigidbody.MovePosition to properly move with physics
            if (rb != null && !rb.isKinematic)
            {
                rb.MovePosition(newPosition);
            }
            else
            {
                transform.position = newPosition;
            }

            // Update positions
            surfacePosition = newPosition;
            burrowedPosition = surfacePosition - Vector3.up * data.hideDepth;

            Debug.Log($"Mushroom {transform.name}: Snapped to ground at {newPosition}");
        }
        else
        {
            Debug.LogWarning($"Mushroom {transform.name}: No ground detected below position!");
        }
    }

    float GetGroundHeight(Vector3 worldPos)
    {
        // Start raycast from high above the position
        Vector3 rayStart = new Vector3(worldPos.x, worldPos.y + raycastDistance * 0.5f, worldPos.z);
        RaycastHit hit;

        // Raycast straight down to find terrain/ground
        if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance, terrainLayerMask))
        {
            Debug.DrawRay(rayStart, Vector3.down * hit.distance, Color.green, 0.1f);
            return hit.point.y;
        }
        else
        {
            Debug.DrawRay(rayStart, Vector3.down * raycastDistance, Color.red, 0.1f);
            Debug.LogWarning($"No ground found below {worldPos} within {raycastDistance} units");
            return worldPos.y; // Return current height if no ground found
        }
    }

    public override void UpdateBehavior()
    {
        // Skip all behavior updates during animations
        if (isBurrowing || isPoppingOut)
        {
            return;
        }

        // Update ground position periodically and during movement
        if (Time.time - lastGroundUpdate > 0.1f) // 10 times per second
        {
            UpdateGroundPosition();
            lastGroundUpdate = Time.time;
        }

        switch (mushroomAI.currentState)
        {
            case MushroomState.Hidden:
                HandleHiddenState();
                break;

            case MushroomState.Idle:
                HandleIdleState();
                break;

            case MushroomState.Alert:
                HandleAlertState();
                break;

            case MushroomState.Fleeing:
                HandleFleeingState();
                break;
        }
    }

    void UpdateGroundPosition()
    {
        // Skip ground updates during burrowing animations
        if (isBurrowing || isPoppingOut) return;

        Vector3 currentPos = transform.position;
        float groundHeight = GetGroundHeight(currentPos);
        float targetY = groundHeight + groundOffset;

        // Only update Y position if it's significantly different
        if (Mathf.Abs(currentPos.y - targetY) > 0.05f)
        {
            Vector3 targetPosition = new Vector3(currentPos.x, targetY, currentPos.z);

            // Use Rigidbody.MovePosition for physics-based movement
            if (rb != null && !rb.isKinematic)
            {
                rb.MovePosition(targetPosition);
            }
            else
            {
                transform.position = targetPosition;
            }

            // Update surface position
            surfacePosition = targetPosition;
            burrowedPosition = surfacePosition - Vector3.up * data.hideDepth;
        }
    }

    void HandleHiddenState()
    {
        mushroomAI.StopMushroom();

        // Check for player proximity while hidden
        if (mushroomAI.Player != null && isFullyBurrowed)
        {
            float distanceToPlayer = Vector3.Distance(transform.position, mushroomAI.Player.position);

            if (distanceToPlayer <= proximityDistance)
            {
                proximityTimer += Time.deltaTime;

                if (proximityTimer >= proximityTime)
                {
                    Debug.Log($"Mushroom {transform.name}: Player near for {proximityTime}s, popping out!");
                    StartPopOut();
                    proximityTimer = 0f;
                    return;
                }
            }
            else
            {
                proximityTimer = 0f; // Reset timer if player moves away
            }
        }

        // Normal hide time behavior
        if (!mushroomAI.PlayerInRange && mushroomAI.StateTimer > hideTime)
        {
            StartPopOut();
        }
    }

    void HandleIdleState()
    {
        mushroomAI.StopMushroom();
        // Mushroom is visible at surface level

        // If player comes near, get alert and pop up
        if (mushroomAI.PlayerInRange)
        {
            ChangeState(MushroomState.Alert);
        }
    }

    void HandleAlertState()
    {
        mushroomAI.StopMushroom();
        // Mushroom is fully visible - no special handling needed

        // Wait a moment, then flee
        if (mushroomAI.StateTimer > alertTime)
        {
            ChangeState(MushroomState.Fleeing);
        }

        // If player leaves during alert, go back to idle
        if (!mushroomAI.PlayerInRange)
        {
            ChangeState(MushroomState.Idle);
        }
    }

    void HandleFleeingState()
    {
        // Check if we should stop fleeing and start burrowing
        float distanceFled = Vector3.Distance(transform.position, fleeStartPosition);
        bool shouldStopFleeing = mushroomAI.StateTimer > fleeTime || distanceFled > fleeDistance;

        if (shouldStopFleeing)
        {
            mushroomAI.StopMushroom();
            StartBurrow(); // This will handle the state change internally
            return; // Exit early to prevent further flee logic
        }

        // Continue fleeing logic only if not stopping
        UpdateFleeDirection();

        // Move away from player using current flee direction
        if (mushroomAI.Player != null)
        {
            Vector3 currentFleeDirection = GetCurrentFleeDirection();

            // Move horizontally - MushroomAI.MoveMushroom only affects X and Z
            Vector3 horizontalMovement = new Vector3(currentFleeDirection.x, 0, currentFleeDirection.z);
            mushroomAI.MoveMushroom(horizontalMovement, data.fleeSpeed);

            // Ground snapping happens in UpdateGroundPosition()
        }
    }

    void StartBurrow()
    {
        if (burrowCoroutine != null)
        {
            StopCoroutine(burrowCoroutine);
        }

        // Immediately stop mushroom movement and set kinematic for full control
        mushroomAI.StopMushroom();

        if (rb != null)
        {
            rb.isKinematic = true; // Disable physics during burrowing
        }

        isBurrowing = true;
        burrowCoroutine = StartCoroutine(BurrowAnimation());
    }

    void StartPopOut()
    {
        if (burrowCoroutine != null)
        {
            StopCoroutine(burrowCoroutine);
        }

        // Keep kinematic during pop out animation
        if (rb != null)
        {
            rb.isKinematic = true; // Keep disabled until we're back on surface
        }

        // Set flags immediately
        isPoppingOut = true;
        isFullyBurrowed = false;

        burrowCoroutine = StartCoroutine(PopOutAnimation());
    }

    IEnumerator BurrowAnimation()
    {
        isBurrowing = true;
        isFullyBurrowed = false;

        PlayRustleSound(); // Play sound when starting to burrow

        float elapsedTime = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = new Vector3(startPos.x, GetGroundHeight(startPos) - data.hideDepth + groundOffset, startPos.z);

        // Get mushroom model for spinning
        Transform modelTransform = mushroomAI.mushroomModel != null ? mushroomAI.mushroomModel.transform : transform;
        Vector3 initialRotation = modelTransform.eulerAngles;

        while (elapsedTime < burrowTime)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / burrowTime;

            // SIMULTANEOUS position and rotation interpolation
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, progress);
            transform.position = currentPos;

            // Spinning animation - synchronized with position progress
            float spinRotation = spinSpeed * progress * (burrowTime / 360f) * 360f; // Full rotations based on progress
            modelTransform.rotation = Quaternion.Euler(initialRotation.x, initialRotation.y + spinRotation, initialRotation.z);

            yield return null;
        }

        // Ensure final position and rotation
        transform.position = targetPos;
        float finalRotation = spinSpeed * (burrowTime / 360f) * 360f;
        modelTransform.rotation = Quaternion.Euler(initialRotation.x, initialRotation.y + finalRotation, initialRotation.z);

        // Reset flags and change state
        isBurrowing = false;
        isFullyBurrowed = true;
        proximityTimer = 0f;

        // Change to hidden state after burrowing is complete
        ChangeState(MushroomState.Hidden);

        Debug.Log($"Mushroom {transform.name}: Fully burrowed!");
        burrowCoroutine = null;
    }

    IEnumerator PopOutAnimation()
    {
        isPoppingOut = true;
        isFullyBurrowed = false;

        PlayRustleSound(); // Play sound when popping out

        float elapsedTime = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = new Vector3(startPos.x, GetGroundHeight(startPos) + groundOffset, startPos.z);

        // Get mushroom model for spinning (reverse spin)
        Transform modelTransform = mushroomAI.mushroomModel != null ? mushroomAI.mushroomModel.transform : transform;
        Vector3 initialRotation = modelTransform.eulerAngles;

        while (elapsedTime < popOutTime)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / popOutTime;

            // SIMULTANEOUS position and rotation interpolation with bounce
            float bounceProgress = Mathf.Pow(progress, 0.6f); // Ease out for pop effect
            Vector3 currentPos = Vector3.Lerp(startPos, targetPos, bounceProgress);
            transform.position = currentPos;

            // Reverse spinning animation - synchronized with position progress
            float spinRotation = -spinSpeed * 0.5f * progress * (popOutTime / 360f) * 360f; // Half speed, reverse direction
            modelTransform.rotation = Quaternion.Euler(initialRotation.x, initialRotation.y + spinRotation, initialRotation.z);

            yield return null;
        }

        // Ensure final position and reset rotation
        transform.position = targetPos;

        // Reset rotation to normal
        modelTransform.rotation = Quaternion.Euler(initialRotation);

        // Re-enable physics now that we're back on surface
        if (rb != null)
        {
            rb.isKinematic = wasKinematic; // Restore original kinematic state
            rb.linearVelocity = Vector3.zero; // Clear any residual velocity
        }

        // Reset flags and change state
        isPoppingOut = false;

        // Change to idle state after popping out
        ChangeState(MushroomState.Idle);

        Debug.Log($"Mushroom {transform.name}: Popped out and physics restored!");
        burrowCoroutine = null;
    }

    void UpdateFleeDirection()
    {
        if (Time.time - lastFleeDirectionUpdate > fleeDirectionUpdateRate)
        {
            if (mushroomAI.Player != null)
            {
                Vector3 newFleeDirection = (transform.position - mushroomAI.Player.position).normalized;
                mushroomAI.UpdateFleeDirection(newFleeDirection);
                lastFleeDirectionUpdate = Time.time;
            }
        }
    }

    Vector3 GetCurrentFleeDirection()
    {
        if (mushroomAI.Player != null)
        {
            return (transform.position - mushroomAI.Player.position).normalized;
        }
        return mushroomAI.FleeDirection;
    }

    public override void OnStateChanged(MushroomState fromState, MushroomState toState)
    {
        Debug.Log($"Mushroom {transform.name}: {fromState} -> {toState}");

        if (toState == MushroomState.Alert)
        {
            PlayRustleSound();
        }
        else if (toState == MushroomState.Fleeing)
        {
            fleeStartPosition = transform.position;
            lastFleeDirectionUpdate = 0f;
            SnapToGround(); // Ensure we're properly positioned when starting to flee
        }
        else if (toState == MushroomState.Idle)
        {
            SnapToGround(); // Snap to ground when becoming visible
            proximityTimer = 0f; // Reset proximity timer

            // Ensure we're not in any animation states
            isBurrowing = false;
            isPoppingOut = false;
            isFullyBurrowed = false;

            // Restore physics if it was disabled
            if (rb != null && rb.isKinematic && !wasKinematic)
            {
                rb.isKinematic = wasKinematic;
                rb.linearVelocity = Vector3.zero;
            }
        }
    }

    void PlayRustleSound()
    {
        if (data.rustleSounds != null && data.rustleSounds.Length > 0)
        {
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(data.rustleSounds[Random.Range(0, data.rustleSounds.Length)]);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw raycast for ground detection
        Vector3 worldPos = transform.position;
        Vector3 rayStart = new Vector3(worldPos.x, worldPos.y + raycastDistance * 0.5f, worldPos.z);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(rayStart, Vector3.down * raycastDistance);

        // Draw ground position if detected
        float groundHeight = GetGroundHeight(worldPos);
        if (groundHeight != worldPos.y)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(new Vector3(worldPos.x, groundHeight, worldPos.z), 0.5f);

            // Draw offset position
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(worldPos.x, groundHeight + groundOffset, worldPos.z), 0.3f);

            // Draw burrowed position
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(new Vector3(worldPos.x, groundHeight - data.hideDepth + groundOffset, worldPos.z), 0.3f);
        }

        // Draw proximity detection range when hidden
        if (mushroomAI != null && mushroomAI.currentState == MushroomState.Hidden && isFullyBurrowed)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, proximityDistance);

            // Show proximity timer progress
            if (proximityTimer > 0)
            {
                Gizmos.color = Color.magenta;
                float timerProgress = proximityTimer / proximityTime;
                Gizmos.DrawWireSphere(transform.position, proximityDistance * timerProgress);
            }
        }

        // Show current animation state
        if (isBurrowing)
        {
            Gizmos.color = Color.orange;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.3f);
        }
        else if (isPoppingOut)
        {
            Gizmos.color = Color.limeGreen;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.3f);
        }

        // Show kinematic state
        if (rb != null && rb.isKinematic)
        {
            Gizmos.color = Color.purple;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 0.2f);
        }
    }
}

