using UnityEngine;
using System.Collections.Generic;
using System;
using UnityEngine.UI;

[Serializable]
public class MushroomQuest
{
    public string questId;
    public string questTitle;
    public List<MushroomRequest> requestedMushrooms;
    public DateTime deadline;
    public bool isCompleted;
}

[Serializable]
public class MushroomRequest
{
    public string mushroomType;
    public int quantity;
    public int collectedQuantity;
}

public class MailSystem : MonoBehaviour
{
    [SerializeField] private List<MushroomQuest> activeQuests = new List<MushroomQuest>();
    [SerializeField] private MushroomQuest currentQuest;

    [Header("UI Settings")]
    public GameObject mailUIPanel;
    public KeyCode toggleMailKey = KeyCode.M;
    private bool isMailOpen = false;

    public Button handInButton;
    [SerializeField] private bool builtInInputEnabled = true;

    [Header("Reward Settings")]
    [SerializeField] private int rewardPointsPerQuest = 100;
    [SerializeField] private int totalUpgradePoints = 0;

    public static MailSystem Instance { get; private set; }

    public MushroomQuest CurrentQuest => currentQuest;
    public int TotalUpgradePoints => totalUpgradePoints;

    public event Action<MushroomQuest> OnNewQuestReceived;
    public event Action<MushroomQuest> OnQuestCompleted;
    public event Action<string> OnMushroomCollected;

    /// <summary>
    /// Fired when a quest is ready to be handed in. UI should show the Hand In button.
    /// </summary>
    public event Action<MushroomQuest> OnQuestReadyToHandIn;

    /// <summary>
    /// Fired when a quest is successfully handed in. Passes points awarded.
    /// </summary>
    public event Action<int> OnQuestHandedIn;

    [Header("Daily Quest Settings")]
    public int questsPerDay = 2;
    public int minMushroomsPerQuest = 1;
    public int maxMushroomsPerQuest = 4;

    [SerializeField]
    private string[] availableMushroomTypes = {
        "Chanterelle", "Shiitake", "Morel", "Oyster", "Porcini", "Enoki"
    };

