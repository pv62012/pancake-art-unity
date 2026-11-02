using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class BuildAndroidNow
{
    [MenuItem("Tools/Build Realme Test APK")]
    public static void Build()
    {
        const string outputPath = "Build/RealmeTest.apk";
        Directory.CreateDirectory("Build");

        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Main.unity" },
            locationPathName = outputPath,
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        Debug.Log($"Android build finished: {report.summary.result}; output: {outputPath}");
        if (report.summary.result != BuildResult.Succeeded)
            Debug.LogError("Android build failed. See the preceding Console messages for details.");
    }
}
