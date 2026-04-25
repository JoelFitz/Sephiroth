using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public enum MushroomState
{
    Hidden,
    Idle,
    Alert,
    Fleeing,
    TongueGrabbed,
    Collected
}


public class MushroomAI : MonoBehaviour
{
    [Header("Mushroom Configuration")]
    public MushroomData mushroomData;
    public Transform player;

    [Header("Visual")]
    public GameObject mushroomModel;
    public Animator animator;

    [Header("Detection")]
    public LayerMask playerLayer = 1;

    [Header("Ambient Idle Wander")]
    public bool enableAmbientIdleWander = true;
    public float ambientWalkSpeed = 1.2f;
    public float ambientWanderRadius = 4f;
    public float ambientWalkMinDuration = 1.2f;
    public float ambientWalkMaxDuration = 2.6f;
    public float ambientPauseMinDuration = 0.7f;
    public float ambientPauseMaxDuration = 2.2f;

    // State Management
    public MushroomState currentState = MushroomState.Hidden;
    private MushroomState previousState;

    // Components
    private MushroomPersonality personality;
    private MushroomAnimationDriver animationDriver;
    private Collider mushroomCollider;
    private Rigidbody rb;

    // Runtime data
    private float stateTimer;
    private bool playerInRange;
    private Vector3 originalPosition;
    private Vector3 fleeDirection;
    private bool ambientIsWalking;
    private float ambientPhaseEndTime;
    private Vector3 ambientMoveDirection;

