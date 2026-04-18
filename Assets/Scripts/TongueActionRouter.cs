using UnityEngine;

/// <summary>
/// Routes a single tongue action input through context-sensitive priorities.
/// Priority: Swing spot -> Rope elevator -> Tongue grapple zone -> Frog lasso/tongue.
/// </summary>
public class TongueActionRouter : MonoBehaviour
{
    [Header("Input")]
    public KeyCode tongueActionKey = KeyCode.Space;

    [Header("Priority Systems")]
    public SwingGrappleSystem swingGrappleSystem;
    public TongueGrappleSystem tongueGrappleSystem;
    public FrogTongueController frogTongueController;

    [Header("Elevator Selection")]
    [Tooltip("Only elevators within this radius are considered for unified tongue action.")]
    public float elevatorSearchRadius = 7f;

    private RopeElevator[] elevatorCache;
    private float elevatorCacheAge;
    private const float ElevatorCacheLifetime = 1f;

    private RopeElevator currentElevator;
    private PlayerHealthStatus playerHealthStatus;

    void Awake()
    {
        playerHealthStatus = GetComponent<PlayerHealthStatus>() ?? GetComponentInParent<PlayerHealthStatus>();

        if (swingGrappleSystem == null)
            swingGrappleSystem = GetComponent<SwingGrappleSystem>() ?? GetComponentInParent<SwingGrappleSystem>();

        if (tongueGrappleSystem == null)
            tongueGrappleSystem = GetComponent<TongueGrappleSystem>() ?? GetComponentInParent<TongueGrappleSystem>();

        if (frogTongueController == null)
            frogTongueController = GetComponent<FrogTongueController>() ?? GetComponentInParent<FrogTongueController>();

        RefreshElevatorCache();
    }

    void Update()
    {
        elevatorCacheAge += Time.deltaTime;
        if (elevatorCacheAge > ElevatorCacheLifetime || elevatorCache == null)
            RefreshElevatorCache();

        if (IsTongueActionBlocked())
            return;

        if (Input.GetKeyDown(tongueActionKey))
            HandleTongueAction();
    }

    void HandleTongueAction()
    {
        if (IsTongueActionBlocked())
            return;

        if (TrySwingAction())
            return;

        if (TryElevatorAction())
            return;

        if (TryTongueGrappleAction())
            return;

        TryFrogTongueAction();
    }

    bool TrySwingAction()
    {
        if (swingGrappleSystem == null)
            return false;

        return swingGrappleSystem.TryUnifiedTongueAction();
    }

    bool TryElevatorAction()
    {
        if (currentElevator != null)
        {
            if (currentElevator.isActiveAndEnabled && currentElevator.TryUnifiedTongueAction())
                return true;

            if (!currentElevator.isActiveAndEnabled)
                currentElevator = null;
        }

        RopeElevator bestElevator = FindBestStartableElevator();
        if (bestElevator == null)
            return false;

        if (!bestElevator.TryUnifiedTongueAction())
            return false;

        currentElevator = bestElevator;
        return true;
    }

    bool TryTongueGrappleAction()
    {
        if (tongueGrappleSystem == null)
            return false;

        return tongueGrappleSystem.TryUnifiedTongueAction();
    }

    bool TryFrogTongueAction()
    {
        if (frogTongueController == null)
            return false;

        return frogTongueController.TryUnifiedTongueAction();
    }

    RopeElevator FindBestStartableElevator()
    {
        if (elevatorCache == null || elevatorCache.Length == 0)
            return null;

        RopeElevator best = null;
        float bestDistance = elevatorSearchRadius;

        for (int i = 0; i < elevatorCache.Length; i++)
        {
            RopeElevator elevator = elevatorCache[i];
            if (elevator == null || !elevator.isActiveAndEnabled)
                continue;

            if (!elevator.CanStartRide())
                continue;

            float distance = Vector3.Distance(transform.position, elevator.transform.position);
            if (distance > bestDistance)
                continue;

            bestDistance = distance;
            best = elevator;
        }

        return best;
    }

    void RefreshElevatorCache()
    {
        elevatorCache = FindObjectsOfType<RopeElevator>();
        elevatorCacheAge = 0f;
    }

    bool IsTongueActionBlocked()
    {
        return playerHealthStatus != null && playerHealthStatus.IsStunned;
    }
}
