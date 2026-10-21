using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>Bounded Play Mode feature smoke test. No builds, profiler recording, or scene saves.</summary>
public static class PartyCartGameFeatureChecks
{
    private const string Request = "Temp/PartyCartGameFeatureChecks.request";
    private const string ResultPath = "Temp/PartyCartGameFeatureChecks.result.json";
    private const string Armed = "PartyCart.FeatureChecks.Armed";
    private const string PrefsKey = "PartyCart.Options.v1";
    [Serializable] private sealed class Report { public string error; public List<string> checks = new List<string>(); }
    private static Report report;
    private static PartyCartGameOverlay overlay;
    private static UIDocument document;
    private static LapManager laps;
    private static Rigidbody body;
    private static RaceProgressTracker tracker;
    private static PartyCartPlayerRecovery recovery;
    private static double deadline, next, wrongStarted;
    private static double pausedTime;
    private static Vector3 pausedPosition;
    private static float oldMaster;
    private static int stage;
    private static bool previousBackground;
    private static GameObject driftFixture, driftGround;
    private static FungsiGerak driftDriver;
    private static Rigidbody driftBody;
    private static MethodInfo driveMethod;
    private static float driftFixedTime;
    private static int driftTicks;
    private static Vector3 driftOrigin;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.playModeStateChanged -= PlayChanged;
        EditorApplication.playModeStateChanged += PlayChanged;
        if (File.Exists(Request)) EditorApplication.delayCall += StartRequested;
    }

    private static void StartRequested()
    {
        if (!File.Exists(Request)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating) { EditorApplication.delayCall += StartRequested; return; }
        File.Delete(Request);
        Start();
    }

    [MenuItem("Party Cart/Diagnostics/Check Pause Options and Recovery")]
    public static void Start()
    {
        if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath("."))).AvailableFreeSpace < 12L * 1024 * 1024 * 1024)
        {
            File.WriteAllText(ResultPath, JsonUtility.ToJson(new Report { error = "Feature test skipped: less than 12GB free. No Play Mode started." }, true));
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode || SceneManager.GetActiveScene().isDirty)
        {
            File.WriteAllText(ResultPath, JsonUtility.ToJson(new Report { error = "Open a saved scene outside Play Mode; user session was not changed." }, true));
            return;
        }
        SessionState.SetBool(Armed, true);
        SessionState.SetBool(Armed + ".HadPrefs", PlayerPrefs.HasKey(PrefsKey));
        SessionState.SetString(Armed + ".Prefs", PlayerPrefs.GetString(PrefsKey, ""));
        EditorApplication.EnterPlaymode();
    }

    private static void PlayChanged(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Armed, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            report = new Report(); stage = 0;
            deadline = EditorApplication.timeSinceStartup + 75;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            EditorApplication.update += Tick;
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            if (report != null && stage != 99) { report.error = "Feature check interrupted; no scene changes saved."; WriteResult(); }
            RestorePreferences();
            EditorApplication.update -= Tick;
            SessionState.SetBool(Armed, false);
        }
    }

    private static void Tick()
    {
        try
        {
            if (new DriveInfo(Path.GetPathRoot(Path.GetFullPath("."))).AvailableFreeSpace < 8L * 1024 * 1024 * 1024)
                throw new InvalidOperationException("Feature test stopped to protect remaining disk space.");
            if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Feature checks exceeded the 75 second limit.");
            if (EditorApplication.timeSinceStartup < next) return;
            switch (stage)
            {
                case 0:
                    overlay = PartyCartGameOverlay.Instance;
                    laps = UnityEngine.Object.FindAnyObjectByType<LapManager>();
                    if (laps == null) { SceneManager.LoadScene("SampleScene"); Wait(.3); return; }
                    if (overlay == null || !RaceCountdownController.RaceStarted || laps.PlayerCar == null) return;
                    overlay.Close();
                    body = laps.PlayerCar;
                    tracker = body.GetComponent<RaceProgressTracker>();
                    recovery = body.GetComponent<PartyCartPlayerRecovery>();
                    document = overlay.GetComponent<UIDocument>();
                    Check("Overlay UXML and recovery component bound to the actual player", document != null && document.rootVisualElement.Q("pause-page") != null && recovery != null);
                    PartyCartDriftChecks.Run(Check);
                    Check("Drift HUD and bounded rear-wheel effects are attached", document.rootVisualElement.Q("drift-hud") != null && body.GetComponent<PartyCartDriftEffects>() != null);
                    var spark = Resources.Load<Material>("PartyCart/DriftSpark");
                    Check("Drift particles use a supported explicitly referenced shader", spark != null && spark.shader != null && spark.shader.isSupported);
                    stage = -2; Wait(1); break;
                case -2:
                    overlay.Close();
                    ScreenCapture.CaptureScreenshot("Temp/PartyCartCameraCheck.png");
                    stage = -1; Wait(.3); break;
                case -1:
                    overlay.OpenPause();
                    Check("Pause freezes scaled time and audio", PartyCartGameOverlay.IsGamePaused && Time.timeScale == 0 && AudioListener.pause);
                    pausedTime = Time.timeAsDouble; pausedPosition = body.position;
                    stage = 1; Wait(.4); break;
                case 1:
                    Check("Player body and race timer remain frozen while paused", Math.Abs(Time.timeAsDouble - pausedTime) < .001 && Vector3.Distance(body.position, pausedPosition) < .001f);
                    ScreenCapture.CaptureScreenshot("Temp/PartyCartPauseCheck.png");
                    stage = 2; Wait(.3); break;
                case 2:
                    Invoke("OpenSettings", true);
                    oldMaster = PartyCartGameSettings.Current.master;
                    document.rootVisualElement.Q<Slider>("master-volume").value = 37;
                    Invoke("CancelSettings");
                    Check("Cancel discards unsaved options and returns to paused race", Math.Abs(PartyCartGameSettings.Current.master - oldMaster) < .001 && PartyCartGameOverlay.IsGamePaused);
                    Invoke("OpenSettings", true);
                    document.rootVisualElement.Q<Slider>("master-volume").value = 37;
                    document.rootVisualElement.Q<Slider>("music-volume").value = 44;
                    document.rootVisualElement.Q<Slider>("effects-volume").value = 55;
                    ScreenCapture.CaptureScreenshot("Temp/PartyCartOptionsCheck.png");
                    stage = 3; Wait(.3); break;
                case 3:
                    Invoke("SaveSettings");
                    Check("Options save volume and preserve pause state", Math.Abs(AudioListener.volume - .37f) < .001 && PartyCartGameOverlay.IsGamePaused && PlayerPrefs.HasKey(PrefsKey));
                    Check("Music and effects have independent saved levels", Math.Abs(PartyCartGameSettings.Current.music - .44f) < .001 && Math.Abs(PartyCartGameSettings.Current.effects - .55f) < .001);
                    var testAudio = new GameObject("Temporary Audio Bus Check");
                    var source = testAudio.AddComponent<AudioSource>(); source.volume = .5f;
                    var bus = testAudio.AddComponent<PartyCartAudioBus>();
                    Check("Effects slider controls actual source volume", Math.Abs(source.volume - .275f) < .001);
                    typeof(PartyCartAudioBus).GetField("channel", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(bus, PartyCartAudioBus.Channel.Music);
                    typeof(PartyCartAudioBus).GetMethod("Apply", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(bus, null);
                    Check("Music slider controls its independent audio bus", Math.Abs(source.volume - .22f) < .001);
                    UnityEngine.Object.Destroy(testAudio);
                    Invoke("OpenSettings", true); Invoke("SelectTab", false);
                    Check("Display tab has working quality and resolution choices", document.rootVisualElement.Q<DropdownField>("graphics-quality").choices.Count == 3 && document.rootVisualElement.Q<DropdownField>("screen-resolution").choices.Count > 0);
                    Invoke("CancelSettings");
                    bool confirmed = false;
                    Invoke("Ask", "TEST", "No scene changes", (Action)(() => confirmed = true));
                    Invoke("CancelConfirm");
                    Check("Cancelling restart/menu confirmation does not execute the action", !confirmed && PartyCartGameOverlay.IsGamePaused);
                    overlay.Close(); stage = 4; Wait(.4); break;
                case 4:
                    Check("Resume restores simulation time and audio", !PartyCartGameOverlay.IsGamePaused && Time.timeScale > 0 && !AudioListener.pause && Time.timeAsDouble > pausedTime);
                    int waypoint = tracker.currentWaypointIndex, lap = tracker.lapCount, checkpoint = laps.NextCheckpoint;
                    int resets = recovery.ResetCount;
                    body.position += Vector3.up * 8;
                    body.transform.position = body.position;
                    typeof(PartyCartPlayerRecovery).GetMethod("TryReset", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(recovery, new object[] { WaypointManager.Instance, tracker.GetProgress() });
                    Check("Recovery finds a clear grounded position and resets velocity", recovery.ResetCount == resets + 1 && body.linearVelocity.sqrMagnitude < .001 && body.angularVelocity.sqrMagnitude < .001);
                    Check("Reset does not award a waypoint, lap, or checkpoint", tracker.currentWaypointIndex == waypoint && tracker.lapCount == lap && laps.NextCheckpoint == checkpoint && !tracker.FinishRecorded);
                    Check("Camera has the lower close chase pose and a forward road aim", Camera.main != null && Camera.main.GetComponent<CameraFollow>().TryGetFollowPose(out var cameraPosition, out _) && cameraPosition.y - body.position.y < 2.9f);
                    wrongStarted = EditorApplication.timeSinceStartup;
                    stage = 5; Wait(.1); break;
                case 5:
                    // Exercise wrong-way detection against the real route without relying on keyboard injection.
                    PartyCartPlayerRecovery.TrackPose(WaypointManager.Instance, tracker.GetProgress(), out _, out var forward);
                    body.rotation = Quaternion.LookRotation(-forward, Vector3.up);
                    body.linearVelocity = -forward * 5;
                    if (EditorApplication.timeSinceStartup - wrongStarted < 1.2) return;
                    Check("Sustained reverse travel triggers the wrong-way warning", recovery.WrongWay);
                    ScreenCapture.CaptureScreenshot("Temp/PartyCartWrongWayCheck.png");
                    stage = 6; Wait(.3); break;
                case 6:
                    body.GetComponent<FungsiGerak>().enabled = false;
                    // The normal guard must reject reset when the driver is disabled (e.g. finish cruise).
                    int before = recovery.ResetCount;
                    recovery.GetType().GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(recovery, null);
                    Check("Recovery is inactive when the driver is disabled or under finish control", recovery.ResetCount == before);
                    body.GetComponent<FungsiGerak>().enabled = true;
                    body.linearVelocity = Vector3.zero;
                    StartDriftFixture();
                    stage = 8; Wait(.05); break;
                case 8:
                    // A temporary grounded kart, not the user car, exercises the native Rigidbody path.
                    if (Time.fixedTime == driftFixedTime) return;
                    driftFixedTime = Time.fixedTime;
                    driveMethod.Invoke(driftDriver, new object[] { 1f, .5f, .02f, true, true });
                    if (++driftTicks < 65) return;
                    Check("Native grounded Rigidbody drift reaches charged state", driftDriver.Drift.Ready);
                    Check("Drift physically turns and moves the kart", Vector3.Distance(driftBody.position, driftOrigin) > 5 && Vector3.Angle(driftBody.rotation * Vector3.forward, Vector3.forward) > 20);
                    driveMethod.Invoke(driftDriver, new object[] { 1f, 0f, .02f, true, false });
                    Check("Native drift release starts boost", driftDriver.Drift.IsBoosting);
                    driftTicks = 0; stage = 9; break;
                case 9:
                    if (Time.fixedTime == driftFixedTime) return;
                    driftFixedTime = Time.fixedTime;
                    driveMethod.Invoke(driftDriver, new object[] { 1f, 0f, .02f, true, false });
                    if (++driftTicks < 25) return;
                    Check("Mini boost physically exceeds the normal maximum speed", Vector3.Dot(driftBody.linearVelocity, driftBody.rotation * Vector3.forward) > driftDriver.speed + .5f);
                    driftTicks = 0; stage = 10; break;
                case 10:
                    if (Time.fixedTime == driftFixedTime) return;
                    driftFixedTime = Time.fixedTime;
                    driveMethod.Invoke(driftDriver, new object[] { 1f, 0f, .02f, true, false });
                    if (++driftTicks < 65) return;
                    Check("Boost expires and native speed settles back to normal", !driftDriver.Drift.IsBoosting && Mathf.Abs(Vector3.Dot(driftBody.linearVelocity, driftBody.rotation * Vector3.forward) - driftDriver.speed) < .6f);
                    CleanupDriftFixture();
                    SceneManager.LoadScene("MainMenu"); stage = 7; Wait(.6); break;
                case 7:
                    overlay.OpenSettingsFromMenu();
                    Check("The same Options panel opens in MainMenu without pausing a race", overlay.IsOpen && !PartyCartGameOverlay.IsGamePaused && Time.timeScale > 0);
                    Finish(); break;
            }
        }
        catch (Exception exception) { report.error = exception.ToString(); Finish(); }
    }

    private static void Wait(double seconds) { next = EditorApplication.timeSinceStartup + seconds; }
    private static void StartDriftFixture()
    {
        driftOrigin = new Vector3(10000, 10.9f, 10000);
        driftGround = new GameObject("Temporary Drift Test Ground");
        driftGround.transform.position = new Vector3(10000, 10, 10000);
        driftGround.AddComponent<BoxCollider>().size = new Vector3(200, .5f, 200);
        driftFixture = new GameObject("Temporary Drift Physics Check");
        driftFixture.transform.position = driftOrigin;
        driftFixture.AddComponent<BoxCollider>().size = new Vector3(1, 1, 2);
        driftBody = driftFixture.AddComponent<Rigidbody>();
        driftDriver = driftFixture.AddComponent<FungsiGerak>();
        driftDriver.speed = 16; driftDriver.acceleration = 10;
        driftDriver.enabled = false; // Only the bounded test supplies input; no player keyboard reads.
        driftBody.linearVelocity = Vector3.forward * 12;
        driveMethod = typeof(FungsiGerak).GetMethod("Drive", BindingFlags.Instance | BindingFlags.NonPublic);
        driftDriver.Drift.Step(1, 0, false, 12, true, true, .02f);
        Physics.SyncTransforms(); driftTicks = 0; driftFixedTime = Time.fixedTime;
    }
    private static void CleanupDriftFixture()
    {
        if (driftFixture != null) UnityEngine.Object.Destroy(driftFixture);
        if (driftGround != null) UnityEngine.Object.Destroy(driftGround);
    }
    private static void Invoke(string method, params object[] args) => typeof(PartyCartGameOverlay).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(overlay, args);
    private static void Check(string label, bool condition)
    {
        if (!condition) throw new InvalidOperationException(label);
        report.checks.Add(label);
    }
    private static void Finish()
    {
        stage = 99;
        CleanupDriftFixture();
        overlay?.Close();
        Application.runInBackground = previousBackground;
        RestorePreferences();
        WriteResult();
        EditorApplication.update -= Tick;
        EditorApplication.ExitPlaymode();
    }
    private static void WriteResult()
    {
        File.WriteAllText(ResultPath, JsonUtility.ToJson(report, true));
        Debug.Log("GAME_FEATURE_CHECKS " + report.checks.Count + " checks. " + report.error);
    }
    private static void RestorePreferences()
    {
        if (SessionState.GetBool(Armed + ".HadPrefs", false)) PlayerPrefs.SetString(PrefsKey, SessionState.GetString(Armed + ".Prefs", ""));
        else PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }
}
