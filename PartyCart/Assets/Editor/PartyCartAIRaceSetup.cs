using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Splines;

// Repeatable setup using the existing track, player model and AI components.
public static class PartyCartAIRaceSetup
{
    [MenuItem("Party Cart/Setup Three AI Racers")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/SampleScene.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved SampleScene outside Play Mode before setting up AI.");
        var player = UnityEngine.Object.FindAnyObjectByType<FungsiGerak>();
        var track = GameObject.Find("Track").GetComponent<SplineContainer>();
        var lapManager = UnityEngine.Object.FindAnyObjectByType<LapManager>();
        var finish = UnityEngine.Object.FindObjectsByType<Checkpoint>(FindObjectsSortMode.None).First(c => c.checkpointIndex == 0);
        if (player == null || track == null || lapManager == null || !track.Spline.Closed)
            throw new InvalidOperationException("The player, closed track spline and LapManager are required.");
        var previous = GameObject.Find("AI Racing");
        if (previous != null) Undo.DestroyObjectImmediate(previous);
        var root = new GameObject("AI Racing");
        Undo.RegisterCreatedObjectUndo(root, "Setup AI Racing");
        root.AddComponent<RaceManager>();
        var waypointRoot = new GameObject("Waypoints");
        waypointRoot.transform.SetParent(root.transform, false);
        var manager = waypointRoot.AddComponent<WaypointManager>();
        var route = BuildRoute(track, finish.transform.position, player.transform.forward);
        for (int i = 0; i < route.Count; i++)
        {
            var node = new GameObject("WP " + i.ToString("D3"));
            node.transform.SetParent(waypointRoot.transform, false);
            node.transform.position = route[i];
            node.AddComponent<Waypoint>().radius = 5f;
        }
        manager.BuildWaypointList();
        var opponents = new GameObject("Opponents");
        opponents.transform.SetParent(root.transform, false);
        var playerProgress = player.GetComponent<RaceProgressTracker>();
        if (playerProgress == null) playerProgress = Undo.AddComponent<RaceProgressTracker>(player.gameObject);
        playerProgress.playerLapManager = lapManager;
        playerProgress.totalLaps = lapManager.TotalLaps;
        playerProgress.waypointReachDistance = 8f;
        Vector3 roadAtPlayer = NearestPoint(track, player.transform.position);
        float[] lanes = { -3.2f, 3.2f, 0f };
        Color[] colors = { new Color(0.35f, 0.85f, 1f), new Color(0.5f, 1f, 0.4f), new Color(1f, 0.8f, 0.3f) };
        for (int i = 0; i < 3; i++)
        {
            var ai = UnityEngine.Object.Instantiate(player.gameObject, opponents.transform);
            ai.name = "AI Kart " + (i + 1).ToString("D2");
            UnityEngine.Object.DestroyImmediate(ai.GetComponent<FungsiGerak>());
            UnityEngine.Object.DestroyImmediate(ai.GetComponent<PotHeadWheelAnimator>());
            var progress = ai.GetComponent<RaceProgressTracker>();
            progress.playerLapManager = null;
            progress.currentWaypointIndex = 0;
            progress.lapCount = 0;
            progress.totalLaps = lapManager.TotalLaps;
            progress.waypointReachDistance = 5.5f;
            Vector3 grid = roadAtPlayer - player.transform.forward * (i < 2 ? 6f : 12f) + player.transform.right * lanes[i];
            grid.y = roadAtPlayer.y + 1.35f;
            ai.transform.SetPositionAndRotation(grid, player.transform.rotation);
            var body = ai.GetComponent<Rigidbody>();
            body.mass = 600f;
            body.linearDamping = 0.05f;
            body.angularDamping = 2f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var box = ai.GetComponent<BoxCollider>();
            box.size = new Vector3(1.5f, 1.1f, 2.5f);
            box.center = new Vector3(0f, -0.15f, 0f);
            var car = ai.AddComponent<CarController>();
            car.rb = body;
            var com = new GameObject("Center of Mass");
            com.transform.SetParent(ai.transform, false);
            com.transform.localPosition = new Vector3(0f, -0.55f, 0f);
            car.centerOfMass = com.transform;
            car.maxMotorTorque = 1200f;
            car.maxSteeringAngle = 36f;
            car.highSpeedSteeringAngle = 15f;
            car.brakeForce = 4000f;
            car.steeringAssist = 0.25f;
            car.antiRollForce = 6000f;
            car.downforce = 80f;
            var fl = ai.transform.Find("PotHeadVisual/FrontLeftSteer/FrontLeftSpin");
            var fr = ai.transform.Find("PotHeadVisual/FrontRightSteer/FrontRightSpin");
            var rl = ai.transform.Find("PotHeadVisual/RearLeftSpin");
            var rr = ai.transform.Find("PotHeadVisual/RearRightSpin");
            car.frontLeft = AddWheel(ai.transform, "Front Left Collider", fl);
            car.frontRight = AddWheel(ai.transform, "Front Right Collider", fr);
            car.rearLeft = AddWheel(ai.transform, "Rear Left Collider", rl);
            car.rearRight = AddWheel(ai.transform, "Rear Right Collider", rr);
            var driver = ai.AddComponent<AIController>();
            driver.baseTargetSpeed = 25f + i * 1.5f;
            driver.speedVariance = 0.3f;
            driver.lookAheadDistance = 10f;
            driver.laneOffset = 0f;
            driver.steeringSensitivity = 1.15f;
            driver.maxSteeringAtHighSpeed = 1f;
            driver.obstacleMask = ~0;
            driver.sensorDistance = 4.5f;
            driver.sensorOffset = 0.65f;
            var visuals = ai.AddComponent<WheelVisualController>();
            var fields = new SerializedObject(visuals);
            SetReference(fields, "carRigidbody", body);
            SetReference(fields, "aiCar", car);
            SetReference(fields, "frontLeftSteerPivot", fl.parent);
            SetReference(fields, "frontRightSteerPivot", fr.parent);
            SetReference(fields, "frontLeftSpinPivot", fl);
            SetReference(fields, "frontRightSpinPivot", fr);
            SetReference(fields, "rearLeftSpinPivot", rl);
            SetReference(fields, "rearRightSpinPivot", rr);
            fields.FindProperty("wheelRadius").floatValue = 0.375f;
            fields.FindProperty("spinDirection").floatValue = 1f;
            fields.ApplyModifiedPropertiesWithoutUndo();
            AssignTeamMaterial(ai, i, colors[i]);
        }
        EditorUtility.SetDirty(playerProgress);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("AI_SETUP: Saved three opponents, player progress and " + route.Count + " spline waypoints.");
    }

