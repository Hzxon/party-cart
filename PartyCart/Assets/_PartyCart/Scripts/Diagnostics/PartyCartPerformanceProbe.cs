#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Opt-in standalone measurement. Completely inactive without -pc-benchmark output.json.</summary>
public sealed class PartyCartPerformanceProbe : MonoBehaviour
{
    [Serializable]
    private sealed class Distribution
    {
        public int samples;
        public double mean, median, p95, p99, maximum;
    }

    [Serializable]
    private sealed class Report
    {
        public string unityVersion, gpu, cpu, scene, quality, mode, error;
        public int width, height, targetFps, frames, seed, racers;
        public double durationSeconds, averageFps, overBudgetPercent, travelledMetres;
        public bool developmentBuild;
        public Distribution frameMs, cpuMainMs, cpuRenderMs, gpuMs, triangles, gcBytes;
    }

    private readonly List<double> frameTimes = new List<double>(16384);
    private readonly List<double>[] counters = new List<double>[5];
    private ProfilerRecorder[] recorders;
    private string output;
    private string mode;
    private int seconds;
    private int cap;
    private bool measuring;
    private double began;
    private double travelled;
    private Vector3 previousPosition;
    private FungsiGerak driver;
    private Rigidbody body;
    private WaypointManager route;
    private int nextWaypoint;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Application.isEditor) return;
        var args = Environment.GetCommandLineArgs();
        string path = Argument(args, "-pc-benchmark", null);
        if (string.IsNullOrEmpty(path)) return;
        var host = new GameObject("Opt-in Performance Probe");
        DontDestroyOnLoad(host);
        var probe = host.AddComponent<PartyCartPerformanceProbe>();
        probe.output = Path.GetFullPath(path);
        probe.mode = Argument(args, "-pc-benchmark-mode", "baseline");
        probe.seconds = int.TryParse(Argument(args, "-pc-benchmark-seconds", "45"), out int duration) ? Mathf.Clamp(duration, 10, 180) : 45;
        probe.cap = int.TryParse(Argument(args, "-pc-benchmark-fps", "-1"), out int fps) ? fps : -1;
        probe.StartCoroutine(probe.Run());
    }

    private static string Argument(string[] args, string name, string fallback)
    {
        for (int i = 0; i + 1 < args.Length; i++)
            if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return fallback;
    }

    private IEnumerator Run()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = cap;
        Screen.SetResolution(1920, 932, FullScreenMode.Windowed);
        UnityEngine.Random.InitState(20261019);
        yield return SceneManager.LoadSceneAsync("SampleScene");
        double deadline = Time.realtimeSinceStartupAsDouble + 45;
        while ((!RaceCountdownController.RaceStarted || FindAnyObjectByType<FungsiGerak>() == null) && Time.realtimeSinceStartupAsDouble < deadline)
            yield return null;
        driver = FindAnyObjectByType<FungsiGerak>();
        route = WaypointManager.Instance;
        if (!driver || route == null || route.waypoints.Count < 2 || !RaceCountdownController.RaceStarted)
        {
            WriteReport("Race did not become ready before timeout.");
            Application.Quit(2);
            yield break;
        }
        body = driver.GetComponent<Rigidbody>();
        driver.enabled = false;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < route.waypoints.Count; i++)
        {
            float distance = (route.GetWaypoint(i).transform.position - body.position).sqrMagnitude;
            if (distance < nearest) { nearest = distance; nextWaypoint = (i + 1) % route.waypoints.Count; }
        }
        // Isolated A/B option only. Does not edit terrain assets or the normal game configuration.
        if (mode == "vegetation-off") foreach (var terrain in Terrain.activeTerrains) terrain.drawTreesAndFoliage = false;

        yield return new WaitForSecondsRealtime(10);
        string[] names = { "CPU Main Thread Frame Time", "CPU Render Thread Frame Time", "GPU Frame Time", "Triangles Count", "GC Allocated In Frame" };
        recorders = new ProfilerRecorder[5];
        for (int i = 0; i < counters.Length; i++) counters[i] = new List<double>(16384);
        for (int i = 0; i < recorders.Length; i++)
            recorders[i] = ProfilerRecorder.StartNew(i == 4 ? ProfilerCategory.Memory : ProfilerCategory.Render, names[i], 1);
        previousPosition = body.position;
        began = Time.realtimeSinceStartupAsDouble;
        measuring = true;
        yield return new WaitForSecondsRealtime(seconds);
        measuring = false;
        WriteReport(null);
        DisposeRecorders();
        Application.Quit(0);
    }

    private void FixedUpdate()
    {
        if (!driver || !body || route == null) return;
        var cruise = driver.GetComponent<RaceFinishCruise>();
        if (cruise != null && cruise.IsCruising) return;
        Vector3 from = route.GetWaypoint(nextWaypoint - 1).transform.position;
        Vector3 to = route.GetWaypoint(nextWaypoint).transform.position;
        for (int i = 0; i < 8 && Vector3.Dot(body.position - to, to - from) > 0; i++)
        {
            nextWaypoint = (nextWaypoint + 1) % route.waypoints.Count;
            from = route.GetWaypoint(nextWaypoint - 1).transform.position;
            to = route.GetWaypoint(nextWaypoint).transform.position;
        }
        Vector3 segment = to - from;
        float fraction = Mathf.Clamp01(Vector3.Dot(body.position - from, segment) / Mathf.Max(.001f, segment.sqrMagnitude));
        Vector3 target = from + fraction * segment;
        float ahead = 12f;
        for (int i = 0, index = nextWaypoint; i < 24; i++, index++)
        {
            Vector3 end = route.GetWaypoint(index).transform.position;
            float length = Vector3.Distance(target, end);
            if (length >= ahead) { target = Vector3.MoveTowards(target, end, ahead); break; }
            ahead -= length;
            target = end;
        }
        Vector3 local = driver.transform.InverseTransformPoint(target);
        float curvature = 2f * local.x / Mathf.Max(1f, local.x * local.x + local.z * local.z);
        float corner = Mathf.Clamp01(Mathf.Abs(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg) / 60f);
        float speed = Mathf.Min(driver.speed, Mathf.Lerp(24f, 9f, corner));
        float steer = Mathf.Clamp(curvature * Mathf.Max(3f, body.linearVelocity.magnitude) * Mathf.Rad2Deg / Mathf.Max(1f, driver.turnSpeed), -1f, 1f);
        driver.DriveAutopilot(speed, steer);
    }

    private void Update()
    {
        if (!measuring || recorders == null) return;
        frameTimes.Add(Time.unscaledDeltaTime * 1000d);
        for (int i = 0; i < recorders.Length; i++)
        {
            if (!recorders[i].Valid) continue;
            long value = recorders[i].LastValue;
            // D3D12 may have no completed GPU timestamp on a frame; zero is missing data, not a fast frame.
            if (i == 2 && value <= 0) continue;
            counters[i].Add(i < 3 ? value / 1000000d : value);
        }
        travelled += Vector3.Distance(previousPosition, body.position);
        previousPosition = body.position;
    }

    private void WriteReport(string error)
    {
        var report = new Report
        {
            unityVersion = Application.unityVersion, gpu = SystemInfo.graphicsDeviceName, cpu = SystemInfo.processorType,
            scene = SceneManager.GetActiveScene().name, quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
            width = Screen.width, height = Screen.height, mode = mode, error = error, targetFps = cap,
            seed = 20261019, developmentBuild = Debug.isDebugBuild, frames = frameTimes.Count,
            durationSeconds = began > 0 ? Time.realtimeSinceStartupAsDouble - began : 0,
            travelledMetres = travelled, racers = FindObjectsByType<RaceProgressTracker>().Length,
            frameMs = Summarize(frameTimes)
        };
        report.averageFps = report.frameMs.mean > 0 ? 1000 / report.frameMs.mean : 0;
        int slow = 0;
        foreach (double value in frameTimes) if (value > 1000d / 60d) slow++;
        report.overBudgetPercent = frameTimes.Count > 0 ? 100d * slow / frameTimes.Count : 0;
        report.cpuMainMs = Summarize(counters[0]); report.cpuRenderMs = Summarize(counters[1]);
        report.gpuMs = Summarize(counters[2]); report.triangles = Summarize(counters[3]); report.gcBytes = Summarize(counters[4]);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        File.WriteAllText(output, JsonUtility.ToJson(report, true));
        using (var writer = new StreamWriter(Path.ChangeExtension(output, ".frames.csv")))
        {
            writer.WriteLine("frame,frame_ms");
            for (int i = 0; i < frameTimes.Count; i++) writer.WriteLine(i + "," + frameTimes[i].ToString("F6", CultureInfo.InvariantCulture));
        }
        Debug.Log("PARTY_CART_BENCHMARK " + output);
    }

    private static Distribution Summarize(List<double> source)
    {
        var result = new Distribution();
        if (source == null || source.Count == 0) return result;
        double[] sorted = source.ToArray();
        Array.Sort(sorted);
        double sum = 0;
        foreach (double value in sorted) sum += value;
        result.samples = sorted.Length; result.mean = sum / sorted.Length;
        result.median = sorted[sorted.Length / 2]; result.p95 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * .95))];
        result.p99 = sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * .99))]; result.maximum = sorted[sorted.Length - 1];
        return result;
    }

    private void DisposeRecorders()
    {
        if (recorders == null) return;
        foreach (var recorder in recorders) recorder.Dispose();
        recorders = null;
    }
    private void OnDestroy() => DisposeRecorders();
}
#endif
