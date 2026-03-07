// ScreenSpaceOutline.shader  –  URP 17
// Fullscreen Sobel-operator outline using depth + normal discontinuities.
// Used by OutlineRendererFeature which reads parameters from OutlineVolumeComponent.
//
// Requirements:
//   • SSAO renderer feature must be present with source = DepthNormals
//     (this enables the _CameraNormalsTexture prepass that we sample here).
//   • Opaque texture must be enabled in the URP Renderer asset.

Shader "Custom/ScreenSpaceOutline"
{
    Properties
    {
        // Set entirely by OutlineRendererFeature – no need to modify manually.
        _OutlineColor      ("Outline Colour",        Color)           = (0, 0, 0, 1)
        _Thickness         ("Thickness (px offset)", Range(0.5, 4))   = 1.0
        _DepthSensitivity  ("Depth Sensitivity",     Range(0, 20))    = 5.0
        _NormalSensitivity ("Normal Sensitivity",    Range(0, 20))    = 1.0
        _EdgeThreshold     ("Edge Threshold",        Range(0, 1))     = 0.1
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        // ── Common render states ──────────────────────────────────────────────
        ZWrite Off
        ZTest  Always
        Blend  Off
        Cull   Off

        Pass
        {
            Name "ScreenSpaceOutlinePass"

            HLSLPROGRAM
            #pragma vertex   Vert
            #pragma fragment Frag

            // Blit.hlsl provides: Varyings, Vert(), _BlitTexture, sampler_LinearClamp,
            // _BlitTexture_TexelSize
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            // ---- Textures ──────────────────────────────────────────────────
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            TEXTURE2D(_CameraNormalsTexture);
            SAMPLER(sampler_CameraNormalsTexture);

            // ---- Properties ────────────────────────────────────────────────
            float4 _OutlineColor;
            float  _Thickness;
            float  _DepthSensitivity;
            float  _NormalSensitivity;
            float  _EdgeThreshold;

            // ── Helpers ──────────────────────────────────────────────────────

            float SampleRawDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_CameraDepthTexture,
                                        sampler_CameraDepthTexture, uv).r;
            }

            // Decode normals from [0,1] to [-1,1]
            float3 SampleWorldNormal(float2 uv)
            {
                return SAMPLE_TEXTURE2D(_CameraNormalsTexture,
                                        sampler_CameraNormalsTexture, uv).rgb * 2.0 - 1.0;
            }

            // ── Sobel on depth (returns gradient magnitude) ──────────────────
            float SobelDepth(float2 uv, float2 ts)
            {
                float t = _Thickness;
                // 3×3 Sobel kernel
                float d00 = SampleRawDepth(uv + ts * float2(-t, -t));
                float d10 = SampleRawDepth(uv + ts * float2( 0, -t));
                float d20 = SampleRawDepth(uv + ts * float2( t, -t));
                float d01 = SampleRawDepth(uv + ts * float2(-t,  0));
                float d21 = SampleRawDepth(uv + ts * float2( t,  0));
                float d02 = SampleRawDepth(uv + ts * float2(-t,  t));
                float d12 = SampleRawDepth(uv + ts * float2( 0,  t));
                float d22 = SampleRawDepth(uv + ts * float2( t,  t));

                float gx = -d00 + d20 - 2*d01 + 2*d21 - d02 + d22;
                float gy = -d00 - 2*d10 - d20 + d02 + 2*d12 + d22;
                return sqrt(gx*gx + gy*gy);
            }

            // ── Sobel on normals (returns gradient magnitude) ────────────────
            float SobelNormals(float2 uv, float2 ts)
            {
                float t = _Thickness;
                float3 n00 = SampleWorldNormal(uv + ts * float2(-t, -t));
                float3 n10 = SampleWorldNormal(uv + ts * float2( 0, -t));
                float3 n20 = SampleWorldNormal(uv + ts * float2( t, -t));
                float3 n01 = SampleWorldNormal(uv + ts * float2(-t,  0));
                float3 n21 = SampleWorldNormal(uv + ts * float2( t,  0));
                float3 n02 = SampleWorldNormal(uv + ts * float2(-t,  t));
                float3 n12 = SampleWorldNormal(uv + ts * float2( 0,  t));
                float3 n22 = SampleWorldNormal(uv + ts * float2( t,  t));

                float3 gx = -n00 + n20 - 2*n01 + 2*n21 - n02 + n22;
                float3 gy = -n00 - 2*n10 - n20 + n02 + 2*n12 + n22;
                return sqrt(dot(gx, gx) + dot(gy, gy));
            }

            // ── Fragment ─────────────────────────────────────────────────────
            float4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 ts = _BlitTexture_TexelSize.xy;   // 1/width, 1/height

                // Scene colour from the blit source (set by Blitter).
                float4 sceneColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv);

                // Compute edges from depth and normals independently.
                float depthEdge  = SobelDepth(uv, ts)   * _DepthSensitivity;
                float normalEdge = SobelNormals(uv, ts) * _NormalSensitivity;

                // Combine and threshold to get a hard silhouette.
                float edge = step(_EdgeThreshold, saturate(depthEdge + normalEdge));

                // Composite: lerp scene -> outline colour by edge mask × outline alpha.
                return lerp(sceneColor, _OutlineColor, edge * _OutlineColor.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
