using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Collections;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MushroomListUI : MonoBehaviour
{
    public GameObject mushroomEntryPrefab;
    public Transform listContainer;
    public TextMeshProUGUI questTitleText;
    public Button handInButton;

    [Header("Hand-In Letter Animation")]
    [SerializeField] private Image letterAnimationImage;
    [SerializeField] private Image panelBackgroundImage;
    [SerializeField] private Sprite[] stampAnimationFrames;
    [SerializeField] private float stampFrameRate = 12f;
    [SerializeField] private Image stampImage;
    [SerializeField] private Sprite[] envelopeAppearsFrames;
    [SerializeField] private float envelopeAppearsFrameRate = 12f;
    [SerializeField] private Sprite[] sendOffFrames;
    [SerializeField] private float sendOffFrameRate = 12f;

#if UNITY_EDITOR
    [Header("Editor Auto Fill")]
    [SerializeField] private string stampAnimationFolder = "Assets/textures/ui/Letter/Stamp Animation PLay";
    [SerializeField] private string envelopeAppearsFolder = "Assets/textures/ui/Letter/Envelope apears";
    [SerializeField] private string sendOffFolder = "Assets/textures/ui/Letter/SendOFF";
#endif

    [Header("Hand-In Messaging")]
    [SerializeField] private TextMeshProUGUI sleepPromptText;
    [SerializeField] private string sleepPromptMessage = "Sleep to get the next quest.";

    private List<GameObject> spawnedEntries = new List<GameObject>();
    private Coroutine handInAnimationRoutine;
    private bool isPlayingHandInAnimation;
    private MushroomQuest pendingQuestUpdate;
    private Color originalPanelBackgroundColor;

    void OnEnable()
    {
        if (letterAnimationImage != null)
            letterAnimationImage.gameObject.SetActive(false);

        if (stampImage != null)
            stampImage.gameObject.SetActive(false);

        if (sleepPromptText != null)
            sleepPromptText.gameObject.SetActive(false);
    }

    void Start()
    {
        if (panelBackgroundImage != null)
            originalPanelBackgroundColor = panelBackgroundImage.color;
        
        Debug.Log("[MushroomListUI] Start called. MailSystem.Instance=" + (MailSystem.Instance != null));
        
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.OnNewQuestReceived += UpdateList;
            MailSystem.Instance.OnQuestCompleted += UpdateList;
            MailSystem.Instance.OnQuestReadyToHandIn += UpdateList;
            MailSystem.Instance.OnQuestHandedIn += HandleQuestHandedIn;
            UpdateList(MailSystem.Instance.CurrentQuest);
            Debug.Log("[MushroomListUI] Subscribed to all MailSystem events.");
        }
        else
        {
            Debug.LogError("[MushroomListUI] MailSystem.Instance is NULL in Start! Cannot subscribe to events.");
        }
    }

    void OnDisable()
    {
        Debug.Log("[MushroomListUI] OnDisable called.");
        
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.OnNewQuestReceived -= UpdateList;
            MailSystem.Instance.OnQuestCompleted -= UpdateList;
            MailSystem.Instance.OnQuestReadyToHandIn -= UpdateList;
            MailSystem.Instance.OnQuestHandedIn -= HandleQuestHandedIn;
        }

        if (handInAnimationRoutine != null)
        {
            StopCoroutine(handInAnimationRoutine);
            handInAnimationRoutine = null;
        }

        isPlayingHandInAnimation = false;
        pendingQuestUpdate = null;
    }

    public void UpdateList(MushroomQuest quest)
    {
        if (isPlayingHandInAnimation)
        {
            pendingQuestUpdate = quest;
            return;
        }

        if (panelBackgroundImage != null)
            panelBackgroundImage.color = originalPanelBackgroundColor;

        // Update quest title
        if (questTitleText != null)
        {
            questTitleText.gameObject.SetActive(true);
            questTitleText.text = quest != null ? quest.questTitle : "";
        }

        if (listContainer != null)
            listContainer.gameObject.SetActive(true);

        if (sleepPromptText != null)
            sleepPromptText.gameObject.SetActive(false);

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

    private void HandleQuestHandedIn(int _)
    {
        Debug.Log("[MushroomListUI] Quest hand-in triggered. Animation fields: letterAnimationImage=" + (letterAnimationImage != null) + ", stampFrames=" + (stampAnimationFrames != null && stampAnimationFrames.Length > 0) + ", envelopeFrames=" + (envelopeAppearsFrames != null && envelopeAppearsFrames.Length > 0) + ", sendOffFrames=" + (sendOffFrames != null && sendOffFrames.Length > 0));
        
        if (handInAnimationRoutine != null)
            StopCoroutine(handInAnimationRoutine);

        handInAnimationRoutine = StartCoroutine(PlayHandInAnimationSequence());
    }

    private IEnumerator PlayHandInAnimationSequence()
    {
        isPlayingHandInAnimation = true;
        pendingQuestUpdate = null;

        if (handInButton != null)
            handInButton.gameObject.SetActive(false);

        if (sleepPromptText != null)
            sleepPromptText.gameObject.SetActive(false);

        if (letterAnimationImage != null)
            letterAnimationImage.gameObject.SetActive(true);

        yield return PlayFrames(stampAnimationFrames, stampFrameRate);
        yield return PlayFrames(envelopeAppearsFrames, envelopeAppearsFrameRate, enableStampDuringPlayback: true);

        if (stampImage != null)
            stampImage.gameObject.SetActive(false);

        if (questTitleText != null)
            questTitleText.gameObject.SetActive(false);
        if (listContainer != null)
            listContainer.gameObject.SetActive(false);

        if (panelBackgroundImage != null)
        {
            Color transparentColor = originalPanelBackgroundColor;
            transparentColor.a = 0;
            panelBackgroundImage.color = transparentColor;
        }

        yield return PlayFrames(sendOffFrames, sendOffFrameRate);

        bool shouldShowSleepPrompt = MailSystem.Instance != null
            && MailSystem.Instance.CurrentQuest != null
            && MailSystem.Instance.CurrentQuest.questId == "QUEST_SLEEP_001";

        if (shouldShowSleepPrompt && sleepPromptText != null)
        {
            sleepPromptText.text = sleepPromptMessage;
            sleepPromptText.gameObject.SetActive(true);
        }
        else
        {
            if (questTitleText != null)
                questTitleText.gameObject.SetActive(true);
            if (listContainer != null)
                listContainer.gameObject.SetActive(true);
        }

        if (letterAnimationImage != null)
            letterAnimationImage.gameObject.SetActive(false);

        isPlayingHandInAnimation = false;

        MushroomQuest refreshQuest = pendingQuestUpdate;
        pendingQuestUpdate = null;
        UpdateList(refreshQuest ?? MailSystem.Instance?.CurrentQuest);

        handInAnimationRoutine = null;
    }

    private IEnumerator PlayFrames(Sprite[] frames, float frameRate)
    {
        if (letterAnimationImage == null)
        {
            Debug.LogWarning("[MushroomListUI] Cannot play animation: letterAnimationImage is null. Assign QuestAnim Image to this field.");
            yield break;
        }
        
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("[MushroomListUI] Cannot play animation: frame array is empty or null.");
            yield break;
        }

        float safeFrameRate = Mathf.Max(1f, frameRate);
        float frameDuration = 1f / safeFrameRate;

        for (int i = 0; i < frames.Length; i++)
        {
            letterAnimationImage.sprite = frames[i];
            yield return new WaitForSeconds(frameDuration);
        }
    }

    private IEnumerator PlayFrames(Sprite[] frames, float frameRate, bool enableStampDuringPlayback)
    {
        if (enableStampDuringPlayback && stampImage != null)
            stampImage.gameObject.SetActive(true);

        yield return PlayFrames(frames, frameRate);

        if (enableStampDuringPlayback && stampImage != null)
            stampImage.gameObject.SetActive(false);
    }

#if UNITY_EDITOR
    [ContextMenu("Auto Fill Letter Animation Frames")]
    private void AutoFillLetterAnimationFrames()
    {
        stampAnimationFrames = LoadSpritesFromFolder(stampAnimationFolder);
        envelopeAppearsFrames = LoadSpritesFromFolder(envelopeAppearsFolder);
        sendOffFrames = LoadSpritesFromFolder(sendOffFolder);

        EditorUtility.SetDirty(this);
        Debug.Log($"MushroomListUI: Auto-filled letter animation frames (Stamp: {stampAnimationFrames.Length}, Envelope: {envelopeAppearsFrames.Length}, SendOFF: {sendOffFrames.Length}).", this);
    }

    private static Sprite[] LoadSpritesFromFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath) || !AssetDatabase.IsValidFolder(folderPath))
        {
            Debug.LogWarning($"MushroomListUI: Invalid folder path '{folderPath}'.");
            return new Sprite[0];
        }

        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });

        return textureGuids
            .Select(AssetDatabase.GUIDToAssetPath)
            .SelectMany(AssetDatabase.LoadAllAssetsAtPath)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name, System.StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
#endif
}