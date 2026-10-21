using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>Explicit, repeatable scene installation; never overwrites a scene automatically.</summary>
public static class PartyCartFinishSetup
{
    [MenuItem("Party Cart/Setup Finish Screen")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.isDirty)
            throw new InvalidOperationException("Save the race scene outside Play Mode before installing the finish UI.");
        var manager = UnityEngine.Object.FindAnyObjectByType<RaceManager>();
        var laps = UnityEngine.Object.FindAnyObjectByType<LapManager>();
        var camera = Camera.main;
        var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/_PartyCart/UI/FinishScreen.uxml");
        var font = AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Examples & Extras/Fonts/Roboto-Bold.ttf");
        if (manager == null || laps == null || camera == null || tree == null || font == null)
            throw new InvalidOperationException("A race manager, lap manager, main camera, FinishScreen and menu font are required.");

        const string settingsPath = "Assets/Settings/PartyCartFinishPanelSettings.asset";
        var settings = AssetDatabase.LoadAssetAtPath<PanelSettings>(settingsPath);
        if (settings == null)
        {
            var menuSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Settings/PartyCartMenuPanelSettings.asset");
            settings = menuSettings != null ? UnityEngine.Object.Instantiate(menuSettings) : ScriptableObject.CreateInstance<PanelSettings>();
            settings.name = "Party Cart Finish Panel Settings";
            settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            settings.referenceResolution = new Vector2Int(1920, 1080);
            settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            settings.match = 0.5f;
            AssetDatabase.CreateAsset(settings, settingsPath);
        }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("Install Party Cart finish screen");
        var controller = UnityEngine.Object.FindAnyObjectByType<RaceFinishController>();
        GameObject root;
        if (controller == null)
        {
            root = new GameObject("Race Finish");
            Undo.RegisterCreatedObjectUndo(root, "Create finish screen");
            Undo.AddComponent<UIDocument>(root);
            Undo.AddComponent<RaceFinishCamera>(root);
            controller = Undo.AddComponent<RaceFinishController>(root);
        }
        else root = controller.gameObject;
        var document = root.GetComponent<UIDocument>();
        Undo.RecordObject(document, "Assign finish UI");
        document.panelSettings = settings;
        document.visualTreeAsset = tree;
        document.sortingOrder = 100;
        Undo.RecordObject(controller, "Bind existing race data");
        var huds = UnityEngine.Object.FindObjectsByType<Canvas>().Where(c =>
            c.GetComponentInChildren<LapHUDController>(true) != null ||
            c.GetComponentInChildren<RaceTimerHUDController>(true) != null ||
            c.GetComponentInChildren<RaceCountdownController>(true) != null).ToArray();
        controller.Configure(manager, laps, font, huds);
        var finishCamera = root.GetComponent<RaceFinishCamera>();
        Undo.RecordObject(finishCamera, "Assign race camera");
        finishCamera.Configure(camera);
        EditorUtility.SetDirty(controller);
        EditorUtility.SetDirty(document);
        EditorUtility.SetDirty(finishCamera);
        Undo.CollapseUndoOperations(group);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save finish screen bindings.");
        Debug.Log("FINISH_SETUP: Saved Kertas Panitia UI, winner camera and existing race bindings in " + scene.path);
    }
}
