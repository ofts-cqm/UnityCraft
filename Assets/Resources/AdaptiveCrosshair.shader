Shader "UnityCraft/Adaptive Crosshair"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Adaptive Crosshair"

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            static const float CrosshairSizePixels = 30.0;
            static const float CrosshairThicknessPixels = 4.0;
            static const float LuminanceThreshold = 0.5;

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord.xy;
                float4 worldColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);

                // Convert the requested display-pixel dimensions into the scaled render target's pixels.
                // This keeps the reticle about 30px after URP upscales a reduced render scale.
                float2 pixel = floor(input.positionCS.xy);
                float2 center = floor(_ScaledScreenParams.xy * 0.5);
                float2 distanceFromCenter = abs(pixel - center);
                float2 renderScale = _ScaledScreenParams.xy / _ScreenParams.xy;
                float2 halfSize = CrosshairSizePixels * 0.5 * renderScale;
                float2 halfThickness = CrosshairThicknessPixels * 0.5 * renderScale;
                bool horizontalArm = distanceFromCenter.x < halfSize.x && distanceFromCenter.y < halfThickness.y;
                bool verticalArm = distanceFromCenter.y < halfSize.y && distanceFromCenter.x < halfThickness.x;

                if (!horizontalArm && !verticalArm) return worldColor;

                float luminance = dot(worldColor.rgb, float3(0.2126, 0.7152, 0.0722));
                float crosshairColor = luminance < LuminanceThreshold ? 1.0 : 0.0;
                return float4(crosshairColor, crosshairColor, crosshairColor, 1.0);
            }
            ENDHLSL
        }
    }
}
