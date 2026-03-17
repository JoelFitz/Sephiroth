using UnityEngine;
using UnityEngine.SceneManagement;

public class GameController : MonoBehaviour
{
    [Header("Game State")]
    public bool showQuestUIOnStart = false;
    public GameObject questUI;
    public GameObject player;

    [Header("Input")]
    public KeyCode toggleUIKey = KeyCode.Tab;
    public KeyCode testCollectKey = KeyCode.E;

    [Header("Scene Behavior")]
    [SerializeField] private string homeSceneName = "HomeScene";
    [SerializeField] private bool forceQuestUIOnFirstHomeSceneLoad = true;

    public static GameController Instance { get; private set; }

    private bool isQuestUIVisible = true;
    private bool hasForcedQuestUIOnFirstHomeSceneLoad;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (questUI != null)
                DontDestroyOnLoad(questUI);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        SetupGame();
    }

    void SetupGame()
    {
        // Ensure MailSystem exists
        if (MailSystem.Instance == null)
        {
            Debug.LogWarning("MailSystem not found! Make sure it's in the scene.");
        }

        // Show/hide quest UI based on setting
        if (questUI != null)
        {
            questUI.SetActive(showQuestUIOnStart);
            isQuestUIVisible = showQuestUIOnStart;
        }

        // Setup player if not assigned
        if (player == null)
        {
            player = GameObject.FindWithTag("Player");
        }

        EnsureQuestUIVisibleForInitialHomeScene();
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureQuestUIVisibleForInitialHomeScene(scene.name);
    }

    void EnsureQuestUIVisibleForInitialHomeScene()
    {
        EnsureQuestUIVisibleForInitialHomeScene(SceneManager.GetActiveScene().name);
    }

    void EnsureQuestUIVisibleForInitialHomeScene(string sceneName)
    {
        if (!forceQuestUIOnFirstHomeSceneLoad || hasForcedQuestUIOnFirstHomeSceneLoad)
            return;

        if (!string.Equals(sceneName, homeSceneName, System.StringComparison.Ordinal))
            return;

        if (questUI == null)
            return;

        questUI.SetActive(true);
        isQuestUIVisible = true;
        hasForcedQuestUIOnFirstHomeSceneLoad = true;
    }

    void Update()
    {
        HandleInput();
    }

    void HandleInput()
    {
        // Toggle quest UI
        if (UnifiedTabMenuController.Instance == null && Input.GetKeyDown(toggleUIKey))
        {
            ToggleQuestUI();
        }

        // Test mushroom collection (for prototyping)
        if (Input.GetKeyDown(testCollectKey))
        {
            TestCollectMushroom();
        }

        // Escape to unlock cursor
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleCursor();
        }
    }

    void ToggleQuestUI()
    {
        if (questUI != null)
        {
            isQuestUIVisible = !isQuestUIVisible;
            questUI.SetActive(isQuestUIVisible);
        }
    }

    void ToggleCursor()
    {
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    void TestCollectMushroom()
    {
        // For testing - randomly collect a mushroom type
        if (MailSystem.Instance != null && MailSystem.Instance.CurrentQuest != null)
        {
            var quest = MailSystem.Instance.CurrentQuest;
            if (quest.requestedMushrooms.Count > 0)
            {
                var randomMushroom = quest.requestedMushrooms[Random.Range(0, quest.requestedMushrooms.Count)];
                MailSystem.Instance.UpdateMushroomProgress(randomMushroom.mushroomType, 1);

                // Refresh UI
                var ui = FindObjectOfType<MushroomListUI>();
                if (ui != null) ui.Refresh();

                Debug.Log($"Collected 1 {randomMushroom.mushroomType}");
            }
        }
    }
}

