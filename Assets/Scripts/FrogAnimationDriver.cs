using System.Collections;
using UnityEngine;

public class FrogAnimationDriver : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private OverheadController overheadController;
    [SerializeField] private Animator animator;
    [SerializeField] private CharacterController characterController;

    [Header("Animation States")]
    [SerializeField] private string idleStateName = "metarig|TPose";
    [SerializeField] private string walkStateName = "metarig|Walking";
    [SerializeField] private string sprintStateName = "metarig|Sprinting";
    [SerializeField] private string jumpStateName = "metarig|Jump";

    [Header("Blending")]
    [SerializeField] private float transitionDuration = 0.12f;
    [SerializeField] private float moveThreshold = 0.08f;

    [Header("Debug")]
    [SerializeField] private bool enableDebugLogs = false;

    private ResolvedState idleState;
    private ResolvedState walkState;
    private ResolvedState sprintState;
    private ResolvedState jumpState;
    private ResolvedState currentState;
    private bool loggedMissingStates;

    private struct ResolvedState
    {
        public bool valid;
        public int fullPathHash;
        public int shortNameHash;
        public string fullPathName;
    }

    void Awake()
    {
        overheadController = overheadController != null ? overheadController : GetComponent<OverheadController>();
        characterController = characterController != null ? characterController : GetComponent<CharacterController>();
        animator = ResolveAnimator();

        ConfigureAnimator();
        CacheStates();
    }

    IEnumerator Start()
    {
        yield return null;
        CacheStates();
    }

    void Update()
    {
        if (animator == null)
        {
            animator = ResolveAnimator();
            if (animator == null)
                return;

            ConfigureAnimator();
            CacheStates();
        }

        if (!idleState.valid || !walkState.valid || !sprintState.valid || !jumpState.valid)
        {
            CacheStates();

            if ((!idleState.valid || !walkState.valid || !sprintState.valid || !jumpState.valid) && !loggedMissingStates)
            {
                loggedMissingStates = true;
                Debug.LogWarning($"FrogAnim {name}: animation states not fully resolved. Idle={idleState.valid}, Walk={walkState.valid}, Sprint={sprintState.valid}, Jump={jumpState.valid}");
            }
        }

        if (overheadController != null && (!overheadController.enabled || !overheadController.IsMovementEnabled()))
        {
            PlayState(idleState, transitionDuration);
            return;
        }

        bool jumpPressed = overheadController != null && Input.GetKeyDown(overheadController.jumpKey);
        bool grounded = characterController == null || characterController.isGrounded;

        if (jumpPressed || !grounded)
        {
            PlayState(jumpState, transitionDuration);
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        float moveMagnitude = new Vector2(horizontal, vertical).magnitude;

        if (moveMagnitude < moveThreshold)
        {
            PlayState(idleState, transitionDuration);
            return;
        }

        bool sprinting = overheadController != null && Input.GetKey(overheadController.sprintKey);
        PlayState(sprinting ? sprintState : walkState, transitionDuration);
    }

    Animator ResolveAnimator()
    {
        if (animator != null)
            return animator;

        Animator childAnimator = GetComponentInChildren<Animator>(true);
        if (childAnimator != null)
            return childAnimator;

        return GetComponent<Animator>();
    }

    void ConfigureAnimator()
    {
        if (animator == null)
            return;

        animator.enabled = true;
        animator.speed = 1f;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        animator.updateMode = AnimatorUpdateMode.Normal;
        animator.applyRootMotion = false;
    }

    void CacheStates()
    {
        if (animator == null)
            return;

        idleState = ResolveState(idleStateName);
        walkState = ResolveState(walkStateName);
        sprintState = ResolveState(sprintStateName);
        jumpState = ResolveState(jumpStateName);

        if (idleState.valid && walkState.valid && sprintState.valid && jumpState.valid)
        {
            loggedMissingStates = false;
        }
        else if (enableDebugLogs)
        {
            Debug.LogWarning($"FrogAnim {name}: unresolved states -> idle={idleState.valid}, walk={walkState.valid}, sprint={sprintState.valid}, jump={jumpState.valid}");
        }
    }

    ResolvedState ResolveState(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return default;

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

    void PlayState(ResolvedState state, float fade)
    {
        if (!state.valid || animator == null)
            return;

        if (currentState.valid && currentState.fullPathHash == state.fullPathHash)
            return;

        string statePath = !string.IsNullOrWhiteSpace(state.fullPathName)
            ? state.fullPathName
            : "Base Layer.metarig|TPose";

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
            Debug.Log($"FrogAnim {name}: playing {state.fullPathName} (fade={fade:F2})");
        }
    }
}