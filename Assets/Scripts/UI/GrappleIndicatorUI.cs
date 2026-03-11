using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Always-visible screen indicator for a SwingGrappleZone.
///
/// Setup (one-time):
///   1. Create a Canvas (Render Mode: Screen Space – Overlay is recommended).
///   2. Add a UI Image child to the Canvas and assign a sprite (e.g. a circle icon).
///   3. Set the Image's pivot and anchor both to (0.5, 0.5) – centred.
///   4. Add this component to the Image GameObject.
///   5. Optionally add a second child Image as the "arrow" (a triangular or chevron
///      sprite, pivot 0.5 / 0.5) and assign it to arrowImage.  It is only visible when
///      the zone is off-screen and rotates to point toward the zone.
///   6. Assign targetZone, or leave it null to auto-find the first SwingGrappleZone.
///
/// Behaviour:
///   • While the grapple anchor is visible on screen the indicator sits directly over
///     the projected world position.
///   • When the anchor is outside the visible area (or behind the camera) the indicator
///     slides to the nearest screen edge / corner, padded by edgePadding pixels, and
///     the optional arrowImage becomes visible and rotates to point toward the zone.
///   • Colour and scale transition smoothly between states.
/// </summary>
[RequireComponent(typeof(RectTransform), typeof(Image))]
public class GrappleIndicatorUI : MonoBehaviour
{
    // ── Inspector ──────────────────────────────────────────────────────────────

    [Header("Target")]
    [Tooltip("Zone to track. If null, the first SwingGrappleZone found in the scene is used.")]
    public SwingGrappleZone targetZone;

    [Tooltip("Optional: the player's SwingGrappleSystem. Used to tint the indicator when " +
             "actively swinging. Auto-found if left null.")]
    public SwingGrappleSystem playerSwingSystem;

    [Header("Layout")]
    [Tooltip("Clearance from each screen edge when the indicator is clamped off-screen (pixels).")]
    public float edgePadding = 48f;

    [Tooltip("Smoothing speed for position lerp. Higher = snappier.")]
    public float smoothSpeed = 12f;

    [Header("Scale")]
    [Tooltip("Scale of the indicator when the zone is on-screen.")]
    public float onScreenScale = 1f;

    [Tooltip("Scale when clamped to the screen edge.")]
    public float offScreenScale = 0.7f;

    [Header("Colours")]
    [Tooltip("Tint when the player is within grapple range and the zone is on-screen.")]
    public Color colorInRange     = new Color(0.3f, 1f, 0.45f);

    [Tooltip("Tint when the zone is on-screen but the player is out of range.")]
    public Color colorOutOfRange  = new Color(1f, 1f, 1f, 0.75f);

    [Tooltip("Tint when the zone is off-screen.")]
    public Color colorOffScreen   = new Color(1f, 0.85f, 0.3f, 0.85f);

    [Tooltip("Tint when the player is actively swinging from this zone.")]
    public Color colorActive      = new Color(0.4f, 0.8f, 1f);

    [Header("Arrow (optional)")]
    [Tooltip("Child Image that is shown only when the zone is off-screen. " +
             "The sprite should point upward in its default orientation.")]
    public Image arrowImage;

    [Tooltip("Distance (in canvas pixels) to push the arrow away from the indicator " +
             "centre when it is shown.")]
    public float arrowOffset = 28f;

    // ── Internal ───────────────────────────────────────────────────────────────

    private RectTransform rectTransform;
    private Image         indicatorImage;
    private Canvas        parentCanvas;
    private RectTransform canvasRect;
    private Camera        mainCamera;
    private Transform     playerTransform;

    // Current display state
    private Vector2 currentScreenPos;
    private float   currentScale;
    private Color   currentColor;
    private bool    isOffScreen;

    // ── Lifecycle ──────────────────────────────────────────────────────────────

    void Awake()
    {
        rectTransform  = GetComponent<RectTransform>();
        indicatorImage = GetComponent<Image>();
    }

    void Start()
    {
        mainCamera = Camera.main;

        parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas != null)
            canvasRect = parentCanvas.GetComponent<RectTransform>();

        // Auto-find zone if unassigned.
        if (targetZone == null)
            targetZone = FindObjectOfType<SwingGrappleZone>();

