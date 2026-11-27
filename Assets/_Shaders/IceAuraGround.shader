Shader "ProjectB/IceAuraGround"
{
    Properties
    {
        _InnerColor ("Inner Color", Color) = (0.55, 0.88, 1.0, 0.08)
        _MidColor   ("Mid Color", Color)   = (0.38, 0.80, 1.0, 0.24)
        _RimColor   ("Rim Color", Color)   = (0.76, 0.96, 1.0, 0.90)
        _PulseColor ("Pulse Color", Color) = (0.92, 0.99, 1.0, 0.95)
        _Pulse      ("Tick Pulse", Range(0.0, 1.0)) = 0.0
        _RotationSpeed ("Rotation Speed", Float) = 0.45
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex IceAuraVert
            #pragma fragment IceAuraFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _InnerColor;
                half4 _MidColor;
                half4 _RimColor;
                half4 _PulseColor;
                half _RotationSpeed;
            #if !defined(UNITY_INSTANCING_ENABLED)
                half _Pulse;
            #endif
            CBUFFER_END

            #if defined(UNITY_INSTANCING_ENABLED)
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half, _Pulse)
            UNITY_INSTANCING_BUFFER_END(Props)
            #define ICE_PULSE UNITY_ACCESS_INSTANCED_PROP(Props, _Pulse)
            #else
            #define ICE_PULSE _Pulse
            #endif

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half   fogFactor  : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings IceAuraVert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.uv = input.uv;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 IceAuraFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 p = input.uv * 2.0 - 1.0;
                float r = length(p);
                if (r > 1.0)
                {
                    discard;
                }

                float a = atan2(p.y, p.x);
                float t = _Time.y;

                // Smooth radial mist: very clear around hero's feet, richer near the outer edge
                half radialT = (half)smoothstep(0.18, 0.90, r);
                half4 fill = lerp(_InnerColor, _MidColor, radialT);

                // Counter-rotating 6-fold and 12-fold frost crystalline veins
                float rotA = cos(a * 6.0 + t * (float)_RotationSpeed);
                float rotB = cos(a * 12.0 - t * (float)_RotationSpeed * 1.4);
                float ringWave = sin(r * 14.0 - t * 1.8) * 0.5 + 0.5;
                half frostPattern = (half)(saturate(rotA * 0.35 + rotB * 0.25 + 0.45) * ringWave);
                half patternMask = (half)(smoothstep(0.24, 0.72, r) * (1.0 - smoothstep(0.92, 1.0, r)));
                fill.rgb = lerp(fill.rgb, _RimColor.rgb, frostPattern * patternMask * 0.32h);
                fill.a += frostPattern * patternMask * 0.08h;

                // Crisp faceted crystalline perimeter rim
                float shimmer = 0.5 + 0.5 * cos(a * 12.0 + t * 1.1);
                float rimInner = 0.88 - shimmer * 0.015;
                half rimMask = (half)(smoothstep(rimInner, 0.94, r) * (1.0 - smoothstep(0.965, 1.0, r)));
                half outerSoft = (half)(1.0 - smoothstep(0.95, 1.0, r));

                half pulse = saturate(ICE_PULSE);
                half3 col = lerp(fill.rgb, _RimColor.rgb * (1.0h + pulse * 0.35h), rimMask);
                half alpha = lerp(fill.a * outerSoft, _RimColor.a, rimMask);

                // Expanding tick pulse ring
                if (pulse > 0.001h)
                {
                    float progress = 1.0 - (float)pulse;
                    float waveR = lerp(0.18, 0.97, progress);
                    float waveBand = 1.0 - smoothstep(0.0, 0.09, abs(r - waveR));
                    half waveAlpha = (half)waveBand * pulse * _PulseColor.a * outerSoft;
                    col = lerp(col, _PulseColor.rgb, saturate(waveAlpha));
                    alpha = max(alpha, waveAlpha);
                }

                col = MixFog(col, input.fogFactor);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
