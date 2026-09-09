Shader "Hidden/UnityCraft/Inventory Sprite Bake"
{
    Properties
    {
        [NoScaleOffset] _TerrainTextures ("Terrain Textures", 2DArray) = "white" {}
        [HideInInspector] _ZWrite ("Z Write", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "InventorySpriteBake"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Back
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_TerrainTextures);
            SAMPLER(sampler_TerrainTextures);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                float4 textureData : TEXCOORD1;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                nointerpolation float slice : TEXCOORD1;
                half shade : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 lightDirection = normalize(float3(-0.45, 0.8, -0.4));

                output.positionCS = positionInputs.positionCS;
                output.uv = input.uv;
                output.slice = input.textureData.x;
                output.shade = 0.62h + 0.38h * saturate(dot(normalWS, lightDirection));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D_ARRAY(
                    _TerrainTextures,
                    sampler_TerrainTextures,
                    input.uv,
                    input.slice);
                color.rgb *= input.shade;
                return color;
            }
            ENDHLSL
        }
    }
}