        // Auto-find player.
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform  = playerObj.transform;
            if (playerSwingSystem == null)
                playerSwingSystem = playerObj.GetComponent<SwingGrappleSystem>()
                                 ?? playerObj.GetComponentInChildren<SwingGrappleSystem>();
        }

        // Seed display state.
        currentScreenPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        currentScale     = onScreenScale;
        currentColor     = colorOutOfRange;

        // Arrow starts hidden.
        SetArrowVisible(false);
    }

    void LateUpdate()
    {
        if (mainCamera == null || targetZone == null || targetZone.anchorPoint == null)
            return;

        ComputeTargetState(out Vector2 targetScreenPos, out float targetScale,
                           out Color targetColor, out bool offScreen, out float arrowAngle);

        // Smooth position.
        currentScreenPos = Vector2.Lerp(currentScreenPos, targetScreenPos,
                                        Time.deltaTime * smoothSpeed);

        // Instant colour + scale so feedback is crisp.
        currentColor = targetColor;
        currentScale = targetScale;
        isOffScreen  = offScreen;

        // Apply to RectTransform.
        ApplyCanvasPosition(currentScreenPos);
        rectTransform.localScale = Vector3.one * currentScale;
        indicatorImage.color = currentColor;

        // Arrow.
        if (arrowImage != null)
        {
            SetArrowVisible(offScreen);
            if (offScreen)
            {
                arrowImage.color = targetColor;
                arrowImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, arrowAngle);

                // Derive the screen-space pointing direction from arrowAngle.
                // A Unity UI sprite rotated by eulerZ degrees (CCW) that starts pointing +Y
                // ends up pointing in the direction (-sin(angle), cos(angle)) in screen space.
                float rad    = arrowAngle * Mathf.Deg2Rad;
                Vector2 outDir = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
                arrowImage.rectTransform.anchoredPosition = outDir * arrowOffset;
            }
            else
            {
                arrowImage.rectTransform.anchoredPosition = Vector2.zero;
            }
        }
    }

    // ── State computation ──────────────────────────────────────────────────────

    void ComputeTargetState(out Vector2 screenPos, out float scale,
                            out Color color, out bool offScreen, out float arrowAngle)
    {
        arrowAngle = 0f;

        // Project world position to screen.
        Vector3 raw = mainCamera.WorldToScreenPoint(targetZone.anchorPoint.position);

        bool behindCamera = raw.z < 0f;

        // When the point is behind the camera, mirror through the screen centre so
        // edge clamping pushes the indicator to the opposite (correct) side.
        if (behindCamera)
        {
            raw.x = Screen.width  - raw.x;
            raw.y = Screen.height - raw.y;
        }

        float cx = Screen.width  * 0.5f;
        float cy = Screen.height * 0.5f;
        float hw = cx - edgePadding;   // usable half-width
        float hh = cy - edgePadding;   // usable half-height

        bool inBounds = !behindCamera
                     && raw.x >= edgePadding && raw.x <= Screen.width  - edgePadding
                     && raw.y >= edgePadding && raw.y <= Screen.height - edgePadding;

        if (inBounds)
        {
            // On-screen: place indicator directly over world position.
            screenPos = new Vector2(raw.x, raw.y);
            offScreen = false;

            bool activeSwing = playerSwingSystem != null
                             && playerSwingSystem.IsSwinging()
                             && playerSwingSystem.CurrentZone() == targetZone;

            bool inRange = playerTransform != null
                        && targetZone.IsPlayerInRange(playerTransform.position);

            color = activeSwing ? colorActive
                  : inRange     ? colorInRange
                  :               colorOutOfRange;
            scale = onScreenScale;
        }
        else
        {
            // Off-screen: clamp to nearest edge.
            float dx = raw.x - cx;
            float dy = raw.y - cy;

            // Scale the vector so the largest component just touches the edge border.
            float sdx = hw > 0 ? Mathf.Abs(dx) / hw : 0f;
            float sdy = hh > 0 ? Mathf.Abs(dy) / hh : 0f;
            float s   = Mathf.Max(sdx, sdy);

            if (s > 0f) { dx /= s; dy /= s; }

            float clampedX = Mathf.Clamp(cx + dx, edgePadding, Screen.width  - edgePadding);
            float clampedY = Mathf.Clamp(cy + dy, edgePadding, Screen.height - edgePadding);

            screenPos = new Vector2(clampedX, clampedY);
            offScreen = true;
            color     = colorOffScreen;
            scale     = offScreenScale;

            // Arrow angle: points from edge indicator toward the off-screen zone.
            // atan2(dy, dx) = angle from +X axis; subtract 90° because sprite points up.
            arrowAngle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg - 90f;
        }
    }

    // ── Canvas positioning ─────────────────────────────────────────────────────

    /// <summary>
    /// Converts a screen-pixel coordinate to the correct canvas coordinate space
    /// and applies it to this RectTransform.  Works with Screen Space Overlay,
    /// Screen Space Camera, and canvases with a CanvasScaler attached.
    /// </summary>
    void ApplyCanvasPosition(Vector2 screenPos)
    {
        if (canvasRect == null)
        {
            // Fallback: direct position assignment (Screen Space Overlay, no scaler).
            rectTransform.position = new Vector3(screenPos.x, screenPos.y, 0f);
            return;
        }

        Camera uiCamera = (parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
                          ? null
                          : parentCanvas.worldCamera;

        Vector2 localPoint;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, uiCamera, out localPoint))
        {
            // localPoint is in the canvas's local coordinate space.
            // Setting localPosition (= position in parent's local space for a direct child)
            // places the indicator at the correct on-screen location regardless of scaling.
            rectTransform.localPosition = new Vector3(localPoint.x, localPoint.y, 0f);
        }
    }

    // ── Arrow helper ───────────────────────────────────────────────────────────

    void SetArrowVisible(bool visible)
    {
        if (arrowImage != null && arrowImage.enabled != visible)
            arrowImage.enabled = visible;
    }
}
