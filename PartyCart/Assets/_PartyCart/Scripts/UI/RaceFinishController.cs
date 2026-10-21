using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;

[RequireComponent(typeof(UIDocument), typeof(RaceFinishCamera))]
public sealed class RaceFinishController : MonoBehaviour
{
    [SerializeField] private RaceManager raceManager;
    [SerializeField] private LapManager lapManager;
    [SerializeField] private Font menuFont;
    [SerializeField] private Canvas[] raceHUDs = Array.Empty<Canvas>();
    [SerializeField, Min(1f)] private float remainingRacersTimeout = 20f;
    [SerializeField] private string mainMenuScene = "MainMenu";

    private VisualElement screen;
    private VisualElement paper;
    private ScrollView rankingList;
    private Label winnerName;
    private Label playerPlace;
    private Label totalTime;
    private Label bestLap;
    private Label raceStatus;
    private Button replayButton;
    private Button menuButton;
    private RaceProgressTracker player;
    private RaceFinishCamera finishCamera;
    private readonly List<ResultRow> rows = new List<ResultRow>();
    private readonly Dictionary<RaceProgressTracker, RaceFinishCruise> cruising = new Dictionary<RaceProgressTracker, RaceFinishCruise>();
    private readonly List<Canvas> hiddenHUDs = new List<Canvas>();
    private List<RaceProgressTracker> finalRanking;
    private readonly HashSet<RaceProgressTracker> didNotFinish = new HashSet<RaceProgressTracker>();
    private float shownAt;
    private float nextRefresh;
    private bool loading;
    private CursorLockMode previousCursorLock;
    private bool previousCursorVisible;

    public bool IsVisible { get; private set; }
    public bool ResultsFinal => finalRanking != null;
    public RaceProgressTracker Winner { get; private set; }

    private sealed class ResultRow
    {
        public VisualElement Root;
        public VisualElement Paint;
        public Label Position;
        public Label Name;
        public Label Time;
    }

    public void Configure(RaceManager manager, LapManager laps, Font font, Canvas[] huds)
    {
        raceManager = manager;
        lapManager = laps;
        menuFont = font;
        raceHUDs = huds;
    }

    private void Start()
    {
        if (raceManager == null) raceManager = FindAnyObjectByType<RaceManager>();
        if (lapManager == null) lapManager = FindAnyObjectByType<LapManager>();
        if (lapManager != null && lapManager.PlayerCar != null)
            player = lapManager.PlayerCar.GetComponent<RaceProgressTracker>();
        finishCamera = GetComponent<RaceFinishCamera>();
        BindView();
    }

    private void BindView()
    {
        var root = GetComponent<UIDocument>().rootVisualElement;
        screen = root.Q("finish-screen");
        if (screen == null)
        {
            Debug.LogError("Race Finish requires the FinishScreen UXML on its UIDocument.", this);
            enabled = false;
            return;
        }
        if (menuFont != null) screen.style.unityFontDefinition = FontDefinition.FromFont(menuFont);
        screen.style.display = DisplayStyle.None;
        paper = screen.Q("results-paper");
        rankingList = screen.Q<ScrollView>("ranking-list");
        winnerName = screen.Q<Label>("winner-name");
        playerPlace = screen.Q<Label>("player-place");
        totalTime = screen.Q<Label>("total-time");
        bestLap = screen.Q<Label>("best-lap");
        raceStatus = screen.Q<Label>("race-status");
        replayButton = screen.Q<Button>("replay-button");
        menuButton = screen.Q<Button>("menu-button");
        replayButton.clicked += Replay;
        menuButton.clicked += ReturnToMenu;
    }

    private void LateUpdate()
    {
        if (screen == null || raceManager == null || lapManager == null || player == null) return;
        if (!RaceCountdownController.RaceStarted) return;
        foreach (var racer in raceManager.Racers)
            if (racer != null && racer.FinishRecorded && !cruising.ContainsKey(racer)) StartCruising(racer);

        if (!IsVisible)
        {
            if (!player.FinishRecorded) return;
            ShowResults();
        }

        float progress = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.65f);
        float ease = 1f - Mathf.Pow(1f - progress, 3f);
        screen.style.opacity = ease;
        paper.style.translate = new Translate(new Length(0f), new Length((1f - ease) * 65f), 0f);
        screen.EnableInClassList("compact", screen.resolvedStyle.width < 1750f);

