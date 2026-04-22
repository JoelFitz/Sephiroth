using UnityEngine;
using UnityEngine.SceneManagement;
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioListener))]
[RequireComponent(typeof(AudioSource))]
public class PlayerAudioController : MonoBehaviour
{
    public static PlayerAudioController Instance { get; private set; }

    [Header("Footsteps")]
    public AudioClip footstepClip;
    public AudioClip landingClip;
    public AudioClip[] walkFootstepClips;
    public AudioClip[] sprintFootstepClips;
    public AudioClip[] landingClips;
    public AudioClip[] grassWalkFootstepClips;
    public AudioClip[] grassSprintFootstepClips;
    public AudioClip[] grassLandingClips;
    public float footstepVolume = 0.8f;
    public float landingVolume = 0.9f;
    public float footstepPitchJitter = 0.03f;
    public float landingPitchJitter = 0.02f;
    public float footstepMinSpeed = 0.15f;
    public float walkStepInterval = 0.45f;
    public float sprintStepInterval = 0.3f;
    public float landingMinSpeed = 1.5f;

    [Header("Scene Footstep Sets")]
    public string homeSceneName = "HomeScene";
    public bool useGrassOutsideHome = true;

    [Header("Tongue / Rope")]
    public float tongueVolume = 0.9f;
    public float ropeVolume = 0.85f;
    public float tonguePitchJitter = 0.025f;
    public float ropePitchJitter = 0.02f;

    private AudioListener audioListener;
    private AudioSource audioSource;
    private OverheadController overheadController;
    private PlayerMotor playerMotor;
    private CharacterController characterController;
    private bool wasGrounded;
    private float footstepTimer;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        TryAutoAssignClips();

        CacheComponents();
        ConfigureAudioSource();
        wasGrounded = IsGrounded();
        EnforceSingleListener();
    }

    void Reset()
    {
        TryAutoAssignClips();
    }

    void OnValidate()
    {
        TryAutoAssignClips();
    }

    void Start()
    {
        EnforceSingleListener();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        EnforceSingleListener();
    }

    void CacheComponents()
    {
        audioListener = GetComponent<AudioListener>();
        audioSource = GetComponent<AudioSource>();
        overheadController = GetComponent<OverheadController>();
        playerMotor = GetComponent<PlayerMotor>();
        characterController = GetComponent<CharacterController>();

        if (audioListener == null)
            audioListener = gameObject.AddComponent<AudioListener>();

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    void ConfigureAudioSource()
    {
        if (audioSource == null)
            return;

        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;

        if (audioSource.clip == null)
        {
            AudioClip fallbackClip = GetFirstAvailableFootstepClip();
            if (fallbackClip != null)
                audioSource.clip = fallbackClip;
        }
    }

    void TryAutoAssignClips()
    {
#if UNITY_EDITOR
        if (walkFootstepClips == null || walkFootstepClips.Length == 0)
            walkFootstepClips = LoadAudioClipsFromFolder("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Walk");

        if (sprintFootstepClips == null || sprintFootstepClips.Length == 0)
            sprintFootstepClips = LoadAudioClipsFromFolder("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Run");

        if (landingClips == null || landingClips.Length == 0)
            landingClips = LoadAudioClipsFromFolder("Assets/Footsteps - Essentials/Footsteps_Wood/Footsteps_Wood_Jump");

        if (grassWalkFootstepClips == null || grassWalkFootstepClips.Length == 0)
            grassWalkFootstepClips = LoadAudioClipsFromFolder("Assets/Footsteps - Essentials/Footsteps_Grass/Footsteps_Grass_Walk");

        if (grassSprintFootstepClips == null || grassSprintFootstepClips.Length == 0)
            grassSprintFootstepClips = LoadAudioClipsFromFolder("Assets/Footsteps - Essentials/Footsteps_Grass/Footsteps_Grass_Run");

        if (grassLandingClips == null || grassLandingClips.Length == 0)
            grassLandingClips = LoadAudioClipsFromFolder("Assets/Footsteps - Essentials/Footsteps_Grass/Footsteps_Grass_Jump");

        if (footstepClip == null)
            footstepClip = GetFirstAvailableFootstepClip();

        if (landingClip == null)
            landingClip = GetFirstAvailableLandingClip();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (audioSource != null && audioSource.clip == null)
        {
            AudioClip fallbackClip = GetFirstAvailableFootstepClip();
            if (fallbackClip != null)
                audioSource.clip = fallbackClip;
        }
#endif
    }

    AudioClip[] LoadAudioClipsFromFolder(string folderPath)
    {
#if UNITY_EDITOR
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:AudioClip", new[] { folderPath });
        AudioClip[] clips = new AudioClip[guids.Length];

        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[i]);
            clips[i] = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
        }

        return clips;
#else
        return System.Array.Empty<AudioClip>();
