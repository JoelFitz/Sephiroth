using UnityEngine;
using System.Collections.Generic;
using System;

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
    public GameObject mailUIPanel; // Add reference to mail UI panel
    public KeyCode toggleMailKey = KeyCode.M; // Use M key for Mail
    private bool isMailOpen = false;

    public static MailSystem Instance { get; private set; }

    public MushroomQuest CurrentQuest => currentQuest;

    public event Action<MushroomQuest> OnNewQuestReceived;
    public event Action<MushroomQuest> OnQuestCompleted;
    public event Action<string> OnMushroomCollected;

    [Header("Daily Quest Settings")]
    public int questsPerDay = 2;
    public int minMushroomsPerQuest = 1;
    public int maxMushroomsPerQuest = 4;

    // Available mushroom types for quest generation
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
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // Hide mail UI by default
        if (mailUIPanel != null)
            mailUIPanel.SetActive(false);

        // Start with a simple test quest but don't show UI
        GenerateTestQuest();
    }

    void Update()
    {
        // Handle mail UI toggle
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

        Debug.Log(isMailOpen ? "📬 Mail opened!" : "📬 Mail closed!");
    }

    void GenerateTestQuest()
    {
        var quest = new MushroomQuest
        {
            questId = "QUEST_001",
            questTitle = "Morning Foraging Order",
            requestedMushrooms = new List<MushroomRequest>
            {
                new MushroomRequest { mushroomType = "Chanterelle", quantity = 3, collectedQuantity = 0 },
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

        // Don't automatically show UI, just refresh if it's open
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

        // Fire event for research book
        OnMushroomCollected?.Invoke(mushroomType);
    }

    void CheckQuestCompletion()
    {
        if (currentQuest == null) return;

        bool allCompleted = currentQuest.requestedMushrooms.TrueForAll(r => r.collectedQuantity >= r.quantity);

        if (allCompleted && !currentQuest.isCompleted)
        {
            currentQuest.isCompleted = true;
            OnQuestCompleted?.Invoke(currentQuest);
            Debug.Log($"Quest completed: {currentQuest.questTitle}");
        }
    }

    /// <summary>
    /// Start a new day - clear old quests and generate new ones
    /// </summary>
    public void StartNewDay()
    {
        currentDay++;
        Debug.Log($"🌅 Day {currentDay} begins!");

        // Clear old quests
        ClearOldQuests();

        // Generate new daily quests
        GenerateDailyQuests();

        // Notify UI to refresh
        RefreshUI();
    }

    /// <summary>
    /// Clear all active quests
    /// </summary>
    private void ClearOldQuests()
    {
        activeQuests.Clear();
        currentQuest = null;
        Debug.Log("🗑️ Cleared old quests");
    }

    /// <summary>
    /// Generate new quests for the day
    /// </summary>
    private void GenerateDailyQuests()
    {
        for (int i = 0; i < questsPerDay; i++)
        {
            var quest = GenerateRandomQuest(i + 1);
            ReceiveNewQuest(quest);
        }

        Debug.Log($"📬 Generated {questsPerDay} new quests for day {currentDay}");
    }

    /// <summary>
    /// Generate a single random quest
    /// </summary>
    private MushroomQuest GenerateRandomQuest(int questNumber)
    {
        var quest = new MushroomQuest
        {
            questId = $"DAY{currentDay}_QUEST_{questNumber:D3}",
            questTitle = GetRandomQuestTitle(),
            requestedMushrooms = new List<MushroomRequest>(),
            deadline = DateTime.Now.AddMinutes(UnityEngine.Random.Range(15, 45)), // 15-45 minute deadlines
            isCompleted = false
        };

        // Generate 1-4 different mushroom requests per quest
        int numDifferentMushrooms = UnityEngine.Random.Range(minMushroomsPerQuest, maxMushroomsPerQuest + 1);
        var usedTypes = new List<string>();

        for (int i = 0; i < numDifferentMushrooms; i++)
        {
            string mushroomType;
            do
            {
                mushroomType = availableMushroomTypes[UnityEngine.Random.Range(0, availableMushroomTypes.Length)];
            }
            while (usedTypes.Contains(mushroomType)); // Avoid duplicates

            usedTypes.Add(mushroomType);

            var request = new MushroomRequest
            {
                mushroomType = mushroomType,
                quantity = UnityEngine.Random.Range(1, 5), // 1-4 of each type
                collectedQuantity = 0
            };

            quest.requestedMushrooms.Add(request);
        }

        return quest;
    }

    /// <summary>
    /// Get a random quest title
    /// </summary>
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

    /// <summary>
    /// Refresh all UI elements
    /// </summary>
    private void RefreshUI()
    {
        // Refresh mushroom list UI
        var mushroomListUI = FindObjectOfType<MushroomListUI>();
        if (mushroomListUI != null)
            mushroomListUI.Refresh();

        // If mail is open, keep it open to show new quests
        if (isMailOpen && mailUIPanel != null)
        {
            // You might want to add a special "new day" notification here
            Debug.Log("📬 New quests available! Check your mail.");
        }
    }

    /// <summary>
    /// Get the current day number
    /// </summary>
    public int GetCurrentDay()
    {
        return currentDay;
    }

    /// <summary>
    /// Get total number of active quests
    /// </summary>
    public int GetActiveQuestCount()
    {
        return activeQuests.Count;
    }

    /// <summary>
    /// Get completed quests count
    /// </summary>
    public int GetCompletedQuestCount()
    {
        return activeQuests.FindAll(q => q.isCompleted).Count;
    }
}

