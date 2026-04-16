using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;

public class IconicSpellcapPersonality : MushroomPersonality
{
    [Header("Iconic Spellcap Behavior")]
    public float detectTime = 0.3f;
    public float chaseTime = 5f;
    public float chaseSpeed = 6f;
    public float attackRange = 1.5f;
    public float attackInterval = 2f;
    public float damagePerHit = 30f;

    [Header("Hide and Retreat")]
    public float hiddenCooldownTime = 2f;
    public float stunRetreatTime = 1.25f;

    private float lastAttackTime = -999f;
    private float retreatUntilTime = 0f;
    private bool isRetreatingFromStun = false;
    private Component playerHealth;
    private PropertyInfo isStunnedProperty;
    private MethodInfo takeDamageMethod;

    public override void Initialize(MushroomAI ai, MushroomData mushroomData)
    {
        base.Initialize(ai, mushroomData);
        ResolvePlayerHealth();
    }

    public override void UpdateBehavior()
    {
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
                HandleChaseState();
                break;
        }
    }

    void HandleHiddenState()
    {
        mushroomAI.StopMushroom();

        ResolvePlayerHealth();
        if (IsPlayerStunned())
            return;

        if (mushroomAI.PlayerInRange)
        {
            ChangeState(MushroomState.Alert);
        }
        else if (mushroomAI.StateTimer > hiddenCooldownTime)
        {
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

        ResolvePlayerHealth();
        if (IsPlayerStunned())
        {
            ChangeState(MushroomState.Hidden);
            return;
        }

        if (mushroomAI.StateTimer > detectTime)
            ChangeState(MushroomState.Fleeing); // Using Fleeing as Chase state

        if (!mushroomAI.PlayerInRange)
            ChangeState(MushroomState.Idle);
    }

    void HandleChaseState()
    {
        if (mushroomAI.Player == null)
        {
            mushroomAI.StopMushroom();
            return;
        }

        ResolvePlayerHealth();

        Vector3 toPlayer = mushroomAI.Player.position - transform.position;
        Vector3 horizontalToPlayer = new Vector3(toPlayer.x, 0f, toPlayer.z);
        Vector3 directionToPlayer = horizontalToPlayer.sqrMagnitude > 0.0001f
            ? horizontalToPlayer.normalized
            : Vector3.zero;

        // If the player is stunned, retreat briefly then hide.
        if (IsPlayerStunned())
        {
            if (!isRetreatingFromStun)
            {
                isRetreatingFromStun = true;
                retreatUntilTime = Time.time + stunRetreatTime;
            }

            if (Time.time < retreatUntilTime)
            {
                mushroomAI.MoveMushroom(-directionToPlayer, chaseSpeed);
            }
            else
            {
                isRetreatingFromStun = false;
                mushroomAI.StopMushroom();
                ChangeState(MushroomState.Hidden);
            }

            return;
        }

        isRetreatingFromStun = false;

        float distanceToPlayer = horizontalToPlayer.magnitude;

        if (distanceToPlayer <= attackRange)
        {
            mushroomAI.StopMushroom();
            TryAttackPlayer();
        }
        else
        {
            mushroomAI.MoveMushroom(directionToPlayer, chaseSpeed);
        }

        // Stop chasing after time limit or if player is out of range.
        if (mushroomAI.StateTimer > chaseTime || !mushroomAI.PlayerInRange)
        {
            mushroomAI.StopMushroom();
            ChangeState(MushroomState.Hidden);
        }
    }

    void TryAttackPlayer()
    {
        if (Time.time - lastAttackTime < attackInterval)
            return;

        ResolvePlayerHealth();

        bool didApplyDamage = false;

        if (takeDamageMethod != null)
        {
            takeDamageMethod.Invoke(playerHealth, new object[] { damagePerHit });
            didApplyDamage = true;
        }

        // Fallback path if direct reflection binding fails for any reason.
        if (!didApplyDamage && mushroomAI.Player != null)
        {
            Transform root = mushroomAI.Player.root;
            if (root != null)
            {
                root.gameObject.SendMessage("TakeDamage", damagePerHit, SendMessageOptions.DontRequireReceiver);
                didApplyDamage = true;
            }
        }

        if (didApplyDamage)
            lastAttackTime = Time.time;
    }

    void ResolvePlayerHealth()
    {
        if (playerHealth != null && isStunnedProperty != null && takeDamageMethod != null)
            return;

        if (mushroomAI.Player == null)
            return;

        playerHealth = null;
        isStunnedProperty = null;
        takeDamageMethod = null;

        List<Component> candidates = new List<Component>();
        candidates.AddRange(mushroomAI.Player.GetComponentsInParent<Component>(true));
        candidates.AddRange(mushroomAI.Player.GetComponentsInChildren<Component>(true));

        for (int i = 0; i < candidates.Count; i++)
        {
            Component c = candidates[i];
            if (c != null && c.GetType().Name == "PlayerHealthStatus")
            {
                playerHealth = c;
                break;
            }
        }

        if (playerHealth != null)
        {
            Type t = playerHealth.GetType();
            isStunnedProperty = t.GetProperty("IsStunned", BindingFlags.Public | BindingFlags.Instance);
            takeDamageMethod = t.GetMethod("TakeDamage", BindingFlags.Public | BindingFlags.Instance);
        }
    }

    bool IsPlayerStunned()
    {
        if (playerHealth == null || isStunnedProperty == null)
            return false;

        object value = isStunnedProperty.GetValue(playerHealth);
        if (value is bool isStunned)
            return isStunned;

        return false;
    }

    public override void OnStateChanged(MushroomState fromState, MushroomState toState)
    {
        Debug.Log($"Iconic Spellcap {transform.name}: {fromState} -> {toState}");

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
        if (data.rustleSounds != null && data.rustleSounds.Length > 0)
        {
            var audioSource = GetComponent<AudioSource>();
            if (audioSource != null)
                audioSource.PlayOneShot(data.rustleSounds[UnityEngine.Random.Range(0, data.rustleSounds.Length)]);
        }
    }
}