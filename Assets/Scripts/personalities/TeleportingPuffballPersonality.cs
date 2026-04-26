using UnityEngine;

public class TeleportingPuffballPersonality : MushroomPersonality
{
    public override bool AllowAmbientIdleWander => true;

    [Header("Sky Gem Behavior")]
    public float alertTime = 0.5f;
    public int maxTeleports = 1;
    public float hiddenResetTime = 3f;
    public GameObject teleportEffect;
    public AudioClip teleportSound;
    public SkyGemTeleportSpots teleportSpots;

    private int teleportCount = 0;

    public override void Initialize(MushroomAI ai, MushroomData mushroomData)
    {
        base.Initialize(ai, mushroomData);
        if (teleportSpots == null)
            teleportSpots = mushroomAI.gameObject.GetComponent<SkyGemTeleportSpots>();
    }

    public override void UpdateBehavior()
    {
        if (mushroomAI.currentState == MushroomState.TongueGrabbed)
        {
            mushroomAI.StopMushroom();
            return;
        }

        if (teleportSpots == null)
            teleportSpots = mushroomAI.gameObject.GetComponent<SkyGemTeleportSpots>();

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
                HandleHiddenState();
                break;
        }
    }

    void HandleHiddenState()
    {
        mushroomAI.StopMushroom();

        if (mushroomAI.StateTimer > hiddenResetTime)
        {
            teleportCount = 0;
            ChangeState(MushroomState.Idle);
        }
    }

    void HandleIdleState()
    {
        mushroomAI.StopMushroom();

        if (mushroomAI.PlayerInRange)
            ChangeState(MushroomState.Alert);
    }

    void HandleAlertState()
    {
        mushroomAI.StopMushroom();

        if (!mushroomAI.PlayerInRange)
        {
            ChangeState(MushroomState.Idle);
            return;
        }

        if (mushroomAI.StateTimer < alertTime)
            return;

        if (teleportCount >= maxTeleports)
        {
            ChangeState(MushroomState.Hidden);
            return;
        }

        TeleportToConfiguredSpot();
        teleportCount++;
        ChangeState(MushroomState.Hidden);
    }

    void TeleportToConfiguredSpot()
    {
        if (teleportSpots == null)
            teleportSpots = mushroomAI.gameObject.GetComponent<SkyGemTeleportSpots>();

        if (teleportSpots == null)
        {
            Debug.LogWarning($"Sky Gem {transform.name} has no teleport spots configured.");
            return;
        }

        if (!teleportSpots.TryGetTeleportSpot(out Vector3 newPosition))
        {
            Debug.LogWarning($"Sky Gem {transform.name} has no valid teleport spots.");
            return;
        }

        Transform mushroomRoot = mushroomAI.transform;

        if (teleportEffect != null)
            Instantiate(teleportEffect, mushroomRoot.position, Quaternion.identity);

        mushroomRoot.position = newPosition;
        mushroomAI.UpdateOriginalPosition(newPosition);

        if (teleportEffect != null)
            Instantiate(teleportEffect, mushroomRoot.position, Quaternion.identity);

        PlayTeleportSound();

        Debug.Log($"Sky Gem teleported ROOT to {newPosition}");
    }

    public override void OnStateChanged(MushroomState fromState, MushroomState toState)
    {
        Debug.Log($"Sky Gem {mushroomAI.transform.name}: {fromState} -> {toState} (Teleports: {teleportCount}/{maxTeleports})");

        if (toState == MushroomState.Alert)
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

    void PlayTeleportSound()
    {
        if (teleportSound != null)
        {
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
            {
                audioSource.PlayOneShot(teleportSound);
                return;
            }
        }

        PlayRustleSound();
    }

    void OnDrawGizmos()
    {
        if (teleportSpots == null || teleportSpots.teleportSpots == null)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < teleportSpots.teleportSpots.Length; i++)
        {
            Transform spot = teleportSpots.teleportSpots[i];
            if (spot != null)
                Gizmos.DrawWireSphere(spot.position, 0.35f);
        }
    }
}