using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEditor;

public class MainMenu : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject loadGamePanel;
    public GameObject galleryPanel;
    public GameObject settingsPanel;
    public GameObject audioSettingsPanel;
    public GameObject visualSettingsPanel;
    public GameObject creditsPanel;
    public GameObject quitBoxPanel;

    [Header("Menu Buttons")]
    public Button newGameButton;
    public Button loadGameButton;
    public Button galleryButton;
    public Button settingsButton;
    public Button creditsButton;
    public Button quitButton;

    [Header("Settings Buttons")]
    public Button audioSettingsButton;
    public Button visualSettingsButton;

    [Header("Back Buttons")]
    public Button loadGameBackButton;
    public Button galleryBackButton;
    public Button settingsBackButton;
    public Button audioSettingsBackButton;
    public Button visualSettingsBackButton;
    public Button creditsBackButton;

    [Header("Quit Box Buttons")]
    public Button quitYesButton;
    public Button quitNoButton;

    [Header("Animation Settings")]
    public AnimationCurve animationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public float animationDuration = 0.5f;
    public float buttonBounceScale = 1.1f;
    public float bounceTime = 0.2f;
    public Vector3 buttonStartScale = Vector3.zero;
    public Vector3 buttonTargetScale = Vector3.one;

    [Header("Hover Settings")]
    public float hoverScale = 1.05f;
    public float hoverDuration = 0.15f;

    [Header("QuitBox Animation Settings")]
    public float quitBoxFadeSpeed = 2f;
    public Vector3 quitBoxStartScale = Vector3.zero;
    public Vector3 quitBoxTargetScale = Vector3.one;
    [SerializeField] private AnimationCurve quitBoxAnimationCurve;

    [Header("UI Audio")]
    public AudioSource uiAudioSource;
    public AudioClip buttonClickClip;
    public AudioClip buttonHoverClip;
    [Range(0f, 1f)] public float buttonClickVolume = 1f;
    [Range(0f, 1f)] public float buttonHoverVolume = 0.8f;

    private CanvasGroup mainCanvasGroup;
    private CanvasGroup quitBoxCanvasGroup;
    private Button[] allMainMenuButtons;

    void Start()
    {
        // Initialize the quit box animation curve if not set
        if (quitBoxAnimationCurve == null || quitBoxAnimationCurve.keys.Length == 0)
        {
            // Create a bounce/elastic ease-out curve manually
            quitBoxAnimationCurve = new AnimationCurve();
            quitBoxAnimationCurve.AddKey(0f, 0f);
            quitBoxAnimationCurve.AddKey(0.6f, 1.1f);
            quitBoxAnimationCurve.AddKey(0.8f, 0.95f);
            quitBoxAnimationCurve.AddKey(1f, 1f);

            // Set tangent modes for smooth interpolation
            for (int i = 0; i < quitBoxAnimationCurve.keys.Length; i++)
            {
                //AnimationUtility.SetKeyLeftTangentMode(quitBoxAnimationCurve, i, AnimationUtility.TangentMode.Auto);
                //AnimationUtility.SetKeyRightTangentMode(quitBoxAnimationCurve, i, AnimationUtility.TangentMode.Auto);
            }
        }

        // Get or add CanvasGroup component for fade animations
        mainCanvasGroup = GetComponent<CanvasGroup>();
        if (mainCanvasGroup == null)
            mainCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        // Setup QuitBox CanvasGroup
        if (quitBoxPanel != null)
        {
            quitBoxCanvasGroup = quitBoxPanel.GetComponent<CanvasGroup>();
            if (quitBoxCanvasGroup == null)
                quitBoxCanvasGroup = quitBoxPanel.AddComponent<CanvasGroup>();
        }

        // Setup UI audio source
        if (uiAudioSource == null)
        {
            uiAudioSource = GetComponent<AudioSource>();
            if (uiAudioSource == null)
                uiAudioSource = gameObject.AddComponent<AudioSource>();
        }

        uiAudioSource.playOnAwake = false;
        uiAudioSource.loop = false;
        uiAudioSource.spatialBlend = 0f;

        // Cache all main menu buttons
        allMainMenuButtons = new Button[]
        {
            newGameButton, loadGameButton, galleryButton,
            settingsButton, creditsButton, quitButton
        };

        // Setup button listeners
        SetupButtonListeners();

        // Setup hover events for all buttons
        SetupHoverEvents();

        // Initialize panels
        InitializePanels();

        // Animate buttons entrance
        AnimateButtonsEntrance();
    }

    void SetupButtonListeners()
    {
        // Main menu buttons
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGameClicked);

        if (loadGameButton != null)
            loadGameButton.onClick.AddListener(OnLoadGameClicked);

        if (galleryButton != null)
            galleryButton.onClick.AddListener(OnGalleryClicked);

        if (settingsButton != null)
            settingsButton.onClick.AddListener(OnSettingsClicked);

        if (creditsButton != null)
            creditsButton.onClick.AddListener(OnCreditsClicked);

        if (quitButton != null)
            quitButton.onClick.AddListener(OnQuitClicked);

        // Settings submenu buttons
        if (audioSettingsButton != null)
            audioSettingsButton.onClick.AddListener(OnAudioSettingsClicked);

        if (visualSettingsButton != null)
            visualSettingsButton.onClick.AddListener(OnVisualSettingsClicked);

        // Back buttons
        if (loadGameBackButton != null)
            loadGameBackButton.onClick.AddListener(OnBackToMainMenu);

        if (galleryBackButton != null)
            galleryBackButton.onClick.AddListener(OnBackToMainMenu);

        if (settingsBackButton != null)
            settingsBackButton.onClick.AddListener(OnBackToMainMenu);

        if (audioSettingsBackButton != null)
            audioSettingsBackButton.onClick.AddListener(OnBackToSettingsMenu);

        if (visualSettingsBackButton != null)
            visualSettingsBackButton.onClick.AddListener(OnBackToSettingsMenu);

        if (creditsBackButton != null)
            creditsBackButton.onClick.AddListener(OnBackToMainMenu);

        // QuitBox buttons
        if (quitYesButton != null)
            quitYesButton.onClick.AddListener(OnQuitConfirmed);

        if (quitNoButton != null)
            quitNoButton.onClick.AddListener(OnQuitCancelled);
    }

    void SetupHoverEvents()
    {
        // Add MenuButtonHover component to each main menu button
        foreach (Button button in allMainMenuButtons)
        {
            if (button != null)
            {
                MenuButtonHover hoverComponent = button.gameObject.GetComponent<MenuButtonHover>();
                if (hoverComponent == null)
                {
                    hoverComponent = button.gameObject.AddComponent<MenuButtonHover>();
                }
                hoverComponent.Initialize(this, button);
            }
        }

        // Setup hover for back buttons
        SetupBackButtonHover(loadGameBackButton);
        SetupBackButtonHover(galleryBackButton);
        SetupBackButtonHover(settingsBackButton);
        SetupBackButtonHover(audioSettingsBackButton);
        SetupBackButtonHover(visualSettingsBackButton);
        SetupBackButtonHover(creditsBackButton);

        // Setup hover for settings buttons
        SetupBackButtonHover(audioSettingsButton);
        SetupBackButtonHover(visualSettingsButton);

        // Setup hover for quit box buttons
        SetupBackButtonHover(quitYesButton);
        SetupBackButtonHover(quitNoButton);
    }

    void SetupBackButtonHover(Button button)
    {
        if (button != null)
        {
            MenuButtonHover hoverComponent = button.gameObject.GetComponent<MenuButtonHover>();
            if (hoverComponent == null)
            {
                hoverComponent = button.gameObject.AddComponent<MenuButtonHover>();
            }
            hoverComponent.Initialize(this, button);
        }
    }

    void InitializePanels()
    {
        // Show only the main menu panel at start
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(true);

        if (loadGamePanel != null)
            loadGamePanel.SetActive(false);

        if (galleryPanel != null)
            galleryPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (audioSettingsPanel != null)
            audioSettingsPanel.SetActive(false);

        if (visualSettingsPanel != null)
            visualSettingsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        // Initialize QuitBox as hidden
        if (quitBoxPanel != null)
        {
            quitBoxPanel.SetActive(false);
            if (quitBoxCanvasGroup != null)
                quitBoxCanvasGroup.alpha = 0f;
        }
    }

    #region Button Click Handlers

    void OnNewGameClicked()
    {
        Debug.Log("Starting new game...");
        AnimateButtonClick(newGameButton.transform);

        // Build a fresh runtime session and shared TAB UI before entering gameplay scenes.
        GameSessionManager.EnsureInstance().BeginNewGameSession();
        UnifiedTabMenuController.EnsureInstance();

        // Load the home scene
        AsyncLoader.LoadScene("HomeScene");
    }

    void OnLoadGameClicked()
    {
        Debug.Log("Opening load game panel...");
        AnimateButtonClick(loadGameButton.transform);
        SwitchPanel(loadGamePanel);
    }

    void OnGalleryClicked()
    {
        Debug.Log("Opening gallery panel...");
        AnimateButtonClick(galleryButton.transform);
        SwitchPanel(galleryPanel);
    }

    void OnSettingsClicked()
    {
        Debug.Log("Opening settings panel...");
        AnimateButtonClick(settingsButton.transform);
        SwitchPanel(settingsPanel);
        OpenSettingsRoot();
    }

    void OnAudioSettingsClicked()
    {
        Debug.Log("Opening audio settings...");
        if (audioSettingsButton != null)
            AnimateButtonClick(audioSettingsButton.transform);
        OpenAudioSettings();
    }

    void OnVisualSettingsClicked()
    {
        Debug.Log("Opening visual settings...");
        if (visualSettingsButton != null)
            AnimateButtonClick(visualSettingsButton.transform);
        OpenVisualSettings();
    }

    void OnCreditsClicked()
    {
        Debug.Log("Opening credits panel...");
        AnimateButtonClick(creditsButton.transform);
        SwitchPanel(creditsPanel);
    }

    void OnQuitClicked()
    {
        Debug.Log("Opening quit confirmation...");
        AnimateButtonClick(quitButton.transform);
        ShowQuitBox();
    }

    void OnQuitConfirmed()
    {
        Debug.Log("Quit confirmed - closing game...");
        AnimateButtonClick(quitYesButton.transform);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnQuitCancelled()
    {
        Debug.Log("Quit cancelled - returning to main menu...");
        AnimateButtonClick(quitNoButton.transform);
        HideQuitBox();
    }

    void OnBackToMainMenu()
    {
        Debug.Log("Returning to main menu...");
        SwitchPanel(mainMenuPanel);
    }

    void OnBackToSettingsMenu()
    {
        Debug.Log("Returning to settings menu...");
        OpenSettingsRoot();
    }

    #endregion

    #region QuitBox Management

    void ShowQuitBox()
    {
        if (quitBoxPanel != null)
        {
            quitBoxPanel.SetActive(true);
            StartCoroutine(AnimateQuitBoxShow());
        }
    }

    void HideQuitBox()
    {
        if (quitBoxPanel != null)
        {
            StartCoroutine(AnimateQuitBoxHide());
        }
    }

    System.Collections.IEnumerator AnimateQuitBoxShow()
    {
        // Start with hidden state
        if (quitBoxCanvasGroup != null)
            quitBoxCanvasGroup.alpha = 0f;

        quitBoxPanel.transform.localScale = quitBoxStartScale;

        // Animate fade in and scale up simultaneously
        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            float curveValue = quitBoxAnimationCurve.Evaluate(progress);

            // Animate scale
            quitBoxPanel.transform.localScale = Vector3.Lerp(quitBoxStartScale, quitBoxTargetScale, curveValue);

            // Animate fade
            if (quitBoxCanvasGroup != null)
                quitBoxCanvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);

            yield return null;
        }

        // Ensure final values
        quitBoxPanel.transform.localScale = quitBoxTargetScale;
        if (quitBoxCanvasGroup != null)
            quitBoxCanvasGroup.alpha = 1f;
    }

    System.Collections.IEnumerator AnimateQuitBoxHide()
    {
        // Animate fade out and scale down simultaneously
        float elapsed = 0f;
        Vector3 startScale = quitBoxPanel.transform.localScale;
        float startAlpha = quitBoxCanvasGroup != null ? quitBoxCanvasGroup.alpha : 1f;

        while (elapsed < animationDuration * 0.5f) // Hide faster than show
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (animationDuration * 0.5f);

            // Animate scale
            quitBoxPanel.transform.localScale = Vector3.Lerp(startScale, quitBoxStartScale, progress);

            // Animate fade
            if (quitBoxCanvasGroup != null)
                quitBoxCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, progress);

            yield return null;
        }

        // Ensure final values and hide
        quitBoxPanel.transform.localScale = quitBoxStartScale;
        if (quitBoxCanvasGroup != null)
            quitBoxCanvasGroup.alpha = 0f;

        quitBoxPanel.SetActive(false);
    }

    #endregion

    #region Panel Management

    void OpenSettingsRoot()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        if (audioSettingsPanel != null)
            audioSettingsPanel.SetActive(false);

        if (visualSettingsPanel != null)
            visualSettingsPanel.SetActive(false);
    }

    void OpenAudioSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (audioSettingsPanel != null)
            audioSettingsPanel.SetActive(true);

        if (visualSettingsPanel != null)
            visualSettingsPanel.SetActive(false);
    }

    void OpenVisualSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (audioSettingsPanel != null)
            audioSettingsPanel.SetActive(false);

        if (visualSettingsPanel != null)
            visualSettingsPanel.SetActive(true);
    }

    void SwitchPanel(GameObject targetPanel)
    {
        // Deactivate all main panels (but not QuitBox)
        if (mainMenuPanel != null)
            mainMenuPanel.SetActive(false);

        if (loadGamePanel != null)
            loadGamePanel.SetActive(false);

        if (galleryPanel != null)
            galleryPanel.SetActive(false);

        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        if (audioSettingsPanel != null)
            audioSettingsPanel.SetActive(false);

        if (visualSettingsPanel != null)
            visualSettingsPanel.SetActive(false);

        if (creditsPanel != null)
            creditsPanel.SetActive(false);

        // Activate target panel
        if (targetPanel != null)
            targetPanel.SetActive(true);

        if (targetPanel == settingsPanel)
            OpenSettingsRoot();
    }

    #endregion

    #region Button Animations

    void AnimateButtonsEntrance()
    {
        // Animate all main menu buttons
        if (newGameButton != null)
            StartCoroutine(AnimateButtonScale(newGameButton.transform, 0f));

        if (loadGameButton != null)
            StartCoroutine(AnimateButtonScale(loadGameButton.transform, 0.1f));

        if (galleryButton != null)
            StartCoroutine(AnimateButtonScale(galleryButton.transform, 0.2f));

        if (settingsButton != null)
            StartCoroutine(AnimateButtonScale(settingsButton.transform, 0.3f));

        if (creditsButton != null)
            StartCoroutine(AnimateButtonScale(creditsButton.transform, 0.4f));

        if (quitButton != null)
            StartCoroutine(AnimateButtonScale(quitButton.transform, 0.5f));
    }

    System.Collections.IEnumerator AnimateButtonScale(Transform buttonTransform, float delay)
    {
        // Start with small scale
        buttonTransform.localScale = buttonStartScale;

        // Wait for delay (staggered animation)
        yield return new WaitForSeconds(delay);

        // Animate to target scale
        float elapsed = 0f;
        Vector3 startScale = buttonStartScale;
        Vector3 targetScale = buttonTargetScale;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / animationDuration;
            float curveValue = animationCurve.Evaluate(progress);

            buttonTransform.localScale = Vector3.Lerp(startScale, targetScale, curveValue);

            yield return null;
        }

        buttonTransform.localScale = targetScale;
    }

    void AnimateButtonClick(Transform buttonTransform)
    {
        PlayButtonClickSound();
        StartCoroutine(ButtonClickAnimation(buttonTransform));
    }

    System.Collections.IEnumerator ButtonClickAnimation(Transform buttonTransform)
    {
        Vector3 originalScale = buttonTransform.localScale;
        Vector3 clickScale = originalScale * 0.9f; // Shrink on click

        // Scale down quickly
        float elapsed = 0f;
        while (elapsed < bounceTime / 2)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (bounceTime / 2);
            buttonTransform.localScale = Vector3.Lerp(originalScale, clickScale, progress);
            yield return null;
        }

        // Scale back up
        elapsed = 0f;
        while (elapsed < bounceTime / 2)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / (bounceTime / 2);
            buttonTransform.localScale = Vector3.Lerp(clickScale, originalScale, progress);
            yield return null;
        }

        buttonTransform.localScale = originalScale;
    }

    public void OnButtonHoverEnter(Button hoveredButton)
    {
        if (hoveredButton != null)
        {
            PlayButtonHoverSound();
            StartCoroutine(ButtonHoverEnterAnimation(hoveredButton.transform));
        }
    }

    public void OnButtonHoverExit(Button hoveredButton)
    {
        if (hoveredButton != null)
            StartCoroutine(ButtonHoverExitAnimation(hoveredButton.transform));
    }

    System.Collections.IEnumerator ButtonHoverEnterAnimation(Transform buttonTransform)
    {
        Vector3 originalScale = buttonTransform.localScale;
        Vector3 targetHoverScale = originalScale * hoverScale;

        float elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / hoverDuration;
            buttonTransform.localScale = Vector3.Lerp(originalScale, targetHoverScale, progress);
            yield return null;
        }

        buttonTransform.localScale = targetHoverScale;
    }

    System.Collections.IEnumerator ButtonHoverExitAnimation(Transform buttonTransform)
    {
        Vector3 currentScale = buttonTransform.localScale;
        Vector3 originalScale = buttonTargetScale; // Return to normal scale

        float elapsed = 0f;
        while (elapsed < hoverDuration)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / hoverDuration;
            buttonTransform.localScale = Vector3.Lerp(currentScale, originalScale, progress);
            yield return null;
        }

        buttonTransform.localScale = originalScale;
    }

    #endregion

    #region Audio

    void PlayButtonClickSound()
    {
        if (uiAudioSource == null || buttonClickClip == null)
            return;

        uiAudioSource.PlayOneShot(buttonClickClip, buttonClickVolume);
    }

    void PlayButtonHoverSound()
    {
        if (uiAudioSource == null || buttonHoverClip == null)
            return;

        uiAudioSource.PlayOneShot(buttonHoverClip, buttonHoverVolume);
    }

    #endregion

    #region IPointerEnterHandler and IPointerExitHandler (if MainMenu itself needs hover)

    public void OnPointerEnter(PointerEventData eventData)
    {
        // This would be called if the entire MainMenu GameObject is hovered
        // Not typically needed, but included for completeness
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // This would be called when leaving the entire MainMenu GameObject
        // Not typically needed, but included for completeness
    }

    #endregion
}

// Separate component for handling individual button hover events
public class MenuButtonHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private MainMenu mainMenu;
    private Button button;

    public void Initialize(MainMenu menu, Button btn)
    {
        mainMenu = menu;
        button = btn;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (mainMenu != null && button != null)
        {
            mainMenu.OnButtonHoverEnter(button);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (mainMenu != null && button != null)
        {
            mainMenu.OnButtonHoverExit(button);
        }
    }
}

