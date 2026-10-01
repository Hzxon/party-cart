using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Checkpoint : MonoBehaviour
{
    [Tooltip("0 = Start/Finish, 1-7 = checkpoints in race order.")]
    public int checkpointIndex;

    [SerializeField] private LapManager lapManager;

    private void OnTriggerEnter(Collider other)
    {
        if (lapManager == null || other.attachedRigidbody == null)
        {
            return;
        }

        lapManager.PlayerPassedCheckpoint(checkpointIndex, other.attachedRigidbody);
    }

    private void OnDrawGizmos()
    {
        var box = GetComponent<BoxCollider>();
        if (box == null)
        {
            return;
        }

        Gizmos.color = checkpointIndex == 0 ? Color.green : Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(box.center, box.size);
    }
}
