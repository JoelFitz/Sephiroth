using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BookAnimationController : MonoBehaviour
{
    public enum FrameSourceMode
    {
        RuntimeLoadWithFallback,
        InspectorOnly
    }

    public enum BookAnimationState
    {
        Closed,
        Opening,
        ZoomingIn,
        Open,
        FlippingForward,
        FlippingBackward,
        ZoomingOut,
        Closing
    }

    [Header("UI Component")]
    public Image bookDisplay;

    [Header("Animation Frames")]
    [Tooltip("All sliced sprites from BookAnimationOpen sprite sheet, in order")]
    public Sprite[] openFrames;
    [Tooltip("All sliced sprites from BookAnimationZoomIn sprite sheet, in order")]
    public Sprite[] zoomInFrames;
    [Tooltip("All sliced sprites from BookAnimationZoomOut sprite sheet, in order")]
    public Sprite[] zoomOutFrames;
    [Tooltip("All sliced sprites from BookAnimationClose sprite sheet, in order")]
    public Sprite[] closeFrames;
    [Tooltip("All sliced sprites from BookAnimationForward sprite sheet, in order")]
    public Sprite[] flipForwardFrames;
    [Tooltip("All sliced sprites from BookAnimationBack sprite sheet, in order")]
    public Sprite[] flipBackwardFrames;

    [Header("Static Sprites")]
    [Tooltip("BookOpen.png — shown when book is fully open")]
    public Sprite bookOpenSprite;

    [Header("Animation Settings")]
    [Tooltip("Frames per second for all animations")]
    public float frameRate = 12f;

    [Header("Runtime PNG Sequence Loading")]
    [SerializeField] private FrameSourceMode frameSourceMode = FrameSourceMode.RuntimeLoadWithFallback;
    [Tooltip("Resources root folder for frame sequences, e.g. 'animations/book/NewBook' if under a Resources folder.")]
    [SerializeField] private string resourcesRoot = "animations/book/NewBook";
    [Tooltip("When enabled, Unity Editor can load sprites directly from Assets if Resources loading fails.")]
    [SerializeField] private bool allowEditorAssetDatabaseFallback = true;
    [Tooltip("Asset path root used by editor fallback, e.g. 'Assets/animations/book/NewBook'.")]
    [SerializeField] private string editorAssetRoot = "Assets/animations/book/NewBook";

    [Header("Scale Settings")]
    [Tooltip("Scale applied while playing frame-by-frame animations (open/zoom/flip/close)")]
    public Vector3 animationScale = Vector3.one;
    [Tooltip("Scale applied when showing the static BookOpen sprite")]
    public Vector3 staticOpenScale = Vector3.one;

    private BookAnimationState currentState = BookAnimationState.Closed;
    private bool hasOpenedBefore = false;
    private bool isInitialized = false;
    private bool hasAttemptedRuntimeLoad = false;

    void Awake()
    {
        EnsureInitialized();
    }

    void OnEnable()
    {
        // If references were lost (or assets were reimported), allow a fresh load attempt.
        if (!HasCoreFramesAssigned())
            hasAttemptedRuntimeLoad = false;

        EnsureInitialized();
    }

    private void OnValidate()
    {
        // Make sure inspector edits can trigger a fresh load pass.
        if (!HasCoreFramesAssigned())
            hasAttemptedRuntimeLoad = false;
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
            return;

        TryPopulateAnimationFrames();

        if (bookDisplay == null)
        {
            Debug.LogError("BookAnimationController: bookDisplay Image not assigned!");
            return;
        }

        // Disable legacy animation systems so they don't override Image.sprite.
        Animator existingAnimator = bookDisplay.GetComponent<Animator>();
        if (existingAnimator != null)
        {
            existingAnimator.enabled = false;
            Debug.LogWarning("BookAnimationController: Disabled Animator on bookDisplay to prevent sprite override.");
        }

        Animation existingAnimation = bookDisplay.GetComponent<Animation>();
        if (existingAnimation != null)
        {
            existingAnimation.enabled = false;
            Debug.LogWarning("BookAnimationController: Disabled Animation component on bookDisplay to prevent sprite override.");
        }

        bookDisplay.gameObject.SetActive(false);
        isInitialized = true;
    }

    void LateUpdate()
    {
        // If anything external changed the sprite while the book is open, enforce static open image.
        Sprite staticSprite = ResolveStaticOpenSprite();
        if (currentState == BookAnimationState.Open && staticSprite != null && bookDisplay != null && bookDisplay.sprite != staticSprite)
            bookDisplay.sprite = staticSprite;
    }

    public IEnumerator OpenBookSequence()
    {
        EnsureInitialized();

        if (!isInitialized)
            yield break;

        if (currentState != BookAnimationState.Closed) yield break;

        bookDisplay.gameObject.SetActive(true);
        SetBookDisplayScale(animationScale);

        if (!hasOpenedBefore)
        {
            currentState = BookAnimationState.Opening;
            yield return StartCoroutine(PlayFrames(openFrames, "Open"));
            currentState = BookAnimationState.ZoomingIn;
            yield return StartCoroutine(PlayFrames(zoomInFrames, "ZoomIn"));
            hasOpenedBefore = true;
        }
        else
        {
            currentState = BookAnimationState.ZoomingIn;
            yield return StartCoroutine(PlayFrames(zoomInFrames, "ZoomIn"));
        }

        currentState = BookAnimationState.Open;
        ShowStaticOpen();
    }

    public IEnumerator CloseBookSequence()
    {
        EnsureInitialized();

        if (!isInitialized)
            yield break;

        if (currentState != BookAnimationState.Open) yield break;

        SetBookDisplayScale(animationScale);
        currentState = BookAnimationState.ZoomingOut;
        yield return StartCoroutine(PlayFrames(zoomOutFrames, "ZoomOut"));
        currentState = BookAnimationState.Closing;
        yield return StartCoroutine(PlayFrames(closeFrames, "Close"));

        bookDisplay.gameObject.SetActive(false);
        currentState = BookAnimationState.Closed;
    }

    public IEnumerator FlipForwardSequence()
    {
        EnsureInitialized();

        if (!isInitialized)
            yield break;

        if (currentState != BookAnimationState.Open) yield break;

        SetBookDisplayScale(animationScale);
        currentState = BookAnimationState.FlippingForward;
        yield return StartCoroutine(PlayFrames(flipForwardFrames, "FlipForward"));

        currentState = BookAnimationState.Open;
        ShowStaticOpen();
    }

    public IEnumerator FlipBackwardSequence()
    {
        EnsureInitialized();

        if (!isInitialized)
            yield break;

        if (currentState != BookAnimationState.Open) yield break;

        SetBookDisplayScale(animationScale);
        currentState = BookAnimationState.FlippingBackward;
        yield return StartCoroutine(PlayFrames(flipBackwardFrames, "FlipBackward"));

        currentState = BookAnimationState.Open;
        ShowStaticOpen();
    }

    private IEnumerator PlayFrames(Sprite[] frames, string animName)
    {
        if (frames == null || frames.Length == 0)
        {
            if (animName.IndexOf("Zoom", StringComparison.OrdinalIgnoreCase) < 0)
                Debug.LogWarning($"BookAnimationController: No frames assigned for '{animName}'");
            yield break;
        }

        float frameDuration = 1f / frameRate;

        foreach (Sprite frame in frames)
        {
            bookDisplay.sprite = frame;
            yield return new WaitForSeconds(frameDuration);
        }
    }

    private void ShowStaticOpen()
    {
        Sprite staticSprite = ResolveStaticOpenSprite();
        if (staticSprite != null)
        {
            bookDisplay.sprite = staticSprite;
            SetBookDisplayScale(staticOpenScale);
        }
        else
            Debug.LogWarning("BookAnimationController: No static open sprite available.");
    }

    private Sprite ResolveStaticOpenSprite()
    {
        if (bookOpenSprite != null)
            return bookOpenSprite;

        if (openFrames != null && openFrames.Length > 0)
            return openFrames[openFrames.Length - 1];

        return null;
    }

    private void TryPopulateAnimationFrames()
    {
        if (hasAttemptedRuntimeLoad)
            return;

        hasAttemptedRuntimeLoad = true;

        // Migration safety: legacy serialized values can leave mode as InspectorOnly.
        // If no usable frames are assigned, still attempt NewBook runtime loading.
        if (frameSourceMode == FrameSourceMode.InspectorOnly && HasCoreFramesAssigned())
            return;

        openFrames = LoadFramesForSequence("Open", openFrames);
        closeFrames = LoadFramesForSequence("Close", closeFrames);
        flipForwardFrames = LoadFramesForSequence("FlipRight", flipForwardFrames);
        flipBackwardFrames = LoadFramesForSequence("FlipLeft", flipBackwardFrames);
        zoomInFrames = LoadFramesForSequence("ZoomIn", zoomInFrames);
        zoomOutFrames = LoadFramesForSequence("ZoomOut", zoomOutFrames);
    }

    private bool HasCoreFramesAssigned()
    {
        return (openFrames != null && openFrames.Length > 0)
            || (closeFrames != null && closeFrames.Length > 0)
            || (flipForwardFrames != null && flipForwardFrames.Length > 0)
            || (flipBackwardFrames != null && flipBackwardFrames.Length > 0);
    }

    [ContextMenu("Reload NewBook Frames")]
    private void ReloadFramesFromNewBook()
    {
        hasAttemptedRuntimeLoad = false;
        TryPopulateAnimationFrames();
    }

    private Sprite[] LoadFramesForSequence(string sequenceName, Sprite[] fallback)
    {
        Sprite[] loaded = null;

#if UNITY_EDITOR
        // In editor, always prefer the explicit NewBook folder PNG sequences.
        if (allowEditorAssetDatabaseFallback)
            loaded = LoadFramesFromAssetDatabase(sequenceName);
#endif

        if (loaded == null || loaded.Length == 0)
            loaded = LoadFramesFromResources(sequenceName);

        if (loaded != null && loaded.Length > 0)
        {
            Debug.Log($"BookAnimationController: Loaded {loaded.Length} frames for {sequenceName}.");
            return loaded;
        }

        if (!string.IsNullOrWhiteSpace(editorAssetRoot))
            Debug.LogWarning($"BookAnimationController: No PNG frames found for sequence '{sequenceName}' under '{editorAssetRoot}/{sequenceName}'. Using fallback inspector frames if assigned.");

        return fallback;
    }

    private Sprite[] LoadFramesFromResources(string sequenceName)
    {
        if (string.IsNullOrWhiteSpace(resourcesRoot))
            return null;

        string path = $"{resourcesRoot}/{sequenceName}";
        Sprite[] frames = Resources.LoadAll<Sprite>(path);
        if (frames == null || frames.Length == 0)
            return null;

        return frames.OrderBy(sprite => sprite.name, StringComparer.Ordinal).ToArray();
    }

#if UNITY_EDITOR
    private Sprite[] LoadFramesFromAssetDatabase(string sequenceName)
    {
        if (string.IsNullOrWhiteSpace(editorAssetRoot))
            return null;

        string folderPath = $"{editorAssetRoot}/{sequenceName}";
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folderPath });
        if (guids == null || guids.Length == 0)
            return null;

        Sprite[] sprites = guids
            .Select(guid => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GUIDToAssetPath(guid)))
            .Where(sprite => sprite != null)
            .OrderBy(sprite => sprite.name, StringComparer.Ordinal)
            .ToArray();

        return sprites.Length > 0 ? sprites : null;
    }
#endif

    private void SetBookDisplayScale(Vector3 targetScale)
    {
        if (bookDisplay == null)
            return;

        RectTransform rectTransform = bookDisplay.rectTransform;
        if (rectTransform != null)
            rectTransform.localScale = targetScale;
    }

    public BookAnimationState GetCurrentState() => currentState;
    public bool HasOpenedBefore() => hasOpenedBefore;
    public void ResetOpenState() => hasOpenedBefore = false;
}
