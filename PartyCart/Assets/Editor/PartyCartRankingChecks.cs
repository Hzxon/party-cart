using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small deterministic regression checks; isolated from the user's loaded scene.</summary>
public static class PartyCartRankingChecks
{
    private const string RequestPath = "Temp/PartyCartRankingChecks.request";
    private const string ResultPath = "Temp/PartyCartRankingChecks.result.json";
    private static readonly FieldInfo CrossedStart = typeof(RaceProgressTracker)
        .GetField("crossedStart", BindingFlags.Instance | BindingFlags.NonPublic);

    [Serializable] private sealed class Result
    {
        public string error;
        public int passed;
        public List<string> checks = new List<string>();
    }

    [InitializeOnLoadMethod]
    private static void RequestedChecks()
    {
        if (!File.Exists(RequestPath)) return;
        EditorApplication.delayCall += RunRequested;
    }

    private static void RunRequested()
    {
        if (!File.Exists(RequestPath)) return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += RunRequested;
            return;
        }
        File.Delete(RequestPath); // Consume only this explicit one-shot test request, once ready.
        Run();
    }

    [MenuItem("Party Cart/Diagnostics/Check Race Ranking")]
    public static void Run()
    {
        var result = new Result();
        var previousWaypoints = WaypointManager.Instance;
        var previousRace = RaceManager.Instance;
        var startedField = typeof(RaceCountdownController).GetField("<RaceStarted>k__BackingField", BindingFlags.Static | BindingFlags.NonPublic);
        bool previousStarted = RaceCountdownController.RaceStarted;
        Scene preview = default;
        try
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running isolated ranking checks.");
            startedField.SetValue(null, true);
            preview = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("Isolated Ranking Checks");
            SceneManager.MoveGameObjectToScene(root, preview);
            root.hideFlags = HideFlags.HideAndDontSave;
            var routeObject = new GameObject("Test Route");
            routeObject.transform.SetParent(root.transform);
            var route = routeObject.AddComponent<WaypointManager>();
            var positions = new List<Vector3>();
            for (int z = 0; z <= 30; z += 6) positions.Add(new Vector3(0, 0, z));
            for (int x = 6; x <= 30; x += 6) positions.Add(new Vector3(x, 0, 30));
            for (int z = 24; z >= -30; z -= 6) positions.Add(new Vector3(30, 0, z));
            for (int x = 24; x >= 0; x -= 6) positions.Add(new Vector3(x, 0, -30));
            for (int z = -24; z <= -6; z += 6) positions.Add(new Vector3(0, 0, z));
            foreach (var point in positions)
            {
                var node = new GameObject("Test WP");
                node.transform.SetParent(routeObject.transform);
                node.transform.localPosition = point;
                node.AddComponent<Waypoint>();
            }
            route.BuildWaypointList();
            WaypointManager.Instance = route;
            var race = root.AddComponent<RaceManager>();
            RaceManager.Instance = race;
            var player = Racer(root.transform, "Player", 8f);
            var ai = Racer(root.transform, "Opponent", 5.5f);
            race.Register(player); // Registering the player first must not award a false lead on ties.
            race.Register(ai);

            Place(player, new Vector3(0, 0, 10), 4);
            Place(ai, new Vector3(3.2f, 0, 11), 3);
            Check(result, "8m player radius cannot outrank an AI physically 1m ahead", race.GetRanking()[0] == ai);
            Check(result, "Nearby segment projection returns the actual 10/6 waypoint progress", Mathf.Abs(player.GetProgress() - 10f / 6f) < .001f);

            Place(player, new Vector3(0, 0, 11.5f), 4);
            Check(result, "Player takes the lead only after physically passing the opponent", race.GetRanking()[0] == player);
            Place(player, new Vector3(0, 0, 10), 4);
            Check(result, "Rank falls again when the player moves back behind", race.GetRanking()[0] == ai);

            Place(player, new Vector3(0, 0, 5.9f), 3);
            Place(ai, new Vector3(3.2f, 0, 6.1f), 2);
            Check(result, "Progress remains ordered across a waypoint boundary", race.GetRanking()[0] == ai);

            Place(player, new Vector3(0, 0, -5), 0, false);
            Place(ai, new Vector3(8, 0, -4), 0, false);
            Check(result, "Grid rank ignores lateral lane distance before the start", race.GetRanking()[0] == ai);

            Place(player, new Vector3(0, 0, -1), 0, false);
            typeof(RaceProgressTracker).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            Check(result, "Entering the player's start radius does not count as crossing the line",
                player.currentWaypointIndex == 0 && !(bool)CrossedStart.GetValue(player));
            player.transform.localPosition = new Vector3(0, 0, 1);
            typeof(RaceProgressTracker).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(player, null);
            Check(result, "Actually crossing the start line initializes route progress", (bool)CrossedStart.GetValue(player));

            Place(player, new Vector3(3, 0, 30), 8);
            Place(ai, new Vector3(4, 0, 32), 7);
            Check(result, "Rank follows the route through a corner, not world Z or lane offset", race.GetRanking()[0] == ai);

            int count = route.waypoints.Count;
            Place(player, new Vector3(0, 0, -1), 0);
            float beforeLine = player.GetProgress();
            Place(player, new Vector3(0, 0, 1), 1);
            player.lapCount = 1;
            float afterLine = player.GetProgress();
            Check(result, "Lap wrap advances continuously without jumping backward or an extra lap",
                beforeLine < count && afterLine > count && afterLine - beforeLine < 1f);
            Place(ai, new Vector3(0, 0, -1), 0);
            Check(result, "A genuine completed lap remains ahead of a racer on the previous lap", race.GetRanking()[0] == player);

            Place(player, new Vector3(0, 0, 2), 1);
            player.lapCount = player.totalLaps;
            player.RecordFinish();
            Place(ai, new Vector3(0, 0, 20), 5);
            Check(result, "Recorded finish stays locked even when another car is physically further ahead",
                player.FinishRecorded && race.GetRanking()[0] == player);
        }
        catch (Exception exception) { result.error = exception.ToString(); }
        finally
        {
            if (preview.IsValid()) EditorSceneManager.ClosePreviewScene(preview);
            WaypointManager.Instance = previousWaypoints;
            RaceManager.Instance = previousRace;
            startedField.SetValue(null, previousStarted);
            File.WriteAllText(ResultPath, JsonUtility.ToJson(result, true));
        }
        if (!string.IsNullOrEmpty(result.error)) Debug.LogError("RANKING_CHECKS " + result.error);
        else Debug.Log("RANKING_CHECKS Passed " + result.passed + " checks. No user scene changes.");
    }

    private static RaceProgressTracker Racer(Transform parent, string name, float radius)
    {
        var car = new GameObject(name);
        car.transform.SetParent(parent);
        var tracker = car.AddComponent<RaceProgressTracker>();
        tracker.waypointReachDistance = radius;
        tracker.totalLaps = 3;
        return tracker;
    }

    private static void Place(RaceProgressTracker racer, Vector3 position, int next, bool started = true)
    {
        racer.transform.localPosition = position;
        racer.currentWaypointIndex = next;
        racer.lapCount = 0;
        CrossedStart.SetValue(racer, started);
    }

    private static void Check(Result result, string name, bool condition)
    {
        if (!condition) throw new InvalidOperationException(name);
        result.checks.Add(name);
        result.passed++;
    }
}
