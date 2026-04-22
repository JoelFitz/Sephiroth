using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Rope elevator that visually shoots a RopeSegment "tongue" from the frog's mouth
/// to an ElevatorAnchorPoint, then pulls the player up using their CharacterController.
/// Attach this to a GameObject that has a trigger collider covering the "entry" area
/// beneath the elevator.
/// </summary>
public class RopeElevator : MonoBehaviour
{
    [Header("Elevator Path")]
    [Tooltip("World-space start of the ride. If not set, uses this transform's position.")]
    public Transform bottomPoint;

    [Tooltip("World-space end of the ride (where the player ends up).")]
    public Transform topPoint;

    [Tooltip("Where the rope tongue should attach on the elevator (often near topPoint).")]
    public Transform elevatorAnchorPoint;

    [Header("Ride Settings")]
    [Tooltip("Units per second the player is pulled along the rope.")]
    public float rideSpeed = 4f;

    [Tooltip("Key the player must press while standing in the trigger to start riding.")]
    public KeyCode useKey = KeyCode.Mouse0;

    [Tooltip("If enabled, this elevator ignores its own key polling and expects a unified input router to call TryUnifiedTongueAction().")]
    public bool useUnifiedTongueInput = true;

    [Tooltip("Maximum distance from the rope line (XZ) to allow starting the ride.")]
    public float allowedHorizontalOffset = 1.0f;

    [Tooltip("If true, starting the ride will gently snap the player onto the rope line horizontally.")]
    public bool snapToRopeOnStart = true;

    [Header("Visual Rope (RopeSegment chain)")]
    [Tooltip("Prefab used for each rope segment (same as FrogTongueController.tongueSegmentPrefab).")]
    public GameObject ropeSegmentPrefab;

    [Tooltip("Desired distance between segments along the rope.")]
    public float segmentLength = 0.3f;

    [Tooltip("How fast the rope shoots out from the frog's mouth.")]
    public float extendSpeed = 25f;

    [Tooltip("Approximate width used when configuring rope segment visuals (if needed).")]
    public float ropeWidth = 0.08f;

    [Tooltip("Optional override material for the rope; if null, will try to pull from FrogTongueController.")]
    public Material ropeMaterial;

    [Header("Elevator Physics")]
    [Tooltip("Final rope length from the anchor when fully reeled in (gives some swing room).")]
    public float targetDistanceFromAnchor = 1.5f;

    [Tooltip("How quickly the rope length (joint max distance) shortens towards the target distance.")]
    public float reelSpeed = 2f;

    [Tooltip("Spring strength of the elevator joint pulling the player towards the anchor.")]
    public float elevatorSpring = 40f;

    [Tooltip("Damping on the elevator joint to prevent excessive oscillation.")]
    public float elevatorDamper = 5f;

    [Header("Debug")]
    [Tooltip("Draw gizmos for the rope path and trigger occupancy.")]
    public bool drawGizmos = true;

    private Transform playerTransform;
    private CharacterController playerController;
    private OverheadController overheadController;
    private TongueGrappleSystem grappleSystem;
    private FrogTongueController frogTongueController;
    private PlayerMotor playerMotor;
    private Rigidbody playerRigidbody;
    private Transform tongueAnchor; // Frog's mouth anchor

    private bool playerInZone;
    private bool isRiding;
    private float rideProgress; // 0 at bottomPoint, 1 at topPoint

    // Rope state
    private enum RopeState { Idle, Extending, Riding }
    private RopeState ropeState = RopeState.Idle;

    private readonly List<GameObject> ropeSegments = new List<GameObject>();
    private float currentRopeLength;
    private float targetRopeLength;
    private SpringJoint elevatorJoint;

    void Reset()
    {
        // Try to ensure any collider on this object is marked as trigger.
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
    }

    void Update()
    {
        switch (ropeState)
        {
            case RopeState.Extending:
                UpdateRopeExtension();
                break;
            case RopeState.Riding:
                UpdateRopeWhileRiding();
                break;
        }

        HandleInput();

        if (isRiding)
        {
            UpdateRide();
        }
    }

