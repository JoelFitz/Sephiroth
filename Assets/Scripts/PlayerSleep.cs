using UnityEngine;
using System.Collections;
using TMPro;

public class PlayerSleep : MonoBehaviour
{
    [Header("Sleep Configuration")]
    public KeyCode interactKey = KeyCode.E;
    public float sleepDuration = 3f; // How long the sleep screen shows
    public float interactionRange = 2f;
    public LayerMask playerLayerMask = 1 << 0; // Default layer

    [Header("Sleep UI")]
    public GameObject sleepUIPanel;
    public TextMeshProUGUI sleepText;
    public CanvasGroup sleepCanvasGroup;
    public float fadeSpeed = 2f;

    [Header("UI Elements")]
    public GameObject interactionPrompt;
    public TextMeshProUGUI promptText;
    public string sleepPromptText = "Press E to sleep";

    [Header("Audio")]
    public AudioClip sleepSound;
    public AudioClip wakeUpSound;

    // Private variables
    private bool playerInRange = false;
    private bool isSleeping = false;
    private GameObject playerObject;
    private AudioSource audioSource;
    private OverheadController playerController;
    private float sleepTimer = 0f;

    // Sleep states
    private enum SleepState { Awake, FadingToSleep, Sleeping, FadingToWake }
    private SleepState currentSleepState = SleepState.Awake;

    void Start()
    {
        InitializeSleepSystem();
    }

    void InitializeSleepSystem()
    {
        // Get or add AudioSource
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        // Set up collider as trigger if not already
        Collider bedCollider = GetComponent<Collider>();
        if (bedCollider == null)
        {
            // Add a box collider if none exists (pond bed area)
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            boxCollider.size = new Vector3(3f, 1f, 2f);
            boxCollider.isTrigger = true;
        }
        else
        {
            bedCollider.isTrigger = true;
        }

        // Hide sleep UI initially
        if (sleepUIPanel != null)
            sleepUIPanel.SetActive(false);

        // Hide interaction prompt initially
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        // Set initial prompt text
        if (promptText != null)
            promptText.text = sleepPromptText;

        // Initialize canvas group alpha
        if (sleepCanvasGroup != null)
            sleepCanvasGroup.alpha = 0f;

        Debug.Log("Player Sleep system initialized at pond bed");
    }

    void Update()
    {
        if (playerInRange && !isSleeping && currentSleepState == SleepState.Awake)
        {
            HandleSleepInteraction();
        }

        UpdatePromptVisibility();
        UpdateSleepState();
    }

    void HandleSleepInteraction()
    {
        if (Input.GetKeyDown(interactKey))
        {
            Debug.Log("Player wants to sleep!");
            StartSleep();
        }
    }

    void StartSleep()
    {
        if (isSleeping) return;

        isSleeping = true;
        currentSleepState = SleepState.FadingToSleep;
        sleepTimer = 0f;

        // Get player controller to disable movement
        if (playerObject != null)
        {
            playerController = playerObject.GetComponent<OverheadController>();
            if (playerController != null)
                playerController.SetMovementEnabled(false);
        }

        // Show sleep UI
        if (sleepUIPanel != null)
            sleepUIPanel.SetActive(true);

        // Hide interaction prompt
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        // Set sleep text
        if (sleepText != null)
            sleepText.text = "Sleeping...";

        // Play sleep sound
        if (sleepSound != null && audioSource != null)
            audioSource.PlayOneShot(sleepSound);

        // Hide cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("Player is going to sleep...");

        // Start the sleep coroutine
        StartCoroutine(SleepCycle());
    }

