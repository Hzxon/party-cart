using UnityEngine;

public class LapManager : MonoBehaviour
{
    [Header("Race Settings")]
    [SerializeField] private int totalLaps = 2;
    [SerializeField] private int totalCheckpoints = 7;
    [SerializeField] private Rigidbody playerCar;

    private int currentLap = 1;
    private int nextCheckpoint = 1;
    private bool raceFinished;

    public int CurrentLap => currentLap;
    public int NextCheckpoint => nextCheckpoint;
    public bool RaceFinished => raceFinished;

    public void PlayerPassedCheckpoint(int checkpointIndex, Rigidbody car)
    {
        if (raceFinished || car != playerCar || checkpointIndex != nextCheckpoint)
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

        if (currentLap >= totalLaps)
        {
            raceFinished = true;
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
