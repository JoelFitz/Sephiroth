Shader "UI/BookPageTextWrap"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)

        _LeftTextTex("Left Text Texture", 2D) = "black" {}
        _RightTextTex("Right Text Texture", 2D) = "black" {}
        _LeftDepthTex("Left Page Color Map", 2D) = "gray" {}
        _RightDepthTex("Right Page Color Map", 2D) = "gray" {}

        _TextTint("Text Tint", Color) = (0.07, 0.06, 0.05, 1)
        _TextBlend("Text Blend", Range(0, 1)) = 1
        _DepthShadow("Depth Shadow", Range(0, 1)) = 0.2
        _DepthBias("Depth Bias", Range(-1, 1)) = 0

        _LeftWarpStrength("Left Warp Strength", Range(-0.25, 0.25)) = 0.03
        _RightWarpStrength("Right Warp Strength", Range(-0.25, 0.25)) = 0.03
        _PageSplit("Page Split", Range(0.1, 0.9)) = 0.5

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
            sampler2D _LeftDepthTex;
            sampler2D _RightDepthTex;

            fixed4 _TextTint;
            float _TextBlend;
            float _DepthShadow;
            float _DepthBias;
            float _LeftWarpStrength;
            float _RightWarpStrength;
            float _PageSplit;

            float4 _ClipRect;

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

            float ColorMatchWeight(float3 sample, float3 targetColor, float softness)
            {
                float distanceToTarget = distance(sample, targetColor);
                return saturate(1.0 - smoothstep(0.0, softness, distanceToTarget));
            }

            float ColorMapToHeight(float3 sample)
            {
                // The NewBook page maps are color-coded, so use hue regions rather than only luminance.
                float purple = ColorMatchWeight(sample, float3(0.58, 0.25, 0.72), 0.55);
                float green = ColorMatchWeight(sample, float3(0.18, 0.62, 0.24), 0.55);
                float cyan = ColorMatchWeight(sample, float3(0.18, 0.80, 0.82), 0.55);
                float orange = ColorMatchWeight(sample, float3(0.90, 0.52, 0.16), 0.55);

                float weighted = purple * 0.30 + green * 0.62 + cyan * 0.44 + orange * 0.76;
                float fallback = Luminance(sample);

                return saturate(max(weighted, fallback));
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                float2 uv = IN.texcoord;
                fixed4 baseCol = tex2D(_MainTex, uv) * IN.color;

                float split = clamp(_PageSplit, 0.1, 0.9);
                float isLeft = step(uv.x, split);

                float2 uvLeft = float2(saturate(uv.x / split), uv.y);
                float2 uvRight = float2(saturate((uv.x - split) / (1.0 - split)), uv.y);

                float3 leftMap = tex2D(_LeftDepthTex, uvLeft).rgb;
                float3 rightMap = tex2D(_RightDepthTex, uvRight).rgb;

                float leftDepth = ColorMapToHeight(leftMap);
                float rightDepth = ColorMapToHeight(rightMap);

                float leftWarp = (leftDepth - 0.5 + _DepthBias) * _LeftWarpStrength;
                float rightWarp = (rightDepth - 0.5 + _DepthBias) * _RightWarpStrength;

                float2 leftTextUV = float2(uvLeft.x, uvLeft.y + leftWarp);
                float2 rightTextUV = float2(uvRight.x, uvRight.y + rightWarp);

                fixed4 leftText = tex2D(_LeftTextTex, leftTextUV);
                fixed4 rightText = tex2D(_RightTextTex, rightTextUV);

                fixed4 textSample = lerp(rightText, leftText, isLeft);
                float depthSample = lerp(rightDepth, leftDepth, isLeft);
                float textAlpha = saturate(textSample.a * _TextBlend);

                float foldShadow = lerp(1.0, saturate(1.0 - (1.0 - depthSample) * _DepthShadow), textAlpha);
                float3 mixed = lerp(baseCol.rgb, _TextTint.rgb, textAlpha) * foldShadow;
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
