using UnityEngine;
using System.Collections.Generic;

public class FrogTongueController : MonoBehaviour
{
    [Header("Tongue Configuration")]
    public GameObject tongueSegmentPrefab;
    public int tongueSegments = 10;
    public float segmentLength = 0.3f;
    public float tongueWidth = 0.08f;

    [Header("Tongue Mechanics")]
    public float extendSpeed = 25f;
    public float retractSpeed = 8f;
    public float maxTongueLength = 6f; 
    public float attachRange = 0.8f;
    public LayerMask catchableLayer = 1;

    [Header("Physics Settings")]
    public float springForce = 80f;
    public float springDamper = 10f;

    [Header("Input")]
    public KeyCode extendKey = KeyCode.Q;
    public KeyCode grabKey = KeyCode.E;

    [Header("Visual")]
    public Material tongueMaterial;

    [Header("Anchor Point")]
    public Transform tongueAnchor; // Frog's mouth position

    private List<GameObject> tongueSegmentObjects = new List<GameObject>();
    private List<RopeSegment> tongueSegmentScripts = new List<RopeSegment>();
    private LineRenderer tongueRenderer;
    private Transform playerTransform;

    private GameObject anchorObject;
    private Rigidbody anchorRigidbody;

    // Tongue states
    private enum TongueState { Retracted, Extending, Attached, Retracting }
    private TongueState currentState = TongueState.Retracted;

    private GameObject attachedTarget;
    private MushroomAI attachedMushroomAI;
    private SpringJoint attachmentJoint;
    private Vector3 tongueDirection;
    private float currentTongueLength;
    private int activeSegments;

    void Start()
    {
        playerTransform = transform;
        SetupTongueAnchor();
        SetupVisualTongue();
        CreateTongue();
    }

    void SetupTongueAnchor()
    {
        // Create invisible anchor point at frog's mouth
        anchorObject = new GameObject("TongueAnchor");
        anchorObject.transform.SetParent(transform);

        // Position anchor at frog's mouth level
        if (tongueAnchor != null)
            anchorObject.transform.position = tongueAnchor.position;
        else
            anchorObject.transform.localPosition = new Vector3(0f, 1.2f, 0.4f);

        anchorRigidbody = anchorObject.AddComponent<Rigidbody>();
        anchorRigidbody.isKinematic = true;
        anchorRigidbody.useGravity = false;
    }

    void SetupVisualTongue()
    {
        // Remove existing LineRenderer if it exists
        if (tongueRenderer != null)
        {
            DestroyImmediate(tongueRenderer);
        }

        tongueRenderer = gameObject.AddComponent<LineRenderer>();

        // Essential LineRenderer settings
        tongueRenderer.useWorldSpace = true;
        tongueRenderer.startWidth = tongueWidth;
        tongueRenderer.endWidth = tongueWidth * 0.7f; // Tapered tip
        tongueRenderer.positionCount = 0; // Start with no positions

        // Material assignment with fallback
        if (tongueMaterial != null)
        {
            tongueRenderer.material = tongueMaterial;
            Debug.Log("Using provided tongue material");
        }
        else
        {
            // Create a default visible material
            Debug.LogWarning("No tongue material assigned, creating default material");
            Material defaultMat = new Material(Shader.Find("Sprites/Default"));
            if (defaultMat.shader == null)
            {
                defaultMat = new Material(Shader.Find("Standard"));
            }
            defaultMat.color = Color.red; // Bright red for visibility
            tongueRenderer.material = defaultMat;
            tongueMaterial = defaultMat; // Store for future use
        }

        // Critical rendering settings
        tongueRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tongueRenderer.receiveShadows = false;
        tongueRenderer.sortingOrder = 100; // Render on top

        // Make sure it's enabled
        tongueRenderer.enabled = true;

        Debug.Log($"LineRenderer setup complete. Material: {tongueRenderer.material.name}, Enabled: {tongueRenderer.enabled}");
    }


