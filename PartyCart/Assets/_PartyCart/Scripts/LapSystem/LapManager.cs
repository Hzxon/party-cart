using UnityEngine;

public class LapManager : MonoBehaviour
{
    [Header("Race Settings")]
    [SerializeField] private int totalLaps = 1;
    [SerializeField] private int totalCheckpoints = 7;
    [SerializeField] private Rigidbody playerCar;

    private int currentLap = 1;
    private int nextCheckpoint = 1;
    private bool raceFinished;
    private double lapStartedAt = double.NaN;

    public int CurrentLap => currentLap;
    public int TotalLaps => totalLaps;
    public int NextCheckpoint => nextCheckpoint;
    public bool RaceFinished => raceFinished;
    public double FinishedAt { get; private set; }
    public Rigidbody PlayerCar => playerCar;
    public double BestLapSeconds { get; private set; } = double.NaN;
    public double LastLapSeconds { get; private set; } = double.NaN;

    public void PlayerPassedCheckpoint(int checkpointIndex, Rigidbody car)
    {
        if (raceFinished || !RaceCountdownController.RaceStarted || car != playerCar || checkpointIndex != nextCheckpoint)
        {
            return;
        }

        // CP 1..7 must be crossed before the start/finish line counts a lap.
        if (checkpointIndex > 0)
        {
            Debug.Log("Lap " + currentLap + "/" + totalLaps + " - Checkpoint " + checkpointIndex + "/" + totalCheckpoints + " dilewati.");
            nextCheckpoint = checkpointIndex == totalCheckpoints ? 0 : checkpointIndex + 1;
            return;
        }

        double start = double.IsNaN(lapStartedAt) ? RaceCountdownController.StartedAt : lapStartedAt;
        if (!double.IsNaN(start))
        {
            LastLapSeconds = System.Math.Max(0d, Time.timeAsDouble - start);
            if (double.IsNaN(BestLapSeconds) || LastLapSeconds < BestLapSeconds)
                BestLapSeconds = LastLapSeconds;
        }
        lapStartedAt = Time.timeAsDouble;

        if (currentLap >= totalLaps)
        {
            raceFinished = true;
            FinishedAt = Time.timeAsDouble;
            playerCar.GetComponent<RaceProgressTracker>()?.RecordFinish();
            Debug.Log("Balapan selesai! " + totalLaps + " lap lengkap.");
        }
        else
        {
            currentLap++;
            nextCheckpoint = 1;
            Debug.Log("Lap " + currentLap + "/" + totalLaps + " dimulai.");
        }
    }

}
