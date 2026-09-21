Shader "UnityCraft/Water Surface"
{
    Properties
    {
        [NoScaleOffset] _TerrainTextures ("Terrain Textures", 2DArray) = "" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _WaveAmplitude ("Wave Amplitude", Range(0, 0.25)) = 0.1
        _WaveFrequency ("Wave Frequency", Range(0.1, 5)) = 1.2
        _WaveSpeed ("Wave Speed", Range(0, 5)) = 1.75
        _ReflectionStrength ("Reflection Strength", Range(0, 1)) = 0.3
        _ReflectionRoughness ("Reflection Roughness", Range(0, 1)) = 0.2
        _FresnelPower ("Reflection Fresnel Power", Range(0.1, 8)) = 3
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" }

        Pass
        {
            Name "WaterForward"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/ImageBasedLighting.hlsl"
            #include "../Resources/VoxelLighting.hlsl"

            TEXTURE2D_ARRAY(_TerrainTextures);
            SAMPLER(sampler_TerrainTextures);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _WaveAmplitude;
                float _WaveFrequency;
                float _WaveSpeed;
                float _ReflectionStrength;
                float _ReflectionRoughness;
                float _FresnelPower;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 textureData : TEXCOORD1;
                half4 light : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float4 textureData : TEXCOORD3;
                half2 light : TEXCOORD4;
            };

            half3 DecodeWaterEnvironment(half4 encodedColor, half4 decodeInstructions)
            {
                half alpha = max(decodeInstructions.w * (encodedColor.a - 1.0h) + 1.0h, 0.0h);
                return decodeInstructions.x * pow(alpha, decodeInstructions.y) * encodedColor.rgb;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionOS = input.positionOS.xyz;
                float3 positionWS = TransformObjectToWorld(positionOS);
                float isSurface = step(0.5, input.normalOS.y);
                float isUpperSideVertex = (1.0 - step(0.5, abs(input.normalOS.y))) * step(0.001, input.uv.y);
                float isMovingVertex = max(isSurface, isUpperSideVertex);
                float phase = dot(positionWS.xz, float2(1.0, 1.0)) * _WaveFrequency + _Time.y * _WaveSpeed;
                positionOS.y += sin(phase) * _WaveAmplitude * isMovingVertex;

                output.positionWS = TransformObjectToWorld(positionOS);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.textureData = input.textureData;
                output.light = input.light.rg;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float animationFrame = fmod(floor(_Time.y * input.textureData.y), input.textureData.z);
                half4 water = SAMPLE_TEXTURE2D_ARRAY(_TerrainTextures, sampler_TerrainTextures,
                    input.uv, input.textureData.x + animationFrame) * _Tint;

                float isSurface = step(0.5, input.normalWS.y);
                float3 viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 reflectionDirectionWS = reflect(-viewDirectionWS, normalize(input.normalWS));
                half reflectionMip = PerceptualRoughnessToMipmapLevel(_ReflectionRoughness);
                half3 reflection = DecodeWaterEnvironment(
                    SAMPLE_TEXTURECUBE_LOD(unity_SpecCube0, samplerunity_SpecCube0, reflectionDirectionWS, reflectionMip),
                    unity_SpecCube0_HDR);
                float fresnel = pow(1.0 - saturate(dot(normalize(input.normalWS), viewDirectionWS)), _FresnelPower);
                float reflectionAmount = fresnel * _ReflectionStrength * isSurface;
                water.rgb *= VoxelIllumination(input.positionWS, normalize(input.normalWS), input.light, input.positionCS.xy / _ScaledScreenParams.xy);
                // The baked environment must not glow through a cave roof or throughout the night.
                reflection *= input.light.x * _VoxelSkyStrength;
                reflectionAmount *= input.light.x;
                water.rgb = lerp(water.rgb, reflection, reflectionAmount);
                return water;
            }
            ENDHLSL
        }
    }
}
