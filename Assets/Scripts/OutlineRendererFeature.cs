using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

// Add this component to your PC_Renderer (Universal Renderer) asset
// via the "Add Renderer Feature" button in its Inspector.
// It reads OutlineVolumeComponent from the Volume stack each frame.
public class OutlineRendererFeature : ScriptableRendererFeature
{
    OutlinePass _pass;

    public override void Create()
    {
        _pass = new OutlinePass();
        // Run just before URP's built-in post-processing so ACES/bloom apply on top.
        _pass.renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Skip for scene-view cameras to keep editor clean.
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        var stack = VolumeManager.instance.stack;
        var outline = stack.GetComponent<OutlineVolumeComponent>();
        if (outline == null || !outline.IsActive()) return;

        renderer.EnqueuePass(_pass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        _pass.Setup(renderer.cameraColorTargetHandle);
    }

    protected override void Dispose(bool disposing)
    {
        _pass?.Dispose();
    }

    // -------------------------------------------------------------------------

    sealed class OutlinePass : ScriptableRenderPass
    {
        RTHandle _source;
        RTHandle _tempRT;
        Material _material;

        static readonly int s_OutlineColor     = Shader.PropertyToID("_OutlineColor");
        static readonly int s_Thickness        = Shader.PropertyToID("_Thickness");
        static readonly int s_DepthSensitivity = Shader.PropertyToID("_DepthSensitivity");
        static readonly int s_NormalSensitivity= Shader.PropertyToID("_NormalSensitivity");
        static readonly int s_EdgeThreshold    = Shader.PropertyToID("_EdgeThreshold");

        public void Setup(RTHandle colorHandle)
        {
            _source = colorHandle;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            var desc = renderingData.cameraData.cameraTargetDescriptor;
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(
                ref _tempRT, desc,
                FilterMode.Bilinear, TextureWrapMode.Clamp,
                name: "_OutlineTempRT");
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (_material == null)
            {
                var shader = Shader.Find("Custom/ScreenSpaceOutline");
                if (shader == null)
                {
                    Debug.LogWarning("[OutlineRendererFeature] Custom/ScreenSpaceOutline shader not found - make sure ScreenSpaceOutline.shader is in your Assets/Shaders folder.");
                    return;
                }
                _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            }

            var stack  = VolumeManager.instance.stack;
            var outline = stack.GetComponent<OutlineVolumeComponent>();
            if (outline == null || !outline.IsActive()) return;

            _material.SetColor(s_OutlineColor,      outline.OutlineColor.value);
            _material.SetFloat(s_Thickness,         outline.Thickness.value);
            _material.SetFloat(s_DepthSensitivity,  outline.DepthSensitivity.value);
            _material.SetFloat(s_NormalSensitivity, outline.NormalSensitivity.value);
            _material.SetFloat(s_EdgeThreshold,     outline.EdgeThreshold.value);

            var cmd = CommandBufferPool.Get("Screen Space Outline");

            // Blit source -> temp with outline shader, then copy back.
            Blitter.BlitCameraTexture(cmd, _source, _tempRT, _material, 0);
            Blitter.BlitCameraTexture(cmd, _tempRT, _source);

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public override void OnCameraCleanup(CommandBuffer cmd) { }

        public void Dispose()
        {
            _tempRT?.Release();
            if (_material != null)
                CoreUtils.Destroy(_material);
        }
    }
}
