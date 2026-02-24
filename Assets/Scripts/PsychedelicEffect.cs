using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Fullscreen psychedelic effect triggered when eating a Shiitake mushroom.
/// Self-contained — creates its own canvas and layers at runtime, no prefabs needed.
/// Call PsychedelicEffect.Instance.Play() from anywhere.
/// </summary>
public class PsychedelicEffect : MonoBehaviour
{
    [Header("Effect Settings")]
    public float duration = 8f;    // Total trip duration in seconds
    public float fadeInTime = 0.5f;
    public float fadeOutTime = 2f;
    public int layerCount = 6;     // Number of overlapping colour layers
    public float maxAlpha = 0.35f; // Keep low enough to still see the world

    public static PsychedelicEffect Instance { get; private set; }

    // Runtime-created UI objects
    private Canvas _canvas;
    private CanvasGroup _masterGroup;
    private RawImage[] _layers;
    private Coroutine _effectCoroutine;
    private bool _isPlaying = false;

    // Each layer gets its own animation parameters
    private float[] _rotationSpeeds;
    private float[] _pulseFrequencies;
    private float[] _pulseAmplitudes;
    private float[] _hueOffsets;
    private float[] _hueSpeed;
    private Vector2[] _scales;

    // Psychedelic colour palette — vivid, saturated hues
    private static readonly Color[] Palette =
    {
        new Color(1f,    0.1f,  0.8f),   // Hot pink
        new Color(0.1f,  0.9f,  1f),     // Cyan
        new Color(0.5f,  0f,    1f),     // Purple
        new Color(1f,    0.6f,  0f),     // Orange
        new Color(0f,    1f,    0.4f),   // Acid green
        new Color(1f,    1f,    0f),     // Yellow
        new Color(1f,    0.1f,  0.1f),   // Red
        new Color(0f,    0.4f,  1f),     // Blue
    };

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildCanvas();
    }

    // ── Canvas / layer setup ─────────────────────────────────────────────────

    void BuildCanvas()
    {
        // Fullscreen canvas, renders on top of everything
        var canvasGO = new GameObject("PsychedelicCanvas");
        canvasGO.transform.SetParent(transform);
        _canvas = canvasGO.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        // Master group lets us fade the whole effect in/out
        _masterGroup = canvasGO.AddComponent<CanvasGroup>();
        _masterGroup.alpha = 0f;
        _masterGroup.blocksRaycasts = false; // Don't block UI clicks

        // Build layers
        _layers = new RawImage[layerCount];
        _rotationSpeeds = new float[layerCount];
        _pulseFrequencies = new float[layerCount];
        _pulseAmplitudes = new float[layerCount];
        _hueOffsets = new float[layerCount];
        _hueSpeed = new float[layerCount];
        _scales = new Vector2[layerCount];

        for (int i = 0; i < layerCount; i++)
        {
            var layerGO = new GameObject($"Layer_{i}");
            layerGO.transform.SetParent(canvasGO.transform, false);

            var rect = layerGO.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);

            // Oversized so rotation doesn't reveal edges
            float size = Screen.height * 1.8f;
            rect.sizeDelta = new Vector2(size, size);

            _layers[i] = layerGO.AddComponent<RawImage>();
            _layers[i].texture = GenerateLayerTexture(i);
            _layers[i].color = new Color(1, 1, 1, 0);

            // Randomise per-layer animation params
            _rotationSpeeds[i] = Random.Range(8f, 40f) * (Random.value > 0.5f ? 1 : -1);
            _pulseFrequencies[i] = Random.Range(0.3f, 1.5f);
            _pulseAmplitudes[i] = Random.Range(0.05f, 0.2f);
            _hueOffsets[i] = (float)i / layerCount;
            _hueSpeed[i] = Random.Range(0.05f, 0.25f) * (Random.value > 0.5f ? 1 : -1);
            _scales[i] = new Vector2(
                Random.Range(0.8f, 1.3f),
                Random.Range(0.8f, 1.3f));
        }

        canvasGO.SetActive(false); // Hidden until triggered
    }

    /// <summary>
    /// Generates a swirling radial gradient texture unique to each layer.
    /// Done in code so no art assets are needed.
    /// </summary>
    Texture2D GenerateLayerTexture(int layerIndex)
    {
        const int res = 256;
        var tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Repeat;

        Color baseColour = Palette[layerIndex % Palette.Length];
        Color accentColour = Palette[(layerIndex + 3) % Palette.Length];

        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                // UV in [-1, 1]
                float u = (x / (float)res) * 2f - 1f;
                float v = (y / (float)res) * 2f - 1f;

                float dist = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);

                // Spiral pattern: concentric rings twisted by angle
                float rings = Mathf.Sin(dist * 8f - angle * (layerIndex + 2));
                // Radial spokes
                float spokes = Mathf.Sin(angle * (4 + layerIndex) + dist * 3f);

                float combined = (rings + spokes) * 0.5f;      // -1..1
                float t = combined * 0.5f + 0.5f;       // 0..1

                // Radial fade so edges are transparent
                float fade = Mathf.Clamp01(1f - dist * 0.6f);

                Color pixel = Color.Lerp(baseColour, accentColour, t);
                pixel.a = fade * maxAlpha;
                tex.SetPixel(x, y, pixel);
            }
        }

        tex.Apply();
        return tex;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void Play()
    {
        if (_isPlaying) StopEffect();
        _effectCoroutine = StartCoroutine(RunEffect());
    }

    public void StopEffect()
    {
        if (_effectCoroutine != null) StopCoroutine(_effectCoroutine);
        _effectCoroutine = null;
        _isPlaying = false;
        _canvas.gameObject.SetActive(false);
        _masterGroup.alpha = 0f;
    }

    // ── Core coroutine ───────────────────────────────────────────────────────

    IEnumerator RunEffect()
    {
        _isPlaying = true;
        _canvas.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            // Master fade envelope
            float masterAlpha;
            if (elapsed < fadeInTime)
                masterAlpha = elapsed / fadeInTime;
            else if (elapsed > duration - fadeOutTime)
                masterAlpha = (duration - elapsed) / fadeOutTime;
            else
                masterAlpha = 1f;

            // Extra intensity burst in the middle of the trip
            float tripProgress = elapsed / duration;
            float intensityBump = 1f + 0.4f * Mathf.Sin(tripProgress * Mathf.PI); // peaks at midpoint
            _masterGroup.alpha = masterAlpha;

            for (int i = 0; i < layerCount; i++)
            {
                if (_layers[i] == null) continue;

                RectTransform rt = _layers[i].rectTransform;

                // Rotation
                float angle = _rotationSpeeds[i] * elapsed * intensityBump;
                rt.localRotation = Quaternion.Euler(0f, 0f, angle);

                // Pulsing scale
                float pulse = 1f + _pulseAmplitudes[i]
                    * Mathf.Sin(elapsed * _pulseFrequencies[i] * Mathf.PI * 2f)
                    * intensityBump;
                rt.localScale = new Vector3(
                    _scales[i].x * pulse,
                    _scales[i].y * pulse,
                    1f);

                // Hue shift over time
                float hue = (_hueOffsets[i] + elapsed * _hueSpeed[i]) % 1f;
                if (hue < 0f) hue += 1f;
                Color col = Color.HSVToRGB(hue, 1f, 1f);
                col.a = maxAlpha * intensityBump;
                _layers[i].color = col;
            }

            yield return null;
        }

        StopEffect();
    }
}