using UnityEngine;
using TMPro;

[System.Serializable]
public class DoorTransitionData
{
    [Header("Scene Configuration")]
    public string targetSceneName = "ForestScene";
    public bool useSceneBuildIndex = false;
    public int targetSceneIndex = 1;

    [Header("Player Spawn")]
    public Vector3 playerSpawnPosition = Vector3.zero;
    public float playerSpawnRotation = 0f;

    [Header("Door Settings")]
    public string doorName = "Forest Door";
    public bool requiresKey = false;
    public string requiredKeyName = "";
}

public class DoorLevelTransition : MonoBehaviour
{
    [Header("Transition Configuration")]
    public DoorTransitionData transitionData;

    [Header("Interaction Settings")]
    public KeyCode interactKey = KeyCode.E;
    public float interactionRange = 3f;
    public LayerMask playerLayerMask = 1 << 0; // Default layer

    [Header("UI Elements")]
    public GameObject interactionPrompt;
    public TextMeshProUGUI promptText = null;
    public string interactPromptText = "Press E to enter";
    public string lockedPromptText = "Door is locked - find the key";

    [Header("Audio & Effects")]
    public AudioClip doorOpenSound;
    public AudioClip doorLockedSound;
    public ParticleSystem transitionEffect;

    [Header("Door Animation")]
    public Animator doorAnimator;
    public string openTrigger = "Open";
    public string closeTrigger = "Close";

    // Private variables
    private bool playerInRange = false;
    private GameObject playerObject;
    private AudioSource audioSource;
    private bool isTransitioning = false;

    void Start()
    {
        InitializeDoor();
    }

    void InitializeDoor()
    {
        // Get or add AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Set up collider as trigger if not already
        Collider doorCollider = GetComponent<Collider>();
        if (doorCollider == null)
        {
            // Add a box collider if none exists
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(2f, 3f, 0.5f);
            boxCollider.isTrigger = true;
        }
        else
        {
            doorCollider.isTrigger = true;
        }

        // Hide interaction prompt initially
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        // Set initial prompt text
        UpdatePromptText();

        Debug.Log($"Door '{transitionData.doorName}' initialized. Target scene: {transitionData.targetSceneName}");
    }

    void Update()
    {
        if (playerInRange && !isTransitioning)
        {
            HandleInteraction();
        }

        UpdatePromptVisibility();
    }

    void HandleInteraction()
    {
        if (Input.GetKeyDown(interactKey))
        {
            Debug.Log($"Player pressed {interactKey} near door '{transitionData.doorName}'");

            if (CanTransition())
            {
                StartTransition();
            }
            else
            {
                HandleLockedDoor();
            }
        }
    }

    bool CanTransition()
    {
        if (!transitionData.requiresKey)
            return true;

        // Check if player has required key
        // You can integrate this with your inventory system
        if (HasRequiredKey())
            return true;

        return false;
    }

    bool HasRequiredKey()
    {
        // Integration with your InventorySystem
        if (InventorySystem.Instance != null && !string.IsNullOrEmpty(transitionData.requiredKeyName))
        {
            return InventorySystem.Instance.HasItem(transitionData.requiredKeyName);
        }

        return !transitionData.requiresKey; // If no key required, return true
    }



    void StartTransition()
    {
        if (isTransitioning) return;

        isTransitioning = true;

        Debug.Log($"Starting transition to scene: {transitionData.targetSceneName}");

        // Play door open sound
        if (doorOpenSound != null && audioSource != null)
            audioSource.PlayOneShot(doorOpenSound);

        // Play door open animation
        if (doorAnimator != null)
            doorAnimator.SetTrigger(openTrigger);

        // Play transition effect
        if (transitionEffect != null)
            transitionEffect.Play();

        // Store player spawn data for the new scene
        StorePlayerSpawnData();

        // Start scene transition with loading screen
        if (transitionData.useSceneBuildIndex)
        {
            AsyncLoader.LoadScene(transitionData.targetSceneIndex);
        }
        else
        {
            AsyncLoader.LoadScene(transitionData.targetSceneName);
        }
    }

    void HandleLockedDoor()
    {
        Debug.Log($"Door '{transitionData.doorName}' is locked!");

        // Play locked sound
        if (doorLockedSound != null && audioSource != null)
            audioSource.PlayOneShot(doorLockedSound);

        // Show locked message (you could add a UI message system here)
        UpdatePromptText();

        // Optional: Add screen shake or other feedback
        StartCoroutine(ShowLockedFeedback());
    }