    void HandleInput()
    {
        if (useUnifiedTongueInput)
            return;

        if (Input.GetKeyDown(useKey))
        {
            // While attached to the elevator, pressing the key again detaches.
            if (isRiding)
            {
                DetachFromElevator();
                return;
            }

            if (grappleSystem != null && grappleSystem.IsGrappling())
            {
                // Do not start elevator while grappling.
                return;
            }

            TryStartRideWithTongue();
        }
    }

    void TryStartRideWithTongue()
    {
        if (!playerInZone)
            return;

        if (bottomPoint == null || topPoint == null)
        {
            Debug.LogWarning("RopeElevator requires both bottomPoint and topPoint assigned.");
            return;
        }

        if (elevatorAnchorPoint == null)
        {
            Debug.LogWarning("RopeElevator requires elevatorAnchorPoint assigned (where the tongue should attach).");
            return;
        }

        if (playerTransform == null)
        {
            return;
        }

        Vector3 start = bottomPoint.position;
        Vector3 end = topPoint.position;
        Vector3 line = end - start;
        float lineMagnitude = line.magnitude;
        if (lineMagnitude < 0.1f)
        {
            Debug.LogWarning("RopeElevator has a very short path; ride cancelled.");
            return;
        }

        // Compute closest point on rope line to player horizontally, and optionally snap them.
        Vector3 playerPos = playerTransform.position;
        Vector3 startXZ = new Vector3(start.x, 0f, start.z);
        Vector3 endXZ = new Vector3(end.x, 0f, end.z);
        Vector3 lineXZ = endXZ - startXZ;
        float tHoriz = 0f;
        if (lineXZ.sqrMagnitude > 0.0001f)
        {
            tHoriz = Mathf.Clamp01(Vector3.Dot(new Vector3(playerPos.x, 0f, playerPos.z) - startXZ,
                                              lineXZ.normalized) / lineXZ.magnitude);
        }
        Vector3 closestOnLineXZ = Vector3.Lerp(startXZ, endXZ, tHoriz);
        float horizontalDist = Vector3.Distance(new Vector3(playerPos.x, 0f, playerPos.z), closestOnLineXZ);

        if (horizontalDist > allowedHorizontalOffset)
        {
            // Player is too far from rope horizontally; don't start.
            return;
        }

        if (snapToRopeOnStart)
        {
            Vector3 targetXZ = new Vector3(closestOnLineXZ.x, playerPos.y, closestOnLineXZ.z);
            Vector3 horizontalDelta = targetXZ - playerPos;

            if (playerMotor != null)
            {
                playerMotor.MoveTo(playerPos + horizontalDelta);
            }
            else if (playerRigidbody != null)
            {
                playerRigidbody.MovePosition(playerPos + horizontalDelta);
            }
            else if (playerController != null)
            {
                playerController.Move(horizontalDelta);
            }

            playerPos = playerTransform.position;
        }

        // Initial progress along the 3D line based on projection of player's position.
        Vector3 toPlayer = playerPos - start;
        float t = Mathf.Clamp01(Vector3.Dot(toPlayer, line.normalized) / lineMagnitude);
        rideProgress = t;

        // Disable normal movement while we're in the elevator sequence.
        // For the physics-based elevator we keep movement enabled so the player has agency.

        // Set up rope/tongue visual and start extending it.
        SetupTongueReferences();
        BeginRopeExtension();
    }

    void SetupTongueReferences()
    {
        if (frogTongueController == null && playerTransform != null)
        {
            frogTongueController = playerTransform.GetComponent<FrogTongueController>();
            if (frogTongueController == null)
            {
                frogTongueController = playerTransform.GetComponentInParent<FrogTongueController>();
            }
        }

        // Use the same anchor as the main tongue if available.
        if (frogTongueController != null && frogTongueController.tongueAnchor != null)
        {
            tongueAnchor = frogTongueController.tongueAnchor;

            // If the elevator wasn't given a prefab/material explicitly, try to borrow them.
            if (ropeSegmentPrefab == null)
            {
                ropeSegmentPrefab = frogTongueController.tongueSegmentPrefab;
            }
            if (ropeMaterial == null)
            {
                ropeMaterial = frogTongueController.tongueMaterial;
            }
            segmentLength = frogTongueController.segmentLength;
            ropeWidth = frogTongueController.tongueWidth;
        }
        else
        {
            // Fallback: use player's transform as anchor if we don't find a dedicated one.
            tongueAnchor = playerTransform;
        }
    }