    IEnumerator SleepCycle()
    {
        // Phase 1: Fade to black
        currentSleepState = SleepState.FadingToSleep;
        yield return StartCoroutine(FadeToBlack());

        // Phase 2: Sleep period
        currentSleepState = SleepState.Sleeping;
        yield return new WaitForSeconds(sleepDuration);

        // Phase 3: Reset quests (new day)
        ResetDailyTasks();

        // Phase 4: Wake up message
        if (sleepText != null)
            sleepText.text = "Good morning!";

        // Play wake up sound
        if (wakeUpSound != null && audioSource != null)
            audioSource.PlayOneShot(wakeUpSound);

        yield return new WaitForSeconds(1f);

        // Phase 5: Fade back to normal
        currentSleepState = SleepState.FadingToWake;
        yield return StartCoroutine(FadeFromBlack());

        // Phase 6: Wake up complete
        WakeUp();
    }

    IEnumerator FadeToBlack()
    {
        if (sleepCanvasGroup == null) yield break;

        float startAlpha = sleepCanvasGroup.alpha;
        float elapsed = 0f;
        float fadeDuration = 1f / fadeSpeed;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeDuration;
            sleepCanvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, progress);
            yield return null;
        }

        sleepCanvasGroup.alpha = 1f;
    }

    IEnumerator FadeFromBlack()
    {
        if (sleepCanvasGroup == null) yield break;

        float startAlpha = sleepCanvasGroup.alpha;
        float elapsed = 0f;
        float fadeDuration = 1f / fadeSpeed;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / fadeDuration;
            sleepCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);
            yield return null;
        }

        sleepCanvasGroup.alpha = 0f;
    }

    void ResetDailyTasks()
    {
        Debug.Log("🌅 New day! Resetting daily tasks...");

        // Reset quest system
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.StartNewDay();
        }
        else
        {
            Debug.LogWarning("MailSystem instance not found!");
        }
    }

    void WakeUp()
    {
        isSleeping = false;
        currentSleepState = SleepState.Awake;

        // Hide sleep UI
        if (sleepUIPanel != null)
            sleepUIPanel.SetActive(false);

        // Re-enable player movement
        if (playerController != null)
            playerController.SetMovementEnabled(true);

        // Show cursor if needed (depending on your game's cursor state)
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Debug.Log("🐸 Player has woken up! Ready for a new day of mushroom foraging!");
    }

    void UpdateSleepState()
    {
        switch (currentSleepState)
        {
            case SleepState.Sleeping:
                sleepTimer += Time.deltaTime;
                // Optional: animate sleeping text with dots
                if (sleepText != null && Time.time % 1f < 0.5f)
                {
                    int dotCount = Mathf.FloorToInt(Time.time) % 4;
                    sleepText.text = "Sleeping" + new string('.', dotCount);
                }
                break;
        }
    }

    void UpdatePromptVisibility()
    {
        if (interactionPrompt == null) return;

        bool shouldShow = playerInRange && !isSleeping && currentSleepState == SleepState.Awake;

        if (interactionPrompt.activeInHierarchy != shouldShow)
        {
            interactionPrompt.SetActive(shouldShow);
        }
    }

    #region Trigger Detection

    void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            playerInRange = true;
            playerObject = other.gameObject;

            Debug.Log("Player approached the pond bed");
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (IsPlayer(other.gameObject))
        {
            playerInRange = false;
            playerObject = null;

            Debug.Log("Player left the pond bed area");
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
    /// Force the player to sleep (useful for scripted events)
    /// </summary>
    public void ForceSleep()
    {
        if (!isSleeping)
        {
            StartSleep();
        }
    }

    /// <summary>
    /// Check if the player is currently sleeping
    /// </summary>
    public bool IsSleeping()
    {
        return isSleeping;
    }

    /// <summary>
    /// Get the current sleep state
    /// </summary>
    public string GetSleepState()
    {
        return currentSleepState.ToString();
    }

    #endregion

    #region Debug

    void OnDrawGizmosSelected()
    {
        // Draw interaction range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Draw bed bounds
        Collider bedCollider = GetComponent<Collider>();
        if (bedCollider != null)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(transform.position + bedCollider.bounds.center, bedCollider.bounds.size);
        }

        // Show sleep state
        if (Application.isPlaying && isSleeping)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position + Vector3.up * 2f, 0.5f);
        }
    }

    #endregion
}

