using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class MushroomResearchEntry
{
    public string mushroomType;
    public string displayName;
    public string scientificName;
    [TextArea(3, 5)]
    public string description;
    [TextArea(2, 3)]
    public string habitat;
    [TextArea(2, 3)]
    public string cookingNotes;
    public Sprite illustration;
    public bool isDiscovered = false;
    public int timesCollected = 0;

    // Progressive unlock thresholds
    public int nameUnlockCount = 1;
    public int habitatUnlockCount = 3;
    public int cookingUnlockCount = 5;
}

public class MushroomResearchBook : MonoBehaviour
{
    private static readonly Dictionary<string, int> SessionCollectionCounts = new Dictionary<string, int>();

    [Header("3D Book Model")]
    public GameObject bookModel; // The 3D book mesh
    public Transform bookClosedPosition; // Where book sits in world
    public Transform bookOpenPosition; // Where book moves when opened (in front of camera)

    [Header("UI Overlay (Screen Space)")]
    public Canvas bookUICanvas; // Screen space overlay canvas
    public GameObject bookUIPanel;

    [Header("Book UI Elements")]
    public TextMeshProUGUI leftPageTitle;
    public TextMeshProUGUI leftPageContent;
    public Image leftPageImage;
    public TextMeshProUGUI rightPageTitle;
    public TextMeshProUGUI rightPageContent;
    public Image rightPageImage;

    [Header("Navigation")]
    public Button nextPageButton;
    public Button previousPageButton;
    public Button closeBookButton;
    public TextMeshProUGUI pageNumberText;

    [Header("Pickup Interaction")]
    public float pickupRange = 2f;
    public KeyCode interactKey = KeyCode.E;
    public GameObject interactionPrompt; // Small world space "Press E" text
    private Book3DInteraction bookInteraction;

    [Header("2D Book Animation")]
    public BookAnimationController bookAnimationController;

    [Header("Research Data")]
    public MushroomResearchEntry[] mushroomEntries;

    // Runtime state
    private List<MushroomResearchEntry> discoveredMushrooms = new List<MushroomResearchEntry>();
    private bool isBookOpen = false;
    private bool isPlayerInRange = false;
    private int currentPagePair = 0; // Which pair of pages we're viewing
    private Transform player;
    private Camera playerCamera;
    [SerializeField] private bool builtInInputEnabled = true;
    [SerializeField] private bool worldInteractionEnabled = true;
    private Coroutine closeBookAnimationRoutine;

    // Page content
    private List<BookPagePair> bookPages = new List<BookPagePair>();

    [System.Serializable]
    public class BookPagePair
    {
        public string leftTitle;
        public string leftContent;
        public Sprite leftImage;
        public string rightTitle;
        public string rightContent;
        public Sprite rightImage;
    }

