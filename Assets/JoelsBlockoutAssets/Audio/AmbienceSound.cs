using UnityEngine;

// ============== INSTRUCTION ==============
// Create empty game object and add "Box Collider" component
// Set the "Box Collider" to "Is Trigger"
// Adjust its size to fit the ambience area
// Create another empty game object and add this script
// Select "Area" as well as "Player" in the inspector
// Add sound to the object

[RequireComponent(typeof(AudioSource))]
public class AmbienceSound : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Trigger collider that defines the ambience zone.")]
    [SerializeField] private Collider area;

    [Tooltip("Player transform (usually the same object that has the AudioListener).")]
    [SerializeField] private Transform player;

    [Tooltip("AudioSource used for ambience playback. If empty, this object's AudioSource is used.")]
    [SerializeField] private AudioSource ambienceSource;

    [Header("Auto Assign")]
    [Tooltip("If enabled and Player is empty, tries to find an object with this tag at runtime.")]
    [SerializeField] private bool autoFindPlayerByTag = true;

    [SerializeField] private string playerTag = "Player";

    [Header("Playback")]
    [Tooltip("Target volume while player is inside the zone.")]
    [Range(0f, 1f)]
    [SerializeField] private float insideVolume = 1f;

    [Tooltip("Seconds to fade in when entering the zone.")]
    [Min(0f)]
    [SerializeField] private float fadeInTime = 0.4f;

    [Tooltip("Seconds to fade out when leaving the zone.")]
    [Min(0f)]
    [SerializeField] private float fadeOutTime = 0.7f;

    [Tooltip("If true, stops playback when fully faded out.")]
    [SerializeField] private bool stopWhenOutside = true;

    [Header("Spatial")]
    [Tooltip("Move this sound emitter to the closest point on the zone to the player every frame.")]
    [SerializeField] private bool followClosestPoint = true;

    [Tooltip("Distance tolerance used for inside/outside detection.")]
    [Min(0.00001f)]
    [SerializeField] private float insideEpsilon = 0.02f;

    private bool isInside;

    private void Reset()
    {
        ambienceSource = GetComponent<AudioSource>();
        if (area == null)
        {
            area = GetComponent<Collider>();
        }
    }

    private void Awake()
    {
        if (ambienceSource == null)
        {
            ambienceSource = GetComponent<AudioSource>();
        }

        if (autoFindPlayerByTag && player == null)
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag(playerTag);
            if (taggedPlayer != null)
            {
                player = taggedPlayer.transform;
            }
        }

        if (ambienceSource != null)
        {
            ambienceSource.playOnAwake = false;
            ambienceSource.loop = true;
        }
    }

    private void Update()
    {
        if (!HasValidSetup())
        {
            return;
        }

        Vector3 closestPoint = area.ClosestPoint(player.position);

        if (followClosestPoint)
        {
            transform.position = closestPoint;
        }

        float distanceToZone = Vector3.Distance(player.position, closestPoint);
        isInside = distanceToZone <= insideEpsilon;

        float targetVolume = isInside ? insideVolume : 0f;
        float fadeTime = isInside ? fadeInTime : fadeOutTime;
        ApplyVolume(targetVolume, fadeTime);
    }

    private bool HasValidSetup()
    {
        if (area == null || player == null || ambienceSource == null)
        {
            return false;
        }

        return true;
    }

    private void ApplyVolume(float targetVolume, float fadeTime)
    {
        if (targetVolume > 0f && !ambienceSource.isPlaying)
        {
            ambienceSource.Play();
        }

        if (fadeTime <= 0f)
        {
            ambienceSource.volume = targetVolume;
        }
        else
        {
            ambienceSource.volume = Mathf.MoveTowards(
                ambienceSource.volume,
                targetVolume,
                Time.deltaTime / fadeTime
            );
        }

        if (stopWhenOutside && targetVolume <= 0f && ambienceSource.isPlaying && ambienceSource.volume <= 0.001f)
        {
            ambienceSource.Stop();
        }
    }

    private void OnValidate()
    {
        if (area != null && !area.isTrigger)
        {
            Debug.LogWarning($"{name}: Ambience area collider should be set to Is Trigger.", this);
        }
    }
}
