using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class FrogHudOverlayController : MonoBehaviour
{
    [Header("Shortcut Hints")]
    [SerializeField] private Image researchIcon;
    [SerializeField] private TMP_Text researchKeyText;
    [SerializeField] private KeyCode researchKey = KeyCode.Alpha2;

    [SerializeField] private Image inventoryIcon;
    [SerializeField] private TMP_Text inventoryKeyText;
    [SerializeField] private KeyCode inventoryKey = KeyCode.Alpha3;

    [Header("Quest Direction Bar")]
    [SerializeField] private Transform playerTransform;
    [SerializeField] private Transform referenceCamera;
    [SerializeField] private Camera referenceCameraComponent;
    [SerializeField] private QuestTargetRegistry targetRegistry;
    [SerializeField] private RectTransform directionBarRect;
    [SerializeField] private RectTransform directionMarker;
    [SerializeField] private Image directionMarkerImage;
    [SerializeField] private float markerPadding = 10f;
    [SerializeField] private float fallbackMarkerTravel = 120f;
    [SerializeField] private bool forceMarkerCenteredAnchors = true;
    [SerializeField] private float markerSmoothTime = 0.08f;
    [SerializeField] private float edgeFadeStart = 0.85f;
    [SerializeField] private float edgeMinAlpha = 0.55f;
    [SerializeField] private TMP_Text targetDistanceText;
    [SerializeField] private bool compassDebugLogs;

    [Header("Mini To-Do")]
    [SerializeField] private GameObject todoCollapsedRoot;
    [SerializeField] private TMP_Text todoTitleText;
    [SerializeField] private TMP_Text todoItemsText;
    [SerializeField] private TMP_Text expandKeyText;
    [SerializeField] private KeyCode expandQuestKey = KeyCode.Tab;

    private Transform activeQuestTarget;
    private MushroomQuest activeQuest;
    private bool markerRectPrepared;
    private float nextCompassDebugTime;
    private float markerCurrentX;
    private float markerVelocityX;
    private Color markerBaseColor = Color.white;

    private void OnEnable()
    {
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.OnNewQuestReceived += HandleQuestUpdated;
            MailSystem.Instance.OnQuestCompleted += HandleQuestUpdated;
            MailSystem.Instance.OnQuestReadyToHandIn += HandleQuestUpdated;
            HandleQuestUpdated(MailSystem.Instance.CurrentQuest);
        }

        RefreshShortcutLabels();
        RefreshExpandLabel();
        EnsureMiniTodoVisible();
    }

    private void OnDisable()
    {
        if (MailSystem.Instance != null)
        {
            MailSystem.Instance.OnNewQuestReceived -= HandleQuestUpdated;
            MailSystem.Instance.OnQuestCompleted -= HandleQuestUpdated;
            MailSystem.Instance.OnQuestReadyToHandIn -= HandleQuestUpdated;
        }
    }

    private void Update()
    {
        HandleShortcutInput();
    }

    private void LateUpdate()
    {
        UpdateDirectionMarker();
    }

    private void PrepareMarkerRect()
    {
        if (markerRectPrepared || directionMarker == null)
            return;

        if (forceMarkerCenteredAnchors)
        {
            directionMarker.anchorMin = new Vector2(0.5f, 0.5f);
            directionMarker.anchorMax = new Vector2(0.5f, 0.5f);
            directionMarker.pivot = new Vector2(0.5f, 0.5f);
        }

        if (directionMarkerImage == null)
            directionMarkerImage = directionMarker.GetComponent<Image>();

        if (directionMarkerImage != null)
            markerBaseColor = directionMarkerImage.color;

        markerCurrentX = directionMarker.anchoredPosition.x;

        LayoutElement layoutElement = directionMarker.GetComponent<LayoutElement>();
        if (layoutElement != null)
            layoutElement.ignoreLayout = true;

        markerRectPrepared = true;
    }

    private void HandleShortcutInput()
    {
        if (Input.GetKeyDown(researchKey))
            OpenCategory(UnifiedTabMenuController.TabCategory.Research);

        if (Input.GetKeyDown(inventoryKey))
            OpenCategory(UnifiedTabMenuController.TabCategory.Inventory);

        if (Input.GetKeyDown(expandQuestKey))
            OpenCategory(UnifiedTabMenuController.TabCategory.Quest);
    }

    private void OpenCategory(UnifiedTabMenuController.TabCategory category)
    {
        if (UnifiedTabMenuController.Instance != null)
        {
            UnifiedTabMenuController.Instance.OpenCategory(category);
            return;
        }

        // Fallback behavior when the unified menu is not present.
        if (category == UnifiedTabMenuController.TabCategory.Quest && MailSystem.Instance != null)
            MailSystem.Instance.OpenMailUI();
        else if (category == UnifiedTabMenuController.TabCategory.Inventory && InventorySystem.Instance != null)
            InventorySystem.Instance.OpenInventory();
        else if (category == UnifiedTabMenuController.TabCategory.Research && MushroomResearchBook.Instance != null)
            MushroomResearchBook.Instance.OpenBook();
    }

    private void RefreshShortcutLabels()
    {
        if (researchKeyText != null)
            researchKeyText.text = researchKey.ToString();

        if (inventoryKeyText != null)
            inventoryKeyText.text = inventoryKey.ToString();

        if (researchIcon != null)
            researchIcon.enabled = researchIcon.sprite != null;

        if (inventoryIcon != null)
            inventoryIcon.enabled = inventoryIcon.sprite != null;
    }

    private void RefreshExpandLabel()
    {
        if (expandKeyText != null)
            expandKeyText.text = "[" + expandQuestKey + "]";
    }

    private void HandleQuestUpdated(MushroomQuest quest)
    {
        activeQuest = quest;

        if (targetRegistry != null)
            activeQuestTarget = targetRegistry.GetTargetForQuest(quest);

        RefreshTodoText();
    }

    private void RefreshTodoText()
    {
        if (todoTitleText != null)
            todoTitleText.text = activeQuest != null ? activeQuest.questTitle : "No Active Quest";

        if (todoItemsText == null)
            return;

        if (activeQuest == null || activeQuest.requestedMushrooms == null || activeQuest.requestedMushrooms.Count == 0)
        {
            todoItemsText.text = "- Check the board for new work";
            return;
        }

        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < activeQuest.requestedMushrooms.Count; i++)
        {
            MushroomRequest req = activeQuest.requestedMushrooms[i];
            sb.Append("- ");
            sb.Append(req.mushroomType);
            sb.Append(": ");
            sb.Append(req.collectedQuantity);
            sb.Append('/');
            sb.Append(req.quantity);

            if (i < activeQuest.requestedMushrooms.Count - 1)
                sb.AppendLine();
        }

        todoItemsText.text = sb.ToString();
    }

    private void EnsureMiniTodoVisible()
    {
        if (todoCollapsedRoot != null)
            todoCollapsedRoot.SetActive(true);
    }

    private void UpdateDirectionMarker()
    {
        PrepareMarkerRect();

        Transform target = ResolveQuestTarget();

        if (playerTransform == null)
        {
            GameObject playerObject = GameObject.FindWithTag("Player");
            if (playerObject != null)
                playerTransform = playerObject.transform;
        }

        if (target == null || directionBarRect == null || directionMarker == null)
        {
            UpdateDistanceText(-1f);
            LogCompassDebug("No target or UI references.");
            return;
        }

        Camera cameraToUse = referenceCameraComponent;

        if (cameraToUse == null && referenceCamera != null)
            cameraToUse = referenceCamera.GetComponent<Camera>();

        if (cameraToUse == null)
            cameraToUse = Camera.main;

        if (cameraToUse == null)
            return;

        if (referenceCamera == null)
            referenceCamera = cameraToUse.transform;

        Transform origin = playerTransform != null ? playerTransform : cameraToUse.transform;
        Vector3 toTarget = target.position - origin.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.0001f)
        {
            UpdateDistanceText(0f);
            LogCompassDebug("Target overlap on XZ plane.");
            return;
        }

        Vector3 cameraForward = cameraToUse.transform.forward;
        cameraForward.y = 0f;
        if (cameraForward.sqrMagnitude < 0.0001f)
            cameraForward = Vector3.forward;
        cameraForward.Normalize();

        Vector3 cameraRight = cameraToUse.transform.right;
        cameraRight.y = 0f;
        if (cameraRight.sqrMagnitude < 0.0001f)
            cameraRight = Vector3.right;
        cameraRight.Normalize();

        Vector3 toTargetNorm = toTarget.normalized;
        float rightDot = Vector3.Dot(cameraRight, toTargetNorm);
        float forwardDot = Vector3.Dot(cameraForward, toTargetNorm);

        // Full bearing around the camera on the XZ plane.
        float bearing = Mathf.Atan2(rightDot, forwardDot);
        float normalized = Mathf.Clamp(bearing / Mathf.PI, -1f, 1f);

        float halfWidth = directionBarRect.rect.width * 0.5f;
        if (halfWidth <= 1f)
            halfWidth = Mathf.Abs(directionBarRect.sizeDelta.x) * 0.5f;

        float travel = Mathf.Max(halfWidth - markerPadding, fallbackMarkerTravel);
        float targetX = normalized * travel;
        markerCurrentX = Mathf.SmoothDamp(markerCurrentX, targetX, ref markerVelocityX, markerSmoothTime);

        Vector2 anchored = directionMarker.anchoredPosition;
        anchored.x = markerCurrentX;
        directionMarker.anchoredPosition = anchored;

        // Keep localPosition in sync for UI setups where layout systems override anchoredPosition.
        Vector3 local = directionMarker.localPosition;
        local.x = markerCurrentX;
        directionMarker.localPosition = local;

        UpdateMarkerFade(normalized);
        UpdateDistanceText(toTarget.magnitude);

        LogCompassDebug("OK", target.name, normalized, targetX, rightDot, forwardDot, bearing);
    }

    private void UpdateMarkerFade(float normalized)
    {
        if (directionMarkerImage == null)
            return;

        float edgeAmount = Mathf.InverseLerp(edgeFadeStart, 1f, Mathf.Abs(normalized));
        float alpha = Mathf.Lerp(markerBaseColor.a, edgeMinAlpha, edgeAmount);
        Color markerColor = markerBaseColor;
        markerColor.a = alpha;
        directionMarkerImage.color = markerColor;
    }

    private void UpdateDistanceText(float distance)
    {
        if (targetDistanceText == null)
            return;

        if (distance < 0f)
        {
            targetDistanceText.text = "";
            return;
        }

        targetDistanceText.text = Mathf.RoundToInt(distance) + "m";
    }

    private Transform ResolveQuestTarget()
    {
        MushroomQuest quest = MailSystem.Instance != null ? MailSystem.Instance.CurrentQuest : null;

        if (targetRegistry != null)
            activeQuestTarget = targetRegistry.GetTargetForQuest(quest);

        return activeQuestTarget;
    }

    private void LogCompassDebug(
        string state,
        string targetName = "",
        float normalized = 0f,
        float targetX = 0f,
        float rightDot = 0f,
        float forwardDot = 0f,
        float bearing = 0f)
    {
        if (!compassDebugLogs)
            return;

        if (Time.unscaledTime < nextCompassDebugTime)
            return;

        nextCompassDebugTime = Time.unscaledTime + 0.5f;
        Debug.Log($"CompassDebug [{state}] target={targetName} normalized={normalized:F3} x={targetX:F1} rightDot={rightDot:F3} forwardDot={forwardDot:F3} bearing={bearing:F3}");
    }
}