    private static WheelCollider AddWheel(Transform root, string name, Transform visual)
    {
        if (visual == null) throw new InvalidOperationException("Missing wheel pivot for " + name);
        var wheelObject = new GameObject(name);
        wheelObject.transform.SetParent(root, false);
        wheelObject.transform.localPosition = root.InverseTransformPoint(visual.position) + Vector3.up * 0.15f;
        var wheel = wheelObject.AddComponent<WheelCollider>();
        wheel.radius = 0.375f;
        wheel.mass = 20f;
        wheel.suspensionDistance = 0.3f;
        var spring = wheel.suspensionSpring;
        spring.spring = 30000f;
        spring.damper = 4500f;
        spring.targetPosition = 0.5f;
        wheel.suspensionSpring = spring;
        var forward = wheel.forwardFriction;
        forward.stiffness = 1.5f;
        wheel.forwardFriction = forward;
        var sideways = wheel.sidewaysFriction;
        sideways.stiffness = 2f;
        wheel.sidewaysFriction = sideways;
        return wheel;
    }

    private static void SetReference(SerializedObject fields, string key, UnityEngine.Object value)
    {
        fields.FindProperty(key).objectReferenceValue = value;
    }

    private static void AssignTeamMaterial(GameObject ai, int index, Color tint)
    {
        const string folder = "Assets/_PartyCart/Materials/AI";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_PartyCart/Materials", "AI");
        foreach (var renderer in ai.GetComponentsInChildren<Renderer>())
        {
            var materials = renderer.sharedMaterials;
            for (int m = 0; m < materials.Length; m++)
            {
                if (materials[m] == null || !materials[m].name.StartsWith("PartyCartBodyPaint")) continue;
                string path = folder + "/AI Kart " + (index + 1).ToString("D2") + ".mat";
                var material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(materials[m]);
                    AssetDatabase.CreateAsset(material, path);
                }
                material.SetColor("_BaseColor", tint);
                EditorUtility.SetDirty(material);
                materials[m] = material;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static Vector3 NearestPoint(SplineContainer track, Vector3 point)
    {
        Vector3 best = Vector3.zero;
        float distance = float.PositiveInfinity;
        for (int i = 0; i < 8192; i++)
        {
            Vector3 sample = track.EvaluatePosition(i / 8192f);
            float d = (sample - point).sqrMagnitude;
            if (d < distance) { distance = d; best = sample; }
        }
        return best;
    }

    private static List<Vector3> BuildRoute(SplineContainer track, Vector3 start, Vector3 forward)
    {
        const int samples = 8192;
        var points = new Vector3[samples];
        int first = 0;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < samples; i++)
        {
            points[i] = track.EvaluatePosition(i / (float)samples);
            float d = (points[i] - start).sqrMagnitude;
            if (d < nearest) { nearest = d; first = i; }
        }
        int direction = Vector3.Dot(points[(first + 1) % samples] - points[first], forward) >= 0 ? 1 : -1;
        var result = new List<Vector3>();
        float travelled = 0f, next = 0f;
        for (int i = 0; i < samples; i++)
        {
            Vector3 a = points[(first + direction * i + samples) % samples];
            Vector3 b = points[(first + direction * (i + 1) + samples) % samples];
            float length = Vector3.Distance(a, b);
            if (length < 0.0001f) continue;
            while (next <= travelled + length)
            {
                result.Add(Vector3.Lerp(a, b, (next - travelled) / length));
                next += 6f;
            }
            travelled += length;
        }
        if (Vector3.Distance(result[result.Count - 1], result[0]) < 3f) result.RemoveAt(result.Count - 1);
        return result;
    }
}
