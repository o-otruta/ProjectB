Shader "ProjectB/FrostZoneGround"
{
    Properties
    {
        _CoreColor  ("Core Color", Color)  = (0.08, 0.22, 0.44, 0.72)
        _MidColor   ("Mid Color", Color)   = (0.18, 0.44, 0.68, 0.62)
        _RimColor   ("Rim Color", Color)   = (0.76, 0.90, 0.98, 0.90)
        _CrackColor ("Crack Color", Color) = (0.56, 0.82, 0.96, 0.78)
        _Fade       ("Fade", Range(0.0, 1.0)) = 1.0
        _PixelSteps ("Pixel Steps", Float) = 22.0
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
            #pragma vertex FrostZoneVert
            #pragma fragment FrostZoneFrag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _CoreColor;
                half4 _MidColor;
                half4 _RimColor;
                half4 _CrackColor;
                float _PixelSteps;
            #if !defined(UNITY_INSTANCING_ENABLED)
                half _Fade;
            #endif
            CBUFFER_END

            #if defined(UNITY_INSTANCING_ENABLED)
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(half, _Fade)
            UNITY_INSTANCING_BUFFER_END(Props)
            #define FROST_FADE UNITY_ACCESS_INSTANCED_PROP(Props, _Fade)
            #else
            #define FROST_FADE _Fade
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

            Varyings FrostZoneVert(Attributes input)
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

            half4 FrostZoneFrag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 rawP = input.uv * 2.0 - 1.0;
                float steps = max(4.0, _PixelSteps);
                float2 quantP = floor(rawP * steps + 0.5) / steps;
                float2 p = lerp(rawP, quantP, 0.65);

                float r = length(p);
                float a = atan2(p.y, p.x);

                // Irregular jagged permafrost edge silhouette
                float edgeR = 0.88 + sin(a * 5.0 + 0.7) * 0.065 + cos(a * 9.0 - 1.3) * 0.045;
                float normR = r / max(0.25, edgeR);
                if (normR > 1.0)
                {
                    discard;
                }

                // Dark hostile glacial core -> cold permafrost body
                half coreT = (half)smoothstep(0.15, 0.78, normR);
                half4 baseCol = lerp(_CoreColor, _MidColor, coreT);

                // Radiating ice fracture lines
                float crackA = 1.0 - smoothstep(0.0, 0.14, abs(sin(a * 4.0 + r * 5.5)));
                float crackB = 1.0 - smoothstep(0.0, 0.11, abs(cos(a * 7.0 - r * 4.2 + 0.6)));
                half crackMask = (half)(saturate(max(crackA, crackB * 0.8)) * smoothstep(0.08, 0.25, normR) * (1.0 - smoothstep(0.86, 1.0, normR)));

                // Frosted pale ice-white jagged border
                half rimMask = (half)(smoothstep(0.80, 0.90, normR) * (1.0 - smoothstep(0.95, 1.0, normR)));
                half outerSoft = (half)(1.0 - smoothstep(0.93, 1.0, normR));

                // Subtle icy glint
                float glint = 0.92 + 0.08 * sin(_Time.y * 2.2 + a * 3.0 + r * 4.0);

                half3 col = lerp(baseCol.rgb, _CrackColor.rgb, crackMask * 0.75h);
                col = lerp(col, _RimColor.rgb, rimMask);
                col *= (half)glint;

                half alpha = max(baseCol.a * outerSoft, crackMask * _CrackColor.a * outerSoft);
                alpha = max(alpha, rimMask * _RimColor.a);
                alpha *= saturate(FROST_FADE);

                col = MixFog(col, input.fogFactor);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }

    Fallback Off
}
