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
    public float targetPullForce = 25f;

    [Header("Terrain Collision")]
    [Tooltip("Layers treated as terrain/ground for anti-clipping correction.")]
    public LayerMask terrainCollisionMask;
    [Tooltip("Height above the point used when probing terrain with a downward ray.")]
    public float terrainProbeHeight = 20f;
    [Tooltip("Small offset above terrain to avoid z-fighting/penetration.")]
    public float terrainClearance = 0.04f;

    [Header("Input")]
    public KeyCode extendKey = KeyCode.Space;
    public KeyCode grabKey = KeyCode.Space;
    [Tooltip("If enabled, this controller ignores direct key polling and expects a unified input router to call TryUnifiedTongueAction().")]
    public bool useUnifiedTongueInput = true;

    [Header("Attached Controls")]
    public int reelMouseButton = 0; // 0 = Left Mouse Button
    public int releaseMouseButton = 1; // 1 = Right Mouse Button

    [Header("Visual")]
    public Material tongueMaterial;

    [Header("Wrap Visual")]
    public bool enableWrapVisual = true;
    [Range(8, 64)] public int wrapArcSegments = 20;
    [Range(0.1f, 1.5f)] public float wrapRadiusMultiplier = 0.6f;
    public float minWrapRadius = 0.12f;
    public float maxWrapRadius = 1.4f;
    public float wrapRingWidth = 0.05f;
    public bool debugWrapGizmos = false;

    [Header("Anchor Point")]
    public Transform tongueAnchor; // Frog's mouth position

    [Header("Character Visuals")]
    [Tooltip("Character transform to rotate while attached so the mouth faces the mushroom/ring contact.")]
    public Transform characterVisual;
    public float characterRotateSpeed = 8f;
    public Vector3 visualRotationOffset = Vector3.zero;

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
    private Rigidbody attachedTargetRigidbody;
    private bool attachedTargetWasKinematic;
    private bool attachedTargetUsedGravity;
    private bool hasAttachedTargetPhysicsState;
    private SpringJoint attachmentJoint;
    private Vector3 tongueDirection;
    private float currentTongueLength;
    private int activeSegments;

    // Visual-only ring wrap state around attached targets.
    private bool wrapVisualActive;
    private Vector3 wrapCenter;
    private Vector3 wrapNormal;
    private Vector3 wrapAxisX;
    private Vector3 wrapAxisY;
    private float wrapRadius;
    private LineRenderer wrapRingRenderer;
    private bool hasWrapContactPoint;
    private Vector3 wrapContactWorldPoint;
    private Quaternion preAttachLocalRotation;
    private bool restoringRotation;

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

        SetupWrapRingRenderer();
    }

    void SetupWrapRingRenderer()
    {
        if (wrapRingRenderer != null)
            return;

        GameObject ringObj = new GameObject("TongueWrapRingVisual");
        ringObj.transform.SetParent(transform);

        wrapRingRenderer = ringObj.AddComponent<LineRenderer>();
        wrapRingRenderer.useWorldSpace = true;
        wrapRingRenderer.loop = true;
        wrapRingRenderer.positionCount = 0;
        wrapRingRenderer.startWidth = wrapRingWidth;
        wrapRingRenderer.endWidth = wrapRingWidth;
        wrapRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        wrapRingRenderer.receiveShadows = false;
        wrapRingRenderer.sortingOrder = 101;
        wrapRingRenderer.enabled = false;

        if (tongueMaterial != null)
            wrapRingRenderer.material = tongueMaterial;
        else
            wrapRingRenderer.material = tongueRenderer.material;
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
        UpdateCharacterRotation();
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
        if (useUnifiedTongueInput)
            return;

        if (Input.GetKeyDown(extendKey) && currentState == TongueState.Retracted)
            ExtendTongue();

        if (Input.GetKeyDown(grabKey) && currentState == TongueState.Attached)
            GrabAttachedTarget();

        if (currentState == TongueState.Attached && Input.GetMouseButtonDown(releaseMouseButton))
        {
            ReleaseMushroom();
            currentState = TongueState.Retracting;
        }
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
                segmentPos = KeepPointAboveTerrain(segmentPos);
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

        if (characterVisual != null)
        {
            preAttachLocalRotation = characterVisual.localRotation;
            restoringRotation = false;
        }

        if (attachedMushroomAI != null)
        {
            attachedMushroomAI.SetTongueGrabbed(true);
            Debug.Log($"Mushroom {target.name} is now tongue-grabbed!");
        }

        if (activeSegments > 0)
        {
            GameObject tongueTip = tongueSegmentObjects[activeSegments - 1];
            attachmentJoint = tongueTip.AddComponent<SpringJoint>();

            attachedTargetRigidbody = target.GetComponent<Rigidbody>();
            if (attachedTargetRigidbody == null)
                attachedTargetRigidbody = target.AddComponent<Rigidbody>();

            CacheAndPrepareAttachedTargetPhysics(attachedTargetRigidbody);

            attachmentJoint.connectedBody = attachedTargetRigidbody;
            attachmentJoint.spring = springForce * 1.5f;
            attachmentJoint.damper = springDamper;
            attachmentJoint.autoConfigureConnectedAnchor = false;
            attachmentJoint.anchor = Vector3.zero;
            attachmentJoint.connectedAnchor = Vector3.zero;
        }

        InitializeWrapVisual(target);
        UpdateAttachmentJointToWrapContact();

        Debug.Log($"Tongue attached to {target.name}! Press Space to grab it.");
    }

    void HandleAttachedState()
    {
        if (attachedTarget == null)
        {
            ReleaseMushroom();
            currentState = TongueState.Retracting;
            return;
        }

        UpdateWrapVisual();
        UpdateAttachmentJointToWrapContact();

        bool isReelHeld = Input.GetMouseButton(reelMouseButton);

        if (isReelHeld && attachmentJoint != null && activeSegments > 0)
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
                        KeepRigidbodyAboveTerrain(segmentRb);
                    }
                }
            }
        }

        if (isReelHeld && attachedTargetRigidbody != null)
        {
            Vector3 toAnchor = anchorObject.transform.position - attachedTargetRigidbody.position;
            attachedTargetRigidbody.AddForce(toAnchor.normalized * targetPullForce, ForceMode.Acceleration);
            KeepRigidbodyAboveTerrain(attachedTargetRigidbody);
        }

        if (attachedTarget != null)
        {
            float distanceToPlayer = Vector3.Distance(
                attachedTarget.transform.position, transform.position);
            if (distanceToPlayer < 2f)
                Debug.Log("Mushroom is close! Press Space to grab it.");
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

        RestoreAttachedTargetPhysics();
        attachedTargetRigidbody = null;

        attachedTarget = null;
        ClearWrapVisual();

        if (characterVisual != null)
            restoringRotation = true;
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
                targetPos = KeepPointAboveTerrain(targetPos);

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

                startPos = KeepPointAboveTerrain(startPos);
                Vector3 endPos = KeepPointAboveTerrain(tongueSegmentObjects[i].transform.position);

                // Force the final visual segment to terminate exactly at the wrap
                // contact point so it does not visually split from the ring.
                if (currentState == TongueState.Attached && i == activeSegments - 1 && hasWrapContactPoint)
                    endPos = KeepPointAboveTerrain(wrapContactWorldPoint);

                segmentLR.positionCount = 2;
                segmentLR.SetPosition(0, startPos);
                segmentLR.SetPosition(1, endPos);
            }
        }

        List<Vector3> positions = new List<Vector3>();
        if (activeSegments > 0)
        {
            positions.Add(KeepPointAboveTerrain(anchorObject.transform.position));
            for (int i = 0; i < activeSegments && i < tongueSegmentObjects.Count; i++)
            {
                if (tongueSegmentObjects[i].activeInHierarchy)
                    positions.Add(KeepPointAboveTerrain(tongueSegmentObjects[i].transform.position));
            }

            if (currentState == TongueState.Attached && hasWrapContactPoint)
                positions.Add(KeepPointAboveTerrain(wrapContactWorldPoint));
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

    void InitializeWrapVisual(GameObject target)
    {
        wrapVisualActive = false;
        hasWrapContactPoint = false;
        SetupWrapRingRenderer();

        if (!enableWrapVisual || target == null)
        {
            if (wrapRingRenderer != null)
                wrapRingRenderer.enabled = false;
            return;
        }

        if (!TryGetWrapCenter(target, out wrapCenter))
            return;

        Vector3 targetUp = target.transform.up;
        wrapNormal = targetUp.sqrMagnitude > 0.0001f ? targetUp.normalized : Vector3.up;

        Bounds visualBounds;
        if (TryGetVisualBounds(target, out visualBounds))
            wrapRadius = ComputeWrapRadiusFromBounds(visualBounds, wrapCenter, wrapNormal);
        else
            wrapRadius = minWrapRadius;

        wrapRadius = Mathf.Clamp(wrapRadius, minWrapRadius, maxWrapRadius);

        Vector3 tipPos = GetCurrentTongueTipPosition();
        Vector3 toTipOnPlane = Vector3.ProjectOnPlane(tipPos - wrapCenter, wrapNormal);
        if (toTipOnPlane.sqrMagnitude < 0.0001f)
            toTipOnPlane = Vector3.ProjectOnPlane(target.transform.right, wrapNormal);
        if (toTipOnPlane.sqrMagnitude < 0.0001f)
            toTipOnPlane = Vector3.ProjectOnPlane(Vector3.forward, wrapNormal);

        wrapAxisX = toTipOnPlane.normalized;
        wrapAxisY = Vector3.Cross(wrapNormal, wrapAxisX).normalized;

        if (wrapAxisX.sqrMagnitude < 0.0001f || wrapAxisY.sqrMagnitude < 0.0001f)
            return;

        wrapVisualActive = true;
        RenderWrapRing();
    }

    void UpdateWrapVisual()
    {
        if (!wrapVisualActive || attachedTarget == null)
            return;

        if (!TryGetWrapCenter(attachedTarget, out wrapCenter))
        {
            wrapVisualActive = false;
            return;
        }

        Vector3 targetUp = attachedTarget.transform.up;
        wrapNormal = targetUp.sqrMagnitude > 0.0001f ? targetUp.normalized : Vector3.up;

        Bounds visualBounds;
        if (TryGetVisualBounds(attachedTarget, out visualBounds))
        {
            float computedRadius = ComputeWrapRadiusFromBounds(visualBounds, wrapCenter, wrapNormal);
            wrapRadius = Mathf.Clamp(computedRadius, minWrapRadius, maxWrapRadius);
        }

        Vector3 tipPos = GetCurrentTongueTipPosition();
        Vector3 toTipOnPlane = Vector3.ProjectOnPlane(tipPos - wrapCenter, wrapNormal);
        if (toTipOnPlane.sqrMagnitude > 0.0001f)
        {
            wrapAxisX = toTipOnPlane.normalized;
            wrapAxisY = Vector3.Cross(wrapNormal, wrapAxisX).normalized;
        }

        wrapContactWorldPoint = GetWrapContactPoint(GetCurrentTongueTipPosition());
        hasWrapContactPoint = true;

        RenderWrapRing();
    }

    void ClearWrapVisual()
    {
        wrapVisualActive = false;
        wrapCenter = Vector3.zero;
        wrapNormal = Vector3.up;
        wrapAxisX = Vector3.right;
        wrapAxisY = Vector3.forward;
        wrapRadius = minWrapRadius;
        hasWrapContactPoint = false;
        wrapContactWorldPoint = Vector3.zero;

        if (wrapRingRenderer != null)
        {
            wrapRingRenderer.positionCount = 0;
            wrapRingRenderer.enabled = false;
        }
    }

    bool TryGetWrapCenter(GameObject target, out Vector3 center)
    {
        Bounds bounds;
        if (TryGetVisualBounds(target, out bounds))
        {
            center = bounds.center;
            return true;
        }

        center = target.transform.position;
        return true;
    }

    bool TryGetVisualBounds(GameObject target, out Bounds bounds)
    {
        Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        bounds = new Bounds(target.transform.position, Vector3.zero);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        if (hasBounds)
            return true;

        Collider[] colliders = target.GetComponentsInChildren<Collider>();
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider col = colliders[i];
            if (col == null || !col.enabled)
                continue;

            if (!hasBounds)
            {
                bounds = col.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return hasBounds;
    }

    float ComputeWrapRadiusFromBounds(Bounds bounds, Vector3 center, Vector3 planeNormal)
    {
        Vector3 ext = bounds.extents;
        Vector3 boundsCenter = bounds.center;
        Vector3[] corners = new Vector3[8]
        {
            boundsCenter + new Vector3( ext.x,  ext.y,  ext.z),
            boundsCenter + new Vector3( ext.x,  ext.y, -ext.z),
            boundsCenter + new Vector3( ext.x, -ext.y,  ext.z),
            boundsCenter + new Vector3( ext.x, -ext.y, -ext.z),
            boundsCenter + new Vector3(-ext.x,  ext.y,  ext.z),
            boundsCenter + new Vector3(-ext.x,  ext.y, -ext.z),
            boundsCenter + new Vector3(-ext.x, -ext.y,  ext.z),
            boundsCenter + new Vector3(-ext.x, -ext.y, -ext.z)
        };

        float maxProjectedDistance = 0f;
        for (int i = 0; i < corners.Length; i++)
        {
            Vector3 onPlane = Vector3.ProjectOnPlane(corners[i] - center, planeNormal);
            float dist = onPlane.magnitude;
            if (dist > maxProjectedDistance)
                maxProjectedDistance = dist;
        }

        return maxProjectedDistance * wrapRadiusMultiplier;
    }

    Vector3 GetCurrentTongueTipPosition()
    {
        if (activeSegments > 0 && activeSegments - 1 < tongueSegmentObjects.Count)
        {
            GameObject tip = tongueSegmentObjects[activeSegments - 1];
            if (tip != null)
                return tip.transform.position;
        }

        return anchorObject != null ? anchorObject.transform.position : transform.position;
    }

    Vector3 GetWrapContactPoint(Vector3 referenceWorldPos)
    {
        if (!wrapVisualActive)
            return wrapCenter;

        Vector3 toRefOnPlane = Vector3.ProjectOnPlane(referenceWorldPos - wrapCenter, wrapNormal);
        if (toRefOnPlane.sqrMagnitude < 0.0001f)
            toRefOnPlane = wrapAxisX;

        Vector3 dir = toRefOnPlane.normalized;
        return KeepPointAboveTerrain(wrapCenter + dir * wrapRadius);
    }

    void UpdateAttachmentJointToWrapContact()
    {
        if (attachmentJoint == null || attachedTargetRigidbody == null)
            return;

        Vector3 tipPos = GetCurrentTongueTipPosition();

        if (wrapVisualActive)
        {
            wrapContactWorldPoint = GetWrapContactPoint(tipPos);
            hasWrapContactPoint = true;

            attachmentJoint.autoConfigureConnectedAnchor = false;
            Vector3 safeContact = KeepPointAboveTerrain(wrapContactWorldPoint);
            wrapContactWorldPoint = safeContact;
            attachmentJoint.connectedAnchor = attachedTargetRigidbody.transform.InverseTransformPoint(safeContact);
        }
        else
        {
            hasWrapContactPoint = false;
            attachmentJoint.autoConfigureConnectedAnchor = true;
        }
    }

    void RenderWrapRing()
    {
        if (wrapRingRenderer == null)
            return;

        if (!enableWrapVisual || !wrapVisualActive || currentState != TongueState.Attached)
        {
            wrapRingRenderer.enabled = false;
            return;
        }

        if (wrapArcSegments < 3 || wrapRadius <= 0f)
        {
            wrapRingRenderer.enabled = false;
            return;
        }

        int ringPoints = wrapArcSegments;
        wrapRingRenderer.positionCount = ringPoints;
        wrapRingRenderer.startWidth = wrapRingWidth;
        wrapRingRenderer.endWidth = wrapRingWidth;
        wrapRingRenderer.enabled = true;

        for (int i = 0; i < ringPoints; i++)
        {
            float angle = (Mathf.PI * 2f * i) / ringPoints;
            Vector3 ringPoint = wrapCenter +
                                (wrapAxisX * Mathf.Cos(angle) + wrapAxisY * Mathf.Sin(angle)) * wrapRadius;
            wrapRingRenderer.SetPosition(i, KeepPointAboveTerrain(ringPoint));
        }

        // Snap one ring vertex directly to the active contact point so the
        // tongue endpoint and ring share the exact same world position.
        if (hasWrapContactPoint)
            wrapRingRenderer.SetPosition(0, KeepPointAboveTerrain(wrapContactWorldPoint));
    }

    Vector3 KeepPointAboveTerrain(Vector3 point)
    {
        if (terrainCollisionMask.value == 0)
            return point;

        float castDistance = terrainProbeHeight * 2f + 2f;
        Vector3 castOrigin = point + Vector3.up * terrainProbeHeight;

        if (Physics.Raycast(castOrigin, Vector3.down, out RaycastHit hit, castDistance,
                            terrainCollisionMask, QueryTriggerInteraction.Ignore))
        {
            float minY = hit.point.y + terrainClearance;
            if (point.y < minY)
                point.y = minY;
        }

        return point;
    }

    void KeepRigidbodyAboveTerrain(Rigidbody rb)
    {
        if (rb == null)
            return;

        Vector3 corrected = KeepPointAboveTerrain(rb.position);
        if (corrected.y <= rb.position.y)
            return;

        rb.position = corrected;
        if (rb.linearVelocity.y < 0f)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
    }

    void CacheAndPrepareAttachedTargetPhysics(Rigidbody targetRb)
    {
        if (targetRb == null)
            return;

        attachedTargetWasKinematic = targetRb.isKinematic;
        attachedTargetUsedGravity = targetRb.useGravity;
        hasAttachedTargetPhysicsState = true;

        targetRb.isKinematic = false;
        targetRb.useGravity = true;
    }

    void RestoreAttachedTargetPhysics()
    {
        if (!hasAttachedTargetPhysicsState || attachedTargetRigidbody == null)
        {
            hasAttachedTargetPhysicsState = false;
            return;
        }

        attachedTargetRigidbody.isKinematic = attachedTargetWasKinematic;
        attachedTargetRigidbody.useGravity = attachedTargetUsedGravity;
        hasAttachedTargetPhysicsState = false;
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

    public bool CanStartUnifiedAction()
    {
        return currentState == TongueState.Retracted;
    }

    public bool TryUnifiedTongueAction()
    {
        if (currentState == TongueState.Attached)
        {
            GrabAttachedTarget();
            return true;
        }

        if (CanStartUnifiedAction())
        {
            ExtendTongue();
            return true;
        }

        return false;
    }

    void OnDestroy()
    {
        if (attachedTarget != null)
            ReleaseMushroom();

        DestroyTongue();
        if (anchorObject != null)
            Destroy(anchorObject);
        if (wrapRingRenderer != null)
            Destroy(wrapRingRenderer.gameObject);
    }

    void UpdateCharacterRotation()
    {
        if (characterVisual == null)
            return;

        if (currentState == TongueState.Attached && attachedTarget != null)
        {
            restoringRotation = false;

            Vector3 lookPoint = hasWrapContactPoint ? wrapContactWorldPoint : attachedTarget.transform.position;
            Vector3 dirToTarget = (lookPoint - characterVisual.position).normalized;
            if (dirToTarget.sqrMagnitude < 0.0001f)
                return;

            Vector3 upRef = Mathf.Abs(dirToTarget.y) > 0.98f
                ? characterVisual.right
                : Vector3.up;

            Quaternion targetRot = Quaternion.LookRotation(dirToTarget, upRef)
                                 * Quaternion.Euler(visualRotationOffset);
            characterVisual.rotation = Quaternion.Slerp(
                characterVisual.rotation,
                targetRot,
                Time.deltaTime * characterRotateSpeed);
        }
        else if (restoringRotation)
        {
            characterVisual.localRotation = Quaternion.Slerp(
                characterVisual.localRotation,
                preAttachLocalRotation,
                Time.deltaTime * characterRotateSpeed);

            if (Quaternion.Angle(characterVisual.localRotation, preAttachLocalRotation) < 0.5f)
            {
                characterVisual.localRotation = preAttachLocalRotation;
                restoringRotation = false;
            }
        }
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

            if (debugWrapGizmos && wrapVisualActive)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(wrapCenter, 0.05f);

                int gizmoSteps = Mathf.Max(8, wrapArcSegments);
                Vector3 prev = wrapCenter + wrapAxisX * wrapRadius;
                for (int i = 1; i <= gizmoSteps; i++)
                {
                    float a = (Mathf.PI * 2f * i) / gizmoSteps;
                    Vector3 next = wrapCenter +
                                   (wrapAxisX * Mathf.Cos(a) + wrapAxisY * Mathf.Sin(a)) * wrapRadius;
                    Gizmos.DrawLine(prev, next);
                    prev = next;
                }

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(wrapCenter, wrapCenter + wrapNormal * (wrapRadius * 0.6f));
            }
        }
    }
}