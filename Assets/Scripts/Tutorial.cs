using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class Tutorial : MonoBehaviour
{
    [Serializable]
    public class TutorialLine
    {
        [TextArea(3, 8)] public string subtitle;
        public AudioClip voiceClip;
        [Min(0f)] public float minimumDuration = 0f;
        [Min(0.01f)] public float mouthSwapInterval = 0.14f;
    }

    public static Tutorial Instance { get; private set; }

    [Header("Scene Names")]
    [SerializeField] private string homeSceneName = "HomeScene";
    [SerializeField] private string level1SceneName = "Level1";

    [Header("UI")]
    [SerializeField] private GameObject tutorialVisualRoot;
    [SerializeField] private Image professorOpenImage;
    [SerializeField] private Image professorClosedImage;
    [SerializeField] private TextMeshProUGUI subtitleText;

    [Header("Audio")]
    [SerializeField] private AudioSource voiceSource;
    [SerializeField, Min(0f)] private float fallbackSecondsPerCharacter = 0.04f;
    [SerializeField, Min(0f)] private float minimumFallbackDuration = 3f;
    [SerializeField, Min(0f)] private float lineEndHoldSeconds = 0.2f;
    [SerializeField, Min(1f)] private float subtitleCharactersPerSecond = 28f;

    [Header("Tutorial Lines")]
    [SerializeField] private TutorialLine[] lines = new TutorialLine[6];

    [Header("Collection Step")]
    [SerializeField, Min(1)] private int mushroomsRequiredForLineFive = 2;

    private bool[] lineReady;
    private bool[] lineCompleted;
    private Coroutine playbackRoutine;
    private int activeLineIndex = -1;
    private bool bookStateHooked;
    private bool mushroomCollectionHooked;
    private bool lineFourHasStarted;
    private int mushroomsCollectedSinceLineFourStarted;
    private bool isInitialized;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        DontDestroyOnLoad(gameObject);

        if (voiceSource == null)
            voiceSource = GetComponent<AudioSource>() ?? gameObject.AddComponent<AudioSource>();

        voiceSource.playOnAwake = false;
        voiceSource.loop = false;
        voiceSource.spatialBlend = 0f;

        EnsureLineArraySize();
        InitializeRuntimeState();
        SetVisualsVisible(false);

        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshSystemBindings();
        EvaluateSceneTriggers(SceneManager.GetActiveScene().name);
        TryStartNextLine();

        isInitialized = true;
    }

    void OnEnable()
    {
        if (!isInitialized)
            return;

        RefreshSystemBindings();
        EvaluateSceneTriggers(SceneManager.GetActiveScene().name);
        TryStartNextLine();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnhookBookState();
        UnhookMushroomCollection();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ResetForNewGame()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        if (voiceSource != null)
            voiceSource.Stop();

        EnsureLineArraySize();
        InitializeRuntimeState();
        SetVisualsVisible(false);

        RefreshSystemBindings();
        EvaluateSceneTriggers(SceneManager.GetActiveScene().name);
        TryStartNextLine();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshSystemBindings();
        EvaluateSceneTriggers(scene.name);
        TryStartNextLine();
    }

    void EnsureLineArraySize()
    {
        if (lines == null || lines.Length != 6)
            Array.Resize(ref lines, 6);

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i] == null)
                lines[i] = new TutorialLine();
        }
    }

    void InitializeRuntimeState()
    {
        lineReady = new bool[lines.Length];
        lineCompleted = new bool[lines.Length];
        activeLineIndex = -1;
        lineFourHasStarted = false;
        mushroomsCollectedSinceLineFourStarted = 0;
    }

    void RefreshSystemBindings()
    {
        HookBookState();
        HookMushroomCollection();
    }

    void HookBookState()
    {
        MushroomResearchBook book = MushroomResearchBook.Instance;
        if (book == null || bookStateHooked)
            return;

        book.OnBookStateChanged += HandleBookStateChanged;
        bookStateHooked = true;
    }

    void UnhookBookState()
    {
        MushroomResearchBook book = MushroomResearchBook.Instance;
        if (book != null && bookStateHooked)
            book.OnBookStateChanged -= HandleBookStateChanged;

        bookStateHooked = false;
    }

    void HookMushroomCollection()
    {
        MailSystem mailSystem = MailSystem.Instance;
        if (mailSystem == null || mushroomCollectionHooked)
            return;

        mailSystem.OnMushroomCollected += HandleMushroomCollected;
        mushroomCollectionHooked = true;
    }

    void UnhookMushroomCollection()
    {
        MailSystem mailSystem = MailSystem.Instance;
        if (mailSystem != null && mushroomCollectionHooked)
            mailSystem.OnMushroomCollected -= HandleMushroomCollected;

        mushroomCollectionHooked = false;
    }

    void EvaluateSceneTriggers(string sceneName)
    {
        if (!string.Equals(sceneName, homeSceneName, StringComparison.Ordinal))
        {
            if (string.Equals(sceneName, level1SceneName, StringComparison.Ordinal) && !lineCompleted[3])
                lineReady[3] = true;

            return;
        }

        if (GameSessionManager.Instance == null || !GameSessionManager.Instance.IsSessionActive)
            return;

        if (!lineCompleted[0])
            lineReady[0] = true;
    }

    void HandleBookStateChanged(bool isOpen)
    {
        if (isOpen)
        {
            if (!lineCompleted[1])
                lineReady[1] = true;
        }
        else
        {
            if (!lineCompleted[2] && (lineReady[1] || activeLineIndex == 1 || lineCompleted[1]))
                lineReady[2] = true;
        }

        TryStartNextLine();
    }

    void HandleMushroomCollected(string mushroomType)
    {
        if (!lineFourHasStarted || lineCompleted[4])
            return;

        mushroomsCollectedSinceLineFourStarted++;

        if (mushroomsCollectedSinceLineFourStarted >= mushroomsRequiredForLineFive)
        {
            lineReady[4] = true;
            TryStartNextLine();
        }
    }

    void TryStartNextLine()
    {
        if (playbackRoutine != null || lineReady == null || lineCompleted == null)
            return;

        int nextLineIndex = GetNextIncompleteLineIndex();
        if (nextLineIndex < 0 || !lineReady[nextLineIndex])
            return;

        if (nextLineIndex > 0 && !lineCompleted[nextLineIndex - 1])
            return;

        playbackRoutine = StartCoroutine(PlayLine(nextLineIndex));
    }

    int GetNextIncompleteLineIndex()
    {
        for (int i = 0; i < lineCompleted.Length; i++)
        {
            if (!lineCompleted[i])
                return i;
        }

        return -1;
    }

    IEnumerator PlayLine(int lineIndex)
    {
        TutorialLine line = lines[lineIndex];
        activeLineIndex = lineIndex;

        if (lineIndex == 3)
        {
            lineFourHasStarted = true;
            mushroomsCollectedSinceLineFourStarted = 0;
        }

        SetVisualsVisible(true);

        if (subtitleText != null)
        {
            subtitleText.text = line.subtitle;
            subtitleText.maxVisibleCharacters = 0;
            subtitleText.ForceMeshUpdate();
        }

        float duration = ResolveLineDuration(line);
        float mouthSwapInterval = line.mouthSwapInterval > 0f ? line.mouthSwapInterval : 0.14f;
        bool mouthOpen = true;
        float elapsed = 0f;
        float nextSwapAt = mouthSwapInterval;

        SetProfessorMouthOpen(mouthOpen);

        if (voiceSource != null)
        {
            voiceSource.Stop();

            if (line.voiceClip != null)
            {
                voiceSource.clip = line.voiceClip;
                voiceSource.Play();
                duration = Mathf.Max(duration, line.voiceClip.length);
            }
        }

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (subtitleText != null)
            {
                int totalCharacters = subtitleText.textInfo.characterCount;
                if (totalCharacters > 0)
                {
                    int visibleCharacters = Mathf.Clamp(Mathf.FloorToInt(elapsed * subtitleCharactersPerSecond), 0, totalCharacters);
                    subtitleText.maxVisibleCharacters = visibleCharacters;
                }
            }

            if (elapsed >= nextSwapAt)
            {
                mouthOpen = !mouthOpen;
                SetProfessorMouthOpen(mouthOpen);
                nextSwapAt += mouthSwapInterval;
            }

            yield return null;
        }

        if (voiceSource != null && voiceSource.isPlaying)
            voiceSource.Stop();

        if (subtitleText != null)
            subtitleText.maxVisibleCharacters = int.MaxValue;

        lineCompleted[lineIndex] = true;
        activeLineIndex = -1;

        SetProfessorMouthOpen(false);

        yield return new WaitForSeconds(lineEndHoldSeconds);

        SetVisualsVisible(false);
        playbackRoutine = null;

        if (lineIndex == 4)
            lineReady[5] = true;

        TryStartNextLine();
    }

    float ResolveLineDuration(TutorialLine line)
    {
        if (line.voiceClip != null)
            return Mathf.Max(line.minimumDuration, line.voiceClip.length);

        float estimatedDuration = line.subtitle == null ? 0f : line.subtitle.Length * fallbackSecondsPerCharacter;
        return Mathf.Max(minimumFallbackDuration, line.minimumDuration, estimatedDuration);
    }

    void SetVisualsVisible(bool visible)
    {
        if (tutorialVisualRoot != null)
        {
            if (tutorialVisualRoot.activeSelf != visible)
                tutorialVisualRoot.SetActive(visible);

            if (!visible && subtitleText != null)
                subtitleText.maxVisibleCharacters = int.MaxValue;

            return;
        }

        if (professorOpenImage != null)
            professorOpenImage.gameObject.SetActive(visible);

        if (professorClosedImage != null)
            professorClosedImage.gameObject.SetActive(visible);

        if (subtitleText != null)
        {
            subtitleText.gameObject.SetActive(visible);
            if (!visible)
                subtitleText.maxVisibleCharacters = int.MaxValue;
        }
    }

    void SetProfessorMouthOpen(bool open)
    {
        if (professorOpenImage != null)
            professorOpenImage.gameObject.SetActive(open);

        if (professorClosedImage != null)
            professorClosedImage.gameObject.SetActive(!open);
    }
}
