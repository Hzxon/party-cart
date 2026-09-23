using UnityEngine;

public class LapManager : MonoBehaviour
{
    [Header("Race Settings")]
    [SerializeField] private int totalLaps = 3;
    [SerializeField] private int totalCheckpoints = 4;

    private int currentLap = 1;
    private int nextCheckpoint = 0;
    private bool raceFinished = false;

    public void PlayerPassedCheckpoint(int checkpointIndex)
    {
        if (raceFinished)
        {
            return;
        }

        // Hanya checkpoint yang sesuai urutan yang diterima
        if (checkpointIndex != nextCheckpoint)
        {
            Debug.Log("Checkpoint belum sesuai urutan.");
            return;
        }

        Debug.Log("Checkpoint " + checkpointIndex + " berhasil dilewati.");

        // Jika ini checkpoint terakhir
        if (checkpointIndex == totalCheckpoints - 1)
        {
            nextCheckpoint = 0;

            if (currentLap >= totalLaps)
            {
                FinishRace();
            }
            else
            {
                currentLap++;
                Debug.Log("Lap sekarang: " + currentLap);
            }
        }
        else
        {
            nextCheckpoint++;
        }
    }

    private void FinishRace()
    {
        raceFinished = true;

        Debug.Log("Balapan selesai!");
        Debug.Log("Player menyelesaikan " + totalLaps + " lap.");
    }
}