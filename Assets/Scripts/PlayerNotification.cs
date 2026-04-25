using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class PlayerNotification : MonoBehaviour
{
    private static PlayerNotification instance;

    [Header("Persistent References")]
    [SerializeField] private SpriteRenderer notificationSprite;
    [SerializeField] private TextMeshProUGUI notificationHudText;

    [Header("Notification Text")]
    [SerializeField] private string elevatorActionText = "use rope elevator";
    [SerializeField] private string grappleActionText = "grapple";
    [SerializeField] private string captureMushroomActionText = "capture mushroom";
    [SerializeField] private string doorActionText = "enter";
    [SerializeField] private string bedActionText = "sleep";

    [Header("Sprite Animation")]
    [SerializeField] private bool animateNotificationSprite = true;
    [SerializeField] private float bobAmplitude = 0.08f;
    [SerializeField] private float bobSpeed = 2.5f;
    [SerializeField] private float rotateAmplitude = 8f;
    [SerializeField] private float rotateSpeed = 3.25f;

    [Header("Capture Settings")]
    [SerializeField] private float mushroomCaptureRadius = 2f;
    [SerializeField] private KeyCode captureKey = KeyCode.E;

    [Header("Discovery")]
    [SerializeField] private float cacheRefreshInterval = 0.5f;

    private RopeElevator[] ropeElevators = new RopeElevator[0];
    private DoorLevelTransition[] doorTransitions = new DoorLevelTransition[0];
    private PlayerSleep[] bedInteractions = new PlayerSleep[0];
    private MushroomAI[] mushrooms = new MushroomAI[0];

    private TongueGrappleSystem tongueGrappleSystem;
    private TongueActionRouter tongueActionRouter;
    private Transform activePlayerTransform;

    private float nextCacheRefreshTime;
    private bool initialized;
    private bool hasSpriteBasePose;
    private Vector3 spriteBaseLocalPosition;
    private Quaternion spriteBaseLocalRotation;

    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (initialized)
            return;

        initialized = true;
        DontDestroyOnLoad(gameObject);
        RebindPlayerSystems();
        ResolvePersistentReferences();
        RefreshSceneCaches();
        HideNotification();
    }

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RebindPlayerSystems();
        ResolvePersistentReferences();
        RefreshSceneCaches();
        HideNotification();
    }

    void Update()
    {
        if (Time.time >= nextCacheRefreshTime)
        {
            RefreshSceneCaches();
        }

        if (activePlayerTransform == null)
            RebindPlayerSystems();

        ResolvePersistentReferences();

        if (TryGetCaptureMushroomNotification(out KeyCode captureNotificationKey, out string captureAction))
        {
            ShowNotification(captureNotificationKey, captureAction);
            return;
        }

        if (TryGetDoorNotification(out KeyCode doorKey, out string doorAction))
        {
            ShowNotification(doorKey, doorAction);
            return;
        }

        if (TryGetBedNotification(out KeyCode bedKey, out string bedAction))
        {
            ShowNotification(bedKey, bedAction);
            return;
        }

        if (TryGetElevatorNotification(out KeyCode elevatorKey, out string elevatorAction))
        {
            ShowNotification(elevatorKey, elevatorAction);
            return;
        }

        if (TryGetGrappleNotification(out KeyCode grappleKey, out string grappleAction))
        {
            ShowNotification(grappleKey, grappleAction);
            return;
        }

        HideNotification();
    }

    void RebindPlayerSystems()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        OverheadController[] controllers = FindObjectsByType<OverheadController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        OverheadController selectedController = null;
        for (int i = 0; i < controllers.Length; i++)
        {
            OverheadController candidate = controllers[i];
            if (candidate == null)
                continue;

            if (candidate.gameObject.scene == activeScene)
            {
                selectedController = candidate;
                break;
            }
        }

        if (selectedController == null)
            selectedController = Object.FindFirstObjectByType<OverheadController>();

        if (selectedController != null)
        {
            activePlayerTransform = selectedController.transform;
            tongueGrappleSystem = selectedController.GetComponent<TongueGrappleSystem>() ?? selectedController.GetComponentInParent<TongueGrappleSystem>();
            tongueActionRouter = selectedController.GetComponent<TongueActionRouter>() ?? selectedController.GetComponentInParent<TongueActionRouter>();
        }
        else
        {
            activePlayerTransform = null;
            tongueGrappleSystem = null;
            tongueActionRouter = null;
        }
    }

    void ResolvePersistentReferences()
    {
        if (notificationSprite == null)
        {
            Transform spriteTransform = FindChildRecursiveByName(transform, "NotificationSprite");
            if (spriteTransform != null)
            {
                notificationSprite = spriteTransform.GetComponent<SpriteRenderer>();
                hasSpriteBasePose = false;
            }
        }

        if (notificationHudText == null)
        {
            TextMeshProUGUI[] allTexts = FindObjectsByType<TextMeshProUGUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < allTexts.Length; i++)
            {
                TextMeshProUGUI candidate = allTexts[i];
                if (candidate == null)
                    continue;

                string lowerName = candidate.name.ToLowerInvariant();
                if (lowerName.Contains("notification") && lowerName.Contains("text"))
                {
                    notificationHudText = candidate;
                    break;
                }

                if (candidate.text.Contains("[TEST]") && candidate.text.Contains("[ACTION]"))
                {
                    notificationHudText = candidate;
                    break;
                }
            }
        }
    }

    static Transform FindChildRecursiveByName(Transform root, string targetName)
    {
        if (root == null)
            return null;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == targetName)
                return child;

            Transform nested = FindChildRecursiveByName(child, targetName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    void RefreshSceneCaches()
    {
        ropeElevators = FindObjectsByType<RopeElevator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        doorTransitions = FindObjectsByType<DoorLevelTransition>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        bedInteractions = FindObjectsByType<PlayerSleep>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        mushrooms = FindObjectsByType<MushroomAI>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        nextCacheRefreshTime = Time.time + cacheRefreshInterval;
    }

    bool TryGetCaptureMushroomNotification(out KeyCode key, out string actionText)
    {
        key = captureKey;
        actionText = captureMushroomActionText;

        if (mushrooms == null || mushrooms.Length == 0)
            return false;

        for (int i = 0; i < mushrooms.Length; i++)
        {
            MushroomAI mushroom = mushrooms[i];
            if (mushroom == null || !mushroom.IsTongueGrabbed())
                continue;

            Vector3 playerPosition = activePlayerTransform != null ? activePlayerTransform.position : transform.position;
            float distance = Vector3.Distance(playerPosition, mushroom.transform.position);
            if (distance <= mushroomCaptureRadius)
                return true;
        }

        return false;
    }

    bool TryGetDoorNotification(out KeyCode key, out string actionText)
    {
        key = KeyCode.E;
        actionText = doorActionText;

        if (doorTransitions == null)
            return false;

        for (int i = 0; i < doorTransitions.Length; i++)
        {
            DoorLevelTransition door = doorTransitions[i];
            if (door == null || !door.CanPlayerInteractNow())
                continue;

            key = door.interactKey;
            actionText = doorActionText;
            return true;
        }

        return false;
    }

    bool TryGetBedNotification(out KeyCode key, out string actionText)
    {
        key = KeyCode.E;
        actionText = bedActionText;

        if (bedInteractions == null)
            return false;

        for (int i = 0; i < bedInteractions.Length; i++)
        {
            PlayerSleep bed = bedInteractions[i];
            if (bed == null || !bed.CanPlayerSleepNow())
                continue;

            key = bed.interactKey;
            actionText = bedActionText;
            return true;
        }

        return false;
    }

    bool TryGetElevatorNotification(out KeyCode key, out string actionText)
    {
        key = KeyCode.Mouse0;
        actionText = elevatorActionText;

        if (ropeElevators == null)
            return false;

        for (int i = 0; i < ropeElevators.Length; i++)
        {
            RopeElevator elevator = ropeElevators[i];
            if (elevator == null || !elevator.CanStartRide())
                continue;

            key = ResolveTongueActionKey(elevator.useKey);
            actionText = elevatorActionText;
            return true;
        }

        return false;
    }

    bool TryGetGrappleNotification(out KeyCode key, out string actionText)
    {
        key = KeyCode.Mouse0;
        actionText = grappleActionText;

        if (tongueGrappleSystem == null || !tongueGrappleSystem.CanStartUnifiedAction())
            return false;

        key = ResolveTongueActionKey(tongueGrappleSystem.grappleKey);
        actionText = grappleActionText;
        return true;
    }

    KeyCode ResolveTongueActionKey(KeyCode fallbackKey)
    {
        if (tongueActionRouter != null)
            return tongueActionRouter.tongueActionKey;

        return fallbackKey;
    }

    void ShowNotification(KeyCode key, string actionText)
    {
        if (notificationSprite != null)
        {
            notificationSprite.enabled = true;
            ApplySpriteAnimation();
        }

        if (notificationHudText != null)
        {
            notificationHudText.text = $"Press [{FormatKey(key)}] to {actionText}.";
            notificationHudText.enabled = true;
        }
    }

    void HideNotification()
    {
        if (notificationSprite != null)
        {
            notificationSprite.enabled = false;
            RestoreSpritePose();
        }

        if (notificationHudText != null)
        {
            notificationHudText.text = string.Empty;
            notificationHudText.enabled = false;
        }
    }

    static string FormatKey(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.Mouse0:
                return "LMB";
            case KeyCode.Mouse1:
                return "RMB";
            case KeyCode.Mouse2:
                return "MMB";
            default:
                return key.ToString().ToUpperInvariant();
        }
    }

    void CacheSpritePoseIfNeeded()
    {
        if (notificationSprite == null || hasSpriteBasePose)
            return;

        Transform spriteTransform = notificationSprite.transform;
        spriteBaseLocalPosition = spriteTransform.localPosition;
        spriteBaseLocalRotation = spriteTransform.localRotation;
        hasSpriteBasePose = true;
    }

    void ApplySpriteAnimation()
    {
        if (!animateNotificationSprite || notificationSprite == null)
            return;

        CacheSpritePoseIfNeeded();

        if (!hasSpriteBasePose)
            return;

        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        float rotationOffset = Mathf.Sin(Time.time * rotateSpeed) * rotateAmplitude;

        Transform spriteTransform = notificationSprite.transform;
        spriteTransform.localPosition = spriteBaseLocalPosition + Vector3.up * bobOffset;

        Camera activeCamera = Camera.main;
        if (activeCamera != null)
        {
            Vector3 toCamera = activeCamera.transform.position - spriteTransform.position;
            Vector3 horizontalToCamera = Vector3.ProjectOnPlane(toCamera, Vector3.up);

            if (horizontalToCamera.sqrMagnitude > 0.0001f)
            {
                Quaternion faceCameraHorizontal = Quaternion.LookRotation(horizontalToCamera.normalized, Vector3.up);
                spriteTransform.rotation = faceCameraHorizontal * Quaternion.Euler(0f, rotationOffset, 0f);
                return;
            }
        }

        spriteTransform.localRotation = spriteBaseLocalRotation * Quaternion.Euler(0f, rotationOffset, 0f);
    }

    void RestoreSpritePose()
    {
        if (notificationSprite == null)
            return;

        CacheSpritePoseIfNeeded();

        if (!hasSpriteBasePose)
            return;

        Transform spriteTransform = notificationSprite.transform;
        spriteTransform.localPosition = spriteBaseLocalPosition;
        spriteTransform.localRotation = spriteBaseLocalRotation;
    }
}

