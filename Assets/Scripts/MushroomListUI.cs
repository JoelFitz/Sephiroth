using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;

public class MushroomListUI : MonoBehaviour
{
    public GameObject mushroomEntryPrefab;
    public Transform listContainer;
    public TextMeshProUGUI questTitleText;
    public Button handInButton;

    private List<GameObject> spawnedEntries = new List<GameObject>();

    void OnEnable()
    {
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.OnNewQuestReceived += UpdateList;
            MailSystem.Instance.OnQuestCompleted += UpdateList;
            MailSystem.Instance.OnQuestReadyToHandIn += UpdateList;
            UpdateList(MailSystem.Instance.CurrentQuest);
        }
    }

    void OnDisable()
    {
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.OnNewQuestReceived -= UpdateList;
            MailSystem.Instance.OnQuestCompleted -= UpdateList;
            MailSystem.Instance.OnQuestReadyToHandIn -= UpdateList;
        }
    }

    public void UpdateList(MushroomQuest quest)
    {
        // Update quest title
        if (questTitleText != null)
            questTitleText.text = quest != null ? quest.questTitle : "";

        // Show hand-in button only when the quest is fully completed
        if (handInButton != null)
            handInButton.gameObject.SetActive(quest != null && quest.isCompleted);

        // Clear old entries
        foreach (var entry in spawnedEntries)
            Destroy(entry);
        spawnedEntries.Clear();

        if (quest == null) return;

        foreach (var req in quest.requestedMushrooms)
        {
            GameObject entry = Instantiate(mushroomEntryPrefab, listContainer);
            var text = entry.GetComponent<TextMeshProUGUI>();
            if (text != null)
                text.text = $"{req.mushroomType}: {req.collectedQuantity}/{req.quantity}";
            spawnedEntries.Add(entry);
        }
    }

    public void Refresh()
    {
        UpdateList(MailSystem.Instance?.CurrentQuest);
    }
}