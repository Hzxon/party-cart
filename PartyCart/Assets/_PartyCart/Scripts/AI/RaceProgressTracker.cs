using UnityEngine;

// Pasang komponen ini di SEMUA racer: player maupun AI.
// Tugasnya cuma satu: tau posisi progress racer ini ada di titik mana di track.
// AIController baca ini buat tau target waypoint & buat rubber-banding.
public class RaceProgressTracker : MonoBehaviour
{
    [Header("Progress")]
    public int currentWaypointIndex = 0;
    public int lapCount = 0;

    [Header("Detection")]
    public float waypointReachDistance = 3f;

    private void Start()
    {
        if (RaceManager.Instance != null)
            RaceManager.Instance.Register(this);
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance != null)
            RaceManager.Instance.Unregister(this);
    }

    private void Update()
    {
        if (WaypointManager.Instance == null) return;

        Waypoint target = WaypointManager.Instance.GetWaypoint(currentWaypointIndex);
        if (target == null) return;

        float dist = Vector3.Distance(transform.position, target.transform.position);
        if (dist <= waypointReachDistance)
        {
            currentWaypointIndex++;

            if (currentWaypointIndex >= WaypointManager.Instance.waypoints.Count)
            {
                currentWaypointIndex = 0;
                lapCount++;
            }
        }
    }

    // Makin besar = makin depan. Dipakai buat rubber-banding & posisi race (1st, 2nd, dst).
    public float GetProgress()
    {
        if (WaypointManager.Instance == null) return 0f;
        return lapCount * WaypointManager.Instance.waypoints.Count + currentWaypointIndex;
    }
}