    void BeginRopeExtension()
    {
        if (ropeSegmentPrefab == null || tongueAnchor == null || elevatorAnchorPoint == null)
        {
            // Missing setup; just start the ride without visual rope.
            isRiding = true;
            ropeState = RopeState.Idle;
            return;
        }

        // Clear any previous rope.
        ClearRopeSegments();

        Vector3 startPos = tongueAnchor.position;
        Vector3 endPos = elevatorAnchorPoint.position;
        targetRopeLength = Vector3.Distance(startPos, endPos);
        currentRopeLength = 0f;
        ropeState = RopeState.Extending;
    }

    void UpdateRopeExtension()
    {
        if (tongueAnchor == null || elevatorAnchorPoint == null)
        {
            ropeState = RopeState.Idle;
            isRiding = true; // Fall back to ride even if visuals break.
            return;
        }

        currentRopeLength += extendSpeed * Time.deltaTime;
        currentRopeLength = Mathf.Min(currentRopeLength, targetRopeLength);

        Vector3 startPos = tongueAnchor.position;
        Vector3 endPos = elevatorAnchorPoint.position;
        Vector3 dir = (endPos - startPos).normalized;

        int segmentsNeeded = Mathf.Max(0, Mathf.FloorToInt(currentRopeLength / Mathf.Max(segmentLength, 0.01f)));
        UpdateRopeSegments(startPos, dir, segmentsNeeded, currentRopeLength);

        // When we've reached the anchor, start riding.
        if (Mathf.Approximately(currentRopeLength, targetRopeLength))
        {
            ropeState = RopeState.Riding;
            AttachElevatorJoint();
        }
    }

    void UpdateRopeWhileRiding()
    {
        if (tongueAnchor == null || elevatorAnchorPoint == null)
        {
            return;
        }

        // As the player moves towards the anchor, shorten the rope and reduce segment count.
        Vector3 startPos = tongueAnchor.position;
        Vector3 endPos = elevatorAnchorPoint.position;
        float distance = Vector3.Distance(startPos, endPos);
        currentRopeLength = distance;

        Vector3 dir = (endPos - startPos).normalized;
        int segmentsNeeded = Mathf.Max(0, Mathf.FloorToInt(currentRopeLength / Mathf.Max(segmentLength, 0.01f)));

        UpdateRopeSegments(startPos, dir, segmentsNeeded, currentRopeLength);
    }

