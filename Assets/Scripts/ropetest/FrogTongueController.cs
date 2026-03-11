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

        int tongueLayer = LayerMask.NameToLayer("Rope");
        int playerLayer = LayerMask.NameToLayer("Player");

        if (tongueLayer != -1 && playerLayer != -1)
            Physics.IgnoreLayerCollision(tongueLayer, playerLayer, true);
    }

    void SetupTongueAnchor()
    {
        anchorObject = new GameObject("TongueAnchor");
        anchorObject.transform.SetParent(transform);

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
        if (tongueRenderer != null)
            DestroyImmediate(tongueRenderer);

        tongueRenderer = gameObject.AddComponent<LineRenderer>();
        tongueRenderer.useWorldSpace = true;
        tongueRenderer.startWidth = tongueWidth;
        tongueRenderer.endWidth = tongueWidth * 0.7f;
        tongueRenderer.positionCount = 0;

        if (tongueMaterial != null)
        {
            tongueRenderer.material = tongueMaterial;
        }
        else
        {
            Debug.LogWarning("No tongue material assigned, creating default material");
            Material defaultMat = new Material(Shader.Find("Sprites/Default"));
            if (defaultMat.shader == null)
                defaultMat = new Material(Shader.Find("Standard"));
            defaultMat.color = Color.red;
            tongueRenderer.material = defaultMat;
            tongueMaterial = defaultMat;
        }

        tongueRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        tongueRenderer.receiveShadows = false;
        tongueRenderer.sortingOrder = 100;
        tongueRenderer.enabled = true;
    }

    void CreateTongue()
    {
        DestroyTongue();
        currentTongueLength = 0f;
        activeSegments = 0;

        Vector3 startPos = anchorObject.transform.position;

        for (int i = 0; i < tongueSegments; i++)
        {
            GameObject segment = Instantiate(tongueSegmentPrefab, startPos, Quaternion.identity);
            ConfigureTonguePhysics(segment);

            RopeSegment tongueScript = segment.GetComponent<RopeSegment>();
            if (tongueScript == null)
                tongueScript = segment.AddComponent<RopeSegment>();

            tongueSegmentObjects.Add(segment);
            tongueSegmentScripts.Add(tongueScript);

            if (i == 0)
                tongueScript.ConnectToSegment(anchorRigidbody, springForce, springDamper);
            else
            {
                Rigidbody previousRb = tongueSegmentObjects[i - 1].GetComponent<Rigidbody>();
                tongueScript.ConnectToSegment(previousRb, springForce, springDamper);
            }

            tongueScript.SetJointDistance(segmentLength);
            segment.SetActive(false);
        }
    }

    void ConfigureTonguePhysics(GameObject segment)
    {
        int tongueLayer = LayerMask.NameToLayer("Rope");
        if (tongueLayer == -1) tongueLayer = 0;
        segment.layer = tongueLayer;

        LineRenderer segmentLineRenderer = segment.GetComponent<LineRenderer>();
        if (segmentLineRenderer != null)
        {
            segmentLineRenderer.useWorldSpace = true;
            segmentLineRenderer.startWidth = tongueWidth;
            segmentLineRenderer.endWidth = tongueWidth;
            segmentLineRenderer.positionCount = 2;

            if (tongueMaterial != null)
                segmentLineRenderer.material = tongueMaterial;
            else
            {
                Material defaultMat = new Material(Shader.Find("Standard"));
                defaultMat.color = Color.green;
                segmentLineRenderer.material = defaultMat;
            }

            segmentLineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            segmentLineRenderer.receiveShadows = false;
            segmentLineRenderer.sortingOrder = 100;
            segmentLineRenderer.enabled = false;
        }

        Collider segmentCollider = segment.GetComponent<Collider>();
        if (segmentCollider == null)
        {
            CapsuleCollider cap = segment.AddComponent<CapsuleCollider>();
            cap.radius = tongueWidth * 0.5f;
            cap.height = segmentLength;
            cap.isTrigger = false;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer != -1)
            Physics.IgnoreLayerCollision(tongueLayer, playerLayer, true);
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
        if (Input.GetKeyDown(extendKey) && currentState == TongueState.Retracted)
            ExtendTongue();

        if (Input.GetKeyDown(grabKey) && currentState == TongueState.Attached)
            GrabAttachedTarget();
    }

    void ExtendTongue()
    {
        currentState = TongueState.Extending;
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
        currentTongueLength += extendSpeed * Time.deltaTime;

        int segmentsNeeded = Mathf.Min(
            Mathf.FloorToInt(currentTongueLength / segmentLength), tongueSegments);

        for (int i = activeSegments; i < segmentsNeeded; i++)
        {
            if (i < tongueSegmentObjects.Count)
            {
                tongueSegmentObjects[i].SetActive(true);
                Vector3 segmentPos = anchorObject.transform.position +
                                     tongueDirection * (i + 1) * segmentLength;
                tongueSegmentObjects[i].transform.position = segmentPos;
            }
        }
        activeSegments = segmentsNeeded;

        CheckForTargetHit();

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
            Vector3 tongueEnd = anchorObject.transform.position +
                                tongueDirection * currentTongueLength;

            Collider[] nearbyObjects = Physics.OverlapSphere(tongueEnd, attachRange, catchableLayer);
            foreach (var obj in nearbyObjects)
            {
                MushroomAI mushroom = obj.GetComponent<MushroomAI>();
                if (mushroom != null && !mushroom.IsTongueGrabbed())
                {
                    AttachToTarget(mushroom.gameObject);
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

        if (attachedMushroomAI != null)
        {
            attachedMushroomAI.SetTongueGrabbed(true);
            Debug.Log($"Mushroom {target.name} is now tongue-grabbed!");
        }

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

        if (attachmentJoint != null && activeSegments > 0)
        {
            for (int i = activeSegments - 1; i >= 0; i--)
            {
                if (tongueSegmentObjects[i] != null)
                {
                    Rigidbody segmentRb = tongueSegmentObjects[i].GetComponent<Rigidbody>();
                    if (segmentRb != null)
                    {
                        Vector3 directionToAnchor =
                            (anchorObject.transform.position - segmentRb.position).normalized;
                        segmentRb.AddForce(directionToAnchor * retractSpeed * 0.5f, ForceMode.Force);
                    }
                }
            }
        }

        if (attachedTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(
                attachedTarget.transform.position, transform.position);
            if (distanceToPlayer < 2f)
                Debug.Log("Mushroom is close! Press E to grab it.");
        }
    }

    void GrabAttachedTarget()
    {
        if (attachedTarget != null)
        {
            Debug.Log($"Grabbed {attachedTarget.name}!");
            if (attachedMushroomAI != null)
                attachedMushroomAI.ChangeState(MushroomState.Collected);

            ReleaseMushroom();
        }

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
            ReleaseMushroom();

        currentTongueLength -= retractSpeed * 3f * Time.deltaTime;

        int segmentsNeeded = Mathf.Max(
            0, Mathf.FloorToInt(currentTongueLength / segmentLength));

        for (int i = 0; i < segmentsNeeded; i++)
        {
            if (tongueSegmentObjects[i] != null)
            {
                // Reposition each segment so its place in the chain reflects the
                // current (shrinking) tongue length, not its original extend position.
                Vector3 targetPos = anchorObject.transform.position +
                                    tongueDirection * (i + 1) * segmentLength;

                Rigidbody segmentRb = tongueSegmentObjects[i].GetComponent<Rigidbody>();
                if (segmentRb != null)
                {
                    // MovePosition respects physics interpolation and looks smooth.
                    segmentRb.MovePosition(Vector3.MoveTowards(
                        segmentRb.position,
                        targetPos,
                        retractSpeed * 3f * Time.deltaTime));
                }
                else
                {
                    tongueSegmentObjects[i].transform.position = Vector3.MoveTowards(
                        tongueSegmentObjects[i].transform.position,
                        targetPos,
                        retractSpeed * 3f * Time.deltaTime);
                }
            }
        }

        // Deactivate segments that are no longer part of the tongue length
        for (int i = activeSegments - 1; i >= segmentsNeeded; i--)
        {
            if (i >= 0 && i < tongueSegmentObjects.Count)
                tongueSegmentObjects[i].SetActive(false);
        }
        activeSegments = segmentsNeeded;

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
        for (int i = 0; i < tongueSegmentObjects.Count; i++)
        {
            if (tongueSegmentObjects[i] == null) continue;

            LineRenderer segmentLR = tongueSegmentObjects[i].GetComponent<LineRenderer>();
            if (segmentLR == null) continue;

            bool isActive = i < activeSegments && tongueSegmentObjects[i].activeInHierarchy;
            segmentLR.enabled = isActive;

            if (isActive)
            {
                Vector3 startPos = i == 0
                    ? anchorObject.transform.position
                    : tongueSegmentObjects[i - 1].transform.position;

                segmentLR.positionCount = 2;
                segmentLR.SetPosition(0, startPos);
                segmentLR.SetPosition(1, tongueSegmentObjects[i].transform.position);
            }
        }

        List<Vector3> positions = new List<Vector3>();
        if (activeSegments > 0)
        {
            positions.Add(anchorObject.transform.position);
            for (int i = 0; i < activeSegments && i < tongueSegmentObjects.Count; i++)
            {
                if (tongueSegmentObjects[i].activeInHierarchy)
                    positions.Add(tongueSegmentObjects[i].transform.position);
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
            ReleaseMushroom();

        DestroyTongue();
        if (anchorObject != null)
            Destroy(anchorObject);
    }

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