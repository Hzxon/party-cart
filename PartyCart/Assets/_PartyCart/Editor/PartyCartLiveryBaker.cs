using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Applies approved finishes without changing geometry, native LODs or wheel pivots.</summary>
public static class PartyCartLiveryBaker
{
    private const string Root = "Assets/_PartyCart/";


    [MenuItem("Party Cart/Art/Apply Approved Racer Liveries")]
    public static void Bake()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/SampleScene.unity")
            throw new InvalidOperationException("Open SampleScene before applying racer finishes.");
        Configure(Root + "Materials/PartyCartBodyPaint.mat", "79C5C1", "173F47", "CE6047", "C56548", "F3E7CE", "EAE0C9", "F5E8CB", false);
        Configure(Root + "Materials/AI/AI Kart 01.mat", "E66F60", "207D85", "E66F60", "CC6555", "C5C8C5", "174B53", "F4E8D0", true);
        Configure(Root + "Materials/AI/AI Kart 02.mat", "F2E8CE", "184B55", "E4DC55", "22545B", "F1E7CF", "DAD15B", "E4DC55", false);
        // Same Pit Crew language, reversed body/stripe palette for the third opponent.
        Configure(Root + "Materials/AI/AI Kart 03.mat", "22545B", "153B42", "D88765", "22545B", "F1E7CF", "DAD15B", "F2E8CE", false);
        AssignWheels();
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Approved racer materials applied: Weekend Club player, Clean Racer and Pit Crew opponents.");
    }

    private static void Configure(string path, string paint, string trim, string accent, string jacket, string pot, string gloves, string stripe, bool metalPot)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Party Cart/Racer Materials");
        if (!mat || !shader || ShaderUtil.ShaderHasError(shader)) throw new InvalidOperationException("Missing or invalid racer material shader.");
        Undo.RecordObject(mat, "Apply approved racer finish");
        mat.shader = shader;
        mat.shaderKeywords = Array.Empty<string>();
        mat.SetTexture("_BaseMap", null);
        mat.SetVector("_ModelOffset", Vector4.zero); mat.SetFloat("_FenderPart", 0);
        mat.SetColor("_BaseColor", Hex(paint)); mat.SetColor("_TrimColor", Hex(trim));
        mat.SetColor("_AccentColor", Hex(accent)); mat.SetColor("_JacketColor", Hex(jacket));
        mat.SetColor("_PotColor", Hex(pot)); mat.SetColor("_GloveColor", Hex(gloves)); mat.SetColor("_StripeColor", Hex(stripe));
        mat.SetFloat("_MetalPot", metalPot ? 1 : 0);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
    }

    private static void AssignWheels()
    {
        string folder = Root + "Materials/Liveries";
        Directory.CreateDirectory(folder);
        var playerRim = Solid(folder + "/WeekendClub_Rim.mat", "EFE3C9", .35f, .42f);
        var cleanRim = Solid(folder + "/CleanRacer_Rim.mat", "BCA77C", .58f, .44f);
        var pitRim = Solid(folder + "/PitCrew_Rim.mat", "EEE6CF", .30f, .42f);
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>())
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) continue;
            string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
            if (!path.Contains("PotHeadRacer/SeparatedWheels/")) continue;
            Undo.RecordObject(renderer, "Apply racer wheel finish");
            var visual = renderer.transform;
            while (visual.parent && visual.name != "PotHeadVisual") visual = visual.parent;
            bool player = visual.root.name == "Mobil";
            bool clean = visual.parent && visual.parent.name == "AI Kart 01";
            if (path.Contains("FixedLiner"))
            {
                // The separated liner also contains fragments of the original shell. Match their paint
                // in the same model coordinates instead of turning those fragments into black patches.
                var bodyMaterial = visual.Find("Body").GetComponent<MeshRenderer>().sharedMaterial;
                string linerPath = folder + "/" + (player ? "WeekendClub" : visual.parent.name.Replace(" ", "")) + "_" + renderer.name + ".mat";
                var liner = AssetDatabase.LoadAssetAtPath<Material>(linerPath);
                if (!liner) { liner = new Material(bodyMaterial); AssetDatabase.CreateAsset(liner, linerPath); }
                else liner.CopyPropertiesFromMaterial(bodyMaterial);
                liner.SetVector("_ModelOffset", renderer.transform.localPosition);
                liner.SetFloat("_FenderPart", 1);
                EditorUtility.SetDirty(liner);
                renderer.sharedMaterial = liner;
            }
            else
            {
                var mats = renderer.sharedMaterials;
                mats[0] = player ? playerRim : clean ? cleanRim : pitRim;
                renderer.sharedMaterials = mats;
            }
            EditorUtility.SetDirty(renderer);
        }
    }

    private static Material Solid(string path, string hex, float metallic, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", Hex(hex)); mat.SetFloat("_Metallic", metallic); mat.SetFloat("_Smoothness", smoothness);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    [MenuItem("Party Cart/Art/Refresh Racer Selection Previews")]
    public static void RefreshPreviews()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play Mode first.");
        Capture("Mobil", Root + "UI/Selection/Character_PotHead.jpg", false);
        Capture("Mobil", Root + "UI/Selection/Portrait_PotHead.jpg", true);
        Capture("AI Racing/Opponents/AI Kart 01", Root + "UI/Selection/Portrait_CleanRacer.jpg", true);
        Capture("AI Racing/Opponents/AI Kart 02", Root + "UI/Selection/Portrait_PitCrew.jpg", true);
        AssetDatabase.Refresh();
        foreach (var hud in UnityEngine.Object.FindObjectsByType<RaceStandingsHUD>())
        {
            var fields = new SerializedObject(hud);
            fields.FindProperty("cleanRacerPortrait").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "UI/Selection/Portrait_CleanRacer.jpg");
            fields.FindProperty("pitCrewPortrait").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "UI/Selection/Portrait_PitCrew.jpg");
            fields.ApplyModifiedProperties();
        }
    }

    private static void Capture(string racer, string path, bool portrait)
    {
        var source = GameObject.Find(racer + "/PotHeadVisual");
        if (!source) throw new InvalidOperationException("Missing preview racer: " + racer);
        var clone = UnityEngine.Object.Instantiate(source);
        clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        clone.transform.localScale = Vector3.one;
        var preview = new PreviewRenderUtility();
        Texture2D texture = null;
        try
        {
            preview.AddSingleGO(clone);
            preview.camera.transform.position = portrait ? new Vector3(.82f, .59f, .76f) : new Vector3(2.3f, 1.2f, 2.8f);
            preview.camera.transform.LookAt(portrait ? new Vector3(0, .22f, -.29f) : new Vector3(0, .02f, 0));
            preview.camera.fieldOfView = portrait ? 35 : 34;
            preview.camera.nearClipPlane = .01f; preview.camera.farClipPlane = 20;
            preview.camera.clearFlags = CameraClearFlags.Color;
            preview.camera.backgroundColor = Hex("F6F3E7");
            preview.lights[0].intensity = 1.5f;
            preview.lights[0].transform.rotation = Quaternion.Euler(35, 30, 0);
            preview.lights[1].intensity = .8f;
            preview.ambientColor = new Color(.6f, .6f, .6f);
            bool asyncCompilation = ShaderUtil.allowAsyncCompilation;
            try
            {
                ShaderUtil.allowAsyncCompilation = false;
                foreach (var renderer in clone.GetComponentsInChildren<Renderer>())
                    foreach (var material in renderer.sharedMaterials)
                        if (material != null)
                            for (int pass = 0; pass < material.passCount; pass++) ShaderUtil.CompilePass(material, pass, true);
                preview.BeginStaticPreview(new Rect(0, 0, portrait ? 512 : 2048, portrait ? 512 : 1536));
                preview.Render(true);
                texture = preview.EndStaticPreview();
            }
            finally { ShaderUtil.allowAsyncCompilation = asyncCompilation; }
            File.WriteAllBytes(path, texture.EncodeToJPG(95));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.mipmapEnabled = true; importer.maxTextureSize = portrait ? 512 : 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        finally
        {
            if (texture) UnityEngine.Object.DestroyImmediate(texture);
            preview.Cleanup();
        }
    }

    private static Color Hex(string hex) { ColorUtility.TryParseHtmlString("#" + hex, out Color color); return color; }
}
