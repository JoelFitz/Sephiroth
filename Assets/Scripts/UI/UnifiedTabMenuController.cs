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
    [SerializeField] private KeyCode questCategoryKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode researchCategoryKey = KeyCode.Alpha2;
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
    }

    private void Update()
    {
        HandleInput();
    }

    private void LateUpdate()
    {
        // Some scene objects can spawn after sceneLoaded callbacks; keep ownership synced.
        if (mailSystem == null || inventorySystem == null || researchBook == null || book3DInteraction == null)
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
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureEventSystem();
        ResolveDependencies();
        ApplyInputOwnership();

        if (!isMenuOpen)
        {
            CloseAllPanels();
        }
        else
        {
            ActivateCategory(currentCategory);
        }
    }

    private void HandleInput()
    {
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

        if (Input.GetKeyDown(closeMenuKey))
        {
            CloseMenu();
            return;
        }

        if (Input.GetKeyDown(questCategoryKey))
            ActivateCategory(TabCategory.Quest);
        else if (Input.GetKeyDown(researchCategoryKey))
            ActivateCategory(TabCategory.Research);
        else if (Input.GetKeyDown(inventoryCategoryKey))
            ActivateCategory(TabCategory.Inventory);
        else if (Input.GetKeyDown(settingsCategoryKey))
            ActivateCategory(TabCategory.Settings);
    }

    private void OpenMenu()
    {
        isMenuOpen = true;
        ActivateCategory(currentCategory);
    }

    private void CloseMenu()
    {
        isMenuOpen = false;
        CloseAllPanels();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
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
                {
                    settingsPanel.SetActive(true);
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                break;
        }
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

        if (mailSystem == null)
            Debug.LogWarning("UnifiedTabMenuController: MailSystem not found in scene.");
        if (inventorySystem == null)
            Debug.LogWarning("UnifiedTabMenuController: InventorySystem not found in scene.");
        if (researchBook == null)
            Debug.LogWarning("UnifiedTabMenuController: MushroomResearchBook not found in scene.");
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
}
