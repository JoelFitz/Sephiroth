using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Central post-processing control hub. Attach to the GameObject that holds your Global Volume.
/// In the Inspector you can tweak all effects from one place and see live results.
/// Call TransitionToPreset() at runtime to blend between moods (e.g. cave, forest, night).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Volume))]
public class PostProcessingController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    // Inspector sections
    // -------------------------------------------------------------------------

    [Header("── Bloom ─────────────────────────────")]
    [Range(0f, 5f)]   public float bloomIntensity  = 0.25f;
    [Range(0f, 2f)]   public float bloomThreshold  = 1f;
    [Range(0f, 1f)]   public float bloomScatter    = 0.7f;
    [ColorUsage(false, true)]
    public Color bloomTint = Color.white;

    [Header("── Colour Adjustments ──────────────────")]
    [Range(-2f, 2f)]    public float postExposure = 0f;
    [Range(-100f, 100f)]public float contrast     = 0f;
    [Range(-100f, 100f)]public float saturation   = 0f;
    [Range(-180f, 180f)]public float hueShift     = 0f;

    [Header("── White Balance ──────────────────────")]
    [Range(-100f, 100f)]public float temperature = 0f;
    [Range(-100f, 100f)]public float whiteBalanceTint = 0f;

    [Header("── Vignette ───────────────────────────")]
    [Range(0f, 1f)]public float vignetteIntensity  = 0.167f;
    [Range(0f, 1f)]public float vignetteSmoothness = 0.477f;
    public Color vignetteColor = Color.black;

    [Header("── Film Grain ─────────────────────────")]
    [Range(0f, 1f)]  public float grainIntensity = 0f;
    [Range(0.3f, 3f)]public float grainSize      = 1f;

    [Header("── Depth of Field ──────────────────────")]
    public bool                     dofEnabled      = false;
    public DepthOfFieldMode         dofMode         = DepthOfFieldMode.Bokeh;
    [Range(0.1f, 100f)]public float dofFocusDistance = 10f;
    [Range(1f, 32f)]   public float dofAperture      = 5.6f;
    [Range(1f, 300f)]  public float dofFocalLength   = 50f;

    [Header("── Oasis Fog ───────────────────────────")]
    [Range(0f, 1f)]    public float fogDensity           = 0f;
    [Range(0f, 200f)]  public float fogStartDistance     = 0f;
    public Vector2                  fogHeightRange        = new Vector2(0f, 50f);
    public Color                    fogTint               = Color.white;
    [Range(0f, 10f)]   public float fogSunScattering     = 2f;

    [Header("── Outline ─────────────────────────────")]
    public bool  outlinesEnabled      = false;
    public Color outlineColor         = new Color(0.05f, 0.03f, 0.08f, 1f);
    [Range(0.5f, 4f)]  public float outlineThickness         = 1f;
    [Range(0f, 20f)]   public float outlineDepthSensitivity  = 5f;
    [Range(0f, 20f)]   public float outlineNormalSensitivity = 1f;
    [Range(0f, 1f)]    public float outlineEdgeThreshold     = 0.1f;

    // -------------------------------------------------------------------------
    // Private refs
    // -------------------------------------------------------------------------

    Volume               _volume;
    Bloom                _bloom;
    ColorAdjustments     _colorAdj;
    WhiteBalance         _wb;
    Vignette             _vignette;
    FilmGrain            _grain;
    DepthOfField         _dof;
    OasisFogVolumeComponent  _fog;
    OutlineVolumeComponent   _outline;

    // -------------------------------------------------------------------------
    // Unity messages
    // -------------------------------------------------------------------------

    void OnEnable()
    {
        _volume = GetComponent<Volume>();
        FetchComponents();
        ReadFromProfile();       // populate Inspector from profile on enable
    }

    void OnValidate()
    {
        if (_volume == null) _volume = GetComponent<Volume>();
        if (_bloom == null) FetchComponents();
        ApplyToProfile();
    }

    // -------------------------------------------------------------------------
    // Component fetching
    // -------------------------------------------------------------------------

    void FetchComponents()
    {
        if (_volume == null || _volume.sharedProfile == null) return;
        var p = _volume.sharedProfile;
        p.TryGet(out _bloom);
        p.TryGet(out _colorAdj);
        p.TryGet(out _wb);
        p.TryGet(out _vignette);
        p.TryGet(out _grain);
        p.TryGet(out _dof);
        p.TryGet(out _fog);
        p.TryGet(out _outline);
    }

    // -------------------------------------------------------------------------
    // Read current profile values into the Inspector fields
    // -------------------------------------------------------------------------

    void ReadFromProfile()
    {
        FetchComponents();

        if (_bloom != null)
        {
            bloomIntensity = _bloom.intensity.value;
            bloomThreshold = _bloom.threshold.value;
            bloomScatter   = _bloom.scatter.value;
            bloomTint      = _bloom.tint.value;
        }
        if (_colorAdj != null)
        {
            postExposure = _colorAdj.postExposure.value;
            contrast     = _colorAdj.contrast.value;
            saturation   = _colorAdj.saturation.value;
            hueShift     = _colorAdj.hueShift.value;
        }
        if (_wb != null)
        {
            temperature       = _wb.temperature.value;
            whiteBalanceTint  = _wb.tint.value;
        }
        if (_vignette != null)
        {
            vignetteIntensity  = _vignette.intensity.value;
            vignetteSmoothness = _vignette.smoothness.value;
            vignetteColor      = _vignette.color.value;
        }
        if (_grain != null)
        {
            grainIntensity = _grain.intensity.value;
        }
        if (_dof != null)
        {
            dofEnabled     = _dof.active;
            dofMode         = _dof.mode.value;
            dofFocusDistance= _dof.focusDistance.value;
            dofAperture    = _dof.aperture.value;
            dofFocalLength  = _dof.focalLength.value;
        }
        if (_fog != null)
        {
            fogDensity       = _fog.Density.value;
            fogStartDistance = _fog.StartDistance.value;
            fogHeightRange   = _fog.HeightRange.value;
            fogTint          = _fog.Tint.value;
            fogSunScattering = _fog.SunScatteringIntensity.value;
        }
        if (_outline != null)
        {
            outlinesEnabled          = _outline.Enabled.value;
            outlineColor             = _outline.OutlineColor.value;
            outlineThickness         = _outline.Thickness.value;
            outlineDepthSensitivity  = _outline.DepthSensitivity.value;
            outlineNormalSensitivity = _outline.NormalSensitivity.value;
            outlineEdgeThreshold     = _outline.EdgeThreshold.value;
        }
    }

    // -------------------------------------------------------------------------
    // Write Inspector fields back to profile
    // -------------------------------------------------------------------------

    public void ApplyToProfile()
    {
        if (_bloom == null) FetchComponents();
        if (_volume == null) return;

        if (_bloom != null)
        {
            _bloom.intensity.Override(bloomIntensity);
            _bloom.threshold.Override(bloomThreshold);
            _bloom.scatter.Override(bloomScatter);
            _bloom.tint.Override(bloomTint);
        }
        if (_colorAdj != null)
        {
            _colorAdj.postExposure.Override(postExposure);
            _colorAdj.contrast.Override(contrast);
            _colorAdj.saturation.Override(saturation);
            _colorAdj.hueShift.Override(hueShift);
        }
        if (_wb != null)
        {
            _wb.temperature.Override(temperature);
            _wb.tint.Override(whiteBalanceTint);
        }
        if (_vignette != null)
        {
            _vignette.intensity.Override(vignetteIntensity);
            _vignette.smoothness.Override(vignetteSmoothness);
            _vignette.color.Override(vignetteColor);
        }
        if (_grain != null)
        {
            _grain.intensity.Override(grainIntensity);
            _grain.response.Override(grainSize);
        }
        if (_dof != null)
        {
            _dof.active = dofEnabled;
            _dof.mode.Override(dofMode);
            _dof.focusDistance.Override(dofFocusDistance);
            _dof.aperture.Override(dofAperture);
            _dof.focalLength.Override(dofFocalLength);
        }
        if (_fog != null)
        {
            _fog.Density.Override(fogDensity);
            _fog.StartDistance.Override(fogStartDistance);
            _fog.HeightRange.Override(fogHeightRange);
            _fog.Tint.Override(fogTint);
            _fog.SunScatteringIntensity.Override(fogSunScattering);
        }
        if (_outline != null)
        {
            _outline.Enabled.Override(outlinesEnabled);
            _outline.OutlineColor.Override(outlineColor);
            _outline.Thickness.Override(outlineThickness);
            _outline.DepthSensitivity.Override(outlineDepthSensitivity);
            _outline.NormalSensitivity.Override(outlineNormalSensitivity);
            _outline.EdgeThreshold.Override(outlineEdgeThreshold);
        }
    }

    // -------------------------------------------------------------------------
    // Runtime mood presets  (call from other scripts / cutscene events)
    // -------------------------------------------------------------------------

    public enum MoodPreset { ForestDay, ForestNight, CaveDeep, CuteFroggy }

    public void ApplyPreset(MoodPreset mood)
    {
        switch (mood)
        {
            case MoodPreset.ForestDay:
                bloomIntensity = 0.25f; bloomThreshold = 1.0f;
                postExposure = 0f; contrast = 10f; saturation = 15f;
                temperature = 0f;
                vignetteIntensity = 0.167f;
                fogDensity = 0f;
                break;

            case MoodPreset.ForestNight:
                bloomIntensity = 0.6f; bloomThreshold = 0.8f;
                bloomTint = new Color(0.4f, 0.5f, 1f);
                postExposure = -0.5f; contrast = 20f; saturation = -10f;
                temperature = -20f;
                vignetteIntensity = 0.35f;
                fogDensity = 0.15f; fogTint = new Color(0.1f, 0.12f, 0.22f);
                break;

            case MoodPreset.CaveDeep:
                bloomIntensity = 0.8f; bloomThreshold = 0.6f;
                bloomTint = new Color(0.5f, 0.3f, 1f);
                postExposure = -0.8f; contrast = 30f; saturation = -20f;
                temperature = -30f;
                vignetteIntensity = 0.5f;
                fogDensity = 0.4f; fogStartDistance = 10f;
                fogTint = new Color(0.05f, 0.03f, 0.1f);
                break;

            case MoodPreset.CuteFroggy:
                // Bright, saturated, warm — ideal for the low-poly frog game.
                bloomIntensity = 0.4f; bloomThreshold = 0.85f;
                bloomTint = new Color(0.8f, 1f, 0.6f);
                postExposure = 0.2f; contrast = -5f; saturation = 25f;
                temperature = 10f;
                vignetteIntensity = 0.08f;
                outlineColor = new Color(0.1f, 0.06f, 0.02f, 1f);
                outlinesEnabled = true; outlineThickness = 1.5f;
                outlineDepthSensitivity = 8f; outlineNormalSensitivity = 2f;
                fogDensity = 0f;
                break;
        }
        ApplyToProfile();
    }
}
