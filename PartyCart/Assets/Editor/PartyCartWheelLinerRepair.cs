using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Extracts fixed wheel housings from the fused source scans, keeping source assets untouched.</summary>
public static class PartyCartWheelLinerRepair
{
    private const string SourceFolder = "Assets/_PartyCart/Models/PotHeadRacer/";
    private const string OutputFolder = SourceFolder + "SeparatedWheels";
    private const float TireRadius = 0.235f;
    private static readonly string[] Names = { "FrontLeft", "FrontRight", "RearLeft", "RearRight" };
    private static readonly string[] Paths = { "FrontLeftSteer/FrontLeftSpin", "FrontRightSteer/FrontRightSpin", "RearLeftSpin", "RearRightSpin" };

    public static void BuildParts(string wheelName, out Mesh rotating, out Mesh stationary)
    {
        var source = AssetDatabase.LoadAssetAtPath<Mesh>(SourceFolder + wheelName + ".asset");
        if (source == null || !source.isReadable) throw new InvalidOperationException("Readable source wheel required.");
        var vertices = source.vertices;
        var indices = source.triangles;
        var rimFaces = new List<int>();
        var linerFaces = new List<int>();
        float inside = wheelName.EndsWith("Left", StringComparison.Ordinal) ? 1f : -1f;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3 center = (vertices[indices[i]] + vertices[indices[i + 1]] + vertices[indices[i + 2]]) / 3f;
            float radius = new Vector2(center.y, center.z).magnitude;
            // The original scan joins the arch and its mounting scraps to the tire surface.
            bool housing = center.x * inside > 0.105f || (center.y > -0.025f && radius > 0.241f);
            bool rim = !housing && radius < 0.179f;
            var destination = housing ? linerFaces : rim ? rimFaces : null;
            if (destination == null) continue;
            destination.Add(indices[i]); destination.Add(indices[i + 1]); destination.Add(indices[i + 2]);
        }
        if (rimFaces.Count == 0 || linerFaces.Count == 0) throw new InvalidOperationException("Wheel split produced an empty part.");
        rotating = Extract(source, rimFaces, wheelName + " Tire And Rim");
        AddCompleteTire(rotating);
        stationary = Extract(source, linerFaces, wheelName + " Fixed Fender Liner");
    }

    private static Mesh Extract(Mesh source, List<int> faces, string name)
    {
        var sourceVertices = source.vertices;
        var sourceNormals = source.normals;
        var sourceUV = source.uv;
        var sourceTangents = source.tangents;
        var remap = new Dictionary<int, int>();
        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var uv = new List<Vector2>();
        var tangents = new List<Vector4>();
        var triangles = new List<int>(faces.Count);
        foreach (int index in faces)
        {
            if (!remap.TryGetValue(index, out int mapped))
            {
                mapped = vertices.Count;
                remap.Add(index, mapped);
                vertices.Add(sourceVertices[index]);
                if (sourceNormals.Length == sourceVertices.Length) normals.Add(sourceNormals[index]);
                if (sourceUV.Length == sourceVertices.Length) uv.Add(sourceUV[index]);
                if (sourceTangents.Length == sourceVertices.Length) tangents.Add(sourceTangents[index]);
            }
            triangles.Add(mapped);
        }
        var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
        mesh.SetVertices(vertices);
        if (normals.Count == vertices.Count) mesh.SetNormals(normals);
        if (uv.Count == vertices.Count) mesh.SetUVs(0, uv);
        if (tangents.Count == vertices.Count) mesh.SetTangents(tangents);
        mesh.SetTriangles(triangles, 0);
        if (normals.Count == 0) mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static void AddCompleteTire(Mesh mesh)
    {
        // A fused scan has no complete hidden tire under its fender. Rebuild the rubber
        // as a closed rotationally symmetric ring, preserving the original textured rim.
        var vertices = new List<Vector3>(mesh.vertices);
        var normals = new List<Vector3>(mesh.normals);
        var uv = new List<Vector2>(mesh.uv);
        var tangents = new List<Vector4>(mesh.tangents);
        while (uv.Count < vertices.Count) uv.Add(Vector2.zero);
        while (tangents.Count < vertices.Count) tangents.Add(new Vector4(0, 0, 1, 1));
        int offset = vertices.Count;
        Vector2[] profile = {
            new Vector2(-0.087f,0.170f), new Vector2(-0.095f,0.187f),
            new Vector2(-0.088f,0.213f), new Vector2(-0.068f,0.229f),
            new Vector2(-0.040f,TireRadius), new Vector2(0.040f,TireRadius),
            new Vector2(0.068f,0.229f), new Vector2(0.088f,0.213f),
            new Vector2(0.095f,0.187f), new Vector2(0.087f,0.170f),
            new Vector2(0.068f,0.163f), new Vector2(-0.068f,0.163f)
        };
        const int segments = 96;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            var radial = new Vector3(0, Mathf.Cos(angle), Mathf.Sin(angle));
            for (int j = 0; j < profile.Length; j++)
            {
                vertices.Add(Vector3.right * profile[j].x + radial * profile[j].y);
                Vector2 tangent = profile[(j + 1) % profile.Length] - profile[(j + profile.Length - 1) % profile.Length];
                Vector2 normal = new Vector2(-tangent.y, tangent.x).normalized;
                normals.Add(Vector3.right * normal.x + radial * normal.y);
                uv.Add(new Vector2(i / (float)segments, j / (float)profile.Length));
                tangents.Add(new Vector4(0, -Mathf.Sin(angle), Mathf.Cos(angle), 1));
            }
        }
        var rubberFaces = new List<int>();
        for (int i = 0; i < segments; i++)
            for (int j = 0; j < profile.Length; j++)
            {
                int a = offset + i * profile.Length + j;
                int b = offset + ((i + 1) % segments) * profile.Length + j;
                int c = offset + ((i + 1) % segments) * profile.Length + (j + 1) % profile.Length;
                int d = offset + i * profile.Length + (j + 1) % profile.Length;
                rubberFaces.Add(a); rubberFaces.Add(b); rubberFaces.Add(c);
                rubberFaces.Add(a); rubberFaces.Add(c); rubberFaces.Add(d);
            }
        int[] rim = mesh.triangles;
        mesh.Clear();
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTangents(tangents);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(rim, 0); mesh.SetTriangles(rubberFaces, 1);
        mesh.RecalculateBounds();
    }

    [MenuItem("Party Cart/Repair Fixed Wheel Fender Liners")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/SampleScene.unity" || scene.isDirty)
            throw new InvalidOperationException("Open saved SampleScene in Edit Mode before repairing the wheel assets.");
        if (!AssetDatabase.IsValidFolder(OutputFolder)) AssetDatabase.CreateFolder(SourceFolder.TrimEnd('/'), "SeparatedWheels");
        var tireMeshes = new Mesh[Names.Length];
        var fixedMeshes = new Mesh[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            BuildParts(Names[i], out var tire, out var liner);
            tireMeshes[i] = SaveMesh(tire, OutputFolder + "/" + Names[i] + "_Rotating.asset");
            fixedMeshes[i] = SaveMesh(liner, OutputFolder + "/" + Names[i] + "_FixedLiner.asset");
        }
        const string materialPath = OutputFolder + "/TireRubber.mat";
        var rubber = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (rubber == null)
        {
            rubber = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "Party Cart Tire Rubber" };
            rubber.SetColor("_BaseColor", new Color(0.055f, 0.060f, 0.068f));
            rubber.SetFloat("_Smoothness", 0.12f);
            AssetDatabase.CreateAsset(rubber, materialPath);
        }
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Separate fixed fenders from rotating wheels");
        var road = GameObject.Find("Track").GetComponent<Collider>();
        foreach (var racer in UnityEngine.Object.FindObjectsByType<RaceProgressTracker>())
        {
            var root = racer.transform;
            var visual = root.Find("PotHeadVisual");
            var car = racer.GetComponent<CarController>();
            WheelCollider[] wheels = car != null ? new[] { car.frontLeft, car.frontRight, car.rearLeft, car.rearRight } : null;
            for (int i = 0; i < Names.Length; i++)
            {
                var spin = visual.Find(Paths[i]);
                var filter = spin.GetComponentInChildren<MeshFilter>();
                var renderer = filter.GetComponent<MeshRenderer>();
                Material originalMaterial = renderer.sharedMaterials[0];
                Vector3 axle = filter.transform.TransformPoint(Vector3.zero);
                Quaternion meshRotation = filter.transform.rotation;
                Transform fixedPart = visual.Find(Names[i] + "FenderLiner");
                if (fixedPart == null)
                {
                    var obj = new GameObject(Names[i] + "FenderLiner");
                    Undo.RegisterCreatedObjectUndo(obj, "Create fixed wheel housing");
                    obj.transform.SetParent(visual, false);
                    obj.AddComponent<MeshFilter>(); obj.AddComponent<MeshRenderer>();
                    fixedPart = obj.transform;
                }
                Undo.RecordObject(fixedPart, "Position fixed wheel housing");
                fixedPart.SetPositionAndRotation(axle, meshRotation);
                fixedPart.localScale = Vector3.one;
                var fixedFilter = fixedPart.GetComponent<MeshFilter>();
                var fixedRenderer = fixedPart.GetComponent<MeshRenderer>();
                Undo.RecordObject(fixedFilter, "Assign fixed wheel housing mesh");
                Undo.RecordObject(fixedRenderer, "Assign wheel housing material");
                fixedFilter.sharedMesh = fixedMeshes[i]; fixedRenderer.sharedMaterial = originalMaterial;
                var pivot = i < 2 ? spin.parent : spin;
                Undo.RecordObject(pivot, "Center axle on rotating tire only");
                Undo.RecordObject(filter.transform, "Reset tire mesh offset");
                Undo.RecordObject(filter, "Assign rotating tire and rim");
                Undo.RecordObject(renderer, "Assign tire materials");
                pivot.position += axle - spin.position;
                filter.transform.localPosition = Vector3.zero;
                filter.sharedMesh = tireMeshes[i];
                renderer.sharedMaterials = new[] { originalMaterial, rubber };
                if (wheels != null)
                {
                    Undo.RecordObject(wheels[i], "Match tire contact radius");
                    Undo.RecordObject(wheels[i].transform, "Match tire axle");
                    wheels[i].radius = TireRadius * Mathf.Abs(visual.lossyScale.y);
                    wheels[i].transform.position = spin.position + root.up * (wheels[i].suspensionDistance * 0.5f);
                }
            }
            if (racer.playerLapManager != null)
            {
                var animator = racer.GetComponent<PotHeadWheelAnimator>();
                Undo.RecordObject(animator, "Match rolling tire radius");
                var serialized = new SerializedObject(animator);
                serialized.FindProperty("wheelRadius").floatValue = TireRadius * Mathf.Abs(visual.lossyScale.y);
                serialized.ApplyModifiedProperties();
                // The arcade chassis rests on a box rather than WheelColliders.
                float min = float.PositiveInfinity, max = float.NegativeInfinity;
                foreach (var filter in visual.GetComponentsInChildren<MeshFilter>())
                {
                    if (filter.name.EndsWith("FenderLiner", StringComparison.Ordinal)) continue;
                    Bounds bounds = filter.sharedMesh.bounds;
                    for (int k = 0; k < 8; k++)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                        float y = root.InverseTransformPoint(filter.transform.TransformPoint(corner)).y;
                        min = Mathf.Min(min, y); max = Mathf.Max(max, y);
                    }
                }
                var box = racer.GetComponent<BoxCollider>();
                Undo.RecordObject(box, "Match chassis grounding to the tires");
                Vector3 center = box.center, size = box.size;
                center.y = (min + max) * 0.5f; size.y = max - min;
                box.center = center; box.size = size;
            }
            float lowest = float.PositiveInfinity;
            foreach (string path in Paths)
                lowest = Mathf.Min(lowest, visual.Find(path).GetComponentInChildren<Renderer>().bounds.min.y);
            if (road.Raycast(new Ray(root.position + Vector3.up * 30f, Vector3.down), out var hit, 60f))
            {
                Undo.RecordObject(root, "Ground repaired tires on starting grid");
                root.position += Vector3.up * (hit.point.y + 0.045f - lowest);
            }
        }
        Undo.CollapseUndoOperations(group);
        Physics.SyncTransforms();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save wheel repair.");
        AssetDatabase.SaveAssets();
        Debug.Log("WHEEL_LINER_REPAIR: Source meshes preserved; fixed fenders and closed rotating tires saved for every racer.");
    }

    private static Mesh SaveMesh(Mesh generated, string path)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing == null) { AssetDatabase.CreateAsset(generated, path); return generated; }
        EditorUtility.CopySerialized(generated, existing);
        UnityEngine.Object.DestroyImmediate(generated);
        EditorUtility.SetDirty(existing);
        return existing;
    }
}
