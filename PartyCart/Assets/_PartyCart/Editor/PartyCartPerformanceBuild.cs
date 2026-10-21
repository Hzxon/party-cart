using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Local development benchmark build; never changes scene contents or the normal startup flow.</summary>
public static class PartyCartPerformanceBuild
{
    private const string Request = "Temp/PartyCartPerformanceBuild.request";
    private const string StatusPath = "Temp/PartyCartPerformanceBuild.status.json";
    [Serializable] private sealed class Status
    {
        public string phase, output, result, error, backend;
        public int errors, warnings;
        public double seconds;
        public ulong bytes;
    }

    [InitializeOnLoadMethod]
    private static void ResumeRequestedBuild()
    {
        if (File.Exists(Request)) EditorApplication.delayCall += BuildWhenReady;
    }

    private static void BuildWhenReady()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.isPlaying = false;
            EditorApplication.delayCall += BuildWhenReady;
            return;
        }
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += BuildWhenReady;
            return;
        }
        string consumed = Request + ".consumed-" + DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        File.Move(Request, consumed);
        Build();
    }

    [MenuItem("Party Cart/Diagnostics/Build Windows Performance Test")]
    public static void Build()
    {
        var status = new Status { phase = "preparing" };
        bool frameTimings = PlayerSettings.enableFrameTimingStats;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode before building.");
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                    throw new InvalidOperationException("Save unsaved scene changes before building; no scenes were overwritten.");
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Windows build support is not installed.");
            status.backend = PlayerSettings.GetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone).ToString();
            string directory = Path.GetFullPath("outputs/PerformanceCheck-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
            if (Directory.Exists(directory)) throw new InvalidOperationException("Output directory already exists; refusing to overwrite.");
            Directory.CreateDirectory(directory);
            status.output = Path.Combine(directory, "PartyCart-Performance.exe");
            status.phase = "building";
            File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
            PlayerSettings.enableFrameTimingStats = true;
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray(),
                locationPathName = status.output, target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development
            });
            status.phase = "finished"; status.result = report.summary.result.ToString();
            status.errors = report.summary.totalErrors; status.warnings = report.summary.totalWarnings;
            status.seconds = report.summary.totalTime.TotalSeconds; status.bytes = report.summary.totalSize;
            if (report.summary.result != BuildResult.Succeeded)
                status.error = string.Join("\n", report.steps.SelectMany(step => step.messages)
                    .Where(message => message.type == LogType.Error || message.type == LogType.Exception).Select(message => message.content).Take(12));
        }
        catch (Exception ex) { status.phase = "failed"; status.error = ex.ToString(); }
        finally
        {
            PlayerSettings.enableFrameTimingStats = frameTimings;
            File.WriteAllText(StatusPath, JsonUtility.ToJson(status, true));
        }
    }
}