    void UpdateRopeSegments(Vector3 startPos, Vector3 dir, int segmentsNeeded, float ropeLength)
    {
        // Ensure we have enough segment objects.
        while (ropeSegments.Count < segmentsNeeded)
        {
            GameObject segment = Instantiate(ropeSegmentPrefab);
            ConfigureSegmentVisual(segment);
            ropeSegments.Add(segment);
        }

        // Position active segments along the line from startPos towards the anchor
        // and update their individual LineRenderers.
        for (int i = 0; i < ropeSegments.Count; i++)
        {
            bool shouldBeActive = i < segmentsNeeded;
            GameObject seg = ropeSegments[i];
            if (seg == null) continue;

            if (shouldBeActive)
            {
                if (!seg.activeSelf) seg.SetActive(true);

                float distanceAlongRope = Mathf.Min((i + 1) * segmentLength, ropeLength);
                Vector3 segPos = startPos + dir * distanceAlongRope;
                seg.transform.position = segPos;
                seg.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);

                // Update this segment's line so it connects from previous point to this one.
                LineRenderer lr = seg.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    Vector3 startPoint = (i == 0)
                        ? startPos
                        : ropeSegments[i - 1] != null
                            ? ropeSegments[i - 1].transform.position
                            : startPos;

                    lr.enabled = true;
                    lr.positionCount = 2;
                    lr.SetPosition(0, startPoint);
                    lr.SetPosition(1, segPos);
                }
            }
            else
            {
                if (seg.activeSelf) seg.SetActive(false);

                LineRenderer lr = seg.GetComponent<LineRenderer>();
                if (lr != null)
                {
                    lr.enabled = false;
                }
            }
        }

        // If the rope is effectively fully retracted, clear everything.
        if (segmentsNeeded == 0 && ropeSegments.Count > 0 && ropeLength < 0.05f)
        {
            ClearRopeSegments();
            ropeState = RopeState.Idle;
        }
    }

    void ConfigureSegmentVisual(GameObject segment)
    {
        // Make sure rope segments do not introduce unwanted physics for the elevator.
        Rigidbody rb = segment.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        LineRenderer lr = segment.GetComponent<LineRenderer>();
        if (lr != null)
        {
            lr.useWorldSpace = true;
            lr.positionCount = 2;
            lr.startWidth = ropeWidth;
            lr.endWidth = ropeWidth;

            if (ropeMaterial != null)
            {
                lr.material = ropeMaterial;
            }
            lr.enabled = false;
        }
    }

    void UpdateRide()
    {
        if (!isRiding || playerTransform == null)
        {
            StopRide();
            return;
        }
        
        if (elevatorJoint == null)
        {
            StopRide();
            return;
        }

        // Gradually reel the maximum rope length towards the target distance to the anchor.
        float currentMax = elevatorJoint.maxDistance;
        float desired = Mathf.Max(targetDistanceFromAnchor, 0.1f);
        elevatorJoint.maxDistance = Mathf.MoveTowards(currentMax, desired, reelSpeed * Time.deltaTime);
    }

    void StopRide()
    {
        DetachFromElevator();
    }

    public bool CanStartRide()
    {
        if (isRiding || ropeState == RopeState.Extending)
            return false;

        if (!playerInZone || playerTransform == null || elevatorAnchorPoint == null)
            return false;

        if (grappleSystem != null && grappleSystem.IsGrappling())
            return false;

        return true;
    }

    public bool TryUnifiedTongueAction()
    {
        if (isRiding)
        {
            DetachFromElevator();
            return true;
        }

        if (!CanStartRide())
            return false;

        TryStartRideWithTongue();
        return isRiding || ropeState == RopeState.Extending || ropeState == RopeState.Riding;
    }

    void AttachElevatorJoint()
    {
        if (playerRigidbody == null || elevatorAnchorPoint == null)
        {
            Debug.LogWarning("RopeElevator: Cannot attach elevator joint – missing Rigidbody or anchor.");
            isRiding = false;
            return;
        }

        // Ensure the anchor has a kinematic Rigidbody to attach to.
        Rigidbody anchorBody = elevatorAnchorPoint.GetComponent<Rigidbody>();
        if (anchorBody == null)
        {
            anchorBody = elevatorAnchorPoint.gameObject.AddComponent<Rigidbody>();
            anchorBody.isKinematic = true;
            anchorBody.useGravity = false;
        }

        float initialDistance = Vector3.Distance(playerTransform.position, elevatorAnchorPoint.position);
        float clampedTarget = Mathf.Clamp(targetDistanceFromAnchor, 0.1f, initialDistance);

        elevatorJoint = playerRigidbody.gameObject.AddComponent<SpringJoint>();
        elevatorJoint.connectedBody = anchorBody;
        elevatorJoint.autoConfigureConnectedAnchor = false;
        elevatorJoint.anchor = Vector3.zero;
        elevatorJoint.connectedAnchor = Vector3.zero;

        elevatorJoint.minDistance = clampedTarget;
        elevatorJoint.maxDistance = initialDistance;
        elevatorJoint.spring = elevatorSpring;
        elevatorJoint.damper = elevatorDamper;
        elevatorJoint.enableCollision = false;

        isRiding = true;
    }

    void DetachFromElevator()
    {
        if (elevatorJoint != null)
        {
            Destroy(elevatorJoint);
            elevatorJoint = null;
        }

        isRiding = false;
        rideProgress = 0f;

        // After reaching the top, let the rope visually retract and clear itself.
        currentRopeLength = 0f;
        ClearRopeSegments();
        ropeState = RopeState.Idle;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        CachePlayerReferences(other);
        playerInZone = true;
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        playerInZone = false;

        // If they leave while riding, we still let the ride finish; otherwise clear refs.
        if (!isRiding)
        {
            ClearPlayerReferences();
        }
    }

    void CachePlayerReferences(Collider other)
    {
        // Player components might be on this object or a parent.
        playerTransform = other.transform;
        playerController = other.GetComponent<CharacterController>();
        overheadController = other.GetComponent<OverheadController>();
        grappleSystem = other.GetComponent<TongueGrappleSystem>();
        frogTongueController = other.GetComponent<FrogTongueController>();
        playerMotor = other.GetComponent<PlayerMotor>();
        playerRigidbody = other.GetComponent<Rigidbody>();

        if ((playerController == null && playerMotor == null && playerRigidbody == null) || overheadController == null)
        {
            playerTransform = other.GetComponentInParent<Transform>();
            playerController = other.GetComponentInParent<CharacterController>();
            overheadController = other.GetComponentInParent<OverheadController>();
            grappleSystem = other.GetComponentInParent<TongueGrappleSystem>();
            frogTongueController = other.GetComponentInParent<FrogTongueController>();
            playerMotor = other.GetComponentInParent<PlayerMotor>();
            playerRigidbody = other.GetComponentInParent<Rigidbody>();
        }
    }

    void ClearPlayerReferences()
    {
        playerTransform = null;
        playerController = null;
        overheadController = null;
        grappleSystem = null;
        frogTongueController = null;
        playerMotor = null;
        playerRigidbody = null;
        tongueAnchor = null;
    }

    void ClearRopeSegments()
    {
        foreach (var seg in ropeSegments)
        {
            if (seg != null)
            {
                Destroy(seg);
            }
        }
        ropeSegments.Clear();
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        // Draw rope path.
        Vector3 start = bottomPoint != null ? bottomPoint.position : transform.position;
        Vector3 end = topPoint != null ? topPoint.position : start + Vector3.up * 5f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawSphere(start, 0.1f);
        Gizmos.DrawSphere(end, 0.1f);

        // Draw tongue anchor to elevator anchor line if available.
        if (elevatorAnchorPoint != null)
        {
            Transform anchor = tongueAnchor != null ? tongueAnchor : (playerTransform != null ? playerTransform : null);
            if (anchor != null)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawLine(anchor.position, elevatorAnchorPoint.position);
                Gizmos.DrawSphere(elevatorAnchorPoint.position, 0.12f);
            }
        }

        // Visualize trigger area if there is a collider.
        Collider col = GetComponent<Collider>();
        if (col != null && col.isTrigger)
        {
            Gizmos.color = playerInZone ? Color.green : Color.cyan;

            // Approximate only common collider types.
            BoxCollider box = col as BoxCollider;
            SphereCollider sphere = col as SphereCollider;
            CapsuleCollider capsule = col as CapsuleCollider;

            if (box != null)
            {
                Gizmos.matrix = box.transform.localToWorldMatrix;
                Gizmos.DrawWireCube(box.center, box.size);
                Gizmos.matrix = Matrix4x4.identity;
            }
            else if (sphere != null)
            {
                Gizmos.DrawWireSphere(sphere.bounds.center, sphere.radius * Mathf.Max(
                    sphere.transform.lossyScale.x,
                    sphere.transform.lossyScale.y,
                    sphere.transform.lossyScale.z));
            }
            else if (capsule != null)
            {
                Gizmos.DrawWireSphere(capsule.bounds.center, Mathf.Max(
                    capsule.radius * Mathf.Max(capsule.transform.lossyScale.x, capsule.transform.lossyScale.z),
                    capsule.height * 0.5f * capsule.transform.lossyScale.y));
            }
        }
    }
}


