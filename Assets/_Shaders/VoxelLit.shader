Shader "ProjectB/VoxelLit"
{
    Properties
    {
        [MainTexture] _BaseMap("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor("Base Color", Color) = (1, 1, 1, 1)

        [HDR] _EmissionColor("Emission Color", Color) = (0, 0, 0, 1)
        [NoScaleOffset] _EmissionMap("Emission Map", 2D) = "white" {}

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.12

        _HitFlash("Hit Flash", Range(0.0, 1.0)) = 0.0
        [HDR] _RimColor("Rim Color", Color) = (0, 0, 0, 0)
        _RimPower("Rim Power", Range(0.5, 8.0)) = 2.6
        _HeightAO("Height AO Strength", Range(0.0, 0.8)) = 0.25
        _HeightAORange("Height AO Range", Range(0.05, 3.0)) = 0.75

        [HideInInspector] _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [HideInInspector] _MainTex("Legacy BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("Legacy Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "SimpleLit"
            "IgnoreProjector" = "True"
        }
        LOD 200

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);        SAMPLER(sampler_BaseMap);
        TEXTURE2D(_EmissionMap);    SAMPLER(sampler_EmissionMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _EmissionColor;
            half4 _Color;
            half _Smoothness;
            half _HeightAO;
            half _HeightAORange;
            half _Cutoff;
        #if !defined(UNITY_INSTANCING_ENABLED)
            half4 _BaseColor;
            half4 _RimColor;
            half _RimPower;
            half _HitFlash;
        #endif
        CBUFFER_END

        #if defined(UNITY_INSTANCING_ENABLED)
        UNITY_INSTANCING_BUFFER_START(Props)
            UNITY_DEFINE_INSTANCED_PROP(half4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(half4, _RimColor)
            UNITY_DEFINE_INSTANCED_PROP(half, _RimPower)
            UNITY_DEFINE_INSTANCED_PROP(half, _HitFlash)
        UNITY_INSTANCING_BUFFER_END(Props)
        #define VOXEL_BASE_COLOR  UNITY_ACCESS_INSTANCED_PROP(Props, _BaseColor)
        #define VOXEL_RIM_COLOR   UNITY_ACCESS_INSTANCED_PROP(Props, _RimColor)
        #define VOXEL_RIM_POWER   UNITY_ACCESS_INSTANCED_PROP(Props, _RimPower)
        #define VOXEL_HIT_FLASH   UNITY_ACCESS_INSTANCED_PROP(Props, _HitFlash)
        #else
        #define VOXEL_BASE_COLOR  _BaseColor
        #define VOXEL_RIM_COLOR   _RimColor
        #define VOXEL_RIM_POWER   _RimPower
        #define VOXEL_HIT_FLASH   _HitFlash
        #endif
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags
            {
                "LightMode" = "UniversalForward"
            }

            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex VoxelLitVertex
            #pragma fragment VoxelLitFragment

            #pragma shader_feature_local_fragment _EMISSION

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float3 positionWS   : TEXCOORD1;
                half3  normalWS     : TEXCOORD2;
                half3  vertexSH     : TEXCOORD3;
                float  positionOSY  : TEXCOORD4;
                half   fogFactor    : TEXCOORD5;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord  : TEXCOORD6;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings VoxelLitVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = posInputs.positionCS;
                output.positionWS = posInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normInputs.normalWS);
                output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                output.vertexSH = SampleSHVertex(output.normalWS);
                output.positionOSY = input.positionOS.y;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                output.shadowCoord = GetShadowCoord(posInputs);
                #endif

                return output;
            }

            half4 VoxelLitFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                half3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                half3 viewDirWS = SafeNormalize(GetWorldSpaceNormalizeViewDir(input.positionWS));

                half4 texCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 baseColor = VOXEL_BASE_COLOR;
                half3 albedo = texCol.rgb * baseColor.rgb;

                // Subtle MagicaVoxel-style ground ambient occlusion near model base
                half heightFactor = saturate(half(input.positionOSY) / max(0.05h, _HeightAORange));
                half ao = lerp(1.0h - saturate(_HeightAO), 1.0h, heightFactor);

                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                float4 shadowCoord = float4(0, 0, 0, 0);
                #endif

                Light mainLight = GetMainLight(shadowCoord);
                half mainAtten = mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                half ndotl = saturate(dot(normalWS, mainLight.direction));
                half3 directDiffuse = mainLight.color * (ndotl * mainAtten);

                // Lightweight Blinn-Phong specular highlight for crisp voxel edges
                half3 halfVec = SafeNormalize(mainLight.direction + viewDirWS);
                half specPow = lerp(8.0h, 64.0h, saturate(_Smoothness));
                half spec = pow(saturate(dot(normalWS, halfVec)), float(specPow)) * _Smoothness * 0.35h * mainAtten;
                half3 directSpec = mainLight.color * spec;

                #ifdef _ADDITIONAL_LIGHTS
                uint additionalCount = GetAdditionalLightsCount();
                for (uint i = 0u; i < additionalCount; ++i)
                {
                    Light addLight = GetAdditionalLight(i, input.positionWS);
                    half addNdotL = saturate(dot(normalWS, addLight.direction));
                    directDiffuse += addLight.color * (addNdotL * addLight.distanceAttenuation * addLight.shadowAttenuation);
                }
                #endif

                half3 ambient = SampleSHPixel(input.vertexSH, normalWS) * ao;
                half3 color = albedo * (ambient + directDiffuse) + directSpec;

                #ifdef _EMISSION
                half3 emissionTex = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb;
                color += emissionTex * _EmissionColor.rgb;
                #endif

                // Fresnel rim glow for Elites and Bosses without ground rings
                half4 rimCol = VOXEL_RIM_COLOR;
                half rim = pow(1.0h - saturate(dot(normalWS, viewDirWS)), max(0.5h, VOXEL_RIM_POWER));
                color += rimCol.rgb * (rim * rimCol.a * 1.45h);

                // True bright white hit-flash controlled per-instance via MaterialPropertyBlock
                half hitFlash = saturate(VOXEL_HIT_FLASH);
                color = lerp(color, half3(1.25h, 1.25h, 1.25h), hitFlash);

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex VoxelShadowVertex
            #pragma fragment VoxelShadowFragment

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings VoxelShadowVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 VoxelShadowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex VoxelDepthVertex
            #pragma fragment VoxelDepthFragment

            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS   : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings VoxelDepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half VoxelDepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Simple Lit"
}
