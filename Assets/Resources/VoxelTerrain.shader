Shader "UnityCraft/Voxel Terrain"
{
    Properties
    {
        [NoScaleOffset] _TerrainTextures ("Terrain Textures", 2DArray) = "white" {}
        _Cutoff ("Alpha cutoff", Range(0,1)) = .5
        [HideInInspector] _SrcBlend ("Source blend", Float) = 1
        [HideInInspector] _DstBlend ("Destination blend", Float) = 0
        [HideInInspector] _ZWrite ("Depth write", Float) = 1
        [HideInInspector] _CastVoxelShadows ("Cast shadows", Float) = 1
        [HideInInspector] _VoxelDynamicLight ("Dynamic light override", Vector) = (0,0,0,0)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        HLSLINCLUDE
        #pragma target 3.5
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D_ARRAY(_TerrainTextures);
        SAMPLER(sampler_TerrainTextures);
        CBUFFER_START(UnityPerMaterial)
            float _Cutoff;
            float _SrcBlend, _DstBlend, _ZWrite, _CastVoxelShadows;
            float4 _VoxelDynamicLight;
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
            half3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float4 textureData : TEXCOORD3;
            half2 light : TEXCOORD4;
            half fog : TEXCOORD5;
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
            output.positionCS = TransformWorldToHClip(output.positionWS);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.uv = input.uv;
            output.textureData = input.textureData;
            output.light = lerp(input.light.rg, _VoxelDynamicLight.xy, _VoxelDynamicLight.w);
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        half4 Texel(Varyings input)
        {
            float frame = fmod(floor(_Time.y * input.textureData.y), max(1, input.textureData.z));
            half4 c = SAMPLE_TEXTURE2D_ARRAY(_TerrainTextures, sampler_TerrainTextures, input.uv, input.textureData.x + frame);
            clip(c.a - _Cutoff);
            return c;
        }
        ENDHLSL
        Pass
        {
            Name "VoxelForward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include "VoxelLighting.hlsl"
            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = Texel(input);
                color.rgb *= VoxelIllumination(input.positionWS, normalize(input.normalWS), input.light, input.positionCS.xy / _ScaledScreenParams.xy);
                color.rgb = MixFog(color.rgb, input.fog);
                return color;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            Cull Back
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection;
            Varyings ShadowVert(Attributes input)
            {
                Varyings output = Vert(input);
                output.positionCS = TransformWorldToHClip(ApplyShadowBias(output.positionWS, output.normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                    output.positionCS.z = min(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #else
                    output.positionCS.z = max(output.positionCS.z, UNITY_NEAR_CLIP_VALUE * output.positionCS.w);
                #endif
                return output;
            }
            half4 ShadowFrag(Varyings input) : SV_Target
            {
                clip(_CastVoxelShadows - .5);
                Texel(input);
                return 0;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment NormalFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            half4 NormalFrag(Varyings input) : SV_Target
            {
                Texel(input);
                float3 normal = normalize(input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormal = PackNormalOctQuadEncode(normal);
                    return half4(PackFloat2To888(saturate(octNormal * .5 + .5)), 0);
                #else
                    return half4(normal, 0);
                #endif
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings input) : SV_Target { Texel(input); return input.positionCS.z; }
            ENDHLSL
        }
    }
}
