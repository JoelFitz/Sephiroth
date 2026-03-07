using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Matches the serialized data in DefaultVolumeProfile.asset
// Parameters: Density, StartDistance, HeightRange, Tint, SunScatteringIntensity
[Serializable, VolumeComponentMenu("Custom/Oasis Fog")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class OasisFogVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("How dense the fog is. 0 = off.")]
    public ClampedFloatParameter Density = new ClampedFloatParameter(0f, 0f, 1f);

    [Tooltip("Distance from camera before fog starts.")]
    public MinFloatParameter StartDistance = new MinFloatParameter(0f, 0f);

    [Tooltip("World-space Y min/max range for height fog falloff.")]
    public Vector2Parameter HeightRange = new Vector2Parameter(new Vector2(0f, 50f));

    [Tooltip("Fog color tint.")]
    public ColorParameter Tint = new ColorParameter(Color.white, true, false, false);

    [Tooltip("Intensity of sun-direction scattering (forward scatter glow).")]
    public ClampedFloatParameter SunScatteringIntensity = new ClampedFloatParameter(2f, 0f, 10f);

    public bool IsActive() => Density.value > 0f;
    public bool IsTileCompatible() => false;
}
