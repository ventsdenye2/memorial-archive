using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class RegressionPlayerBuild
{
    public static void Build()
    {
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
            locationPathName = "Builds/Regression/Memorial Archive.exe",
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.Development
        });
        if (report.summary.result != BuildResult.Succeeded)
            throw new System.Exception("Regression player build failed: " + report.summary.result);
    }
}
