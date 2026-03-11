using UnityEngine;
using System.Collections;

/// <summary>
/// Player-side swing grapple system that couples with one or more SwingGrappleZone
/// world markers to deliver a physics-authentic pendulum swing.
///
/// Setup:
///   - Add this component to the Player GameObject (alongside PlayerMotor,
///     OverheadController, and Rigidbody).
///   - Optionally assign ropeMaterial for the LineRenderer.
///   - In the scene, place SwingGrappleZone objects above any gappable areas.
///
/// Swing physics model:
///   A SpringJoint with high spring force approximates an inextensible rope while
///   still allowing Unity's Rigidbody gravity to drive the pendulum.  During a swing
///   the player can:
///     • Pump: WASD adds a tangential impulse to build swing energy.
///     • Reel: Mouse ScrollWheel (or Q/E) shortens / lengthens the rope, shifting the
///       swing radius and letting the player steer their apex height.
///     • Release: Press the grapple key (default F) again to detach at any moment.
///   On release the player's current Rigidbody velocity carries them through the air.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SwingGrappleSystem : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Input")]
    [Tooltip("Key to grab / release the rope.")]
    public KeyCode grappleKey = KeyCode.F;

    [Header("Detection")]
    [Tooltip("Scan radius used when searching for SwingGrappleZones.")]
    public float detectionRadius = 12f;

    [Header("Rope Physics")]
    [Tooltip("Multiplier applied to the grab-point-to-anchor distance to set the initial rope length. " +
             "Values > 1 give a slightly longer rope so the swing is more generous.")]
    public float ropeLengthMultiplier = 1.05f;

    [Tooltip("Minimum rope length the player can reel to.")]
    public float minRopeLength = 2f;

    [Tooltip("Speed at which the player can reel in/out using the scroll wheel.")]
    public float reelSpeed = 3f;

    [Tooltip("Spring stiffness of the rope joint. Higher = stiffer / less elastic.")]
    public float ropeSpring = 800f;

    [Tooltip("Damping on the rope joint. Higher = less oscillation.")]
    public float ropeDamper = 25f;

    [Header("Swing Feel")]
    [Tooltip("Instantaneous horizontal velocity added toward the far side when grabbing the rope.")]
    public float initialSwingImpulse = 5f;

    [Tooltip("Tangential force applied per unit of normalised WASD input while swinging.")]
    public float pumpForce = 12f;

    [Tooltip("Multiplier on horizontal velocity applied at the moment of release for extra flight arc.")]
    [Range(1f, 1.5f)]
    public float releaseBoostMultiplier = 1.15f;

    [Tooltip("How many seconds of being grounded before the rope auto-detaches.")]
    public float autoReleaseGroundedDelay = 0.4f;

    [Header("Rope Visuals")]
    [Tooltip("Material applied to the rope LineRenderer.")]
    public Material ropeMaterial;

    [Tooltip("Width of the rendered rope.")]
    public float ropeWidth = 0.07f;

    [Tooltip("Number of simulation points for the visual rope sag.")]
    [Range(4, 20)]
    public int ropeSimPoints = 10;

    [Tooltip("Maximum vertical sag offset at the rope midpoint.")]
    public float ropeSag = 0.9f;

    [Tooltip("Speed of the subtle lateral sway animation.")]
    public float ropeSwayCycleSpeed = 3f;

    [Header("Audio")]
    public AudioClip attachSound;
    public AudioClip releaseSound;

    [Header("Character Visuals")]
    [Tooltip("Assign the character model Transform here (e.g. the frog mesh root). " +
             "While swinging it will rotate so its forward (mouth) faces the anchor point. " +
             "Leave null to skip the effect.")]
    public Transform characterVisual;

    [Tooltip("How quickly the character rotates to face the anchor / recovers after release. " +
             "Higher = snappier.")]
    public float characterRotateSpeed = 6f;

    [Tooltip("Euler angle offset applied on top of the computed look rotation. " +
             "Adjust this if the model faces the wrong direction when swinging.\n\n" +
             "Common starting points:\n" +
             "  Mouth is +Y (top of model): X = -90\n" +
             "  Mouth is -Z (back of model): Y = 180\n" +
             "  Mouth is +X (right of model): Y = -90\n" +
             "Use the Inspector to tune while in Play mode.")]
    public Vector3 visualRotationOffset = Vector3.zero;

    // ── Internal state ─────────────────────────────────────────────────────────

    private Rigidbody rb;
    private PlayerMotor playerMotor;
    private OverheadController overheadController;

    private enum SwingState { Idle, Shooting, Swinging }
    private SwingState state = SwingState.Idle;

    private SwingGrappleZone currentZone;
    private Transform anchorPoint;
    private float maxRopeLength;   // distance at grab time
    private float currentRopeLen;  // mutable by reel

    private SpringJoint swingJoint;
    private Rigidbody anchorRigidbody; // kinematic body added to the anchor transform

    private float groundedTimer;

    // Character visual rotation
    private Quaternion preSwingLocalRotation;  // local rotation captured just before a swing
    private bool restoringRotation;            // true while lerping back after release

    // Rope visualisation
    private LineRenderer ropeRenderer;
    private Vector3[] simPos;
    private Vector3[] simVel;
    private float simTime;

    // Zone cache
    private SwingGrappleZone[] zoneCache;
    private float zoneCacheAge;
    private const float ZoneCacheLifetime = 5f;

    private AudioSource audioSource;

    // Accumulated reel input — written in Update, consumed in FixedUpdate to
    // avoid the scroll wheel value being zeroed before FixedUpdate runs.
    private float pendingReelDelta;

    // ── Unity lifecycle ────────────────────────────────────────────────────────

    void Awake()
    {
        rb = GetComponent<Rigidbody>();

        playerMotor     = GetComponent<PlayerMotor>()     ?? GetComponentInParent<PlayerMotor>();
        overheadController = GetComponent<OverheadController>() ?? GetComponentInParent<OverheadController>();

        SetupLineRenderer();
        SetupAudio();

        simPos = new Vector3[ropeSimPoints];
        simVel = new Vector3[ropeSimPoints];
    }

    void Start()
    {
        RefreshZoneCache();
    }

    void Update()
    {
        if (state == SwingState.Idle)
            UpdateZoneDetection();

        HandleInput();

        // Accumulate reel input here (Update rate) so FixedUpdate never misses it.
        if (state == SwingState.Swinging)
            pendingReelDelta += Input.GetAxis("Mouse ScrollWheel");

        UpdateCharacterRotation();
        UpdateRopeVisuals();
    }

    void FixedUpdate()
    {
        if (state == SwingState.Swinging)
        {
            ApplyRopeReel();
            ApplyPumpForce();
            CheckAutoRelease();
        }
    }

    // ── Zone scanning ──────────────────────────────────────────────────────────

    void RefreshZoneCache()
    {
        zoneCache = FindObjectsOfType<SwingGrappleZone>();
        zoneCacheAge = 0f;
    }

    void UpdateZoneDetection()
    {
        // Lazy refresh of the zone list every few seconds.
        zoneCacheAge += Time.deltaTime;
        if (zoneCacheAge > ZoneCacheLifetime || zoneCache == null)
            RefreshZoneCache();

        SwingGrappleZone best  = null;
        float           bestD = detectionRadius;

        foreach (var zone in zoneCache)
        {
            if (zone == null) continue;
            float d = Vector3.Distance(transform.position, zone.transform.position);
            if (d < bestD && zone.IsPlayerInRange(transform.position))
            {
                best  = zone;
                bestD = d;
            }
        }

        currentZone = best;
    }

    // ── Input ──────────────────────────────────────────────────────────────────

    void HandleInput()
    {
        if (!Input.GetKeyDown(grappleKey)) return;

        switch (state)
        {
            case SwingState.Idle:
                if (currentZone != null) BeginSwing();
                break;

            case SwingState.Swinging:
                ReleaseSwing(boosted: true);
                break;
        }
    }

    // ── Swing start ────────────────────────────────────────────────────────────

    void BeginSwing()
    {
        if (currentZone?.anchorPoint == null || rb == null) return;

        anchorPoint      = currentZone.anchorPoint;
        maxRopeLength    = Vector3.Distance(transform.position, anchorPoint.position) * ropeLengthMultiplier;
        maxRopeLength    = Mathf.Max(maxRopeLength, minRopeLength);
        currentRopeLen   = maxRopeLength;

        state = SwingState.Shooting;
        StartCoroutine(ShootRopeRoutine());
    }

    IEnumerator ShootRopeRoutine()
    {
        const float shootDuration = 0.22f;
        float elapsed = 0f;

        Vector3 origin = transform.position + Vector3.up * 0.5f;
        Vector3 target = anchorPoint.position;

        // Animate the rope growing toward the anchor.
        while (elapsed < shootDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / shootDuration;

            if (ropeRenderer != null)
            {
                ropeRenderer.positionCount = 2;
                ropeRenderer.SetPosition(0, transform.position + Vector3.up * 0.5f);
                ropeRenderer.SetPosition(1, Vector3.Lerp(origin, target, t));
            }

            yield return null;
        }

        AttachToAnchor();
    }

    void AttachToAnchor()
    {
        if (rb == null || anchorPoint == null) return;

        // Ensure a kinematic Rigidbody exists on the anchor so SpringJoint can bind to it.
        anchorRigidbody = anchorPoint.GetComponent<Rigidbody>();
        if (anchorRigidbody == null)
        {
            anchorRigidbody            = anchorPoint.gameObject.AddComponent<Rigidbody>();
            anchorRigidbody.isKinematic = true;
            anchorRigidbody.useGravity  = false;
        }

        // Create a SpringJoint that enforces a maximum distance (rope can't stretch beyond
        // currentRopeLen, but can be closer).  High spring gives near-inextensible behaviour.
        swingJoint                         = rb.gameObject.AddComponent<SpringJoint>();
        swingJoint.connectedBody           = anchorRigidbody;
        swingJoint.autoConfigureConnectedAnchor = false;
        swingJoint.anchor                  = Vector3.zero;
        swingJoint.connectedAnchor         = Vector3.zero;
        swingJoint.minDistance             = 0f;
        swingJoint.maxDistance             = currentRopeLen;
        swingJoint.spring                  = ropeSpring;
        swingJoint.damper                  = ropeDamper;
        swingJoint.enableCollision         = true;

        // Give the player an initial push toward the far side so the swing begins immediately.
        Transform nearSide = currentZone.GetNearestActivationPoint(transform.position);
        Transform farSide  = currentZone.GetOppositeSide(nearSide);
        if (farSide != null)
        {
            Vector3 dir = farSide.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
                rb.AddForce(dir.normalized * initialSwingImpulse, ForceMode.VelocityChange);
        }

        // Disable WASD motor input; Rigidbody physics drives movement during the swing.
        if (overheadController != null)
            overheadController.SetMovementEnabled(false);
        else if (playerMotor != null)
            playerMotor.SetMovementEnabled(false);

        // Seed the rope simulation in a straight line from player to anchor.
        for (int i = 0; i < ropeSimPoints; i++)
        {
            float t = (float)i / (ropeSimPoints - 1);
            simPos[i] = Vector3.Lerp(transform.position + Vector3.up * 0.5f, anchorPoint.position, t);
            simVel[i] = Vector3.zero;
        }
        simTime = 0f;

        // Save local rotation so we can restore it gracefully after release.
        if (characterVisual != null)
        {
            preSwingLocalRotation = characterVisual.localRotation;
            restoringRotation     = false;
        }

        state          = SwingState.Swinging;
        groundedTimer  = 0f;

        if (attachSound != null && audioSource != null)
            audioSource.PlayOneShot(attachSound);
    }

    // ── In-flight physics (FixedUpdate) ───────────────────────────────────────

    /// <summary>Adjusts rope length using input accumulated since the last FixedUpdate.</summary>
    void ApplyRopeReel()
    {
        if (Mathf.Abs(pendingReelDelta) < 0.0001f) return;

        currentRopeLen = Mathf.Clamp(
            currentRopeLen - pendingReelDelta * reelSpeed,
            minRopeLength,
            maxRopeLength);

        if (swingJoint != null)
            swingJoint.maxDistance = currentRopeLen;

        pendingReelDelta = 0f;
    }

    /// <summary>
    /// Applies a tangential force based on player input, preserving pendulum energy
    /// in a way analogous to pumping a playground swing.
    /// </summary>
    void ApplyPumpForce()
    {
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        if (Mathf.Abs(h) < 0.05f && Mathf.Abs(v) < 0.05f) return;
        if (anchorPoint == null) return;

        // Rotate raw input through the camera's yaw so the player pumps in the
        // direction they're pressing relative to their camera view.
        float camYaw = Camera.main != null
            ? Camera.main.transform.eulerAngles.y
            : transform.eulerAngles.y;

        Vector3 inputWorld = Quaternion.Euler(0f, camYaw, 0f) * new Vector3(h, 0f, v);

        // Project input onto the plane perpendicular to the rope so we only add
        // tangential (arc-arc) energy, never fighting the length constraint.
        Vector3 toAnchor    = (anchorPoint.position - rb.position).normalized;
        Vector3 tangential  = Vector3.ProjectOnPlane(inputWorld, toAnchor).normalized;

        rb.AddForce(tangential * pumpForce, ForceMode.Force);
    }

    /// <summary>Releases the rope automatically after the player lands.</summary>
    void CheckAutoRelease()
    {
        bool grounded = playerMotor != null
            ? playerMotor.IsGrounded()
            : rb.linearVelocity.y < 0.1f && Physics.CheckSphere(
                  transform.position - Vector3.up * 0.1f, 0.4f,
                  ~LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore);

        if (grounded)
        {
            groundedTimer += Time.fixedDeltaTime;
            if (groundedTimer >= autoReleaseGroundedDelay)
                ReleaseSwing(boosted: false);
        }
        else
        {
            groundedTimer = 0f;
        }
    }

    // ── Release ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Detaches the rope and re-enables player locomotion.
    /// If boosted is true, the player's horizontal velocity is multiplied by
    /// releaseBoostMultiplier for a more satisfying launch arc.
    /// </summary>
    void ReleaseSwing(bool boosted)
    {
        if (swingJoint != null)
        {
            Destroy(swingJoint);
            swingJoint = null;
        }

        // Boost horizontal velocity on a manual release.
        if (boosted && rb != null && releaseBoostMultiplier > 1f)
        {
            Vector3 v  = rb.linearVelocity;
            float   hs = new Vector3(v.x, 0f, v.z).magnitude;
            if (hs > 0.5f)
                rb.linearVelocity = new Vector3(v.x * releaseBoostMultiplier,
                                          v.y,
                                          v.z * releaseBoostMultiplier);
        }

        if (overheadController != null)
            overheadController.SetMovementEnabled(true);
        else if (playerMotor != null)
            playerMotor.SetMovementEnabled(true);

        state           = SwingState.Idle;
        anchorPoint     = null;
        currentZone     = null;
        groundedTimer   = 0f;
        pendingReelDelta = 0f;

        // Begin smoothly restoring the character's original orientation.
        if (characterVisual != null)
            restoringRotation = true;

        if (ropeRenderer != null)
            ropeRenderer.positionCount = 0;

        if (releaseSound != null && audioSource != null)
            audioSource.PlayOneShot(releaseSound);
    }

    // ── Character visual rotation ──────────────────────────────────────────────

    /// <summary>
    /// Rotates characterVisual so its forward (mouth) tracks the anchor point while
    /// swinging, then smoothly restores the original local rotation after release.
    /// </summary>
    void UpdateCharacterRotation()
    {
        if (characterVisual == null) return;

        if (state == SwingState.Swinging && anchorPoint != null)
        {
            restoringRotation = false;

            Vector3 dirToAnchor = (anchorPoint.position - characterVisual.position).normalized;

            // Guard against degenerate direction (anchor exactly overhead / behind).
            // When nearly straight up, use the camera forward as the horizontal reference
            // so LookRotation doesn't gimbal-lock.
            Vector3 upRef = Mathf.Abs(dirToAnchor.y) > 0.98f
                ? characterVisual.right
                : Vector3.up;

            Quaternion targetRot = Quaternion.LookRotation(dirToAnchor, upRef)
                                 * Quaternion.Euler(visualRotationOffset);
            characterVisual.rotation = Quaternion.Slerp(
                characterVisual.rotation, targetRot,
                Time.deltaTime * characterRotateSpeed);
        }
        else if (restoringRotation)
        {
            // Lerp local rotation back to the state it was in before the swing.
            characterVisual.localRotation = Quaternion.Slerp(
                characterVisual.localRotation, preSwingLocalRotation,
                Time.deltaTime * characterRotateSpeed);

            if (Quaternion.Angle(characterVisual.localRotation, preSwingLocalRotation) < 0.5f)
            {
                characterVisual.localRotation = preSwingLocalRotation;
                restoringRotation = false;
            }
        }
    }

    // ── Rope visuals ───────────────────────────────────────────────────────────

    void UpdateRopeVisuals()
    {
        if (ropeRenderer == null) return;

        if (state == SwingState.Swinging && anchorPoint != null)
        {
            simTime += Time.deltaTime;

            Vector3 startPt = transform.position + Vector3.up * 0.5f;
            Vector3 endPt   = anchorPoint.position;
            Vector3 ropDir  = (endPt - startPt).normalized;
            Vector3 swayDir = Vector3.Cross(ropDir, Vector3.up).normalized;

            for (int i = 0; i < ropeSimPoints; i++)
            {
                float t = (float)i / (ropeSimPoints - 1);

                // Catenary-inspired rest position: straight line with midpoint sag.
                Vector3 rest = Vector3.Lerp(startPt, endPt, t);
                rest.y -= Mathf.Sin(t * Mathf.PI) * ropeSag;

                // Subtle lateral sway.
                float sway = Mathf.Sin(simTime * ropeSwayCycleSpeed + i * 0.7f)
                             * 0.12f * t * (1f - t);
                rest += swayDir * sway;

                // Verlet spring toward rest position.
                Vector3 force  = (rest - simPos[i]) * 14f;
                force         -= simVel[i]           * 7f;
                simVel[i]     += force * Time.deltaTime;
                simPos[i]     += simVel[i] * Time.deltaTime;

                // Pin endpoints.
                if (i == 0)                  simPos[i] = startPt;
                if (i == ropeSimPoints - 1)  simPos[i] = endPt;
            }

            ropeRenderer.positionCount = ropeSimPoints;
            for (int i = 0; i < ropeSimPoints; i++)
                ropeRenderer.SetPosition(i, simPos[i]);
        }
        else if (state == SwingState.Idle)
        {
            ropeRenderer.positionCount = 0;
        }
        // Shooting state: coroutine manages the renderer directly.
    }

    // ── Setup helpers ──────────────────────────────────────────────────────────

    void SetupLineRenderer()
    {
        // Use a dedicated child GameObject so this LineRenderer is completely isolated
        // from other components on the player that manage their own LineRenderers
        // (e.g. FrogTongueController), preventing any cross-component interference.
        var ropeGO = new GameObject("SwingRopeRenderer");
        ropeGO.transform.SetParent(transform, false);

        ropeRenderer = ropeGO.AddComponent<LineRenderer>();
        ropeRenderer.useWorldSpace   = true;
        ropeRenderer.positionCount   = 0;
        ropeRenderer.startWidth      = ropeWidth;
        ropeRenderer.endWidth        = ropeWidth * 0.65f;
        ropeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ropeRenderer.receiveShadows  = false;

        if (ropeMaterial != null)
        {
            ropeRenderer.material = ropeMaterial;
        }
        else
        {
            // Fallback: a plain unlit material so the rope is always visible.
            Shader fallback = Shader.Find("Sprites/Default")
                           ?? Shader.Find("Unlit/Color")
                           ?? Shader.Find("Standard");
            if (fallback != null)
            {
                Material mat = new Material(fallback);
                mat.color = new Color(0.55f, 0.35f, 0.15f);
                ropeRenderer.material = mat;
            }
        }
    }

    void SetupAudio()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.volume = 0.75f;
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public bool IsSwinging()              => state != SwingState.Idle;
    public SwingGrappleZone CurrentZone() => currentZone;

    void OnDestroy()
    {
        // Clean up the joint if the player object is destroyed mid-swing.
        if (swingJoint != null) Destroy(swingJoint);

        // Destroy the rope renderer child we created.
        if (ropeRenderer != null) Destroy(ropeRenderer.gameObject);
    }

    // ── Editor helpers ─────────────────────────────────────────────────────────

    void OnDrawGizmos()
    {
        if (state == SwingState.Swinging && anchorPoint != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position + Vector3.up * 0.5f, anchorPoint.position);
        }

        if (state == SwingState.Idle && currentZone != null)
        {
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, 0.6f);
        }
    }
}
