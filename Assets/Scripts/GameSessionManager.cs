using UnityEngine;

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance { get; private set; }

    [Header("Session State")]
    [SerializeField] private bool isSessionActive;
    [SerializeField] private int currentDay = 1;

    private static readonly string[] KnownMushroomTypes =
    {
        "Chanterelle", "Shiitake", "Morel", "Oyster", "Porcini", "Enoki"
    };

    public bool IsSessionActive => isSessionActive;
    public int CurrentDay => currentDay;

    public static GameSessionManager EnsureInstance()
    {
        if (Instance != null)
            return Instance;

        GameObject root = new GameObject("GameSessionManager");
        return root.AddComponent<GameSessionManager>();
    }

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

    public void BeginNewGameSession()
    {
        ResetPersistentGameplaySystems();
        ClearSessionKeys();
        MushroomResearchBook.ResetSessionResearchData();

        isSessionActive = true;
        currentDay = 1;

        Debug.Log("GameSessionManager: started new game session.");
    }

    public void SetCurrentDay(int day)
    {
        currentDay = Mathf.Max(1, day);
    }

    public void AdvanceDay()
    {
        currentDay++;
    }

    private void ResetPersistentGameplaySystems()
    {
        // Reset persistent UI systems to clean state instead of destroying
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.ResetForNewGame();
        }

        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.ResetForNewGame();
        }

        if (MushroomResearchBook.Instance != null)
        {
            MushroomResearchBook.Instance.ResetForNewGame();
        }

        // Destroy only the legacy GameController if it exists
        GameController controller = Object.FindFirstObjectByType<GameController>();
        if (controller != null)
        {
            Destroy(controller.gameObject);
        }

        UnifiedTabMenuController uiController = UnifiedTabMenuController.Instance;
        if (uiController != null)
        {
            uiController.ResetMenuState();
        }
    }

    private void ClearSessionKeys()
    {
        for (int i = 0; i < KnownMushroomTypes.Length; i++)
        {
            string type = KnownMushroomTypes[i];
            PlayerPrefs.DeleteKey($"Mushroom_{type}_Discovered");
            PlayerPrefs.DeleteKey($"Mushroom_{type}_Count");
        }

        PlayerPrefs.DeleteKey("SpawnPosX");
        PlayerPrefs.DeleteKey("SpawnPosY");
        PlayerPrefs.DeleteKey("SpawnPosZ");
        PlayerPrefs.DeleteKey("SpawnRotY");
        PlayerPrefs.DeleteKey("LastDoorUsed");
        PlayerPrefs.Save();
    }
}
