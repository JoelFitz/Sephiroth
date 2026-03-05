using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class TongueGrappleSystem : MonoBehaviour
{
    [Header("Grapple Settings")]
    public LayerMask grapplePointLayer = -1;
    public float grappleRange = 8f;
    public float grappleSpeed = 15f;
    public KeyCode grappleKey = KeyCode.Space;

    [Header("Tongue Visuals")]
    public GameObject tongueSegmentPrefab;
    public Material tongueMaterial;
    public int maxTongueSegments = 15;
    public float segmentLength = 0.4f;
    public float tongueWidth = 0.1f;

    [Header("Grapple Physics - Updated")]
    public float swingSpeed = 8f;
    public float pullForce = 12f;
    public float maxGrappleTime = 5f;
    public float arcHeight = 2f; // How high the swing arc goes

    [Header("Audio & Effects")]
    public AudioClip grappleShootSound;
    public AudioClip grappleAttachSound;
    public AudioClip grappleReleaseSound;
    public ParticleSystem grappleEffect;

    [Header("Rope Physics")]
    public bool useRopePhysics = true;
    public float ropeSway = 2f;
    public float ropeSwaySpeed = 3f;
    public float ropeGravityEffect = 1f;
    public int ropeSimulationPoints = 5;

    // Internal Components
    private CharacterController characterController;
    private OverheadController playerController;
    private PlayerMotor playerMotor;
    private Rigidbody playerRigidbody;
    private LineRenderer tongueRenderer;
    private AudioSource audioSource;

    // Grapple State
    private enum GrappleState { Ready, Shooting, Attached, Swinging, Retracting }
    private GrappleState currentState = GrappleState.Ready;

    // Grapple Data
    private List<GameObject> tongueSegments = new List<GameObject>();
    private GrappleZone currentGrappleZone;
    private Transform grapplePoint;
    private Vector3 grappleDirection;
    private float grappleStartTime;

    // Swing Data
    private Vector3 swingStartPosition;
    private Vector3 swingTargetPosition;
    private float swingProgress = 0f;
    private Vector3 playerVelocity = Vector3.zero;

    private Vector3[] ropePoints;
    private Vector3[] ropeVelocities;
    private float ropePhysicsTime = 0f;

    void Start()
    {
        Debug.Log("TongueGrappleSystem initialized!");
        InitializeComponents();
        SetupTongueRenderer();
        InitializeRopePhysics();
    }

    void InitializeComponents()
    {
        characterController = GetComponent<CharacterController>();
        playerController = GetComponent<OverheadController>();

        playerMotor = GetComponent<PlayerMotor>();
        playerRigidbody = GetComponent<Rigidbody>();

        if (characterController == null && playerMotor == null && playerRigidbody == null)
        {
            Debug.LogError("TongueGrappleSystem requires either a CharacterController, PlayerMotor, or Rigidbody component!");
        }

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.volume = 0.7f;
    }

    void InitializeRopePhysics()
    {
        ropePoints = new Vector3[ropeSimulationPoints];
        ropeVelocities = new Vector3[ropeSimulationPoints];

        // Initialize points in a straight line
        for (int i = 0; i < ropeSimulationPoints; i++)
        {
            ropePoints[i] = Vector3.zero;
            ropeVelocities[i] = Vector3.zero;
        }
    }

    void SetupTongueRenderer()
    {
        try
        {
            // Check if we already have a LineRenderer
            tongueRenderer = GetComponent<LineRenderer>();
            if (tongueRenderer == null)
            {
                Debug.Log("TongueGrappleSystem: Adding LineRenderer component...");
                tongueRenderer = gameObject.AddComponent<LineRenderer>();
            }

            if (tongueRenderer == null)
            {
                Debug.LogError("TongueGrappleSystem: Failed to create LineRenderer component!");
                return;
            }

            Debug.Log("TongueGrappleSystem: LineRenderer component created successfully");

            // Safe material assignment
            if (tongueMaterial != null)
            {
                Debug.Log("TongueGrappleSystem: Assigning provided material");
                tongueRenderer.material = tongueMaterial;
                Debug.Log("TongueGrappleSystem: Using assigned tongue material");
            }
            else
            {
                Debug.LogWarning("TongueGrappleSystem: No tongue material assigned, creating default material");

                // Try different shader fallbacks
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null)
                {
                    Debug.Log("TongueGrappleSystem: Sprites/Default not found, trying Standard");
                    shader = Shader.Find("Standard");
                }
                if (shader == null)
                {
                    Debug.Log("TongueGrappleSystem: Standard not found, trying Legacy Shaders/Diffuse");
                    shader = Shader.Find("Legacy Shaders/Diffuse");
                }

                if (shader != null)
                {
                    Debug.Log($"TongueGrappleSystem: Creating material with shader: {shader.name}");
                    Material defaultMat = new Material(shader);
                    defaultMat.color = Color.green;
                    tongueRenderer.material = defaultMat;
                    Debug.Log($"TongueGrappleSystem: Created material with shader: {shader.name}");
                }
                else
                {
                    Debug.LogWarning("TongueGrappleSystem: No shader found, using LineRenderer default");
                }
            }

            Debug.Log($"TongueGrappleSystem: Setting LineRenderer properties - width: {tongueWidth}");
            tongueRenderer.startWidth = tongueWidth;
            tongueRenderer.endWidth = tongueWidth * 0.6f;
            tongueRenderer.positionCount = 0;
            tongueRenderer.useWorldSpace = true;

            Debug.Log("TongueGrappleSystem: LineRenderer setup complete");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"TongueGrappleSystem: Failed to setup LineRenderer: {e.Message}");
            Debug.LogError($"TongueGrappleSystem: Stack trace: {e.StackTrace}");
            tongueRenderer = null;
        }
    }




    void Update()
    {
        DetectGrappleZones();
        HandleInput();
        UpdateGrappleState();
        UpdateTongueVisuals();
    }

    void DetectGrappleZones()
    {
        if (currentState != GrappleState.Ready) return;

        // Alternative: Find all GrappleZone components in range
        GrappleZone[] allZones = FindObjectsOfType<GrappleZone>();

        //Debug.Log($"Found {allZones.Length} total grapple zones in scene");

        GrappleZone bestZone = null;
        float bestScore = 0f;

        foreach (var zone in allZones)
        {
            float distance = Vector3.Distance(transform.position, zone.transform.position);

            if (distance <= grappleRange)
            {
                //Debug.Log($"Zone {zone.name} is in range (distance: {distance})");

                if (zone.CanGrapple())
                {
                    Vector3 directionToZone = (zone.transform.position - transform.position).normalized;
                    float dotProduct = Vector3.Dot(transform.forward, directionToZone);

                    //Debug.Log($"  - Dot product: {dotProduct}");

                    if (dotProduct > 0.3f) // More lenient facing requirement
                    {
                        float score = dotProduct / distance;
                        //Debug.Log($"  - Valid zone! Score: {score}");

                        if (score > bestScore)
                        {
                            bestScore = score;
                            bestZone = zone;
                            //Debug.Log($"  - New best zone: {zone.name}");
                        }
                    }
                }
                else
                {
                    //Debug.Log($"  - Zone {zone.name} cannot grapple");
                }
            }
        }

        if (currentGrappleZone != bestZone)
        {
            currentGrappleZone = bestZone;
            //Debug.Log($"Current grapple zone changed to: {(bestZone != null ? bestZone.name : "NULL")}");
        }
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(grappleKey))
        {
            Debug.Log($"Grapple key pressed! Current state: {currentState}");
            Debug.Log($"Current grapple zone: {(currentGrappleZone != null ? currentGrappleZone.name : "NULL")}");

            if (currentState == GrappleState.Ready && currentGrappleZone != null)
            {
                Debug.Log("Attempting to start grapple...");
                StartGrapple();
            }
            else if (currentState == GrappleState.Attached || currentState == GrappleState.Swinging)
            {
                Debug.Log("Attempting to release grapple...");
                ReleaseGrapple();
            }
            else
            {
                Debug.Log($"Cannot grapple: State={currentState}, Zone={currentGrappleZone}");
            }
        }
    }

    void StartGrapple()
    {
        if (currentGrappleZone == null)
        {
            Debug.LogError("StartGrapple: currentGrappleZone is null!");
            return;
        }

        grapplePoint = currentGrappleZone.GetGrapplePoint();
        if (grapplePoint == null)
        {
            Debug.LogError($"StartGrapple: GrapplePoint is null on zone {currentGrappleZone.name}!");
            return;
        }

        grappleDirection = (grapplePoint.position - transform.position).normalized;
        currentState = GrappleState.Shooting;
        grappleStartTime = Time.time;

        // Disable player movement but keep camera following
        if (playerController != null)
        {
            playerController.SetMovementEnabled(false);
            Debug.Log("Player movement disabled for grappling");
        }

        PlaySound(grappleShootSound);

        // Start shooting effect
        if (grappleEffect != null)
            grappleEffect.Play();

        StartCoroutine(ShootTongueSimple());

        Debug.Log($"Starting grapple to {grapplePoint.name}");
    }



    IEnumerator ShootTongueSimple()
    {
        if (grapplePoint == null)
        {
            Debug.LogError("ShootTongueSimple: grapplePoint is null!");
            ReleaseGrapple();
            yield break;
        }

        Debug.Log($"TongueGrappleSystem: Shooting tongue to {grapplePoint.name}");

        // Show the tongue line immediately
        if (tongueRenderer != null)
        {
            tongueRenderer.positionCount = 2;
            tongueRenderer.SetPosition(0, transform.position + Vector3.up);
            tongueRenderer.SetPosition(1, grapplePoint.position);
        }

        yield return new WaitForSeconds(0.3f); // Brief shoot time

        AttachToGrapplePoint();
    }






    void AttachToGrapplePoint()
    {
        currentState = GrappleState.Attached;
        PlaySound(grappleAttachSound);

        // Setup swing parameters
        swingStartPosition = transform.position;
        swingTargetPosition = currentGrappleZone.GetTargetPosition();
        swingProgress = 0f;

        // Start swinging
        StartCoroutine(SwingToTargetCharacterController());

        Debug.Log($"Grapple attached! Swinging from {swingStartPosition} to {swingTargetPosition}");
    }

    IEnumerator SwingToTargetCharacterController()
    {
        currentState = GrappleState.Swinging;

        float swingStartTime = Time.time;
        Vector3 startPos = swingStartPosition;
        Vector3 endPos = swingTargetPosition;

        // Calculate the midpoint for the arc
        Vector3 midPoint = (startPos + endPos) * 0.5f;
        midPoint.y += arcHeight; // Add height for arc effect

        Debug.Log($"Swinging: Start={startPos}, Mid={midPoint}, End={endPos}");

        while (currentState == GrappleState.Swinging)
        {
            float elapsed = Time.time - swingStartTime;
            swingProgress = elapsed / (Vector3.Distance(startPos, endPos) / swingSpeed);

            // Check for timeout
            if (elapsed > maxGrappleTime)
            {
                Debug.Log("Grapple timed out");
                ReleaseGrapple();
                yield break;
            }

            // Check if swing is complete
            if (swingProgress >= 1f)
            {
                Debug.Log("Swing complete!");

                // Move to exact target position
                if (playerMotor != null)
                {
                    playerMotor.MoveTo(endPos);
                }
                else if (characterController != null)
                {
                    Vector3 finalMove = endPos - transform.position;
                    characterController.Move(finalMove);
                }
                else if (playerRigidbody != null)
                {
                    playerRigidbody.MovePosition(endPos);
                }

                ReleaseGrapple();
                yield break;
            }

            // Calculate current position along arc
            Vector3 targetPosition;
            if (swingProgress < 0.5f)
            {
                // First half: start to midpoint
                float t = swingProgress * 2f;
                targetPosition = Vector3.Lerp(startPos, midPoint, t);
            }
            else
            {
                // Second half: midpoint to end
                float t = (swingProgress - 0.5f) * 2f;
                targetPosition = Vector3.Lerp(midPoint, endPos, t);
            }

            // Move player along the swing arc
            Vector3 moveDirection = targetPosition - transform.position;
            if (playerMotor != null)
            {
                playerMotor.MoveTo(targetPosition);
            }
            else if (characterController != null && moveDirection.magnitude > 0.1f)
            {
                Debug.Log($"Moving player: {moveDirection.magnitude:F2} units");
                characterController.Move(moveDirection);
            }
            else if (playerRigidbody != null)
            {
                playerRigidbody.MovePosition(targetPosition);
            }

            yield return null;
        }
    }

    void ReleaseGrapple()
    {
        currentState = GrappleState.Retracting;
        PlaySound(grappleReleaseSound);

        // Start retracting tongue
        StartCoroutine(RetractTongue());
    }

    IEnumerator RetractTongue()
    {
        // Hide visual tongue
        if (tongueRenderer != null)
            tongueRenderer.positionCount = 0;

        yield return new WaitForSeconds(0.2f);

        // Re-enable player movement
        if (playerController != null)
        {
            playerController.SetMovementEnabled(true);
            Debug.Log("Player movement re-enabled after grappling");
        }

        currentState = GrappleState.Ready;
        currentGrappleZone = null;
        grapplePoint = null;
        swingProgress = 0f;

        Debug.Log("Grapple retracted - ready for next grapple");
    }





    void UpdateGrappleState()
    {
        // Add extra gravity only for CharacterController-based movement; Rigidbody uses built-in gravity.
        if (currentState == GrappleState.Swinging && characterController != null)
        {
            // Light downward force to keep character grounded to the arc
            Vector3 downwardForce = Vector3.up * Physics.gravity.y * 0.1f * Time.deltaTime;
            characterController.Move(downwardForce);
        }
    }

    void UpdateTongueVisuals()
    {
        if (tongueRenderer == null) return;

        // Only show visual during grappling states
        if (currentState == GrappleState.Attached || currentState == GrappleState.Swinging)
        {
            if (grapplePoint != null)
            {
                if (useRopePhysics)
                {
                    UpdateRopePhysics();
                    DrawPhysicsRope();
                }
                else
                {
                    // Simple straight line
                    tongueRenderer.positionCount = 2;
                    tongueRenderer.SetPosition(0, transform.position + Vector3.up);
                    tongueRenderer.SetPosition(1, grapplePoint.position);
                }
            }
        }
        else
        {
            // Hide the line when not grappling
            tongueRenderer.positionCount = 0;
        }
    }


    void UpdateRopePhysics()
    {
        ropePhysicsTime += Time.deltaTime;

        Vector3 startPoint = transform.position + Vector3.up;
        Vector3 endPoint = grapplePoint.position;

        // Update rope simulation points
        for (int i = 0; i < ropeSimulationPoints; i++)
        {
            float t = (float)i / (ropeSimulationPoints - 1);
            Vector3 targetPos = Vector3.Lerp(startPoint, endPoint, t);

            // Add gravity effect (make rope sag)
            float sagAmount = Mathf.Sin(t * Mathf.PI) * ropeGravityEffect;
            targetPos.y -= sagAmount;

            // Add sway effect
            float swayOffset = Mathf.Sin(ropePhysicsTime * ropeSwaySpeed + i * 0.5f) * ropeSway * t * (1 - t);
            Vector3 swayDirection = Vector3.Cross((endPoint - startPoint).normalized, Vector3.up).normalized;
            targetPos += swayDirection * swayOffset;

            // Spring physics towards target position
            Vector3 force = (targetPos - ropePoints[i]) * 10f;
            force += -ropeVelocities[i] * 5f; // Damping

            ropeVelocities[i] += force * Time.deltaTime;
            ropePoints[i] += ropeVelocities[i] * Time.deltaTime;

            // Constrain to fixed endpoints
            if (i == 0)
                ropePoints[i] = startPoint;
            else if (i == ropeSimulationPoints - 1)
                ropePoints[i] = endPoint;
        }
    }

    void DrawPhysicsRope()
    {
        tongueRenderer.positionCount = ropeSimulationPoints;
        for (int i = 0; i < ropeSimulationPoints; i++)
        {
            tongueRenderer.SetPosition(i, ropePoints[i]);
        }
    }


    void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null)
            audioSource.PlayOneShot(clip);
    }

    // Public methods for external control
    public bool IsGrappling()
    {
        return currentState != GrappleState.Ready;
    }

    public bool CanGrapple()
    {
        return currentState == GrappleState.Ready && currentGrappleZone != null;
    }

    public GrappleZone GetCurrentGrappleZone()
    {
        return currentGrappleZone;
    }

    void OnDestroy()
    {
        // Cleanup
        foreach (var segment in tongueSegments)
        {
            if (segment != null)
                Destroy(segment);
        }
    }

    // Debug visualization
    void OnDrawGizmos()
    {
        // Draw grapple range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, grappleRange);

        // Draw current grapple zone
        if (currentGrappleZone != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, currentGrappleZone.transform.position);

            if (currentGrappleZone.GetGrapplePoint() != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(currentGrappleZone.GetGrapplePoint().position, 0.5f);
            }

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(currentGrappleZone.GetTargetPosition(), 0.3f);
        }

        // Draw swing arc when grappling
        if (currentState == GrappleState.Swinging)
        {
            Gizmos.color = Color.magenta;

            Vector3 startPos = swingStartPosition;
            Vector3 endPos = swingTargetPosition;
            Vector3 midPoint = (startPos + endPos) * 0.5f;
            midPoint.y += arcHeight;

            // Draw arc
            for (int i = 0; i < 20; i++)
            {
                float t1 = i / 20f;
                float t2 = (i + 1) / 20f;

                Vector3 pos1 = CalculateArcPosition(startPos, midPoint, endPos, t1);
                Vector3 pos2 = CalculateArcPosition(startPos, midPoint, endPos, t2);

                Gizmos.DrawLine(pos1, pos2);
            }

            // Draw current position
            Gizmos.color = Color.white;
            Vector3 currentPos = CalculateArcPosition(startPos, midPoint, endPos, swingProgress);
            Gizmos.DrawSphere(currentPos, 0.2f);
        }
    }

    Vector3 CalculateArcPosition(Vector3 start, Vector3 mid, Vector3 end, float t)
    {
        if (t < 0.5f)
        {
            float localT = t * 2f;
            return Vector3.Lerp(start, mid, localT);
        }
        else
        {
            float localT = (t - 0.5f) * 2f;
            return Vector3.Lerp(mid, end, localT);
        }
    }
}

