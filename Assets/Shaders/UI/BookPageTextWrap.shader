Shader "UI/BookPageTextWrap"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)

        _LeftTextTex("Left Text Texture", 2D) = "black" {}
        _RightTextTex("Right Text Texture", 2D) = "black" {}
        _BookDepthTex("Book Depth Map", 2D) = "gray" {}
        _LeftDepthTex("Left Page Color Map", 2D) = "gray" {}
        _RightDepthTex("Right Page Color Map", 2D) = "gray" {}

        _TextTint("Text Tint", Color) = (1, 1, 1, 1)
        _TextTintStrength("Text Tint Strength", Range(0, 1)) = 0
        _TextBlend("Text Blend", Range(0, 1)) = 1
        _DepthShadow("Depth Shadow", Range(0, 1)) = 0.2
        _DepthBias("Depth Bias", Range(-1, 1)) = 0
        _TextScale("Page Image Scale", Range(0.5, 1.5)) = 1
        _TextScaleX("Page Image Scale X", Range(0.5, 1.5)) = 1
        _PageSeparation("Page Separation", Range(-1, 1)) = 0

        _LeftWarpStrength("Left Warp Strength", Range(-0.25, 0.25)) = 0.03
        _RightWarpStrength("Right Warp Strength", Range(-0.25, 0.25)) = 0.03
        _PageSplit("Page Split", Range(0.1, 0.9)) = 0.5
        _DepthColorSoftness("Depth Color Softness", Range(0.1, 1)) = 0.4
        _DepthColorThreshold("Depth Color Threshold", Range(0, 1)) = 0.35

        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255

        _ColorMask("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;

            sampler2D _LeftTextTex;
            sampler2D _RightTextTex;
            sampler2D _BookDepthTex;
            sampler2D _LeftDepthTex;
            sampler2D _RightDepthTex;
            float4 _LeftTextTex_ST;
            float4 _RightTextTex_ST;
            float4 _BookDepthTex_ST;

            fixed4 _TextTint;
            float _TextTintStrength;
            float _TextBlend;
            float _DepthShadow;
            float _DepthBias;
            float _TextScale;
            float _TextScaleX;
            float _PageSeparation;
            float _LeftWarpStrength;
            float _RightWarpStrength;
            float _PageSplit;
            float _DepthColorSoftness;
            float _DepthColorThreshold;

            float4 _ClipRect;

            float2 ApplyST(float2 uv, float4 st)
            {
                return uv * st.xy + st.zw;
            }

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = IN.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = IN.texcoord;
                OUT.color = IN.color * _Color;
                return OUT;
            }

            float Luminance(float3 c)
            {
                return dot(c, float3(0.2126, 0.7152, 0.0722));
            }

            float BrightnessValue(float3 c)
            {
                return max(c.r, max(c.g, c.b));
            }

            float ColorMatchWeight(float3 sample, float3 targetColor, float softness)
            {
                float distanceToTarget = distance(sample, targetColor);
                return saturate(1.0 - smoothstep(0.0, softness, distanceToTarget));
            }

            float ColorMapToHeight(float3 sample)
            {
                // Restrict depth to the four intended page hues; non-matching colors become neutral depth.
                float purple = ColorMatchWeight(sample, float3(0.58, 0.25, 0.72), _DepthColorSoftness);
                float green = ColorMatchWeight(sample, float3(0.18, 0.62, 0.24), _DepthColorSoftness);
                float cyan = ColorMatchWeight(sample, float3(0.18, 0.80, 0.82), _DepthColorSoftness);
                float orange = ColorMatchWeight(sample, float3(0.90, 0.52, 0.16), _DepthColorSoftness);

                float colorMask = max(max(purple, green), max(cyan, orange));
                float isolatedMask = smoothstep(_DepthColorThreshold, 1.0, colorMask);
                float valueDepth = BrightnessValue(sample);

                return lerp(0.5, valueDepth, isolatedMask);
            }

            float2 ScaleUVCentered(float2 uv, float scaleX, float scaleY)
            {
                float2 safeScale = max(float2(scaleX, scaleY), float2(0.0001, 0.0001));
                return (uv - 0.5) / safeScale + 0.5;
            }

            float UVInBoundsMask(float2 uv)
            {
                return step(0.0, uv.x) * step(uv.x, 1.0) * step(0.0, uv.y) * step(uv.y, 1.0);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 baseCol = tex2D(_MainTex, uv) * IN.color;

                float split = clamp(_PageSplit, 0.1, 0.9);
                float isLeft = step(uv.x, split);

                float2 uvLeft = float2(saturate(uv.x / split), uv.y);
                float2 uvRight = float2(saturate((uv.x - split) / (1.0 - split)), uv.y);

                float leftWarp;
                float rightWarp;

                float2 leftTextUV = float2(uvLeft.x, uvLeft.y);
                float2 rightTextUV = float2(uvRight.x, uvRight.y);

                leftTextUV = ScaleUVCentered(leftTextUV, _TextScale * _TextScaleX, _TextScale);
                rightTextUV = ScaleUVCentered(rightTextUV, _TextScale * _TextScaleX, _TextScale);

                // Positive separation creates a center gutter. Negative values pull pages closer.
                leftTextUV.x += _PageSeparation;
                rightTextUV.x -= _PageSeparation;

                float2 leftDepthBookUV = float2(uvLeft.x * split, uvLeft.y);
                float2 rightDepthBookUV = float2(split + uvRight.x * (1.0 - split), uvRight.y);

                float leftDepth = ColorMapToHeight(tex2D(_BookDepthTex, ApplyST(leftDepthBookUV, _BookDepthTex_ST)).rgb);
                float rightDepth = ColorMapToHeight(tex2D(_BookDepthTex, ApplyST(rightDepthBookUV, _BookDepthTex_ST)).rgb);

                leftWarp = (leftDepth - 0.5 + _DepthBias) * _LeftWarpStrength;
                rightWarp = (rightDepth - 0.5 + _DepthBias) * _RightWarpStrength;

                // Warp toward/away from seam in X so it curls rather than sliding upward.
                leftTextUV.x += leftWarp;
                rightTextUV.x -= rightWarp;

                float leftMask = UVInBoundsMask(leftTextUV);
                float rightMask = UVInBoundsMask(rightTextUV);

                float2 leftSampleUV = saturate(leftTextUV);
                float2 rightSampleUV = saturate(rightTextUV);
                float2 leftAtlasUV = ApplyST(leftSampleUV, _LeftTextTex_ST);
                float2 rightAtlasUV = ApplyST(rightSampleUV, _RightTextTex_ST);

                fixed4 leftText = tex2D(_LeftTextTex, leftAtlasUV) * leftMask;
                fixed4 rightText = tex2D(_RightTextTex, rightAtlasUV) * rightMask;

                fixed4 textSample = lerp(rightText, leftText, isLeft);
                float depthSample = lerp(rightDepth, leftDepth, isLeft);
                float textAlpha = saturate(textSample.a * _TextBlend);

                float foldShadow = lerp(1.0, saturate(1.0 - (1.0 - depthSample) * _DepthShadow), textAlpha);
                float3 tintedPageColor = textSample.rgb * _TextTint.rgb;
                float3 pageColor = lerp(textSample.rgb, tintedPageColor, saturate(_TextTintStrength));
                float3 mixed = lerp(baseCol.rgb, pageColor, textAlpha) * foldShadow;
                fixed4 color = fixed4(mixed, baseCol.a);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
