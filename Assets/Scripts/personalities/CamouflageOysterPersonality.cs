using System.Reflection;
using UnityEngine;

public class CamouflageOysterPersonality : MushroomPersonality
{
    public override bool AllowAmbientIdleWander => true;

    [Header("Electric Lilac Behavior")]
    public float fleeSpeed = 9f;
    public float fleeDistance = 20f;
    public float electricJoltCooldown = 1.5f;
    public AudioClip electricJoltSound;
    public GameObject electricJoltEffect;

    private Vector3 fleeStartPosition;
    private float lastJoltTime = -999f;
    private MethodInfo enterStunMethod;

    public override void UpdateBehavior()
    {
        if (mushroomAI.currentState == MushroomState.TongueGrabbed)
        {
            mushroomAI.StopMushroom();
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
                HandleFleeingState();
                break;
        }
    }

    void HandleHiddenState()
    {
        mushroomAI.StopMushroom();

        if (mushroomAI.StateTimer > 2f)
            ChangeState(MushroomState.Idle);
    }

    void HandleIdleState()
    {
        mushroomAI.StopMushroom();

        if (mushroomAI.PlayerInRange)
        {
            fleeStartPosition = transform.position;
            ChangeState(MushroomState.Fleeing);
        }
    }

    void HandleAlertState()
    {
        HandleFleeingState();
    }

    void HandleFleeingState()
    {
        if (mushroomAI.Player == null)
        {
            mushroomAI.StopMushroom();
            ChangeState(MushroomState.Hidden);
            return;
        }

        Vector3 fleeDirection = (transform.position - mushroomAI.Player.position).normalized;
        mushroomAI.MoveMushroom(fleeDirection, fleeSpeed);

        if (Vector3.Distance(transform.position, fleeStartPosition) > fleeDistance && !mushroomAI.PlayerInRange)
        {
            mushroomAI.StopMushroom();
            ChangeState(MushroomState.Hidden);
        }

        if (!mushroomAI.PlayerInRange && mushroomAI.StateTimer > 0.75f)
            ChangeState(MushroomState.Hidden);
    }

    void OnTriggerEnter(Collider other)
    {
        TryJoltContact(other.gameObject);
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider != null)
            TryJoltContact(collision.collider.gameObject);
    }

    void TryJoltContact(GameObject touchedObject)
    {
        if (mushroomAI.IsTongueGrabbed() || Time.time - lastJoltTime < electricJoltCooldown)
            return;

        if (touchedObject == null)
            return;

        PlayerHealthStatus playerHealth = touchedObject.GetComponentInParent<PlayerHealthStatus>();
        if (playerHealth == null || playerHealth.IsStunned)
            return;

        lastJoltTime = Time.time;

        if (electricJoltEffect != null)
            Instantiate(electricJoltEffect, transform.position, Quaternion.identity);

        PlayElectricJoltSound();
        TriggerStun(playerHealth);
    }

    void TriggerStun(PlayerHealthStatus playerHealth)
    {
        if (playerHealth == null)
            return;

        if (enterStunMethod == null)
            enterStunMethod = typeof(PlayerHealthStatus).GetMethod("EnterStun", BindingFlags.Instance | BindingFlags.NonPublic);

        if (enterStunMethod != null)
            enterStunMethod.Invoke(playerHealth, null);
    }

    public override void OnStateChanged(MushroomState fromState, MushroomState toState)
    {
        Debug.Log($"Electric Lilac {transform.name}: {fromState} -> {toState}");

        if (toState == MushroomState.Fleeing)
            PlayRustleSound();
    }

    void PlayRustleSound()
    {
        if (data.rustleSounds != null && data.rustleSounds.Length > 0)
        {
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
                audioSource.PlayOneShot(data.rustleSounds[Random.Range(0, data.rustleSounds.Length)]);
        }
    }

    void PlayElectricJoltSound()
    {
        if (electricJoltSound == null)
            return;

        var audioSource = GetComponent<AudioSource>();
        if (audioSource != null)
            audioSource.PlayOneShot(electricJoltSound);
    }

    void OnDrawGizmos()
    {
        if (mushroomAI == null || mushroomAI.Player == null)
            return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 1.75f);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(fleeStartPosition, fleeDistance);
    }
}