using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

/// <summary>Produces an isolated diagnostic Player without changing the normal game's save location.</summary>
public static class VoxelLightingValidationBuild
{
    public static void Build()
    {
        string output = Environment.GetEnvironmentVariable("UNITYCRAFT_VALIDATION_BUILD") ?? "/private/tmp/UnityCraftLightingValidation.app";
        string previous = PlayerSettings.productName;
        try
        {
            PlayerSettings.productName = "UnityCraft Lighting Validation";
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/WorldSelection.unity", "Assets/Scenes/SampleScene.unity" },
                locationPathName = output,
                target = BuildTarget.StandaloneOSX,
                options = BuildOptions.Development
            });
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("Lighting validation build failed.");
        }
        finally { PlayerSettings.productName = previous; AssetDatabase.SaveAssets(); }
    }
}