    private Vector3 modelOriginalLocalPosition;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        InitializeMushroom();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryResolvePlayer();
    }

    void InitializeMushroom()
    {
        // Get components
        mushroomCollider = GetComponent<Collider>();
        rb = GetComponent<Rigidbody>();

        // Find player if not assigned
        TryResolvePlayer();

        // Store original position of the ROOT GameObject
        originalPosition = transform.position;

        if (mushroomModel != null)
        {
            modelOriginalLocalPosition = mushroomModel.transform.localPosition;
            Debug.Log($"Mushroom {name}: Model original local position = {modelOriginalLocalPosition}");
        }

        if (animator == null && mushroomModel != null)
        {
            animator = mushroomModel.GetComponentInChildren<Animator>(true);
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (animator != null)
        {
            animationDriver = GetComponent<MushroomAnimationDriver>();
            if (animationDriver == null)
            {
                animationDriver = gameObject.AddComponent<MushroomAnimationDriver>();
            }

            animationDriver.Initialize(this, animator);
        }

        // Load personality behavior
        if (mushroomData != null && mushroomData.personalityPrefab != null)
        {
            var personalityObj = Instantiate(mushroomData.personalityPrefab, transform);
            personality = personalityObj.GetComponent<MushroomPersonality>();
            personality.Initialize(this, mushroomData);
        }

        // Start in Idle state so mushrooms are visible
        ChangeState(MushroomState.Idle);
    }

    void Update()
    {
        if (player == null)
            TryResolvePlayer();

        if (player == null || mushroomData == null) return;

        UpdateDetection();
        UpdateStateBehavior();
        UpdateVisuals();
    }

    void TryResolvePlayer()
    {
        if (player != null)
            return;

        OverheadController[] controllers = FindObjectsByType<OverheadController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Scene activeScene = SceneManager.GetActiveScene();

        for (int i = 0; i < controllers.Length; i++)
        {
            OverheadController candidate = controllers[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.scene == activeScene)
            {
                player = candidate.transform;
                return;
            }
        }

        if (controllers.Length > 0 && controllers[0] != null)
        {
            player = controllers[0].transform;
            return;
        }

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;
    }

    void UpdateDetection()
    {
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        playerInRange = distanceToPlayer <= mushroomData.detectionRange;

        // Debug logging disabled to reduce console noise.
        // if (Time.frameCount % 60 == 0)
        // {
        //     Debug.Log($"Mushroom {name}: Distance={distanceToPlayer:F1}, InRange={playerInRange}, State={currentState}");
        // }
    }

    void UpdateStateBehavior()
    {
        stateTimer += Time.deltaTime;

        // Let personality handle state logic
        if (personality != null)
        {
            personality.UpdateBehavior();
        }
        else
        {
            // Default behavior if no personality
            DefaultBehavior();
        }

        UpdateAmbientIdleWander();
    }

    void UpdateAmbientIdleWander()
    {
        if (!ShouldAmbientIdleWander())
        {
            ResetAmbientIdleWander();
            return;
        }

        if (Time.time >= ambientPhaseEndTime)
        {
            if (ambientIsWalking)
            {
                ambientIsWalking = false;
                ambientPhaseEndTime = Time.time + Random.Range(ambientPauseMinDuration, ambientPauseMaxDuration);
                StopMushroom();
            }
            else
            {
                Vector2 randomOffset = Random.insideUnitCircle * ambientWanderRadius;
                Vector3 wanderTarget = originalPosition + new Vector3(randomOffset.x, 0f, randomOffset.y);
                Vector3 toTarget = wanderTarget - transform.position;
                toTarget.y = 0f;

                if (toTarget.sqrMagnitude <= 0.04f)
                {
                    ambientIsWalking = false;
                    ambientPhaseEndTime = Time.time + Random.Range(ambientPauseMinDuration, ambientPauseMaxDuration);
                    StopMushroom();
                    return;
                }

                ambientMoveDirection = toTarget.normalized;
                ambientIsWalking = true;
                ambientPhaseEndTime = Time.time + Random.Range(ambientWalkMinDuration, ambientWalkMaxDuration);
            }
        }

        if (!ambientIsWalking)
        {
            StopMushroom();
            return;
        }

        Vector3 fromOrigin = transform.position - originalPosition;
        fromOrigin.y = 0f;
        if (fromOrigin.magnitude > ambientWanderRadius * 1.15f)
            ambientMoveDirection = (-fromOrigin).normalized;

        MoveMushroom(ambientMoveDirection, ambientWalkSpeed);
    }

    bool ShouldAmbientIdleWander()
    {
        if (!enableAmbientIdleWander)
            return false;

        if (currentState != MushroomState.Idle)
            return false;

        if (playerInRange)
            return false;

        if (personality != null && !personality.AllowAmbientIdleWander)
            return false;

        return true;
    }

    void ResetAmbientIdleWander()
    {
        ambientIsWalking = false;
        ambientPhaseEndTime = 0f;
        ambientMoveDirection = Vector3.zero;
    }

    void DefaultBehavior()
    {
        switch (currentState)
        {
            case MushroomState.Hidden:
                if (playerInRange)
                    ChangeState(MushroomState.Alert);
                break;

            case MushroomState.Alert:
                if (!playerInRange)
                    ChangeState(MushroomState.Hidden);
                break;
        }
    }

    void UpdateVisuals()
    {
        // Visual movement is now driven by the Animator clips.
    }


    public void ChangeState(MushroomState newState)
    {
        if (currentState == newState) return;

        previousState = currentState;
        currentState = newState;
        stateTimer = 0f;

        if (newState != MushroomState.Idle)
            ResetAmbientIdleWander();

        Debug.Log($"Mushroom {name}: State changed from {previousState} to {currentState}");

        // Notify personality of state change
        if (personality != null)
        {
            personality.OnStateChanged(previousState, currentState);
        }

        OnStateChanged();
    }

    public void StopMushroom()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    public void UpdateFleeDirection(Vector3 newDirection)
    {
        fleeDirection = newDirection;
    }

    // For teleporting mushrooms
    public void UpdateOriginalPosition(Vector3 newPosition)
    {
        originalPosition = newPosition;

        // Model stays at its local position, no need to change it
    }

    void OnDrawGizmosSelected()
    {
        if (mushroomData == null) return;

        // Detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, mushroomData.detectionRange);

        // Current flee direction
        if (currentState == MushroomState.Fleeing)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, fleeDirection * 3f);

            // Show current direction to player
            if (player != null)
            {
                Gizmos.color = Color.blue;
                Vector3 dirToPlayer = (player.position - transform.position).normalized;
                Gizmos.DrawRay(transform.position, dirToPlayer * 2f);
            }
        }
    }

    void OnStateChanged()
    {
        switch (currentState)
        {
            case MushroomState.Fleeing:
                StartFleeing();
                break;

            case MushroomState.Collected:
                OnCollected();
                break;
        }
    }

    void StartFleeing()
    {
        if (player != null)
        {
            fleeDirection = (transform.position - player.position).normalized;
        }
    }

    public void MoveMushroom(Vector3 direction, float speed)
    {
        if (rb != null)
        {
            rb.linearVelocity = new Vector3(direction.x * speed, rb.linearVelocity.y, direction.z * speed);
        }
    }

    public void BeginHideSequence()
    {
        if (animationDriver != null)
        {
            animationDriver.BeginHideSequence();
        }
    }

    public void BeginEmergeSequence()
    {
        if (animationDriver != null)
        {
            animationDriver.BeginEmergeSequence();
        }
    }

    public void OnCollected()
    {
        // Immediately hide the mushroom model to give instant feedback
        if (mushroomModel != null)
        {
            mushroomModel.SetActive(false);
        }

        // Also hide the main collider so it can't be collected again
        if (mushroomCollider != null)
        {
            mushroomCollider.enabled = false;
        }

        // Add to inventory instead of just destroying
        if (InventorySystem.Instance != null)
        {
            bool success = InventorySystem.Instance.AddMushroom(mushroomData);

            if (success)
            {
                // Notify mail system
                if (MailSystem.Instance != null)
                {
                    MailSystem.Instance.UpdateMushroomProgress(mushroomData.mushroomType, 1);

                    // Refresh UI
                    var ui = FindObjectOfType<MushroomListUI>();
                    if (ui != null) ui.Refresh();
                }

                // Play collection effect
                if (mushroomData.collectionEffect != null)
                {
                    Instantiate(mushroomData.collectionEffect, transform.position, Quaternion.identity);
                }

                // Destroy mushroom immediately (no delay)
                Destroy(gameObject);
            }
            else
            {
                Debug.Log("Inventory full! Mushroom dropped to ground.");

                // Re-enable components if inventory is full
                if (mushroomModel != null)
                {
                    mushroomModel.SetActive(true);
                }
                if (mushroomCollider != null)
                {
                    mushroomCollider.enabled = true;
                }

                // Convert to pickup instead of destroying
                gameObject.AddComponent<MushroomPickup>().mushroomData = mushroomData;
            }
        }
        else
        {
            // Fallback - destroy immediately
            Debug.LogWarning("InventorySystem not found! Destroying mushroom.");
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") && currentState != MushroomState.Collected)
        {
            ChangeState(MushroomState.Collected);
        }
    }

    public void SetTongueGrabbed(bool grabbed)
    {
        if (grabbed)
        {
            ChangeState(MushroomState.TongueGrabbed);
        }
        else if (currentState == MushroomState.TongueGrabbed)
        {
            ChangeState(MushroomState.Idle);
        }
    }

    public bool IsTongueGrabbed()
    {
        return currentState == MushroomState.TongueGrabbed;
    }

    // Getters for personality scripts
    public float StateTimer => stateTimer;
    public bool PlayerInRange => playerInRange;
    public Transform Player => player;
    public Vector3 OriginalPosition => originalPosition;
    public Vector3 FleeDirection => fleeDirection;
    public MushroomData Data => mushroomData;
}