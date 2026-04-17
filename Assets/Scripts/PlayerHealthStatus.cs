using UnityEngine;

public class PlayerHealthStatus : MonoBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;
    public float regenPerSecond = 2.5f;
    public float stunnedRegenPerSecond = 12f;

    [Header("Damage Tint")]
    public Color maxDamageTint = new Color(1f, 0.15f, 0.15f, 1f);

    [Header("Stun Physics")]
    public bool enableRagdollOnStun = true;
    public float fallbackToppleTorque = 18f;
    public float fallbackToppleLift = 1.25f;
    public float fallbackKnockdownAngle = 80f;
    public LayerMask terrainLayerMask = ~0;

    public bool IsStunned { get; private set; }

    private OverheadController overheadController;
    private FrogAnimationDriver frogAnimationDriver;
    private Animator animator;
    private CharacterController characterController;
    private Rigidbody rb;
    private Collider rootCollider;
    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock propertyBlock;
    private RigidbodyConstraints normalConstraints;
    private bool rootWasKinematic;
    private bool rootColliderWasEnabled;
    private bool characterControllerWasEnabled;
    private bool animatorWasEnabled;
    private bool frogAnimationDriverWasEnabled;

    private Rigidbody[] ragdollBodies;
    private Collider[] ragdollColliders;
    private bool hasRagdoll;
    private bool ragdollActive;
    private Quaternion preStunRotation;
    private StunMode activeStunMode = StunMode.None;

    private enum StunMode
    {
        None,
        Ragdoll,
        Pose
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    void Awake()
    {
        overheadController = GetComponent<OverheadController>();
        frogAnimationDriver = GetComponent<FrogAnimationDriver>();
        animator = GetComponentInChildren<Animator>(true);
        characterController = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        rootCollider = GetComponent<Collider>();
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        propertyBlock = new MaterialPropertyBlock();

        normalConstraints = rb != null
            ? rb.constraints
            : RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        rootWasKinematic = rb != null && rb.isKinematic;
        rootColliderWasEnabled = rootCollider != null && rootCollider.enabled;
        characterControllerWasEnabled = characterController != null && characterController.enabled;
        animatorWasEnabled = animator != null && animator.enabled;
        frogAnimationDriverWasEnabled = frogAnimationDriver != null && frogAnimationDriver.enabled;

        CacheRagdollParts();
        SetRagdollActive(false);

        currentHealth = Mathf.Clamp(currentHealth, 0f, maxHealth);
        ApplyDamageTint();
    }

    void Update()
    {
        RegenerateHealth(Time.deltaTime);

        if (IsStunned && currentHealth >= maxHealth)
            ExitStun();
    }

    public void TakeDamage(float damageAmount)
    {
        if (damageAmount <= 0f)
            return;

        currentHealth = Mathf.Max(0f, currentHealth - damageAmount);
        ApplyDamageTint();

        if (!IsStunned && currentHealth <= 0f)
            EnterStun();
    }

    public void Heal(float healAmount)
    {
        if (healAmount <= 0f)
            return;

        currentHealth = Mathf.Min(maxHealth, currentHealth + healAmount);
        ApplyDamageTint();

        if (IsStunned && currentHealth >= maxHealth)
            ExitStun();
    }

    void RegenerateHealth(float deltaTime)
    {
        if (currentHealth >= maxHealth)
            return;

        float regenRate = IsStunned ? stunnedRegenPerSecond : regenPerSecond;
        currentHealth = Mathf.Min(maxHealth, currentHealth + regenRate * deltaTime);
        ApplyDamageTint();
    }

    void EnterStun()
    {
        IsStunned = true;
        preStunRotation = transform.rotation;
        activeStunMode = StunMode.None;

        if (overheadController != null)
            overheadController.SetMovementEnabled(false);

        if (frogAnimationDriver != null)
            frogAnimationDriver.enabled = false;

        if (animator != null)
            animator.enabled = false;

        bool useRagdoll = enableRagdollOnStun && IsRagdollUsable();
        if (useRagdoll)
        {
            activeStunMode = StunMode.Ragdoll;

            if (characterController != null)
                characterController.enabled = false;

            SetRagdollActive(true);

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            if (rootCollider != null)
                rootCollider.enabled = false;
        }
        else
        {
            activeStunMode = StunMode.Pose;

            // Safe fallback: keep CharacterController and root collider active to avoid tunneling.
            if (characterController != null)
                characterController.enabled = true;

            if (rootCollider != null)
                rootCollider.enabled = rootColliderWasEnabled;

            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                rb.constraints = RigidbodyConstraints.None;
            }

            // Non-physics knockdown pose so player appears toppled but cannot fall through map.
            Vector3 e = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(fallbackKnockdownAngle, e.y, e.z);
        }

        Debug.Log("Player stunned: movement disabled and faster regeneration started.");
    }

    void ExitStun()
    {
        IsStunned = false;
        currentHealth = maxHealth;

        if (activeStunMode == StunMode.Ragdoll && ragdollActive)
        {
            SetRagdollActive(false);

            if (rb != null)
            {
                rb.isKinematic = rootWasKinematic;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            if (rootCollider != null)
                rootCollider.enabled = rootColliderWasEnabled;
        }

        if (rb != null)
        {
            // Always restore root body simulation state after stun.
            rb.isKinematic = rootWasKinematic;

            // Ensure player stands upright and freeze X after recovery.
            rb.constraints = (normalConstraints | RigidbodyConstraints.FreezeRotationX) & ~RigidbodyConstraints.FreezeRotationY;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (animator != null)
            animator.enabled = animatorWasEnabled;

        if (frogAnimationDriver != null)
            frogAnimationDriver.enabled = frogAnimationDriverWasEnabled;

        if (characterController != null)
            characterController.enabled = characterControllerWasEnabled;

        // Restore upright pose after stun.
        Quaternion uprightRotation = Quaternion.Euler(0f, preStunRotation.eulerAngles.y, 0f);
        if (rb != null)
            rb.rotation = uprightRotation;
        transform.rotation = uprightRotation;

        if (overheadController != null)
            overheadController.SetMovementEnabled(true);

        ApplyDamageTint();
        Debug.Log("Player recovered from stun.");
        activeStunMode = StunMode.None;
    }

    void CacheRagdollParts()
    {
        Rigidbody[] allBodies = GetComponentsInChildren<Rigidbody>(true);
        int bodyCount = 0;
        for (int i = 0; i < allBodies.Length; i++)
        {
            if (allBodies[i] != null && allBodies[i] != rb)
                bodyCount++;
        }

        ragdollBodies = new Rigidbody[bodyCount];
        int idx = 0;
        for (int i = 0; i < allBodies.Length; i++)
        {
            if (allBodies[i] != null && allBodies[i] != rb)
                ragdollBodies[idx++] = allBodies[i];
        }

        Collider[] allColliders = GetComponentsInChildren<Collider>(true);
        int colliderCount = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] == null)
                continue;

            if (allColliders[i] == rootCollider)
                continue;

            if (characterController != null && allColliders[i].gameObject == characterController.gameObject)
                continue;

            colliderCount++;
        }

        ragdollColliders = new Collider[colliderCount];
        idx = 0;
        for (int i = 0; i < allColliders.Length; i++)
        {
            if (allColliders[i] == null)
                continue;

            if (allColliders[i] == rootCollider)
                continue;

            if (characterController != null && allColliders[i].gameObject == characterController.gameObject)
                continue;

            ragdollColliders[idx++] = allColliders[i];
        }

        hasRagdoll = ragdollBodies != null && ragdollBodies.Length > 0;
    }

    void SetRagdollActive(bool active)
    {
        ragdollActive = active;

        if (ragdollBodies != null)
        {
            for (int i = 0; i < ragdollBodies.Length; i++)
            {
                Rigidbody body = ragdollBodies[i];
                if (body == null)
                    continue;

                body.isKinematic = !active;

                if (!active)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }
        }

        if (ragdollColliders != null)
        {
            for (int i = 0; i < ragdollColliders.Length; i++)
            {
                Collider col = ragdollColliders[i];
                if (col == null)
                    continue;

                col.enabled = active;
            }
        }
    }

    bool IsRagdollUsable()
    {
        if (!hasRagdoll)
            return false;

        if (ragdollColliders == null || ragdollColliders.Length == 0)
            return false;

        for (int i = 0; i < ragdollColliders.Length; i++)
        {
            Collider c = ragdollColliders[i];
            if (c != null && !c.isTrigger && CanCollideWithTerrain(c.gameObject.layer))
                return true;
        }

        return false;
    }

    bool CanCollideWithTerrain(int sourceLayer)
    {
        int mask = terrainLayerMask.value;
        for (int i = 0; i < 32; i++)
        {
            int bit = 1 << i;
            if ((mask & bit) == 0)
                continue;

            if (!Physics.GetIgnoreLayerCollision(sourceLayer, i))
                return true;
        }

        return false;
    }

    void ApplyDamageTint()
    {
        if (cachedRenderers == null || cachedRenderers.Length == 0)
            return;

        float damageFraction = 1f - (currentHealth / Mathf.Max(0.001f, maxHealth));
        Color tintedColor = Color.Lerp(Color.white, maxDamageTint, damageFraction);

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(propertyBlock);

            if (renderer.sharedMaterial != null)
            {
                if (renderer.sharedMaterial.HasProperty(BaseColorId))
                    propertyBlock.SetColor(BaseColorId, tintedColor);

                if (renderer.sharedMaterial.HasProperty(ColorId))
                    propertyBlock.SetColor(ColorId, tintedColor);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }
}