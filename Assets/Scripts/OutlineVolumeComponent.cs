using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Matches the serialized data in DefaultVolumeProfile.asset
// The existing asset only has "Enabled" - all new params default to sensible values.
[Serializable, VolumeComponentMenu("Custom/Screen Space Outline")]
[SupportedOnRenderPipeline(typeof(UniversalRenderPipelineAsset))]
public sealed class OutlineVolumeComponent : VolumeComponent, IPostProcessComponent
{
    [Tooltip("Toggle outlines on/off.")]
    public BoolParameter Enabled = new BoolParameter(false);

    [Tooltip("Outline colour and opacity (alpha controls strength).")]
    public ColorParameter OutlineColor = new ColorParameter(new Color(0.05f, 0.03f, 0.08f, 1f));

    [Tooltip("How many pixels wide the Sobel sample offset is.")]
    public ClampedFloatParameter Thickness = new ClampedFloatParameter(1f, 0.5f, 4f);

    [Tooltip("How strongly depth discontinuities generate outlines.")]
    public ClampedFloatParameter DepthSensitivity = new ClampedFloatParameter(5f, 0f, 20f);

    [Tooltip("How strongly normal discontinuities generate outlines.")]
    public ClampedFloatParameter NormalSensitivity = new ClampedFloatParameter(1f, 0f, 20f);

    [Tooltip("Minimum edge magnitude before an outline is drawn.")]
    public ClampedFloatParameter EdgeThreshold = new ClampedFloatParameter(0.1f, 0f, 1f);

    public bool IsActive() => Enabled.value && (DepthSensitivity.value > 0f || NormalSensitivity.value > 0f);
    public bool IsTileCompatible() => false;
}
