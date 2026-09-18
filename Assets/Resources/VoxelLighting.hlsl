#ifndef UNITYCRAFT_VOXEL_LIGHTING
#define UNITYCRAFT_VOXEL_LIGHTING
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
float _VoxelSkyStrength;
float _VoxelDarkness;
half4 _VoxelSkyColor;

half3 VoxelIllumination(float3 positionWS, half3 normalWS, half2 levels, float2 screenUV)
{
    Light sun = GetMainLight(TransformWorldToShadowCoord(positionWS));
    half sky = saturate(levels.x);
    half local = saturate(levels.y);
    half face = .72h + .28h * saturate(normalWS.y * .5h + .5h);
    // The readability floor has directional face contrast, but no fictitious headlamp.
    half3 ambient = max(_VoxelDarkness, .035h) * face;
    ambient += _VoxelSkyColor.rgb * (_VoxelSkyStrength * sky);
    ambient += local * local * .9h;
    half direct = saturate(dot(normalWS, sun.direction)) * sun.shadowAttenuation;
    // The horizon guard also protects against any below-world directional light configuration.
    direct *= step(.0001h, sun.direction.y) * sky;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(screenUV);
        ambient *= ao.indirectAmbientOcclusion;
        direct *= ao.directAmbientOcclusion;
    #endif
    return ambient + sun.color * direct;
}
#endif