        if (Time.unscaledTime >= nextRefresh)
        {
            nextRefresh = Time.unscaledTime + 0.15f;
            RefreshResults();
        }
    }

    private void ShowResults()
    {
        Winner = raceManager.Winner;
        if (Winner == null) return;
        IsVisible = true;
        shownAt = Time.unscaledTime;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        foreach (var hud in raceHUDs)
        {
            if (hud == null || !hud.enabled) continue;
            hud.enabled = false;
            hiddenHUDs.Add(hud);
        }
        screen.style.display = DisplayStyle.Flex;
        screen.style.opacity = 0f;
        screen.Q<Label>("lap-count").text = lapManager.TotalLaps + " LAP";
        finishCamera.Present(Winner.transform);
        RefreshResults();
    }

    private void RefreshResults()
    {
        var ranking = finalRanking ?? raceManager.GetRanking();
        bool allFinished = ranking.TrueForAll(r => r.FinishRecorded && !didNotFinish.Contains(r));
        if (finalRanking == null && (allFinished || Time.unscaledTime - shownAt >= remainingRacersTimeout))
        {
            finalRanking = ranking;
            foreach (var racer in ranking)
            {
                if (!racer.FinishRecorded) didNotFinish.Add(racer);
                if (!cruising.ContainsKey(racer)) StartCruising(racer);
            }
        }

        winnerName.text = Winner == player ? "KAMU JUARA!" : "JUARA: " + Winner.DisplayName.ToUpperInvariant();
        int playerIndex = ranking.IndexOf(player);
        playerPlace.text = playerIndex >= 0 ? "POSISIMU  " + (playerIndex + 1) + " / " + ranking.Count : "FINISH";
        totalTime.text = FormatTime(player.FinishedAt - RaceCountdownController.StartedAt);
        bestLap.text = FormatTime(lapManager.BestLapSeconds);
        int finishedCount = 0;
        for (int i = 0; i < ranking.Count; i++)
        {
            if (i >= rows.Count) rows.Add(CreateRow());
            var racer = ranking[i];
            var row = rows[i];
            row.Root.style.display = DisplayStyle.Flex;
            row.Root.EnableInClassList("racer-row--player", racer == player);
            row.Root.EnableInClassList("racer-row--winner", racer == Winner);
            row.Position.text = (i + 1).ToString("00");
            row.Name.text = racer == player ? "KAMU" : racer.DisplayName;
            row.Root.tooltip = racer.DisplayName + (racer == Winner ? " - Juara pertama" : string.Empty);
            if (didNotFinish.Contains(racer)) row.Time.text = "DNF";
            else if (racer.FinishRecorded)
            {
                finishedCount++;
                row.Time.text = FormatTime(racer.FinishedAt - RaceCountdownController.StartedAt);
            }
            else row.Time.text = ResultsFinal ? "DNF" : "BALAP";
            row.Paint.style.backgroundColor = VehiclePaint(racer);
        }
        for (int i = ranking.Count; i < rows.Count; i++) rows[i].Root.style.display = DisplayStyle.None;
        if (!loading)
        {
            int remaining = Mathf.Max(0, Mathf.CeilToInt(remainingRacersTimeout - (Time.unscaledTime - shownAt)));
            raceStatus.text = ResultsFinal
                ? (allFinished ? "Semua pembalap sudah finish." : "Waktu habis. Pembalap tersisa: DNF.")
                : "Finish " + finishedCount + " / " + ranking.Count + "  |  Menunggu " + remaining + " dtk";
        }
    }

    private ResultRow CreateRow()
    {
        var row = new ResultRow { Root = new VisualElement(), Paint = new VisualElement(),
            Position = new Label(), Name = new Label(), Time = new Label() };
        row.Root.AddToClassList("racer-row");
        row.Position.AddToClassList("racer-position");
        row.Paint.AddToClassList("racer-paint");
        row.Name.AddToClassList("racer-name");
        row.Time.AddToClassList("racer-time");
        row.Root.Add(row.Position); row.Root.Add(row.Paint); row.Root.Add(row.Name); row.Root.Add(row.Time);
        rankingList.Add(row.Root);
        return row;
    }

    private static Color VehiclePaint(RaceProgressTracker racer)
    {
        foreach (var renderer in racer.GetComponentsInChildren<Renderer>())
            foreach (var material in renderer.sharedMaterials)
                if (material != null && (material.name.StartsWith("AI Kart", StringComparison.Ordinal) ||
                                         material.name.StartsWith("PartyCartBodyPaint", StringComparison.Ordinal)))
                    return material.HasProperty("_BaseColor") ? material.GetColor("_BaseColor") : material.color;
        return new Color(0.07f, 0.82f, 0.93f);
    }

    public static string FormatTime(double seconds)
    {
        if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d) return "--:--.--";
        long cs = (long)(seconds * 100d);
        return (cs / 6000).ToString("00") + ":" + (cs / 100 % 60).ToString("00") + "." + (cs % 100).ToString("00");
    }

    private void StartCruising(RaceProgressTracker racer)
    {
        var cruise = racer.GetComponent<RaceFinishCruise>();
        if (cruise == null) cruise = racer.gameObject.AddComponent<RaceFinishCruise>();
        cruise.Begin(racer);
        cruising.Add(racer, cruise);
    }

    private void Replay()
    {
        if (loading) return;
        var scene = SceneManager.GetActiveScene();
        LoadScene(scene.path);
    }

    private void ReturnToMenu() => LoadScene(mainMenuScene);

    private void LoadScene(string sceneName)
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            raceStatus.text = "Scene belum tersedia di Build Settings.";
            return;
        }
        loading = true;
        replayButton.SetEnabled(false);
        menuButton.SetEnabled(false);
        raceStatus.text = "Memuat...";
        Time.timeScale = 1f;
        SceneManager.LoadSceneAsync(sceneName);
    }

    private void OnDisable()
    {
        if (replayButton != null) replayButton.clicked -= Replay;
        if (menuButton != null) menuButton.clicked -= ReturnToMenu;
        if (finishCamera != null) finishCamera.Restore();
        foreach (var hud in hiddenHUDs) if (hud != null) hud.enabled = true;
        hiddenHUDs.Clear();
        foreach (var cruise in cruising.Values)
        {
            if (cruise == null) continue;
            cruise.StopCruising();
            Destroy(cruise);
        }
        cruising.Clear();
        if (IsVisible)
        {
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
        }
        if (screen != null) screen.style.display = DisplayStyle.None;
        IsVisible = false;
    }
}
