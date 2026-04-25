using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;

public class UnifiedTabMenuController : MonoBehaviour
{
    public enum TabCategory
    {
        Quest = 0,
        Research = 1,
        Inventory = 2,
        Settings = 3
    }

    public static UnifiedTabMenuController Instance { get; private set; }

    [Header("Input")]
    [SerializeField] private KeyCode toggleMenuKey = KeyCode.Tab;
    [SerializeField] private KeyCode closeMenuKey = KeyCode.Escape;
    [SerializeField] private KeyCode questCategoryKey = KeyCode.L;
    [SerializeField] private KeyCode researchCategoryKey = KeyCode.B;
    [SerializeField] private KeyCode inventoryCategoryKey = KeyCode.Alpha3;
    [SerializeField] private KeyCode settingsCategoryKey = KeyCode.Alpha4;

    [Header("Settings Panel (Optional)")]
    [SerializeField] private GameObject settingsPanel;

    [Header("State")]
    [SerializeField] private bool isMenuOpen;
    [SerializeField] private TabCategory currentCategory = TabCategory.Quest;

    private MailSystem mailSystem;
    private InventorySystem inventorySystem;
    private MushroomResearchBook researchBook;
    private Book3DInteraction book3DInteraction;
    private OverheadController playerController;
    private bool hasLoggedMissingDependencies;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        EnsureEventSystem();
        ResolveDependencies();
        ApplyInputOwnership();
        CloseAllPanels();
        SetGameplayMode(true);
    }

    private void Update()
    {
        HandleInput();
    }

    private void LateUpdate()
    {
        // Some scene objects can spawn after sceneLoaded callbacks; keep ownership synced.
        if (mailSystem == null || inventorySystem == null || researchBook == null || book3DInteraction == null || playerController == null)
        {
            ResolveDependencies();
            ApplyInputOwnership();
        }
    }

    public static UnifiedTabMenuController EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject("UnifiedTabMenuController");
        return root.AddComponent<UnifiedTabMenuController>();
    }

    public void ResetMenuState()
    {
        isMenuOpen = false;
        currentCategory = TabCategory.Quest;
        CloseAllPanels();
        SetGameplayMode(true);
    }

    public void OpenCategory(TabCategory category)
    {
        isMenuOpen = true;
        ActivateCategory(category);
    }

    public void OpenQuestCategory()
    {
        OpenCategory(TabCategory.Quest);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        ResolveDependencies();
        ApplyInputOwnership();

        if (!isMenuOpen)
        {
            CloseAllPanels();
            SetGameplayMode(true);
        }
        else
        {
            ActivateCategory(currentCategory);
        }
    }

    private void HandleInput()
    {
        // Dedicated category keys should work even when the unified menu is currently closed.
        if (Input.GetKeyDown(questCategoryKey))
        {
            OpenCategory(TabCategory.Quest);
            return;
        }

        if (Input.GetKeyDown(researchCategoryKey))
        {
            OpenCategory(TabCategory.Research);
            return;
        }

        if (Input.GetKeyDown(closeMenuKey))
        {
            if (isMenuOpen)
                CloseMenu();
            else
                OpenCategory(TabCategory.Settings);
            return;
        }

        if (Input.GetKeyDown(toggleMenuKey))
        {
            if (isMenuOpen)
                CloseMenu();
            else
                OpenMenu();
            return;
        }

        if (!isMenuOpen)
            return;

        if (Input.GetKeyDown(inventoryCategoryKey))
            ActivateCategory(TabCategory.Inventory);
        else if (Input.GetKeyDown(settingsCategoryKey))
            ActivateCategory(TabCategory.Settings);
    }

    private void OpenMenu()
    {
        isMenuOpen = true;
        ActivateCategory(TabCategory.Inventory);
    }

    private void CloseMenu()
    {
        isMenuOpen = false;
        CloseAllPanels();
        SetGameplayMode(true);
    }

    private void ActivateCategory(TabCategory category)
    {
        currentCategory = category;

        CloseAllPanels();

        switch (category)
        {
            case TabCategory.Quest:
                if (mailSystem != null)
                    mailSystem.OpenMailUI();
                break;
            case TabCategory.Research:
                if (researchBook != null)
                    researchBook.OpenBook();
                break;
            case TabCategory.Inventory:
                if (inventorySystem != null)
                    inventorySystem.OpenInventory();
                break;
            case TabCategory.Settings:
                if (settingsPanel != null)
                    settingsPanel.SetActive(true);
                break;
        }

        SetGameplayMode(false);
    }

    private void CloseAllPanels()
    {
        if (mailSystem != null)
            mailSystem.CloseMailUI();

        if (researchBook != null)
            researchBook.CloseBook();

        if (inventorySystem != null)
            inventorySystem.CloseInventory();

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    private void ResolveDependencies()
    {
        if (mailSystem == null)
            mailSystem = MailSystem.Instance != null ? MailSystem.Instance : Object.FindFirstObjectByType<MailSystem>();

        if (inventorySystem == null)
            inventorySystem = InventorySystem.Instance != null ? InventorySystem.Instance : Object.FindFirstObjectByType<InventorySystem>();

        if (researchBook == null)
            researchBook = Object.FindFirstObjectByType<MushroomResearchBook>();

        if (book3DInteraction == null)
            book3DInteraction = Object.FindFirstObjectByType<Book3DInteraction>();

        if (playerController == null)
            playerController = FindPreferredPlayerController();

        bool missingMail = mailSystem == null;
        bool missingInventory = inventorySystem == null;
        bool missingResearch = researchBook == null;
        bool missingPlayer = playerController == null;

        bool anyMissing = missingMail || missingInventory || missingResearch || missingPlayer;
        if (anyMissing)
        {
            if (!hasLoggedMissingDependencies)
            {
                if (missingMail)
                    Debug.LogWarning("UnifiedTabMenuController: MailSystem not found in scene.");
                if (missingInventory)
                    Debug.LogWarning("UnifiedTabMenuController: InventorySystem not found in scene.");
                if (missingResearch)
                    Debug.LogWarning("UnifiedTabMenuController: MushroomResearchBook not found in scene.");
                if (missingPlayer)
                    Debug.LogWarning("UnifiedTabMenuController: OverheadController not found in scene.");

                hasLoggedMissingDependencies = true;
            }
        }
        else
        {
            hasLoggedMissingDependencies = false;
        }
    }

    private OverheadController FindPreferredPlayerController()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        OverheadController[] controllers = Object.FindObjectsByType<OverheadController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < controllers.Length; i++)
        {
            OverheadController candidate = controllers[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.scene == activeScene)
                return candidate;
        }

        return controllers.Length > 0 ? controllers[0] : null;
    }

    private void ApplyInputOwnership()
    {
        if (mailSystem != null)
            mailSystem.SetBuiltInInputEnabled(false);

        if (inventorySystem != null)
            inventorySystem.SetBuiltInInputEnabled(false);

        if (researchBook != null)
        {
            researchBook.SetBuiltInInputEnabled(false);
            researchBook.SetWorldInteractionEnabled(false);
        }

        if (book3DInteraction != null)
            book3DInteraction.SetInteractionEnabled(false);
    }

    private void EnsureEventSystem()
    {
        EventSystem eventSystem = Object.FindFirstObjectByType<EventSystem>();
        if (eventSystem != null)
            return;

        GameObject eventSystemObject = new GameObject("EventSystem");
        eventSystemObject.AddComponent<EventSystem>();
        eventSystemObject.AddComponent<StandaloneInputModule>();
        DontDestroyOnLoad(eventSystemObject);
    }

    private void SetGameplayMode(bool gameplayMode)
    {
        SetCursorForMenuOpen(!gameplayMode);

        if (playerController != null)
            playerController.SetMovementEnabled(gameplayMode);
    }

    private void SetCursorForMenuOpen(bool menuOpen)
    {
        Cursor.lockState = menuOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = menuOpen;
    }
}
