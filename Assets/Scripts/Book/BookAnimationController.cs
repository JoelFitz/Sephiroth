using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class BookAnimationController : MonoBehaviour
{
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

    private BookAnimationState currentState = BookAnimationState.Closed;
    private bool hasOpenedBefore = false;

    void Start()
    {
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
    }

    void LateUpdate()
    {
        // If anything external changed the sprite while the book is open, enforce static open image.
        if (currentState == BookAnimationState.Open && bookOpenSprite != null && bookDisplay != null && bookDisplay.sprite != bookOpenSprite)
            bookDisplay.sprite = bookOpenSprite;
    }

    public IEnumerator OpenBookSequence()
    {
        if (currentState != BookAnimationState.Closed) yield break;

        bookDisplay.gameObject.SetActive(true);

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
        if (currentState != BookAnimationState.Open) yield break;

        currentState = BookAnimationState.ZoomingOut;
        yield return StartCoroutine(PlayFrames(zoomOutFrames, "ZoomOut"));
        currentState = BookAnimationState.Closing;
        yield return StartCoroutine(PlayFrames(closeFrames, "Close"));

        bookDisplay.gameObject.SetActive(false);
        currentState = BookAnimationState.Closed;
    }

    public IEnumerator FlipForwardSequence()
    {
        if (currentState != BookAnimationState.Open) yield break;

        currentState = BookAnimationState.FlippingForward;
        yield return StartCoroutine(PlayFrames(flipForwardFrames, "FlipForward"));

        currentState = BookAnimationState.Open;
        ShowStaticOpen();
    }

    public IEnumerator FlipBackwardSequence()
    {
        if (currentState != BookAnimationState.Open) yield break;

        currentState = BookAnimationState.FlippingBackward;
        yield return StartCoroutine(PlayFrames(flipBackwardFrames, "FlipBackward"));

        currentState = BookAnimationState.Open;
        ShowStaticOpen();
    }

    private IEnumerator PlayFrames(Sprite[] frames, string animName)
    {
        if (frames == null || frames.Length == 0)
        {
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
        if (bookOpenSprite != null)
            bookDisplay.sprite = bookOpenSprite;
        else
            Debug.LogWarning("BookAnimationController: bookOpenSprite not assigned!");
    }

    public BookAnimationState GetCurrentState() => currentState;
    public bool HasOpenedBefore() => hasOpenedBefore;
    public void ResetOpenState() => hasOpenedBefore = false;
}
