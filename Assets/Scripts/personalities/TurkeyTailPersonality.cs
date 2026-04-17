using UnityEngine;

public class TurkeyTailPersonality : MushroomPersonality
{
    public override bool AllowAmbientIdleWander => false;

    [Header("Turkey Tail Behavior")]
    public float startupHideDelay = 0.05f;
    public float emergeDelay = 0.3f;
    public float approachSpeed = 3.2f;
    public float retreatSpeed = 4.2f;
    public float retreatDuration = 1.25f;
    public float retreatDistance = 7f;
    public float injuredPlayerDetectionRange = 12f;

    [Header("Healing Aura")]
    public float healingRadius = 3f;
    public float healPerSecond = 6f;
    public float desiredHealStandDistance = 1.6f;

    private PlayerHealthStatus playerHealth;
    private bool startupHideApplied;
    private float retreatStartTime;
    private Vector3 retreatFromPosition;

    public override void Initialize(MushroomAI ai, MushroomData mushroomData)
    {
        base.Initialize(ai, mushroomData);
        ResolvePlayerHealth();
    }

    public override void UpdateBehavior()
    {
        ResolvePlayerHealth();
        ApplyHealingAura();

        // MushroomAI defaults to Idle on startup. Force Turkey Tail to start hidden.
        if (!startupHideApplied && mushroomAI.StateTimer >= startupHideDelay)
        {
            startupHideApplied = true;
            if (mushroomAI.currentState != MushroomState.Hidden)
                ChangeState(MushroomState.Hidden);
            return;
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
                HandleHelpingState();
                break;
        }
    }

    void HandleHiddenState()
    {
        mushroomAI.StopMushroom();

        if (ShouldHelpPlayer())
            ChangeState(MushroomState.Alert);
    }

    void HandleIdleState()
    {
        mushroomAI.StopMushroom();

        if (ShouldHelpPlayer())
        {
            ChangeState(MushroomState.Alert);
            return;
        }

        if (IsPlayerFullHealth())
            ChangeState(MushroomState.Hidden);
    }

    void HandleAlertState()
    {
        mushroomAI.StopMushroom();

        if (!ShouldHelpPlayer())
        {
            ChangeState(MushroomState.Hidden);
            return;
        }

        if (mushroomAI.StateTimer >= emergeDelay)
            ChangeState(MushroomState.Fleeing);
    }

    void HandleHelpingState()
    {
        if (mushroomAI.Player == null)
        {
            mushroomAI.StopMushroom();
            ChangeState(MushroomState.Hidden);
            return;
        }

        // When player is healthy, retreat and hide again.
        if (IsPlayerFullHealth())
        {
            Vector3 away = transform.position - mushroomAI.Player.position;
            away.y = 0f;

            if (away.sqrMagnitude > 0.001f)
                mushroomAI.MoveMushroom(away.normalized, retreatSpeed);
            else
                mushroomAI.StopMushroom();

            if (retreatStartTime <= 0f)
            {
                retreatStartTime = Time.time;
                retreatFromPosition = transform.position;
            }

            float retreatedDistance = Vector3.Distance(transform.position, retreatFromPosition);
            if ((Time.time - retreatStartTime) >= retreatDuration || retreatedDistance >= retreatDistance)
            {
                retreatStartTime = 0f;
                mushroomAI.StopMushroom();
                ChangeState(MushroomState.Hidden);
            }

            return;
        }

        retreatStartTime = 0f;

        Vector3 toPlayer = mushroomAI.Player.position - transform.position;
        toPlayer.y = 0f;

        float distanceToPlayer = toPlayer.magnitude;

        // Stop when close enough so Turkey Tail doesn't push the player while healing.
        if (distanceToPlayer <= desiredHealStandDistance || distanceToPlayer <= healingRadius * 0.6f)
        {
            mushroomAI.StopMushroom();
            return;
        }

        if (toPlayer.sqrMagnitude < 0.05f)
        {
            mushroomAI.StopMushroom();
            return;
        }

        mushroomAI.MoveMushroom(toPlayer.normalized, approachSpeed);
    }

    bool ShouldHelpPlayer()
    {
        if (mushroomAI.Player == null || playerHealth == null)
            return false;

        if (playerHealth.currentHealth >= playerHealth.maxHealth)
            return false;

        float dist = Vector3.Distance(transform.position, mushroomAI.Player.position);
        return dist <= injuredPlayerDetectionRange;
    }

    bool IsPlayerFullHealth()
    {
        return playerHealth == null || playerHealth.currentHealth >= playerHealth.maxHealth;
    }

    void ApplyHealingAura()
    {
        if (mushroomAI.Player == null || playerHealth == null)
            return;

        if (playerHealth.currentHealth >= playerHealth.maxHealth)
            return;

        float dist = Vector3.Distance(transform.position, mushroomAI.Player.position);
        if (dist > healingRadius)
            return;

        playerHealth.Heal(healPerSecond * Time.deltaTime);
    }

    void ResolvePlayerHealth()
    {
        if (playerHealth != null)
            return;

        if (mushroomAI.Player == null)
            return;

        playerHealth = mushroomAI.Player.GetComponentInParent<PlayerHealthStatus>();
        if (playerHealth == null)
            playerHealth = mushroomAI.Player.GetComponentInChildren<PlayerHealthStatus>();
    }

    public override void OnStateChanged(MushroomState fromState, MushroomState toState)
    {
        if (toState == MushroomState.Alert)
        {
            PlayRustleSound();

            if (fromState == MushroomState.Hidden)
                mushroomAI.BeginEmergeSequence();
        }
        else if (toState == MushroomState.Hidden)
        {
            mushroomAI.BeginHideSequence();
        }
    }

    void PlayRustleSound()
    {
        if (data.rustleSounds == null || data.rustleSounds.Length == 0)
            return;

        var audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
            audioSource.PlayOneShot(data.rustleSounds[UnityEngine.Random.Range(0, data.rustleSounds.Length)]);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, injuredPlayerDetectionRange);

        Gizmos.color = new Color(0.2f, 1f, 0.45f, 1f);
        Gizmos.DrawWireSphere(transform.position, healingRadius);
    }
}