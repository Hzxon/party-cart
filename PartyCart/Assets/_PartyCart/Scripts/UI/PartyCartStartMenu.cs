using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

/// <summary>Button behavior for the separate Party Cart main-menu scene.</summary>
public sealed class PartyCartStartMenu : MonoBehaviour
{
    private const string CatalogResource = "PartyCart/RaceCatalog";
    private const string SelectionResource = "PartyCart/RaceSelection";
    private UIDocument document;
    private Button playButton;
    private Button quitButton;
    private Button shopButton;
    private Button optionButton;
    private Label menuStatus;
    private VisualElement mainMenu;
    private bool loading;

    public PartyCartRaceSetupMenu SetupMenu { get; private set; }
    public bool IsLoading => loading;

    private void OnEnable()
    {
        document = GetComponent<UIDocument>();
        var root = document.rootVisualElement;
        loading = false;
        mainMenu = root.Q("main-menu");
        menuStatus = root.Q<Label>("menu-status");
        playButton = root.Q<Button>("play-button");
        if (playButton == null)
        {
            Debug.LogError("Party Cart main menu could not find the PLAY button.");
            return;
        }

        playButton.clicked += OpenRaceSelection;
        quitButton = root.Query<Button>().ToList()
            .FirstOrDefault(button => string.Equals(button.text, "QUIT", StringComparison.OrdinalIgnoreCase));
        if (quitButton != null)
            quitButton.clicked += QuitGame;
        shopButton = root.Q<Button>("shop-button");
        optionButton = root.Q<Button>("option-button");
        if (shopButton != null) shopButton.clicked += ShowShopStatus;
        if (optionButton != null) optionButton.clicked += ShowOptionStatus;
        var catalog = Resources.Load<PartyCartRaceCatalog>(CatalogResource);
        var selection = Resources.Load<VisualTreeAsset>(SelectionResource);
        if (catalog != null && selection != null)
            SetupMenu = new PartyCartRaceSetupMenu(root, selection, catalog, ReturnToMainMenu, StartGame);
    }

    private void OnDisable()
    {
        if (playButton != null)
            playButton.clicked -= OpenRaceSelection;
        if (quitButton != null)
            quitButton.clicked -= QuitGame;
        if (shopButton != null) shopButton.clicked -= ShowShopStatus;
        if (optionButton != null) optionButton.clicked -= ShowOptionStatus;
        SetupMenu?.Dispose();
        SetupMenu = null;
    }

    private void OpenRaceSelection()
    {
        if (loading) return;
        if (SetupMenu == null)
        {
            if (menuStatus != null) menuStatus.text = "Pilihan balapan belum tersedia.";
            Debug.LogError("MainMenu requires the RaceCatalog and RaceSelection Resources assets.", this);
            return;
        }
        mainMenu.style.display = DisplayStyle.None;
        SetupMenu.Show();
    }

    private void ReturnToMainMenu()
    {
        if (loading) return;
        SetupMenu.Hide();
        mainMenu.style.display = DisplayStyle.Flex;
        playButton.Focus();
    }

    private void StartGame(PartyCartMapOption map, PartyCartCharacterOption character)
    {
        if (loading || map == null || character == null) return;
        if (!Application.CanStreamedLevelBeLoaded(map.sceneName))
        {
            SetupMenu.SetStatus("Scene map ini belum tersedia di Build Settings.");
            return;
        }
        PartyCartRaceChoice.Confirm(map, character);
        loading = true;
        SetupMenu.SetLoading(true);
        Time.timeScale = 1f;
        try
        {
            if (SceneManager.LoadSceneAsync(map.sceneName) == null)
                throw new InvalidOperationException("The selected race scene could not be loaded.");
        }
        catch (Exception error)
        {
            loading = false;
            SetupMenu.SetLoading(false);
            SetupMenu.SetStatus("Map gagal dimuat. Silakan coba lagi.");
            Debug.LogException(error, this);
        }
    }

    private void ShowShopStatus() { if (menuStatus != null) menuStatus.text = "SHOP BELUM TERSEDIA."; }
    private void ShowOptionStatus() { PartyCartGameOverlay.Instance?.OpenSettingsFromMenu(); }

    private static void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
