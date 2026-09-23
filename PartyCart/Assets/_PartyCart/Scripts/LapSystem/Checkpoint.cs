using UnityEngine;

public class Checkpoint : MonoBehaviour
{
    [Header("Checkpoint Settings")]
    public int checkpointIndex;

    [SerializeField] private LapManager lapManager;

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
        {
            return;
        }

        lapManager.PlayerPassedCheckpoint(checkpointIndex);
    }
}