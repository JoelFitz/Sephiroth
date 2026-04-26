using UnityEngine;

public class CuriousMorelPersonality : MushroomPersonality
{
    public override bool AllowAmbientIdleWander => true;

    [Header("Capped Knight Behavior")]
    public float hookSearchRadius = 16f;
    public float hookScanInterval = 0.1f;
    public float hookApproachSpeed = 6f;
    public float chaseSpeed = 7f;
    public float attackRange = 1.75f;
    public float attackDamage = 20f;
    public float attackInterval = 1f;
    public float unhookDuration = 1f;
    public float unhookRange = 1.5f;

    private MushroomAI hookTarget;
    private float hookBreakStartTime = -1f;
    private float lastHookScanTime = -999f;
    private float lastAttackTime = -999f;
    private PlayerHealthStatus playerHealth;
    private FrogTongueController frogTongueController;

    public override void Initialize(MushroomAI ai, MushroomData mushroomData)
    {
        base.Initialize(ai, mushroomData);
        ResolvePlayerHealth();
        ResolveTongueController();
    }

    public override void UpdateBehavior()
    {
        ResolvePlayerHealth();
        ResolveTongueController();

        if (mushroomAI.currentState == MushroomState.TongueGrabbed)
        {
            mushroomAI.StopMushroom();
            return;
        }

        if (Time.time - lastHookScanTime > hookScanInterval)
        {
            ResolveHookTarget();
            lastHookScanTime = Time.time;
        }

        switch (mushroomAI.currentState)
        {
            case MushroomState.Hidden:
                HandleHiddenState();
                break;

            case MushroomState.Idle:
                HandleIdleState();
                break;

            case MushroomState.Alert:
                HandleAlertState();
                break;

            case MushroomState.Fleeing:
                HandleFleeingState();
                break;
        }
    }

    void HandleHiddenState()
    {
        mushroomAI.StopMushroom();

        if (hookTarget != null || mushroomAI.PlayerInRange)
        {
            ChangeState(MushroomState.Alert);
            return;
        }

        if (mushroomAI.StateTimer > 1f)
            ChangeState(MushroomState.Idle);
    }

    void HandleIdleState()
    {
        mushroomAI.StopMushroom();

        if (hookTarget != null || mushroomAI.PlayerInRange)
        {
            ChangeState(MushroomState.Alert);
        }
    }

    void HandleAlertState()
    {
        if (hookTarget != null)
        {
            if (hookTarget == null || !hookTarget.IsTongueGrabbed())
            {
                ClearHookTarget();
                return;
            }

            Vector3 directionToHookedMushroom = hookTarget.transform.position - transform.position;
            float distanceToHookedMushroom = directionToHookedMushroom.magnitude;

            if (distanceToHookedMushroom > unhookRange)
            {
                hookBreakStartTime = -1f;
                mushroomAI.MoveMushroom(directionToHookedMushroom.normalized, hookApproachSpeed);
            }
            else
            {
                mushroomAI.StopMushroom();

                if (hookBreakStartTime < 0f)
                    hookBreakStartTime = Time.time;

                if (Time.time - hookBreakStartTime >= unhookDuration)
                {
                    TryForceReleaseHookTarget();
                    PlayRustleSound();
                    ClearHookTarget();

                    if (mushroomAI.PlayerInRange)
                        ChangeState(MushroomState.Fleeing);
                    else
                        ChangeState(MushroomState.Hidden);

                    return;
                }
            }

            return;
        }

        if (mushroomAI.PlayerInRange)
            ChangeState(MushroomState.Fleeing);
        else if (mushroomAI.StateTimer > 0.5f)
            ChangeState(MushroomState.Hidden);
    }

    void HandleFleeingState()
    {
        if (hookTarget != null)
        {
            ChangeState(MushroomState.Alert);
            return;
        }

        if (mushroomAI.Player == null)
        {
            mushroomAI.StopMushroom();
            ChangeState(MushroomState.Hidden);
            return;
        }

        Vector3 toPlayer = mushroomAI.Player.position - transform.position;
        float distanceToPlayer = toPlayer.magnitude;

        if (distanceToPlayer <= attackRange)
        {
            mushroomAI.StopMushroom();
            TryAttackPlayer();
        }
        else
        {
            mushroomAI.MoveMushroom(toPlayer.normalized, chaseSpeed);
        }

        if (!mushroomAI.PlayerInRange)
        {
            mushroomAI.StopMushroom();
            ChangeState(MushroomState.Hidden);
        }
    }

    void ResolveHookTarget()
    {
        if (hookTarget != null && !hookTarget.IsTongueGrabbed())
        {
            ClearHookTarget();
        }

        MushroomAI[] mushrooms = FindObjectsByType<MushroomAI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MushroomAI bestTarget = null;
        float bestDistance = hookSearchRadius;

        for (int i = 0; i < mushrooms.Length; i++)
        {
            MushroomAI candidate = mushrooms[i];
            if (candidate == null || candidate == mushroomAI || !candidate.IsTongueGrabbed())
                continue;

            float distanceToCandidate = Vector3.Distance(transform.position, candidate.transform.position);
            if (distanceToCandidate > hookSearchRadius)
                continue;

            if (distanceToCandidate <= bestDistance)
            {
                bestDistance = distanceToCandidate;
                bestTarget = candidate;
            }
        }

        if (bestTarget != hookTarget)
        {
            hookTarget = bestTarget;
            hookBreakStartTime = -1f;
        }
    }

    void TryForceReleaseHookTarget()
    {
        if (hookTarget == null)
            return;

        if (frogTongueController != null && frogTongueController.TryForceReleaseTarget(hookTarget.gameObject))
            return;

        hookTarget.SetTongueGrabbed(false);
    }

    void ResolvePlayerHealth()
    {
        if (playerHealth != null || mushroomAI.Player == null)
            return;

        playerHealth = mushroomAI.Player.GetComponentInParent<PlayerHealthStatus>();
        if (playerHealth == null)
            playerHealth = mushroomAI.Player.GetComponentInChildren<PlayerHealthStatus>(true);
    }

    void ResolveTongueController()
    {
        if (frogTongueController != null)
            return;

        frogTongueController = FindObjectOfType<FrogTongueController>();
    }

    void TryAttackPlayer()
    {
        if (playerHealth == null || Time.time - lastAttackTime < attackInterval)
            return;

        playerHealth.TakeDamage(attackDamage);
        lastAttackTime = Time.time;
        PlayRustleSound();
    }

    void ClearHookTarget()
    {
        hookTarget = null;
        hookBreakStartTime = -1f;
    }

    public override void OnStateChanged(MushroomState fromState, MushroomState toState)
    {
        Debug.Log($"Capped Knight {transform.name}: {fromState} -> {toState}");

        if (toState == MushroomState.Alert || toState == MushroomState.Fleeing)
        {
            PlayRustleSound();
        }
    }

    void PlayRustleSound()
    {
        if (data.rustleSounds != null && data.rustleSounds.Length > 0)
        {
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(data.rustleSounds[Random.Range(0, data.rustleSounds.Length)]);
            }
        }
    }
}