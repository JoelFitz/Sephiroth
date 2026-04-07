using UnityEngine;
using System.Collections;

public class MushroomAnimationDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private MushroomAI mushroomAI;
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rb;

    [Header("Animation States")]
    [SerializeField] private string idleStateName = "idle";
    [SerializeField] private string runStateName = "run";
    [SerializeField] private string jumpStateName = "jump";

    [Header("Fallback State Names")]
    [SerializeField] private string idleFallbackStateName = "metarig_002_Idle Animation_001";
    [SerializeField] private string runFallbackStateName = "metarig_002_Run Animation_001";
    [SerializeField] private string jumpFallbackStateName = "metarig_002_Jump In_001";

    [Header("Blending")]
    [SerializeField] private float transitionDuration = 0.2f;
    [SerializeField] private float speedSmoothing = 10f;
    [SerializeField] private float moveThreshold = 0.08f;

    [Header("Facing")]
    [SerializeField] private bool faceMoveDirection = true;
    [SerializeField] private float turnSpeed = 8f;
    [SerializeField] private Vector3 faceAxis = Vector3.up;

    [Header("Burrow Motion")]
    [SerializeField] private float surfaceLerpSpeed = 6f;
    [SerializeField] private float burrowLerpSpeed = 7f;
    [SerializeField] private float wiggleAmount = 0.015f;
    [SerializeField] private float wiggleSpeed = 12f;
    [SerializeField] private float jumpBurrowStartNormalizedTime = 0.82f;
    [SerializeField] private float hiddenDepthMultiplier = 1f;
    [SerializeField] private float jumpRunUpDistance = 0.35f;
    [SerializeField] private float jumpRunUpSpeed = 8f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = true;
    [SerializeField] private float debugLogInterval = 1.5f;

    [Header("Rig Binding")]
    [SerializeField] private string expectedRigRootName = "metarig.002";

    private MushroomState lastState;
    private bool hideJumpPlaying;
    private bool hideBurrowStarted;
    private bool hideSequenceActive;
    private bool emergeSequenceActive;
    private float smoothedSpeed;
    private bool loggedMissingStates;
    private float nextDebugLogTime;
    private Vector3 modelOriginalLocalPosition;
    private Vector3 modelHiddenLocalPosition;
    private Vector3 hideSequenceStartWorldPosition;
    private Vector3 hideSequenceForwardDirection = Vector3.forward;
    private bool cachedModelPositions;

    private ResolvedState idleState;
    private ResolvedState runState;
    private ResolvedState jumpState;
    private ResolvedState currentState;

    private struct ResolvedState
    {
        public bool valid;
        public int fullPathHash;
        public int shortNameHash;
        public string fullPathName;
    }

    public void Initialize(MushroomAI ai, Animator anim)
    {
        mushroomAI = ai;
        animator = anim != null ? anim : ResolveAnimator();
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (animator != null)
        {
            animator.enabled = true;
            animator.speed = 1f;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.applyRootMotion = false;
            animator.Rebind();
        }

        CacheModelPositions();

        CacheStates();
        LogSetup("Initialize");

        if (mushroomAI != null)
        {
            lastState = mushroomAI.currentState;
            ForceVisibleState();
        }
    }

    void Awake()
    {
        if (mushroomAI == null)
        {
            mushroomAI = GetComponent<MushroomAI>();
        }

        animator = ResolveAnimator();

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (animator != null)
        {
            animator.enabled = true;
            animator.speed = 1f;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.applyRootMotion = false;
        }

        CacheModelPositions();

        CacheStates();
        LogSetup("Awake");

        if (mushroomAI != null)
        {
            lastState = mushroomAI.currentState;
        }
    }

    IEnumerator Start()
    {
        // Animator state tables can be unavailable during Awake on some prefabs.
        yield return null;
        CacheStates();
    }

    void Update()
    {
        if (mushroomAI == null || animator == null)
        {
            return;
        }

        if (!idleState.valid || !runState.valid || !jumpState.valid)
        {
            CacheStates();

            if ((!idleState.valid || !runState.valid || !jumpState.valid) && !loggedMissingStates)
            {
                loggedMissingStates = true;
                Debug.LogWarning($"Mushroom {name}: Animation states not fully resolved. Idle={idleState.valid}, Run={runState.valid}, Jump={jumpState.valid}");
            }
        }

        if (lastState != mushroomAI.currentState)
        {
            HandleStateChanged(lastState, mushroomAI.currentState);
            lastState = mushroomAI.currentState;
        }

        UpdateAnimation();
        UpdateFacing();
        UpdateBurrowMotion();

        if (enableDebugLogs && Time.time >= nextDebugLogTime)
        {
            nextDebugLogTime = Time.time + Mathf.Max(0.2f, debugLogInterval);
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log($"MushroomAnim {name}: state={mushroomAI.currentState}, clipHash={info.shortNameHash}, norm={info.normalizedTime:F2}, vel={GetHorizontalSpeed():F2}, smooth={smoothedSpeed:F2}, layerWeight={animator.GetLayerWeight(0):F2}, animatorEnabled={animator.enabled}");
        }
    }

    Animator ResolveAnimator()
    {
        if (animator != null)
        {
            return TryAutoFixAnimatorBinding(animator);
        }

        if (mushroomAI != null && mushroomAI.animator != null)
        {
            return TryAutoFixAnimatorBinding(mushroomAI.animator);
        }

        if (mushroomAI != null && mushroomAI.mushroomModel != null)
        {
            Animator modelAnimator = mushroomAI.mushroomModel.GetComponentInChildren<Animator>(true);
            if (modelAnimator != null)
            {
                return TryAutoFixAnimatorBinding(modelAnimator);
            }
        }

        Animator fallback = GetComponentInChildren<Animator>(true);
        return TryAutoFixAnimatorBinding(fallback);
    }

    Animator TryAutoFixAnimatorBinding(Animator sourceAnimator)
    {
        if (sourceAnimator == null)
        {
            return null;
        }

        if (HasExpectedRigRoot(sourceAnimator.transform))
        {
            return sourceAnimator;
        }

        Transform correctedHost = FindRigHostTransform(sourceAnimator.transform);
        if (correctedHost == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"MushroomAnim {name}: Animator '{sourceAnimator.name}' cannot find rig root '{expectedRigRootName}' under its transform. Place Animator on the parent that contains '{expectedRigRootName}'.");
            }

            return sourceAnimator;
        }

        Animator correctedAnimator = correctedHost.GetComponent<Animator>();
        if (correctedAnimator == null)
        {
            correctedAnimator = correctedHost.gameObject.AddComponent<Animator>();
            correctedAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            correctedAnimator.avatar = sourceAnimator.avatar;
            correctedAnimator.applyRootMotion = sourceAnimator.applyRootMotion;
            correctedAnimator.cullingMode = sourceAnimator.cullingMode;
            correctedAnimator.updateMode = sourceAnimator.updateMode;
            correctedAnimator.speed = sourceAnimator.speed;
        }

        if (correctedAnimator != sourceAnimator)
        {
            sourceAnimator.enabled = false;
        }

        if (enableDebugLogs)
        {
            Debug.LogWarning($"MushroomAnim {name}: Rebound Animator from '{sourceAnimator.name}' to '{correctedAnimator.name}' for rig root '{expectedRigRootName}'.");
        }

        return correctedAnimator;
    }

    bool HasExpectedRigRoot(Transform animatorHost)
    {
        if (animatorHost == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(expectedRigRootName))
        {
            return true;
        }

        return animatorHost.Find(expectedRigRootName) != null;
    }

    Transform FindRigHostTransform(Transform start)
    {
        if (start == null)
        {
            return null;
        }

        Transform current = start;
        while (current != null)
        {
            if (HasExpectedRigRoot(current))
            {
                return current;
            }

            current = current.parent;
        }

        if (mushroomAI != null && mushroomAI.mushroomModel != null)
        {
            Transform modelRoot = mushroomAI.mushroomModel.transform;
            if (HasExpectedRigRoot(modelRoot))
            {
                return modelRoot;
            }
        }

        return null;
    }

    void CacheStates()
    {
        animator = ResolveAnimator();
        if (animator == null)
        {
            return;
        }

        idleState = ResolveState(idleStateName, idleFallbackStateName);
        runState = ResolveState(runStateName, runFallbackStateName);
        jumpState = ResolveState(jumpStateName, jumpFallbackStateName);

        if (idleState.valid && runState.valid && jumpState.valid)
        {
            loggedMissingStates = false;
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"MushroomAnim {name}: unresolved states -> idle={idleState.valid}, run={runState.valid}, jump={jumpState.valid}");
        }
    }

    void CacheModelPositions()
    {
        if (cachedModelPositions || mushroomAI == null || mushroomAI.mushroomModel == null)
        {
            return;
        }

        Transform modelTransform = mushroomAI.mushroomModel.transform;
        modelOriginalLocalPosition = modelTransform.localPosition;
        modelHiddenLocalPosition = modelOriginalLocalPosition + Vector3.down * mushroomAI.Data.hideDepth * hiddenDepthMultiplier;
        cachedModelPositions = true;
    }

    void LogSetup(string source)
    {
        if (!enableDebugLogs || animator == null)
        {
            return;
        }

        string controllerName = animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.name
            : "<none>";

        Debug.Log($"MushroomAnim {name}: [{source}] animator={animator.name}, controller={controllerName}, layers={animator.layerCount}, idle={idleState.valid}, run={runState.valid}, jump={jumpState.valid}");
    }

    ResolvedState ResolveState(string preferredName, string fallbackName)
    {
        ResolvedState resolved = ResolveStateName(preferredName);
        if (resolved.valid)
        {
            return resolved;
        }

        resolved = ResolveStateName(ToTitleCase(preferredName));
        if (resolved.valid)
        {
            return resolved;
        }

        resolved = ResolveStateName(fallbackName);
        if (resolved.valid)
        {
            return resolved;
        }

        resolved = ResolveStateName(ToTitleCase(fallbackName));
        if (resolved.valid)
        {
            return resolved;
        }

        return default;
    }

    string ToTitleCase(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        if (value.Length == 1)
        {
            return value.ToUpperInvariant();
        }

        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    ResolvedState ResolveStateName(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
        {
            return default;
        }

        if (TryResolveFullPath(stateName, out int stateHash))
        {
            return new ResolvedState
            {
                valid = true,
                fullPathHash = stateHash,
                shortNameHash = Animator.StringToHash(GetShortName(stateName)),
                fullPathName = stateName
            };
        }

        string fullLayerPath = $"Base Layer.{stateName}";
        if (TryResolveFullPath(fullLayerPath, out stateHash))
        {
            return new ResolvedState
            {
                valid = true,
                fullPathHash = stateHash,
                shortNameHash = Animator.StringToHash(stateName),
                fullPathName = fullLayerPath
            };
        }

        return default;
    }

    bool TryResolveFullPath(string fullPath, out int hash)
    {
        hash = Animator.StringToHash(fullPath);
        return animator != null && animator.HasState(0, hash);
    }

    string GetShortName(string stateName)
    {
        int dotIndex = stateName.LastIndexOf('.');
        if (dotIndex >= 0 && dotIndex + 1 < stateName.Length)
        {
            return stateName.Substring(dotIndex + 1);
        }

        return stateName;
    }

    void HandleStateChanged(MushroomState fromState, MushroomState toState)
    {
        if (enableDebugLogs)
        {
            Debug.Log($"MushroomAnim {name}: AI state {fromState} -> {toState}");
        }

        if (toState == MushroomState.Hidden)
        {
            if (!hideSequenceActive)
            {
                BeginHideSequence();
            }
            return;
        }

        hideJumpPlaying = false;
        hideBurrowStarted = false;
        hideSequenceActive = false;
        emergeSequenceActive = false;

        if (toState == MushroomState.Collected || toState == MushroomState.TongueGrabbed)
        {
            smoothedSpeed = 0f;
            PlayState(idleState, transitionDuration);
            return;
        }

        ForceVisibleState();
    }

    void UpdateAnimation()
    {
        if (hideSequenceActive)
        {
            if (hideJumpPlaying && jumpState.valid)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                bool onJump = info.fullPathHash == jumpState.fullPathHash || info.shortNameHash == jumpState.shortNameHash;

                if (onJump && !hideBurrowStarted && info.normalizedTime >= jumpBurrowStartNormalizedTime)
                {
                    hideBurrowStarted = true;
                }

                if (hideBurrowStarted && info.normalizedTime >= 0.98f)
                {
                    hideJumpPlaying = false;
                    smoothedSpeed = 0f;
                    if (mushroomAI != null && mushroomAI.currentState == MushroomState.Hidden)
                    {
                        PlayState(idleState, transitionDuration);
                    }
                }

                return;
            }

            if (!hideJumpPlaying)
            {
                hideJumpPlaying = true;
                hideBurrowStarted = false;
                PlayState(jumpState, 0.05f);
            }

            return;
        }

        if (mushroomAI.currentState == MushroomState.Hidden)
        {
            return;
        }

        if (mushroomAI.currentState == MushroomState.Collected || mushroomAI.currentState == MushroomState.TongueGrabbed)
        {
            smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, 0f, Time.deltaTime * speedSmoothing);
            return;
        }

        float horizontalSpeed = GetHorizontalSpeed();
        smoothedSpeed = Mathf.MoveTowards(smoothedSpeed, horizontalSpeed, Time.deltaTime * speedSmoothing);

        bool isMoving = smoothedSpeed > moveThreshold;
        PlayState(isMoving ? runState : idleState, transitionDuration);
    }

    void UpdateFacing()
    {
        if (!faceMoveDirection || mushroomAI == null)
        {
            return;
        }

        if (mushroomAI.currentState == MushroomState.Hidden)
        {
            return;
        }

        Vector3 direction = GetHorizontalMovementDirection();
        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 axis = faceAxis.sqrMagnitude < 0.0001f ? Vector3.up : faceAxis.normalized;
        Vector3 forward = Vector3.ProjectOnPlane(direction, axis);
        if (forward.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(forward.normalized, axis);
        Transform targetTransform = mushroomAI.mushroomModel != null ? mushroomAI.mushroomModel.transform : transform;
        targetTransform.rotation = Quaternion.Slerp(targetTransform.rotation, targetRotation, Time.deltaTime * turnSpeed);
    }

    Vector3 GetHorizontalMovementDirection()
    {
        if (rb == null)
        {
            return Vector3.zero;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;

        if (velocity.sqrMagnitude > 0.001f)
        {
            return velocity.normalized;
        }

        return Vector3.zero;
    }

    void UpdateBurrowMotion()
    {
        if (mushroomAI == null || mushroomAI.mushroomModel == null)
        {
            return;
        }

        CacheModelPositions();

        Transform modelTransform = mushroomAI.mushroomModel.transform;
        Vector3 targetLocalPosition = modelOriginalLocalPosition;

        if (hideSequenceActive && hideBurrowStarted)
        {
            float hiddenWiggle = Mathf.Sin(Time.time * wiggleSpeed) * wiggleAmount;
            targetLocalPosition = modelHiddenLocalPosition + new Vector3(hiddenWiggle, 0f, 0f);
            modelTransform.localPosition = Vector3.Lerp(modelTransform.localPosition, targetLocalPosition, Time.deltaTime * burrowLerpSpeed);
            return;
        }

        if (hideSequenceActive)
        {
            UpdateHideRunUpMotion();
            return;
        }

        if (emergeSequenceActive)
        {
            float emergeWiggle = Mathf.Sin(Time.time * wiggleSpeed) * wiggleAmount;
            targetLocalPosition = modelOriginalLocalPosition + new Vector3(emergeWiggle, 0f, 0f);
            modelTransform.localPosition = Vector3.Lerp(modelTransform.localPosition, targetLocalPosition, Time.deltaTime * surfaceLerpSpeed);
            return;
        }

        float surfaceWiggle = Mathf.Sin(Time.time * wiggleSpeed) * wiggleAmount;
        targetLocalPosition = modelOriginalLocalPosition + new Vector3(surfaceWiggle, 0f, 0f);
        modelTransform.localPosition = Vector3.Lerp(modelTransform.localPosition, targetLocalPosition, Time.deltaTime * surfaceLerpSpeed);
    }

    void UpdateHideRunUpMotion()
    {
        if (mushroomAI == null)
        {
            return;
        }

        AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
        float runUpProgress = Mathf.Clamp01(info.normalizedTime / Mathf.Max(0.01f, jumpBurrowStartNormalizedTime));
        float forwardDistance = Mathf.Lerp(0f, jumpRunUpDistance, Mathf.SmoothStep(0f, 1f, runUpProgress));
        Vector3 targetPosition = hideSequenceStartWorldPosition + hideSequenceForwardDirection * forwardDistance;

        Transform rootTransform = transform;
        rootTransform.position = Vector3.Lerp(rootTransform.position, targetPosition, Time.deltaTime * jumpRunUpSpeed);
    }

    float GetHorizontalSpeed()
    {
        if (rb == null)
        {
            return 0f;
        }

        Vector3 velocity = rb.linearVelocity;
        velocity.y = 0f;
        return velocity.magnitude;
    }

    void ForceVisibleState()
    {
        if (mushroomAI == null)
        {
            return;
        }

        if (mushroomAI.currentState == MushroomState.Fleeing)
        {
            PlayState(runState, transitionDuration);
            return;
        }

        PlayState(idleState, transitionDuration);
    }

    void PlayState(ResolvedState state, float fade)
    {
        if (!state.valid || animator == null)
        {
            if (enableDebugLogs)
            {
                Debug.LogWarning($"MushroomAnim {name}: PlayState skipped, valid={state.valid}, animator={(animator != null)}");
            }
            return;
        }

        if (currentState.valid && currentState.fullPathHash == state.fullPathHash)
        {
            return;
        }

        string statePath = !string.IsNullOrWhiteSpace(state.fullPathName)
            ? state.fullPathName
            : "Base Layer.idle";

        if (fade <= 0f)
        {
            animator.Play(statePath, 0, 0f);
        }
        else
        {
            animator.CrossFade(statePath, fade, 0);
        }
        currentState = state;

        if (enableDebugLogs)
        {
            Debug.Log($"MushroomAnim {name}: playing {state.fullPathName} (fade={fade:F2})");
        }
    }

    public void BeginHideSequence()
    {
        if (animator == null || !jumpState.valid)
        {
            return;
        }

        hideSequenceActive = true;
        emergeSequenceActive = false;
        hideJumpPlaying = true;
        hideBurrowStarted = false;
        hideSequenceStartWorldPosition = transform.position;
        if (mushroomAI != null && mushroomAI.mushroomModel != null)
        {
            Vector3 forward = mushroomAI.mushroomModel.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = transform.forward;
                forward.y = 0f;
            }

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = Vector3.forward;
            }

            hideSequenceForwardDirection = forward.normalized;
        }
        else
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            hideSequenceForwardDirection = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
        }
        PlayState(jumpState, 0.05f);
    }

    public void BeginEmergeSequence()
    {
        hideSequenceActive = false;
        hideJumpPlaying = false;
        hideBurrowStarted = false;
        emergeSequenceActive = true;
    }
}
