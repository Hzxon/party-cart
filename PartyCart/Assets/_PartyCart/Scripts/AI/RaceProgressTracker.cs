using UnityEngine;

// Pasang komponen ini di SEMUA racer: player maupun AI.
// Tugasnya cuma satu: tau posisi progress racer ini ada di titik mana di track.
// AIController baca ini buat tau target waypoint & buat rubber-banding.
public class RaceProgressTracker : MonoBehaviour
{
    [Header("Progress")]
    public int currentWaypointIndex = 0;
    public int lapCount = 0;
    public int totalLaps = 1;
    public LapManager playerLapManager;
    public bool HasFinished => playerLapManager != null ? playerLapManager.RaceFinished : lapCount >= totalLaps;
    public double FinishedAt { get; private set; } = double.NaN;
    public bool FinishRecorded => !double.IsNaN(FinishedAt);
    public int FinishOrder { get; private set; } = int.MaxValue;

    [Tooltip("Optional name on the results sheet. Empty uses the vehicle's actual name.")]
    [SerializeField] private string displayName;
    private string raceDisplayName;
    public string DisplayName => !string.IsNullOrWhiteSpace(displayName) ? displayName :
        (!string.IsNullOrWhiteSpace(raceDisplayName) ? raceDisplayName : gameObject.name);
    internal bool NeedsRaceName => playerLapManager == null && string.IsNullOrWhiteSpace(displayName) &&
        string.IsNullOrWhiteSpace(raceDisplayName);

    internal void AssignRaceName(string value)
    {
        if (NeedsRaceName) raceDisplayName = value;
    }

    [Header("Detection")]
    public float waypointReachDistance = 3f;
    private bool crossedStart;
    private int waypointsSinceStart;

    private void Start()
    {
        // All competitors race the same distance as the player's checkpoint system.
        var laps = playerLapManager != null ? playerLapManager : FindAnyObjectByType<LapManager>();
        if (laps != null) totalLaps = laps.TotalLaps;
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
        if (HasFinished)
        {
            RecordFinish();
            return;
        }
        var manager = WaypointManager.Instance;
        if (manager == null || manager.waypoints.Count < 3 || HasFinished || !RaceCountdownController.RaceStarted) return;

        // Test the target plane as well as its radius so fast cars cannot miss a node.
        for (int advanced = 0; advanced < 8; advanced++)
        {
            var target = manager.GetWaypoint(currentWaypointIndex);
            var previous = manager.GetWaypoint(currentWaypointIndex - 1);
            Vector3 offset = Vector3.ProjectOnPlane(transform.position - target.transform.position, Vector3.up);
            Vector3 direction = Vector3.ProjectOnPlane(target.transform.position - previous.transform.position, Vector3.up).normalized;
            bool near = offset.magnitude <= waypointReachDistance;
            bool passed = Vector3.Dot(offset, direction) >= 0f && offset.magnitude <= waypointReachDistance * 2f;
            // Starting and completing a lap both require crossing the line, not entering its radius.
            if (currentWaypointIndex == 0 && !passed) break;
            if (!near && !passed) break;

            if (currentWaypointIndex == 0)
            {
                if (crossedStart && waypointsSinceStart >= manager.waypoints.Count - 1) lapCount++;
                crossedStart = true;
                waypointsSinceStart = 0;
            }
            else if (crossedStart) waypointsSinceStart++;
            currentWaypointIndex = (currentWaypointIndex + 1) % manager.waypoints.Count;
            if (HasFinished)
            {
                RecordFinish();
                break;
            }
        }
    }

    public void RecordFinish()
    {
        if (FinishRecorded || !HasFinished || !RaceCountdownController.RaceStarted) return;
        FinishedAt = playerLapManager != null ? playerLapManager.FinishedAt : Time.timeAsDouble;
        if (RaceManager.Instance != null) FinishOrder = RaceManager.Instance.RecordFinishOrder(this);
    }

    // Makin besar = makin depan. Dipakai buat rubber-banding & posisi race (1st, 2nd, dst).
    public float GetProgress()
    {
        var manager = WaypointManager.Instance;
        if (manager == null || manager.waypoints.Count == 0) return 0f;
        if (HasFinished) return totalLaps * manager.waypoints.Count;
        Vector3 position = transform.position;
        position.y = 0f;
        if (!crossedStart)
        {
            // Grid lanes must not change rank: measure along the road, not distance to WP 0.
            Vector3 start = manager.GetWaypoint(0).transform.position;
            Vector3 direction = manager.GetWaypoint(1).transform.position - start;
            start.y = direction.y = 0f;
            return Vector3.Dot(position - start, direction) / Mathf.Max(direction.sqrMagnitude, 0.001f);
        }

        int count = manager.waypoints.Count;
        int target = ((currentWaypointIndex % count) + count) % count;
        // WP 0 at the end of a lap represents count, not the beginning of that lap.
        int targetStep = target == 0 ? count : target;
        float nearestDistance = float.PositiveInfinity;
        float routeProgress = targetStep - 1;
        int behind = Mathf.Min(8, count - 1);
        int ahead = Mathf.Min(2, count - behind);
        for (int offset = -behind; offset < ahead; offset++)
        {
            int step = targetStep + offset;
            Vector3 from = manager.GetWaypoint(step).transform.position;
            Vector3 to = manager.GetWaypoint(step + 1).transform.position;
            from.y = to.y = 0f;
            Vector3 segment = to - from;
            if (segment.sqrMagnitude < 0.001f) continue;
            float fraction = Mathf.Clamp01(Vector3.Dot(position - from, segment) / segment.sqrMagnitude);
            float distance = (position - (from + segment * fraction)).sqrMagnitude;
            if (distance >= nearestDistance) continue;
            nearestDistance = distance;
            routeProgress = step + fraction;
        }

        // Waypoint radii are only navigation hints. Project onto nearby road segments so
        // the player's 8m radius cannot award more progress than an AI's 5.5m radius.
        // Stay local to the validated route index; never jump to another nearby track branch.
        // Use the same route lap counter for every car, avoiding checkpoint/waypoint wrap offsets.
        return lapCount * count + routeProgress;
    }
}
