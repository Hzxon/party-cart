using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

/// <summary>Shared persistent Pause/Options UI, with no changes to the saved menu or race scenes.</summary>
[DefaultExecutionOrder(150)]
public sealed class PartyCartGameOverlay : MonoBehaviour
{
    public static PartyCartGameOverlay Instance { get; private set; }
    public static bool IsGamePaused => Instance != null && Instance.paused;
    public bool IsOpen => page != Page.Closed;
    private enum Page { Closed, Pause, Settings, Confirm, DisplayConfirm }
    private Page page;
    private UIDocument document;
    private PanelSettings panel;
    private VisualElement modal, pausePage, settingsPage, confirmPage, warning, resetPrompt, resetFill, audioPage, displayPage;
    private Label resetCaption, status, confirmTitle, confirmMessage;
    private Button confirmYes, confirmNo, audioTab, displayTab;
    private Slider master, music, effects;
    private DropdownField quality, resolution;
    private Toggle fullscreen;
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private LapManager laps;
    private FungsiGerak driver;
    private PartyCartPlayerRecovery recovery;
    private VisualElement driftHud, driftFill;
    private Label driftTitle, driftHint;
    private PartyCartGameSettings.Values beforeSettings;
    private bool settingsFromPause, paused, loading, previousAudioPause, previousCursorVisible;
    private float previousTimeScale, displayDeadline, originalMasterVolume;
    private CursorLockMode previousCursorLock;
    private Action confirmed;
    private Coroutine bindRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { Instance = null; SceneManager.sceneLoaded -= SceneLoaded; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
        if (Instance == null) new GameObject("Party Cart Pause and Options").AddComponent<PartyCartGameOverlay>();
    }

    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (Instance != null) Instance.QueueBind();
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        originalMasterVolume = AudioListener.volume;
        PartyCartGameSettings.Initialize();
        var template = FindAnyObjectByType<UIDocument>();
        var tree = Resources.Load<VisualTreeAsset>("PartyCart/GameOverlay");
        if (template == null || template.panelSettings == null || tree == null)
        {
            Debug.LogError("Party Cart overlay needs GameOverlay UXML and the existing scene PanelSettings.");
            enabled = false;
            return;
        }
        panel = Instantiate(template.panelSettings);
        panel.name = "Party Cart Overlay (Runtime)";
        panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panel.referenceResolution = new Vector2Int(1920, 1080);
        panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        panel.match = .5f;
        panel.sortingOrder = 100;
        panel.clearColor = false;
        document = gameObject.AddComponent<UIDocument>();
        document.enabled = false;
        document.panelSettings = panel;
        document.sortingOrder = 100;
        document.visualTreeAsset = tree;
        document.enabled = true;
        BindView();
        QueueBind();
    }

    private void BindView()
    {
        var root = document.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        modal = root.Q("game-modal");
        pausePage = root.Q("pause-page"); settingsPage = root.Q("settings-page"); confirmPage = root.Q("confirm-page");
        warning = root.Q("wrong-way"); resetPrompt = root.Q("reset-prompt"); resetFill = root.Q("reset-fill");
        driftHud = root.Q("drift-hud"); driftFill = root.Q("drift-fill");
        driftTitle = root.Q<Label>("drift-title"); driftHint = root.Q<Label>("drift-hint");
        resetCaption = root.Q<Label>("reset-caption"); status = root.Q<Label>("options-status");
        audioPage = root.Q("audio-options"); displayPage = root.Q("display-options");
        audioTab = root.Q<Button>("audio-tab"); displayTab = root.Q<Button>("display-tab");
        audioTab.clicked += () => SelectTab(true); displayTab.clicked += () => SelectTab(false);
        root.Q<Button>("resume").clicked += Close;
        root.Q<Button>("pause-settings").clicked += () => OpenSettings(true);
        root.Q<Button>("restart").clicked += () => Ask("ULANG BALAP?", "Progres balapan ini akan dimulai dari awal.", () => LoadScene(SceneManager.GetActiveScene().name));
        root.Q<Button>("main-menu").clicked += () => Ask("KEMBALI KE MENU?", "Balapan yang sedang berjalan tidak akan disimpan.", () => LoadScene("MainMenu"));
        root.Q<Button>("options-back").clicked += CancelSettings;
        root.Q<Button>("options-save").clicked += SaveSettings;
        confirmTitle = root.Q<Label>("confirm-title"); confirmMessage = root.Q<Label>("confirm-message");
        confirmYes = root.Q<Button>("confirm-yes"); confirmNo = root.Q<Button>("confirm-no");
        confirmYes.clicked += () => confirmed?.Invoke();
        confirmNo.clicked += CancelConfirm;
        master = root.Q<Slider>("master-volume"); music = root.Q<Slider>("music-volume"); effects = root.Q<Slider>("effects-volume");
        BindVolume(master, root.Q<Label>("master-value")); BindVolume(music, root.Q<Label>("music-value")); BindVolume(effects, root.Q<Label>("effects-value"));
        quality = root.Q<DropdownField>("graphics-quality");
        quality.choices = new List<string> { "Rendah", "Sedang", "Tinggi" };
        resolution = root.Q<DropdownField>("screen-resolution"); fullscreen = root.Q<Toggle>("fullscreen");
        Show(Page.Closed);
    }

    private static void BindVolume(Slider slider, Label value)
    {
        var track = slider.Q(className: "unity-base-slider__tracker");
        if (track != null)
        {
            var fill = new VisualElement { name = "volume-fill", pickingMode = PickingMode.Ignore };
            fill.AddToClassList("volume-fill");
            track.Add(fill);
        }
        slider.RegisterValueChangedCallback(evt => { value.text = Mathf.RoundToInt(evt.newValue) + "%"; UpdateVolumeFill(slider); });
    }

    private static void UpdateVolumeFill(Slider slider)
    {
        var fill = slider.Q("volume-fill");
        if (fill != null) fill.style.width = Length.Percent(slider.value);
    }

    private void QueueBind()
    {
        if (bindRoutine != null) StopCoroutine(bindRoutine);
        bindRoutine = StartCoroutine(BindScene());
    }

    private IEnumerator BindScene()
    {
        Close();
        loading = false;
        yield return null; // Scene Awake/Start establishes the new countdown and player references first.
        laps = FindAnyObjectByType<LapManager>();
        driver = laps != null && laps.PlayerCar != null ? laps.PlayerCar.GetComponent<FungsiGerak>() : null;
        recovery = driver != null ? driver.GetComponent<PartyCartPlayerRecovery>() : null;
        if (driver != null && recovery == null) recovery = driver.gameObject.AddComponent<PartyCartPlayerRecovery>();
        if (driver != null && driver.GetComponent<PartyCartDriftEffects>() == null)
            driver.gameObject.AddComponent<PartyCartDriftEffects>();
        PartyCartGameSettings.BindSceneAudio();
        bindRoutine = null;
    }

    private bool CanPause => !loading && laps != null && driver != null && RaceCountdownController.RaceStarted && !laps.RaceFinished;

    private void Update()
    {
        if (modal == null) return;
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            switch (page)
            {
                case Page.Pause: Close(); break;
                case Page.Settings: CancelSettings(); break;
                case Page.Confirm:
                case Page.DisplayConfirm: CancelConfirm(); break;
                case Page.Closed: if (CanPause) OpenPause(); break;
            }
        }
        if (page == Page.DisplayConfirm)
        {
            int seconds = Mathf.Max(0, Mathf.CeilToInt(displayDeadline - Time.unscaledTime));
            confirmMessage.text = "Pertahankan pengaturan layar ini? Kembali otomatis dalam " + seconds + " detik.";
            if (Time.unscaledTime >= displayDeadline) CancelConfirm();
        }
        bool racing = page == Page.Closed && CanPause;
        Visible(warning, racing && recovery != null && recovery.WrongWay);
        Visible(resetPrompt, racing && recovery != null && (recovery.NeedsHelp || recovery.HoldProgress > 0 || !string.IsNullOrEmpty(recovery.Status)));
        if (recovery != null && racing)
        {
            resetFill.style.width = Length.Percent(recovery.HoldProgress * 100f);
            resetCaption.text = string.IsNullOrEmpty(recovery.Status) ? "Kembali ke titik aman terakhir" : recovery.Status;
        }
        var drift = driver != null ? driver.Drift : null;
        bool resetting = recovery != null && (recovery.NeedsHelp || recovery.HoldProgress > 0 || !string.IsNullOrEmpty(recovery.Status));
        bool showDrift = racing && driver.enabled && !resetting && drift != null && (drift.IsDrifting || drift.IsBoosting);
        Visible(driftHud, showDrift);
        if (showDrift)
        {
            driftHud.EnableInClassList("is-ready", drift.Ready || drift.IsBoosting);
            driftTitle.text = drift.IsBoosting ? "MINI BOOST!" : drift.Ready ? "BOOST SIAP!" : "DRIFT";
            driftHint.text = drift.IsBoosting ? "+22%" : drift.Ready ? "LEPAS SPACE" : "TAHAN SPACE + BELOK";
            driftFill.style.width = Length.Percent((drift.IsBoosting ? drift.Boost01 : drift.Charge01) * 100);
        }
    }

    public void OpenPause()
    {
        if (!CanPause || page != Page.Closed) return;
        SuspendRace();
        Show(Page.Pause);
        document.rootVisualElement.Q<Button>("resume").Focus();
    }

    private void SuspendRace()
    {
        if (paused) return;
        previousTimeScale = Time.timeScale; previousAudioPause = AudioListener.pause;
        previousCursorLock = Cursor.lockState; previousCursorVisible = Cursor.visible;
        paused = true;
        recovery?.CancelHold(); driver?.ClearInput();
        Time.timeScale = 0;
        AudioListener.pause = true;
        Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
    }

    public void Close()
    {
        if (page == Page.DisplayConfirm && beforeSettings != null) PartyCartGameSettings.Apply(beforeSettings, true);
        if (paused)
        {
            Time.timeScale = previousTimeScale;
            AudioListener.pause = previousAudioPause;
            Cursor.lockState = previousCursorLock; Cursor.visible = previousCursorVisible;
            paused = false;
            driver?.ClearInput(); recovery?.CancelHold();
        }
        beforeSettings = null;
        confirmed = null;
        Show(Page.Closed);
    }

    public void OpenSettingsFromMenu()
    {
        if (page != Page.Closed || loading) return;
        OpenSettings(false);
    }

    private void OpenSettings(bool fromPause)
    {
        settingsFromPause = fromPause;
        beforeSettings = PartyCartGameSettings.Current.Copy();
        var value = beforeSettings;
        master.SetValueWithoutNotify(value.master * 100); music.SetValueWithoutNotify(value.music * 100); effects.SetValueWithoutNotify(value.effects * 100);
        var root = document.rootVisualElement;
        root.Q<Label>("master-value").text = Mathf.RoundToInt(master.value) + "%";
        root.Q<Label>("music-value").text = Mathf.RoundToInt(music.value) + "%";
        root.Q<Label>("effects-value").text = Mathf.RoundToInt(effects.value) + "%";
        UpdateVolumeFill(master); UpdateVolumeFill(music); UpdateVolumeFill(effects);
        quality.index = value.quality;
        resolutions.Clear();
        foreach (var entry in Screen.resolutions) AddResolution(entry.width, entry.height);
        AddResolution(value.width, value.height); AddResolution(Screen.width, Screen.height);
        int maxWidth = Mathf.Max(Screen.width, Display.main.systemWidth), maxHeight = Mathf.Max(Screen.height, Display.main.systemHeight);
        foreach (var candidate in new[] { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) })
            if (candidate.x <= maxWidth && candidate.y <= maxHeight) AddResolution(candidate.x, candidate.y);
        resolutions.Sort((a, b) => (a.x * a.y).CompareTo(b.x * b.y));
        resolution.choices = resolutions.Select(size => size.x + " × " + size.y).ToList();
        resolution.index = Mathf.Max(0, resolutions.IndexOf(new Vector2Int(value.width, value.height)));
        fullscreen.SetValueWithoutNotify(value.fullscreen);
        status.text = Application.isEditor ? "Resolusi dan fullscreen diterapkan pada build game." : "";
        SelectTab(true);
        Show(Page.Settings);
        audioTab.Focus();
    }

    private void AddResolution(int width, int height)
    {
        var size = new Vector2Int(width, height);
        if (width >= 640 && height >= 360 && !resolutions.Contains(size)) resolutions.Add(size);
    }

    private void SelectTab(bool audio)
    {
        Visible(audioPage, audio); Visible(displayPage, !audio);
        audioTab.EnableInClassList("is-selected", audio); displayTab.EnableInClassList("is-selected", !audio);
    }

    private void SaveSettings()
    {
        var size = resolutions[Mathf.Clamp(resolution.index, 0, resolutions.Count - 1)];
        var value = new PartyCartGameSettings.Values { master = master.value / 100, music = music.value / 100,
            effects = effects.value / 100, quality = quality.index, width = size.x, height = size.y, fullscreen = fullscreen.value };
        bool screenChanged = !Application.isEditor && (value.width != beforeSettings.width || value.height != beforeSettings.height || value.fullscreen != beforeSettings.fullscreen);
        PartyCartGameSettings.Apply(value, true);
        if (screenChanged)
        {
            displayDeadline = Time.unscaledTime + 15;
            confirmTitle.text = "LAYAR SUDAH PAS?";
            confirmYes.text = "PERTAHANKAN"; confirmNo.text = "KEMBALIKAN";
            confirmed = CommitSettings;
            Show(Page.DisplayConfirm);
        }
        else CommitSettings();
    }

    private void CommitSettings()
    {
        PartyCartGameSettings.Save(); beforeSettings = null; confirmed = null;
        if (settingsFromPause) Show(Page.Pause); else Close();
    }

    private void CancelSettings()
    {
        beforeSettings = null;
        if (settingsFromPause) Show(Page.Pause); else Close();
    }

    private void Ask(string title, string message, Action action)
    {
        confirmTitle.text = title; confirmMessage.text = message;
        confirmYes.text = "YA, LANJUT"; confirmNo.text = "BATAL";
        confirmed = action; Show(Page.Confirm); confirmNo.Focus();
    }

    private void CancelConfirm()
    {
        if (page == Page.DisplayConfirm)
        {
            PartyCartGameSettings.Apply(beforeSettings, true);
            Show(Page.Settings);
            status.text = "Pengaturan layar dikembalikan. Belum disimpan.";
        }
        else Show(Page.Pause);
        confirmed = null;
    }

    private void Show(Page value)
    {
        page = value;
        if (modal == null) return;
        Visible(modal, value != Page.Closed);
        Visible(pausePage, value == Page.Pause); Visible(settingsPage, value == Page.Settings);
        Visible(confirmPage, value == Page.Confirm || value == Page.DisplayConfirm);
    }

    private static void Visible(VisualElement element, bool visible) { if (element != null) element.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None; }

    private void LoadScene(string name)
    {
        if (loading || !Application.CanStreamedLevelBeLoaded(name)) { confirmMessage.text = "Scene belum tersedia."; return; }
        Close(); loading = true;
        Time.timeScale = 1;
        SceneManager.LoadSceneAsync(name);
    }

    private void OnApplicationFocus(bool focused) { if (!focused && page == Page.Closed && CanPause) OpenPause(); }
    private void OnDestroy()
    {
        if (Instance != this) return;
        Close();
        SceneManager.sceneLoaded -= SceneLoaded;
        PartyCartGameSettings.ReleaseRuntimeGraphics();
        AudioListener.volume = originalMasterVolume;
        if (panel != null) Destroy(panel);
        Instance = null;
    }
}