    System.Collections.IEnumerator ShowLockedFeedback()
    {
        // Flash the prompt text or show a message
        if (promptText != null)
        {
            Color originalColor = promptText.color;
            promptText.color = Color.red;
            yield return new WaitForSeconds(0.5f);
            promptText.color = originalColor;
        }
    }

    void StorePlayerSpawnData()
    {
        // Store spawn data in PlayerPrefs or a persistent data system
        // This allows the new scene to know where to spawn the player
        PlayerPrefs.SetFloat("SpawnPosX", transitionData.playerSpawnPosition.x);
        PlayerPrefs.SetFloat("SpawnPosY", transitionData.playerSpawnPosition.y);
        PlayerPrefs.SetFloat("SpawnPosZ", transitionData.playerSpawnPosition.z);
        PlayerPrefs.SetFloat("SpawnRotY", transitionData.playerSpawnRotation);
        PlayerPrefs.SetString("LastDoorUsed", transitionData.doorName);
        PlayerPrefs.Save();
        
        Debug.Log($"DoorLevelTransition: Stored spawn data - Position: {transitionData.playerSpawnPosition}, Door: '{transitionData.doorName}'");
    }

    void UpdatePromptText()
    {
        if (promptText == null) return;

        if (CanTransition())
        {
            promptText.text = interactPromptText;
            promptText.color = Color.white;
        }
        else
        {
            promptText.text = lockedPromptText;
            promptText.color = Color.red;
        }
    }

    void UpdatePromptVisibility()
    {
        if (interactionPrompt == null) return;

        bool shouldShow = playerInRange && !isTransitioning;

        if (interactionPrompt.activeInHierarchy != shouldShow)
        {
            interactionPrompt.SetActive(shouldShow);
            if (shouldShow)
                UpdatePromptText();
        }
    }

    #region Trigger Detection

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            playerInRange = true;
            playerObject = other.gameObject;

            Debug.Log($"Player entered range of door '{transitionData.doorName}'");

            // Optional: Play door close animation when player approaches
            if (doorAnimator != null)
                doorAnimator.SetTrigger(closeTrigger);
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            playerInRange = false;
            playerObject = null;

            Debug.Log($"Player left range of door '{transitionData.doorName}'");
        }
    }

    bool IsPlayer(GameObject obj)
    {
        // Check if object is on player layer
        int objLayer = obj.layer;
        return (playerLayerMask.value & (1 << objLayer)) != 0 || obj.CompareTag("Player");
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Manually trigger the door transition (useful for scripted events)
    /// </summary>
    public void TriggerTransition()
    {
        if (CanTransition())
        {
            StartTransition();
        }
    }

    public bool CanPlayerInteractNow()
    {
        return playerInRange && !isTransitioning;
    }

    /// <summary>
    /// Set the target scene for this door
    /// </summary>
    public void SetTargetScene(string sceneName)
    {
        transitionData.targetSceneName = sceneName;
        transitionData.useSceneBuildIndex = false;
    }

    /// <summary>
    /// Set the target scene by build index
    /// </summary>
    public void SetTargetScene(int sceneIndex)
    {
        transitionData.targetSceneIndex = sceneIndex;
        transitionData.useSceneBuildIndex = true;
    }

    /// <summary>
    /// Lock or unlock the door
    /// </summary>
    public void SetDoorLocked(bool locked, string keyName = "")
    {
        transitionData.requiresKey = locked;
        if (locked && !string.IsNullOrEmpty(keyName))
        {
            transitionData.requiredKeyName = keyName;
        }

        UpdatePromptText();
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        // Draw interaction range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Draw spawn position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transitionData.playerSpawnPosition, 1f);

        // Draw spawn rotation
        Vector3 forward = Quaternion.Euler(0, transitionData.playerSpawnRotation, 0) * Vector3.forward;
        Gizmos.DrawRay(transitionData.playerSpawnPosition, forward * 2f);

        // Draw door bounds
        Collider doorCollider = GetComponent<Collider>();
        if (doorCollider != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(transform.position + doorCollider.bounds.center, doorCollider.bounds.size);
        }
    }

    #endregion
}