    public static MushroomResearchBook Instance { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (bookUICanvas != null)
            {
                DontDestroyOnLoad(bookUICanvas.gameObject);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        RebindChildReferences();
    }

    void Start()
    {
        bookInteraction = GetComponent<Book3DInteraction>();

        RebindChildReferences();

        InitializeBook();
        SetupEventListeners();
        LoadProgress();

        // Subscribe to mushroom collection events
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.OnMushroomCollected += OnMushroomCollected;
        }

        // Find player and camera
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerCamera = Camera.main;
        }
    }

    private void RebindChildReferences()
    {
        // Rebind bookAnimationController after scene transitions
        if (bookAnimationController == null && bookUIPanel != null)
        {
            bookAnimationController = bookUIPanel.GetComponent<BookAnimationController>();
            
            if (bookAnimationController == null && bookUICanvas != null)
            {
                bookAnimationController = bookUICanvas.GetComponentInChildren<BookAnimationController>();
            }

            if (bookAnimationController == null)
            {
                Debug.LogWarning("MushroomResearchBook: BookAnimationController not found after scene transition. UI may not animate properly.");
            }
        }

        // Reacquire player reference in case it was recreated
        if (player == null || !player.gameObject.activeSelf)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                player = playerObj.transform;
                playerCamera = Camera.main;
            }
        }
    }

    void InitializeBook()
    {
        // Start with book closed and UI hidden
        if (bookModel != null && bookClosedPosition != null)
            bookModel.transform.position = bookClosedPosition.position;

        if (bookUICanvas != null)
            bookUICanvas.gameObject.SetActive(false);

        if (bookUIPanel != null)
            bookUIPanel.SetActive(false);

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        GenerateBookPages();
    }

    void SetupEventListeners()
    {
        if (nextPageButton != null)
            nextPageButton.onClick.AddListener(NextPageWithAnimation);

        if (previousPageButton != null)
            previousPageButton.onClick.AddListener(PreviousPageWithAnimation);

        if (closeBookButton != null)
            closeBookButton.onClick.AddListener(CloseBook);
    }

    void Update()
    {
        if (worldInteractionEnabled)
            CheckPlayerProximity();
        else if (interactionPrompt != null && interactionPrompt.activeSelf)
            interactionPrompt.SetActive(false);

        if (builtInInputEnabled)
            HandleInteractionInput();

        HandlePageNavigationInput();
    }

    void CheckPlayerProximity()
    {
        if (player == null) return;

        float distance = Vector3.Distance(transform.position, player.position);
        bool wasInRange = isPlayerInRange;
        isPlayerInRange = distance <= pickupRange && !isBookOpen;

        if (isPlayerInRange != wasInRange)
        {
            if (interactionPrompt != null)
                interactionPrompt.SetActive(isPlayerInRange);
        }
    }

    void HandleInteractionInput()
    {
        if (Input.GetKeyDown(interactKey))
        {
            if (isPlayerInRange && !isBookOpen)
            {
                OpenBook();
            }
            else if (isBookOpen)
            {
                CloseBook();
            }
        }
    }

    void HandlePageNavigationInput()
    {
        // Keep page navigation active while the book is open, even when unified menu owns open/close input.
        if (isBookOpen)
        {
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
                PreviousPageWithAnimation();
            else if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
                NextPageWithAnimation();
        }
    }

    public void OpenBook()
    {
        if (isBookOpen) return;

        RebindChildReferences();

        if (closeBookAnimationRoutine != null)
        {
            StopCoroutine(closeBookAnimationRoutine);
            closeBookAnimationRoutine = null;
        }

        isBookOpen = true;

        // Notify interaction script
        if (bookInteraction != null)
            bookInteraction.OnBookStateChanged(true);

        // Hide interaction prompt
        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        // Hide 3D book (replaced by 2D animations)
        if (bookModel != null)
            bookModel.SetActive(false);

        // Show the canvas first regardless of animation path
        if (bookUICanvas != null)
            bookUICanvas.gameObject.SetActive(true);

        // Ensure the book UI panel/buttons are visible while open
        if (bookUIPanel != null)
            bookUIPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Play 2D animation sequence
        if (bookAnimationController != null)
        {
            StartCoroutine(bookAnimationController.OpenBookSequence());
        }
        else
        {
            // No animation controller - canvas already shown above
        }

        // Start from first page
        currentPagePair = 0;
        UpdatePageDisplay();

        // Lock player movement (optional)
        var playerController = player?.GetComponent<OverheadController>();
        if (playerController != null)
            playerController.enabled = false;

        Debug.Log("📖 Research book opened!");
    }

    public void CloseBook()
    {
        if (!isBookOpen) return;

        isBookOpen = false;

        // Notify interaction script
        if (bookInteraction != null)
            bookInteraction.OnBookStateChanged(false);

        // Play 2D close animation sequence
        if (bookAnimationController != null)
        {
            closeBookAnimationRoutine = StartCoroutine(CloseBookWithAnimation());
        }
        else
        {
            // Fallback: just hide the UI if no animation controller
            if (bookUIPanel != null)
                bookUIPanel.SetActive(false);

            if (bookUICanvas != null)
                bookUICanvas.gameObject.SetActive(false);
            
            if (bookModel != null)
                bookModel.SetActive(true);
        }

        // Unlock player movement
        var playerController = player?.GetComponent<OverheadController>();
        if (playerController != null)
            playerController.enabled = true;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        Debug.Log("📖 Research book closed!");
    }

    // Wrapper coroutine to handle close animation and show 3D book
    private IEnumerator CloseBookWithAnimation()
    {
        yield return StartCoroutine(bookAnimationController.CloseBookSequence());

        // Hide panel/buttons and canvas after close sequence completes.
        if (bookUIPanel != null)
            bookUIPanel.SetActive(false);

        if (bookUICanvas != null)
            bookUICanvas.gameObject.SetActive(false);
        
        // Show 3D book again after animations complete
        if (bookModel != null)
            bookModel.SetActive(true);

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

        closeBookAnimationRoutine = null;
    }

    System.Collections.IEnumerator AnimateBookToPosition(Vector3 targetPos, Quaternion targetRot, float duration)
    {
        if (bookModel == null) yield break;

        Vector3 startPos = bookModel.transform.position;
        Quaternion startRot = bookModel.transform.rotation;
        float elapsedTime = 0;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / duration;
            t = Mathf.SmoothStep(0, 1, t); // Smooth easing

            bookModel.transform.position = Vector3.Lerp(startPos, targetPos, t);
            bookModel.transform.rotation = Quaternion.Lerp(startRot, targetRot, t);

            yield return null;
        }

        bookModel.transform.position = targetPos;
        bookModel.transform.rotation = targetRot;
    }

    public void OnMushroomCollected(string mushroomType)
    {
        var entry = mushroomEntries.FirstOrDefault(e => e.mushroomType == mushroomType);
        if (entry != null)
        {
            entry.timesCollected++;
            SessionCollectionCounts[mushroomType] = entry.timesCollected;

            bool wasDiscovered = entry.isDiscovered;
            entry.isDiscovered = true;

            if (!wasDiscovered)
            {
                discoveredMushrooms.Add(entry);
                ShowDiscoveryNotification(entry);
            }

            GenerateBookPages();
            SaveProgress();
        }
    }

    void GenerateBookPages()
    {
        bookPages.Clear();

        // Cover page
        bookPages.Add(new BookPagePair
        {
            leftTitle = "Mushroom Research Journal",
            leftContent = "A Field Guide to Fungal Discoveries\n\nBy: Frog Naturalist\n\nDiscovered Species: " + discoveredMushrooms.Count,
            leftImage = null,
            rightTitle = "Table of Contents",
            rightContent = GenerateTableOfContents(),
            rightImage = null
        });

        // Mushroom pages (one page pair per mushroom)
        foreach (var entry in discoveredMushrooms.OrderBy(e => e.displayName))
        {
            bookPages.Add(CreateMushroomPagePair(entry));
        }
    }

    [ContextMenu("Add Test Mushrooms")]
    void AddTestMushrooms()
    {
        // For testing - manually add some mushrooms as discovered
        foreach (var entry in mushroomEntries)
        {
            if (entry.isDiscovered && !discoveredMushrooms.Contains(entry))
            {
                entry.timesCollected = Mathf.Max(1, entry.timesCollected);
                discoveredMushrooms.Add(entry);
            }
        }

        GenerateBookPages();

        if (isBookOpen)
            UpdatePageDisplay();

        Debug.Log($"Test: Added {discoveredMushrooms.Count} mushrooms to book");
    }

    string GenerateTableOfContents()
    {
        if (discoveredMushrooms.Count == 0)
            return "No species discovered yet.\n\nExplore the world to find mushrooms!";

        string toc = "";
        int pageNum = 2; // Start after cover

        foreach (var entry in discoveredMushrooms.OrderBy(e => e.displayName))
        {
            toc += $"• {entry.displayName} ........ {pageNum}\n";
            if (entry.timesCollected > 1)
                toc += $"  (Collected ×{entry.timesCollected})\n";
            pageNum++;
        }

        return toc;
    }

    BookPagePair CreateMushroomPagePair(MushroomResearchEntry entry)
    {
        // Left page - Basic info and illustration
        string leftContent = "";
        if (entry.timesCollected >= entry.nameUnlockCount)
        {
            leftContent += $"<b>Scientific Name:</b>\n<i>{entry.scientificName}</i>\n\n";
            leftContent += $"<b>Specimens Collected:</b> {entry.timesCollected}\n\n";
            leftContent += $"<b>Description:</b>\n{entry.description}";
        }
        else
        {
            leftContent = "Collect this mushroom to unlock research data.";
        }

        // Right page - Habitat and cooking info
        string rightContent = "";
        if (entry.timesCollected >= entry.habitatUnlockCount)
        {
            rightContent += $"<b>Habitat:</b>\n{entry.habitat}\n\n";
        }
        else if (entry.timesCollected >= entry.nameUnlockCount)
        {
            rightContent += "<b>Habitat:</b>\n<i>[Collect more specimens]</i>\n\n";
        }

        if (entry.timesCollected >= entry.cookingUnlockCount)
        {
            rightContent += $"<b>Culinary Notes:</b>\n{entry.cookingNotes}";
        }
        else if (entry.timesCollected >= entry.nameUnlockCount)
        {
            rightContent += "<b>Culinary Notes:</b>\n<i>[Collect more specimens]</i>";
        }

        if (rightContent == "")
        {
            rightContent = "Additional research data will be unlocked as you collect more specimens of this species.";
        }

        return new BookPagePair
        {
            leftTitle = entry.isDiscovered ? entry.displayName : "Unknown Species",
            leftContent = leftContent,
            leftImage = entry.isDiscovered ? entry.illustration : null,
            rightTitle = "Research Notes",
            rightContent = rightContent,
            rightImage = null
        };
    }

    public void NextPage()
    {
        if (currentPagePair < bookPages.Count - 1)
        {
            currentPagePair++;
            UpdatePageDisplay();
        }
    }

    public void PreviousPage()
    {
        if (currentPagePair > 0)
        {
            currentPagePair--;
            UpdatePageDisplay();
        }
    }

    // Wrapper methods for animation-aware page navigation
    public void NextPageWithAnimation()
    {
        if (!isBookOpen || bookAnimationController == null)
        {
            NextPage();
            return;
        }

        StartCoroutine(NextPageAnimationSequence());
    }

    public void PreviousPageWithAnimation()
    {
        if (!isBookOpen || bookAnimationController == null)
        {
            PreviousPage();
            return;
        }

        StartCoroutine(PreviousPageAnimationSequence());
    }

    private IEnumerator NextPageAnimationSequence()
    {
        // Play flip forward animation
        yield return StartCoroutine(bookAnimationController.FlipForwardSequence());
        
        // Update page content after animation
        NextPage();
    }

    private IEnumerator PreviousPageAnimationSequence()
    {
        // Play flip backward animation
        yield return StartCoroutine(bookAnimationController.FlipBackwardSequence());
        
        // Update page content after animation
        PreviousPage();
    }

    void UpdatePageDisplay()
    {
        if (currentPagePair >= bookPages.Count) return;

        var pagePair = bookPages[currentPagePair];

        // Left page
        if (leftPageTitle != null)
            leftPageTitle.text = pagePair.leftTitle;
        if (leftPageContent != null)
            leftPageContent.text = pagePair.leftContent;
        if (leftPageImage != null)
        {
            leftPageImage.sprite = pagePair.leftImage;
            leftPageImage.gameObject.SetActive(pagePair.leftImage != null);
        }

        // Right page
        if (rightPageTitle != null)
            rightPageTitle.text = pagePair.rightTitle;
        if (rightPageContent != null)
            rightPageContent.text = pagePair.rightContent;
        if (rightPageImage != null)
        {
            rightPageImage.sprite = pagePair.rightImage;
            rightPageImage.gameObject.SetActive(pagePair.rightImage != null);
        }

        // Page number
        if (pageNumberText != null)
            pageNumberText.text = $"Page {(currentPagePair * 2) + 1}-{(currentPagePair * 2) + 2}";

        // Update navigation buttons
        if (nextPageButton != null)
            nextPageButton.interactable = currentPagePair < bookPages.Count - 1;
        if (previousPageButton != null)
            previousPageButton.interactable = currentPagePair > 0;
    }

    void ShowDiscoveryNotification(MushroomResearchEntry entry)
    {
        Debug.Log($"📚 New species discovered: {entry.displayName}");
        // You could add a popup notification here
    }

    void SaveProgress()
    {
        if (GameSessionManager.Instance != null && GameSessionManager.Instance.IsSessionActive)
            return;

        foreach (var entry in mushroomEntries)
        {
            PlayerPrefs.SetInt($"Mushroom_{entry.mushroomType}_Discovered", entry.isDiscovered ? 1 : 0);
            PlayerPrefs.SetInt($"Mushroom_{entry.mushroomType}_Count", entry.timesCollected);
        }
        PlayerPrefs.Save();
    }

    void LoadProgress()
    {
        discoveredMushrooms.Clear();

        bool useSessionState = GameSessionManager.Instance != null && GameSessionManager.Instance.IsSessionActive;

        foreach (var entry in mushroomEntries)
        {
            bool savedDiscovered;
            int savedCount;

            if (useSessionState)
            {
                int sessionCount = 0;
                SessionCollectionCounts.TryGetValue(entry.mushroomType, out sessionCount);
                savedCount = sessionCount;
                savedDiscovered = sessionCount > 0;
            }
            else
            {
                savedDiscovered = PlayerPrefs.GetInt($"Mushroom_{entry.mushroomType}_Discovered", 0) == 1;
                savedCount = PlayerPrefs.GetInt($"Mushroom_{entry.mushroomType}_Count", 0);
            }

            // Use inspector values as fallback/override for testing
            if (!savedDiscovered && entry.isDiscovered)
            {
                // Inspector override - mushroom is marked as discovered for testing
                Debug.Log($"Using inspector override for {entry.displayName}");
                // Don't overwrite inspector values
            }
            else
            {
                // Use saved values
                entry.isDiscovered = savedDiscovered;
                entry.timesCollected = savedCount;
            }

            // Add to discovered list if marked as discovered (either saved or inspector)
            if (entry.isDiscovered)
                discoveredMushrooms.Add(entry);
        }

        GenerateBookPages();

        Debug.Log($"Loaded {discoveredMushrooms.Count} discovered mushrooms");
        foreach (var mushroom in discoveredMushrooms)
        {
            Debug.Log($"- {mushroom.displayName} (×{mushroom.timesCollected})");
        }
    }

    public void ResetForNewGame()
    {
        CloseBook();
        discoveredMushrooms.Clear();
        isBookOpen = false;
        isPlayerInRange = false;
        currentPagePair = 0;
        builtInInputEnabled = true;
        worldInteractionEnabled = true;
        bookPages.Clear();

        if (bookUICanvas != null)
            bookUICanvas.gameObject.SetActive(false);

        if (bookUIPanel != null)
            bookUIPanel.SetActive(false);

        if (interactionPrompt != null)
            interactionPrompt.SetActive(false);

        ResetSessionResearchData();
        Debug.Log("MushroomResearchBook: Reset for new game.");
    }

    public static void ResetSessionResearchData()
    {
        SessionCollectionCounts.Clear();
    }


    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRange);
    }

    void OnDestroy()
    {
        if (MailSystem.Instance != null)
            MailSystem.Instance.OnMushroomCollected -= OnMushroomCollected;
    }

    public bool IsBookOpen()
    {
        return isBookOpen;
    }

    public void SetBuiltInInputEnabled(bool enabled)
    {
        builtInInputEnabled = enabled;
    }

    public void SetWorldInteractionEnabled(bool enabled)
    {
        worldInteractionEnabled = enabled;

        if (!worldInteractionEnabled && interactionPrompt != null)
            interactionPrompt.SetActive(false);
    }
}
