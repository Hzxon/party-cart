using TMPro;
using UnityEngine;

public sealed class RaceTimerHUDController : MonoBehaviour
{
    [SerializeField] private LapManager lapManager;
    [SerializeField] private TMP_Text timerText;
    private long displayedCentiseconds = -1;

    public double ElapsedSeconds { get; private set; }

    public void Configure(LapManager manager, TMP_Text label)
    {
        lapManager = manager;
        timerText = label;
    }

    private void Update()
    {
        if (timerText == null || lapManager == null) return;
        double start = RaceCountdownController.StartedAt;
        double end = lapManager.RaceFinished ? lapManager.FinishedAt : Time.timeAsDouble;
        ElapsedSeconds = RaceCountdownController.RaceStarted && !double.IsNaN(start)
            ? System.Math.Max(0d, end - start) : 0d;
        long centiseconds = (long)(ElapsedSeconds * 100d);
        if (centiseconds == displayedCentiseconds) return;
        displayedCentiseconds = centiseconds;
        timerText.SetText("<size=44%>TIME</size>\n" + (centiseconds / 6000).ToString("00") + ":" +
            (centiseconds / 100 % 60).ToString("00") + "." + (centiseconds % 100).ToString("00"));
    }
}
