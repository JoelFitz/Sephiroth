// LowPolyCuteToon.shader  –  URP 17  (Deferred-compatible)
// A three-pass toon shader designed for low-poly "cute" characters.
//
// Features:
//   • Inverted-hull outline pass  (consistent NDC width)
//   • Stepped toon diffuse  (shadow / optional midtone / lit)
//   • Toon specular highlight  (hard dot)
//   • Fresnel rim light  (great for cartoon edge-glow)
//   • Receives URP main-light shadows & fog
//   • Proper ShadowCaster and DepthOnly passes

Shader "Custom/LowPolyCuteToon"
{
    Properties
    {
        [Header(Base)]
        _MainTex    ("Albedo Texture",  2D)    = "white" {}
        _Color      ("Base Colour",     Color) = (0.35, 0.75, 0.25, 1)

        [Header(Toon Diffuse)]
        _ShadowColor      ("Shadow Colour",       Color)         = (0.18, 0.28, 0.42, 1)
        _ShadowThreshold  ("Shadow Threshold",    Range(-1, 1))  = 0.0
        _ShadowSmoothness ("Shadow Edge Soft",    Range(0.001, 0.3)) = 0.04

        [Toggle] _UseMidtone ("Use Midtone Step", Float) = 0
        _MidtoneColor     ("Midtone Colour",      Color)         = (0.25, 0.55, 0.30, 1)
        _MidtoneThreshold ("Midtone Threshold",   Range(-1, 1))  = 0.35

        [Header(Specular)]
        [HDR] _SpecColor  ("Specular Colour",     Color)         = (1, 1, 1, 1)
        _Glossiness       ("Glossiness",          Range(0, 1))   = 0.65
        _SpecThreshold    ("Spec Edge Threshold", Range(0.001, 0.3)) = 0.04

        [Header(Rim Light)]
        [HDR] _RimColor   ("Rim Colour",          Color)         = (0.6, 1.0, 0.65, 1)
        _RimPower         ("Rim Power",           Range(0.5, 8)) = 3.5
        _RimThreshold     ("Rim Threshold",       Range(0, 1))   = 0.55
        _RimSmoothness    ("Rim Edge Soft",       Range(0.001, 0.3)) = 0.08

        [Header(Ambient)]
        _AmbientStrength  ("Ambient multiplier",  Range(0, 1))   = 0.25

        [Header(Outline)]
        _OutlineWidth     ("Outline Width",       Range(0, 0.05))= 0.012
        _OutlineColor     ("Outline Colour",      Color)         = (0.07, 0.04, 0.10, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }
        LOD 200

        // =====================================================================
        // PASS 0  –  Inverted-hull outline
        // =====================================================================
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull  Front
            ZWrite On
            ZTest Less

            HLSLPROGRAM
            #pragma vertex   OutlineVert
            #pragma fragment OutlineFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ShadowColor;
                float4 _MidtoneColor;
                float4 _SpecColor;
                float4 _RimColor;
                float4 _OutlineColor;
                float4 _MainTex_ST;
                float  _ShadowThreshold;
                float  _ShadowSmoothness;
                float  _UseMidtone;
                float  _MidtoneThreshold;
                float  _Glossiness;
                float  _SpecThreshold;
                float  _RimPower;
                float  _RimThreshold;
                float  _RimSmoothness;
                float  _AmbientStrength;
                float  _OutlineWidth;
            CBUFFER_END

            struct AttributesOL
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct VaryingsOL
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsOL OutlineVert(AttributesOL input)
            {
                VaryingsOL o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float4 posCS = TransformObjectToHClip(input.positionOS.xyz);

                // Project normal to clip space and expand uniformly in NDC.
                float3 normWS = TransformObjectToWorldNormal(input.normalOS);
                float2 clipNorm = mul((float3x3)UNITY_MATRIX_VP, normWS).xy;
                clipNorm = normalize(clipNorm);
                posCS.xy += clipNorm * (_OutlineWidth * posCS.w);

                o.positionCS = posCS;
                return o;
            }

            float4 OutlineFrag(VaryingsOL i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 1  –  Forward Lit  (toon shading)
        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull  Back
            ZWrite On

            HLSLPROGRAM
            #pragma vertex   ToonVert
            #pragma fragment ToonFrag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _ShadowColor;
                float4 _MidtoneColor;
                float4 _SpecColor;
                float4 _RimColor;
                float4 _OutlineColor;
                float4 _MainTex_ST;
                float  _ShadowThreshold;
                float  _ShadowSmoothness;
                float  _UseMidtone;
                float  _MidtoneThreshold;
                float  _Glossiness;
                float  _SpecThreshold;
                float  _RimPower;
                float  _RimThreshold;
                float  _RimSmoothness;
                float  _AmbientStrength;
                float  _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                o.positionWS  = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS  = TransformWorldToHClip(o.positionWS);
                o.normalWS    = TransformObjectToWorldNormal(input.normalOS);
                o.uv          = TRANSFORM_TEX(input.uv, _MainTex);
                o.shadowCoord = TransformWorldToShadowCoord(o.positionWS);
                o.fogFactor   = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            float4 ToonFrag(Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));

                // ---- Main light ----
                Light mainLight = GetMainLight(i.shadowCoord);
                float3 L = normalize(mainLight.direction);
                float  atten = mainLight.shadowAttenuation * mainLight.distanceAttenuation;

                // ---- Toon diffuse step ----
                float NdotL = dot(N, L);
                float shadow = smoothstep(
                    _ShadowThreshold - _ShadowSmoothness,
                    _ShadowThreshold + _ShadowSmoothness,
                    NdotL * atten);

                float3 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).rgb * _Color.rgb;

                float3 diffuse;
                if (_UseMidtone > 0.5)
                {
                    float mid = smoothstep(
                        _MidtoneThreshold - _ShadowSmoothness,
                        _MidtoneThreshold + _ShadowSmoothness,
                        NdotL * atten);
                    diffuse = lerp(_ShadowColor.rgb,
                                   lerp(_MidtoneColor.rgb, albedo, mid),
                                   shadow);
                }
                else
                {
                    diffuse = lerp(_ShadowColor.rgb, albedo, shadow);
                }

                // ---- Toon specular ----
                float3 H    = normalize(L + V);
                float  spec = pow(max(0.0, dot(N, H)), _Glossiness * 128.0);
                float  toonSpec = smoothstep(
                    _SpecThreshold,
                    _SpecThreshold + 0.02,
                    spec) * shadow;
                float3 specular = _SpecColor.rgb * toonSpec;

                // ---- Rim / fresnel ----
                float rim     = 1.0 - saturate(dot(V, N));
                rim           = pow(rim, _RimPower);
                float toonRim = smoothstep(
                    _RimThreshold - _RimSmoothness,
                    _RimThreshold + _RimSmoothness,
                    rim) * shadow;
                float3 rimLight = _RimColor.rgb * toonRim;

                // ---- Ambient (spherical harmonics) ----
                float3 ambient = SampleSH(N) * _AmbientStrength;

                // ---- Combine ----
                float3 col = diffuse * mainLight.color + specular + rimLight + ambient;

                // ---- Fog ----
                col = MixFog(col, i.fogFactor);

                return float4(col, _Color.a);
            }
            ENDHLSL
        }

        // =====================================================================
        // PASS 2  –  Shadow Caster
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull  Back

            HLSLPROGRAM
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttribs
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 posCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttribs input)
            {
                ShadowVaryings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 posWS  = TransformObjectToWorld(input.posOS.xyz);
                float3 normWS = TransformObjectToWorldNormal(input.normOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDir = normalize(_LightPosition - posWS);
                #else
                    float3 lightDir = _LightDirection;
                #endif

                o.posCS = TransformWorldToHClip(ApplyShadowBias(posWS, normWS, lightDir));
                return o;
            }

            float4 ShadowFrag(ShadowVaryings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // =====================================================================
        // PASS 3  –  Depth Only  (for SSAO / depth prepass)
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthOnlyVert
            #pragma fragment DepthOnlyFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DepthAttribs
            {
                float4 posOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 posCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthOnlyVert(DepthAttribs input)
            {
                DepthVaryings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.posCS = TransformObjectToHClip(input.posOS.xyz);
                return o;
            }

            float DepthOnlyFrag(DepthVaryings i) : SV_Target { return 0; }
            ENDHLSL
        }

        // =====================================================================
        // PASS 4  –  DepthNormals  (required by SSAO DepthNormals source)
        // =====================================================================
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex   DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct DNAttribs
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DNVaryings
            {
                float4 posCS   : SV_POSITION;
                float3 normWS  : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DNVaryings DepthNormalsVert(DNAttribs input)
            {
                DNVaryings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.posCS  = TransformObjectToHClip(input.posOS.xyz);
                o.normWS = TransformObjectToWorldNormal(input.normOS);
                return o;
            }

            float4 DepthNormalsFrag(DNVaryings i) : SV_Target
            {
                // Pack normal into [0,1] for the normals texture.
                float3 n = normalize(i.normWS) * 0.5 + 0.5;
                return float4(n, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
