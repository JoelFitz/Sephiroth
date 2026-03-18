using UnityEngine;
using TMPro;

[RequireComponent(typeof(MushroomResearchBook))]
public class Book3DInteraction : MonoBehaviour
{
    [Header("3D Interaction")]
    public float interactionRange = 2.5f;
    public KeyCode interactKey = KeyCode.E;
    public LayerMask playerLayer = 1;

    [Header("Visual Feedback")]
    public GameObject highlightEffect; // Optional glow/outline when in range
    public TextMeshPro worldSpacePrompt; // World space "Press E" text
    public float promptHeight = 1f; // How high above book the prompt floats
    public Color promptColor = Color.white;

    [Header("3D Book Animation")]
    public float bobAmount = 0.1f; // Gentle up/down movement
    public float bobSpeed = 2f;
    public float rotateSpeed = 30f; // Slow rotation when interactable

    [Header("Audio")]
    public AudioClip pickupSound;
    public AudioClip putDownSound;

    private MushroomResearchBook researchBook;
    private Transform player;
    private bool playerInRange = false;
    private bool isBookOpen = false;
    private Vector3 originalPosition;
    private AudioSource audioSource;
    [SerializeField] private bool interactionEnabled = true;

    // Animation state
    private float bobTimer = 0f;

    void Start()
    {
        researchBook = GetComponent<MushroomResearchBook>();

        // Find player
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
            player = playerObj.transform;

        // Store original position for bobbing animation
        originalPosition = transform.position;

        // Setup audio
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.volume = 0.7f;
            audioSource.spatialBlend = 1f; // 3D audio
        }

        // Setup world space prompt
        SetupWorldSpacePrompt();

        // Hide highlight initially
        if (highlightEffect != null)
            highlightEffect.SetActive(false);
    }

    void SetupWorldSpacePrompt()
    {
        if (worldSpacePrompt == null)
        {
            // Create world space prompt if not assigned
            GameObject promptObj = new GameObject("InteractionPrompt");
            promptObj.transform.SetParent(transform);
            promptObj.transform.localPosition = Vector3.up * promptHeight;

            worldSpacePrompt = promptObj.AddComponent<TextMeshPro>();
            worldSpacePrompt.text = "Press E to read";
            worldSpacePrompt.fontSize = 2f;
            worldSpacePrompt.color = promptColor;
            worldSpacePrompt.alignment = TextAlignmentOptions.Center;
            worldSpacePrompt.sortingOrder = 10;
        }

        // Hide prompt initially
        worldSpacePrompt.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!interactionEnabled)
        {
            if (playerInRange)
            {
                playerInRange = false;
                UpdateInteractionUI();
            }
            return;
        }

        CheckPlayerProximity();
        HandleVisualEffects();
        // Input handling is now in MushroomResearchBook to avoid duplicate calls
    }

    void CheckPlayerProximity()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool wasInRange = playerInRange;
        playerInRange = distance <= interactionRange && !isBookOpen;

        // Update UI when range status changes
        if (playerInRange != wasInRange)
        {
            UpdateInteractionUI();
        }
    }

    void UpdateInteractionUI()
    {
        // Show/hide world space prompt
        if (worldSpacePrompt != null)
        {
            worldSpacePrompt.gameObject.SetActive(playerInRange);

            // Make prompt face the player
            if (playerInRange && player != null)
            {
                Vector3 lookDirection = player.position - worldSpacePrompt.transform.position;
                lookDirection.y = 0; // Keep prompt upright
                if (lookDirection != Vector3.zero)
                {
                    worldSpacePrompt.transform.rotation = Quaternion.LookRotation(lookDirection);
                }
            }
        }

        // Show/hide highlight effect
        if (highlightEffect != null)
            highlightEffect.SetActive(playerInRange);
    }

    void HandleVisualEffects()
    {
        if (isBookOpen) return; // No animations when book is open

        // Gentle bobbing animation
        bobTimer += Time.deltaTime * bobSpeed;
        float bobOffset = Mathf.Sin(bobTimer) * bobAmount;
        transform.position = originalPosition + Vector3.up * bobOffset;

        // Slow rotation when interactable
        if (playerInRange)
        {
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
        }
    }

    void HandleInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            if (playerInRange && !isBookOpen)
            {
                OpenBook();
            }
            else if (isBookOpen)
            {
                CloseBook();
            }
        }
    }

    public void OpenBook()
    {
        if (researchBook == null) return;

        isBookOpen = true;

        // Play pickup sound
        if (audioSource != null && pickupSound != null)
            audioSource.PlayOneShot(pickupSound);

        // Hide interaction UI
        UpdateInteractionUI();

        // Tell research book to open
        researchBook.OpenBook();

        Debug.Log("📖 Picked up research book!");
    }

    public void CloseBook()
    {
        if (researchBook == null) return;

        isBookOpen = false;

        // Play put down sound
        if (audioSource != null && putDownSound != null)
            audioSource.PlayOneShot(putDownSound);

        // Tell research book to close
        researchBook.CloseBook();

        // Reset position and rotation
        transform.position = originalPosition;
        transform.rotation = Quaternion.identity;
        bobTimer = 0f;

        Debug.Log("📚 Put down research book!");
    }

    // Called by MushroomResearchBook when it opens/closes
    public void OnBookStateChanged(bool isOpen)
    {
        isBookOpen = isOpen;
        UpdateInteractionUI();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;

        if (!interactionEnabled)
        {
            playerInRange = false;
            isBookOpen = false;
            UpdateInteractionUI();
        }
    }

    void OnDrawGizmosSelected()
    {
        // Draw interaction range
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, interactionRange);

        // Draw prompt position
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position + Vector3.up * promptHeight, 0.2f);
    }

    void OnDestroy()
    {
        // Clean up any created objects
        if (worldSpacePrompt != null && worldSpacePrompt.gameObject != null)
            Destroy(worldSpacePrompt.gameObject);
    }
}
