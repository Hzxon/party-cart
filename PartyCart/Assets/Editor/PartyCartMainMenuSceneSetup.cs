using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>
/// One-time project setup: creates the menu scene without changing SampleScene,
/// then places MainMenu before SampleScene in Build Settings.
/// </summary>
[InitializeOnLoad]
internal static class PartyCartMainMenuSceneSetup
{
    private const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    private const string GameScenePath = "Assets/Scenes/SampleScene.unity";
    private const string PanelSettingsPath = "Assets/Settings/PartyCartMenuPanelSettings.asset";
    private const string MenuUxmlPath = "Assets/Resources/Figma/Untitled/start.uxml";
    private const string ControllerPath = "Assets/_PartyCart/Scripts/UI/PartyCartStartMenu.cs";

    static PartyCartMainMenuSceneSetup()
    {
        EditorApplication.delayCall += EnsureMenuScene;
    }

    private static void EnsureMenuScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(MenuUxmlPath);
        var controller = AssetDatabase.LoadAssetAtPath<MonoScript>(ControllerPath);
        if (visualTree == null || controller == null)
        {
            Debug.LogError("Party Cart menu setup stopped: the runtime UXML or controller asset is missing.");
            return;
        }

        var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (panelSettings == null)
        {
            panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            panelSettings.name = "Party Cart Menu Panel Settings";
            panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panelSettings.referenceResolution = new Vector2Int(2022, 1129);
            panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panelSettings.match = 0.5f;
            AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
        }

        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null)
            CreateMenuScene(visualTree, panelSettings, controller);

        PutScenesInBuildOrder();
        EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void CreateMenuScene(VisualTreeAsset visualTree, PanelSettings panelSettings, MonoScript controller)
    {
        var previousActiveScene = SceneManager.GetActiveScene();
        var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);

        var menuObject = new GameObject("Main Menu UI");
        menuObject.SetActive(false);
        var document = menuObject.AddComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.visualTreeAsset = visualTree;
        menuObject.AddComponent(controller.GetClass());
        menuObject.SetActive(true);

        EditorSceneManager.MarkSceneDirty(menuScene);
        if (!EditorSceneManager.SaveScene(menuScene, MenuScenePath))
            Debug.LogError("Party Cart menu scene could not be saved.");

        EditorSceneManager.CloseScene(menuScene, true);
        if (previousActiveScene.IsValid() && previousActiveScene.isLoaded)
            SceneManager.SetActiveScene(previousActiveScene);
    }

    private static void PutScenesInBuildOrder()
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        var sampleSceneExists = scenes.Any(scene => scene.path == GameScenePath);
        var menuIsAlreadyFirst = scenes.Count > 0
            && scenes[0].path == MenuScenePath
            && scenes[0].enabled
            && sampleSceneExists;
        if (menuIsAlreadyFirst)
            return;

        scenes.RemoveAll(scene => scene.path == MenuScenePath);
        scenes.Insert(0, new EditorBuildSettingsScene(MenuScenePath, true));

        if (!scenes.Any(scene => scene.path == GameScenePath))
            scenes.Add(new EditorBuildSettingsScene(GameScenePath, true));

        EditorBuildSettings.scenes = scenes.ToArray();
    }
}