#endif
    }

    AudioClip GetFirstAvailableFootstepClip()
    {
        if (footstepClip != null)
            return footstepClip;

        if (walkFootstepClips != null && walkFootstepClips.Length > 0 && walkFootstepClips[0] != null)
            return walkFootstepClips[0];

        if (sprintFootstepClips != null && sprintFootstepClips.Length > 0 && sprintFootstepClips[0] != null)
            return sprintFootstepClips[0];

        if (grassWalkFootstepClips != null && grassWalkFootstepClips.Length > 0 && grassWalkFootstepClips[0] != null)
            return grassWalkFootstepClips[0];

        if (grassSprintFootstepClips != null && grassSprintFootstepClips.Length > 0 && grassSprintFootstepClips[0] != null)
            return grassSprintFootstepClips[0];

        return null;
    }

    AudioClip GetFirstAvailableLandingClip()
    {
        if (landingClip != null)
            return landingClip;

        if (landingClips != null && landingClips.Length > 0 && landingClips[0] != null)
            return landingClips[0];

        if (grassLandingClips != null && grassLandingClips.Length > 0 && grassLandingClips[0] != null)
            return grassLandingClips[0];

        return null;
    }

    bool UseGrassFootstepsForCurrentScene()
    {
        if (!useGrassOutsideHome)
            return false;

        Scene activeScene = SceneManager.GetActiveScene();
        if (!activeScene.IsValid())
            return false;

        return !string.Equals(activeScene.name, homeSceneName, System.StringComparison.Ordinal);
    }

    AudioClip PickRandomClip(AudioClip[] pool)
    {
        if (pool == null || pool.Length == 0)
            return null;

        int attempts = pool.Length;
        while (attempts-- > 0)
        {
            AudioClip clip = pool[Random.Range(0, pool.Length)];
            if (clip != null)
                return clip;
        }

        return null;
    }

    void EnforceSingleListener()
    {
        if (audioListener == null)
            audioListener = GetComponent<AudioListener>();

        if (audioListener != null)
            audioListener.enabled = true;

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            AudioListener listener = listeners[i];
            if (listener == null)
                continue;

            if (listener == audioListener)
                continue;

            listener.enabled = false;
        }
    }

    bool IsMovementEnabled()
    {
        if (overheadController != null && !overheadController.IsMovementEnabled())
            return false;

        if (playerMotor != null && !playerMotor.IsMovementEnabled())
            return false;

        return true;
    }

    bool IsGrounded()
    {
        if (playerMotor != null)
            return playerMotor.IsGrounded();

        if (characterController != null)
            return characterController.isGrounded;

        return false;
    }

    float GetHorizontalSpeed()
    {
        if (playerMotor != null)
        {
            Rigidbody rb = playerMotor.GetRigidbody();
            if (rb != null)
            {
                Vector3 velocity = rb.linearVelocity;
                return new Vector3(velocity.x, 0f, velocity.z).magnitude;
            }
        }

        if (characterController != null)
        {
            Vector3 velocity = characterController.velocity;
            return new Vector3(velocity.x, 0f, velocity.z).magnitude;
        }

        return 0f;
    }

    AudioClip PickFootstepClip(bool sprinting)
    {
        AudioClip clip = null;

        if (UseGrassFootstepsForCurrentScene())
        {
            clip = sprinting ? PickRandomClip(grassSprintFootstepClips) : PickRandomClip(grassWalkFootstepClips);
            if (clip != null)
                return clip;
        }

        AudioClip[] pool = sprinting && sprintFootstepClips != null && sprintFootstepClips.Length > 0
            ? sprintFootstepClips
            : walkFootstepClips;

        clip = PickRandomClip(pool);
        if (clip != null)
            return clip;

        return GetFirstAvailableFootstepClip();
    }

    AudioClip PickLandingClip()
    {
        if (UseGrassFootstepsForCurrentScene())
        {
            AudioClip grassClip = PickRandomClip(grassLandingClips);
            if (grassClip != null)
                return grassClip;
        }

        AudioClip clip = PickRandomClip(landingClips);
        if (clip != null)
            return clip;

        return GetFirstAvailableLandingClip();
    }

    public void PlayCue(AudioClip clip, float volumeScale = 1f, float pitchJitter = 0f)
    {
        if (clip == null || audioSource == null)
            return;

        float originalPitch = audioSource.pitch;
        if (pitchJitter > 0f)
            audioSource.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);

        audioSource.PlayOneShot(clip, volumeScale);
        audioSource.pitch = originalPitch;
    }

    public void PlayFootstep()
    {
        PlayFootstep(false);
    }

    public void PlayLanding()
    {
        PlayCue(PickLandingClip(), landingVolume, landingPitchJitter);
    }

    public void PlayFootstep(bool sprinting)
    {
        PlayCue(PickFootstepClip(sprinting), footstepVolume, footstepPitchJitter);
    }

    public void NotifyMovement(float moveMagnitude, bool sprinting, bool grounded, bool movementEnabled, float deltaTime)
    {
        if (!movementEnabled || moveMagnitude < footstepMinSpeed)
        {
            footstepTimer = 0f;
            wasGrounded = grounded;
            return;
        }

        footstepTimer += deltaTime;
        float stepInterval = sprinting ? Mathf.Max(walkStepInterval * 0.5f, 0.01f) : walkStepInterval;

        if (footstepTimer >= stepInterval)
        {
            PlayFootstep(sprinting);
            footstepTimer -= stepInterval;
        }

        wasGrounded = grounded;
    }

    public void PlayTongueShoot(AudioClip clip)
    {
        PlayCue(clip, tongueVolume, tonguePitchJitter);
    }

    public void PlayTongueAttach(AudioClip clip)
    {
        PlayCue(clip, tongueVolume, tonguePitchJitter);
    }

    public void PlayTongueRelease(AudioClip clip)
    {
        PlayCue(clip, tongueVolume, tonguePitchJitter);
    }

    public void PlayRopeAttach(AudioClip clip)
    {
        PlayCue(clip, ropeVolume, ropePitchJitter);
    }

    public void PlayRopeRelease(AudioClip clip)
    {
        PlayCue(clip, ropeVolume, ropePitchJitter);
    }
}