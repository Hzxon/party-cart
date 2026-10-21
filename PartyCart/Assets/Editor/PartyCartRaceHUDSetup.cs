using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class PartyCartRaceHUDSetup
{
    [MenuItem("Party Cart/Setup Timer And Minimap")]
    public static void Apply()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/SampleScene.unity" || scene.isDirty)
            throw new InvalidOperationException("Open the saved SampleScene outside Play Mode before setting up the race HUD.");
        var lap = UnityEngine.Object.FindAnyObjectByType<LapManager>();
        var player = UnityEngine.Object.FindAnyObjectByType<FungsiGerak>();
        var route = UnityEngine.Object.FindAnyObjectByType<WaypointManager>();
        var ai = UnityEngine.Object.FindObjectsByType<AIController>().OrderBy(a => a.name).ToArray();
        var lapText = UnityEngine.Object.FindAnyObjectByType<LapHUDController>().GetComponentInChildren<TMP_Text>();
        if (lap == null || player == null || route == null || ai.Length != 3)
            throw new InvalidOperationException("The existing lap manager, player, route and three AI racers are required.");

        Undo.SetCurrentGroupName("Add race timer and minimap");
        int group = Undo.GetCurrentGroup();
        var old = GameObject.Find("Race Info HUD");
        if (old != null) Undo.DestroyObjectImmediate(old);
        Undo.RecordObject(lap, "Set race to one lap");
        var settings = new SerializedObject(lap);
        settings.FindProperty("totalLaps").intValue = 1;
        settings.ApplyModifiedPropertiesWithoutUndo();
        foreach (var tracker in UnityEngine.Object.FindObjectsByType<RaceProgressTracker>())
        {
            Undo.RecordObject(tracker, "Set racer to one lap");
            tracker.totalLaps = 1;
            EditorUtility.SetDirty(tracker);
        }

        const string folder = "Assets/_PartyCart/Materials/UI";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/_PartyCart/Materials", "UI");
        var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "/RaceHUDText.mat");
        if (material == null)
        {
            material = new Material(lapText.fontSharedMaterial) { name = "Race HUD Text Outline" };
            AssetDatabase.CreateAsset(material, folder + "/RaceHUDText.mat");
        }
        material.EnableKeyword("OUTLINE_ON");
        material.SetColor("_FaceColor", Color.white);
        material.SetColor("_OutlineColor", Color.black);
        material.SetFloat("_OutlineWidth", 0.28f);
        material.SetFloat("_OutlineSoftness", 0f);
        EditorUtility.SetDirty(material);

        var root = new GameObject("Race Info HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 21;
        canvas.additionalShaderChannels = AdditionalCanvasShaderChannels.TexCoord1;
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var timerRect = Rect("Race Timer", root.transform, new Vector2(430f, 140f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-34f, -18f));
        var timerText = Label(timerRect, "<size=44%>TIME</size>\n00:00.00", 68f, TextAlignmentOptions.TopRight, lapText.font, material);
        timerRect.gameObject.AddComponent<RaceTimerHUDController>().Configure(lap, timerText);

        var mapRect = Rect("Minimap", root.transform, new Vector2(392f, 350f), new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 28f));
        var mapGraphic = mapRect.gameObject.AddComponent<RaceMinimapTrackGraphic>();
        mapGraphic.raycastTarget = false;

        Color[] colors = { new Color(1f, 0.27f, 0.4f), new Color(0.35f, 0.85f, 1f), new Color(0.5f, 1f, 0.4f), new Color(1f, 0.8f, 0.3f) };
        string[] names = { "Player Head", "AI 01 Head", "AI 02 Head", "AI 03 Head" };
        var markers = new RaceMinimapPortraitGraphic[4];
        for (int i = 0; i < markers.Length; i++)
        {
            var marker = Rect(names[i], mapRect, Vector2.one * (i == 0 ? 34f : 28f), Vector2.one * 0.5f, Vector2.one * 0.5f, Vector2.zero);
            markers[i] = marker.gameObject.AddComponent<RaceMinimapPortraitGraphic>();
            markers[i].Configure(colors[i], i == 0);
        }
        mapRect.gameObject.AddComponent<RaceMinimapHUD>().Configure(route, player.transform, ai.Select(a => a.transform).ToArray(), mapGraphic, markers);

        Undo.RegisterCreatedObjectUndo(root, "Add race timer and minimap");
        Undo.CollapseUndoOperations(group);
        EditorUtility.SetDirty(lap);
        EditorSceneManager.MarkSceneDirty(scene);
        AssetDatabase.SaveAssets();
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save the race HUD.");
        Debug.Log("RACE_HUD: Saved one lap, top-right timer and bottom-right route minimap with four character heads.");
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 size, Vector2 anchor, Vector2 pivot, Vector2 position)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        var rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot; rect.sizeDelta = size; rect.anchoredPosition = position;
        return rect;
    }

    private static TextMeshProUGUI Label(RectTransform rect, string value, float size, TextAlignmentOptions alignment, TMP_FontAsset font, Material material)
    {
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.fontSharedMaterial = material;
        text.text = value; text.fontSize = size; text.alignment = alignment;
        text.color = Color.white; text.richText = true; text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap; text.overflowMode = TextOverflowModes.Overflow;
        text.extraPadding = true;
        return text;
    }
}
