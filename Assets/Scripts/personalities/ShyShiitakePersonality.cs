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
    public float burrowTime = 1.5f;
    public float popOutTime = 0.8f;
    public float spinSpeed = 360f;
    public float proximityTime = 2f;
    public float proximityDistance = 3f;

    [Header("Ground Detection")]
    public float groundOffset = 0.1f;
    public float raycastDistance = 50f;
    public LayerMask terrainLayerMask = -1;

    private Vector3 fleeStartPosition;
    private float lastFleeDirectionUpdate = 0f;
    private float lastGroundUpdate = 0f;
    private Rigidbody rb;
    private bool wasKinematic;

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
            wasKinematic = rb.isKinematic;

        SnapToGround();

        surfacePosition = transform.position;
        burrowedPosition = surfacePosition - Vector3.up * data.hideDepth;
    }

    // ── Lobotomy helper ───────────────────────────────────────────────────────
    // Kills every coroutine, zeroes velocity, and resets all animation flags.
    // Called the instant the mushroom enters TongueGrabbed so nothing can
    // override the tongue's control.
    void Lobotomise()
    {
        // Stop any running burrow/pop coroutine
        if (burrowCoroutine != null)
        {
            StopCoroutine(burrowCoroutine);
            burrowCoroutine = null;
        }

        // Clear all animation flags
        isBurrowing = false;
        isPoppingOut = false;
        isFullyBurrowed = false;
        proximityTimer = 0f;

        // Restore physics so the tongue's spring joint can actually move it,
        // then zero velocity so it doesn't carry momentum from fleeing/burrowing
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        mushroomAI.StopMushroom();
    }
    // ─────────────────────────────────────────────────────────────────────────

    public override void UpdateBehavior()
    {
        // While tongue-grabbed the mushroom is a pure physics prop — do nothing
        if (mushroomAI.currentState == MushroomState.TongueGrabbed)
            return;

        // Skip behavior during animations
        if (isBurrowing || isPoppingOut)
            return;

        if (Time.time - lastGroundUpdate > 0.1f)
        {
            UpdateGroundPosition();
            lastGroundUpdate = Time.time;
        }

        switch (mushroomAI.currentState)
        {
            case MushroomState.Hidden: HandleHiddenState(); break;
            case MushroomState.Idle: HandleIdleState(); break;
            case MushroomState.Alert: HandleAlertState(); break;
            case MushroomState.Fleeing: HandleFleeingState(); break;
        }
    }

    public override void OnStateChanged(MushroomState fromState, MushroomState toState)
    {
        Debug.Log($"Mushroom {transform.name}: {fromState} -> {toState}");

        // ── Tongue grabbed: immediately kill everything ────────────────────
        if (toState == MushroomState.TongueGrabbed)
        {
            Lobotomise();
            return;
        }
        // ─────────────────────────────────────────────────────────────────

        if (toState == MushroomState.Alert)
        {
            PlayRustleSound();
        }
        else if (toState == MushroomState.Fleeing)
        {
            fleeStartPosition = transform.position;
            lastFleeDirectionUpdate = 0f;
            SnapToGround();
        }
        else if (toState == MushroomState.Idle)
        {
            SnapToGround();
            proximityTimer = 0f;
            isBurrowing = false;
            isPoppingOut = false;
            isFullyBurrowed = false;

            if (rb != null && rb.isKinematic && !wasKinematic)
            {
                rb.isKinematic = wasKinematic;
                rb.linearVelocity = Vector3.zero;
            }
        }
    }

    void SnapToGround()
    {
        Vector3 worldPos = transform.position;
        float groundHeight = GetGroundHeight(worldPos);

        if (groundHeight != worldPos.y)
        {
            Vector3 newPosition = new Vector3(worldPos.x, groundHeight + groundOffset, worldPos.z);

            if (rb != null && !rb.isKinematic)
                rb.MovePosition(newPosition);
            else
                transform.position = newPosition;

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
        Vector3 rayStart = new Vector3(worldPos.x, worldPos.y + raycastDistance * 0.5f, worldPos.z);
        RaycastHit hit;

        if (Physics.Raycast(rayStart, Vector3.down, out hit, raycastDistance, terrainLayerMask))
        {
            Debug.DrawRay(rayStart, Vector3.down * hit.distance, Color.green, 0.1f);
            return hit.point.y;
        }
        else
        {
            Debug.DrawRay(rayStart, Vector3.down * raycastDistance, Color.red, 0.1f);
            Debug.LogWarning($"No ground found below {worldPos} within {raycastDistance} units");
            return worldPos.y;
        }
    }

    void UpdateGroundPosition()
    {
        if (isBurrowing || isPoppingOut) return;

        Vector3 currentPos = transform.position;
        float groundHeight = GetGroundHeight(currentPos);
        float targetY = groundHeight + groundOffset;

        if (Mathf.Abs(currentPos.y - targetY) > 0.05f)
        {
            Vector3 targetPosition = new Vector3(currentPos.x, targetY, currentPos.z);

            if (rb != null && !rb.isKinematic)
                rb.MovePosition(targetPosition);
            else
                transform.position = targetPosition;

            surfacePosition = targetPosition;
            burrowedPosition = surfacePosition - Vector3.up * data.hideDepth;
        }
    }

    void HandleHiddenState()
    {
        mushroomAI.StopMushroom();

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
                proximityTimer = 0f;
            }
        }

        if (!mushroomAI.PlayerInRange && mushroomAI.StateTimer > hideTime)
            StartPopOut();
    }

    void HandleIdleState()
    {
        mushroomAI.StopMushroom();

        if (mushroomAI.PlayerInRange)
            ChangeState(MushroomState.Alert);
    }

    void HandleAlertState()
    {
        mushroomAI.StopMushroom();

        if (mushroomAI.StateTimer > alertTime)
            ChangeState(MushroomState.Fleeing);

        if (!mushroomAI.PlayerInRange)
            ChangeState(MushroomState.Idle);
    }

    void HandleFleeingState()
    {
        float distanceFled = Vector3.Distance(transform.position, fleeStartPosition);
        bool shouldStopFleeing = mushroomAI.StateTimer > fleeTime || distanceFled > fleeDistance;

        if (shouldStopFleeing)
        {
            mushroomAI.StopMushroom();
            StartBurrow();
            return;
        }

        UpdateFleeDirection();

        if (mushroomAI.Player != null)
        {
            Vector3 currentFleeDirection = GetCurrentFleeDirection();
            Vector3 horizontalMovement = new Vector3(currentFleeDirection.x, 0, currentFleeDirection.z);
            mushroomAI.MoveMushroom(horizontalMovement, data.fleeSpeed);
        }
    }

    void StartBurrow()
    {
        if (burrowCoroutine != null)
            StopCoroutine(burrowCoroutine);

        mushroomAI.StopMushroom();

        if (rb != null)
            rb.isKinematic = true;

        isBurrowing = true;
        burrowCoroutine = StartCoroutine(BurrowAnimation());
    }

    void StartPopOut()
    {
        if (burrowCoroutine != null)
            StopCoroutine(burrowCoroutine);

        if (rb != null)
            rb.isKinematic = true;

        isPoppingOut = true;
        isFullyBurrowed = false;

        burrowCoroutine = StartCoroutine(PopOutAnimation());
    }

    IEnumerator BurrowAnimation()
    {
        isBurrowing = true;
        isFullyBurrowed = false;

        PlayRustleSound();

        float elapsedTime = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = new Vector3(startPos.x, GetGroundHeight(startPos) - data.hideDepth + groundOffset, startPos.z);

        Transform modelTransform = mushroomAI.mushroomModel != null ? mushroomAI.mushroomModel.transform : transform;
        Vector3 initialRotation = modelTransform.eulerAngles;

        while (elapsedTime < burrowTime)
        {
            // Abort mid-animation if tongue grabs us
            if (mushroomAI.currentState == MushroomState.TongueGrabbed)
            {
                isBurrowing = false;
                burrowCoroutine = null;
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / burrowTime;

            transform.position = Vector3.Lerp(startPos, targetPos, progress);

            float spinRotation = spinSpeed * progress * (burrowTime / 360f) * 360f;
            modelTransform.rotation = Quaternion.Euler(
                initialRotation.x, initialRotation.y + spinRotation, initialRotation.z);

            yield return null;
        }

        transform.position = targetPos;
        float finalRotation = spinSpeed * (burrowTime / 360f) * 360f;
        modelTransform.rotation = Quaternion.Euler(
            initialRotation.x, initialRotation.y + finalRotation, initialRotation.z);

        isBurrowing = false;
        isFullyBurrowed = true;
        proximityTimer = 0f;

        ChangeState(MushroomState.Hidden);
        Debug.Log($"Mushroom {transform.name}: Fully burrowed!");
        burrowCoroutine = null;
    }

    IEnumerator PopOutAnimation()
    {
        isPoppingOut = true;
        isFullyBurrowed = false;

        PlayRustleSound();

        float elapsedTime = 0f;
        Vector3 startPos = transform.position;
        Vector3 targetPos = new Vector3(startPos.x, GetGroundHeight(startPos) + groundOffset, startPos.z);

        Transform modelTransform = mushroomAI.mushroomModel != null ? mushroomAI.mushroomModel.transform : transform;
        Vector3 initialRotation = modelTransform.eulerAngles;

        while (elapsedTime < popOutTime)
        {
            // Abort mid-animation if tongue grabs us
            if (mushroomAI.currentState == MushroomState.TongueGrabbed)
            {
                isPoppingOut = false;
                burrowCoroutine = null;
                yield break;
            }

            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / popOutTime;

            float bounceProgress = Mathf.Pow(progress, 0.6f);
            transform.position = Vector3.Lerp(startPos, targetPos, bounceProgress);

            float spinRotation = -spinSpeed * 0.5f * progress * (popOutTime / 360f) * 360f;
            modelTransform.rotation = Quaternion.Euler(
                initialRotation.x, initialRotation.y + spinRotation, initialRotation.z);

            yield return null;
        }

        transform.position = targetPos;
        modelTransform.rotation = Quaternion.Euler(initialRotation);

        if (rb != null)
        {
            rb.isKinematic = wasKinematic;
            rb.linearVelocity = Vector3.zero;
        }

        isPoppingOut = false;
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
            return (transform.position - mushroomAI.Player.position).normalized;
        return mushroomAI.FleeDirection;
    }

    void PlayRustleSound()
    {
        if (data.rustleSounds != null && data.rustleSounds.Length > 0)
        {
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
                audioSource.PlayOneShot(data.rustleSounds[Random.Range(0, data.rustleSounds.Length)]);
        }
    }

    void OnDrawGizmosSelected()
    {
        Vector3 worldPos = transform.position;
        Vector3 rayStart = new Vector3(worldPos.x, worldPos.y + raycastDistance * 0.5f, worldPos.z);

        Gizmos.color = Color.blue;
        Gizmos.DrawRay(rayStart, Vector3.down * raycastDistance);

        float groundHeight = GetGroundHeight(worldPos);
        if (groundHeight != worldPos.y)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(new Vector3(worldPos.x, groundHeight, worldPos.z), 0.5f);

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(new Vector3(worldPos.x, groundHeight + groundOffset, worldPos.z), 0.3f);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(new Vector3(worldPos.x, groundHeight - data.hideDepth + groundOffset, worldPos.z), 0.3f);
        }

        if (mushroomAI != null && mushroomAI.currentState == MushroomState.Hidden && isFullyBurrowed)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, proximityDistance);

            if (proximityTimer > 0)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(transform.position, proximityDistance * (proximityTimer / proximityTime));
            }
        }

        if (isBurrowing)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.3f);
        }
        else if (isPoppingOut)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + Vector3.up * 0.5f, Vector3.one * 0.3f);
        }

        if (rb != null && rb.isKinematic)
        {
            Gizmos.color = Color.purple;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 1f, 0.2f);
        }
    }
}