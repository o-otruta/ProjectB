Shader "ProjectB/UI/ToySurface"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255

        _CullMode ("Cull Mode", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
        _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_CullMode]
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
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex        : SV_POSITION;
                fixed4 color         : COLOR;
                float2 texcoord      : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 mask          : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;
            int _UIVertexColorAlwaysGammaSpace;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;

                float2 pixelSize = vPosition.w;
                pixelSize /= abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
                {
                    v.color.rgb = UIGammaToLinear(v.color.rgb);
                }
                OUT.color = v.color * _Color;
                return OUT;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 tex = tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd;
                half alpha = tex.a * IN.color.a;

                half L = IsGammaSpace() ? tex.r : LinearToGammaSpace(tex.rgb).r;
                half3 C = IsGammaSpace() ? IN.color.rgb : LinearToGammaSpace(IN.color.rgb);
                half lumC = dot(C, half3(0.299, 0.587, 0.114));

                half3 cShadow = half3(0.020, 0.022, 0.075);
                half3 cOutline = lerp(half3(0.030, 0.030, 0.090), C * 0.08, 0.15);
                half3 cLedge = lerp(C * 0.56, pow(max(C, 0.001), half3(1.65, 1.75, 1.75)) * 0.86, smoothstep(0.15, 0.65, lumC));
                half3 cFaceBot = C * 0.94;
                half3 cFaceTop = saturate(C * 1.03);
                half3 cHiDark = saturate(C * 1.34 + half3(0.11, 0.11, 0.16));
                half3 cHiBright = lerp(C, half3(1.0, 0.98, 0.92), 0.54);
                half3 cHi = lerp(cHiDark, cHiBright, smoothstep(0.22, 0.62, lumC));

                half3 outRgb;
                if (L < 0.05)
                {
                    outRgb = lerp(cShadow, cOutline, saturate(L / 0.05));
                }
                else if (L < 0.38)
                {
                    outRgb = lerp(cOutline, cLedge, saturate((L - 0.05) / 0.33));
                }
                else if (L < 0.72)
                {
                    outRgb = lerp(cLedge, cFaceBot, saturate((L - 0.38) / 0.34));
                }
                else if (L < 0.78)
                {
                    outRgb = lerp(cFaceBot, cFaceTop, saturate((L - 0.72) / 0.06));
                }
                else
                {
                    outRgb = lerp(cFaceTop, cHi, saturate((L - 0.78) / 0.22));
                }

                half3 finalRgb = IsGammaSpace() ? outRgb : GammaToLinearSpace(outRgb);
                half4 color = half4(finalRgb, alpha);

                #if UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
                color.a *= m.x * m.y;
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