    void CreateTongue()
    {
        // Clear any existing tongue segments
        DestroyTongue();
        currentTongueLength = 0f;
        activeSegments = 0;

        Vector3 startPos = anchorObject.transform.position;

        for (int i = 0; i < tongueSegments; i++)
        {
            // Create segment at anchor initially (all retracted)
            GameObject segment = Instantiate(tongueSegmentPrefab, startPos, Quaternion.identity);

            ConfigureTonguePhysics(segment);

            RopeSegment tongueScript = segment.GetComponent<RopeSegment>();
            if (tongueScript == null)
                tongueScript = segment.AddComponent<RopeSegment>();

            tongueSegmentObjects.Add(segment);
            tongueSegmentScripts.Add(tongueScript);

            // Connect segments
            if (i == 0)
            {
                tongueScript.ConnectToSegment(anchorRigidbody, springForce, springDamper);
            }
            else
            {
                Rigidbody previousRb = tongueSegmentObjects[i - 1].GetComponent<Rigidbody>();
                tongueScript.ConnectToSegment(previousRb, springForce, springDamper);
            }

            tongueScript.SetJointDistance(segmentLength);

            // Initially disable segments (tongue retracted)
            segment.SetActive(false);
        }
    }

    void ConfigureTonguePhysics(GameObject segment)
    {
        int tongueLayer = LayerMask.NameToLayer("Rope");
        if (tongueLayer == -1) tongueLayer = 0;
        segment.layer = tongueLayer;

        // Configure the LineRenderer on this segment
        LineRenderer segmentLineRenderer = segment.GetComponent<LineRenderer>();
        if (segmentLineRenderer != null)
        {
            // Set up the LineRenderer for this segment
            segmentLineRenderer.useWorldSpace = true;
            segmentLineRenderer.startWidth = tongueWidth;
            segmentLineRenderer.endWidth = tongueWidth;
            segmentLineRenderer.positionCount = 2; // Start and end point

            // Use the tongue material
            if (tongueMaterial != null)
            {
                segmentLineRenderer.material = tongueMaterial;
            }
            else
            {
                // Create default material
                Material defaultMat = new Material(Shader.Find("Standard"));
                defaultMat.color = Color.green;
                segmentLineRenderer.material = defaultMat;
            }

            segmentLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            segmentLineRenderer.receiveShadows = false;
            segmentLineRenderer.sortingOrder = 100;

            // Initially disable it
            segmentLineRenderer.enabled = false;
        }
        else
        {
            Debug.LogWarning($"No LineRenderer found on segment {segment.name}");
        }

        // Add collider
        Collider segmentCollider = segment.GetComponent<Collider>();
        if (segmentCollider == null)
        {
            CapsuleCollider cap = segment.AddComponent<CapsuleCollider>();
            cap.radius = tongueWidth * 0.5f;
            cap.height = segmentLength;
            cap.isTrigger = false;
        }

        // Ignore collisions with player
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer != -1)
        {
            Physics.IgnoreLayerCollision(tongueLayer, playerLayer, true);
        }
    }



    void Update()
    {
        HandleInput();
        UpdateAnchorPosition();
        UpdateTongueState();
        UpdateVisualTongue();
    }

    void UpdateAnchorPosition()
    {
        if (tongueAnchor != null)
            anchorObject.transform.position = tongueAnchor.position;
        else
            anchorObject.transform.position = transform.position + Vector3.up * 1.2f + transform.forward * 0.4f;
    }

    void HandleInput()
    {
        // Extend tongue in direction player is facing
        if (Input.GetKeyDown(extendKey) && currentState == TongueState.Retracted)
        {
            ExtendTongue();
        }

        // Grab mushroom when it's close enough
        if (Input.GetKeyDown(grabKey) && currentState == TongueState.Attached)
        {
            GrabAttachedTarget();
        }
    }

    void ExtendTongue()
    {
        currentState = TongueState.Extending;

        // Tongue extends in the direction the player is facing
        tongueDirection = playerTransform.forward;
        currentTongueLength = 0f;
        activeSegments = 0;

        Debug.Log("Frog tongue extending...");
    }

    void UpdateTongueState()
    {
        switch (currentState)
        {
            case TongueState.Extending:
                HandleTongueExtension();
                break;

            case TongueState.Attached:
                HandleAttachedState();
                break;

            case TongueState.Retracting:
                HandleTongueRetraction();
                break;
        }
    }

    void HandleTongueExtension()
    {
        // Rapidly extend tongue
        currentTongueLength += extendSpeed * Time.deltaTime;

        int segmentsNeeded = Mathf.Min(Mathf.FloorToInt(currentTongueLength / segmentLength), tongueSegments);

        // Activate segments as tongue extends
        for (int i = activeSegments; i < segmentsNeeded; i++)
        {
            if (i < tongueSegmentObjects.Count)
            {
                tongueSegmentObjects[i].SetActive(true);

                // Position segment along tongue direction
                Vector3 segmentPos = anchorObject.transform.position + tongueDirection * (i + 1) * segmentLength;
                tongueSegmentObjects[i].transform.position = segmentPos;
            }
        }
        activeSegments = segmentsNeeded;

        // Check for target hit
        CheckForTargetHit();

        // Stop extending at max length
        if (currentTongueLength >= maxTongueLength)
        {
            currentState = TongueState.Retracting;
            Debug.Log("Tongue missed - retracting");
        }
    }

    void CheckForTargetHit()
    {
        if (activeSegments > 0)
        {
            Vector3 tongueEnd = anchorObject.transform.position + tongueDirection * currentTongueLength;

            Collider[] nearbyObjects = Physics.OverlapSphere(tongueEnd, attachRange, catchableLayer);

            foreach (var obj in nearbyObjects)
            {
                MushroomAI mushroom = obj.GetComponent<MushroomAI>();
                if (mushroom != null && !mushroom.IsTongueGrabbed())
                {
                    AttachToTarget(obj.gameObject);
                    return;
                }
            }
        }
    }

    void AttachToTarget(GameObject target)
    {
        attachedTarget = target;
        attachedMushroomAI = target.GetComponent<MushroomAI>();
        currentState = TongueState.Attached;

        // Put mushroom in TongueGrabbed state
        if (attachedMushroomAI != null)
        {
            attachedMushroomAI.SetTongueGrabbed(true);
            Debug.Log($"Mushroom {target.name} is now tongue-grabbed!");
        }

        // springwrap
        if (activeSegments > 0)
        {
            GameObject tongueTip = tongueSegmentObjects[activeSegments - 1];
            attachmentJoint = tongueTip.AddComponent<SpringJoint>();

            Rigidbody targetRb = target.GetComponent<Rigidbody>();
            if (targetRb == null)
                targetRb = target.AddComponent<Rigidbody>();

            attachmentJoint.connectedBody = targetRb;
            attachmentJoint.spring = springForce * 1.5f;
            attachmentJoint.damper = springDamper;
            attachmentJoint.autoConfigureConnectedAnchor = true;
        }

        Debug.Log($"Tongue attached to {target.name}! Press E to grab it.");
    }

    void HandleAttachedState()
    {
        if (attachedTarget == null)
        {
            ReleaseMushroom();
            currentState = TongueState.Retracting;
            return;
        }

        // tongue extend and maintain tension
        // todo todo

        // Slowly reel in the target using the spring joint tension
        if (attachmentJoint != null && activeSegments > 0)
        {
            for (int i = activeSegments - 1; i >= 0; i--)
            {
                if (tongueSegmentObjects[i] != null)
                {
                    Rigidbody segmentRb = tongueSegmentObjects[i].GetComponent<Rigidbody>();
                    if (segmentRb != null)
                    {
                        Vector3 directionToAnchor = (anchorObject.transform.position - segmentRb.position).normalized;
                        segmentRb.AddForce(directionToAnchor * retractSpeed * 0.5f, ForceMode.Force);
                    }
                }
            }
        }

        // Check if target is close enough to grab
        if (attachedTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(attachedTarget.transform.position, transform.position);
            if (distanceToPlayer < 2f)
            {
                Debug.Log("Mushroom is close! Press E to grab it.");
            }
        }
    }

    void GrabAttachedTarget()
    {
        if (attachedTarget != null)
        {
            Debug.Log($"Grabbed {attachedTarget.name}!");

            // Trigger mushroom collection
            if (attachedMushroomAI != null)
            {
                attachedMushroomAI.ChangeState(MushroomState.Collected);
            }

            ReleaseMushroom();
        }

        // Start retracting tongue
        currentState = TongueState.Retracting;
    }

    void ReleaseMushroom()
    {
        if (attachedMushroomAI != null)
        {
            attachedMushroomAI.SetTongueGrabbed(false);
            attachedMushroomAI = null;
        }

        if (attachmentJoint != null)
        {
            Destroy(attachmentJoint);
            attachmentJoint = null;
        }

        attachedTarget = null;
    }

    void HandleTongueRetraction()
    {
        if (attachedTarget != null)
        {
            ReleaseMushroom();
        }

        // retract tongue
        currentTongueLength -= retractSpeed * 3f * Time.deltaTime;

        int segmentsNeeded = Mathf.Max(0, Mathf.FloorToInt(currentTongueLength / segmentLength));

        // Deactivate segments as tongue retracts
        for (int i = activeSegments - 1; i >= segmentsNeeded; i--)
        {
            if (i >= 0 && i < tongueSegmentObjects.Count)
            {
                tongueSegmentObjects[i].SetActive(false);
            }
        }
        activeSegments = segmentsNeeded;

        // Fully retracted
        if (currentTongueLength <= 0)
        {
            currentState = TongueState.Retracted;
            currentTongueLength = 0f;
            activeSegments = 0;
            Debug.Log("Tongue retracted");
        }
    }

    void UpdateVisualTongue()
    {
        // Update individual segment LineRenderers
        for (int i = 0; i < tongueSegmentObjects.Count; i++)
        {
            if (tongueSegmentObjects[i] != null)
            {
                LineRenderer segmentLineRenderer = tongueSegmentObjects[i].GetComponent<LineRenderer>();

                if (segmentLineRenderer != null)
                {
                    bool isActive = i < activeSegments && tongueSegmentObjects[i].activeInHierarchy;
                    segmentLineRenderer.enabled = isActive;

                    if (isActive)
                    {
                        // Set the line from this segment to the next (or to anchor for first segment)
                        Vector3 startPos, endPos;

                        if (i == 0)
                        {
                            // First segment connects to anchor
                            startPos = anchorObject.transform.position;
                            endPos = tongueSegmentObjects[i].transform.position;
                        }
                        else
                        {
                            // Connect to previous segment
                            startPos = tongueSegmentObjects[i - 1].transform.position;
                            endPos = tongueSegmentObjects[i].transform.position;
                        }

                        segmentLineRenderer.positionCount = 2;
                        segmentLineRenderer.SetPosition(0, startPos);
                        segmentLineRenderer.SetPosition(1, endPos);
                    }
                }
            }
        }

        // Also update the main LineRenderer for overall tongue visualization
        List<Vector3> positions = new List<Vector3>();
        if (activeSegments > 0)
        {
            positions.Add(anchorObject.transform.position);

            for (int i = 0; i < activeSegments && i < tongueSegmentObjects.Count; i++)
            {
                if (tongueSegmentObjects[i].activeInHierarchy)
                {
                    positions.Add(tongueSegmentObjects[i].transform.position);
                }
            }
        }

        tongueRenderer.positionCount = positions.Count;
        if (positions.Count > 1)
        {
            tongueRenderer.SetPositions(positions.ToArray());
            tongueRenderer.enabled = true;
        }
        else
        {
            tongueRenderer.enabled = false;
        }
    }



    void DestroyTongue()
    {
        foreach (var segment in tongueSegmentObjects)
        {
            if (segment != null)
                Destroy(segment);
        }

        tongueSegmentObjects.Clear();
        tongueSegmentScripts.Clear();
    }

    void OnDestroy()
    {
        if (attachedTarget != null)
        {
            ReleaseMushroom();
        }

        DestroyTongue();
        if (anchorObject != null)
            Destroy(anchorObject);
    }

    // debuyg
    void OnDrawGizmos()
    {
        if (currentState == TongueState.Extending || currentState == TongueState.Attached)
        {
            Gizmos.color = Color.green;
            Vector3 tongueEnd = anchorObject.transform.position + tongueDirection * currentTongueLength;
            Gizmos.DrawWireSphere(tongueEnd, attachRange);

            Gizmos.color = Color.red;
            Gizmos.DrawRay(anchorObject.transform.position, tongueDirection * maxTongueLength);
        }

        if (currentState == TongueState.Attached && attachedTarget != null)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(anchorObject.transform.position, attachedTarget.transform.position);
        }
    }
}