    private int currentDay = 1;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (mailUIPanel != null)
            {
                DontDestroyOnLoad(mailUIPanel);
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void OnEnable()
    {
        // Rebind mailUIPanel reference after scene transitions
        if (mailUIPanel == null)
        {
            // Try to find it in children
            if (transform.childCount > 0)
            {
                mailUIPanel = transform.GetChild(0).gameObject;
            }
        }
    }

    void Start()
    {
        if (mailUIPanel != null)
            mailUIPanel.SetActive(false);

        if (GameSessionManager.Instance != null && GameSessionManager.Instance.IsSessionActive)
        {
            currentDay = GameSessionManager.Instance.CurrentDay;
        }

        GenerateTestQuest();
    }

    void Update()
    {
        if (!builtInInputEnabled)
            return;

        if (Input.GetKeyDown(toggleMailKey))
        {
            ToggleMailUI();
        }
    }

    void ToggleMailUI()
    {
        isMailOpen = !isMailOpen;

        if (mailUIPanel != null)
            mailUIPanel.SetActive(isMailOpen);

        // FIX: Refresh UI content whenever the mail panel is opened
        if (isMailOpen)
        {
            var listUI = FindObjectOfType<MushroomListUI>();
            if (listUI != null)
                listUI.Refresh();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        } else
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

            Debug.Log(isMailOpen ? "📬 Mail opened!" : "📬 Mail closed!");
    }

    public void OpenMailUI()
    {
        if (isMailOpen)
            return;

        ToggleMailUI();
    }

    public void CloseMailUI()
    {
        if (!isMailOpen)
            return;

        ToggleMailUI();
    }

    public bool IsMailOpen()
    {
        return isMailOpen;
    }

    public void SetBuiltInInputEnabled(bool enabled)
    {
        builtInInputEnabled = enabled;
    }

    public void ResetForNewGame()
    {
        CloseMailUI();
        activeQuests.Clear();
        currentQuest = null;
        totalUpgradePoints = 0;
        currentDay = 1;
        isMailOpen = false;
        builtInInputEnabled = true;

        if (mailUIPanel != null)
            mailUIPanel.SetActive(false);

        Debug.Log("MailSystem: Reset for new game.");
    }

    void GenerateTestQuest()
    {
        var quest = new MushroomQuest
        {
            questId = "QUEST_001",
            questTitle = "Morning Foraging Order",
            requestedMushrooms = new List<MushroomRequest>
            {
                new MushroomRequest { mushroomType = "Shiitake", quantity = 2, collectedQuantity = 0 }
            },
            deadline = DateTime.Now.AddMinutes(10),
            isCompleted = false
        };

        ReceiveNewQuest(quest);
    }

    public void ReceiveNewQuest(MushroomQuest quest)
    {
        activeQuests.Add(quest);
        currentQuest = quest;
        OnNewQuestReceived?.Invoke(quest);

        if (isMailOpen)
        {
            FindObjectOfType<MushroomListUI>()?.Refresh();
        }

        Debug.Log($"New quest received: {quest.questTitle}");
    }

    public void UpdateMushroomProgress(string mushroomType, int amount)
    {
        if (currentQuest == null) return;

        var request = currentQuest.requestedMushrooms.Find(r => r.mushroomType == mushroomType);
        if (request != null)
        {
            request.collectedQuantity = Mathf.Min(request.collectedQuantity + amount, request.quantity);
            CheckQuestCompletion();
        }

        OnMushroomCollected?.Invoke(mushroomType);
    }

    void CheckQuestCompletion()
    {
        if (currentQuest == null) return;

        bool allCompleted = currentQuest.requestedMushrooms.TrueForAll(
            r => r.collectedQuantity >= r.quantity);

        if (allCompleted && !currentQuest.isCompleted)
        {
            currentQuest.isCompleted = true;
            OnQuestCompleted?.Invoke(currentQuest);

            // Fire the hand-in event so the UI can show the button
            OnQuestReadyToHandIn?.Invoke(currentQuest);

            // If mail is open, refresh so the hand-in button appears immediately
            if (isMailOpen)
                FindObjectOfType<MushroomListUI>()?.Refresh();

            Debug.Log($"✅ Quest complete and ready to hand in: {currentQuest.questTitle}");
        }
    }

    /// <summary>
    /// Called when the player clicks the Hand In button.
    /// Awards upgrade points and removes the quest.
    /// </summary>
    public void HandInCurrentQuest()
    {
        if (currentQuest == null || !currentQuest.isCompleted)
        {
            Debug.LogWarning("No completed quest to hand in.");
            return;
        }

        // Award points
        totalUpgradePoints += rewardPointsPerQuest;
        OnQuestHandedIn?.Invoke(rewardPointsPerQuest);

        Debug.Log($"🏆 Quest handed in: {currentQuest.questTitle} | +" +
                  $"{rewardPointsPerQuest} pts | Total: {totalUpgradePoints} pts");

        // Remove the handed-in quest
        activeQuests.Remove(currentQuest);
        currentQuest = null;

        // Set the next active quest if one exists
        if (activeQuests.Count > 0)
            currentQuest = activeQuests[0];

        // Refresh UI
        if (isMailOpen)
            FindObjectOfType<MushroomListUI>()?.Refresh();
    }

    /// <summary>
    /// Start a new day — clears old quests and generates new ones.
    /// Call this from your sleep/day-cycle system.
    /// </summary>
    public void StartNewDay()
    {
        currentDay++;

        if (GameSessionManager.Instance != null)
        {
            GameSessionManager.Instance.SetCurrentDay(currentDay);
        }

        Debug.Log($"🌅 Day {currentDay} begins!");

        ClearOldQuests();
        GenerateDailyQuests();
        RefreshUI();
    }

    private void ClearOldQuests()
    {
        activeQuests.Clear();
        currentQuest = null;
        Debug.Log("🗑️ Cleared old quests");
    }

    private void GenerateDailyQuests()
    {
        for (int i = 0; i < questsPerDay; i++)
        {
            var quest = GenerateRandomQuest(i + 1);
            ReceiveNewQuest(quest);
        }

        Debug.Log($"📬 Generated {questsPerDay} new quests for day {currentDay}");
    }

    private MushroomQuest GenerateRandomQuest(int questNumber)
    {
        var quest = new MushroomQuest
        {
            questId = $"DAY{currentDay}_QUEST_{questNumber:D3}",
            questTitle = GetRandomQuestTitle(),
            requestedMushrooms = new List<MushroomRequest>(),
            deadline = DateTime.Now.AddMinutes(UnityEngine.Random.Range(15, 45)),
            isCompleted = false
        };

        int numDifferentMushrooms = UnityEngine.Random.Range(
            minMushroomsPerQuest, maxMushroomsPerQuest + 1);
        var usedTypes = new List<string>();

        for (int i = 0; i < numDifferentMushrooms; i++)
        {
            string mushroomType;
            do
            {
                mushroomType = availableMushroomTypes[
                    UnityEngine.Random.Range(0, availableMushroomTypes.Length)];
            }
            while (usedTypes.Contains(mushroomType));

            usedTypes.Add(mushroomType);

            quest.requestedMushrooms.Add(new MushroomRequest
            {
                mushroomType = mushroomType,
                quantity = UnityEngine.Random.Range(1, 5),
                collectedQuantity = 0
            });
        }

        return quest;
    }

    private string GetRandomQuestTitle()
    {
        string[] titles = {
            "Morning Foraging Order",
            "Special Mushroom Request",
            "Daily Harvest Task",
            "Forest Bounty Collection",
            "Mushroom Delivery Service",
            "Nature's Grocery List",
            "Woodland Treasure Hunt",
            "Fungi Gathering Mission",
            "Fresh Harvest Request",
            "Seasonal Mushroom Order"
        };

        return titles[UnityEngine.Random.Range(0, titles.Length)];
    }

    private void RefreshUI()
    {
        var mushroomListUI = FindObjectOfType<MushroomListUI>();
        if (mushroomListUI != null)
            mushroomListUI.Refresh();

        if (isMailOpen && mailUIPanel != null)
            Debug.Log("📬 New quests available! Check your mail.");
    }

    public int GetCurrentDay() => currentDay;
    public int GetActiveQuestCount() => activeQuests.Count;
    public int GetCompletedQuestCount() => activeQuests.FindAll(q => q.isCompleted).Count;
}